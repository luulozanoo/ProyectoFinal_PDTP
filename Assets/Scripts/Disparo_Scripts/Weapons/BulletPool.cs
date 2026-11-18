using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Patrón Object Pool: en lugar de crear y destruir cada bala (lento y genera basura),
/// las desactiva y las reutiliza. Mantiene un pool por cada tipo de bala.
///
/// No hace falta ponerlo en la escena: se crea solo la primera vez que alguien dispara,
/// y se destruye con la escena, así que al reiniciar la partida empieza limpio.
/// </summary>
public class BulletPool : MonoBehaviour
{
    private static BulletPool instance;

    public static BulletPool Instance
    {
        get
        {
            if (instance == null)
                instance = new GameObject("BulletPool").AddComponent<BulletPool>();
            return instance;
        }
    }

    [SerializeField] private int defaultCapacity = 64;
    [SerializeField] private int maxSize = 1000;

    private readonly Dictionary<Bullet, ObjectPool<Bullet>> pools = new();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    public Bullet Get(Bullet prefab)
    {
        if (!pools.TryGetValue(prefab, out ObjectPool<Bullet> pool))
        {
            pool = CreatePool(prefab);
            pools.Add(prefab, pool);
        }
        return pool.Get();
    }

    private ObjectPool<Bullet> CreatePool(Bullet prefab)
    {
        ObjectPool<Bullet> pool = null;
        pool = new ObjectPool<Bullet>(
            createFunc: () =>
            {
                Bullet bullet = Instantiate(prefab, transform);
                bullet.Pool = pool;
                return bullet;
            },
            actionOnGet: bullet => bullet.gameObject.SetActive(true),
            actionOnRelease: bullet => bullet.gameObject.SetActive(false),
            actionOnDestroy: bullet => Destroy(bullet.gameObject),
            collectionCheck: false,
            defaultCapacity: defaultCapacity,
            maxSize: maxSize);
        return pool;
    }
}
