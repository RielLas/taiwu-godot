using System;
using Godot;
using TaiwuGu.Core;
using TaiwuGu.Psychology;
using Color = Godot.Color;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Phase 2 / Milestone 2.3: Scroll of Taiwu-style Bottom NPC Dialogue Pavilion (太吾式人际会晤画阁).
    /// Displays NPC information, favorability bar, greeting speech, interaction feedback,
    /// and 5 core Daoist social actions: Spar, Barter, Extort, Ambush, Depart.
    /// Strictly adheres to Halal mandate (calligraphy, seal tokens, sacred geometry) and event symmetry.
    /// </summary>
    public partial class NPCDialoguePavilionView : CanvasLayer, IModalView
    {
        private bool _isOpen = false;
        public bool IsOpen => _isOpen;
        public void Dismiss() => GameEventManager.RaiseRequestCloseNPCDialogue();
        private CultivatorEntityData? _currentNPC;

        private Control? _pavilionBox;
        private Label? _lblNPCName;
        private Label? _lblNPCDetails;
        private Label? _lblSpeechText;
        private Label? _lblFeedbackText;
        private ProgressBar? _barFavorability;

        private Button? _btnSpar;
        private Button? _btnBarter;
        private Button? _btnExtort;
        private Button? _btnAmbush;
        private Button? _btnDepart;

        public override void _EnterTree()
        {
            Layer = 14; // Above HUD, below top-level alerts
            Visible = false;

            BuildViewTree();

            GameEventManager.OnNPCDialogueOpened   += HandleDialogueOpened;
            GameEventManager.OnNPCDialogueFeedback += HandleDialogueFeedback;
            GameEventManager.OnNPCDialogueClosed   += HandleDialogueClosed;
            GameEventManager.OnEscapeRequested     += HandleEscapeRequested;
        }

        public override void _ExitTree()
        {
            GameEventManager.OnNPCDialogueOpened   -= HandleDialogueOpened;
            GameEventManager.OnNPCDialogueFeedback -= HandleDialogueFeedback;
            GameEventManager.OnNPCDialogueClosed   -= HandleDialogueClosed;
            GameEventManager.OnEscapeRequested     -= HandleEscapeRequested;
        }

        private void BuildViewTree()
        {
            // Bottom-aligned Dialogue Pavilion Container
            _pavilionBox = new PanelContainer
            {
                Name = "NPCDialoguePavilion",
                CustomMinimumSize = new Vector2(960, 210),
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            _pavilionBox.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
            _pavilionBox.GrowHorizontal = Control.GrowDirection.Both;
            _pavilionBox.GrowVertical = Control.GrowDirection.Begin;
            _pavilionBox.OffsetLeft = 160;
            _pavilionBox.OffsetRight = -160;
            _pavilionBox.OffsetTop = -225;
            _pavilionBox.OffsetBottom = -15;

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
            _pavilionBox.AddThemeStyleboxOverride("panel", panelStyle);
            AddChild(_pavilionBox);

            var mainHBox = new HBoxContainer();
            mainHBox.AddThemeConstantOverride("separation", 14);
            _pavilionBox.AddChild(mainHBox);

            // Left Section: NPC Identity & Favorability
            var leftVBox = new VBoxContainer
            {
                CustomMinimumSize = new Vector2(300, 190)
            };
            leftVBox.AddThemeConstantOverride("separation", 6);
            mainHBox.AddChild(leftVBox);

            _lblNPCName = new Label
            {
                Text = "【修】 Cultivator Name",
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _lblNPCName.AddThemeColorOverride("font_color", new Color(0.984f, 0.749f, 0.141f, 1f));
            _lblNPCName.AddThemeFontSizeOverride("font_size", 15);
            leftVBox.AddChild(_lblNPCName);

            _lblNPCDetails = new Label
            {
                Text = "Clan: Gu Yue Clan | Rank 1 Initial\nDisposition: Pragmatic (中庸)\nStones: 120 块 | Age: 20/100",
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _lblNPCDetails.AddThemeColorOverride("font_color", new Color(0.204f, 0.827f, 0.600f, 1f));
            _lblNPCDetails.AddThemeFontSizeOverride("font_size", 11);
            leftVBox.AddChild(_lblNPCDetails);

            var lblFavorTitle = new Label
            {
                Text = "Favorability (好感度):",
                HorizontalAlignment = HorizontalAlignment.Left
            };
            lblFavorTitle.AddThemeColorOverride("font_color", new Color(0.88f, 0.92f, 0.94f, 1f));
            lblFavorTitle.AddThemeFontSizeOverride("font_size", 10);
            leftVBox.AddChild(lblFavorTitle);

            _barFavorability = new ProgressBar
            {
                MinValue = -300,
                MaxValue = 300,
                Value = 0,
                CustomMinimumSize = new Vector2(280, 16),
                ShowPercentage = false
            };
            leftVBox.AddChild(_barFavorability);

            // Right Section: Speech bubble, feedback, and action buttons
            var rightVBox = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            rightVBox.AddThemeConstantOverride("separation", 8);
            mainHBox.AddChild(rightVBox);

            _lblSpeechText = new Label
            {
                Text = "\"Greetings, fellow cultivator. Speak your business.\"",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(0, 48)
            };
            _lblSpeechText.AddThemeColorOverride("font_color", new Color(0.95f, 0.95f, 0.90f, 1f));
            _lblSpeechText.AddThemeFontSizeOverride("font_size", 12);
            rightVBox.AddChild(_lblSpeechText);

            _lblFeedbackText = new Label
            {
                Text = "",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(0, 24)
            };
            _lblFeedbackText.AddThemeColorOverride("font_color", new Color(0.984f, 0.749f, 0.141f, 1f));
            _lblFeedbackText.AddThemeFontSizeOverride("font_size", 11);
            rightVBox.AddChild(_lblFeedbackText);

            // Action Buttons
            var actionsHBox = new HBoxContainer();
            actionsHBox.AddThemeConstantOverride("separation", 8);
            rightVBox.AddChild(actionsHBox);

            _btnSpar = CreateActionButton("Spar (切磋)\n[+25 Favor]", new Color(0.20f, 0.83f, 0.60f, 1f), () =>
            {
                if (_currentNPC != null) GameEventManager.RaiseRequestNPCSpar(_currentNPC.EntityId);
            });
            actionsHBox.AddChild(_btnSpar);

            _btnBarter = CreateActionButton("Barter (交易)\n[Rations & Stones]", new Color(0.22f, 0.74f, 0.97f, 1f), () =>
            {
                if (_currentNPC != null) GameEventManager.RaiseRequestNPCBarter(_currentNPC.EntityId);
            });
            actionsHBox.AddChild(_btnBarter);

            _btnExtort = CreateActionButton("Extort (勒索)\n[Demand Stones]", new Color(0.95f, 0.65f, 0.20f, 1f), () =>
            {
                if (_currentNPC != null) GameEventManager.RaiseRequestNPCExtort(_currentNPC.EntityId);
            });
            actionsHBox.AddChild(_btnExtort);

            _btnAmbush = CreateActionButton("Ambush (截杀)\n[Lethal Combat]", new Color(0.94f, 0.35f, 0.35f, 1f), () =>
            {
                if (_currentNPC != null) GameEventManager.RaiseRequestNPCAmbush(_currentNPC.EntityId);
            });
            actionsHBox.AddChild(_btnAmbush);

            _btnDepart = CreateActionButton("Depart (告辞)\n[Esc / Leave]", new Color(0.80f, 0.85f, 0.88f, 1f), () =>
            {
                GameEventManager.RaiseRequestCloseNPCDialogue();
            });
            actionsHBox.AddChild(_btnDepart);
        }

        private Button CreateActionButton(string text, Color accentColor, Action onClick)
        {
            var btn = new Button
            {
                Text = text,
                CustomMinimumSize = new Vector2(105, 40),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
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
            btn.AddThemeFontSizeOverride("font_size", 11);
            btn.Pressed += onClick;
            return btn;
        }

        private void HandleDialogueOpened(CultivatorEntityData npc, string greeting)
        {
            _currentNPC = npc;
            _isOpen = true;
            Visible = true;
            GameEventManager.RaiseModalOpened(this);

            if (_lblNPCName != null)
            {
                _lblNPCName.Text = $"【修】 {npc.Name}";
            }
            if (_lblNPCDetails != null)
            {
                _lblNPCDetails.Text = $"Clan: {npc.Clan} | Rank {npc.Rank} (Substage {npc.Substage})\nDisposition: {npc.Disposition}\nStones: {npc.PrimevalStones} 块 | Age: {npc.Age}/{npc.LifespanYears} yrs";
            }
            if (_barFavorability != null)
            {
                _barFavorability.Value = npc.Favorability;
            }
            if (_lblSpeechText != null)
            {
                _lblSpeechText.Text = greeting;
            }
            if (_lblFeedbackText != null)
            {
                _lblFeedbackText.Text = "";
            }
        }

        private void HandleDialogueFeedback(string feedback)
        {
            if (_lblFeedbackText != null)
            {
                _lblFeedbackText.Text = feedback;
            }
            if (_barFavorability != null && _currentNPC != null)
            {
                _barFavorability.Value = _currentNPC.Favorability;
            }
        }

        private void HandleDialogueClosed()
        {
            _isOpen = false;
            Visible = false;
            _currentNPC = null;
            GameEventManager.RaiseModalClosed(this);
        }

        private void HandleEscapeRequested()
        {
            if (!_isOpen) return;

            GameEventManager.EscapeHandled = true;
            GameEventManager.RaiseRequestCloseNPCDialogue();
        }
    }
}
