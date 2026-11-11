using System.Collections.Generic;
using Godot;
using Color = Godot.Color;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Systems;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Character Ledger & Cultivator Inspection Modal View (Godot 2D).
    /// Direct-read presentation observer wired to WorldStore via IWorldStateProvider & WorldSignals.
    /// Chrome and the tab strip come from TabbedModal.tscn; each page is a Control bound to an
    /// immutable snapshot (CultivationTab, GuWormsTab, DaoMarksTab, FactionsTab).
    /// </summary>
    public partial class TaiwuLedgerView : Control, IModalView
    {
        private const string LedgerTitle = "CULTIVATOR APERTURE LEDGER (空窍录)";

        private bool _isOpen = false;
        private TaiwuModalStack? _modalStack;
        private IWorldStateProvider? _stateProvider;
        public bool IsOpen => _isOpen;

        public void Initialize(TaiwuModalStack modalStack, IWorldStateProvider stateProvider)
        {
            _modalStack = modalStack;
            _stateProvider = stateProvider;
            _scaffold?.BindModalStack(modalStack);
            UpdateAllDisplays();
        }

        public void Dismiss() => CloseLedger();

        private int _activeTabIndex = 0; // 0: Cultivation, 1: Gu Worms, 2: Dao Marks, 3: Factions
        private bool _returnToLedgerOnCauldronClose = false;

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

            WorldSignals.OnTopicsChanged        += HandleTopicsChanged;
            WorldSignals.OnToggleLedgerRequested += HandleLedgerToggleRequested;
            WorldSignals.OnPOIModalToggled      += HandlePOIModalToggled;
            WorldSignals.OnCauldronModalToggled += HandleCauldronModalToggled;

            UpdateAllDisplays();
        }

        public override void _ExitTree()
        {
            WorldSignals.OnTopicsChanged        -= HandleTopicsChanged;
            WorldSignals.OnToggleLedgerRequested -= HandleLedgerToggleRequested;
            WorldSignals.OnPOIModalToggled      -= HandlePOIModalToggled;
            WorldSignals.OnCauldronModalToggled -= HandleCauldronModalToggled;

            if (_cultivationTab != null)
            {
                _cultivationTab.MeditateRequested -= HandleMeditateRequested;
                _cultivationTab.ConsumeStoneRequested -= HandleConsumeStoneRequested;
                _cultivationTab.WashWallRequested -= HandleWashWallRequested;
                _cultivationTab.BreakthroughRequested -= HandleBreakthroughRequested;
            }
            if (_guWormsTab != null)
            {
                _guWormsTab.OpenCauldronRequested -= OpenCauldronFromLedger;
                _guWormsTab.FeedGuRequested -= HandleFeedGuRequested;
            }
            if (_tabs != null)
            {
                _tabs.TabSelected -= HandleStripTabSelected;
            }

            if (_isOpen)
            {
                _isOpen = false;
                _scaffold?.Close();
                WorldSignals.EmitLedgerToggled(false);
            }
        }

        public void ToggleLedger() => HandleLedgerToggleRequested();

        public void OpenLedger()
        {
            if (_isOpen) return;

            _isOpen = true;
            Visible = true;
            _scaffold?.Open(this, LedgerTitle);
            WorldSignals.EmitLedgerToggled(true);
            UpdateAllDisplays();
        }

        public void CloseLedger()
        {
            if (!_isOpen) return;

            _isOpen = false;
            Visible = false;
            _scaffold?.Close();
            WorldSignals.EmitLedgerToggled(false);
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

        private void HandleCauldronModalToggled(bool isOpen)
        {
            if (isOpen)
            {
                if (_isOpen)
                {
                    _returnToLedgerOnCauldronClose = true;
                    _isOpen = false;
                    Visible = false;
                    _scaffold?.Close();
                    WorldSignals.EmitLedgerToggled(false);
                }
            }
            else if (_returnToLedgerOnCauldronClose)
            {
                _returnToLedgerOnCauldronClose = false;
                _isOpen = true;
                Visible = true;
                _activeTabIndex = 1; // Return to Gu Worms loadout
                _scaffold?.Open(this, LedgerTitle);
                WorldSignals.EmitLedgerToggled(true);
                SwitchTab(1);
                UpdateAllDisplays();
            }
        }

        private void OpenCauldronFromLedger()
        {
            var world = _stateProvider?.Current;
            if (world == null) return;

            _returnToLedgerOnCauldronClose = true;
            _isOpen = false;
            Visible = false;
            _scaffold?.Close();
            WorldSignals.EmitLedgerToggled(false);
            RefinementSystem.OpenCauldron(world);
        }

        private void HandleMeditateRequested()
        {
            if (_stateProvider?.Current is WorldStore world) CultivationSystem.Meditate(world);
        }

        private void HandleConsumeStoneRequested()
        {
            if (_stateProvider?.Current is WorldStore world) CultivationSystem.ConsumeStone(world);
        }

        private void HandleWashWallRequested()
        {
            if (_stateProvider?.Current is WorldStore world) CultivationSystem.WashApertureWall(world);
        }

        private void HandleFeedGuRequested(string guId, GuDietType diet, int quantity)
        {
            if (_stateProvider?.Current is WorldStore world)
                GuEcologySystem.FeedGuByCatalogId(world, guId, diet, quantity);
        }

        private void HandleBreakthroughRequested()
        {
            CloseLedger();
            if (_stateProvider?.Current is WorldStore world)
                BreakthroughSystem.OpenSession(world);
        }

        private void HandleTopicsChanged(DirtyTopic topics)
        {
            if ((topics & (DirtyTopic.Cultivator | DirtyTopic.Aperture | DirtyTopic.Calendar)) != 0)
            {
                RefreshCultivation();
                RefreshDaoMarks();
            }
            if ((topics & DirtyTopic.Inventory) != 0)
            {
                RefreshGuWorms();
                UpdateTabHeaders();
            }
            if ((topics & DirtyTopic.Factions) != 0)
            {
                RefreshFactions();
            }
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
                Text = string.Empty,
                Modulate = new Color(0.984f, 0.749f, 0.141f, 1f) // #FBBF24
            };
            _lblSubtitle.AddThemeFontSizeOverride("font_size", 11);
            headerRows.AddChild(_lblSubtitle);

            // 4-Tab Navigation Bar: the strip itself is declared in TabbedModal.tscn
            float tabWidth = (840f - 36f - 18f) / 4f;

            _cultivationTab = new CultivationTab();
            _cultivationTab.MeditateRequested += HandleMeditateRequested;
            _cultivationTab.ConsumeStoneRequested += HandleConsumeStoneRequested;
            _cultivationTab.WashWallRequested += HandleWashWallRequested;
            _cultivationTab.BreakthroughRequested += HandleBreakthroughRequested;
            tabs.AddTab("1. Cultivation (修持)", _cultivationTab, tabWidth);

            _guWormsTab = new GuWormsTab();
            _guWormsTab.OpenCauldronRequested += OpenCauldronFromLedger;
            _guWormsTab.FeedGuRequested += HandleFeedGuRequested;
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
            var world = _stateProvider?.Current;
            if (world == null) return;

            var cult = world.Get<CultivatorComponent>(world.PlayerEntity);
            var ap = world.Get<ApertureComponent>(world.PlayerEntity);
            var marks = world.Get<DaoMarksComponent>(world.PlayerEntity);
            var clock = world.GetSingleton<WorldClockComponent>();
            if (cult == null || ap == null) return;

            string substageName = ap.GetSubstageName();
            if (_lblSubtitle != null)
                _lblSubtitle.Text = $"Gu Yue Clan Heir: {cult.Name} | Rank {ap.Rank} {substageName}";

            int humanMarks = marks?.Ledger.HumanPathMarks ?? ApertureComponent.DeriveInnateHumanPathMarks(cult.AptitudePercent);
            int termIdx = clock?.CurrentSolarTermIndex ?? 4;
            string seasonalAffinity = termIdx switch
            {
                >= 1 and <= 6 => "Spring: Enslavement Path +15%",
                >= 7 and <= 12 => "Summer: Refinement Path +15%",
                >= 13 and <= 18 => "Autumn: Moon Path +15%",
                _ => "Winter: Blood Path +15%"
            };

            _cultivationTab?.Bind(new CultivationSnapshot(
                cult.Name,
                ap.Rank,
                ap.Substage,
                substageName,
                ap.GetMembraneTypeName(),
                cult.AptitudePercent,
                cult.PrimevalStones,
                humanMarks,
                cult.Age,
                cult.LifespanYears,
                cult.RemainingLifespanYears,
                seasonalAffinity,
                ap.CurrentEssenceSea,
                cult.AptitudePercent,
                ap.WallWashingProgress,
                ap.ApertureWallDurability,
                ap.MaxWallDurability));
        }

        private void RefreshGuWorms()
        {
            var world = _stateProvider?.Current;
            if (world == null) return;

            var (equipped, reserve) = GuEcologySystem.GetLoadout(world);
            _guWormsTab?.Bind(new GuWormsSnapshot(equipped, reserve));
        }

        private void RefreshDaoMarks()
        {
            var world = _stateProvider?.Current;
            if (world == null) return;

            var cult = world.Get<CultivatorComponent>(world.PlayerEntity);
            var marks = world.Get<DaoMarksComponent>(world.PlayerEntity);
            float aptitude = cult?.AptitudePercent ?? 44f;
            int humanMarks = marks?.Ledger.HumanPathMarks ?? ApertureComponent.DeriveInnateHumanPathMarks(aptitude);

            _daoMarksTab?.Bind(new DaoMarksSnapshot(humanMarks, aptitude));
        }

        private void RefreshFactions()
        {
            var world = _stateProvider?.Current;
            var factions = new List<FactionData>();
            if (world != null)
            {
                foreach (var (_, fc) in world.GetAll<FactionComponent>())
                {
                    factions.Add(fc.ToFactionData());
                }
                factions.Sort((a, b) => a.ID.CompareTo(b.ID));
            }

            _factionsTab?.Bind(new FactionsSnapshot(factions));
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
            var world = _stateProvider?.Current;
            int totalGu = 0;
            if (world != null)
            {
                var (equipped, reserve) = GuEcologySystem.GetLoadout(world);
                totalGu = equipped.Count + reserve.Count;
            }
            _tabs?.SetTabLabel(1, $"2. Gu Worms ({totalGu})");
        }
    }
}
