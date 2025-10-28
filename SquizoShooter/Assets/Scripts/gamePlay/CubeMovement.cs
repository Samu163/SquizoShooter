using UnityEngine;

public class CubeMovement : MonoBehaviour
{
    private Vector3 movement = Vector3.zero;
    private float moveSpeed = 5f;
    private UDPClient udpClient;

    private bool isLocalPlayer = false;

    void Start()
    {
        udpClient = FindObjectOfType<UDPClient>();

        // Inicializar movement con la posición actual
        movement = transform.position;
    }

    void Update()
    {
        if (!isLocalPlayer) return;

        Vector3 delta = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) delta += Vector3.forward;
        if (Input.GetKey(KeyCode.S)) delta += Vector3.back;
        if (Input.GetKey(KeyCode.A)) delta += Vector3.left;
        if (Input.GetKey(KeyCode.D)) delta += Vector3.right;

        if (delta != Vector3.zero)
        {
            movement += delta * moveSpeed * Time.deltaTime;
            transform.position = movement;

            // Enviar la nueva posición al servidor
            if (udpClient != null && udpClient.IsConnected)
            {
                udpClient.SendCubeMovement(movement);
            }
        }
    }

    // Método para actualizar posición desde red
    public void UpdateCubePosition(Vector3 newPosition)
    {
        movement = newPosition;
        transform.position = newPosition;
    }

    public void SetAsLocalPlayer(bool isLocal)
    {
        isLocalPlayer = isLocal;
    }
}
