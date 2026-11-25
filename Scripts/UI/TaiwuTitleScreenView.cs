using System;
using System.IO;
using Godot;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Psychology;
using TaiwuGu.Serialization;
using Color = Godot.Color;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Master Title Screen View (太吾蛊道 标题画面).
    /// Halal mountain-ink presentation supporting:
    /// - Begin Journey with Character Creation (Origin Clan, Aptitude Grade, Starting Sector, Vital Gu, Disposition -> NewRunConfig).
    /// - Load Game (lists recorded chronicles and loads selected slot).
    /// - Settings (audio and display preferences).
    /// - Quit game.
    /// - Headless test pass-through: immediately redirects to OverworldScene if --verify or --capture is present.
    /// </summary>
    public partial class TaiwuTitleScreenView : Control
    {
        public static TaiwuTitleScreenView? Instance { get; private set; }
        public static NewRunConfig? PendingRunConfig { get; set; }
        public static string? PendingLoadSlot { get; set; }

        [Export] public VideoStreamPlayer? VideoBackdrop { get; set; }
        [Export] public TextureRect? FallbackBackdrop { get; set; }

        [Export] public Control? MainMenuContainer { get; set; }
        [Export] public Control? CharCreationContainer { get; set; }
        [Export] public Control? LoadGameContainer { get; set; }
        [Export] public Control? SettingsContainer { get; set; }

        [Export] public Button? BtnBeginJourney { get; set; }
        [Export] public Button? BtnLoadGame { get; set; }
        [Export] public Button? BtnSettings { get; set; }
        [Export] public Button? BtnQuit { get; set; }

        // Character Creation Controls
        [Export] public LineEdit? TxtPlayerName { get; set; }
        [Export] public OptionButton? OptClan { get; set; }
        [Export] public OptionButton? OptAptitude { get; set; }
        [Export] public OptionButton? OptSector { get; set; }
        [Export] public OptionButton? OptVitalGu { get; set; }
        [Export] public OptionButton? OptDisposition { get; set; }
        [Export] public Button? BtnConfirmEmbark { get; set; }
        [Export] public Button? BtnBackFromCreation { get; set; }

        // Load Game Controls
        [Export] public VBoxContainer? SlotListContainer { get; set; }
        [Export] public Button? BtnBackFromLoad { get; set; }

        // Settings Controls
        [Export] public Button? BtnBackFromSettings { get; set; }

        private bool _transitioning = false;
        private bool _videoSubscribed = false;

        public override void _EnterTree()
        {
            Instance = this;
        }

        public override void _Ready()
        {
            // Configure save directory seam
            SavePaths.BaseSaveDirectory = ProjectSettings.GlobalizePath("user://Saves");

            // Headless testing check: immediately redirect if --verify or --capture is present
            if (CheckAutomatedTestFlags())
            {
                GD.Print("[TitleScreen] Automated test flag detected (--verify/--capture). Deferring redirect to OverworldScene...");
                Callable.From(() => GetTree().ChangeSceneToFile("res://Scenes/OverworldScene.tscn")).CallDeferred();
                return;
            }

            SetupMainMenuButtons();
            SetupCharacterCreation();
            SetupLoadGameMenu();
            SetupSettingsMenu();

            if (VideoBackdrop != null)
            {
                if (VideoBackdrop.Stream == null)
                {
                    var stream = ResourceLoader.Load<VideoStream>("res://Resources/Videos/OpeningScreen.ogv");
                    if (stream != null)
                    {
                        VideoBackdrop.Stream = stream;
                    }
                }
                VideoBackdrop.Autoplay = true;
                VideoBackdrop.Loop = false;
                VideoBackdrop.Expand = true;
                VideoBackdrop.Finished += OnVideoFinished;
                _videoSubscribed = true;
                if (VideoBackdrop.Stream != null)
                {
                    if (!VideoBackdrop.IsPlaying())
                    {
                        VideoBackdrop.Play();
                    }
                }
                else if (FallbackBackdrop != null)
                {
                    var mod = FallbackBackdrop.Modulate;
                    FallbackBackdrop.Modulate = new Color(mod.R, mod.G, mod.B, 1.0f);
                }
            }

            ShowSubMenu(MainMenuContainer);
        }

        private void OnVideoFinished()
        {
            if (VideoBackdrop != null && IsInstanceValid(VideoBackdrop))
            {
                VideoBackdrop.Stop();
                VideoBackdrop.Play();
            }
        }

        public override void _ExitTree()
        {
            if (VideoBackdrop != null && IsInstanceValid(VideoBackdrop))
            {
                if (_videoSubscribed)
                {
                    VideoBackdrop.Finished -= OnVideoFinished;
                    _videoSubscribed = false;
                }
                VideoBackdrop.Stop();
                VideoBackdrop.Stream = null;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private bool CheckAutomatedTestFlags()
        {
            var userArgs = OS.GetCmdlineUserArgs();
            if (userArgs != null)
            {
                foreach (var arg in userArgs)
                {
                    if (arg.Equals("--verify", StringComparison.Ordinal) ||
                        arg.StartsWith("--verify=", StringComparison.Ordinal) ||
                        arg.Equals("--capture", StringComparison.Ordinal) ||
                        arg.StartsWith("--capture", StringComparison.Ordinal) ||
                        arg.Equals("--preview-v2-tiles", StringComparison.Ordinal) ||
                        arg.Equals("--v2-tiles", StringComparison.Ordinal) ||
                        arg.Equals("--tiles", StringComparison.Ordinal))
                        return true;
                }
            }

            var allArgs = OS.GetCmdlineArgs();
            if (allArgs != null)
            {
                foreach (var arg in allArgs)
                {
                    if (arg.Equals("--verify", StringComparison.Ordinal) ||
                        arg.StartsWith("--verify=", StringComparison.Ordinal) ||
                        arg.Equals("--capture", StringComparison.Ordinal) ||
                        arg.StartsWith("--capture", StringComparison.Ordinal) ||
                        arg.Equals("--preview-v2-tiles", StringComparison.Ordinal) ||
                        arg.Equals("--v2-tiles", StringComparison.Ordinal) ||
                        arg.Equals("--tiles", StringComparison.Ordinal))
                        return true;
                }
            }

            return false;
        }

        private void SetupMainMenuButtons()
        {
            if (BtnBeginJourney != null)
            {
                BtnBeginJourney.FocusMode = FocusModeEnum.None;
                BtnBeginJourney.Pressed += () => ShowSubMenu(CharCreationContainer);
            }

            if (BtnLoadGame != null)
            {
                BtnLoadGame.FocusMode = FocusModeEnum.None;
                BtnLoadGame.Pressed += () =>
                {
                    PopulateLoadSlots();
                    ShowSubMenu(LoadGameContainer);
                };
            }

            if (BtnSettings != null)
            {
                BtnSettings.FocusMode = FocusModeEnum.None;
                BtnSettings.Pressed += () => ShowSubMenu(SettingsContainer);
            }

            if (BtnQuit != null)
            {
                BtnQuit.FocusMode = FocusModeEnum.None;
                BtnQuit.Pressed += () => GetTree().Quit();
            }
        }

        private void SetupCharacterCreation()
        {
            if (OptClan != null && OptClan.ItemCount == 0)
            {
                OptClan.Clear();
                OptClan.AddItem("Gu Yue Clan (古月氏) — Moon Path Heritage", 0);
                OptClan.AddItem("Bai Clan (白氏) — Bone Path Heritage", 1);
                OptClan.AddItem("Xiong Clan (熊氏) — Strength Path Heritage", 2);
                OptClan.AddItem("Shang Clan (商氏) — Wealth & Commerce", 3);
                OptClan.AddItem("Tie Clan (铁家) — Iron & Judgment", 4);
                OptClan.AddItem("Demonic Wanderer (散修/魔修) — Free Walker", 5);
                OptClan.Select(0);
            }

            if (OptAptitude != null && OptAptitude.ItemCount == 0)
            {
                OptAptitude.Clear();
                OptAptitude.AddItem("Ding Grade 丁等 (28.0% Aperture Sea Cap)", 0);
                OptAptitude.AddItem("Bing Grade 丙等 (52.0% Aperture Sea Cap)", 1);
                OptAptitude.AddItem("Yi Grade 乙等 (74.0% Aperture Sea Cap)", 2);
                OptAptitude.AddItem("Jia Grade 甲等 (93.0% Aperture Sea Cap - Peak Talent)", 3);
                OptAptitude.Select(1); // Default Bing grade
            }

            if (OptSector != null && OptSector.ItemCount == 0)
            {
                OptSector.Clear();
                OptSector.AddItem("Gu Yue Village (古月山寨, Southern Border)", 0);
                OptSector.SetItemMetadata(0, "Sector_SouthernBorder_GuYueVillage");
                OptSector.AddItem("Bai Clan Mountain (白家山, Southern Border)", 1);
                OptSector.SetItemMetadata(1, "Sector_SouthernBorder_BaiClanMountain");
                OptSector.AddItem("Xiong Ridge (熊家寨, Southern Border)", 2);
                OptSector.SetItemMetadata(2, "Sector_SouthernBorder_XiongRidge");
                OptSector.AddItem("Qing Mao Gorge (青茅幽峡, Southern Border)", 3);
                OptSector.SetItemMetadata(3, "Sector_SouthernBorder_QingMaoGorge");
                OptSector.Select(0);
            }

            if (OptVitalGu != null && OptVitalGu.ItemCount == 0)
            {
                OptVitalGu.Clear();
                OptVitalGu.AddItem("Moonlight Gu (月光蛊, Rank 1 Moon Path)", 0);
                OptVitalGu.SetItemMetadata(0, "gu_moonlight_r1");
                OptVitalGu.AddItem("Liquor Worm (酒虫, Rank 1 Food/Essence Path)", 1);
                OptVitalGu.SetItemMetadata(1, "gu_liquor_worm_r1");
                OptVitalGu.AddItem("Bear Strength Gu (熊力蛊, Rank 2 Strength Path)", 2);
                OptVitalGu.SetItemMetadata(2, "gu_bear_strength_r2");
                OptVitalGu.AddItem("White Boar Gu (白猪蛊, Rank 1 Strength Path)", 3);
                OptVitalGu.SetItemMetadata(3, "gu_white_boar_r1");
                OptVitalGu.AddItem("Little Light Gu (小光蛊, Rank 1 Light Path)", 4);
                OptVitalGu.SetItemMetadata(4, "gu_little_light_r1");
                OptVitalGu.Select(0);
            }

            if (OptDisposition != null && OptDisposition.ItemCount == 0)
            {
                OptDisposition.Clear();
                OptDisposition.AddItem("Gangzheng 刚正 (Righteous / Principled)", (int)TaiwuDisposition.Righteous);
                OptDisposition.AddItem("Ren'ai 仁爱 (Benevolent / Compassionate)", (int)TaiwuDisposition.Benevolent);
                OptDisposition.AddItem("Zhongyong 中庸 (Pragmatic / Moderate)", (int)TaiwuDisposition.Pragmatic);
                OptDisposition.AddItem("Panli 叛逆 (Rebellious / Unfettered)", (int)TaiwuDisposition.Rebellious);
                OptDisposition.AddItem("Kuangxie 狂邪 (Demonic / Ruthless)", (int)TaiwuDisposition.Egoist);
                OptDisposition.Select(4); // Default Demonic/Ruthless (Fang Yuan archetype)
            }

            if (BtnConfirmEmbark != null)
            {
                BtnConfirmEmbark.FocusMode = FocusModeEnum.None;
                BtnConfirmEmbark.Pressed += OnConfirmEmbarkPressed;
            }

            if (BtnBackFromCreation != null)
            {
                BtnBackFromCreation.FocusMode = FocusModeEnum.None;
                BtnBackFromCreation.Pressed += () => ShowSubMenu(MainMenuContainer);
            }
        }

        private void SetupLoadGameMenu()
        {
            if (BtnBackFromLoad != null)
            {
                BtnBackFromLoad.FocusMode = FocusModeEnum.None;
                BtnBackFromLoad.Pressed += () => ShowSubMenu(MainMenuContainer);
            }
        }

        private void SetupSettingsMenu()
        {
            if (BtnBackFromSettings != null)
            {
                BtnBackFromSettings.FocusMode = FocusModeEnum.None;
                BtnBackFromSettings.Pressed += () => ShowSubMenu(MainMenuContainer);
            }
        }

        private void ShowSubMenu(Control? target)
        {
            if (MainMenuContainer != null) MainMenuContainer.Visible = (MainMenuContainer == target);
            if (CharCreationContainer != null) CharCreationContainer.Visible = (CharCreationContainer == target);
            if (LoadGameContainer != null) LoadGameContainer.Visible = (LoadGameContainer == target);
            if (SettingsContainer != null) SettingsContainer.Visible = (SettingsContainer == target);
        }

        private void OnConfirmEmbarkPressed()
        {
            if (_transitioning) return;
            _transitioning = true;

            string name = TxtPlayerName?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Gu Yue Fang Yuan";
            }

            string clan = OptClan?.GetItemText(OptClan.Selected) ?? "Gu Yue Clan (古月氏)";
            
            float aptitude = 44.0f;
            if (OptAptitude != null)
            {
                aptitude = OptAptitude.Selected switch
                {
                    0 => 28.0f,
                    1 => 52.0f,
                    2 => 74.0f,
                    3 => 93.0f,
                    _ => 44.0f
                };
            }

            string sectorKey = WorldFactory.DefaultSectorKey;
            if (OptSector != null)
            {
                var meta = OptSector.GetItemMetadata(OptSector.Selected);
                if (meta.VariantType != Variant.Type.Nil)
                {
                    sectorKey = meta.AsString();
                }
            }

            string vitalGu = "gu_moonlight_r1";
            if (OptVitalGu != null)
            {
                var meta = OptVitalGu.GetItemMetadata(OptVitalGu.Selected);
                if (meta.VariantType != Variant.Type.Nil)
                {
                    vitalGu = meta.AsString();
                }
            }

            TaiwuDisposition disp = TaiwuDisposition.Egoist;
            if (OptDisposition != null)
            {
                disp = (TaiwuDisposition)OptDisposition.GetSelectedId();
            }

            var config = new NewRunConfig(
                runSeed: (int)(GD.Randi() % 1000000) + 1,
                playerName: name,
                originClan: clan,
                startingSectorKey: sectorKey,
                aptitudePercent: aptitude,
                disposition: disp,
                vitalGuId: vitalGu
            );

            PendingRunConfig = config;
            PendingLoadSlot = null;

            GD.Print($"[TitleScreen] Launching new journey for {config.PlayerName} (Aptitude: {config.AptitudePercent}%, Sector: {config.StartingSectorKey}, Vital Gu: {config.VitalGuId})...");

            var overlay = UniversalLoadingOverlay.EnsureInstance(GetTree());
            _ = overlay.WrapSceneTransitionAsync(
                "res://Scenes/OverworldScene.tscn",
                "Condensing Aperture & Awakening Primeval Sea (凝窍开海)...",
                "\"The Aperture holds the primeval sea; one wave of essence stirs a thousand transformations.\""
            );
        }

        private void PopulateLoadSlots()
        {
            if (SlotListContainer == null) return;

            foreach (var child in SlotListContainer.GetChildren())
            {
                child.QueueFree();
            }

            string rootDir = SavePaths.RootSaveDirectory;
            if (!Directory.Exists(rootDir))
            {
                var emptyLbl = new Label
                {
                    Text = "No recorded chronicles found (暂无存档).",
                    Modulate = new Color(0.6f, 0.65f, 0.7f, 1f)
                };
                SlotListContainer.AddChild(emptyLbl);
                return;
            }

            var dirs = Directory.GetDirectories(rootDir);
            if (dirs.Length == 0)
            {
                var emptyLbl = new Label
                {
                    Text = "No recorded chronicles found (暂无存档).",
                    Modulate = new Color(0.6f, 0.65f, 0.7f, 1f)
                };
                SlotListContainer.AddChild(emptyLbl);
                return;
            }

            foreach (var dir in dirs)
            {
                string slotId = Path.GetFileName(dir);
                string savePath = Path.Combine(dir, SavePaths.SAVE_FILE_NAME);
                if (!File.Exists(savePath)) continue;

                var meta = SaveDiskIO.LoadMetadata(slotId);
                string title = meta != null
                    ? $"【{slotId}】 {meta.PlayerName} — Year {meta.CurrentYear} {meta.SolarTermName} ({meta.CurrentMountainID})"
                    : $"【{slotId}】 Chronicle Record";

                var btn = new Button
                {
                    Text = title,
                    CustomMinimumSize = new Vector2(400f, 36f),
                    FocusMode = FocusModeEnum.None
                };
                btn.AddThemeFontSizeOverride("font_size", 11);
                btn.Pressed += () =>
                {
                    if (_transitioning) return;
                    _transitioning = true;

                    PendingRunConfig = null;
                    PendingLoadSlot = slotId;
                    GD.Print($"[TitleScreen] Selected slot [{slotId}] for loading. Launching OverworldScene...");

                    var overlay = UniversalLoadingOverlay.EnsureInstance(GetTree());
                    _ = overlay.WrapSceneTransitionAsync(
                        "res://Scenes/OverworldScene.tscn",
                        $"Restoring Chronicle [{slotId}]...",
                        "Resuming path of cultivation..."
                    );
                };
                SlotListContainer.AddChild(btn);
            }
        }
    }
}
