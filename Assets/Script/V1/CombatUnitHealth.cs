using UnityEngine;
using System;

public class CombatUnitHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;
    private float currentHealth;

    public event Action OnDeath;
    public event Action<float> OnDamageTaken;

    public float HealthPercentage => currentHealth / maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsDead => currentHealth <= 0;

    public void ResetHealth()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount)
    {
        if (currentHealth <= 0) return;

        currentHealth -= amount;
        OnDamageTaken?.Invoke(amount);

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            OnDeath?.Invoke();
        }
    }
}