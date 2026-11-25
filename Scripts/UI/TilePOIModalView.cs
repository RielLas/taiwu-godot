using System;
using Godot;
using Color = Godot.Color;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Map;
using TaiwuGu.Psychology;
using TaiwuGu.Systems;
using Vector2Int = TaiwuGu.Core.GridCoord;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Direct-Read Tile POI Modal View: reads landmarks, active NPCs, and crime scenes directly from WorldStore at _gridPos.
    /// Zero transient cultivator/crime caches -> impossible for NPCs to vanish on tile re-clicks.
    /// </summary>
    public partial class TilePOIModalView : Control, IModalView
    {
        private const string ScaffoldScenePath = "res://Scenes/UI/ModalScaffold.tscn";
        private const string CultivatorCardScenePath = "res://Scenes/UI/CultivatorCard.tscn";

        private IWorldStateProvider? _worldProvider;
        private TaiwuModalStack? _modalStack;
        private ModalScaffold? _scaffold;
        private PackedScene? _cultivatorCardScene;

        public bool IsOpen => _scaffold?.IsOpen ?? false;

        public void Initialize(TaiwuModalStack modalStack, IWorldStateProvider provider)
        {
            _modalStack = modalStack;
            _worldProvider = provider;
            _scaffold?.BindModalStack(modalStack);
        }

        public void Dismiss() => CloseModal();

        private Vector2Int _gridPos;
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

            WorldSignals.OnOpenTilePOIRequested += OpenTile;
            WorldSignals.OnCloseTilePOIRequested += CloseModal;
            WorldSignals.OnTopicsChanged += HandleTopicsChanged;
            WorldSignals.OnBattleStageChanged += HandleBattleStageChanged;
        }

        public override void _ExitTree()
        {
            WorldSignals.OnOpenTilePOIRequested -= OpenTile;
            WorldSignals.OnCloseTilePOIRequested -= CloseModal;
            WorldSignals.OnTopicsChanged -= HandleTopicsChanged;
            WorldSignals.OnBattleStageChanged -= HandleBattleStageChanged;

            if (_cultivatorsContainer != null)
            {
                foreach (Node child in _cultivatorsContainer.GetChildren())
                {
                    if (child is CultivatorCardView existingCard)
                    {
                        existingCard.InteractRequested -= HandleCultivatorInteractRequested;
                    }
                }
            }

            if (IsOpen)
            {
                Visible = false;
                _scaffold?.Close();
                WorldSignals.EmitPOIModalToggled(false);
            }
        }

        public void OpenTile(Vector2Int pos)
        {
            _gridPos = pos;
            Visible = true;

            string title = ResolveTileTitle(pos);
            _scaffold?.Open(this, title);
            WorldSignals.EmitPOIModalToggled(true);

            RefreshFromWorld();
        }

        public void CloseModal()
        {
            if (!IsOpen) return;
            Visible = false;
            _scaffold?.Close();
            WorldSignals.EmitPOIModalToggled(false);
        }

        /// <summary>
        /// The Way Station's caravan button and the diagnostic smoke both enter the
        /// bazaar through this single presentation-owned route.
        /// </summary>
        public bool TryOpenCaravanBazaar()
        {
            if (_worldProvider?.Current is not { } world) return false;
            CaravanEconomySystem.OpenBazaar(world);
            return true;
        }

        private void HandleBattleStageChanged(bool inBattle)
        {
            if (inBattle && IsOpen) CloseModal();
        }

        private void HandleTopicsChanged(DirtyTopic topics)
        {
            if (!IsOpen || _worldProvider == null) return;

            if ((topics & DirtyTopic.PlayerPos) != 0 && _worldProvider.Current.GetPlayerPosition() != _gridPos)
            {
                CloseModal();
                return;
            }

            if ((topics & (DirtyTopic.NPCs | DirtyTopic.Forensics | DirtyTopic.Map)) != 0)
            {
                RefreshFromWorld();
            }
        }

        private string ResolveTileTitle(Vector2Int pos)
        {
            if (_worldProvider == null) return "Wilderness Node (荒野)";
            var world = _worldProvider.Current;
            var tile = world.GetCurrentSector()?.GetTile(pos.X, pos.Y) ?? default;
            var def = LandmarkRegistry.Resolve(tile);
            if (def != null) return def.Name;

            if (SocialVendettaSystem.GetAliveNPCsAt(world, pos).Count > 0)
                return "Wilderness Gathering (荒野聚落)";
            if (ForensicsSystem.GetActiveCrimeSceneAt(world, pos).HasValue)
                return "Wilderness Crime Scene (案发死地)";
            return "Wilderness Node (荒野)";
        }

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

        private void RefreshFromWorld()
        {
            if (_worldProvider == null) return;
            var world = _worldProvider.Current;
            var tile = world.GetCurrentSector()?.GetTile(_gridPos.X, _gridPos.Y) ?? default;
            var def = LandmarkRegistry.Resolve(tile);
            var npcs = SocialVendettaSystem.GetAliveNPCsAt(world, _gridPos);
            var crime = ForensicsSystem.GetActiveCrimeSceneAt(world, _gridPos);

            string title = ResolveTileTitle(_gridPos);
            string desc = def != null
                ? def.LoreDescription
                : (npcs.Count > 0
                    ? "Fellow wandering cultivators rest and convene under the mountain canopy."
                    : (crime.HasValue
                        ? "Morbid blood stains and lingering spiritual residue mar the secluded wilderness floor."
                        : "Untamed wilderness blanketed by bamboo groves and mist."));

            _scaffold?.SetTitle(title);
            if (_lblCoords != null) _lblCoords.Text = $"Grid Coordinates: [{_gridPos.X}, {_gridPos.Y}] | Regional Landmark";
            if (_lblDanger != null) _lblDanger.Text = $"Danger {GetDangerRoman(tile.DangerRank)}";
            if (_lblDesc != null) _lblDesc.Text = desc;

            // Actions
            if (_actionsContainer != null)
            {
                foreach (Node child in _actionsContainer.GetChildren()) child.QueueFree();

                if (def != null && def.AvailableActions.Count > 0)
                {
                    if (tile.POI == TilePOIType.WayStation)
                    {
                        var btnCaravan = new Button { Text = "◆ Enter Jia Family Caravan Grand Bazaar (贾家商队大集) ◆", CustomMinimumSize = new Vector2(0f, 34f) };
                        btnCaravan.Pressed += () => TryOpenCaravanBazaar();
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
                            ExplorationSystem.ExecutePOIAction(_worldProvider.Current, _gridPos, actId);
                        };
                        _actionsContainer.AddChild(btn);
                    }
                }
                else
                {
                    string msg = npcs.Count > 0
                        ? "Wilderness gathering point. Converse with resident cultivators below (同格同行修士)."
                        : (def != null && def.Category == LandmarkCategory.Scenic
                            ? $"【{def.SealText}】 {def.Name}: {def.LoreDescription}"
                            : "No special interactive structures on this wilderness tile.");
                    var lblNone = new Label { Text = msg, Modulate = new Color(0.6f, 0.65f, 0.7f, 1f), AutowrapMode = TextServer.AutowrapMode.WordSmart };
                    lblNone.AddThemeFontSizeOverride("font_size", 11);
                    _actionsContainer.AddChild(lblNone);
                }
            }

            // Resident Cultivators
            if (_cultivatorsContainer != null)
            {
                foreach (Node child in _cultivatorsContainer.GetChildren())
                {
                    if (child is CultivatorCardView existingCard)
                    {
                        existingCard.InteractRequested -= HandleCultivatorInteractRequested;
                    }
                    child.QueueFree();
                }

                if (npcs.Count == 0)
                {
                    var lblEmpty = new Label { Text = "No cultivators currently present on this tile. (此地暂无同行修士)", Modulate = new Color(0.5f, 0.55f, 0.6f, 1f) };
                    lblEmpty.AddThemeFontSizeOverride("font_size", 11);
                    _cultivatorsContainer.AddChild(lblEmpty);
                }
                else
                {
                    foreach (var (eid, cult, ap) in npcs)
                    {
                        int rank = ap?.Rank ?? 1;
                        int substage = ap?.Substage ?? 1;
                        var card = _cultivatorCardScene?.Instantiate<CultivatorCardView>();
                        if (card != null)
                        {
                            _cultivatorsContainer.AddChild(card);
                            card.Bind(eid.Value.ToString(), rank, substage, cult.Name, cult.Clan, cult.Favorability, cult.PrimevalStones);
                            card.InteractRequested += HandleCultivatorInteractRequested;
                        }
                        else
                        {
                            var fallback = new Label { Text = $"[R{rank} S{substage}] 【 {cult.Name} 】 — {cult.Clan}", Modulate = new Color(0.886f, 0.910f, 0.941f, 1f) };
                            fallback.AddThemeFontSizeOverride("font_size", 11);
                            _cultivatorsContainer.AddChild(fallback);
                        }
                    }
                }
            }

            // Crime Scene
            if (_crimeSceneContainer != null)
            {
                foreach (Node child in _crimeSceneContainer.GetChildren()) child.QueueFree();

                if (crime.HasValue)
                {
                    var lblCrime = new Label
                    {
                        Text = $"⚠ ACTIVE CRIME SCENE: {crime.Value.Scene.LingeringSoulQiTerms} Solar Terms Lingering Soul Qi",
                        Modulate = new Color(0.937f, 0.267f, 0.267f, 1f)
                    };
                    lblCrime.AddThemeFontSizeOverride("font_size", 11);
                    _crimeSceneContainer.AddChild(lblCrime);

                    var btnsRow = new HBoxContainer();
                    btnsRow.AddThemeConstantOverride("separation", 8);
                    _crimeSceneContainer.AddChild(btnsRow);

                    var btnCleanse = new Button { Text = "Cleanse Soul Qi", CustomMinimumSize = new Vector2(0f, 30f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
                    btnCleanse.Pressed += () => ForensicsSystem.TamperCrimeScene(_worldProvider.Current, _gridPos, CrimeTamperChoice.Cleanse);
                    btnsRow.AddChild(btnCleanse);

                    var btnFrameBeasts = new Button { Text = "Frame Wild Beasts", CustomMinimumSize = new Vector2(0f, 30f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
                    btnFrameBeasts.Pressed += () => ForensicsSystem.TamperCrimeScene(_worldProvider.Current, _gridPos, CrimeTamperChoice.FrameWildBeasts);
                    btnsRow.AddChild(btnFrameBeasts);

                    var btnFrameClan = new Button { Text = "Frame Rival Clan", CustomMinimumSize = new Vector2(0f, 30f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
                    btnFrameClan.Pressed += () => ForensicsSystem.TamperCrimeScene(_worldProvider.Current, _gridPos, CrimeTamperChoice.FrameRivalClan);
                    btnsRow.AddChild(btnFrameClan);
                }
            }
        }

        private void HandleCultivatorInteractRequested(string entityIdRaw)
        {
            CloseModal();
            if (ulong.TryParse(entityIdRaw, out ulong idVal))
            {
                WorldSignals.EmitOpenNPCDialogue(new EntityId(idVal));
            }
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
