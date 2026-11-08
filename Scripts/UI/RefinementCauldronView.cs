using System;
using System.Collections.Generic;
using Godot;
using Color = Godot.Color;
using TaiwuGu.Core;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Phase 3 & 4: Alchemical Refinement Cauldron View (Godot 2D).
    /// Pure Presentation Layer Observer under Constitutional Laws 1, 4 & 6.
    /// Strictly decoupled from Domain Services & GameStateContext.
    /// 3-Phase Interactive Alchemical State Machine:
    /// - Phase 1: Thermal Equilibrium (heat pulse to stay within 20°C - 80°C green zone)
    /// - Phase 2: Will Fusion (steer will focus needle over wild will spike, suppress will)
    /// - Phase 3: Condensation & Stability (crush primeval stones to maintain nascent stability)
    /// Halal-compliant dark lacquer, imperial amber gold, cyan, and emerald aesthetic.
    /// </summary>
    public partial class RefinementCauldronView : Control, IModalView
    {
        private const string CauldronTitle = "◆ MORTAL GU REFINEMENT CAULDRON (合炼鼎炉) ◆";

        private bool _isOpen = false;
        private TaiwuModalStack? _modalStack;
        public bool IsOpen => _isOpen;

        public void InitializeModalStack(TaiwuModalStack modalStack)
        {
            _modalStack = modalStack;
            _scaffold?.BindModalStack(modalStack);
        }

        public void Dismiss() => GameEventManager.RaiseRequestCloseCauldron();

        // Presentation cached state
        private RefinementPhase _currentPhase = RefinementPhase.Idle;
        private RefinementFailureTier _lastFailureTier = RefinementFailureTier.None;

        // Every recipe, resource and timer below is filled from CauldronRefinementState or the
        // resource events; the view owns no recipe, balance value or duration of its own.
        private string _recipeName = string.Empty;
        private string _primaryGuName = string.Empty;
        private string _secondaryGuName = string.Empty;
        private string _resultGuName = string.Empty;
        private int _targetRank;
        private int _minStonesRequired;

        private bool _hasPrimaryGu;
        private bool _hasSecondaryGu;
        private bool _primaryIsVital;

        private float _cauldronTemperature;
        private float _phase1Timer;
        private float _phase1Duration;

        private float _wildWillResistance;
        private float _masterWillFocus;
        private float _wildWillFrequency;
        private float _willAlignmentProgress;
        private float _phase2Timer;
        private float _phase2Duration;

        private float _nascentStability;
        private float _phase3Timer;
        private float _phase3Duration;
        private int _stonesCrushed;

        private int _playerStones;
        private float _playerEssenceSea;
        private string _statusMessage = string.Empty;

        // UI Element References (the chrome itself lives in Scenes/UI/ModalScaffold.tscn)
        private ModalScaffold? _scaffold;
        private Label? _lblRecipeSubtitle;
        private Label? _lblIngredientsSummary;
        private Label? _lblResourcesSummary;

        // Phase Containers
        private Control? _panelIdle;
        private Control? _panelPhase1;
        private Control? _panelPhase2;
        private Control? _panelPhase3;
        private Control? _panelSuccess;
        private Control? _panelFailure;

        // Phase 1 widgets
        private ProgressBar? _barTemperature;
        private Label? _lblTempTimer;
        private Label? _lblTempStatus;

        // Phase 2 widgets
        private ProgressBar? _barWillAlignment;
        private ProgressBar? _barWildResistance;
        private Control? _trackWillFusion;
        private ColorRect? _wildWillZoneRect;
        private ColorRect? _masterFocusNeedleRect;
        private Label? _lblWillTimer;

        // Phase 3 widgets
        private ProgressBar? _barNascentStability;
        private Label? _lblStabilityTimer;
        private Label? _lblCrushedCounter;

        // Outcome widgets
        private Label? _lblSuccessTitle;
        private Label? _lblFailureTitle;
        private Label? _lblStatusOutcome;

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;
            Visible = false;

            BuildUIHierarchy();

            GameEventManager.OnCauldronOpened += HandleCauldronOpened;
            GameEventManager.OnCauldronClosed += HandleCauldronClosed;
            GameEventManager.OnCauldronModalToggled += HandleCauldronModalToggled;
            GameEventManager.OnCauldronStateUpdated += HandleCauldronStateUpdated;
            GameEventManager.OnCauldronSuccess += HandleCauldronSuccess;
            GameEventManager.OnCauldronFailed += HandleCauldronFailed;
            GameEventManager.OnPrimevalSeaChanged += HandlePrimevalSeaChanged;
            GameEventManager.OnPrimevalStonesChanged += HandlePrimevalStonesChanged;
            GameEventManager.OnGuInventoryUpdated += HandleGuInventoryUpdated;
        }

        public override void _ExitTree()
        {
            GameEventManager.OnCauldronOpened -= HandleCauldronOpened;
            GameEventManager.OnCauldronClosed -= HandleCauldronClosed;
            GameEventManager.OnCauldronModalToggled -= HandleCauldronModalToggled;
            GameEventManager.OnCauldronStateUpdated -= HandleCauldronStateUpdated;
            GameEventManager.OnCauldronSuccess -= HandleCauldronSuccess;
            GameEventManager.OnCauldronFailed -= HandleCauldronFailed;
            GameEventManager.OnPrimevalSeaChanged -= HandlePrimevalSeaChanged;
            GameEventManager.OnPrimevalStonesChanged -= HandlePrimevalStonesChanged;
            GameEventManager.OnGuInventoryUpdated -= HandleGuInventoryUpdated;

            if (_isOpen)
            {
                _isOpen = false;
                _scaffold?.Close();
            }
        }

        public void OpenWithState(CauldronRefinementState state) => HandleCauldronOpened(state);

        public override void _UnhandledInput(InputEvent @event)
        {
            if (!_isOpen) return;

            if (@event is InputEventKey k && k.Pressed && !k.Echo)
            {
                if (_currentPhase == RefinementPhase.Phase1_Extraction)
                {
                    if (k.Keycode == Key.W || k.Keycode == Key.Up)
                    {
                        GameEventManager.RaiseRequestCauldronPulseHeat();
                        GetViewport().SetInputAsHandled();
                    }
                }
                else if (_currentPhase == RefinementPhase.Phase2_WillFusion)
                {
                    if (k.Keycode == Key.A || k.Keycode == Key.Left)
                    {
                        GameEventManager.RaiseRequestCauldronAdjustWillFocus(-0.08f);
                        GetViewport().SetInputAsHandled();
                    }
                    else if (k.Keycode == Key.D || k.Keycode == Key.Right)
                    {
                        GameEventManager.RaiseRequestCauldronAdjustWillFocus(0.08f);
                        GetViewport().SetInputAsHandled();
                    }
                    else if (k.Keycode == Key.Space || k.Keycode == Key.Enter)
                    {
                        GameEventManager.RaiseRequestCauldronSuppressWill();
                        GetViewport().SetInputAsHandled();
                    }
                }
                else if (_currentPhase == RefinementPhase.Phase3_Condensation)
                {
                    if (k.Keycode == Key.F || k.Keycode == Key.Space || k.Keycode == Key.Enter)
                    {
                        GameEventManager.RaiseRequestCauldronCrushStone();
                        GetViewport().SetInputAsHandled();
                    }
                }
            }
        }

        public override void _Process(double delta)
        {
            if (!_isOpen) return;

            if (_currentPhase == RefinementPhase.Phase2_WillFusion)
            {
                float steerSpeed = 1.0f * (float)delta;
                if (Godot.Input.IsKeyPressed(Key.A) || Godot.Input.IsKeyPressed(Key.Left))
                {
                    GameEventManager.RaiseRequestCauldronAdjustWillFocus(-steerSpeed);
                }
                else if (Godot.Input.IsKeyPressed(Key.D) || Godot.Input.IsKeyPressed(Key.Right))
                {
                    GameEventManager.RaiseRequestCauldronAdjustWillFocus(steerSpeed);
                }
            }
        }

        private void HandleCauldronOpened(CauldronRefinementState state)
        {
            bool wasOpen = _isOpen;
            _isOpen = true;
            Visible = true;
            if (!wasOpen)
            {
                _scaffold?.Open(this, CauldronTitle);
            }
            UpdateFromCauldronState(state);
            _statusMessage = string.Empty;
            UpdatePhaseUI();
        }

        private void HandleCauldronClosed()
        {
            bool wasOpen = _isOpen;
            _isOpen = false;
            Visible = false;
            if (wasOpen)
            {
                _scaffold?.Close();
            }
        }

        private void HandleCauldronModalToggled(bool isOpen)
        {
            if (_isOpen == isOpen) return;
            _isOpen = isOpen;
            Visible = isOpen;

            // Keep the stack in step even if a toggle arrives without the matching
            // Opened/Closed event, so IsModalOpen<RefinementCauldronView>() stays honest.
            if (isOpen) _scaffold?.Open(this, CauldronTitle);
            else _scaffold?.Close();
        }

        private void HandleCauldronStateUpdated(CauldronRefinementState state)
        {
            UpdateFromCauldronState(state);
            UpdatePhaseUI();
        }

        private void UpdateFromCauldronState(CauldronRefinementState state)
        {
            if (state == null) return;

            _currentPhase = state.CurrentPhase;
            _lastFailureTier = state.LastFailureTier;

            if (state.ActiveRecipe != null)
            {
                _recipeName = state.ActiveRecipe.RecipeName;
                _resultGuName = state.ActiveRecipe.ResultGuName;
                _targetRank = state.ActiveRecipe.TargetRank;
                _phase1Duration = state.ActiveRecipe.Phase1Duration;
                _phase2Duration = state.ActiveRecipe.Phase2Duration;
                _phase3Duration = state.ActiveRecipe.Phase3Duration;
                _minStonesRequired = state.CalculateMinStonesRequired();
            }

            _cauldronTemperature = state.CauldronTemperature;
            _phase1Timer = state.Phase1Timer;

            _wildWillResistance = state.WildWillResistance;
            _masterWillFocus = state.MasterWillFocus;
            _wildWillFrequency = state.WildWillFrequency;
            _willAlignmentProgress = state.WillAlignmentProgress;
            _phase2Timer = state.Phase2Timer;

            _nascentStability = state.NascentStability;
            _phase3Timer = state.Phase3Timer;
            _stonesCrushed = state.StonesCrushed;
        }

        private void HandleCauldronSuccess(GuWormData refinedGu)
        {
            _currentPhase = RefinementPhase.Success;
            if (refinedGu != null)
            {
                _resultGuName = refinedGu.Name;
            }
            _statusMessage = $"Alchemical fusion complete! Obtained [{_resultGuName}].";
            UpdatePhaseUI();
        }

        private void HandleCauldronFailed(RefinementFailureTier tier, string reason)
        {
            _currentPhase = RefinementPhase.Failure;
            _lastFailureTier = tier;
            _statusMessage = reason;
            UpdatePhaseUI();
        }

        private void HandlePrimevalSeaChanged(float currentSea, float maxCap)
        {
            _playerEssenceSea = currentSea;
            UpdateResourceSummaries();
        }

        private void HandlePrimevalStonesChanged(int stones)
        {
            _playerStones = stones;
            UpdateResourceSummaries();
        }

        private void HandleGuInventoryUpdated(List<GuWormData> equipped, List<GuWormData> reserve)
        {
            if (equipped == null) return;

            var primary = equipped.Find(g => g.GuID == "gu_moonlight_01" || g.GuID.Contains("moonlight"));
            var secondary = equipped.Find(g => g.GuID == "gu_white_boar_01" || g.GuID.Contains("boar"));

            if (primary == null && reserve != null)
            {
                primary = reserve.Find(g => g.GuID == "gu_moonlight_01" || g.GuID.Contains("moonlight"));
            }
            if (secondary == null && reserve != null)
            {
                secondary = reserve.Find(g => g.GuID == "gu_white_boar_01" || g.GuID.Contains("boar"));
            }

            _hasPrimaryGu = primary != null;
            if (primary != null)
            {
                _primaryGuName = primary.Name;
                _primaryIsVital = primary.IsVitalGu;
            }

            _hasSecondaryGu = secondary != null;
            if (secondary != null)
            {
                _secondaryGuName = secondary.Name;
            }

            UpdateResourceSummaries();
        }

        private void BuildUIHierarchy()
        {
            // Chrome (backdrop, panel, header, close button, Escape, modal registration)
            // is Scenes/UI/ModalScaffold.tscn; this view only fills the body.
            var scaffoldScene = GD.Load<PackedScene>("res://Scenes/UI/ModalScaffold.tscn");
            if (scaffoldScene == null)
            {
                GD.PushError("[RefinementCauldronView] Missing modal scaffold scene at res://Scenes/UI/ModalScaffold.tscn.");
                return;
            }

            _scaffold = scaffoldScene.Instantiate<ModalScaffold>();
            AddChild(_scaffold);
            _scaffold.BindModalStack(_modalStack);
            _scaffold.SetTitle(CauldronTitle);

            // 640x520 crucible window, keeping the original azure title and Return [Esc] button
            if (_scaffold.Panel != null)
            {
                _scaffold.Panel.CustomMinimumSize = new Vector2(640f, 520f);
            }
            if (_scaffold.Backdrop != null)
            {
                _scaffold.Backdrop.Color = new Color(0f, 0f, 0f, 0.75f);
            }
            if (_scaffold.TitleLabel != null)
            {
                _scaffold.TitleLabel.AddThemeColorOverride("font_color", new Color(0.22f, 0.74f, 0.97f, 1f)); // Azure #38BDF8
            }
            if (_scaffold.CloseButton != null)
            {
                _scaffold.CloseButton.Text = "← Return [Esc]";
                _scaffold.CloseButton.CustomMinimumSize = new Vector2(110f, 28f);
            }

            var rootVBox = _scaffold.ContentRoot;
            if (rootVBox == null) return;
            rootVBox.AddThemeConstantOverride("separation", 10);

            _lblRecipeSubtitle = new Label
            {
                Text = $"Recipe: {_recipeName} | Target: Rank {_targetRank}",
                Modulate = new Color(0.984f, 0.749f, 0.141f, 1f) // Amber Gold #FBBF24
            };
            _lblRecipeSubtitle.AddThemeFontSizeOverride("font_size", 11);
            rootVBox.AddChild(_lblRecipeSubtitle);

            // --- INGREDIENTS & RESOURCES BOX ---
            var summaryPanel = new PanelContainer();
            var summaryStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.015f, 0.025f, 0.030f, 0.95f),
                BorderColor = new Color(0.18f, 0.24f, 0.30f, 0.8f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3
            };
            summaryPanel.AddThemeStyleboxOverride("panel", summaryStyle);
            rootVBox.AddChild(summaryPanel);

            var summaryVBox = new VBoxContainer();
            summaryVBox.AddThemeConstantOverride("separation", 4);
            var sumMargin = new MarginContainer();
            sumMargin.AddThemeConstantOverride("margin_left", 12);
            sumMargin.AddThemeConstantOverride("margin_right", 12);
            sumMargin.AddThemeConstantOverride("margin_top", 8);
            sumMargin.AddThemeConstantOverride("margin_bottom", 8);
            sumMargin.AddChild(summaryVBox);
            summaryPanel.AddChild(sumMargin);

            _lblIngredientsSummary = new Label
            {
                Text = string.Empty,
                Modulate = new Color(0.204f, 0.827f, 0.6f, 1f) // Emerald #34D399
            };
            _lblIngredientsSummary.AddThemeFontSizeOverride("font_size", 11);
            summaryVBox.AddChild(_lblIngredientsSummary);

            _lblResourcesSummary = new Label
            {
                Text = string.Empty,
                Modulate = new Color(0.886f, 0.910f, 0.941f, 1f)
            };
            _lblResourcesSummary.AddThemeFontSizeOverride("font_size", 11);
            summaryVBox.AddChild(_lblResourcesSummary);

            // --- DYNAMIC CONTENT BODY ---
            var bodyContainer = new PanelContainer
            {
                SizeFlagsVertical = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Stop
            };
            rootVBox.AddChild(bodyContainer);

            BuildPhasePanels(bodyContainer);

            // --- FOOTER ---
            var btnLeave = new Button
            {
                Text = "Close Crucible (离开鼎炉) [Esc]",
                CustomMinimumSize = new Vector2(0f, 32f),
                MouseFilter = MouseFilterEnum.Stop
            };
            btnLeave.Pressed += () => GameEventManager.RaiseRequestCloseCauldron();
            rootVBox.AddChild(btnLeave);
        }

        private void BuildPhasePanels(PanelContainer container)
        {
            var slotBg = new StyleBoxFlat
            {
                BgColor = new Color(0.02f, 0.035f, 0.039f, 0.95f),
                BorderColor = new Color(0.12f, 0.16f, 0.23f, 1f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1
            };

            // 1. Idle Panel
            _panelIdle = new VBoxContainer { Name = "PanelIdle", Alignment = BoxContainer.AlignmentMode.Center };
            _panelIdle.AddThemeConstantOverride("separation", 16);
            container.AddChild(_panelIdle);

            var lblIdleDesc = new Label
            {
                Text = "Sacred Alchemical Cauldron ready. Catalysts and spiritual fuel must meet ancient formula criteria.",
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = new Color(0.7f, 0.75f, 0.8f, 1f)
            };
            lblIdleDesc.AddThemeFontSizeOverride("font_size", 12);
            _panelIdle.AddChild(lblIdleDesc);

            var btnIgnite = new Button
            {
                Text = "Ignite Cauldron (起鼎合炼)",
                CustomMinimumSize = new Vector2(240f, 44f),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
                MouseFilter = MouseFilterEnum.Stop
            };
            btnIgnite.Pressed += () => GameEventManager.RaiseRequestInitiateRefinement();
            _panelIdle.AddChild(btnIgnite);

            // 2. Phase 1 Panel: Thermal Equilibrium
            _panelPhase1 = new VBoxContainer { Name = "PanelPhase1", Alignment = BoxContainer.AlignmentMode.Center };
            _panelPhase1.AddThemeConstantOverride("separation", 12);
            container.AddChild(_panelPhase1);

            _lblTempTimer = new Label
            {
                Text = "Phase 1: Material Extraction (提炼)",
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = new Color(0.984f, 0.749f, 0.141f, 1f)
            };
            _lblTempTimer.AddThemeFontSizeOverride("font_size", 12);
            _panelPhase1.AddChild(_lblTempTimer);

            _lblTempStatus = new Label
            {
                Text = "Cauldron Temperature (Ideal Green Zone: 20°C - 80°C)",
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = new Color(0.886f, 0.910f, 0.941f, 1f)
            };
            _lblTempStatus.AddThemeFontSizeOverride("font_size", 12);
            _panelPhase1.AddChild(_lblTempStatus);

            var tempBarContainer = new MarginContainer { CustomMinimumSize = new Vector2(400f, 32f), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
            _panelPhase1.AddChild(tempBarContainer);

            var tempFill = new StyleBoxFlat
            {
                BgColor = new Color(0.937f, 0.267f, 0.267f, 1f), // Crimson
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3
            };
            _barTemperature = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 100,
                Value = 0,
                ShowPercentage = false,
                CustomMinimumSize = new Vector2(400f, 32f)
            };
            _barTemperature.AddThemeStyleboxOverride("background", (StyleBox)slotBg.Duplicate());
            _barTemperature.AddThemeStyleboxOverride("fill", tempFill);
            tempBarContainer.AddChild(_barTemperature);

            // Safe green zone overlay (20% to 80%)
            var greenZoneOverlay = new ColorRect
            {
                Color = new Color(0.2f, 0.9f, 0.4f, 0.25f),
                Size = new Vector2(240f, 32f),
                Position = new Vector2(80f, 0f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            tempBarContainer.AddChild(greenZoneOverlay);

            var btnPulseHeat = new Button
            {
                Text = "Pulse Aperture Flame [W] (+12°C | -2.5% Sea)",
                CustomMinimumSize = new Vector2(300f, 40f),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
                MouseFilter = MouseFilterEnum.Stop
            };
            btnPulseHeat.Pressed += () => GameEventManager.RaiseRequestCauldronPulseHeat();
            _panelPhase1.AddChild(btnPulseHeat);

            // 3. Phase 2 Panel: Will Fusion
            _panelPhase2 = new VBoxContainer { Name = "PanelPhase2", Alignment = BoxContainer.AlignmentMode.Center };
            _panelPhase2.AddThemeConstantOverride("separation", 10);
            container.AddChild(_panelPhase2);

            _lblWillTimer = new Label
            {
                Text = "Phase 2: Will Fusion (融意)",
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = new Color(0.22f, 0.74f, 0.97f, 1f)
            };
            _lblWillTimer.AddThemeFontSizeOverride("font_size", 12);
            _panelPhase2.AddChild(_lblWillTimer);

            var willBarBox = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            willBarBox.AddThemeConstantOverride("separation", 16);
            _panelPhase2.AddChild(willBarBox);

            var alignFill = new StyleBoxFlat { BgColor = new Color(0.204f, 0.827f, 0.6f, 1f) };
            _barWillAlignment = new ProgressBar { MinValue = 0, MaxValue = 50, Value = 0, ShowPercentage = true, CustomMinimumSize = new Vector2(200f, 20f) };
            _barWillAlignment.AddThemeStyleboxOverride("background", (StyleBox)slotBg.Duplicate());
            _barWillAlignment.AddThemeStyleboxOverride("fill", alignFill);
            willBarBox.AddChild(_barWillAlignment);

            var resistFill = new StyleBoxFlat { BgColor = new Color(0.937f, 0.267f, 0.267f, 1f) };
            _barWildResistance = new ProgressBar { MinValue = 0, MaxValue = 100, Value = 0, ShowPercentage = true, CustomMinimumSize = new Vector2(200f, 20f) };
            _barWildResistance.AddThemeStyleboxOverride("background", (StyleBox)slotBg.Duplicate());
            _barWildResistance.AddThemeStyleboxOverride("fill", resistFill);
            willBarBox.AddChild(_barWildResistance);

            // Interactive Will Fusion Track
            _trackWillFusion = new Control { CustomMinimumSize = new Vector2(440f, 32f), SizeFlagsHorizontal = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Stop };
            var trackBg = new ColorRect { Color = new Color(0.12f, 0.16f, 0.22f, 1f), Size = new Vector2(440f, 32f), MouseFilter = MouseFilterEnum.Ignore };
            _trackWillFusion.AddChild(trackBg);

            // Red spike zone
            _wildWillZoneRect = new ColorRect { Color = new Color(0.9f, 0.2f, 0.2f, 0.55f), Size = new Vector2(60f, 32f), Position = new Vector2(190f, 0f), MouseFilter = MouseFilterEnum.Ignore };
            _trackWillFusion.AddChild(_wildWillZoneRect);

            // Blue master focus needle
            _masterFocusNeedleRect = new ColorRect { Color = new Color(0.22f, 0.74f, 0.97f, 1f), Size = new Vector2(10f, 38f), Position = new Vector2(215f, -3f), MouseFilter = MouseFilterEnum.Ignore };
            _trackWillFusion.AddChild(_masterFocusNeedleRect);

            _trackWillFusion.GuiInput += (ev) =>
            {
                if (ev is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
                {
                    float ratio = Math.Clamp(mb.Position.X / 440f, 0f, 1f);
                    float delta = ratio - _masterWillFocus;
                    GameEventManager.RaiseRequestCauldronAdjustWillFocus(delta);
                }
            };
            _panelPhase2.AddChild(_trackWillFusion);

            var willBtnsHBox = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            willBtnsHBox.AddThemeConstantOverride("separation", 12);
            _panelPhase2.AddChild(willBtnsHBox);

            var btnSteerLeft = new Button { Text = "◀ Steer [A]", CustomMinimumSize = new Vector2(90f, 36f) };
            btnSteerLeft.Pressed += () => GameEventManager.RaiseRequestCauldronAdjustWillFocus(-0.08f);
            willBtnsHBox.AddChild(btnSteerLeft);

            var btnSuppress = new Button { Text = "⚡ SUPPRESS WILL! [Space] (-1.5% Sea)", CustomMinimumSize = new Vector2(240f, 36f) };
            btnSuppress.Pressed += () => GameEventManager.RaiseRequestCauldronSuppressWill();
            willBtnsHBox.AddChild(btnSuppress);

            var btnSteerRight = new Button { Text = "Steer [D] ▶", CustomMinimumSize = new Vector2(90f, 36f) };
            btnSteerRight.Pressed += () => GameEventManager.RaiseRequestCauldronAdjustWillFocus(0.08f);
            willBtnsHBox.AddChild(btnSteerRight);

            // 4. Phase 3 Panel: Condensation & Stability
            _panelPhase3 = new VBoxContainer { Name = "PanelPhase3", Alignment = BoxContainer.AlignmentMode.Center };
            _panelPhase3.AddThemeConstantOverride("separation", 12);
            container.AddChild(_panelPhase3);

            _lblStabilityTimer = new Label
            {
                Text = "Phase 3: Condensation (成形) - Locking Dao Marks...",
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = new Color(0.984f, 0.749f, 0.141f, 1f)
            };
            _lblStabilityTimer.AddThemeFontSizeOverride("font_size", 12);
            _panelPhase3.AddChild(_lblStabilityTimer);

            var stabFill = new StyleBoxFlat { BgColor = new Color(0.204f, 0.827f, 0.6f, 1f) };
            _barNascentStability = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 100,
                Value = 0,
                ShowPercentage = true,
                CustomMinimumSize = new Vector2(400f, 32f),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter
            };
            _barNascentStability.AddThemeStyleboxOverride("background", (StyleBox)slotBg.Duplicate());
            _barNascentStability.AddThemeStyleboxOverride("fill", stabFill);
            _panelPhase3.AddChild(_barNascentStability);

            _lblCrushedCounter = new Label
            {
                Text = "Nascent Dao Mark Stability",
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = new Color(0.886f, 0.910f, 0.941f, 1f)
            };
            _lblCrushedCounter.AddThemeFontSizeOverride("font_size", 11);
            _panelPhase3.AddChild(_lblCrushedCounter);

            var btnCrush = new Button
            {
                Text = "CRUSH PRIMEVAL STONE! [F] (+14% Stability)",
                CustomMinimumSize = new Vector2(320f, 44f),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
                MouseFilter = MouseFilterEnum.Stop
            };
            btnCrush.Pressed += () => GameEventManager.RaiseRequestCauldronCrushStone();
            _panelPhase3.AddChild(btnCrush);

            // 5. Success Panel
            _panelSuccess = new VBoxContainer { Name = "PanelSuccess", Alignment = BoxContainer.AlignmentMode.Center };
            _panelSuccess.AddThemeConstantOverride("separation", 14);
            container.AddChild(_panelSuccess);

            _lblSuccessTitle = new Label
            {
                Text = "★★★ REFINEMENT SUCCESS! ★★★",
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = new Color(0.204f, 0.827f, 0.6f, 1f)
            };
            _lblSuccessTitle.AddThemeFontSizeOverride("font_size", 16);
            _panelSuccess.AddChild(_lblSuccessTitle);

            // 6. Failure Panel
            _panelFailure = new VBoxContainer { Name = "PanelFailure", Alignment = BoxContainer.AlignmentMode.Center };
            _panelFailure.AddThemeConstantOverride("separation", 14);
            container.AddChild(_panelFailure);

            _lblFailureTitle = new Label
            {
                Text = "【 炼化失败 】 REFINEMENT FAILED 【 毁损 】",
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = new Color(0.937f, 0.267f, 0.267f, 1f)
            };
            _lblFailureTitle.AddThemeFontSizeOverride("font_size", 16);
            _panelFailure.AddChild(_lblFailureTitle);

            _lblStatusOutcome = new Label
            {
                Text = "",
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = new Color(0.7f, 0.75f, 0.8f, 1f)
            };
            _lblStatusOutcome.AddThemeFontSizeOverride("font_size", 12);
            _panelFailure.AddChild(_lblStatusOutcome);

            UpdatePhaseUI();
        }

        private void UpdateResourceSummaries()
        {
            if (_lblIngredientsSummary != null)
            {
                string pri = _hasPrimaryGu ? $"{_primaryGuName}{(_primaryIsVital ? " (★ Vital Gu)" : "")}" : "Missing";
                string sec = _hasSecondaryGu ? _secondaryGuName : "Missing";
                _lblIngredientsSummary.Text = $"Primary: {pri} | Secondary: {sec}";
            }

            if (_lblResourcesSummary != null)
            {
                _lblResourcesSummary.Text = $"Primeval Stones: {_playerStones} / {_minStonesRequired} Min Needed | Aperture Sea: {_playerEssenceSea:F1}% (Min 25%)";
            }
        }

        private void UpdatePhaseUI()
        {
            if (_panelIdle == null || _panelPhase1 == null || _panelPhase2 == null ||
                _panelPhase3 == null || _panelSuccess == null || _panelFailure == null)
            {
                return;
            }

            _panelIdle.Visible = _currentPhase == RefinementPhase.Idle;
            _panelPhase1.Visible = _currentPhase == RefinementPhase.Phase1_Extraction;
            _panelPhase2.Visible = _currentPhase == RefinementPhase.Phase2_WillFusion;
            _panelPhase3.Visible = _currentPhase == RefinementPhase.Phase3_Condensation;
            _panelSuccess.Visible = _currentPhase == RefinementPhase.Success;
            _panelFailure.Visible = _currentPhase == RefinementPhase.Failure;

            if (_currentPhase == RefinementPhase.Phase1_Extraction)
            {
                if (_barTemperature != null) _barTemperature.Value = _cauldronTemperature;
                if (_lblTempTimer != null) _lblTempTimer.Text = $"Phase 1: Material Extraction (提炼) - Time: {_phase1Timer:F1}s / {_phase1Duration:F1}s";
                if (_lblTempStatus != null) _lblTempStatus.Text = $"Cauldron Temperature: {_cauldronTemperature:F0}°C (Ideal Range: 20°C - 80°C)";
            }
            else if (_currentPhase == RefinementPhase.Phase2_WillFusion)
            {
                if (_lblWillTimer != null) _lblWillTimer.Text = $"Phase 2: Will Fusion (融意) - Alignment: {_willAlignmentProgress:F0}%/50% | Wild Resistance: {_wildWillResistance:F0}%/100%";
                if (_barWillAlignment != null) _barWillAlignment.Value = _willAlignmentProgress;
                if (_barWildResistance != null) _barWildResistance.Value = _wildWillResistance;

                if (_wildWillZoneRect != null)
                {
                    float trackW = 440f;
                    float zoneW = 60f;
                    float zoneX = Math.Clamp((trackW * _wildWillFrequency) - (zoneW * 0.5f), 0f, trackW - zoneW);
                    _wildWillZoneRect.Position = new Vector2(zoneX, 0f);
                }

                if (_masterFocusNeedleRect != null)
                {
                    float trackW = 440f;
                    float needleX = Math.Clamp((trackW * _masterWillFocus) - 5f, 0f, trackW - 10f);
                    _masterFocusNeedleRect.Position = new Vector2(needleX, -3f);
                }
            }
            else if (_currentPhase == RefinementPhase.Phase3_Condensation)
            {
                if (_lblStabilityTimer != null) _lblStabilityTimer.Text = $"Phase 3: Condensation (成形) - Time: {_phase3Timer:F1}s / {_phase3Duration:F1}s";
                if (_barNascentStability != null) _barNascentStability.Value = _nascentStability;
                if (_lblCrushedCounter != null) _lblCrushedCounter.Text = $"Nascent Dao Mark Stability: {_nascentStability:F0}% (Stones Crushed: {_stonesCrushed})";
            }
            else if (_currentPhase == RefinementPhase.Success)
            {
                if (_lblSuccessTitle != null) _lblSuccessTitle.Text = $"★★★ REFINEMENT SUCCESS! ★★★\nObtained [{_resultGuName}] (Rank {_targetRank})";
            }
            else if (_currentPhase == RefinementPhase.Failure)
            {
                if (_lblFailureTitle != null) _lblFailureTitle.Text = $"【 炼化失败 】 REFINEMENT FAILED ({_lastFailureTier}) 【 毁损 】\nAuxiliary components destroyed. Check Aperture health!";
                if (_lblStatusOutcome != null) _lblStatusOutcome.Text = _statusMessage;
            }

            UpdateResourceSummaries();
        }
    }
}
