using System.Collections.Generic;
using Godot;
using Color = Godot.Color;
using TaiwuGu.Core;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Character Ledger & Cultivator Inspection Modal View (Godot 2D).
    /// Pure Presentation Layer Observer under Constitutional Laws 1, 4 & 6.
    /// Strictly decoupled from Domain Services & GameStateContext.
    /// Chrome and the tab strip come from TabbedModal.tscn; each page is a Control bound to an
    /// immutable snapshot (CultivationTab, GuWormsTab, DaoMarksTab, FactionsTab). This view owns
    /// only the cached event state, open/close and the cauldron round-trip.
    /// </summary>
    public partial class TaiwuLedgerView : Control, IModalView
    {
        private const string LedgerTitle = "CULTIVATOR APERTURE LEDGER (空窍录)";

        private bool _isOpen = false;
        private TaiwuModalStack? _modalStack;
        public bool IsOpen => _isOpen;

        public void InitializeModalStack(TaiwuModalStack modalStack)
        {
            _modalStack = modalStack;
            _scaffold?.BindModalStack(modalStack);
        }

        public void Dismiss() => CloseLedger();

        private int _activeTabIndex = 0; // 0: Cultivation, 1: Gu Worms, 2: Dao Marks, 3: Factions
        private bool _returnToLedgerOnCauldronClose = false;

        // --- Cached event state (never seeded; the four pages render from these) ---
        private string _cultivatorName = string.Empty;
        private int _rank;
        private int _substage;
        private string _substageName = string.Empty;
        private float _currentEssenceSea;
        private float _maxEssenceCapacity;
        private int _primevalStones;
        private float _aptitudePercent;

        // Aperture Chamber & Wall fields
        private float _wallWashingProgress;
        private float _apertureWallDurability;
        private float _maxWallDurability;
        private string _membraneName = string.Empty;
        private int _humanPathDaoMarks;

        // Inviolate Lifespan Law & 24 Solar Terms Resonance
        private int _age;
        private int _lifespanYears;
        private int _remainingLifespanYears;
        private string _seasonalAffinityText = string.Empty;

        private readonly List<GuWormData> _equippedGu = new();
        private readonly List<GuWormData> _reserveGu = new();

        // UI references (chrome, tab strip and tab body live in Scenes/UI/TabbedModal.tscn)
        private TabbedModal? _tabs;
        private ModalScaffold? _scaffold;
        private Label? _lblSubtitle;

        private CultivationTab? _cultivationTab;
        private GuWormsTab? _guWormsTab;
        private DaoMarksTab? _daoMarksTab;
        private FactionsTab? _factionsTab;

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;
            Visible = false;

            BuildUIHierarchy();

            // Strict Event Subscription Symmetry
            GameEventManager.OnLedgerToggleRequested        += HandleLedgerToggleRequested;
            GameEventManager.OnPOIModalToggled              += HandlePOIModalToggled;
            GameEventManager.OnCauldronOpened               += HandleCauldronOpened;
            GameEventManager.OnCauldronClosed               += HandleCauldronClosed;
            GameEventManager.OnCultivatorIdentityChanged    += HandleCultivatorIdentityChanged;
            GameEventManager.OnCultivatorCultivationChanged += HandleCultivatorCultivationChanged;
            GameEventManager.OnPrimevalSeaChanged           += HandlePrimevalSeaChanged;
            GameEventManager.OnPrimevalStonesChanged        += HandlePrimevalStonesChanged;
            GameEventManager.OnApertureWallStateChanged     += HandleApertureWallStateChanged;
            GameEventManager.OnHumanPathDaoMarksChanged     += HandleHumanPathDaoMarksChanged;
            GameEventManager.OnLifespanChanged             += HandleLifespanChanged;
            GameEventManager.OnSolarTermAdvanced            += HandleSolarTermAdvanced;
            GameEventManager.OnGuInventoryUpdated           += HandleGuInventoryUpdated;
            GameEventManager.OnGuFed                        += HandleGuFed;
            GameEventManager.OnGuStarvationChanged          += HandleGuStarvationChanged;
            GameEventManager.OnFactionReputationChanged     += HandleFactionReputationChanged;
        }

        public override void _ExitTree()
        {
            GameEventManager.OnLedgerToggleRequested        -= HandleLedgerToggleRequested;
            GameEventManager.OnPOIModalToggled              -= HandlePOIModalToggled;
            GameEventManager.OnCauldronOpened               -= HandleCauldronOpened;
            GameEventManager.OnCauldronClosed               -= HandleCauldronClosed;
            GameEventManager.OnCultivatorIdentityChanged    -= HandleCultivatorIdentityChanged;
            GameEventManager.OnCultivatorCultivationChanged -= HandleCultivatorCultivationChanged;
            GameEventManager.OnPrimevalSeaChanged           -= HandlePrimevalSeaChanged;
            GameEventManager.OnPrimevalStonesChanged        -= HandlePrimevalStonesChanged;
            GameEventManager.OnApertureWallStateChanged     -= HandleApertureWallStateChanged;
            GameEventManager.OnHumanPathDaoMarksChanged     -= HandleHumanPathDaoMarksChanged;
            GameEventManager.OnLifespanChanged             -= HandleLifespanChanged;
            GameEventManager.OnSolarTermAdvanced            -= HandleSolarTermAdvanced;
            GameEventManager.OnGuInventoryUpdated           -= HandleGuInventoryUpdated;
            GameEventManager.OnGuFed                        -= HandleGuFed;
            GameEventManager.OnGuStarvationChanged          -= HandleGuStarvationChanged;
            GameEventManager.OnFactionReputationChanged     -= HandleFactionReputationChanged;

            if (_isOpen)
            {
                _isOpen = false;
                _scaffold?.Close();
                GameEventManager.TriggerLedgerToggled(false);
            }
        }

        public void ToggleLedger() => HandleLedgerToggleRequested();

        public void OpenLedger()
        {
            if (_isOpen) return;

            _isOpen = true;
            Visible = true;
            _scaffold?.Open(this, LedgerTitle);
            GameEventManager.TriggerLedgerToggled(true);
            UpdateAllDisplays();
        }

        public void CloseLedger()
        {
            if (!_isOpen) return;

            _isOpen = false;
            Visible = false;
            _scaffold?.Close();
            GameEventManager.TriggerLedgerToggled(false);
        }

        private void HandleLedgerToggleRequested()
        {
            if (_isOpen) CloseLedger();
            else OpenLedger();
        }

        private void HandlePOIModalToggled(bool isPOIModalOpen)
        {
            if (isPOIModalOpen && _isOpen)
            {
                CloseLedger();
            }
        }

        private void HandleCauldronOpened(CauldronRefinementState state)
        {
            if (_isOpen)
            {
                _returnToLedgerOnCauldronClose = true;
                _isOpen = false;
                Visible = false;
                _scaffold?.Close();
                GameEventManager.TriggerLedgerToggled(false);
            }
        }

        private void HandleCauldronClosed()
        {
            if (_returnToLedgerOnCauldronClose)
            {
                _returnToLedgerOnCauldronClose = false;
                _isOpen = true;
                Visible = true;
                _activeTabIndex = 1; // Return to Gu Worms loadout
                _scaffold?.Open(this, LedgerTitle);
                GameEventManager.TriggerLedgerToggled(true);
                SwitchTab(1);
                UpdateAllDisplays();
            }
        }

        private void OpenCauldronFromLedger()
        {
            _returnToLedgerOnCauldronClose = true;
            _isOpen = false;
            Visible = false;
            _scaffold?.Close();
            GameEventManager.TriggerLedgerToggled(false);
            GameEventManager.RaiseRequestOpenCauldron();
        }

        private void HandleBreakthroughRequested()
        {
            CloseLedger();
            GameEventManager.RaiseRequestOpenBreakthroughMinigame();
        }

        private void HandleCultivatorIdentityChanged(string cultivatorName)
        {
            _cultivatorName = cultivatorName;
            RefreshCultivation();
        }

        private void HandleCultivatorCultivationChanged(int rank, int substage, string name)
        {
            _rank = rank;
            _substage = substage;
            _substageName = name;
            RefreshCultivation();
        }

        private void HandlePrimevalSeaChanged(float currentSea, float maxCapacity)
        {
            _currentEssenceSea = currentSea;
            // The service broadcasts the aperture's ceiling, which is the innate aptitude: it is
            // the only event that carries that number, and no view may seed it.
            _maxEssenceCapacity = maxCapacity;
            _aptitudePercent = maxCapacity;
            RefreshCultivation();
        }

        private void HandlePrimevalStonesChanged(int stones)
        {
            _primevalStones = stones;
            RefreshCultivation();
        }

        private void HandleApertureWallStateChanged(float washingProgress, float durability, float maxDurability, string membraneName)
        {
            _wallWashingProgress = washingProgress;
            _apertureWallDurability = durability;
            _maxWallDurability = maxDurability;
            _membraneName = membraneName;
            RefreshCultivation();
        }

        private void HandleHumanPathDaoMarksChanged(int marks)
        {
            _humanPathDaoMarks = marks;
            RefreshCultivation();
            RefreshDaoMarks();
        }

        private void HandleLifespanChanged(int age, int lifespanYears, int remainingYears)
        {
            _age = age;
            _lifespanYears = lifespanYears;
            _remainingLifespanYears = remainingYears;
            RefreshCultivation();
        }

        private void HandleSolarTermAdvanced(int year, SolarTerm term, string termName)
        {
            int index = (int)term;
            if (index >= 1 && index <= 6) _seasonalAffinityText = "Spring: Enslavement Path +15%";
            else if (index >= 7 && index <= 12) _seasonalAffinityText = "Summer: Refinement Path +15%";
            else if (index >= 13 && index <= 18) _seasonalAffinityText = "Autumn: Moon Path +15%";
            else _seasonalAffinityText = "Winter: Blood Path +15%";
            RefreshCultivation();
            RefreshDaoMarks();
        }

        private void HandleGuInventoryUpdated(List<GuWormData> equipped, List<GuWormData> reserve)
        {
            _equippedGu.Clear();
            if (equipped != null) _equippedGu.AddRange(equipped);

            _reserveGu.Clear();
            if (reserve != null) _reserveGu.AddRange(reserve);

            RefreshGuWorms();
            UpdateTabHeaders();
        }

        private void HandleGuFed(string guId, bool wasFed, float newSatiety)
        {
            for (int i = 0; i < _equippedGu.Count; i++)
            {
                if (_equippedGu[i].GuID == guId)
                {
                    _equippedGu[i].Satiety = newSatiety;
                    RefreshGuWorms();
                    return;
                }
            }
            for (int i = 0; i < _reserveGu.Count; i++)
            {
                if (_reserveGu[i].GuID == guId)
                {
                    _reserveGu[i].Satiety = newSatiety;
                    RefreshGuWorms();
                    return;
                }
            }
        }

        private void HandleGuStarvationChanged(string guId, GuStarvationStage st)
        {
            RefreshGuWorms();
        }

        private void HandleFactionReputationChanged(FactionID faction, int newScore, int delta)
        {
            RefreshFactions();
        }

        // =========================================================================
        // UI HIERARCHY CONSTRUCTION
        // =========================================================================

        private void BuildUIHierarchy()
        {
            // Chrome comes from ModalScaffold.tscn inside TabbedModal.tscn, which also declares
            // the tab strip and tab body; this view only fills the subtitle area and pages.
            var tabbedScene = GD.Load<PackedScene>("res://Scenes/UI/TabbedModal.tscn");
            if (tabbedScene == null)
            {
                GD.PushError("[TaiwuLedgerView] Missing tabbed modal scene at res://Scenes/UI/TabbedModal.tscn.");
                return;
            }

            var tabs = tabbedScene.Instantiate<TabbedModal>();
            _tabs = tabs;
            AddChild(tabs);

            _scaffold = tabs.Scaffold;
            if (_scaffold == null)
            {
                GD.PushError("[TaiwuLedgerView] TabbedModal.tscn is missing its ModalScaffold child.");
                return;
            }

            _scaffold.BindModalStack(_modalStack);
            _scaffold.SetTitle(LedgerTitle);

            // 840x590 ledger window, with the original 16px title and its [Esc] hint
            if (_scaffold.Panel != null)
            {
                _scaffold.Panel.CustomMinimumSize = new Vector2(840f, 590f);
            }
            if (_scaffold.TitleLabel != null)
            {
                _scaffold.TitleLabel.AddThemeFontSizeOverride("font_size", 16);
            }
            if (_scaffold.CloseButton != null)
            {
                _scaffold.CloseButton.Text = "✕ [Esc]";
                _scaffold.CloseButton.CustomMinimumSize = new Vector2(75f, 26f);
            }

            var headerRows = tabs.HeaderRows;
            if (headerRows == null)
            {
                GD.PushError("[TaiwuLedgerView] TabbedModal.tscn is missing its HeaderRows node.");
                return;
            }
            if (_scaffold.ContentRoot != null)
            {
                _scaffold.ContentRoot.AddThemeConstantOverride("separation", 8);
            }

            _lblSubtitle = new Label
            {
                Text = $"Gu Yue Clan Heir: {_cultivatorName} | Rank {_rank} {_substageName}",
                Modulate = new Color(0.984f, 0.749f, 0.141f, 1f) // #FBBF24
            };
            _lblSubtitle.AddThemeFontSizeOverride("font_size", 11);
            headerRows.AddChild(_lblSubtitle);

            // 4-Tab Navigation Bar: the strip itself is declared in TabbedModal.tscn
            float tabWidth = (840f - 36f - 18f) / 4f;

            _cultivationTab = new CultivationTab();
            _cultivationTab.BreakthroughRequested += HandleBreakthroughRequested;
            tabs.AddTab("1. Cultivation (修持)", _cultivationTab, tabWidth);

            _guWormsTab = new GuWormsTab();
            _guWormsTab.OpenCauldronRequested += OpenCauldronFromLedger;
            tabs.AddTab("2. Gu Worms (0)", _guWormsTab, tabWidth);

            _daoMarksTab = new DaoMarksTab();
            tabs.AddTab("3. Dao Marks (道痕)", _daoMarksTab, tabWidth);

            _factionsTab = new FactionsTab();
            tabs.AddTab("4. Factions (宗族恩仇)", _factionsTab, tabWidth);

            // Tab body keeps the ledger's inset lacquer panel
            var body = tabs.Body;
            if (body != null)
            {
                var bodyStyle = new StyleBoxFlat
                {
                    BgColor = new Color(0.047f, 0.063f, 0.055f, 0.95f),
                    BorderColor = new Color(0.133f, 0.200f, 0.188f, 1f),
                    BorderWidthBottom = 1,
                    BorderWidthTop = 1,
                    BorderWidthLeft = 1,
                    BorderWidthRight = 1
                };
                body.AddThemeStyleboxOverride("panel", bodyStyle);
            }

            tabs.TabSelected += HandleStripTabSelected;

            SwitchTab(0);
            UpdateAllDisplays();
        }

        // =========================================================================
        // SNAPSHOT BINDING
        // =========================================================================

        public void UpdateAllDisplays()
        {
            RefreshCultivation();
            RefreshGuWorms();
            RefreshDaoMarks();
            RefreshFactions();
            UpdateTabHeaders();
        }

        private void RefreshCultivation()
        {
            _scaffold?.SetTitle(LedgerTitle);

            if (_lblSubtitle != null)
                _lblSubtitle.Text = $"Gu Yue Clan Heir: {_cultivatorName} | Rank {_rank} {_substageName}";

            _cultivationTab?.Bind(new CultivationSnapshot(
                _cultivatorName,
                _rank,
                _substage,
                _substageName,
                _membraneName,
                _aptitudePercent,
                _primevalStones,
                _humanPathDaoMarks,
                _age,
                _lifespanYears,
                _remainingLifespanYears,
                _seasonalAffinityText,
                _currentEssenceSea,
                _maxEssenceCapacity,
                _wallWashingProgress,
                _apertureWallDurability,
                _maxWallDurability));
        }

        private void RefreshGuWorms()
        {
            _guWormsTab?.Bind(new GuWormsSnapshot(_equippedGu, _reserveGu));
        }

        private void RefreshDaoMarks()
        {
            _daoMarksTab?.Bind(new DaoMarksSnapshot(_humanPathDaoMarks, _aptitudePercent));
        }

        private void RefreshFactions()
        {
            var factionState = FactionContextProvider.FactionResolver?.Invoke();
            if (factionState == null || factionState.Factions == null || factionState.Factions.Count == 0)
            {
                factionState = new WorldFactionState();
                factionState.InitializeDefaultFactions();
            }

            _factionsTab?.Bind(new FactionsSnapshot(factionState.Factions));
        }

        // =========================================================================
        // TAB CONTROL
        // =========================================================================

        /// <summary>
        /// A strip click selects the page inside TabbedModal; this keeps the view's own tab
        /// state and its per-tab refresh running exactly as the old hand-built buttons did.
        /// </summary>
        private void HandleStripTabSelected(int tabIndex)
        {
            if (_activeTabIndex == tabIndex) return;
            SwitchTab(tabIndex);
        }

        private void SwitchTab(int tabIndex)
        {
            _activeTabIndex = tabIndex;
            _tabs?.SelectTab(tabIndex);

            if (_activeTabIndex == 1) RefreshGuWorms();
            if (_activeTabIndex == 3) RefreshFactions();
        }

        private void UpdateTabHeaders()
        {
            int totalGu = _equippedGu.Count + _reserveGu.Count;
            _tabs?.SetTabLabel(1, $"2. Gu Worms ({totalGu})");
        }
    }
}
