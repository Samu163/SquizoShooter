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
        SceneManager.LoadScene("Gameplay"); 
        GameManager.Instance.SetGameMode(GameManager.GameMode.Server);
    }

    // Unirse a juego (cliente)
    void JoinGame()
    {
        SceneManager.LoadScene("Gameplay");
        GameManager.Instance.SetGameMode(GameManager.GameMode.Client);
    }

    public void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
    }
}
