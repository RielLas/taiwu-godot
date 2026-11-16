using System;
using Godot;
using TaiwuGu.Core;
using TaiwuGu.Entities;

namespace Taiwu_Godot.Scripts.Map
{
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
        private float _trauma = 0f;
        private bool _isPauseLocked = false;
        private bool _isUIModalLocked = false;
        private bool _isBattleLocked = false;

        public override void _Ready()
        {
            _targetZoom = DefaultZoom;
            Zoom = new Vector2(_targetZoom, _targetZoom);

            WorldSignals.OnPauseToggled += HandlePauseToggled;
            WorldSignals.OnUIModalActive += HandleUIModalActive;
            WorldSignals.OnBattleStageChanged += HandleBattleStageStateChanged;
            WorldSignals.OnPanRequested += HandlePan;
            WorldSignals.OnZoomRequested += HandleZoom;
            WorldSignals.OnRecenterCameraRequested += LockOnTarget;
            WorldSignals.OnTopicsChanged += HandleTopicsChanged;

            if (Target != null)
            {
                Position = Target.Position;
            }
        }

        public override void _ExitTree()
        {
            WorldSignals.OnPauseToggled -= HandlePauseToggled;
            WorldSignals.OnUIModalActive -= HandleUIModalActive;
            WorldSignals.OnBattleStageChanged -= HandleBattleStageStateChanged;
            WorldSignals.OnPanRequested -= HandlePan;
            WorldSignals.OnZoomRequested -= HandleZoom;
            WorldSignals.OnRecenterCameraRequested -= LockOnTarget;
            WorldSignals.OnTopicsChanged -= HandleTopicsChanged;
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

        public void LockOnTarget() => IsLockedToTarget = true;
        public void UnlockTarget() => IsLockedToTarget = false;

        public void AddTrauma(float amount)
        {
            _trauma = Math.Clamp(_trauma + amount, 0f, 1f);
        }

        private void HandlePauseToggled(bool isPaused) => _isPauseLocked = isPaused;
        private void HandleUIModalActive(bool isActive) => _isUIModalLocked = isActive;
        private void HandleBattleStageStateChanged(bool inBattle)
        {
            _isBattleLocked = inBattle;
            if (!inBattle)
            {
                _trauma = 0f;
                Offset = Vector2.Zero;
            }
        }

        private void HandleTopicsChanged(DirtyTopic topics)
        {
            if ((topics & DirtyTopic.PlayerPos) == 0) return;
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
            float dt = (float)delta;

            // Decaying camera trauma shake (Trauma^2 non-linear falloff)
            if (_trauma > 0f)
            {
                _trauma = MathF.Max(0f, _trauma - dt * 1.8f);
                float shake = _trauma * _trauma;
                float maxOffset = 0.45f;
                Offset = new Vector2(
                    (float)(Random.Shared.NextDouble() * 2.0 - 1.0) * maxOffset * shake,
                    (float)(Random.Shared.NextDouble() * 2.0 - 1.0) * maxOffset * shake
                );
            }
            else if (Offset != Vector2.Zero)
            {
                Offset = Vector2.Zero;
            }

            if (_isPauseLocked || _isUIModalLocked || _isBattleLocked) return;

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
