using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Roguelite upgrade panel hosted on the Sector Map scene. After a combat node is
    /// cleared the game offers three random weapon upgrades; the player taps one and it
    /// is applied to the live weapon immediately.
    /// </summary>
    public class WeaponUpgradeUI : MonoBehaviour
    {
        public static WeaponUpgradeUI Instance { get; private set; }

        [Header("Panel")]
        [SerializeField] public GameObject rootPanel;
        [SerializeField] public Text titleText;

        [Header("Choice buttons (one per offered upgrade)")]
        [SerializeField] public Button[] choiceButtons = new Button[3];

        private IReadOnlyList<WeaponUpgrades.Upgrade> offered;

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
            // Hide the panel whenever we leave the sector map (combat, hub, menu).
            if (state != GameState.SectorMap && rootPanel != null) rootPanel.SetActive(false);
        }

        /// <summary>Shows the panel with a fresh set of upgrades to choose from.</summary>
        public void Offer(IReadOnlyList<WeaponUpgrades.Upgrade> choices)
        {
            if (rootPanel == null) return;

            offered = choices;
            rootPanel.SetActive(true);

            if (titleText != null)
            {
                titleText.text = "SECTOR CLEARED  //  CHOOSE AN UPGRADE";
            }

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                Button button = choiceButtons[i];
                if (button == null) continue;

                bool hasUpgrade = i < choices.Count;
                button.gameObject.SetActive(hasUpgrade);
                if (!hasUpgrade) continue;

                WeaponUpgrades.Upgrade upgrade = choices[i];
                Text label = button.GetComponentInChildren<Text>();
                if (label != null)
                {
                    label.text = $"{upgrade.title}\n<size=28>{upgrade.description}</size>";
                }
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnChoicePicked(upgrade));
            }
        }

        private void OnChoicePicked(WeaponUpgrades.Upgrade upgrade)
        {
            AudioManager.Instance?.PlaySfx("sfx_pickup");

            PlayerShip player = PlayerShip.Instance;
            if (player != null && player.EquippedWeapon != null)
            {
                WeaponUpgrades.Apply(player.EquippedWeapon, upgrade);
            }

            if (rootPanel != null) rootPanel.SetActive(false);
            offered = null;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
