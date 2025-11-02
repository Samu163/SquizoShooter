using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthBarUI : MonoBehaviour
{
    public static HealthBarUI instance;

    public Image imagenRelleno;
    public TextMeshProUGUI textoSalud; 

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

    public void UpdateUI(float saludActual, float saludMaxima)
    {
        if (imagenRelleno != null)
        {
            float fill = saludActual / saludMaxima;
            imagenRelleno.fillAmount = Mathf.Clamp(fill, 0f, 1f);
        }

        if (textoSalud != null)
        {
            textoSalud.text = Mathf.RoundToInt(saludActual).ToString();

        }
    }
}