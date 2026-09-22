using System;
using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Module C - status effect model. Applied on hit by weapon mods (e.g. Cryo-Yolk
    /// Stabilizer injects Freeze) and consumed by <see cref="Chicken"/>.
    /// </summary>
    [Serializable]
    public struct StatusEffect
    {
        public StatusEffectType type;
        public float duration;
        public float magnitude;

        public StatusEffect(StatusEffectType type, float duration, float magnitude)
        {
            this.type = type;
            this.duration = duration;
            this.magnitude = magnitude;
        }

        public bool IsValid => duration > 0f;
    }

    public enum StatusEffectType
    {
        None,
        Freeze,
        Ignite,
        ShieldLeech
    }

    /// <summary>Contract for on-hit modification injected by Core mods.</summary>
    public interface IOnHitHandler
    {
        /// <summary>Called once per projectile impact. damageDealt is pre-resistance damage.</summary>
        void OnHit(Chicken target, float damageDealt, Vector2 hitPoint);
    }
}
