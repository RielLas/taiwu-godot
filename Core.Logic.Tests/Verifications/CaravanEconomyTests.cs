using System;
using System.IO;
using System.Linq;
using Xunit;
using TaiwuGu.Core;
using TaiwuGu.Entities;
using TaiwuGu.Serialization;
using TaiwuGu.Systems;

namespace TaiwuGu.Tests
{
    public class CaravanEconomyTests : IDisposable
    {
        private readonly string _originalRoot;
        private readonly string _testRoot;

        public CaravanEconomyTests()
        {
            WorldSignals.ClearAllSubscriptions();
            EnsureGuDatabase();

            _originalRoot = SavePaths.RootSaveDirectory;
            _testRoot = Path.Combine(Path.GetTempPath(), "taiwu_caravan_tests_" + Guid.NewGuid().ToString("N"));
            SavePaths.RootSaveDirectory = _testRoot;
        }

        public void Dispose()
        {
            SavePaths.RootSaveDirectory = _originalRoot;
            WorldSignals.ClearAllSubscriptions();
            try
            {
                if (Directory.Exists(_testRoot)) Directory.Delete(_testRoot, true);
            }
            catch { }
        }

        [Fact]
        public void EnsureStock_IsDeterministic_AndAutumnAddsSeasonalRankTwoOrThree()
        {
            var a = CreateWorld(777, 3, 4);
            var b = CreateWorld(777, 3, 4);

            var sa = CaravanEconomySystem.EnsureStock(a);
            var sb = CaravanEconomySystem.EnsureStock(b);

            Assert.Equal(sa.Stock.Count, sb.Stock.Count);
            for (int i = 0; i < sa.Stock.Count; i++)
            {
                Assert.Equal(sa.Stock[i].Id, sb.Stock[i].Id);
                Assert.Equal(sa.Stock[i].Kind, sb.Stock[i].Kind);
                Assert.Equal(sa.Stock[i].BaseUnitPrice, sb.Stock[i].BaseUnitPrice);
                Assert.Equal(sa.Stock[i].Remaining, sb.Stock[i].Remaining);
                Assert.Equal(sa.Stock[i].IsSeasonal, sb.Stock[i].IsSeasonal);
            }
            Assert.DoesNotContain(sa.Stock, e => e.IsSeasonal);

            var bailu = CreateWorld(777, 3, (int)SolarTerm.Bailu_WhiteDew);
            var sbailu = CaravanEconomySystem.EnsureStock(bailu);
            var autumnLots = sbailu.Stock.Where(e => e.IsSeasonal).ToList();
            Assert.Equal(2, autumnLots.Count);
            Assert.All(autumnLots, e => Assert.InRange(e.Rank, 2, 3));

            var qiufen = CreateWorld(777, 3, (int)SolarTerm.Qiufen_AutumnEquinox);
            var sqiufen = CaravanEconomySystem.EnsureStock(qiufen);
            Assert.Equal(2, sqiufen.Stock.Count(e => e.IsSeasonal));

            int oldSeed = sbailu.StockSeed;
            bailu.GetSingleton<WorldClockComponent>()!.CurrentSolarTermIndex = (int)SolarTerm.Qiufen_AutumnEquinox;
            var refreshed = CaravanEconomySystem.EnsureStock(bailu);

            Assert.NotEqual(oldSeed, refreshed.StockSeed);
            Assert.Equal((int)SolarTerm.Qiufen_AutumnEquinox, refreshed.RestockTerm);
            Assert.Equal(2, refreshed.Stock.Count(e => e.IsSeasonal));
        }

