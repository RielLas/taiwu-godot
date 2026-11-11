using System;
using Godot;
using TaiwuGu.Combat;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using Color = Godot.Color;
using Mathf = Godot.Mathf;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Real-time battle HUD: direct-reads the live arena simulation and WorldStore each frame.
    /// Visible only while WorldSignals.OnBattleStageChanged is true.
    /// </summary>
    public partial class CombatHUDView : Control
    {
        private IWorldStateProvider? _provider;

        private ProgressBar? _barHealth;
        private Label? _lblHealth;
        private ProgressBar? _barSea;
        private Label? _lblSea;
        private Label? _lblEnemy;
        private Button? _btnStone;
        private Button? _btnRetreat;
        private readonly Button?[] _skillButtons = new Button?[CombatArenaSimulation.MAX_SKILLS];

        public void Initialize(IWorldStateProvider provider) => _provider = provider;

        public override void _Ready()
        {
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;
            Visible = false;

            BuildUIHierarchy();

            WorldSignals.OnBattleStageChanged += HandleBattleStageChanged;
            WorldSignals.OnEscapeRequested += HandleEscapeRequested;
            WorldSignals.OnTopicsChanged += HandleTopicsChanged;

            RefreshFromStore();
        }

        public override void _ExitTree()
        {
            WorldSignals.OnBattleStageChanged -= HandleBattleStageChanged;
            WorldSignals.OnEscapeRequested -= HandleEscapeRequested;
            WorldSignals.OnTopicsChanged -= HandleTopicsChanged;
        }

        public override void _Process(double delta)
        {
            if (Visible) RefreshFromStore();
        }

        private void BuildUIHierarchy()
        {
            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.02f, 0.035f, 0.039f, 0.95f),
                BorderColor = new Color(0.118f, 0.161f, 0.231f, 1f),
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderWidthTop = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2,
                CornerRadiusBottomLeft = 2,
                CornerRadiusBottomRight = 2
            };

            var fillJade = new StyleBoxFlat { BgColor = new Color(0.20f, 0.83f, 0.60f, 1f) };
            var fillEmerald = new StyleBoxFlat { BgColor = new Color(0.024f, 0.494f, 0.408f, 1f) };

            // --- Top-left player vitals ---
            var vitals = new VBoxContainer
            {
                Name = "Vitals",
                Position = new Vector2(18f, 16f),
                CustomMinimumSize = new Vector2(280f, 76f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            vitals.AddThemeConstantOverride("separation", 3);
            AddChild(vitals);

            _lblHealth = new Label
            {
                Text = "HP 100/100",
                Modulate = new Color(0.88f, 0.91f, 0.94f, 1f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            _lblHealth.AddThemeFontSizeOverride("font_size", 11);
            vitals.AddChild(_lblHealth);

            _barHealth = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 100,
                Value = 100,
                ShowPercentage = false,
                CustomMinimumSize = new Vector2(280f, 14f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            _barHealth.AddThemeStyleboxOverride("background", style);
            _barHealth.AddThemeStyleboxOverride("fill", fillJade);
            vitals.AddChild(_barHealth);

            _lblSea = new Label
            {
                Text = "Sea 44.0% / 44.0%",
                Modulate = new Color(0.13f, 0.83f, 0.93f, 1f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            _lblSea.AddThemeFontSizeOverride("font_size", 11);
            vitals.AddChild(_lblSea);

            _barSea = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 44,
                Value = 44,
                ShowPercentage = false,
                CustomMinimumSize = new Vector2(280f, 10f),
                MouseFilter = MouseFilterEnum.Ignore
            };
            _barSea.AddThemeStyleboxOverride("background", (StyleBox)style.Duplicate());
            _barSea.AddThemeStyleboxOverride("fill", fillEmerald);
            vitals.AddChild(_barSea);

            // --- Enemy title ---
            _lblEnemy = new Label
            {
                Name = "EnemyTitle",
                Text = "【战】",
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
                Modulate = new Color(0.98f, 0.75f, 0.14f, 1f)
            };
            _lblEnemy.SetAnchorsPreset(LayoutPreset.TopWide);
            _lblEnemy.OffsetTop = 14f;
            _lblEnemy.OffsetBottom = 44f;
            _lblEnemy.AddThemeFontSizeOverride("font_size", 15);
            AddChild(_lblEnemy);

            // --- Controls hint ---
            var hint = new Label
            {
                Name = "ControlHint",
                Text = "[WASD] Move   [Mouse] Aim   [1-5] Cast Gu   [Space] Dash   [Q] Crush Stone",
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
                Modulate = new Color(0.58f, 0.64f, 0.72f, 0.9f)
            };
            hint.SetAnchorsPreset(LayoutPreset.BottomWide);
            hint.OffsetTop = -112f;
            hint.OffsetBottom = -86f;
            hint.AddThemeFontSizeOverride("font_size", 10);
            AddChild(hint);

            // --- Bottom hotbar ---
            var hotbar = new HBoxContainer
            {
                Name = "Hotbar",
                Alignment = BoxContainer.AlignmentMode.Center,
                MouseFilter = MouseFilterEnum.Ignore
            };
            hotbar.SetAnchorsPreset(LayoutPreset.BottomWide);
            hotbar.OffsetLeft = 24f;
            hotbar.OffsetRight = -24f;
            hotbar.OffsetTop = -82f;
            hotbar.OffsetBottom = -16f;
            hotbar.AddThemeConstantOverride("separation", 6);
            AddChild(hotbar);

            for (int i = 0; i < CombatArenaSimulation.MAX_SKILLS; i++)
            {
                int slot = i;
                var button = new Button
                {
                    Text = $"{i + 1} —",
                    CustomMinimumSize = new Vector2(0f, 60f),
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    MouseFilter = MouseFilterEnum.Stop,
                    FocusMode = FocusModeEnum.None,
                    ClipText = true
                };
                button.AddThemeStyleboxOverride("normal", style);
                button.AddThemeColorOverride("font_color", new Color(0.88f, 0.91f, 0.94f, 1f));
                button.AddThemeFontSizeOverride("font_size", 10);
                button.Pressed += () =>
                {
                    var world = _provider?.Current;
                    if (world == null) return;
                    var session = world.Get<CombatSessionComponent>(world.WorldEntity);
                    session?.Simulation.TryCastSkill(world, slot);
                };
                _skillButtons[i] = button;
                hotbar.AddChild(button);
            }

            _btnStone = new Button
            {
                Text = "Q Crush Stone (0)",
                CustomMinimumSize = new Vector2(0f, 60f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Stop,
                FocusMode = FocusModeEnum.None
            };
            _btnStone.AddThemeStyleboxOverride("normal", style);
            _btnStone.AddThemeColorOverride("font_color", new Color(0.98f, 0.75f, 0.14f, 1f));
            _btnStone.AddThemeFontSizeOverride("font_size", 10);
            _btnStone.Pressed += () =>
            {
                var world = _provider?.Current;
                if (world == null) return;
                var session = world.Get<CombatSessionComponent>(world.WorldEntity);
                session?.Simulation.TryCrushStone(world);
            };
            hotbar.AddChild(_btnStone);

            _btnRetreat = new Button
            {
                Text = "Retreat [Esc]",
                CustomMinimumSize = new Vector2(0f, 60f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Stop,
                FocusMode = FocusModeEnum.None
            };
            _btnRetreat.AddThemeStyleboxOverride("normal", style);
            _btnRetreat.AddThemeColorOverride("font_color", new Color(0.94f, 0.35f, 0.35f, 1f));
            _btnRetreat.AddThemeFontSizeOverride("font_size", 10);
            _btnRetreat.Pressed += () =>
            {
                var world = _provider?.Current;
                if (world == null) return;
                var session = world.Get<CombatSessionComponent>(world.WorldEntity);
                session?.Simulation.RequestRetreat();
            };
            hotbar.AddChild(_btnRetreat);
        }

        private void HandleBattleStageChanged(bool inBattle)
        {
            Visible = inBattle;
            if (inBattle) RefreshFromStore();
        }

        private void HandleTopicsChanged(DirtyTopic topics)
        {
            if (Visible) RefreshFromStore();
        }

        private void HandleEscapeRequested()
        {
            if (!Visible || WorldSignals.EscapeHandled) return;

            var world = _provider?.Current;
            if (world == null) return;
            var session = world.Get<CombatSessionComponent>(world.WorldEntity);
            if (session == null || !session.InBattle) return;

            WorldSignals.EscapeHandled = true;
            session.Simulation.RequestRetreat();
        }

        private void RefreshFromStore()
        {
            var world = _provider?.Current;
            if (world == null) return;
            var session = world.Get<CombatSessionComponent>(world.WorldEntity);
            if (session == null) return;

            var simulation = session.Simulation;
            var aperture = world.Get<ApertureComponent>(world.PlayerEntity);
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity);

            if (aperture != null)
            {
                if (_barHealth != null)
                {
                    _barHealth.MaxValue = Mathf.Max(1f, aperture.MaxHealth);
                    _barHealth.Value = aperture.CurrentHealth;
                }
                if (_lblHealth != null)
                {
                    _lblHealth.Text = $"HP {aperture.CurrentHealth:F0}/{aperture.MaxHealth:F0}";
                }
            }

            if (_barSea != null && cult != null)
            {
                _barSea.MaxValue = Mathf.Max(0.01f, cult.AptitudePercent);
            }
            if (_barSea != null && aperture != null)
            {
                _barSea.Value = aperture.CurrentEssenceSea;
            }
            if (_lblSea != null)
            {
                _lblSea.Text = $"Sea {aperture?.CurrentEssenceSea ?? 0f:F1}% / {cult?.AptitudePercent ?? 0f:F1}%";
            }

            if (_lblEnemy != null)
            {
                _lblEnemy.Text = $"【战】 {simulation.EnemyDisplayName}";
            }

            for (int i = 0; i < CombatArenaSimulation.MAX_SKILLS; i++)
            {
                var button = _skillButtons[i];
                if (button == null) continue;

                CombatSkillSlot skill = simulation.PlayerSkills[i];
                if (!skill.IsPopulated)
                {
                    button.Text = $"{i + 1} —";
                    continue;
                }

                string cooldown = skill.CooldownRemaining > 0f ? $"{skill.CooldownRemaining:F1}s" : "Ready";
                button.Text = $"{i + 1} {skill.DisplayName}\n{cooldown}";
            }

            if (_btnStone != null)
            {
                _btnStone.Text = $"Q Crush Stone ({cult?.PrimevalStones ?? 0})";
            }
        }
    }
}
