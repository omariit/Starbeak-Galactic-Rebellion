using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        /// <summary>Raised when a node resolves: true = sector cleared.</summary>
        public static event Action<bool> NodeResolved;

        private static string SceneNameFor(GameState state)
        {
            switch (state)
            {
                case GameState.MainMenu:  return "MainMenu";
                case GameState.SectorMap: return "SectorMap";
                case GameState.Gameplay:  return "Gameplay";
                case GameState.HubBase:   return "Hub";
                default: return null;
            }
        }

        private void Awake()
        {
            CrashLog.Info("GameManager.Awake:enter");
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CrashLog.Info("GameManager.Awake:singleton");

            IsProductionBuild = Debug.isDebugBuild == false;
            ApplyProductionOptimizations();
            CrashLog.Info("GameManager.Awake:optimizations");
            CurrentRunSeed = Environment.TickCount;

            // Portrait only, never upside down. Some OEM launchers rotate a
            // portrait-locked activity 180 degrees unless this is explicit.
            Screen.autorotateToPortrait = true;
            Screen.autorotateToPortraitUpsideDown = false;
            if (Screen.orientation != ScreenOrientation.Portrait) Screen.orientation = ScreenOrientation.Portrait;
            CrashLog.Info("GameManager.Awake:orientation");
        }

        /// <summary>True when the most recent completed sector was the galaxy boss.</summary>
        public static bool LastRunVictory { get; private set; }

        private void Start()
        {
            CrashLog.Info("GameManager.Start:enter");
            SceneManager.sceneLoaded += HandleSceneLoaded;

            // Boot into the main menu on first launch.
            if (!CrashLog.HasArgument("-starbeakNoSceneLoad"))
            {
                ChangeState(GameState.MainMenu);
                StartCoroutine(TraceFirstFrames());
            }
            CrashLog.Info("GameManager.Start:leave");
        }

        private System.Collections.IEnumerator TraceFirstFrames()
        {
            for (int frame = 0; frame < 3; frame++)
            {
                CrashLog.Info($"GameManager.Frame:{frame}:begin");
                yield return new WaitForEndOfFrame();
                CrashLog.Info($"GameManager.Frame:{frame}:end");
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CrashLog.Info($"GameManager.SceneLoaded:{scene.name}:enter");
            // Per-scene UI canvases only exist after their scene activates; run the
            // state's init once more so this scene's controllers pick up the session.
            RunStateInit(CurrentState);
            CrashLog.Info($"GameManager.SceneLoaded:{scene.name}:leave");
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
            CrashLog.Info("GameManager.Optimizations:enter");
            Application.targetFrameRate = GameConstants.TargetFrameRate;
            QualitySettings.vSyncCount = 0;
            Time.fixedDeltaTime = GameConstants.FixedDeltaTime;
            CrashLog.Info("GameManager.Optimizations:leave");
        }

        public void ChangeState(GameState newState)
        {
            CrashLog.Info($"GameManager.ChangeState:{newState}:enter");
            bool stateSame = CurrentState == newState;
            bool sceneOk = SceneMatchesState(newState);
            if (stateSame && sceneOk) return;

            CurrentState = newState;
            StateChanged?.Invoke(newState);
            CrashLog.Info($"GameManager.ChangeState:{newState}:event");

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
            CrashLog.Info($"GameManager.ChangeState:{newState}:music");

            // Fresh camera shake state on every transition.
            VFXManager.Instance?.ClearTrauma();

            string scene = SceneNameFor(newState);
            if (!string.IsNullOrEmpty(scene) &&
                !string.Equals(SceneManager.GetActiveScene().name, scene, System.StringComparison.Ordinal))
            {
                // Transition data lives in DontDestroyOnLoad singletons; the per-scene
                // UI controllers wake up on the load and get initialized in HandleSceneLoaded.
                CrashLog.Info($"GameManager.ChangeState:{newState}:load:{scene}");
                SceneManager.LoadScene(scene, LoadSceneMode.Single);
                CrashLog.Info($"GameManager.ChangeState:{newState}:loaded:{scene}");
            }
            else
            {
                RunStateInit(newState);
            }
            CrashLog.Info($"GameManager.ChangeState:{newState}:leave");
        }

        private static bool SceneMatchesState(GameState state)
        {
            string wanted = SceneNameFor(state);
            return string.IsNullOrEmpty(wanted) ||
                   string.Equals(SceneManager.GetActiveScene().name, wanted, System.StringComparison.Ordinal);
        }

        private void RunStateInit(GameState state)
        {
            CrashLog.Info($"GameManager.RunStateInit:{state}:enter");
            // Every transition is guarded: a failure in one subsystem must never abort
            // the boot sequence (an exception inside a native scene-load callback is
            // fatal on IL2CPP releases).
            try
            {
                switch (state)
                {
                    case GameState.MainMenu:  InitializeMainMenu();  break;
                    case GameState.SectorMap: GenerateOrLoadMap();   break;
                    case GameState.Gameplay:  StartCombatLoop();     break;
                    case GameState.HubBase:   OpenHubInterface();    break;
                }
            }
            catch (System.Exception e)
            {
                CrashLog.Error($"RunStateInit({state})", e);
                Debug.LogError($"[GameManager] state init failed for {state}: {e}");
            }
            CrashLog.Info($"GameManager.RunStateInit:{state}:leave");
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

            LastRunVictory = true;
            SectorMapGenerator.Instance?.MarkNodeCompleted(nodeId);
            NodeResolved?.Invoke(true);

            if (SectorMapGenerator.Instance != null &&
                SectorMapGenerator.Instance.IsBossDefeated)
            {
                AddResource(ResourceType.GoldenFeathers, 3);
                ChangeState(GameState.HubBase);
            }
            else
            {
                ChangeState(GameState.SectorMap);
            }
        }

        /// <summary>Called by PlayerShip when the hull is destroyed.</summary>
        public void RegisterDefeat()
        {
            LastRunVictory = false;
            NodeResolved?.Invoke(false);
            ChangeState(GameState.SectorMap);
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
            NodeResolved = null;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
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
