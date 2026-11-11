using System;
using System.Collections.Generic;
using TaiwuGu.Core;
using TaiwuGu.Psychology;

namespace TaiwuGu.Entities
{
    [Serializable]
    public class FactionComponent
    {
        public FactionID ID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ChineseName { get; set; } = string.Empty;
        public string SealCalligraphy { get; set; } = string.Empty;
        public int ReputationScore { get; set; }
        public string HomeSectorKey { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public FactionComponent() { }

        public FactionComponent(FactionID id, string name, string chineseName, string seal, int initialRep, string homeSector, string desc)
        {
            ID = id;
            Name = name;
            ChineseName = chineseName;
            SealCalligraphy = seal;
            ReputationScore = Math.Clamp(initialRep, -300, 300);
            HomeSectorKey = homeSector;
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

        public string GetStandingTitle() => GetStandingTier() switch
        {
            FactionStandingTier.Nemesis  => "Blood Nemesis (死敌血仇)",
            FactionStandingTier.Hostile  => "Hostile / Expelled (冷眼驱逐)",
            FactionStandingTier.Neutral  => "Neutral Passerby (萍水相逢)",
            FactionStandingTier.Friendly => "Trusted Ally (引为知己)",
            FactionStandingTier.Revered  => "Revered Benefactor (崇敬上宾)",
            _ => "Neutral"
        };

        public float GetPriceMultiplier() => GetStandingTier() switch
        {
            FactionStandingTier.Nemesis  => 999.0f,
            FactionStandingTier.Hostile  => 1.50f,
            FactionStandingTier.Neutral  => 1.00f,
            FactionStandingTier.Friendly => 0.90f,
            FactionStandingTier.Revered  => 0.80f,
            _ => 1.00f
        };

        public int ModifyReputation(int delta)
        {
            ReputationScore = Math.Clamp(ReputationScore + delta, -300, 300);
            return ReputationScore;
        }

        public FactionData ToFactionData() => new FactionData(ID, Name, ChineseName, SealCalligraphy, ReputationScore, HomeSectorKey, Description);

        public static FactionID? ResolveFactionFromClan(string clanName)
        {
            if (string.IsNullOrEmpty(clanName)) return null;
            if (clanName.Contains("Gu Yue") || clanName.Contains("古月")) return FactionID.GuYueClan;
            if (clanName.Contains("Bai") || clanName.Contains("白家")) return FactionID.BaiClan;
            if (clanName.Contains("Xiong") || clanName.Contains("熊家")) return FactionID.XiongClan;
            if (clanName.Contains("Jia") || clanName.Contains("贾家")) return FactionID.JiaConsortium;
            if (clanName.Contains("Tie") || clanName.Contains("铁家")) return FactionID.TieClan;
            if (clanName.Contains("Demonic") || clanName.Contains("魔修") || clanName.Contains("魔道")) return FactionID.DemonicRogues;
            return null;
        }
    }

    [Serializable]
    public class CrimeSceneComponent
    {
        public string VictimName { get; set; } = string.Empty;
        public CrimeSignature Signature { get; set; }
        public int LingeringSoulQiTerms { get; set; } = 2;
        public bool IsTamperedClean { get; set; } = false;
        public string FramedFaction { get; set; } = string.Empty;

        public CrimeSceneComponent() { }

        public CrimeSceneComponent(string victimName, CrimeSignature sig, int terms = 2)
        {
            VictimName = victimName;
            Signature = sig;
            LingeringSoulQiTerms = terms;
        }
    }

    [Serializable]
    public class KinshipLedgerComponent
    {
        public List<KinshipEdge> KinshipEdges { get; set; } = new();
        public List<ClanBountyNotice> ActiveBounties { get; set; } = new();

        public void AddEdge(string sourceExtId, string targetExtId, KinshipRelationshipType rel, float lambda, float retributionMult)
        {
            KinshipEdges.Add(new KinshipEdge
            {
                SourceEntityId = sourceExtId,
                TargetEntityId = targetExtId,
                Relationship = rel,
                EdgeWeightLambda = lambda,
                FacadeRetributionMultiplier = retributionMult
            });
        }
    }
}