        [Fact]
        public void JiaPricing_AppliesTierMultipliers_AndRefusesNemesis()
        {
            var cases = new (int Reputation, float Multiplier)[]
            {
                (100, 0.90f),
                (-100, 1.50f),
                (200, 0.80f)
            };

            int index = 0;
            foreach (var (reputation, multiplier) in cases)
            {
                var world = CreateWorld(900 + index++, 1, 4);
                SetJiaReputation(world, reputation);
                var cult = world.Get<CultivatorComponent>(world.PlayerEntity)!;
                var inv = world.Get<InventoryComponent>(world.PlayerEntity)!;
                cult.PrimevalStones = 1000;

                var session = CaravanEconomySystem.EnsureStock(world);
                var fodder = session.Stock.First(e => e.Kind == CaravanStockKind.Fodder);
                int foodBefore = inv.GetFoodQuantity(fodder.Diet);
                int expectedCost = (int)MathF.Ceiling(fodder.BaseUnitPrice * 5 * multiplier);

                Assert.True(CaravanEconomySystem.TryBuyStock(world, fodder.Id, out string message), message);
                Assert.Equal(1000 - expectedCost, cult.PrimevalStones);
                Assert.Equal(foodBefore + 5, inv.GetFoodQuantity(fodder.Diet));
            }

            var nemesis = CreateWorld(950, 1, 4);
            SetJiaReputation(nemesis, -200);
            var nemesisCult = nemesis.Get<CultivatorComponent>(nemesis.PlayerEntity)!;
            var nemesisInv = nemesis.Get<InventoryComponent>(nemesis.PlayerEntity)!;
            nemesisCult.PrimevalStones = 1000;
            var nemesisSession = CaravanEconomySystem.EnsureStock(nemesis);
            var nemesisFodder = nemesisSession.Stock.First(e => e.Kind == CaravanStockKind.Fodder);
            int nemesisFood = nemesisInv.GetFoodQuantity(nemesisFodder.Diet);

            Assert.False(CaravanEconomySystem.TryBuyStock(nemesis, nemesisFodder.Id, out string refusal));
            Assert.False(string.IsNullOrWhiteSpace(refusal));
            Assert.Equal(1000, nemesisCult.PrimevalStones);
            Assert.Equal(nemesisFood, nemesisInv.GetFoodQuantity(nemesisFodder.Diet));
        }

        [Fact]
        public void BuyingFodderAndGu_MutatesStonesInventoryAndOwnership_ThenSellBackWorks()
        {
            var world = CreateWorld(111, 2, 4);
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity)!;
            var inv = world.Get<InventoryComponent>(world.PlayerEntity)!;
            cult.PrimevalStones = 1000;

            var session = CaravanEconomySystem.EnsureStock(world);
            var fodder = session.Stock.First(e => e.Kind == CaravanStockKind.Fodder);
            int fodderCost = CaravanEconomySystem.GetBuyPrice(world, fodder, 5);
            int foodBefore = inv.GetFoodQuantity(fodder.Diet);

            Assert.True(CaravanEconomySystem.TryBuyStock(world, fodder.Id, out string fodderMessage), fodderMessage);
            Assert.Equal(foodBefore + 5, inv.GetFoodQuantity(fodder.Diet));
            Assert.Equal(1000 - fodderCost, cult.PrimevalStones);

            var guEntry = session.Stock.First(e => e.Kind == CaravanStockKind.Gu);
            int guCost = CaravanEconomySystem.GetBuyPrice(world, guEntry, 1);
            int ownedBefore = world.GetOwned<GuWormComponent>(world.PlayerEntity).Count();

            Assert.True(CaravanEconomySystem.TryBuyStock(world, guEntry.Id, out string guMessage), guMessage);
            Assert.Equal(ownedBefore + 1, world.GetOwned<GuWormComponent>(world.PlayerEntity).Count());
            Assert.Equal(0, guEntry.Remaining);
            Assert.Equal(1000 - fodderCost - guCost, cult.PrimevalStones);

            EntityId boughtGu = FindOwnedGu(world, guEntry.GuId);
            Assert.NotEqual(EntityId.None, boughtGu);
            var boughtComponent = world.Get<GuWormComponent>(boughtGu)!;
            int guSellPrice = CaravanEconomySystem.GetGuSellPrice(world, boughtComponent);

            Assert.True(CaravanEconomySystem.TrySellGu(world, boughtGu, out string sellMessage), sellMessage);
            Assert.False(world.IsAlive(boughtGu));
            Assert.Equal(ownedBefore, world.GetOwned<GuWormComponent>(world.PlayerEntity).Count());
            Assert.Equal(1000 - fodderCost - guCost + guSellPrice, cult.PrimevalStones);

            int foodBeforeSell = inv.GetFoodQuantity(fodder.Diet);
            int fodderSellPrice = CaravanEconomySystem.GetFodderSellPrice(world, fodder.Diet, 5);
            Assert.True(CaravanEconomySystem.TrySellFodder(world, fodder.Diet, out string fodderSellMessage), fodderSellMessage);
            Assert.Equal(foodBeforeSell - 5, inv.GetFoodQuantity(fodder.Diet));
            Assert.Equal(1000 - fodderCost - guCost + guSellPrice + fodderSellPrice, cult.PrimevalStones);
        }

