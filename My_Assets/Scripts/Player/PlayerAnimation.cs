using UnityEngine;

/// <summary>
/// Elige qué animación reproduce el cuerpo según hacia dónde apunta el jugador
/// y si se está moviendo. Los nombres deben coincidir con los estados del Animator.
/// </summary>
[RequireComponent(typeof(PlayerMovement), typeof(PlayerAim))]
public class PlayerAnimation : MonoBehaviour
{
    [SerializeField] private Animator bodyAnimator;

    [SerializeField, Tooltip("Velocidad por debajo de la cual se considera que está quieto")]
    private float moveThreshold = 0.1f;

    private PlayerMovement movement;
    private PlayerAim aim;
    private string currentState;

    private void Awake()
    {
        movement = GetComponent<PlayerMovement>();
        aim = GetComponent<PlayerAim>();
    }

    private void Update()
    {
        bool moving = movement.Velocity.sqrMagnitude > moveThreshold * moveThreshold;
        string prefix = moving ? "run_" : "idle_";
        PlayState(prefix + DirectionSuffix(aim.AimDirection));
    }

    /// <summary>Permite a otros sistemas (por ejemplo, la esquiva) forzar una animación.</summary>
    public void PlayState(string stateName)
    {
        if (stateName == currentState)
            return; // evita reiniciar la animación en cada fotograma

        bodyAnimator.Play(stateName);
        currentState = stateName;
    }

    private static string DirectionSuffix(Vector2 dir)
    {
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            return "side";            // izquierda la resuelve el flipX de PlayerAim
        return dir.y > 0f ? "up" : "down";
    }
}
