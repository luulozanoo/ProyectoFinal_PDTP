using UnityEngine;

public class IceProjectile : MonoBehaviour
{
    public float speed = 9f;
    public int damage = 1;
    public float lifetime = 4f;
    public float slowDuration = 2f;

    private Rigidbody2D rb;

    public void Launch(Vector2 direction)
    {
        rb = GetComponent<Rigidbody2D>();
        direction.Normalize();

        rb.linearVelocity = direction * speed;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        Destroy(gameObject, lifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy") || other.isTrigger) return;

        if (other.CompareTag("Player"))
        {
            Debug.Log($"¡El jugador recibió {damage} de daño y ralentización por {slowDuration}s!");
        }

        Destroy(gameObject);
    }
}