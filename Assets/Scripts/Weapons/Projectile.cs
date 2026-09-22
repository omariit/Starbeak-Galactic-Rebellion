using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Player projectile. Pooled, optional piercing (rail/beam), optional injected status
    /// effect, and an OnHit hook back into the firing <see cref="WeaponInstance"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float lifetime = 3f;
        [SerializeField] private bool piercing;
        [SerializeField] private int maxPierceTargets = 3;

        private Rigidbody2D body;
        private Vector2 velocity;
        private float damage;
        private float lifeTimer;
        private int ownerInstanceId;
        private int pierceRemaining;
        private StatusEffect statusEffect;
        private WeaponInstance sourceWeapon;
        private bool released;

        public bool IsPiercing => piercing;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        /// <summary>Called by the pooler on every spawn.</summary>
        public void ResetState()
        {
            velocity = Vector2.zero;
            damage = 0f;
            lifeTimer = lifetime;
            pierceRemaining = maxPierceTargets;
            statusEffect = default;
            sourceWeapon = null;
            released = false;
            gameObject.SetActive(true);
        }

        public void Initialize(float projectileDamage, Vector2 launchVelocity, int ownerId)
        {
            damage = projectileDamage;
            velocity = launchVelocity;
            ownerInstanceId = ownerId;
            lifeTimer = lifetime;
            pierceRemaining = piercing ? maxPierceTargets : 1;
        }

        public void AttachStatusEffect(StatusEffect effect)
        {
            statusEffect = effect;
        }

        public void AttachSourceWeapon(WeaponInstance weapon)
        {
            sourceWeapon = weapon;
        }

        public void SetPiercing(bool value, int maxTargets)
        {
            piercing = value;
            maxPierceTargets = Mathf.Max(1, maxTargets);
        }

        private void FixedUpdate()
        {
            if (!gameObject.activeInHierarchy) return;

            lifeTimer -= Time.fixedDeltaTime;
            if (lifeTimer <= 0f)
            {
                ReturnToPool();
                return;
            }
            body.MovePosition(body.position + velocity * Time.fixedDeltaTime);

            // Cull above the top edge.
            if (body.position.y > 1150f) ReturnToPool();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            Chicken chicken = collision.collider.GetComponent<Chicken>();
            if (chicken == null)
            {
                ReturnToPool();
                return;
            }

            float damageDealt = damage;
            bool lethal = chicken.TakeDamage(damageDealt);

            // Impact feedback: sparks fly along the surface normal + hit sound.
            if (!lethal)
            {
                Vector2 sparkNormal = (body.position - (Vector2)collision.collider.transform.position);
                if (sparkNormal.sqrMagnitude < 0.001f) sparkNormal = Vector2.up;
                sparkNormal.Normalize();
                VFXManager.Instance?.SpawnHitSpark(transform.position, sparkNormal, 0.8f);
            }

            if (statusEffect.IsValid && statusEffect.type == StatusEffectType.Freeze)
            {
                // Freeze: reduce enemy movement speed by 40% for 2.0 seconds.
                chicken.ApplyFreeze(statusEffect.duration);
            }

            // Core mod OnHit delegate (Supercharged Dynamo shield leech, etc.).
            sourceWeapon?.HandleOnHit(chicken, damageDealt, transform.position);

            if (!piercing || pierceRemaining <= 1 || lethal)
            {
                ReturnToPool();
                return;
            }

            pierceRemaining--;
        }

        private void ReturnToPool()
        {
            if (released) return;
            released = true;
            ObjectPooler.Instance?.ReturnPlayerProjectile(this);
        }
    }
}
