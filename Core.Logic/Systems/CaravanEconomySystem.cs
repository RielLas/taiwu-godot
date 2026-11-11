using System;
using System.Collections.Generic;
using TaiwuGu.Core;
using TaiwuGu.Entities;

namespace TaiwuGu.Systems
{
    /// <summary>
    /// Stateless static merchant system for the Jia Family Caravan (贾家商队).
    /// All mutable state lives on WorldStore.WorldEntity as a serializable session;
    /// stock restocks are deterministic from run seed, year, and solar term.
    /// </summary>
    public static class CaravanEconomySystem
    {
        private const int FodderBundleSize = 5;
        private const int UnlimitedStock = -1;

        private static readonly GuDietType[] FodderDiets =
        {
            GuDietType.MoonOrchidPetals,
            GuDietType.FreshBoarFlesh,
            GuDietType.VintageWine,
            GuDietType.IronOrePowder
        };

        private static readonly GuDietType[] GambleFodderPool =
        {
            GuDietType.MoonOrchidPetals,
            GuDietType.FreshBoarFlesh,
            GuDietType.VintageWine,
            GuDietType.IronOrePowder
        };

        private static readonly CaravanOreTier[] OreTiers =
        {
            CaravanOreTier.Low,
            CaravanOreTier.Mid,
            CaravanOreTier.High
        };

        // Common lots span rank 1-2. Autumn lots focus on rank 2-3 and only appear
        // during Bailu / Qiufen, which keeps the seasonal arrival meaningful.
        private static readonly string[] CommonGuPool =
        {
            "gu_moonlight_r1",
            "gu_little_light_r1",
            "gu_white_boar_r1",
            "gu_black_boar_r1",
            "gu_bronze_skin_r1",
            "gu_white_jade_r1",
            "gu_stealth_scales_r1",
            "gu_jumping_grass_r1",
            "gu_sweet_water_r1",
            "gu_liquor_worm_r1",
            "gu_tusita_flower_r1",
            "gu_breath_concealment_r1",
            "gu_nine_fragrances_bug_r1",
            "gu_moonglow_r2",
            "gu_moonscar_r2",
            "gu_steel_boar_r2",
            "gu_jade_skin_r2",
            "gu_iron_bone_r2"
        };

        private static readonly string[] AutumnGuPool =
        {
            "gu_moonglow_r2",
            "gu_moonscar_r2",
            "gu_golden_moon_r3",
            "gu_blood_moon_r3",
            "gu_steel_boar_r2",
            "gu_bear_strength_r2",
            "gu_brown_bear_innate_strength_r3",
            "gu_jade_skin_r2",
            "gu_iron_bone_r2",
            "gu_sky_canopy_r3",
            "gu_spiral_leaping_grass_r2",
            "gu_wind_pedal_r2",
            "gu_lightning_chaser_r3",
            "gu_heal_wound_r2",
            "gu_nine_leaf_vitality_grass_r2",
            "gu_undying_grass_r3",
            "gu_earth_hearing_ear_r2",
            "gu_subterranean_root_ear_r3",
            "gu_tusita_pouch_r2",
            "gu_four_liquors_worm_r2",
            "gu_seven_fragrances_liquor_worm_r3"
        };

        public static CaravanSessionComponent EnsureStock(WorldStore world)
        {
            var session = world.GetOrCreateSingleton<CaravanSessionComponent>();
            session.Stock ??= new List<CaravanStockEntry>();

            var clock = world.GetSingleton<WorldClockComponent>();
            if (clock == null) return session;

            int term = Math.Clamp(clock.CurrentSolarTermIndex, 1, WorldClockComponent.TERMS_PER_YEAR);
            if (session.HasStock && session.RestockYear == clock.CurrentYear && session.RestockTerm == term)
            {
                return session;
            }

            session.RestockYear = clock.CurrentYear;
            session.RestockTerm = term;
            session.StockSeed = unchecked(world.RunSeed * 397 ^ clock.CurrentYear * 7919 ^ term * 104729);
            session.Stock.Clear();

            var rng = new SeededRandom(session.StockSeed);
            AddFodderStock(session, rng);
            AddCommonGuStock(session, rng);
            if (IsAutumnArrival(term)) AddAutumnGuStock(session, rng);
            AddOreStock(session);

            session.HasStock = true;
            return session;
        }

