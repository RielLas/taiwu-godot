using System;
using Godot;
using TaiwuGu.Core;
using Taiwu_Godot.Scripts.UI;

namespace Taiwu_Godot.Scripts.Input
{
    /// <summary>
    /// Phase 2: Standalone Desktop Hardware Input Provider (Godot 2D).
    /// Handles Keyboard, Mouse, and Desktop Wheel interactions.
    /// Critical Polish Point 2: Processes mouse clicks strictly inside _UnhandledInput
    /// so UI Control nodes in TaiwuHUDView automatically absorb clicks.
    /// </summary>
    public partial class StandaloneInputProvider : Node2D
    {
        private TaiwuModalStack? _modalStack;

        public void InitializeModalStack(TaiwuModalStack modalStack) => _modalStack = modalStack;

        private bool _isDragging = false;
        private bool _isPointerDown = false;
        private Vector2 _pointerDownPos;
        private bool _dragThresholdExceeded = false;
        private bool _isModalActive = false;
        private const float DRAG_THRESHOLD = 12f;

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;
        }

        public void SetModalActive(bool active)
        {
            _isModalActive = active;
            if (active)
            {
                _isDragging = false;
                _isPointerDown = false;
                _dragThresholdExceeded = false;
            }
        }

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb)
            {
                // Suppress overworld clicks and scroll wheel when modal is active
                if (_isModalActive) return;

                // Scroll Wheel Zoom
                if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed)
                {
                    GameEventManager.TriggerZoom(1.0f);
                    GetViewport().SetInputAsHandled();
                    return;
                }
                else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed)
                {
                    GameEventManager.TriggerZoom(-1.0f);
                    GetViewport().SetInputAsHandled();
                    return;
                }

                // Middle / Right Drag Panning
                if (mb.ButtonIndex == MouseButton.Middle || mb.ButtonIndex == MouseButton.Right)
                {
                    if (mb.Pressed)
                    {
                        _isDragging = true;
                    }
                    else
                    {
                        _isDragging = false;
                    }
                    GetViewport().SetInputAsHandled();
                    return;
                }

                // Primary Pointer Click (Left Mouse Button)
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
                            // In Godot 2D, GetGlobalMousePosition() accounts for camera transformation & zoom
                            Vector2 worldPos = GetGlobalMousePosition();
                            GameEventManager.TriggerWorldClick(worldPos.ToWorldVec2());
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
                if (_isModalActive) return;

                if (_isPointerDown && !_dragThresholdExceeded)
                {
                    if (mm.Position.DistanceTo(_pointerDownPos) > DRAG_THRESHOLD)
                    {
                        _dragThresholdExceeded = true;
                    }
                }

                if (_isDragging)
                {
                    GameEventManager.TriggerPan(mm.Relative.ToWorldVec2());
                    GetViewport().SetInputAsHandled();
                    return;
                }
            }
            else if (@event is InputEventKey k && k.Pressed && !k.Echo)
            {
                if (k.Keycode == Key.Escape)
                {
                    GameEventManager.TriggerEscape();
                    GetViewport().SetInputAsHandled();
                }
                else if (k.Keycode == Key.C || k.Keycode == Key.Tab)
                {
                    if (_isModalActive && _modalStack?.IsModalOpen<TaiwuLedgerView>() != true)
                    {
                        return;
                    }
                    GameEventManager.TriggerLedgerToggle();
                    GetViewport().SetInputAsHandled();
                }
                else if (k.Keycode == Key.R)
                {
                    if (_isModalActive)
                    {
                        if (_modalStack?.IsModalOpen<RefinementCauldronView>() == true)
                        {
                            GameEventManager.RaiseRequestCloseCauldron();
                            GetViewport().SetInputAsHandled();
                        }
                        return;
                    }
                    GameEventManager.RaiseRequestOpenCauldron();
                    GetViewport().SetInputAsHandled();
                }
                else if (!_isModalActive && (k.Keycode == Key.F || k.Keycode == Key.Home))
                {
                    GameEventManager.TriggerRecenterCamera();
                    GetViewport().SetInputAsHandled();
                }
            }
        }

        public override void _Process(double delta)
        {
            if (_isModalActive) return;

            // Continuous Keyboard Panning (WASD / Arrow Keys)
            Vector2 kbMove = Vector2.Zero;
            if (Godot.Input.IsKeyPressed(Key.W) || Godot.Input.IsKeyPressed(Key.Up)) kbMove.Y -= 1f;
            if (Godot.Input.IsKeyPressed(Key.S) || Godot.Input.IsKeyPressed(Key.Down)) kbMove.Y += 1f;
            if (Godot.Input.IsKeyPressed(Key.A) || Godot.Input.IsKeyPressed(Key.Left)) kbMove.X -= 1f;
            if (Godot.Input.IsKeyPressed(Key.D) || Godot.Input.IsKeyPressed(Key.Right)) kbMove.X += 1f;

            if (kbMove != Vector2.Zero)
            {
                Vector2 dir = kbMove.Normalized();
                GameEventManager.TriggerPan(new WorldVec2(dir.X * 10000f, dir.Y * 10000f));
            }
        }
    }
}
