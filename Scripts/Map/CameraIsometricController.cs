using System;
using Godot;
using TaiwuGu.Core;

namespace Taiwu_Godot.Scripts.Map
{
    /// <summary>
    /// Phase 2: Isometric Camera Controller (Godot 2D).
    /// Locks cleanly to the PlayerCultivatorController with position smoothing and smooth zoom.
    /// Strictly obeys Constitutional Law 1 (Observer Mandate) and eliminates fragile edge-panning hacks.
    /// </summary>
    public partial class CameraIsometricController : Camera2D
    {
        [Export] public Node2D? Target { get; set; }
        [Export] public float FollowSpeed { get; set; } = 6.0f;
        [Export] public bool IsLockedToTarget { get; set; } = true;

        [Export] public float PanSpeed { get; set; } = 1.0f;
        [Export] public float ZoomSpeed { get; set; } = 0.15f;
        [Export] public float MinZoom { get; set; } = 15.0f;
        [Export] public float MaxZoom { get; set; } = 120.0f;
        [Export] public float DefaultZoom { get; set; } = 68.0f;

        private float _targetZoom = 68.0f;
        private bool _isPauseLocked = false;
        private bool _isUIModalLocked = false;
        private bool _isBattleLocked = false;

        public override void _Ready()
        {
            _targetZoom = DefaultZoom;
            Zoom = new Vector2(_targetZoom, _targetZoom);

            GameEventManager.OnPauseToggled += HandlePauseToggled;
            GameEventManager.OnUIModalActive += HandleUIModalActive;
            GameEventManager.OnBattleStageStateChanged += HandleBattleStageStateChanged;
            GameEventManager.OnPanRequested += HandlePan;
            GameEventManager.OnZoomRequested += HandleZoom;
            GameEventManager.OnRecenterCameraRequested += LockOnTarget;
            GameEventManager.OnPlayerMoved += HandlePlayerMoved;

            if (Target != null)
            {
                Position = Target.Position;
            }
        }

        public override void _ExitTree()
        {
            GameEventManager.OnPauseToggled -= HandlePauseToggled;
            GameEventManager.OnUIModalActive -= HandleUIModalActive;
            GameEventManager.OnBattleStageStateChanged -= HandleBattleStageStateChanged;
            GameEventManager.OnPanRequested -= HandlePan;
            GameEventManager.OnZoomRequested -= HandleZoom;
            GameEventManager.OnRecenterCameraRequested -= LockOnTarget;
            GameEventManager.OnPlayerMoved -= HandlePlayerMoved;
        }

        public void Initialize(Node2D targetNode)
        {
            Target = targetNode;
            IsLockedToTarget = true;
            if (Target != null)
            {
                Position = Target.Position;
            }
        }

        public void LockOnTarget()
        {
            IsLockedToTarget = true;
        }

        public void UnlockTarget()
        {
            IsLockedToTarget = false;
        }

        private void HandlePauseToggled(bool isPaused) => _isPauseLocked = isPaused;
        private void HandleUIModalActive(bool isActive) => _isUIModalLocked = isActive;
        private void HandleBattleStageStateChanged(bool inBattle) => _isBattleLocked = inBattle;

        private void HandlePlayerMoved(GridCoord newGridPos)
        {
            if (_isPauseLocked || _isUIModalLocked || _isBattleLocked) return;
            LockOnTarget();
        }

        private void HandlePan(WorldVec2 screenDelta)
        {
            if (_isPauseLocked || _isUIModalLocked || _isBattleLocked) return;

            IsLockedToTarget = false;

            float magSq = screenDelta.X * screenDelta.X + screenDelta.Y * screenDelta.Y;
            if (magSq >= 1000000f) // Keyboard synthetic delta: view moves in key direction
            {
                Vector2 dir = new Vector2(screenDelta.X, screenDelta.Y).Normalized();
                Position += dir * (PanSpeed * 12.0f / Zoom.X);
            }
            else // Mouse drag pixel delta: grab-and-drag moves world with mouse
            {
                Vector2 worldDelta = new Vector2(screenDelta.X, screenDelta.Y) / Zoom.X * PanSpeed;
                Position -= worldDelta;
            }
        }

        private void HandleZoom(float delta)
        {
            if (_isPauseLocked || _isUIModalLocked || _isBattleLocked) return;

            float factor = 1.0f + delta * ZoomSpeed;
            _targetZoom = Math.Clamp(_targetZoom * factor, MinZoom, MaxZoom);
        }

        public override void _Process(double delta)
        {
            if (_isPauseLocked || _isUIModalLocked || _isBattleLocked) return;

            float dt = (float)delta;

            // Smooth position follow when locked to player
            if (IsLockedToTarget && Target != null)
            {
                Position = Position.Lerp(Target.Position, FollowSpeed * dt);
            }

            // Smooth zoom lerp
            float currentZoom = Zoom.X;
            float newZoom = Godot.Mathf.Lerp(currentZoom, _targetZoom, 10.0f * dt);
            Zoom = new Vector2(newZoom, newZoom);
        }
    }
}
