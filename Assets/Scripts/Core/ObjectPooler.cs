using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Central spawner / object pooler. Hosts strongly typed pools for player projectiles,
    /// enemy projectiles, chickens, and transient effects. Budgets satisfy the 200 projectile /
    /// 50 entity target at 60 FPS with zero post-warmup allocation.
    /// </summary>
    public class ObjectPooler : MonoBehaviour
    {
        public static ObjectPooler Instance { get; private set; }

        [Header("Prefabs")]
        [SerializeField] public Projectile playerProjectilePrefab;
        [SerializeField] public EnemyProjectile enemyProjectilePrefab;
        [SerializeField] public Chicken chickenEnemyPrefab;
        [SerializeField] public HazardArea hazardPrefab;
        [SerializeField] public EnemyProjectile shrapnelPrefab;

        [Header("Budgets")]
        [SerializeField] public int playerProjectileBudget = GameConstants.PlayerProjectileBudget;
        [SerializeField] public int enemyProjectileBudget = GameConstants.EnemyProjectileBudget;
        [SerializeField] public int enemyBudget = GameConstants.EnemyBudget;
        [SerializeField] public int effectBudget = GameConstants.EffectBudget;

        private Pool<Projectile> playerProjectilePool;
        private Pool<EnemyProjectile> enemyProjectilePool;
        private Pool<Chicken> enemyPool;
        private Pool<HazardArea> hazardPool;
        private Pool<EnemyProjectile> shrapnelPool;

        private bool initialized;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;

            Transform tf = transform;
            playerProjectilePool = new Pool<Projectile>(playerProjectilePrefab, playerProjectileBudget, tf);
            enemyProjectilePool = new Pool<EnemyProjectile>(enemyProjectilePrefab, enemyProjectileBudget, tf);
            enemyPool = new Pool<Chicken>(chickenEnemyPrefab, enemyBudget, tf);
            hazardPool = new Pool<HazardArea>(hazardPrefab, effectBudget, tf);
            shrapnelPool = new Pool<EnemyProjectile>(shrapnelPrefab, effectBudget, tf);
        }

        /// <summary>Preallocates every pool before combat begins.</summary>
        public void Prewarm()
        {
            Initialize();
        }

        // ---------------------------------------------------------------- Player projectiles
        public Projectile SpawnPlayerProjectile(Vector3 position, Quaternion rotation)
        {
            Initialize();
            Projectile projectile = playerProjectilePool.Spawn(position, rotation);
            projectile.ResetState();
            return projectile;
        }

        public void ReturnPlayerProjectile(Projectile projectile)
        {
            playerProjectilePool?.Release(projectile);
        }

        // ---------------------------------------------------------------- Enemy projectiles
        public EnemyProjectile SpawnEnemyProjectile(Vector3 position, Quaternion rotation)
        {
            Initialize();
            EnemyProjectile projectile = enemyProjectilePool.Spawn(position, rotation);
            projectile.ResetState();
            return projectile;
        }

        public void ReturnEnemyProjectile(EnemyProjectile projectile)
        {
            enemyProjectilePool?.Release(projectile);
        }

        // ---------------------------------------------------------------- Enemies
        public Chicken SpawnChicken(Vector3 position, Quaternion rotation)
        {
            Initialize();
            Chicken chicken = enemyPool.Spawn(position, rotation);
            chicken.ResetState();
            return chicken;
        }

        public void ReturnChicken(Chicken chicken)
        {
            enemyPool?.Release(chicken);
        }

        // ---------------------------------------------------------------- Hazards (napalm)
        public HazardArea SpawnHazard(Vector3 position)
        {
            Initialize();
            return hazardPool.Spawn(position, Quaternion.identity);
        }

        public void ReturnHazard(HazardArea hazard)
        {
            hazardPool?.Release(hazard);
        }

        // ---------------------------------------------------------------- Shrapnel
        public EnemyProjectile SpawnShrapnel(Vector3 position, Quaternion rotation)
        {
            Initialize();
            EnemyProjectile projectile = shrapnelPool.Spawn(position, rotation);
            projectile.ResetState();
            return projectile;
        }

        public void ReturnShrapnel(EnemyProjectile projectile)
        {
            shrapnelPool?.Release(projectile);
        }

        /// <summary>Flushes everything back to the pools at the end of a run.</summary>
        public void FlushAll()
        {
            playerProjectilePool?.ReleaseAll();
            enemyProjectilePool?.ReleaseAll();
            enemyPool?.ReleaseAll();
            hazardPool?.ReleaseAll();
            shrapnelPool?.ReleaseAll();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
