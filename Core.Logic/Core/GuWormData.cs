using System;

namespace TaiwuGu.Core
{
    public enum GuDietType
    {
        None = 0,
        MoonOrchidPetals = 1, // Moonlight Gu line
        FreshBoarFlesh = 2,   // White Boar Gu line
        VintageWine = 3,      // Liquor Worm line
        WillowLeafDew = 4,    // Light/Wood line
        IronOrePowder = 5,    // Metal/Bone line
        WolfBlood = 6,        // Blood/Beast line
        SpringWater = 7,      // Water line
        SpiritStonePowder = 8 // Heavenly Essence Treasure Lotus
    }

    public enum GuSlotCategory
    {
        Attack = 0,
        Defense = 1,
        Movement = 2,
        Healing = 3,
        Recon = 4,
        Storage = 5,
        Support = 6
    }

    public enum GuStarvationStage
    {
        Healthy,      // > 50% Satiety
        Hungry,       // 25% - 50%: +25% activation cost
        Starving,     // 1% - 24%: sluggish cast time, output halved, killer moves disabled
        Hibernating,  // 0% Satiety: curled in deep sleep, shell pale, cannot be summoned
        Dead          // Desiccated to ash (Auxiliary only)
    }

    [Serializable]
    public class InventoryItem
    {
        public string ItemID = "";
        public string Name = "";
        public GuDietType DietType;
        public int Quantity;
        public int BasePriceStones;

        public InventoryItem()
        {
            Quantity = 1;
            BasePriceStones = 5;
        }

        public InventoryItem(string id, string name, GuDietType diet, int qty, int price = 5)
        {
            ItemID = id;
            Name = name;
            DietType = diet;
            Quantity = qty;
            BasePriceStones = price;
        }
    }

    /// <summary>
    /// Canon Gu Resilience & Starvation Pacing Engine.
    /// Satiety drops by a modest -25% to -35% per missed feeding cycle.
    /// At 0%: Enters Deep Hibernation.
    /// </summary>
    [Serializable]
    public class GuWormData
    {
        public string GuID = "";
        public string Name = "";
        public int Rank = 1;
        public GuSlotCategory Slot;
        public GuDietType Diet;
        public float BaseEssenceCost; // Base Essence Units (BEU)

        public float Satiety = 100f;
        public bool IsVitalGu = false; // 本命蛊
        public bool HasHostileWill = false; // Must cleanse before equipping
        public bool IsPredatory = false; // Eats player flesh/blood if starving

        public int ConsecutiveHibernationCycles = 0; // Cycles spent at 0% satiety
        public bool IsInTraumaSleep = false; // Refinement failure dormancy

        public GuStarvationStage GetStarvationStage()
        {
            if (ConsecutiveHibernationCycles >= GetMaxHibernationCyclesAllowed() && !IsVitalGu)
            {
                return GuStarvationStage.Dead;
            }
            if (IsInTraumaSleep || Satiety <= 0f) return GuStarvationStage.Hibernating;
            if (Satiety < 25f) return GuStarvationStage.Starving;
            if (Satiety <= 50f) return GuStarvationStage.Hungry;
            return GuStarvationStage.Healthy;
        }

        public int GetMaxHibernationCyclesAllowed()
        {
            if (IsVitalGu) return int.MaxValue;
            return Rank switch
            {
                1 => 2,
                2 => 4,
                3 => 4,
                4 => 8,
                5 => 12,
                _ => 2
            };
        }

        public float GetPowerMultiplier()
        {
            var stage = GetStarvationStage();
            return stage switch
            {
                GuStarvationStage.Healthy => 1.0f,
                GuStarvationStage.Hungry => 0.85f,
                GuStarvationStage.Starving => 0.5f,
                _ => 0.0f
            };
        }

        public float GetRealEssenceCost(int userRank, int userSubstage)
        {
            double density = CultivatorData.CalculateDensityCoefficient(userRank, userSubstage);
            float baseCost = BaseEssenceCost;

            if (IsInTraumaSleep && IsVitalGu)
            {
                baseCost *= 2.0f; // 200% Desperation Cast cost
            }
            else
            {
                var stage = GetStarvationStage();
                if (stage == GuStarvationStage.Hungry)
                {
                    baseCost *= 1.25f; // +25% penalty
                }
                else if (stage == GuStarvationStage.Starving)
                {
                    baseCost *= 1.50f; // +50% penalty
                }
            }

            return (float)(baseCost / density);
        }

        public void ApplyFeedingCycleResult(bool wasFed, float degradation = 30f)
        {
            if (wasFed)
            {
                Satiety = 100f;
                ConsecutiveHibernationCycles = 0;
                IsInTraumaSleep = false;
            }
            else
            {
                Satiety = Mathf.Max(0f, Satiety - degradation);
                if (Satiety <= 0f)
                {
                    ConsecutiveHibernationCycles++;
                }
            }
        }

        public void Feed(float satietyGain)
        {
            Satiety = Mathf.Clamp(Satiety + satietyGain, 0f, 100f);
            if (Satiety > 0f)
            {
                ConsecutiveHibernationCycles = 0;
                IsInTraumaSleep = false;
            }
        }
    }
}
