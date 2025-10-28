using UnityEngine;

public class CubeMovement : MonoBehaviour
{
    private Vector3 movement = Vector3.zero;
    private float moveSpeed = 5f;

    private UDPClient udpClient;

    void Start()
    {
        // Busca el UDPClient en escena (aseg�rate de que exista un objeto con UDPClient)
        udpClient = FindObjectOfType<UDPClient>();
        if (udpClient == null)
        {
            Debug.LogWarning("No se encontr� UDPClient en la escena. Aseg�rate de tener un objeto con UDPClient.");
        }
    }

    void Update()
    {
        // Movimiento local (mueve la posici�n absoluta usando WASD)
        Vector3 delta = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) delta += Vector3.forward;
        if (Input.GetKey(KeyCode.S)) delta += Vector3.back;
        if (Input.GetKey(KeyCode.A)) delta += Vector3.left;
        if (Input.GetKey(KeyCode.D)) delta += Vector3.right;

        if (delta != Vector3.zero)
        {
            movement += delta * moveSpeed * Time.deltaTime;
            transform.position = movement;

            // Enviar la nueva posici�n al servidor si estamos conectados
            if (udpClient != null && udpClient.IsConnected)
            {
                udpClient.SendCubeMovement(movement);
            }
        }
    }

    // M�todo que servidor/cliente puede llamar para forzar nueva posici�n
    public void UpdateCubePosition(Vector3 newPosition)
    {
        movement = newPosition;
        transform.position = newPosition;
    }
}
