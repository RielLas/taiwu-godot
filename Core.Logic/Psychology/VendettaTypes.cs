using System;
using System.Collections.Generic;
using TaiwuGu.Core;

namespace TaiwuGu.Psychology
{
    public enum KinshipRelationshipType
    {
        Parent,
        Child,
        Spouse,
        DaoCompanion,
        Master,
        Disciple,
        SwornSibling,
        ClanPatriarch
    }

    public enum VengeancePursuitType
    {
        None,
        SoloAmbush,         // VengefulNPC.Rank >= Player.Rank -> Takes field directly
        ClanBountyWarrant,  // VengefulNPC.Rank < Player.Rank -> Spends clan merit/stones to issue bounty
        HiredMercenary      // Hires higher-rank Clan Elder / Tie Clan hunter
    }

    [Serializable]
    public class NPCProfile
    {
        public string EntityId = "";
        public string Name = "";
        public string Clan = "";                  // E.g. "Gu Yue Clan", "Bai Clan", "Xiong Clan", "Tie Clan"
        public int Rank = 1;
        public int Favorability = 0;         // [-300 to +300]
        public bool SoulTabletBound = false; // Constraint 1: True = Core Bloodline/Elder (Tablet shatters on death)
        public string PersonalityTrait = "Gangzheng"; // "Huduan" (1.5x retribution), "Gangzheng", "Weiwo"
        public Vector2Int CurrentGridPos;
        public VengeancePursuitType PursuitState = VengeancePursuitType.None;
        public string TargetSlayerId = "";
        public Vector2Int InvestigationTargetPos;
    }

    [Serializable]
    public struct KinshipEdge
    {
        public string SourceEntityId;
        public string TargetEntityId;
        public KinshipRelationshipType Relationship;
        public float EdgeWeightLambda;         // Closeness factor [0.1 - 2.0]
        public float FacadeRetributionMultiplier; // E.g., HUDUAN (护短) trait = 1.5x
    }

    [Serializable]
    public class ClanBountyNotice
    {
        public string TargetId = "";
        public string IssuerClan = "";
        public int RewardStones;
        public int ThreatRank;
        public string Reason = "";
    }

    [Serializable]
    public class WorldSocialVendettaState
    {
        public List<NPCProfile> NPCs = new List<NPCProfile>();
        public List<KinshipEdge> KinshipEdges = new List<KinshipEdge>();
        public List<ClanBountyNotice> ActiveBounties = new List<ClanBountyNotice>();
    }
}
