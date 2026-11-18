using UnityEngine;

/// <summary>
/// Mueve al jugador mediante física. No sabe nada de teclado ni de ratón:
/// recibe la dirección deseada desde fuera.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField, Tooltip("Velocidad máxima en unidades (tiles) por segundo")]
    private float speed = 5f;

    [SerializeField, Tooltip("Cuanto más alto, más rápido arranca y frena")]
    private float acceleration = 60f;

    private Rigidbody2D rb;
    private Vector2 desiredVelocity;

    public Vector2 Velocity => rb.linearVelocity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>Dirección de movimiento con magnitud entre 0 y 1.</summary>
    public void SetMoveInput(Vector2 input)
    {
        desiredVelocity = input * speed;
    }

    public void Stop()
    {
        desiredVelocity = Vector2.zero;
        rb.linearVelocity = Vector2.zero;
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = Vector2.MoveTowards(
            rb.linearVelocity, desiredVelocity, acceleration * Time.fixedDeltaTime);
    }
}
