using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class WeaponStation : MonoBehaviour
{
    [Header("Configuración de Red")]
    public int weaponStationID = -1;
    public int weaponID = 1;

    [Header("Configuración")]
    public float velocidadRotacion = 60f;
    public float tiempoCooldown = 5.0f; // Sincroniza esto con el servidor

    [Header("Referencias")]
    public GameObject modeloVisual;
    public GameObject canvasCooldown;
    public Image imagenCooldownRadial;

    private UDPClient udpClient;
    private Collider miCollider;
    private bool enCooldownLocal = false;

    void Start()
    {
        miCollider = GetComponent<Collider>();
        udpClient = FindObjectOfType<UDPClient>();

        if (udpClient != null)
        {
            //Modificar esto para que sea RegisterWeaponStation
            //udpClient.RegisterWeaponStation(healStationID, this);
        }
        else
        {
            Debug.LogError("¡No se encontró UDPClient en la escena!", this);
        }

        // Estado inicial por defecto (el servidor lo corregirá si es necesario)
        SetNetworkState(false); // Falso = disponible
    }

    void Update()
    {
        if (modeloVisual != null && modeloVisual.activeSelf)
        {
            modeloVisual.transform.Rotate(Vector3.up * velocidadRotacion * Time.deltaTime, Space.Self);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // No enviamos peticiones si ya estamos en cooldown
        if (enCooldownLocal || !other.CompareTag("Player"))
        {
            return;
        }

        PlayerController player = other.GetComponent<PlayerController>();

        // Si es el JUGADOR LOCAL, enviar petición
        if (player != null && player.IsLocalPlayer)
        {
            enCooldownLocal = true; // Prevenimos spam de peticiones
            //Modificar esto para que sea SendWeaponRequest
            //udpClient.SendWeaponRequest(weaponStationID);
            WeaponManager shooting = player.GetComponent<WeaponManager>();
            shooting.SwitchWeapon(weaponID);
        }
    }

    // --- FUNCIÓN ÚNICA LLAMADA POR EL SERVIDOR ---

    // El servidor nos dice en qué estado ponernos
    public void SetNetworkState(bool isCooldown)
    {
        enCooldownLocal = isCooldown;
        miCollider.enabled = !isCooldown; // Collider activo si NO está en cooldown

        if (modeloVisual != null) modeloVisual.SetActive(!isCooldown);
        if (canvasCooldown != null) canvasCooldown.SetActive(isCooldown);

        if (isCooldown)
        {
            // Si entramos en cooldown, iniciar la corrutina visual
            StopAllCoroutines(); // Detener cualquier corrutina anterior
            StartCoroutine(CooldownVisual());
        }
        else
        {
            // Si volvemos a estar disponibles, detener corrutinas
            StopAllCoroutines();
        }
    }

    // Corrutina puramente VISUAL para el radial
    private IEnumerator CooldownVisual()
    {
        float tiempoPasado = 0f;
        if (imagenCooldownRadial != null) imagenCooldownRadial.fillAmount = 0;

        while (tiempoPasado < tiempoCooldown)
        {
            tiempoPasado += Time.deltaTime;
            if (imagenCooldownRadial != null)
            {
                imagenCooldownRadial.fillAmount = tiempoPasado / tiempoCooldown;
            }
            yield return null;
        }

        if (imagenCooldownRadial != null) imagenCooldownRadial.fillAmount = 1;
    }
}
