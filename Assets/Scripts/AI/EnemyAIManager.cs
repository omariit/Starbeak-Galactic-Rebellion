using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Module B - The Adaptive AI Faction System.
    /// Hosts the Flock Mind Matrix: scans player coordinates every 0.25s, detects the
    /// player's weapon spread angle and weapon archetypes, then reshapes flock
    /// formations accordingly (ArmoredColumns vs. side-stepping offsets).
    /// </summary>
    public class EnemyAIManager : MonoBehaviour
    {
        public static EnemyAIManager Instance { get; private set; }

        [Header("Fleet")]
        [SerializeField] private int fleetSize = 24;
        [SerializeField] private float formationCellWidth = 150f;
        [SerializeField] private float formationCellHeight = 140f;
        [SerializeField] private int formationColumns = 6;
        [SerializeField] private float formationSwayAmplitude = 120f;
        [SerializeField] private float formationSwayPeriod = 4f;
        [SerializeField] private float formationDescentSpeed = 16f;

        [Header("Flock Mind Matrix")]
        [SerializeField] private float scanInterval = GameConstants.FlockScanInterval;
        [SerializeField] private float wideSpreadThreshold = GameConstants.WideSpreadThresholdDeg;
        [SerializeField] private float armoredColumnHealthBonus = GameConstants.ArmoredColumnHealthBonus;
        [SerializeField] private float beamSideStepOffset = GameConstants.BeamSideStepOffset;

        [Header("Difficulty")]
        [SerializeField] private float eliteChance = 0.22f;
        [SerializeField] private float healthScalePerTier = 0.35f;
        [SerializeField] private float eliteEliteWeaponChance = 0.6f;

        private readonly List<Chicken> activeChickens = new List<Chicken>(GameConstants.EnemyBudget);
        private float scanTimer;
        private float swayTime;
        private float formationDescent;
        private int activeTier = 1;
        private bool active;
        private bool bossNodePending;
        private Chicken bossChicken;

        /// <summary>The most recent player position sampled by the game camera.</summary>
        public Vector2 LastKnownPlayerPosition { get; private set; }

        /// <summary>Fires with the total kill count whenever a chicken dies.</summary>
        public static event Action<int> EnemyKilled;

        /// <summary>True while the sector boss is alive on screen.</summary>
        public bool BossActive => bossChicken != null && bossChicken.isActiveAndEnabled;

        private int killCount;

        /// <summary>True while the fleet is currently grouped into horizontal ArmoredColumns.</summary>
        public bool ArmoredColumnsActive { get; private set; }

        /// <summary>True while the fleet is side-stepping to break a piercing beam.</summary>
        public bool SideStepActive { get; private set; }

        public int ActiveCount => activeChickens.Count;
        public int NodeId { get; set; } = -1;

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

        // ---------------------------------------------------------------- Lifecycle
        public void Activate()
        {
            active = true;
            scanTimer = 0f;
            swayTime = 0f;
            formationDescent = 0f;
            ArmoredColumnsActive = false;
            SideStepActive = false;
            killCount = 0;
            bossChicken = null;
        }

        public void Deactivate()
        {
            active = false;
            bossNodePending = false;
            bossChicken = null;
            for (int i = activeChickens.Count - 1; i >= 0; i--)
            {
                ObjectPooler.Instance?.ReturnChicken(activeChickens[i]);
            }
            activeChickens.Clear();
        }

        /// <summary>Configures tier-scaled difficulty for the incoming sector.</summary>
        public void ConfigureForNode(int nodeId, int tier, bool isBossNode)
        {
            NodeId = nodeId;
            activeTier = Mathf.Max(1, tier);
            eliteChance = Mathf.Min(0.5f, 0.15f + activeTier * 0.06f);
            bossNodePending = isBossNode;
        }

        // ---------------------------------------------------------------- Spawning
        /// <summary>Builds the fleet for the current sector node.</summary>
        public void SpawnFleet(int count)
        {
            if (bossNodePending)
            {
                SpawnBoss();
                count = Mathf.Max(4, count / 2); // boss is escorted by a smaller elite wing
            }

            int target = Mathf.Clamp(count, 1, GameConstants.EnemyBudget);
            for (int i = 0; i < target; i++) SpawnChicken(i);

            ApplyFlockFormation();
        }

        /// <summary>The Rooster Warlord: single big entity with an elite weapon rotation.</summary>
        private void SpawnBoss()
        {
            Chicken boss = ObjectPooler.Instance.SpawnChicken(new Vector2(0f, 760f), Quaternion.identity);
            boss.NodeId = NodeId;
            boss.ConfigureAsBoss(activeTier);
            boss.SetFormationAnchor(new Vector2(0f, 520f));
            boss.IsFrontRow = false;
            activeChickens.Add(boss);
            bossChicken = boss;

            VFXManager.Instance?.SpawnBossTelegraph(boss.transform.position, 400f);
            AudioManager.Instance?.PlaySfx("sfx_warning", 1f);
        }

        /// <summary>Routed by Chicken.Die so the kill streak + map progress stay in sync.</summary>
        public void NotifyChickenKilled(Chicken chicken)
        {
            killCount++;
            EnemyKilled?.Invoke(killCount);
            if (chicken == bossChicken) bossChicken = null;
        }

        private void SpawnChicken(int formationIndex)
        {
            Vector2 position = ComputeFormationSlot(formationIndex, formationColumns);
            Chicken chicken = ObjectPooler.Instance.SpawnChicken(position, Quaternion.identity);
            chicken.NodeId = NodeId;

            float tierHealthScale = 1f + (activeTier - 1) * healthScalePerTier;
            chicken.SetMaxHealth(30f * tierHealthScale, true);

            if (Random.value < eliteChance)
            {
                if (Random.value < eliteEliteWeaponChance)
                {
                    EliteWeapon[] weapons = {
                        EliteWeapon.BoiledNapalmEgg,
                        EliteWeapon.MagneticYolkCharge,
                        EliteWeapon.FlakShrapnel
                    };
                    chicken.SetEliteWeapon(weapons[Random.Range(0, weapons.Length)]);
                }
            }
            chicken.SetFormationAnchor(position);
            activeChickens.Add(chicken);
        }

        /// <summary>Computes the grid slot (in world space) for a formation index.</summary>
        private Vector2 ComputeFormationSlot(int index, int columns)
        {
            int row = index / columns;
            int column = index % columns;

            float width = columns * formationCellWidth;
            float x = (column - (columns - 1) * 0.5f) * formationCellWidth;
            float y = 700f - row * formationCellHeight;
            return new Vector2(x, y);
        }

        // ---------------------------------------------------------------- Flock Mind Matrix
        private void Update()
        {
            if (!active) return;

            float dt = Time.deltaTime;
            swayTime += dt;
            formationDescent += formationDescentSpeed * dt;

            scanTimer -= dt;
            if (scanTimer <= 0f)
            {
                scanTimer = scanInterval;
                RunFlockMindScan();
            }

            UpdateSideStepOffset(dt);
            ApplyFlockFormation();
        }

        /// <summary>Scans player coordinates + weapon profile every 0.25s and adapts the faction.</summary>
        private void RunFlockMindScan()
        {
            PlayerShip player = PlayerShip.Instance;
            if (player == null) return;

            LastKnownPlayerPosition = player.Position;
            WeaponInstance weapon = player.EquippedWeapon;

            float spreadDeg = weapon != null ? weapon.FinalSpreadAngle : 0f;
            bool isPiercingBeam = weapon != null && weapon.IsPiercingBeam;

            // Wide-Spread Detection: group chickens horizontally into clustered ArmoredColumns.
            bool shouldColumn = spreadDeg > wideSpreadThreshold;
            if (shouldColumn != ArmoredColumnsActive)
            {
                ArmoredColumnsActive = shouldColumn;
            }

            // Narrow Beam Detection: side-step to break piercing lines.
            bool shouldSideStep = isPiercingBeam && !shouldColumn;
            if (shouldSideStep != SideStepActive)
            {
                SideStepActive = shouldSideStep;
                if (SideStepActive) sideStepTimer = 0f;
            }
        }

        private float sideStepTimer;
        private float currentSideStep;

        private void UpdateSideStepOffset(float dt)
        {
            if (!SideStepActive) return;

            sideStepTimer -= dt;
            if (sideStepTimer > 0f) return;
            sideStepTimer = 1.1f;

            // Apply side-stepping flock offset vectors of X ± 150px to break lines.
            currentSideStep = (currentSideStep <= 0f ? 1f : -1f) * beamSideStepOffset;
        }

        /// <summary>Rebuilds per-chicken formation targets with sway, descent, and flock offsets.</summary>
        private void ApplyFlockFormation()
        {
            float sway = Mathf.Sin(swayTime * (Mathf.PI * 2f / formationSwayPeriod)) * formationSwayAmplitude;

            for (int i = 0; i < activeChickens.Count; i++)
            {
                Chicken chicken = activeChickens[i];
                if (chicken == null) continue;

                Vector2 anchor = chicken.FormationAnchor;
                Vector2 target;

                if (ArmoredColumnsActive)
                {
                    // Horizontal clustered columns: collapse vertical spread into tight rows.
                    target = new Vector2(anchor.x + sway, anchor.y * 0.35f - formationDescent);
                    chicken.ApplyArmorColumnBonus(armoredColumnHealthBonus);
                }
                else
                {
                    target = new Vector2(anchor.x + sway, anchor.y - formationDescent);
                    chicken.IsFrontRow = false;
                }

                if (SideStepActive) target.x += currentSideStep;
                chicken.StepMovement(target, Time.deltaTime);
                chicken.StepEliteWeapons(Time.deltaTime);
            }
        }

        /// <summary>Removes a chicken that was returned to the pool.</summary>
        public void Unregister(Chicken chicken)
        {
            if (chicken == null) return;
            activeChickens.Remove(chicken);
        }

        /// <summary>True when every enemy in the sector has been cleared.</summary>
        public bool SectorCleared => active && activeChickens.Count == 0;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
