using UnityEngine;

public class LookAtPlayer : MonoBehaviour
{
    private Transform camaraPrincipal;

    void Start()
    {
        if (Camera.main != null)
        {
           // camaraPrincipal = Camera.main.transform;
        }
        else
        {
            Debug.LogError("No se encontró la cámara 'MainCamera'. ¡Asegúrate de que la cámara de tu Player tiene ese Tag!");
        }
    }

    public void AssignCamera(Camera camera)
    {
        camaraPrincipal = camera.transform;
    }
    void LateUpdate()
    {
        if (camaraPrincipal == null)
            return;

        // Rota para mirar en la dirección opuesta a la cámara
        // (Calcula la dirección desde la cámara hacia el objeto)
        transform.rotation = Quaternion.LookRotation(transform.position - camaraPrincipal.position);
    }
}