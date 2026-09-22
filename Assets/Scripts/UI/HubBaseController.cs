using UnityEngine;
using UnityEngine.UI;

namespace StarbeakGalacticRebellion
{
    /// <summary>
    /// Hub base: crafting bench, blueprint fabricator, loadout, and the transient Black
    /// Market overlay reached from BlackMarket sector nodes.
    /// </summary>
    public class HubBaseController : MonoBehaviour
    {
        public static HubBaseController Instance { get; private set; }

        [Header("Panels")]
        [SerializeField] private GameObject rootPanel;
        [SerializeField] private GameObject craftingPanel;
        [SerializeField] private GameObject blueprintsPanel;
        [SerializeField] private GameObject marketPanel;

        [Header("Navigation")]
        [SerializeField] private Button craftButton;
        [SerializeField] private Button blueprintsButton;
        [SerializeField] private Button departButton;
        [SerializeField] private Button marketBuyScrapButton;
        [SerializeField] private Button marketBuyYolkButton;
        [SerializeField] private Text marketFundsText;

        [Header("Crafting slots")]
        [SerializeField] private Transform muzzleSlotContainer;
        [SerializeField] private Transform magazineSlotContainer;
        [SerializeField] private Transform coreSlotContainer;
        [SerializeField] private Button modEntryPrefab;

        [Header("Loadout readout")]
        [SerializeField] private Text loadoutText;
        [SerializeField] private Text scrapText;
        [SerializeField] private Text yolkText;
        [SerializeField] private Text featherText;

        private int transientMarketNodeId = -1;

        public bool IsTransientMarketOpen => transientMarketNodeId != -1;

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
            if (craftButton != null) craftButton.onClick.AddListener(() => TogglePanel(craftingPanel));
            if (blueprintsButton != null) blueprintsButton.onClick.AddListener(() => TogglePanel(blueprintsPanel));
            if (departButton != null) departButton.onClick.AddListener(OnDepart);
            if (marketBuyScrapButton != null) marketBuyScrapButton.onClick.AddListener(() => BuyResource(ResourceType.ScrapIron, 40, 1));
            if (marketBuyYolkButton != null) marketBuyYolkButton.onClick.AddListener(() => BuyResource(ResourceType.YolkCores, 1, 25));

            PopulateCraftingSlots();
        }

        private void HandleStateChanged(GameState state)
        {
            bool hub = state == GameState.HubBase;
            if (rootPanel != null) rootPanel.SetActive(hub);
            if (hub)
            {
                RefreshAll();
            }
            else
            {
                transientMarketNodeId = -1;
                if (marketPanel != null) marketPanel.SetActive(false);
            }
        }

        public void Show()
        {
            if (rootPanel != null) rootPanel.SetActive(true);
            RefreshAll();
        }

        /// <summary>Opens the in-map Black Market overlay without leaving the sector map.</summary>
        public void ShowTransientMarket(int nodeId)
        {
            transientMarketNodeId = nodeId;
            if (marketPanel != null) marketPanel.SetActive(true);
            RefreshMarketFunds();
        }

        private void OnDepart()
        {
            // Close any transient market then return to navigation.
            if (transientMarketNodeId != -1)
            {
                SectorMapGenerator.Instance?.MarkNodeCompleted(transientMarketNodeId);
                transientMarketNodeId = -1;
                if (marketPanel != null) marketPanel.SetActive(false);
                GameManager.Instance.ChangeState(GameState.SectorMap);
                return;
            }
            GameManager.Instance.StartNewRun();
        }

        private void TogglePanel(GameObject panel)
        {
            if (panel == null) return;
            panel.SetActive(!panel.activeSelf);
        }

        private void BuyResource(ResourceType type, int amount, int scrapCost)
        {
            if (!GameManager.Instance.TrySpendResource(ResourceType.ScrapIron, scrapCost)) return;
            GameManager.Instance.AddResource(type, amount);
            RefreshAll();
        }

        // ---------------------------------------------------------------- Crafting bench
        private void PopulateCraftingSlots()
        {
            WeaponCraftingEngine engine = WeaponCraftingEngine.Instance;
            if (engine == null || modEntryPrefab == null) return;

            foreach (var pair in engine.Mods)
            {
                WeaponMod mod = pair.Value;
                Transform container;
                switch (mod.slot)
                {
                    case ModSlot.Muzzle:   container = muzzleSlotContainer; break;
                    case ModSlot.Magazine: container = magazineSlotContainer; break;
                    default:               container = coreSlotContainer; break;
                }
                if (container == null) continue;

                Button entry = Instantiate(modEntryPrefab, container);
                entry.GetComponentInChildren<Text>().text = $"{mod.displayName}\n{mod.scrapCost} Scrap";
                entry.onClick.AddListener(() => OnModSelected(mod));
            }
        }

        private void OnModSelected(WeaponMod mod)
        {
            if (!mod.IsUnlocked)
            {
                if (!WeaponCraftingEngine.Instance.TryPurchaseMod(mod.id)) return;
            }
            WeaponCraftingEngine.Instance.EquipMod(mod.id, PlayerShip.Instance);
            RefreshAll();
        }

        private void RefreshAll()
        {
            RefreshResources();
            RefreshLoadout();
            RefreshMarketFunds();
        }

        private void RefreshResources()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return;

            if (scrapText != null) scrapText.text = gm.GetResource(ResourceType.ScrapIron).ToString("N0");
            if (yolkText != null) yolkText.text = gm.GetResource(ResourceType.YolkCores).ToString("N0");
            if (featherText != null) featherText.text = gm.GetResource(ResourceType.GoldenFeathers).ToString("N0");
        }

        private void RefreshLoadout()
        {
            if (loadoutText == null || PlayerShip.Instance == null || PlayerShip.Instance.EquippedWeapon == null) return;

            WeaponInstance weapon = PlayerShip.Instance.EquippedWeapon;
            loadoutText.text =
                $"Damage {weapon.FinalDamage:0.0}\n" +
                $"Cooldown {weapon.FinalCooldown:0.00}s\n" +
                $"Spread {weapon.FinalSpreadAngle:0.0} deg\n" +
                $"Muzzle: {weapon.GetSlotId(ModSlot.Muzzle)}\n" +
                $"Magazine: {weapon.GetSlotId(ModSlot.Magazine)}\n" +
                $"Core: {weapon.GetSlotId(ModSlot.Core)}";
        }

        private void RefreshMarketFunds()
        {
            if (marketFundsText == null) return;
            GameManager gm = GameManager.Instance;
            marketFundsText.text = gm != null
                ? $"Scrap: {gm.GetResource(ResourceType.ScrapIron):N0}"
                : string.Empty;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
