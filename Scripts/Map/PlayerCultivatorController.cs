using System;
using System.Collections.Generic;
using Godot;
using Color = Godot.Color;
using TaiwuGu.Core;
using TaiwuGu.Map;
using Taiwu_Godot.Scripts.UI;

namespace Taiwu_Godot.Scripts.Map
{
    /// <summary>
    /// Phase 2: Player Cultivator Controller (Godot 2D).
    /// Halal-compliant jade medallion token with authentic seal calligraphy 【道】.
    /// Supports Scroll of Taiwu two-step movement confirmation (Route Preview -> Confirm Walk).
    /// Strictly obeys Constitutional Law 1 (Observer Mandate) and Law 6 (CQRS-Lite).
    /// </summary>
    public partial class PlayerCultivatorController : Node2D
    {
        [Export] public Vector2I CurrentGridPos { get; set; } = new Vector2I(15, 15);
        [Export] public float MoveSpeed { get; set; } = 4.0f;
        [Export] public float JumpArcHeight { get; set; } = 0.45f;
        [Export] public IsometricTileManager? TileManager { get; set; }
        private TaiwuModalStack? _modalStack;
        private TileLandmarkVisualManager.EllipticalInkGroundShadow? _shadow;
        private TileLandmarkVisualManager.TalismanMedallionToken? _medallion;

        public void InitializeModalStack(TaiwuModalStack modalStack) => _modalStack = modalStack;

        private Sprite2D? _tokenSprite;
        private Label? _sealLabel;

        private bool _isMoving = false;
        private bool _isPauseLocked = false;
        private bool _isUIModalLocked = false;
        private int _currentAP = 30;

        private Vector2I? _selectedTargetTile = null;
        private List<Vector2I>? _cachedPath = null;

        // Path following state
        private List<Vector2I>? _activePath = null;
        private int _currentPathIndex = 0;
        private Vector2 _stepStartPos;
        private Vector2 _stepTargetPos;
        private float _stepElapsed = 0f;
        private float _stepDuration = 0.25f;

        public bool IsMoving => _isMoving;
        public bool HasTargetSelection => _selectedTargetTile.HasValue;
        public Vector2I? SelectedTargetTile => _selectedTargetTile;

        public override void _Ready()
        {
            YSortEnabled = true;
            ZIndex = 10;

            EnsureVisuals();

            GameEventManager.OnWorldClickRequested += HandleWorldClick;
            GameEventManager.OnMoveRequested += HandleMoveRequested;
            GameEventManager.OnEscapeRequested += HandleEscapeRequested;
            GameEventManager.OnActionPointsChanged += HandleActionPointsChanged;
            GameEventManager.OnPauseToggled += HandlePauseToggled;
            GameEventManager.OnUIModalActive += HandleUIModalActive;
            GameEventManager.OnWildGuEncounterOpened += HandleHaltMovement;
            GameEventManager.OnWildBeastAmbushed += HandleHaltMovementAmbush;
            GameEventManager.OnEnterCombatRequested += HandleHaltMovementCombat;

            if (TileManager != null)
            {
                Position = TileManager.GridToWorld(CurrentGridPos);
            }
            else
            {
                Position = ExplorationService.GridToWorld(CurrentGridPos.ToGridCoord()).ToGodot();
            }
        }

        public override void _ExitTree()
        {
            GameEventManager.OnWorldClickRequested -= HandleWorldClick;
            GameEventManager.OnMoveRequested -= HandleMoveRequested;
            GameEventManager.OnEscapeRequested -= HandleEscapeRequested;
            GameEventManager.OnActionPointsChanged -= HandleActionPointsChanged;
            GameEventManager.OnPauseToggled -= HandlePauseToggled;
            GameEventManager.OnUIModalActive -= HandleUIModalActive;
            GameEventManager.OnWildGuEncounterOpened -= HandleHaltMovement;
            GameEventManager.OnWildBeastAmbushed -= HandleHaltMovementAmbush;
            GameEventManager.OnEnterCombatRequested -= HandleHaltMovementCombat;
        }

        private void HandleHaltMovement(SubjugationSessionSnapshot snapshot) => HaltMovement();
        private void HandleHaltMovementAmbush(GridCoord pos, string beast) => HaltMovement();
        private void HandleHaltMovementCombat(TaiwuGu.Combat.ArenaEncounterType enc, GridCoord pos) => HaltMovement();

        public void HaltMovement()
        {
            _isMoving = false;
            _activePath = null;
            ClearTargetSelection();
        }

