using UnityEngine;

public class PlayerVisuals : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private GameObject visualModel;

    [Header("Gun Visual Settings")]
    [SerializeField] private Animator Animator;

    public void Initialize(Transform playerTransform)
    {
        if (visualModel == null)
        {
            // Search for a child named "Model" or similar
            foreach (Transform child in playerTransform)
            {
                if (child.name.Contains("Model") || child.name.Contains("Visual") || child.name.Contains("Mesh"))
                {
                    visualModel = child.gameObject;
                    break;
                }
            }

            if (visualModel == null && playerTransform.childCount > 0)
            {
                foreach (Transform child in playerTransform)
                {
                    if (child.GetComponent<Renderer>() != null)
                    {
                        visualModel = child.gameObject;
                        break;
                    }
                }
            }
        }
    }

    public void SetAsLocalPlayer(bool isLocal)
    {
        // Any visual-specific setup for local player
    }

    public void PlayShootAnimation()
    {
        if (Animator != null)
        {
            Animator.SetTrigger("shoot");
        }
    }

    public void HideModel()
    {
        if (visualModel != null)
        {
            visualModel.SetActive(false);
        }
    }

    public void ShowModel()
    {
        if (visualModel != null)
        {
            visualModel.SetActive(true);
        }
    }

    public void UpdateVisualOnHealth(float health)
    {
        if (health <= 0f && visualModel != null)
        {
            visualModel.SetActive(false);
        }
        else if (health > 0f && visualModel != null)
        {
            visualModel.SetActive(true);
        }
    }
}