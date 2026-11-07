using System;

namespace TaiwuGu.Core
{
    public enum GameState
    {
        MainMenu,
        OverworldPlay,
        Battle,
        Paused
    }

    /// <summary>
    /// Global State Machine & Temporal Lock Engine.
    /// Manages high-level game states and broadcasts events.
    /// </summary>
    public static class GameStateManager
    {
        private static GameState _currentState = GameState.OverworldPlay;
        private static GameState _previousState = GameState.OverworldPlay;

        public static GameState CurrentState => _currentState;
        public static bool IsPaused => _currentState == GameState.Paused;

        public static event Action<GameState, GameState>? OnGameStateChanged;
        public static event Action<bool>? OnPauseToggled;

        public static void SetState(GameState newState)
        {
            if (_currentState == newState) return;

            GameState oldState = _currentState;
            _previousState = oldState;
            _currentState = newState;

            if (newState == GameState.Paused)
            {
                OnPauseToggled?.Invoke(true);
                GameEventManager.TriggerPauseToggled(true);
            }
            else if (oldState == GameState.Paused)
            {
                OnPauseToggled?.Invoke(false);
                GameEventManager.TriggerPauseToggled(false);
            }

            Debug.Log($"[GameStateManager] State Transition: {oldState} -> {newState}");
            OnGameStateChanged?.Invoke(oldState, newState);
        }

        public static void TogglePause()
        {
            if (_currentState == GameState.Paused)
            {
                SetState(_previousState);
            }
            else
            {
                SetState(GameState.Paused);
            }
        }

        public static void ResumeGame()
        {
            if (_currentState == GameState.Paused)
            {
                SetState(_previousState);
            }
        }
    }
}
