using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Central game state machine + global resource inventory. Persistent singleton across scenes.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Global Resource Inventory")]
        [SerializeField] private int scrapIron;
        [SerializeField] private int yolkCores;
        [SerializeField] private int goldenFeathers;

        /// <summary>Run-local seed, regenerated for each new galaxy.</summary>
        public int CurrentRunSeed { get; private set; }

        public GameState CurrentState { get; private set; }
        public bool IsProductionBuild { get; private set; }

        /// <summary>Raised whenever the active state changes.</summary>
        public static event Action<GameState> StateChanged;

        private static readonly int[] EmptyIntArray = new int[0];

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            IsProductionBuild = Debug.isDebugBuild == false;
            ApplyProductionOptimizations();
            CurrentRunSeed = Environment.TickCount;
        }

        private void Start()
        {
            // Boot into the main menu on first launch.
            ChangeState(GameState.MainMenu);
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && CurrentState == GameState.Gameplay)
            {
                SaveSystem.Instance?.Flush();
            }
        }

        private void OnApplicationQuit()
        {
            SaveSystem.Instance?.Flush();
        }

        /// <summary>
        /// Force low-overhead GC settings and a precise 60 FPS software lock.
        /// Disable runtime debug logs in production APK builds to maximize CPU efficiency.
        /// </summary>
        private void ApplyProductionOptimizations()
        {
            Application.targetFrameRate = GameConstants.TargetFrameRate;
            QualitySettings.vSyncCount = 0;
            Time.fixedDeltaTime = GameConstants.FixedDeltaTime;

            if (IsProductionBuild)
            {
                Debug.unityLogger.logEnabled = false;
                Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
                Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.None);
            }
        }

        public void ChangeState(GameState newState)
        {
            if (CurrentState == newState) return;

            CurrentState = newState;
            StateChanged?.Invoke(newState);

            switch (newState)
            {
                case GameState.MainMenu:  InitializeMainMenu();  break;
                case GameState.SectorMap: GenerateOrLoadMap();   break;
                case GameState.Gameplay:  StartCombatLoop();     break;
                case GameState.HubBase:   OpenHubInterface();    break;
            }

            // Theme music follows the state machine.
            AudioManager audio = AudioManager.Instance;
            if (audio != null)
            {
                switch (newState)
                {
                    case GameState.MainMenu:  audio.PlayMusic("music_menu");   break;
                    case GameState.Gameplay:  audio.PlayMusic("music_combat"); break;
                    default:                  audio.PlayMusic("music_menu");   break;
                }
            }

            // Fresh camera shake state on every transition.
            VFXManager.Instance?.ClearTrauma();
        }

        /// <summary>Rolls a fresh seed and begins a brand new run.</summary>
        public void StartNewRun()
        {
            CurrentRunSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            SectorMapGenerator.Instance?.DiscardMap();
            ChangeState(GameState.SectorMap);
        }

        // ---------------------------------------------------------------- Resource API
        public int GetResource(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.ScrapIron:     return scrapIron;
                case ResourceType.YolkCores:     return yolkCores;
                case ResourceType.GoldenFeathers: return goldenFeathers;
                default: return 0;
            }
        }

        public void AddResource(ResourceType type, int amount)
        {
            switch (type)
            {
                case ResourceType.ScrapIron:      scrapIron += amount; break;
                case ResourceType.YolkCores:     yolkCores += amount; break;
                case ResourceType.GoldenFeathers: goldenFeathers += amount; break;
            }
            if (amount > 0) AudioManager.Instance?.PlaySfxJittered("sfx_pickup", 0.7f, 0.05f);
            MarkProfileDirty();
        }

        public bool TrySpendResource(ResourceType type, int amount)
        {
            if (amount <= 0) return true;
            if (GetResource(type) < amount) return false;

            AddResource(type, -amount);
            return true;
        }

        public void SnapshotResources(ref int scrap, ref int yolk, ref int feathers)
        {
            scrap = scrapIron;
            yolk = yolkCores;
            feathers = goldenFeathers;
        }

        public void RestoreResources(int scrap, int yolk, int feathers)
        {
            scrapIron = scrap;
            yolkCores = yolk;
            goldenFeathers = feathers;
        }

        private void MarkProfileDirty()
        {
            if (SaveSystem.Instance != null) SaveSystem.Instance.MarkDirty();
        }

        // ---------------------------------------------------------------- State handlers
        private void InitializeMainMenu()
        {
            SaveSystem.Instance?.Load();
            MainMenuController.Instance?.Show();
        }

        private void GenerateOrLoadMap()
        {
            SaveSystem.Instance?.Load();
            SectorMapGenerator.Instance?.GenerateOrLoad(CurrentRunSeed);
            SectorMapUI.Instance?.Render();
        }

        private void StartCombatLoop()
        {
            // CombatDirector owns pool prewarm, player respawn, and fleet spawning
            // (it reacts to StateChanged before this handler runs).
            SaveSystem.Instance?.Load();
            HUDController.Instance?.Show();
        }

        private void OpenHubInterface()
        {
            SaveSystem.Instance?.Load();
            HubBaseController.Instance?.Show();
        }

        // ---------------------------------------------------------------- Node completion bridge
        /// <summary>Called by the combat loop when the player clears/abandons a sector node.</summary>
        public void CompleteNode(int nodeId, bool victory)
        {
            if (!victory) return;

            SectorMapGenerator.Instance?.MarkNodeCompleted(nodeId);

            if (SectorMapGenerator.Instance != null &&
                SectorMapGenerator.Instance.IsBossDefeated)
            {
                AddResource(ResourceType.GoldenFeathers, 1);
                ChangeState(GameState.HubBase);
            }
            else
            {
                ChangeState(GameState.SectorMap);
            }
        }

        public int[] GetCompletedNodeIds()
        {
            return SectorMapGenerator.Instance != null
                ? SectorMapGenerator.Instance.GetCompletedNodeIds()
                : EmptyIntArray;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            StateChanged = null;
        }
    }

    public enum GameState
    {
        MainMenu,
        SectorMap,
        Gameplay,
        HubBase
    }

    public enum ResourceType
    {
        ScrapIron,
        YolkCores,
        GoldenFeathers
    }
}
