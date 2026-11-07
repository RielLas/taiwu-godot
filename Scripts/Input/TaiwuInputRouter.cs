using System;
using Godot;
using TaiwuGu.Core;
using Taiwu_Godot.Scripts.Map;
using Taiwu_Godot.Scripts.UI;

namespace Taiwu_Godot.Scripts.Input
{
    /// <summary>
    /// The single place the game reads keyboard intent.
    ///
    /// Bindings live in <c>project.godot</c>'s <c>[input]</c> map, so remapping a key is a project
    /// setting rather than a code change. Pointer input (drag, wheel, click) stays in
    /// <see cref="StandaloneInputProvider"/> because it is not an action. The bootstrapper no longer
    /// inspects keycodes at all.
    /// </summary>
    public partial class TaiwuInputRouter : Node
    {
        [Export] public StandaloneInputProvider? StandaloneProvider { get; set; }

        // Routed-to handlers, assigned by the bootstrapper (no scene searching).
        public PlayerCultivatorController? PlayerController { get; set; }
        public TaiwuSystemMenuView? SystemMenuView { get; set; }
        public ApertureBreakthroughModalView? BreakthroughView { get; set; }

        private TaiwuModalStack? _modalStack;

        public void InitializeModalStack(TaiwuModalStack modalStack) => _modalStack = modalStack;

        public bool IsModalActive { get; private set; } = false;

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;
            if (StandaloneProvider == null)
                GD.PushError("TaiwuInputRouter requires StandaloneProvider to be assigned in the scene.");
            else
                StandaloneProvider.ProcessMode = ProcessModeEnum.Always;

            GameEventManager.OnUIModalActive += HandleUIModalActive;
        }

        public override void _ExitTree()
        {
            GameEventManager.OnUIModalActive -= HandleUIModalActive;
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

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is not InputEventKey key || !key.Pressed || key.Echo) return;

            // Order matters: ui_cancel owns the LIFO chain, so a second action bound to the same
            // key (toggle_system_menu defaults to Escape) never double-fires.
            if (key.IsActionPressed("ui_cancel"))
            {
                ConsumeEscape();
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.IsActionPressed("toggle_ledger"))
            {
                if (!IsModalActive || _modalStack?.IsModalOpen<TaiwuLedgerView>() == true)
                {
                    GameEventManager.TriggerLedgerToggle();
                }
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.IsActionPressed("open_cauldron"))
            {
                if (_modalStack?.IsModalOpen<RefinementCauldronView>() == true)
                {
                    GameEventManager.RaiseRequestCloseCauldron();
                }
                else if (!IsModalActive || _modalStack?.IsModalOpen<TaiwuLedgerView>() == true)
                {
                    GameEventManager.RaiseRequestOpenCauldron();
                }
                GetViewport().SetInputAsHandled();
                return;
            }

            if (key.IsActionPressed("open_breakthrough"))
            {
                if (BreakthroughView?.IsOpen == true)
                {
                    GameEventManager.RaiseRequestCloseBreakthrough();
                }
                else
                {
                    GameEventManager.RaiseRequestOpenBreakthroughMinigame();
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
                GameEventManager.TriggerRecenterCamera();
                GetViewport().SetInputAsHandled();
            }
        }

        /// <summary>Escape order: dismiss the top modal, then a pending move, then the system menu.</summary>
        private void ConsumeEscape()
        {
            if (_modalStack != null && _modalStack.TryPop()) return;

            if (PlayerController?.HasTargetSelection == true)
            {
                PlayerController.ClearTargetSelection();
                return;
            }

            GameEventManager.TriggerEscape();
        }
    }
}
