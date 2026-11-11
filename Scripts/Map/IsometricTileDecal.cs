using System.Collections.Generic;
using Godot;
using Color = Godot.Color;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Systems;

namespace Taiwu_Godot.Scripts.Map
{
    public partial class IsometricTileDecal : Node2D
    {
        [Export] public Color OuterColor { get; set; } = new Color(0.9843f, 0.7490f, 0.1412f, 0.95f);
        [Export] public Color InnerColor { get; set; } = new Color(0.9608f, 0.6196f, 0.0431f, 0.35f);
        [Export] public float LineWidth { get; set; } = 0.08f;

        private GridCoord? _selectedTargetPos = null;
        private Vector2 _lastMousePos = Vector2.Zero;

        public override void _Ready()
        {
            ZIndex = 2;
            Visible = true;
            Position = ExplorationSystem.GridToWorld(new GridCoord(15, 14)).ToGodot();
            WorldSignals.OnTargetTileSelected += HandleTargetTileSelected;
        }

        public override void _ExitTree()
        {
            WorldSignals.OnTargetTileSelected -= HandleTargetTileSelected;
        }

        public override void _Process(double delta)
        {
            if (_selectedTargetPos.HasValue) return;

            Vector2 mousePos = GetGlobalMousePosition();
            if (mousePos != _lastMousePos)
            {
                _lastMousePos = mousePos;
                GridCoord hoverCoord = ExplorationSystem.WorldToGrid(mousePos.ToWorldVec2());
                if (hoverCoord.X >= 0 && hoverCoord.X < 30 && hoverCoord.Y >= 0 && hoverCoord.Y < 30)
                {
                    Position = ExplorationSystem.GridToWorld(hoverCoord).ToGodot();
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
                _lastMousePos = Vector2.Zero;
                return;
            }

            Position = ExplorationSystem.GridToWorld(targetPos.Value).ToGodot();
            Visible = true;
            QueueRedraw();
        }

        public override void _Draw()
        {
            float hw = ExplorationSystem.TILE_HALF_WIDTH;
            float hh = ExplorationSystem.TILE_HALF_HEIGHT;

            Vector2[] diamondPoints =
            {
                new(0f, -hh),
                new(hw, 0f),
                new(0f, hh),
                new(-hw, 0f),
                new(0f, -hh)
            };

            DrawColoredPolygon(new Vector2[]
            {
                new(0f, -hh),
                new(hw, 0f),
                new(0f, hh),
                new(-hw, 0f)
            }, InnerColor);

            DrawPolyline(diamondPoints, OuterColor, LineWidth, antialiased: true);
        }
    }
}
