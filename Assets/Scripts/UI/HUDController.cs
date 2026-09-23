using UnityEngine;
using UnityEngine.UI;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// In-combat HUD: hull/shield bars, resource counters, node name, kill streaks,
    /// and the pause control. (Victory/defeat banners are handled by BannerHost
    /// because the gameplay canvas dies when the scene transitions.)
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

        [Header("Retreat (optional)")]
        [SerializeField] public Button retreatButton;

        private int streakCount;
        private float streakTimer;
        private Text streakText;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            CreateStreakWidget();
        }

        private void OnEnable()
        {
            GameManager.StateChanged += HandleStateChanged;
            EnemyAIManager.EnemyKilled += HandleEnemyKilled;
        }

        private void OnDisable()
        {
            GameManager.StateChanged -= HandleStateChanged;
            EnemyAIManager.EnemyKilled -= HandleEnemyKilled;
        }

        private void Start()
        {
            if (pauseButton != null) pauseButton.onClick.AddListener(OnPause);
            if (retreatButton != null) retreatButton.onClick.AddListener(OnRetreat);
            if (pauseOverlay != null) pauseOverlay.SetActive(false);
        }

        private void CreateStreakWidget()
        {
            streakText = AddCenterText("StreakText", 84, 0f, 540f, 700f, 160f);
            streakText.color = new Color(1f, 0.85f, 0.4f);
            SetAlpha(streakText, 0f);
        }

        private Text AddCenterText(string name, int fontSize, float x, float y, float w, float h)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.layer = gameObject.layer;

            Text text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.75f);
            outline.effectDistance = new Vector2(3f, -3f);

            RectTransform rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(w, h);
            return text;
        }

        private static void SetAlpha(Text text, float a)
        {
            Color c = text.color;
            c.a = a;
            text.color = c;
        }

        private void HandleEnemyKilled(int total)
        {
            streakTimer = 2.2f;
            streakCount++;

            if (streakCount >= 3)
            {
                streakText.text = $"STREAK x{streakCount}!";
                SetAlpha(streakText, 1f);
            }
            // Punchy kill feedback: camera jolt scales with the streak.
            VFXManager.Instance?.AddTrauma(0.06f + Mathf.Min(0.12f, streakCount * 0.01f));
        }

        private void HandleStateChanged(GameState state)
        {
            if (rootPanel != null) rootPanel.SetActive(state == GameState.Gameplay);
            if (state == GameState.Gameplay)
            {
                streakCount = 0;
                streakTimer = 0f;
                SetAlpha(streakText, 0f);
                RefreshSectorLabel();
            }
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

            // Streak decay: kills within a short window keep the counter alive.
            if (streakTimer > 0f)
            {
                streakTimer -= Time.deltaTime;
                if (streakTimer <= 0f)
                {
                    streakCount = 0;
                    SetAlpha(streakText, 0f);
                }
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

        private void OnRetreat()
        {
            Time.timeScale = 1f;
            AudioManager.Instance?.PlaySfx("sfx_ui_back");
            CombatDirector.Instance?.AbandonSector();
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
