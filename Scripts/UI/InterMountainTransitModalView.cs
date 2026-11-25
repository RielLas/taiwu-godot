using System;
using System.Collections.Generic;
using Godot;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Map;
using TaiwuGu.Systems;
using Color = Godot.Color;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Phase 2 / Milestone 2.6: Inter-Mountain & Regional Transit Modal (十里凉亭行驿 / 五域界壁).
    /// Direct-read presentation observer wired to WorldStore & TransitSystem.
    /// </summary>
    public partial class InterMountainTransitModalView : CanvasLayer, IModalView
    {
        private IWorldStateProvider? _stateProvider;
        private bool _isOpen = false;
        private bool _isRegionalMode = false;
        private bool _sealAperture = false;

        private Control? _modalContainer;
        private Label? _lblTitle;
        private Label? _lblLocationInfo;
        private Label? _lblCostInfo;
        private CheckBox? _chkSealAperture;
        private Label? _lblRankWarning;
        private VBoxContainer? _destinationsContainer;
        private Button? _btnTabMountain;
        private Button? _btnTabRegional;

        private Texture2D? _texBannerMountains;

        public bool IsOpen => _isOpen;

        public void Initialize(IWorldStateProvider? stateProvider)
        {
            _stateProvider = stateProvider;
        }

        public void Dismiss()
        {
            if (_stateProvider?.Current is { } world)
                TransitSystem.CloseTransitModal(world);
        }

        public override void _EnterTree()
        {
            Layer = 16;
            Visible = false;
        }

        public override void _Ready()
        {
            _texBannerMountains = ResourceLoader.Load<Texture2D>("res://Resources/UI/banner_ink_mountains.png");
            BuildUI();

            WorldSignals.OnTransitModalToggled += HandleTransitModalToggled;
            WorldSignals.OnTopicsChanged += HandleTopicsChanged;
            WorldSignals.OnEscapeRequested += HandleEscapeRequested;
        }

        public override void _ExitTree()
        {
            WorldSignals.OnTransitModalToggled -= HandleTransitModalToggled;
            WorldSignals.OnTopicsChanged -= HandleTopicsChanged;
            WorldSignals.OnEscapeRequested -= HandleEscapeRequested;

            if (_isOpen)
            {
                _isOpen = false;
                WorldSignals.EmitModalClosed(this);
            }
        }

        private void BuildUI()
        {
            var backdrop = new ColorRect
            {
                Name = "BackdropDimmer",
                Color = new Color(0.02f, 0.04f, 0.04f, 0.82f),
                AnchorRight = 1f,
                AnchorBottom = 1f,
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            AddChild(backdrop);

            var centerContainer = new CenterContainer
            {
                AnchorRight = 1f,
                AnchorBottom = 1f,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            AddChild(centerContainer);

            _modalContainer = new PanelContainer
            {
                Name = "TransitModalPanel",
                CustomMinimumSize = new Vector2(680f, 520f),
                MouseFilter = Control.MouseFilterEnum.Stop
            };

            var modalStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.06f, 0.10f, 0.09f, 0.98f),
                BorderColor = new Color(0.85f, 0.65f, 0.20f, 0.90f),
                BorderWidthBottom = 2,
                BorderWidthTop = 2,
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4
            };
            _modalContainer.AddThemeStyleboxOverride("panel", modalStyle);
            centerContainer.AddChild(_modalContainer);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 20);
            margin.AddThemeConstantOverride("margin_right", 20);
            margin.AddThemeConstantOverride("margin_top", 14);
            margin.AddThemeConstantOverride("margin_bottom", 14);
            _modalContainer.AddChild(margin);

            var rootVBox = new VBoxContainer();
            rootVBox.AddThemeConstantOverride("separation", 6);
            margin.AddChild(rootVBox);

            if (_texBannerMountains != null)
            {
                var banner = new TextureRect
                {
                    Texture = _texBannerMountains,
                    CustomMinimumSize = new Vector2(0f, 40f),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.Scale
                };
                rootVBox.AddChild(banner);
            }

            _lblTitle = new Label
            {
                Text = "◆ MOUNTAIN TRANSIT WAY STATION (十里凉亭行驿) ◆",
                Modulate = new Color(0.22f, 0.74f, 0.97f, 1f),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _lblTitle.AddThemeFontSizeOverride("font_size", 15);
            rootVBox.AddChild(_lblTitle);

            _lblLocationInfo = new Label
            {
                Text = "Current Location: Gu Yue Mountain (古月山寨) | Southern Border (南疆)",
                Modulate = new Color(0.984f, 0.749f, 0.141f, 1f),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _lblLocationInfo.AddThemeFontSizeOverride("font_size", 11);
            rootVBox.AddChild(_lblLocationInfo);

            _lblCostInfo = new Label
            {
                Text = "Transit Toll: 25 Stones + 6 Days (AP) | Ambush Risk: 25%",
                Modulate = new Color(0.85f, 0.88f, 0.90f, 1f),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _lblCostInfo.AddThemeFontSizeOverride("font_size", 11);
            rootVBox.AddChild(_lblCostInfo);

            // Tab bar for switching between Mountain and Regional transit
            var tabHBox = new HBoxContainer();
            tabHBox.AddThemeConstantOverride("separation", 8);
            rootVBox.AddChild(tabHBox);

            _btnTabMountain = new Button
            {
                Text = "Qing Mao Mountain Sectors (十里行驿)",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0f, 28f)
            };
            _btnTabMountain.Pressed += () => SwitchMode(false);
            tabHBox.AddChild(_btnTabMountain);

            _btnTabRegional = new Button
            {
                Text = "Five Regions Regional Wall Crossing (五域界壁)",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0f, 28f)
            };
            _btnTabRegional.Pressed += () => SwitchMode(true);
            tabHBox.AddChild(_btnTabRegional);

            // Restraint Gu CheckBox for Regional crossing
            _chkSealAperture = new CheckBox
            {
                Text = " [RESTRAINT GU APERTURE SEALING (敛息封窍)] Suppress Sea to 0% to avoid Dao backlash (Recommended for Rank 4-5)",
                Visible = false
            };
            _chkSealAperture.Toggled += (on) =>
            {
                _sealAperture = on;
                UpdateRankWarning();
            };
            rootVBox.AddChild(_chkSealAperture);

            _lblRankWarning = new Label
            {
                Text = "⚠ WARNING: High-rank brute-force crossing triggers severe wall backlash!",
                Modulate = new Color(0.937f, 0.267f, 0.267f, 1f),
                HorizontalAlignment = HorizontalAlignment.Center,
                Visible = false
            };
            _lblRankWarning.AddThemeFontSizeOverride("font_size", 11);
            rootVBox.AddChild(_lblRankWarning);

            // Scroll container for destination cards
            var scroll = new ScrollContainer
            {
                CustomMinimumSize = new Vector2(0f, 240f),
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            rootVBox.AddChild(scroll);

            _destinationsContainer = new VBoxContainer();
            _destinationsContainer.AddThemeConstantOverride("separation", 6);
            _destinationsContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            scroll.AddChild(_destinationsContainer);

            // Bottom Close Button
            var btnClose = new Button
            {
                Text = "Close Transit Station (离开驿站) [Esc]",
                CustomMinimumSize = new Vector2(200f, 32f),
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
            };
            btnClose.Pressed += Dismiss;
            rootVBox.AddChild(btnClose);
        }

        private void SwitchMode(bool regionalMode)
        {
            _isRegionalMode = regionalMode;
            RefreshUI();
        }

        private void HandleTransitModalToggled(bool isOpen)
        {
            if (_isOpen == isOpen) return;
            _isOpen = isOpen;
            if (isOpen && _stateProvider?.Current is { } world)
            {
                _isRegionalMode = TransitSystem.GetOrCreateSession(world).IsRegionalMode;
            }
            Visible = isOpen;

            if (isOpen)
            {
                WorldSignals.EmitModalOpened(this);
                RefreshUI();
            }
            else
            {
                WorldSignals.EmitModalClosed(this);
            }
        }

        private void HandleTopicsChanged(DirtyTopic topics)
        {
            if (_isOpen && (topics & (DirtyTopic.Transit | DirtyTopic.Cultivator | DirtyTopic.Aperture | DirtyTopic.Calendar | DirtyTopic.Map)) != 0)
            {
                RefreshUI();
            }
        }

        private void HandleEscapeRequested()
        {
            if (!_isOpen || WorldSignals.EscapeHandled) return;

            Dismiss();
            WorldSignals.EscapeHandled = true;
        }

        private void UpdateRankWarning()
        {
            int rank = _stateProvider?.Current is { } world
                ? (world.Get<ApertureComponent>(world.PlayerEntity)?.Rank ?? 1)
                : 1;

            if (_isRegionalMode && rank >= 4 && !_sealAperture)
            {
                _lblRankWarning!.Visible = true;
                _lblRankWarning.Text = rank == 5
                    ? "☠ CATASTROPHIC BACKLASH: Rank 5 mortal crossing without Restraint Gu inflicts -50% HP, -60% Wall Durability, drains Sea!"
                    : "⚠ SEVERE BACKLASH: Rank 4 crossing without Restraint Gu inflicts -30% Wall Durability, -75% Sea!";
            }
            else
            {
                _lblRankWarning!.Visible = false;
            }
        }

        public void RefreshUI()
        {
            var world = _stateProvider?.Current;
            if (world == null) return;

            var sector = world.GetCurrentSector();
            WorldRegionID curRegion = sector?.Region ?? WorldRegionID.SouthernBorder;
            SouthernMountainID curMountain = sector?.MountainId ?? SouthernMountainID.GuYueVillage;

            if (_chkSealAperture != null) _chkSealAperture.Visible = _isRegionalMode;
            UpdateRankWarning();

            if (!_isRegionalMode)
            {
                _lblTitle!.Text = "◆ MOUNTAIN TRANSIT WAY STATION (十里凉亭行驿) ◆";
                _lblTitle.Modulate = new Color(0.22f, 0.74f, 0.97f, 1f);
                string mName = TransitSystem.MountainDatabase.TryGetValue(curMountain, out var curM) ? curM.MountainName : "Gu Yue Mountain";
                _lblLocationInfo!.Text = $"Current Location: {mName} | {RegionalWallLaw.GetRegionName(curRegion)}";
                _lblCostInfo!.Text = $"Transit Toll: {TransitSystem.TRAVEL_STONE_COST} Stones + {TransitSystem.TRAVEL_AP_COST} Days (AP) | Ambush Risk: 25%";

                PopulateMountainDestinations(world, curMountain, curRegion);
            }
            else
            {
                _lblTitle!.Text = "◆ FIVE REGIONS REGIONAL WALL CROSSING (五域界壁跨域大阵) ◆";
                _lblTitle.Modulate = new Color(0.76f, 0.48f, 0.98f, 1f);
                _lblLocationInfo!.Text = $"Current Active Region: {RegionalWallLaw.GetRegionName(curRegion)}";
                _lblCostInfo!.Text = $"Caravan Toll: {TransitSystem.REGIONAL_TRANSIT_STONE_COST} Stones + {TransitSystem.REGIONAL_TRANSIT_AP_COST} Days (AP)";

                PopulateRegionalDestinations(world, curRegion);
            }
        }

        private void PopulateMountainDestinations(WorldStore world, SouthernMountainID currentMountain, WorldRegionID currentRegion)
        {
            if (_destinationsContainer == null) return;
            foreach (Node child in _destinationsContainer.GetChildren()) child.QueueFree();

            foreach (var kvp in TransitSystem.MountainDatabase)
            {
                var mountain = kvp.Value;
                bool isCurrent = mountain.MountainID == currentMountain && currentRegion == WorldRegionID.SouthernBorder;
                bool canTravel = TransitSystem.CanTravelToMountain(world, mountain.MountainID, out string failReason);

                string costText = canTravel
                    ? $"Travel (-{TransitSystem.TRAVEL_STONE_COST} Stones, -{TransitSystem.TRAVEL_AP_COST} AP)"
                    : $"Blocked [{failReason}]";

                var card = CreateDestinationCard(
                    mountain.MountainName,
                    isCurrent ? "[CURRENT MOUNTAIN]" : "Qing Mao Mountain Sector",
                    isCurrent,
                    mountain.Description,
                    canTravel,
                    costText,
                    new Color(0.05f, 0.58f, 0.41f, 1f),
                    () => TransitSystem.TravelToMountain(world, mountain.MountainID)
                );

                _destinationsContainer.AddChild(card);
            }
        }

        private void PopulateRegionalDestinations(WorldStore world, WorldRegionID currentRegion)
        {
            if (_destinationsContainer == null) return;
            foreach (Node child in _destinationsContainer.GetChildren()) child.QueueFree();

            foreach (var kvp in TransitSystem.RegionalDatabase)
            {
                var reg = kvp.Value;
                bool isCurrent = reg.RegionID == currentRegion;
                bool canTravel = TransitSystem.CanTravelToRegion(world, reg.RegionID, out string failReason);

                string wallTitle = RegionalWallLaw.GetWallName(reg.WallType);
                string costText = canTravel
                    ? (_sealAperture ? "Cross Wall (Sealed)" : "Cross Wall (Brute)")
                    : $"Locked [{failReason}]";

                var card = CreateDestinationCard(
                    $"{reg.RegionName} — {wallTitle}",
                    isCurrent ? "[CURRENT REGION]" : "Boundary Wall Corridor",
                    isCurrent,
                    reg.LoreDescription,
                    canTravel,
                    costText,
                    new Color(0.48f, 0.22f, 0.92f, 1f),
                    () => TransitSystem.TravelToRegion(world, reg.RegionID, _sealAperture)
                );

                _destinationsContainer.AddChild(card);
            }
        }

        /// <summary>
        /// Loop helper to create uniform destination cards cleanly without layout duplication.
        /// </summary>
        private PanelContainer CreateDestinationCard(
            string title,
            string badgeText,
            bool isCurrent,
            string description,
            bool canTravel,
            string actionButtonText,
            Color buttonColor,
            Action onTravelPressed)
        {
            var cardPanel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(0f, 62f),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };

            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.04f, 0.07f, 0.07f, 0.92f),
                BorderColor = isCurrent ? new Color(0.204f, 0.827f, 0.600f, 0.8f) : new Color(0.25f, 0.35f, 0.35f, 0.6f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3
            };
            cardPanel.AddThemeStyleboxOverride("panel", style);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 12);
            margin.AddThemeConstantOverride("margin_right", 12);
            margin.AddThemeConstantOverride("margin_top", 8);
            margin.AddThemeConstantOverride("margin_bottom", 8);
            cardPanel.AddChild(margin);

            var vBox = new VBoxContainer();
            vBox.AddThemeConstantOverride("separation", 4);
            margin.AddChild(vBox);

            var topRow = new HBoxContainer();
            vBox.AddChild(topRow);

            var lblTitle = new Label
            {
                Text = title,
                Modulate = new Color(0.984f, 0.749f, 0.141f, 1f),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            lblTitle.AddThemeFontSizeOverride("font_size", 12);
            topRow.AddChild(lblTitle);

            if (isCurrent)
            {
                var lblBadge = new Label
                {
                    Text = badgeText,
                    Modulate = new Color(0.204f, 0.827f, 0.600f, 1f)
                };
                lblBadge.AddThemeFontSizeOverride("font_size", 11);
                topRow.AddChild(lblBadge);
            }
            else
            {
                var btnTravel = new Button
                {
                    Text = actionButtonText,
                    CustomMinimumSize = new Vector2(170f, 26f),
                    Disabled = !canTravel
                };
                btnTravel.AddThemeColorOverride("font_color", canTravel ? Colors.White : new Color(0.6f, 0.6f, 0.6f, 1f));
                btnTravel.Pressed += onTravelPressed;
                topRow.AddChild(btnTravel);
            }

            var lblDesc = new Label
            {
                Text = description,
                Modulate = new Color(0.70f, 0.75f, 0.80f, 1f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            lblDesc.AddThemeFontSizeOverride("font_size", 10);
            vBox.AddChild(lblDesc);

            return cardPanel;
        }
    }
}
