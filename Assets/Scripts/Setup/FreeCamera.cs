using UnityEngine;
using UnityEngine.InputSystem;

public class FreeCamera : MonoBehaviour
{
    [Header("Base Settings")]
    public float moveSpeed = 20f;
    public float lookSpeed = 0.2f;

    [Header("Smoothing")]
    public float moveSmoothness = 5f; 
    public float lookSmoothness = 10f; 

    private float targetRotationX = 0f;
    private float targetRotationY = 0f;
    private float currentRotationX = 0f;
    private float currentRotationY = 0f;

    private Vector3 targetVelocity = Vector3.zero;
    private Vector3 currentVelocity = Vector3.zero;

    void Update()
    {
        if (Mouse.current == null || Keyboard.current == null) return;

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        targetRotationY += mouseDelta.x * lookSpeed;
        targetRotationX -= mouseDelta.y * lookSpeed;
        targetRotationX = Mathf.Clamp(targetRotationX, -90f, 90f);

        currentRotationX = Mathf.Lerp(currentRotationX, targetRotationX, lookSmoothness * Time.deltaTime);
        currentRotationY = Mathf.Lerp(currentRotationY, targetRotationY, lookSmoothness * Time.deltaTime);
        
        transform.localRotation = Quaternion.Euler(currentRotationX, currentRotationY, 0f);

        float moveX = 0f;
        float moveZ = 0f;
        float moveY = 0f;

        if (Keyboard.current.wKey.isPressed) moveZ = 1f;
        if (Keyboard.current.sKey.isPressed) moveZ = -1f;
        if (Keyboard.current.aKey.isPressed) moveX = -1f;
        if (Keyboard.current.dKey.isPressed) moveX = 1f;
        if (Keyboard.current.eKey.isPressed) moveY = 1f;
        if (Keyboard.current.qKey.isPressed) moveY = -1f;
        
        Vector3 inputDirection = transform.right * moveX + transform.up * moveY + transform.forward * moveZ;
        
        if (inputDirection.magnitude > 1f) inputDirection.Normalize();

        targetVelocity = inputDirection * moveSpeed;

        currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, moveSmoothness * Time.deltaTime);
        transform.position += currentVelocity * Time.deltaTime;
    }
}