        public static bool IsAutumnArrival(int solarTermIndex) =>
            solarTermIndex == (int)SolarTerm.Bailu_WhiteDew ||
            solarTermIndex == (int)SolarTerm.Qiufen_AutumnEquinox;

        public static bool TryBuyStock(WorldStore world, string stockId, out string message)
        {
            message = "The merchant has no such lot.";
            var session = EnsureStock(world);
            var entry = FindStock(session, stockId);
            if (entry == null) return false;

            if (entry.Kind == CaravanStockKind.RoughOre)
            {
                message = "Rough ore is cut at the stone table, not purchased directly.";
                return false;
            }

            if (IsTradeRefused(world))
            {
                message = "The Jia Consortium refuses to trade with a blood nemesis.";
                return false;
            }

            int quantity = GetStockBuyQuantity(entry);
            if (entry.Remaining >= 0 && entry.Remaining < quantity)
            {
                message = $"{entry.DisplayName} is sold out.";
                return false;
            }

            var cult = world.Get<CultivatorComponent>(world.PlayerEntity);
            var inv = world.Get<InventoryComponent>(world.PlayerEntity);
            var faction = world.GetFaction(FactionID.JiaConsortium);
            if (cult == null || inv == null || faction == null)
            {
                message = "The caravan cannot find your ledger.";
                return false;
            }

            int price = GetBuyPrice(world, entry, quantity);
            if (cult.PrimevalStones < price)
            {
                message = $"You need {price} primeval stones for {entry.DisplayName}.";
                return false;
            }

            GuWormComponent? gu = null;
            if (entry.Kind == CaravanStockKind.Gu)
            {
                gu = GuDatabase.CreateGuWormComponent(entry.GuId);
                if (gu == null)
                {
                    message = "The Gu catalog cannot appraise this lot.";
                    return false;
                }
            }

            cult.PrimevalStones -= price;
            if (entry.Kind == CaravanStockKind.Fodder)
            {
                inv.AddFood(entry.Diet, quantity);
            }
            else
            {
                WorldFactory.SpawnGuForOwner(world, world.PlayerEntity, gu!, isEquipped: false);
            }

            if (entry.Remaining >= 0) entry.Remaining -= quantity;
            faction.ModifyReputation(1);
            WorldSignals.EmitTopics(DirtyTopic.Inventory | DirtyTopic.Factions | DirtyTopic.Cultivator);
            message = $"Acquired {entry.DisplayName} for {price} stones.";
            return true;
        }

        public static bool TrySellGu(WorldStore world, EntityId guEntity, out string message)
        {
            message = "That Gu is not in your reserve.";

            if (IsTradeRefused(world))
            {
                message = "The Jia Consortium refuses to trade with a blood nemesis.";
                return false;
            }

            if (!world.IsAlive(guEntity) ||
                !world.TryGet<GuWormComponent>(guEntity, out var gu) ||
                !world.TryGet<OwnerComponent>(guEntity, out var owner) ||
                owner.OwnerEntityId != world.PlayerEntity.Value)
            {
                return false;
            }

            if (gu.IsVitalGu)
            {
                message = "A vital Gu cannot be sold.";
                return false;
            }

            if (owner.IsEquipped)
            {
                message = "Unequip the Gu before selling it.";
                return false;
            }

            var cult = world.Get<CultivatorComponent>(world.PlayerEntity);
            if (cult == null) return false;

            int price = GetGuSellPrice(world, gu);
            cult.PrimevalStones += price;
            message = $"Sold {gu.Name} for {price} stones.";
            world.DestroyEntity(guEntity);
            WorldSignals.EmitTopics(DirtyTopic.Inventory | DirtyTopic.Cultivator);
            return true;
        }

