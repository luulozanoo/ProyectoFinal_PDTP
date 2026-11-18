using UnityEngine;

/// <summary>
/// Gira el arma hacia un punto del mundo y voltea los sprites
/// para que el personaje mire hacia donde apunta.
/// </summary>
public class PlayerAim : MonoBehaviour
{
    [SerializeField] private Transform weaponPivot;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private SpriteRenderer weaponRenderer;

    public Vector2 AimDirection { get; private set; } = Vector2.right;

    public void AimAt(Vector2 worldPoint)
    {
        Vector2 direction = worldPoint - (Vector2)weaponPivot.position;
        if (direction.sqrMagnitude < 0.0001f)
            return;

        AimDirection = direction.normalized;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        weaponPivot.rotation = Quaternion.Euler(0f, 0f, angle);

        bool aimingLeft = direction.x < 0f;
        bodyRenderer.flipX = aimingLeft; // el sprite lateral mira a la derecha

        // Voltear la escala del pivote (en vez de flipY del sprite) también voltea el FirePoint,
        // así las balas siguen saliendo por la punta del cañón al apuntar a la izquierda.
        weaponPivot.localScale = new Vector3(1f, aimingLeft ? -1f : 1f, 1f);

        // Si apunta hacia arriba, el arma se dibuja detrás del cuerpo.
        weaponRenderer.sortingOrder = direction.y > 0.3f
            ? bodyRenderer.sortingOrder - 1
            : bodyRenderer.sortingOrder + 1;
    }
}
