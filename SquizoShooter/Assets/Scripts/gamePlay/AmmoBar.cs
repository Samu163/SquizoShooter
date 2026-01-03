using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AmmoBarUI : MonoBehaviour
{
    public static AmmoBarUI instance;

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

    public void UpdateUI(float ammoActual, float ammoMaximo)
    {
        if (imagenRelleno != null)
        {
            float fill = ammoActual / ammoMaximo;
            imagenRelleno.fillAmount = Mathf.Clamp(fill, 0f, 1f);
        }

        if (textoSalud != null)
        {
            textoSalud.text = Mathf.RoundToInt(ammoActual).ToString();

        }
    }
}
