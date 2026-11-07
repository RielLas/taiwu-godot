using System;
using System.Collections.Generic;
using Godot;
using TaiwuGu.Core;
using TaiwuGu.Map;

namespace Taiwu_Godot.Scripts.Map
{
    /// <summary>
    /// Manages fog of war and dynamic sensory radius on the 2.5D Isometric Diamond Grid.
    /// Unveils tiles in real time as the player travels across wilderness nodes,
    /// dynamically writing updated vertex luminance to the isometric diamond mesh.
    /// Strictly ports Unity's FogOfWarManager.cs with 100% functional parity.
    /// </summary>
    public partial class FogOfWarManager : Node
    {
        [Export] public int BaseSensoryRadius { get; set; } = 3;
        [Export] public IsometricTileManager? TileManager { get; set; }

        private readonly HashSet<Vector2I> _currentlyVisibleTiles = new();
        private readonly HashSet<Vector2I> _exploredTiles = new();

        public override void _Ready()
        {
            GameEventManager.OnPlayerMoved += HandlePlayerMoved;
        }

        public override void _ExitTree()
        {
            GameEventManager.OnPlayerMoved -= HandlePlayerMoved;
            _currentlyVisibleTiles.Clear();
            _exploredTiles.Clear();
        }

        public void Initialize(IsometricTileManager manager)
        {
            TileManager = manager;
        }

        private void HandlePlayerMoved(GridCoord playerPos)
        {
            RefreshVision(new Vector2I(playerPos.X, playerPos.Y));
        }

        public void RefreshVision(Vector2I playerGridPos, int reconBonusRadius = 0)
        {
            if (TileManager == null) return;

            int totalRadius = BaseSensoryRadius + reconBonusRadius;

            // Reset previous active vision to dimmed explored state
            foreach (var pos in _currentlyVisibleTiles)
            {
                TileManager.SetVisible(pos.X, pos.Y, false);
            }
            _currentlyVisibleTiles.Clear();

            // Calculate new Manhattan radius on isometric grid (Math.Abs(dx) + Math.Abs(dy) <= totalRadius)
            for (int dx = -totalRadius; dx <= totalRadius; dx++)
            {
                for (int dy = -totalRadius; dy <= totalRadius; dy++)
                {
                    if (Math.Abs(dx) + Math.Abs(dy) <= totalRadius)
                    {
                        Vector2I target = playerGridPos + new Vector2I(dx, dy);
                        if (TileManager.IsWithinBounds(target))
                        {
                            _currentlyVisibleTiles.Add(target);
                            _exploredTiles.Add(target);
                            TileManager.SetExplored(target.X, target.Y, true);
                            TileManager.SetVisible(target.X, target.Y, true);
                        }
                    }
                }
            }

            // Push updated colors directly into the combined mesh
            TileManager.UpdateFogMeshColors();

            // Broadcast event so landmarks, tokens, and mini-map sync
            GameEventManager.TriggerFogOfWarUpdated(new GridCoord(playerGridPos.X, playerGridPos.Y), totalRadius);
        }

        public bool IsTileExplored(Vector2I pos) => _exploredTiles.Contains(pos);
        public bool IsTileVisible(Vector2I pos) => _currentlyVisibleTiles.Contains(pos);
    }
}
