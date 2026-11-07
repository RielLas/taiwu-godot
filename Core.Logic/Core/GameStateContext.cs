using System;
using System.Collections.Generic;
using TaiwuGu.Map;
using TaiwuGu.Psychology;

namespace TaiwuGu.Core
{
    [Serializable]
    public class CultivatorState
    {
        public string Name = "Gu Yue Fang Yuan";
        public AptitudeGrade Aptitude = AptitudeGrade.C_Grade;
        public float AptitudePercent = 44.0f; // Canon C-Grade 44%

        public int Rank = 1;                  // Rank 1-5 Mortal Gu Master
        public int Substage = 1;              // 1: Initial, 2: Middle, 3: Upper, 4: Peak

        public float CurrentEssenceSea = 44.0f; // Current Primeval Sea level (%)
        public int PrimevalStones = 250;        // Universal cultivation currency
        public float CurrentNurtureProgress = 0f;

        // --- Reverend Insanity Aperture Wall & Membrane Attributes ---
        public float WallWashingProgress = 0f;        // [0% - 100%] Substage wall nourishment progress
        public float ApertureWallDurability = 100f;    // Current wall integrity during major breakthrough
        public float MaxWallDurability = 100f;        // Maximum wall integrity for current Rank
        public int HumanPathDaoMarks = 4;             // Innate Human Path marks

        public DaoMarksLedger DaoMarks = new DaoMarksLedger();
        public float MaxHealth = 100f;
        public float CurrentHealth = 100f;

        // --- Reverend Insanity Inviolate Lifespan Law (寿元天则) ---
        public int Age = 15;
        public int LifespanYears = 100;
        public int LifespanGuConsumed = 0;
        public bool IsAlive => Age < LifespanYears;
        public int RemainingLifespanYears => Mathf.Max(0, LifespanYears - Age);

        public void ConsumeLifespanGu(int years)
        {
            LifespanYears += years;
            LifespanGuConsumed += years;
        }

        public void AgeOneYear()
        {
            Age++;
        }

        public static int DeriveInnateHumanPathMarks(float aptitudePercent)
        {
            if (aptitudePercent >= 100f)
                return 75; // Ten Extreme Physiques canon range (50-100)
            
            int marks = Mathf.Clamp(Mathf.FloorToInt(aptitudePercent / 10f) - 1, 1, 9);
            return marks;
        }

        public static float GetMaxWallDurabilityForRank(int rank)
        {
            return rank switch
            {
                1 => 100f,  // Rank 1: Light Membrane (光膜)
                2 => 250f,  // Rank 2: Water Membrane (水膜)
                3 => 500f,  // Rank 3: Stone Membrane (石膜)
                4 => 1000f, // Rank 4: Crystal Membrane (晶膜)
                5 => 2000f, // Rank 5: Illusory Membrane (幻膜)
                _ => 100f * rank
            };
        }

        public string GetMembraneTypeName()
        {
            return Rank switch
            {
                1 => "Light Membrane (光膜)",
                2 => "Water Membrane (水膜)",
                3 => "Stone Membrane (石膜)",
                4 => "Crystal Membrane (晶膜)",
                5 => "Illusory Membrane (幻膜)",
                _ => "Primeval Membrane"
            };
        }

        public Color GetPrimevalSeaColor()
        {
            return Rank switch
            {
                1 => new Color(0.13f, 0.77f, 0.37f, 1f), // Copper Green (青铜) #22c55e
                2 => new Color(0.94f, 0.27f, 0.27f, 1f), // Iron Red (赤铁) #ef4444
                3 => new Color(0.95f, 0.96f, 0.98f, 1f), // Silver White (白银) #f1f5f9
                4 => new Color(0.92f, 0.70f, 0.03f, 1f), // Gold Yellow (黄金) #eab308
                5 => new Color(0.66f, 0.33f, 0.97f, 1f), // Crystal Purple (紫晶) #a855f7
                _ => new Color(0.13f, 0.77f, 0.37f, 1f)
            };
        }

