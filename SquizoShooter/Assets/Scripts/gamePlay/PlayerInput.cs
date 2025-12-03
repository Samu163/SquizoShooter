using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    private PlayerController playerController;
    // Current frame input
    public float HorizontalInput { get; private set; }
    public float VerticalInput { get; private set; }
    public bool IsSprinting { get; private set; }
    public bool JumpPressed { get; private set; }
    public bool JumpHeld { get; private set; }
    public bool SlidePressed { get; private set; }
    public bool ShootPressed { get; private set; }
    public float MouseX { get; private set; }
    public float MouseY { get; private set; }

    public void Initialize(PlayerController pc)
    {
        playerController = pc;
    }

    public void HandleInput()
    {
        // Movement input
        HorizontalInput = Input.GetAxis("Horizontal");
        VerticalInput = Input.GetAxis("Vertical");

        // Sprint
        IsSprinting = Input.GetKey(KeyCode.LeftShift);

        // Jump
        JumpPressed = Input.GetButtonDown("Jump");
        JumpHeld = Input.GetButton("Jump");

        if (JumpPressed)
        {
            Debug.Log("");

        }

        // Slide
        SlidePressed = Input.GetKeyDown(KeyCode.LeftControl);

        // Shooting
        ShootPressed = Input.GetMouseButtonDown(0);

        // Mouse input
        MouseX = Input.GetAxis("Mouse X");
        MouseY = Input.GetAxis("Mouse Y");

        // Handle slide input through movement component
        SlideComponent slideComponent = playerController.GetSlideComponent();
        WallJumpComponent wallJumpComponent = playerController.GetWallJumpComponent();

        slideComponent.HandleSlideInput(this);
        wallJumpComponent.DetectWallRun(this);
        
    }
}