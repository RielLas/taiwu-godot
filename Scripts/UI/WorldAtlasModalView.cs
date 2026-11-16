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
    /// Phase 3: Five-Regions World Atlas Modal (五域万象天图).
    /// Pure presentation view governed by TaiwuModalStack and ModalScaffold.
    /// Features:
    /// - Sacred-geometry Five-Regions node-graph displaying all 17 sectors from WorldAtlasRegistry.
    /// - Displays Boundary Wall arcs, active WorldDirectorSystem Calamity beacons, and closing Bounty Hunters.
    /// - Sector inspection: Danger Floor, Native Factions, Calamities, and Boundary Wall Backlash preview.
    /// - Explicit Travel confirmation when transit requirements (Stones, AP) are satisfied.
    /// </summary>
    public partial class WorldAtlasModalView : Control, IModalView
    {
        public static WorldAtlasModalView? Instance { get; private set; }

        private const string ModalTitle = "◆ Five Regions Heavenly World Atlas (五域万象天图) ◆";

        [Export] public ModalScaffold? Scaffold { get; set; }

        // Region Node Containers
        [Export] public VBoxContainer? CentralContinentNodes { get; set; }
        [Export] public VBoxContainer? SouthernBorderNodes { get; set; }
        [Export] public VBoxContainer? NorthernPlainsNodes { get; set; }
        [Export] public VBoxContainer? WesternDesertNodes { get; set; }
        [Export] public VBoxContainer? EasternSeaNodes { get; set; }

        // Detail Inspector UI References
        [Export] public Label? LblSectorTitle { get; set; }
        [Export] public Label? LblRegionAndBiome { get; set; }
        [Export] public Label? LblDangerFloor { get; set; }
        [Export] public Label? LblNativeFaction { get; set; }
        [Export] public Label? LblNativePaths { get; set; }
        [Export] public Label? LblCalamityBeacon { get; set; }
        [Export] public Label? LblBountyHunters { get; set; }
        [Export] public Label? LblBacklashPreview { get; set; }
        [Export] public CheckBox? ChkSealAperture { get; set; }
        [Export] public Button? BtnTravel { get; set; }
        [Export] public Label? LblTransitCost { get; set; }
        [Export] public Label? LblFeedback { get; set; }

        private TaiwuModalStack? _modalStack;
        private IWorldStateProvider? _stateProvider;
        private bool _isOpen = false;
        private string? _selectedSectorKey;

        private readonly Dictionary<string, Button> _sectorButtons = new(StringComparer.Ordinal);

        public bool IsOpen => _isOpen;

        public void Initialize(TaiwuModalStack modalStack, IWorldStateProvider stateProvider)
        {
            _modalStack = modalStack;
            _stateProvider = stateProvider;
            Scaffold?.BindModalStack(modalStack);
        }

        public override void _EnterTree()
        {
            Instance = this;
        }

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;
            Visible = false;

            if (Scaffold != null)
            {
                Scaffold.BindModalStack(_modalStack);
                if (Scaffold.Panel != null)
                {
                    Scaffold.Panel.CustomMinimumSize = new Vector2(960f, 620f);
                }
            }

            if (BtnTravel != null)
            {
                BtnTravel.FocusMode = FocusModeEnum.None;
                BtnTravel.Pressed += OnTravelPressed;
            }

            if (ChkSealAperture != null)
            {
                ChkSealAperture.FocusMode = FocusModeEnum.None;
                ChkSealAperture.Toggled += _ => UpdateTransitRequirements();
            }

            PopulateSectorGraph();

            WorldSignals.OnTopicsChanged += HandleTopicsChanged;
            WorldSignals.OnBattleStageChanged += HandleBattleStageChanged;
        }

        public override void _ExitTree()
        {
            WorldSignals.OnTopicsChanged -= HandleTopicsChanged;
            WorldSignals.OnBattleStageChanged -= HandleBattleStageChanged;

            if (Instance == this)
            {
                Instance = null;
            }

            if (_isOpen)
            {
                CloseAtlas();
            }
        }

        public void Toggle()
        {
            if (_isOpen) Dismiss();
            else OpenModal();
        }

        public void OpenModal()
        {
            if (_isOpen || Scaffold == null) return;

            _isOpen = true;
            Visible = true;
            Scaffold.BindModalStack(_modalStack);
            Scaffold.Open(this, ModalTitle);

            var world = _stateProvider?.Current;
            _selectedSectorKey = world?.CurrentSectorKey ?? "Sector_SouthernBorder_GuYueVillage";

            RefreshAll();
        }

        public void Dismiss() => CloseAtlas();

        private void CloseAtlas()
        {
            if (!_isOpen) return;

            _isOpen = false;
            Visible = false;
            Scaffold?.Close();
        }

        private void HandleTopicsChanged(DirtyTopic topics)
        {
            if (_isOpen && (topics & (DirtyTopic.Map | DirtyTopic.Director | DirtyTopic.Bounty | DirtyTopic.Transit | DirtyTopic.Cultivator)) != 0)
            {
                RefreshAll();
            }
        }

        private void HandleBattleStageChanged(bool inBattle)
        {
            if (inBattle && _isOpen)
            {
                CloseAtlas();
            }
        }

        private void PopulateSectorGraph()
        {
            _sectorButtons.Clear();

            foreach (var bp in WorldAtlasRegistry.GetAll())
            {
                var container = GetContainerForRegion(bp.Region);
                if (container == null) continue;

                var btn = new Button
                {
                    Name = $"BtnSector_{bp.SectorKey}",
                    CustomMinimumSize = new Vector2(165f, 32f),
                    FocusMode = FocusModeEnum.None,
                    Text = bp.DisplayName
                };
                btn.AddThemeFontSizeOverride("font_size", 10);

                string key = bp.SectorKey;
                btn.Pressed += () => SelectSector(key);

                container.AddChild(btn);
                _sectorButtons[key] = btn;
            }
        }

        private VBoxContainer? GetContainerForRegion(WorldRegionID region)
        {
            return region switch
            {
                WorldRegionID.CentralContinent => CentralContinentNodes,
                WorldRegionID.SouthernBorder   => SouthernBorderNodes,
                WorldRegionID.NorthernPlains   => NorthernPlainsNodes,
                WorldRegionID.WesternDesert    => WesternDesertNodes,
                WorldRegionID.EasternSea       => EasternSeaNodes,
                _                              => SouthernBorderNodes
            };
        }

        private void SelectSector(string sectorKey)
        {
            _selectedSectorKey = sectorKey;
            RefreshDetailPanel();
        }

        public void RefreshAll()
        {
            var world = _stateProvider?.Current;
            if (world == null) return;

            var eventComp = world.GetSingleton<WorldEventComponent>();
            var bountyComp = world.GetSingleton<BountyTrackerComponent>();
            string currentSector = world.CurrentSectorKey;

            foreach (var (key, btn) in _sectorButtons)
            {
                if (!WorldAtlasRegistry.TryGet(key, out var bp) || bp == null) continue;

                bool isCurrent = string.Equals(key, currentSector, StringComparison.Ordinal);
                bool hasCalamity = false;
                bool hasHunter = false;

                if (eventComp != null)
                {
                    foreach (var evt in eventComp.ActiveEvents)
                    {
                        if (evt.SectorKey == key && evt.Phase != WorldCalamityPhase.Expired)
                        {
                            hasCalamity = true;
                            break;
                        }
                    }
                }

                if (bountyComp != null)
                {
                    foreach (var h in bountyComp.Hunters)
                    {
                        if (h.CurrentSectorKey == key && h.State == BountyHunterState.Pursuing)
                        {
                            hasHunter = true;
                            break;
                        }
                    }
                }

                string prefix = "";
                if (isCurrent) prefix = "★ ";
                else if (hasCalamity) prefix = "⚠ ";
                else if (hasHunter) prefix = "⚔ ";

                btn.Text = $"{prefix}{bp.DisplayName} [R{bp.DangerRankFloor}]";

                if (isCurrent)
                {
                    btn.Modulate = new Color(0.204f, 0.827f, 0.6f, 1f); // Jade green
                }
                else if (hasCalamity)
                {
                    btn.Modulate = new Color(0.984f, 0.35f, 0.2f, 1f); // Vermilion calamity
                }
                else if (hasHunter)
                {
                    btn.Modulate = new Color(0.984f, 0.749f, 0.141f, 1f); // Amber warning
                }
                else
                {
                    btn.Modulate = new Color(0.85f, 0.88f, 0.92f, 1f);
                }
            }

            RefreshDetailPanel();
        }

        private void RefreshDetailPanel()
        {
            var world = _stateProvider?.Current;
            if (world == null || string.IsNullOrEmpty(_selectedSectorKey)) return;

            if (!WorldAtlasRegistry.TryGet(_selectedSectorKey, out var bp) || bp == null) return;

            if (LblSectorTitle != null)
            {
                LblSectorTitle.Text = $"【{bp.DisplayName}】";
            }

            if (LblRegionAndBiome != null)
            {
                string regionName = RegionalWallLaw.GetRegionName(bp.Region);
                LblRegionAndBiome.Text = $"Region: {regionName}  |  Biome: {bp.Biome}";
            }

            if (LblDangerFloor != null)
            {
                LblDangerFloor.Text = $"Danger Floor: Rank {bp.DangerRankFloor}+  (Max Wild Gu: Rank {bp.MaxWildGuRank})";
            }

            if (LblNativeFaction != null)
            {
                string factionStr = bp.NativeFaction.HasValue ? bp.NativeFaction.Value.ToString() : "Neutral / Independent (无统辖)";
                LblNativeFaction.Text = $"Governing Clan / Faction: {factionStr}";
            }

            if (LblNativePaths != null)
            {
                string paths = bp.NativeGuPaths.Count > 0 ? string.Join(", ", bp.NativeGuPaths) : "None (诸道杂居)";
                LblNativePaths.Text = $"Native Gu Dao Marks: {paths}";
            }

            // Calamity Beacon Check
            var eventComp = world.GetSingleton<WorldEventComponent>();
            string calamityInfo = "None (四海升平)";
            if (eventComp != null)
            {
                foreach (var evt in eventComp.ActiveEvents)
                {
                    if (evt.SectorKey == _selectedSectorKey && evt.Phase != WorldCalamityPhase.Expired)
                    {
                        calamityInfo = $"【{evt.Kind}】 {evt.Title} (Phase: {evt.Phase}, Remaining: {evt.RemainingTerms} terms)\n{evt.Description}";
                        break;
                    }
                }
            }
            if (LblCalamityBeacon != null)
            {
                LblCalamityBeacon.Text = $"Active Calamity Beacon: {calamityInfo}";
            }

            // Bounty Hunter Check
            var bountyComp = world.GetSingleton<BountyTrackerComponent>();
            string hunterInfo = "None (无人跟踪)";
            if (bountyComp != null)
            {
                foreach (var h in bountyComp.Hunters)
                {
                    if (h.CurrentSectorKey == _selectedSectorKey && h.State == BountyHunterState.Pursuing)
                    {
                        hunterInfo = $"【{h.IssuingFaction}】 {h.HunterName} (Threat Rank {h.ThreatRank}, Bribe: {h.BribeCostStones} stones)";
                        break;
                    }
                }
            }
            if (LblBountyHunters != null)
            {
                LblBountyHunters.Text = $"Closing Bounty Hunters: {hunterInfo}";
            }

            UpdateTransitRequirements();
        }

        private void UpdateTransitRequirements()
        {
            var world = _stateProvider?.Current;
            if (world == null || string.IsNullOrEmpty(_selectedSectorKey)) return;

            if (!WorldAtlasRegistry.TryGet(_selectedSectorKey, out var bp) || bp == null) return;

            string currentSector = world.CurrentSectorKey;
            bool isCurrent = string.Equals(_selectedSectorKey, currentSector, StringComparison.Ordinal);

            var cult = world.Get<CultivatorComponent>(world.PlayerEntity);
            var apComp = world.Get<ApertureComponent>(world.PlayerEntity);
            var clock = world.GetSingleton<WorldClockComponent>();

            int stones = cult?.PrimevalStones ?? 0;
            int currentAP = clock?.ActionPointsRemaining ?? 0;
            int playerRank = apComp?.Rank ?? 1;

            WorldRegionID currentRegion = world.GetCurrentSector()?.Region ?? WorldRegionID.SouthernBorder;
            bool isRegionalCross = bp.Region != currentRegion;

            if (isCurrent)
            {
                if (LblBacklashPreview != null) LblBacklashPreview.Text = "You are currently situated in this sector.";
                if (LblTransitCost != null) LblTransitCost.Text = "Cost: 0 Stones | 0 AP";
                if (BtnTravel != null) BtnTravel.Disabled = true;
                return;
            }

            int reqStones = isRegionalCross ? TransitSystem.REGIONAL_TRANSIT_STONE_COST : TransitSystem.TRAVEL_STONE_COST;
            int reqAP = isRegionalCross ? TransitSystem.REGIONAL_TRANSIT_AP_COST : TransitSystem.TRAVEL_AP_COST;

            if (LblTransitCost != null)
            {
                LblTransitCost.Text = $"Transit Cost: {reqStones} Primeval Stones ◆ | {reqAP} AP / Days (Have: {stones} ◆ | {currentAP} AP)";
            }

            if (isRegionalCross)
            {
                var wall = RegionalWallLaw.GetBoundaryWall(currentRegion, bp.Region);
                string wallName = RegionalWallLaw.GetWallName(wall);

                bool sealedAperture = ChkSealAperture?.ButtonPressed == true;
                string backlashPreview;

                if (sealedAperture)
                {
                    backlashPreview = $"Crossing {wallName} with Sealed Aperture:\n" +
                                      "- Essence Sea will be completely drained to 0%\n" +
                                      "- Aperture Wall suffers 5% structural damage\n" +
                                      "- Gu worms and physical body remain protected!";
                }
                else
                {
                    string penalty = playerRank switch
                    {
                        1 => "Essence Sea -10%",
                        2 => "Essence Sea -25% | Equipped Gu Satiety -15",
                        3 => "Essence Sea -50% | Equipped Gu Satiety -35",
                        4 => "Essence Sea -75% | Wall Durability -30% | Equipped Gu Satiety -60",
                        _ => "Essence Sea -100% | HP -50% | Wall Durability -60% | Equipped Gu Satiety -85"
                    };
                    backlashPreview = $"Crossing {wallName} (Rank {playerRank} Backlash):\n- {penalty}";
                }

                if (LblBacklashPreview != null) LblBacklashPreview.Text = backlashPreview;
            }
            else
            {
                if (LblBacklashPreview != null)
                {
                    LblBacklashPreview.Text = "Intra-regional travel through mountainous paths.\n25% chance of wild beast or rogue cultivator road ambush.";
                }
            }

            bool canAfford = stones >= reqStones && currentAP >= reqAP;
            if (BtnTravel != null)
            {
                BtnTravel.Disabled = !canAfford;
            }

            if (LblFeedback != null)
            {
                if (!canAfford)
                {
                    LblFeedback.Text = "Insufficient resources for journey.";
                    LblFeedback.Modulate = new Color(0.95f, 0.4f, 0.4f, 1f);
                }
                else
                {
                    LblFeedback.Text = "Transit route is clear. Ready to embark.";
                    LblFeedback.Modulate = new Color(0.204f, 0.827f, 0.6f, 1f);
                }
            }
        }

        private void OnTravelPressed()
        {
            var world = _stateProvider?.Current;
            if (world == null || string.IsNullOrEmpty(_selectedSectorKey)) return;

            if (!WorldAtlasRegistry.TryGet(_selectedSectorKey, out var bp) || bp == null) return;

            var cult = world.Get<CultivatorComponent>(world.PlayerEntity);
            var clock = world.GetSingleton<WorldClockComponent>();

            WorldRegionID currentRegion = world.GetCurrentSector()?.Region ?? WorldRegionID.SouthernBorder;
            bool isRegionalCross = bp.Region != currentRegion;

            int reqStones = isRegionalCross ? TransitSystem.REGIONAL_TRANSIT_STONE_COST : TransitSystem.TRAVEL_STONE_COST;
            int reqAP = isRegionalCross ? TransitSystem.REGIONAL_TRANSIT_AP_COST : TransitSystem.TRAVEL_AP_COST;

            if (cult == null || cult.PrimevalStones < reqStones) return;
            if (clock == null || clock.ActionPointsRemaining < reqAP) return;

            // Deduct costs
            cult.PrimevalStones -= reqStones;
            TimeSystem.DeductActionPoints(world, reqAP);

            // Apply wall backlash if cross-regional
            if (isRegionalCross)
            {
                var wall = RegionalWallLaw.GetBoundaryWall(currentRegion, bp.Region);
                bool sealedAperture = ChkSealAperture?.ButtonPressed == true;
                TransitSystem.ApplyRegionalWallBacklash(world, wall, sealedAperture);
            }

            world.GetCurrentSector()?.CommitTilesToSnapshot();

            string targetKey = _selectedSectorKey;
            CloseAtlas();

            var overlay = UniversalLoadingOverlay.EnsureInstance(GetTree());
            _ = overlay.WrapSectorTransitAsync(world, targetKey);
        }
    }
}
