using System;
using System.Collections.Generic;
using TaiwuGu.Core;
using TaiwuGu.Map;

namespace TaiwuGu.Entities
{
    [Serializable]
    public class WorldEventEntry
    {
        public string EventId { get; set; } = Guid.NewGuid().ToString("N");
        public WorldCalamityKind Kind { get; set; } = WorldCalamityKind.BeastTide;
        public WorldCalamityPhase Phase { get; set; } = WorldCalamityPhase.Foreshadowed;
        public string SectorKey { get; set; } = string.Empty;
        public GridCoord TargetCoord { get; set; } = GridCoord.Zero;
        public TilePOIType PreviousPOI { get; set; } = TilePOIType.None;
        public string? PreviousCustomLandmarkId { get; set; }
        public int RemainingTerms { get; set; } = 2;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public WorldEventEntry() { }

        public WorldEventEntry(
            WorldCalamityKind kind,
            WorldCalamityPhase phase,
            string sectorKey,
            GridCoord targetCoord,
            int remainingTerms,
            string title,
            string description)
        {
            Kind = kind;
            Phase = phase;
            SectorKey = sectorKey;
            TargetCoord = targetCoord;
            RemainingTerms = remainingTerms;
            Title = title;
            Description = description;
        }
    }

    [Serializable]
    public class WorldEventComponent
    {
        public int NextEventCountdownTerms { get; set; } = 6;
        public List<WorldEventEntry> ActiveEvents { get; set; } = new();

        public WorldEventComponent() { }
    }

    [Serializable]
    public class BountyHunterRecord
    {
        public ulong HunterEntityId { get; set; }
        public ulong TargetPlayerId { get; set; }
        public FactionID IssuingFaction { get; set; } = FactionID.GuYueClan;
        public string HunterName { get; set; } = string.Empty;
        public string CurrentSectorKey { get; set; } = string.Empty;
        public GridCoord CurrentCoord { get; set; } = GridCoord.Zero;
        public BountyHunterState State { get; set; } = BountyHunterState.Pursuing;
        public int BribeCostStones { get; set; } = 100;
        public int ThreatRank { get; set; } = 1;

        public BountyHunterRecord() { }

        public BountyHunterRecord(
            ulong hunterEntityId,
            ulong targetPlayerId,
            FactionID issuingFaction,
            string hunterName,
            string sectorKey,
            GridCoord coord,
            int bribeCost = 100,
            int threatRank = 1)
        {
            HunterEntityId = hunterEntityId;
            TargetPlayerId = targetPlayerId;
            IssuingFaction = issuingFaction;
            HunterName = hunterName;
            CurrentSectorKey = sectorKey;
            CurrentCoord = coord;
            State = BountyHunterState.Pursuing;
            BribeCostStones = bribeCost;
            ThreatRank = threatRank;
        }
    }

    [Serializable]
    public class BountyTrackerComponent
    {
        public List<BountyHunterRecord> Hunters { get; set; } = new();

        public BountyTrackerComponent() { }
    }
}
