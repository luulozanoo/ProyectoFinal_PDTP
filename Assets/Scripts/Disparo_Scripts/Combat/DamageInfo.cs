using UnityEngine;

/// <summary>Tipos de daño. La mutación de enemigos usará estas categorías para sus resistencias.</summary>
public enum DamageType
{
    Kinetic,
    Fire,
    Ice,
    Poison
}

/// <summary>
/// Todo lo que necesita saber quien recibe un golpe.
/// Es un struct: se copia al pasarlo y no genera basura en memoria con cada bala.
/// </summary>
public readonly struct DamageInfo
{
    public readonly float Amount;
    public readonly DamageType Type;
    public readonly Vector2 Direction;
    public readonly GameObject Source;

    public DamageInfo(float amount, DamageType type, Vector2 direction, GameObject source)
    {
        Amount = amount;
        Type = type;
        Direction = direction;
        Source = source;
    }
}

/// <summary>
/// Cualquier cosa que pueda recibir daño: jugador, enemigos, barriles, cajas...
/// Las balas solo conocen esta interfaz, no las clases concretas.
/// </summary>
public interface IDamageable
{
    void TakeDamage(DamageInfo info);
}
