using System;

namespace TaiwuGu.Core
{
    public enum WorldCalamityKind
    {
        BeastTide,
        InheritanceRuins,
        GrandCaravanAuction,
        InterClanSkirmish
    }

    public enum WorldCalamityPhase
    {
        Foreshadowed,
        Active,
        Resolved,
        Expired
    }

    public enum BountyHunterState
    {
        Pursuing,
        Engaged,
        Bribed,
        Defeated
    }
}
