using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class HealStation : MonoBehaviour
{
    [Header("Configuración de Red")]
    public int healStationID = -1;

    [Header("Configuración")]
    public float velocidadRotacion = 60f;
    public float tiempoCooldown = 5.0f;

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
            udpClient.RegisterHealStation(healStationID, this);
        }
        else
        {
            Debug.LogError("¡No se encontró UDPClient en la escena!", this);
        }

        SetNetworkState(false);
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
        if (enCooldownLocal || !other.CompareTag("Player"))
        {
            return;
        }

        PlayerController player = other.GetComponent<PlayerController>();

        if (player != null && player.IsLocalPlayer)
        {
            enCooldownLocal = true; 
            udpClient.SendHealRequest(healStationID);
        }
    }

    public void SetNetworkState(bool isCooldown)
    {
        enCooldownLocal = isCooldown;
        miCollider.enabled = !isCooldown;

        if (modeloVisual != null) modeloVisual.SetActive(!isCooldown);
        if (canvasCooldown != null) canvasCooldown.SetActive(isCooldown);

        if (isCooldown)
        {
            StopAllCoroutines();
            StartCoroutine(CooldownVisual());
        }
        else
        {
            StopAllCoroutines();
        }
    }

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