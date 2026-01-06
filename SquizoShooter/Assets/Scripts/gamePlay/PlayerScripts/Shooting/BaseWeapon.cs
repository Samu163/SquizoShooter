using System.Collections.Generic;
using UnityEngine;

public abstract class BaseWeapon : MonoBehaviour
{
    [Header("Weapon Stats")]
    [SerializeField] protected float shootRange = 100f;
    [SerializeField] protected float shootDamage = 25f;
    [SerializeField] protected float fireRate = 4f;
    [SerializeField] protected float Ammo = 4f;

    [Header("Recoil Data")]
    [SerializeField] public float recoilPitch = 5f;
    [SerializeField] public float recoilYaw = 1f;
    [SerializeField] public float recoilBack = 0.2f;

    [Header("Visuals")]
    [SerializeField] protected GameObject weaponModel;
    [SerializeField] protected Transform firePoint;
    [SerializeField] protected GameObject bulletTrailPrefab;
    private static List<BulletTrail> bulletPool = new List<BulletTrail>();

    protected float lastFireTime = 0f;
    public int WeaponID { get; protected set; }

    public virtual void Initialize()
    {
    }

    public virtual bool CanShoot()
    {
        float cooldown = 1f / Mathf.Max(0.0001f, fireRate);
        return Time.time - lastFireTime >= cooldown;
    }

    public virtual void PerformShoot(Transform shootOrigin, UDPClient udpClient, string myKey)
    {
        lastFireTime = Time.time;
        Vector3 targetPoint = shootOrigin.position + (shootOrigin.forward * shootRange);
        if (Physics.Raycast(shootOrigin.position, shootOrigin.forward, out RaycastHit hit, shootRange))
        {
            if (!hit.collider.isTrigger)
                targetPoint = hit.point;
        }

        if (firePoint != null)
        {
            CreateTrail(firePoint.position, targetPoint);
        }

        OnShootLogic(shootOrigin, udpClient, myKey);
    }

    public virtual void SimulateShootVisualsForNetwork()
    {
        if (firePoint == null) return;

        Vector3 start = firePoint.position;
        Vector3 end = start + (firePoint.forward * shootRange);

        if (Physics.Raycast(start, firePoint.forward, out RaycastHit hit, shootRange))
        {
            if (!hit.collider.isTrigger)
                end = hit.point;
        }

        CreateTrail(start, end);
    }

    protected void CreateTrail(Vector3 start, Vector3 end)
    {
        if (BulletTrailPool.Instance != null)
        {
            BulletTrail trail = BulletTrailPool.Instance.GetTrail();
            if (trail != null)
            {
                trail.SetPositions(start, end);
            }
        }
        else
        {
            Debug.LogWarning("¡Falta el BulletTrailPool en la escena! No se verán las balas.");
        }
    }

    protected abstract void OnShootLogic(Transform origin, UDPClient client, string myKey);

    public void SetActive(bool active)
    {
        if (weaponModel != null) weaponModel.SetActive(active);
    }

    protected void SendDamage(UDPClient client, string targetKey, float damage)
    {
        if (client != null && client.IsConnected)
        {
            client.SendShotToServer(targetKey, damage);
        }
        if (UiController.Instance != null)
        {
            UiController.Instance.ShowHitMarker();
        }
    }

    // NEW: ammo getters so derived weapons can override and HUD can query
    public virtual int GetCurrentAmmo() => 0;
    public virtual int GetMaxAmmo() => 0;
}