using UnityEngine;

public class PlayerShooting : MonoBehaviour
{
    private PlayerController playerController;
    private WeaponManager weaponManager; 

    public void Initialize(PlayerController pc, WeaponManager wm)
    {
        playerController = pc;
        weaponManager = wm;
    }

    public void TryShoot()
    {
        BaseWeapon currentWeapon = weaponManager.GetCurrentWeapon();
        if (currentWeapon != null)
        {
            currentWeapon.TryShoot();
        }
    }
}