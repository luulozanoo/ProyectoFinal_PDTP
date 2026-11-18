using UnityEngine;

/// <summary>
/// Configuración de un arma guardada como asset. Pistola, escopeta y ametralladora usan
/// el mismo código de Weapon; solo cambian estos datos. Para crear una:
/// clic derecho en Project → Create → Gungeon → Weapon Data.
/// </summary>
[CreateAssetMenu(fileName = "NewWeapon", menuName = "Gungeon/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("General")]
    [SerializeField] private string displayName = "Pistola";
    [SerializeField] private Bullet bulletPrefab;

    [Header("Daño")]
    [SerializeField, Min(0f)] private float damage = 1f;
    [SerializeField] private DamageType damageType = DamageType.Kinetic;

    [Header("Disparo")]
    [SerializeField, Min(0.1f), Tooltip("Disparos por segundo")]
    private float fireRate = 4f;

    [SerializeField, Min(1), Tooltip("Balas por disparo (escopeta > 1)")]
    private int bulletsPerShot = 1;

    [SerializeField, Range(0f, 180f), Tooltip("Ángulo total del abanico cuando hay varias balas")]
    private float spreadAngle = 0f;

    [SerializeField, Range(0f, 30f), Tooltip("Imprecisión aleatoria por bala, en grados")]
    private float inaccuracy = 2f;

    [Header("Balas")]
    [SerializeField, Min(0.1f)] private float bulletSpeed = 14f;

    [SerializeField, Range(0f, 0.5f), Tooltip("Variación aleatoria de velocidad (0.1 = ±10 %)")]
    private float speedVariance = 0f;

    [SerializeField, Min(0.1f), Tooltip("Distancia máxima antes de desaparecer, en tiles")]
    private float range = 12f;

    public string DisplayName => displayName;
    public Bullet BulletPrefab => bulletPrefab;
    public float Damage => damage;
    public DamageType DamageType => damageType;
    public float FireRate => fireRate;
    public int BulletsPerShot => bulletsPerShot;
    public float SpreadAngle => spreadAngle;
    public float Inaccuracy => inaccuracy;
    public float BulletSpeed => bulletSpeed;
    public float SpeedVariance => speedVariance;
    public float Range => range;
}
