using System;
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

        [Header("Settings widgets (built by the project builder)")]
        [SerializeField] public Slider masterSlider;
        [SerializeField] public Slider sfxSlider;
        [SerializeField] public Slider musicSlider;
        [SerializeField] public Button autofireButton;
        [SerializeField] public Button dragButton;
        [SerializeField] public Button qualityButton;
        [SerializeField] public Button closeSettingsButton;

        private bool widgetsReady;

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
            if (closeSettingsButton != null) closeSettingsButton.onClick.AddListener(CloseSettings);

            wireSettingsControls();
            SyncSettingsWidgets();
            widgetsReady = true;
        }

        private static GameSettings SettingsOrNull()
        {
            SaveSystem save = SaveSystem.Instance;
            return save != null && save.Profile != null ? save.Profile.settings : null;
        }

        private void PushAudio()
        {
            GameSettings settings = SettingsOrNull();
            if (settings == null) return;
            AudioManager.Instance?.ApplySettings(settings);
            SaveSystem.Instance.MarkDirty();
        }

        private void wireSettingsControls()
        {
            if (masterSlider != null)
            {
                masterSlider.onValueChanged.AddListener(v =>
                {
                    GameSettings s = SettingsOrNull();
                    if (s == null) return;
                    s.masterVolume = v;
                    PushAudio();
                });
            }
            if (sfxSlider != null)
            {
                sfxSlider.onValueChanged.AddListener(v =>
                {
                    GameSettings s = SettingsOrNull();
                    if (s == null) return;
                    s.sfxVolume = v;
                    PushAudio();
                });
            }
            if (musicSlider != null)
            {
                musicSlider.onValueChanged.AddListener(v =>
                {
                    GameSettings s = SettingsOrNull();
                    if (s == null) return;
                    s.musicVolume = v;
                    PushAudio();
                });
            }
            if (autofireButton != null) autofireButton.onClick.AddListener(ToggleAutofire);
            if (dragButton != null) dragButton.onClick.AddListener(ToggleDragMode);
            if (qualityButton != null) qualityButton.onClick.AddListener(CycleQuality);
        }

        private void SyncSettingsWidgets()
        {
            SaveSystem save = SaveSystem.Instance;
            if (save == null || save.Profile == null) return;

            GameSettings settings = save.Profile.settings;
            if (settings == null) return;

            if (masterSlider != null) SetSliderNoNotify(masterSlider, settings.masterVolume);
            if (sfxSlider != null) SetSliderNoNotify(sfxSlider, settings.sfxVolume);
            if (musicSlider != null) SetSliderNoNotify(musicSlider, settings.musicVolume);

            SetButtonLabel(autofireButton, "AUTOFIRE", settings.autofire);
            SetButtonLabel(dragButton, "DRAG", !settings.absoluteDragInput ? false : true);
            if (qualityButton != null)
            {
                string label = QualitySettings.names.Length > 0
                    ? QualitySettings.names[Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, QualitySettings.names.Length - 1)]
                    : "MEDIUM";
                qualityButton.GetComponentInChildren<Text>().text = $"QUALITY: {label}";
            }
        }

        private static void SetSliderNoNotify(Slider slider, float value)
        {
            slider.SetValueWithoutNotify(Mathf.Clamp01(value));
        }

        private static void SetButtonLabel(Button button, string name, bool on)
        {
            if (button == null) return;
            Text label = button.GetComponentInChildren<Text>();
            if (label != null)
            {
                label.text = $"{name}: {(on ? "ON" : "OFF")}";
                Color c = button.targetGraphic != null ? button.targetGraphic.color : Color.white;
                c.a = on ? 1f : 0.55f;
                if (button.targetGraphic != null) button.targetGraphic.color = c;
            }
        }

        private void ToggleAutofire()
        {
            AudioManager.Instance?.PlaySfx("sfx_ui_click");
            SaveSystem save = SaveSystem.Instance;
            if (save == null || save.Profile == null) return;
            save.Profile.settings.autofire = !save.Profile.settings.autofire;
            save.MarkDirty();
            SetButtonLabel(autofireButton, "AUTOFIRE", save.Profile.settings.autofire);
        }

        private void ToggleDragMode()
        {
            AudioManager.Instance?.PlaySfx("sfx_ui_click");
            SaveSystem save = SaveSystem.Instance;
            if (save == null || save.Profile == null) return;
            save.Profile.settings.absoluteDragInput = !save.Profile.settings.absoluteDragInput;
            save.MarkDirty();
            InputController.Instance?.SetInputScheme(save.Profile.settings.absoluteDragInput);
            SetButtonLabel(dragButton, "DRAG", save.Profile.settings.absoluteDragInput);
        }

        private void CycleQuality()
        {
            AudioManager.Instance?.PlaySfx("sfx_ui_click");
            int next = (QualitySettings.GetQualityLevel() + 1) % Mathf.Max(1, QualitySettings.names.Length);
            QualitySettings.SetQualityLevel(next, true);
            SyncSettingsWidgets();
            SaveSystem save = SaveSystem.Instance;
            if (save != null && save.Profile != null)
            {
                save.Profile.settings.qualityTier = next;
                save.MarkDirty();
            }
        }

        private void CloseSettings()
        {
            AudioManager.Instance?.PlaySfx("sfx_ui_back");
            if (settingsPanel != null) settingsPanel.SetActive(false);
            SaveSystem.Instance?.Flush();
        }

        private void OnSettings()
        {
            AudioManager.Instance?.PlaySfx("sfx_ui_back");
            if (settingsPanel != null)
            {
                settingsPanel.SetActive(true);
                SyncSettingsWidgets();
            }
        }

        private void HandleStateChanged(GameState state)
        {
            if (rootPanel != null) rootPanel.SetActive(state == GameState.MainMenu);
            if (settingsPanel != null) settingsPanel.SetActive(false);

            if (state == GameState.MainMenu && widgetsReady)
            {
                SyncSettingsWidgets();
            }
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
