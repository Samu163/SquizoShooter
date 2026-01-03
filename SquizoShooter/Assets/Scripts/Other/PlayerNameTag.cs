using UnityEngine;
using TMPro;

public class PlayerNameTag : MonoBehaviour
{
    [Header("Optional: Text Component")]
    public TextMeshPro textMesh; 

    private Transform targetCamera;

    void Update()
    {
        if (Camera.main != null)
        {
            targetCamera = Camera.main.transform;
        }
        else
        {
            Camera cam = Camera.current;
            if (cam != null) targetCamera = cam.transform;
        }
        if (targetCamera != null)
        {
            transform.rotation = Quaternion.LookRotation(transform.position - targetCamera.position);
        }
    }

    // Método para asignar el nombre desde el UDPClient
    public void SetText(string text, Color color)
    {
        if (textMesh != null)
        {
            textMesh.text = text;
            textMesh.color = color;
        }
    }
}