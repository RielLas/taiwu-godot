using System;
using Godot;
using Color = Godot.Color;
using TaiwuGu.Combat;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Map;
using TaiwuGu.Systems;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Phase 2: Taiwu Master HUD View (Godot 2D).
    /// Direct-read presentation observer wired to WorldStore via IWorldStateProvider & WorldSignals.
    /// Replicates Unity's clean code-drawn dark-lacquer minimalist aesthetic:
    /// - Top-Left: Solid flat cyan AP bar and flat emerald Sea bar in midnight slots with dim hotkeys.
    /// - Top-Center: Crisp dark-lacquer banner with amber gold title and ivory subtitle.
    /// - Top-Right: Crimson lacquer Pass Month button, slate grey System button, and jade badges.
    /// </summary>
    public partial class TaiwuHUDView : Control
    {
        private TaiwuModalStack? _modalStack;
        private IWorldStateProvider? _stateProvider;
        private WorldAtlasModalView? _atlasModal;

        public void Initialize(TaiwuModalStack modalStack, IWorldStateProvider stateProvider)
        {
            _modalStack = modalStack;
            _stateProvider = stateProvider;

            // Consume pending NewRunConfig or save load from Title Screen
            if (stateProvider is WorldStateProvider provider)
            {
                if (TaiwuTitleScreenView.PendingRunConfig != null)
                {
                    var config = TaiwuTitleScreenView.PendingRunConfig;
                    TaiwuTitleScreenView.PendingRunConfig = null;
                    GD.Print($"[TaiwuHUDView] Applying PendingRunConfig: {config.PlayerName} (Aptitude: {config.AptitudePercent}%, Sector: {config.StartingSectorKey}, Vital Gu: {config.VitalGuId})");
                    provider.Replace(WorldFactory.CreateFromConfig(config));
                    WorldSignals.EmitTopics(DirtyTopic.All);
                }
                else if (!string.IsNullOrEmpty(TaiwuTitleScreenView.PendingLoadSlot))
                {
                    string slot = TaiwuTitleScreenView.PendingLoadSlot;
                    TaiwuTitleScreenView.PendingLoadSlot = null;
                    GD.Print($"[TaiwuHUDView] Applying PendingLoadSlot: [{slot}]");
                    SaveSystem.LoadAndSwap(slot, provider);
                }
            }

            RefreshFromStore();
        }

        // UI Node references
        private Label? _lblRegionTitle;
        private Label? _lblDateSubtitle;
        private ProgressBar? _barAP;
        private Label? _lblAPText;
        private ProgressBar? _barSea;
        private Label? _lblSeaText;
        private RichTextLabel? _lblCultivatorBadge;
        private Button? _btnPassMonth;
        private MarginContainer? _topBarMargin;
        private PanelContainer? _scenicToastPanel;
        private Label? _lblScenicToast;
        private Tween? _toastTween;

        // Seal-Stamp Event Banner (Ink-Crimson Animation)
        private PanelContainer? _eventBannerPanel;
        private Label? _lblBannerSeal;
        private Label? _lblBannerDetail;
        private Tween? _bannerTween;

        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;

            BuildUIHierarchy();

            WorldSignals.OnTopicsChanged += HandleTopicsChanged;
            WorldSignals.OnScenicToastRequested += HandleTileInspected;
            WorldSignals.OnTerrainHazardTriggered += HandleTerrainHazard;
            WorldSignals.OnRoadAmbushOccurred += HandleRoadAmbush;
            WorldSignals.OnBattleStageChanged += HandleBattleStageChanged;
            WorldSignals.OnCombatResolved += HandleCombatResolved;
            WorldSignals.OnWorldCalamityUpdated += HandleWorldCalamityUpdated;
            WorldSignals.OnBountyHunterAmbush += HandleBountyHunterAmbush;
            WorldSignals.OnBreakthroughResolved += HandleBreakthroughResolved;

            RefreshFromStore();
        }

        public override void _ExitTree()
        {
            WorldSignals.OnTopicsChanged -= HandleTopicsChanged;
            WorldSignals.OnScenicToastRequested -= HandleTileInspected;
            WorldSignals.OnTerrainHazardTriggered -= HandleTerrainHazard;
            WorldSignals.OnRoadAmbushOccurred -= HandleRoadAmbush;
            WorldSignals.OnBattleStageChanged -= HandleBattleStageChanged;
            WorldSignals.OnCombatResolved -= HandleCombatResolved;
            WorldSignals.OnWorldCalamityUpdated -= HandleWorldCalamityUpdated;
            WorldSignals.OnBountyHunterAmbush -= HandleBountyHunterAmbush;
            WorldSignals.OnBreakthroughResolved -= HandleBreakthroughResolved;

            _toastTween?.Kill();
            _bannerTween?.Kill();
        }

        private void BuildUIHierarchy()
        {
            // =========================================================================
            // TOP BAR MARGIN CONTAINER (Full Width, Zero Overlap Guaranteed)
            // =========================================================================
            _topBarMargin = new MarginContainer
            {
                Name = "TopBarMargin",
                MouseFilter = MouseFilterEnum.Ignore
            };
            _topBarMargin.SetAnchorsPreset(LayoutPreset.TopWide);
            _topBarMargin.OffsetTop = 8f;
            _topBarMargin.OffsetBottom = 68f;
            _topBarMargin.OffsetLeft = 8f;
            _topBarMargin.OffsetRight = -8f;
            AddChild(_topBarMargin);

            var topBarHBox = new HBoxContainer
            {
                Name = "TopBarHBox",
                Alignment = BoxContainer.AlignmentMode.Begin,
                MouseFilter = MouseFilterEnum.Ignore
            };
            _topBarMargin.AddChild(topBarHBox);

            // =========================================================================
            // 1. TOP-LEFT: DIM HOTKEYS + AP & PRIMEVAL SEA GAUGES
            // =========================================================================
            var leftHBox = new HBoxContainer
            {
                Name = "LeftHBox",
                Alignment = BoxContainer.AlignmentMode.Center,
                MouseFilter = MouseFilterEnum.Stop
            };
            leftHBox.AddThemeConstantOverride("separation", 10);
            topBarHBox.AddChild(leftHBox);

            // Dim hotkeys column (vertically stacked matching Unity Image 2)
            var hotkeysVBox = new VBoxContainer
            {
                Name = "HotkeysVBox",
                Alignment = BoxContainer.AlignmentMode.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            hotkeysVBox.AddThemeConstantOverride("separation", 1);
            leftHBox.AddChild(hotkeysVBox);

            var lblLedgerHint = new Label
            {
                Text = "[Tab / L] Ledger",
                Modulate = new Color(0.58f, 0.64f, 0.72f, 0.75f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            lblLedgerHint.AddThemeFontSizeOverride("font_size", 10);
            hotkeysVBox.AddChild(lblLedgerHint);

            var lblMapHint = new Label
            {
                Text = "[M] World Map",
                Modulate = new Color(0.58f, 0.64f, 0.72f, 0.75f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            lblMapHint.AddThemeFontSizeOverride("font_size", 10);
            hotkeysVBox.AddChild(lblMapHint);

            var lblCauldronHint = new Label
            {
                Text = "[C] Cauldron",
                Modulate = new Color(0.58f, 0.64f, 0.72f, 0.75f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            lblCauldronHint.AddThemeFontSizeOverride("font_size", 10);
            hotkeysVBox.AddChild(lblCauldronHint);

            var lblEscHint = new Label
            {
                Text = "[Esc] Menu",
                Modulate = new Color(0.58f, 0.64f, 0.72f, 0.75f), // Slate grey #94A3B8
                MouseFilter = MouseFilterEnum.Ignore
            };
            lblEscHint.AddThemeFontSizeOverride("font_size", 10);
            hotkeysVBox.AddChild(lblEscHint);

            var lblPanHint = new Label
            {
                Text = "[WASD / Drag] Pan",
                Modulate = new Color(0.58f, 0.64f, 0.72f, 0.75f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            lblPanHint.AddThemeFontSizeOverride("font_size", 10);
            hotkeysVBox.AddChild(lblPanHint);

            // Gauges column
            var gaugesContainer = new VBoxContainer
            {
                Name = "GaugesContainer",
                Alignment = BoxContainer.AlignmentMode.Center,
                CustomMinimumSize = new Vector2(195f, 44f),
                MouseFilter = MouseFilterEnum.Stop
            };
            gaugesContainer.AddThemeConstantOverride("separation", 3);
            leftHBox.AddChild(gaugesContainer);

            // StyleBox for Midnight Slot (#05090A)
            var slotBgStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.020f, 0.035f, 0.039f, 0.95f), // Midnight slot #05090A
                BorderColor = new Color(0.118f, 0.161f, 0.231f, 1f), // Subtle dark rim #1E293B
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 2,
                CornerRadiusBottomRight = 2,
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2
            };

            // AP Gauge: Solid flat cyan fill (#06B6D4 / #10B981)
            var apFillStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.063f, 0.714f, 0.831f, 1f), // Vibrant flat cyan #06B6D4
                CornerRadiusBottomLeft = 2,
                CornerRadiusBottomRight = 2,
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2
            };

            var apContainer = new Control
            {
                CustomMinimumSize = new Vector2(195f, 20f),
                MouseFilter = MouseFilterEnum.Stop
            };
            gaugesContainer.AddChild(apContainer);

            _barAP = new ProgressBar
            {
                Name = "BarAP",
                MinValue = 0,
                MaxValue = 1,
                Value = 0,
                ShowPercentage = false,
                Size = new Vector2(195f, 20f),
                MouseFilter = MouseFilterEnum.Stop
            };
            _barAP.AddThemeStyleboxOverride("background", slotBgStyle);
            _barAP.AddThemeStyleboxOverride("fill", apFillStyle);
            apContainer.AddChild(_barAP);

            _lblAPText = new Label
            {
                Name = "LblAPText",
                Text = string.Empty,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Size = new Vector2(195f, 20f),
                Modulate = new Color(1f, 0.984f, 0.922f, 1f), // Warm ivory gold #FFFBEB
                MouseFilter = MouseFilterEnum.Ignore
            };
            _lblAPText.AddThemeFontSizeOverride("font_size", 11);
            _lblAPText.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
            _lblAPText.AddThemeConstantOverride("outline_size", 2);
            apContainer.AddChild(_lblAPText);

            // Sea Gauge: Solid flat emerald fill (#067E68)
            var seaFillStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.024f, 0.494f, 0.408f, 1f), // Green copper / deep emerald #067E68
                CornerRadiusBottomLeft = 2,
                CornerRadiusBottomRight = 2,
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2
            };

            var seaContainer = new Control
            {
                CustomMinimumSize = new Vector2(195f, 20f),
                MouseFilter = MouseFilterEnum.Stop
            };
            gaugesContainer.AddChild(seaContainer);

            _barSea = new ProgressBar
            {
                Name = "BarSea",
                MinValue = 0,
                MaxValue = 100,
                Value = 0,
                ShowPercentage = false,
                Size = new Vector2(195f, 20f),
                MouseFilter = MouseFilterEnum.Stop
            };
            _barSea.AddThemeStyleboxOverride("background", (StyleBox)slotBgStyle.Duplicate());
            _barSea.AddThemeStyleboxOverride("fill", seaFillStyle);
            seaContainer.AddChild(_barSea);

            _lblSeaText = new Label
            {
                Name = "LblSeaText",
                Text = string.Empty,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Size = new Vector2(195f, 20f),
                Modulate = new Color(1f, 0.984f, 0.922f, 1f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            _lblSeaText.AddThemeFontSizeOverride("font_size", 11);
            _lblSeaText.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
            _lblSeaText.AddThemeConstantOverride("outline_size", 2);
            seaContainer.AddChild(_lblSeaText);

            // Spacer 1 (pushes banner toward center)
            var spacerLeft = new Control
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Ignore
            };
            topBarHBox.AddChild(spacerLeft);

            // =========================================================================
            // 2. TOP-CENTER: SLEEK DARK-LACQUER BANNER (REGION & SOLAR TERM)
            // =========================================================================
            var bannerBoxStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.047f, 0.078f, 0.078f, 0.96f), // Deep dark lacquer #0C1414
                BorderColor = new Color(0.851f, 0.467f, 0.024f, 0.60f), // Muted amber gold border #D97706
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 2,
                CornerRadiusBottomRight = 2,
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2
            };

            var bannerContainer = new PanelContainer
            {
                Name = "BannerContainer",
                CustomMinimumSize = new Vector2(440f, 48f),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
                MouseFilter = MouseFilterEnum.Stop
            };
            bannerContainer.AddThemeStyleboxOverride("panel", bannerBoxStyle);
            topBarHBox.AddChild(bannerContainer);

            var bannerVBox = new VBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            bannerContainer.AddChild(bannerVBox);

            _lblRegionTitle = new Label
            {
                Name = "LblRegionTitle",
                Text = string.Empty,
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = new Color(0.984f, 0.749f, 0.141f, 1f), // Imperial Amber Gold #FBBF24
                MouseFilter = MouseFilterEnum.Ignore
            };
            _lblRegionTitle.AddThemeFontSizeOverride("font_size", 12);
            bannerVBox.AddChild(_lblRegionTitle);

            _lblDateSubtitle = new Label
            {
                Name = "LblDateSubtitle",
                Text = string.Empty,
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = new Color(0.886f, 0.910f, 0.941f, 1f), // Soft Ivory White #E2E8F0
                MouseFilter = MouseFilterEnum.Ignore
            };
            _lblDateSubtitle.AddThemeFontSizeOverride("font_size", 11);
            bannerVBox.AddChild(_lblDateSubtitle);

            // Spacer 2 (pushes buttons toward right)
            var spacerRight = new Control
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Ignore
            };
            topBarHBox.AddChild(spacerRight);

            // =========================================================================
            // 3. TOP-RIGHT: CRIMSON PASS MONTH, SLATE SYSTEM, & REALM SUMMARY
            // =========================================================================
            var rightContainer = new VBoxContainer
            {
                Name = "RightContainer",
                Alignment = BoxContainer.AlignmentMode.End,
                SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
                MouseFilter = MouseFilterEnum.Stop
            };
            topBarHBox.AddChild(rightContainer);

            var buttonsHBox = new HBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.End,
                MouseFilter = MouseFilterEnum.Stop
            };
            buttonsHBox.AddThemeConstantOverride("separation", 4);
            rightContainer.AddChild(buttonsHBox);

            // Button Styles: Crimson Lacquer for Pass Month
            var passMonthNormal = new StyleBoxFlat
            {
                BgColor = new Color(0.231f, 0.067f, 0.082f, 0.98f), // Rich Crimson Lacquer #3B1115
                BorderColor = new Color(0.514f, 0.133f, 0.169f, 1f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 2,
                CornerRadiusBottomRight = 2,
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2
            };
            var passMonthHover = new StyleBoxFlat
            {
                BgColor = new Color(0.322f, 0.082f, 0.110f, 1f), // #52151C
                BorderColor = new Color(0.984f, 0.749f, 0.141f, 1f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 2,
                CornerRadiusBottomRight = 2,
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2
            };

            // Button Styles: Dark Slate for System & Seals
            var slateNormal = new StyleBoxFlat
            {
                BgColor = new Color(0.118f, 0.161f, 0.231f, 0.98f), // Dark Slate #1E293B
                BorderColor = new Color(0.200f, 0.255f, 0.333f, 1f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 2,
                CornerRadiusBottomRight = 2,
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2
            };
            var slateHover = new StyleBoxFlat
            {
                BgColor = new Color(0.200f, 0.255f, 0.333f, 1f), // #334155
                BorderColor = new Color(0.380f, 0.471f, 0.588f, 1f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 2,
                CornerRadiusBottomRight = 2,
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2
            };

            // Pass Month Button
            _btnPassMonth = new Button
            {
                Name = "BtnPassMonth",
                Text = "Pass Month",
                CustomMinimumSize = new Vector2(104f, 26f),
                MouseFilter = MouseFilterEnum.Stop,
                FocusMode = FocusModeEnum.None
            };
            _btnPassMonth.AddThemeStyleboxOverride("normal", passMonthNormal);
            _btnPassMonth.AddThemeStyleboxOverride("hover", passMonthHover);
            _btnPassMonth.AddThemeStyleboxOverride("pressed", passMonthHover);
            _btnPassMonth.AddThemeColorOverride("font_color", new Color(0.984f, 0.749f, 0.141f, 1f)); // Amber gold #FBBF24
            _btnPassMonth.AddThemeFontSizeOverride("font_size", 11);
            _btnPassMonth.Pressed += OnPassMonthPressed;
            buttonsHBox.AddChild(_btnPassMonth);

            // Cultivator Ledger Button
            var btnLedger = new Button
            {
                Name = "BtnLedger",
                Text = "【籍】 Ledger [Tab]",
                CustomMinimumSize = new Vector2(96f, 26f),
                MouseFilter = MouseFilterEnum.Stop,
                FocusMode = FocusModeEnum.None
            };
            btnLedger.AddThemeStyleboxOverride("normal", slateNormal);
            btnLedger.AddThemeStyleboxOverride("hover", slateHover);
            btnLedger.AddThemeStyleboxOverride("pressed", slateHover);
            btnLedger.AddThemeColorOverride("font_color", new Color(0.204f, 0.827f, 0.6f, 1f)); // Jade green #34D399
            btnLedger.AddThemeFontSizeOverride("font_size", 11);
            btnLedger.Pressed += () => WorldSignals.EmitToggleLedger();
            buttonsHBox.AddChild(btnLedger);

            // Five-Regions World Map Button
            var btnMap = new Button
            {
                Name = "BtnMap",
                Text = "【图】 Map [M]",
                CustomMinimumSize = new Vector2(86f, 26f),
                MouseFilter = MouseFilterEnum.Stop,
                FocusMode = FocusModeEnum.None
            };
            btnMap.AddThemeStyleboxOverride("normal", (StyleBox)slateNormal.Duplicate());
            btnMap.AddThemeStyleboxOverride("hover", (StyleBox)slateHover.Duplicate());
            btnMap.AddThemeStyleboxOverride("pressed", (StyleBox)slateHover.Duplicate());
            btnMap.AddThemeColorOverride("font_color", new Color(0.984f, 0.749f, 0.141f, 1f)); // Amber gold #FBBF24
            btnMap.AddThemeFontSizeOverride("font_size", 11);
            btnMap.Pressed += ToggleAtlasModal;
            buttonsHBox.AddChild(btnMap);

            // Alchemical Refinement Cauldron Button
            var btnCauldron = new Button
            {
                Name = "BtnCauldron",
                Text = "【炼】 Cauldron [C]",
                CustomMinimumSize = new Vector2(96f, 26f),
                MouseFilter = MouseFilterEnum.Stop,
                FocusMode = FocusModeEnum.None
            };
            btnCauldron.AddThemeStyleboxOverride("normal", slateNormal);
            btnCauldron.AddThemeStyleboxOverride("hover", slateHover);
            btnCauldron.AddThemeStyleboxOverride("pressed", slateHover);
            btnCauldron.AddThemeColorOverride("font_color", new Color(0.984f, 0.749f, 0.141f, 1f)); // Amber gold #FBBF24
            btnCauldron.AddThemeFontSizeOverride("font_size", 11);
            btnCauldron.Pressed += () =>
            {
                var world = _stateProvider?.Current;
                if (world == null) return;
                if (_modalStack?.IsModalOpen<RefinementCauldronView>() == true)
                {
                    RefinementSystem.CloseCauldron(world);
                }
                else
                {
                    RefinementSystem.OpenCauldron(world);
                }
            };
            buttonsHBox.AddChild(btnCauldron);

            // System Button
            var btnSystem = new Button
            {
                Name = "BtnSystem",
                Text = "System [Esc]",
                CustomMinimumSize = new Vector2(76f, 26f),
                MouseFilter = MouseFilterEnum.Stop,
                FocusMode = FocusModeEnum.None
            };
            btnSystem.AddThemeStyleboxOverride("normal", slateNormal);
            btnSystem.AddThemeStyleboxOverride("hover", slateHover);
            btnSystem.AddThemeStyleboxOverride("pressed", slateHover);
            btnSystem.AddThemeColorOverride("font_color", new Color(0.886f, 0.910f, 0.941f, 1f)); // Soft white #E2E8F0
            btnSystem.AddThemeFontSizeOverride("font_size", 11);
            btnSystem.Pressed += () => WorldSignals.EmitEscape();
            buttonsHBox.AddChild(btnSystem);

            // Cultivator Realm & Primeval Stone Badge (Two-tone BBCode matching Unity Image 2)
            _lblCultivatorBadge = new RichTextLabel
            {
                Name = "LblCultivatorBadge",
                BbcodeEnabled = true,
                FitContent = true,
                ScrollActive = false,
                CustomMinimumSize = new Vector2(200f, 18f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            _lblCultivatorBadge.AddThemeFontSizeOverride("normal_font_size", 11);
            rightContainer.AddChild(_lblCultivatorBadge);

            // Scenic / Landmark Lore Toast (Bottom Center)
            var toastMargin = new MarginContainer
            {
                Name = "ScenicToastMargin",
                MouseFilter = MouseFilterEnum.Ignore
            };
            toastMargin.SetAnchorsPreset(LayoutPreset.BottomWide);
            toastMargin.OffsetBottom = -30f;
            toastMargin.OffsetTop = -90f;
            toastMargin.OffsetLeft = 180f;
            toastMargin.OffsetRight = -180f;
            AddChild(toastMargin);

            var toastCenter = new CenterContainer
            {
                Name = "ToastCenter",
                MouseFilter = MouseFilterEnum.Ignore
            };
            toastMargin.AddChild(toastCenter);

            var toastStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.04f, 0.06f, 0.07f, 0.90f),
                BorderColor = new Color(0.18f, 0.70f, 0.45f, 0.85f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4
            };

            _scenicToastPanel = new PanelContainer
            {
                Name = "ScenicToastPanel",
                CustomMinimumSize = new Vector2(460f, 44f),
                MouseFilter = MouseFilterEnum.Ignore,
                Visible = false,
                Modulate = new Color(1f, 1f, 1f, 0f)
            };
            _scenicToastPanel.AddThemeStyleboxOverride("panel", toastStyle);
            toastCenter.AddChild(_scenicToastPanel);

            var toastPad = new MarginContainer();
            toastPad.AddThemeConstantOverride("margin_left", 14);
            toastPad.AddThemeConstantOverride("margin_right", 14);
            toastPad.AddThemeConstantOverride("margin_top", 8);
            toastPad.AddThemeConstantOverride("margin_bottom", 8);
            _scenicToastPanel.AddChild(toastPad);

            _lblScenicToast = new Label
            {
                Name = "LblScenicToast",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _lblScenicToast.AddThemeFontSizeOverride("font_size", 11);
            _lblScenicToast.AddThemeColorOverride("font_color", new Color(0.92f, 0.95f, 0.92f, 1f));
            toastPad.AddChild(_lblScenicToast);

            // =========================================================================
            // SEAL-STAMP EVENT BANNER (Top Center below TopBar, Ink-Crimson Animation)
            // =========================================================================
            var bannerMargin = new MarginContainer
            {
                Name = "EventBannerMargin",
                MouseFilter = MouseFilterEnum.Ignore
            };
            bannerMargin.SetAnchorsPreset(LayoutPreset.TopWide);
            bannerMargin.OffsetTop = 76f;
            bannerMargin.OffsetBottom = 136f;
            bannerMargin.OffsetLeft = 240f;
            bannerMargin.OffsetRight = -240f;
            AddChild(bannerMargin);

            var bannerCenter = new CenterContainer
            {
                Name = "BannerCenter",
                MouseFilter = MouseFilterEnum.Ignore
            };
            bannerMargin.AddChild(bannerCenter);

            var bannerStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.32f, 0.05f, 0.07f, 0.96f), // Rich lacquer ink-crimson #520D12
                BorderColor = new Color(0.984f, 0.749f, 0.141f, 0.95f), // Imperial gold #FBBF24
                BorderWidthBottom = 2,
                BorderWidthTop = 2,
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3
            };

            _eventBannerPanel = new PanelContainer
            {
                Name = "EventBannerPanel",
                CustomMinimumSize = new Vector2(480f, 52f),
                MouseFilter = MouseFilterEnum.Ignore,
                Visible = false,
                Modulate = new Color(1f, 1f, 1f, 0f),
                PivotOffset = new Vector2(240f, 26f)
            };
            _eventBannerPanel.AddThemeStyleboxOverride("panel", bannerStyle);
            bannerCenter.AddChild(_eventBannerPanel);

            var eventBannerPad = new MarginContainer();
            eventBannerPad.AddThemeConstantOverride("margin_left", 16);
            eventBannerPad.AddThemeConstantOverride("margin_right", 16);
            eventBannerPad.AddThemeConstantOverride("margin_top", 6);
            eventBannerPad.AddThemeConstantOverride("margin_bottom", 6);
            _eventBannerPanel.AddChild(eventBannerPad);

            var eventBannerVBox = new VBoxContainer
            {
                Alignment = BoxContainer.AlignmentMode.Center
            };
            eventBannerPad.AddChild(eventBannerVBox);

            _lblBannerSeal = new Label
            {
                Name = "LblBannerSeal",
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = new Color(0.984f, 0.749f, 0.141f, 1f) // Amber gold
            };
            _lblBannerSeal.AddThemeFontSizeOverride("font_size", 14);
            eventBannerVBox.AddChild(_lblBannerSeal);

            _lblBannerDetail = new Label
            {
                Name = "LblBannerDetail",
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = new Color(0.92f, 0.95f, 0.92f, 1f) // Ivory white
            };
            _lblBannerDetail.AddThemeFontSizeOverride("font_size", 11);
            eventBannerVBox.AddChild(_lblBannerDetail);
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventKey key && key.Pressed && !key.Echo)
            {
                if (key.Keycode == Key.M || key.PhysicalKeycode == Key.M || key.IsActionPressed("toggle_atlas"))
                {
                    if (key.CtrlPressed || key.AltPressed || key.MetaPressed || key.ShiftPressed) return;
                    Control? focusOwner = GetViewport().GuiGetFocusOwner();
                    if (focusOwner is LineEdit || focusOwner is TextEdit) return;

                    ToggleAtlasModal();
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        private void ToggleAtlasModal()
        {
            if (_atlasModal == null)
            {
                _atlasModal = WorldAtlasModalView.Instance;
                if (_atlasModal == null)
                {
                    _atlasModal = GetTree().Root.FindChild("WorldAtlasModal", true, false) as WorldAtlasModalView;
                    if (_atlasModal == null)
                    {
                        var packed = GD.Load<PackedScene>("res://Scenes/UI/WorldAtlasModal.tscn");
                        if (packed != null)
                        {
                            _atlasModal = packed.Instantiate<WorldAtlasModalView>();
                            var uiRoot = GetParent() ?? this;
                            uiRoot.AddChild(_atlasModal);
                        }
                    }
                }

                if (_atlasModal != null && _modalStack != null && _stateProvider != null)
                {
                    _atlasModal.Initialize(_modalStack, _stateProvider);
                }
            }

            if (_atlasModal != null)
            {
                _atlasModal.Toggle();
            }
        }

        private void ShowSealStampBanner(string seal, string detail)
        {
            if (_eventBannerPanel == null || _lblBannerSeal == null || _lblBannerDetail == null) return;

            _lblBannerSeal.Text = seal;
            _lblBannerDetail.Text = detail;
            _eventBannerPanel.Visible = true;

            _bannerTween?.Kill();
            _eventBannerPanel.Modulate = new Color(1f, 1f, 1f, 0f);
            _eventBannerPanel.Scale = new Vector2(1.15f, 1.15f);

            _bannerTween = CreateTween();
            _bannerTween.SetParallel(true);
            _bannerTween.TweenProperty(_eventBannerPanel, "modulate:a", 1.0f, 0.22f);
            _bannerTween.TweenProperty(_eventBannerPanel, "scale", Vector2.One, 0.22f).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            _bannerTween.Chain().TweenInterval(4.0f);
            _bannerTween.Chain().TweenProperty(_eventBannerPanel, "modulate:a", 0.0f, 0.5f);
            _bannerTween.TweenCallback(Callable.From(() =>
            {
                if (_eventBannerPanel != null) _eventBannerPanel.Visible = false;
            }));
        }

        private void HandleWorldCalamityUpdated(WorldEventEntry entry)
        {
            if (entry == null) return;
            string seal = entry.Kind switch
            {
                WorldCalamityKind.BeastTide => "【兽潮降临】",
                WorldCalamityKind.InterClanSkirmish => "【宗门征伐】",
                WorldCalamityKind.InheritanceRuins => "【仙府出世】",
                WorldCalamityKind.GrandCaravanAuction => "【万商大集】",
                _ => "【天地灾异】"
            };
            string detail = $"{entry.Title} (Phase: {entry.Phase}) — {entry.Description}";
            ShowSealStampBanner(seal, detail);
        }

        private void HandleBountyHunterAmbush(BountyHunterRecord hunter, GridCoord pos)
        {
            if (hunter == null) return;
            string seal = hunter.IssuingFaction == FactionID.TieClan ? "【铁家追捕】" : $"【{hunter.IssuingFaction}追捕】";
            string detail = $"{hunter.HunterName} (Threat Rank {hunter.ThreatRank}) closing in at {pos}!";
            ShowSealStampBanner(seal, detail);
        }

        private void HandleBreakthroughResolved(BreakthroughState state, int newRank)
        {
            if (state == BreakthroughState.Succeeded)
            {
                ShowSealStampBanner("【破境大成】", $"Ascended to Rank {newRank} Supreme Aperture Realm!");
            }
        }

        private void OnPassMonthPressed()
        {
            var world = _stateProvider?.Current;
            if (world != null) TimeSystem.RollOverMonth(world);
        }

        private void HandleTopicsChanged(DirtyTopic topics)
        {
            if ((topics & (DirtyTopic.Calendar | DirtyTopic.Cultivator | DirtyTopic.Aperture | DirtyTopic.Map | DirtyTopic.Director | DirtyTopic.Bounty)) != 0)
            {
                RefreshFromStore();
            }
        }

        private void HandleTileInspected(string title, string seal, string lore, GridCoord pos)
        {
            ShowToast($"【{seal}】 {title}\n{lore}");
        }

        private void HandleTerrainHazard(GridCoord pos, string hazardName, int hpDmg, float seaDmg)
        {
            ShowToast($"【危】 Terrain Hazard: {hazardName}\nHP -{hpDmg} | Sea -{seaDmg:F1}%");
        }

        private void HandleRoadAmbush(ArenaEncounterType encounter, GridCoord pos)
        {
            ShowToast($"【伏击】 Mountain Road Ambush ({encounter})\nTransit paused pending combat resolution at {pos}.");
        }

        private void HandleBattleStageChanged(bool inBattle)
        {
            if (_topBarMargin != null) _topBarMargin.Visible = !inBattle;
        }

        private void HandleCombatResolved(ArenaEncounterType encounter, CombatOutcome outcome, string message)
        {
            string seal = outcome switch
            {
                CombatOutcome.Victory => "胜",
                CombatOutcome.Defeat => "败",
                _ => "退"
            };
            ShowToast($"【{seal}】 {message}");
        }

        private void ShowToast(string text)
        {
            if (_scenicToastPanel == null || _lblScenicToast == null) return;

            _lblScenicToast.Text = text;
            _scenicToastPanel.Visible = true;

            _toastTween?.Kill();
            _toastTween = CreateTween();
            _toastTween.TweenProperty(_scenicToastPanel, "modulate:a", 1.0f, 0.25f);
            _toastTween.TweenInterval(3.5f);
            _toastTween.TweenProperty(_scenicToastPanel, "modulate:a", 0.0f, 0.6f);
            _toastTween.TweenCallback(Callable.From(() =>
            {
                if (_scenicToastPanel != null) _scenicToastPanel.Visible = false;
            }));
        }

        private void RefreshFromStore()
        {
            var world = _stateProvider?.Current;
            if (world == null) return;

            var clock = world.GetSingleton<WorldClockComponent>();
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity);
            var ap = world.Get<ApertureComponent>(world.PlayerEntity);
            var sector = world.GetCurrentSector();

            int currentAP = clock?.ActionPointsRemaining ?? 0;
            int maxAP = clock?.MonthlyAPBudget ?? 30;
            if (_barAP != null)
            {
                _barAP.MaxValue = Math.Max(1, maxAP);
                _barAP.Value = currentAP;
            }
            if (_lblAPText != null) _lblAPText.Text = $"AP: {currentAP}/{maxAP}";
            if (_btnPassMonth != null) _btnPassMonth.Text = $"Pass Month ({currentAP} AP)";

            float currentSea = ap?.CurrentEssenceSea ?? 0f;
            float maxSea = cult?.AptitudePercent ?? 44f;
            if (_barSea != null)
            {
                _barSea.MaxValue = Math.Max(0.01f, maxSea);
                _barSea.Value = currentSea;
            }
            if (_lblSeaText != null) _lblSeaText.Text = $"Sea: {currentSea:F1}% / {maxSea:F1}%";

            if (_lblDateSubtitle != null && clock != null)
            {
                _lblDateSubtitle.Text = $"Year {clock.CurrentYear} | {clock.GetSolarTermName()}";
            }

            if (_lblRegionTitle != null && sector != null)
            {
                _lblRegionTitle.Text = $"{RegionalWallLaw.GetRegionName(sector.Region)}: {sector.DisplayName}";
            }

            if (_lblCultivatorBadge != null && ap != null && cult != null)
            {
                _lblCultivatorBadge.Text = $"[right][color=#34D399]Rank {ap.Rank} {ap.GetSubstageName()} | [/color][color=#E2E8F0]🪨 {cult.PrimevalStones}[/color][/right]";
            }
        }
    }
}
