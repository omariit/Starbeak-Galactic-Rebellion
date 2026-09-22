using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Static hazard zone left behind by Boiled Napalm Eggs. Spans a fixed duration,
    /// damages the player hull on contact, then returns itself to the pool.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class HazardArea : MonoBehaviour
    {
        [SerializeField] private float damagePerSecond = 8f;
        [SerializeField] private float radius = 120f;
        [SerializeField] private float warningFadeIn = 0.25f;

        public float RemainingDuration { get; private set; }
        public bool IsActive => RemainingDuration > 0f;

        private Collider2D hazardCollider;

        private void Awake()
        {
            hazardCollider = GetComponent<Collider2D>();
            hazardCollider.isTrigger = true;
            // hazard.png covers 512x256 world units; fit the visual to the damage radius.
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            float spriteWidth = renderer != null && renderer.sprite != null
                ? renderer.sprite.bounds.size.x : 512f;
            transform.localScale = Vector3.one * (radius * 2f / spriteWidth);
        }

        /// <summary>Activates the hazard for the supplied duration (seconds).</summary>
        public void Activate(float duration)
        {
            RemainingDuration = duration;
            gameObject.SetActive(true);
            AudioManager.Instance?.PlaySfxJittered("sfx_hazard", 0.7f, 0.1f);
        }

        private void Update()
        {
            if (RemainingDuration <= 0f) return;

            RemainingDuration -= Time.deltaTime;
            if (RemainingDuration <= 0f)
            {
                ObjectPooler.Instance?.ReturnHazard(this);
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (RemainingDuration <= 0f) return;

            PlayerShip ship = other.GetComponent<PlayerShip>();
            if (ship != null && ship.IsVulnerable)
            {
                ship.TakeDamage(damagePerSecond * Time.deltaTime);
            }
        }
    }
}
