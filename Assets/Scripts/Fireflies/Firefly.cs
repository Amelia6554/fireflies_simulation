using UnityEngine;

public class Firefly : MonoBehaviour
{
    [Header("Clock Parameters")] public float phase = 0f;
    public float naturalFrequency;

    [Header("Kuramoto Parameters")] public float couplingStrength = 2.0f;
    public float visionRadius = 5.0f;
    
    [Header("Visuals (Fading and Bloom")]
    public Color flashColor = new Color(0.6f, 1.0f, 0.2f, 1f);

    public float flashIntensity = 4.0f;
    public float fadeSpeed = 3.0f;
    
    private Material fireflyMaterial;
    private Color currentColor;

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

    void Update()
    {
        float phaseCorrection = 0f;
        int neighborsCount = 0;

        foreach (Firefly neighbour in FireflySpawner.allFireflies)
        {
            if(neighbour == this) continue;
            
            float distance = Vector3.Distance(transform.position, neighbour.transform.position);

            if (distance <= visionRadius)
            {
                phaseCorrection += Mathf.Sin(neighbour.phase - this.phase);
                neighborsCount++;
            }
        }

        if (neighborsCount > 0)
        {
            phaseCorrection = (couplingStrength / neighborsCount) * phaseCorrection;
        }
        
        phase += (naturalFrequency + phaseCorrection) * Time.deltaTime;

        if (phase > 2f * Mathf.PI)
        {
            Flash();
            phase -= 2f * Mathf.PI;
        }

        if (fireflyMaterial != null)
        {
            currentColor = Color.Lerp(currentColor, Color.black, fadeSpeed * Time.deltaTime);
            fireflyMaterial.SetColor("_EmissionColor", currentColor);
        }
    }
    
    void Flash()
    {
        currentColor = flashColor * flashIntensity;
    }

}
