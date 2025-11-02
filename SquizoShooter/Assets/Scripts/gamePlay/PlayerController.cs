using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] public float health = 100f;

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

    private Vector3 lastSentPosition;
    private Vector3 lastSentRotation;
    private float lastSentHealth;
    private const float positionThreshold = 0.01f;
    private const float rotationThreshold = 0.5f;
    private const float healthThreshold = 0.01f;

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
        SendRotationToServer();
        SendPlayerDataToServer();

        if(Input.GetKeyDown(KeyCode.K))
        {
            health -= 5f;
            if (health < 0f) health = 0f;
            Debug.LogWarning($"Player health decreased to {health}");
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
    void SendPlayerDataToServer()
    {
        if (udpClient != null && udpClient.IsConnected)
        {
            if (Mathf.Abs(health - lastSentHealth) > healthThreshold)
            {
                udpClient.SendPlayerHealth(health);
                lastSentHealth = health;
            }
        }
    }
    void SendPositionToServer()
    {
        if (udpClient != null && udpClient.IsConnected)
        {
            if (Vector3.Distance(transform.position, lastSentPosition) > positionThreshold)
            {
                udpClient.SendCubeMovement(transform.position);
                lastSentPosition = transform.position;
            }
        }
    }

    void SendRotationToServer()
    {
        if (udpClient != null && udpClient.IsConnected)
        {
            float yaw = transform.eulerAngles.y;
            Vector3 currentRotation = new Vector3(0, yaw, 0);

            if (Mathf.Abs(Mathf.DeltaAngle(lastSentRotation.y, currentRotation.y)) > rotationThreshold)
            {
                udpClient.SendCubeRotation(currentRotation);
                lastSentRotation = currentRotation;
            }
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

    public void UpdateRotation(Vector3 rotation)
    {
        transform.rotation = Quaternion.Euler(rotation);
    }

    public void UpdateHealth(float newHealth)
    {
        health = newHealth;
    }
}