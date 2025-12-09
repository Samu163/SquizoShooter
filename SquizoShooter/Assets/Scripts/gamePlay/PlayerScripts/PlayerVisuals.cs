using UnityEngine;

public class PlayerVisuals : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private GameObject visualModel;

    [Header("Weapon Models")]
    [SerializeField] private GameObject pistolModel;   // ID 1
    [SerializeField] private GameObject minigunModel;  // ID 2
    [SerializeField] private GameObject shotgunModel;  // ID 3

    [Header("Gun Visual Settings")]
    [SerializeField] private Animator Animator;

    [Header("Aiming Settings")]
    [SerializeField] private Transform aimPivot;

    private float currentPitch = 0f;
    private bool isLocalPlayer = false;

    public void Initialize(Transform playerTransform)
    {
        if (visualModel == null)
        {
            foreach (Transform child in playerTransform)
            {
                if (child.name.Contains("Model") || child.name.Contains("Visual") || child.name.Contains("Mesh"))
                {
                    visualModel = child.gameObject;
                    break;
                }
            }
        }
    }

    public void SetEquippedWeapon(int weaponID)
    {
        // 1. Desactivar todas primero
        if (pistolModel) pistolModel.SetActive(false);
        if (minigunModel) minigunModel.SetActive(false);
        if (shotgunModel) shotgunModel.SetActive(false);

        // 2. Activar la correcta según el ID definido en tus scripts de armas
        switch (weaponID)
        {
            case 1: // PistolWeapon.cs tiene ID 1
                if (pistolModel) pistolModel.SetActive(true);
                break;
            case 2: // MiniGunWeapon.cs tiene ID 2
                if (minigunModel) minigunModel.SetActive(true);
                break;
            case 3: // ShotgunWeapon.cs tiene ID 3
                if (shotgunModel) shotgunModel.SetActive(true);
                break;
            default:
                Debug.LogWarning($"[PlayerVisuals] ID de arma desconocido: {weaponID}");
                break;
        }
    }

    public void SetAsLocalPlayer(bool isLocal)
    {
        isLocalPlayer = isLocal;
    }

    public void PlayShootAnimation()
    {
        if (Animator != null) Animator.SetTrigger("shoot");
    }

    public void HideModel()
    {
        if (visualModel != null) visualModel.SetActive(false);
    }

    public void ShowModel()
    {
        if (visualModel != null) visualModel.SetActive(true);
    }

    public void UpdateVisualOnHealth(float health)
    {
        if (health <= 0f && visualModel != null)
            visualModel.SetActive(false);
        else if (health > 0f && visualModel != null)
            visualModel.SetActive(true);
    }

    public void UpdateAiming(float pitch)
    {
        if (pitch > 180) pitch -= 360;
        currentPitch = pitch;
    }

    void LateUpdate()
    {
        if (isLocalPlayer) return;

        if (aimPivot != null)
        {
            Vector3 currentLocalRotation = aimPivot.localEulerAngles;
            aimPivot.localRotation = Quaternion.Euler(currentPitch, currentLocalRotation.y, currentLocalRotation.z);
        }
    }
}