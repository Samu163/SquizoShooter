using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] public float maxHealth = 100f;
    [SerializeField] public float health = 100f;

    [Header("Movement Settings")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float jumpForce = 8f;
    [SerializeField] private float gravity = -20f;

    [Header("Camera Settings")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float mouseSensitivityX = 2f;
    [SerializeField] private float mouseSensitivityY = 2f;
    [SerializeField] private float verticalLimitMin = -90f;
    [SerializeField] private float verticalLimitMax = 90f;
    [SerializeField] private bool smoothMovement = true;
    [SerializeField] private float smoothing = 10f;

    [Header("Visual Settings")]
    [SerializeField] private GameObject visualModel;

    // Network/Multiplayer
    private UDPClient udpClient;
    private bool isLocalPlayer = false;
    public Camera playerCamera;

    // Death state
    private bool isDead = false;

    private CharacterController controller;
    private Vector3 verticalVelocity;
    private float rotationX = 0f;
    private float rotationY = 0f;
    private float targetRotationX = 0f;
    private float targetRotationY = 0f;
    private bool isGrounded;

    private Vector3 lastSentPosition;
    private Vector3 lastSentRotation;
    private float lastSentHealth;
    private const float positionThreshold = 0.01f;
    private const float rotationThreshold = 0.5f;
    private const float healthThreshold = 0.01f;

    

    public bool IsLocalPlayer => isLocalPlayer;
    public bool IsDead => isDead;

  
    void Start()
    {
        controller = GetComponent<CharacterController>();
        udpClient = FindObjectOfType<UDPClient>();
        //playerCamera = GetComponent<Camera>();

        //if (playerCamera == null && cameraTransform != null)
        //{
        //    playerCamera = cameraTransform.GetComponent<Camera>();
        //}
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
            if (playerCamera != null)
            {
                cameraTransform = playerCamera.transform;
            }
        }

        if (cameraTransform == null && playerCamera != null)
        {
            cameraTransform = playerCamera.transform;
        }

        if (visualModel == null)
        {
            // Buscar un hijo llamado "Model" o similar, o usar el primer hijo
            foreach (Transform child in transform)
            {
                if (child.name.Contains("Model") || child.name.Contains("Visual") || child.name.Contains("Mesh"))
                {
                    visualModel = child.gameObject;
                    break;
                }
            }

            if (visualModel == null && transform.childCount > 0)
            {
                foreach (Transform child in transform)
                {
                    if (child.GetComponent<Renderer>() != null)
                    {
                        visualModel = child.gameObject;
                        break;
                    }
                }
            }
        }

        // Disable camera initially
        if (playerCamera != null)
        {
            playerCamera.enabled = false;
        }

        // Init rotation
        if (cameraTransform != null)
        {
            rotationY = transform.eulerAngles.y;
            rotationX = cameraTransform.localEulerAngles.x;
            if (rotationX > 180f)
            {
                rotationX -= 360f;
            }
        }
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        if (isDead) return;

        if (playerCamera != null && !playerCamera.enabled)
        {
            playerCamera.enabled = true;
        }

        HandleMovement();
        HandleCamera();
        HandleJump();
        SendPositionToServer();
        SendRotationToServer();
        SendPlayerDataToServer();

        // Test: reducir vida con K
        if (Input.GetKeyDown(KeyCode.K))
        {
            TakeDamage(10f);
        }

        CheckDeath();
    }

    void HandleMovement()
    {
        isGrounded = controller.isGrounded;

        if (isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -2f;
        }

        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        Vector3 movement = transform.right * moveX + transform.forward * moveZ;

        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed;

        controller.Move(movement * currentSpeed * Time.deltaTime);

        verticalVelocity.y += gravity * Time.deltaTime;
        controller.Move(verticalVelocity * Time.deltaTime);
    }

    void HandleCamera()
    {
        if (cameraTransform == null) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivityX;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivityY;

        targetRotationY += mouseX;
        targetRotationX -= mouseY;
        targetRotationX = Mathf.Clamp(targetRotationX, verticalLimitMin, verticalLimitMax);

        if (smoothMovement)
        {
            rotationX = Mathf.Lerp(rotationX, targetRotationX, Time.deltaTime * smoothing);
            rotationY = Mathf.Lerp(rotationY, targetRotationY, Time.deltaTime * smoothing);
        }
        else
        {
            rotationX = targetRotationX;
            rotationY = targetRotationY;
        }

        transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
        cameraTransform.localRotation = Quaternion.Euler(rotationX, 0f, 0f);
    }

    void HandleJump()
    {
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            verticalVelocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
        }
    }

    void CheckDeath()
    {
        if (health <= 0f && !isDead)
        {
            Die();
        }
    }

    void Die()
    {
        isDead = true;
        health = 0f;
        Debug.Log("[PlayerController] Player died!");

        // Ocultar el modelo visual (mejor que desactivar renderers)
        if (visualModel != null)
        {
            visualModel.SetActive(false);
        }

        // Deshabilitar el CharacterController para evitar colisiones
        if (controller != null)
        {
            controller.enabled = false;
        }

        // Actualizar UI
        if (HealthBarUI.instance != null)
        {
            HealthBarUI.instance.UpdateUI(0f, maxHealth);
        }

        // Enviar muerte al servidor
        SendPlayerDataToServer();

        // Mostrar pantalla de muerte (solo si es jugador local)
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
        Debug.Log("[PlayerController] Respawning player...");

        // Resetear vida
        health = maxHealth;
        isDead = false;

        // Obtener nueva posicion de spawn
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

        Debug.Log($"[PlayerController] Target spawn position: {spawnPos}");

        // IMPORTANTE: Deshabilitar el controller ANTES de mover
        if (controller != null)
        {
            controller.enabled = false;
        }

        transform.position = spawnPos;

        Debug.Log($"[PlayerController] Position set to: {transform.position}");

        // Resetear velocidad vertical
        verticalVelocity = Vector3.zero;

        if (controller != null)
        {
            controller.enabled = true;
        }

        // Mostrar el modelo visual
        if (visualModel != null)
        {
            visualModel.SetActive(true);
        }

        // Actualizar UI
        if (HealthBarUI.instance != null)
        {
            HealthBarUI.instance.UpdateUI(health, maxHealth);
        }

        
        if (udpClient != null && udpClient.IsConnected)
        {
            lastSentPosition = Vector3.zero; 
            udpClient.SendCubeMovement(spawnPos);
            udpClient.SendPlayerHealth(health);
        }

        Debug.Log($"[PlayerController] Player respawned at {transform.position} with full health");
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        health -= damage;
        health = Mathf.Clamp(health, 0f, maxHealth);

        Debug.LogWarning($"[PlayerController] Player took {damage} damage. Health: {health}/{maxHealth}");

        if (HealthBarUI.instance != null)
        {
            HealthBarUI.instance.UpdateUI(health, maxHealth);
        }
    }

    void SendPlayerDataToServer()
    {
        if (udpClient != null && udpClient.IsConnected)
        {
            if (Mathf.Abs(health - lastSentHealth) > healthThreshold)
            {
                udpClient.SendPlayerHealth(health);
                lastSentHealth = health;
            }
        }
    }

    void SendPositionToServer()
    {
        if (udpClient != null && udpClient.IsConnected)
        {
            if (Vector3.Distance(transform.position, lastSentPosition) > positionThreshold)
            {
                udpClient.SendCubeMovement(transform.position);
                lastSentPosition = transform.position;
            }
        }
    }

    void SendRotationToServer()
    {
        if (udpClient != null && udpClient.IsConnected)
        {
            float yaw = transform.eulerAngles.y;
            Vector3 currentRotation = new Vector3(0, yaw, 0);

            if (Mathf.Abs(Mathf.DeltaAngle(lastSentRotation.y, currentRotation.y)) > rotationThreshold)
            {
                udpClient.SendCubeRotation(currentRotation);
                lastSentRotation = currentRotation;
            }
        }
    }

    public void SetAsLocalPlayer(bool isLocal)
    {
        isLocalPlayer = isLocal;

        if (playerCamera != null)
        {
            playerCamera.enabled = isLocal;
        }

        if (isLocal)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (HealthBarUI.instance != null)
            {
                health = maxHealth;
                HealthBarUI.instance.UpdateUI(health, maxHealth);
            }
            else
            {
                Debug.LogError("No se encontro la HealthBarUI!");
            }
        }

        enabled = true;
    }

    public void SetSensitivity(float sensX, float sensY)
    {
        mouseSensitivityX = sensX;
        mouseSensitivityY = sensY;
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
        health = newHealth;

        // Si es jugador remoto y la vida llega a 0, ocultar el modelo
        if (!isLocalPlayer)
        {
            if (health <= 0f && visualModel != null)
            {
                visualModel.SetActive(false);
            }
            else if (health > 0f && visualModel != null)
            {
                visualModel.SetActive(true);
            }
        }

        if (isLocalPlayer && HealthBarUI.instance != null)
        {
            HealthBarUI.instance.UpdateUI(health, maxHealth);
        }
    }
}