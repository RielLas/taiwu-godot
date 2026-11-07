using System;
using System.Collections.Generic;
using Godot;
using Color = Godot.Color;
using Mathf = Godot.Mathf;
using TaiwuGu.Core;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Phase 3: Character Ledger & Cultivator Inspection Modal View (Godot 2D).
    /// Pure Presentation Layer Observer under Constitutional Laws 1, 4 & 6.
    /// Strictly decoupled from Domain Services & GameStateContext.
    /// 1:1 Pixel-Perfect and Functional Parity with Pristine 1,273-line Unity Reference.
    /// Contains 4 Full Tabs:
    /// - Tab 0: Cultivation & Aperture (Rank, Substage, Lifespan, Aptitude %, Essence Sea %, Wall Washing %, Durability %, Density Multiplier, 4 CQRS Action Buttons, Daoist Footnote)
    /// - Tab 1: Gu Worms Loadout & Reserves (5-Slot Loadout, Vital Gu tag, Halal talisman icons, Satiety gauges with starvation color coding, Feeding actions, Reserve inventory, Alchemical Cauldron jump button)
    /// - Tab 2: Dao Marks & Great Dao Prohibitions (Innate Human Path Card with +% Wash and +% Batter math, Bagua mandala, 7 External Paths Grid with Dao Seals and Rank 6 Ascension Lore)
    /// - Tab 3: Autonomous Institutional Hierarchy (6 Southern Border Factions, dynamic diplomatic standing tiers, trade tariffs, sanctuary privileges, clan banners/seal medallions, [-300..+300] reputation gauges with center zero line)
    /// LIFO Escape Key Priority: #1 in hierarchy.
    /// Halal compliant dark-lacquer Xianxia aesthetic.
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

        // --- Cached Rendering State (Populated exclusively via GameEventManager) ---
        private string _cultivatorName = "Gu Yue Fang Yuan";
        private int _rank = 1;
        private int _substage = 1;
        private string _substageName = "Initial Stage (初阶)";
        private float _currentEssenceSea = 44.0f;
        private float _maxEssenceCapacity = 44.0f;
        private int _primevalStones = 250;
        private float _aptitudePercent = 44.0f;

        // Aperture Chamber & Wall fields
        private float _wallWashingProgress = 0f;
        private float _apertureWallDurability = 100f;
        private float _maxWallDurability = 100f;
        private string _membraneName = "Light Membrane (光膜)";
        private int _humanPathDaoMarks = 4;

        // Inviolate Lifespan Law & 24 Solar Terms Resonance
        private int _age = 15;
        private int _lifespanYears = 100;
        private int _remainingLifespanYears = 85;
        private string _seasonalAffinityText = "Spring: Enslavement Path +15%";

        private readonly List<GuWormData> _equippedGu = new();
        private readonly List<GuWormData> _reserveGu = new();

        // UI references (the chrome itself lives in Scenes/UI/ModalScaffold.tscn)
        private ModalScaffold? _scaffold;
        private Label? _lblSubtitle;

        // Tab buttons
        private Button? _btnTabCultivation;
        private Button? _btnTabGuWorms;
        private Button? _btnTabDaoMarks;
        private Button? _btnTabFactions;

        // Tab panels
        private Control? _panelCultivation;
        private Control? _panelGuWorms;
        private Control? _panelDaoMarks;
        private Control? _panelFactions;

        // Cultivation widgets
        private Label? _lblCultivatorRealm;
        private Label? _lblMembrane;
        private Label? _lblAptitude;
        private Label? _lblStones;
        private Label? _lblHumanDaoMarks;
        private Label? _lblDensity;
        private Label? _lblLifespan;
        private Label? _lblSeasonal;
        private Label? _lblSeaTitle;
        private StyledGaugeControl? _gaugeSea;
        private Label? _lblWallTitle;
        private StyledGaugeControl? _gaugeWall;
        private Button? _btnActionMeditate;
        private Button? _btnActionStone;
        private Button? _btnActionWash;
        private Button? _btnActionBreakthrough;

        // Gu Worms widgets
        private VBoxContainer? _equippedContainer;
        private VBoxContainer? _reserveContainer;

        // Dao marks widgets
        private Label? _lblHumanCardTitle;
        private Label? _lblHumanCardDesc;

        // Factions widgets
        private VBoxContainer? _factionsContainer;

        // Texture Cache
        private Texture2D? _texBannerMountains;
        private Texture2D? _texFrameBarBronze;
        private Texture2D? _texFillEmerald;
        private Texture2D? _texFillCyan;
        private Texture2D? _texFillCrimson;
        private Texture2D? _texSlotIron;
        private Texture2D? _texSlotBronze;
        private Texture2D? _texSlotBrass;
        private Texture2D? _texSlotJade;
        private Texture2D? _texBaguaMandala;
        private Texture2D? _texDaoHuman;
        private Texture2D? _texDaoMoon;
        private Texture2D? _texDaoStrength;
        private Texture2D? _texDaoBone;
        private Texture2D? _texDaoPoison;
        private Texture2D? _texDaoBlood;
        private Texture2D? _texDaoRefine;
        private Texture2D? _texDaoEnslave;
        private Texture2D? _texBannerGuYue;
        private Texture2D? _texBannerBai;
        private Texture2D? _texBannerXiong;
        private Texture2D? _iconMoonShadow;
        private Texture2D? _iconBoarStrength;
        private Texture2D? _iconMoonglow;

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;
            Visible = false;

            LoadTextures();
            BuildUIHierarchy();

            // Strict Event Subscription Symmetry
            GameEventManager.OnLedgerToggleRequested        += HandleLedgerToggleRequested;
            GameEventManager.OnPOIModalToggled              += HandlePOIModalToggled;
            GameEventManager.OnCauldronOpened               += HandleCauldronOpened;
            GameEventManager.OnCauldronClosed               += HandleCauldronClosed;
            GameEventManager.OnCultivatorCultivationChanged += HandleCultivatorCultivationChanged;
            GameEventManager.OnPrimevalSeaChanged           += HandlePrimevalSeaChanged;
            GameEventManager.OnPrimevalStonesChanged         += HandlePrimevalStonesChanged;
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
            GameEventManager.OnCultivatorCultivationChanged -= HandleCultivatorCultivationChanged;
            GameEventManager.OnPrimevalSeaChanged           -= HandlePrimevalSeaChanged;
            GameEventManager.OnPrimevalStonesChanged         -= HandlePrimevalStonesChanged;
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

        private void LoadTextures()
        {
            _texBannerMountains = LoadTextureSafe("res://Resources/UI/banner_ink_mountains.png");
            _texFrameBarBronze  = LoadTextureSafe("res://Resources/UI/frame_bar_bronze.png");
            _texFillEmerald     = LoadTextureSafe("res://Resources/UI/fill_fluid_emerald.png");
            _texFillCyan        = LoadTextureSafe("res://Resources/UI/fill_fluid_cyan.png");
            _texFillCrimson     = LoadTextureSafe("res://Resources/UI/fill_fluid_crimson.png");
            _texSlotIron        = LoadTextureSafe("res://Resources/UI/frame_slot_iron.png");
            _texSlotBronze      = LoadTextureSafe("res://Resources/UI/frame_slot_bronze.png");
            _texSlotBrass       = LoadTextureSafe("res://Resources/UI/frame_slot_brass.png");
            _texSlotJade        = LoadTextureSafe("res://Resources/UI/frame_slot_jade.png");
            _texBaguaMandala    = LoadTextureSafe("res://Resources/Tokens/dao_bagua_mandala.png");

            _texDaoHuman        = LoadTextureSafe("res://Resources/DaoPaths/dao_human_seal.png");
            _texDaoMoon         = LoadTextureSafe("res://Resources/DaoPaths/dao_moon_seal.png");
            _texDaoStrength     = LoadTextureSafe("res://Resources/DaoPaths/dao_strength_seal.png");
            _texDaoBone         = LoadTextureSafe("res://Resources/DaoPaths/dao_bone_seal.png");
            _texDaoPoison       = LoadTextureSafe("res://Resources/DaoPaths/dao_poison_seal.png");
            _texDaoBlood        = LoadTextureSafe("res://Resources/DaoPaths/dao_blood_seal.png");
            _texDaoRefine       = LoadTextureSafe("res://Resources/DaoPaths/dao_refine_seal.png");
            _texDaoEnslave      = LoadTextureSafe("res://Resources/DaoPaths/dao_enslave_seal.png");

            _texBannerGuYue     = LoadTextureSafe("res://Resources/ClanBanners/banner_guyue_clan.png");
            _texBannerBai       = LoadTextureSafe("res://Resources/ClanBanners/banner_bai_clan.png");
            _texBannerXiong     = LoadTextureSafe("res://Resources/ClanBanners/banner_xiong_clan.png");

            _iconMoonShadow     = LoadTextureSafe("res://Resources/Combat/gu_moon_shadow.png");
            _iconBoarStrength   = LoadTextureSafe("res://Resources/Combat/gu_boar_strength.png");
            _iconMoonglow       = LoadTextureSafe("res://Resources/Combat/gu_moonglow.png");
        }

        private static Texture2D? LoadTextureSafe(string path)
        {
            try
            {
                string globalPath = ProjectSettings.GlobalizePath(path);
                if (System.IO.File.Exists(globalPath))
                {
                    var img = Image.LoadFromFile(globalPath);
                    if (img != null && !img.IsEmpty())
                    {
                        return ImageTexture.CreateFromImage(img);
                    }
                }
            }
            catch { }

            if (ResourceLoader.Exists(path))
            {
                try
                {
                    return GD.Load<Texture2D>(path);
                }
                catch { }
            }
            return null;
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

        private void HandleCultivatorCultivationChanged(int rank, int substage, string name)
        {
            _rank = rank;
            _substage = substage;
            _substageName = name;
            UpdateCultivationDisplay();
        }

        private void HandlePrimevalSeaChanged(float currentSea, float maxCapacity)
        {
            _currentEssenceSea = currentSea;
            _maxEssenceCapacity = maxCapacity;
            UpdateCultivationDisplay();
        }

        private void HandlePrimevalStonesChanged(int stones)
        {
            _primevalStones = stones;
            UpdateCultivationDisplay();
        }

        private void HandleApertureWallStateChanged(float washingProgress, float durability, float maxDurability, string membraneName)
        {
            _wallWashingProgress = washingProgress;
            _apertureWallDurability = durability;
            _maxWallDurability = maxDurability;
            _membraneName = membraneName;
            UpdateCultivationDisplay();
        }

        private void HandleHumanPathDaoMarksChanged(int marks)
        {
            _humanPathDaoMarks = marks;
            UpdateCultivationDisplay();
            UpdateDaoMarksDisplay();
        }

        private void HandleLifespanChanged(int age, int lifespanYears, int remainingYears)
        {
            _age = age;
            _lifespanYears = lifespanYears;
            _remainingLifespanYears = remainingYears;
            UpdateCultivationDisplay();
        }

        private void HandleSolarTermAdvanced(int year, SolarTerm term, string termName)
        {
            int index = (int)term;
            if (index >= 1 && index <= 6) _seasonalAffinityText = "Spring: Enslavement Path +15%";
            else if (index >= 7 && index <= 12) _seasonalAffinityText = "Summer: Refinement Path +15%";
            else if (index >= 13 && index <= 18) _seasonalAffinityText = "Autumn: Moon Path +15%";
            else _seasonalAffinityText = "Winter: Blood Path +15%";
            UpdateCultivationDisplay();
            UpdateDaoMarksDisplay();
        }

        private void HandleGuInventoryUpdated(List<GuWormData> equipped, List<GuWormData> reserve)
        {
            _equippedGu.Clear();
            if (equipped != null) _equippedGu.AddRange(equipped);

            _reserveGu.Clear();
            if (reserve != null) _reserveGu.AddRange(reserve);

            UpdateGuWormsDisplay();
            UpdateTabHeaders();
        }

        private void HandleGuFed(string guId, bool wasFed, float newSatiety)
        {
            for (int i = 0; i < _equippedGu.Count; i++)
            {
                if (_equippedGu[i].GuID == guId)
                {
                    _equippedGu[i].Satiety = newSatiety;
                    UpdateGuWormsDisplay();
                    return;
                }
            }
            for (int i = 0; i < _reserveGu.Count; i++)
            {
                if (_reserveGu[i].GuID == guId)
                {
                    _reserveGu[i].Satiety = newSatiety;
                    UpdateGuWormsDisplay();
                    return;
                }
            }
        }

        private void HandleGuStarvationChanged(string guId, GuStarvationStage st)
        {
            UpdateGuWormsDisplay();
        }

        private void HandleFactionReputationChanged(FactionID faction, int newScore, int delta)
        {
            UpdateFactionsDisplay();
        }

        // =========================================================================
        // UI HIERARCHY CONSTRUCTION (Dark Lacquer Xianxia Masterpiece)
        // =========================================================================

        private void BuildUIHierarchy()
        {
            // Chrome (backdrop, panel, header, close button, Escape, modal registration)
            // is Scenes/UI/ModalScaffold.tscn; this view only fills the body.
            var scaffoldScene = GD.Load<PackedScene>("res://Scenes/UI/ModalScaffold.tscn");
            if (scaffoldScene == null)
            {
                GD.PushError("[TaiwuLedgerView] Missing modal scaffold scene at res://Scenes/UI/ModalScaffold.tscn.");
                return;
            }

            _scaffold = scaffoldScene.Instantiate<ModalScaffold>();
            AddChild(_scaffold);
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

            var rootVBox = _scaffold.ContentRoot;
            if (rootVBox == null) return;
            rootVBox.AddThemeConstantOverride("separation", 8);

            _lblSubtitle = new Label
            {
                Text = $"Gu Yue Clan Heir: {_cultivatorName} | Rank {_rank} {_substageName}",
                Modulate = new Color(0.984f, 0.749f, 0.141f, 1f) // #FBBF24
            };
            _lblSubtitle.AddThemeFontSizeOverride("font_size", 11);
            rootVBox.AddChild(_lblSubtitle);

            // 4-Tab Navigation Bar
            var tabsHBox = new HBoxContainer();
            tabsHBox.AddThemeConstantOverride("separation", 6);
            rootVBox.AddChild(tabsHBox);

            float tabWidth = (840f - 36f - 18f) / 4f;

            _btnTabCultivation = CreateTabButton("1. Cultivation (修持)", tabWidth, () => SwitchTab(0));
            tabsHBox.AddChild(_btnTabCultivation);

            _btnTabGuWorms = CreateTabButton("2. Gu Worms (0)", tabWidth, () => SwitchTab(1));
            tabsHBox.AddChild(_btnTabGuWorms);

            _btnTabDaoMarks = CreateTabButton("3. Dao Marks (道痕)", tabWidth, () => SwitchTab(2));
            tabsHBox.AddChild(_btnTabDaoMarks);

            _btnTabFactions = CreateTabButton("4. Factions (宗族恩仇)", tabWidth, () => SwitchTab(3));
            tabsHBox.AddChild(_btnTabFactions);

            // Content Area Container
            var contentContainer = new PanelContainer
            {
                SizeFlagsVertical = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Stop
            };
            var contentStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.047f, 0.063f, 0.055f, 0.95f),
                BorderColor = new Color(0.133f, 0.200f, 0.188f, 1f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1
            };
            contentContainer.AddThemeStyleboxOverride("panel", contentStyle);
            rootVBox.AddChild(contentContainer);

            // Build the 4 individual tabs
            BuildCultivationTab(contentContainer);
            BuildGuWormsTab(contentContainer);
            BuildDaoMarksTab(contentContainer);
            BuildFactionsTab(contentContainer);

            SwitchTab(0);
            UpdateAllDisplays();
        }

        private Button CreateTabButton(string text, float minWidth, Action onPressed)
        {
            var btn = new Button
            {
                Text = text,
                CustomMinimumSize = new Vector2(minWidth, 32f),
                MouseFilter = MouseFilterEnum.Stop,
                FocusMode = FocusModeEnum.None
            };
            btn.Pressed += onPressed;
            return btn;
        }

        private void SwitchTab(int tabIndex)
        {
            _activeTabIndex = tabIndex;

            if (_panelCultivation != null) _panelCultivation.Visible = (_activeTabIndex == 0);
            if (_panelGuWorms != null)     _panelGuWorms.Visible     = (_activeTabIndex == 1);
            if (_panelDaoMarks != null)    _panelDaoMarks.Visible    = (_activeTabIndex == 2);
            if (_panelFactions != null)    _panelFactions.Visible    = (_activeTabIndex == 3);

            StyleTabButton(_btnTabCultivation, _activeTabIndex == 0);
            StyleTabButton(_btnTabGuWorms,     _activeTabIndex == 1);
            StyleTabButton(_btnTabDaoMarks,    _activeTabIndex == 2);
            StyleTabButton(_btnTabFactions,    _activeTabIndex == 3);

            if (_activeTabIndex == 1) UpdateGuWormsDisplay();
            if (_activeTabIndex == 3) UpdateFactionsDisplay();
        }

        private static void StyleTabButton(Button? btn, bool isActive)
        {
            if (btn == null) return;
            var style = new StyleBoxFlat
            {
                BgColor = isActive ? new Color(0.133f, 0.200f, 0.188f, 1f) : new Color(0.082f, 0.118f, 0.114f, 1f),
                BorderColor = isActive ? new Color(0.961f, 0.620f, 0.043f, 1f) : new Color(0.133f, 0.200f, 0.188f, 0.8f),
                BorderWidthBottom = isActive ? 2 : 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3
            };
            btn.AddThemeStyleboxOverride("normal", style);
            btn.AddThemeStyleboxOverride("hover", style);
            btn.AddThemeStyleboxOverride("pressed", style);
            btn.Modulate = isActive ? new Color(1f, 1f, 1f, 1f) : new Color(0.7f, 0.75f, 0.8f, 0.9f);
        }

        private void UpdateTabHeaders()
        {
            int totalGu = _equippedGu.Count + _reserveGu.Count;
            if (_btnTabGuWorms != null)
            {
                _btnTabGuWorms.Text = $"2. Gu Worms ({totalGu})";
            }
        }

        // =========================================================================
        // TAB 0: CULTIVATION & APERTURE (修持)
        // =========================================================================

        private void BuildCultivationTab(PanelContainer container)
        {
            _panelCultivation = new MarginContainer { Name = "PanelCultivation" };
            var margin = (MarginContainer)_panelCultivation;
            margin.AddThemeConstantOverride("margin_left", 14);
            margin.AddThemeConstantOverride("margin_right", 14);
            margin.AddThemeConstantOverride("margin_top", 12);
            margin.AddThemeConstantOverride("margin_bottom", 12);
            container.AddChild(_panelCultivation);

            var vBox = new VBoxContainer();
            vBox.AddThemeConstantOverride("separation", 10);
            margin.AddChild(vBox);

            // 1. Aperture Chamber Overview & Dao Moon Medallion
            var headerRow = new HBoxContainer();
            vBox.AddChild(headerRow);

            var lblSection = new Label
            {
                Text = "APERTURE CHAMBER & PRIMEVAL SEA (元海窍穴室)",
                Modulate = new Color(0.22f, 0.74f, 0.97f, 1f), // #38BDF8
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            lblSection.AddThemeFontSizeOverride("font_size", 13);
            headerRow.AddChild(lblSection);

            // Jade Frame + Moon Seal Medallion (Right Aligned)
            var moonMedallion = new PanelContainer { CustomMinimumSize = new Vector2(46f, 46f) };
            var moonStyle = new StyleBoxFlat { BgColor = new Color(0.08f, 0.14f, 0.12f, 1f), BorderColor = new Color(0.20f, 0.83f, 0.60f, 1f), BorderWidthBottom = 1, BorderWidthTop = 1, BorderWidthLeft = 1, BorderWidthRight = 1 };
            moonMedallion.AddThemeStyleboxOverride("panel", moonStyle);
            var moonTex = new TextureRect { Texture = _texDaoMoon, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
            moonMedallion.AddChild(moonTex);
            headerRow.AddChild(moonMedallion);

            // 2-Column Cultivation Data Grid
            var grid = new GridContainer { Columns = 2 };
            grid.AddThemeConstantOverride("h_separation", 24);
            grid.AddThemeConstantOverride("v_separation", 4);
            vBox.AddChild(grid);

            // Column 1 (Left)
            _lblCultivatorRealm = CreateStatLabel("Cultivation Realm: Rank 1 Initial Stage (初阶)", new Color(0.133f, 0.773f, 0.369f, 1f));
            grid.AddChild(_lblCultivatorRealm);

            _lblMembrane = CreateStatLabel("Aperture Membrane: Light Membrane (光膜)", new Color(0.961f, 0.620f, 0.043f, 1f));
            grid.AddChild(_lblMembrane);

            _lblAptitude = CreateStatLabel("Innate Aptitude: 44% (Gu Yue Fang Yuan)", new Color(0.984f, 0.749f, 0.141f, 1f));
            grid.AddChild(_lblAptitude);

            _lblStones = CreateStatLabel("Primeval Stones: 250 Stones", new Color(0.984f, 0.749f, 0.141f, 1f));
            grid.AddChild(_lblStones);

            _lblHumanDaoMarks = CreateStatLabel("Human Path Dao Marks: 4 Marks (+8% Wash, +4% Batter)", new Color(0.22f, 0.74f, 0.97f, 1f));
            grid.AddChild(_lblHumanDaoMarks);

            _lblDensity = CreateStatLabel("Density Multiplier: 1x (Qualitative Purity)", new Color(0.961f, 0.620f, 0.043f, 1f));
            grid.AddChild(_lblDensity);

            _lblLifespan = CreateStatLabel("Heavenly Lifespan (寿元): Age 15 / 100 Years (85 yrs left)", new Color(0.204f, 0.827f, 0.6f, 1f));
            grid.AddChild(_lblLifespan);

            _lblSeasonal = CreateStatLabel("Seasonal Resonance: Spring: Enslavement Path +15%", new Color(0.204f, 0.827f, 0.6f, 1f));
            grid.AddChild(_lblSeasonal);

            // 2. Primeval Sea Gauge Bar
            var seaBox = new VBoxContainer();
            seaBox.AddThemeConstantOverride("separation", 2);
            vBox.AddChild(seaBox);

            _lblSeaTitle = new Label { Text = "Green Copper Primeval Sea (青铜元海): 44.0% / 44.0%", Modulate = new Color(0.88f, 0.91f, 0.94f, 1f) };
            _lblSeaTitle.AddThemeFontSizeOverride("font_size", 11);
            seaBox.AddChild(_lblSeaTitle);

            _gaugeSea = new StyledGaugeControl
            {
                CustomMinimumSize = new Vector2(0f, 38f),
                FrameTexture = _texFrameBarBronze,
                FillTexture = _texFillEmerald,
                Value = 1.0f,
                CenterText = "Primeval Sea Fill: 100.0% (44.0% of 44.0% Max Capacity)"
            };
            seaBox.AddChild(_gaugeSea);

            // 3. Aperture Wall / Membrane Condition Bar
            var wallBox = new VBoxContainer();
            wallBox.AddThemeConstantOverride("separation", 2);
            vBox.AddChild(wallBox);

            _lblWallTitle = new Label { Text = "Aperture Wall-Washing Progress: 0.0% / 100%  (Tempering towards Middle Stage (中阶))", Modulate = new Color(0.88f, 0.91f, 0.94f, 1f) };
            _lblWallTitle.AddThemeFontSizeOverride("font_size", 11);
            wallBox.AddChild(_lblWallTitle);

            _gaugeWall = new StyledGaugeControl
            {
                CustomMinimumSize = new Vector2(0f, 38f),
                FrameTexture = _texFrameBarBronze,
                FillTexture = _texFillCyan,
                Value = 0.0f,
                CenterText = "Membrane Tempering: 0.0%"
            };
            wallBox.AddChild(_gaugeWall);

            // 4. Chamber Cultivation Actions (4 Balanced Xianxia Buttons)
            var actionsHeader = new Label
            {
                Text = "CHAMBER CULTIVATION ACTIONS",
                Modulate = new Color(0.22f, 0.74f, 0.97f, 1f)
            };
            actionsHeader.AddThemeFontSizeOverride("font_size", 12);
            vBox.AddChild(actionsHeader);

            var actionsHBox = new HBoxContainer();
            actionsHBox.AddThemeConstantOverride("separation", 8);
            vBox.AddChild(actionsHBox);

            _btnActionMeditate = CreateActionButton("Meditate & Condense\n(-1 AP / Replenish Sea)", () => GameEventManager.RaiseRequestMeditate());
            actionsHBox.AddChild(_btnActionMeditate);

            _btnActionStone = CreateActionButton("Crush Primeval Stone\n(-1 Stone / +10.0% Sea)", () => GameEventManager.RaiseRequestConsumeStone());
            actionsHBox.AddChild(_btnActionStone);

            _btnActionWash = CreateActionButton("Wash Aperture Wall\n(-15% Sea, -1 AP / Substage)", () => GameEventManager.RaiseRequestWashApertureWall());
            actionsHBox.AddChild(_btnActionWash);

            _btnActionBreakthrough = CreateActionButton("Batter Barrier Chamber\n(Breakthrough Seclusion)", () =>
            {
                CloseLedger();
                GameEventManager.RaiseRequestOpenBreakthroughMinigame();
            });
            actionsHBox.AddChild(_btnActionBreakthrough);

            // 5. Canonical Lore & Math Footnote
            var lblQuote = new Label
            {
                Text = "\"Man is the spirit of all living things, Gu are the essence of heaven and earth.\nInnate Human Path Dao marks determine initial aperture talent. Mobilise primeval sea to wash the membrane for substages; summon surging tidal waves to batter and shatter the rigid wall to achieve major rank breakthrough.\"",
                Modulate = new Color(0.58f, 0.64f, 0.72f, 1f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            lblQuote.AddThemeFontSizeOverride("font_size", 10);
            vBox.AddChild(lblQuote);
        }

        private static Label CreateStatLabel(string text, Color color)
        {
            var lbl = new Label { Text = text, Modulate = color };
            lbl.AddThemeFontSizeOverride("font_size", 11);
            return lbl;
        }

        private static Button CreateActionButton(string text, Action onPressed)
        {
            var btn = new Button
            {
                Text = text,
                CustomMinimumSize = new Vector2(185f, 42f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Stop,
                FocusMode = FocusModeEnum.None
            };
            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.122f, 0.180f, 0.169f, 1f),
                BorderColor = new Color(0.169f, 0.247f, 0.231f, 1f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3
            };
            btn.AddThemeStyleboxOverride("normal", style);
            btn.Pressed += onPressed;
            return btn;
        }

        // =========================================================================
        // TAB 1: GU WORMS LOADOUT & FEEDING (蛊虫与本命)
        // =========================================================================

        private void BuildGuWormsTab(PanelContainer container)
        {
            _panelGuWorms = new MarginContainer { Name = "PanelGuWorms" };
            var margin = (MarginContainer)_panelGuWorms;
            margin.AddThemeConstantOverride("margin_left", 14);
            margin.AddThemeConstantOverride("margin_right", 14);
            margin.AddThemeConstantOverride("margin_top", 10);
            margin.AddThemeConstantOverride("margin_bottom", 10);
            container.AddChild(_panelGuWorms);

            var vBox = new VBoxContainer();
            vBox.AddThemeConstantOverride("separation", 8);
            margin.AddChild(vBox);

            // Header Action Bar
            var topHBox = new HBoxContainer();
            vBox.AddChild(topHBox);

            var lblHeader = new Label
            {
                Text = "APERTURE PARASITE ECOLOGY & FEEDING (蛊虫饲养与存亡)",
                Modulate = new Color(0.204f, 0.827f, 0.6f, 1f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            lblHeader.AddThemeFontSizeOverride("font_size", 12);
            topHBox.AddChild(lblHeader);

            var btnOpenCauldron = new Button
            {
                Text = "◆ Alchemical Cauldron (合炼鼎炉) [R] ◆",
                CustomMinimumSize = new Vector2(250f, 28f),
                MouseFilter = MouseFilterEnum.Stop
            };
            btnOpenCauldron.Pressed += OpenCauldronFromLedger;
            topHBox.AddChild(btnOpenCauldron);

            // Scroll container for loadout & reserve
            var scroll = new ScrollContainer
            {
                SizeFlagsVertical = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever
            };
            vBox.AddChild(scroll);

            var contentList = new VBoxContainer();
            contentList.AddThemeConstantOverride("separation", 10);
            contentList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.AddChild(contentList);

            // Section 1: Active Loadout
            var lblActiveSection = new Label
            {
                Text = "ACTIVE APERTURE LOADOUT (5 COMBAT SLOTS)",
                Modulate = new Color(0.961f, 0.620f, 0.043f, 1f)
            };
            lblActiveSection.AddThemeFontSizeOverride("font_size", 11);
            contentList.AddChild(lblActiveSection);

            _equippedContainer = new VBoxContainer();
            _equippedContainer.AddThemeConstantOverride("separation", 6);
            contentList.AddChild(_equippedContainer);

            // Section 2: Reserve Inventory
            var lblReserveSection = new Label
            {
                Text = "RESERVE & CAPTURED GU WORMS (空窍蕴藏)",
                Modulate = new Color(0.22f, 0.74f, 0.97f, 1f)
            };
            lblReserveSection.AddThemeFontSizeOverride("font_size", 11);
            contentList.AddChild(lblReserveSection);

            _reserveContainer = new VBoxContainer();
            _reserveContainer.AddThemeConstantOverride("separation", 6);
            contentList.AddChild(_reserveContainer);
        }

        // =========================================================================
        // TAB 2: DAO MARKS & RESTRICTIONS (道痕与凡人禁制)
        // =========================================================================

        private void BuildDaoMarksTab(PanelContainer container)
        {
            _panelDaoMarks = new MarginContainer { Name = "PanelDaoMarks" };
            var margin = (MarginContainer)_panelDaoMarks;
            margin.AddThemeConstantOverride("margin_left", 14);
            margin.AddThemeConstantOverride("margin_right", 14);
            margin.AddThemeConstantOverride("margin_top", 10);
            margin.AddThemeConstantOverride("margin_bottom", 10);
            container.AddChild(_panelDaoMarks);

            var vBox = new VBoxContainer();
            vBox.AddThemeConstantOverride("separation", 8);
            margin.AddChild(vBox);

            // 1. Header with Bagua Mandala
            var headerHBox = new HBoxContainer();
            vBox.AddChild(headerHBox);

            var lblSection = new Label
            {
                Text = "DAO MARKS & MORTAL RESTRICTIONS (道痕与凡人禁制)",
                Modulate = new Color(0.22f, 0.74f, 0.97f, 1f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            lblSection.AddThemeFontSizeOverride("font_size", 12);
            headerHBox.AddChild(lblSection);

            var baguaMedallion = new PanelContainer { CustomMinimumSize = new Vector2(40f, 40f) };
            var baguaTex = new TextureRect { Texture = _texBaguaMandala, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
            baguaMedallion.AddChild(baguaTex);
            headerHBox.AddChild(baguaMedallion);

            // 2. Innate Human Path Card with Emerald Left Accent
            var humanCard = new PanelContainer();
            var humanStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.047f, 0.063f, 0.055f, 1f),
                BorderColor = new Color(0.204f, 0.827f, 0.6f, 1f), // Emerald accent
                BorderWidthLeft = 4,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopRight = 3,
                CornerRadiusBottomRight = 3
            };
            humanCard.AddThemeStyleboxOverride("panel", humanStyle);
            vBox.AddChild(humanCard);

            var humanMargin = new MarginContainer();
            humanMargin.AddThemeConstantOverride("margin_left", 10);
            humanMargin.AddThemeConstantOverride("margin_right", 10);
            humanMargin.AddThemeConstantOverride("margin_top", 8);
            humanMargin.AddThemeConstantOverride("margin_bottom", 8);
            humanCard.AddChild(humanMargin);

            var humanHBox = new HBoxContainer();
            humanHBox.AddThemeConstantOverride("separation", 12);
            humanMargin.AddChild(humanHBox);

            var humanSealRect = new TextureRect
            {
                Texture = _texDaoHuman,
                CustomMinimumSize = new Vector2(44f, 44f),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };
            humanHBox.AddChild(humanSealRect);

            var humanTextVBox = new VBoxContainer();
            humanHBox.AddChild(humanTextVBox);

            _lblHumanCardTitle = new Label
            {
                Text = $"Innate Human Path Dao Marks (人道道痕): {_humanPathDaoMarks} Marks (Aptitude: {_aptitudePercent:F0}%)",
                Modulate = new Color(0.961f, 0.620f, 0.043f, 1f)
            };
            _lblHumanCardTitle.AddThemeFontSizeOverride("font_size", 12);
            humanTextVBox.AddChild(_lblHumanCardTitle);

            _lblHumanCardDesc = new Label
            {
                Text = $"• Cultivation Amplification: +{_humanPathDaoMarks * 2}% Substage Wall-Washing Rate, +{_humanPathDaoMarks}% Major Wall-Battering Force.\n• Canon Law: Normal mortals capped at <= 10 marks; Ten Extreme Physiques possess 50-100 marks.",
                Modulate = new Color(0.58f, 0.64f, 0.72f, 1f)
            };
            _lblHumanCardDesc.AddThemeFontSizeOverride("font_size", 10);
            humanTextVBox.AddChild(_lblHumanCardDesc);

            // 3. External Great Dao Marks (7 Paths Grid)
            var lblExternalHeader = new Label
            {
                Text = "EXTERNAL PATH DAO MARKS (GREAT DAO PROHIBITION - 凡躯不承道痕)",
                Modulate = new Color(0.88f, 0.91f, 0.94f, 1f)
            };
            lblExternalHeader.AddThemeFontSizeOverride("font_size", 11);
            vBox.AddChild(lblExternalHeader);

            var pathsGrid = new GridContainer { Columns = 2 };
            pathsGrid.AddThemeConstantOverride("h_separation", 10);
            pathsGrid.AddThemeConstantOverride("v_separation", 6);
            vBox.AddChild(pathsGrid);

            (Texture2D? icon, string name, string cnName)[] paths = new[]
            {
                (_texDaoMoon,     "Moon Path",        "月道"),
                (_texDaoStrength, "Strength Path",    "力道"),
                (_texDaoBone,     "Bone Path",        "骨道"),
                (_texDaoPoison,   "Poison Path",      "毒道"),
                (_texDaoBlood,    "Blood Path",       "血道"),
                (_texDaoRefine,   "Refinement Path",  "炼道"),
                (_texDaoEnslave,  "Enslavement Path", "奴道")
            };

            foreach (var p in paths)
            {
                pathsGrid.AddChild(CreateExternalPathCard(p.icon, p.name, p.cnName));
            }

            // 4. Great Dao Lore Footnote
            var lblLore = new Label
            {
                Text = "\"A mortal aperture and physical body cannot bear the immense weight of external Great Dao marks without collapsing into dust.\nOnly upon balancing heaven, earth, and human qi, withstanding the Earthly Calamity and Heavenly Tribulation to ascend to a Rank 6 Gu Immortal, can external Dao marks permanently etch into the immortal aperture.\"",
                Modulate = new Color(0.58f, 0.64f, 0.72f, 1f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            lblLore.AddThemeFontSizeOverride("font_size", 10);
            vBox.AddChild(lblLore);
        }

        private PanelContainer CreateExternalPathCard(Texture2D? icon, string name, string cnName)
        {
            var card = new PanelContainer { CustomMinimumSize = new Vector2(380f, 38f) };
            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.047f, 0.063f, 0.055f, 1f),
                BorderColor = new Color(0.133f, 0.200f, 0.188f, 1f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 2,
                CornerRadiusBottomRight = 2,
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2
            };
            card.AddThemeStyleboxOverride("panel", style);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 8);
            margin.AddThemeConstantOverride("margin_right", 8);
            margin.AddThemeConstantOverride("margin_top", 4);
            margin.AddThemeConstantOverride("margin_bottom", 4);
            card.AddChild(margin);

            var hBox = new HBoxContainer();
            hBox.AddThemeConstantOverride("separation", 8);
            margin.AddChild(hBox);

            var iconRect = new TextureRect
            {
                Texture = icon,
                CustomMinimumSize = new Vector2(28f, 28f),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };
            hBox.AddChild(iconRect);

            var textVBox = new VBoxContainer();
            textVBox.AddThemeConstantOverride("separation", 0);
            hBox.AddChild(textVBox);

            var lblTitle = new Label { Text = $"{name} ({cnName})", Modulate = new Color(0.88f, 0.91f, 0.94f, 1f) };
            lblTitle.AddThemeFontSizeOverride("font_size", 11);
            textVBox.AddChild(lblTitle);

            var lblLocked = new Label { Text = "0 Marks (Locked - Rank 6 Ascension)", Modulate = new Color(0.392f, 0.455f, 0.545f, 1f) };
            lblLocked.AddThemeFontSizeOverride("font_size", 9);
            textVBox.AddChild(lblLocked);

            return card;
        }

        // =========================================================================
        // TAB 3: AUTONOMOUS INSTITUTIONAL HIERARCHY (宗族恩仇与阵营声望)
        // =========================================================================

        private void BuildFactionsTab(PanelContainer container)
        {
            _panelFactions = new MarginContainer { Name = "PanelFactions" };
            var margin = (MarginContainer)_panelFactions;
            margin.AddThemeConstantOverride("margin_left", 14);
            margin.AddThemeConstantOverride("margin_right", 14);
            margin.AddThemeConstantOverride("margin_top", 10);
            margin.AddThemeConstantOverride("margin_bottom", 10);
            container.AddChild(_panelFactions);

            var vBox = new VBoxContainer();
            vBox.AddThemeConstantOverride("separation", 8);
            margin.AddChild(vBox);

            var lblHeader = new Label
            {
                Text = "AUTONOMOUS INSTITUTIONAL HIERARCHY (宗族恩仇与阵营声望)",
                Modulate = new Color(0.22f, 0.74f, 0.97f, 1f)
            };
            lblHeader.AddThemeFontSizeOverride("font_size", 12);
            vBox.AddChild(lblHeader);

            var lblSub = new Label
            {
                Text = "Dynamic diplomatic standing across the 6 major Southern Border powers. Dictates trade tariffs, pavilion sanctuary, and bounty pursuit.",
                Modulate = new Color(0.58f, 0.64f, 0.72f, 1f)
            };
            lblSub.AddThemeFontSizeOverride("font_size", 10);
            vBox.AddChild(lblSub);

            var scroll = new ScrollContainer
            {
                SizeFlagsVertical = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever
            };
            vBox.AddChild(scroll);

            _factionsContainer = new VBoxContainer();
            _factionsContainer.AddThemeConstantOverride("separation", 8);
            _factionsContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.AddChild(_factionsContainer);
        }

        // =========================================================================
        // STATE REFRESH CONTROLLERS
        // =========================================================================

        public void UpdateAllDisplays()
        {
            UpdateCultivationDisplay();
            UpdateGuWormsDisplay();
            UpdateDaoMarksDisplay();
            UpdateFactionsDisplay();
            UpdateTabHeaders();
        }

        private void UpdateCultivationDisplay()
        {
            _scaffold?.SetTitle(LedgerTitle);

            if (_lblSubtitle != null)
                _lblSubtitle.Text = $"Gu Yue Clan Heir: {_cultivatorName} | Rank {_rank} {_substageName}";

            if (_lblCultivatorRealm != null)
                _lblCultivatorRealm.Text = $"Cultivation Realm: Rank {_rank} {_substageName}";

            if (_lblMembrane != null)
                _lblMembrane.Text = $"Aperture Membrane: {_membraneName}";

            if (_lblAptitude != null)
                _lblAptitude.Text = $"Innate Aptitude: {_aptitudePercent:F0}% ({_cultivatorName})";

            if (_lblStones != null)
                _lblStones.Text = $"Primeval Stones: {_primevalStones} Stones";

            if (_lblHumanDaoMarks != null)
                _lblHumanDaoMarks.Text = $"Human Path Dao Marks: {_humanPathDaoMarks} Marks (+{_humanPathDaoMarks * 2}% Wash, +{_humanPathDaoMarks}% Batter)";

            double density = CultivatorData.CalculateDensityCoefficient(_rank, _substage);
            if (_lblDensity != null)
                _lblDensity.Text = $"Density Multiplier: {density:N0}x (Qualitative Purity)";

            if (_lblLifespan != null)
                _lblLifespan.Text = $"Heavenly Lifespan (寿元): Age {_age} / {_lifespanYears} Years ({_remainingLifespanYears} yrs left)";

            if (_lblSeasonal != null)
                _lblSeasonal.Text = $"Seasonal Resonance: {_seasonalAffinityText}";

            // Primeval Sea gauge
            float fillPct = Mathf.Clamp(_maxEssenceCapacity > 0 ? _currentEssenceSea / _maxEssenceCapacity : 0f, 0f, 1f);
            if (_lblSeaTitle != null)
                _lblSeaTitle.Text = $"{GetEssenceSeaName(_rank)}: {_currentEssenceSea:F1}% / {_maxEssenceCapacity:F1}%";

            if (_gaugeSea != null)
            {
                _gaugeSea.Value = fillPct;
                _gaugeSea.CenterText = $"Primeval Sea Fill: {fillPct * 100f:F1}% ({_currentEssenceSea:F1}% of {_maxEssenceCapacity:F1}% Max Capacity)";
                _gaugeSea.QueueRedraw();
            }

            // Aperture Wall gauge
            if (_substage < 4)
            {
                string nextStage = _substage switch { 1 => "Middle Stage (中阶)", 2 => "Upper Stage (高阶)", 3 => "Peak Stage (巅峰)", _ => "Next Stage" };
                if (_lblWallTitle != null)
                    _lblWallTitle.Text = $"Aperture Wall-Washing Progress: {_wallWashingProgress:F1}% / 100%  (Tempering towards {nextStage})";

                if (_gaugeWall != null)
                {
                    _gaugeWall.FillTexture = _texFillCyan;
                    _gaugeWall.Value = Mathf.Clamp(_wallWashingProgress / 100f, 0f, 1f);
                    _gaugeWall.CenterText = $"Membrane Tempering: {_wallWashingProgress:F1}%";
                    _gaugeWall.QueueRedraw();
                }
            }
            else
            {
                string statusText = _apertureWallDurability <= _maxWallDurability * 0.3f ? "CRACKED RIGID MEMBRANE" : "Solidified Peak Membrane";
                if (_lblWallTitle != null)
                    _lblWallTitle.Text = $"Aperture Barrier Durability: {_apertureWallDurability:F1} / {_maxWallDurability:F1}  ({statusText})";

                if (_gaugeWall != null)
                {
                    _gaugeWall.FillTexture = _texFillCrimson;
                    _gaugeWall.Value = Mathf.Clamp(_maxWallDurability > 0 ? _apertureWallDurability / _maxWallDurability : 0f, 0f, 1f);
                    _gaugeWall.CenterText = $"Membrane Barrier Integrity: {_apertureWallDurability:F1} / {_maxWallDurability:F1}";
                    _gaugeWall.QueueRedraw();
                }
            }

            // Stone recovery percentage update on button
            float singleRecovery = CultivatorData.CalculateStoneRecoveryPercentage(_rank, _substage, _aptitudePercent);
            if (_btnActionStone != null)
            {
                _btnActionStone.Text = $"Crush Primeval Stone\n(-1 Stone / +{singleRecovery:F1}% Sea)";
            }

            if (_btnActionWash != null)
            {
                if (_substage < 4)
                {
                    _btnActionWash.Text = "Wash Aperture Wall\n(-15% Sea, -1 AP / Substage)";
                    _btnActionWash.Disabled = false;
                }
                else
                {
                    _btnActionWash.Text = "Wall Fully Solidified\n(Peak Stage Reached)";
                    _btnActionWash.Disabled = true;
                }
            }

            if (_btnActionBreakthrough != null)
            {
                if (_substage == 4 && _rank < 5)
                {
                    _btnActionBreakthrough.Text = "Batter Barrier Chamber\n(Breakthrough Seclusion)";
                    _btnActionBreakthrough.Disabled = false;
                }
                else if (_rank >= 5)
                {
                    _btnActionBreakthrough.Text = "Mortal Ceiling Reached\n(Requires Tribulation)";
                    _btnActionBreakthrough.Disabled = true;
                }
                else
                {
                    _btnActionBreakthrough.Text = "Breakthrough Locked\n(Requires Peak Stage)";
                    _btnActionBreakthrough.Disabled = true;
                }
            }
        }

        private void UpdateGuWormsDisplay()
        {
            if (_equippedContainer == null || _reserveContainer == null) return;

            // Clear old entries
            foreach (var child in _equippedContainer.GetChildren()) child.QueueFree();
            foreach (var child in _reserveContainer.GetChildren()) child.QueueFree();

            // Render Equipped Gu
            if (_equippedGu.Count == 0)
            {
                var lblEmpty = new Label { Text = "No Gu worms currently loaded in aperture.", Modulate = new Color(0.58f, 0.64f, 0.72f, 1f) };
                lblEmpty.AddThemeFontSizeOverride("font_size", 11);
                _equippedContainer.AddChild(lblEmpty);
            }
            else
            {
                foreach (var gu in _equippedGu)
                {
                    _equippedContainer.AddChild(CreateGuCard(gu, isEquipped: true));
                }
            }

            // Render Reserve Gu
            if (_reserveGu.Count == 0)
            {
                var lblEmpty = new Label { Text = "No reserve Gu worms in aperture. Hunt beasts or explore ancient caves to obtain wild Gu.", Modulate = new Color(0.58f, 0.64f, 0.72f, 1f) };
                lblEmpty.AddThemeFontSizeOverride("font_size", 11);
                _reserveContainer.AddChild(lblEmpty);
            }
            else
            {
                foreach (var gu in _reserveGu)
                {
                    _reserveContainer.AddChild(CreateGuCard(gu, isEquipped: false));
                }
            }
        }

        private PanelContainer CreateGuCard(GuWormData gu, bool isEquipped)
        {
            var card = new PanelContainer { CustomMinimumSize = new Vector2(0f, 68f) };
            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.047f, 0.063f, 0.055f, 1f),
                BorderColor = gu.IsVitalGu ? new Color(0.984f, 0.749f, 0.141f, 1f) : new Color(0.133f, 0.200f, 0.188f, 1f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3
            };
            card.AddThemeStyleboxOverride("panel", style);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 8);
            margin.AddThemeConstantOverride("margin_right", 8);
            margin.AddThemeConstantOverride("margin_top", 6);
            margin.AddThemeConstantOverride("margin_bottom", 6);
            card.AddChild(margin);

            var hBox = new HBoxContainer();
            hBox.AddThemeConstantOverride("separation", 10);
            margin.AddChild(hBox);

            // 1. Slot frame + Gu icon
            var slotFrame = new PanelContainer { CustomMinimumSize = new Vector2(52f, 52f) };
            Texture2D? frameTex = gu.IsVitalGu ? (_texSlotJade ?? _texSlotBrass) : (gu.Rank >= 3 ? _texSlotBrass : (gu.Rank == 2 ? _texSlotBronze : _texSlotIron));
            var frameRect = new TextureRect { Texture = frameTex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize };
            slotFrame.AddChild(frameRect);

            Texture2D? iconTex = GetGuIcon(gu.Name);
            var iconRect = new TextureRect
            {
                Texture = iconTex,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };
            slotFrame.AddChild(iconRect);
            hBox.AddChild(slotFrame);

            // 2. Info details
            var infoVBox = new VBoxContainer();
            infoVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            hBox.AddChild(infoVBox);

            string vitalTag = gu.IsVitalGu ? "  [VITAL GU / 本命蛊]" : (gu.HasHostileWill ? "  [WILD / HOSTILE WILL]" : (isEquipped ? "" : "  [TAME RESERVE]"));
            Color nameColor = gu.IsVitalGu ? new Color(0.984f, 0.749f, 0.141f, 1f) : (gu.HasHostileWill ? new Color(0.937f, 0.267f, 0.267f, 1f) : new Color(0.88f, 0.91f, 0.94f, 1f));

            var lblName = new Label { Text = $"{gu.Name}{vitalTag}", Modulate = nameColor };
            lblName.AddThemeFontSizeOverride("font_size", 12);
            infoVBox.AddChild(lblName);

            var lblDetails = new Label { Text = $"Rank {gu.Rank} | Slot: {gu.Slot} | Diet: {gu.Diet}", Modulate = new Color(0.58f, 0.64f, 0.72f, 1f) };
            lblDetails.AddThemeFontSizeOverride("font_size", 10);
            infoVBox.AddChild(lblDetails);

            // 3. Satiety Meter
            var satVBox = new VBoxContainer { CustomMinimumSize = new Vector2(160f, 0f) };
            hBox.AddChild(satVBox);

            Color satColor = gu.Satiety <= 25f ? new Color(0.937f, 0.267f, 0.267f, 1f) : (gu.Satiety <= 50f ? new Color(0.984f, 0.749f, 0.141f, 1f) : new Color(0.204f, 0.827f, 0.6f, 1f));
            var lblSatiety = new Label { Text = $"Satiety: {gu.Satiety:F0}% ({gu.GetStarvationStage()})", Modulate = satColor };
            lblSatiety.AddThemeFontSizeOverride("font_size", 10);
            satVBox.AddChild(lblSatiety);

            var satGauge = new StyledGaugeControl
            {
                CustomMinimumSize = new Vector2(160f, 16f),
                FrameTexture = _texFrameBarBronze,
                FillTexture = (gu.Satiety <= 25f && _texFillCrimson != null) ? _texFillCrimson : _texFillEmerald,
                Value = Mathf.Clamp(gu.Satiety / 100f, 0f, 1f),
                CenterText = $"{gu.Satiety:F0}%"
            };
            satVBox.AddChild(satGauge);

            // 4. Feed Button
            var btnFeed = new Button
            {
                Text = "Feed 1x Ration",
                CustomMinimumSize = new Vector2(110f, 34f),
                MouseFilter = MouseFilterEnum.Stop
            };
            btnFeed.Pressed += () => GameEventManager.RaiseRequestFeedGu(gu.GuID, gu.Diet, 1);
            hBox.AddChild(btnFeed);

            return card;
        }

        private Texture2D? GetGuIcon(string guName)
        {
            if (string.IsNullOrEmpty(guName)) return _iconMoonShadow;
            if (guName.Contains("Moon Shadow") || guName.Contains("Moonlight")) return _iconMoonShadow;
            if (guName.Contains("Boar") || guName.Contains("Strength")) return _iconBoarStrength;
            if (guName.Contains("Moonglow")) return _iconMoonglow;
            return _iconMoonShadow;
        }

        private void UpdateDaoMarksDisplay()
        {
            if (_lblHumanCardTitle != null)
                _lblHumanCardTitle.Text = $"Innate Human Path Dao Marks (人道道痕): {_humanPathDaoMarks} Marks (Aptitude: {_aptitudePercent:F0}%)";

            if (_lblHumanCardDesc != null)
                _lblHumanCardDesc.Text = $"• Cultivation Amplification: +{_humanPathDaoMarks * 2}% Substage Wall-Washing Rate, +{_humanPathDaoMarks}% Major Wall-Battering Force.\n• Canon Law: Normal mortals capped at <= 10 marks; Ten Extreme Physiques possess 50-100 marks.";
        }

        private void UpdateFactionsDisplay()
        {
            if (_factionsContainer == null) return;

            foreach (var child in _factionsContainer.GetChildren()) child.QueueFree();

            var factionState = FactionContextProvider.FactionResolver?.Invoke();
            if (factionState == null || factionState.Factions == null || factionState.Factions.Count == 0)
            {
                factionState = new WorldFactionState();
                factionState.InitializeDefaultFactions();
            }

            foreach (var faction in factionState.Factions)
            {
                _factionsContainer.AddChild(CreateFactionCard(faction));
            }
        }

        private PanelContainer CreateFactionCard(FactionData faction)
        {
            var card = new PanelContainer { CustomMinimumSize = new Vector2(0f, 84f) };
            var tier = faction.GetStandingTier();
            Color accentColor = tier switch
            {
                FactionStandingTier.Revered  => new Color(0.204f, 0.827f, 0.6f, 1f),
                FactionStandingTier.Friendly => new Color(0.220f, 0.741f, 0.973f, 1f),
                FactionStandingTier.Neutral  => new Color(0.580f, 0.640f, 0.720f, 1f),
                FactionStandingTier.Hostile  => new Color(0.976f, 0.451f, 0.090f, 1f),
                FactionStandingTier.Nemesis  => new Color(0.937f, 0.267f, 0.267f, 1f),
                _ => new Color(0.5f, 0.5f, 0.5f, 1f)
            };

            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.047f, 0.063f, 0.055f, 1f),
                BorderColor = accentColor,
                BorderWidthLeft = 4, // Left standing accent strip
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                CornerRadiusBottomRight = 3,
                CornerRadiusTopRight = 3
            };
            card.AddThemeStyleboxOverride("panel", style);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 8);
            margin.AddThemeConstantOverride("margin_right", 8);
            margin.AddThemeConstantOverride("margin_top", 6);
            margin.AddThemeConstantOverride("margin_bottom", 6);
            card.AddChild(margin);

            var hBox = new HBoxContainer();
            hBox.AddThemeConstantOverride("separation", 10);
            margin.AddChild(hBox);

            // 1. Clan Banner or Seal Badge
            Texture2D? bannerTex = faction.ID switch
            {
                FactionID.GuYueClan => _texBannerGuYue,
                FactionID.BaiClan   => _texBannerBai,
                FactionID.XiongClan => _texBannerXiong,
                FactionID.JiaConsortium => _texDaoHuman,
                FactionID.TieClan   => _texDaoStrength,
                _ => null
            };

            var bannerPanel = new PanelContainer { CustomMinimumSize = new Vector2(46f, 68f) };
            if (bannerTex != null)
            {
                var bannerRect = new TextureRect
                {
                    Texture = bannerTex,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
                };
                bannerPanel.AddChild(bannerRect);
            }
            else
            {
                var lblSeal = new Label { Text = faction.SealCalligraphy, Modulate = accentColor, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                lblSeal.AddThemeFontSizeOverride("font_size", 14);
                bannerPanel.AddChild(lblSeal);
            }
            hBox.AddChild(bannerPanel);

            // 2. Titles & Status
            var infoVBox = new VBoxContainer();
            infoVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            hBox.AddChild(infoVBox);

            var lblTitle = new Label { Text = $"{faction.Name} ({faction.ChineseName})", Modulate = new Color(0.961f, 0.620f, 0.043f, 1f) };
            lblTitle.AddThemeFontSizeOverride("font_size", 12);
            infoVBox.AddChild(lblTitle);

            var lblStanding = new Label { Text = $"Standing: {faction.GetStandingTitle()}", Modulate = accentColor };
            lblStanding.AddThemeFontSizeOverride("font_size", 10);
            infoVBox.AddChild(lblStanding);

            // Tariffs & Sanctuary
            string tradeStatus;
            float priceMult = faction.GetPriceMultiplier();
            if (priceMult >= 900f) tradeStatus = "Trade Embargoed (拒绝交易)";
            else if (priceMult > 1.0f) tradeStatus = $"Tariff +{(priceMult - 1f) * 100:F0}% (冷眼加价)";
            else if (priceMult < 1.0f) tradeStatus = $"Discount -{(1f - priceMult) * 100:F0}% (知己特惠)";
            else tradeStatus = "Standard Rates (平价交易)";

            string sanctuaryStatus = (tier == FactionStandingTier.Nemesis || tier == FactionStandingTier.Hostile)
                ? "Sanctuary: Barred (驱逐)"
                : "Sanctuary: Welcome (容留)";

            var lblTariff = new Label { Text = $"{tradeStatus} | {sanctuaryStatus}", Modulate = new Color(0.58f, 0.64f, 0.72f, 1f) };
            lblTariff.AddThemeFontSizeOverride("font_size", 9);
            infoVBox.AddChild(lblTariff);

            var lblLore = new Label { Text = faction.Description, Modulate = new Color(0.45f, 0.50f, 0.55f, 1f), AutowrapMode = TextServer.AutowrapMode.WordSmart };
            lblLore.AddThemeFontSizeOverride("font_size", 9);
            infoVBox.AddChild(lblLore);

            // 3. Reputation Gauge [-300..+300]
            var gaugeVBox = new VBoxContainer { CustomMinimumSize = new Vector2(170f, 0f) };
            hBox.AddChild(gaugeVBox);

            string scoreSign = faction.ReputationScore > 0 ? $"+{faction.ReputationScore}" : $"{faction.ReputationScore}";
            var lblScore = new Label { Text = $"Score: {scoreSign} / [-300..+300]", Modulate = accentColor };
            lblScore.AddThemeFontSizeOverride("font_size", 10);
            gaugeVBox.AddChild(lblScore);

            var repGauge = new FactionReputationGaugeControl
            {
                CustomMinimumSize = new Vector2(170f, 16f),
                Score = faction.ReputationScore
            };
            gaugeVBox.AddChild(repGauge);

            return card;
        }

        private static string GetEssenceSeaName(int rank)
        {
            return rank switch
            {
                1 => "Green Copper Primeval Sea (青铜元海)",
                2 => "Red Iron Primeval Sea (赤铁元海)",
                3 => "White Silver Primeval Sea (白银元海)",
                4 => "Yellow Gold Primeval Sea (黄金元海)",
                5 => "Purple Crystal Primeval Sea (紫晶元海)",
                _ => "Primeval Sea"
            };
        }
    }

    // =========================================================================
    // CUSTOM XIANXIA CONTROLS: GAUGES WITH BRONZE FRAMES & DIRECTIONAL REPUTATION
    // =========================================================================

    public partial class StyledGaugeControl : Control
    {
        public Texture2D? FrameTexture { get; set; }
        public Texture2D? FillTexture { get; set; }
        public float Value { get; set; } = 1.0f; // [0..1]
        public string CenterText { get; set; } = "";

        public override void _Draw()
        {
            var size = Size;
            var bgRect = new Rect2(Vector2.Zero, size);

            float padX = (FrameTexture != null) ? size.X * 0.1435f : 0f;
            float padY = (FrameTexture != null) ? size.Y * 0.2209f : 0f;
            float innerW = size.X - (padX * 2f);
            float innerH = size.Y - (padY * 2f);
            float clampedFill = Mathf.Clamp(Value, 0f, 1f);

            // 1. Dark background in cavity
            DrawRect(new Rect2(padX, padY, innerW, innerH), new Color(0.047f, 0.063f, 0.055f, 1f));

            // 2. Fluid fill strictly inside cavity (never leaks through ornamental bronze ends)
            if (clampedFill > 0.001f)
            {
                float fillW = innerW * clampedFill;
                var fillRect = new Rect2(padX, padY, fillW, innerH);

                if (FillTexture != null)
                {
                    var srcRect = new Rect2(0f, 0f, FillTexture.GetWidth() * clampedFill, FillTexture.GetHeight());
                    DrawTextureRectRegion(FillTexture, fillRect, srcRect);
                }
                else
                {
                    DrawRect(fillRect, new Color(0.133f, 0.773f, 0.369f, 0.85f));
                }
            }

            // 3. Bronze Frame Over entire bounds
            if (FrameTexture != null)
            {
                DrawTextureRect(FrameTexture, bgRect, false);
            }
            else
            {
                DrawRect(bgRect, new Color(0.851f, 0.467f, 0.024f, 0.6f), false, 1f);
            }

            // 4. Center Label with Drop Shadow
            if (!string.IsNullOrEmpty(CenterText))
            {
                var font = ThemeDB.FallbackFont;
                int fontSize = 11;
                var stringSize = font.GetStringSize(CenterText, HorizontalAlignment.Center, -1, fontSize);
                var textPos = new Vector2((size.X - stringSize.X) * 0.5f, (size.Y + stringSize.Y * 0.65f) * 0.5f);

                DrawString(font, textPos + new Vector2(1f, 1f), CenterText, HorizontalAlignment.Center, -1, fontSize, new Color(0f, 0f, 0f, 0.9f));
                DrawString(font, textPos, CenterText, HorizontalAlignment.Center, -1, fontSize, new Color(1f, 1f, 1f, 0.95f));
            }
        }
    }

    public partial class FactionReputationGaugeControl : Control
    {
        public int Score { get; set; } = 0; // [-300 .. +300]

        public override void _Draw()
        {
            var size = Size;
            var bgRect = new Rect2(Vector2.Zero, size);

            // 1. Dark background
            DrawRect(bgRect, new Color(0.047f, 0.063f, 0.055f, 1f));

            float midX = size.X * 0.5f;

            // 2. Directional Fill
            if (Score > 0)
            {
                float ratio = Mathf.Clamp(Score / 300f, 0f, 1f);
                float fillW = (size.X * 0.5f) * ratio;
                DrawRect(new Rect2(midX, 1f, fillW, size.Y - 2f), new Color(0.204f, 0.827f, 0.6f, 0.85f));
            }
            else if (Score < 0)
            {
                float ratio = Mathf.Clamp(Mathf.Abs(Score) / 300f, 0f, 1f);
                float fillW = (size.X * 0.5f) * ratio;
                DrawRect(new Rect2(midX - fillW, 1f, fillW, size.Y - 2f), new Color(0.937f, 0.267f, 0.267f, 0.85f));
            }

            // 3. Center zero line
            DrawLine(new Vector2(midX, 0f), new Vector2(midX, size.Y), new Color(0.58f, 0.64f, 0.72f, 1f), 1.5f);

            // 4. Border outline
            DrawRect(bgRect, new Color(0.133f, 0.200f, 0.188f, 1f), false, 1f);
        }
    }
}
