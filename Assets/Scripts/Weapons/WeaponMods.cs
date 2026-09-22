using System;
using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Module C - Weapon Modular Mod Matrix. Mods are pure-data attachments that stack
    /// structural stats multiplicatively/additively inside <see cref="WeaponInstance.Fire"/>.
    /// </summary>
    [Serializable]
    public abstract class WeaponMod
    {
        public string id;
        public string displayName;
        public string description;
        public int scrapCost;
        public int yolkCoreCost;
        public ModSlot slot;

        public bool IsUnlocked => SaveSystem.Instance != null
            && SaveSystem.Instance.Profile != null
            && SaveSystem.Instance.Profile.unlockedMods.Contains(id);

        public bool CanAfford => GameManager.Instance != null
            && GameManager.Instance.TrySpendResource(ResourceType.ScrapIron, 0);

        protected WeaponMod(string id, string displayName, ModSlot slot)
        {
            this.id = id;
            this.displayName = displayName;
            this.slot = slot;
            description = string.Empty;
        }
    }

    public enum ModSlot
    {
        Muzzle,
        Magazine,
        Core
    }

    // ---------------------------------------------------------------- Muzzle mods
    [Serializable]
    public class MuzzleMod : WeaponMod
    {
        public const string ChokeBoreLensId = "muzzle.choke_bore_lens";

        /// <summary>Multiplies base damage (Choke-Bore Lens = 1.25).</summary>
        public float damageMultiplier = 1f;

        /// <summary>Added to spread angle in degrees (Choke-Bore Lens = -15).</summary>
        public float spreadAngleOffset = 0f;

        public MuzzleMod() : base(ChokeBoreLensId, "Choke-Bore Lens", ModSlot.Muzzle)
        {
            // Crafting Data Table: damageMultiplier 1.25, spreadAngleOffset -15.
            damageMultiplier = GameConstants.MuzzleDamageMultiplier;
            spreadAngleOffset = GameConstants.MuzzleSpreadOffset;
            description = "Focuses the blast cone: +25% damage, -15 deg spread.";
        }
    }

    // ---------------------------------------------------------------- Magazine mods
    [Serializable]
    public class MagazineMod : WeaponMod
    {
        public const string CryoYolkStabilizerId = "magazine.cryo_yolk_stabilizer";

        /// <summary>Multiplies base fire cooldown (Cryo-Yolk Stabilizer = 1.10).</summary>
        public float cooldownMultiplier = 1f;

        /// <summary>Status effect injected into every round (Freeze: -40% move speed, 2.0s).</summary>
        public StatusEffect injectedEffect;

        public MagazineMod() : base(CryoYolkStabilizerId, "Cryo-Yolk Stabilizer", ModSlot.Magazine)
        {
            // Crafting Data Table: cooldownMultiplier 1.10, injects Freeze (-40% speed, 2.0s).
            cooldownMultiplier = GameConstants.MagazineCooldownMultiplier;
            injectedEffect = new StatusEffect(StatusEffectType.Freeze, GameConstants.FreezeDuration, GameConstants.FreezeSlowMultiplier);
            description = "Cryo-stabilized magazine: +10% fire rate, freezes targets.";
        }
    }

    // ---------------------------------------------------------------- Core mods
    [Serializable]
    public class CoreMod : WeaponMod
    {
        public const string SuperchargedDynamoId = "core.supercharged_dynamo";

        /// <summary>Probability per hit that the OnHit delegate fires (Dynamo = 5%).</summary>
        public float onHitProbability = 0f;

        /// <summary>Shield restored as a fraction of damage dealt (Dynamo = 5%).</summary>
        public float shieldLeechRatio = 0f;

        public CoreMod() : base(SuperchargedDynamoId, "Supercharged Dynamo", ModSlot.Core)
        {
            // Crafting Data Table: 5% probability to leech shields for 5% of damage dealt.
            onHitProbability = GameConstants.CoreShieldLeechProbability;
            shieldLeechRatio = GameConstants.CoreShieldLeechRatio;
            description = "Overcharged core: on-hit, 5% chance to restore shields by 5% of damage.";
        }
    }
}
