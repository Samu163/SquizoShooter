using UnityEngine;
using TMPro;

public class KillCountUI : MonoBehaviour
{
    public static KillCountUI instance;
    public TextMeshProUGUI killCountText;

    private int currentKills = 0;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        UpdateUI(0);
    }

    public void UpdateUI(int kills)
    {
        currentKills = kills;

        if (killCountText != null)
        {
            killCountText.text = $"Kills: {currentKills}";
        }
    }

    public void AddKill()
    {
        currentKills++;
        UpdateUI(currentKills);
        Debug.Log($"[KillCountUI] Kill registered! Total kills: {currentKills}");
    }

    public void ResetKills()
    {
        UpdateUI(0);
    }

    public int GetCurrentKills()
    {
        return currentKills;
    }
}