        public static bool TrySellFodder(WorldStore world, GuDietType diet, out string message)
        {
            message = "You have no such fodder to sell.";

            if (diet == GuDietType.None) return false;
            if (IsTradeRefused(world))
            {
                message = "The Jia Consortium refuses to trade with a blood nemesis.";
                return false;
            }

            var cult = world.Get<CultivatorComponent>(world.PlayerEntity);
            var inv = world.Get<InventoryComponent>(world.PlayerEntity);
            if (cult == null || inv == null) return false;

            int available = inv.GetFoodQuantity(diet);
            if (available <= 0) return false;

            int quantity = Math.Min(FodderBundleSize, available);
            int price = GetFodderSellPrice(world, diet, quantity);
            if (!inv.TryConsumeFood(diet, quantity))
            {
                message = "The fodder stores could not be opened.";
                return false;
            }

            cult.PrimevalStones += price;
            message = $"Sold {quantity} x {GetFodderName(diet)} for {price} stones.";
            WorldSignals.EmitTopics(DirtyTopic.Inventory | DirtyTopic.Cultivator);
            return true;
        }

        public static bool TryGamble(WorldStore world, CaravanOreTier tier, out CaravanGambleResult result)
        {
            result = RefusedResult("The stone table is closed.");
            var session = EnsureStock(world);
            var cult = world.Get<CultivatorComponent>(world.PlayerEntity);
            var faction = world.GetFaction(FactionID.JiaConsortium);
            if (cult == null || faction == null) return false;

            if (IsTradeRefused(world))
            {
                result = RefusedResult("The Jia Consortium refuses to trade with a blood nemesis.");
                return false;
            }

            int cost = GetOreCost(tier);
            if (cult.PrimevalStones < cost)
            {
                result = RefusedResult($"You need {cost} primeval stones to cut this ore.");
                return false;
            }

            cult.PrimevalStones -= cost;
            session.GambleSequence++;
            int seed = unchecked(session.StockSeed ^ (session.GambleSequence * 486187739) ^ ((int)tier * 97));
            var rng = new SeededRandom(seed);
            int roll = rng.Next(100);

            if (IsSlag(tier, roll))
            {
                faction.ModifyReputation(1);
                EmitTradeTopics();
                result = new CaravanGambleResult(
                    CaravanGambleOutcome.Slag,
                    "The rough ore splits open: dead slag (废石).",
                    0,
                    null,
                    GuDietType.None,
                    0);
                return true;
            }

            if (IsRebate(tier, roll))
            {
                int rebate = tier switch
                {
                    CaravanOreTier.Low => 10,
                    CaravanOreTier.Mid => 25,
                    _ => 80
                };
                cult.PrimevalStones += rebate;
                faction.ModifyReputation(1);
                EmitTradeTopics();
                result = new CaravanGambleResult(
                    CaravanGambleOutcome.StoneRebate,
                    $"A vein of primeval stone glimmers within: +{rebate} stones rebate.",
                    rebate,
                    null,
                    GuDietType.None,
                    0);
                return true;
            }

            if (IsFodderCache(tier, roll))
            {
                var diet = GambleFodderPool[rng.Next(GambleFodderPool.Length)];
                int quantity = rng.Next(5, 11);
                world.Get<InventoryComponent>(world.PlayerEntity)?.AddFood(diet, quantity);
                faction.ModifyReputation(1);
                EmitTradeTopics();
                result = new CaravanGambleResult(
                    CaravanGambleOutcome.FodderCache,
                    $"A sealed clay jar of {GetFodderName(diet)} yields {quantity} rations.",
                    0,
                    null,
                    diet,
                    quantity);
                return true;
            }

            string? fossilGuId = PickFossilGu(tier, rng);
            GuWormComponent? fossil = fossilGuId != null ? GuDatabase.CreateGuWormComponent(fossilGuId) : null;
            if (fossil != null)
            {
                fossil.IsInTraumaSleep = true;
                fossil.Satiety = 10f;
                WorldFactory.SpawnGuForOwner(world, world.PlayerEntity, fossil, isEquipped: false);
            }

            faction.ModifyReputation(1);
            EmitTradeTopics();
            result = new CaravanGambleResult(
                CaravanGambleOutcome.FossilGu,
                fossil != null
                    ? $"A dormant fossilized Gu sleeps within the stone: {fossil.Name}."
                    : "A fossilized Gu shell crumbles to dust before it can be claimed.",
                0,
                fossilGuId,
                GuDietType.None,
                0);
            return true;
        }

