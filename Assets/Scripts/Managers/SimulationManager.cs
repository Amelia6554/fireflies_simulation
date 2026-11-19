using System;
using System.Collections.Generic;
using UnityEngine;

public class SimulationManager : MonoBehaviour
{
    public static SimulationManager Instance { get; private set; }

    // Bezpieczne limity 
    public const float MinCoupling = 0f, MaxCoupling = 10f;
    public const float MinVisionRadius = 0.5f, MaxVisionRadius = 25f;
    public const int MinPopulation = 1, MaxPopulation = 1000;
    public const float MinBaseFrequency = 0.1f, MaxBaseFrequency = 5f;
    public const float MinFrequencySpread = 0f, MaxFrequencySpread = 1.5f;
    public const float MinMoveSpeed = 0f, MaxMoveSpeed = 10f;
    public const float MinWanderStrength = 0f, MaxWanderStrength = 3f;
    public const float MinBoundsRadius = 2f, MaxBoundsRadius = 30f;
    public const float MinSimSpeed = 0f, MaxSimSpeed = 3f;

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

    [Header("Time Control")]
    [Range(MinSimSpeed, MaxSimSpeed)] public float simulationSpeed = 1.0f;
    public bool isPaused = false;
    public float OrderParameter { get; private set; }
    public readonly List<Firefly> allFireflies = new List<Firefly>();
    public event Action OnParametersChanged;
    private readonly Dictionary<Vector3Int, List<Firefly>> grid = new Dictionary<Vector3Int, List<Firefly>>();
    private readonly List<Firefly> neighborBuffer = new List<Firefly>(128);
    private float simTime = 0f;

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
            spawner = FindFirstObjectByType<FireflySpawner>();
        }
    }

    private void OnValidate()
    {
        ClampAllParameters();
        if (Application.isPlaying && spawner != null)
        {
            spawner.SyncPopulation(populationSize);
            OnParametersChanged?.Invoke();
        }
    }

    private void Update()
    {
        float dt = isPaused ? 0f : Time.deltaTime * simulationSpeed;
        simTime += dt;

        int count = allFireflies.Count;
        if (count == 0)
        {
            OrderParameter = 0f;
            return;
        }

        float cellSize = Mathf.Max(MinVisionRadius, visionRadius);
        float visionRadiusSqr = visionRadius * visionRadius;
        Vector3 boundsCenter = spawner != null ? spawner.transform.position : transform.position;

        // 1. Odbudowa Spatial Grid
        foreach (var cellList in grid.Values)
        {
            cellList.Clear();
        }

        for (int i = 0; i < count; i++)
        {
            Firefly f = allFireflies[i];
            Vector3Int cell = GetCellCoord(f.transform.position, cellSize);
            if (!grid.TryGetValue(cell, out List<Firefly> list))
            {
                list = new List<Firefly>(16);
                grid[cell] = list;
            }
            list.Add(f);
        }

        // 2. Krok Kuramoto - obliczenie korekt faz na podstawie stanu z pocz¹tku klatki
        if (dt > 0f)
        {
            for (int i = 0; i < count; i++)
            {
                Firefly f = allFireflies[i];
                FillNeighbors(f.transform.position, cellSize, neighborBuffer);
                f.CalculateKuramoto(neighborBuffer, couplingStrength, visionRadiusSqr);
            }
        }

        // 3. Aktualizacja faz, emisji, ruchu i parametru porz¹dku r
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
    }

    private Vector3Int GetCellCoord(Vector3 pos, float size)
    {
        return new Vector3Int(
            Mathf.FloorToInt(pos.x / size),
            Mathf.FloorToInt(pos.y / size),
            Mathf.FloorToInt(pos.z / size)
        );
    }

    private void FillNeighbors(Vector3 pos, float size, List<Firefly> result)
    {
        result.Clear();
        Vector3Int centerCell = GetCellCoord(pos, size);

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    Vector3Int cell = centerCell + new Vector3Int(x, y, z);
                    if (grid.TryGetValue(cell, out List<Firefly> cellFireflies))
                    {
                        result.AddRange(cellFireflies);
                    }
                }
            }
        }
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

    // Publiczne settery z walidacj¹ pod UI

    public void SetCouplingStrength(float value) => couplingStrength = Mathf.Clamp(value, MinCoupling, MaxCoupling);
    public void SetVisionRadius(float value) => visionRadius = Mathf.Clamp(value, MinVisionRadius, MaxVisionRadius);
    public void SetBaseFrequency(float value) => baseFrequency = Mathf.Clamp(value, MinBaseFrequency, MaxBaseFrequency);
    public void SetFrequencySpread(float value) => frequencySpread = Mathf.Clamp(value, MinFrequencySpread, MaxFrequencySpread);
    public void SetMoveSpeed(float value) => moveSpeed = Mathf.Clamp(value, MinMoveSpeed, MaxMoveSpeed);
    public void SetWanderStrength(float value) => wanderStrength = Mathf.Clamp(value, MinWanderStrength, MaxWanderStrength);
    public void SetBoundsRadius(float value) => boundsRadius = Mathf.Clamp(value, MinBoundsRadius, MaxBoundsRadius);
    public void SetSimulationSpeed(float value) => simulationSpeed = Mathf.Clamp(value, MinSimSpeed, MaxSimSpeed);

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
        boundsRadius = 10f;
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