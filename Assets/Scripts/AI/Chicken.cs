using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Module B entity. A single chicken combatant driven by <see cref="EnemyAIManager"/>.
    /// Implements the Elite Weapon Matrix (napalm eggs, magnetic yolk, flak shrapnel)
    /// and the front-row ArmoredColumn health bonus.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class Chicken : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private ChickenVariant variant = ChickenVariant.Standard;
        [SerializeField] private EliteWeapon eliteWeapon = EliteWeapon.None;

        [Header("Stats")]
        [SerializeField] private float maxHealth = 30f;
        [SerializeField] private float contactDamage = 10f;
        [SerializeField] private float baseMoveSpeed = 140f;
        [SerializeField] private int scrapDrop = 2;
        [SerializeField] private bool isBoss;

        [Header("Elite Tuning")]
        [SerializeField] private float napalmEggSpeed = 260f;
        [SerializeField] private float magneticChargeSpeed = 210f;
        [SerializeField] private float shrapnelSpeed = 320f;
        [SerializeField] private float eliteFireInterval = 2.4f;

        [Header("Boss art (wired by the project builder)")]
        public Sprite bossSprite;

        public int NodeId { get; set; } = -1;
        public ChickenVariant Variant => variant;
        public EliteWeapon EliteWeapon => eliteWeapon;
        public bool IsBoss => isBoss;
        public bool IsFrontRow { get; set; }
        public float ContactDamage => contactDamage;

        /// <summary>Commanded flock offset, set by the Flock Mind Matrix.</summary>
        public Vector2 FlockOffset { get; set; }

        /// <summary>Normalized formation slot this chicken occupies within the fleet.</summary>
        public Vector2 FormationAnchor { get; set; }

        public void SetEliteWeapon(EliteWeapon weapon)
        {
            eliteWeapon = weapon;
        }

        public void SetFormationAnchor(Vector2 anchor)
        {
            FormationAnchor = anchor;
        }

        private Rigidbody2D body;
        private SpriteRenderer spriteRenderer;
        private Sprite standardSprite;
        private Vector3 normalScale = Vector3.one * 0.75f;
        private float health;
        private float freezeTimer;
        private float eliteFireTimer;
        private float armorHealthBonus;
        private float lastContactTime;
        private bool released;

        public bool IsFrozen => freezeTimer > 0f;
        public float HealthPct => maxHealth <= 0f ? 0f : health / maxHealth;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.simulated = true;
            // Dynamic (gravity-free) so kinematic player projectiles register collisions:
            // Unity suppresses collision events between two kinematic bodies.
            body.bodyType = RigidbodyType2D.Dynamic;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                standardSprite = spriteRenderer.sprite;
                spriteRenderer.color = Color.white;
            }
            normalScale = transform.localScale;
        }

        /// <summary>Called by the pooler every time this chicken is spawned.</summary>
        public void ResetState()
        {
            ClearBossConfig();

            health = maxHealth;
            freezeTimer = 0f;
            eliteFireTimer = Random.Range(0f, eliteFireInterval);
            armorHealthBonus = 0f;
            IsFrontRow = false;
            FlockOffset = Vector2.zero;
            released = false;
            transform.localScale = normalScale;
            if (spriteRenderer != null) spriteRenderer.color = Color.white;
            gameObject.SetActive(true);
        }

        public void ApplyArmorColumnBonus(float bonus)
        {
            // Front row of a clustered ArmoredColumn scales health by +50%.
            if (bonus <= armorHealthBonus) return;
            float increase = (maxHealth + bonus) - (maxHealth + armorHealthBonus);
            health += increase;
            armorHealthBonus = bonus;
            IsFrontRow = true;
        }

        public void ApplyFreeze(float duration)
        {
            freezeTimer = Mathf.Max(freezeTimer, duration);
        }

        public void SetMaxHealth(float value, bool healToFull)
        {
            maxHealth = Mathf.Max(1f, value);
            if (healToFull) health = maxHealth;
        }

        /// <summary>Applies damage; returns true if this hit was lethal.</summary>
        public bool TakeDamage(float amount)
        {
            if (health <= 0f || amount <= 0f) return false;

            health -= amount;
            if (health > 0f)
            {
                // White pop flash for readability against the busy starfield.
                VFXManager.Instance?.AddHitFlash(gameObject, isBoss ? new Color(1f, 0.75f, 0.35f) : Color.white);
                return false;
            }

            Die();
            return true;
        }

        private void Die()
        {
            if (released) return;
            released = true;

            EnemyAIManager aiManager = EnemyAIManager.Instance;

            // Rooster Flak Shrapnel: on death, instantiate 8 radial feather projectiles at 45 degrees.
            if (eliteWeapon == EliteWeapon.FlakShrapnel || isBoss)
            {
                SpawnFlakShrapnel();
            }

            // Punchy death feedback: explosion scaled to the enemy tier.
            VFXManager vfx = VFXManager.Instance;
            if (vfx != null)
            {
                Vector3 popPoint = transform.position;
                if (isBoss)
                {
                    vfx.SpawnExplosion(popPoint, ExplosionScale.Boss);
                    vfx.SpawnExplosion(popPoint + Vector3.left * 90f, ExplosionScale.Large);
                    vfx.SpawnExplosion(popPoint + Vector3.right * 90f, ExplosionScale.Large);
                    AudioManager.Instance?.PlaySfx("sfx_boss_roar", 0.9f);
                }
                else if (variant == ChickenVariant.Armored || variant == ChickenVariant.Elite)
                {
                    vfx.SpawnExplosion(popPoint, ExplosionScale.Medium);
                }
                else
                {
                    vfx.SpawnExplosion(popPoint, ExplosionScale.Small);
                }
            }

            GameManager.Instance?.AddResource(ResourceType.ScrapIron, scrapDrop);
            SaveSystem.Instance?.RecordEnemyKill(isBoss);
            aiManager?.NotifyChickenKilled(this);

            EnemyAIManager.Instance?.Unregister(this);
            ObjectPooler.Instance?.ReturnChicken(this);
        }

        /// <summary>
        /// Transforms this pooled chicken into the sector boss: bigger, 12x health,
        /// rotating elite weapons, aggressive fire cadence. Called right after spawn.
        /// (Must be invoked AFTER ResetState / pool spawn, never before.)
        /// </summary>
        public void ConfigureAsBoss(int tier)
        {
            isBoss = true;
            variant = ChickenVariant.BossRooster;
            contactDamage = 26f;
            scrapDrop = 25;
            eliteFireInterval = 1.5f;

            float tierScale = 1f + Mathf.Max(0, tier - 1) * 0.25f;
            SetMaxHealth(30f * GameConstants.BossHealthModifier * tierScale, true);

            EliteWeapon[] weapons =
            {
                EliteWeapon.BoiledNapalmEgg,
                EliteWeapon.MagneticYolkCharge,
                EliteWeapon.FlakShrapnel
            };
            eliteWeapon = weapons[Random.Range(0, weapons.Length)];

            if (bossSprite != null && spriteRenderer != null) spriteRenderer.sprite = bossSprite;
            transform.localScale = normalScale * 2.2f;
        }

        /// <summary>Restores standard (non-boss) identity on pooled reuse.</summary>
        private void ClearBossConfig()
        {
            if (!isBoss) return;

            isBoss = false;
            variant = ChickenVariant.Standard;
            contactDamage = 10f;
            scrapDrop = 2;
            eliteFireInterval = 2.4f;
            maxHealth = 30f;
            eliteWeapon = EliteWeapon.None;

            if (spriteRenderer != null && standardSprite != null) spriteRenderer.sprite = standardSprite;
        }

        private void SpawnFlakShrapnel()
        {
            ObjectPooler pooler = ObjectPooler.Instance;
            if (pooler == null) return;

            Vector3 position = transform.position;
            for (int i = 0; i < GameConstants.FlakShrapnelCount; i++)
            {
                float angle = i * GameConstants.FlakShrapnelSpreadDeg;
                Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
                EnemyProjectile shrapnel = pooler.SpawnShrapnel(position, rotation);
                shrapnel.Launch(position, rotation * Vector2.up, shrapnelSpeed, contactDamage * 0.4f);
            }
        }

        /// <summary>Called by the AI manager each fixed step.</summary>
        public void StepMovement(Vector2 formationTarget, float deltaTime)
        {
            float speed = baseMoveSpeed;
            if (IsFrozen) speed *= GameConstants.FreezeSlowMultiplier;

            Vector2 target = formationTarget + FlockOffset;
            Vector2 newPosition = Vector2.MoveTowards(
                body.position, target, speed * deltaTime);

            body.MovePosition(newPosition);
        }

        /// <summary>Elite weapon cooldown tick, driven by the AI manager.</summary>
        public void StepEliteWeapons(float deltaTime)
        {
            if (eliteWeapon == EliteWeapon.None || IsFrozen) return;

            eliteFireTimer -= deltaTime;
            if (eliteFireTimer > 0f) return;

            eliteFireTimer = eliteFireInterval;
            FireEliteWeapon();
        }

        private void FireEliteWeapon()
        {
            switch (eliteWeapon)
            {
                case EliteWeapon.BoiledNapalmEgg:  FireNapalmEgg(); break;
                case EliteWeapon.MagneticYolkCharge: FireMagneticYolk(); break;
                case EliteWeapon.FlakShrapnel:      FireFlakBurst(); break;
            }
        }

        /// <summary>
        /// Boiled Napalm Eggs: drops a projectile that instantiates a static HazardArea
        /// spanning 3 seconds upon contact with the bottom screen edge or the player hull.
        /// </summary>
        private void FireNapalmEgg()
        {
            ObjectPooler pooler = ObjectPooler.Instance;
            if (pooler == null) return;

            EnemyProjectile egg = pooler.SpawnEnemyProjectile(transform.position, Quaternion.identity);
            egg.ConfigureHazardOnImpact(GameConstants.NapalmHazardDuration);
            egg.Launch(transform.position, Vector2.down, napalmEggSpeed, contactDamage);
        }

        /// <summary>
        /// Magnetic Yolk Charges: applies an attractive force vector to the player ship
        /// calculated as F = G * (m1 * m2) / r^2.
        /// </summary>
        private void FireMagneticYolk()
        {
            ObjectPooler pooler = ObjectPooler.Instance;
            if (pooler == null) return;

            EnemyProjectile charge = pooler.SpawnEnemyProjectile(transform.position, Quaternion.identity);
            charge.ConfigureMagnetic(GameConstants.MagneticGravitationalConstant);
            charge.Launch(transform.position, Vector2.down, magneticChargeSpeed, contactDamage);
        }

        private void FireFlakBurst()
        {
            // A short forward fan, distinct from the radial on-death shrapnel.
            ObjectPooler pooler = ObjectPooler.Instance;
            if (pooler == null) return;

            Vector3 position = transform.position;
            const int burstCount = 3;
            for (int i = 0; i < burstCount; i++)
            {
                float angle = -30f + (30f * i);
                Quaternion rotation = Quaternion.Euler(0f, 0f, 180f + angle);
                EnemyProjectile feather = pooler.SpawnEnemyProjectile(position, rotation);
                feather.Launch(position, rotation * Vector2.up, shrapnelSpeed, contactDamage * 0.5f);
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (released) return;

            PlayerShip ship = collision.collider.GetComponent<PlayerShip>();
            if (ship == null || !ship.IsVulnerable) return;

            // The boss is a durable threat: it bleeds contact damage on a cadence
            // and knocks itself away instead of kamikaze-dying like standard chicken.
            if (isBoss)
            {
                float now = Time.time;
                if (now - lastContactTime < 0.7f) return;
                lastContactTime = now;

                ship.TakeDamage(contactDamage);
                Vector2 push = (Vector2)(transform.position - ship.transform.position);
                if (push.sqrMagnitude > 0.01f) body.AddForce(push.normalized * 600f, ForceMode2D.Impulse);
                return;
            }

            ship.TakeDamage(contactDamage);
            Die();
        }
    }

    public enum ChickenVariant
    {
        Standard,
        Armored,
        Elite,
        BossRooster
    }

    public enum EliteWeapon
    {
        None,
        BoiledNapalmEgg,
        MagneticYolkCharge,
        FlakShrapnel
    }
}
