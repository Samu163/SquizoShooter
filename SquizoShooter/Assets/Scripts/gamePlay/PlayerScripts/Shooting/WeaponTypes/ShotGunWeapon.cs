using UnityEngine;

public class ShotgunWeapon : BaseWeapon
{
    [Header("Shotgun Specific")]
    [SerializeField] private int pelletCount = 6;
    [SerializeField] private float spreadAngle = 6f;
    [SerializeField] private float perPelletDamage = 9f;

    void Awake()
    {
        WeaponID = 3;

        // Shotgun stats
        fireRate = 1.2f;
        recoilPitch = 8f;
        recoilYaw = 2.2f;
        recoilBack = 0.35f;
    }

    protected override void Shoot()
    {
        Transform cameraTransform = playerCamera?.CameraTransform;
        if (cameraTransform == null) return;

        PlayShootAnimation();

        Vector3 origin = cameraTransform.position;

        // Fire multiple pellets
        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 dir = ApplySpread(cameraTransform.forward, spreadAngle);
            FirePellet(origin, dir);
        }
    }

    void FirePellet(Vector3 origin, Vector3 direction)
    {
        Ray ray = new Ray(origin, direction);
        RaycastHit[] hits = Physics.RaycastAll(ray, shootRange);

        if (hits == null || hits.Length == 0) return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            // Block pellet if hits a wall
            if (hit.collider != null && hit.collider.CompareTag("Wall"))
            {
                Debug.Log("[ShotgunWeapon] Pellet blocked by Wall at " + hit.point);
                return;
            }

            // Check for player hit
            string targetKey = GetTargetKeyFromHit(hit);
            if (!string.IsNullOrEmpty(targetKey))
            {
                SendDamageToServer(targetKey, perPelletDamage);
                return; // Pellet consumed
            }
        }
    }

    Vector3 ApplySpread(Vector3 forward, float angleDeg)
    {
        float yaw = Random.Range(-angleDeg, angleDeg);
        float pitch = Random.Range(-angleDeg, angleDeg);
        Quaternion spreadRot = Quaternion.Euler(pitch, yaw, 0f);
        return spreadRot * forward;
    }
}