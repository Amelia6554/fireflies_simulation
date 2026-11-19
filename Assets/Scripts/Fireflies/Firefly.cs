using System.Collections.Generic;
using UnityEngine;

public class Firefly : MonoBehaviour
{
    [Header("State")]
    public float phase;

    [Header("Visual Setup")]
    [Tooltip("Numer slotu w Mesh Renderer, na którym jest materiał emisyjny")]
    public int glowMaterialIndex = 0;

    private const float TwoPi = 2f * Mathf.PI;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static MaterialPropertyBlock propBlock;

    private Renderer meshRenderer;
    private Color currentColor = Color.black;
    private float frequencyNoise; 
    private float phaseCorrection;
    private Vector3 randomOffset;

    private void Awake()
    {
        meshRenderer = GetComponentInChildren<Renderer>();
        if (propBlock == null)
        {
            propBlock = new MaterialPropertyBlock();
        }
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
    }

    public void CalculateKuramoto(List<Firefly> neighbors, float couplingStrength, float visionRadiusSqr)
    {
        phaseCorrection = 0f;
        int validNeighbors = 0;
        Vector3 myPos = transform.position;

        for (int i = 0; i < neighbors.Count; i++)
        {
            Firefly neighbor = neighbors[i];
            if (neighbor == this) continue;

            if ((myPos - neighbor.transform.position).sqrMagnitude <= visionRadiusSqr)
            {
                phaseCorrection += Mathf.Sin(neighbor.phase - phase);
                validNeighbors++;
            }
        }

        if (validNeighbors > 0)
        {
            phaseCorrection = (couplingStrength / validNeighbors) * phaseCorrection;
        }
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
            phase = (phase % TwoPi) + TwoPi;
        }

        if (meshRenderer != null)
        {
            currentColor = Color.Lerp(currentColor, Color.black, fadeSpeed * dt);

            meshRenderer.GetPropertyBlock(propBlock, glowMaterialIndex);
            propBlock.SetColor(EmissionColorId, currentColor);
            meshRenderer.SetPropertyBlock(propBlock, glowMaterialIndex);
        }
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

        float t = simTime * wanderStrength;
        float noiseX = Mathf.PerlinNoise(t + randomOffset.x, 0f) * 2f - 1f;
        float noiseY = Mathf.PerlinNoise(t + randomOffset.y, 0f) * 2f - 1f;
        float noiseZ = Mathf.PerlinNoise(t + randomOffset.z, 0f) * 2f - 1f;

        Vector3 direction = new Vector3(noiseX, noiseY, noiseZ);

        // Miękkie zawracanie, gdy świetlik oddali się poza promień symulacji
        Vector3 toCenter = boundsCenter - transform.position;
        float distSqr = toCenter.sqrMagnitude;
        float radiusSqr = boundsRadius * boundsRadius;

        if (distSqr > radiusSqr)
        {
            float overshoot = Mathf.Sqrt(distSqr) - boundsRadius;
            direction += toCenter.normalized * (overshoot * 0.5f);
        }

        if (direction.sqrMagnitude > 0.0001f)
        {
            direction.Normalize();
            transform.position += direction * (moveSpeed * dt);
        }
    }
}