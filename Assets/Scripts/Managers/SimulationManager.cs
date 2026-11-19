using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

public class SimulationManager : MonoBehaviour
{
    public static SimulationManager Instance { get; private set; }

    public const float MinCoupling = 0f, MaxCoupling = 10f;
    public const float MinVisionRadius = 0.5f, MaxVisionRadius = 25f;
    public const int MinPopulation = 1, MaxPopulation = 1000;
    public const float MinBaseFrequency = 0.1f, MaxBaseFrequency = 5f;
    public const float MinFrequencySpread = 0f, MaxFrequencySpread = 1.5f;
    public const float MinMoveSpeed = 0f, MaxMoveSpeed = 10f;
    public const float MinWanderStrength = 0f, MaxWanderStrength = 3f;
    public const float MinBoundsRadius = 2f, MaxBoundsRadius = 50f;
    public const float MinSimSpeed = 0f, MaxSimSpeed = 3f;

    private const int MaxInstancesPerBatch = 1023;
    private const int EmissionLevels = 32; 

    [Header("References")]
    public FireflySpawner spawner;

    [Header("Kuramoto Parameters")]
    [Range(MinCoupling, MaxCoupling)] public float couplingStrength = 2.0f;
    [Range(MinVisionRadius, MaxVisionRadius)] public float visionRadius = 4.0f;
    [Range(MinBaseFrequency, MaxBaseFrequency)] public float baseFrequency = 1.5f;
    [Range(MinFrequencySpread, MaxFrequencySpread)] public float frequencySpread = 0.2f;

    [Header("Population & Space")]
    [Range(MinPopulation, MaxPopulation)] public int populationSize = 100;
    [Range(MinBoundsRadius, MaxBoundsRadius)] public float boundsRadius = 10f;

    [Header("Movement")]
    [Range(MinMoveSpeed, MaxMoveSpeed)] public float moveSpeed = 2.0f;
    [Range(MinWanderStrength, MaxWanderStrength)] public float wanderStrength = 0.5f;

    [Header("Visuals (Global)")]
    public Color flashColor = new Color(0.6f, 1.0f, 0.2f, 1f);
    [Range(0.5f, 10f)] public float flashIntensity = 4.0f;
    [Range(0.5f, 10f)] public float fadeSpeed = 3.0f;

    [Header("Instanced Rendering (performance)")]
    [Tooltip("Draw all fireflies with a few instanced draw calls. Meshes/materials/transforms are read from the spawner's prefab automatically.")]
    public bool useInstancedRendering = true;

    [Header("Level of Detail")]
    [Tooltip("Near fireflies use the prefab's full model, distant ones use the low-detail mesh.")]
    public bool useLod = true;
    [Tooltip("Fireflies closer than this distance to the camera use the full model.")]
    [Min(0f)] public float lodDistance = 50;
    [Tooltip("Safety cap: at most this many fireflies are drawn with the full model per frame. Lower it if FPS drops when the camera is close.")]
    [Min(0)] public int maxHighDetailCount = 500;
    [Tooltip("Low-detail mesh used for distant fireflies (and for all of them if LOD is off). Empty = Unity's built-in Sphere.")]
    public Mesh meshOverride;
    [Tooltip("Scale of the low-detail mesh. Match it to the size of your full model.")]
    public Vector3 overrideScale = new Vector3(0.3f, 0.3f, 0.3f);

    [Header("Time Control")]
    [Range(MinSimSpeed, MaxSimSpeed)] public float simulationSpeed = 1.0f;
    public bool isPaused = false;

    public float OrderParameter { get; private set; }
    public readonly List<Firefly> allFireflies = new List<Firefly>();
    public event Action OnParametersChanged;

    private float simTime = 0f;

    private const int MaxGridCells = 250000;
    private const int ParallelThreshold = 300;

    private float[] px = new float[0], py = new float[0], pz = new float[0];
    private float[] sp = new float[0], cp = new float[0], corr = new float[0];
    private int[] sortedOrder = new int[0], sortedCell = new int[0], cellOf = new int[0];
    private int[] cellStart = new int[1], cellCursor = new int[1];
    private int gridX, gridY, gridZ;
    private float workR2, workCoupling;
    private Vector3 swarmMin, swarmMax;
    private Action<int> couplingWorker;

