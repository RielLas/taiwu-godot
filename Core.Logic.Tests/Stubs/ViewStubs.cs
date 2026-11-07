using System;
using System.Collections.Generic;
using TaiwuGu.Core;
using TaiwuGu.Combat;
using TaiwuGu.Map;

namespace TaiwuGu.UI
{
    /// <summary>
    /// Passive view stub for architectural decoupling verification tests.
    /// Strictly adheres to Law 1: zero domain service references.
    /// </summary>
    public class TaiwuHUDView
    {
        // UI fields only - no domain services or GameStateContext references
        public bool IsVisible { get; set; }
    }

    /// <summary>
    /// Passive view stub for architectural decoupling verification tests.
    /// </summary>
    public class TaiwuLedgerView
    {
        public bool IsOpen { get; set; }
    }

    /// <summary>
    /// Passive view stub for architectural decoupling verification tests.
    /// </summary>
    public class TilePOIModalView
    {
        public bool IsOpen { get; set; }
        public bool IsModalOpen { get => IsOpen; set => IsOpen = value; }
    }

    /// <summary>
    /// Passive view stub for architectural decoupling verification tests.
    /// </summary>
    public class RefinementCauldronView
    {
        public bool IsOpen { get; set; }
        public bool IsActive { get => IsOpen; set => IsOpen = value; }
    }

    /// <summary>
    /// Passive view stub for architectural decoupling verification tests.
    /// </summary>
    public class TaiwuSystemMenuView
    {
        public bool IsOpen { get; set; }
    }
}

namespace TaiwuGu.Core
{
    public class TaiwuInputRouter
    {
        public InputControlMode ControlMode { get; private set; } = InputControlMode.Overworld;
        public bool InputActive { get; private set; } = true;

        public void SetInputActive(bool active) => InputActive = active;

        public void OnEnable()
        {
            GameEventManager.OnCauldronOpened += HandleCauldronOpened;
            GameEventManager.OnCauldronClosed += HandleCauldronClosed;
            GameEventManager.OnBattleStageStateChanged += HandleBattleStageChanged;
        }

        public void OnDisable()
        {
            GameEventManager.OnCauldronOpened -= HandleCauldronOpened;
            GameEventManager.OnCauldronClosed -= HandleCauldronClosed;
            GameEventManager.OnBattleStageStateChanged -= HandleBattleStageChanged;
        }

        private void HandleCauldronOpened(CauldronRefinementState state)
        {
            ControlMode = InputControlMode.AlchemicalCauldron;
        }

        private void HandleCauldronClosed()
        {
            ControlMode = InputControlMode.Overworld;
        }

        private void HandleBattleStageChanged(bool inBattle)
        {
            ControlMode = inBattle ? InputControlMode.CombatArena : InputControlMode.Overworld;
        }
    }
}

namespace TaiwuGu.Combat
{
    public static class FullscreenCombatViewport
    {
        public static readonly Vector3 ArenaOrigin = new Vector3(2000f, 2000f, 0f);
    }

    public class DedicatedBattleStage
    {
        public bool IsArenaActive { get; private set; }
        public Vector2 CurrentMoveIntent { get; private set; } = Vector2.zero;

        public void OnEnable()
        {
            GameEventManager.OnCombatMoveRequested += HandleMoveRequest;
        }

        public void OnDisable()
        {
            GameEventManager.OnCombatMoveRequested -= HandleMoveRequest;
        }

        private void HandleMoveRequest(Vector2 dir)
        {
            CurrentMoveIntent = dir;
        }

        public void EnterBattleStage(CultivatorData cultivator, List<GuWormData> loadout, ArenaEncounterType encounterType, Vector2Int originGrid)
        {
            IsArenaActive = true;
        }

        public void ExitBattleStage()
        {
            IsArenaActive = false;
        }
    }
}
