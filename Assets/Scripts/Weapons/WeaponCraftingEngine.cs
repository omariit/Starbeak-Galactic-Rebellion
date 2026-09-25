using System.Collections.Generic;
using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Module C - the crafting bench. Resolves mod ids into mod instances, assembles a
    /// <see cref="WeaponInstance"/> from an <see cref="EquippedLoadout"/>, and gates
    /// acquisition behind Scrap Iron / Yolk Fusion Core costs.
    /// </summary>
    public class WeaponCraftingEngine : MonoBehaviour
    {
        public static WeaponCraftingEngine Instance { get; private set; }

        private readonly Dictionary<string, WeaponMod> modRegistry = new Dictionary<string, WeaponMod>();

        /// <summary>Weapon archetypes the player can switch between in the Hub.</summary>
        private readonly Dictionary<string, WeaponInstance> blueprintRegistry = new Dictionary<string, WeaponInstance>();

        public IReadOnlyDictionary<string, WeaponMod> Mods => modRegistry;

        private void Awake()
        {
            CrashLog.Info("WeaponCraftingEngine.Awake:enter");
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            RegisterDefaultMods();
            RegisterDefaultBlueprints();
            CrashLog.Info("WeaponCraftingEngine.Awake:leave");
        }

        private void RegisterDefaultMods()
        {
            RegisterMod(new MuzzleMod());
            RegisterMod(new MagazineMod());
            RegisterMod(new CoreMod());
        }

        private void RegisterDefaultBlueprints()
        {
            RegisterBlueprint("blueprint.scatter_peck", "Scatter Peck",
                damage: 12f, fireRate: 0.18f, spread: 34f, projectiles: 5, piercing: false);
            RegisterBlueprint("blueprint.rail_egg", "Rail Egg",
                damage: 46f, fireRate: 0.65f, spread: 0f, projectiles: 1, piercing: true);
            RegisterBlueprint("blueprint.standard", "Standard Laser",
                damage: 15f, fireRate: 0.2f, spread: 0f, projectiles: 1, piercing: false);
        }

        private void RegisterMod(WeaponMod mod)
        {
            modRegistry[mod.id] = mod;
        }

        private void RegisterBlueprint(string id, string name, float damage, float fireRate, float spread, int projectiles, bool piercing)
        {
            WeaponInstance weapon = new WeaponInstance
            {
                baseDamage = damage,
                baseFireRate = fireRate,
                baseSpreadAngle = spread,
                projectilesPerShot = projectiles,
                isPiercingBeam = piercing
            };
            blueprintRegistry[id] = weapon;
        }

        public WeaponMod GetMod(string modId)
        {
            if (string.IsNullOrEmpty(modId)) return null;
            return modRegistry.TryGetValue(modId, out WeaponMod mod) ? mod : null;
        }

        public WeaponInstance GetBlueprint(string blueprintId)
        {
            if (string.IsNullOrEmpty(blueprintId)) return null;
            return blueprintRegistry.TryGetValue(blueprintId, out WeaponInstance weapon) ? weapon : null;
        }

        /// <summary>Assembles the player's active weapon from a persisted loadout.</summary>
        public WeaponInstance BuildFromLoadout(EquippedLoadout loadout)
        {
            if (loadout == null) return new WeaponInstance();

            WeaponInstance weapon = new WeaponInstance
            {
                muzzleSlot = GetMod(loadout.muzzleModId) as MuzzleMod,
                magazineSlot = GetMod(loadout.magazineModId) as MagazineMod,
                coreSlot = GetMod(loadout.coreModId) as CoreMod
            };
            weapon.RecomputeStats();
            return weapon;
        }

        /// <summary>Purchases a mod if the player can afford it. Returns success.</summary>
        public bool TryPurchaseMod(string modId)
        {
            WeaponMod mod = GetMod(modId);
            if (mod == null || mod.IsUnlocked) return false;

            if (!GameManager.Instance.TrySpendResource(ResourceType.ScrapIron, mod.scrapCost)) return false;
            if (!GameManager.Instance.TrySpendResource(ResourceType.YolkCores, mod.yolkCoreCost)) return false;

            SaveSystem.Instance?.RecordModUnlocked(modId);
            return true;
        }

        /// <summary>Equips a mod into its matching slot and recomputes the live weapon.</summary>
        public bool EquipMod(string modId, PlayerShip player)
        {
            WeaponMod mod = GetMod(modId);
            if (mod == null || !mod.IsUnlocked || player == null) return false;

            switch (mod.slot)
            {
                case ModSlot.Muzzle:   player.EquippedWeapon.muzzleSlot = (MuzzleMod)mod; break;
                case ModSlot.Magazine: player.EquippedWeapon.magazineSlot = (MagazineMod)mod; break;
                case ModSlot.Core:     player.EquippedWeapon.coreSlot = (CoreMod)mod; break;
                default: return false;
            }
            player.EquippedWeapon.RecomputeStats();
            PersistLoadout(player);
            return true;
        }

        public void PersistLoadout(PlayerShip player)
        {
            if (player == null) return;
            ProfileData profile = SaveSystem.Instance?.Profile;
            if (profile == null) return;

            profile.loadout.muzzleModId = player.EquippedWeapon.GetSlotId(ModSlot.Muzzle);
            profile.loadout.magazineModId = player.EquippedWeapon.GetSlotId(ModSlot.Magazine);
            profile.loadout.coreModId = player.EquippedWeapon.GetSlotId(ModSlot.Core);
            SaveSystem.Instance.MarkDirty();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
