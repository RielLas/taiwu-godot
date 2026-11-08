using System;
using Godot;
using Color = Godot.Color;
using Mathf = Godot.Mathf;
using TaiwuGu.Core;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Page 2 of the ledger: the active aperture loadout, the reserve inventory and the
    /// cauldron jump. It rebuilds its cards from a <see cref="GuWormsSnapshot"/> and keeps no
    /// state; feeding is dispatched straight to the domain.
    /// </summary>
    public partial class GuWormsTab : MarginContainer
    {
        public event Action? OpenCauldronRequested;

        private VBoxContainer? _equippedContainer;
        private VBoxContainer? _reserveContainer;

        public GuWormsTab()
        {
            Name = "PanelGuWorms";
            AddThemeConstantOverride("margin_left", 14);
            AddThemeConstantOverride("margin_right", 14);
            AddThemeConstantOverride("margin_top", 10);
            AddThemeConstantOverride("margin_bottom", 10);

            Build();
        }

        private void Build()
        {
            var vBox = new VBoxContainer();
            vBox.AddThemeConstantOverride("separation", 8);
            AddChild(vBox);

            var topHBox = new HBoxContainer();
            vBox.AddChild(topHBox);

            var lblHeader = new Label
            {
                Text = "APERTURE PARASITE ECOLOGY & FEEDING (蛊虫饲养与存亡)",
                Modulate = new Color(0.204f, 0.827f, 0.6f, 1f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            lblHeader.AddThemeFontSizeOverride("font_size", 12);
            topHBox.AddChild(lblHeader);

            var btnOpenCauldron = new Button
            {
                Text = "◆ Alchemical Cauldron (合炼鼎炉) [R] ◆",
                CustomMinimumSize = new Vector2(250f, 28f),
                MouseFilter = MouseFilterEnum.Stop
            };
            btnOpenCauldron.Pressed += () => OpenCauldronRequested?.Invoke();
            topHBox.AddChild(btnOpenCauldron);

            var scroll = new ScrollContainer
            {
                SizeFlagsVertical = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever
            };
            vBox.AddChild(scroll);

            var contentList = new VBoxContainer();
            contentList.AddThemeConstantOverride("separation", 10);
            contentList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.AddChild(contentList);

            var lblActiveSection = new Label
            {
                Text = "ACTIVE APERTURE LOADOUT (5 COMBAT SLOTS)",
                Modulate = new Color(0.961f, 0.620f, 0.043f, 1f)
            };
            lblActiveSection.AddThemeFontSizeOverride("font_size", 11);
            contentList.AddChild(lblActiveSection);

            _equippedContainer = new VBoxContainer();
            _equippedContainer.AddThemeConstantOverride("separation", 6);
            contentList.AddChild(_equippedContainer);

            var lblReserveSection = new Label
            {
                Text = "RESERVE & CAPTURED GU WORMS (空窍蕴藏)",
                Modulate = new Color(0.22f, 0.74f, 0.97f, 1f)
            };
            lblReserveSection.AddThemeFontSizeOverride("font_size", 11);
            contentList.AddChild(lblReserveSection);

            _reserveContainer = new VBoxContainer();
            _reserveContainer.AddThemeConstantOverride("separation", 6);
            contentList.AddChild(_reserveContainer);
        }

        public void Bind(GuWormsSnapshot state)
        {
            if (_equippedContainer == null || _reserveContainer == null) return;

            foreach (var child in _equippedContainer.GetChildren()) child.QueueFree();
            foreach (var child in _reserveContainer.GetChildren()) child.QueueFree();

            if (state.Equipped.Count == 0)
            {
                _equippedContainer.AddChild(CreateEmptyLabel("No Gu worms currently loaded in aperture."));
            }
            else
            {
                foreach (var gu in state.Equipped)
                {
                    _equippedContainer.AddChild(CreateGuCard(gu, isEquipped: true));
                }
            }

            if (state.Reserve.Count == 0)
            {
                _reserveContainer.AddChild(CreateEmptyLabel("No reserve Gu worms in aperture. Hunt beasts or explore ancient caves to obtain wild Gu."));
            }
            else
            {
                foreach (var gu in state.Reserve)
                {
                    _reserveContainer.AddChild(CreateGuCard(gu, isEquipped: false));
                }
            }
        }

        private static Label CreateEmptyLabel(string text)
        {
            var lblEmpty = new Label { Text = text, Modulate = new Color(0.58f, 0.64f, 0.72f, 1f) };
            lblEmpty.AddThemeFontSizeOverride("font_size", 11);
            return lblEmpty;
        }

        private PanelContainer CreateGuCard(GuWormData gu, bool isEquipped)
        {
            var card = new PanelContainer { CustomMinimumSize = new Vector2(0f, 68f) };
            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.047f, 0.063f, 0.055f, 1f),
                BorderColor = gu.IsVitalGu ? new Color(0.984f, 0.749f, 0.141f, 1f) : new Color(0.133f, 0.200f, 0.188f, 1f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 3,
                CornerRadiusBottomRight = 3,
                CornerRadiusTopLeft = 3,
                CornerRadiusTopRight = 3
            };
            card.AddThemeStyleboxOverride("panel", style);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 8);
            margin.AddThemeConstantOverride("margin_right", 8);
            margin.AddThemeConstantOverride("margin_top", 6);
            margin.AddThemeConstantOverride("margin_bottom", 6);
            card.AddChild(margin);

            var hBox = new HBoxContainer();
            hBox.AddThemeConstantOverride("separation", 10);
            margin.AddChild(hBox);

            // Slot frame + Gu icon
            var slotFrame = new PanelContainer { CustomMinimumSize = new Vector2(52f, 52f) };
            Texture2D? frameTex = gu.IsVitalGu
                ? (LedgerArt.SlotJade ?? LedgerArt.SlotBrass)
                : (gu.Rank >= 3 ? LedgerArt.SlotBrass : (gu.Rank == 2 ? LedgerArt.SlotBronze : LedgerArt.SlotIron));
            var frameRect = new TextureRect { Texture = frameTex, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize };
            slotFrame.AddChild(frameRect);

            var iconRect = new TextureRect
            {
                Texture = GetGuIcon(gu.Name),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };
            slotFrame.AddChild(iconRect);
            hBox.AddChild(slotFrame);

            // Name, rank, slot and diet
            var infoVBox = new VBoxContainer();
            infoVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            hBox.AddChild(infoVBox);

            string vitalTag = gu.IsVitalGu
                ? "  [VITAL GU / 本命蛊]"
                : (gu.HasHostileWill ? "  [WILD / HOSTILE WILL]" : (isEquipped ? "" : "  [TAME RESERVE]"));
            Color nameColor = gu.IsVitalGu
                ? new Color(0.984f, 0.749f, 0.141f, 1f)
                : (gu.HasHostileWill ? new Color(0.937f, 0.267f, 0.267f, 1f) : new Color(0.88f, 0.91f, 0.94f, 1f));

            var lblName = new Label { Text = $"{gu.Name}{vitalTag}", Modulate = nameColor };
            lblName.AddThemeFontSizeOverride("font_size", 12);
            infoVBox.AddChild(lblName);

            var lblDetails = new Label
            {
                Text = $"Rank {gu.Rank} | Slot: {gu.Slot} | Diet: {gu.Diet}",
                Modulate = new Color(0.58f, 0.64f, 0.72f, 1f)
            };
            lblDetails.AddThemeFontSizeOverride("font_size", 10);
            infoVBox.AddChild(lblDetails);

            // Satiety meter
            var satVBox = new VBoxContainer { CustomMinimumSize = new Vector2(160f, 0f) };
            hBox.AddChild(satVBox);

            Color satColor = gu.Satiety <= 25f
                ? new Color(0.937f, 0.267f, 0.267f, 1f)
                : (gu.Satiety <= 50f ? new Color(0.984f, 0.749f, 0.141f, 1f) : new Color(0.204f, 0.827f, 0.6f, 1f));
            var lblSatiety = new Label
            {
                Text = $"Satiety: {gu.Satiety:F0}% ({gu.GetStarvationStage()})",
                Modulate = satColor
            };
            lblSatiety.AddThemeFontSizeOverride("font_size", 10);
            satVBox.AddChild(lblSatiety);

            var satGauge = new StyledGaugeControl
            {
                CustomMinimumSize = new Vector2(160f, 16f),
                FrameTexture = LedgerArt.FrameBarBronze,
                FillTexture = gu.Satiety <= 25f ? LedgerArt.FillCrimson : LedgerArt.FillEmerald,
                Value = Mathf.Clamp(gu.Satiety / 100f, 0f, 1f),
                CenterText = $"{gu.Satiety:F0}%"
            };
            satVBox.AddChild(satGauge);

            // Feeding action
            var btnFeed = new Button
            {
                Text = "Feed 1x Ration",
                CustomMinimumSize = new Vector2(110f, 34f),
                MouseFilter = MouseFilterEnum.Stop
            };
            btnFeed.Pressed += () => GameEventManager.RaiseRequestFeedGu(gu.GuID, gu.Diet, 1);
            hBox.AddChild(btnFeed);

            return card;
        }

        private static Texture2D? GetGuIcon(string guName)
        {
            if (string.IsNullOrEmpty(guName)) return LedgerArt.IconMoonShadow;
            if (guName.Contains("Moon Shadow") || guName.Contains("Moonlight")) return LedgerArt.IconMoonShadow;
            if (guName.Contains("Boar") || guName.Contains("Strength")) return LedgerArt.IconBoarStrength;
            if (guName.Contains("Moonglow")) return LedgerArt.IconMoonglow;
            return LedgerArt.IconMoonShadow;
        }
    }
}
