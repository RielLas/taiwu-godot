using System;
using Godot;
using TaiwuGu.Core;
using Color = Godot.Color;
using Mathf = Godot.Mathf;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Phase 2 / Milestone 2.1: Aperture Major Realm Breakthrough Modal.
    /// Elevates the breakthrough experience with:
    /// - Live Daoist Harmonic Resonance Wave Track with sinusoidal pulse & sweet spot.
    /// - Foreign Turbulence high-frequency jitter destabilization.
    /// - Authentic bronze-framed gauges with crimson, emerald, and cyan fluid.
    /// - Membrane fracture feedback and Ascension Banner.
    /// - 100% Halal compliance and strict CQRS event symmetry.
    /// </summary>
    public partial class ApertureBreakthroughModalView : CanvasLayer, IModalView
    {
        private bool _isOpen = false;
        public bool IsOpen => _isOpen;
        public void Dismiss() => GameEventManager.RaiseRequestCloseBreakthrough();
        private BreakthroughSessionSnapshot _latestSnapshot;

        // UI Controls
        private Control? _modalContainer;
        private Label? _lblTitle;
        private Label? _lblSubtitle;
        private Label? _lblCultivatorInfo;
        private Label? _lblStatusMessage;

        private StyledGaugeControl? _gaugeMembrane;
        private StyledGaugeControl? _gaugeEssence;
        private StyledGaugeControl? _gaugeTurbulence;
        private HarmonicWaveTrackControl? _waveTrack;

        private Button? _btnWave;
        private Button? _btnCrushStone;
        private Button? _btnRelicGu;
        private Button? _btnAbort;

        private Control? _resolutionPanel;
        private Label? _lblResolutionBanner;
        private Label? _lblResolutionDetail;
        private Button? _btnResolutionExit;

        // Visual Assets
        private Texture2D? _texBannerMountains;
        private Texture2D? _texFrameBarBronze;
        private Texture2D? _texFillEmerald;
        private Texture2D? _texFillCyan;
        private Texture2D? _texFillCrimson;

        // Impact Animation
        private float _impactFlashTimer = 0f;
        private Color _impactFlashColor = Colors.Transparent;
        private string _impactFeedbackText = "";

        public override void _EnterTree()
        {
            Layer = 15; // Above overworld HUD (5) and Ledger (10)
            Visible = false;

            LoadTextures();
            BuildViewTree();

            // Subscribe to CQRS events
            GameEventManager.OnBreakthroughSessionOpened += HandleBreakthroughSessionOpened;
            GameEventManager.OnBreakthroughSessionClosed += HandleBreakthroughSessionClosed;
            GameEventManager.OnBreakthroughStateUpdated   += HandleBreakthroughStateUpdated;
            GameEventManager.OnBreakthroughWaveImpact    += HandleBreakthroughWaveImpact;
            GameEventManager.OnBreakthroughResolved      += HandleBreakthroughResolved;
            GameEventManager.OnEscapeRequested           += HandleEscapeRequested;
        }

        public override void _ExitTree()
        {
            // Unsubscribe strictly from all events (Constitutional Law 2)
            GameEventManager.OnBreakthroughSessionOpened -= HandleBreakthroughSessionOpened;
            GameEventManager.OnBreakthroughSessionClosed -= HandleBreakthroughSessionClosed;
            GameEventManager.OnBreakthroughStateUpdated   -= HandleBreakthroughStateUpdated;
            GameEventManager.OnBreakthroughWaveImpact    -= HandleBreakthroughWaveImpact;
            GameEventManager.OnBreakthroughResolved      -= HandleBreakthroughResolved;
            GameEventManager.OnEscapeRequested           -= HandleEscapeRequested;
        }

        private void LoadTextures()
        {
            _texBannerMountains = LoadTextureSafe("res://Resources/UI/banner_ink_mountains.png");
            _texFrameBarBronze  = LoadTextureSafe("res://Resources/UI/frame_bar_bronze.png");
            _texFillEmerald     = LoadTextureSafe("res://Resources/UI/fill_fluid_emerald.png");
            _texFillCyan        = LoadTextureSafe("res://Resources/UI/fill_fluid_cyan.png");
            _texFillCrimson     = LoadTextureSafe("res://Resources/UI/fill_fluid_crimson.png");
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

        private void BuildViewTree()
        {
            // 1. Fullscreen Dark Ink Backdrop
            var backdrop = new ColorRect
            {
                Name = "BreakthroughBackdrop",
                Color = new Color(0.012f, 0.024f, 0.024f, 0.90f),
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(backdrop);

            // 2. Centered Modal Box
            _modalContainer = new PanelContainer
            {
                Name = "BreakthroughPanel",
                CustomMinimumSize = new Vector2(760, 530),
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            _modalContainer.SetAnchorsPreset(Control.LayoutPreset.Center);
            _modalContainer.GrowHorizontal = Control.GrowDirection.Both;
            _modalContainer.GrowVertical = Control.GrowDirection.Both;
            _modalContainer.OffsetLeft = -380;
            _modalContainer.OffsetRight = 380;
            _modalContainer.OffsetTop = -265;
            _modalContainer.OffsetBottom = 265;

            var panelStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.043f, 0.075f, 0.071f, 0.98f),
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                BorderWidthTop = 2,
                BorderWidthBottom = 2,
                BorderColor = new Color(0.24f, 0.38f, 0.35f, 1f),
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 6,
                CornerRadiusBottomRight = 6,
                ShadowColor = new Color(0f, 0f, 0f, 0.75f),
                ShadowSize = 16
            };
            _modalContainer.AddThemeStyleboxOverride("panel", panelStyle);
            backdrop.AddChild(_modalContainer);

            var mainVBox = new VBoxContainer
            {
                CustomMinimumSize = new Vector2(720, 500)
            };
            mainVBox.AddThemeConstantOverride("separation", 6);
            _modalContainer.AddChild(mainVBox);

            // 3. Shanshui Mountain Header Banner
            if (_texBannerMountains != null)
            {
                var bannerRect = new TextureRect
                {
                    Texture = _texBannerMountains,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                    CustomMinimumSize = new Vector2(720, 44)
                };
                mainVBox.AddChild(bannerRect);
            }

            // 4. Header Titles
            _lblTitle = new Label
            {
                Text = "◆ MAJOR REALM CRISIS: SHATTERING APERTURE MEMBRANE ◆",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _lblTitle.AddThemeColorOverride("font_color", new Color(0.984f, 0.749f, 0.141f, 1f)); // Radiant gold
            _lblTitle.AddThemeFontSizeOverride("font_size", 16);
            mainVBox.AddChild(_lblTitle);

            _lblSubtitle = new Label
            {
                Text = "Rank 1 Peak ➔ Rank 2 Initial",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _lblSubtitle.AddThemeColorOverride("font_color", new Color(0.204f, 0.827f, 0.600f, 1f)); // Jade green
            _lblSubtitle.AddThemeFontSizeOverride("font_size", 13);
            mainVBox.AddChild(_lblSubtitle);

            // 5. Cultivator Info Subhead
            _lblCultivatorInfo = new Label
            {
                Text = "Primeval Sea: 44.0% / 44.0% | Carried Stones: 10 块 | Human Path Dao Marks: 4 Marks (+16% Sweet Spot, +16% Force)",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _lblCultivatorInfo.AddThemeColorOverride("font_color", new Color(0.88f, 0.92f, 0.94f, 1f));
            _lblCultivatorInfo.AddThemeFontSizeOverride("font_size", 11);
            mainVBox.AddChild(_lblCultivatorInfo);

            // 6. Three Bronze-Framed Gauges
            // Gauge 1: Membrane Durability
            var membraneLabel = new Label
            {
                Text = "Aperture Barrier Membrane Integrity:"
            };
            membraneLabel.AddThemeColorOverride("font_color", new Color(0.97f, 0.44f, 0.44f, 1f));
            membraneLabel.AddThemeFontSizeOverride("font_size", 11);
            mainVBox.AddChild(membraneLabel);

            _gaugeMembrane = new StyledGaugeControl
            {
                CustomMinimumSize = new Vector2(720, 28),
                FrameTexture = _texFrameBarBronze,
                FillTexture = _texFillCrimson,
                Value = 1.0f,
                CenterText = "Membrane Integrity: 100 / 100 HP"
            };
            mainVBox.AddChild(_gaugeMembrane);

            // Gauge 2: Primeval Sea
            var essenceLabel = new Label
            {
                Text = "Primeval Sea Reserve:"
            };
            essenceLabel.AddThemeColorOverride("font_color", new Color(0.204f, 0.827f, 0.600f, 1f));
            essenceLabel.AddThemeFontSizeOverride("font_size", 11);
            mainVBox.AddChild(essenceLabel);

            _gaugeEssence = new StyledGaugeControl
            {
                CustomMinimumSize = new Vector2(720, 28),
                FrameTexture = _texFrameBarBronze,
                FillTexture = _texFillEmerald,
                Value = 1.0f,
                CenterText = "Primeval Sea: 44.0% / 44.0%"
            };
            mainVBox.AddChild(_gaugeEssence);

            // Gauge 3: Foreign Turbulence
            var turbulenceLabel = new Label
            {
                Text = "Foreign Turbulence (Feedback Rupture Threshold: 100):"
            };
            turbulenceLabel.AddThemeColorOverride("font_color", new Color(0.22f, 0.74f, 0.97f, 1f));
            turbulenceLabel.AddThemeFontSizeOverride("font_size", 11);
            mainVBox.AddChild(turbulenceLabel);

            _gaugeTurbulence = new StyledGaugeControl
            {
                CustomMinimumSize = new Vector2(720, 28),
                FrameTexture = _texFrameBarBronze,
                FillTexture = _texFillCyan,
                Value = 0f,
                CenterText = "Foreign Turbulence: 0 / 100"
            };
            mainVBox.AddChild(_gaugeTurbulence);

            // 7. Harmonic Resonance Wave Track
            var trackHeader = new Label
            {
                Text = "◆ Aperture Tidal Resonance Track [Harmonic Crest = Golden Sweet Spot] ◆",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            trackHeader.AddThemeColorOverride("font_color", new Color(0.984f, 0.749f, 0.141f, 1f));
            trackHeader.AddThemeFontSizeOverride("font_size", 11);
            mainVBox.AddChild(trackHeader);

            _waveTrack = new HarmonicWaveTrackControl
            {
                CustomMinimumSize = new Vector2(720, 52)
            };
            mainVBox.AddChild(_waveTrack);

            // 8. Live Status Feedback Banner
            _lblStatusMessage = new Label
            {
                Text = "Time your assault when the tidal pulse aligns with the golden harmonic resonance zone.",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _lblStatusMessage.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.55f, 1f));
            _lblStatusMessage.AddThemeFontSizeOverride("font_size", 11);
            mainVBox.AddChild(_lblStatusMessage);

            // 9. Action Button Controls
            var actionsGrid = new HBoxContainer
            {
                CustomMinimumSize = new Vector2(720, 34)
            };
            actionsGrid.AddThemeConstantOverride("separation", 10);
            mainVBox.AddChild(actionsGrid);

            _btnWave = CreateStyledButton("Assault Tidal Wave [Space / Click]", new Color(0.98f, 0.75f, 0.14f, 1f), () =>
            {
                GameEventManager.RaiseRequestReleaseEssenceWave(1.0f);
            });
            _btnWave.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            actionsGrid.AddChild(_btnWave);

            _btnCrushStone = CreateStyledButton("Crush Primeval Stone [C]", new Color(0.22f, 0.74f, 0.97f, 1f), () =>
            {
                GameEventManager.RaiseRequestBreakthroughCrushStone();
            });
            _btnCrushStone.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            actionsGrid.AddChild(_btnCrushStone);

            var secondaryActions = new HBoxContainer
            {
                CustomMinimumSize = new Vector2(720, 30)
            };
            secondaryActions.AddThemeConstantOverride("separation", 10);
            mainVBox.AddChild(secondaryActions);

            _btnRelicGu = CreateStyledButton("Consume Relic Gu (Instant Transmutation) [R]", new Color(0.20f, 0.83f, 0.60f, 1f), () =>
            {
                GameEventManager.RaiseRequestBreakthroughUseRelicGu();
            });
            _btnRelicGu.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            secondaryActions.AddChild(_btnRelicGu);

            _btnAbort = CreateStyledButton("Abort Seclusion (Rest & Recover) [Esc]", new Color(0.94f, 0.44f, 0.44f, 1f), () =>
            {
                GameEventManager.RaiseRequestCloseBreakthrough();
            });
            _btnAbort.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            secondaryActions.AddChild(_btnAbort);

            // 10. Breakthrough Resolution Overlay Panel
            _resolutionPanel = new PanelContainer
            {
                Name = "ResolutionPanel",
                CustomMinimumSize = new Vector2(720, 180),
                Visible = false
            };
            _resolutionPanel.SetAnchorsPreset(Control.LayoutPreset.Center);

            var resStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.03f, 0.05f, 0.05f, 0.95f),
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                BorderWidthTop = 2,
                BorderWidthBottom = 2,
                BorderColor = new Color(0.98f, 0.75f, 0.14f, 1f),
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 6,
                CornerRadiusBottomRight = 6
            };
            _resolutionPanel.AddThemeStyleboxOverride("panel", resStyle);

            var resVBox = new VBoxContainer();
            resVBox.AddThemeConstantOverride("separation", 12);
            _resolutionPanel.AddChild(resVBox);

            _lblResolutionBanner = new Label
            {
                Text = "★★★ MAJOR REALM TRANSCENDED ★★★",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _lblResolutionBanner.AddThemeFontSizeOverride("font_size", 18);
            _lblResolutionBanner.AddThemeColorOverride("font_color", new Color(0.984f, 0.749f, 0.141f, 1f));
            resVBox.AddChild(_lblResolutionBanner);

            _lblResolutionDetail = new Label
            {
                Text = "The membrane shatters! Your primeval sea surges with qualitative transformation.",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            _lblResolutionDetail.AddThemeFontSizeOverride("font_size", 12);
            _lblResolutionDetail.AddThemeColorOverride("font_color", Colors.White);
            resVBox.AddChild(_lblResolutionDetail);

            _btnResolutionExit = CreateStyledButton("Exit Seclusion Chamber [Esc]", new Color(0.98f, 0.75f, 0.14f, 1f), () =>
            {
                GameEventManager.RaiseRequestCloseBreakthrough();
            });
            _btnResolutionExit.CustomMinimumSize = new Vector2(240, 38);
            _btnResolutionExit.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            resVBox.AddChild(_btnResolutionExit);

            _modalContainer.AddChild(_resolutionPanel);
        }

        private Button CreateStyledButton(string text, Color accentColor, Action onClick)
        {
            var btn = new Button
            {
                Text = text
            };
            var normalStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.13f, 0.12f, 1f),
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderWidthTop = 1,
                BorderWidthBottom = 1,
                BorderColor = accentColor,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4
            };
            var hoverStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.14f, 0.22f, 0.20f, 1f),
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderWidthTop = 1,
                BorderWidthBottom = 1,
                BorderColor = Colors.White,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4
            };
            btn.AddThemeStyleboxOverride("normal", normalStyle);
            btn.AddThemeStyleboxOverride("hover", hoverStyle);
            btn.AddThemeStyleboxOverride("pressed", hoverStyle);
            btn.AddThemeColorOverride("font_color", accentColor);
            btn.AddThemeColorOverride("font_hover_color", Colors.White);
            btn.AddThemeFontSizeOverride("font_size", 12);
            btn.Pressed += onClick;
            return btn;
        }

        public override void _Process(double delta)
        {
            if (!_isOpen) return;

            float dt = (float)delta;

            // Update impact flash timer
            if (_impactFlashTimer > 0f)
            {
                _impactFlashTimer -= dt;
            }

            // Sync wave track real-time visual
            if (_waveTrack != null)
            {
                _waveTrack.ElapsedSessionTime = _latestSnapshot.ElapsedSessionTime;
                _waveTrack.Frequency = _latestSnapshot.OscillationFrequency;
                _waveTrack.TurbulenceRatio = _latestSnapshot.TurbulenceRatio;
                _waveTrack.SweetSpotWidth = _latestSnapshot.SweetSpotWidth;
                _waveTrack.QueueRedraw();
            }

            // Keyboard Shortcuts
            if (_latestSnapshot.State == BreakthroughState.PoundingInSession)
            {
                if (Godot.Input.IsActionJustPressed("ui_select") || Godot.Input.IsKeyPressed(Key.Space))
                {
                    GameEventManager.RaiseRequestReleaseEssenceWave(1.0f);
                }
                else if (Godot.Input.IsKeyPressed(Key.C))
                {
                    GameEventManager.RaiseRequestBreakthroughCrushStone();
                }
                else if (Godot.Input.IsKeyPressed(Key.R))
                {
                    GameEventManager.RaiseRequestBreakthroughUseRelicGu();
                }
            }
        }

        private void HandleEscapeRequested()
        {
            if (!_isOpen) return;

            // High-priority modal intercept (LIFO)
            GameEventManager.EscapeHandled = true;
            GameEventManager.RaiseRequestCloseBreakthrough();
        }

        private void HandleBreakthroughSessionOpened(BreakthroughSessionSnapshot snapshot)
        {
            _isOpen = true;
            Visible = true;
            _latestSnapshot = snapshot;
            if (_resolutionPanel != null) _resolutionPanel.Visible = false;

            GameEventManager.RaiseModalOpened(this);
            UpdateUIFromSnapshot(snapshot);
        }

        private void HandleBreakthroughSessionClosed()
        {
            _isOpen = false;
            Visible = false;
            if (_resolutionPanel != null) _resolutionPanel.Visible = false;
            GameEventManager.RaiseModalClosed(this);
        }

        private void HandleBreakthroughStateUpdated(BreakthroughSessionSnapshot snapshot)
        {
            _latestSnapshot = snapshot;
            UpdateUIFromSnapshot(snapshot);
        }

        private void HandleBreakthroughWaveImpact(bool isHarmonic, bool isDispersion, float damage)
        {
            _impactFlashTimer = 0.35f;
            if (isHarmonic)
            {
                _impactFlashColor = new Color(0.98f, 0.75f, 0.14f, 0.8f);
                _impactFeedbackText = $"【HARMONIC RESIDUAL RESONANCE: -{damage:F0} HP!】";
            }
            else if (isDispersion)
            {
                _impactFlashColor = new Color(0.6f, 0.6f, 0.6f, 0.6f);
                _impactFeedbackText = $"【WAVE DISPERSED WEAKLY: -{damage:F0} HP】";
            }
            else
            {
                _impactFlashColor = new Color(0.2f, 0.7f, 0.9f, 0.7f);
                _impactFeedbackText = $"【SOLID IMPACT: -{damage:F0} HP】";
            }

            if (_waveTrack != null)
            {
                _waveTrack.TriggerImpactRipple(isHarmonic, isDispersion);
            }
        }

        private void HandleBreakthroughResolved(BreakthroughState state, int newRank)
        {
            if (_resolutionPanel == null) return;
            _resolutionPanel.Visible = true;

            if (state == BreakthroughState.Succeeded)
            {
                _lblResolutionBanner!.Text = "★★★ MAJOR REALM TRANSCENDED ★★★";
                _lblResolutionBanner.AddThemeColorOverride("font_color", new Color(0.984f, 0.749f, 0.141f, 1f));
                _lblResolutionDetail!.Text = $"The membrane has completely shattered! You have broken through mortal limits into Rank {newRank}!\nAperture essence density expands exponentially.";
                _btnResolutionExit!.Text = "Consolidate Realm & Exit Seclusion [Esc]";
            }
            else if (state == BreakthroughState.FailedRupture)
            {
                _lblResolutionBanner!.Text = "⚠ CATASTROPHIC APERTURE RUPTURE ⚠";
                _lblResolutionBanner.AddThemeColorOverride("font_color", new Color(0.94f, 0.27f, 0.27f, 1f));
                _lblResolutionDetail!.Text = "Excessive Foreign Turbulence provoked violent somatic feedback!\nPrimeval sea collapsed and internal organs suffered severe hemorrhage.";
                _btnResolutionExit!.Text = "Treat Injuries & Exit Seclusion [Esc]";
            }
            else if (state == BreakthroughState.FailedEssenceDry)
            {
                _lblResolutionBanner!.Text = "MEMBRANE RESEALED";
                _lblResolutionBanner.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.55f, 1f));
                _lblResolutionDetail!.Text = "Primeval Sea was exhausted before the membrane gave way.\nThe aperture wall rapidly healed shut. Cultivation remains at peak.";
                _btnResolutionExit!.Text = "Recover Stamina & Exit Seclusion [Esc]";
            }
        }

        private void UpdateUIFromSnapshot(BreakthroughSessionSnapshot s)
        {
            if (_lblTitle != null)
            {
                _lblTitle.Text = $"◆ MAJOR REALM CRISIS: SHATTERING {s.MembraneName.ToUpper()} ◆";
            }
            if (_lblSubtitle != null)
            {
                _lblSubtitle.Text = $"Rank {s.CurrentRank} Peak ({s.MembraneName}) ➔ Rank {s.TargetRank} Initial ({s.TargetRankName})";
            }
            if (_lblCultivatorInfo != null)
            {
                _lblCultivatorInfo.Text = $"Primeval Sea: {s.CurrentEssenceSea:F1}% / {s.MaxEssenceCapacity:F1}% | Carried Stones: {s.PrimevalStones} 块 | Human Path Dao Marks: {s.HumanPathMarks} Marks (+{s.HumanPathMarks * 1.5f:F1}% Sweet Spot, +{s.HumanPathMarks * 4}% Force)";
            }

            // Gauges
            if (_gaugeMembrane != null)
            {
                _gaugeMembrane.Value = s.MembraneHPRatio;
                string regenNotice = s.IsRegenerating ? $" [RAPID HEALING: +{s.NaturalRegenRate:F1} HP/s]" : "";
                _gaugeMembrane.CenterText = $"Membrane Barrier Integrity: {s.MembraneCurrentHP:F0} / {s.MembraneMaxHP:F0} HP ({(s.MembraneHPRatio * 100f):F0}%){regenNotice}";
                _gaugeMembrane.QueueRedraw();
            }

            if (_gaugeEssence != null)
            {
                _gaugeEssence.Value = s.EssenceRatio;
                _gaugeEssence.CenterText = $"Primeval Sea: {s.CurrentEssenceSea:F1}% / {s.MaxEssenceCapacity:F1}% Max Capacity";
                _gaugeEssence.QueueRedraw();
            }

            if (_gaugeTurbulence != null)
            {
                _gaugeTurbulence.Value = s.TurbulenceRatio;
                _gaugeTurbulence.CenterText = $"Foreign Turbulence: {s.CurrentTurbulence:F0} / {s.TurbulenceLimit:F0}";
                _gaugeTurbulence.QueueRedraw();
            }

            if (_lblStatusMessage != null)
            {
                if (!string.IsNullOrEmpty(_impactFeedbackText) && _impactFlashTimer > 0f)
                {
                    _lblStatusMessage.Text = _impactFeedbackText;
                    _lblStatusMessage.AddThemeColorOverride("font_color", _impactFlashColor);
                }
                else if (!string.IsNullOrEmpty(s.StatusMessage))
                {
                    _lblStatusMessage.Text = s.StatusMessage;
                    _lblStatusMessage.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.55f, 1f));
                }
            }

            // Buttons availability
            bool inSession = s.State == BreakthroughState.PoundingInSession;
            if (_btnWave != null) _btnWave.Disabled = !inSession;
            if (_btnCrushStone != null)
            {
                _btnCrushStone.Disabled = !inSession || s.PrimevalStones <= 0;
                _btnCrushStone.Text = $"Crush Primeval Stone [C] (Stock: {s.PrimevalStones})";
            }
            if (_btnRelicGu != null) _btnRelicGu.Disabled = !inSession;
        }
    }

    /// <summary>
    /// Custom Daoist CanvasItem widget rendering live sinusoidal wave oscillation,
    /// harmonic sweet spot zone, foreign turbulence jitter, and wave impact ripples.
    /// </summary>
    public partial class HarmonicWaveTrackControl : Control
    {
        public float ElapsedSessionTime { get; set; } = 0f;
        public float Frequency { get; set; } = 1.2f;
        public float TurbulenceRatio { get; set; } = 0f;
        public float SweetSpotWidth { get; set; } = 0.22f;

        private float _rippleTimer = 0f;
        private Color _rippleColor = Colors.Transparent;

        public void TriggerImpactRipple(bool isHarmonic, bool isDispersion)
        {
            _rippleTimer = 0.4f;
            _rippleColor = isHarmonic ? new Color(0.98f, 0.75f, 0.14f, 0.9f) :
                           (isDispersion ? new Color(0.5f, 0.5f, 0.5f, 0.7f) : new Color(0.2f, 0.7f, 0.9f, 0.8f));
        }

        public override void _Process(double delta)
        {
            if (_rippleTimer > 0f)
            {
                _rippleTimer -= (float)delta;
                QueueRedraw();
            }
        }

        public override void _Draw()
        {
            var size = Size;
            var bgRect = new Rect2(Vector2.Zero, size);

            // 1. Dark ink-wash background
            DrawRect(bgRect, new Color(0.035f, 0.055f, 0.051f, 1f));
            DrawRect(bgRect, new Color(0.18f, 0.28f, 0.26f, 1f), false, 1.5f);

            // 2. Center Equilibrium Line
            float midY = size.Y * 0.5f;
            DrawLine(new Vector2(0f, midY), new Vector2(size.X, midY), new Color(0.22f, 0.35f, 0.32f, 0.8f), 1f);

            // 3. Golden Sweet Spot Zone (Right side high-crest alignment: [1 - sweetWidth .. 1])
            float sweetStartX = size.X * (1f - SweetSpotWidth);
            float sweetW = size.X * SweetSpotWidth;
            var sweetRect = new Rect2(sweetStartX, 2f, sweetW, size.Y - 4f);

            // Golden translucent fill & border
            DrawRect(sweetRect, new Color(0.984f, 0.749f, 0.141f, 0.22f));
            DrawRect(sweetRect, new Color(0.984f, 0.749f, 0.141f, 0.75f), false, 1.5f);

            // Sweet Spot Daoist Calligraphy Marker
            var font = ThemeDB.FallbackFont;
            if (font != null)
            {
                string sweetLabel = "【金丹共鸣区 (Sweet Spot)】";
                int fontSize = 10;
                var strSize = font.GetStringSize(sweetLabel, HorizontalAlignment.Center, -1, fontSize);
                var pos = new Vector2(sweetStartX + (sweetW - strSize.X) * 0.5f, 16f);
                DrawString(font, pos, sweetLabel, HorizontalAlignment.Center, -1, fontSize, new Color(1f, 0.92f, 0.65f, 0.95f));
            }

            // 4. Live Sinusoidal Wave Geometry with Turbulence Jitter
            float amplitude = (size.Y * 0.38f);
            int sampleCount = 90;
            var wavePoints = new Vector2[sampleCount];

            float jitterAmp = TurbulenceRatio * 7.0f; // Destabilization jitter amplitude
            float spatialScale = 2.0f; // 2 wave cycles across track width

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / (sampleCount - 1);
                float x = t * size.X;

                // High frequency jitter noise from crushed stone turbulence
                float jitter = 0f;
                if (jitterAmp > 0.01f)
                {
                    jitter = Mathf.Sin((ElapsedSessionTime * 32f) + (t * 24f)) * jitterAmp;
                }

                float phase = (ElapsedSessionTime * Frequency * Mathf.Pi * 2f) + (t * spatialScale * Mathf.Pi * 2f);
                float y = midY - (Mathf.Sin(phase) * amplitude) + jitter;
                wavePoints[i] = new Vector2(x, y);
            }

            // Draw wave line in radiant jade/celestial cyan
            Color waveColor = TurbulenceRatio > 0.65f ? new Color(0.95f, 0.40f, 0.40f, 0.95f) : new Color(0.204f, 0.827f, 0.600f, 0.95f);
            DrawPolyline(wavePoints, waveColor, 2.5f);

            // 5. Oscillating Resonance Pulse Indicator (The Gu Master's Tidal Breath)
            float rawSine = Mathf.Sin(ElapsedSessionTime * Frequency * Mathf.Pi * 2f);
            float currentPulseX = size.X * ((rawSine + 1f) * 0.5f);
            float pulseY = midY - (rawSine * amplitude);

            // Check if currently inside sweet spot
            bool inSweetZone = rawSine >= (0.75f - (SweetSpotWidth * 0.5f));
            Color pulseColor = inSweetZone ? new Color(0.984f, 0.749f, 0.141f, 1f) : new Color(0.88f, 0.94f, 0.96f, 1f);

            // Vertical tracking line
            DrawLine(new Vector2(currentPulseX, 0f), new Vector2(currentPulseX, size.Y), inSweetZone ? new Color(0.98f, 0.75f, 0.14f, 0.6f) : new Color(0.88f, 0.94f, 0.96f, 0.35f), 1.5f);

            // Yin-Yang Radiant Core Bead
            DrawCircle(new Vector2(currentPulseX, pulseY), 7f, pulseColor);
            DrawCircle(new Vector2(currentPulseX, pulseY), 3.5f, new Color(0.05f, 0.08f, 0.08f, 1f));

            // 6. Impact Shockwave Ripple Effect
            if (_rippleTimer > 0f)
            {
                float progress = 1f - (_rippleTimer / 0.4f);
                float rippleRadius = 10f + (progress * 45f);
                Color c = new Color(_rippleColor.R, _rippleColor.G, _rippleColor.B, _rippleColor.A * (1f - progress));
                DrawCircle(new Vector2(currentPulseX, pulseY), rippleRadius, c);
            }
        }
    }
}
