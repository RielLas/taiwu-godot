using System;
using Godot;
using Color = Godot.Color;
using Mathf = Godot.Mathf;
using TaiwuGu.Core;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Page 1 of the ledger: realm, membrane, aperture sea and wall gauges, the four chamber
    /// actions and the lore footnote. It renders a <see cref="CultivationSnapshot"/> and owns
    /// no cached state of its own; the breakthrough button raises an intent because the ledger
    /// has to close itself before the minigame opens.
    /// </summary>
    public partial class CultivationTab : MarginContainer
    {
        public event Action? BreakthroughRequested;

        private Label? _lblRealm;
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
        private Button? _btnStone;
        private Button? _btnWash;
        private Button? _btnBreakthrough;

        public CultivationTab()
        {
            Name = "PanelCultivation";
            AddThemeConstantOverride("margin_left", 14);
            AddThemeConstantOverride("margin_right", 14);
            AddThemeConstantOverride("margin_top", 12);
            AddThemeConstantOverride("margin_bottom", 12);

            Build();
        }

        private void Build()
        {
            var vBox = new VBoxContainer();
            vBox.AddThemeConstantOverride("separation", 10);
            AddChild(vBox);

            // Aperture chamber overview with the Dao Moon medallion
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

            var moonMedallion = new PanelContainer { CustomMinimumSize = new Vector2(46f, 46f) };
            var moonStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.14f, 0.12f, 1f),
                BorderColor = new Color(0.20f, 0.83f, 0.60f, 1f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1
            };
            moonMedallion.AddThemeStyleboxOverride("panel", moonStyle);
            var moonTex = new TextureRect
            {
                Texture = LedgerArt.DaoMoon,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };
            moonMedallion.AddChild(moonTex);
            headerRow.AddChild(moonMedallion);

            // Two-column cultivation data grid
            var grid = new GridContainer { Columns = 2 };
            grid.AddThemeConstantOverride("h_separation", 24);
            grid.AddThemeConstantOverride("v_separation", 4);
            vBox.AddChild(grid);

            _lblRealm = CreateStatLabel("Cultivation Realm", new Color(0.133f, 0.773f, 0.369f, 1f));
            grid.AddChild(_lblRealm);

            _lblMembrane = CreateStatLabel("Aperture Membrane", new Color(0.961f, 0.620f, 0.043f, 1f));
            grid.AddChild(_lblMembrane);

            _lblAptitude = CreateStatLabel("Innate Aptitude", new Color(0.984f, 0.749f, 0.141f, 1f));
            grid.AddChild(_lblAptitude);

            _lblStones = CreateStatLabel("Primeval Stones", new Color(0.984f, 0.749f, 0.141f, 1f));
            grid.AddChild(_lblStones);

            _lblHumanDaoMarks = CreateStatLabel("Human Path Dao Marks", new Color(0.22f, 0.74f, 0.97f, 1f));
            grid.AddChild(_lblHumanDaoMarks);

            _lblDensity = CreateStatLabel("Density Multiplier", new Color(0.961f, 0.620f, 0.043f, 1f));
            grid.AddChild(_lblDensity);

            _lblLifespan = CreateStatLabel("Heavenly Lifespan (寿元)", new Color(0.204f, 0.827f, 0.6f, 1f));
            grid.AddChild(_lblLifespan);

            _lblSeasonal = CreateStatLabel("Seasonal Resonance", new Color(0.204f, 0.827f, 0.6f, 1f));
            grid.AddChild(_lblSeasonal);

            // Primeval sea gauge
            var seaBox = new VBoxContainer();
            seaBox.AddThemeConstantOverride("separation", 2);
            vBox.AddChild(seaBox);

            _lblSeaTitle = new Label
            {
                Text = "Green Copper Primeval Sea (青铜元海)",
                Modulate = new Color(0.88f, 0.91f, 0.94f, 1f)
            };
            _lblSeaTitle.AddThemeFontSizeOverride("font_size", 11);
            seaBox.AddChild(_lblSeaTitle);

            _gaugeSea = new StyledGaugeControl
            {
                CustomMinimumSize = new Vector2(0f, 38f),
                FrameTexture = LedgerArt.FrameBarBronze,
                FillTexture = LedgerArt.FillEmerald,
                Value = 0f,
                CenterText = string.Empty
            };
            seaBox.AddChild(_gaugeSea);

            // Aperture wall / membrane condition gauge
            var wallBox = new VBoxContainer();
            wallBox.AddThemeConstantOverride("separation", 2);
            vBox.AddChild(wallBox);

            _lblWallTitle = new Label
            {
                Text = "Aperture Wall-Washing Progress",
                Modulate = new Color(0.88f, 0.91f, 0.94f, 1f)
            };
            _lblWallTitle.AddThemeFontSizeOverride("font_size", 11);
            wallBox.AddChild(_lblWallTitle);

            _gaugeWall = new StyledGaugeControl
            {
                CustomMinimumSize = new Vector2(0f, 38f),
                FrameTexture = LedgerArt.FrameBarBronze,
                FillTexture = LedgerArt.FillCyan,
                Value = 0f,
                CenterText = string.Empty
            };
            wallBox.AddChild(_gaugeWall);

            // Chamber actions
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

            actionsHBox.AddChild(CreateActionButton(
                "Meditate & Condense\n(-1 AP / Replenish Sea)",
                () => GameEventManager.RaiseRequestMeditate()));

            _btnStone = CreateActionButton(
                "Crush Primeval Stone",
                () => GameEventManager.RaiseRequestConsumeStone());
            actionsHBox.AddChild(_btnStone);

            _btnWash = CreateActionButton(
                "Wash Aperture Wall",
                () => GameEventManager.RaiseRequestWashApertureWall());
            actionsHBox.AddChild(_btnWash);

            _btnBreakthrough = CreateActionButton(
                "Batter Barrier Chamber\n(Breakthrough Seclusion)",
                () => BreakthroughRequested?.Invoke());
            actionsHBox.AddChild(_btnBreakthrough);

            // Canonical lore footnote
            var lblQuote = new Label
            {
                Text = "\"Man is the spirit of all living things, Gu are the essence of heaven and earth.\nInnate Human Path Dao marks determine initial aperture talent. Mobilise primeval sea to wash the membrane for substages; summon surging tidal waves to batter and shatter the rigid wall to achieve major rank breakthrough.\"",
                Modulate = new Color(0.58f, 0.64f, 0.72f, 1f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            lblQuote.AddThemeFontSizeOverride("font_size", 10);
            vBox.AddChild(lblQuote);
        }

        public void Bind(CultivationSnapshot state)
        {
            if (_lblRealm != null)
                _lblRealm.Text = $"Cultivation Realm: Rank {state.Rank} {state.SubstageName}";

            if (_lblMembrane != null)
                _lblMembrane.Text = $"Aperture Membrane: {state.MembraneName}";

            if (_lblAptitude != null)
                _lblAptitude.Text = $"Innate Aptitude: {state.AptitudePercent:F0}% ({state.CultivatorName})";

            if (_lblStones != null)
                _lblStones.Text = $"Primeval Stones: {state.PrimevalStones} Stones";

            if (_lblHumanDaoMarks != null)
                _lblHumanDaoMarks.Text = $"Human Path Dao Marks: {state.HumanPathDaoMarks} Marks (+{state.HumanPathDaoMarks * 2}% Wash, +{state.HumanPathDaoMarks}% Batter)";

            double density = CultivatorData.CalculateDensityCoefficient(state.Rank, state.Substage);
            if (_lblDensity != null)
                _lblDensity.Text = $"Density Multiplier: {density:N0}x (Qualitative Purity)";

            if (_lblLifespan != null)
                _lblLifespan.Text = $"Heavenly Lifespan (寿元): Age {state.Age} / {state.LifespanYears} Years ({state.RemainingLifespanYears} yrs left)";

            if (_lblSeasonal != null)
                _lblSeasonal.Text = $"Seasonal Resonance: {state.SeasonalAffinity}";

            // Primeval sea
            float fillPct = Mathf.Clamp(state.MaxSea > 0 ? state.CurrentSea / state.MaxSea : 0f, 0f, 1f);
            if (_lblSeaTitle != null)
                _lblSeaTitle.Text = $"{GetEssenceSeaName(state.Rank)}: {state.CurrentSea:F1}% / {state.MaxSea:F1}%";

            if (_gaugeSea != null)
            {
                _gaugeSea.Value = fillPct;
                _gaugeSea.CenterText = $"Primeval Sea Fill: {fillPct * 100f:F1}% ({state.CurrentSea:F1}% of {state.MaxSea:F1}% Max Capacity)";
                _gaugeSea.QueueRedraw();
            }

            // Aperture wall
            if (state.Substage < 4)
            {
                string nextStage = state.Substage switch
                {
                    1 => "Middle Stage (中阶)",
                    2 => "Upper Stage (高阶)",
                    3 => "Peak Stage (巅峰)",
                    _ => "Next Stage"
                };

                if (_lblWallTitle != null)
                    _lblWallTitle.Text = $"Aperture Wall-Washing Progress: {state.WallWashingProgress:F1}% / 100%  (Tempering towards {nextStage})";

                if (_gaugeWall != null)
                {
                    _gaugeWall.FillTexture = LedgerArt.FillCyan;
                    _gaugeWall.Value = Mathf.Clamp(state.WallWashingProgress / 100f, 0f, 1f);
                    _gaugeWall.CenterText = $"Membrane Tempering: {state.WallWashingProgress:F1}%";
                    _gaugeWall.QueueRedraw();
                }
            }
            else
            {
                string statusText = state.WallDurability <= state.MaxWallDurability * 0.3f
                    ? "CRACKED RIGID MEMBRANE"
                    : "Solidified Peak Membrane";

                if (_lblWallTitle != null)
                    _lblWallTitle.Text = $"Aperture Barrier Durability: {state.WallDurability:F1} / {state.MaxWallDurability:F1}  ({statusText})";

                if (_gaugeWall != null)
                {
                    _gaugeWall.FillTexture = LedgerArt.FillCrimson;
                    _gaugeWall.Value = Mathf.Clamp(state.MaxWallDurability > 0 ? state.WallDurability / state.MaxWallDurability : 0f, 0f, 1f);
                    _gaugeWall.CenterText = $"Membrane Barrier Integrity: {state.WallDurability:F1} / {state.MaxWallDurability:F1}";
                    _gaugeWall.QueueRedraw();
                }
            }

            // Stone recovery figure on the crush button
            float singleRecovery = CultivatorData.CalculateStoneRecoveryPercentage(state.Rank, state.Substage, state.AptitudePercent);
            if (_btnStone != null)
            {
                _btnStone.Text = $"Crush Primeval Stone\n(-1 Stone / +{singleRecovery:F1}% Sea)";
            }

            if (_btnWash != null)
            {
                if (state.Substage < 4)
                {
                    _btnWash.Text = "Wash Aperture Wall\n(-15% Sea, -1 AP / Substage)";
                    _btnWash.Disabled = false;
                }
                else
                {
                    _btnWash.Text = "Wall Fully Solidified\n(Peak Stage Reached)";
                    _btnWash.Disabled = true;
                }
            }

            if (_btnBreakthrough != null)
            {
                if (state.Substage == 4 && state.Rank < 5)
                {
                    _btnBreakthrough.Text = "Batter Barrier Chamber\n(Breakthrough Seclusion)";
                    _btnBreakthrough.Disabled = false;
                }
                else if (state.Rank >= 5)
                {
                    _btnBreakthrough.Text = "Mortal Ceiling Reached\n(Requires Tribulation)";
                    _btnBreakthrough.Disabled = true;
                }
                else
                {
                    _btnBreakthrough.Text = "Breakthrough Locked\n(Requires Peak Stage)";
                    _btnBreakthrough.Disabled = true;
                }
            }
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
}
