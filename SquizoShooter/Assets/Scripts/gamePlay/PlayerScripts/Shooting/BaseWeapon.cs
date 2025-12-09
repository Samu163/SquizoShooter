using UnityEngine;

public abstract class BaseWeapon : MonoBehaviour
{
    [Header("Base Weapon Settings")]
    [SerializeField] protected float shootRange = 100f;
    [SerializeField] protected float shootDamage = 25f;
    [SerializeField] protected float fireRate = 4f;
    [SerializeField] protected float recoilPitch = 5f;
    [SerializeField] protected float recoilYaw = 1f;
    [SerializeField] protected float recoilBack = 0.2f;

    [Header("Weapon Model")]
    [SerializeField] protected GameObject weaponModel;

    protected PlayerController playerController;
    protected PlayerSync playerSync;
    protected PlayerCamera playerCamera;
    protected PlayerMovement playerMovement;
    protected PlayerVisuals playerVisuals;
    protected float lastFireTime = 0f;

    public int WeaponID { get; protected set; }
    public GameObject WeaponModel => weaponModel;

    public virtual void Initialize(PlayerController pc, PlayerSync sync, PlayerCamera cam)
    {
        playerController = pc;
        playerSync = sync;
        playerCamera = cam;
        playerMovement = pc.GetMovement();
        playerVisuals = pc.GetVisuals();
    }

    public virtual bool CanShoot()
    {
        float cooldown = 1f / Mathf.Max(0.0001f, fireRate);
        return Time.time - lastFireTime >= cooldown;
    }

    public virtual void TryShoot()
    {
        if (!CanShoot()) return;

        lastFireTime = Time.time;
        Shoot();
        ApplyRecoil();
    }

    protected abstract void Shoot();

    protected virtual void ApplyRecoil()
    {
        if (playerCamera != null)
        {
            playerCamera.ApplyRecoil(recoilPitch, recoilYaw);
        }

        if (playerMovement != null)
        {
            playerMovement.ApplyPhysicalRecoil(recoilBack);
        }
    }

    protected virtual void PlayShootAnimation()
    {
        if (playerVisuals != null)
        {
            playerVisuals.PlayShootAnimation();
        }

        UDPClient udpClient = playerSync?.GetUDPClient();
        if (udpClient != null && udpClient.IsConnected)
        {
            udpClient.SendShootAnim();
        }
    }

    protected virtual void SendDamageToServer(string targetKey, float damage)
    {
        UDPClient udpClient = playerSync?.GetUDPClient();
        if (udpClient != null && udpClient.IsConnected)
        {
            udpClient.SendShotToServer(targetKey, damage);
        }
    }

    protected string GetTargetKeyFromHit(RaycastHit hit)
    {
        Transform t = hit.collider.transform;
        while (t != null && !t.CompareTag("Player"))
        {
            t = t.parent;
        }

        if (t != null)
        {
            GameObject hitPlayerGO = t.gameObject;
            UDPClient udpClient = playerSync?.GetUDPClient();

            if (udpClient == null)
            {
                Debug.LogWarning("[BaseWeapon] No UDPClient to send SHOT.");
                return null;
            }

            string targetKey = udpClient.GetKeyForGameObject(hitPlayerGO);

            // Avoid self-damage
            if (udpClient.ClientKey == targetKey)
            {
                Debug.Log("[BaseWeapon] Shot ignored: self-hit.");
                return null;
            }

            return targetKey;
        }

        return null;
    }

    public virtual void SetActive(bool active)
    {
        if (weaponModel != null)
        {
            weaponModel.SetActive(active);
        }
    }

    public float GetShootRange() => shootRange;
}