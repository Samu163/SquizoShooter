using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthBarUI : MonoBehaviour
{
    public static HealthBarUI instance;

    [Header("Referencias UI")]
    public Image imagenRelleno;
    public TextMeshProUGUI textoSalud;

    [Header("Feedback Visual (Pantalla)")]
    public Image imagenFeedback;

    [Header("Configuración Daño (Rojo)")]
    public Color colorDano = new Color(1f, 0f, 0f, 0.3f);
    public float velocidadFadeDano = 5f;

    [Header("Configuración Curación (Verde)")]
    public Color colorCuracion = new Color(0f, 1f, 0f, 0.3f);
    public float velocidadFadeCuracion = 2f;

    private float _saludAnterior;
    private float _velocidadFadeActual;
    private bool _esPrimerFrame = true;

    void Awake()
    {
        if (instance == null) instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        if (imagenFeedback != null)
        {
            imagenFeedback.color = Color.clear;
        }
    }

    void Update()
    {
        if (imagenFeedback != null && imagenFeedback.color.a > 0)
        {
            imagenFeedback.color = Color.Lerp(imagenFeedback.color, Color.clear, _velocidadFadeActual * Time.deltaTime);
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

        if (_esPrimerFrame)
        {
            _saludAnterior = saludActual;
            _esPrimerFrame = false;

  
        }

        if (saludActual < _saludAnterior)
        {
            TriggerFeedback(colorDano, velocidadFadeDano);
        }
        else if (saludActual > _saludAnterior)
        {
            TriggerFeedback(colorCuracion, velocidadFadeCuracion);
        }

        _saludAnterior = saludActual;
    }

    public void TriggerFeedback(Color color, float velocidad)
    {
        if (imagenFeedback != null)
        {
            imagenFeedback.color = color;
            _velocidadFadeActual = velocidad;
        }
    }
}