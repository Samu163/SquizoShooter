using UnityEngine;

public class CubeMovement : MonoBehaviour
{
    private Vector3 movement = Vector3.zero;
    private float moveSpeed = 5f;

    private UDPClient udpClient;

    void Start()
    {
        // Obtener la referencia al UDPClient desde GameManager (u otro objeto adecuado)
        udpClient = GameManager.Instance.GetComponent<UDPClient>();
    }

    void Update()
    {
        // Movimiento local con WASD
        if (Input.GetKey(KeyCode.W)) movement += Vector3.forward * moveSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.S)) movement -= Vector3.forward * moveSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.A)) movement -= Vector3.right * moveSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.D)) movement += Vector3.right * moveSpeed * Time.deltaTime;

        // Mover el cubo en la escena
        transform.position = movement;

        // Enviar la nueva posición al servidor
        if (udpClient != null && udpClient.IsConnected)
        {
            udpClient.SendCubeMovement(movement);
        }
    }

    // Método para actualizar la posición del cubo desde el servidor
    public void UpdateCubePosition(Vector3 newPosition)
    {
        movement = newPosition;
        transform.position = newPosition;
    }
}
