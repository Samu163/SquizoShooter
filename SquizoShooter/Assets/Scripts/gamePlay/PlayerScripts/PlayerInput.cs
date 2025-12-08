using UnityEngine;
using System;

public class PlayerInput : MonoBehaviour
{
    // Events/Actions
    public event Action OnJumpPressed;
    public event Action OnShootPressed;
    public event Action OnSlidePressed;

    // Continuous input (no events, just read)
    public float HorizontalInput { get; private set; }
    public float VerticalInput { get; private set; }
    public bool JumpHeld { get; private set; }
    public bool SprintHeld { get; private set; }
    public float MouseX { get; private set; }
    public float MouseY { get; private set; }

    private PlayerController playerController;

    public void Initialize(PlayerController pc)
    {
        playerController = pc;
    }

    void Update()
    {
        if (playerController != null && !playerController.IsLocalPlayer) return;

        CaptureInput();
    }

    void CaptureInput()
    {
        // Continuous input
        HorizontalInput = Input.GetAxis("Horizontal");
        VerticalInput = Input.GetAxis("Vertical");
        JumpHeld = Input.GetButton("Jump");
        SprintHeld = Input.GetKey(KeyCode.LeftShift);
        MouseX = Input.GetAxis("Mouse X");
        MouseY = Input.GetAxis("Mouse Y");

        // Button down events
        if (Input.GetButtonDown("Jump"))
        {
            OnJumpPressed?.Invoke();
        }

        if (Input.GetMouseButtonDown(0))
        {
            OnShootPressed?.Invoke();
        }

        if (Input.GetKeyDown(KeyCode.LeftControl))
        {
            OnSlidePressed?.Invoke();
        }
    }
}