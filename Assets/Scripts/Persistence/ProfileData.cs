using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Root JSON schema persisted to usr_profile.dat. Versioned for forward migration.
    /// </summary>
    [Serializable]
    public class ProfileData
    {
        public const int CurrentSchemaVersion = 1;

        public int schemaVersion = CurrentSchemaVersion;

        // ---------------------------------------------------------------- Meta
        public string profileId = string.Empty;
        public long createdUnixTime;
        public long lastSavedUnixTime;
        public long totalPlayTimeSeconds;

        // ---------------------------------------------------------------- Resources
        public int scrapIron;
        public int yolkCores;
        public int goldenFeathers;

        // ---------------------------------------------------------------- Progression
        public int galaxiesCleared;
        public int highestTierReached;
        public int totalChickenKills;
        public int bossKills;

        // ---------------------------------------------------------------- Run persistence
        public int currentRunSeed;
        /// <summary>Node ids completed in the active galaxy run.</summary>
        public List<int> completedNodeIds = new List<int>();

        // ---------------------------------------------------------------- Crafting
        /// <summary>Mod ids the player has found/purchased.</summary>
        public List<string> unlockedMods = new List<string>();
        /// <summary>Currently equipped loadout, keyed by slot.</summary>
        public EquippedLoadout loadout = new EquippedLoadout();
        /// <summary>Blueprint ids unlocked by the hub fabricator.</summary>
        public List<string> unlockedBlueprints = new List<string>();

        // ---------------------------------------------------------------- Settings
        public GameSettings settings = new GameSettings();

        // ---------------------------------------------------------------- Validation
        public bool IsValid()
        {
            if (schemaVersion <= 0) return false;
            if (scrapIron < 0 || yolkCores < 0 || goldenFeathers < 0) return false;
            if (galaxiesCleared < 0 || highestTierReached < 0 || totalChickenKills < 0) return false;
            return true;
        }

        public void Sanitize()
        {
            if (schemaVersion > CurrentSchemaVersion) schemaVersion = CurrentSchemaVersion;
            if (completedNodeIds == null) completedNodeIds = new List<int>();
            if (unlockedMods == null) unlockedMods = new List<string>();
            if (unlockedBlueprints == null) unlockedBlueprints = new List<string>();
            if (loadout == null) loadout = new EquippedLoadout();
            if (settings == null) settings = new GameSettings();

            scrapIron = Mathf.Max(0, scrapIron);
            yolkCores = Mathf.Max(0, yolkCores);
            goldenFeathers = Mathf.Max(0, goldenFeathers);
            galaxiesCleared = Mathf.Max(0, galaxiesCleared);
            highestTierReached = Mathf.Clamp(highestTierReached, 0, GameConstants.TierCount - 1);
            totalChickenKills = Mathf.Max(0, totalChickenKills);
            totalPlayTimeSeconds = System.Math.Max(0L, totalPlayTimeSeconds);
        }

        public ProfileData Clone()
        {
            string json = JsonUtility.ToJson(this);
            ProfileData copy = JsonUtility.FromJson<ProfileData>(json);
            copy.Sanitize();
            return copy;
        }

        public static ProfileData CreateDefault()
        {
            ProfileData data = new ProfileData
            {
                profileId = Guid.NewGuid().ToString("N"),
                createdUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                highestTierReached = 0,
                scrapIron = 0,
                yolkCores = 0,
                goldenFeathers = 0
            };
            data.unlockedMods.Add(MuzzleMod.ChokeBoreLensId);
            data.unlockedMods.Add(MagazineMod.CryoYolkStabilizerId);
            data.unlockedMods.Add(CoreMod.SuperchargedDynamoId);
            data.settings.RestoreDefaults();
            return data;
        }
    }

    [Serializable]
    public class EquippedLoadout
    {
        public string muzzleModId = MuzzleMod.ChokeBoreLensId;
        public string magazineModId = MagazineMod.CryoYolkStabilizerId;
        public string coreModId = CoreMod.SuperchargedDynamoId;
    }

    [Serializable]
    public class GameSettings
    {
        public float masterVolume = 1f;
        public float sfxVolume = 1f;
        public float musicVolume = 0.8f;
        /// <summary>Direct absolute-finger tracking (true) or virtual joystick (false).</summary>
        public bool absoluteDragInput = true;
        public float touchSensitivity = 1f;
        public bool hapticsEnabled = true;
        /// <summary>When true, vibrate on hull damage in addition to events.</summary>
        public bool damageHaptics = true;
        /// <summary>Continuous fire without holding the trigger (mobile comfort default).</summary>
        public bool autofire = true;
        /// <summary>Unity quality level index persisted across sessions.</summary>
        public int qualityTier = 3;

        public void RestoreDefaults()
        {
            masterVolume = 1f;
            sfxVolume = 1f;
            musicVolume = 0.8f;
            absoluteDragInput = true;
            touchSensitivity = 1f;
            hapticsEnabled = true;
            damageHaptics = true;
            autofire = true;
            qualityTier = 3;
        }
    }
}
