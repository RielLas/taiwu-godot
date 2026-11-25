using System;
using Godot;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Systems;
using Color = Godot.Color;
using Mathf = Godot.Mathf;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Phase 2 / Milestone 2.5: Wild Organism Will Subjugation Modal (野生蛊虫强行炼化).
    /// Direct-read presentation observer wired to WorldStore & WildEncounterSystem.
    /// </summary>
    public partial class WildGuEncounterModalView : CanvasLayer, IModalView
    {
        private bool _isOpen = false;
        private IWorldStateProvider? _stateProvider;

        public void Initialize(IWorldStateProvider stateProvider) => _stateProvider = stateProvider;

        public void Dismiss()
        {
            if (_stateProvider?.Current is { } world)
                WildEncounterSystem.CloseEncounter(world);
        }

        // UI Controls
        private Control? _modalContainer;
        private Label? _lblTitle;
        private Label? _lblSubtitle;
        private Label? _lblLore;
        private Label? _lblRankDiff;

        private StyledGaugeControl? _gaugeFeralWill;
        private StyledGaugeControl? _gaugeAlignment;
        private WillHarmonicTrackControl? _harmonicTrack;

        private HSlider? _sliderNeedle;
        private Button? _btnSteerLeft;
        private Button? _btnSteerRight;
        private Button? _btnPulseEssence;
        private Button? _btnInstantOverwrite;
        private Button? _btnBottomAction;

        private Control? _clashingContainer;
        private Control? _resolutionPanel;
        private Label? _lblResolutionHeader;
        private Label? _lblResolutionDetail;
        private Button? _btnResolutionClose;

        // Visual Assets
        private Texture2D? _texBannerMountains;
        private Texture2D? _texFrameBarBronze;
        private Texture2D? _texFillEmerald;
        private Texture2D? _texFillCrimson;

        public bool IsOpen => _isOpen;

        public override void _EnterTree()
        {
            Layer = 16; // Above Breakthrough (15), Ledger (10), HUD (5)
            Visible = false;
        }

        public override void _Ready()
        {
            LoadTextures();
            BuildUI();

            WorldSignals.OnTopicsChanged          += HandleTopicsChanged;
            WorldSignals.OnWildEncounterResolved  += HandleEncounterResolved;
            WorldSignals.OnEscapeRequested        += HandleEscapeRequested;
        }

        public override void _ExitTree()
        {
            WorldSignals.OnTopicsChanged          -= HandleTopicsChanged;
            WorldSignals.OnWildEncounterResolved  -= HandleEncounterResolved;
            WorldSignals.OnEscapeRequested        -= HandleEscapeRequested;

            if (_isOpen)
            {
                _isOpen = false;
                WorldSignals.EmitModalClosed(this);
            }
        }

        private void LoadTextures()
        {
            _texBannerMountains = ResourceLoader.Load<Texture2D>("res://Resources/UI/banner_ink_mountains.png");
            _texFrameBarBronze = ResourceLoader.Load<Texture2D>("res://Resources/UI/frame_bar_bronze.png");
            _texFillEmerald = ResourceLoader.Load<Texture2D>("res://Resources/UI/fill_fluid_emerald.png");
            _texFillCrimson = ResourceLoader.Load<Texture2D>("res://Resources/UI/fill_fluid_crimson.png");
        }

        private void BuildUI()
        {
            // Fullscreen Dimmer Backdrop
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
                Name = "CenterContainer",
                AnchorRight = 1f,
                AnchorBottom = 1f,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            AddChild(centerContainer);

            // Main Modal Window Panel (640x500)
            _modalContainer = new PanelContainer
            {
                Name = "MainModalPanel",
                CustomMinimumSize = new Vector2(640f, 500f),
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
            margin.AddThemeConstantOverride("margin_top", 16);
            margin.AddThemeConstantOverride("margin_bottom", 16);
            _modalContainer.AddChild(margin);

            var rootVBox = new VBoxContainer();
            rootVBox.AddThemeConstantOverride("separation", 8);
            margin.AddChild(rootVBox);

            // Shanshui Mountain Header Banner
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

            // Title & Subtitle
            _lblTitle = new Label
            {
                Text = "◆ WILD ORGANISM WILL SUBJUGATION (野生蛊虫强行炼化) ◆",
                Modulate = new Color(0.984f, 0.749f, 0.141f, 1f),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _lblTitle.AddThemeFontSizeOverride("font_size", 16);
            rootVBox.AddChild(_lblTitle);

            _lblSubtitle = new Label
            {
                Text = "Wild Feral Entity | Rank 1 | Docile",
                Modulate = new Color(0.204f, 0.827f, 0.600f, 1f),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _lblSubtitle.AddThemeFontSizeOverride("font_size", 12);
            rootVBox.AddChild(_lblSubtitle);

            // Discovery Lore
            _lblLore = new Label
            {
                Text = "A wild Gu organism nestled in primeval mountain qi.",
                Modulate = new Color(0.85f, 0.88f, 0.90f, 1f),
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            _lblLore.AddThemeFontSizeOverride("font_size", 11);
            rootVBox.AddChild(_lblLore);

            // Rank Differential Banner
            var diffPanel = new PanelContainer();
            var diffStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.04f, 0.07f, 0.07f, 0.9f),
                BorderColor = new Color(0.25f, 0.35f, 0.35f, 0.7f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1
            };
            diffPanel.AddThemeStyleboxOverride("panel", diffStyle);
            rootVBox.AddChild(diffPanel);

            var diffMargin = new MarginContainer();
            diffMargin.AddThemeConstantOverride("margin_left", 8);
            diffMargin.AddThemeConstantOverride("margin_right", 8);
            diffMargin.AddThemeConstantOverride("margin_top", 4);
            diffMargin.AddThemeConstantOverride("margin_bottom", 4);
            diffPanel.AddChild(diffMargin);

            _lblRankDiff = new Label
            {
                Text = "Rank Differential: ΔR +0 (Equal Contest - 标准共鸣)",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _lblRankDiff.AddThemeFontSizeOverride("font_size", 12);
            diffMargin.AddChild(_lblRankDiff);

            // Clashing Minigame Section
            _clashingContainer = new VBoxContainer();
            _clashingContainer.AddThemeConstantOverride("separation", 8);
            rootVBox.AddChild(_clashingContainer);

            // Instantaneous Will Overwrite Button (ΔR >= 3)
            _btnInstantOverwrite = new Button
            {
                Text = "⚡ INSTANTANEOUS WILL OVERWRITE (阶位秒炼)\n[No Minigame Required - Drown Feral Consciousness With High-Rank Majesty]",
                CustomMinimumSize = new Vector2(0f, 44f),
                Visible = false
            };
            _btnInstantOverwrite.Pressed += () =>
            {
                if (_stateProvider?.Current is { } world) WildEncounterSystem.InstantSubjugate(world);
            };
            _clashingContainer.AddChild(_btnInstantOverwrite);

            // Progress Bars (Wild Will & Alignment)
            var gaugesRow = new VBoxContainer();
            gaugesRow.AddThemeConstantOverride("separation", 4);
            _clashingContainer.AddChild(gaugesRow);

            _gaugeFeralWill = new StyledGaugeControl
            {
                CustomMinimumSize = new Vector2(0f, 24f),
                FrameTexture = _texFrameBarBronze,
                FillTexture = _texFillCrimson,
                Value = 1.0f,
                CenterText = "Wild Will: 100%"
            };
            gaugesRow.AddChild(_gaugeFeralWill);

            _gaugeAlignment = new StyledGaugeControl
            {
                CustomMinimumSize = new Vector2(0f, 24f),
                FrameTexture = _texFrameBarBronze,
                FillTexture = _texFillEmerald,
                Value = 0.0f,
                CenterText = "Your Alignment: 0%"
            };
            gaugesRow.AddChild(_gaugeAlignment);

            // Harmonic Frequency Track
            var lblTrack = new Label
            {
                Text = "Harmonic Frequency Track (意志共鸣频段):",
                Modulate = new Color(0.984f, 0.749f, 0.141f, 1f)
            };
            lblTrack.AddThemeFontSizeOverride("font_size", 11);
            _clashingContainer.AddChild(lblTrack);

            _harmonicTrack = new WillHarmonicTrackControl
            {
                CustomMinimumSize = new Vector2(0f, 32f)
            };
            _clashingContainer.AddChild(_harmonicTrack);

            // Steering Controls (Left, Slider, Right)
            var steerHBox = new HBoxContainer();
            steerHBox.AddThemeConstantOverride("separation", 8);
            _clashingContainer.AddChild(steerHBox);

            _btnSteerLeft = new Button
            {
                Text = "◀ Steer Left",
                CustomMinimumSize = new Vector2(100f, 30f)
            };
            _btnSteerLeft.Pressed += () =>
            {
                if (_stateProvider?.Current is { } world) WildEncounterSystem.AdjustNeedle(world, -0.06f);
            };
            steerHBox.AddChild(_btnSteerLeft);

            _sliderNeedle = new HSlider
            {
                MinValue = 0.0,
                MaxValue = 1.0,
                Step = 0.005,
                Value = 0.5,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0f, 30f)
            };
            _sliderNeedle.ValueChanged += (val) =>
            {
                if (_stateProvider?.Current is { } world) WildEncounterSystem.SetNeedle(world, (float)val);
            };
            steerHBox.AddChild(_sliderNeedle);

            _btnSteerRight = new Button
            {
                Text = "Steer Right ▶",
                CustomMinimumSize = new Vector2(100f, 30f)
            };
            _btnSteerRight.Pressed += () =>
            {
                if (_stateProvider?.Current is { } world) WildEncounterSystem.AdjustNeedle(world, 0.06f);
            };
            steerHBox.AddChild(_btnSteerRight);

            // Action: Essence Pulse Wave
            _btnPulseEssence = new Button
            {
                Text = "🌊 PULSE ESSENCE WAVE (真元冲刷 -4% Sea)\n[Force Feral Will Into Alignment]",
                CustomMinimumSize = new Vector2(0f, 40f)
            };
            _btnPulseEssence.Pressed += () =>
            {
                if (_stateProvider?.Current is { } world) WildEncounterSystem.PulseEssence(world);
            };
            _clashingContainer.AddChild(_btnPulseEssence);

            // Resolution Panel (Succeeded or Backlash)
            _resolutionPanel = new VBoxContainer
            {
                Name = "ResolutionPanel",
                Visible = false
            };
            _resolutionPanel.AddThemeConstantOverride("separation", 12);
            rootVBox.AddChild(_resolutionPanel);

            _lblResolutionHeader = new Label
            {
                Text = "★ SUBJUGATION ACCOMPLISHED ★",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _lblResolutionHeader.AddThemeFontSizeOverride("font_size", 16);
            _resolutionPanel.AddChild(_lblResolutionHeader);

            _lblResolutionDetail = new Label
            {
                Text = "The wild consciousness has been completely subdued.",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            _lblResolutionDetail.AddThemeFontSizeOverride("font_size", 12);
            _resolutionPanel.AddChild(_lblResolutionDetail);

            _btnResolutionClose = new Button
            {
                Text = "Accept & Return [Esc]",
                CustomMinimumSize = new Vector2(180f, 34f),
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
            };
            _btnResolutionClose.Pressed += Dismiss;
            _resolutionPanel.AddChild(_btnResolutionClose);

            // Bottom Spacer & Abandon Button
            var spacer = new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            rootVBox.AddChild(spacer);

            var bottomHBox = new HBoxContainer();
            rootVBox.AddChild(bottomHBox);

            var bottomSpacerL = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            bottomHBox.AddChild(bottomSpacerL);

            _btnBottomAction = new Button
            {
                Text = "Abandon Encounter (离开)",
                CustomMinimumSize = new Vector2(180f, 32f)
            };
            _btnBottomAction.Pressed += Dismiss;
            bottomHBox.AddChild(_btnBottomAction);

            var bottomSpacerR = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            bottomHBox.AddChild(bottomSpacerR);
        }

        private void HandleEscapeRequested()
        {
            if (!_isOpen || WorldSignals.EscapeHandled) return;

            Dismiss();
            WorldSignals.EscapeHandled = true;
        }

        private void HandleTopicsChanged(DirtyTopic topics)
        {
            if ((topics & DirtyTopic.WildEncounter) == 0) return;
            var world = _stateProvider?.Current;
            if (world == null) return;

            var session = WildEncounterSystem.GetOrCreateSession(world);
            if (session.IsOpen && !_isOpen)
            {
                _isOpen = true;
                Visible = true;
                WorldSignals.EmitModalOpened(this);
                if (_clashingContainer != null) _clashingContainer.Visible = true;
                if (_resolutionPanel != null) _resolutionPanel.Visible = false;
                if (_btnBottomAction != null) _btnBottomAction.Text = "Abandon Encounter (离开)";
            }
            else if (!session.IsOpen && _isOpen)
            {
                _isOpen = false;
                Visible = false;
                WorldSignals.EmitModalClosed(this);
                return;
            }

            if (_isOpen)
            {
                UpdateUIFromSnapshot(WildEncounterSystem.BuildSnapshot(world));
            }
        }

        private void HandleEncounterResolved(SubjugationState state, GuWormComponent? gu, string msg)
        {
            if (!_isOpen) return;

            if (_clashingContainer != null) _clashingContainer.Visible = false;
            if (_resolutionPanel != null) _resolutionPanel.Visible = true;
            if (_btnBottomAction != null) _btnBottomAction.Text = "Close (确定)";

            if (state == SubjugationState.Success)
            {
                _lblResolutionHeader!.Text = "★ SUBJUGATION ACCOMPLISHED (强行炼化成功) ★";
                _lblResolutionHeader.Modulate = new Color(0.204f, 0.827f, 0.600f, 1f);
                string guName = gu != null ? gu.Name : "Tamed Gu Worm";
                _lblResolutionDetail!.Text = $"{msg}\n{guName} is now compliant and placed into your Reserve Gu.";
            }
            else
            {
                _lblResolutionHeader!.Text = "☠ SUBJUGATION FAILED (反噬重创) ☠";
                _lblResolutionHeader.Modulate = new Color(0.937f, 0.267f, 0.267f, 1f);
                _lblResolutionDetail!.Text = $"{msg}\nAperture essence suffered 20% leakage backlash damage.";
            }
        }

        private void UpdateUIFromSnapshot(SubjugationSessionSnapshot s)
        {
            if (_lblSubtitle != null && s.Target != null)
            {
                _lblSubtitle.Text = $"{s.Target.Name} | Rank {s.Target.Rank} | Temperament: {s.Target.Temperament} | Diet: {s.Target.Diet}";
            }

            if (_lblLore != null && s.Target != null)
            {
                _lblLore.Text = s.Target.DiscoveryLore;
            }

            if (_lblRankDiff != null)
            {
                string desc = s.DeltaRank switch
                {
                    >= 3 => "Instantaneous Will Overwrite (阶位秒炼 - 0.0s)",
                    2 => "Dominant Overpower (绝对压制 - 极宽共鸣)",
                    1 => "Effortless Cleansing (从容炼化 - 宽共鸣)",
                    0 => "Equal Contest (同阶缠斗 - 标准共鸣)",
                    _ => "Reckless Up-Rank Gamble (越阶强炼 - 极险)"
                };

                Color diffColor = s.DeltaRank >= 3 ? new Color(0.204f, 0.827f, 0.600f, 1f)
                    : (s.DeltaRank >= 1 ? new Color(0.984f, 0.749f, 0.141f, 1f)
                    : (s.DeltaRank == 0 ? new Color(0.22f, 0.74f, 0.97f, 1f)
                    : new Color(0.937f, 0.267f, 0.267f, 1f)));

                _lblRankDiff.Text = $"Rank Differential: ΔR {s.DeltaRank:+0;-0;+0} ({desc})";
                _lblRankDiff.Modulate = diffColor;
            }

            if (_btnInstantOverwrite != null)
            {
                _btnInstantOverwrite.Visible = s.IsInstantAvailable;
            }

            if (_gaugeFeralWill != null)
            {
                _gaugeFeralWill.Value = s.FeralWillResistance / 100f;
                _gaugeFeralWill.CenterText = $"Wild Will: {s.FeralWillResistance:F0}%";
                _gaugeFeralWill.QueueRedraw();
            }

            if (_gaugeAlignment != null)
            {
                _gaugeAlignment.Value = s.PlayerWillAlignment / 100f;
                _gaugeAlignment.CenterText = $"Your Alignment: {s.PlayerWillAlignment:F0}%";
                _gaugeAlignment.QueueRedraw();
            }

            if (_harmonicTrack != null)
            {
                _harmonicTrack.UpdateTrack(s.FeralWillFrequency, s.PlayerWillNeedle, s.HarmonicTolerance);
            }

            if (_sliderNeedle != null && Mathf.Abs((float)_sliderNeedle.Value - s.PlayerWillNeedle) > 0.04f)
            {
                _sliderNeedle.SetValueNoSignal(s.PlayerWillNeedle);
            }
        }
    }

    /// <summary>
    /// Custom Daoist Harmonic Frequency Track control for the capture minigame.
    /// Draws tolerance resonance window, oscillating feral frequency indicator, and player focus needle.
    /// </summary>
    public partial class WillHarmonicTrackControl : Control
    {
        public float FeralFrequency { get; set; } = 0.5f;
        public float PlayerNeedle { get; set; } = 0.5f;
        public float HarmonicTolerance { get; set; } = 0.16f;

        public void UpdateTrack(float feralFreq, float playerNeedle, float tolerance)
        {
            FeralFrequency = Mathf.Clamp(feralFreq, 0f, 1f);
            PlayerNeedle = Mathf.Clamp(playerNeedle, 0f, 1f);
            HarmonicTolerance = Mathf.Clamp(tolerance, 0.02f, 0.5f);
            QueueRedraw();
        }

        public override void _Draw()
        {
            var size = Size;

            // 1. Dark Track Background
            DrawRect(new Rect2(0, 0, size.X, size.Y), new Color(0.04f, 0.08f, 0.07f, 0.95f));
            DrawRect(new Rect2(0, 0, size.X, size.Y), new Color(0.20f, 0.30f, 0.28f, 0.90f), false, 1.5f);

            // 2. Center Equilibrium Line
            float midY = size.Y * 0.5f;
            DrawLine(new Vector2(0f, midY), new Vector2(size.X, midY), new Color(0.25f, 0.35f, 0.32f, 0.7f), 1f);

            // 3. Feral Harmonic Resonance Window (Golden / Jade Translucent Box)
            float feralX = size.X * FeralFrequency;
            float tolWidth = size.X * HarmonicTolerance;
            float winStartX = feralX - (tolWidth * 0.5f);
            var tolRect = new Rect2(winStartX, 2f, tolWidth, size.Y - 4f);

            DrawRect(tolRect, new Color(0.204f, 0.827f, 0.600f, 0.30f));
            DrawRect(tolRect, new Color(0.984f, 0.749f, 0.141f, 0.70f), false, 1.5f);

            // 4. Wild Feral Target Needle (Crimson indicator)
            DrawRect(new Rect2(feralX - 3f, 1f, 6f, size.Y - 2f), new Color(0.937f, 0.267f, 0.267f, 0.95f));

            // 5. Player Focus Needle (Celestial Cyan / Gold Marker)
            float playerX = size.X * PlayerNeedle;
            bool isAligned = Mathf.Abs(PlayerNeedle - FeralFrequency) <= HarmonicTolerance;
            Color playerColor = isAligned ? new Color(0.984f, 0.749f, 0.141f, 1f) : new Color(0.22f, 0.74f, 0.97f, 1f);

            // Vertical indicator needle
            DrawRect(new Rect2(playerX - 4f, -2f, 8f, size.Y + 4f), playerColor);
            DrawCircle(new Vector2(playerX, midY), 6f, playerColor);
            DrawCircle(new Vector2(playerX, midY), 3f, new Color(0.04f, 0.08f, 0.07f, 1f));
        }
    }
}
