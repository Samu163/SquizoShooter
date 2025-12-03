using UnityEngine;

public class WallJumpComponent : MonoBehaviour
{
    [Header("Wallride Settings")]
    [SerializeField] private float wallCheckDistance = 0.8f;
    [SerializeField] private float wallSlideFallSpeed = -2f;
    [SerializeField] private float wallJumpPush = 6f;
    [SerializeField] private float wallRunMaxTime = 1.5f;
    [SerializeField] private float wallRunGravity = -2f;
    [SerializeField] private float wallRunSpeed = 5f;
    [SerializeField] private float minWallAttachFallSpeed = -0.5f;
    [SerializeField] private float wallReattachCooldown = 0.25f;
    [SerializeField] private float wallAttachWindow = 0.35f;
    [SerializeField] private float wallJumpCoyoteTime = 0.2f;
    [SerializeField] private float wallStickDistance = 0.5f;

    private CharacterController controller;
    private PlayerController playerController;

    // Wall states
    private bool isWallRiding = false;
    private bool isWallRunning = false;
    private Vector3 currentWallNormal = Vector3.zero;
    private Vector3 lastWallNormal = Vector3.zero;
    private float wallRunTimer = 0f;
    private int wallRunSide = 0;
    private float lastWallRunEndTime = -10f;
    private float lastWallJumpTime = -10f;
    private Vector3 lastWallJumpNormal = Vector3.zero;
    private const float sameWallNormalTolerance = 0.1f;
    private float lastJumpTime = -10f;

    public bool IsWallRiding => isWallRiding;
    public bool IsWallRunning => isWallRunning;
    public float WallSlideFallSpeed => wallSlideFallSpeed;

    public void Initialize(CharacterController ctrl, PlayerController pc)
    {
        controller = ctrl;
        playerController = pc;
    }

    public void DetectWallRun(PlayerInput input)
    {
        PlayerMovement movement = playerController.GetMovement();
        if (movement == null) return;

        bool isGrounded = movement.IsGrounded;

        // Reset wallrun when grounded
        if (isGrounded)
        {
            if (isWallRunning) EndWallRun();
            isWallRiding = false;
            currentWallNormal = Vector3.zero;
            return;
        }

        bool jumpHeld = input.JumpHeld;

        // Detect frontal wall for slide
        Vector3 origin = playerController.transform.position + Vector3.up * (controller != null ? controller.height * 0.5f : 0.9f);
        Vector3 forward = playerController.transform.forward;
        if (Physics.Raycast(origin, forward, out RaycastHit frontHit, wallCheckDistance))
        {
            if (frontHit.collider.CompareTag("Wall"))
            {
                if (movement.VerticalVelocity.y < minWallAttachFallSpeed || (Time.time - lastJumpTime) <= wallAttachWindow)
                {
                    isWallRiding = true;
                    currentWallNormal = frontHit.normal;
                }
            }
        }
        else
        {
            isWallRiding = false;
            currentWallNormal = Vector3.zero;
        }

        // Lateral wallrun - Lucio style
        if (jumpHeld && !isGrounded)
        {
            // Check right
            if (Physics.Raycast(origin, playerController.transform.right, out RaycastHit rightHit, wallCheckDistance))
            {
                if (rightHit.collider.CompareTag("Wall"))
                {
                    if (!isWallRunning && Time.time - lastWallJumpTime > wallReattachCooldown && !IsSameWallNormal(lastWallJumpNormal, rightHit.normal))
                    {
                        StartWallRun(rightHit.normal, 1);
                        return;
                    }
                }
            }

            // Check left
            if (Physics.Raycast(origin, -playerController.transform.right, out RaycastHit leftHit, wallCheckDistance))
            {
                if (leftHit.collider.CompareTag("Wall"))
                {
                    if (!isWallRunning && Time.time - lastWallJumpTime > wallReattachCooldown && !IsSameWallNormal(lastWallJumpNormal, leftHit.normal))
                    {
                        StartWallRun(leftHit.normal, -1);
                        return;
                    }
                }
            }
        }

        // End wallrun if not holding jump
        if (isWallRunning && !jumpHeld)
        {
            EndWallRun();
            return;
        }

        // Maintain wallrun check
        if (isWallRunning)
        {
            if (wallRunTimer > 0.1f && !IsStillOnWall()) EndWallRun();
        }
    }

