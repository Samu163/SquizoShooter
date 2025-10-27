using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    // Enum para gestionar los tipos de juego
    public enum GameMode { Server, Client, None }
    private GameMode currentGameMode = GameMode.None;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject); // Asegurarse de que solo exista una instancia
        }
    }

    // Establecer el tipo de juego
    public void SetGameMode(GameMode mode)
    {
        currentGameMode = mode;
    }

    // Obtener el tipo de juego actual
    public GameMode GetGameMode()
    {
        return currentGameMode;
    }
}

