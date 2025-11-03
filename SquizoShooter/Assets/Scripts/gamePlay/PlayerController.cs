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

    [Header("Shooting")]
    [SerializeField] private float shootRange = 100f;
    [SerializeField] private float shootDamage = 25f;
    [SerializeField] private float fireRate = 4f; // disparos por segundo
    [SerializeField] private float recoilPitch = 5f; // subida de cámara
    [SerializeField] private float recoilYaw = 1f;   // desviación horizontal
    [SerializeField] private float recoilBack = 0.2f; // pequeño retroceso físico

    [Header("Gizmos (editor)")]
    [SerializeField] private bool showShootGizmos = true;
    [SerializeField] private Color gizmoLineColor = Color.red;
    [SerializeField] private Color gizmoHitColor = Color.yellow;
    [SerializeField] private float gizmoHitRadius = 0.25f;

    [Header("Wallride Settings")]
    [SerializeField] private float wallCheckDistance = 0.8f;
    [SerializeField] private float wallSlideFallSpeed = -2f; // velocidad al deslizar por la pared frontal
    [SerializeField] private float wallJumpPush = 6f; // impulso alejado de la pared
    [SerializeField] private float wallRunMaxTime = 1.5f;
    [SerializeField] private float wallRunGravity = -2f;
    [SerializeField] private float wallRunMinForward = 0.2f; // input mínimo hacia delante para iniciar wallrun
    [SerializeField] private float wallRunSpeed = 5f;

    [SerializeField] private float minWallAttachFallSpeed = -0.5f; // debes estar cayendo al menos este valor para enganchar
    [SerializeField] private float wallReattachCooldown = 0.25f;   // tiempo mínimo tras wall-jump para volver a enganchar

    [SerializeField] private float wallAttachWindow = 0.35f;

    [Header("Slide Settings")]
    [SerializeField] private float slideDuration = 0.9f;
    [SerializeField] private float slideSpeedMultiplier = 1.6f;
    [SerializeField] private float slideHeightMultiplier = 0.5f;

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

    // Shooting cadence
    private float lastFireTime = 0f;

    // Wall states
    private bool isWallRiding = false;   // frontal wall slide
    private bool isWallRunning = false;  // lateral wallrun
    private Vector3 currentWallNormal = Vector3.zero;
    private Vector3 lastWallNormal = Vector3.zero;
    private float wallRunTimer = 0f;
    private int wallRunSide = 0; // -1 left, 1 right

    // Para evitar re-attach tras wall-jump
    private float lastWallJumpTime = -10f;
    private Vector3 lastWallJumpNormal = Vector3.zero;
    private const float sameWallNormalTolerance = 0.1f; // tolerancia para comparar normales

    // Para ventana post-salto
    private float lastJumpTime = -10f;

    // Sliding state
    private bool isSliding = false;
    private float slideTimer = 0f;
    private float originalControllerHeight;
    private Vector3 originalControllerCenter;

    public bool IsLocalPlayer => isLocalPlayer;
    public bool IsDead => isDead;


    void Start()
    {
        controller = GetComponent<CharacterController>();
        udpClient = FindObjectOfType<UDPClient>();

        if (controller != null)
        {
            originalControllerHeight = controller.height;
            originalControllerCenter = controller.center;
        }

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

        if (playerCamera != null)
        {
            playerCamera.enabled = false;
        }

        if (cameraTransform != null)
        {
            rotationY = transform.eulerAngles.y;
            rotationX = cameraTransform.localEulerAngles.x;
            if (rotationX > 180f)
            {
                rotationX -= 360f;
            }

            targetRotationX = rotationX;
            targetRotationY = rotationY;
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

        isGrounded = controller.isGrounded;

        // Inputs y estados
        HandleSlideInput();
        DetectWallRun();      // detecta y activa wallrun
        HandleShooting();

        // Movimiento y física
        HandleMovement();
        HandleCamera();
        HandleJump(); // maneja salto normal y salto desde slide
        SendPositionToServer();
        SendRotationToServer();
        SendPlayerDataToServer();

        // Test: reducir vida con K
        if (Input.GetKeyDown(KeyCode.K))
        {
            TakeDamage(10f);
        }

        CheckDeath();

        // Update sliding movement timer (if sliding)
        if (isSliding)
        {
            slideTimer -= Time.deltaTime;
            if (slideTimer <= 0f)
            {
                EndSlide();
            }
        }

        // Update wallrun timer
        if (isWallRunning)
        {
            wallRunTimer += Time.deltaTime;
            if (wallRunTimer >= wallRunMaxTime)
            {
                EndWallRun();
            }
        }
    }

    void HandleShooting()
    {
        if (playerCamera == null || udpClient == null) return;

        // Solo izquierdo del ratón
        if (Input.GetMouseButtonDown(0))
        {
            float cooldown = 1f / Mathf.Max(0.0001f, fireRate);
            if (Time.time - lastFireTime < cooldown) return;
            lastFireTime = Time.time;

            TryShoot();
            ApplyRecoil();
        }
    }

    void TryShoot()
    {
        if (cameraTransform == null) return;

        Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, shootRange);
        if (hits == null || hits.Length == 0) return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            // Si nos topamos con una pared (tag "Wall") el disparo se bloquea ahí
            if (hit.collider != null && hit.collider.CompareTag("Wall"))
            {
                Debug.Log("[PlayerController] Disparo bloqueado por Wall en " + hit.point);
                return;
            }

            // Buscar jugador en jerarquía
            Transform t = hit.collider.transform;
            while (t != null && !t.CompareTag("Player"))
            {
                t = t.parent;
            }

            if (t != null)
            {
                GameObject hitPlayerGO = t.gameObject;

                if (udpClient == null)
                {
                    Debug.LogWarning("[PlayerController] No hay UDPClient para enviar SHOT.");
                    return;
                }

                string targetKey = udpClient.GetKeyForGameObject(hitPlayerGO);
                if (string.IsNullOrEmpty(targetKey))
                {
                    Debug.LogWarning("[PlayerController] Disparo: no existe key para el GameObject golpeado.");
                    return;
                }

                // Evitar autodaño: si la key objetivo es la misma que la nuestra, ignorar
                if (udpClient.ClientKey == targetKey)
                {
                    Debug.Log("[PlayerController] Disparo ignorado: targetKey == ClientKey (self-hit).");
                    return;
                }

                // Enviar disparo al servidor (servidor aplicará daño y retransmitirá PLAYERDATA)
                udpClient.SendShotToServer(targetKey, shootDamage);
                return;
            }

            // Si el hit no es ni Wall ni Player, lo ignoramos y seguimos buscando el siguiente hit
        }
    }

    void ApplyRecoil()
    {
        // Modificar rotaciones objetivo para que HandleCamera haga la interpolación visible
        targetRotationY += Random.Range(-recoilYaw, recoilYaw);
        targetRotationX = Mathf.Clamp(targetRotationX - recoilPitch, verticalLimitMin, verticalLimitMax);

        // Aplicar un pequeño retroceso físico inmediato
        if (controller != null)
        {
            controller.Move(-transform.forward * recoilBack);
        }
    }

    void HandleMovement()
    {
        // Si estamos deslizando, movemos hacia delante con fuerza de slide y no procesamos input normal
        if (isSliding)
        {
            Vector3 slideMove = transform.forward * (sprintSpeed * slideSpeedMultiplier);
            // conservar la componente vertical (gravedad/wallrun)
            slideMove.y = verticalVelocity.y;
            controller.Move(slideMove * Time.deltaTime);
            // aplicar gravedad manualmente si no wallrunning
            if (!isWallRunning)
                verticalVelocity.y += gravity * Time.deltaTime;
            return;
        }

        // Movimiento regular
        isGrounded = controller.isGrounded;

        if (isGrounded && verticalVelocity.y < 0)
        {
            verticalVelocity.y = -2f;
        }

        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        // Si estamos wallrunning, forzamos movimiento a lo largo de la pared
        if (isWallRunning)
        {
            Vector3 wallForward = Vector3.Cross(lastWallNormal, Vector3.up).normalized;
            if (Vector3.Dot(wallForward, transform.forward) < 0f) wallForward = -wallForward;

            Vector3 moveDir = wallForward * Mathf.Max(0f, moveZ);
            controller.Move(moveDir * wallRunSpeed * Time.deltaTime);

            // reducir gravedad
            verticalVelocity.y = wallRunGravity;
            controller.Move(verticalVelocity * Time.deltaTime);
            return;
        }

        Vector3 movement = transform.right * moveX + transform.forward * moveZ;

        float currentSpeed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed;

        controller.Move(movement * currentSpeed * Time.deltaTime);

        // Aplicar gravedad (si no estamos en wallride)
        if (!isWallRiding)
        {
            verticalVelocity.y += gravity * Time.deltaTime;
        }
        else
        {
            // wall slide frontal: limitar caída
            verticalVelocity.y = Mathf.Max(verticalVelocity.y, wallSlideFallSpeed);
        }

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

    void DetectWallRun()
    {
        // Reset wallrun when grounded
        if (isGrounded)
        {
            if (isWallRunning) EndWallRun();
            isWallRiding = false;
            currentWallNormal = Vector3.zero;
            return;
        }

        // detect frontal wall for slide
        Vector3 origin = transform.position + Vector3.up * (controller.height * 0.5f);
        Vector3 forward = transform.forward;
        if (Physics.Raycast(origin, forward, out RaycastHit frontHit, wallCheckDistance))
        {
            if (frontHit.collider.CompareTag("Wall"))
            {
                // frontal wall slide: permitir si estamos cayendo suficiente O si estamos dentro de la ventana post-salto
                if (verticalVelocity.y < minWallAttachFallSpeed || (Time.time - lastJumpTime) <= wallAttachWindow)
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

        // Detect walls on left/right for wallrun
        float forwardInput = Input.GetAxis("Vertical");
        if (forwardInput > wallRunMinForward && !isGrounded && (verticalVelocity.y < minWallAttachFallSpeed || (Time.time - lastJumpTime) <= wallAttachWindow))
        {
            // check right
            Vector3 rightOrigin = origin;
            if (Physics.Raycast(rightOrigin, transform.right, out RaycastHit rightHit, wallCheckDistance))
            {
                if (rightHit.collider.CompareTag("Wall"))
                {
                    // evitar re-attach inmediato al mismo wall desde el mismo normal
                    if (Time.time - lastWallJumpTime > wallReattachCooldown && !IsSameWallNormal(lastWallJumpNormal, rightHit.normal))
                    {
                        StartWallRun(rightHit.normal, 1);
                        return;
                    }
                }
            }

            // check left
            if (Physics.Raycast(origin, -transform.right, out RaycastHit leftHit, wallCheckDistance))
            {
                if (leftHit.collider.CompareTag("Wall"))
                {
                    if (Time.time - lastWallJumpTime > wallReattachCooldown && !IsSameWallNormal(lastWallJumpNormal, leftHit.normal))
                    {
                        StartWallRun(leftHit.normal, -1);
                        return;
                    }
                }
            }
        }

        // If no lateral walls detected, end wallrun
        if (isWallRunning)
        {
            // small cooldown to prevent flicker
            if (wallRunTimer > 0.15f && !IsNearWallSide()) EndWallRun();
        }
    }

    bool IsSameWallNormal(Vector3 a, Vector3 b)
    {
        return Vector3.Distance(a, b) < sameWallNormalTolerance;
    }

    bool IsNearWallSide()
    {
        Vector3 origin = transform.position + Vector3.up * (controller.height * 0.5f);
        if (Physics.Raycast(origin, transform.right, out RaycastHit r, wallCheckDistance) && r.collider.CompareTag("Wall")) return true;
        if (Physics.Raycast(origin, -transform.right, out RaycastHit l, wallCheckDistance) && l.collider.CompareTag("Wall")) return true;
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
        // optionally adjust verticalVelocity to reduce drop
        verticalVelocity.y = Mathf.Max(verticalVelocity.y, wallRunGravity);
    }

    void EndWallRun()
    {
        isWallRunning = false;
        wallRunTimer = 0f;
        wallRunSide = 0;
        lastWallNormal = Vector3.zero;
    }

    void HandleJump()
    {
        // Wall jump: si estamos wallrunning o en wall slide frontal, y solo si estamos cayendo o hemos saltado recientemente
        if ((isWallRunning || isWallRiding) && Input.GetButtonDown("Jump") && (verticalVelocity.y < 0f || (Time.time - lastJumpTime) <= wallAttachWindow))
        {
            // salto normal vertical
            verticalVelocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);

            // Empuje lateral: si wallrunning, empujar fuera de la pared
            Vector3 push = Vector3.zero;
            if (isWallRunning)
            {
                push = lastWallNormal * wallJumpPush;
                EndWallRun();
            }
            else if (isWallRiding)
            {
                push = currentWallNormal * wallJumpPush;
                isWallRiding = false;
            }

            // Registrar wall-jump para evitar re-attach inmediato
            lastWallJumpTime = Time.time;
            // Guardar la normal de la pared desde la que saltamos (no la inversa)
            if (isWallRunning)
                lastWallJumpNormal = lastWallNormal.normalized;
            else if (isWallRiding)
                lastWallJumpNormal = currentWallNormal.normalized;
            else
                lastWallJumpNormal = Vector3.zero;

            // Registrar como salto reciente también
            lastJumpTime = Time.time;

            controller.Move(push * Time.deltaTime);
            return;
        }

        // Salto normal si estamos en suelo y pulsamos Jump
        if (Input.GetButtonDown("Jump") && isGrounded)
        {
            verticalVelocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            lastJumpTime = Time.time; // registrar salto desde suelo
        }

        // salto durante slide
        if (isSliding && Input.GetButtonDown("Jump"))
        {
            EndSlide();
            verticalVelocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            lastJumpTime = Time.time; // registrar salto desde slide
        }
    }

    void HandleSlideInput()
    {
        // Iniciar slide: debe estar en suelo, correr (LeftShift) y pulsar LeftControl
        if (!isSliding && isGrounded && Input.GetKeyDown(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift))
        {
            StartSlide();
        }
    }

    void StartSlide()
    {
        if (controller == null) return;

        isSliding = true;
        slideTimer = slideDuration;

        // Reducir la altura del CharacterController para "agacharse"
        controller.height = Mathf.Max(0.1f, originalControllerHeight * slideHeightMultiplier);
        Vector3 c = controller.center;
        c.y = originalControllerCenter.y - (originalControllerHeight * (1f - slideHeightMultiplier) * 0.5f);
        controller.center = c;
    }

    void EndSlide()
    {
        if (controller == null) return;

        isSliding = false;
        // Restaurar altura y centro
        controller.height = originalControllerHeight;
        controller.center = originalControllerCenter;
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

        if (visualModel != null)
        {
            visualModel.SetActive(false);
        }

        if (controller != null)
        {
            controller.enabled = false;
        }

        if (HealthBarUI.instance != null)
        {
            HealthBarUI.instance.UpdateUI(0f, maxHealth);
        }

        if (isLocalPlayer && KillCountUI.instance != null)
        {
            KillCountUI.instance.ResetKills();
            Debug.Log("[PlayerController] Kills reseteados al morir");
        }

        // Enviar muerte al servidor
        SendPlayerDataToServer();

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
        health = maxHealth;
        isDead = false;
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

        verticalVelocity = Vector3.zero;

        if (controller != null)
        {
            controller.enabled = true;
        }

        if (visualModel != null)
        {
            visualModel.SetActive(true);
        }

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

    void OnDrawGizmos()
    {
        if (!showShootGizmos) return;

        if (Application.isPlaying && !isLocalPlayer) return;

        Transform camTransform = cameraTransform;
        if (camTransform == null && playerCamera != null) camTransform = playerCamera.transform;
        if (camTransform == null) return;

        Vector3 origin = camTransform.position;
        Vector3 dir = camTransform.forward;

        RaycastHit[] hits = Physics.RaycastAll(origin, dir, shootRange);
        if (hits != null && hits.Length > 0)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var hit in hits)
            {
                if (hit.collider != null && hit.collider.CompareTag("Wall"))
                {
                    Gizmos.color = gizmoLineColor;
                    Gizmos.DrawLine(origin, hit.point);
                    Gizmos.color = Color.black;
                    Gizmos.DrawWireSphere(hit.point, gizmoHitRadius);
                    return;
                }

                Transform t = hit.collider.transform;
                while (t != null && !t.CompareTag("Player"))
                    t = t.parent;
                if (t != null)
                {
                    Gizmos.color = gizmoLineColor;
                    Gizmos.DrawLine(origin, hit.point);
                    Gizmos.color = gizmoHitColor;
                    Gizmos.DrawWireSphere(hit.point, gizmoHitRadius);
                    return;
                }
            }
        }

        Gizmos.color = gizmoLineColor;
        Gizmos.DrawLine(origin, origin + dir * shootRange);
    }
}