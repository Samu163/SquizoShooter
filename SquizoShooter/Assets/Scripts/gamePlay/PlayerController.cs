using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float gravity = -20f;

    [Header("Camera Settings")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float mouseSensitivityX = 2f;
    [SerializeField] private float mouseSensitivityY = 2f;
    [SerializeField] private float verticalLimitMin = -90f;
    [SerializeField] private float verticalLimitMax = 90f;
    [SerializeField] private bool smoothMovement = true;
    [SerializeField] private float smoothing = 10f;

    // Network/Multiplayer
    private UDPClient udpClient;
    private bool isLocalPlayer = false;
    private Camera playerCamera;

    private CharacterController controller;
    private Vector3 verticalVelocity;
    private float rotationX = 0f;
    private float rotationY = 0f;
    private float targetRotationX = 0f;
    private float targetRotationY = 0f;
    private bool isGrounded;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        udpClient = FindObjectOfType<UDPClient>();
        playerCamera = GetComponent<Camera>();
        if (playerCamera == null && cameraTransform != null)
        {
            playerCamera = cameraTransform.GetComponent<Camera>();
        }
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

        // Disable camera
        if (playerCamera != null)
        {
            playerCamera.enabled = false;
        }

        // Init rotation
        if (cameraTransform != null)
        {
            rotationY = transform.eulerAngles.y;
            rotationX = cameraTransform.localEulerAngles.x;
            if (rotationX > 180f)
            {
                rotationX -= 360f;
            }
        }
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        if (playerCamera != null && !playerCamera.enabled)
        {
            playerCamera.enabled = true;
        }

        HandleMovement();
        HandleCamera();
        HandleJump();
        SendPositionToServer();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        if (Input.GetMouseButtonDown(0) && Cursor.lockState == CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void HandleMovement()
    {
        isGrounded = controller.isGrounded;

        if (isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -2f; 
        }

        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        Vector3 movement = transform.right * moveX + transform.forward * moveZ;


        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed;

        controller.Move(movement * currentSpeed * Time.deltaTime);

        // Apply gravity
        verticalVelocity.y += gravity * Time.deltaTime;
        controller.Move(verticalVelocity * Time.deltaTime);
    }

    void HandleCamera()
    {
        if (cameraTransform == null) return;

        // input
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivityX;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivityY;

        targetRotationY += mouseX;
        targetRotationX -= mouseY;
        targetRotationX = Mathf.Clamp(targetRotationX, verticalLimitMin, verticalLimitMax);

        // Smoothly interpolate to target rotation
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

        transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
        cameraTransform.localRotation = Quaternion.Euler(rotationX, 0f, 0f);
    }

    void HandleJump()
    {
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            verticalVelocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
        }
    }

    void SendPositionToServer()
    {
        if (udpClient != null && udpClient.IsConnected)
        {
            udpClient.SendCubeMovement(transform.position);
        }
    }

    // Called by network manager to update remote player position
    public void UpdatePlayerPosition(Vector3 newPosition)
    {
        if (!isLocalPlayer)
        {
            transform.position = newPosition;
        }
    }

    // Set if this is the local player or a remote player
    public void SetAsLocalPlayer(bool isLocal)
    {
        isLocalPlayer = isLocal;

        if (playerCamera != null)
        {
            playerCamera.enabled = isLocal;
        }

        if (isLocal)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        enabled = true;
    }

    public void SetSensitivity(float sensX, float sensY)
    {
        mouseSensitivityX = sensX;
        mouseSensitivityY = sensY;
    }

    public void UpdatePosition(Vector3 position)
    {
        transform.position = position;
    }

    //TODO: UpdateRotation
}