using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Player hull + shield system. Driven by <see cref="InputController"/> for movement
    /// and fires the equipped <see cref="WeaponInstance"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class PlayerShip : MonoBehaviour
    {
        public static PlayerShip Instance { get; private set; }

        [Header("Movement")]
        [SerializeField] private float maxSpeed = 640f;
        [SerializeField] private float acceleration = 4200f;
        [SerializeField] private float damping = 12f;
        [SerializeField] private float horizontalBounds = 470f;
        [SerializeField] private float verticalBoundsTop = 820f;
        [SerializeField] private float verticalBoundsBottom = -860f;

        [Header("Hull")]
        [SerializeField] private float maxHull = 100f;
        [SerializeField] private float invulnerabilityTime = 1.2f;
        [SerializeField] private bool spawnProtection = true;

        [Header("Shields")]
        [SerializeField] private float maxShield = 50f;
        [SerializeField] private float shieldRegenPerSecond = 6f;
        [SerializeField] private float shieldRegenDelay = 2.5f;

        [Header("Weapon")]
        [SerializeField] private Vector2 muzzleOffset = new Vector2(0f, 130f);
        [SerializeField] private string startingBlueprintId = "blueprint.standard";

        public Vector2 Position => body != null ? body.position : (Vector2)transform.position;
        public float HullPct => maxHull <= 0f ? 0f : currentHull / maxHull;
        public float ShieldPct => maxShield <= 0f ? 0f : currentShield / maxShield;
        public bool IsVulnerable => !spawnProtection && invulnerabilityTimer <= 0f;
        public WeaponInstance EquippedWeapon { get; set; }

        private Rigidbody2D body;
        private Vector2 velocity;
        private float currentHull;
        private float currentShield;
        private float invulnerabilityTimer;
        private float shieldRegenTimer;
        private bool alive;
        private int instanceId;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Dynamic;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.freezeRotation = true;
            instanceId = GetInstanceID();
        }

        private void Start()
        {
            Respawn();
        }

        /// <summary>Full reset used on combat loop start and after death.</summary>
        public void Respawn()
        {
            currentHull = maxHull;
            currentShield = maxShield;
            velocity = Vector2.zero;
            invulnerabilityTimer = 0f;
            shieldRegenTimer = 0f;
            spawnProtection = true;
            alive = true;
            transform.position = new Vector3(0f, -640f, 0f);

            EquippedWeapon = WeaponCraftingEngine.Instance != null
                ? WeaponCraftingEngine.Instance.BuildFromLoadout(SaveSystem.Instance?.Profile?.loadout)
                : new WeaponInstance();

            gameObject.SetActive(true);
            Invoke(nameof(EndSpawnProtection), invulnerabilityTime);
        }

        private void EndSpawnProtection()
        {
            spawnProtection = false;
        }

        /// <summary>Applies a movement input vector (normalized -1..1) from the touch controller.</summary>
        public void ApplyMovementInput(Vector2 normalizedInput, float deltaTime)
        {
            if (!alive) return;

            Vector2 desiredVelocity = normalizedInput * maxSpeed;
            velocity = Vector2.MoveTowards(velocity, desiredVelocity, acceleration * deltaTime);
            if (normalizedInput.sqrMagnitude < 0.01f)
            {
                velocity = Vector2.Lerp(velocity, Vector2.zero, damping * deltaTime);
            }

            Vector2 next = body.position + velocity * deltaTime;
            next.x = Mathf.Clamp(next.x, -horizontalBounds, horizontalBounds);
            next.y = Mathf.Clamp(next.y, verticalBoundsBottom, verticalBoundsTop);
            body.MovePosition(next);
        }

        /// <summary>Reports whether the player is currently holding a fire input.</summary>
        public bool IsFiring => InputController.Instance != null && InputController.Instance.IsFiring;

        /// <summary>Autofire: the rebel fleet never stops shooting on mobile.</summary>
        private bool AutoFireEnabled
        {
            get
            {
                GameSettings settings = SaveSystem.Instance?.Profile?.settings;
                return settings == null || settings.autofire;
            }
        }

        private void Update()
        {
            if (!alive) return;

            EquippedWeapon?.TickCooldown(Time.deltaTime);
            RegenerateShields(Time.deltaTime);

            if (invulnerabilityTimer > 0f) invulnerabilityTimer -= Time.deltaTime;

            if (IsFiring || AutoFireEnabled) FireEquippedWeapon();
        }

        private void FireEquippedWeapon()
        {
            if (EquippedWeapon == null || !EquippedWeapon.CanFire) return;

            Vector2 origin = body.position + muzzleOffset;
            EquippedWeapon.Fire(origin, Vector2.up, instanceId);

            // Muzzle flash + laser report, thinned so rapid fire never overwhelms.
            VFXManager.Instance?.SpawnMuzzleFlash(origin, Vector2.up);
            AudioManager.Instance?.PlaySfxJittered("sfx_laser", 0.55f, 0.08f);
        }

        private void RegenerateShields(float deltaTime)
        {
            if (currentShield >= maxShield) return;

            shieldRegenTimer -= deltaTime;
            if (shieldRegenTimer > 0f) return;

            currentShield = Mathf.Min(maxShield, currentShield + shieldRegenPerSecond * deltaTime);
        }

        /// <summary>Shields.Add: used by the Supercharged Dynamo core mod on hit.</summary>
        public void RestoreShield(float amount)
        {
            if (amount <= 0f || !alive) return;
            currentShield = Mathf.Min(maxShield, currentShield + amount);
        }

        /// <summary>Applies damage to shields first, then hull. Respects i-frames.</summary>
        public void TakeDamage(float amount)
        {
            if (!alive || amount <= 0f || !IsVulnerable) return;

            invulnerabilityTimer = 0.35f;
            shieldRegenTimer = shieldRegenDelay;

            float remaining = amount;
            if (currentShield > 0f)
            {
                float absorbed = Mathf.Min(currentShield, remaining);
                currentShield -= absorbed;
                remaining -= absorbed;
            }

            if (remaining > 0f)
            {
                currentHull -= remaining;
                TriggerDamageHaptics();
                VFXManager.Instance?.AddTrauma(0.35f);
                AudioManager.Instance?.PlaySfxJittered("sfx_player_hit", 0.9f, 0.1f);
                if (currentHull <= 0f) Die();
            }
        }

        private void TriggerDamageHaptics()
        {
            GameSettings settings = SaveSystem.Instance?.Profile?.settings;
            if (settings == null || !settings.hapticsEnabled || !settings.damageHaptics) return;
            Handheld.Vibrate();
        }

        private void Die()
        {
            alive = false;
            currentHull = 0f;
            currentShield = 0f;

            VFXManager.Instance?.SpawnExplosion(transform.position, ExplosionScale.Large);
            AudioManager.Instance?.PlaySfx("sfx_player_death", 1f);

            EnemyAIManager.Instance?.Deactivate();
            ObjectPooler.Instance?.FlushAll();
            GameManager.Instance?.RegisterDefeat();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            EnemyProjectile enemyProjectile = collision.collider.GetComponent<EnemyProjectile>();
            if (enemyProjectile != null) return; // handled by the projectile

            Chicken chicken = collision.collider.GetComponent<Chicken>();
            if (chicken != null)
            {
                TakeDamage(chicken.ContactDamage);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
