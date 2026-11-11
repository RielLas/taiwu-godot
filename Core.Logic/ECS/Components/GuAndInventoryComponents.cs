using System;
using System.Collections.Generic;
using TaiwuGu.Core;

namespace TaiwuGu.Entities
{
    [Serializable]
    public class InventoryComponent
    {
        public List<InventoryItem> FoodStockpile { get; set; } = new();

        public void AddFood(InventoryItem item)
        {
            var existing = FoodStockpile.Find(i => i.ItemID == item.ItemID);
            if (existing != null) existing.Quantity += item.Quantity;
            else FoodStockpile.Add(item);
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
                string id = "food_" + diet.ToString().ToLowerInvariant();
                FoodStockpile.Add(new InventoryItem(id, diet.ToString(), diet, quantity, 1));
            }
        }

        public bool TryConsumeFood(GuDietType diet, int quantity = 1)
        {
            var item = FoodStockpile.Find(i => i.DietType == diet && i.Quantity >= quantity);
            if (item == null) return false;
            item.Quantity -= quantity;
            if (item.Quantity <= 0) FoodStockpile.Remove(item);
            return true;
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
    }

    /// <summary>
    /// First-class living Gu worm entity component. Owned by any cultivator via OwnerComponent.
    /// </summary>
    [Serializable]
    public class GuWormComponent
    {
        public string GuID { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Rank { get; set; } = 1;
        public GuSlotCategory Slot { get; set; }
        public GuDietType Diet { get; set; }
        public float BaseEssenceCost { get; set; }
        public float Satiety { get; set; } = 100f;
        public bool IsVitalGu { get; set; } = false;
        public bool HasHostileWill { get; set; } = false;
        public bool IsPredatory { get; set; } = false;
        public int ConsecutiveHibernationCycles { get; set; } = 0;
        public bool IsInTraumaSleep { get; set; } = false;

        public GuStarvationStage GetStarvationStage()
        {
            if (ConsecutiveHibernationCycles >= GetMaxHibernationCyclesAllowed() && !IsVitalGu)
                return GuStarvationStage.Dead;
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

        public float GetPowerMultiplier() => GetStarvationStage() switch
        {
            GuStarvationStage.Healthy => 1.0f,
            GuStarvationStage.Hungry => 0.85f,
            GuStarvationStage.Starving => 0.5f,
            _ => 0.0f
        };

        public float GetRealEssenceCost(int userRank, int userSubstage)
        {
            double density = ApertureComponent.CalculateDensityCoefficient(userRank, userSubstage);
            float baseCost = BaseEssenceCost;

            if (IsInTraumaSleep && IsVitalGu)
            {
                baseCost *= 2.0f;
            }
            else
            {
                var stage = GetStarvationStage();
                if (stage == GuStarvationStage.Hungry) baseCost *= 1.25f;
                else if (stage == GuStarvationStage.Starving) baseCost *= 1.50f;
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
                Satiety = Math.Max(0f, Satiety - degradation);
                if (Satiety <= 0f) ConsecutiveHibernationCycles++;
            }
        }

        public void Feed(float satietyGain = 100f)
        {
            Satiety = Math.Min(100f, Satiety + satietyGain);
            if (Satiety > 0f)
            {
                ConsecutiveHibernationCycles = 0;
                IsInTraumaSleep = false;
            }
        }
    }
}
