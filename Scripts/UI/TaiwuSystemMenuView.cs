using System;
using System.Collections.Generic;
using Godot;
using Color = Godot.Color;
using TaiwuGu.Core;
using TaiwuGu.Serialization;
using Taiwu_Godot.Scripts.Map;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Phase 3: System & Pause Menu View (系统菜单 / 封卷存盘) (Godot 2D).
    /// Pure Presentation Layer Observer under Constitutional Laws 1, 4 & 6.
    /// Strictly decoupled from Domain Services & GameStateContext.
    /// Features:
    /// - Strict LIFO ESC Interception: Only opens when Ledger, Cauldron, POI modal, and route preview are closed.
    /// - Temporal Stasis: Sets GetTree().Paused = true while open, unfreezes on close.
    /// - Multi-slot Save & Load via SaveDiskIO metadata and CQRS command intents.
    /// - Controls Guide for keybindings.
    /// - Return to Title confirmation.
    /// Halal compliant dark-lacquer Xianxia aesthetic.
    /// </summary>
    public partial class TaiwuSystemMenuView : Control, IModalView
    {
        private const string MenuTitle = "◆ TAIWU SYSTEM MENU (系统封卷) ◆";

        private bool _isOpen = false;
        private TaiwuModalStack? _modalStack;
        private PlayerCultivatorController? _playerController;
        public bool IsOpen => _isOpen;

        public void InitializeModalStack(TaiwuModalStack modalStack, PlayerCultivatorController? playerController)
        {
            _modalStack = modalStack;
            _playerController = playerController;
            _scaffold?.BindModalStack(modalStack);
        }

        /// <summary>
        /// Dismissal request from the shared scaffold (Escape or backdrop): back out of a
        /// sub-view first, otherwise resume the game. The header Resume button goes straight
        /// to <see cref="CloseMenu"/> because resuming is not the same as dismissing.
        /// </summary>
        public void Dismiss()
        {
            if (!_isOpen) return;

            if (_currentViewIndex != 0)
            {
                SwitchToView(0);
                return;
            }

            CloseMenu();
        }
        public void ToggleMenu() { if (_isOpen) CloseMenu(); else OpenMenu(); }

        private int _currentViewIndex = 0; // 0: Main Menu, 1: Save Archives, 2: Load Archives, 3: Controls Guide, 4: Exit Confirm
        private string _statusMessage = "";
        private readonly string[] _slots = new string[] { "slot_01", "slot_02", "slot_03" };
        private readonly Dictionary<string, SaveMetadataDTO> _cachedSlotMetadata = new();

        // UI references (the chrome itself lives in Scenes/UI/ModalScaffold.tscn)
        private ModalScaffold? _scaffold;
        private Label? _lblStatus;

        // View panels
        private Control? _panelMainMenu;
        private Control? _panelSaveArchives;
        private Control? _panelLoadArchives;
        private Control? _panelControlsGuide;
        private Control? _panelExitConfirm;

        private VBoxContainer? _saveSlotsContainer;
        private VBoxContainer? _loadSlotsContainer;

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;
            Visible = false;

            BuildUIHierarchy();

            GameEventManager.OnEscapeRequested += HandleEscapeRequested;
            GameEventManager.OnGameSaved += HandleGameSaved;
            GameEventManager.OnGameLoaded += HandleGameLoaded;
            GameEventManager.OnSaveDeleted += HandleSaveDeleted;
        }

        public override void _ExitTree()
        {
            GameEventManager.OnEscapeRequested -= HandleEscapeRequested;
            GameEventManager.OnGameSaved -= HandleGameSaved;
            GameEventManager.OnGameLoaded -= HandleGameLoaded;
            GameEventManager.OnSaveDeleted -= HandleSaveDeleted;

            if (_isOpen)
            {
                CloseMenu();
            }
        }

        private void HandleEscapeRequested()
        {
            if (GameEventManager.EscapeHandled) return;

            // While the menu is open Escape belongs to its scaffold: only the top modal answers,
            // and it calls Dismiss() (back out of a sub-view, else resume). Here we cover only
            // the closed state, where Esc opens the menu if nothing else claims the key.
            if (_isOpen) return;

            // Strict LIFO: Only when all modals are closed does Esc toggle SystemMenu
            if (_modalStack?.IsModalOpen<TaiwuLedgerView>() == true) return;
            if (_modalStack?.IsModalOpen<RefinementCauldronView>() == true) return;
            if (_modalStack?.IsModalOpen<TilePOIModalView>() == true) return;
            if (_playerController?.HasTargetSelection == true) return;

            OpenMenu();
            GameEventManager.EscapeHandled = true;
        }

        public void OpenMenu()
        {
            _isOpen = true;
            Visible = true;
            _currentViewIndex = 0;
            _statusMessage = "";

            GetTree().Paused = true; // Temporal Stasis
            GameEventManager.TriggerPauseToggled(true);
            _scaffold?.Open(this, MenuTitle);

            RefreshSlotMetadata();
            SwitchToView(0);
        }

        public void CloseMenu()
        {
            _isOpen = false;
            Visible = false;
            _currentViewIndex = 0;

            GetTree().Paused = false; // Restore Temporal Flow
            _scaffold?.Close();
            GameEventManager.TriggerPauseToggled(false);
        }

        private void SwitchToView(int viewIndex)
        {
            _currentViewIndex = viewIndex;
            if (_panelMainMenu != null) _panelMainMenu.Visible = (_currentViewIndex == 0);
            if (_panelSaveArchives != null) _panelSaveArchives.Visible = (_currentViewIndex == 1);
            if (_panelLoadArchives != null) _panelLoadArchives.Visible = (_currentViewIndex == 2);
            if (_panelControlsGuide != null) _panelControlsGuide.Visible = (_currentViewIndex == 3);
            if (_panelExitConfirm != null) _panelExitConfirm.Visible = (_currentViewIndex == 4);

            if (_currentViewIndex == 1) UpdateSaveSlotsUI();
            if (_currentViewIndex == 2) UpdateLoadSlotsUI();
        }

        private void RefreshSlotMetadata()
        {
            _cachedSlotMetadata.Clear();
            foreach (var slot in _slots)
            {
                try
                {
                    var meta = SaveDiskIO.LoadMetadata(slot);
                    if (meta != null)
                    {
                        _cachedSlotMetadata[slot] = meta;
                    }
                }
                catch
                {
                    // Ignore disk errors on slot read
                }
            }
        }

        private void HandleGameSaved(string slotID, bool success)
        {
            if (success)
            {
                _statusMessage = $"Archive committed to '{slotID}' successfully!";
                RefreshSlotMetadata();
                if (_currentViewIndex == 1) UpdateSaveSlotsUI();
            }
            else
            {
                _statusMessage = $"Failed to commit archive to '{slotID}'!";
            }
            if (_lblStatus != null) _lblStatus.Text = _statusMessage;
        }

        private void HandleGameLoaded(string slotID, bool success)
        {
            if (success)
            {
                _statusMessage = $"Archive '{slotID}' loaded & state hydrated!";
                CloseMenu();
            }
            else
            {
                _statusMessage = $"Failed to hydrate archive '{slotID}'!";
            }
            if (_lblStatus != null) _lblStatus.Text = _statusMessage;
        }

        private void HandleSaveDeleted(string slotID, bool success)
        {
            RefreshSlotMetadata();
            if (_currentViewIndex == 1) UpdateSaveSlotsUI();
            if (_currentViewIndex == 2) UpdateLoadSlotsUI();
        }

        private void BuildUIHierarchy()
        {
            // Chrome (backdrop, panel, header, close button, Escape, modal registration)
            // is Scenes/UI/ModalScaffold.tscn; this view only fills the body.
            var scaffoldScene = GD.Load<PackedScene>("res://Scenes/UI/ModalScaffold.tscn");
            if (scaffoldScene == null)
            {
                GD.PushError("[TaiwuSystemMenuView] Missing modal scaffold scene at res://Scenes/UI/ModalScaffold.tscn.");
                return;
            }

            _scaffold = scaffoldScene.Instantiate<ModalScaffold>();
            AddChild(_scaffold);
            _scaffold.BindModalStack(_modalStack);
            _scaffold.SetTitle(MenuTitle);

            // 560x480 menu window and the original 0.75 dim. The scaffold title already uses
            // this menu's imperial amber, so only the size and backdrop tint need matching.
            if (_scaffold.Panel != null)
            {
                _scaffold.Panel.CustomMinimumSize = new Vector2(560f, 480f);
            }
            if (_scaffold.Backdrop != null)
            {
                _scaffold.Backdrop.Color = new Color(0f, 0f, 0f, 0.75f);
            }

            // Resume is not the scaffold's generic dismiss (in a sub-view it would only step
            // back), so it takes the header accessory slot and the close button is hidden.
            if (_scaffold.CloseButton != null)
            {
                _scaffold.CloseButton.Visible = false;
            }
            if (_scaffold.HeaderAccessory != null)
            {
                var btnResume = new Button
                {
                    Text = "Resume [Esc]",
                    CustomMinimumSize = new Vector2(100f, 28f),
                    MouseFilter = MouseFilterEnum.Stop
                };
                btnResume.Pressed += CloseMenu;
                _scaffold.HeaderAccessory.AddChild(btnResume);
            }

            var rootVBox = _scaffold.ContentRoot;
            if (rootVBox == null) return;
            rootVBox.AddThemeConstantOverride("separation", 14);

            _lblStatus = new Label
            {
                Text = "",
                Modulate = new Color(0.204f, 0.827f, 0.6f, 1f)
            };
            _lblStatus.AddThemeFontSizeOverride("font_size", 11);
            rootVBox.AddChild(_lblStatus);

            // Dynamic Body
            var bodyContainer = new PanelContainer
            {
                SizeFlagsVertical = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Stop
            };
            rootVBox.AddChild(bodyContainer);

            BuildMainMenuTab(bodyContainer);
            BuildSaveArchivesTab(bodyContainer);
            BuildLoadArchivesTab(bodyContainer);
            BuildControlsGuideTab(bodyContainer);
            BuildExitConfirmTab(bodyContainer);

            SwitchToView(0);
        }

        private void BuildMainMenuTab(PanelContainer container)
        {
            _panelMainMenu = new VBoxContainer { Name = "PanelMainMenu", Alignment = BoxContainer.AlignmentMode.Center };
            _panelMainMenu.AddThemeConstantOverride("separation", 12);
            container.AddChild(_panelMainMenu);

            var btnCont = new Button { Text = "Resume Journey (继续征途)", CustomMinimumSize = new Vector2(260f, 38f), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
            btnCont.Pressed += CloseMenu;
            _panelMainMenu.AddChild(btnCont);

            var btnSave = new Button { Text = "Save Archive (封卷存盘)", CustomMinimumSize = new Vector2(260f, 38f), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
            btnSave.Pressed += () => SwitchToView(1);
            _panelMainMenu.AddChild(btnSave);

            var btnLoad = new Button { Text = "Load Archive (读取前尘)", CustomMinimumSize = new Vector2(260f, 38f), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
            btnLoad.Pressed += () => SwitchToView(2);
            _panelMainMenu.AddChild(btnLoad);

            var btnGuide = new Button { Text = "Keybinding Guide (操持指南)", CustomMinimumSize = new Vector2(260f, 38f), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
            btnGuide.Pressed += () => SwitchToView(3);
            _panelMainMenu.AddChild(btnGuide);

            var btnExit = new Button { Text = "Return to Title (离世归寂)", CustomMinimumSize = new Vector2(260f, 38f), SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
            btnExit.Pressed += () => SwitchToView(4);
            _panelMainMenu.AddChild(btnExit);
        }

        private void BuildSaveArchivesTab(PanelContainer container)
        {
            _panelSaveArchives = new VBoxContainer { Name = "PanelSaveArchives" };
            _panelSaveArchives.AddThemeConstantOverride("separation", 10);
            container.AddChild(_panelSaveArchives);

            var topRow = new HBoxContainer();
            _panelSaveArchives.AddChild(topRow);

            var lblTitle = new Label { Text = "SELECT ARCHIVE SLOT TO COMMIT:", Modulate = new Color(0.984f, 0.749f, 0.141f, 1f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            lblTitle.AddThemeFontSizeOverride("font_size", 12);
            topRow.AddChild(lblTitle);

            var btnBack = new Button { Text = "← Back", CustomMinimumSize = new Vector2(70f, 26f) };
            btnBack.Pressed += () => SwitchToView(0);
            topRow.AddChild(btnBack);

            _saveSlotsContainer = new VBoxContainer();
            _saveSlotsContainer.AddThemeConstantOverride("separation", 8);
            _panelSaveArchives.AddChild(_saveSlotsContainer);
        }

        private void UpdateSaveSlotsUI()
        {
            if (_saveSlotsContainer == null) return;
            foreach (Node child in _saveSlotsContainer.GetChildren()) child.QueueFree();

            foreach (var slot in _slots)
            {
                bool hasData = _cachedSlotMetadata.TryGetValue(slot, out var meta);
                var slotRow = CreateSlotCard(slot, meta, isSaveMode: true);
                _saveSlotsContainer.AddChild(slotRow);
            }
        }

        private void BuildLoadArchivesTab(PanelContainer container)
        {
            _panelLoadArchives = new VBoxContainer { Name = "PanelLoadArchives" };
            _panelLoadArchives.AddThemeConstantOverride("separation", 10);
            container.AddChild(_panelLoadArchives);

            var topRow = new HBoxContainer();
            _panelLoadArchives.AddChild(topRow);

            var lblTitle = new Label { Text = "SELECT ARCHIVE SLOT TO LOAD:", Modulate = new Color(0.22f, 0.74f, 0.97f, 1f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            lblTitle.AddThemeFontSizeOverride("font_size", 12);
            topRow.AddChild(lblTitle);

            var btnBack = new Button { Text = "← Back", CustomMinimumSize = new Vector2(70f, 26f) };
            btnBack.Pressed += () => SwitchToView(0);
            topRow.AddChild(btnBack);

            _loadSlotsContainer = new VBoxContainer();
            _loadSlotsContainer.AddThemeConstantOverride("separation", 8);
            _panelLoadArchives.AddChild(_loadSlotsContainer);
        }

        private void UpdateLoadSlotsUI()
        {
            if (_loadSlotsContainer == null) return;
            foreach (Node child in _loadSlotsContainer.GetChildren()) child.QueueFree();

            foreach (var slot in _slots)
            {
                bool hasData = _cachedSlotMetadata.TryGetValue(slot, out var meta);
                var slotRow = CreateSlotCard(slot, meta, isSaveMode: false);
                _loadSlotsContainer.AddChild(slotRow);
            }
        }

        private PanelContainer CreateSlotCard(string slotId, SaveMetadataDTO? meta, bool isSaveMode)
        {
            var card = new PanelContainer();
            var cardStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.06f, 0.08f, 0.10f, 0.95f),
                BorderColor = new Color(0.2f, 0.28f, 0.35f, 0.9f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1
            };
            card.AddThemeStyleboxOverride("panel", cardStyle);

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            card.AddChild(row);

            string slotTitle = slotId.ToUpperInvariant();
            string descText = meta != null
                ? $"【 {meta.PlayerName} 】 Rank {meta.PreviewRank} Stage {meta.PreviewSubstage} | Year {meta.CurrentYear} {meta.SolarTermName} | 🪨 {meta.PreviewStones}"
                : "Empty Archive Slot (虚位待存)";

            var lblInfo = new Label
            {
                Text = $"{slotTitle}: {descText}",
                Modulate = meta != null ? new Color(0.886f, 0.910f, 0.941f, 1f) : new Color(0.5f, 0.55f, 0.6f, 1f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            lblInfo.AddThemeFontSizeOverride("font_size", 11);
            row.AddChild(lblInfo);

            if (isSaveMode)
            {
                var btnSave = new Button { Text = "Save (存入)", CustomMinimumSize = new Vector2(90f, 28f) };
                string sid = slotId;
                btnSave.Pressed += () => GameEventManager.RaiseRequestSaveGame(sid);
                row.AddChild(btnSave);

                if (meta != null)
                {
                    var btnDelete = new Button { Text = "Delete", CustomMinimumSize = new Vector2(70f, 28f) };
                    btnDelete.Pressed += () => GameEventManager.RaiseRequestDeleteSave(sid);
                    row.AddChild(btnDelete);
                }
            }
            else
            {
                var btnLoad = new Button
                {
                    Text = "Load (读取)",
                    CustomMinimumSize = new Vector2(90f, 28f),
                    Disabled = (meta == null)
                };
                string sid = slotId;
                btnLoad.Pressed += () => GameEventManager.RaiseRequestLoadGame(sid);
                row.AddChild(btnLoad);
            }

            return card;
        }

        private void BuildControlsGuideTab(PanelContainer container)
        {
            _panelControlsGuide = new VBoxContainer { Name = "PanelControlsGuide" };
            _panelControlsGuide.AddThemeConstantOverride("separation", 10);
            container.AddChild(_panelControlsGuide);

            var topRow = new HBoxContainer();
            _panelControlsGuide.AddChild(topRow);

            var lblTitle = new Label { Text = "MARTIAL MASTER KEYBINDINGS (操持按键):", Modulate = new Color(0.204f, 0.827f, 0.6f, 1f), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            lblTitle.AddThemeFontSizeOverride("font_size", 12);
            topRow.AddChild(lblTitle);

            var btnBack = new Button { Text = "← Back", CustomMinimumSize = new Vector2(70f, 26f) };
            btnBack.Pressed += () => SwitchToView(0);
            topRow.AddChild(btnBack);

            var lblGuide = new Label
            {
                Text = "• [Esc] - System Menu / Close Active Modal View (LIFO Priority)\n" +
                       "• [Tab] / [C] - Character Cultivation Ledger & Aperture Inspection\n" +
                       "• [R] - Mortal Gu Refinement Alchemical Cauldron\n" +
                       "• [WASD] / [Right-Mouse Drag] - Pan Overworld Camera\n" +
                       "• [Scroll Wheel] - Zoom Map In & Out\n" +
                       "• [Left Click] - Select Tile Route Preview / Confirm Movement Step\n" +
                       "• [Home] / [F] - Recenter Camera onto Cultivator Medallion\n\n" +
                       "Cauldron Mini-Game Hotkeys:\n" +
                       "• [W] - Pulse Aperture Flame (Phase 1)\n" +
                       "• [A] / [D] - Steer Master Focus Left / Right (Phase 2)\n" +
                       "• [Space] - Suppress Wild Will Spikes (Phase 2)\n" +
                       "• [F] - Crush Primeval Stone for Stability (Phase 3)",
                Modulate = new Color(0.886f, 0.910f, 0.941f, 1f)
            };
            lblGuide.AddThemeFontSizeOverride("font_size", 11);
            _panelControlsGuide.AddChild(lblGuide);
        }

        private void BuildExitConfirmTab(PanelContainer container)
        {
            _panelExitConfirm = new VBoxContainer { Name = "PanelExitConfirm", Alignment = BoxContainer.AlignmentMode.Center };
            _panelExitConfirm.AddThemeConstantOverride("separation", 16);
            container.AddChild(_panelExitConfirm);

            var lblWarning = new Label
            {
                Text = "Are you sure you wish to return to Title?\nAny unsaved progress in this solar term will be lost.",
                HorizontalAlignment = HorizontalAlignment.Center,
                Modulate = new Color(0.937f, 0.267f, 0.267f, 1f)
            };
            lblWarning.AddThemeFontSizeOverride("font_size", 13);
            _panelExitConfirm.AddChild(lblWarning);

            var btnsRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            btnsRow.AddThemeConstantOverride("separation", 16);
            _panelExitConfirm.AddChild(btnsRow);

            var btnConfirm = new Button
            {
                Text = "Confirm Return to Title (返回主界面)",
                CustomMinimumSize = new Vector2(210f, 36f)
            };
            btnConfirm.Pressed += () =>
            {
                CloseMenu();
                GameEventManager.TriggerReturnToTitle();
            };
            btnsRow.AddChild(btnConfirm);

            var btnCancel = new Button
            {
                Text = "Cancel",
                CustomMinimumSize = new Vector2(100f, 36f)
            };
            btnCancel.Pressed += () => SwitchToView(0);
            btnsRow.AddChild(btnCancel);
        }
    }
}