        public static void OpenBazaar(WorldStore world)
        {
            EnsureStock(world);
            WorldSignals.EmitCloseTilePOI();
            WorldSignals.EmitCaravanBazaarRequested();
        }

        public static CaravanStockEntry? FindStock(CaravanSessionComponent session, string stockId)
        {
            foreach (var entry in session.Stock)
            {
                if (string.Equals(entry.Id, stockId, StringComparison.Ordinal)) return entry;
            }
            return null;
        }

        public static int GetStockBuyQuantity(CaravanStockEntry entry) =>
            entry.Kind == CaravanStockKind.Fodder ? FodderBundleSize : 1;

        public static int GetBuyPrice(WorldStore world, CaravanStockEntry entry, int quantity)
        {
            if (entry == null || quantity <= 0) return 0;
            float total = entry.BaseUnitPrice * quantity * GetJiaPriceMultiplier(world);
            return Math.Max(1, (int)MathF.Ceiling(total));
        }

        public static int GetGuSellPrice(WorldStore world, GuWormComponent gu)
        {
            float total = GetGuBasePrice(gu.Rank) * 0.5f / GetJiaPriceMultiplier(world);
            return Math.Max(1, (int)MathF.Floor(total));
        }

        public static int GetFodderSellPrice(WorldStore world, GuDietType diet, int quantity)
        {
            if (quantity <= 0) return 0;
            float total = GetFodderBasePrice(diet) * quantity * 0.5f / GetJiaPriceMultiplier(world);
            return Math.Max(1, (int)MathF.Floor(total));
        }

        public static float GetJiaPriceMultiplier(WorldStore world) =>
            world.GetFaction(FactionID.JiaConsortium)?.GetPriceMultiplier() ?? 1f;

        public static bool IsTradeRefused(WorldStore world)
        {
            var faction = world.GetFaction(FactionID.JiaConsortium);
            return faction != null && faction.GetStandingTier() == FactionStandingTier.Nemesis;
        }

        public static int GetGuBasePrice(int rank) => rank switch
        {
            1 => 90,
            2 => 220,
            3 => 480,
            _ => Math.Max(90, 90 * rank)
        };

        public static int GetFodderBasePrice(GuDietType diet) => diet switch
        {
            GuDietType.MoonOrchidPetals => 2,
            GuDietType.FreshBoarFlesh => 2,
            GuDietType.VintageWine => 6,
            GuDietType.IronOrePowder => 4,
            GuDietType.WillowLeafDew => 3,
            GuDietType.WolfBlood => 5,
            GuDietType.SpringWater => 1,
            GuDietType.SpiritStonePowder => 8,
            _ => 1
        };

