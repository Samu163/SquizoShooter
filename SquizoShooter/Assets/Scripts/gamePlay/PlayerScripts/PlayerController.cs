using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Components")]
    private LifeComponent lifeComponent;
    private PlayerMovement playerMovement;
    private WallJumpComponent wallRunComponent;
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

        //Añadir logica paraquitar componentes si no es local player
        lifeComponent = GetComponent<LifeComponent>();
        if (lifeComponent == null) lifeComponent = gameObject.AddComponent<LifeComponent>();

        playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement == null) playerMovement = gameObject.AddComponent<PlayerMovement>();

        wallRunComponent = GetComponent<WallJumpComponent>();
        if (wallRunComponent == null) wallRunComponent = gameObject.AddComponent<WallJumpComponent>();

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
        wallRunComponent.Initialize(controller, this, playerMovement);
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
        playerMovement.HandleJump(wallRunComponent, slideComponent);
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
        if (IsDead) return;

        playerCamera.EnableCameraIfNeeded();
        wallRunComponent.DetectWallRun();
        playerMovement.HandleMovement(wallRunComponent, slideComponent);
        playerSync.SendPositionToServer();
        playerSync.SendRotationToServer();
        playerSync.SendPlayerDataToServer();

        // Test damage with K key
        if (Input.GetKeyDown(KeyCode.K))
        {
            lifeComponent.TakeDamage(10f);
        }

        slideComponent.UpdateTimer();
        wallRunComponent.UpdateTimer();
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
        wallRunComponent.ResetState();
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
    public PlayerInput GetInput() => playerInput;
    public PlayerCamera GetPlayerCamera() => playerCamera;
    public WallJumpComponent GetWallJumpComponent() => wallRunComponent;
    public SlideComponent GetSlideComponent() => slideComponent;

}