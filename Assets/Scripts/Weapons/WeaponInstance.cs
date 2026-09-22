using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Module C - a weapon with three slotted addons whose stats stack structurally.
    /// </summary>
    public class WeaponInstance
    {
        public float baseDamage = 15f;
        public float baseFireRate = 0.2f; // cooldown in seconds
        public float baseSpreadAngle = 0f;
        public float projectileSpeed = 760f;
        public int projectilesPerShot = 1;
        public bool isPiercingBeam;

        // Slotted Addons
        public MuzzleMod muzzleSlot;
        public MagazineMod magazineSlot;
        public CoreMod coreSlot;

        private float cooldownTimer;

        /// <summary>Final computed stats, re-evaluated only when a slot changes.</summary>
        public float FinalDamage { get; private set; }
        public float FinalCooldown { get; private set; }
        public float FinalSpreadAngle { get; private set; }

        /// <summary>True when this weapon is a piercing beam/railgun archetype.</summary>
        public bool IsPiercingBeam => isPiercingBeam;

        public WeaponInstance()
        {
            RecomputeStats();
        }

        /// <summary>Rebuilds stacked stats. Call after any mod is equipped/removed.</summary>
        public void RecomputeStats()
        {
            // Muzzle: stacks damageMultiplier and spreadAngleOffset.
            FinalDamage = baseDamage * (muzzleSlot != null ? muzzleSlot.damageMultiplier : 1f);
            FinalSpreadAngle = Mathf.Clamp(
                baseSpreadAngle + (muzzleSlot != null ? muzzleSlot.spreadAngleOffset : 0f), 0f, 90f);

            // Magazine: stacks cooldownMultiplier.
            FinalCooldown = baseFireRate * (magazineSlot != null ? magazineSlot.cooldownMultiplier : 1f);
        }

        /// <summary>True when the weapon is ready to fire this frame.</summary>
        public bool CanFire => cooldownTimer <= 0f;

        public void TickCooldown(float deltaTime)
        {
            if (cooldownTimer > 0f) cooldownTimer -= deltaTime;
        }

        /// <summary>
        /// Fire logic executing bullet instantiation via Object Pool. Returns the number
        /// of projectiles spawned this call (0 when on cooldown).
        /// </summary>
        public int Fire(Vector2 origin, Vector2 aimDirection, int ownerInstanceId)
        {
            if (cooldownTimer > 0f) return 0;
            cooldownTimer = FinalCooldown;

            int count = Mathf.Max(1, projectilesPerShot);
            float spread = FinalSpreadAngle;

            for (int i = 0; i < count; i++)
            {
                float offset = count > 1 ? (spread * (i / (float)(count - 1) - 0.5f)) : 0f;
                Vector2 direction = Rotate(aimDirection, offset);
                Quaternion rotation = Quaternion.LookRotation(Vector3.forward, direction);

                Projectile projectile = ObjectPooler.Instance.SpawnPlayerProjectile(origin, rotation);
                projectile.Initialize(FinalDamage, direction * projectileSpeed, ownerInstanceId);
                projectile.AttachSourceWeapon(this);

                ApplyInjectedStatus(projectile);
            }
            return count;
        }

        private void ApplyInjectedStatus(Projectile projectile)
        {
            if (magazineSlot == null || !magazineSlot.injectedEffect.IsValid) return;
            projectile.AttachStatusEffect(magazineSlot.injectedEffect);
        }

        /// <summary>Invoked by a projectile when it impacts a chicken: runs Core OnHit delegates.</summary>
        public void HandleOnHit(Chicken target, float damageDealt, Vector2 hitPoint)
        {
            if (coreSlot == null) return;
            if (Random.value > coreSlot.onHitProbability) return;

            // Supercharged Dynamo: PlayerShip.Shields.Add(damageDealt * 0.05).
            if (coreSlot.shieldLeechRatio > 0f && PlayerShip.Instance != null)
            {
                PlayerShip.Instance.RestoreShield(damageDealt * coreSlot.shieldLeechRatio);
            }
        }

        private static Vector2 Rotate(Vector2 vector, float degrees)
        {
            if (degrees == 0f) return vector;
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);
        }

        /// <summary>Snapshot of the mod ids in each slot, for save serialization.</summary>
        public string GetSlotId(ModSlot slot)
        {
            switch (slot)
            {
                case ModSlot.Muzzle:   return muzzleSlot != null ? muzzleSlot.id : string.Empty;
                case ModSlot.Magazine: return magazineSlot != null ? magazineSlot.id : string.Empty;
                case ModSlot.Core:     return coreSlot != null ? coreSlot.id : string.Empty;
                default:               return string.Empty;
            }
        }
    }
}