        public double GetDensityCoefficient()
        {
            return Math.Pow(80.0, Rank - 1) * Math.Pow(2.0, Substage - 1);
        }

        public string GetSubstageName()
        {
            return Substage switch
            {
                1 => "Initial Stage (初阶)",
                2 => "Middle Stage (中阶)",
                3 => "Upper Stage (高阶)",
                4 => "Peak Stage (巅峰)",
                _ => "Initial Stage"
            };
        }

        public string GetRankDescription()
        {
            return $"Rank {Rank} {GetSubstageName()}";
        }

        public float CalculateStoneRecoveryPercentage()
        {
            double density = GetDensityCoefficient();
            double deltaPct = (CultivatorData.BASE_STONE_ESSENCE / (AptitudePercent * density)) * 100.0;
            return (float)deltaPct;
        }

        public CultivatorData ToCultivatorData()
        {
            return new CultivatorData(Name, Aptitude, AptitudePercent)
            {
                Rank = Rank,
                Substage = Substage,
                CurrentEssenceSea = CurrentEssenceSea,
                PrimevalStones = PrimevalStones,
                CurrentNurtureProgress = CurrentNurtureProgress,
                WallWashingProgress = WallWashingProgress,
                ApertureWallDurability = ApertureWallDurability,
                MaxWallDurability = MaxWallDurability > 0 ? MaxWallDurability : GetMaxWallDurabilityForRank(Rank),
                HumanPathDaoMarks = HumanPathDaoMarks > 0 ? HumanPathDaoMarks : DeriveInnateHumanPathMarks(AptitudePercent),
                MaxHealth = MaxHealth,
                CurrentHealth = CurrentHealth
            };
        }

        public void SyncFromCultivatorData(CultivatorData data)
        {
            if (data == null) return;
            Name = data.Name;
            Aptitude = data.Aptitude;
            AptitudePercent = data.AptitudePercent;
            Rank = data.Rank;
            Substage = data.Substage;
            CurrentEssenceSea = data.CurrentEssenceSea;
            PrimevalStones = data.PrimevalStones;
            CurrentNurtureProgress = data.CurrentNurtureProgress;
            WallWashingProgress = data.WallWashingProgress;
            ApertureWallDurability = data.ApertureWallDurability > 0 ? data.ApertureWallDurability : GetMaxWallDurabilityForRank(Rank);
            MaxWallDurability = data.MaxWallDurability > 0 ? data.MaxWallDurability : GetMaxWallDurabilityForRank(Rank);
            HumanPathDaoMarks = data.HumanPathDaoMarks > 0 ? data.HumanPathDaoMarks : DeriveInnateHumanPathMarks(AptitudePercent);
            DaoMarks.HumanPathMarks = HumanPathDaoMarks;
            MaxHealth = data.MaxHealth;
            CurrentHealth = data.CurrentHealth;
        }
    }

    [Serializable]
    public class GuInventoryState
    {
        public List<GuWormData> EquippedGu = new List<GuWormData>();
        public List<GuWormData> ReserveGu = new List<GuWormData>();
        public List<InventoryItem> FoodStockpile = new List<InventoryItem>();

        public void AddFood(InventoryItem item)
        {
            var existing = FoodStockpile.Find(i => i.ItemID == item.ItemID);
            if (existing != null)
            {
                existing.Quantity += item.Quantity;
            }
            else
            {
                FoodStockpile.Add(item);
            }
        }

        public void AddFood(GuDietType diet, int quantity)
        {
            var existing = FoodStockpile.Find(i => i.DietType == diet);
            if (existing != null)
            {
                existing.Quantity += quantity;
            }
            else
            {
                string id = "food_" + diet.ToString().ToLower();
                string name = diet.ToString();
                FoodStockpile.Add(new InventoryItem(id, name, diet, quantity, 1));
            }
        }

