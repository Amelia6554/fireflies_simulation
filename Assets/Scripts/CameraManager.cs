using UnityEngine;
using UnityEngine.InputSystem;

public class CameraManager : MonoBehaviour
{
    [Header("Camera settings")]
    public Camera freeCamera;
    public Camera topCamera;

    private AudioListener freeListener;
    private AudioListener topListener;

    void Start()
    {
        if (freeCamera != null) freeListener = freeCamera.GetComponent<AudioListener>();
        if (topCamera != null) topListener = topCamera.GetComponent<AudioListener>();

        ActivateFreeCamera();
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            ActivateFreeCamera();
        }

        if (Keyboard.current.tKey.wasPressedThisFrame)
        {
            ActivateTopCamera();
        }
    }

    private void ActivateFreeCamera()
    {
        if (topCamera != null) topCamera.enabled = false;
        if (topListener != null) topListener.enabled = false;

        if (freeCamera != null) freeCamera.enabled = true;
        if (freeListener != null) freeListener.enabled = true;
    }

    private void ActivateTopCamera()
    {
        if (freeCamera != null) freeCamera.enabled = false;
        if (freeListener != null) freeListener.enabled = false;

        if (topCamera != null) topCamera.enabled = true;
        if (topListener != null) topListener.enabled = true;
    }
}