        public static string GetFodderName(GuDietType diet) => diet switch
        {
            GuDietType.MoonOrchidPetals => "Moon Orchid Petals (月兰花瓣)",
            GuDietType.FreshBoarFlesh => "Fresh Boar Flesh (新鲜野猪肉)",
            GuDietType.VintageWine => "Green Bamboo Wine (青竹老酒)",
            GuDietType.WillowLeafDew => "Willow Leaf Dew (柳叶露)",
            GuDietType.IronOrePowder => "Iron Ore Powder (铁精粉)",
            GuDietType.WolfBlood => "Wolf Blood (狼血)",
            GuDietType.SpringWater => "Spring Water (山泉)",
            GuDietType.SpiritStonePowder => "Spirit Stone Powder (灵石粉)",
            _ => "Unspecified Ration (无名口粮)"
        };

        public static int GetOreCost(CaravanOreTier tier) => tier switch
        {
            CaravanOreTier.Low => 25,
            CaravanOreTier.Mid => 70,
            _ => 160
        };

        public static string GetOreName(CaravanOreTier tier) => tier switch
        {
            CaravanOreTier.Low => "Rough Ore - Low Grade (粗石·下品)",
            CaravanOreTier.Mid => "Rough Ore - Mid Grade (粗石·中品)",
            _ => "Rough Ore - High Grade (粗石·上品)"
        };

        public static string GetOreOdds(CaravanOreTier tier) => tier switch
        {
            CaravanOreTier.Low => "55% slag • 25% rebate • 15% fodder • 5% fossil Gu",
            CaravanOreTier.Mid => "40% slag • 25% rebate • 20% fodder • 15% fossil Gu",
            _ => "25% slag • 25% rebate • 20% fodder • 30% fossil Gu"
        };

        public static string DescribeStock(CaravanStockEntry entry)
        {
            string seasonal = entry.IsSeasonal ? "Autumn arrival • " : string.Empty;
            return entry.Kind switch
            {
                CaravanStockKind.Fodder =>
                    $"{seasonal}Gu fodder • {entry.Remaining}/{entry.Quantity} rations • {GetFodderName(entry.Diet)}",
                CaravanStockKind.Gu =>
                    $"{seasonal}Rank {entry.Rank} Gu • diet: {GetFodderName(entry.Diet)} • stock {entry.Remaining}",
                CaravanStockKind.RoughOre =>
                    $"Rough ore • {GetOreOdds(entry.OreTier)}",
                _ => entry.DisplayName
            };
        }

        public static string GetNextAutumnRestockLabel(WorldStore world)
        {
            var clock = world.GetSingleton<WorldClockComponent>();
            if (clock == null) return "Autumn arrival: unknown";

            int term = Math.Clamp(clock.CurrentSolarTermIndex, 1, WorldClockComponent.TERMS_PER_YEAR);
            if (IsAutumnArrival(term))
            {
                return "Autumn caravan active — Bailu (白露) / Qiufen (秋分) seasonal lots present.";
            }

            int delta = term < (int)SolarTerm.Bailu_WhiteDew
                ? (int)SolarTerm.Bailu_WhiteDew - term
                : WorldClockComponent.TERMS_PER_YEAR - term + (int)SolarTerm.Bailu_WhiteDew;
            return $"Next autumn arrival: Bailu (白露) in {delta} solar term(s).";
        }

        private static void AddFodderStock(CaravanSessionComponent session, SeededRandom rng)
        {
            foreach (var diet in FodderDiets)
            {
                int quantity = 20 + rng.Next(0, 5) * 5;
                session.Stock.Add(new CaravanStockEntry
                {
                    Id = "fodder_" + (int)diet,
                    Kind = CaravanStockKind.Fodder,
                    Diet = diet,
                    DisplayName = GetFodderName(diet),
                    BaseUnitPrice = GetFodderBasePrice(diet),
                    Quantity = quantity,
                    Remaining = quantity
                });
            }
        }

        private static void AddCommonGuStock(CaravanSessionComponent session, SeededRandom rng)
        {
            var pool = new List<string>(CommonGuPool);
            for (int i = 0; i < 3 && pool.Count > 0; i++)
            {
                int index = rng.Next(pool.Count);
                AddGuStockEntry(session, pool[index], isSeasonal: false);
                pool.RemoveAt(index);
            }
        }

