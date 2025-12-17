using UnityEngine;

public abstract class BaseWeapon : MonoBehaviour
{
    [Header("Weapon Stats")]
    [SerializeField] protected float shootRange = 100f;
    [SerializeField] protected float shootDamage = 25f;
    [SerializeField] protected float fireRate = 4f;

    [Header("Recoil Data")]
    [SerializeField] public float recoilPitch = 5f;
    [SerializeField] public float recoilYaw = 1f;
    [SerializeField] public float recoilBack = 0.2f;

    [Header("Visuals")]
    [SerializeField] protected GameObject weaponModel;

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
        OnShootLogic(shootOrigin, udpClient, myKey);
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
}