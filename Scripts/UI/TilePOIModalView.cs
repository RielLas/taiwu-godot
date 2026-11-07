using System;
using System.Collections.Generic;
using Godot;
using Color = Godot.Color;
using TaiwuGu.Core;
using TaiwuGu.Map;
using TaiwuGu.Psychology;
using Vector2Int = TaiwuGu.Core.GridCoord;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Phase 3: Tile POI Modal View (Landmark Interaction, Forensics & Cultivators Dialog) (Godot 2D).
    /// Pure Presentation Layer Observer under Constitutional Laws 1, 4 & 6.
    /// Strictly decoupled from Domain Services & GameStateContext.
    /// Handles:
    /// - Contextual interactions (Spirit Spring meditation, Jia Family Caravan barter trade, Clan Post rest, Beast Den tracking, Cave exploration)
    /// - Forensics Tampering on Crime Scenes (Cleanse, Frame Beasts, Frame Rival Clan)
    /// - Resident & Wandering NPC Cultivator Cards on current tile
    /// Chrome (backdrop, panel, header, close button, Escape, modal registration) comes
    /// from Scenes/UI/ModalScaffold.tscn; this view only supplies its own body rows.
    /// Strict LIFO Esc Hierarchy: priority #3, enforced by the scaffold's stack-top check.
    /// Halal compliant dark-lacquer Xianxia aesthetic.
    /// </summary>
    public partial class TilePOIModalView : Control, IModalView
    {
        private const string ScaffoldScenePath = "res://Scenes/UI/ModalScaffold.tscn";
        private const string CultivatorCardScenePath = "res://Scenes/UI/CultivatorCard.tscn";

        private TaiwuModalStack? _modalStack;
        private ModalScaffold? _scaffold;
        private PackedScene? _cultivatorCardScene;

        public bool IsOpen => _scaffold?.IsOpen ?? false;

        public void InitializeModalStack(TaiwuModalStack modalStack)
        {
            _modalStack = modalStack;
            _scaffold?.BindModalStack(modalStack);
        }

        public void Dismiss() => CloseModal();

        private TilePOIType _currentPOI = TilePOIType.None;
        private Vector2Int _gridPos;
        private string _title = "";
        private string _description = "";
        private int _dangerRank = 1;

        // Crime scene status on current tile
        private bool _hasCrimeScene = false;
        private int _soulQiTerms = 0;
        private Vector2Int _crimeScenePos = new Vector2Int(-999, -999);

        // Resident Cultivators on current tile
        private readonly List<CultivatorEntityData> _residentCultivators = new();
        private Vector2Int _cultivatorsPos = new Vector2Int(-999, -999);

        // UI references supplied by the scaffold and this view's own body rows
        private Label? _lblDanger;
        private Label? _lblCoords;
        private Label? _lblDesc;

        private VBoxContainer? _actionsContainer;
        private VBoxContainer? _cultivatorsContainer;
        private VBoxContainer? _crimeSceneContainer;

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;
            Visible = false;

            BuildScaffold();

            GameEventManager.OnPOIArrived += HandlePOIArrived;
            GameEventManager.OnCrimeScenePresent += HandleCrimeScenePresent;
            GameEventManager.OnCrimeSceneUpdated += HandleCrimeSceneUpdated;
            GameEventManager.OnTileCultivatorsUpdated += HandleTileCultivatorsUpdated;
            GameEventManager.OnPlayerMoved += HandlePlayerMoved;
            GameEventManager.OnLedgerToggled += HandleLedgerToggled;
            GameEventManager.OnCauldronOpened += HandleCauldronOpened;
            GameEventManager.OnBattleStageStateChanged += HandleBattleStageChanged;
        }

        public override void _ExitTree()
        {
            GameEventManager.OnPOIArrived -= HandlePOIArrived;
            GameEventManager.OnCrimeScenePresent -= HandleCrimeScenePresent;
            GameEventManager.OnCrimeSceneUpdated -= HandleCrimeSceneUpdated;
            GameEventManager.OnTileCultivatorsUpdated -= HandleTileCultivatorsUpdated;
            GameEventManager.OnPlayerMoved -= HandlePlayerMoved;
            GameEventManager.OnLedgerToggled -= HandleLedgerToggled;
            GameEventManager.OnCauldronOpened -= HandleCauldronOpened;
            GameEventManager.OnBattleStageStateChanged -= HandleBattleStageChanged;

            if (IsOpen)
            {
                ResetTileTransientState();
                Visible = false;
                _scaffold?.Close();
                GameEventManager.TriggerPOIModalToggled(false);
            }
        }

        public void OpenWithPOI(TilePOIType poi, Vector2Int pos, string title, string desc, int dangerRank)
            => HandlePOIArrived(poi, pos, title, desc, dangerRank);

        private void HandlePOIArrived(TilePOIType poi, Vector2Int pos, string title, string desc, int dangerRank)
        {
            _currentPOI = poi;
            _gridPos = pos;
            _title = title;
            _description = desc;
            _dangerRank = dangerRank;
            Visible = true;

            // Only clear crime scene or cultivators if not already primed for this exact tile
            if (_crimeScenePos != pos)
            {
                _hasCrimeScene = false;
                _soulQiTerms = 0;
            }
            if (_cultivatorsPos != pos)
            {
                _residentCultivators.Clear();
            }

            _scaffold?.Open(this, _title);
            GameEventManager.TriggerPOIModalToggled(true);

            UpdateAllDisplays();
        }

        public void CloseModal()
        {
            if (!IsOpen) return;

            ResetTileTransientState();
            Visible = false;
            _scaffold?.Close();
            GameEventManager.TriggerPOIModalToggled(false);
        }

        private void ResetTileTransientState()
        {
            _hasCrimeScene = false;
            _soulQiTerms = 0;
            _crimeScenePos = new Vector2Int(-999, -999);
            _cultivatorsPos = new Vector2Int(-999, -999);
            _residentCultivators.Clear();
        }

        private void HandlePlayerMoved(Vector2Int newPos)
        {
            if (IsOpen && newPos != _gridPos)
            {
                CloseModal();
            }
        }

        private void HandleLedgerToggled(bool isOpen)
        {
            // Retain state; LIFO Esc closes Ledger before POI modal
        }

        private void HandleCauldronOpened(CauldronRefinementState state)
        {
            // Retain state; LIFO Esc closes Cauldron before POI modal
        }

        private void HandleBattleStageChanged(bool inBattle)
        {
            if (inBattle && IsOpen)
            {
                CloseModal();
            }
        }

        private void HandleCrimeScenePresent(Vector2Int pos, int termsRemaining)
        {
            _crimeScenePos = pos;
            _hasCrimeScene = true;
            _soulQiTerms = termsRemaining;

            if (IsOpen && pos == _gridPos)
            {
                UpdateCrimeSceneDisplay();
            }
        }

        private void HandleCrimeSceneUpdated(Vector2Int pos)
        {
            if (pos == _gridPos || pos == _crimeScenePos)
            {
                _hasCrimeScene = false;
                _soulQiTerms = 0;
                if (IsOpen)
                {
                    UpdateCrimeSceneDisplay();
                }
            }
        }

        private void HandleTileCultivatorsUpdated(Vector2Int pos, List<CultivatorEntityData> cultivators)
        {
            _cultivatorsPos = pos;
            _residentCultivators.Clear();
            if (cultivators != null)
            {
                _residentCultivators.AddRange(cultivators);
            }

            if (IsOpen && pos == _gridPos)
            {
                UpdateCultivatorsDisplay();
            }
        }

        // =========================================================================
        // SCAFFOLD + BODY CONSTRUCTION
        // =========================================================================

        private void BuildScaffold()
        {
            var scaffoldScene = GD.Load<PackedScene>(ScaffoldScenePath);
            if (scaffoldScene == null)
            {
                GD.PushError($"[TilePOIModalView] Missing modal scaffold scene at {ScaffoldScenePath}.");
                return;
            }

            _scaffold = scaffoldScene.Instantiate<ModalScaffold>();
            _scaffold.BindModalStack(_modalStack);
            AddChild(_scaffold);

            _lblDanger = new Label
            {
                Text = "Danger I (Mortal)",
                Modulate = new Color(0.937f, 0.267f, 0.267f, 1f)
            };
            _lblDanger.AddThemeFontSizeOverride("font_size", 11);
            _scaffold.HeaderAccessory?.AddChild(_lblDanger);

            _cultivatorCardScene = GD.Load<PackedScene>(CultivatorCardScenePath);
            if (_cultivatorCardScene == null)
            {
                GD.PushError($"[TilePOIModalView] Missing cultivator card scene at {CultivatorCardScenePath}.");
            }

            BuildBody(_scaffold.ContentRoot);
        }

        private void BuildBody(VBoxContainer? content)
        {
            if (content == null) return;

            _lblCoords = new Label
            {
                Text = "Grid Coordinates: [15, 15] | Regional Landmark",
                Modulate = new Color(0.58f, 0.64f, 0.72f, 1f)
            };
            _lblCoords.AddThemeFontSizeOverride("font_size", 11);
            content.AddChild(_lblCoords);

            _lblDesc = new Label
            {
                Text = "Landmark description lore...",
                Modulate = new Color(0.886f, 0.910f, 0.941f, 1f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            _lblDesc.AddThemeFontSizeOverride("font_size", 11);
            content.AddChild(_lblDesc);

            content.AddChild(CreateSectionHeader("AVAILABLE INTERACTIONS (地块交互)", new Color(0.204f, 0.827f, 0.6f, 1f)));

            _actionsContainer = new VBoxContainer();
            _actionsContainer.AddThemeConstantOverride("separation", 6);
            content.AddChild(_actionsContainer);

            content.AddChild(CreateSectionHeader("RESIDENT CHARACTERS / CULTIVATORS (同格人物)", new Color(0.22f, 0.74f, 0.97f, 1f)));

            var scrollCultivators = new ScrollContainer
            {
                CustomMinimumSize = new Vector2(0f, 100f),
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            content.AddChild(scrollCultivators);

            _cultivatorsContainer = new VBoxContainer();
            _cultivatorsContainer.AddThemeConstantOverride("separation", 4);
            _cultivatorsContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scrollCultivators.AddChild(_cultivatorsContainer);

            _crimeSceneContainer = new VBoxContainer();
            _crimeSceneContainer.AddThemeConstantOverride("separation", 6);
            content.AddChild(_crimeSceneContainer);

            var btnDepart = new Button
            {
                Text = "【离】 Depart / Leave (离开) [Esc]",
                CustomMinimumSize = new Vector2(0f, 34f),
                MouseFilter = MouseFilterEnum.Stop
            };
            btnDepart.Pressed += CloseModal;
            content.AddChild(btnDepart);
        }

        private static Label CreateSectionHeader(string text, Color modulate)
        {
            var label = new Label { Text = text, Modulate = modulate };
            label.AddThemeFontSizeOverride("font_size", 11);
            return label;
        }

        // =========================================================================
        // DISPLAY UPDATES
        // =========================================================================

        private void UpdateAllDisplays()
        {
            _scaffold?.SetTitle(_title);
            if (_lblCoords != null) _lblCoords.Text = $"Grid Coordinates: [{_gridPos.X}, {_gridPos.Y}] | Regional Landmark";
            if (_lblDanger != null) _lblDanger.Text = $"Danger {GetDangerRoman(_dangerRank)}";
            if (_lblDesc != null) _lblDesc.Text = _description;

            UpdateActionsDisplay();
            UpdateCultivatorsDisplay();
            UpdateCrimeSceneDisplay();
        }

        private void UpdateActionsDisplay()
        {
            if (_actionsContainer == null) return;
            foreach (Node child in _actionsContainer.GetChildren()) child.QueueFree();

            var def = LandmarkRegistry.GetByPOI(_currentPOI);
            if (def != null && def.AvailableActions.Count > 0)
            {
                // If WayStation, retain the ON WORK banner for the upcoming Grand Bazaar
                if (_currentPOI == TilePOIType.WayStation)
                {
                    var btnCaravan = new Button { Text = "◆ Enter Jia Family Caravan Grand Bazaar (贾家商队大集) ◆", CustomMinimumSize = new Vector2(0f, 34f) };
                    btnCaravan.Pressed += () =>
                    {
                        GameEventManager.RaiseRequestShowOnWorkNotice(
                            "Jia Family Caravan Barter Economy (贾家商队)",
                            "The grand trade pavilion is currently ON WORK.\nSeasonal arrivals (Bailu / Qiufen), Stone Gambling Den, and Gu auctions are underway."
                        );
                    };
                    _actionsContainer.AddChild(btnCaravan);
                }

                foreach (var action in def.AvailableActions)
                {
                    var btn = new Button
                    {
                        Text = action.Label,
                        CustomMinimumSize = new Vector2(0f, 34f),
                        MouseFilter = MouseFilterEnum.Stop
                    };
                    string actId = action.ActionId;
                    btn.Pressed += () =>
                    {
                        if (actId.Contains("transit") || actId.Contains("subjugate") || actId.Contains("combat"))
                        {
                            CloseModal();
                        }
                        GameEventManager.RaiseRequestPOIAction(_currentPOI, _gridPos, actId);
                    };
                    _actionsContainer.AddChild(btn);
                }
            }
            else
            {
                string msg = _residentCultivators.Count > 0 
                    ? "Wilderness gathering point. Converse with resident cultivators below (同格同行修士)." 
                    : (def != null && def.Category == LandmarkCategory.Scenic 
                        ? $"【{def.SealText}】 {def.Name}: {def.LoreDescription}" 
                        : "No special interactive structures on this wilderness tile.");
                var lblNone = new Label { Text = msg, Modulate = new Color(0.6f, 0.65f, 0.7f, 1f), AutowrapMode = TextServer.AutowrapMode.WordSmart };
                lblNone.AddThemeFontSizeOverride("font_size", 11);
                _actionsContainer.AddChild(lblNone);
            }
        }

        private void UpdateCultivatorsDisplay()
        {
            if (_cultivatorsContainer == null) return;
            foreach (Node child in _cultivatorsContainer.GetChildren()) child.QueueFree();

            if (_residentCultivators.Count == 0)
            {
                var lblEmpty = new Label { Text = "No cultivators currently present on this tile. (此地暂无同行修士)", Modulate = new Color(0.5f, 0.55f, 0.6f, 1f) };
                lblEmpty.AddThemeFontSizeOverride("font_size", 11);
                _cultivatorsContainer.AddChild(lblEmpty);
            }
            else
            {
                for (int i = 0; i < _residentCultivators.Count; i++)
                {
                    var card = CreateCultivatorCard(_residentCultivators[i]);
                    _cultivatorsContainer.AddChild(card);
                }
            }
        }

        private Control CreateCultivatorCard(CultivatorEntityData npc)
        {
            var card = _cultivatorCardScene?.Instantiate<CultivatorCardView>();
            if (card == null)
            {
                var fallback = new Label
                {
                    Text = $"[R{npc.Rank} S{npc.Substage}] 【 {npc.Name} 】 — {npc.Clan}",
                    Modulate = new Color(0.886f, 0.910f, 0.941f, 1f)
                };
                fallback.AddThemeFontSizeOverride("font_size", 11);
                return fallback;
            }

            card.Bind(npc.EntityId, npc.Rank, npc.Substage, npc.Name, npc.Clan, npc.Favorability, npc.PrimevalStones);
            card.InteractRequested += HandleCultivatorInteractRequested;
            return card;
        }

        private void HandleCultivatorInteractRequested(string entityId)
        {
            CloseModal();
            GameEventManager.RaiseRequestInteractWithNPC(entityId);
        }

        private void UpdateCrimeSceneDisplay()
        {
            if (_crimeSceneContainer == null) return;
            foreach (Node child in _crimeSceneContainer.GetChildren()) child.QueueFree();

            if (!_hasCrimeScene || _gridPos != _crimeScenePos) return;

            var lblCrime = new Label
            {
                Text = $"⚠ ACTIVE CRIME SCENE: {_soulQiTerms} Solar Terms Lingering Soul Qi",
                Modulate = new Color(0.937f, 0.267f, 0.267f, 1f)
            };
            lblCrime.AddThemeFontSizeOverride("font_size", 11);
            _crimeSceneContainer.AddChild(lblCrime);

            var btnsRow = new HBoxContainer();
            btnsRow.AddThemeConstantOverride("separation", 8);
            _crimeSceneContainer.AddChild(btnsRow);

            var btnCleanse = new Button { Text = "Cleanse Soul Qi", CustomMinimumSize = new Vector2(0f, 30f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            btnCleanse.Pressed += () => GameEventManager.RaiseRequestTamperCrimeEvidence(_gridPos, CrimeTamperChoice.Cleanse);
            btnsRow.AddChild(btnCleanse);

            var btnFrameBeasts = new Button { Text = "Frame Wild Beasts", CustomMinimumSize = new Vector2(0f, 30f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            btnFrameBeasts.Pressed += () => GameEventManager.RaiseRequestTamperCrimeEvidence(_gridPos, CrimeTamperChoice.FrameWildBeasts);
            btnsRow.AddChild(btnFrameBeasts);

            var btnFrameClan = new Button { Text = "Frame Rival Clan", CustomMinimumSize = new Vector2(0f, 30f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            btnFrameClan.Pressed += () => GameEventManager.RaiseRequestTamperCrimeEvidence(_gridPos, CrimeTamperChoice.FrameRivalClan);
            btnsRow.AddChild(btnFrameClan);
        }

        private static string GetDangerRoman(int rank) => rank switch
        {
            1 => "I (Mortal)",
            2 => "II (Dangerous)",
            3 => "III (Lethal)",
            4 => "IV (Nightmare)",
            5 => "V (Calamity)",
            _ => "I"
        };
    }
}
