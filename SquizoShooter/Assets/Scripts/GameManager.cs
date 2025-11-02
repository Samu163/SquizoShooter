using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;
    public enum GameMode { Server, Client, None }
    private GameMode currentGameMode = GameMode.None;

    void Awake()
    {
        Application.runInBackground = true;
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        SetWindowMode();
    }

    public void SetGameMode(GameMode mode)
    {
        currentGameMode = mode;
    }

    public GameMode GetGameMode()
    {
        return currentGameMode;
    }

    private void SetWindowMode()
    {
        Screen.fullScreen = false;
        Screen.SetResolution(1920, 1080, false);  
    }
}
