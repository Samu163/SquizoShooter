using UnityEngine;

public class ShotgunWeapon : BaseWeapon
{
    [Header("Shotgun Specific")]
    [SerializeField] private int pelletCount = 6;
    [SerializeField] private float spreadAngle = 6f;

    void Awake()
    {
        WeaponID = 3;
        // Stats 
        fireRate = 1.2f;
        recoilPitch = 8f;
        recoilYaw = 2.2f;
        recoilBack = 0.35f;
        shootDamage = 9f; 
    }

    protected override void OnShootLogic(Transform origin, UDPClient client, string myKey)
    {
        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 dir = ApplySpread(origin.forward, spreadAngle);
            FirePellet(origin.position, dir, client, myKey);
        }
    }

    void FirePellet(Vector3 pos, Vector3 dir, UDPClient client, string myKey)
    {
        Ray ray = new Ray(pos, dir);
        if (Physics.Raycast(ray, out RaycastHit hit, shootRange))
        {
            if (hit.collider.CompareTag("Wall")) return;

            Transform t = hit.collider.transform;
            while (t != null && !t.CompareTag("Player")) t = t.parent;

            if (t != null && client != null)
            {
                string targetKey = client.GetKeyForGameObject(t.gameObject);
                if (!string.IsNullOrEmpty(targetKey) && targetKey != myKey)
                {
                    SendDamage(client, targetKey, shootDamage);
                }
            }
        }
    }

    Vector3 ApplySpread(Vector3 forward, float angleDeg)
    {
        float yaw = Random.Range(-angleDeg, angleDeg);
        float pitch = Random.Range(-angleDeg, angleDeg);
        return Quaternion.Euler(pitch, yaw, 0f) * forward;
    }
}