using System;
using Godot;
using TaiwuGu.Core;
using Taiwu_Godot.Scripts.UI;

namespace Taiwu_Godot.Scripts.Input
{
    /// <summary>
    /// Phase 2: Central Hardware Input Gateway & Router (Godot 2D).
    /// Dispatches hardware input events to active input providers.
    /// Strictly obeys Constitutional Law 1 (Observer Mandate).
    /// </summary>
    public partial class TaiwuInputRouter : Node
    {
        [Export] public StandaloneInputProvider? StandaloneProvider { get; set; }
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
    }
}
