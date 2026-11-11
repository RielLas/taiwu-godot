using System;
using Godot;
using TaiwuGu.Core;
using TaiwuGu.Entities;

namespace Taiwu_Godot.Scripts.Input
{
    /// <summary>
    /// Standalone Desktop Hardware Input Provider (Godot 2D).
    /// Handles mouse wheel zoom, drag/WASD pan, and pointer world clicks inside _UnhandledInput.
    /// </summary>
    public partial class StandaloneInputProvider : Node2D
    {
        private bool _isDragging = false;
        private bool _isPointerDown = false;
        private Vector2 _pointerDownPos;
        private bool _dragThresholdExceeded = false;
        private bool _isModalActive = false;
        private bool _isBattleLocked = false;
        private const float DRAG_THRESHOLD = 12f;

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;
        }

        public void SetModalActive(bool active)
        {
            _isModalActive = active;
            if (active) ResetPointerState();
        }

        public void SetBattleLocked(bool locked)
        {
            _isBattleLocked = locked;
            if (locked) ResetPointerState();
        }

        private void ResetPointerState()
        {
            _isDragging = false;
            _isPointerDown = false;
            _dragThresholdExceeded = false;
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (_isModalActive || _isBattleLocked) return;

            if (@event is InputEventMouseButton mb)
            {

                if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed)
                {
                    WorldSignals.EmitZoom(1.0f);
                    GetViewport().SetInputAsHandled();
                    return;
                }
                else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed)
                {
                    WorldSignals.EmitZoom(-1.0f);
                    GetViewport().SetInputAsHandled();
                    return;
                }

                if (mb.ButtonIndex == MouseButton.Middle || mb.ButtonIndex == MouseButton.Right)
                {
                    _isDragging = mb.Pressed;
                    GetViewport().SetInputAsHandled();
                    return;
                }

                if (mb.ButtonIndex == MouseButton.Left)
                {
                    if (mb.Pressed)
                    {
                        _isPointerDown = true;
                        _pointerDownPos = mb.Position;
                        _dragThresholdExceeded = false;
                    }
                    else if (_isPointerDown)
                    {
                        if (!_dragThresholdExceeded)
                        {
                            Vector2 worldPos = GetGlobalMousePosition();
                            WorldSignals.EmitWorldClick(worldPos.ToWorldVec2());
                        }
                        _isPointerDown = false;
                        _dragThresholdExceeded = false;
                    }
                    GetViewport().SetInputAsHandled();
                    return;
                }
            }
            else if (@event is InputEventMouseMotion mm)
            {
                if (_isPointerDown && !_dragThresholdExceeded &&
                    mm.Position.DistanceTo(_pointerDownPos) > DRAG_THRESHOLD)
                {
                    _dragThresholdExceeded = true;
                }

                if (_isDragging)
                {
                    WorldSignals.EmitPan(mm.Relative.ToWorldVec2());
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        public override void _Process(double delta)
        {
            if (_isModalActive || _isBattleLocked) return;

            Vector2 kbMove = Vector2.Zero;
            if (Godot.Input.IsKeyPressed(Key.W) || Godot.Input.IsKeyPressed(Key.Up)) kbMove.Y -= 1f;
            if (Godot.Input.IsKeyPressed(Key.S) || Godot.Input.IsKeyPressed(Key.Down)) kbMove.Y += 1f;
            if (Godot.Input.IsKeyPressed(Key.A) || Godot.Input.IsKeyPressed(Key.Left)) kbMove.X -= 1f;
            if (Godot.Input.IsKeyPressed(Key.D) || Godot.Input.IsKeyPressed(Key.Right)) kbMove.X += 1f;

            if (kbMove != Vector2.Zero)
            {
                Vector2 dir = kbMove.Normalized();
                WorldSignals.EmitPan(new WorldVec2(dir.X * 10000f, dir.Y * 10000f));
            }
        }
    }
}
