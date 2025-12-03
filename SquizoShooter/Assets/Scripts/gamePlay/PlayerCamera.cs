using UnityEngine;

public class PlayerCamera : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float mouseSensitivityX = 2f;
    [SerializeField] private float mouseSensitivityY = 2f;
    [SerializeField] private float verticalLimitMin = -90f;
    [SerializeField] private float verticalLimitMax = 90f;
    [SerializeField] private bool smoothMovement = true;
    [SerializeField] private float smoothing = 10f;

    public Camera playerCamera;

    private float rotationX = 0f;
    private float rotationY = 0f;
    private float targetRotationX = 0f;
    private float targetRotationY = 0f;

    private Transform playerTransform;

    public Transform CameraTransform => cameraTransform;

    public void Initialize(Transform playerTrans)
    {
        playerTransform = playerTrans;

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera != null)
            {
                cameraTransform = playerCamera.transform;
            }
        }

        if (cameraTransform == null && playerCamera != null)
        {
            cameraTransform = playerCamera.transform;
        }

        if (playerCamera != null)
        {
            playerCamera.enabled = false;
        }

        if (cameraTransform != null)
        {
            rotationY = playerTransform.eulerAngles.y;
            rotationX = cameraTransform.localEulerAngles.x;
            if (rotationX > 180f)
            {
                rotationX -= 360f;
            }

            targetRotationX = rotationX;
            targetRotationY = rotationY;
        }
    }

    public void EnableCameraIfNeeded()
    {
        if (playerCamera != null && !playerCamera.enabled)
        {
            playerCamera.enabled = true;
        }
    }

    public void HandleCamera(PlayerInput input)
    {
        if (cameraTransform == null) return;

        float mouseX = input.MouseX * mouseSensitivityX;
        float mouseY = input.MouseY * mouseSensitivityY;

        targetRotationY += mouseX;
        targetRotationX -= mouseY;
        targetRotationX = Mathf.Clamp(targetRotationX, verticalLimitMin, verticalLimitMax);

        if (smoothMovement)
        {
            rotationX = Mathf.Lerp(rotationX, targetRotationX, Time.deltaTime * smoothing);
            rotationY = Mathf.Lerp(rotationY, targetRotationY, Time.deltaTime * smoothing);
        }
        else
        {
            rotationX = targetRotationX;
            rotationY = targetRotationY;
        }

        playerTransform.rotation = Quaternion.Euler(0f, rotationY, 0f);
        cameraTransform.localRotation = Quaternion.Euler(rotationX, 0f, 0f);
    }

    public void ApplyRecoil(float recoilPitch, float recoilYaw)
    {
        targetRotationY += Random.Range(-recoilYaw, recoilYaw);
        targetRotationX = Mathf.Clamp(targetRotationX - recoilPitch, verticalLimitMin, verticalLimitMax);
    }

    public void SetAsLocalCamera(bool isLocal)
    {
        if (playerCamera != null)
        {
            playerCamera.enabled = isLocal;
        }
    }

    public void SetSensitivity(float sensX, float sensY)
    {
        mouseSensitivityX = sensX;
        mouseSensitivityY = sensY;
    }
}