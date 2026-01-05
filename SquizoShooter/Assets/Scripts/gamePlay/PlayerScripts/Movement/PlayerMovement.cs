using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float gravity = -20f;

    private CharacterController controller;
    private PlayerController playerController;
    private PlayerInput playerInput;
    private Vector3 verticalVelocity;
    private bool isGrounded;
    private PlayerAudioController audioController;
    private UDPClient udpClient;

    public Vector3 VerticalVelocity => verticalVelocity;
    public bool IsGrounded => isGrounded;
    public float Gravity => gravity;
    public float JumpForce => jumpForce;

    public void Initialize(CharacterController ctrl, PlayerController pc)
    {
        controller = ctrl;
        playerController = pc;
        playerInput = pc.GetInput();
        audioController = pc.GetComponent<PlayerAudioController>();
        udpClient = FindObjectOfType<UDPClient>();
    }

    public void HandleMovement(WallJumpComponent wallRun, SlideComponent slide)
    {
        if (playerInput == null) return;

        // If sliding, move forward with slide force
        if (slide.IsSliding)
        {
            Vector3 slideMove = playerController.transform.forward * (sprintSpeed * slide.SlideSpeedMultiplier);
            slideMove.y = verticalVelocity.y;
            controller.Move(slideMove * Time.deltaTime);

            if (!wallRun.IsWallRunning)
                verticalVelocity.y += gravity * Time.deltaTime;
            return;
        }

        isGrounded = controller.isGrounded;

        if (isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -2f;
        }

        float moveX = playerInput.HorizontalInput;
        float moveZ = playerInput.VerticalInput;

        // Wallrunning movement is handled by WallRunComponent
        if (wallRun.IsWallRunning)
        {
            wallRun.HandleWallRunMovement(moveX, moveZ, ref verticalVelocity);
            return;
        }

        Vector3 movement = playerController.transform.right * moveX + playerController.transform.forward * moveZ;
        float currentSpeed = playerInput.SprintHeld ? sprintSpeed : walkSpeed;
        controller.Move(movement * currentSpeed * Time.deltaTime);

        // Apply gravity
        if (!wallRun.IsWallRiding)
        {
            verticalVelocity.y += gravity * Time.deltaTime;
        }
        else
        {
            verticalVelocity.y = Mathf.Max(verticalVelocity.y, wallRun.WallSlideFallSpeed);
        }

        controller.Move(verticalVelocity * Time.deltaTime);
    }

    public void HandleJump(WallJumpComponent wallRun, SlideComponent slide)
    {
        // Wall jump handling is delegated to WallRunComponent
        if (wallRun.TryWallJump(ref verticalVelocity))
        {
            return;
        }

        // Jump during slide
        if (slide.IsSliding)
        {
            slide.EndSlide();
            verticalVelocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            wallRun.RegisterJump();
            return;
        }

        // Normal jump
        if (isGrounded)
        {
            verticalVelocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            wallRun.RegisterJump();

            var audio = playerController.GetComponent<PlayerAudioController>();
            if (audio) audio.PlayJump();

            if (playerController.IsLocalPlayer && udpClient != null)
            {
                udpClient.SendJump();
            }
        }
    }

    public void ResetVelocity()
    {
        verticalVelocity = Vector3.zero;
    }

    public void ApplyPhysicalRecoil(float recoilBack)
    {
        if (controller != null)
        {
            controller.Move(-playerController.transform.forward * recoilBack);
        }
    }

    public float GetHorizontalSpeed()
    {
        return controller != null
            ? new Vector3(controller.velocity.x, 0f, controller.velocity.z).magnitude
            : 0f;
    }
}