        public bool TryConsumeFood(GuDietType diet, int quantity = 1)
        {
            var item = FoodStockpile.Find(i => i.DietType == diet && i.Quantity >= quantity);
            if (item != null)
            {
                item.Quantity -= quantity;
                if (item.Quantity <= 0)
                {
                    FoodStockpile.Remove(item);
                }
                return true;
            }
            return false;
        }

        public int GetFoodQuantity(GuDietType diet)
        {
            int total = 0;
            foreach (var item in FoodStockpile)
            {
                if (item.DietType == diet) total += item.Quantity;
            }
            return total;
        }

        public GuWormData? FindGu(string guId)
        {
            var gu = EquippedGu.Find(g => g.GuID == guId);
            if (gu != null) return gu;
            return ReserveGu.Find(g => g.GuID == guId);
        }
    }

    [Serializable]
    public class WorldCalendarState
    {
        public const int BASE_MONTHLY_AP = 30;
        public const int TERMS_PER_YEAR = 24;

        public int CurrentYear = 1;
        public int CurrentSolarTermIndex = 4; // Spring Equinox (Chunfen)
        public int ActionPointsRemaining = BASE_MONTHLY_AP;

        public int RealmBonusAP = 0;
        public int SeasonalBonusAP = 0;
        public int FatiguePenaltyAP = 0;

        public int MonthlyAPBudget => Math.Max(10, BASE_MONTHLY_AP + RealmBonusAP + SeasonalBonusAP - FatiguePenaltyAP);
        public SolarTerm ActiveSolarTerm => (SolarTerm)Mathf.Clamp(CurrentSolarTermIndex, 1, TERMS_PER_YEAR);

        public string GetSolarTermName() => ActiveSolarTerm switch
        {
            SolarTerm.Lichun_BeginningOfSpring => "Beginning of Spring (立春)",
            SolarTerm.Yushui_RainWater => "Rain Water (雨水)",
            SolarTerm.Jingzhe_AwakeningOfInsects => "Awakening of Insects (惊蛰)",
            SolarTerm.Chunfen_SpringEquinox => "Spring Equinox (春分)",
            SolarTerm.Qingming_PureBrightness => "Pure Brightness (清明)",
            SolarTerm.Guyu_GrainRain => "Grain Rain (谷雨)",
            SolarTerm.Lixia_BeginningOfSummer => "Beginning of Summer (立夏)",
            SolarTerm.Xiaoman_GrainBuds => "Grain Buds (小满)",
            SolarTerm.Mangzhong_GrainInEar => "Grain in Ear (芒种)",
            SolarTerm.Xiazhi_SummerSolstice => "Summer Solstice (夏至)",
            SolarTerm.Xiaoshu_MinorHeat => "Minor Heat (小暑)",
            SolarTerm.Dashu_MajorHeat => "Major Heat (大暑)",
            SolarTerm.Liqiu_BeginningOfAutumn => "Beginning of Autumn (立秋)",
            SolarTerm.Chushu_EndOfHeat => "End of Heat (处暑)",
            SolarTerm.Bailu_WhiteDew => "White Dew (白露)",
            SolarTerm.Qiufen_AutumnEquinox => "Autumn Equinox (秋分)",
            SolarTerm.Hanlu_ColdDew => "Cold Dew (寒露)",
            SolarTerm.Shuangjiang_FrostDescent => "Frost's Descent (霜降)",
            SolarTerm.Lidong_BeginningOfWinter => "Beginning of Winter (立冬)",
            SolarTerm.Xiaoxue_MinorSnow => "Minor Snow (小雪)",
            SolarTerm.Daxue_MajorSnow => "Major Snow (大雪)",
            SolarTerm.Dongzhi_WinterSolstice => "Winter Solstice (冬至)",
            SolarTerm.Xiaohan_MinorCold => "Minor Cold (小寒)",
            SolarTerm.Dahan_MajorCold => "Major Cold (大寒)",
            _ => "Spring Equinox (春分)"
        };

