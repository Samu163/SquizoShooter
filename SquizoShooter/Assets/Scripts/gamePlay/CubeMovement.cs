using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CubeMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float sprintMultiplier = 1.5f;
    public float jumpForce = 5f;
    public LayerMask groundMask;
    public float groundCheckDistance = 0.1f;

    [Header("Camera Settings")]
    public float mouseSensitivity = 100f;

    private float xRotation = 0f;
    private Rigidbody rb;
    private UDPClient udpClient;
    private bool isLocalPlayer = false;
    private bool isGrounded = false;
    private Camera playerCamera;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        udpClient = FindObjectOfType<UDPClient>();

        // Obtener la c�mara directamente del mismo GameObject
        playerCamera = GetComponent<Camera>();

        // Desactivar cualquier MainCamera en escena si no es esta
        Camera mainCam = Camera.main;
        if (mainCam != null && mainCam.gameObject != gameObject)
            mainCam.gameObject.SetActive(false);

        // Desactivar c�mara hasta saber si es jugador local
        if (playerCamera != null)
            playerCamera.enabled = false;

        if (Camera.allCamerasCount == 0 && playerCamera != null)
            playerCamera.enabled = true;
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        // Activar c�mara local
        if (playerCamera != null && !playerCamera.enabled)
        {
            playerCamera.enabled = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        HandleMouseLook();
        HandleMovement();
        HandleJump();
        SendPositionToServer();
    }

    private void HandleMouseLook()
    {
        if (playerCamera == null) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        // Rotaci�n vertical (c�mara)
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);
        playerCamera.transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);

        // Rotaci�n horizontal (jugador)
        transform.Rotate(Vector3.up * mouseX);
    }

    private void HandleMovement()
    {
        float moveX = 0f;
        float moveZ = 0f;

        if (Input.GetKey(KeyCode.W)) moveZ += 1f;
        if (Input.GetKey(KeyCode.S)) moveZ -= 1f;
        if (Input.GetKey(KeyCode.A)) moveX -= 1f;
        if (Input.GetKey(KeyCode.D)) moveX += 1f;

        Vector3 move = (transform.right * moveX + transform.forward * moveZ).normalized;

        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift))
            speed *= sprintMultiplier;

        Vector3 moveVelocity = move * speed;
        Vector3 currentVelocity = rb.linearVelocity;
        rb.linearVelocity = new Vector3(moveVelocity.x, currentVelocity.y, moveVelocity.z);
    }

    private void HandleJump()
    {
        isGrounded = Physics.Raycast(transform.position, Vector3.down, groundCheckDistance + 0.1f, groundMask);

        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }

    private void SendPositionToServer()
    {
        if (udpClient != null && udpClient.IsConnected)
            udpClient.SendCubeMovement(transform.position);
    }

    public void UpdateCubePosition(Vector3 newPosition)
    {
        transform.position = newPosition;
    }

    public void SetAsLocalPlayer(bool isLocal)
    {
        isLocalPlayer = isLocal;

        if (playerCamera != null)
            playerCamera.enabled = isLocal;
    }
}