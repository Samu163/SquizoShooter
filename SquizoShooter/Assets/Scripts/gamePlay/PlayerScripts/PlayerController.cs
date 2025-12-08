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

        // Get components
        lifeComponent = GetComponent<LifeComponent>();
        playerMovement = GetComponent<PlayerMovement>();
        wallJumpComponent = GetComponent<WallJumpComponent>();
        slideComponent = GetComponent<SlideComponent>();
        playerShooting = GetComponent<PlayerShooting>();
        playerSync = GetComponent<PlayerSync>();
        playerInput = GetComponent<PlayerInput>();
        playerVisuals = GetComponent<PlayerVisuals>();
        playerCamera = GetComponent<PlayerCamera>();

        // Validate components
        if (lifeComponent == null) Debug.LogError("[PlayerController] LifeComponent is missing!");
        if (playerMovement == null) Debug.LogError("[PlayerController] PlayerMovement is missing!");
        if (wallJumpComponent == null) Debug.LogError("[PlayerController] WallRunComponent is missing!");
        if (slideComponent == null) Debug.LogError("[PlayerController] SlideComponent is missing!");
        if (playerShooting == null) Debug.LogError("[PlayerController] PlayerShooting is missing!");
        if (playerSync == null) Debug.LogError("[PlayerController] PlayerSync is missing!");
        if (playerInput == null) Debug.LogError("[PlayerController] PlayerInput is missing!");
        if (playerVisuals == null) Debug.LogError("[PlayerController] PlayerVisuals is missing!");
        if (playerCamera == null) Debug.LogError("[PlayerController] PlayerCamera is missing!");
    }

    void Start()
    {
        InitializeComponents();
        SubscribeToInputEvents();
    }

    void OnDestroy()
    {
        UnsubscribeFromInputEvents();
    }

    void InitializeComponents()
    {
        lifeComponent.Initialize(this);
        playerMovement.Initialize(controller, this);
        wallJumpComponent.Initialize(controller, this, playerMovement);
        slideComponent.Initialize(controller, this, playerMovement);
        playerShooting.Initialize(this, playerSync, playerCamera);
        playerSync.Initialize(this);
        playerInput.Initialize(this);
        playerVisuals.Initialize(transform);
        playerCamera.Initialize(transform, playerInput);
    }

    void SubscribeToInputEvents()
    {
        if (playerInput == null) return;

        playerInput.OnJumpPressed += HandleJumpPressed;
        playerInput.OnShootPressed += HandleShootPressed;
        playerInput.OnSlidePressed += HandleSlidePressed;
    }

    void UnsubscribeFromInputEvents()
    {
        if (playerInput == null) return;

        playerInput.OnJumpPressed -= HandleJumpPressed;
        playerInput.OnShootPressed -= HandleShootPressed;
        playerInput.OnSlidePressed -= HandleSlidePressed;
    }

    void HandleJumpPressed()
    {
        if (IsDead) return;
        playerMovement.HandleJump(wallJumpComponent, slideComponent);
    }

    void HandleShootPressed()
    {
        if (IsDead) return;
        playerShooting.TryShoot();
    }

    void HandleSlidePressed()
    {
        if (IsDead) return;
        slideComponent.TryStartSlide();
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        if (!IsDead)
        {
            playerCamera.EnableCameraIfNeeded();
            wallJumpComponent.DetectWallRun();
            playerMovement.HandleMovement(wallJumpComponent, slideComponent);
            playerSync.SendPositionToServer();
            playerSync.SendRotationToServer();
            playerSync.SendPlayerDataToServer();
            if (Input.GetKeyDown(KeyCode.K))
            {
                lifeComponent.TakeDamage(10f);
            }
            slideComponent.UpdateTimer();
            wallJumpComponent.UpdateTimer();
        }
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
            transform.rotation = Quaternion.Euler(0, rotation.y, 0);

            playerVisuals.UpdateAiming(rotation.x);
        }
    }

    public void UpdateHealth(float newHealth)
    {
        
        lifeComponent.UpdateHealth(newHealth, isLocalPlayer);
        if (!isLocalPlayer)
        {
            playerVisuals.UpdateVisualOnHealth(newHealth);
        }
        if (isLocalPlayer && newHealth <= 0 && !IsDead)
        {
            Debug.Log("[PlayerController] Salud llegó a 0 por red. Ejecutando muerte.");
            HandleDeath();
        }
    }

    // Getters
    public CharacterController GetController() => controller;
    public LifeComponent GetLifeComponent() => lifeComponent;
    public PlayerMovement GetMovement() => playerMovement;
    public PlayerSync GetSync() => playerSync;
    public PlayerVisuals GetVisuals() => playerVisuals;
    public PlayerInput GetInput() => playerInput;
    public PlayerCamera GetPlayerCamera() => playerCamera;
    public WallJumpComponent GetWallJumpComponent() => wallJumpComponent;
    public SlideComponent GetSlideComponent() => slideComponent;
    public PlayerShooting GetPlayerShooting() => playerShooting;
}