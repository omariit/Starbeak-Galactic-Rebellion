using System;
using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Data persistence engine. Encrypts the profile to JSON and writes it to
    /// Application.persistentDataPath/usr_profile.dat. Dirty-flagged + flushed on
    /// pause/quit so combat never stalls on disk I/O.
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        public static SaveSystem Instance { get; private set; }

        private const float AutoSaveInterval = 30f;

        private ProfileData activeProfile;
        private bool dirty;
        private float autoSaveTimer;
        private bool loadingInProgress;

        public ProfileData Profile => activeProfile;
        public bool HasProfile => activeProfile != null;
        public string FilePath => System.IO.Path.Combine(Application.persistentDataPath, GameConstants.ProfileFileName);

        /// <summary>Raised after a profile has been loaded (profile may be null on first launch).</summary>
        public static event Action<ProfileData> ProfileLoaded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>Loads the profile if not already in memory. Safe to call repeatedly.</summary>
        public void Load()
        {
            if (loadingInProgress || activeProfile != null) return;

            loadingInProgress = true;
            try
            {
                byte[] blob = AesEncryptor.ReadFile(FilePath);
                if (blob != null && blob.Length > 0)
                {
                    string json = AesEncryptor.Decrypt(blob);
                    if (!string.IsNullOrEmpty(json))
                    {
                        ProfileData loaded = JsonUtility.FromJson<ProfileData>(json);
                        if (loaded != null && loaded.IsValid())
                        {
                            loaded.Sanitize();
                            activeProfile = loaded;
                            loadingInProgress = false;
                            ProfileLoaded?.Invoke(activeProfile);
                            return;
                        }
                    }
                }
                // Corrupt, missing, or tampered: fall back to a fresh profile.
                activeProfile = ProfileData.CreateDefault();
                MarkDirty();
            }
            finally
            {
                loadingInProgress = false;
            }

            ProfileLoaded?.Invoke(activeProfile);
        }

        /// <summary>Immediate encrypted write. Use sparingly; prefer MarkDirty().</summary>
        public bool Flush()
        {
            if (!dirty || activeProfile == null) return false;

            activeProfile.lastSavedUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            activeProfile.totalPlayTimeSeconds += (long)Time.unscaledDeltaTime;

            string json = JsonUtility.ToJson(activeProfile);
            byte[] payload = AesEncryptor.Encrypt(json);

            bool ok = AesEncryptor.WriteFile(FilePath, payload);
            if (ok) dirty = false;
            return ok;
        }

        public void MarkDirty()
        {
            dirty = true;
        }

        /// <summary>Commits live game state into the profile before saving.</summary>
        public void CommitRuntimeState()
        {
            if (activeProfile == null || GameManager.Instance == null) return;

            GameManager.Instance.SnapshotResources(
                ref activeProfile.scrapIron,
                ref activeProfile.yolkCores,
                ref activeProfile.goldenFeathers);

            activeProfile.currentRunSeed = GameManager.Instance.CurrentRunSeed;
            activeProfile.highestTierReached = Mathf.Max(
                activeProfile.highestTierReached,
                SectorMapGenerator.Instance != null ? SectorMapGenerator.Instance.HighestReachedTier : 0);
        }

        public void RecordNodeCompleted(int nodeId)
        {
            if (activeProfile == null) return;
            if (!activeProfile.completedNodeIds.Contains(nodeId))
            {
                activeProfile.completedNodeIds.Add(nodeId);
            }
            MarkDirty();
        }

        public void RecordEnemyKill(bool isBoss)
        {
            if (activeProfile == null) return;
            activeProfile.totalChickenKills++;
            if (isBoss)
            {
                activeProfile.bossKills++;
                activeProfile.galaxiesCleared++;
            }
            MarkDirty();
        }

        public void RecordModUnlocked(string modId)
        {
            if (activeProfile == null) return;
            if (!string.IsNullOrEmpty(modId) && !activeProfile.unlockedMods.Contains(modId))
            {
                activeProfile.unlockedMods.Add(modId);
                MarkDirty();
            }
        }

        public void RecordBlueprintUnlocked(string blueprintId)
        {
            if (activeProfile == null) return;
            if (!string.IsNullOrEmpty(blueprintId) && !activeProfile.unlockedBlueprints.Contains(blueprintId))
            {
                activeProfile.unlockedBlueprints.Add(blueprintId);
                MarkDirty();
            }
        }

        public void ClearRunProgress()
        {
            if (activeProfile == null) return;
            activeProfile.completedNodeIds.Clear();
            MarkDirty();
        }

        /// <summary>Danger zone: wipes the profile back to defaults (used by settings panel).</summary>
        public void WipeProfile()
        {
            activeProfile = ProfileData.CreateDefault();
            dirty = true;
            Flush();
            ProfileLoaded?.Invoke(activeProfile);
        }

        private void Update()
        {
            if (!dirty || activeProfile == null) return;

            autoSaveTimer += Time.unscaledDeltaTime;
            if (autoSaveTimer >= AutoSaveInterval)
            {
                autoSaveTimer = 0f;
                CommitRuntimeState();
                Flush();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