        [Fact]
        public void StoneGambling_IsDeterministic_AndDeductsStones()
        {
            var tiers = new[] { CaravanOreTier.Low, CaravanOreTier.Mid, CaravanOreTier.High };
            foreach (var tier in tiers)
            {
                var a = CreateWorld(42, 2, 4);
                var b = CreateWorld(42, 2, 4);
                var cultA = a.Get<CultivatorComponent>(a.PlayerEntity)!;
                var cultB = b.Get<CultivatorComponent>(b.PlayerEntity)!;
                cultA.PrimevalStones = 1000;
                cultB.PrimevalStones = 1000;

                var sessionA = CaravanEconomySystem.EnsureStock(a);
                CaravanEconomySystem.EnsureStock(b);
                int before = cultA.PrimevalStones;
                int cost = CaravanEconomySystem.GetOreCost(tier);

                Assert.True(CaravanEconomySystem.TryGamble(a, tier, out var resultA), resultA.Message);
                Assert.True(CaravanEconomySystem.TryGamble(b, tier, out var resultB), resultB.Message);

                Assert.Equal(resultA.Outcome, resultB.Outcome);
                Assert.Equal(resultA.StonesAwarded, resultB.StonesAwarded);
                Assert.Equal(resultA.GuId, resultB.GuId);
                Assert.Equal(resultA.FodderDiet, resultB.FodderDiet);
                Assert.Equal(resultA.FodderQuantity, resultB.FodderQuantity);
                Assert.Equal(before - cost + resultA.StonesAwarded, cultA.PrimevalStones);
                Assert.Equal(1, sessionA.GambleSequence);
                Assert.False(string.IsNullOrWhiteSpace(resultA.Message));

                if (resultA.Outcome == CaravanGambleOutcome.FossilGu && resultA.GuId != null)
                {
                    Assert.NotEqual(EntityId.None, FindOwnedGu(a, resultA.GuId));
                }
            }
        }

        [Fact]
        public void SaveLoadV3_PreservesCaravanStockAndSequence()
        {
            var world = CreateWorld(31415, 4, (int)SolarTerm.Bailu_WhiteDew);
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity)!;
            cult.PrimevalStones = 1000;

            var session = CaravanEconomySystem.EnsureStock(world);
            var fodder = session.Stock.First(e => e.Kind == CaravanStockKind.Fodder);
            Assert.True(CaravanEconomySystem.TryBuyStock(world, fodder.Id, out string buyMessage), buyMessage);
            Assert.True(CaravanEconomySystem.TryGamble(world, CaravanOreTier.Low, out var gambleResult), gambleResult.Message);

            Assert.True(SaveDiskIO.SaveWorld("caravan_round_trip", world));
            var loaded = SaveDiskIO.LoadWorld("caravan_round_trip");
            Assert.NotNull(loaded);

            var loadedSession = loaded!.GetSingleton<CaravanSessionComponent>();
            Assert.NotNull(loadedSession);
            Assert.Equal(session.RestockYear, loadedSession!.RestockYear);
            Assert.Equal(session.RestockTerm, loadedSession.RestockTerm);
            Assert.Equal(session.StockSeed, loadedSession.StockSeed);
            Assert.Equal(session.GambleSequence, loadedSession.GambleSequence);
            Assert.Equal(session.Stock.Count, loadedSession.Stock.Count);
            Assert.Equal(session.Stock.Select(e => e.Id), loadedSession.Stock.Select(e => e.Id));

            var loadedFodder = loadedSession.Stock.Single(e => e.Id == fodder.Id);
            Assert.Equal(fodder.Remaining, loadedFodder.Remaining);
        }

        private static WorldStore CreateWorld(int seed, int year, int term)
        {
            var world = WorldFactory.CreateDefault(runSeed: seed);
            var clock = world.GetSingleton<WorldClockComponent>()!;
            clock.CurrentYear = year;
            clock.CurrentSolarTermIndex = term;
            return world;
        }

        private static void SetJiaReputation(WorldStore world, int reputation)
        {
            var faction = world.GetFaction(FactionID.JiaConsortium);
            Assert.NotNull(faction);
            faction!.ReputationScore = reputation;
        }

        private static EntityId FindOwnedGu(WorldStore world, string guId)
        {
            foreach (var (eid, gu) in world.GetOwned<GuWormComponent>(world.PlayerEntity))
            {
                if (string.Equals(gu.GuID, guId, StringComparison.Ordinal)) return eid;
            }
            return EntityId.None;
        }

        private static void EnsureGuDatabase()
        {
            if (GuDatabase.IsLoaded) return;

            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "Resources", "Data", "gu_worms_database.json");
                if (File.Exists(candidate))
                {
                    Assert.True(GuDatabase.Initialize(File.ReadAllText(candidate)));
                    return;
                }
                dir = dir.Parent;
            }

            throw new InvalidOperationException("Unable to locate Resources/Data/gu_worms_database.json from the test output directory.");
        }
    }
}
