using System;
using System.Collections.Generic;

namespace StarbeakGalacticRebellion
{
    public enum NodeType
    {
        Combat,
        NebulaAnomaly,
        BlackMarket,
        BossStronghold
    }

    /// <summary>
    /// A single node in the procedural galaxy graph.
    /// </summary>
    [Serializable]
    public struct SectorNode
    {
        public int id;
        public int tier;
        public NodeType type;
        public List<int> connectedNodeIds;
        public bool isCompleted;

        public bool IsBoss => type == NodeType.BossStronghold;

        public SectorNode(int id, int tier, NodeType type)
        {
            this.id = id;
            this.tier = tier;
            this.type = type;
            connectedNodeIds = new List<int>(4);
            isCompleted = false;
        }

        public void ConnectTo(int otherId)
        {
            if (otherId == id || connectedNodeIds.Contains(otherId)) return;
            connectedNodeIds.Add(otherId);
        }
    }

    /// <summary>Serializable snapshot of a generated galaxy for persistence.</summary>
    [Serializable]
    public class SectorMapData
    {
        public int seed;
        public List<SectorNode> nodes = new List<SectorNode>();
        public int entryNodeId = -1;
        public int bossNodeId = -1;

        public int NodeCount => nodes.Count;
    }
}