        private static void AddAutumnGuStock(CaravanSessionComponent session, SeededRandom rng)
        {
            var pool = new List<string>();
            foreach (var guId in AutumnGuPool)
            {
                if (!IsAlreadyStocked(session, guId)) pool.Add(guId);
            }

            for (int i = 0; i < 2 && pool.Count > 0; i++)
            {
                int index = rng.Next(pool.Count);
                AddGuStockEntry(session, pool[index], isSeasonal: true);
                pool.RemoveAt(index);
            }
        }

        private static bool IsAlreadyStocked(CaravanSessionComponent session, string guId)
        {
            foreach (var entry in session.Stock)
            {
                if (entry.Kind == CaravanStockKind.Gu &&
                    string.Equals(entry.GuId, guId, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static void AddGuStockEntry(CaravanSessionComponent session, string guId, bool isSeasonal)
        {
            var def = GuDatabase.Get(guId);
            if (def == null) return;

            string displayName = string.IsNullOrEmpty(def.ChineseName)
                ? def.CanonicalName
                : $"{def.CanonicalName} ({def.ChineseName})";
            var diet = def.Diet != null ? (GuDietType)def.Diet.DietType : GuDietType.None;

            session.Stock.Add(new CaravanStockEntry
            {
                Id = "gu_" + guId,
                Kind = CaravanStockKind.Gu,
                GuId = guId,
                Diet = diet,
                Rank = def.Rank,
                DisplayName = displayName,
                BaseUnitPrice = GetGuBasePrice(def.Rank),
                Quantity = 1,
                Remaining = 1,
                IsSeasonal = isSeasonal
            });
        }

        private static void AddOreStock(CaravanSessionComponent session)
        {
            foreach (var tier in OreTiers)
            {
                session.Stock.Add(new CaravanStockEntry
                {
                    Id = "ore_" + (int)tier,
                    Kind = CaravanStockKind.RoughOre,
                    DisplayName = GetOreName(tier),
                    BaseUnitPrice = GetOreCost(tier),
                    Quantity = UnlimitedStock,
                    Remaining = UnlimitedStock,
                    OreTier = tier
                });
            }
        }

        private static bool IsSlag(CaravanOreTier tier, int roll) => tier switch
        {
            CaravanOreTier.Low => roll < 55,
            CaravanOreTier.Mid => roll < 40,
            _ => roll < 25
        };

        private static bool IsRebate(CaravanOreTier tier, int roll) => tier switch
        {
            CaravanOreTier.Low => roll < 80,
            CaravanOreTier.Mid => roll < 65,
            _ => roll < 50
        };

        private static bool IsFodderCache(CaravanOreTier tier, int roll) => tier switch
        {
            CaravanOreTier.Low => roll < 95,
            CaravanOreTier.Mid => roll < 85,
            _ => roll < 70
        };

        private static string? PickFossilGu(CaravanOreTier tier, SeededRandom rng)
        {
            int rank = tier switch
            {
                CaravanOreTier.Low => 1,
                CaravanOreTier.Mid => 2,
                _ => 3
            };

            string[] pool = rank == 1 ? CommonGuPool : AutumnGuPool;
            var candidates = new List<string>();
            foreach (var guId in pool)
            {
                if (GuDatabase.Get(guId)?.Rank == rank) candidates.Add(guId);
            }

            if (candidates.Count == 0) return null;
            return candidates[rng.Next(candidates.Count)];
        }

        private static CaravanGambleResult RefusedResult(string message) =>
            new(CaravanGambleOutcome.Refused, message, 0, null, GuDietType.None, 0);

        private static void EmitTradeTopics() =>
            WorldSignals.EmitTopics(DirtyTopic.Inventory | DirtyTopic.Factions | DirtyTopic.Cultivator);
    }
}
