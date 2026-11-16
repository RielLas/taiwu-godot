using System;
using System.Threading.Tasks;
using Godot;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Systems;
using Color = Godot.Color;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Persistent Layer 99 Universal Loading & Transition Overlay.
    /// Provides smooth ~120 ms ink-wash fade transitions and blocks input (MouseFilter.Stop)
    /// during SaveSystem swaps, TransitSystem sector transitions, and scene changes.
    /// Strictly complies with Halal geometric/ink landscape aesthetics.
    /// </summary>
    public partial class UniversalLoadingOverlay : CanvasLayer
    {
        public static UniversalLoadingOverlay? Instance { get; private set; }

        private const float FadeDuration = 0.12f; // ~120 ms

        private static readonly string[] HalalCultivationQuotes =
        {
            "\"The Aperture holds the primeval sea; one wave of essence stirs a thousand transformations.\"",
            "\"Food for the Gu, essence for the master. Neglect sustenance, and the parasite turns upon the flesh.\"",
            "\"Five Regions separated by heavenly boundary walls; only supreme strength pierces the earth miasma.\"",
            "\"Primeval stone in hand, the path of the Gu unfolds.\"",
            "\"天地不仁，以万物为刍狗。蛊道苍茫，唯强者独尊。\"",
            "\"空窍藏真海，一念起风雷。\"",
            "\"五域界壁，天堑难越。唯坚道心，方登绝顶。\""
        };

        private static readonly string[] LoadingArtPaths =
        {
            "res://Resources/LoadingScreens/1.png",
            "res://Resources/LoadingScreens/2.png",
            "res://Resources/LoadingScreens/3.png",
            "res://Resources/LoadingScreens/4.jpeg",
            "res://Resources/LoadingScreens/5.jpg",
            "res://Resources/LoadingScreens/6.jpg",
            "res://Resources/LoadingScreens/7.png"
        };

        [Export] public Control? OverlayRoot { get; set; }
        [Export] public ColorRect? Backdrop { get; set; }
        [Export] public TextureRect? LoadingArtRect { get; set; }
        [Export] public Label? SealLabel { get; set; }
        [Export] public Label? TitleLabel { get; set; }
        [Export] public Label? TipLabel { get; set; }
        [Export] public ProgressBar? ProgressBar { get; set; }

        private Tween? _activeTween;
        private bool _isLoading = false;
        private int _quoteIndex = 0;

        public bool IsLoading => _isLoading;

        public static UniversalLoadingOverlay EnsureInstance(SceneTree tree)
        {
            if (Instance != null && GodotObject.IsInstanceValid(Instance))
            {
                return Instance;
            }

            var packed = GD.Load<PackedScene>("res://Scenes/UI/UniversalLoadingOverlay.tscn");
            var instance = packed.Instantiate<UniversalLoadingOverlay>();
            tree.Root.CallDeferred(Node.MethodName.AddChild, instance);
            Instance = instance;
            return instance;
        }

        public override void _EnterTree()
        {
            Layer = 99;
            ProcessMode = ProcessModeEnum.Always;

            if (Instance == null || !GodotObject.IsInstanceValid(Instance))
            {
                Instance = this;
            }
        }

        public override void _Ready()
        {
            if (OverlayRoot != null)
            {
                OverlayRoot.Visible = false;
                OverlayRoot.Modulate = new Color(1f, 1f, 1f, 0f);
                OverlayRoot.MouseFilter = Control.MouseFilterEnum.Ignore;
            }

            if (Backdrop != null)
            {
                Backdrop.MouseFilter = Control.MouseFilterEnum.Ignore;
            }

            WorldSignals.OnGameLoaded += HandleGameLoaded;
        }

        public override void _ExitTree()
        {
            WorldSignals.OnGameLoaded -= HandleGameLoaded;

            if (Instance == this)
            {
                Instance = null;
            }

            _activeTween?.Kill();
        }

        private void HandleGameLoaded(string slot, bool success)
        {
            if (_isLoading)
            {
                _ = HideAsync();
            }
        }

        public async Task ShowAsync(string title = "Traversing Heavenly Realm (穿梭界域)", string tip = "")
        {
            _isLoading = true;

            if (TitleLabel != null)
            {
                TitleLabel.Text = title;
            }

            if (LoadingArtRect != null)
            {
                int artIdx = Random.Shared.Next(LoadingArtPaths.Length);
                var tex = ResourceLoader.Load<Texture2D>(LoadingArtPaths[artIdx]);
                if (tex != null)
                {
                    LoadingArtRect.Texture = tex;
                    LoadingArtRect.Visible = true;
                }
            }

            if (TipLabel != null)
            {
                if (!string.IsNullOrWhiteSpace(tip))
                {
                    TipLabel.Text = tip;
                }
                else
                {
                    TipLabel.Text = HalalCultivationQuotes[_quoteIndex % HalalCultivationQuotes.Length];
                    _quoteIndex++;
                }
            }

            if (ProgressBar != null)
            {
                ProgressBar.Value = 0.15;
            }

            if (OverlayRoot != null)
            {
                OverlayRoot.Visible = true;
                OverlayRoot.MouseFilter = Control.MouseFilterEnum.Stop;
            }

            if (Backdrop != null)
            {
                Backdrop.MouseFilter = Control.MouseFilterEnum.Stop;
            }

            _activeTween?.Kill();
            _activeTween = CreateTween();
            _activeTween.SetParallel(true);
            if (OverlayRoot != null)
            {
                _activeTween.TweenProperty(OverlayRoot, "modulate:a", 1.0f, FadeDuration);
            }
            if (ProgressBar != null)
            {
                _activeTween.TweenProperty(ProgressBar, "value", 0.70, FadeDuration * 1.5f);
            }

            await ToSignal(_activeTween, Tween.SignalName.Finished);
        }

        public async Task HideAsync()
        {
            if (!_isLoading && OverlayRoot != null && !OverlayRoot.Visible)
            {
                return;
            }

            _activeTween?.Kill();
            _activeTween = CreateTween();
            _activeTween.SetParallel(true);

            if (ProgressBar != null)
            {
                _activeTween.TweenProperty(ProgressBar, "value", 1.0, FadeDuration * 0.5f);
            }
            if (OverlayRoot != null)
            {
                _activeTween.TweenProperty(OverlayRoot, "modulate:a", 0.0f, FadeDuration);
            }

            await ToSignal(_activeTween, Tween.SignalName.Finished);

            if (OverlayRoot != null)
            {
                OverlayRoot.Visible = false;
                OverlayRoot.MouseFilter = Control.MouseFilterEnum.Ignore;
            }

            if (Backdrop != null)
            {
                Backdrop.MouseFilter = Control.MouseFilterEnum.Ignore;
            }

            _isLoading = false;
        }

        public async Task<bool> WrapSaveLoadAsync(string slotId, WorldStateProvider provider)
        {
            await ShowAsync("Channeling Chronicle (载入存档)...", $"Restoring celestial record [{slotId}]...");

            bool ok = false;
            try
            {
                ok = SaveSystem.LoadAndSwap(slotId, provider);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[UniversalLoadingOverlay] LoadAndSwap threw: {ex.Message}");
            }

            // Short stabilization tick
            await Task.Delay(80);
            await HideAsync();
            return ok;
        }

        public async Task WrapSectorTransitAsync(WorldStore world, string sectorKey)
        {
            await ShowAsync("Traversing Boundary Wall (跨越五域界壁)...", $"Entering sector [{sectorKey}]...");

            try
            {
                TransitSystem.LoadSector(world, sectorKey);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[UniversalLoadingOverlay] LoadSector threw: {ex.Message}");
            }

            await Task.Delay(80);
            await HideAsync();
        }

        public async Task WrapSceneTransitionAsync(string scenePath, string title = "Traversing Heavenly Realm...", string tip = "")
        {
            await ShowAsync(title, tip);

            GetTree().ChangeSceneToFile(scenePath);

            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await Task.Delay(80);
            await HideAsync();
        }

        public async Task WrapActionAsync(Action action, string title = "Traversing Realm...", string tip = "")
        {
            await ShowAsync(title, tip);
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[UniversalLoadingOverlay] WrapAction threw: {ex.Message}");
            }
            await Task.Delay(80);
            await HideAsync();
        }
    }
}
