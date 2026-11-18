using System.Collections;
using UnityEngine;

/// <summary>Tiñe el sprite un instante al recibir daño. Escucha el evento de Health.</summary>
[RequireComponent(typeof(Health))]
public class DamageFlash : MonoBehaviour
{
    [SerializeField] private SpriteRenderer target;
    [SerializeField] private Color flashColor = new Color(1f, 0.35f, 0.35f);
    [SerializeField] private float duration = 0.08f;

    private Health health;
    private Color originalColor;
    private Coroutine running;

    private void Awake()
    {
        health = GetComponent<Health>();
        if (target == null)
            target = GetComponentInChildren<SpriteRenderer>();
        originalColor = target.color;
    }

    private void OnEnable() => health.Damaged += OnDamaged;
    private void OnDisable() => health.Damaged -= OnDamaged;

    private void OnDamaged(DamageInfo _)
    {
        if (running != null)
            StopCoroutine(running);
        running = StartCoroutine(Flash());
    }

    private IEnumerator Flash()
    {
        target.color = flashColor;
        yield return new WaitForSeconds(duration);
        target.color = originalColor;
        running = null;
    }
}
