using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Enemy projectile: standard egg, magnetic yolk charge, napalm egg, or feather shrapnel.
    /// Optional magnetic attraction follows F = G * (m1 * m2) / r^2 against the player hull.
    /// Optional hazard-on-impact spawns a static HazardArea on bottom-edge or hull contact.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
    public class EnemyProjectile : MonoBehaviour
    {
        [SerializeField] private float lifetime = 6f;
        [SerializeField] private bool rotateToVelocity = true;

        private Rigidbody2D body;
        private Vector2 velocity;
        private float damage;
        private float speed;
        private float lifeTimer;
        private float hazardDuration;
        private float gravitationalConstant;
        private bool magnetic;
        private bool released;

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
            speed = 0f;
            lifeTimer = lifetime;
            hazardDuration = 0f;
            gravitationalConstant = 0f;
            magnetic = false;
            released = false;
            gameObject.SetActive(true);
        }

        public void Launch(Vector2 origin, Vector2 direction, float launchSpeed, float projectileDamage)
        {
            body.position = origin;
            velocity = direction.normalized * launchSpeed;
            speed = launchSpeed;
            damage = projectileDamage;
            lifeTimer = lifetime;

            if (rotateToVelocity && velocity.sqrMagnitude > 0.01f)
            {
                float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle - 90f);
            }
        }

        /// <summary>Configures this projectile to leave a napalm HazardArea on impact.</summary>
        public void ConfigureHazardOnImpact(float duration)
        {
            hazardDuration = duration;
        }

        /// <summary>Configures this projectile to exert gravitational attraction on the player.</summary>
        public void ConfigureMagnetic(float gravitationalConstant)
        {
            magnetic = true;
            this.gravitationalConstant = gravitationalConstant;
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

            Vector2 nextPosition = body.position + velocity * Time.fixedDeltaTime;

            if (magnetic && PlayerShip.Instance != null)
            {
                nextPosition += ComputeAttraction(nextPosition, Time.fixedDeltaTime);
            }

            body.MovePosition(nextPosition);

            // Off-screen bounds cull (portrait playfield).
            const float cullX = 900f;
            const float cullY = 1300f;
            if (Mathf.Abs(nextPosition.x) > cullX || nextPosition.y < -cullY)
            {
                HandleBottomEdgeContact(nextPosition);
            }
        }

        /// <summary>
        /// Magnetic Yolk Charges: attractive force vector on the player ship,
        /// F = G * (m1 * m2) / r^2, applied as an acceleration on this projectile
        /// toward the hull so the charge curves into the player.
        /// </summary>
        private Vector2 ComputeAttraction(Vector2 currentPosition, float dt)
        {
            Vector2 toPlayer = (Vector2)PlayerShip.Instance.Position - currentPosition;
            float distanceSquared = toPlayer.sqrMagnitude;
            if (distanceSquared < 1f) return Vector2.zero;

            // m1 * m2 folded into the tuning constant; clamp avoids singularities.
            float forceMagnitude = gravitationalConstant / Mathf.Max(distanceSquared, 900f);
            Vector2 direction = toPlayer.normalized;
            return direction * forceMagnitude * dt;
        }

        private void HandleBottomEdgeContact(Vector2 position)
        {
            if (hazardDuration > 0f)
            {
                HazardArea hazard = ObjectPooler.Instance?.SpawnHazard(position);
                if (hazard != null) hazard.Activate(hazardDuration);
            }
            ReturnToPool();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            PlayerShip ship = collision.collider.GetComponent<PlayerShip>();
            if (ship != null && ship.IsVulnerable)
            {
                ship.TakeDamage(damage);

                if (hazardDuration > 0f)
                {
                    HazardArea hazard = ObjectPooler.Instance?.SpawnHazard(transform.position);
                    if (hazard != null) hazard.Activate(hazardDuration);
                }
            }
            ReturnToPool();
        }

        private void ReturnToPool()
        {
            if (released) return;
            released = true;
            ObjectPooler.Instance?.ReturnEnemyProjectile(this);
        }
    }
}
