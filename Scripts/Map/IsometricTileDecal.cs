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
        [Export] public Color OuterColor { get; set; } = new Color(0.2039f, 0.8275f, 0.6000f, 0.85f); // #34D399 Jade-Celadon
        [Export] public Color CornerColor { get; set; } = new Color(0.4039f, 0.9098f, 0.9765f, 0.95f); // #67E8F9 Luminous Celadon
        [Export] public Color InnerColor { get; set; } = new Color(0.2039f, 0.8275f, 0.6000f, 0.04f); // Subtle translucent jade mist (no heavy yellow)
        [Export] public float LineWidth { get; set; } = 0.028f; // ~1.5 - 2px in world space (at camera zoom ~68)

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

            if (InnerColor.A > 0.005f)
            {
                DrawColoredPolygon(new Vector2[]
                {
                    new(0f, -hh),
                    new(hw, 0f),
                    new(0f, hh),
                    new(-hw, 0f)
                }, InnerColor);
            }

            // Thin jade-celadon diamond outline (1.5-2px)
            DrawPolyline(diamondPoints, OuterColor, LineWidth, antialiased: true);

            // Refined celadon corner brackets (#67E8F9) at diamond vertices
            const float bracketFrac = 0.25f;
            float cornerWidth = LineWidth * 1.35f;

            // North corner bracket
            DrawPolyline(new Vector2[]
            {
                new(-hw * bracketFrac, -hh + hh * bracketFrac),
                new(0f, -hh),
                new(hw * bracketFrac, -hh + hh * bracketFrac)
            }, CornerColor, cornerWidth, antialiased: true);

            // East corner bracket
            DrawPolyline(new Vector2[]
            {
                new(hw - hw * bracketFrac, -hh * bracketFrac),
                new(hw, 0f),
                new(hw - hw * bracketFrac, hh * bracketFrac)
            }, CornerColor, cornerWidth, antialiased: true);

            // South corner bracket
            DrawPolyline(new Vector2[]
            {
                new(hw * bracketFrac, hh - hh * bracketFrac),
                new(0f, hh),
                new(-hw * bracketFrac, hh - hh * bracketFrac)
            }, CornerColor, cornerWidth, antialiased: true);

            // West corner bracket
            DrawPolyline(new Vector2[]
            {
                new(-hw + hw * bracketFrac, hh * bracketFrac),
                new(-hw, 0f),
                new(-hw + hw * bracketFrac, -hh * bracketFrac)
            }, CornerColor, cornerWidth, antialiased: true);
        }
    }
}
