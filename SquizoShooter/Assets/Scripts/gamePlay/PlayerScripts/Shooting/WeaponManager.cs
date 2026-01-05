using UnityEngine;
using System.Collections.Generic;

public class WeaponManager : MonoBehaviour
{
    [Header("Weapons")]
    [SerializeField] private List<BaseWeapon> availableWeapons = new List<BaseWeapon>();

    private BaseWeapon currentWeapon;
    private int currentWeaponIndex = 0;

    private PlayerController playerController;
    private PlayerSync playerSync;
    private PlayerCamera playerCamera;

    public int CurrentWeaponID => currentWeapon != null ? currentWeapon.WeaponID : 1;

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

        // Equip first weapon
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
        if (weaponIndex == currentWeaponIndex && currentWeapon != null) return;

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

            Debug.Log($"[WeaponManager] Equipped: {currentWeapon.WeaponID}");
        }
    }

    public void UnequipCurrentWeapon()
    {
        if (currentWeapon != null)
        {
            currentWeapon.SetActive(false);
            currentWeapon = null;
        }

        currentWeaponIndex = -1; 
       
        if (playerController != null)
        {
            playerController.SwitchWeaponVisuals(0); 
        }

        Debug.Log("[WeaponManager] Weapon unequipped - player has no weapon");
    }
    public void SetWeaponByID(int weaponID)
    {
        for (int i = 0; i < availableWeapons.Count; i++)
        {
            if (availableWeapons[i] != null && availableWeapons[i].WeaponID == weaponID)
            {
                if (currentWeapon != null && currentWeapon.WeaponID == weaponID) return;

                EquipWeapon(i);
                return;
            }
        }
    }
    public void HandleWeaponSwitchInput()
    {
        if (currentWeapon == null) return;

        if (Input.GetKeyDown(KeyCode.Alpha1)) SwitchWeapon(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) SwitchWeapon(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) SwitchWeapon(2);
    }
}