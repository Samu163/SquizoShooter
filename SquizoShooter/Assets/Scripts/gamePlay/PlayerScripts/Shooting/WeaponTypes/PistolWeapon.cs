using UnityEngine;
using System.Linq; // Necesario para ordenar si usas Array.Sort o Linq

public class PistolWeapon : BaseWeapon
{
    [Header("Ammo")]
    [SerializeField] private int maxAmmo = 12;
    [SerializeField] private int currentAmmo;

    private PlayerController owner;

    void Awake()
    {
        WeaponID = 1;

        // Stats
        shootDamage = 25f;
        fireRate = 4f;
        recoilPitch = 3f;
        recoilYaw = 0.5f;
        recoilBack = 0.15f;

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

    protected override void OnShootLogic(Transform origin, UDPClient client, string myKey)
    {
        // Consume 1 ammo per shot
        currentAmmo = Mathf.Max(0, currentAmmo - 1);
        UpdateAmmoUI();

        Ray ray = new Ray(origin.position, origin.forward);

        RaycastHit[] hits = Physics.RaycastAll(ray, shootRange);
        if (hits == null || hits.Length == 0) return;
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (var hit in hits)
        {
            if (hit.collider.CompareTag("Wall"))
            {
                return;
            }

            Transform t = hit.collider.transform;
            while (t != null && !t.CompareTag("Player"))
            {
                t = t.parent;
            }

            if (t != null && client != null)
            {
                string targetKey = client.GetKeyForGameObject(t.gameObject);

                if (!string.IsNullOrEmpty(targetKey) && targetKey != myKey)
                {
                    SendDamage(client, targetKey, shootDamage);
                    return; 
                }
            }
        }
    }

    private void UpdateAmmoUI()
    {
        if (AmmoBarUI.instance == null) return;
        if (owner != null && !owner.IsLocalPlayer) return;
        AmmoBarUI.instance.UpdateUI(currentAmmo, maxAmmo);
    }

    // HUD queries
    public override int GetCurrentAmmo() => currentAmmo;
    public override int GetMaxAmmo() => maxAmmo;
}