    private class RenderPart
    {
        public Mesh mesh;
        public Matrix4x4 offset;          
        public Material[] materials;     
        public Material[][] glowMaterials;
    }

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private readonly List<RenderPart> highParts = new List<RenderPart>(); // full model (near)
    private readonly List<RenderPart> lowParts = new List<RenderPart>();  // low-detail mesh (far)
    private readonly Matrix4x4[] matrixBatch = new Matrix4x4[MaxInstancesPerBatch];
    private readonly int[] levelStart = new int[EmissionLevels + 1];
    private readonly int[] levelFill = new int[EmissionLevels];
    private int[] levelOf = new int[0];
    private int[] sortedIdx = new int[0];
    private int[] nearIdx = new int[0];
    private int[] farIdx = new int[0];
    private int[] candIdx = new int[0];
    private float[] candDist = new float[0];

    private Camera cachedCam;
    private bool partsDirty = true;
    private bool emissionDirty = true;
    private bool warnedNoParts = false;
    private Color lastFlashColor;
    private float lastFlashIntensity;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (spawner == null)
        {
            spawner = FindAnyObjectByType<FireflySpawner>();
        }
    }

    private void OnDestroy()
    {
        ClearParts(highParts);
        ClearParts(lowParts);
    }

    private void OnValidate()
    {
        ClampAllParameters();
        partsDirty = true;

        if (Application.isPlaying) pendingSync = true;
    }

    private bool pendingSync = false;

    private void Update()
    {
        if (pendingSync)
        {
            pendingSync = false;
            if (spawner != null)
            {
                spawner.SyncPopulation(populationSize);
                OnParametersChanged?.Invoke();
            }
        }

        float dt = isPaused ? 0f : Time.deltaTime * simulationSpeed;
        simTime += dt;

        int count = allFireflies.Count;
        if (count == 0)
        {
            OrderParameter = 0f;
            return;
        }

        bool instanced = false;
        if (useInstancedRendering)
        {
            if (partsDirty) BuildParts();
            instanced = highParts.Count + lowParts.Count > 0;

            if (!instanced && !warnedNoParts)
            {
                warnedNoParts = true;
                Debug.LogWarning("SimulationManager: instanced rendering could not be set up (prefab has no MeshFilter/MeshRenderer). Using per-object rendering.", this);
            }
        }

        Vector3 boundsCenter = spawner != null ? spawner.transform.position : transform.position;

        swarmMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        swarmMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        for (int i = 0; i < count; i++)
        {
            Firefly f = allFireflies[i];
            f.SetOwnRendering(!instanced);
            f.CacheState();

            swarmMin = Vector3.Min(swarmMin, f.cachedPos);
            swarmMax = Vector3.Max(swarmMax, f.cachedPos);
        }

        if (dt > 0f)
        {
            ComputeCoupling(count);
        }

        // 3. Update phases, emission, movement and the order parameter r
        float sumCos = 0f;
        float sumSin = 0f;

        for (int i = 0; i < count; i++)
        {
            Firefly f = allFireflies[i];
            if (dt > 0f)
            {
                f.UpdatePhaseAndVisuals(dt, baseFrequency, frequencySpread, flashColor, flashIntensity, fadeSpeed);
                f.MoveOrganically(dt, simTime, moveSpeed, wanderStrength, boundsCenter, boundsRadius);
            }

            sumCos += Mathf.Cos(f.phase);
            sumSin += Mathf.Sin(f.phase);
        }

        OrderParameter = Mathf.Sqrt(sumCos * sumCos + sumSin * sumSin) / count;

        if (instanced)
        {
            RenderInstanced(count, boundsCenter);
        }
    }

    // ------------------------------------------------------------------
    // Instanced rendering
    // ------------------------------------------------------------------

    private static void ClearParts(List<RenderPart> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            RenderPart p = list[i];
            for (int s = 0; s < p.materials.Length; s++)
            {
                if (p.materials[s] != null) Destroy(p.materials[s]);
                if (p.glowMaterials[s] != null)
                {
                    for (int l = 0; l < p.glowMaterials[s].Length; l++)
                    {
                        if (p.glowMaterials[s][l] != null) Destroy(p.glowMaterials[s][l]);
                    }
                }
            }
        }
        list.Clear();
    }

    private static Matrix4x4 LocalToRoot(Transform t, Transform root)
    {
        Matrix4x4 m = Matrix4x4.identity;
        while (t != null && t != root)
        {
            m = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale) * m;
            t = t.parent;
        }
        return m;
    }

    private static Material MakeInstancedCopy(Material src, bool emissive)
    {
        var copy = new Material(src);
        copy.enableInstancing = true;
        if (emissive) copy.EnableKeyword("_EMISSION");
        return copy;
    }

    private static Material[] MakeLevelMaterials(Material src)
    {
        var arr = new Material[EmissionLevels];
        for (int l = 0; l < EmissionLevels; l++)
        {
            arr[l] = MakeInstancedCopy(src, true);
        }
        return arr;
    }

    private void BuildParts()
    {
        partsDirty = false;
        emissionDirty = true;
        warnedNoParts = false;
        ClearParts(highParts);
        ClearParts(lowParts);

        if (spawner == null || spawner.fireflyPrefab == null) return;

        GameObject prefab = spawner.fireflyPrefab;
        Transform root = prefab.transform;
        int glowIdx = 0;
        if (prefab.TryGetComponent(out Firefly prefabFirefly)) glowIdx = prefabFirefly.glowMaterialIndex;

        var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
        MeshRenderer glowRenderer = renderers.Length > 0 ? renderers[0] : null;

        Mesh lowMesh = meshOverride != null ? meshOverride : Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
        if (glowRenderer != null && lowMesh != null)
        {
            Material[] src = glowRenderer.sharedMaterials;
            if (src.Length > 0)
            {
                Material glowSrc = src[Mathf.Clamp(glowIdx, 0, src.Length - 1)];
                if (glowSrc != null)
                {
                    int subs = lowMesh.subMeshCount;
                    var part = new RenderPart
                    {
                        mesh = lowMesh,
                        offset = Matrix4x4.Scale(overrideScale),
                        materials = new Material[subs],
                        glowMaterials = new Material[subs][]
                    };
                    for (int s = 0; s < subs; s++)
                    {
                        part.glowMaterials[s] = MakeLevelMaterials(glowSrc);
                    }
                    lowParts.Add(part);
                }
            }
        }

        if (!useLod && lowParts.Count > 0) return;

        for (int r = 0; r < renderers.Length; r++)
        {
            MeshRenderer mr = renderers[r];
            if (!mr.TryGetComponent(out MeshFilter mf) || mf.sharedMesh == null) continue;

            Mesh mesh = mf.sharedMesh;
            Material[] src = mr.sharedMaterials;
            int subCount = Mathf.Min(mesh.subMeshCount, src.Length);
            if (subCount == 0) continue;

            bool isGlowPart = mr == glowRenderer;
            var part = new RenderPart
            {
                mesh = mesh,
                offset = Matrix4x4.Scale(root.localScale) * LocalToRoot(mr.transform, root),
                materials = new Material[subCount],
                glowMaterials = new Material[subCount][]
            };

            for (int s = 0; s < subCount; s++)
            {
                if (src[s] == null) continue;

                if (isGlowPart && s == glowIdx)
                    part.glowMaterials[s] = MakeLevelMaterials(src[s]);
                else
                    part.materials[s] = MakeInstancedCopy(src[s], false);
            }
            highParts.Add(part);
        }
    }

    private void UpdateEmissionLevels()
    {
        if (!emissionDirty && flashColor == lastFlashColor && Mathf.Approximately(flashIntensity, lastFlashIntensity))
            return;

        emissionDirty = false;
        lastFlashColor = flashColor;
        lastFlashIntensity = flashIntensity;

        Color peak = flashColor * flashIntensity;
        ApplyEmission(highParts, peak);
        ApplyEmission(lowParts, peak);
    }

    private static void ApplyEmission(List<RenderPart> list, Color peak)
    {
        for (int p = 0; p < list.Count; p++)
        {
            var glow = list[p].glowMaterials;
            for (int s = 0; s < glow.Length; s++)
            {
                if (glow[s] == null) continue;
                for (int l = 0; l < EmissionLevels; l++)
                {
                    glow[s][l].SetColor(EmissionColorId, peak * (l / (float)(EmissionLevels - 1)));
                }
            }
        }
    }

    private bool TryGetCameraPosition(out Vector3 pos)
    {
        if (cachedCam == null)
        {
            cachedCam = Camera.main;
            if (cachedCam == null) cachedCam = FindAnyObjectByType<Camera>();
        }

        if (cachedCam != null)
        {
            pos = cachedCam.transform.position;
            return true;
        }
        pos = Vector3.zero;
        return false;
    }

    private void EnsureCapacity(int count)
    {
        if (levelOf.Length >= count) return;
        levelOf = new int[count];
        sortedIdx = new int[count];
        nearIdx = new int[count];
        farIdx = new int[count];
        candIdx = new int[count];
        candDist = new float[count];
    }

    private void RenderInstanced(int count, Vector3 boundsCenter)
    {
        UpdateEmissionLevels();
        EnsureCapacity(count);

        bool haveHigh = highParts.Count > 0;
        bool haveLow = lowParts.Count > 0;
        bool haveCam = TryGetCameraPosition(out Vector3 camPos);
        bool allHigh = haveHigh && !haveLow;
        bool lod = haveHigh && haveLow && haveCam;
        float lodSqr = lodDistance * lodDistance;

        int nearN = 0, farN = 0, candN = 0;
        for (int i = 0; i < count; i++)
        {
            if (allHigh)
            {
                nearIdx[nearN++] = i;
            }
            else if (lod)
            {
                float d = (allFireflies[i].cachedPos - camPos).sqrMagnitude;
                if (d <= lodSqr)
                {
                    candIdx[candN] = i;
                    candDist[candN] = d;
                    candN++;
                }
                else
                {
                    farIdx[farN++] = i;
                }
            }
            else
            {
                farIdx[farN++] = i;
            }
        }

        if (lod)
        {
            int keep = candN;
            if (candN > maxHighDetailCount)
            {
                Array.Sort(candDist, candIdx, 0, candN);
                keep = maxHighDetailCount;
            }
            for (int j = 0; j < keep; j++) nearIdx[nearN++] = candIdx[j];
            for (int j = keep; j < candN; j++) farIdx[farN++] = candIdx[j];
        }

        float extent = boundsRadius * 4f + 20f;
        Bounds bounds = new Bounds(boundsCenter, new Vector3(extent, extent, extent));

        RenderGroup(highParts, nearIdx, nearN, bounds);
        RenderGroup(lowParts, farIdx, farN, bounds);
    }

    private void RenderGroup(List<RenderPart> group, int[] indices, int n, Bounds bounds)
    {
        if (n == 0 || group.Count == 0) return;

        Array.Clear(levelStart, 0, levelStart.Length);
        float peakBrightness = Mathf.Max(0.0001f, flashColor.maxColorComponent * flashIntensity);
        for (int j = 0; j < n; j++)
        {
            float b = allFireflies[indices[j]].CurrentColor.maxColorComponent / peakBrightness;
            int lvl = Mathf.Clamp(Mathf.RoundToInt(b * (EmissionLevels - 1)), 0, EmissionLevels - 1);
            levelOf[j] = lvl;
            levelStart[lvl + 1]++;
        }
        for (int l = 0; l < EmissionLevels; l++)
        {
            levelStart[l + 1] += levelStart[l];
            levelFill[l] = levelStart[l];
        }
        for (int j = 0; j < n; j++)
        {
            sortedIdx[levelFill[levelOf[j]]++] = indices[j];
        }

        for (int p = 0; p < group.Count; p++)
        {
            RenderPart part = group[p];
            for (int s = 0; s < part.materials.Length; s++)
            {
                if (part.glowMaterials[s] != null)
                {
                    for (int l = 0; l < EmissionLevels; l++)
                    {
                        int from = levelStart[l];
                        int to = levelStart[l + 1];
                        if (to > from) DrawBatches(part, s, part.glowMaterials[s][l], sortedIdx, from, to, bounds);
                    }
                }
                else if (part.materials[s] != null)
                {
                    DrawBatches(part, s, part.materials[s], indices, 0, n, bounds);
                }
            }
        }
    }

    private void DrawBatches(RenderPart part, int submesh, Material mat, int[] order, int from, int to, Bounds bounds)
    {
        var rp = new RenderParams(mat)
        {
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            worldBounds = bounds
        };

        for (int start = from; start < to; start += MaxInstancesPerBatch)
        {
            int n = Mathf.Min(MaxInstancesPerBatch, to - start);
            for (int k = 0; k < n; k++)
            {
                Matrix4x4 m = part.offset;
                Vector3 pos = allFireflies[order[start + k]].cachedPos;
                m.m03 += pos.x;
                m.m13 += pos.y;
                m.m23 += pos.z;
                matrixBatch[k] = m;
            }
            Graphics.RenderMeshInstanced(rp, part.mesh, submesh, matrixBatch, n);
        }
    }

    // ------------------------------------------------------------------
    // Spatial grid
    // ------------------------------------------------------------------

    private void ComputeCoupling(int count)
    {
        if (px.Length < count)
        {
            px = new float[count]; py = new float[count]; pz = new float[count];
            sp = new float[count]; cp = new float[count]; corr = new float[count];
            sortedOrder = new int[count]; sortedCell = new int[count]; cellOf = new int[count];
        }

        float r = visionRadius;
        Vector3 size = swarmMax - swarmMin;

        if (size.sqrMagnitude <= r * r)
        {
            float sumS = 0f, sumC = 0f;
            for (int i = 0; i < count; i++)
            {
                sumS += allFireflies[i].sinPhase;
                sumC += allFireflies[i].cosPhase;
            }

            int others = count - 1;
            for (int i = 0; i < count; i++)
            {
                Firefly f = allFireflies[i];
                f.SetPhaseCorrection(others > 0
                    ? (couplingStrength / others) * (f.cosPhase * sumS - f.sinPhase * sumC)
                    : 0f);
            }
            return;
        }

        float cellSize = r;
        while (true)
        {
            gridX = (int)(size.x / cellSize) + 1;
            gridY = (int)(size.y / cellSize) + 1;
            gridZ = (int)(size.z / cellSize) + 1;
            if ((long)gridX * gridY * gridZ <= MaxGridCells) break;
            cellSize *= 1.3f;
        }

        int cells = gridX * gridY * gridZ;
        if (cellStart.Length < cells + 1)
        {
            cellStart = new int[cells + 1];
            cellCursor = new int[cells + 1];
        }
        Array.Clear(cellStart, 0, cells + 1);

        float inv = 1f / cellSize;
        for (int i = 0; i < count; i++)
        {
            Vector3 p = allFireflies[i].cachedPos;
            int cx = Mathf.Min((int)((p.x - swarmMin.x) * inv), gridX - 1);
            int cy = Mathf.Min((int)((p.y - swarmMin.y) * inv), gridY - 1);
            int cz = Mathf.Min((int)((p.z - swarmMin.z) * inv), gridZ - 1);
            int c = cx + gridX * (cy + gridY * cz);
            cellOf[i] = c;
            cellStart[c + 1]++;
        }

        for (int c = 0; c < cells; c++)
        {
            cellStart[c + 1] += cellStart[c];
        }
        Array.Copy(cellStart, cellCursor, cells);

        for (int i = 0; i < count; i++)
        {
            Firefly f = allFireflies[i];
            int c = cellOf[i];
            int s = cellCursor[c]++;
            sortedOrder[s] = i;
            sortedCell[s] = c;
            px[s] = f.cachedPos.x;
            py[s] = f.cachedPos.y;
            pz[s] = f.cachedPos.z;
            sp[s] = f.sinPhase;
            cp[s] = f.cosPhase;
        }

        workR2 = r * r;
        workCoupling = couplingStrength;

        if (count < ParallelThreshold)
        {
            for (int s = 0; s < count; s++) CouplingWorker(s);
        }
        else
        {
            if (couplingWorker == null) couplingWorker = CouplingWorker;
            Parallel.For(0, count, couplingWorker);
        }

        for (int s = 0; s < count; s++)
        {
            allFireflies[sortedOrder[s]].SetPhaseCorrection(corr[s]);
        }
    }

    private void CouplingWorker(int s)
    {
        int c = sortedCell[s];
        int cx = c % gridX;
        int t = c / gridX;
        int cy = t % gridY;
        int cz = t / gridY;

        float x = px[s], y = py[s], z = pz[s];
        float sumS = 0f, sumC = 0f;
        int n = 0;

        int x0 = cx > 0 ? cx - 1 : 0;
        int x1 = cx < gridX - 1 ? cx + 1 : gridX - 1;
        int y0 = cy > 0 ? cy - 1 : 0;
        int y1 = cy < gridY - 1 ? cy + 1 : gridY - 1;
        int z0 = cz > 0 ? cz - 1 : 0;
        int z1 = cz < gridZ - 1 ? cz + 1 : gridZ - 1;

        for (int zz = z0; zz <= z1; zz++)
        {
            for (int yy = y0; yy <= y1; yy++)
            {
                int rowBase = gridX * (yy + gridY * zz);
                int from = cellStart[rowBase + x0];
                int to = cellStart[rowBase + x1 + 1];

                for (int j = from; j < to; j++)
                {
                    float dx = px[j] - x;
                    float dy = py[j] - y;
                    float dz = pz[j] - z;
                    if (dx * dx + dy * dy + dz * dz <= workR2)
                    {
                        sumS += sp[j];
                        sumC += cp[j];
                        n++;
                    }
                }
            }
        }

        corr[s] = n > 1 ? (workCoupling / (n - 1)) * (cp[s] * sumS - sp[s] * sumC) : 0f;
    }

    private void ClampAllParameters()
    {
        couplingStrength = Mathf.Clamp(couplingStrength, MinCoupling, MaxCoupling);
        visionRadius = Mathf.Clamp(visionRadius, MinVisionRadius, MaxVisionRadius);
        populationSize = Mathf.Clamp(populationSize, MinPopulation, MaxPopulation);
        baseFrequency = Mathf.Clamp(baseFrequency, MinBaseFrequency, MaxBaseFrequency);
        frequencySpread = Mathf.Clamp(frequencySpread, MinFrequencySpread, MaxFrequencySpread);
        moveSpeed = Mathf.Clamp(moveSpeed, MinMoveSpeed, MaxMoveSpeed);
        wanderStrength = Mathf.Clamp(wanderStrength, MinWanderStrength, MaxWanderStrength);
        boundsRadius = Mathf.Clamp(boundsRadius, MinBoundsRadius, MaxBoundsRadius);
        simulationSpeed = Mathf.Clamp(simulationSpeed, MinSimSpeed, MaxSimSpeed);
    }


    public void SetCouplingStrength(float value) => couplingStrength = Mathf.Clamp(value, MinCoupling, MaxCoupling);
    public void SetVisionRadius(float value) => visionRadius = Mathf.Clamp(value, MinVisionRadius, MaxVisionRadius);
    public void SetBaseFrequency(float value) => baseFrequency = Mathf.Clamp(value, MinBaseFrequency, MaxBaseFrequency);
    public void SetFrequencySpread(float value) => frequencySpread = Mathf.Clamp(value, MinFrequencySpread, MaxFrequencySpread);
    public void SetMoveSpeed(float value) => moveSpeed = Mathf.Clamp(value, MinMoveSpeed, MaxMoveSpeed);
    public void SetWanderStrength(float value) => wanderStrength = Mathf.Clamp(value, MinWanderStrength, MaxWanderStrength);
    public void SetSimulationSpeed(float value) => simulationSpeed = Mathf.Clamp(value, MinSimSpeed, MaxSimSpeed);

    public void SetBoundsRadius(float value)
    {
        float newRadius = Mathf.Clamp(value, MinBoundsRadius, MaxBoundsRadius);
        float oldRadius = boundsRadius;
        boundsRadius = newRadius;

        if (oldRadius > 0.001f && !Mathf.Approximately(oldRadius, newRadius))
        {
            float scale = newRadius / oldRadius;
            Vector3 center = spawner != null ? spawner.transform.position : transform.position;

            for (int i = 0; i < allFireflies.Count; i++)
            {
                Vector3 offset = allFireflies[i].transform.position - center;
                allFireflies[i].transform.position = center + offset * scale;
            }
        }
    }
    public void SetPopulationSize(float value)
    {
        int clamped = Mathf.Clamp(Mathf.RoundToInt(value), MinPopulation, MaxPopulation);
        if (clamped == populationSize) return;

        populationSize = clamped;
        if (spawner != null)
        {
            spawner.SyncPopulation(populationSize);
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        OnParametersChanged?.Invoke();
    }

    public void RandomizeAllPhases()
    {
        for (int i = 0; i < allFireflies.Count; i++)
        {
            allFireflies[i].RandomizePhase();
        }
    }

    public void ResetToDefaults()
    {
        couplingStrength = 2.0f;
        visionRadius = 4.0f;
        populationSize = 100;
        baseFrequency = 1.5f;
        frequencySpread = 0.2f;
        moveSpeed = 2.0f;
        wanderStrength = 0.5f;
        SetBoundsRadius(10.0f);
        simulationSpeed = 1.0f;
        isPaused = false;

        if (spawner != null)
        {
            spawner.SyncPopulation(populationSize);
        }

        RandomizeAllPhases();
        OnParametersChanged?.Invoke();
    }
}