        public string GetSeasonName()
        {
            int index = CurrentSolarTermIndex;
            if (index >= 1 && index <= 6) return "Spring (春)";
            if (index >= 7 && index <= 12) return "Summer (夏)";
            if (index >= 13 && index <= 18) return "Autumn (秋)";
            return "Winter (冬)";
        }

        public float GetSeasonalPathAmplification(DaoPathType path)
        {
            int index = CurrentSolarTermIndex;
            if (index >= 1 && index <= 6)
            {
                if (path == DaoPathType.EnslavementPath) return 1.15f;
            }
            else if (index >= 7 && index <= 12)
            {
                if (path == DaoPathType.RefinementPath) return 1.15f;
            }
            else if (index >= 13 && index <= 18)
            {
                if (path == DaoPathType.MoonPath) return 1.15f;
            }
            else
            {
                if (path == DaoPathType.BloodPath) return 1.15f;
            }

            return 1.0f;
        }

        public string GetSeasonalAffinityDescription()
        {
            int index = CurrentSolarTermIndex;
            if (index >= 1 && index <= 6) return "Spring: Enslavement Path +15%";
            if (index >= 7 && index <= 12) return "Summer: Refinement Path +15%";
            if (index >= 13 && index <= 18) return "Autumn: Moon Path +15%";
            return "Winter: Blood Path +15%";
        }
    }

    [Serializable]
    public class SectorMapSnapshot
    {
        public string SectorKey = "";
        public int Seed;
        public WorldRegionID Region;
        public int GridWidth = 30;
        public int GridHeight = 30;
        public Vector2Int PlayerGridPos;
        public List<int> ExploredTileIndices = new List<int>();
        public List<int> VisibleTileIndices = new List<int>();
        public TileNode[]? Tiles;
    }

    [Serializable]
    public class WorldMapState
    {
        public int Seed = 17881588;
        public WorldRegionID CurrentRegion = WorldRegionID.SouthernBorder;
        public string CurrentSectorKey = "Sector_SouthernBorder_GuYueVillage";
        public int GridWidth = 30;
        public int GridHeight = 30;
        public Vector2Int PlayerGridPos = new Vector2Int(15, 15);
        public List<int> ExploredTileIndices = new List<int>();
        public List<int> VisibleTileIndices = new List<int>();
        public TileNode[]? Tiles;

        public Dictionary<string, SectorMapSnapshot> SectorSnapshots = new Dictionary<string, SectorMapSnapshot>();

        public void SaveCurrentSectorSnapshot(string sectorKey)
        {
            if (string.IsNullOrEmpty(sectorKey)) return;
            CurrentSectorKey = sectorKey;
            var snap = new SectorMapSnapshot
            {
                SectorKey = sectorKey,
                Seed = Seed,
                Region = CurrentRegion,
                GridWidth = GridWidth,
                GridHeight = GridHeight,
                PlayerGridPos = PlayerGridPos,
                ExploredTileIndices = new List<int>(ExploredTileIndices),
                VisibleTileIndices = new List<int>(VisibleTileIndices),
                Tiles = (TileNode[])Tiles?.Clone()!
            };
            SectorSnapshots[sectorKey] = snap;
        }

        public bool TryLoadSectorSnapshot(string sectorKey)
        {
            if (string.IsNullOrEmpty(sectorKey) || !SectorSnapshots.TryGetValue(sectorKey, out var snap) || snap?.Tiles == null)
                return false;

            Seed = snap.Seed;
            CurrentRegion = snap.Region;
            CurrentSectorKey = sectorKey;
            GridWidth = snap.GridWidth > 0 ? snap.GridWidth : 30;
            GridHeight = snap.GridHeight > 0 ? snap.GridHeight : 30;
            PlayerGridPos = snap.PlayerGridPos;
            ExploredTileIndices = new List<int>(snap.ExploredTileIndices);
            VisibleTileIndices = new List<int>(snap.VisibleTileIndices);
            Tiles = (TileNode[])snap.Tiles.Clone();
            return true;
        }

