using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Sector map navigation view: renders the procedural node graph with one button per
    /// node and connector lines between them, gating taps behind node accessibility.
    /// </summary>
    public class SectorMapUI : MonoBehaviour
    {
        public static SectorMapUI Instance { get; private set; }

        [Header("View")]
        [SerializeField] public GameObject rootPanel;
        [SerializeField] public RectTransform graphContainer;

        [Header("Prefabs")]
        [SerializeField] public Button nodeButtonPrefab;
        [SerializeField] public GameObject edgeLinePrefab;
        [SerializeField] public Sprite combatIcon;
        [SerializeField] public Sprite anomalyIcon;
        [SerializeField] public Sprite marketIcon;
        [SerializeField] public Sprite bossIcon;

        [Header("Labels")]
        [SerializeField] public Text headerText;

        private readonly List<Button> spawnedButtons = new List<Button>(32);
        private readonly List<GameObject> spawnedEdges = new List<GameObject>(32);
        private readonly List<(Vector3 a, Vector3 b)> edges = new List<(Vector3, Vector3)>(32);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
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
            if (rootPanel != null) rootPanel.SetActive(state == GameState.SectorMap);
            if (state == GameState.SectorMap) Render();
        }

        /// <summary>Rebuilds the whole map view from the generator's current galaxy.</summary>
        public void Render()
        {
            SectorMapGenerator generator = SectorMapGenerator.Instance;
            if (generator == null || rootPanel == null) return;

            ClearView();

            IReadOnlyList<SectorNode> nodes = generator.Nodes;
            for (int i = 0; i < nodes.Count; i++)
            {
                SpawnNodeButton(nodes[i], generator);
            }

            generator.GetEdges(edges);
            for (int i = 0; i < edges.Count; i++)
            {
                SpawnEdge(edges[i].a, edges[i].b);
            }

            if (headerText != null)
            {
                headerText.text = $"Galaxy Seed {generator.ActiveSeed}  -  {nodes.Count} Sectors";
            }
        }

        private void ClearView()
        {
            for (int i = 0; i < spawnedButtons.Count; i++)
            {
                if (spawnedButtons[i] != null) Destroy(spawnedButtons[i].gameObject);
            }
            for (int i = 0; i < spawnedEdges.Count; i++)
            {
                if (spawnedEdges[i] != null) Destroy(spawnedEdges[i]);
            }
            spawnedButtons.Clear();
            spawnedEdges.Clear();
        }

        private void SpawnNodeButton(SectorNode node, SectorMapGenerator generator)
        {
            if (nodeButtonPrefab == null || graphContainer == null) return;

            Button button = Instantiate(nodeButtonPrefab, graphContainer);
            RectTransform rect = button.GetComponent<RectTransform>();
            Vector3 position = generator.GetNodeWorldPosition(node.id);
            rect.anchoredPosition = position;

            ConfigureNodeVisual(button, node, generator);
            button.onClick.AddListener(() => OnNodeSelected(node.id, node));
            spawnedButtons.Add(button);
        }

        private void ConfigureNodeVisual(Button button, SectorNode node, SectorMapGenerator generator)
        {
            Image image = button.targetGraphic as Image;
            if (image != null)
            {
                switch (node.type)
                {
                    case NodeType.Combat:         image.sprite = combatIcon; break;
                    case NodeType.NebulaAnomaly:  image.sprite = anomalyIcon; break;
                    case NodeType.BlackMarket:    image.sprite = marketIcon; break;
                    case NodeType.BossStronghold: image.sprite = bossIcon; break;
                }
            }

            bool accessible = generator.IsNodeAccessible(node.id);
            bool completed = generator.IsNodeCompleted(node.id);
            button.interactable = accessible && !completed;

            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                string name = node.IsBoss ? "BOSS" : node.type.ToString();
                label.text = completed ? $"{name}\n[Clear]" : name;
            }
        }

        private void SpawnEdge(Vector3 a, Vector3 b)
        {
            if (edgeLinePrefab == null || graphContainer == null) return;

            GameObject edge = Instantiate(edgeLinePrefab, graphContainer);
            RectTransform rect = edge.GetComponent<RectTransform>();

            Vector2 midpoint = (a + b) * 0.5f;
            float distance = Vector2.Distance(a, b);
            float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;

            rect.anchoredPosition = midpoint;
            rect.sizeDelta = new Vector2(distance, 6f);
            rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            spawnedEdges.Add(edge);
        }

        private void OnNodeSelected(int nodeId, SectorNode node)
        {
            SectorMapGenerator generator = SectorMapGenerator.Instance;
            if (generator == null || !generator.IsNodeAccessible(nodeId)) return;

            AudioManager.Instance?.PlaySfx("sfx_node_select");
            if (node.IsBoss) AudioManager.Instance?.PlaySfx("sfx_warning");

            switch (node.type)
            {
                case NodeType.BlackMarket:
                    // Markets resolve at the hub-style overlay, then return here.
                    HubBaseController.Instance?.ShowTransientMarket(nodeId);
                    break;

                case NodeType.NebulaAnomaly:
                    // Anomalies grant a resource windfall without combat.
                    GameManager.Instance.AddResource(ResourceType.YolkCores, 1);
                    AudioManager.Instance?.PlaySfx("sfx_pickup");
                    generator.MarkNodeCompleted(nodeId);
                    Render();
                    break;

                default:
                    EnemyAIManager.Instance?.ConfigureForNode(nodeId, node.tier, node.IsBoss);
                    GameManager.Instance.ChangeState(GameState.Gameplay);
                    break;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
