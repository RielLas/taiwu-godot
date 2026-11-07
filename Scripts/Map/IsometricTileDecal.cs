using System;
using System.Collections.Generic;
using Godot;
using Color = Godot.Color;
using TaiwuGu.Core;

namespace Taiwu_Godot.Scripts.Map
{
    /// <summary>
    /// Phase 2: High-performance isometric selection decal (Godot 2D).
    /// Renders a crisp world-space diamond decal over the currently selected tile.
    /// Strictly obeys Constitutional Law 1 (Observer Mandate).
    /// </summary>
    public partial class IsometricTileDecal : Node2D
    {
        [Export] public Color OuterColor { get; set; } = new Color(0.9843f, 0.7490f, 0.1412f, 0.95f); // #FBBF24 bright outline
        [Export] public Color InnerColor { get; set; } = new Color(0.9608f, 0.6196f, 0.0431f, 0.35f); // #F59E0B @ 35% alpha
        [Export] public float LineWidth { get; set; } = 0.08f;

        private GridCoord? _selectedTargetPos = null;
        private Vector2 _lastMousePos = Vector2.Zero;

        public override void _Ready()
        {
            ZIndex = 2; // Directly above ground tiles (0) and grid lines (1)
            Visible = true;
            Position = ExplorationService.GridToWorld(new GridCoord(15, 14)).ToGodot();

            GameEventManager.OnTargetTileSelected += HandleTargetTileSelected;
        }

        public override void _ExitTree()
        {
            GameEventManager.OnTargetTileSelected -= HandleTargetTileSelected;
        }

        public override void _Process(double delta)
        {
            if (_selectedTargetPos.HasValue) return; // Keep locked to explicitly selected tile

            Vector2 mousePos = GetGlobalMousePosition();
            if (mousePos != _lastMousePos)
            {
                _lastMousePos = mousePos;
                GridCoord hoverCoord = ExplorationService.WorldToGrid(mousePos.ToWorldVec2());
                if (hoverCoord.X >= 0 && hoverCoord.X < 30 && hoverCoord.Y >= 0 && hoverCoord.Y < 30)
                {
                    Vector2 worldPos = ExplorationService.GridToWorld(hoverCoord).ToGodot();
                    Position = worldPos;
                    Visible = true;
                    QueueRedraw();
                }
            }
        }

        private void HandleTargetTileSelected(GridCoord? targetPos, List<GridCoord>? path)
        {
            _selectedTargetPos = targetPos;
            if (!targetPos.HasValue)
            {
                // Fall back to mouse hover position
                _lastMousePos = Vector2.Zero;
                return;
            }

            Vector2 worldPos = ExplorationService.GridToWorld(targetPos.Value).ToGodot();
            Position = worldPos;
            Visible = true;
            QueueRedraw();
        }

        public override void _Draw()
        {
            float hw = ExplorationService.TILE_HALF_WIDTH;
            float hh = ExplorationService.TILE_HALF_HEIGHT;

            // In Godot 2D: -Y is North, +Y is South
            Vector2[] diamondPoints = new Vector2[]
            {
                new Vector2(0f, -hh), // North
                new Vector2(hw, 0f),  // East
                new Vector2(0f, hh),  // South
                new Vector2(-hw, 0f), // West
                new Vector2(0f, -hh)  // Loop back to North
            };

            // Soft translucent diamond fill
            DrawColoredPolygon(new Vector2[]
            {
                new Vector2(0f, -hh),
                new Vector2(hw, 0f),
                new Vector2(0f, hh),
                new Vector2(-hw, 0f)
            }, InnerColor);

            // Crisp golden amber outline border
            DrawPolyline(diamondPoints, OuterColor, LineWidth, antialiased: true);
        }
    }
}