        public bool IsWithinBounds(Vector2Int pos)
        {
            return pos.x >= 0 && pos.x < GridWidth && pos.y >= 0 && pos.y < GridHeight;
        }

        public TileNode GetTile(int x, int y)
        {
            if (Tiles == null || x < 0 || x >= GridWidth || y < 0 || y >= GridHeight) return default;
            return Tiles[y * GridWidth + x];
        }

        public void SetTile(int x, int y, TileNode node)
        {
            if (Tiles == null || x < 0 || x >= GridWidth || y < 0 || y >= GridHeight) return;
            Tiles[y * GridWidth + x] = node;
        }

        public void SetExplored(int x, int y, bool explored)
        {
            if (Tiles == null || x < 0 || x >= GridWidth || y < 0 || y >= GridHeight) return;
            int idx = y * GridWidth + x;
            Tiles[idx].IsExplored = explored;
            if (explored && !ExploredTileIndices.Contains(idx))
            {
                ExploredTileIndices.Add(idx);
            }
            else if (!explored && ExploredTileIndices.Contains(idx))
            {
                ExploredTileIndices.Remove(idx);
            }
        }

        public void SetVisible(int x, int y, bool visible)
        {
            if (Tiles == null || x < 0 || x >= GridWidth || y < 0 || y >= GridHeight) return;
            int idx = y * GridWidth + x;
            Tiles[idx].IsVisible = visible;
            if (visible && !VisibleTileIndices.Contains(idx))
            {
                VisibleTileIndices.Add(idx);
            }
            else if (!visible && VisibleTileIndices.Contains(idx))
            {
                VisibleTileIndices.Remove(idx);
            }
        }

        public void InitializeDefaultGrid()
        {
            Tiles = new TileNode[GridWidth * GridHeight];
            ExploredTileIndices.Clear();
            VisibleTileIndices.Clear();

            for (int x = 0; x < GridWidth; x++)
            {
                for (int y = 0; y < GridHeight; y++)
                {
                    int distance = Mathf.Abs(x - 15) + Mathf.Abs(y - 15);
                    int danger = Mathf.Clamp(distance / 6 + 1, 1, 5);
                    bool explored = (distance <= 5);
                    bool visible = (distance <= 4);

                    var node = new TileNode
                    {
                        GridPos = new Vector2Int(x, y),
                        Biome = BiomeType.BambooMountain,
                        POI = TilePOIType.None,
                        DangerRank = danger,
                        IsExplored = explored,
                        IsVisible = visible,
                        IsActive = true
                    };

                    int idx = y * GridWidth + x;
                    Tiles[idx] = node;
                    if (explored) ExploredTileIndices.Add(idx);
                    if (visible) VisibleTileIndices.Add(idx);
                }
            }

            // Gu Yue Clan Stronghold
            SetPOI(15, 15, TilePOIType.ClanPost);

            // Jia Family Caravan Inn
            SetPOI(19, 15, TilePOIType.WayStation);

            // Primeval Essence Springs
            SetPOI(14, 17, TilePOIType.SpiritVein);
            SetPOI(10, 12, TilePOIType.SpiritVein);
            SetPOI(21, 20, TilePOIType.SpiritVein);

            // Ancient Gu Caves
            SetPOI(6, 22, TilePOIType.AncientGuCave);
            SetPOI(23, 8, TilePOIType.AncientGuCave);
            SetPOI(5, 7, TilePOIType.AncientGuCave);
            SetPOI(25, 24, TilePOIType.AncientGuCave);
            SetPOI(11, 25, TilePOIType.AncientGuCave);

            // Wild Beast Dens
            SetPOI(18, 11, TilePOIType.WildBeastDen);
            SetPOI(9, 19, TilePOIType.WildBeastDen);
            SetPOI(22, 17, TilePOIType.WildBeastDen);
            SetPOI(13, 7, TilePOIType.WildBeastDen);
            SetPOI(26, 12, TilePOIType.WildBeastDen);
            SetPOI(4, 15, TilePOIType.WildBeastDen);
        }

