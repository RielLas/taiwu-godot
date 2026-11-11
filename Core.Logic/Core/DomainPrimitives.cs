using System;

namespace TaiwuGu.Core
{
    public enum AptitudeGrade
    {
        D_Grade, // 20-39%
        C_Grade, // 40-59%
        B_Grade, // 60-79%
        A_Grade  // 80-99%
    }

    public enum GuDietType
    {
        None = 0,
        MoonOrchidPetals = 1,
        FreshBoarFlesh = 2,
        VintageWine = 3,
        WillowLeafDew = 4,
        IronOrePowder = 5,
        WolfBlood = 6,
        SpringWater = 7,
        SpiritStonePowder = 8
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
        Healthy,
        Hungry,
        Starving,
        Hibernating,
        Dead
    }

    [Serializable]
    public class InventoryItem
    {
        public string ItemID { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public GuDietType DietType { get; set; }
        public int Quantity { get; set; } = 1;
        public int BasePriceStones { get; set; } = 5;

        public InventoryItem() { }

        public InventoryItem(string id, string name, GuDietType diet, int qty, int price = 5)
        {
            ItemID = id;
            Name = name;
            DietType = diet;
            Quantity = qty;
            BasePriceStones = price;
        }
    }

    public enum HalalSoundType
    {
        Footstep,
        ParchmentRustle,
        BladeSlash,
        BluntImpact,
        DashWhoosh,
        CauldronFire,
        StoneShatter,
        CauldronSteam,
        WoodenKnock
    }

    public enum RefinementPhase
    {
        Idle,
        Phase1_Extraction,
        Phase2_WillFusion,
        Phase3_Condensation,
        Success,
        Failure
    }

    public enum RefinementFailureTier
    {
        None,
        Tier1_CleanFail,
        Tier2_MaterialRupture,
        Tier3_CatastrophicBacklash
    }
}

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
}
