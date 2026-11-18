using System;
using UnityEngine;

/// <summary>
/// Dispara según los datos de un WeaponData. Sirve igual para el jugador y para los enemigos:
/// lo único que cambia es a qué capas pueden golpear sus balas (hitMask).
/// </summary>
public class Weapon : MonoBehaviour
{
    [SerializeField] private WeaponData data;

    [SerializeField, Tooltip("Punto vacío en la punta del cañón")]
    private Transform firePoint;

    [SerializeField, Tooltip("Capas que pueden golpear las balas. Jugador: Enemy + Wall")]
    private LayerMask hitMask;

    [SerializeField, Tooltip("Quién dispara. Si se deja vacío, se usa el objeto raíz")]
    private GameObject owner;

    private float nextFireTime;

    public WeaponData Data => data;

    /// <summary>Para efectos, sonido, retroceso de cámara o estadísticas.</summary>
    public event Action<Weapon> Fired;

    private void Awake()
    {
        if (firePoint == null)
            firePoint = transform;
        if (owner == null)
            owner = transform.root.gameObject;
    }

    public void Equip(WeaponData newData)
    {
        data = newData;
        nextFireTime = 0f;
    }

    /// <summary>Intenta disparar. Devuelve false si el arma aún se está recargando.</summary>
    public bool TryFire(Vector2 aimDirection)
    {
        if (data == null || data.BulletPrefab == null || Time.time < nextFireTime)
            return false;

        nextFireTime = Time.time + 1f / data.FireRate;

        float baseAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
        int count = data.BulletsPerShot;

        for (int i = 0; i < count; i++)
        {
            // Reparte las balas en abanico de forma uniforme y añade algo de imprecisión.
            float t = count == 1 ? 0.5f : i / (count - 1f);
            float angle = baseAngle
                          + Mathf.Lerp(-data.SpreadAngle / 2f, data.SpreadAngle / 2f, t)
                          + UnityEngine.Random.Range(-data.Inaccuracy, data.Inaccuracy);

            Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
            float speed = data.BulletSpeed
                          * UnityEngine.Random.Range(1f - data.SpeedVariance, 1f + data.SpeedVariance);

            var damage = new DamageInfo(data.Damage, data.DamageType, direction, owner);

            Bullet bullet = BulletPool.Instance.Get(data.BulletPrefab);
            bullet.Launch(firePoint.position, direction, speed, data.Range / speed, damage, hitMask);
        }

        Fired?.Invoke(this);
        return true;
    }
}