        private void SetPOI(int x, int y, TilePOIType poi)
        {
            if (Tiles != null && x >= 0 && x < GridWidth && y >= 0 && y < GridHeight)
            {
                Tiles[y * GridWidth + x].POI = poi;
            }
        }
    }

    [Serializable]
    public class WorldForensicsState
    {
        public List<CrimeSceneData> ActiveCrimeScenes = new List<CrimeSceneData>();

        public void AdvanceTerms()
        {
            for (int i = ActiveCrimeScenes.Count - 1; i >= 0; i--)
            {
                var scene = ActiveCrimeScenes[i];
                scene.LingeringSoulQiTerms--;
                if (scene.LingeringSoulQiTerms <= 0)
                {
                    ActiveCrimeScenes.RemoveAt(i);
                }
            }
        }
    }

    public enum RefinementPhase
    {
        Idle,
        Phase1_Extraction,   // 提炼: Thermal equilibrium [20..80]
        Phase2_WillFusion,    // 融意: Willpower clash & harmonic needle alignment
        Phase3_Condensation,  // 成形: Rapid Dao mark feeding & stone crushing
        Success,
        Failure
    }

    public enum RefinementFailureTier
    {
        None,
        Tier1_CleanFail,          // Auxiliary lost, base Gu survives at 0% satiety
        Tier2_MaterialRupture,     // Non-vital vaporized, vital in trauma sleep, -15% HP / -20% sea
        Tier3_CatastrophicBacklash // Non-vital vaporized, vital in trauma sleep, somatic explosion (sea drained to 0%)
    }

    [Serializable]
    public class GuRefinementRecipe
    {
        public string RecipeName = "Moonglow Beast Gu Refinement (月芒彘力合炼)";
        public string PrimaryGuID = "gu_moonlight_01";
        public string SecondaryGuID = "gu_white_boar_01";
        public string ResultGuID = "gu_moonglow_beast_02";
        public string ResultGuName = "Moonglow Beast Gu (月芒彘力蛊)";
        public int TargetRank = 2;
        public GuSlotCategory SlotCategory = GuSlotCategory.Attack;
        public GuDietType Diet = GuDietType.MoonOrchidPetals;
        public float BaseEssenceCost = 22f;

        public float Phase1Duration = 4.0f;
        public float Phase2Duration = 4.5f;
        public float Phase3Duration = 3.5f;
        public float DecayRateK = 18.0f; // k_decay
    }

    [Serializable]
    public class CauldronRefinementState
    {
        public bool IsActive = false;
        public RefinementPhase CurrentPhase = RefinementPhase.Idle;
        public RefinementFailureTier LastFailureTier = RefinementFailureTier.None;

        public GuRefinementRecipe ActiveRecipe = new GuRefinementRecipe();

        // Phase 1: Thermal Equilibrium [20..80]
        public float CauldronTemperature = 50f;
        public float Phase1Timer = 0f;
        public float OutOfBoundsTimer = 0f;

        // Phase 2: Will Fusion
        public float WildWillResistance = 50f;   // [0..100]
        public float MasterWillFocus = 0.5f;      // [0..1]
        public float WildWillFrequency = 0.5f;    // [0..1]
        public float WillAlignmentProgress = 0f;  // [0..100]
        public float Phase2Timer = 0f;
        public bool WildNeedleMovingRight = true;

        // Phase 3: Rapid Condensation
        public float NascentStability = 100f;     // [0..100]
        public float Phase3Timer = 0f;
        public int StonesCrushed = 0;

        public int CalculateMinStonesRequired()
        {
            if (ActiveRecipe == null) return 10;
            return Mathf.CeilToInt((ActiveRecipe.Phase3Duration * ActiveRecipe.DecayRateK * ActiveRecipe.TargetRank) / 12f);
        }

