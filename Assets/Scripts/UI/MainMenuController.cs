using UnityEngine;
using UnityEngine.UI;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Main menu panel: play, settings, and the local-save account panel.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        public static MainMenuController Instance { get; private set; }

        [Header("Panels")]
        [SerializeField] public GameObject rootPanel;
        [SerializeField] public GameObject settingsPanel;

        [Header("Buttons")]
        [SerializeField] public Button continueButton;
        [SerializeField] public Button newRunButton;
        [SerializeField] public Button settingsButton;
        [SerializeField] public Button quitButton;

        [Header("Resource Readout")]
        [SerializeField] public Text scrapText;
        [SerializeField] public Text yolkText;
        [SerializeField] public Text featherText;

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
            SaveSystem.ProfileLoaded += HandleProfileLoaded;
            GameManager.StateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            SaveSystem.ProfileLoaded -= HandleProfileLoaded;
            GameManager.StateChanged -= HandleStateChanged;
        }

        private void Start()
        {
            if (continueButton != null) continueButton.onClick.AddListener(OnContinue);
            if (newRunButton != null) newRunButton.onClick.AddListener(OnNewRun);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettings);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuit);
        }

        private void HandleStateChanged(GameState state)
        {
            if (rootPanel != null) rootPanel.SetActive(state == GameState.MainMenu);
            if (settingsPanel != null) settingsPanel.SetActive(false);
        }

        private void HandleProfileLoaded(ProfileData profile)
        {
            RefreshResourceReadout();
        }

        public void Show()
        {
            if (rootPanel != null) rootPanel.SetActive(true);
            RefreshResourceReadout();
        }

        public void Hide()
        {
            if (rootPanel != null) rootPanel.SetActive(false);
        }

        private void RefreshResourceReadout()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;

            if (scrapText != null) scrapText.text = gm.GetResource(ResourceType.ScrapIron).ToString("N0");
            if (yolkText != null) yolkText.text = gm.GetResource(ResourceType.YolkCores).ToString("N0");
            if (featherText != null) featherText.text = gm.GetResource(ResourceType.GoldenFeathers).ToString("N0");
        }

        private void OnContinue()
        {
            // Resumes the existing run seed if one is saved.
            SaveSystem save = SaveSystem.Instance;
            if (save != null && save.HasProfile && save.Profile.currentRunSeed != 0)
            {
                AudioManager.Instance?.PlaySfx("sfx_ui_click");
                GameManager.Instance.ChangeState(GameState.SectorMap);
            }
            else
            {
                OnNewRun();
            }
        }

        private void OnNewRun()
        {
            AudioManager.Instance?.PlaySfx("sfx_ui_click");
            GameManager.Instance.StartNewRun();
        }

        private void OnSettings()
        {
            AudioManager.Instance?.PlaySfx("sfx_ui_back");
            if (settingsPanel != null) settingsPanel.SetActive(true);
        }

        private void OnQuit()
        {
            AudioManager.Instance?.PlaySfx("sfx_ui_back");
            SaveSystem.Instance?.Flush();
            Application.Quit();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
