using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    public Button createGameButton;
    public Button joinGameButton;

    void Start()
    {
        createGameButton.onClick.AddListener(CreateGame);
        joinGameButton.onClick.AddListener(JoinGame);
    }

    // Crear juego (servidor)
    void CreateGame()
    {
        SceneManager.LoadScene("Gameplay"); // Cargar escena Gameplay

        // Establecer el modo como servidor
        GameManager.Instance.SetGameMode(GameManager.GameMode.Server);
    }

    // Unirse a juego (cliente)
    void JoinGame()
    {
        SceneManager.LoadScene("Gameplay"); // Cargar la lista de servidores

        // Establecer el modo como cliente
        GameManager.Instance.SetGameMode(GameManager.GameMode.Client);
    }
}
