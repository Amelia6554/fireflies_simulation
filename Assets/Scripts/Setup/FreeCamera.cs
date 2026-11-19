using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class FreeCamera : MonoBehaviour
{
    [Header("Base Settings")]
    public float moveSpeed = 20f;
    public float lookSpeed = 0.2f;
    public bool requireRightMouseToLook = true;

    [Header("Smoothing")]
    public float moveSmoothness = 5f;
    public float lookSmoothness = 10f;

    [Header("State")]
    public bool isLocked = false;

    private float targetRotationX;
    private float targetRotationY;
    private float currentRotationX;
    private float currentRotationY;

    private Vector3 targetVelocity;
    private Vector3 currentVelocity;

    private void Start()
    {
        Vector3 angles = transform.localEulerAngles;
        targetRotationX = currentRotationX = angles.x > 180f ? angles.x - 360f : angles.x;
        targetRotationY = currentRotationY = angles.y;
    }

    private void Update()
    {
        if (Mouse.current == null || Keyboard.current == null) return;

        if (Keyboard.current.lKey.wasPressedThisFrame)
        {
            isLocked = !isLocked;
        }

        bool pointerOverUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        bool wantsToLook = !isLocked && (!requireRightMouseToLook || Mouse.current.rightButton.isPressed);

        if (wantsToLook && !pointerOverUI)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            targetRotationY += mouseDelta.x * lookSpeed;
            targetRotationX = Mathf.Clamp(targetRotationX - mouseDelta.y * lookSpeed, -90f, 90f);
        }

        if (!isLocked)
        {
            float moveX = 0f, moveY = 0f, moveZ = 0f;

            if (Keyboard.current.wKey.isPressed) moveZ = 1f;
            if (Keyboard.current.sKey.isPressed) moveZ = -1f;
            if (Keyboard.current.aKey.isPressed) moveX = -1f;
            if (Keyboard.current.dKey.isPressed) moveX = 1f;
            if (Keyboard.current.eKey.isPressed) moveY = 1f;
            if (Keyboard.current.qKey.isPressed) moveY = -1f;

            Vector3 inputDirection = transform.right * moveX + transform.up * moveY + transform.forward * moveZ;
            if (inputDirection.sqrMagnitude > 1f) inputDirection.Normalize();

            targetVelocity = inputDirection * moveSpeed;
        }
        else
        {
            targetVelocity = Vector3.zero;
        }

        currentRotationX = Mathf.Lerp(currentRotationX, targetRotationX, lookSmoothness * Time.deltaTime);
        currentRotationY = Mathf.Lerp(currentRotationY, targetRotationY, lookSmoothness * Time.deltaTime);
        transform.localRotation = Quaternion.Euler(currentRotationX, currentRotationY, 0f);

        currentVelocity = Vector3.Lerp(currentVelocity, targetVelocity, moveSmoothness * Time.deltaTime);
        transform.position += currentVelocity * Time.deltaTime;
    }
}