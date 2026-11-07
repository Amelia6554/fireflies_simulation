using System.Collections.Generic;
using UnityEngine;

public class Firefly : MonoBehaviour
{
    [Header("Clock Parameters")] public float phase = 0f;
    public float naturalFrequency;

    [Header("Visuals (Fading and Bloom")]
    public Color flashColor = new Color(0.6f, 1.0f, 0.2f, 1f);

    public float flashIntensity = 4.0f;
    public float fadeSpeed = 3.0f;
    
    private Material fireflyMaterial;
    private Color currentColor;
    private float phaseCorrection = 0f;

    void Start()
    {
        phase = Random.Range(0f, 2f * Mathf.PI);
        naturalFrequency = Random.Range(0.9f, 1.1f);
        
        Renderer renderer =  GetComponent<Renderer>();
        if (renderer != null)
        {
            fireflyMaterial = renderer.material;
            fireflyMaterial.EnableKeyword("_EMISSION");
        }
        
        currentColor = Color.black;
    }

    public void CalculateKuramoto(List<Firefly> neighbors)
    {
        phaseCorrection = 0f;
        int validNeighbors = 0;

        float k = SimulationManager.instance.couplingStrenghth;
        float radius = SimulationManager.instance.visionRadius;

        foreach (Firefly neighbor in neighbors)
        {
           if (neighbor == this) continue;

           if (Vector3.Distance(transform.position, neighbor.transform.position) <= radius)
           {
               phaseCorrection += Mathf.Sin(neighbor.phase - this.phase);
               validNeighbors++;
           }
        }

        if (validNeighbors > 0)
        {
            phaseCorrection = (k / validNeighbors) * phaseCorrection;
        }
    }

    public void UpdatePhaseAndVisuals()
    {
        phase += (naturalFrequency + phaseCorrection) * Time.deltaTime;

        if (phase >= 2f * Mathf.PI)
        {
            currentColor = flashColor * flashIntensity;
            phase -= 2f * Mathf.PI;
        }

        if (fireflyMaterial != null)
        {
            currentColor = Color.Lerp(currentColor, Color.black, fadeSpeed * Time.deltaTime);
            fireflyMaterial.SetColor("_EmissionColor", currentColor);
        }
    }

}
