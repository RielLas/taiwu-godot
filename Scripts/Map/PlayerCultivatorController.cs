using System;
using System.Collections.Generic;
using Godot;
using TaiwuGu.Combat;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Map;
using TaiwuGu.Systems;
using Taiwu_Godot.Scripts.UI;

namespace Taiwu_Godot.Scripts.Map
{
    /// <summary>
    /// Overworld Player Cultivator Controller.
    /// Reads Action Points & coordinates directly from WorldStore and invokes ExplorationSystem directly.
    /// </summary>
    public partial class PlayerCultivatorController : Node2D
    {
        [Export] public Vector2I CurrentGridPos { get; set; } = new Vector2I(15, 15);
        [Export] public float MoveSpeed { get; set; } = 4.0f;
        [Export] public float JumpArcHeight { get; set; } = 0.45f;
        [Export] public IsometricTileManager? TileManager { get; set; }

        private IWorldStateProvider? _worldProvider;
        private TaiwuModalStack? _modalStack;
        private TileLandmarkVisualManager.EllipticalInkGroundShadow? _shadow;
        private TileLandmarkVisualManager.TalismanMedallionToken? _medallion;

        private bool _isMoving = false;
        private bool _isPauseLocked = false;
        private bool _isUIModalLocked = false;

        private Vector2I? _selectedTargetTile = null;
        private List<Vector2I>? _cachedPath = null;

        private List<Vector2I>? _activePath = null;
        private int _currentPathIndex = 0;
        private Vector2 _stepStartPos;
        private Vector2 _stepTargetPos;
        private float _stepElapsed = 0f;
        private float _stepDuration = 0.25f;

        public bool IsMoving => _isMoving;
        public bool HasTargetSelection => _selectedTargetTile.HasValue;
        public Vector2I? SelectedTargetTile => _selectedTargetTile;

        private int CurrentAP => _worldProvider?.Current.GetSingleton<WorldClockComponent>()?.ActionPointsRemaining ?? 0;

        public void Initialize(IsometricTileManager manager, Vector2I startPos, IWorldStateProvider? provider = null)
        {
            TileManager = manager;
            _worldProvider = provider;
            CurrentGridPos = startPos;
            Position = TileManager.GridToWorld(startPos);
            EnsureVisuals();
        }

        public void InitializeModalStack(TaiwuModalStack modalStack) => _modalStack = modalStack;

        public override void _Ready()
        {
            YSortEnabled = true;
            ZIndex = 10;

            EnsureVisuals();

            WorldSignals.OnWorldClickRequested += HandleWorldClick;
            WorldSignals.OnEscapeRequested += HandleEscapeRequested;
            WorldSignals.OnPauseToggled += HandlePauseToggled;
            WorldSignals.OnUIModalActive += HandleUIModalActive;
            WorldSignals.OnTopicsChanged += HandleTopicsChanged;
            WorldSignals.OnWildBeastAmbushed += HandleHaltMovementAmbush;
            WorldSignals.OnCombatRequested += HandleHaltMovementCombat;

            if (TileManager != null)
            {
                Position = TileManager.GridToWorld(CurrentGridPos);
            }
            else
            {
                Position = ExplorationSystem.GridToWorld(CurrentGridPos.ToGridCoord()).ToGodot();
            }
        }

        public override void _ExitTree()
        {
            WorldSignals.OnWorldClickRequested -= HandleWorldClick;
            WorldSignals.OnEscapeRequested -= HandleEscapeRequested;
            WorldSignals.OnPauseToggled -= HandlePauseToggled;
            WorldSignals.OnUIModalActive -= HandleUIModalActive;
            WorldSignals.OnTopicsChanged -= HandleTopicsChanged;
            WorldSignals.OnWildBeastAmbushed -= HandleHaltMovementAmbush;
            WorldSignals.OnCombatRequested -= HandleHaltMovementCombat;
        }

        private void HandleTopicsChanged(DirtyTopic topics)
        {
            if ((topics & DirtyTopic.WildEncounter) != 0 && _worldProvider != null)
            {
                var wild = _worldProvider.Current.Get<WildEncounterSessionComponent>(_worldProvider.Current.PlayerEntity);
                if (wild?.IsOpen == true) HaltMovement();
            }

            if ((topics & (DirtyTopic.PlayerPos | DirtyTopic.Map)) != 0 && !_isMoving && _worldProvider != null)
            {
                Vector2I domainPos = _worldProvider.Current.GetPlayerPosition().ToGodot();
                if (domainPos != CurrentGridPos)
                {
                    CurrentGridPos = domainPos;
                    Position = TileManager != null
                        ? TileManager.GridToWorld(domainPos)
                        : ExplorationSystem.GridToWorld(domainPos.ToGridCoord()).ToGodot();
                }
            }
        }

        private void HandleHaltMovementAmbush(GridCoord pos, string beast) => HaltMovement();
        private void HandleHaltMovementCombat(ArenaEncounterType enc, GridCoord pos) => HaltMovement();

        public void HaltMovement()
        {
            _isMoving = false;
            _activePath = null;
            ClearTargetSelection();
        }

