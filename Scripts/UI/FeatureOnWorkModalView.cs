using System;
using Godot;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using Color = Godot.Color;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Reusable Xianxia Modal for systems currently in progress / on work.
    /// Strictly adheres to Constitutional Law 2 (Event symmetry) and Halal directives.
    /// </summary>
    public partial class FeatureOnWorkModalView : CanvasLayer, IModalView
    {
        private bool _isOpen = false;
        public bool IsOpen => _isOpen;
        public void Dismiss() => CloseModal();
        private PanelContainer? _panelContainer;
        private Label? _lblTitle;
        private Label? _lblFeatureName;
        private Label? _lblDetail;
        private Texture2D? _texBannerMountains;

        public override void _EnterTree()
        {
            Layer = 16; // Top-level modal
            Visible = false;

            LoadTextures();
            BuildViewTree();

            WorldSignals.OnOnWorkNoticeRequested += HandleShowOnWorkNotice;
            WorldSignals.OnEscapeRequested       += HandleEscapeRequested;
        }

        public override void _ExitTree()
        {
            WorldSignals.OnOnWorkNoticeRequested -= HandleShowOnWorkNotice;
            WorldSignals.OnEscapeRequested       -= HandleEscapeRequested;
        }

        private void LoadTextures()
        {
            try
            {
                string globalPath = ProjectSettings.GlobalizePath("res://Resources/UI/banner_ink_mountains.png");
                if (System.IO.File.Exists(globalPath))
                {
                    var img = Image.LoadFromFile(globalPath);
                    if (img != null && !img.IsEmpty())
                    {
                        _texBannerMountains = ImageTexture.CreateFromImage(img);
                        return;
                    }
                }
            }
            catch { }

            if (ResourceLoader.Exists("res://Resources/UI/banner_ink_mountains.png"))
            {
                try { _texBannerMountains = GD.Load<Texture2D>("res://Resources/UI/banner_ink_mountains.png"); } catch { }
            }
        }

        private void BuildViewTree()
        {
            // 1. Dark Backdrop
            var backdrop = new ColorRect
            {
                Name = "OnWorkBackdrop",
                Color = new Color(0.012f, 0.024f, 0.024f, 0.88f),
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            backdrop.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(backdrop);

            // 2. Centered Modal Box
            _panelContainer = new PanelContainer
            {
                Name = "OnWorkPanel",
                CustomMinimumSize = new Vector2(540, 270),
                MouseFilter = Control.MouseFilterEnum.Stop
            };
            _panelContainer.SetAnchorsPreset(Control.LayoutPreset.Center);
            _panelContainer.GrowHorizontal = Control.GrowDirection.Both;
            _panelContainer.GrowVertical = Control.GrowDirection.Both;
            _panelContainer.OffsetLeft = -270;
            _panelContainer.OffsetRight = 270;
            _panelContainer.OffsetTop = -135;
            _panelContainer.OffsetBottom = 135;

            var panelStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.043f, 0.075f, 0.071f, 0.98f),
                BorderWidthLeft = 2,
                BorderWidthRight = 2,
                BorderWidthTop = 2,
                BorderWidthBottom = 2,
                BorderColor = new Color(0.984f, 0.749f, 0.141f, 0.85f), // Imperial gold trim
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 6,
                CornerRadiusBottomRight = 6,
                ShadowColor = new Color(0f, 0f, 0f, 0.75f),
                ShadowSize = 16
            };
            _panelContainer.AddThemeStyleboxOverride("panel", panelStyle);
            backdrop.AddChild(_panelContainer);

            var vBox = new VBoxContainer
            {
                CustomMinimumSize = new Vector2(500, 240)
            };
            vBox.AddThemeConstantOverride("separation", 10);
            _panelContainer.AddChild(vBox);

            // 3. Shanshui Banner
            if (_texBannerMountains != null)
            {
                var banner = new TextureRect
                {
                    Texture = _texBannerMountains,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                    CustomMinimumSize = new Vector2(500, 42)
                };
                vBox.AddChild(banner);
            }

            // 4. Title
            _lblTitle = new Label
            {
                Text = "◆ FEATURE UNDER ACTIVE RESTORATION ◆",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _lblTitle.AddThemeColorOverride("font_color", new Color(0.984f, 0.749f, 0.141f, 1f));
            _lblTitle.AddThemeFontSizeOverride("font_size", 14);
            vBox.AddChild(_lblTitle);

            // 5. Feature Name
            _lblFeatureName = new Label
            {
                Text = "Jia Family Caravan Barter Economy (贾家商队)",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _lblFeatureName.AddThemeColorOverride("font_color", new Color(0.204f, 0.827f, 0.600f, 1f));
            _lblFeatureName.AddThemeFontSizeOverride("font_size", 13);
            vBox.AddChild(_lblFeatureName);

            // 6. Detail Description
            _lblDetail = new Label
            {
                Text = "Status: ON WORK\nRestoration roadmap sequence underway.",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            _lblDetail.AddThemeColorOverride("font_color", new Color(0.88f, 0.92f, 0.94f, 1f));
            _lblDetail.AddThemeFontSizeOverride("font_size", 11);
            vBox.AddChild(_lblDetail);

            // Spacer
            var spacer = new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            vBox.AddChild(spacer);

            // 7. Dismiss Button
            var btnClose = new Button
            {
                Text = "Understood (遵命) [Esc / Click]",
                CustomMinimumSize = new Vector2(220, 34),
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
            };
            var btnStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.13f, 0.12f, 1f),
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderWidthTop = 1,
                BorderWidthBottom = 1,
                BorderColor = new Color(0.984f, 0.749f, 0.141f, 0.85f),
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4
            };
            var btnHover = new StyleBoxFlat
            {
                BgColor = new Color(0.14f, 0.22f, 0.20f, 1f),
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderWidthTop = 1,
                BorderWidthBottom = 1,
                BorderColor = Colors.White,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4
            };
            btnClose.AddThemeStyleboxOverride("normal", btnStyle);
            btnClose.AddThemeStyleboxOverride("hover", btnHover);
            btnClose.AddThemeStyleboxOverride("pressed", btnHover);
            btnClose.AddThemeColorOverride("font_color", new Color(0.984f, 0.749f, 0.141f, 1f));
            btnClose.AddThemeColorOverride("font_hover_color", Colors.White);
            btnClose.AddThemeFontSizeOverride("font_size", 12);
            btnClose.Pressed += CloseModal;
            vBox.AddChild(btnClose);
        }

        private void HandleShowOnWorkNotice(string featureName, string detail)
        {
            _isOpen = true;
            Visible = true;
            if (_lblFeatureName != null) _lblFeatureName.Text = featureName;
            if (_lblDetail != null) _lblDetail.Text = $"Status: ON WORK\n{detail}";
            WorldSignals.EmitModalOpened(this);
            WorldSignals.EmitUIModalActive(true);
        }

        private void HandleEscapeRequested()
        {
            if (!_isOpen) return;

            WorldSignals.EscapeHandled = true;
            CloseModal();
        }

        public void CloseModal()
        {
            if (!_isOpen) return;
            _isOpen = false;
            Visible = false;
            WorldSignals.EmitModalClosed(this);
            WorldSignals.EmitUIModalActive(false);
        }
    }
}
