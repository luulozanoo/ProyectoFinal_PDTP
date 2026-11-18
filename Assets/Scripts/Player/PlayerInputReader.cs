using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Única clase que lee el teclado, el ratón o el mando.
/// El resto del jugador solo consulta sus propiedades, así que más adelante
/// se puede sustituir por otra fuente de entrada (por ejemplo, un agente de ML-Agents).
/// </summary>
public class PlayerInputReader : MonoBehaviour
{
    public Vector2 Move { get; private set; }
    public Vector2 AimScreenPosition { get; private set; }
    public bool FireHeld { get; private set; }

    /// <summary>Se lanza una vez cada vez que se pulsa el botón de esquivar.</summary>
    public event Action DodgePressed;

    private InputAction moveAction;
    private InputAction fireAction;
    private InputAction dodgeAction;

    private void Awake()
    {
        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");
        moveAction.AddBinding("<Gamepad>/leftStick");

        fireAction = new InputAction("Fire", InputActionType.Button, "<Mouse>/leftButton");
        fireAction.AddBinding("<Gamepad>/rightTrigger");

        dodgeAction = new InputAction("Dodge", InputActionType.Button, "<Keyboard>/space");
        dodgeAction.AddBinding("<Mouse>/rightButton");
        dodgeAction.AddBinding("<Gamepad>/leftTrigger");
        dodgeAction.performed += OnDodgePerformed;
    }

    private void OnEnable()
    {
        moveAction.Enable();
        fireAction.Enable();
        dodgeAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        fireAction.Disable();
        dodgeAction.Disable();
    }

    private void OnDestroy()
    {
        dodgeAction.performed -= OnDodgePerformed;
        moveAction.Dispose();
        fireAction.Dispose();
        dodgeAction.Dispose();
    }

    private void Update()
    {
        // ClampMagnitude evita que en diagonal se vaya más rápido.
        Move = Vector2.ClampMagnitude(moveAction.ReadValue<Vector2>(), 1f);
        FireHeld = fireAction.IsPressed();

        if (Mouse.current != null)
            AimScreenPosition = Mouse.current.position.ReadValue();
    }

    private void OnDodgePerformed(InputAction.CallbackContext _) => DodgePressed?.Invoke();
}
