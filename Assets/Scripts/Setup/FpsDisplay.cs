using UnityEngine;

public class FpsDisplay : MonoBehaviour
{
    private int fps;
    public TMPro.TextMeshProUGUI FpsCounter;
    void Start()
    {
       InvokeRepeating("GetFps", 1f, 1f); 
    }

    void GetFps()
    {
        fps = (int)( 1.0f / Time.deltaTime);
        FpsCounter.text = "FPS: " + fps.ToString("F2");
        
    }
}
