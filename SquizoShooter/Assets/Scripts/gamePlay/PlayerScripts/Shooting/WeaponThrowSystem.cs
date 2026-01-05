
using UnityEngine;

public class WeaponThrowSystem : MonoBehaviour
{
    [Header("Throw Settings")]
    [SerializeField] private float throwForce = 8f;
    [SerializeField] private float throwUpwardForce = 2f;
    [SerializeField] private float throwTorque = 5f;
    [SerializeField] private float despawnTime = 30f;

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

        // Apply physics
        Rigidbody rb = droppedWeapon.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 force = direction * throwForce + Vector3.up * throwUpwardForce;
            rb.AddForce(force, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * throwTorque, ForceMode.Impulse);
        }
    }

    private GameObject GetDropPrefabForWeapon(int weaponID)
    {
        switch (weaponID)
        {
            case 1: return pistolDropPrefab;
            case 2: return minigunDropPrefab;
            case 3: return shotgunDropPrefab;
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
                //PickupWeapon(dropped);
                break;
            }
        }
    }

    //    private void PickupWeapon(DroppedWeapon dropped)
    //    {
    //        int weaponID = dropped.WeaponID;

    //        // IMPORTANTE: Re-equipar el arma recogida (forzar equip)
    //        weaponManager.ForceEquipWeaponByID(weaponID);

    //        // Destroy the dropped weapon
    //        Destroy(dropped.gameObject);

    //        // Sync with server - this will broadcast to all clients
    //        if (playerSync != null && playerController.IsLocalPlayer)
    //        {
    //            playerSync.SendWeaponPickup(weaponID);
    //        }

    //        Debug.Log($"[WeaponThrowSystem] Picked up weapon {weaponID}");
    //    }
    //}

}
// Separate component for dropped weapons
public class DroppedWeapon : MonoBehaviour
{
    public int WeaponID { get; private set; }
    public bool CanPickup { get; private set; } = true;

    private float despawnTimer;
    private bool initialized = false;

    public void Initialize(int id, float despawnTime)
    {
        WeaponID = id;
        despawnTimer = despawnTime;
        initialized = true;

        // Add collider if missing
        if (GetComponent<Collider>() == null)
        {
            BoxCollider col = gameObject.AddComponent<BoxCollider>();
            col.isTrigger = true;
        }

        // Add rigidbody if missing
        if (GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 1f;
            rb.linearDamping = 1f;
            rb.angularDamping = 0.5f;
        }
    }

    void Update()
    {
        if (!initialized) return;

        despawnTimer -= Time.deltaTime;
        if (despawnTimer <= 0)
        {
            Destroy(gameObject);
        }
    }
}