    public void HandleWallRunMovement(float moveX, float moveZ, ref Vector3 verticalVelocity)
    {
        Vector3 inputWorld = (playerController.transform.right * moveX + playerController.transform.forward * moveZ);
        Vector3 wallDir = Vector3.ProjectOnPlane(inputWorld, lastWallNormal);
        if (wallDir.sqrMagnitude > 0.0001f)
        {
            wallDir.Normalize();
            controller.Move(wallDir * wallRunSpeed * Time.deltaTime);
        }

        controller.Move(-lastWallNormal * 0.5f * Time.deltaTime);
        verticalVelocity.y = wallRunGravity;
        controller.Move(verticalVelocity * Time.deltaTime);
    }

    public bool TryWallJump(PlayerInput input, ref Vector3 verticalVelocity, float jumpForce, float gravity)
    {
        // Wallrun jump (tap while wallrunning)
        if (isWallRunning && input.JumpPressed)
        {
            DoWallJump(lastWallNormal, ref verticalVelocity, jumpForce, gravity);
            return true;
        }

        // Coyote time jump
        if (!isWallRunning && (Time.time - lastWallRunEndTime) <= wallJumpCoyoteTime && input.JumpPressed)
        {
            DoWallJump(lastWallNormal, ref verticalVelocity, jumpForce, gravity);
            return true;
        }

        // Wall slide frontal jump
        if (isWallRiding && input.JumpPressed)
        {
            verticalVelocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            Vector3 push = currentWallNormal * wallJumpPush;
            isWallRiding = false;

            lastWallJumpTime = Time.time;
            lastWallJumpNormal = currentWallNormal.normalized;
            lastJumpTime = Time.time;

            controller.Move(push * Time.deltaTime);
            return true;
        }

        return false;
    }

    void DoWallJump(Vector3 wallNormal, ref Vector3 verticalVelocity, float jumpForce, float gravity)
    {
        verticalVelocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
        Vector3 push = wallNormal.normalized * wallJumpPush;

        EndWallRun();
        lastWallJumpTime = Time.time;
        lastWallJumpNormal = wallNormal.normalized;
        lastJumpTime = Time.time;

        controller.Move(push * Time.deltaTime);
    }

    bool IsSameWallNormal(Vector3 a, Vector3 b)
    {
        return Vector3.Distance(a, b) < sameWallNormalTolerance;
    }

    bool IsStillOnWall()
    {
        if (lastWallNormal == Vector3.zero) return false;
        Vector3 origin = playerController.transform.position + Vector3.up * (controller != null ? controller.height * 0.5f : 0.9f);
        float maxDist = wallCheckDistance + wallStickDistance;

        if (Physics.SphereCast(origin, 0.25f, -lastWallNormal, out RaycastHit hit, maxDist))
        {
            return hit.collider != null && hit.collider.CompareTag("Wall");
        }
        return false;
    }

    void StartWallRun(Vector3 wallNormal, int side)
    {
        if (isWallRunning) return;
        isWallRunning = true;
        isWallRiding = false;
        lastWallNormal = wallNormal;
        wallRunSide = side;
        wallRunTimer = 0f;
    }

    void EndWallRun()
    {
        if (!isWallRunning) return;
        isWallRunning = false;
        wallRunTimer = 0f;
        wallRunSide = 0;
        lastWallRunEndTime = Time.time;
    }

    public void UpdateTimer()
    {
        if (isWallRunning)
        {
            wallRunTimer += Time.deltaTime;
            if (wallRunTimer >= wallRunMaxTime)
            {
                EndWallRun();
            }
        }
    }

    public void RegisterJump()
    {
        lastJumpTime = Time.time;
    }

    public void ResetState()
    {
        isWallRiding = false;
        isWallRunning = false;
        currentWallNormal = Vector3.zero;
        lastWallNormal = Vector3.zero;
        wallRunTimer = 0f;
        wallRunSide = 0;
    }
}