        public void ResetToIdle()
        {
            IsActive = false;
            CurrentPhase = RefinementPhase.Idle;
            LastFailureTier = RefinementFailureTier.None;
            CauldronTemperature = 50f;
            Phase1Timer = 0f;
            OutOfBoundsTimer = 0f;
            WildWillResistance = 50f;
            MasterWillFocus = 0.5f;
            WildWillFrequency = 0.5f;
            WillAlignmentProgress = 0f;
            Phase2Timer = 0f;
            WildNeedleMovingRight = true;
            NascentStability = 100f;
            Phase3Timer = 0f;
            StonesCrushed = 0;
        }
    }

    [Serializable]
    public class WorldFactionState
    {
        public List<FactionData> Factions = new List<FactionData>();

        public void InitializeDefaultFactions()
        {
            Factions.Clear();
            Factions.Add(new FactionData(FactionID.GuYueClan, "Gu Yue Clan", "古月氏", "【古】", 100, "Sector_SouthernBorder_GuYueVillage", "Righteous bloodline clan commanding Qing Mao Mountain. Revering filial bloodlines and moon path heritage."));
            Factions.Add(new FactionData(FactionID.BaiClan, "Bai Clan", "白家寨", "【白】", -10, "Sector_SouthernBorder_BaiClanMountain", "Ancient limestone karst clan famed for bone-path secret arts, secluded springs, and prideful descendants."));
            Factions.Add(new FactionData(FactionID.XiongClan, "Xiong Clan", "熊家寨", "【熊】", -10, "Sector_SouthernBorder_XiongRidge", "Fierce mountain stronghold honoring strength-path brute force, bear hunting, and survival of the fittest."));
            Factions.Add(new FactionData(FactionID.JiaConsortium, "Jia Consortium", "贾家商会", "【贾】", 25, "Sector_SouthernBorder_GuYueVillage", "Pan-regional merchant caravan syndicate. Neutral arbiters of commerce, food rations, and currency exchange."));
            Factions.Add(new FactionData(FactionID.TieClan, "Tie Clan", "铁家神捕", "【铁】", 0, "Sector_SouthernBorder_GuYueVillage", "Iron-masked righteous enforcers who uphold absolute cosmic justice, hunt demonic fiends, and issue bounties."));
            Factions.Add(new FactionData(FactionID.DemonicRogues, "Demonic Rogues", "散修魔修", "【魔】", -25, "Sector_SouthernBorder_GuYueVillage", "Lawless outlaws, demonic vagabonds, and contraband smugglers lurking in deep ravines and wilderness crevices."));
        }

        public FactionData GetFaction(FactionID id)
        {
            if (Factions.Count == 0) InitializeDefaultFactions();
            return Factions.Find(f => f.ID == id)!;
        }

        public int GetReputation(FactionID id)
        {
            var fac = GetFaction(id);
            return fac != null ? fac.ReputationScore : 0;
        }

        public int ModifyReputation(FactionID id, int delta)
        {
            var fac = GetFaction(id);
            if (fac != null)
            {
                fac.ReputationScore = Mathf.Clamp(fac.ReputationScore + delta, -300, 300);
                return fac.ReputationScore;
            }
            return 0;
        }

        public FactionStandingTier GetStandingTier(FactionID id)
        {
            var fac = GetFaction(id);
            return fac != null ? fac.GetStandingTier() : FactionStandingTier.Neutral;
        }

        public static FactionID? ResolveFactionFromClan(string clanName)
        {
            if (string.IsNullOrEmpty(clanName)) return null;
            if (clanName.Contains("Gu Yue") || clanName.Contains("古月")) return FactionID.GuYueClan;
            if (clanName.Contains("Bai") || clanName.Contains("白家")) return FactionID.BaiClan;
            if (clanName.Contains("Xiong") || clanName.Contains("熊家")) return FactionID.XiongClan;
            if (clanName.Contains("Jia") || clanName.Contains("贾家")) return FactionID.JiaConsortium;
            if (clanName.Contains("Tie") || clanName.Contains("铁家")) return FactionID.TieClan;
            if (clanName.Contains("Demonic") || clanName.Contains("魔道")) return FactionID.DemonicRogues;
            return null;
        }

