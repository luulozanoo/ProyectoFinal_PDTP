using System.Collections;
using UnityEngine;

public class IceSpecterAI : MonoBehaviour
{
    [Header("Referencias")]
    public Transform firePoint;
    public GameObject iceProjectilePrefab;
    public SpriteRenderer visualRenderer;
    public LayerMask obstacleLayer;

    [Header("Movimiento y Detección")]
    public float moveSpeed = 2.5f;
    public float detectionRange = 8f;
    public float preferredDistance = 4.5f;
    public float retreatDistance = 2.5f;

    [Header("Combate")]
    public float chargeTime = 0.5f;
    public float attackCooldown = 2.2f;
    public int maxHealth = 3;

    private Transform player;
    private Rigidbody2D rb;
    private int currentHealth;
    private bool isAttacking;
    private float nextAttackTime;
    private Color originalColor;

    private void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        currentHealth = maxHealth;

        if (visualRenderer != null)
            originalColor = visualRenderer.color;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
    }

    private void FixedUpdate()
    {
        if (player == null || isAttacking)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float distance = Vector2.Distance(transform.position, player.position);

        if (distance > detectionRange || !HasLineOfSight())
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 dirToPlayer = (player.position - transform.position).normalized;

        if (distance > preferredDistance)
        {
            rb.linearVelocity = dirToPlayer * moveSpeed;
        }
        else if (distance < retreatDistance)
        {
            rb.linearVelocity = -dirToPlayer * (moveSpeed * 0.75f);
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (Time.time >= nextAttackTime)
        {
            StartCoroutine(AttackRoutine());
        }
    }

    private bool HasLineOfSight()
    {
        Vector2 origin = transform.position;
        Vector2 direction = (player.position - transform.position).normalized;
        float distance = Vector2.Distance(origin, player.position);

        RaycastHit2D hit = Physics2D.Raycast(origin, direction, distance, obstacleLayer);
        return hit.collider == null;
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        rb.linearVelocity = Vector2.zero;

        if (visualRenderer != null)
            visualRenderer.color = Color.white;

        yield return new WaitForSeconds(chargeTime);

        if (player != null && iceProjectilePrefab != null && firePoint != null)
        {
            Vector2 shootDir = (player.position - firePoint.position).normalized;
            GameObject projObj = Instantiate(iceProjectilePrefab, firePoint.position, Quaternion.identity);
            IceProjectile proj = projObj.GetComponent<IceProjectile>();
            if (proj != null)
                proj.Launch(shootDir);
        }

        if (visualRenderer != null)
            visualRenderer.color = originalColor;

        nextAttackTime = Time.time + attackCooldown;
        isAttacking = false;
    }

    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0)
            Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectionRange);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, preferredDistance);
    }
}