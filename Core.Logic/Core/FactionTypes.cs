using System;
using System.Collections.Generic;

namespace TaiwuGu.Core
{
    public enum FactionID
    {
        GuYueClan,       // 古月氏 (Righteous Bloodline Mountain Clan)
        BaiClan,         // 白家寨 (Righteous Rival Bone-Path Clan)
        XiongClan,       // 熊家寨 (Ferocious Strength-Path Bear Clan)
        JiaConsortium,   // 贾家商会 (Neutral Merchant Syndicate)
        TieClan,         // 铁家神捕 (Righteous Iron-Mask Enforcers)
        DemonicRogues    // 散修魔修 (Outlaws, Demonic Rogues & Contraband Traders)
    }

    public enum FactionStandingTier
    {
        Nemesis,    // -300 to -151: Attack on sight, active arrest warrants/bounties
        Hostile,    // -150 to -50: Trade embargo, refused sanctuary, prices +50%
        Neutral,    // -49 to +49: Standard relations and regular prices
        Friendly,   // +50 to +149: Welcome in pavilions, 10% trade discount
        Revered     // +150 to +300: Sanctuary, priority missions, 20% trade discount
    }

    [Serializable]
    public class FactionData
    {
        public FactionID ID;
        public string Name = "";
        public string ChineseName = "";
        public string SealCalligraphy = ""; // E.g. "【古】", "【白】", "【熊】", "【贾】", "【铁】", "【魔】"
        public int ReputationScore;     // [-300 .. +300]
        public string HomeSectorKey = "";
        public string Description = "";

        public FactionData() { }

        public FactionData(FactionID id, string name, string chineseName, string seal, int initialReputation, string sectorKey, string desc)
        {
            ID = id;
            Name = name;
            ChineseName = chineseName;
            SealCalligraphy = seal;
            ReputationScore = Mathf.Clamp(initialReputation, -300, 300);
            HomeSectorKey = sectorKey;
            Description = desc;
        }

        public FactionStandingTier GetStandingTier()
        {
            if (ReputationScore <= -151) return FactionStandingTier.Nemesis;
            if (ReputationScore <= -50) return FactionStandingTier.Hostile;
            if (ReputationScore <= 49) return FactionStandingTier.Neutral;
            if (ReputationScore <= 149) return FactionStandingTier.Friendly;
            return FactionStandingTier.Revered;
        }

        public string GetStandingTitle()
        {
            return GetStandingTier() switch
            {
                FactionStandingTier.Nemesis  => "Blood Nemesis (死敌血仇)",
                FactionStandingTier.Hostile  => "Hostile / Expelled (冷眼驱逐)",
                FactionStandingTier.Neutral  => "Neutral Passerby (萍水相逢)",
                FactionStandingTier.Friendly => "Trusted Ally (引为知己)",
                FactionStandingTier.Revered  => "Revered Benefactor (崇敬上宾)",
                _ => "Neutral"
            };
        }

        public float GetPriceMultiplier()
        {
            return GetStandingTier() switch
            {
                FactionStandingTier.Nemesis  => 999.0f, // Trade completely blocked
                FactionStandingTier.Hostile  => 1.50f,  // +50% gouge
                FactionStandingTier.Neutral  => 1.00f,  // Base price
                FactionStandingTier.Friendly => 0.90f,  // 10% discount
                FactionStandingTier.Revered  => 0.80f,  // 20% discount
                _ => 1.00f
            };
        }
    }
}