        private void EnsureVisuals()
        {
            if (_shadow == null)
            {
                _shadow = new TileLandmarkVisualManager.EllipticalInkGroundShadow(0.48f, 0.24f, 0.50f)
                {
                    Name = "PlayerShadow"
                };
                AddChild(_shadow);
            }

            if (_medallion == null)
            {
                var jadeTex = ResourceLoader.Load<Texture2D>("res://Resources/Tokens/token_player_jade.png");
                _medallion = new TileLandmarkVisualManager.TalismanMedallionToken(jadeTex, "道", 0.75f)
                {
                    Name = "PlayerMedallion"
                };
                AddChild(_medallion);
            }
        }

        private void HandlePauseToggled(bool isPaused) => _isPauseLocked = isPaused;
        private void HandleUIModalActive(bool isActive) => _isUIModalLocked = isActive;

        public void ClearTargetSelection()
        {
            if (_selectedTargetTile.HasValue || _cachedPath != null)
            {
                _selectedTargetTile = null;
                _cachedPath = null;
                WorldSignals.EmitTargetTileSelected(null, new List<GridCoord>());
            }
        }

        private void HandleEscapeRequested()
        {
            if (WorldSignals.EscapeHandled) return;
            if (_modalStack?.Count > 0) return;

            if (HasTargetSelection)
            {
                ClearTargetSelection();
                WorldSignals.EscapeHandled = true;
            }
        }

        public void RequestMoveTo(GridCoord targetGrid)
        {
            if (_isPauseLocked || _isUIModalLocked || _isMoving || TileManager == null) return;

            Vector2I target = targetGrid.ToGodot();
            if (!TileManager.IsWithinBounds(target) || target == CurrentGridPos) return;

            var path = TilePathfinder.FindPath(CurrentGridPos, target, TileManager);
            if (path == null || path.Count == 0 || CurrentAP <= 0) return;

            ClearTargetSelection();
            StartPathMovement(path);
        }

        private void HandleWorldClick(WorldVec2 clickPos)
        {
            if (_isPauseLocked || _isUIModalLocked || _isMoving || TileManager == null) return;

            GridCoord clickedGrid = ExplorationSystem.WorldToGrid(clickPos);
            Vector2I clicked = clickedGrid.ToGodot();

            if (!TileManager.IsWithinBounds(clicked))
            {
                ClearTargetSelection();
                return;
            }

            if (clicked == CurrentGridPos)
            {
                ClearTargetSelection();
                if (_worldProvider != null)
                {
                    ExplorationSystem.InspectTile(_worldProvider.Current, clickedGrid);
                }
                return;
            }

            if (!TileManager.IsTileWalkable(clicked))
            {
                ClearTargetSelection();
                return;
            }

            if (!_selectedTargetTile.HasValue || _selectedTargetTile.Value != clicked)
            {
                if (CurrentAP <= 0)
                {
                    ClearTargetSelection();
                    return;
                }

                var path = TilePathfinder.FindPath(CurrentGridPos, clicked, TileManager);
                if (path == null || path.Count == 0)
                {
                    ClearTargetSelection();
                    return;
                }

                _selectedTargetTile = clicked;
                _cachedPath = path;

                var gridCoordPath = new List<GridCoord>(path.Count);
                for (int i = 0; i < path.Count; i++) gridCoordPath.Add(path[i].ToGridCoord());
                WorldSignals.EmitTargetTileSelected(_selectedTargetTile.Value.ToGridCoord(), gridCoordPath);
                return;
            }

            var revalidatedPath = TilePathfinder.FindPath(CurrentGridPos, clicked, TileManager);
            if (revalidatedPath == null || revalidatedPath.Count == 0 || CurrentAP <= 0)
            {
                ClearTargetSelection();
                return;
            }

            ClearTargetSelection();
            StartPathMovement(revalidatedPath);
        }

        private void StartPathMovement(List<Vector2I> path)
        {
            _activePath = path;
            _currentPathIndex = 0;
            _isMoving = true;
            _stepDuration = 1.0f / MoveSpeed;
            SetupNextStep();
        }

        private void SetupNextStep()
        {
            if (_activePath == null || _currentPathIndex >= _activePath.Count || TileManager == null)
            {
                _isMoving = false;
                _activePath = null;
                return;
            }

            Vector2I targetGrid = _activePath[_currentPathIndex];
            _stepStartPos = Position;
            _stepTargetPos = TileManager.GridToWorld(targetGrid);
            _stepElapsed = 0f;
        }

        public override void _Process(double delta)
        {
            if (!_isMoving || _activePath == null || TileManager == null || _isPauseLocked) return;

            _stepElapsed += (float)delta;
            float t = Math.Clamp(_stepElapsed / _stepDuration, 0f, 1f);

            Vector2 currentPos = _stepStartPos.Lerp(_stepTargetPos, t);
            currentPos.Y -= MathF.Sin(t * MathF.PI) * JumpArcHeight;
            Position = currentPos;

            if (t >= 1.0f)
            {
                Vector2I stepTarget = _activePath[_currentPathIndex];
                CurrentGridPos = stepTarget;
                Position = _stepTargetPos;

                bool isFinalStep = _currentPathIndex + 1 >= _activePath.Count;
                if (_worldProvider != null)
                {
                    ExplorationSystem.MovePlayer(_worldProvider.Current, stepTarget.ToGridCoord(), openInteractivePOI: isFinalStep);
                }

                _currentPathIndex++;
                if (isFinalStep)
                {
                    _isMoving = false;
                    _activePath = null;
                    return;
                }

                SetupNextStep();
            }
        }
    }
}
