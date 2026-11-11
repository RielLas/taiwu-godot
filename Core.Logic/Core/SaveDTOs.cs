using System;
using TaiwuGu.Entities;

namespace TaiwuGu.Core
{
    [Serializable]
    public class SaveMetadataDTO
    {
        public string SlotID { get; set; } = "";
        public string PlayerName { get; set; } = "";
        public int CurrentYear { get; set; }
        public int CurrentSolarTerm { get; set; }
        public string SolarTermName { get; set; } = "";
        public string CurrentMountainID { get; set; } = "";
        public double TotalPlaytimeSeconds { get; set; }
        public string LastPlayedTimestamp { get; set; } = "";
        public int PreviewRank { get; set; }
        public int PreviewSubstage { get; set; }
        public float PreviewEssencePercent { get; set; }
        public int PreviewStones { get; set; }
    }

    [Serializable]
    public class WorldSaveEnvelope
    {
        public string SaveVersion { get; set; } = "3.0.0";
        public SaveMetadataDTO Metadata { get; set; } = new();
        public WorldStore Store { get; set; } = new();
    }
}
