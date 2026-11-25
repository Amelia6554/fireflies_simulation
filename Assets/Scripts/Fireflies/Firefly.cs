using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class Firefly : MonoBehaviour
{
    [Header("State")]
    public float phase;

    [Header("Visual Setup")]
    [Tooltip("Index of the material slot in the Mesh Renderer that has the emissive material")]
    public int glowMaterialIndex = 0;

    private const float TwoPi = 2f * Mathf.PI;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static MaterialPropertyBlock propBlock;

    private Renderer meshRenderer;
    private Renderer[] allRenderers;
    private Color currentColor = Color.black;
    private float frequencyNoise;
    private float phaseCorrection;
    private Vector3 randomOffset;

    private bool ownRendering = true;
    private bool wasDark = false;

    [System.NonSerialized] public float sinPhase;
    [System.NonSerialized] public float cosPhase;
    [System.NonSerialized] public Vector3 cachedPos;

    public Color CurrentColor => currentColor;

    private void Awake()
    {
        allRenderers = GetComponentsInChildren<Renderer>(true);
        meshRenderer = allRenderers.Length > 0 ? allRenderers[0] : null;
        for (int i = 0; i < allRenderers.Length; i++)
        {
            allRenderers[i].shadowCastingMode = ShadowCastingMode.Off;
            allRenderers[i].receiveShadows = false;
        }

        if (propBlock == null)
        {
            propBlock = new MaterialPropertyBlock();
        }

        cachedPos = transform.position;
    }

    private void Start()
    {
        RandomizePhase();
        frequencyNoise = Random.Range(-1f, 1f);
        randomOffset = new Vector3(
            Random.Range(0f, 100f),
            Random.Range(0f, 100f),
            Random.Range(0f, 100f)
        );
    }

    public void RandomizePhase()
    {
        phase = Random.Range(0f, TwoPi);
        currentColor = Color.black;
        phaseCorrection = 0f;
        wasDark = false;
    }

    public void SetOwnRendering(bool on)
    {
        if (ownRendering == on) return;
        ownRendering = on;
        for (int i = 0; i < allRenderers.Length; i++)
        {
            allRenderers[i].enabled = on;
        }

        if (on) transform.position = cachedPos;
        wasDark = false;
    }

    public void CacheState()
    {
        sinPhase = Mathf.Sin(phase);
        cosPhase = Mathf.Cos(phase);
        if (ownRendering) cachedPos = transform.position;
    }

    public void SetPhaseCorrection(float value)
    {
        phaseCorrection = value;
    }

    public void UpdatePhaseAndVisuals(
        float dt,
        float baseFrequency,
        float frequencySpread,
        Color flashColor,
        float flashIntensity,
        float fadeSpeed)
    {
        float naturalFrequency = Mathf.Max(0.05f, baseFrequency + frequencyNoise * frequencySpread);
        phase += (naturalFrequency + phaseCorrection) * dt;

        if (phase >= TwoPi)
        {
            currentColor = flashColor * flashIntensity;
            phase %= TwoPi;
        }
        else if (phase < 0f)
        {
            phase = TwoPi + (phase % TwoPi);
        }

        currentColor = Color.Lerp(currentColor, Color.black, fadeSpeed * dt);
        bool dark = currentColor.maxColorComponent < 0.002f;
        if (dark) currentColor = Color.black;

        if (ownRendering && meshRenderer != null && !(dark && wasDark))
        {
            meshRenderer.GetPropertyBlock(propBlock, glowMaterialIndex);
            propBlock.SetColor(EmissionColorId, currentColor);
            meshRenderer.SetPropertyBlock(propBlock, glowMaterialIndex);
        }

        wasDark = dark;
    }

    public void MoveOrganically(
        float dt,
        float simTime,
        float moveSpeed,
        float wanderStrength,
        Vector3 boundsCenter,
        float boundsRadius)
    {
        if (moveSpeed <= 0f) return;

        float t1 = simTime * wanderStrength;
        float t2 = simTime * wanderStrength * 2.3f;

        float noiseX = (Mathf.PerlinNoise(t1 + randomOffset.x, randomOffset.y) * 2f - 1f)
                       + 0.5f * (Mathf.PerlinNoise(t2 + randomOffset.y, randomOffset.z) * 2f - 1f);
        float noiseY = (Mathf.PerlinNoise(t1 + randomOffset.y, randomOffset.z) * 2f - 1f)
                       + 0.5f * (Mathf.PerlinNoise(t2 + randomOffset.z, randomOffset.x) * 2f - 1f);
        float noiseZ = (Mathf.PerlinNoise(t1 + randomOffset.z, randomOffset.x) * 2f - 1f)
                       + 0.5f * (Mathf.PerlinNoise(t2 + randomOffset.x, randomOffset.y) * 2f - 1f);

        Vector3 direction = new Vector3(noiseX, noiseY, noiseZ);

        Vector3 toCenter = boundsCenter - cachedPos;
        float dist = toCenter.magnitude;
        float softStart = boundsRadius * 0.7f;

        if (dist > softStart && dist > 0.001f)
        {
            float pullStrength = ((dist - softStart) / (boundsRadius - softStart)) * 2.5f;
            direction += (toCenter / dist) * pullStrength;
        }

        if (direction.sqrMagnitude > 0.0001f)
        {
            direction.Normalize();
            cachedPos += direction * (moveSpeed * dt);
            if (ownRendering) transform.position = cachedPos;
        }
    }
}