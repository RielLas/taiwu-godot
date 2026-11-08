using Godot;
using Color = Godot.Color;
using TaiwuGu.Core;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Page 3 of the ledger: innate Human Path dao marks, the external path seals and the
    /// Great Dao prohibition lore. Renders a <see cref="DaoMarksSnapshot"/> only.
    /// </summary>
    public partial class DaoMarksTab : MarginContainer
    {
        private Label? _lblHumanCardTitle;
        private Label? _lblHumanCardDesc;

        public DaoMarksTab()
        {
            Name = "PanelDaoMarks";
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

            var headerHBox = new HBoxContainer();
            vBox.AddChild(headerHBox);

            var lblSection = new Label
            {
                Text = "DAO MARKS & MORTAL RESTRICTIONS (道痕与凡人禁制)",
                Modulate = new Color(0.22f, 0.74f, 0.97f, 1f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            lblSection.AddThemeFontSizeOverride("font_size", 12);
            headerHBox.AddChild(lblSection);

            var baguaMedallion = new PanelContainer { CustomMinimumSize = new Vector2(40f, 40f) };
            var baguaTex = new TextureRect
            {
                Texture = LedgerArt.BaguaMandala,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };
            baguaMedallion.AddChild(baguaTex);
            headerHBox.AddChild(baguaMedallion);

            // Innate Human Path card with the emerald left accent
            var humanCard = new PanelContainer();
            var humanStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.047f, 0.063f, 0.055f, 1f),
                BorderColor = new Color(0.204f, 0.827f, 0.6f, 1f),
                BorderWidthLeft = 4,
                BorderWidthTop = 1,
                BorderWidthRight = 1,
                BorderWidthBottom = 1,
                CornerRadiusTopRight = 3,
                CornerRadiusBottomRight = 3
            };
            humanCard.AddThemeStyleboxOverride("panel", humanStyle);
            vBox.AddChild(humanCard);

            var humanMargin = new MarginContainer();
            humanMargin.AddThemeConstantOverride("margin_left", 10);
            humanMargin.AddThemeConstantOverride("margin_right", 10);
            humanMargin.AddThemeConstantOverride("margin_top", 8);
            humanMargin.AddThemeConstantOverride("margin_bottom", 8);
            humanCard.AddChild(humanMargin);

            var humanHBox = new HBoxContainer();
            humanHBox.AddThemeConstantOverride("separation", 12);
            humanMargin.AddChild(humanHBox);

            var humanSealRect = new TextureRect
            {
                Texture = LedgerArt.DaoHuman,
                CustomMinimumSize = new Vector2(44f, 44f),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };
            humanHBox.AddChild(humanSealRect);

            var humanTextVBox = new VBoxContainer();
            humanHBox.AddChild(humanTextVBox);

            _lblHumanCardTitle = new Label
            {
                Text = "Innate Human Path Dao Marks (人道道痕)",
                Modulate = new Color(0.961f, 0.620f, 0.043f, 1f)
            };
            _lblHumanCardTitle.AddThemeFontSizeOverride("font_size", 12);
            humanTextVBox.AddChild(_lblHumanCardTitle);

            _lblHumanCardDesc = new Label
            {
                Text = "• Canon Law: Normal mortals capped at <= 10 marks; Ten Extreme Physiques possess 50-100 marks.",
                Modulate = new Color(0.58f, 0.64f, 0.72f, 1f)
            };
            _lblHumanCardDesc.AddThemeFontSizeOverride("font_size", 10);
            humanTextVBox.AddChild(_lblHumanCardDesc);

            // External Great Dao marks (seven paths)
            var lblExternalHeader = new Label
            {
                Text = "EXTERNAL PATH DAO MARKS (GREAT DAO PROHIBITION - 凡躯不承道痕)",
                Modulate = new Color(0.88f, 0.91f, 0.94f, 1f)
            };
            lblExternalHeader.AddThemeFontSizeOverride("font_size", 11);
            vBox.AddChild(lblExternalHeader);

            var pathsGrid = new GridContainer { Columns = 2 };
            pathsGrid.AddThemeConstantOverride("h_separation", 10);
            pathsGrid.AddThemeConstantOverride("v_separation", 6);
            vBox.AddChild(pathsGrid);

            (Texture2D? icon, string name, string cnName)[] paths = new[]
            {
                (LedgerArt.DaoMoon,     "Moon Path",        "月道"),
                (LedgerArt.DaoStrength, "Strength Path",    "力道"),
                (LedgerArt.DaoBone,     "Bone Path",        "骨道"),
                (LedgerArt.DaoPoison,   "Poison Path",      "毒道"),
                (LedgerArt.DaoBlood,    "Blood Path",       "血道"),
                (LedgerArt.DaoRefine,   "Refinement Path",  "炼道"),
                (LedgerArt.DaoEnslave,  "Enslavement Path", "奴道")
            };

            foreach (var p in paths)
            {
                pathsGrid.AddChild(CreateExternalPathCard(p.icon, p.name, p.cnName));
            }

            var lblLore = new Label
            {
                Text = "\"A mortal aperture and physical body cannot bear the immense weight of external Great Dao marks without collapsing into dust.\nOnly upon balancing heaven, earth, and human qi, withstanding the Earthly Calamity and Heavenly Tribulation to ascend to a Rank 6 Gu Immortal, can external Dao marks permanently etch into the immortal aperture.\"",
                Modulate = new Color(0.58f, 0.64f, 0.72f, 1f),
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            lblLore.AddThemeFontSizeOverride("font_size", 10);
            vBox.AddChild(lblLore);
        }

        public void Bind(DaoMarksSnapshot state)
        {
            if (_lblHumanCardTitle != null)
                _lblHumanCardTitle.Text = $"Innate Human Path Dao Marks (人道道痕): {state.HumanPathDaoMarks} Marks (Aptitude: {state.AptitudePercent:F0}%)";

            if (_lblHumanCardDesc != null)
                _lblHumanCardDesc.Text = $"• Cultivation Amplification: +{state.HumanPathDaoMarks * 2}% Substage Wall-Washing Rate, +{state.HumanPathDaoMarks}% Major Wall-Battering Force.\n• Canon Law: Normal mortals capped at <= 10 marks; Ten Extreme Physiques possess 50-100 marks.";
        }

        private static PanelContainer CreateExternalPathCard(Texture2D? icon, string name, string cnName)
        {
            var card = new PanelContainer { CustomMinimumSize = new Vector2(380f, 38f) };
            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.047f, 0.063f, 0.055f, 1f),
                BorderColor = new Color(0.133f, 0.200f, 0.188f, 1f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 2,
                CornerRadiusBottomRight = 2,
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2
            };
            card.AddThemeStyleboxOverride("panel", style);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 8);
            margin.AddThemeConstantOverride("margin_right", 8);
            margin.AddThemeConstantOverride("margin_top", 4);
            margin.AddThemeConstantOverride("margin_bottom", 4);
            card.AddChild(margin);

            var hBox = new HBoxContainer();
            hBox.AddThemeConstantOverride("separation", 8);
            margin.AddChild(hBox);

            var iconRect = new TextureRect
            {
                Texture = icon,
                CustomMinimumSize = new Vector2(28f, 28f),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };
            hBox.AddChild(iconRect);

            var textVBox = new VBoxContainer();
            textVBox.AddThemeConstantOverride("separation", 0);
            hBox.AddChild(textVBox);

            var lblTitle = new Label { Text = $"{name} ({cnName})", Modulate = new Color(0.88f, 0.91f, 0.94f, 1f) };
            lblTitle.AddThemeFontSizeOverride("font_size", 11);
            textVBox.AddChild(lblTitle);

            var lblLocked = new Label
            {
                Text = "0 Marks (Locked - Rank 6 Ascension)",
                Modulate = new Color(0.392f, 0.455f, 0.545f, 1f)
            };
            lblLocked.AddThemeFontSizeOverride("font_size", 9);
            textVBox.AddChild(lblLocked);

            return card;
        }
    }
}
