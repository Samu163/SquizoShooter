using UnityEngine;

public class SlideComponent : MonoBehaviour
{
    [Header("Slide Settings")]
    [SerializeField] private float slideDuration = 0.9f;
    [SerializeField] private float slideSpeedMultiplier = 1.6f;
    [SerializeField] private float slideHeightMultiplier = 0.5f;
    [SerializeField] private float slideMinSpeed = 7f;

    private CharacterController controller;
    private PlayerController playerController;

    private bool isSliding = false;
    private float slideTimer = 0f;
    private float originalControllerHeight;
    private Vector3 originalControllerCenter;

    public bool IsSliding => isSliding;
    public float SlideSpeedMultiplier => slideSpeedMultiplier;

    public void Initialize(CharacterController ctrl, PlayerController pc)
    {
        controller = ctrl;
        playerController = pc;

        if (controller != null)
        {
            originalControllerHeight = controller.height;
            originalControllerCenter = controller.center;
        }
    }

    public void HandleSlideInput(PlayerInput input)
    {
        if (isSliding) return;

        if (!input.SlidePressed) return;

        PlayerMovement movement = playerController.GetMovement();
        if (movement == null) return;

        float horizontalSpeed = movement.GetHorizontalSpeed();

        if (horizontalSpeed >= slideMinSpeed)
        {
            StartSlide();
        }
    }

    void StartSlide()
    {
        if (controller == null) return;

        isSliding = true;
        slideTimer = slideDuration;

        controller.height = Mathf.Max(0.1f, originalControllerHeight * slideHeightMultiplier);
        Vector3 c = controller.center;
        c.y = originalControllerCenter.y - (originalControllerHeight * (1f - slideHeightMultiplier) * 0.5f);
        controller.center = c;
    }

    public void EndSlide()
    {
        if (controller == null) return;
        if (!isSliding) return;

        isSliding = false;
        controller.height = originalControllerHeight;
        controller.center = originalControllerCenter;
    }

    public void UpdateTimer()
    {
        if (isSliding)
        {
            slideTimer -= Time.deltaTime;
            if (slideTimer <= 0f)
            {
                EndSlide();
            }
        }
    }

    public void ResetState()
    {
        if (isSliding)
        {
            EndSlide();
        }
        slideTimer = 0f;
    }
}