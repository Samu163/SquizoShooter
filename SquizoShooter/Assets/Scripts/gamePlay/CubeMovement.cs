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

    private Rigidbody rb;
    private UDPClient udpClient;
    private bool isLocalPlayer = false;
    private bool isGrounded = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true; 
        udpClient = FindObjectOfType<UDPClient>();
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        HandleMovement();
        HandleJump();
        SendPositionToServer();
    }

    private void HandleMovement()
    {
        float moveX = 0f;
        float moveZ = 0f;

        if (Input.GetKey(KeyCode.W)) moveZ += 1f;
        if (Input.GetKey(KeyCode.S)) moveZ -= 1f;
        if (Input.GetKey(KeyCode.A)) moveX -= 1f;
        if (Input.GetKey(KeyCode.D)) moveX += 1f;

        Vector3 move = new Vector3(moveX, 0f, moveZ).normalized;

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
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    private void SendPositionToServer()
    {
        if (udpClient != null && udpClient.IsConnected)
        {
            udpClient.SendCubeMovement(transform.position);
        }
    }

    public void UpdateCubePosition(Vector3 newPosition)
    {
        transform.position = newPosition;
    }

    public void SetAsLocalPlayer(bool isLocal)
    {
        isLocalPlayer = isLocal;
    }
}