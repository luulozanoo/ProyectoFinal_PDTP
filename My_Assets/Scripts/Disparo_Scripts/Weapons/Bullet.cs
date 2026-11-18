using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Proyectil. No usa Rigidbody ni Collider: en cada fotograma lanza un CircleCast
/// a lo largo de su recorrido. Así nunca atraviesa paredes aunque vaya muy rápida,
/// y aguanta cientos de balas en pantalla sin coste de física.
/// </summary>
public class Bullet : MonoBehaviour
{
    [SerializeField, Tooltip("Radio de impacto en unidades (1 = un tile)")]
    private float radius = 0.08f;

    /// <summary>Lo asigna BulletPool. Si es null, la bala no viene de un pool y se destruye.</summary>
    public IObjectPool<Bullet> Pool { get; set; }

    private Vector2 direction;
    private float speed;
    private float lifeRemaining;
    private DamageInfo damage;
    private LayerMask hitMask;
    private bool active;

    public void Launch(Vector2 position, Vector2 direction, float speed, float lifetime,
                       DamageInfo damage, LayerMask hitMask)
    {
        transform.position = position;
        transform.right = direction; // orienta el sprite hacia donde va

        this.direction = direction;
        this.speed = speed;
        this.damage = damage;
        this.hitMask = hitMask;
        lifeRemaining = lifetime;
        active = true;
    }

    private void Update()
    {
        if (!active)
            return;

        float step = speed * Time.deltaTime;
        RaycastHit2D hit = Physics2D.CircleCast(transform.position, radius, direction, step, hitMask);

        if (hit.collider != null)
        {
            // GetComponentInParent: el collider puede estar en un hijo del enemigo.
            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            target?.TakeDamage(damage);

            transform.position = hit.centroid;
            Despawn();
            return;
        }

        transform.position += (Vector3)(direction * step);

        lifeRemaining -= Time.deltaTime;
        if (lifeRemaining <= 0f)
            Despawn();
    }

    private void Despawn()
    {
        if (!active)
            return; // evita devolverla dos veces al pool
        active = false;

        if (Pool != null)
            Pool.Release(this);
        else
            Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