        public static FactionData? ResolveClanForSector(WorldFactionState factionState, string sectorKey)
        {
            if (factionState == null || factionState.Factions == null) return null;
            if (!string.IsNullOrEmpty(sectorKey))
            {
                if (sectorKey.Contains("Bai")) return factionState.GetFaction(FactionID.BaiClan);
                if (sectorKey.Contains("Xiong")) return factionState.GetFaction(FactionID.XiongClan);
            }
            return factionState.GetFaction(FactionID.GuYueClan);
        }
    }

    [Serializable]
    public class GameStateContext
    {
        public CultivatorState Cultivator { get; set; } = new CultivatorState();
        public GuInventoryState GuInventory { get; set; } = new GuInventoryState();
        public WorldCalendarState Calendar { get; set; } = new WorldCalendarState();
        public WorldMapState WorldMap { get; set; } = new WorldMapState();
        public WorldForensicsState Forensics { get; set; } = new WorldForensicsState();
        public WorldSocialVendettaState SocialVendetta { get; set; } = new WorldSocialVendettaState();
        public CauldronRefinementState Cauldron { get; set; } = new CauldronRefinementState();
        public WorldFactionState Faction { get; set; } = new WorldFactionState();
        public List<CultivatorEntityData> LivingNPCs { get; set; } = new List<CultivatorEntityData>();

        public static GameStateContext CreateDefault()
        {
            var ctx = new GameStateContext();

            // Seed Fang Yuan
            ctx.Cultivator.Name = "Gu Yue Fang Yuan";
            ctx.Cultivator.Aptitude = AptitudeGrade.C_Grade;
            ctx.Cultivator.AptitudePercent = 44.0f;
            ctx.Cultivator.Rank = 1;
            ctx.Cultivator.Substage = 1;
            ctx.Cultivator.CurrentEssenceSea = 44.0f;
            ctx.Cultivator.PrimevalStones = 250;
            ctx.Cultivator.CurrentNurtureProgress = 0f;

            // Seed starter Gu
            var moonlightGu = new GuWormData
            {
                GuID = "gu_moonlight_01",
                Name = "Moonlight Gu (月光蛊)",
                Rank = 1,
                Slot = GuSlotCategory.Attack,
                Diet = GuDietType.MoonOrchidPetals,
                BaseEssenceCost = 10f,
                Satiety = 100f,
                IsVitalGu = true
            };

            var boarGu = new GuWormData
            {
                GuID = "gu_white_boar_01",
                Name = "White Boar Gu (白豕蛊)",
                Rank = 1,
                Slot = GuSlotCategory.Support,
                Diet = GuDietType.FreshBoarFlesh,
                BaseEssenceCost = 15f,
                Satiety = 100f,
                IsVitalGu = false
            };

            ctx.GuInventory.EquippedGu.Add(moonlightGu);
            ctx.GuInventory.EquippedGu.Add(boarGu);

            // Seed starter rations in stockpile
            ctx.GuInventory.FoodStockpile.Add(new InventoryItem("food_moon_orchid", "Moon Orchid Petals (月兰花瓣)", GuDietType.MoonOrchidPetals, 12, 3));
            ctx.GuInventory.FoodStockpile.Add(new InventoryItem("food_boar_meat", "Fresh Boar Flesh (新鲜野猪肉)", GuDietType.FreshBoarFlesh, 8, 2));
            ctx.GuInventory.FoodStockpile.Add(new InventoryItem("food_vintage_wine", "Green Bamboo Wine (青竹老酒)", GuDietType.VintageWine, 3, 10));

            // Seed world map tiles and landmarks
            ctx.WorldMap.InitializeDefaultGrid();

            // Seed 6 autonomous factions
            ctx.Faction.InitializeDefaultFactions();

            return ctx;
        }
    }
}
