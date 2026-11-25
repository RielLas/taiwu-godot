using System;
using System.Collections.Generic;
using Godot;
using Color = Godot.Color;
using TaiwuGu.Core;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Page 3 of the ledger: the cultivator's physical inventory, primeval stones treasury,
    /// food stockpile quantities by GuDietType, and parasite ecology sustenance.
    /// Rebuilds its view purely from an <see cref="InventorySnapshot"/>.
    /// </summary>
    public partial class InventoryTab : MarginContainer
    {
        public event Action<string, GuDietType, int>? FeedGuRequested;

        private Label? _lblStones;
        private VBoxContainer? _dietSummaryContainer;
        private VBoxContainer? _itemsContainer;

        public InventoryTab()
        {
            Name = "PanelInventory";
            AddThemeConstantOverride("margin_left", 14);
            AddThemeConstantOverride("margin_right", 14);
            AddThemeConstantOverride("margin_top", 10);
            AddThemeConstantOverride("margin_bottom", 10);

            Build();
        }

        private void Build()
        {
            var vBox = new VBoxContainer();
            vBox.AddThemeConstantOverride("separation", 10);
            AddChild(vBox);

            // 1. Primeval Stones Treasury Header
            var treasuryCard = new PanelContainer { CustomMinimumSize = new Vector2(0f, 54f) };
            var treasuryStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.047f, 0.063f, 0.055f, 1f),
                BorderColor = new Color(0.984f, 0.749f, 0.141f, 0.85f), // Amber gold border
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 4,
                CornerRadiusBottomRight = 4,
                CornerRadiusTopLeft = 4,
                CornerRadiusTopRight = 4
            };
            treasuryCard.AddThemeStyleboxOverride("panel", treasuryStyle);

            var treasuryMargin = new MarginContainer();
            treasuryMargin.AddThemeConstantOverride("margin_left", 12);
            treasuryMargin.AddThemeConstantOverride("margin_right", 12);
            treasuryMargin.AddThemeConstantOverride("margin_top", 8);
            treasuryMargin.AddThemeConstantOverride("margin_bottom", 8);
            treasuryCard.AddChild(treasuryMargin);

            var treasuryHBox = new HBoxContainer();
            treasuryHBox.AddThemeConstantOverride("separation", 16);
            treasuryMargin.AddChild(treasuryHBox);

            var stoneIconFrame = new PanelContainer { CustomMinimumSize = new Vector2(38f, 38f) };
            var stoneFrameRect = new TextureRect
            {
                Texture = LedgerArt.SlotJade ?? LedgerArt.SlotBrass,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize
            };
            stoneIconFrame.AddChild(stoneFrameRect);
            var stoneDaoRect = new TextureRect
            {
                Texture = LedgerArt.DaoRefine ?? LedgerArt.DaoHuman,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };
            stoneIconFrame.AddChild(stoneDaoRect);
            treasuryHBox.AddChild(stoneIconFrame);

            var treasuryInfo = new VBoxContainer();
            treasuryInfo.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            treasuryHBox.AddChild(treasuryInfo);

            _lblStones = new Label
            {
                Text = "PRIMEVAL STONES: 0 (元石)",
                Modulate = new Color(0.984f, 0.749f, 0.141f, 1f) // Amber
            };
            _lblStones.AddThemeFontSizeOverride("font_size", 14);
            treasuryInfo.AddChild(_lblStones);

            var lblStonesDesc = new Label
            {
                Text = "Universal spiritual currency of the Gu world. Used for trade, refining, and rapid aperture replenishment.",
                Modulate = new Color(0.58f, 0.64f, 0.72f, 1f)
            };
            lblStonesDesc.AddThemeFontSizeOverride("font_size", 10);
            treasuryInfo.AddChild(lblStonesDesc);

            vBox.AddChild(treasuryCard);

            // 2. Scrollable Section for Diet Demands & Food Stockpile
            var scroll = new ScrollContainer
            {
                SizeFlagsVertical = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever
            };
            vBox.AddChild(scroll);

            var contentList = new VBoxContainer();
            contentList.AddThemeConstantOverride("separation", 12);
            contentList.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.AddChild(contentList);

            // 2a. Diet Demands Section
            var lblDietHeader = new Label
            {
                Text = "ECOLOGY CONSUMPTION LEDGER (蛊虫食性与月耗需求)",
                Modulate = new Color(0.204f, 0.827f, 0.6f, 1f) // Jade
            };
            lblDietHeader.AddThemeFontSizeOverride("font_size", 11);
            contentList.AddChild(lblDietHeader);

            _dietSummaryContainer = new VBoxContainer();
            _dietSummaryContainer.AddThemeConstantOverride("separation", 4);
            contentList.AddChild(_dietSummaryContainer);

            // 2b. Stockpile Cards Section
            var lblStockpileHeader = new Label
            {
                Text = "FOOD STOCKPILE & PARASITE RATIONS (行囊食料储蓄)",
                Modulate = new Color(0.22f, 0.74f, 0.97f, 1f) // Cyan
            };
            lblStockpileHeader.AddThemeFontSizeOverride("font_size", 11);
            contentList.AddChild(lblStockpileHeader);

            _itemsContainer = new VBoxContainer();
            _itemsContainer.AddThemeConstantOverride("separation", 6);
            contentList.AddChild(_itemsContainer);
        }

        public void Bind(InventorySnapshot snapshot)
        {
            if (_lblStones != null)
            {
                _lblStones.Text = $"PRIMEVAL STONES: {snapshot.PrimevalStones} (元石)";
            }

            // Bind Diet Demands
            if (_dietSummaryContainer != null)
            {
                foreach (var child in _dietSummaryContainer.GetChildren()) child.QueueFree();

                if (snapshot.DietDemands.Count == 0)
                {
                    _dietSummaryContainer.AddChild(CreateEmptyLabel("No active Gu worms require sustenance at this time."));
                }
                else
                {
                    foreach (var demand in snapshot.DietDemands)
                    {
                        _dietSummaryContainer.AddChild(CreateDietDemandRow(demand));
                    }
                }
            }

            // Bind Food Items
            if (_itemsContainer != null)
            {
                foreach (var child in _itemsContainer.GetChildren()) child.QueueFree();

                if (snapshot.FoodStockpile.Count == 0)
                {
                    _itemsContainer.AddChild(CreateEmptyLabel("Inventory stockpile is empty. Purchase food from caravans or forage in the wilderness."));
                }
                else
                {
                    foreach (var item in snapshot.FoodStockpile)
                    {
                        _itemsContainer.AddChild(CreateItemCard(item));
                    }
                }
            }
        }

        private static Label CreateEmptyLabel(string text)
        {
            var lblEmpty = new Label { Text = text, Modulate = new Color(0.58f, 0.64f, 0.72f, 1f) };
            lblEmpty.AddThemeFontSizeOverride("font_size", 11);
            return lblEmpty;
        }

        private PanelContainer CreateDietDemandRow(DietDemandInfo demand)
        {
            var panel = new PanelContainer();
            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.06f, 0.08f, 0.07f, 0.8f),
                BorderColor = new Color(0.18f, 0.25f, 0.22f, 1f),
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusBottomLeft = 2,
                CornerRadiusBottomRight = 2,
                CornerRadiusTopLeft = 2,
                CornerRadiusTopRight = 2
            };
            panel.AddThemeStyleboxOverride("panel", style);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 8);
            margin.AddThemeConstantOverride("margin_right", 8);
            margin.AddThemeConstantOverride("margin_top", 4);
            margin.AddThemeConstantOverride("margin_bottom", 4);
            panel.AddChild(margin);

            var hBox = new HBoxContainer();
            hBox.AddThemeConstantOverride("separation", 10);
            margin.AddChild(hBox);

            bool hasStock = demand.TotalStockpile >= demand.ActiveConsumersCount;
            Color statusColor = hasStock ? new Color(0.204f, 0.827f, 0.6f, 1f) : new Color(0.937f, 0.267f, 0.267f, 1f);
            string statusText = hasStock ? "[SUPPLIED]" : "[DEFICIT WARNING]";

            var lblDiet = new Label
            {
                Text = $"◆ {demand.DietType} ({demand.ActiveConsumersCount} Gu Worms)",
                Modulate = new Color(0.984f, 0.749f, 0.141f, 1f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            lblDiet.AddThemeFontSizeOverride("font_size", 11);
            hBox.AddChild(lblDiet);

            var lblConsumers = new Label
            {
                Text = $"Consumers: {demand.DependentGuNames}",
                Modulate = new Color(0.72f, 0.78f, 0.85f, 1f),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            lblConsumers.AddThemeFontSizeOverride("font_size", 10);
            hBox.AddChild(lblConsumers);

            var lblStock = new Label
            {
                Text = $"Stock: {demand.TotalStockpile}x | {statusText}",
                Modulate = statusColor
            };
            lblStock.AddThemeFontSizeOverride("font_size", 11);
            hBox.AddChild(lblStock);

            return panel;
        }

        private PanelContainer CreateItemCard(InventoryItem item)
        {
            var card = new PanelContainer { CustomMinimumSize = new Vector2(0f, 56f) };
            var style = new StyleBoxFlat
            {
                BgColor = new Color(0.047f, 0.063f, 0.055f, 1f),
                BorderColor = new Color(0.133f, 0.200f, 0.188f, 1f),
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

            // Icon slot frame
            var slotFrame = new PanelContainer { CustomMinimumSize = new Vector2(44f, 44f) };
            var frameRect = new TextureRect
            {
                Texture = LedgerArt.SlotBronze ?? LedgerArt.SlotIron,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize
            };
            slotFrame.AddChild(frameRect);

            var iconRect = new TextureRect
            {
                Texture = LedgerArt.DaoMoon ?? LedgerArt.DaoHuman,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };
            slotFrame.AddChild(iconRect);
            hBox.AddChild(slotFrame);

            // Name, diet type, unit price
            var infoVBox = new VBoxContainer();
            infoVBox.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            hBox.AddChild(infoVBox);

            var lblName = new Label
            {
                Text = item.Name,
                Modulate = new Color(0.88f, 0.91f, 0.94f, 1f)
            };
            lblName.AddThemeFontSizeOverride("font_size", 12);
            infoVBox.AddChild(lblName);

            var lblDetails = new Label
            {
                Text = $"Diet Category: {item.DietType} | Value: ~{item.BasePriceStones} Stones/unit",
                Modulate = new Color(0.58f, 0.64f, 0.72f, 1f)
            };
            lblDetails.AddThemeFontSizeOverride("font_size", 10);
            infoVBox.AddChild(lblDetails);

            // Quantity display badge
            var qtyBadge = new Label
            {
                Text = $"x{item.Quantity}",
                Modulate = new Color(0.984f, 0.749f, 0.141f, 1f),
                HorizontalAlignment = HorizontalAlignment.Right,
                CustomMinimumSize = new Vector2(80f, 0f)
            };
            qtyBadge.AddThemeFontSizeOverride("font_size", 14);
            hBox.AddChild(qtyBadge);

            // Quick feed button
            var btnFeed = new Button
            {
                Text = "Feed 1x",
                CustomMinimumSize = new Vector2(80f, 30f),
                MouseFilter = MouseFilterEnum.Stop,
                Disabled = item.Quantity <= 0
            };
            btnFeed.Pressed += () => FeedGuRequested?.Invoke(string.Empty, item.DietType, 1);
            hBox.AddChild(btnFeed);

            return card;
        }
    }
}
