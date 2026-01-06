using UnityEngine;
using System.Collections.Generic;

public class WeaponManager : MonoBehaviour
{
    [Header("Weapons")]
    [SerializeField] private List<BaseWeapon> availableWeapons = new List<BaseWeapon>();

    private BaseWeapon currentWeapon;
    private int currentWeaponIndex = -1;

    private PlayerController playerController;
    private PlayerSync playerSync;
    private PlayerCamera playerCamera;

    public int CurrentWeaponID => currentWeapon != null ? currentWeapon.WeaponID : 0;

    public void Initialize(PlayerController pc, PlayerSync sync, PlayerCamera cam)
    {
        playerController = pc;
        playerSync = sync;
        playerCamera = cam;

        foreach (var weapon in availableWeapons)
        {
            if (weapon != null)
            {
                weapon.Initialize();
                weapon.SetActive(false);
            }
        }

        if (availableWeapons.Count > 0)
        {
            EquipWeapon(0);
        }
    }

    public BaseWeapon GetCurrentWeapon()
    {
        return currentWeapon;
    }

    public void SwitchWeapon(int weaponIndex)
    {
        if (weaponIndex < 0 || weaponIndex >= availableWeapons.Count) return;
        if (weaponIndex == currentWeaponIndex && currentWeapon != null)
        {
            PushAmmoToHUD();
            return;
        }

        EquipWeapon(weaponIndex);

        if (playerSync != null)
        {
            playerSync.SendWeaponChange(CurrentWeaponID);
        }
    }

    void EquipWeapon(int index)
    {
        if (currentWeapon != null)
        {
            currentWeapon.SetActive(false);
        }

        currentWeaponIndex = index;
        currentWeapon = availableWeapons[index];

        if (currentWeapon != null)
        {
            currentWeapon.SetActive(true);
            if (playerController != null)
            {
                playerController.SwitchWeaponVisuals(currentWeapon.WeaponID);
            }

            PushAmmoToHUD();
        }
    }

    private void PushAmmoToHUD()
    {
        if (AmmoBarUI.instance == null) return;
        if (playerController != null && !playerController.IsLocalPlayer) return;
        if (currentWeapon == null) return;

        AmmoBarUI.instance.UpdateUI(currentWeapon.GetCurrentAmmo(), currentWeapon.GetMaxAmmo());
    }

    public void UnequipCurrentWeapon()
    {
        if (currentWeapon != null)
        {
            currentWeapon.SetActive(false);
        }

        currentWeapon = null;
        currentWeaponIndex = -1;

        if (playerController != null)
        {
            playerController.SwitchWeaponVisuals(0);
        }

        if (AmmoBarUI.instance != null)
        {
            AmmoBarUI.instance.UpdateUI(0, 1);
        }

        Debug.Log("[WeaponManager] Weapon unequipped - player has no weapon");
    }

    public void ForceEquipWeaponByID(int weaponID)
    {
        if (currentWeapon != null && currentWeapon.WeaponID == weaponID)
        {
            PushAmmoToHUD();
            return;
        }

        for (int i = 0; i < availableWeapons.Count; i++)
        {
            if (availableWeapons[i] != null && availableWeapons[i].WeaponID == weaponID)
            {
                if (currentWeapon != null)
                {
                    currentWeapon.SetActive(false);
                }

                currentWeaponIndex = i;
                currentWeapon = availableWeapons[i];

                if (currentWeapon != null)
                {
                    currentWeapon.SetActive(true);

                    if (playerController != null)
                    {
                        playerController.SwitchWeaponVisuals(currentWeapon.WeaponID);
                    }

                    PushAmmoToHUD();
                }

                if (playerSync != null && playerController != null && playerController.IsLocalPlayer)
                {
                    playerSync.SendWeaponChange(weaponID);
                }

                return;
            }
        }

        Debug.LogError($"[WeaponManager] No se encontró arma con ID {weaponID}");
    }

    public void SetWeaponByID(int weaponID)
    {
        if (currentWeapon != null && currentWeapon.WeaponID == weaponID)
        {
            PushAmmoToHUD();
            return;
        }

        for (int i = 0; i < availableWeapons.Count; i++)
        {
            if (availableWeapons[i] != null && availableWeapons[i].WeaponID == weaponID)
            {
                EquipWeapon(i);
                return;
            }
        }
    }

    // NEW: reload all owned weapons and refresh HUD
    public void ReloadAllWeaponsAmmo()
    {
        foreach (var weapon in availableWeapons)
        {
            if (weapon == null) continue;

            if (weapon is PistolWeapon pistol)
                pistol.ReloadAllAmmo();
            else if (weapon is MiniGunWeapon minigun)
                minigun.ReloadAllAmmo();
            else if (weapon is ShotgunWeapon shotgun)
                shotgun.ReloadAllAmmo();
        }

        if (AmmoBarUI.instance != null && currentWeapon != null)
        {
            AmmoBarUI.instance.UpdateUI(currentWeapon.GetCurrentAmmo(), currentWeapon.GetMaxAmmo());
        }
    }
}