        private void EnsureVisuals()
        {
            // 1. Soft Elliptical Ink Grounding Shadow beneath player token (2:1 isometric ratio)
            if (_shadow == null)
            {
                _shadow = new TileLandmarkVisualManager.EllipticalInkGroundShadow(0.48f, 0.24f, 0.50f)
                {
                    Name = "PlayerShadow"
                };
                AddChild(_shadow);
            }

            // 2. Halal-compliant Carved Imperial Jade Medallion (~0.75 units) with Radiant Gold Seal Calligraphy 【道】
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

        public void Initialize(IsometricTileManager manager, Vector2I startPos)
        {
            TileManager = manager;
            CurrentGridPos = startPos;
            Position = TileManager.GridToWorld(startPos);
            EnsureVisuals();
        }

        private void HandlePauseToggled(bool isPaused) => _isPauseLocked = isPaused;
        private void HandleUIModalActive(bool isActive) => _isUIModalLocked = isActive;
        private void HandleActionPointsChanged(int currentAP, int maxBudget) => _currentAP = currentAP;

        public void ClearTargetSelection()
        {
            if (_selectedTargetTile.HasValue || _cachedPath != null)
            {
                _selectedTargetTile = null;
                _cachedPath = null;
                GameEventManager.TriggerTargetTileSelected(null, new List<GridCoord>());
            }
        }

        private void HandleEscapeRequested()
        {
            if (GameEventManager.EscapeHandled) return;

            if (_modalStack?.IsModalOpen<TaiwuLedgerView>() == true ||
                _modalStack?.IsModalOpen<RefinementCauldronView>() == true ||
                _modalStack?.IsModalOpen<TilePOIModalView>() == true)
            {
                return;
            }

            if (HasTargetSelection)
            {
                ClearTargetSelection();
                GameEventManager.EscapeHandled = true;
            }
        }

        private void HandleMoveRequested(GridCoord targetGrid)
        {
            if (_isPauseLocked || _isUIModalLocked || _isMoving) return;
            if (TileManager == null) return;

            Vector2I target = targetGrid.ToGodot();
            if (!TileManager.IsWithinBounds(target) || target == CurrentGridPos) return;

            var path = TilePathfinder.FindPath(CurrentGridPos, target, TileManager);
            if (path == null || path.Count == 0 || _currentAP <= 0) return;

            ClearTargetSelection();
            StartPathMovement(path);
        }

        private void HandleWorldClick(WorldVec2 clickPos)
        {
            if (_isPauseLocked || _isUIModalLocked || _isMoving) return;
            if (TileManager == null) return;

            GridCoord clickedGrid = ExplorationService.WorldToGrid(clickPos);
            Vector2I clicked = clickedGrid.ToGodot();

            // Out of bounds check
            if (!TileManager.IsWithinBounds(clicked))
            {
                ClearTargetSelection();
                return;
            }

            // Clicked on player's current tile: Direct Landmark Interaction / Inspect
            if (clicked == CurrentGridPos)
            {
                ClearTargetSelection();
                GameEventManager.RaiseRequestInspectTile(clickedGrid);
                return;
            }

            // Clicked on an impassable terrain obstacle: Cannot path there
            if (!TileManager.IsTileWalkable(clicked))
            {
                ClearTargetSelection();
                GD.Print($"[PlayerCultivator] Tile {clicked} is an impassable terrain obstacle.");
                return;
            }

            // --- STEP 1: First Click on New Target (Selection & Route Preview) ---
            if (!_selectedTargetTile.HasValue || _selectedTargetTile.Value != clicked)
            {
                if (_currentAP <= 0)
                {
                    GD.Print("[PlayerCultivator] Cannot travel: Action Points exhausted (0 AP).");
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

                List<GridCoord> gridCoordPath = new();
                for (int i = 0; i < path.Count; i++)
                {
                    gridCoordPath.Add(path[i].ToGridCoord());
                }

                GameEventManager.TriggerTargetTileSelected(_selectedTargetTile.Value.ToGridCoord(), gridCoordPath);
                return;
            }

            // --- STEP 2: Second Click on Highlighted Tile (Confirmation & Execution) ---
            var revalidatedPath = TilePathfinder.FindPath(CurrentGridPos, clicked, TileManager);
            if (revalidatedPath == null || revalidatedPath.Count == 0 || _currentAP <= 0)
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
            if (!_isMoving || _activePath == null || TileManager == null) return;
            if (_isPauseLocked) return;

            _stepElapsed += (float)delta;
            float t = Math.Clamp(_stepElapsed / _stepDuration, 0f, 1f);

            Vector2 currentPos = _stepStartPos.Lerp(_stepTargetPos, t);
            // In Godot 2D, jump arc moves UP towards -Y
            currentPos.Y -= MathF.Sin(t * MathF.PI) * JumpArcHeight;
            Position = currentPos;

            if (t >= 1.0f)
            {
                Vector2I stepTarget = _activePath[_currentPathIndex];
                CurrentGridPos = stepTarget;
                Position = _stepTargetPos;

                TileNode node = TileManager.GetTile(stepTarget.X, stepTarget.Y);
                GameEventManager.TriggerPlayerMoved(CurrentGridPos.ToGridCoord());
                GameEventManager.TriggerTileStepped(node.Biome, node.POI);

                _currentPathIndex++;
                if (_currentPathIndex >= _activePath.Count)
                {
                    _isMoving = false;
                    _activePath = null;

                    // Arrived at final path destination: If interactive landmark, trigger interaction
                    if (LandmarkRegistry.IsInteractive(node.POI, node.CustomLandmarkId))
                    {
                        GameEventManager.RaiseRequestInspectTile(CurrentGridPos.ToGridCoord());
                    }
                    return;
                }

                SetupNextStep();
            }
        }
    }
}
