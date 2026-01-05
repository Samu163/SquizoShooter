using UnityEngine;

public class WeaponThrowSystem : MonoBehaviour
{
    [Header("Throw Settings")]
    [SerializeField] private float throwForwardForce = 10f;
    [SerializeField] private float throwUpwardForce = 4f;
    [SerializeField] private float throwTorque = 8f;
    [SerializeField] private float despawnTime = 30f;
    [SerializeField] private float gravity = 2f;

    [Header("Pickup Settings")]
    [SerializeField] private float pickupRadius = 2f;
    [SerializeField] private LayerMask playerLayer;

    [Header("Prefabs")]
    [SerializeField] private GameObject pistolDropPrefab;
    [SerializeField] private GameObject minigunDropPrefab;
    [SerializeField] private GameObject shotgunDropPrefab;

    private WeaponManager weaponManager;
    private PlayerController playerController;
    private PlayerCamera playerCamera;
    private PlayerSync playerSync;

    public void Initialize(WeaponManager wm, PlayerController pc, PlayerCamera cam, PlayerSync sync)
    {
        weaponManager = wm;
        playerController = pc;
        playerCamera = cam;
        playerSync = sync;
    }

    public void ThrowCurrentWeapon()
    {
        if (weaponManager == null || playerController == null) return;
        if (playerController.IsDead) return;

        BaseWeapon currentWeapon = weaponManager.GetCurrentWeapon();
        if (currentWeapon == null) return;

        int weaponID = currentWeapon.WeaponID;
        GameObject dropPrefab = GetDropPrefabForWeapon(weaponID);

        if (dropPrefab == null)
        {
            Debug.LogWarning($"[WeaponThrowSystem] No drop prefab for weapon ID {weaponID}");
            return;
        }

        // Get throw position and direction
        Vector3 throwPosition = playerCamera.CameraTransform.position + playerCamera.CameraTransform.forward * 0.5f;
        Vector3 throwDirection = playerCamera.CameraTransform.forward;

        // Spawn dropped weapon locally
        SpawnDroppedWeapon(weaponID, throwPosition, throwDirection);

        // Desequipar el arma actual dejando al jugador sin arma
        weaponManager.UnequipCurrentWeapon();

        // Sync with server
        if (playerSync != null && playerController.IsLocalPlayer)
        {
            playerSync.SendWeaponThrow(weaponID, throwPosition, throwDirection);
        }

        Debug.Log($"[WeaponThrowSystem] Threw weapon {weaponID}, player now has no weapon");
    }

    // Called by network when remote player throws weapon
    public void HandleRemoteWeaponThrow(int weaponID, Vector3 position, Vector3 direction)
    {
        SpawnDroppedWeapon(weaponID, position, direction);
        Debug.Log($"[WeaponThrowSystem] Remote weapon throw: ID {weaponID}");
    }

    private void SpawnDroppedWeapon(int weaponID, Vector3 position, Vector3 direction)
    {
        GameObject dropPrefab = GetDropPrefabForWeapon(weaponID);
        if (dropPrefab == null) return;

        GameObject droppedWeapon = Instantiate(dropPrefab, position, Quaternion.identity);
        DroppedWeapon dropComponent = droppedWeapon.GetComponent<DroppedWeapon>();

        if (dropComponent != null)
        {
            dropComponent.Initialize(weaponID, despawnTime);
        }

        // Apply physics for parabolic trajectory
        Rigidbody rb = droppedWeapon.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Usar gravedad custom para un mejor control del arco
            rb.useGravity = true;
            rb.mass = 1.5f;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.8f;

            // Fuerza hacia adelante + arriba para tiro parab�lico
            Vector3 throwVector = direction.normalized * throwForwardForce + Vector3.up * throwUpwardForce;
            rb.linearVelocity = throwVector;

            // Rotaci�n aleatoria para efecto visual
            Vector3 randomTorque = new Vector3(
                Random.Range(-throwTorque, throwTorque),
                Random.Range(-throwTorque, throwTorque),
                Random.Range(-throwTorque, throwTorque)
            );
            rb.angularVelocity = randomTorque;
        }
    }

    private GameObject GetDropPrefabForWeapon(int weaponID)
    {
        switch (weaponID)
        {
            case 1: return pistolDropPrefab;
            case 2: return shotgunDropPrefab;
            case 3: return minigunDropPrefab;
            default: return null;
        }
    }

    public void TryPickupWeapon()
    {
        if (playerController == null || !playerController.IsLocalPlayer) return;
        if (playerController.IsDead) return;

        Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, pickupRadius);

        foreach (Collider col in nearbyObjects)
        {
            DroppedWeapon dropped = col.GetComponent<DroppedWeapon>();
            if (dropped != null && dropped.CanPickup)
            {
                PickupWeapon(dropped);
                break;
            }
        }
    }

    private void PickupWeapon(DroppedWeapon dropped)
    {
        int weaponID = dropped.WeaponID;

        // IMPORTANTE: Re-equipar el arma recogida (forzar equip)
        weaponManager.ForceEquipWeaponByID(weaponID);

        // Destroy the dropped weapon
        Destroy(dropped.gameObject);

        // Sync with server - this will broadcast to all clients
        if (playerSync != null && playerController.IsLocalPlayer)
        {
            playerSync.SendWeaponPickup(weaponID);
        }

        Debug.Log($"[WeaponThrowSystem] Picked up weapon {weaponID}");
    }
}

// Separate component for dropped weapons
public class DroppedWeapon : MonoBehaviour
{
    public int WeaponID { get; private set; }
    public bool CanPickup { get; private set; } = true;

    private float despawnTimer;
    private bool initialized = false;
    private bool hasLanded = false;
    private float landedTime = 0f;
    private const float pickupDelay = 0.3f; // Delay antes de poder recoger

    public void Initialize(int id, float despawnTime)
    {
        WeaponID = id;
        despawnTimer = despawnTime;
        initialized = true;

        // Add collider if missing
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            BoxCollider boxCol = gameObject.AddComponent<BoxCollider>();
            boxCol.isTrigger = false; // NO trigger para que colisione con el suelo
        }
        else
        {
            col.isTrigger = false;
        }

        // Add rigidbody if missing
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }

        // Configuraci�n f�sica para tiro parab�lico realista
        rb.mass = 1.5f;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 0.8f;
        rb.useGravity = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        // Add layer for proper collision
        gameObject.layer = LayerMask.NameToLayer("Default");
    }

    void Update()
    {
        if (!initialized) return;

        // Despawn timer
        despawnTimer -= Time.deltaTime;
        if (despawnTimer <= 0)
        {
            Destroy(gameObject);
            return;
        }

        // Check if weapon has landed and enable pickup after delay
        if (!hasLanded)
        {
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null && rb.linearVelocity.magnitude < 0.5f)
            {
                hasLanded = true;
                landedTime = Time.time;
                CanPickup = false;
            }
        }
        else if (!CanPickup && Time.time - landedTime >= pickupDelay)
        {
            CanPickup = true;
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        // Opcional: A�adir efecto de sonido al impactar
        if (collision.gameObject.CompareTag("Ground") || collision.gameObject.CompareTag("Wall"))
        {
            // Aqu� podr�as reproducir un sonido de impacto
        }
    }
}