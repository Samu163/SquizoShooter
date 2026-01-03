using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RoundScoreUI : MonoBehaviour
{
    public static RoundScoreUI Instance;

    [Header("UI References")]
    public Slider scoreSlider;         
    public TextMeshProUGUI roundWinnerText; 

    private int currentScore = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (roundWinnerText != null) roundWinnerText.gameObject.SetActive(false);
    }

    public void Configure(int maxRounds)
    {
        currentScore = 0;
        if (scoreSlider != null)
        {
            scoreSlider.minValue = 0;
            scoreSlider.maxValue = maxRounds;
            scoreSlider.value = 0;
            scoreSlider.wholeNumbers = true;
        }
    }

    public void AddWin()
    {
        currentScore++;
        if (scoreSlider != null)
        {
            scoreSlider.value = currentScore;
        }
    }

    public void ShowRoundWinner(string winnerName, bool isMatchWin = false)
    {
        if (roundWinnerText != null)
        {
            string msg = isMatchWin ? $"Winner of the game: {winnerName}" : $"{winnerName} won the round";
            roundWinnerText.text = msg;
            roundWinnerText.gameObject.SetActive(true);
        }
    }

    public void HideRoundMessage()
    {
        if (roundWinnerText != null) roundWinnerText.gameObject.SetActive(false);
    }

    public void ResetScore()
    {
        currentScore = 0;
        if (scoreSlider != null) scoreSlider.value = 0;
        HideRoundMessage();
    }
}