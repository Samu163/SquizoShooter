using UnityEngine;

public class ShotgunWeapon : BaseWeapon
{
    [Header("Shotgun Specific")]
    [SerializeField] private int pelletCount = 8;  
    [SerializeField] private float spreadAngle = 6f;

    [Header("Ammo")]
    [SerializeField] private int maxAmmo = 8;
    [SerializeField] private int currentAmmo;

    private PlayerController owner;

    void Awake()
    {
        WeaponID = 3;
        fireRate = 1.0f; 
        recoilPitch = 8f;
        recoilYaw = 2.2f;
        recoilBack = 0.35f;
        shootDamage = 9f;

        currentAmmo = maxAmmo;
        owner = GetComponentInParent<PlayerController>();
        UpdateAmmoUI();
    }

    void OnEnable()
    {
        if (owner == null) owner = GetComponentInParent<PlayerController>();
        UpdateAmmoUI();
    }

    public override bool CanShoot()
    {
        return base.CanShoot() && currentAmmo > 0;
    }

    public void ReloadAllAmmo()
    {
        currentAmmo = maxAmmo;
        UpdateAmmoUI();
    }

    public override void PerformShoot(Transform shootOrigin, UDPClient udpClient, string myKey)
    {
        if (!CanShoot())
        {
            UpdateAmmoUI();
            return;
        }

        lastFireTime = Time.time;
        // Consume 1 shell per trigger pull (not per pellet)
        currentAmmo = Mathf.Max(0, currentAmmo - 1);
        UpdateAmmoUI();

        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 direction = ApplySpread(shootOrigin.forward, spreadAngle);
            Vector3 endPoint = shootOrigin.position + (direction * shootRange);
            Ray ray = new Ray(shootOrigin.position, direction);
            if (Physics.Raycast(ray, out RaycastHit hit, shootRange))
            {
                if (!hit.collider.isTrigger)
                {
                    endPoint = hit.point;
                }
                if (!hit.collider.CompareTag("Wall"))
                {
                    Transform t = hit.collider.transform;
                    while (t != null && !t.CompareTag("Player")) t = t.parent;

                    if (t != null && udpClient != null)
                    {
                        string targetKey = udpClient.GetKeyForGameObject(t.gameObject);
                        if (!string.IsNullOrEmpty(targetKey) && targetKey != myKey)
                        {
                            SendDamage(udpClient, targetKey, shootDamage);
                        }
                    }
                }
            }

            if (firePoint != null)
            {
                CreateTrail(firePoint.position, endPoint);
            }
        }
    }

    public override void SimulateShootVisualsForNetwork()
    {
        if (firePoint == null) return;

        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 direction = ApplySpread(firePoint.forward, spreadAngle);
            Vector3 start = firePoint.position;
            Vector3 end = start + (direction * shootRange);
            if (Physics.Raycast(start, direction, out RaycastHit hit, shootRange))
            {
                if (!hit.collider.isTrigger)
                    end = hit.point;
            }

            CreateTrail(start, end);
        }
    }

    protected override void OnShootLogic(Transform origin, UDPClient client, string myKey)
    {
        // Shotgun uses PerformShoot; no per-bullet logic here
    }

    Vector3 ApplySpread(Vector3 forward, float angleDeg)
    {
        float x = Random.Range(-angleDeg, angleDeg);
        float y = Random.Range(-angleDeg, angleDeg); 
        return Quaternion.Euler(x, y, 0f) * forward;
    }

    private void UpdateAmmoUI()
    {
        if (AmmoBarUI.instance == null) return;
        if (owner != null && !owner.IsLocalPlayer) return;
        AmmoBarUI.instance.UpdateUI(currentAmmo, maxAmmo);
    }

    public override int GetCurrentAmmo() => currentAmmo;
    public override int GetMaxAmmo() => maxAmmo;
}