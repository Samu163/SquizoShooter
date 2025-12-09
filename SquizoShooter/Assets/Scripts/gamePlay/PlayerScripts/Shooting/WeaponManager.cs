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

        // Initialize all weapons
        foreach (var weapon in availableWeapons)
        {
            if (weapon != null)
            {
                weapon.Initialize(pc, sync, cam);
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
        if (weaponIndex == currentWeaponIndex) return;

        EquipWeapon(weaponIndex);

        // Sync weapon change with server
        if (playerSync != null)
        {
            playerSync.SendWeaponChange(CurrentWeaponID);
        }
    }

    void EquipWeapon(int index)
    {
        // Deactivate current weapon
        if (currentWeapon != null)
        {
            currentWeapon.SetActive(false);
        }

        // Activate new weapon
        currentWeaponIndex = index;
        currentWeapon = availableWeapons[index];

        if (currentWeapon != null)
        {
            currentWeapon.SetActive(true);
        }

        Debug.Log($"[WeaponManager] Equipped weapon: {currentWeapon?.GetType().Name} (ID: {CurrentWeaponID})");
    }

    public void SetWeaponByID(int weaponID)
    {
        for (int i = 0; i < availableWeapons.Count; i++)
        {
            if (availableWeapons[i] != null && availableWeapons[i].WeaponID == weaponID)
            {
                EquipWeapon(i);
                return;
            }
        }

        Debug.LogWarning($"[WeaponManager] Weapon with ID {weaponID} not found!");
    }

    public void HandleWeaponSwitchInput()
    {
        // Number keys 1-3 for direct weapon selection
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
        {
            SwitchWeapon(0);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
        {
            SwitchWeapon(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3))
        {
            SwitchWeapon(2);
        }       
    }
}