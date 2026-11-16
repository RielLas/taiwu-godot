using System;
using Godot;
using TaiwuGu.Entities;
using TaiwuGu.Systems;
using Taiwu_Godot.Scripts.Map;
using Taiwu_Godot.Scripts.UI;

namespace Taiwu_Godot.Scripts.Input
{
    /// <summary>
    /// Single entry point for keyboard action bindings defined in project.godot.
    /// Routes directly to modal controllers and static ECS Systems.
    /// </summary>
    public partial class TaiwuInputRouter : Node
    {
        [Export] public StandaloneInputProvider? StandaloneProvider { get; set; }

        public IWorldStateProvider? WorldProvider { get; set; }
        public PlayerCultivatorController? PlayerController { get; set; }
        public TaiwuSystemMenuView? SystemMenuView { get; set; }
        public ApertureBreakthroughModalView? BreakthroughView { get; set; }
        public WorldAtlasModalView? WorldAtlasView { get; set; }

        private TaiwuModalStack? _modalStack;
        private bool _isBattleLocked = false;

        public void Initialize(TaiwuModalStack modalStack, IWorldStateProvider provider)
        {
            _modalStack = modalStack;
            WorldProvider = provider;
        }

        public bool IsModalActive { get; private set; } = false;

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;
            if (StandaloneProvider == null)
                GD.PushError("TaiwuInputRouter requires StandaloneProvider to be assigned in the scene.");
            else
                StandaloneProvider.ProcessMode = ProcessModeEnum.Always;

            WorldSignals.OnUIModalActive += HandleUIModalActive;
            WorldSignals.OnBattleStageChanged += HandleBattleStageChanged;
        }

        public override void _ExitTree()
        {
            WorldSignals.OnUIModalActive -= HandleUIModalActive;
            WorldSignals.OnBattleStageChanged -= HandleBattleStageChanged;
        }

        private void HandleBattleStageChanged(bool inBattle)
        {
            _isBattleLocked = inBattle;
            StandaloneProvider?.SetBattleLocked(inBattle);
        }

        private void HandleUIModalActive(bool isActive)
        {
            if (!isActive)
            {
                if (_modalStack?.IsModalOpen<TaiwuLedgerView>() == true ||
                    _modalStack?.IsModalOpen<RefinementCauldronView>() == true ||
                    _modalStack?.IsModalOpen<TilePOIModalView>() == true ||
                    _modalStack?.IsModalOpen<TaiwuSystemMenuView>() == true)
                    return;
            }

            IsModalActive = isActive;
            StandaloneProvider?.SetModalActive(isActive);
        }

        public override void _Input(InputEvent @event)
        {
            if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;

            // Intercept Tab / L before Godot's built-in GUI focus navigation (ui_focus_next) consumes it
            if (key.Keycode == Key.Tab || key.PhysicalKeycode == Key.Tab ||
                key.Keycode == Key.L || key.PhysicalKeycode == Key.L ||
                key.IsActionPressed("toggle_ledger"))
            {
                // Guard against modifier shortcuts (Alt+Tab window swap, Shift+Tab reverse focus, Ctrl+L, etc.)
                if (key.CtrlPressed || key.AltPressed || key.MetaPressed || key.ShiftPressed) return;

                // Don't intercept if user is actively typing in a text field
                Control? focusOwner = GetViewport().GuiGetFocusOwner();
                if (focusOwner is LineEdit || focusOwner is TextEdit) return;

                // Consume Tab during combat so it cannot fall through to ui_focus_next
                if (_isBattleLocked)
                {
                    GetViewport().SetInputAsHandled();
                    return;
                }

                if (!IsModalActive || _modalStack?.IsModalOpen<TaiwuLedgerView>() == true)
                {
                    WorldSignals.EmitToggleLedger();
                }
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;

            if (key.IsActionPressed("ui_cancel"))
            {
                ConsumeEscape();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (_isBattleLocked &&
                (key.IsActionPressed("open_cauldron") ||
                 key.IsActionPressed("open_breakthrough") ||
                 key.IsActionPressed("recenter_camera") ||
                 key.IsActionPressed("toggle_atlas")))
            {
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.IsActionPressed("open_cauldron") && WorldProvider != null)
            {
                if (_modalStack?.IsModalOpen<RefinementCauldronView>() == true)
                {
                    RefinementSystem.CloseCauldron(WorldProvider.Current);
                }
                else if (!IsModalActive || _modalStack?.IsModalOpen<TaiwuLedgerView>() == true)
                {
                    RefinementSystem.OpenCauldron(WorldProvider.Current);
                }
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.IsActionPressed("open_breakthrough") && WorldProvider != null)
            {
                if (BreakthroughView?.IsOpen == true)
                {
                    BreakthroughSystem.CloseSession(WorldProvider.Current);
                }
                else
                {
                    BreakthroughSystem.OpenSession(WorldProvider.Current);
                }
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.IsActionPressed("toggle_atlas"))
            {
                if (!IsModalActive || _modalStack?.IsModalOpen<WorldAtlasModalView>() == true)
                {
                    if (WorldAtlasView != null)
                    {
                        WorldAtlasView.Toggle();
                    }
                    else if (WorldAtlasModalView.Instance != null)
                    {
                        WorldAtlasModalView.Instance.Toggle();
                    }
                }
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.IsActionPressed("toggle_system_menu"))
            {
                SystemMenuView?.ToggleMenu();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.IsActionPressed("recenter_camera"))
            {
                WorldSignals.EmitRecenterCamera();
                GetViewport().SetInputAsHandled();
            }
        }

        private void ConsumeEscape()
        {
            if (_modalStack != null && _modalStack.TryPop()) return;

            if (PlayerController?.HasTargetSelection == true)
            {
                PlayerController.ClearTargetSelection();
                return;
            }

            WorldSignals.EmitEscape();
        }
    }
}
