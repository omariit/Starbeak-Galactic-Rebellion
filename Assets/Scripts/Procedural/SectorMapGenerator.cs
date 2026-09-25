using System.Collections.Generic;
using UnityEngine;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Module A - The Procedural Run Engine.
    /// Generates a node-based network spanning 5 deep-space tiers per galaxy:
    /// Tier 0 has 1 entry node, tiers 1-3 branch into 2-4 paths from a seed, and
    /// Tier 4 consolidates into 1 mandatory Boss stronghold node. Adjacent tiers
    /// are connected dynamically with a guaranteed no-orphan pass.
    /// </summary>
    public class SectorMapGenerator : MonoBehaviour
    {
        public static SectorMapGenerator Instance { get; private set; }

        [Header("Layout")]
        [SerializeField] private int tierCount = GameConstants.TierCount;
        [SerializeField] private int minBranches = GameConstants.MinBranchesPerTier;
        [SerializeField] private int maxBranches = GameConstants.MaxBranchesPerTier;
        [SerializeField] private float tierVerticalSpacing = 340f;
        [SerializeField] private float nodeHorizontalSpread = 640f;

        /// <summary>True when the mandatory boss node has been cleared.</summary>
        public bool IsBossDefeated { get; private set; }
        public int HighestReachedTier { get; private set; }

        private SectorMapData map;
        private System.Random rng;
        private int nextId;

        /// <summary>Flat id -> node lookup, rebuilt on generation.</summary>
        private readonly Dictionary<int, SectorNode> nodeLookup = new Dictionary<int, SectorNode>(32);

        public IReadOnlyList<SectorNode> Nodes => map.nodes;
        public int EntryNodeId => map != null ? map.entryNodeId : -1;
        public int BossNodeId => map != null ? map.bossNodeId : -1;
        public int ActiveSeed => map != null ? map.seed : 0;

        private void Awake()
        {
            CrashLog.Info("SectorMapGenerator.Awake:enter");
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            CrashLog.Info("SectorMapGenerator.Awake:leave");
        }

        /// <summary>
        /// Loads a map for the given seed if one already exists for this run,
        /// otherwise generates a fresh procedural galaxy.
        /// </summary>
        public void GenerateOrLoad(int seed)
        {
            if (map != null && map.seed == seed) return;
            Generate(seed);
        }

        public void Generate(int seed)
        {
            DiscardMap();
            map = new SectorMapData { seed = seed };
            rng = new System.Random(seed);
            nextId = 0;
            IsBossDefeated = false;

            GenerateEntryNode();
            GenerateBranchingTiers();
            GenerateBossNode();
            ConnectAdjacentTiers();
            EnsureNoOrphanedNodes();

            HighestReachedTier = 0;
        }

        public void DiscardMap()
        {
            map = new SectorMapData { seed = -1 };
            nodeLookup.Clear();
            IsBossDefeated = false;
            HighestReachedTier = 0;
        }

        // ---------------------------------------------------------------- Generation passes
        private void GenerateEntryNode()
        {
            SectorNode entry = CreateNode(GameConstants.EntryTier, NodeType.Combat);
            map.entryNodeId = entry.id;
        }

        /// <summary>Tiers 1..(tierCount-2) split into 2-4 branching paths.</summary>
        private void GenerateBranchingTiers()
        {
            int lastBranchingTier = tierCount - 2; // tier 3 when tierCount == 5

            for (int tier = 1; tier <= lastBranchingTier; tier++)
            {
                int branches = rng.Next(minBranches, maxBranches + 1);

                for (int b = 0; b < branches; b++)
                {
                    NodeType type = RollNodeType(tier);
                    SectorNode node = CreateNode(tier, type);

                    // Pick a random pre-existing node as the parent. Node ids are dense and
                    // 0-based, so a uniform id in [0, nodeCount - 1] is always valid here
                    // (the boss is created later and is therefore excluded).
                    int parentId = map.nodes[rng.Next(0, map.nodes.Count)].id;
                    Link(parentId, node.id);
                }
            }
        }

        /// <summary>Tier 4 consolidates into exactly one mandatory Boss stronghold node.</summary>
        private void GenerateBossNode()
        {
            SectorNode boss = CreateNode(GameConstants.BossTier, NodeType.BossStronghold);
            map.bossNodeId = boss.id;

            // Every non-boss node in the final branching tier feeds into the boss.
            int finalBranchingTier = tierCount - 2;
            for (int i = 0; i < map.nodes.Count; i++)
            {
                if (map.nodes[i].tier == finalBranchingTier)
                {
                    Link(map.nodes[i].id, boss.id);
                }
            }
        }

        /// <summary>Connect adjacent tiers dynamically: guarantees every tier-n node has at least one tier-(n+1) link.</summary>
        private void ConnectAdjacentTiers()
        {
            for (int tier = 0; tier < tierCount - 1; tier++)
            {
                List<SectorNode> currentTier = GetTier(tier);
                List<SectorNode> nextTierNodes = GetTier(tier + 1);
                if (currentTier.Count == 0 || nextTierNodes.Count == 0) continue;

                // The boss tier already has all inbound edges; only stitch ordinary tiers.
                if (tier + 1 == GameConstants.BossTier) continue;

                for (int i = 0; i < currentTier.Count; i++)
                {
                    SectorNode node = currentTier[i];
                    bool hasForwardLink = AnyForwardLink(node, tier + 1);
                    if (!hasForwardLink)
                    {
                        SectorNode target = nextTierNodes[rng.Next(0, nextTierNodes.Count)];
                        Link(node.id, target.id);
                    }
                }
            }
        }

        /// <summary>
        /// Final safety sweep: any node with zero edges is stitched to a neighbor in an
        /// adjacent tier so no orphaned nodes exist.
        /// </summary>
        private void EnsureNoOrphanedNodes()
        {
            // Snapshot the ids first: Link() mutates the list during iteration.
            int[] candidateIds = new int[map.nodes.Count];
            for (int i = 0; i < map.nodes.Count; i++) candidateIds[i] = map.nodes[i].id;

            for (int i = 0; i < candidateIds.Length; i++)
            {
                int nodeId = candidateIds[i];
                if (GetNode(nodeId).connectedNodeIds.Count > 0) continue;

                SectorNode adopter = FindAdopter(GetNode(nodeId));
                if (adopter.id != -1)
                {
                    Link(nodeId, adopter.id);
                }
                else if (candidateIds.Length > 1)
                {
                    Link(nodeId, candidateIds[(i + 1) % candidateIds.Length]);
                }
            }
        }

        // ---------------------------------------------------------------- Node creation / lookup

        /// <summary>
        /// Bidirectionally links two nodes. SectorNode is a struct, so mutations must be
        /// written back into BOTH the list and the lookup or they are silently discarded.
        /// All edge creation goes through this helper.
        /// </summary>
        private void Link(int nodeAId, int nodeBId)
        {
            Store(nodeAId, n => n.ConnectTo(nodeBId));
            Store(nodeBId, n => n.ConnectTo(nodeAId));
        }

        /// <summary>Applies a mutation to a node and persists it to both collections.</summary>
        private void Store(int nodeId, System.Action<SectorNode> mutation)
        {
            if (!nodeLookup.TryGetValue(nodeId, out SectorNode node)) return;
            mutation(node);
            nodeLookup[nodeId] = node;

            for (int i = 0; i < map.nodes.Count; i++)
            {
                if (map.nodes[i].id == nodeId)
                {
                    map.nodes[i] = node;
                    return;
                }
            }
        }

        private SectorNode CreateNode(int tier, NodeType type)
        {
            SectorNode node = new SectorNode(nextId++, tier, type);
            map.nodes.Add(node);
            nodeLookup.Add(node.id, node);
            return node;
        }

        private NodeType RollNodeType(int tier)
        {
            double roll = rng.NextDouble();
            if (roll < 0.10) return NodeType.NebulaAnomaly;
            if (roll < 0.22) return NodeType.BlackMarket;
            return NodeType.Combat;
        }

        public SectorNode GetNode(int id)
        {
            return nodeLookup.TryGetValue(id, out SectorNode node) ? node : default;
        }

        private List<SectorNode> GetTier(int tier)
        {
            List<SectorNode> result = new List<SectorNode>(8);
            for (int i = 0; i < map.nodes.Count; i++)
            {
                if (map.nodes[i].tier == tier) result.Add(map.nodes[i]);
            }
            return result;
        }

        private bool AnyForwardLink(SectorNode node, int targetTier)
        {
            for (int i = 0; i < node.connectedNodeIds.Count; i++)
            {
                if (GetNode(node.connectedNodeIds[i]).tier == targetTier) return true;
            }
            return false;
        }

        private SectorNode FindAdopter(SectorNode orphan)
        {
            // Prefer a same-or-adjacent-tier node that is not itself the orphan.
            for (int delta = 1; delta < tierCount; delta++)
            {
                int forward = orphan.tier + delta;
                int backward = orphan.tier - delta;

                if (forward < tierCount)
                {
                    List<SectorNode> tier = GetTier(forward);
                    if (tier.Count > 0) return tier[rng.Next(0, tier.Count)];
                }
                if (backward >= 0)
                {
                    List<SectorNode> tier = GetTier(backward);
                    if (tier.Count > 0) return tier[rng.Next(0, tier.Count)];
                }
            }
            return default;
        }

        // ---------------------------------------------------------------- Progression API
        public void MarkNodeCompleted(int nodeId)
        {
            if (map == null) return;

            Store(nodeId, node => node.isCompleted = true);
            HighestReachedTier = Mathf.Max(HighestReachedTier, GetNode(nodeId).tier);
            SaveSystem.Instance?.RecordNodeCompleted(nodeId);

            if (nodeId == map.bossNodeId)
            {
                IsBossDefeated = true;
                SaveSystem.Instance?.RecordEnemyKill(isBoss: true);
            }
        }

        /// <summary>True when the node is unlocked: it is the entry, or a connected node is completed.</summary>
        public bool IsNodeAccessible(int nodeId)
        {
            if (map == null) return false;
            if (nodeId == map.entryNodeId) return true;

            if (!nodeLookup.TryGetValue(nodeId, out SectorNode node)) return false;

            for (int i = 0; i < node.connectedNodeIds.Count; i++)
            {
                if (nodeLookup.TryGetValue(node.connectedNodeIds[i], out SectorNode linked) && linked.isCompleted)
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsNodeCompleted(int nodeId)
        {
            return nodeLookup.TryGetValue(nodeId, out SectorNode node) && node.isCompleted;
        }

        public int[] GetCompletedNodeIds()
        {
            if (map == null) return System.Array.Empty<int>();

            List<int> ids = new List<int>(map.nodes.Count);
            for (int i = 0; i < map.nodes.Count; i++)
            {
                if (map.nodes[i].isCompleted) ids.Add(map.nodes[i].id);
            }
            return ids.ToArray();
        }

        /// <summary>Restores completion flags from a save (e.g. after relaunch).</summary>
        public void RestoreCompletedNodes(IEnumerable<int> completedIds)
        {
            if (map == null) return;
            if (completedIds == null) return;

            foreach (int id in completedIds)
            {
                if (!nodeLookup.ContainsKey(id)) continue;

                Store(id, node => node.isCompleted = true);
                HighestReachedTier = Mathf.Max(HighestReachedTier, GetNode(id).tier);
                if (id == map.bossNodeId) IsBossDefeated = true;
            }
        }

        /// <summary>World-space position for a node, used by the map navigation view.</summary>
        public Vector3 GetNodeWorldPosition(int nodeId)
        {
            if (!nodeLookup.TryGetValue(nodeId, out SectorNode node)) return Vector3.zero;

            List<SectorNode> tierNodes = GetTier(node.tier);
            int index = tierNodes.IndexOf(node);
            int count = Mathf.Max(1, tierNodes.Count);

            // Center nodes horizontally across the tier's spread.
            float x = ((index + 0.5f) / count - 0.5f) * nodeHorizontalSpread;
            float y = (tierCount - 1 - node.tier) * tierVerticalSpacing;
            return new Vector3(x, y, 0f);
        }

        /// <summary>World-space positions of the edges between connected nodes (for line rendering).</summary>
        public void GetEdges(List<(Vector3 a, Vector3 b)> outEdges)
        {
            outEdges.Clear();
            if (map == null) return;

            HashSet<int> visited = new HashSet<int>();
            for (int i = 0; i < map.nodes.Count; i++)
            {
                SectorNode node = map.nodes[i];
                for (int j = 0; j < node.connectedNodeIds.Count; j++)
                {
                    int otherId = node.connectedNodeIds[j];
                    int key = node.id < otherId ? node.id * 10000 + otherId : otherId * 10000 + node.id;
                    if (visited.Add(key))
                    {
                        outEdges.Add((GetNodeWorldPosition(node.id), GetNodeWorldPosition(otherId)));
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
