using UnityEngine;

public class FpsDisplay : MonoBehaviour
{
    public TMPro.TextMeshProUGUI FpsCounter;
    private float deltaTime = 0.0f;
    void Start()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = -1;
    }

    void Update()
    {
        deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;
        
        float currentfFps = 1.0f / deltaTime;

        if (FpsCounter != null)
        {
            FpsCounter.text = "FPS: " + Mathf.CeilToInt(currentfFps).ToString();
        }
    }
}
