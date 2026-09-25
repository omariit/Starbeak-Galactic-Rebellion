using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Combat loop director. Wires the combat scene together when the state machine
    /// enters <see cref="GameState.Gameplay"/>: prewarms pools, respawns the player,
    /// configures the faction for the selected node, and spawns the sector fleet.
    /// </summary>
    public class CombatDirector : MonoBehaviour
    {
        public static CombatDirector Instance { get; private set; }

        [Header("Fleet sizing")]
        [SerializeField] public int baseFleetSize = 16;
        [SerializeField] public int fleetSizePerTier = 4;
        [SerializeField] public int bossFleetSize = 1;

        [Header("References")]
        [SerializeField] public ObjectPooler pooler;
        [SerializeField] public EnemyAIManager aiManager;
        [SerializeField] public PlayerShip playerShip;

        private bool running;

        private void Awake()
        {
            CrashLog.Info("CombatDirector.Awake:enter");
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CrashLog.Info("CombatDirector.Awake:leave");
        }

        private void OnEnable()
        {
            GameManager.StateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            GameManager.StateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.Gameplay)
            {
                BeginSector();
            }
            else if (running)
            {
                EndSector();
            }
        }

        private void BeginSector()
        {
            running = true;

            if (pooler == null) pooler = ObjectPooler.Instance;
            if (aiManager == null) aiManager = EnemyAIManager.Instance;
            if (playerShip == null) playerShip = PlayerShip.Instance;

            pooler?.Prewarm();
            playerShip?.Respawn();
            aiManager?.Activate();
            aiManager?.SpawnFleet(ComputeFleetSize());

            // Sector entry feedback: warning sting or a full boss roar.
            AudioManager audio = AudioManager.Instance;
            if (audio != null)
            {
                SectorMapGenerator map = SectorMapGenerator.Instance;
                int currentNodeId = aiManager != null ? aiManager.NodeId : -1;
                SectorNode node = map != null ? map.GetNode(currentNodeId) : default;
                if (node.id >= 0 && node.IsBoss) audio.PlaySfx("sfx_boss_roar", 1f);
            }
            VFXManager.Instance?.CacheCamera();
        }

        private void EndSector()
        {
            running = false;
            aiManager?.Deactivate();
            pooler?.FlushAll();
        }

        private int ComputeFleetSize()
        {
            EnemyAIManager ai = aiManager != null ? aiManager : EnemyAIManager.Instance;
            SectorMapGenerator map = SectorMapGenerator.Instance;
            if (ai == null || map == null) return baseFleetSize;

            SectorNode node = map.GetNode(ai.NodeId);
            if (node.IsBoss) return bossFleetSize;

            int galaxy = GameManager.Instance != null ? GameManager.Instance.GalaxyIndex : 0;
            return Mathf.Clamp(baseFleetSize + node.tier * fleetSizePerTier + galaxy * 3, 1, GameConstants.EnemyBudget);
        }

        /// <summary>Abandons the current sector (used by the pause menu's retreat option).</summary>
        public void AbandonSector()
        {
            int nodeId = EnemyAIManager.Instance != null ? EnemyAIManager.Instance.NodeId : -1;
            EnemyAIManager.Instance?.Deactivate();
            ObjectPooler.Instance?.FlushAll();
            GameManager.Instance?.CompleteNode(nodeId, victory: false);
            running = false;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
