using UnityEngine;

public class PlayerShooting : MonoBehaviour
{
    private PlayerController playerController;
    private WeaponManager weaponManager;

    private PlayerSync playerSync;
    private PlayerCamera playerCamera;
    private PlayerMovement playerMovement;
    private PlayerVisuals playerVisuals;
    private PlayerAudioController audioController;

    public void Initialize(PlayerController pc, WeaponManager wm)
    {
        playerController = pc;
        weaponManager = wm;

        playerSync = pc.GetSync();
        playerCamera = pc.GetPlayerCamera();
        playerMovement = pc.GetMovement();
        playerVisuals = pc.GetVisuals();
        audioController = pc.GetComponent<PlayerAudioController>();
    }

    public void TryShoot()
    {
        if (weaponManager == null) return;

        BaseWeapon currentWeapon = weaponManager.GetCurrentWeapon();
        if (currentWeapon == null) return;

        if (currentWeapon.CanShoot())
        {
            PerformShoot(currentWeapon);
        }
    }

    private void PerformShoot(BaseWeapon weapon)
    {
        ApplyPlayerEffects(weapon);

        UDPClient client = playerSync.GetUDPClient();
        if (client != null && client.IsConnected)
        {
            client.SendShootAnim();
        }
        string myKey = client != null ? client.ClientKey : "";
        weapon.PerformShoot(playerCamera.CameraTransform, client, myKey);
        if (audioController != null)
        {
            audioController.PlayShoot(weapon.WeaponID);
        }
    }

    private void ApplyPlayerEffects(BaseWeapon weapon)
    {
        if (playerVisuals != null)
        {
            playerVisuals.PlayShootAnimation();
        }

        if (playerCamera != null)
        {
            playerCamera.ApplyRecoil(weapon.recoilPitch, weapon.recoilYaw);
        }

        if (playerMovement != null)
        {
            playerMovement.ApplyPhysicalRecoil(weapon.recoilBack);
        }
    }
}