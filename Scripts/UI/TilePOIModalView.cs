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
    /// Strict LIFO Esc Hierarchy: priority #3.
    /// Halal compliant dark-lacquer Xianxia aesthetic.
    /// </summary>
    public partial class TilePOIModalView : Control, IModalView
    {
        private bool _isOpen = false;
        private TaiwuModalStack? _modalStack;
        public bool IsOpen => _isOpen;

        public void InitializeModalStack(TaiwuModalStack modalStack) => _modalStack = modalStack;

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

        // UI references
        private ColorRect? _backdrop;
        private PanelContainer? _mainModalPanel;
        private Label? _lblTitle;
        private Label? _lblCoords;
        private Label? _lblDanger;
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

            BuildUIHierarchy();

            GameEventManager.OnPOIArrived += HandlePOIArrived;
            GameEventManager.OnEscapeRequested += HandleEscapeRequested;
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
            GameEventManager.OnEscapeRequested -= HandleEscapeRequested;
            GameEventManager.OnCrimeScenePresent -= HandleCrimeScenePresent;
            GameEventManager.OnCrimeSceneUpdated -= HandleCrimeSceneUpdated;
            GameEventManager.OnTileCultivatorsUpdated -= HandleTileCultivatorsUpdated;
            GameEventManager.OnPlayerMoved -= HandlePlayerMoved;
            GameEventManager.OnLedgerToggled -= HandleLedgerToggled;
            GameEventManager.OnCauldronOpened -= HandleCauldronOpened;
            GameEventManager.OnBattleStageStateChanged -= HandleBattleStageChanged;

            if (_isOpen)
            {
                _isOpen = false;
                GameEventManager.TriggerUIModalActive(false);
                GameEventManager.TriggerPOIModalToggled(false);
            }
        }

        public void OpenWithPOI(TilePOIType poi, Vector2Int pos, string title, string desc, int dangerRank)
            => HandlePOIArrived(poi, pos, title, desc, dangerRank);

        private void HandleEscapeRequested()
        {
            if (GameEventManager.EscapeHandled) return;

            // Strict LIFO: Ledger and Cauldron have priority over POI modal!
            if (_modalStack?.IsModalOpen<TaiwuLedgerView>() == true ||
                _modalStack?.IsModalOpen<RefinementCauldronView>() == true)
            {
                return;
            }

            if (_isOpen)
            {
                CloseModal();
                GameEventManager.EscapeHandled = true;
            }
        }

        private void HandlePOIArrived(TilePOIType poi, Vector2Int pos, string title, string desc, int dangerRank)
        {
            _currentPOI = poi;
            _gridPos = pos;
            _title = title;
            _description = desc;
            _dangerRank = dangerRank;
            _isOpen = true;
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

            GameEventManager.TriggerUIModalActive(true);
            GameEventManager.TriggerPOIModalToggled(true);
            GameEventManager.RaiseModalOpened(this);

            UpdateAllDisplays();
        }

        public void CloseModal()
        {
            if (_isOpen)
            {
                _isOpen = false;
                Visible = false;
                _hasCrimeScene = false;
                _soulQiTerms = 0;
                _crimeScenePos = new Vector2Int(-999, -999);
                _cultivatorsPos = new Vector2Int(-999, -999);
                _residentCultivators.Clear();
                GameEventManager.RaiseModalClosed(this);
                GameEventManager.TriggerUIModalActive(false);
                GameEventManager.TriggerPOIModalToggled(false);
            }
        }

        private void HandlePlayerMoved(Vector2Int newPos)
        {
            if (_isOpen && newPos != _gridPos)
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
            if (inBattle && _isOpen)
            {
                CloseModal();
            }
        }

        private void HandleCrimeScenePresent(Vector2Int pos, int termsRemaining)
        {
            _crimeScenePos = pos;
            _hasCrimeScene = true;
            _soulQiTerms = termsRemaining;

            if (_isOpen && pos == _gridPos)
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
                if (_isOpen)
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

            if (_isOpen && pos == _gridPos)
            {
                UpdateCultivatorsDisplay();
            }
        }

        private void BuildUIHierarchy()
        {
            _backdrop = new ColorRect
            {
                Name = "POIBackdrop",
                Color = new Color(0f, 0f, 0f, 0.75f),
                MouseFilter = MouseFilterEnum.Stop
            };
            _backdrop.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(_backdrop);

            var centerContainer = new CenterContainer
            {
                Name = "CenterContainer",
                MouseFilter = MouseFilterEnum.Ignore
            };
            centerContainer.SetAnchorsPreset(LayoutPreset.FullRect);
            AddChild(centerContainer);

            var modalStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.035f, 0.055f, 0.055f, 0.98f),
                BorderColor = new Color(0.851f, 0.467f, 0.024f, 0.85f),
                BorderWidthBottom = 2,
                BorderWidthTop = 2,
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4
            };

            _mainModalPanel = new PanelContainer
            {
                Name = "MainModalPanel",
                CustomMinimumSize = new Vector2(680f, 540f),
                MouseFilter = MouseFilterEnum.Stop
            };
            _mainModalPanel.AddThemeStyleboxOverride("panel", modalStyle);
            centerContainer.AddChild(_mainModalPanel);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 20);
            margin.AddThemeConstantOverride("margin_right", 20);
            margin.AddThemeConstantOverride("margin_top", 16);
            margin.AddThemeConstantOverride("margin_bottom", 16);
            _mainModalPanel.AddChild(margin);

            var rootVBox = new VBoxContainer();
            rootVBox.AddThemeConstantOverride("separation", 10);
            margin.AddChild(rootVBox);

            // Header Row
            var headerHBox = new HBoxContainer();
            rootVBox.AddChild(headerHBox);

            _lblTitle = new Label
            {
                Text = "Landmark Interaction",
                Modulate = new Color(0.984f, 0.749f, 0.141f, 1f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            _lblTitle.AddThemeFontSizeOverride("font_size", 15);
            headerHBox.AddChild(_lblTitle);

            _lblDanger = new Label
            {
                Text = "Danger I (Mortal)",
                Modulate = new Color(0.937f, 0.267f, 0.267f, 1f)
            };
            _lblDanger.AddThemeFontSizeOverride("font_size", 11);
            headerHBox.AddChild(_lblDanger);

            var btnClose = new Button
            {
                Text = "✕",
                CustomMinimumSize = new Vector2(36f, 28f),
                MouseFilter = MouseFilterEnum.Stop
            };
            btnClose.Pressed += CloseModal;
            headerHBox.AddChild(btnClose);

            _lblCoords = new Label
            {
                Text = "Grid Coordinates: [15, 15] | Regional Landmark",
                Modulate = new Color(0.58f, 0.64f, 0.72f, 1f)
            };
            _lblCoords.AddThemeFontSizeOverride("font_size", 11);
            rootVBox.AddChild(_lblCoords);

            _lblDesc = new Label
            {
                Text = "Landmark description lore...",
                Modulate = new Color(0.886f, 0.910f, 0.941f, 1f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            _lblDesc.AddThemeFontSizeOverride("font_size", 11);
            rootVBox.AddChild(_lblDesc);

            // Divider: Section 1
            var lblSec1 = new Label { Text = "AVAILABLE INTERACTIONS (地块交互)", Modulate = new Color(0.204f, 0.827f, 0.6f, 1f) };
            lblSec1.AddThemeFontSizeOverride("font_size", 11);
            rootVBox.AddChild(lblSec1);

            _actionsContainer = new VBoxContainer();
            _actionsContainer.AddThemeConstantOverride("separation", 6);
            rootVBox.AddChild(_actionsContainer);

            // Divider: Section 2
            var lblSec2 = new Label { Text = "RESIDENT CHARACTERS / CULTIVATORS (同格人物)", Modulate = new Color(0.22f, 0.74f, 0.97f, 1f) };
            lblSec2.AddThemeFontSizeOverride("font_size", 11);
            rootVBox.AddChild(lblSec2);

            var scrollCultivators = new ScrollContainer { CustomMinimumSize = new Vector2(0f, 100f), SizeFlagsVertical = SizeFlags.ExpandFill };
            rootVBox.AddChild(scrollCultivators);

            _cultivatorsContainer = new VBoxContainer();
            _cultivatorsContainer.AddThemeConstantOverride("separation", 4);
            _cultivatorsContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scrollCultivators.AddChild(_cultivatorsContainer);

            // Crime Scene Box
            _crimeSceneContainer = new VBoxContainer();
            _crimeSceneContainer.AddThemeConstantOverride("separation", 6);
            rootVBox.AddChild(_crimeSceneContainer);

            // Footer Depart Button
            var btnDepart = new Button
            {
                Text = "【离】 Depart / Leave (离开) [Esc]",
                CustomMinimumSize = new Vector2(0f, 34f),
                MouseFilter = MouseFilterEnum.Stop
            };
            btnDepart.Pressed += CloseModal;
            rootVBox.AddChild(btnDepart);
        }

        private void UpdateAllDisplays()
        {
            if (_lblTitle != null) _lblTitle.Text = _title;
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
                    var npc = _residentCultivators[i];
                    var card = CreateCultivatorCard(npc);
                    _cultivatorsContainer.AddChild(card);
                }
            }
        }

        private PanelContainer CreateCultivatorCard(CultivatorEntityData npc)
        {
            var cardPanel = new PanelContainer();
            var cardStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.06f, 0.08f, 0.10f, 0.95f),
                BorderColor = new Color(0.2f, 0.28f, 0.35f, 0.9f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1
            };
            cardPanel.AddThemeStyleboxOverride("panel", cardStyle);

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            cardPanel.AddChild(row);

            var lblBadge = new Label
            {
                Text = $"[R{npc.Rank} S{npc.Substage}]",
                Modulate = new Color(0.984f, 0.749f, 0.141f, 1f)
            };
            lblBadge.AddThemeFontSizeOverride("font_size", 11);
            row.AddChild(lblBadge);

            var lblInfo = new Label
            {
                Text = $"【 {npc.Name} 】 — {npc.Clan} | Favor: {npc.Favorability:+0;-0;0} | Stones: {npc.PrimevalStones}",
                Modulate = new Color(0.886f, 0.910f, 0.941f, 1f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            lblInfo.AddThemeFontSizeOverride("font_size", 11);
            row.AddChild(lblInfo);

            var btnInteract = new Button
            {
                Text = "Interact / 论道",
                CustomMinimumSize = new Vector2(110f, 26f)
            };
            string id = npc.EntityId;
            btnInteract.Pressed += () =>
            {
                CloseModal();
                GameEventManager.RaiseRequestInteractWithNPC(id);
            };
            row.AddChild(btnInteract);

            return cardPanel;
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
