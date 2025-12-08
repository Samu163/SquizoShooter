using UnityEngine;

public class LifeComponent : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] public float maxHealth = 100f;
    [SerializeField] public float health = 100f;

    private PlayerController playerController;
    private bool isDead = false;

    public bool IsDead => isDead;
    public float Health => health;
    public float MaxHealth => maxHealth;

    public void Initialize(PlayerController controller)
    {
        playerController = controller;
        health = maxHealth;
    }

    public void InitializeHealthUI()
    {
        health = maxHealth;
        if (HealthBarUI.instance != null)
        {
            HealthBarUI.instance.UpdateUI(health, maxHealth);
        }
        else
        {
            Debug.LogError("No se encontro la HealthBarUI!");
        }
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        health -= damage;
        health = Mathf.Clamp(health, 0f, maxHealth);

        Debug.LogWarning($"[LifeComponent] Player took {damage} damage. Health: {health}/{maxHealth}");

        if (HealthBarUI.instance != null)
        {
            HealthBarUI.instance.UpdateUI(health, maxHealth);
        }

        CheckDeath();
    }

    void CheckDeath()
    {
        if (health <= 0f && !isDead)
        {
            Die();
        }
    }

    void Die()
    {
        isDead = true;
        health = 0f;
        Debug.Log("[LifeComponent] Player died!");

        if (HealthBarUI.instance != null)
        {
            HealthBarUI.instance.UpdateUI(0f, maxHealth);
        }

        // Notify controller to handle death logic
        if (playerController != null)
        {
            playerController.HandleDeath();
        }
    }

    public void ResetHealth()
    {
        health = maxHealth;
        isDead = false;
    }

    public void UpdateHealth(float newHealth, bool isLocalPlayer)
    {
        health = newHealth;

        if (isLocalPlayer && HealthBarUI.instance != null)
        {
            HealthBarUI.instance.UpdateUI(health, maxHealth);
        }
    }
}