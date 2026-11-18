using UnityEngine;

/// <summary>
/// Coordina los componentes del jugador: pasa la entrada al movimiento, al apuntado y al arma.
/// Más adelante aquí irá la máquina de estados (Normal, Esquivando, Muerto).
/// </summary>
[RequireComponent(typeof(PlayerInputReader), typeof(PlayerMovement), typeof(PlayerAim))]
public class PlayerController : MonoBehaviour
{
    [SerializeField, Tooltip("El componente Weapon del objeto Gun")]
    private Weapon weapon;

    private PlayerInputReader input;
    private PlayerMovement movement;
    private PlayerAim aim;
    private Camera mainCamera;

    private void Awake()
    {
        input = GetComponent<PlayerInputReader>();
        movement = GetComponent<PlayerMovement>();
        aim = GetComponent<PlayerAim>();
        mainCamera = Camera.main;

        if (weapon == null)
            weapon = GetComponentInChildren<Weapon>();
    }

    private void Update()
    {
        movement.SetMoveInput(input.Move);

        Vector2 mouseWorld = mainCamera.ScreenToWorldPoint(input.AimScreenPosition);
        aim.AimAt(mouseWorld);

        if (input.FireHeld && weapon != null)
            weapon.TryFire(aim.AimDirection);
    }
}
