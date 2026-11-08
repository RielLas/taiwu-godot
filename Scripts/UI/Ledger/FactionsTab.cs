using Godot;
using Color = Godot.Color;
using TaiwuGu.Core;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Page 4 of the ledger: the six Southern Border factions with their standing tier,
    /// tariffs, sanctuary status and reputation gauge. Renders a <see cref="FactionsSnapshot"/>.
    /// </summary>
    public partial class FactionsTab : MarginContainer
    {
        private VBoxContainer? _factionsContainer;

        public FactionsTab()
        {
            Name = "PanelFactions";
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

            var lblHeader = new Label
            {
                Text = "AUTONOMOUS INSTITUTIONAL HIERARCHY (宗族恩仇与阵营声望)",
                Modulate = new Color(0.22f, 0.74f, 0.97f, 1f)
            };
            lblHeader.AddThemeFontSizeOverride("font_size", 12);
            vBox.AddChild(lblHeader);

            var lblSub = new Label
            {
                Text = "Dynamic diplomatic standing across the 6 major Southern Border powers. Dictates trade tariffs, pavilion sanctuary, and bounty pursuit.",
                Modulate = new Color(0.58f, 0.64f, 0.72f, 1f)
            };
            lblSub.AddThemeFontSizeOverride("font_size", 10);
            vBox.AddChild(lblSub);

            var scroll = new ScrollContainer
            {
                SizeFlagsVertical = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever
            };
            vBox.AddChild(scroll);

            _factionsContainer = new VBoxContainer();
            _factionsContainer.AddThemeConstantOverride("separation", 8);
            _factionsContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.AddChild(_factionsContainer);
        }

        public void Bind(FactionsSnapshot state)
        {
            if (_factionsContainer == null) return;

            foreach (var child in _factionsContainer.GetChildren()) child.QueueFree();

            foreach (var faction in state.Factions)
            {
                _factionsContainer.AddChild(CreateFactionCard(faction));
            }
        }

        private static PanelContainer CreateFactionCard(FactionData faction)
        {
            var card = new PanelContainer { CustomMinimumSize = new Vector2(0f, 84f) };
            var tier = faction.GetStandingTier();
            Color accentColor = tier switch
            {
                FactionStandingTier.Revered  => new Color(0.204f, 0.827f, 0.6f, 1f),
                FactionStandingTier.Friendly => new Color(0.220f, 0.741f, 0.973f, 1f),
                FactionStandingTier.Neutral  => new Color(0.580f, 0.640f, 0.720f, 1f),
                FactionStandingTier.Hostile  => new Color(0.976f, 0.451f, 0.090f, 1f),
                FactionStandingTier.Nemesis  => new Color(0.937f, 0.267f, 0.267f, 1f),
                _ => new Color(0.5f, 0.5f, 0.5f, 1f)
            };

            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.047f, 0.063f, 0.055f, 1f),
                BorderColor = accentColor,
                BorderWidthLeft = 4,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                CornerRadiusBottomRight = 3,
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

            // Clan banner, or the calligraphy seal when the clan has no banner
            Texture2D? bannerTex = faction.ID switch
            {
                FactionID.GuYueClan => LedgerArt.BannerGuYue,
                FactionID.BaiClan   => LedgerArt.BannerBai,
                FactionID.XiongClan => LedgerArt.BannerXiong,
                FactionID.JiaConsortium => LedgerArt.DaoHuman,
                FactionID.TieClan   => LedgerArt.DaoStrength,
                _ => null
            };

            var bannerPanel = new PanelContainer { CustomMinimumSize = new Vector2(46f, 68f) };
            if (bannerTex != null)
            {
                var bannerRect = new TextureRect
                {
                    Texture = bannerTex,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
                };
                bannerPanel.AddChild(bannerRect);
            }
            else
            {
                var lblSeal = new Label
                {
                    Text = faction.SealCalligraphy,
                    Modulate = accentColor,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                lblSeal.AddThemeFontSizeOverride("font_size", 14);
                bannerPanel.AddChild(lblSeal);
            }
            hBox.AddChild(bannerPanel);

            // Titles and diplomatic status
            var infoVBox = new VBoxContainer();
            infoVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            hBox.AddChild(infoVBox);

            var lblTitle = new Label
            {
                Text = $"{faction.Name} ({faction.ChineseName})",
                Modulate = new Color(0.961f, 0.620f, 0.043f, 1f)
            };
            lblTitle.AddThemeFontSizeOverride("font_size", 12);
            infoVBox.AddChild(lblTitle);

            var lblStanding = new Label
            {
                Text = $"Standing: {faction.GetStandingTitle()}",
                Modulate = accentColor
            };
            lblStanding.AddThemeFontSizeOverride("font_size", 10);
            infoVBox.AddChild(lblStanding);

            string tradeStatus;
            float priceMult = faction.GetPriceMultiplier();
            if (priceMult >= 900f) tradeStatus = "Trade Embargoed (拒绝交易)";
            else if (priceMult > 1.0f) tradeStatus = $"Tariff +{(priceMult - 1f) * 100:F0}% (冷眼加价)";
            else if (priceMult < 1.0f) tradeStatus = $"Discount -{(1f - priceMult) * 100:F0}% (知己特惠)";
            else tradeStatus = "Standard Rates (平价交易)";

            string sanctuaryStatus = (tier == FactionStandingTier.Nemesis || tier == FactionStandingTier.Hostile)
                ? "Sanctuary: Barred (驱逐)"
                : "Sanctuary: Welcome (容留)";

            var lblTariff = new Label
            {
                Text = $"{tradeStatus} | {sanctuaryStatus}",
                Modulate = new Color(0.58f, 0.64f, 0.72f, 1f)
            };
            lblTariff.AddThemeFontSizeOverride("font_size", 9);
            infoVBox.AddChild(lblTariff);

            var lblLore = new Label
            {
                Text = faction.Description,
                Modulate = new Color(0.45f, 0.50f, 0.55f, 1f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            lblLore.AddThemeFontSizeOverride("font_size", 9);
            infoVBox.AddChild(lblLore);

            // Reputation gauge in the [-300..+300] band
            var gaugeVBox = new VBoxContainer { CustomMinimumSize = new Vector2(170f, 0f) };
            hBox.AddChild(gaugeVBox);

            string scoreSign = faction.ReputationScore > 0 ? $"+{faction.ReputationScore}" : $"{faction.ReputationScore}";
            var lblScore = new Label
            {
                Text = $"Score: {scoreSign} / [-300..+300]",
                Modulate = accentColor
            };
            lblScore.AddThemeFontSizeOverride("font_size", 10);
            gaugeVBox.AddChild(lblScore);

            var repGauge = new FactionReputationGaugeControl
            {
                CustomMinimumSize = new Vector2(170f, 16f),
                Score = faction.ReputationScore
            };
            gaugeVBox.AddChild(repGauge);

            return card;
        }
    }
}
