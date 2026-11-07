using System;
using System.Collections.Generic;

namespace TaiwuGu.Core
{
    [Serializable]
    public class SaveMetadataDTO
    {
        public string SlotID = "";
        public string PlayerName = "";
        public int CurrentYear;
        public int CurrentSolarTerm;
        public string SolarTermName = "";
        public string CurrentMountainID = "";
        public double TotalPlaytimeSeconds;
        public string LastPlayedTimestamp = "";
        public int PreviewRank;
        public int PreviewSubstage;
        public float PreviewEssencePercent;
        public int PreviewStones;
    }

    [Serializable]
    public class SavePayloadDTO
    {
        public string SaveVersion = "2.0.0";
        public SaveMetadataDTO Metadata = new SaveMetadataDTO();
        public GameStateContext Context = new GameStateContext();
    }
}
