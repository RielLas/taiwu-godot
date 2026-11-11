using System;
using Godot;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Systems;
using Color = Godot.Color;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>
    /// Scene-first Jia Family Caravan bazaar. The scene owns every page and label;
    /// this controller only binds live WorldStore values, instantiates lot cards,
    /// and delegates all market rules to CaravanEconomySystem.
    /// </summary>
    public partial class CaravanBazaarModalView : Control, IModalView
    {
        private const string ModalTitle = "◆ Jia Family Caravan Grand Bazaar (贾家商队大集) ◆";

        [Export] public TabbedModal? Tabs { get; set; }
        [Export] public Control? HeaderInfo { get; set; }
        [Export] public Control? BuyPage { get; set; }
        [Export] public Control? SellPage { get; set; }
        [Export] public Control? GamblePage { get; set; }
        [Export] public PackedScene? StockCardScene { get; set; }

        private IWorldStateProvider? _provider;
        private TaiwuModalStack? _modalStack;
        private TabbedModal? _tabs;
        private ModalScaffold? _scaffold;
        private bool _isOpen;

        private Label? _lblStanding;
        private Label? _lblStones;
        private Label? _lblRestock;
        private Label? _lblFeedback;
        private VBoxContainer? _buyList;
        private VBoxContainer? _sellList;
        private VBoxContainer? _gambleList;
        private Label? _lblGambleResult;

        public bool IsOpen => _isOpen;

        public void Initialize(TaiwuModalStack modalStack, IWorldStateProvider provider)
        {
            _modalStack = modalStack;
            _provider = provider;
            _scaffold?.BindModalStack(modalStack);
        }

        public void Dismiss() => CloseBazaar();

        public override void _Ready()
        {
            ProcessMode = ProcessModeEnum.Always;
            SetAnchorsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Ignore;
            Visible = false;

            _tabs = Tabs;
            if (_tabs == null)
            {
                GD.PushError("[CaravanBazaarModalView] Tabs export is missing.");
                return;
            }

            _scaffold = _tabs.Scaffold;
            if (_scaffold == null)
            {
                GD.PushError("[CaravanBazaarModalView] TabbedModal is missing its ModalScaffold.");
                return;
            }

            _scaffold.BindModalStack(_modalStack);
            if (_scaffold.Panel != null) _scaffold.Panel.CustomMinimumSize = new Vector2(900f, 610f);

            if (HeaderInfo != null && _tabs.HeaderRows != null && HeaderInfo.GetParent() != _tabs.HeaderRows)
            {
                if (HeaderInfo.GetParent() != null) HeaderInfo.Reparent(_tabs.HeaderRows);
                else _tabs.HeaderRows.AddChild(HeaderInfo);
            }

            _tabs.AddTab("Buy Supplies & Gu (采买)", BuyPage);
            _tabs.AddTab("Sell Reserve (售出)", SellPage);
            _tabs.AddTab("Stone Gambling 赌石", GamblePage);

            CacheSceneNodes();

            if (StockCardScene == null)
            {
                GD.PushError("[CaravanBazaarModalView] CaravanStockCard.tscn export is missing.");
            }

            WorldSignals.OnCaravanBazaarRequested += HandleBazaarRequested;
            WorldSignals.OnBattleStageChanged += HandleBattleStageChanged;
        }

        public override void _ExitTree()
        {
            WorldSignals.OnCaravanBazaarRequested -= HandleBazaarRequested;
            WorldSignals.OnBattleStageChanged -= HandleBattleStageChanged;

            ClearList(_buyList);
            ClearList(_sellList);
            ClearList(_gambleList);

            if (_isOpen)
            {
                _isOpen = false;
                Visible = false;
                _scaffold?.Close();
            }
        }

        private void CacheSceneNodes()
        {
            if (HeaderInfo != null)
            {
                _lblStanding = HeaderInfo.GetNodeOrNull<Label>("Standing");
                _lblStones = HeaderInfo.GetNodeOrNull<Label>("Stones");
                _lblRestock = HeaderInfo.GetNodeOrNull<Label>("Restock");
                _lblFeedback = HeaderInfo.GetNodeOrNull<Label>("Feedback");
            }

            _buyList = BuyPage?.GetNodeOrNull<VBoxContainer>("Scroll/StockList");
            _sellList = SellPage?.GetNodeOrNull<VBoxContainer>("Scroll/StockList");
            _gambleList = GamblePage?.GetNodeOrNull<VBoxContainer>("Scroll/StockList");
            _lblGambleResult = GamblePage?.GetNodeOrNull<Label>("Result");
        }

        private void HandleBazaarRequested() => OpenBazaar();

        private void HandleBattleStageChanged(bool inBattle)
        {
            if (inBattle && _isOpen) CloseBazaar();
        }

        private void OpenBazaar()
        {
            if (_isOpen || _scaffold == null || _tabs == null || _provider == null) return;

            var world = _provider.Current;
            CaravanEconomySystem.EnsureStock(world);

            _isOpen = true;
            Visible = true;
            _scaffold.BindModalStack(_modalStack);
            _scaffold.Open(this, ModalTitle);
            _tabs.SelectTab(0);

            if (_lblFeedback != null) _lblFeedback.Text = "The caravan's ledgers are open.";
            if (_lblGambleResult != null) _lblGambleResult.Text = "The stone saw waits.";
            RefreshAll();
        }

        private void CloseBazaar()
        {
            if (!_isOpen) return;

            _isOpen = false;
            Visible = false;
            _scaffold?.Close();
        }

        private void RefreshAll()
        {
            if (_provider?.Current is not { } world) return;

            var session = CaravanEconomySystem.EnsureStock(world);
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity);
            var faction = world.GetFaction(FactionID.JiaConsortium);
            int stones = cult?.PrimevalStones ?? 0;

            if (_lblStanding != null)
            {
                string standing = faction?.GetStandingTitle() ?? "No Jia representative";
                string multiplier = faction != null && faction.GetStandingTier() == FactionStandingTier.Nemesis
                    ? "TRADE REFUSED"
                    : $"x{faction?.GetPriceMultiplier() ?? 1f:0.00}";
                _lblStanding.Text = $"【贾】 Jia Standing: {standing}  [{multiplier}]";
            }

            if (_lblStones != null) _lblStones.Text = $"Primeval Stones: {stones} ◆";
            if (_lblRestock != null) _lblRestock.Text = CaravanEconomySystem.GetNextAutumnRestockLabel(world);

            PopulateBuyList(world, session, stones);
            PopulateSellList(world);
            PopulateGambleList(world, session, stones);
        }

        private void PopulateBuyList(WorldStore world, CaravanSessionComponent session, int stones)
        {
            ClearList(_buyList);
            bool refused = CaravanEconomySystem.IsTradeRefused(world);

            foreach (var entry in session.Stock)
            {
                if (entry.Kind == CaravanStockKind.RoughOre) continue;

                int quantity = CaravanEconomySystem.GetStockBuyQuantity(entry);
                int price = CaravanEconomySystem.GetBuyPrice(world, entry, quantity);
                bool soldOut = entry.Remaining == 0;
                bool enabled = !refused && !soldOut && stones >= price;
                string action = entry.Kind == CaravanStockKind.Fodder ? "Buy x5" : "Acquire";
                AddCard(_buyList, "buy:" + entry.Id, entry.DisplayName, CaravanEconomySystem.DescribeStock(entry),
                    $"◆ {price} Stones", action, enabled);
            }
        }

        private void PopulateSellList(WorldStore world)
        {
            ClearList(_sellList);
            bool refused = CaravanEconomySystem.IsTradeRefused(world);

            foreach (var (eid, gu) in world.GetOwned<GuWormComponent>(world.PlayerEntity))
            {
                if (gu.IsVitalGu) continue;

                var owner = world.Get<OwnerComponent>(eid);
                if (owner == null || owner.IsEquipped) continue;

                int price = CaravanEconomySystem.GetGuSellPrice(world, gu);
                AddCard(_sellList, "gu:" + eid.Value, gu.Name,
                    $"Rank {gu.Rank} reserve • buyback {price} stones", $"+{price} Stones", "Sell", !refused);
            }

            var inv = world.Get<InventoryComponent>(world.PlayerEntity);
            if (inv != null)
            {
                foreach (var item in inv.FoodStockpile)
                {
                    if (item.Quantity <= 0 || item.DietType == GuDietType.None) continue;

                    int quantity = Math.Min(5, item.Quantity);
                    int price = CaravanEconomySystem.GetFodderSellPrice(world, item.DietType, quantity);
                    string action = quantity >= 5 ? "Sell x5" : $"Sell x{quantity}";
                    AddCard(_sellList, "food:" + (int)item.DietType,
                        CaravanEconomySystem.GetFodderName(item.DietType),
                        $"Reserve x{item.Quantity} • buyback {price} stones", $"+{price} Stones", action, !refused);
                }
            }

        }

        private void PopulateGambleList(WorldStore world, CaravanSessionComponent session, int stones)
        {
            ClearList(_gambleList);
            bool refused = CaravanEconomySystem.IsTradeRefused(world);

            foreach (var entry in session.Stock)
            {
                if (entry.Kind != CaravanStockKind.RoughOre) continue;

                int cost = CaravanEconomySystem.GetOreCost(entry.OreTier);
                bool enabled = !refused && stones >= cost;
                AddCard(_gambleList, "ore:" + (int)entry.OreTier, entry.DisplayName,
                    CaravanEconomySystem.GetOreOdds(entry.OreTier), $"◆ {cost} Stones", "Gamble 赌石", enabled);
            }
        }

        private void AddCard(VBoxContainer? list, string key, string title, string detail, string price,
            string actionText, bool enabled)
        {
            if (list == null || StockCardScene == null) return;

            var card = StockCardScene.Instantiate<CaravanStockCardView>();
            if (card == null) return;

            list.AddChild(card);
            card.Bind(key, title, detail, price, actionText, enabled);
            card.ActionRequested += HandleCardAction;
        }

        private void ClearList(VBoxContainer? list)
        {
            if (list == null) return;

            foreach (Node child in list.GetChildren())
            {
                if (child is CaravanStockCardView stockCard)
                {
                    stockCard.ActionRequested -= HandleCardAction;
                }
                list.RemoveChild(child);
                child.QueueFree();
            }
        }

        private void HandleCardAction(string key)
        {
            if (_provider?.Current is not { } world) return;

            int separator = key.IndexOf(':');
            if (separator <= 0) return;

            string kind = key[..separator];
            string payload = key[(separator + 1)..];
            string message = "The lot could not be resolved.";

            switch (kind)
            {
                case "buy":
                    CaravanEconomySystem.TryBuyStock(world, payload, out message);
                    break;

                case "gu":
                    if (ulong.TryParse(payload, out ulong rawId))
                    {
                        CaravanEconomySystem.TrySellGu(world, new EntityId(rawId), out message);
                    }
                    break;

                case "food":
                    if (int.TryParse(payload, out int dietValue))
                    {
                        CaravanEconomySystem.TrySellFodder(world, (GuDietType)dietValue, out message);
                    }
                    break;

                case "ore":
                    if (int.TryParse(payload, out int tierValue))
                    {
                        CaravanEconomySystem.TryGamble(world, (CaravanOreTier)tierValue, out var result);
                        message = result.Message;
                        if (_lblGambleResult != null) _lblGambleResult.Text = result.Message;
                    }
                    break;
            }

            if (_lblFeedback != null) _lblFeedback.Text = message;
            RefreshAll();
        }
    }
}
