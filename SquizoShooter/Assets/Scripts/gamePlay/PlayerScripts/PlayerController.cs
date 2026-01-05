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
    private WeaponManager weaponManager;
    private PlayerAudioController audioController;
    private WeaponThrowSystem weaponThrowSystem;

    private CharacterController controller;
    private bool isLocalPlayer = false;
    private float _lastHealth = -1f;

    public bool IsLocalPlayer => isLocalPlayer;
    public bool IsDead => lifeComponent != null && lifeComponent.IsDead;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        lifeComponent = GetComponent<LifeComponent>();
        playerMovement = GetComponent<PlayerMovement>();
        wallJumpComponent = GetComponent<WallJumpComponent>();
        slideComponent = GetComponent<SlideComponent>();
        playerShooting = GetComponent<PlayerShooting>();
        playerSync = GetComponent<PlayerSync>();
        playerInput = GetComponent<PlayerInput>();
        playerVisuals = GetComponent<PlayerVisuals>();
        playerCamera = GetComponent<PlayerCamera>();
        weaponManager = GetComponent<WeaponManager>();
        weaponThrowSystem = GetComponent<WeaponThrowSystem>();

        if (lifeComponent == null) Debug.LogError("[PlayerController] LifeComponent is missing!");
        if (playerMovement == null) Debug.LogError("[PlayerController] PlayerMovement is missing!");
        if (wallJumpComponent == null) Debug.LogError("[PlayerController] WallRunComponent is missing!");
        if (slideComponent == null) Debug.LogError("[PlayerController] SlideComponent is missing!");
        if (playerShooting == null) Debug.LogError("[PlayerController] PlayerShooting is missing!");
        if (playerSync == null) Debug.LogError("[PlayerController] PlayerSync is missing!");
        if (playerInput == null) Debug.LogError("[PlayerController] PlayerInput is missing!");
        if (playerVisuals == null) Debug.LogError("[PlayerController] PlayerVisuals is missing!");
        if (playerCamera == null) Debug.LogError("[PlayerController] PlayerCamera is missing!");
        if (weaponManager == null) Debug.LogError("[PlayerController] WeaponManager is missing!");
        if (weaponThrowSystem == null) Debug.LogError("[PlayerController] WeaponThrowSystem is missing!");
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
        weaponManager.Initialize(this, playerSync, playerCamera);
        playerShooting.Initialize(this, weaponManager);
        playerSync.Initialize(this);
        playerInput.Initialize(this);
        playerVisuals.Initialize(transform);
        playerCamera.Initialize(transform, playerInput);
        audioController = GetComponent<PlayerAudioController>();
        weaponThrowSystem.Initialize(weaponManager, this, playerCamera, playerSync);
    }

    void SubscribeToInputEvents()
    {
        if (playerInput == null) return;

        playerInput.OnJumpPressed += HandleJumpPressed;
        playerInput.OnShootPressed += HandleShootPressed;
        playerInput.OnSlidePressed += HandleSlidePressed;
        playerInput.OnShootHeld += HandleShootHeld;
        playerInput.OnQPressed += HandleThrowWeapon;
        playerInput.OnEPressed += HandlePickUpWeapon;
    }

    void UnsubscribeFromInputEvents()
    {
        if (playerInput == null) return;

        playerInput.OnJumpPressed -= HandleJumpPressed;
        playerInput.OnShootPressed -= HandleShootPressed;
        playerInput.OnSlidePressed -= HandleSlidePressed;
        playerInput.OnShootHeld -= HandleShootHeld;
        playerInput.OnQPressed -= HandleThrowWeapon;
        playerInput.OnEPressed -= HandlePickUpWeapon;
    }

    void HandleJumpPressed()
    {
        if (IsDead) return;
        playerMovement.HandleJump(wallJumpComponent, slideComponent);
    }

    void HandleShootPressed()
    {
        if (IsDead) return;
        if (playerShooting != null)
        {
            playerShooting.TryShoot();
        }
    }

    void HandleShootHeld()
    {
        if (playerShooting != null)
        {
            playerShooting.TryShoot();
        }
    }

    void HandleSlidePressed()
    {
        if (IsDead) return;
        slideComponent.TryStartSlide();
    }

    void HandleThrowWeapon()
    {
        if (IsDead) return;

        if (weaponThrowSystem != null)
        {
            weaponThrowSystem.ThrowCurrentWeapon();
        }
        else
        {
            Debug.LogWarning("[PlayerController] WeaponThrowSystem no est� disponible para lanzar el arma.");
        }
    }

    void HandlePickUpWeapon()
    {
        if (IsDead) return;
        if (weaponThrowSystem != null)
        {
            weaponThrowSystem.TryPickupWeapon();
        }
        else
        {
            Debug.LogWarning("[PlayerController] WeaponThrowSystem no est� disponible para recoger el arma.");
        }
    }
    void Update()
    {
        if (!isLocalPlayer) return;

        if (!IsDead)
        {
            playerCamera.EnableCameraIfNeeded();
            wallJumpComponent.DetectWallRun();
            playerMovement.HandleMovement(wallJumpComponent, slideComponent);
            if (weaponManager != null)
            {
                weaponManager.HandleWeaponSwitchInput();
            }
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
        AudioListener listener = GetComponentInChildren<AudioListener>();
        if (listener != null)
        {
            listener.enabled = isLocal;
        }
        else
        {
            if (isLocal) Debug.LogWarning("�Cuidado! El Player Local no tiene AudioListener en sus hijos.");
        }
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

    public void HandleDeath()
    {
        if (audioController != null)
        {
            audioController.PlayDeath();
        }
        playerVisuals.HideModel();

        if (controller != null)
        {
            controller.enabled = false;
        }

        if (isLocalPlayer && KillCountUI.instance != null)
        {
            KillCountUI.instance.ResetKills();
        }

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

    public void Respawn()
    {
        if (lifeComponent) lifeComponent.ResetHealth();

        Vector3 spawnPos = Vector3.zero;

        if (GameplayManager.Instance != null)
        {
            UDPClient client = FindObjectOfType<UDPClient>();
            int finalIndex = 0;

            if (client != null)
            {
                finalIndex = client.MySpawnIndex + client.CurrentRoundOffset;
            }

            spawnPos = GameplayManager.Instance.GetSpawnPosition(finalIndex);
        }
        else
        {
            spawnPos = new Vector3(0, 2, 0);
        }

        if (controller != null) controller.enabled = false;
        transform.position = spawnPos;

        if (playerMovement) playerMovement.ResetVelocity();
        if (wallJumpComponent) wallJumpComponent.ResetState();
        if (slideComponent) slideComponent.ResetState();

        if (controller != null) controller.enabled = true;
        if (playerVisuals) playerVisuals.ShowModel();

        if (HealthBarUI.instance != null && lifeComponent != null)
            HealthBarUI.instance.UpdateUI(lifeComponent.Health, lifeComponent.MaxHealth);

        if (playerSync && lifeComponent)
            playerSync.ResetSync(spawnPos, lifeComponent.Health);
    }

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
        if (_lastHealth < 0)
        {
            _lastHealth = newHealth;
        }

        if (audioController != null)
        {
            if (newHealth < _lastHealth && newHealth > 0)
            {
                audioController.PlayDamage();
            }
            else if (newHealth > _lastHealth)
            {
                audioController.PlayHeal();
            }
        }

        lifeComponent.UpdateHealth(newHealth, isLocalPlayer);

        if (!isLocalPlayer)
        {
            playerVisuals.UpdateVisualOnHealth(newHealth);
        }

        if (isLocalPlayer && newHealth <= 0 && !IsDead)
        {
            HandleDeath();
        }

        _lastHealth = newHealth;
    }

    public void SwitchWeaponVisuals(int weaponID)
    {
        if (playerVisuals != null) playerVisuals.SetEquippedWeapon(weaponID);
    }

    public void PlayShootVisuals()
    {
        if (playerVisuals != null)
        {
            playerVisuals.PlayShootAnimation();
        }

        if (weaponManager != null)
        {
            BaseWeapon currentWeapon = weaponManager.GetCurrentWeapon();
            if (currentWeapon != null)
            {
                currentWeapon.SimulateShootVisualsForNetwork();
                if (audioController != null)
                {
                    audioController.PlayShoot(currentWeapon.WeaponID);
                }
            }
        }
    }

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
    public WeaponManager GetWeaponManager() => weaponManager;

    public WeaponThrowSystem GetWeaponThrowSystem() => weaponThrowSystem;
}