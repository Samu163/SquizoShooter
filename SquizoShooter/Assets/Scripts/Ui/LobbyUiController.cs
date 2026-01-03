using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

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
        if (client == null) client = FindObjectOfType<UDPClient>();

        bool isHost = false;
        if (GameManager.Instance != null)
            isHost = GameManager.Instance.GetGameMode() == GameManager.GameMode.Server;

        if (startButton)
        {
            startButton.gameObject.SetActive(isHost);
            startButton.interactable = false;
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(OnStartClicked);
        }

        if (readyButton)
        {
            readyButton.gameObject.SetActive(!isHost);
            readyButton.onClick.RemoveAllListeners();
            readyButton.onClick.AddListener(OnReadyClicked);

            isLocalReady = false;
            UpdateReadyButtonVisuals();
        }

        if (exitButton)
        {
            exitButton.onClick.RemoveAllListeners();
            exitButton.onClick.AddListener(OnExitClicked);
        }

        if (roundsSlider != null)
        {
            roundsSlider.onValueChanged.RemoveAllListeners(); 

            if (!isHost)
            {
                roundsSlider.gameObject.SetActive(false);
                if (roundsValueText) roundsValueText.gameObject.SetActive(false);
            }
            else
            {
                roundsSlider.gameObject.SetActive(true);
                if (roundsValueText) roundsValueText.gameObject.SetActive(true);

                roundsSlider.interactable = true;
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
            client.OnLobbyUpdated -= UpdateLobbyUI; 
            client.OnLobbyUpdated += UpdateLobbyUI;

            client.OnGameStarted -= StartCountdownSequence;
            client.OnGameStarted += StartCountdownSequence;

            if (client.CurrentLobbyPlayers != null && client.CurrentLobbyPlayers.Count > 0)
            {
                UpdateLobbyUI(client.CurrentLobbyPlayers);
            }
        }

        lobbyPanel.SetActive(true);
        if (countdownText != null) countdownText.gameObject.SetActive(false);
    }

    void UpdateRoundsText(float value)
    {
        if (roundsValueText != null) roundsValueText.text = $"{value}";
    }

    void OnReadyClicked()
    {
        isLocalReady = !isLocalReady;
        if (client) client.SendReadyState(isLocalReady);
        UpdateReadyButtonVisuals();
    }

    void UpdateReadyButtonVisuals()
    {
        if (readyButton == null) return;
        var btnText = readyButton.GetComponentInChildren<TextMeshProUGUI>();
        if (btnText != null) btnText.text = isLocalReady ? "CANCEL READY" : "READY";
        readyButton.image.color = isLocalReady ? Color.green : Color.white;
    }

    void OnStartClicked()
    {
        var server = FindObjectOfType<UDPServer>();
        if (server != null)
        {
            int rounds = 5;
            if (roundsSlider != null) rounds = (int)roundsSlider.value;
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

            if (client != null && p.Key == client.ClientKey)
            {
                txt.color = localPlayerHighlightColor;
                txt.fontStyle = FontStyles.Bold;
            }
            else txt.color = defaultPlayerColor;

            if (!p.IsReady) allReady = false;
        }

        if (startButton != null && startButton.gameObject.activeSelf)
        {
            bool clientsReady = true;
            if (client != null)
            {
                foreach (var p in players) if (p.Key != client.ClientKey && !p.IsReady) clientsReady = false;
            }
            startButton.interactable = clientsReady && players.Count > 0;
        }
    }

    void StartCountdownSequence() { StartCoroutine(CountdownCoroutine()); }

    IEnumerator CountdownCoroutine()
    {
        lobbyPanel.SetActive(false);
        if (countdownText)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = "3"; yield return new WaitForSeconds(1);
            countdownText.text = "2"; yield return new WaitForSeconds(1);
            countdownText.text = "1"; yield return new WaitForSeconds(1);
            countdownText.text = "GO!";
        }

        if (client) client.SpawnMyPlayerNow();
        yield return new WaitForSeconds(0.5f);

        if (countdownText) countdownText.gameObject.SetActive(false);
        if (UiController.Instance) UiController.Instance.EnableGameHUD();
    }

    void OnDestroy()
    {
        if (client)
        {
            client.OnLobbyUpdated -= UpdateLobbyUI;
            client.OnGameStarted -= StartCountdownSequence;
        }
    }
}