using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

public class LobbyUiController : MonoBehaviour
{
    [Header("UI References")]
    public GameObject lobbyPanel;
    public Transform playersContainer;
    public GameObject playerListPrefab;

    [Header("Rounds Settings")]
    public Slider roundsSlider;         
    public TextMeshProUGUI roundsValueText; 

    [Header("Buttons")]
    public Button readyButton;
    public Button startButton;
    public Button exitButton;

    [Header("Countdown")]
    public TextMeshProUGUI countdownText;

    [Header("Visual Settings")]
    public Color localPlayerHighlightColor = Color.green;
    public Color defaultPlayerColor = Color.white;

    private UDPClient client;
    private bool isLocalReady = false;

    void Start()
    {
        client = FindObjectOfType<UDPClient>();
        Init();
    }

    public void Init()
    {
        bool isHost = GameManager.Instance.GetGameMode() == GameManager.GameMode.Server;
        startButton.gameObject.SetActive(isHost);
        startButton.interactable = false;
        readyButton.gameObject.SetActive(!isHost);

        if (roundsSlider != null)
        {
            if(!isHost)
            {
                roundsSlider.gameObject.SetActive(false);
                roundsValueText.gameObject.SetActive(false);
            }
            else
            {
                roundsSlider.interactable = isHost;
                roundsSlider.minValue = 1;
                roundsSlider.maxValue = 10;
                roundsSlider.wholeNumbers = true;
                roundsSlider.value = 5;

                roundsSlider.onValueChanged.AddListener(UpdateRoundsText);

                UpdateRoundsText(roundsSlider.value);
            } 
        }
        else
        {
            if (roundsValueText != null) roundsValueText.text = "5";
        }

        if (client != null)
        {
            client.OnLobbyUpdated += UpdateLobbyUI;
            client.OnGameStarted += StartCountdownSequence;

            if (client.CurrentLobbyPlayers != null && client.CurrentLobbyPlayers.Count > 0)
            {
                UpdateLobbyUI(client.CurrentLobbyPlayers);
            }
        }

        readyButton.onClick.AddListener(OnReadyClicked);
        startButton.onClick.AddListener(OnStartClicked);
        exitButton.onClick.AddListener(OnExitClicked);

        lobbyPanel.SetActive(true);
        if (countdownText != null) countdownText.gameObject.SetActive(false);
    }

    void UpdateRoundsText(float value)
    {
        if (roundsValueText != null)
        {
            roundsValueText.text = $"{value}";
        }
    }

    void OnReadyClicked()
    {
        isLocalReady = !isLocalReady;
        client.SendReadyState(isLocalReady);
        var btnText = readyButton.GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null) btnText.text = isLocalReady ? "CANCEL READY" : "READY";
        readyButton.image.color = isLocalReady ? Color.green : Color.white;
    }

    void OnStartClicked()
    {
        var server = FindObjectOfType<UDPServer>();
        if (server != null)
        {
            // Leer el valor del Slider
            int rounds = 5;
            if (roundsSlider != null)
            {
                rounds = (int)roundsSlider.value;
            }

            // Iniciar juego con esas rondas
            server.RequestStartGame(rounds);
        }
    }

    void OnExitClicked()
    {
        if (client != null) client.Disconnect();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void UpdateLobbyUI(List<UDPClient.LobbyPlayerInfo> players)
    {
        foreach (Transform child in playersContainer) Destroy(child.gameObject);
        bool allReady = true;

        foreach (var p in players)
        {
            GameObject item = Instantiate(playerListPrefab, playersContainer);
            TextMeshProUGUI txt = item.GetComponentInChildren<TextMeshProUGUI>();
            string status = p.IsReady ? "<color=green>READY</color>" : "<color=red>WAITING</color>";
            txt.text = $"{p.Name} - {status}";

            if (p.Key == client.ClientKey)
            {
                txt.color = localPlayerHighlightColor;
                txt.fontStyle = FontStyles.Bold;
            }
            else txt.color = defaultPlayerColor;

            if (!p.IsReady) allReady = false;
        }

        if (startButton.gameObject.activeSelf)
        {
            bool clientsReady = true;
            foreach (var p in players) if (p.Key != client.ClientKey && !p.IsReady) clientsReady = false;
            startButton.interactable = clientsReady && players.Count > 0;
        }
    }

    void StartCountdownSequence() { StartCoroutine(CountdownCoroutine()); }

    IEnumerator CountdownCoroutine()
    {
        lobbyPanel.SetActive(false);
        if (countdownText) { countdownText.gameObject.SetActive(true); countdownText.text = "3"; yield return new WaitForSeconds(1); countdownText.text = "2"; yield return new WaitForSeconds(1); countdownText.text = "1"; yield return new WaitForSeconds(1); countdownText.text = "GO!"; }
        if (client) client.SpawnMyPlayerNow();
        yield return new WaitForSeconds(0.5f);
        if (countdownText) countdownText.gameObject.SetActive(false);
        if (UiController.Instance) UiController.Instance.EnableGameHUD();
    }

    void OnDestroy()
    {
        if (client) { client.OnLobbyUpdated -= UpdateLobbyUI; client.OnGameStarted -= StartCountdownSequence; }
    }
}