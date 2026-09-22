using UnityEngine;
using UnityEngine.UI;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// In-combat HUD: hull/shield bars, resource counters, node name, and the pause control.
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        public static HUDController Instance { get; private set; }

        [SerializeField] public GameObject rootPanel;
        [SerializeField] public Image hullFill;
        [SerializeField] public Image shieldFill;
        [SerializeField] public Text scrapText;
        [SerializeField] public Text yolkText;
        [SerializeField] public Text sectorLabel;
        [SerializeField] public Button pauseButton;
        [SerializeField] public GameObject pauseOverlay;

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

        private void Start()
        {
            if (pauseButton != null) pauseButton.onClick.AddListener(OnPause);
            if (pauseOverlay != null) pauseOverlay.SetActive(false);
        }

        private void HandleStateChanged(GameState state)
        {
            if (rootPanel != null) rootPanel.SetActive(state == GameState.Gameplay);
            if (state == GameState.Gameplay) RefreshSectorLabel();
        }

        public void Show()
        {
            if (rootPanel != null) rootPanel.SetActive(true);
            RefreshSectorLabel();
        }

        public void Hide()
        {
            if (rootPanel != null) rootPanel.SetActive(false);
        }

        private void Update()
        {
            if (rootPanel == null || !rootPanel.activeSelf) return;

            PlayerShip player = PlayerShip.Instance;
            if (player == null) return;

            if (hullFill != null) hullFill.fillAmount = player.HullPct;
            if (shieldFill != null) shieldFill.fillAmount = player.ShieldPct;

            GameManager gm = GameManager.Instance;
            if (gm != null)
            {
                if (scrapText != null) scrapText.text = gm.GetResource(ResourceType.ScrapIron).ToString("N0");
                if (yolkText != null) yolkText.text = gm.GetResource(ResourceType.YolkCores).ToString("N0");
            }

            if (EnemyAIManager.Instance != null && EnemyAIManager.Instance.SectorCleared)
            {
                OnSectorCleared();
            }
        }

        private void RefreshSectorLabel()
        {
            if (sectorLabel == null) return;
            SectorMapGenerator generator = SectorMapGenerator.Instance;
            sectorLabel.text = generator != null
                ? $"Sector {generator.EntryNodeId + 1} -> Node {EnemyAIManager.Instance?.NodeId ?? -1}"
                : "Sector";
        }

        private void OnSectorCleared()
        {
            int nodeId = EnemyAIManager.Instance.NodeId;
            EnemyAIManager.Instance.Deactivate();
            GameManager.Instance?.CompleteNode(nodeId, victory: true);
        }

        private void OnPause()
        {
            if (pauseOverlay == null) return;
            bool paused = !pauseOverlay.activeSelf;
            pauseOverlay.SetActive(paused);
            Time.timeScale = paused ? 0f : 1f;
            AudioManager.Instance?.PlaySfx(paused ? "sfx_ui_back" : "sfx_ui_click");
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                Time.timeScale = 1f;
            }
        }
    }
}
