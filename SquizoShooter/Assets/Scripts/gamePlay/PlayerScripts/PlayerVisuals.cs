using UnityEngine;

public class PlayerVisuals : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private GameObject visualModel;

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