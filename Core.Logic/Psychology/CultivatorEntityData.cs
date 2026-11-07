using System;
using System.Collections.Generic;
using TaiwuGu.Core;

namespace TaiwuGu.Psychology
{
    public enum TaiwuDisposition
    {
        Righteous,   // 刚正: High moral code, fights to death, refuses illegal trade
        Benevolent,  // 仁善: Compassionate, yields easily to threats, offers trade discounts
        Pragmatic,   // 中庸: Balanced, self-contained, trades strictly on value
        Rebellious,  // 叛逆: Unpredictable, trades contraband, holds long grudges
        Egoist       // 唯我: Pure demonic self-interest, gouges prices, ambushes for profit
    }

    [Serializable]
    public class CultivatorEntityData
    {
        public string EntityId = "";
        public string Name = "";
        public string Clan = "";
        public int Rank = 1;
        public int Substage = 1;
        public AptitudeGrade Aptitude = AptitudeGrade.C_Grade;
        public float AptitudePercent = 45f;
        public float CurrentEssenceSea;
        public int PrimevalStones = 120;
        public TaiwuDisposition Disposition = TaiwuDisposition.Pragmatic;
        public int Favorability = 0; // [-300 Nemesis .. +300 Revered]
        public bool SoulTabletBound = false;
        public Vector2Int CurrentTilePos;
        public string CurrentSectorKey = "Sector_SouthernBorder_GuYueVillage";

        // --- Reverend Insanity Inviolate Lifespan Law (寿元天则) ---
        public int Age = 20;
        public int LifespanYears = 100;
        public bool IsAlive => Age < LifespanYears;
        public int RemainingLifespanYears => Mathf.Max(0, LifespanYears - Age);

        public List<GuWormData> EquippedGu = new List<GuWormData>();
        public List<InventoryItem> Inventory = new List<InventoryItem>();
        public List<string> MemoryLogs = new List<string>();

        public void AddMemory(string log) => MemoryLogs.Add($"[{DateTime.UtcNow:HH:mm:ss}] {log}");

        public CultivatorEntityData()
        {
            CurrentEssenceSea = AptitudePercent;
        }

        public CultivatorEntityData(string id, string name, string clan, int rank, int substage, TaiwuDisposition disp, Vector2Int startPos, string sectorKey = "Sector_SouthernBorder_GuYueVillage", int age = 20, int lifespanYears = 100)
        {
            EntityId = id;
            Name = name;
            Clan = clan;
            Rank = rank;
            Substage = substage;
            Disposition = disp;
            CurrentTilePos = startPos;
            CurrentSectorKey = sectorKey;
            CurrentEssenceSea = AptitudePercent;
            Age = age;
            LifespanYears = lifespanYears;
        }

        public bool AgeOneYear()
        {
            Age++;
            return IsAlive;
        }
    }
}
