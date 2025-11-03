using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class HealStation : MonoBehaviour
{
    [Header("Configuración")]
    public float velocidadRotacion = 60f;
    public float tiempoCooldown = 5.0f;

    [Header("Referencias (Arrastrar)")]
    public GameObject modeloVisual; // El objeto 3D que rota
    public GameObject canvasCooldown; // El Canvas hijo
    public Image imagenCooldownRadial; // La imagen radial

    private bool enCooldown = false;
    private Collider miCollider; // Referencia al collider de esta estación

    void Start()
    {
        // Guardamos la referencia al collider para poder apagarlo y encenderlo
        miCollider = GetComponent<Collider>();

        // Estado inicial
        if (canvasCooldown != null) canvasCooldown.SetActive(false);
        if (modeloVisual != null) modeloVisual.SetActive(true);
        miCollider.enabled = true;
        enCooldown = false;
    }

    void Update()
    {
        // Solo rotamos si el modelo está activo
        if (modeloVisual != null && modeloVisual.activeSelf)
        {
            modeloVisual.transform.Rotate(Vector3.up * velocidadRotacion * Time.deltaTime, Space.Self);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Salir si ya estamos en cooldown o no es el jugador
        if (enCooldown || !other.CompareTag("Player"))
        {
            return;
        }

        PlayerController player = other.GetComponent<PlayerController>();

        // Si el jugador es válido y necesita curación
        if (player != null && player.health < player.maxHealth)
        {
            // 1. Curar al jugador
            player.health = player.maxHealth;
            Debug.Log("¡Jugador curado!");

            // 2. Actualizar la UI del jugador
            if (HealthBarUI.instance != null)
            {
                HealthBarUI.instance.UpdateUI(player.health, player.maxHealth);
            }

            // 3. Iniciar el cooldown
            StartCoroutine(IniciarCooldown());
        }
    }

    private IEnumerator IniciarCooldown()
    {
        // --- APAGADO ---
        enCooldown = true;
        miCollider.enabled = false; // <<< Desactivamos el collider (para no triggerear)
        if (modeloVisual != null) modeloVisual.SetActive(false); // <<< Desactivamos el modelo
        if (canvasCooldown != null) canvasCooldown.SetActive(true); // <<< Activamos el canvas

        // --- PROCESO DE COOLDOWN ---
        float tiempoPasado = 0f;
        if (imagenCooldownRadial != null) imagenCooldownRadial.fillAmount = 0;

        while (tiempoPasado < tiempoCooldown)
        {
            tiempoPasado += Time.deltaTime;

            if (imagenCooldownRadial != null)
            {
                imagenCooldownRadial.fillAmount = tiempoPasado / tiempoCooldown;
            }
            yield return null; // Espera al siguiente frame
        }

        // --- ENCENDIDO ---
        if (canvasCooldown != null) canvasCooldown.SetActive(false); // <<< Ocultamos el canvas
        if (modeloVisual != null) modeloVisual.SetActive(true); // <<< Reactivamos el modelo
        miCollider.enabled = true; // <<< Reactivamos el collider
        enCooldown = false;
    }
}