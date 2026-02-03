using UnityEngine;
using System;

public class CombatUnitHealth : MonoBehaviour
{
    [Header("Health Config")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Stun Config")]
    [SerializeField] private float maxStun = 50f;          // Nguong chiu dung
    [SerializeField] private float stunRecoveryRate = 5f;  // Toc do giam stun
    [SerializeField] private float stunDuration = 2.0f;    // Thoi gian bi choang

    private float _currentHealth;
    private float _currentStun;
    private float _stunTimer;
    private bool _isStunned;

    public event Action OnDeath;
    public event Action<float> OnDamageTaken;
    public event Action OnStunned;
    public event Action OnRecovered;

    public float HealthPercentage => _currentHealth / maxHealth;
    public float StunPercentage => _currentStun / maxStun;
    public float CurrentHealth => _currentHealth;
    public bool IsDead => _currentHealth <= 0;
    public bool IsStunned => _isStunned;

    private void OnEnable()
    {
        ResetHealth();
    }

    private void Update()
    {
        if (IsDead) return;

        if (_isStunned)
        {
            _stunTimer -= Time.deltaTime;
            if (_stunTimer <= 0)
            {
                RecoverFromStun();
            }
        }
        else
        {
            // Tu giam thanh Stun theo thoi gian (Cooldown)
            if (_currentStun > 0)
            {
                _currentStun -= stunRecoveryRate * Time.deltaTime;
                if (_currentStun < 0) _currentStun = 0;
            }
        }
    }

    public void ResetHealth()
    {
        _currentHealth = maxHealth;
        _currentStun = 0;
        _isStunned = false;
        _stunTimer = 0;
    }

    public void TakeDamage(float amount, float stunAmount = 0f)
    {
        if (IsDead) return;

        _currentHealth -= amount;
        OnDamageTaken?.Invoke(amount);

        if (_currentHealth <= 0)
        {
            _currentHealth = 0;
            OnDeath?.Invoke();
            return;
        }

        // Logic Stun
        if (!_isStunned && stunAmount > 0)
        {
            _currentStun += stunAmount;
            if (_currentStun >= maxStun)
            {
                ApplyStun();
            }
        }
    }

    private void ApplyStun()
    {
        _isStunned = true;
        _currentStun = maxStun;
        _stunTimer = stunDuration;
        OnStunned?.Invoke();
    }

    private void RecoverFromStun()
    {
        _isStunned = false;
        _currentStun = 0;
        OnRecovered?.Invoke();
    }
}