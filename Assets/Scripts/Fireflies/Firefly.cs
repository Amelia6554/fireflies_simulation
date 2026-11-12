using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class Firefly : MonoBehaviour
{
    [Header("Clock Parameters")] public float phase = 0f;
    public float naturalFrequency;

    [Header("Visuals (Fading and Bloom")]
    public Color flashColor = new Color(0.6f, 1.0f, 0.2f, 1f);

    public float flashIntensity = 4.0f;
    public float fadeSpeed = 3.0f;
    
    private static MaterialPropertyBlock propBlock;
    private Renderer meshRenderer;
    private Color currentColor;
    private float phaseCorrection = 0f;

    private void Awake()
    {
        meshRenderer =  GetComponent<Renderer>();
        if (propBlock != null)
        {
            propBlock = new MaterialPropertyBlock();
        }
    }

    void Start()
    {
        phase = Random.Range(0f, 2f * Mathf.PI);
        naturalFrequency = Random.Range(0.9f, 1.1f);
        
        currentColor = Color.black;
    }

    public void CalculateKuramoto(List<Firefly> neighbors)
    {
        phaseCorrection = 0f;
        int validNeighbors = 0;

        float k = SimulationManager.instance.couplingStrenghth;
        float radius = SimulationManager.instance.visionRadius;
        float readiusQuared = radius * radius;

        foreach (Firefly neighbor in neighbors)
        {
           if (neighbor == this) continue;

           if ((transform.position - neighbor.transform.position).sqrMagnitude <= readiusQuared)
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

        if (meshRenderer != null)
        {
            currentColor = Color.Lerp(currentColor, Color.black, fadeSpeed * Time.deltaTime);
            
            if (propBlock == null)
            {
                propBlock = new MaterialPropertyBlock();
            }
            
            meshRenderer.GetPropertyBlock(propBlock);
            propBlock.SetColor("_EmissionColor", currentColor);
            meshRenderer.SetPropertyBlock(propBlock);
        }
    }

}
