using System;
using UnityEngine;

/// <summary>
/// Vida genérica. La usan el jugador y los enemigos.
/// No decide qué pasa al morir: lo anuncia con eventos y otros componentes reaccionan (patrón Observer).
/// </summary>
public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private float maxHealth = 6f;

    [SerializeField, Tooltip("Útil para enemigos de prueba. El jugador no debería destruirse.")]
    private bool destroyOnDeath;

    public float Current { get; private set; }
    public float Max => maxHealth;
    public bool IsDead { get; private set; }

    /// <summary>Lo activa, por ejemplo, la esquiva.</summary>
    public bool IsInvulnerable { get; set; }

    public event Action<DamageInfo> Damaged;
    public event Action<float, float> Changed; // (actual, máxima), para la barra de vida
    public event Action Died;

    private void Awake()
    {
        Current = maxHealth;
    }

    public void TakeDamage(DamageInfo info)
    {
        if (IsDead || IsInvulnerable || info.Amount <= 0f)
            return;

        Current = Mathf.Max(0f, Current - info.Amount);
        Damaged?.Invoke(info);
        Changed?.Invoke(Current, maxHealth);

        if (Current <= 0f)
            Die();
    }

    public void Heal(float amount)
    {
        if (IsDead || amount <= 0f)
            return;

        Current = Mathf.Min(maxHealth, Current + amount);
        Changed?.Invoke(Current, maxHealth);
    }

    /// <summary>Para reiniciar la partida sin recargar el objeto.</summary>
    public void ResetHealth()
    {
        IsDead = false;
        Current = maxHealth;
        Changed?.Invoke(Current, maxHealth);
    }

    private void Die()
    {
        IsDead = true;
        Died?.Invoke();

        if (destroyOnDeath)
            Destroy(gameObject);
    }
}
