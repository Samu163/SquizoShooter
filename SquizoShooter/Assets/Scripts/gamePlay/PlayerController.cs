using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Components")]
    private LifeComponent lifeComponent;
    private PlayerMovement playerMovement;
    private WallJumpComponent wallJumpComponent;
    private SlideComponent slideComponent;
    private PlayerShooting playerShooting;
    private PlayerSync playerSync;
    private PlayerInput playerInput;
    private PlayerVisuals playerVisuals;
    private PlayerCamera playerCamera;

    private CharacterController controller;
    private bool isLocalPlayer = false;

    public bool IsLocalPlayer => isLocalPlayer;
    public bool IsDead => lifeComponent != null && lifeComponent.IsDead;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        // Get or add components
        lifeComponent = GetComponent<LifeComponent>();
        if (lifeComponent == null) lifeComponent = gameObject.AddComponent<LifeComponent>();

        playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement == null) playerMovement = gameObject.AddComponent<PlayerMovement>();

        wallJumpComponent = GetComponent<WallJumpComponent>();
        if (wallJumpComponent == null) wallJumpComponent = gameObject.AddComponent<WallJumpComponent>();

        slideComponent = GetComponent<SlideComponent>();
        if (slideComponent == null) slideComponent = gameObject.AddComponent<SlideComponent>();

        playerShooting = GetComponent<PlayerShooting>();
        if (playerShooting == null) playerShooting = gameObject.AddComponent<PlayerShooting>();

        playerSync = GetComponent<PlayerSync>();
        if (playerSync == null) playerSync = gameObject.AddComponent<PlayerSync>();

        playerInput = GetComponent<PlayerInput>();
        if (playerInput == null) playerInput = gameObject.AddComponent<PlayerInput>();

        playerVisuals = GetComponent<PlayerVisuals>();
        if (playerVisuals == null) playerVisuals = gameObject.AddComponent<PlayerVisuals>();

        playerCamera = GetComponent<PlayerCamera>();
        if (playerCamera == null) playerCamera = gameObject.AddComponent<PlayerCamera>();
    }

    void Start()
    {
        InitializeComponents();
    }

    void InitializeComponents()
    {
        // Initialize Life Component
        lifeComponent.Initialize(this);

        // Initialize Movement
        playerMovement.Initialize(controller, this);

        // Initialize WallRun
        wallJumpComponent.Initialize(controller, this);

        // Initialize Slide
        slideComponent.Initialize(controller, this);

        // Initialize Shooting
        playerShooting.Initialize(this, playerSync, playerCamera);

        // Initialize Sync
        playerSync.Initialize(this);

        // Initialize Input
        playerInput.Initialize(this);

        // Initialize Visuals
        playerVisuals.Initialize(transform);

        // Initialize Camera
        playerCamera.Initialize(transform);
    }

    void Update()
    {
        if (!isLocalPlayer) return;
        if (IsDead) return;

        playerCamera.EnableCameraIfNeeded();

        // Input handling
        playerInput.HandleInput();

        // Wall detection and slide input
        wallJumpComponent.DetectWallRun(playerInput);
        slideComponent.HandleSlideInput(playerInput);

        // Movement and physics
        playerMovement.HandleMovement(playerInput, wallJumpComponent, slideComponent);
        playerCamera.HandleCamera(playerInput);
        playerMovement.HandleJump(playerInput, wallJumpComponent, slideComponent);

        // Shooting
        playerShooting.HandleShooting(playerInput, playerCamera.CameraTransform);

        // Sync with server
        playerSync.SendPositionToServer();
        playerSync.SendRotationToServer();
        playerSync.SendPlayerDataToServer();

        // Test damage with K key
        if (Input.GetKeyDown(KeyCode.K))
        {
            lifeComponent.TakeDamage(10f);
        }

        // Update timers
        slideComponent.UpdateTimer();
        wallJumpComponent.UpdateTimer();
    }

    public void SetAsLocalPlayer(bool isLocal)
    {
        isLocalPlayer = isLocal;

        playerCamera.SetAsLocalCamera(isLocal);
        playerVisuals.SetAsLocalPlayer(isLocal);

        if (isLocal)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            lifeComponent.InitializeHealthUI();
        }

        enabled = true;
    }

    public void SetSensitivity(float sensX, float sensY)
    {
        playerCamera.SetSensitivity(sensX, sensY);
    }

    // Death handling
    public void HandleDeath()
    {
        playerVisuals.HideModel();

        if (controller != null)
        {
            controller.enabled = false;
        }

        if (isLocalPlayer && KillCountUI.instance != null)
        {
            KillCountUI.instance.ResetKills();
            Debug.Log("[PlayerController] Kills reseteados al morir");
        }

        // Send death to server
        playerSync.SendPlayerDataToServer();

        if (isLocalPlayer)
        {
            UiController uiController = FindObjectOfType<UiController>();
            if (uiController != null)
            {
                uiController.ShowDeathScreen();
            }
        }
    }

    // Respawn handling
    public void Respawn()
    {
        lifeComponent.ResetHealth();

        Vector3 spawnPos = Vector3.zero;
        if (GameplayManager.Instance != null)
        {
            spawnPos = GameplayManager.Instance.GetRandomSpawnPosition();
        }
        else
        {
            spawnPos = new Vector3(
                Random.Range(-10f, 10f),
                1f,
                Random.Range(-10f, 10f)
            );
        }

        if (controller != null)
        {
            controller.enabled = false;
        }

        transform.position = spawnPos;

        playerMovement.ResetVelocity();
        wallJumpComponent.ResetState();
        slideComponent.ResetState();

        if (controller != null)
        {
            controller.enabled = true;
        }

        playerVisuals.ShowModel();

        if (HealthBarUI.instance != null)
        {
            HealthBarUI.instance.UpdateUI(lifeComponent.Health, lifeComponent.MaxHealth);
        }

        playerSync.ResetSync(spawnPos, lifeComponent.Health);
    }

    // Public API for remote players
    public void PlayShootAnimation()
    {
        playerVisuals.PlayShootAnimation();
    }

    public void UpdatePosition(Vector3 position)
    {
        if (!isLocalPlayer)
        {
            transform.position = position;
        }
    }

    public void UpdateRotation(Vector3 rotation)
    {
        if (!isLocalPlayer)
        {
            transform.rotation = Quaternion.Euler(rotation);
        }
    }

    public void UpdateHealth(float newHealth)
    {
        lifeComponent.UpdateHealth(newHealth, isLocalPlayer);

        if (!isLocalPlayer)
        {
            playerVisuals.UpdateVisualOnHealth(newHealth);
        }
    }

    // Getters
    public CharacterController GetController() => controller;
    public LifeComponent GetLifeComponent() => lifeComponent;
    public PlayerMovement GetMovement() => playerMovement;
    public PlayerSync GetSync() => playerSync;
    public PlayerVisuals GetVisuals() => playerVisuals;
    public SlideComponent GetSlideComponent() => slideComponent;
    public WallJumpComponent GetWallJumpComponent() => wallJumpComponent;
    public PlayerShooting GetShootingComponent() => playerShooting;
    public PlayerCamera GetPlayerCamera() => playerCamera;
    public PlayerInput GetPlayerInput() => playerInput;
}