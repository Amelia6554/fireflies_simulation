using UnityEngine;

public class FpsDisplay : MonoBehaviour
{
    private float fps;
    public TMPro.TextMeshProUGUI FpsCounter;
    void Start()
    {
       InvokeRepeating("GetFps", 1f, 1f); 
    }

    void GetFps()
    {
        fps =  1.0f / Time.deltaTime;
        FpsCounter.text = "FPS: " + fps.ToString("F2");
        
    }
}
