using System.Collections.Generic;
using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Run-local roguelite weapon upgrades. After clearing a combat node the player picks
    /// one of three random upgrades; this is what makes rocket counts and types change
    /// mid-run (the user's "عدد الصواريخ وأنواعها تتغير" request).
    ///
    /// Upgrades are stored as ids so they survive the weapon rebuild that happens on every
    /// sector respawn (PlayerShip.Respawn rebuilds from the persisted loadout, then this
    /// engine re-applies the run's accumulated upgrades on top).
    /// </summary>
    public static class WeaponUpgrades
    {
        public struct Upgrade
        {
            public string id;
            public string title;
            public string description;
            public System.Action<WeaponInstance> apply;
        }

        private static readonly Upgrade[] pool =
        {
            new Upgrade
            {
                id = "multishot", title = "TWIN BARREL",
                description = "+1 projectile per volley",
                apply = w => w.projectilesPerShot = Mathf.Min(6, w.projectilesPerShot + 1)
            },
            new Upgrade
            {
                id = "damage", title = "OVERCHARGE",
                description = "+30% weapon damage",
                apply = w => w.baseDamage *= 1.3f
            },
            new Upgrade
            {
                id = "firerate", title = "RAPID FIRE",
                description = "-20% cooldown between shots",
                apply = w => w.baseFireRate = Mathf.Max(0.05f, w.baseFireRate * 0.8f)
            },
            new Upgrade
            {
                id = "spread", title = "SCATTER MOD",
                description = "+18 deg spread (pairs with multishot)",
                apply = w => w.baseSpreadAngle = Mathf.Min(80f, w.baseSpreadAngle + 18f)
            },
            new Upgrade
            {
                id = "piercing", title = "PIERCING ROUNDS",
                description = "shots pass through enemies",
                apply = w => w.isPiercingBeam = true
            },
            new Upgrade
            {
                id = "velocity", title = "MAG-ACCELERATOR",
                description = "+25% projectile speed",
                apply = w => w.projectileSpeed *= 1.25f
            }
        };

        private static readonly List<string> applied = new List<string>(8);
        private static readonly List<Upgrade> rolled = new List<Upgrade>(3);

        /// <summary>Upgrade ids accumulated during the current run.</summary>
        public static IReadOnlyList<string> Applied => applied;

        /// <summary>True when at least one upgrade has been collected this run.</summary>
        public static bool HasUpgrades => applied.Count > 0;

        /// <summary>Wipes the run's upgrades (new galaxy / new run).</summary>
        public static void ResetForNewRun()
        {
            applied.Clear();
            rolled.Clear();
        }

        /// <summary>Draws `count` distinct random upgrades from the pool.</summary>
        public static List<Upgrade> RollChoices(int count)
        {
            rolled.Clear();
            count = Mathf.Clamp(count, 1, pool.Length);

            // Fisher-Yates over indices so the same upgrade is never offered twice in one draw.
            int[] order = new int[pool.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = order[i];
                order[i] = order[j];
                order[j] = tmp;
            }
            for (int i = 0; i < count; i++) rolled.Add(pool[order[i]]);
            return rolled;
        }

        /// <summary>Applies one upgrade to the live weapon and records it for this run.</summary>
        public static void Apply(WeaponInstance weapon, Upgrade upgrade)
        {
            if (weapon == null) return;
            upgrade.apply(weapon);
            weapon.RecomputeStats();
            if (!applied.Contains(upgrade.id)) applied.Add(upgrade.id);
        }

        /// <summary>Re-applies the whole run onto a freshly rebuilt weapon (called after Respawn).</summary>
        public static void Reapply(WeaponInstance weapon)
        {
            if (weapon == null || applied.Count == 0) return;
            for (int i = 0; i < applied.Count; i++)
            {
                for (int p = 0; p < pool.Length; p++)
                {
                    if (pool[p].id == applied[i])
                    {
                        pool[p].apply(weapon);
                        break;
                    }
                }
            }
            weapon.RecomputeStats();
        }

        public static Upgrade Find(string id)
        {
            for (int i = 0; i < pool.Length; i++)
            {
                if (pool[i].id == id) return pool[i];
            }
            return default;
        }
    }
}
