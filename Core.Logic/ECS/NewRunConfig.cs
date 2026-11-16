using System;
using TaiwuGu.Core;
using TaiwuGu.Psychology;

namespace TaiwuGu.Entities
{
    /// <summary>
    /// Pure POCO configuration for seeding a new run / journey.
    /// Passed from Title Screen Character Creation into WorldFactory.CreateFromConfig.
    /// </summary>
    [Serializable]
    public class NewRunConfig
    {
        public int RunSeed { get; set; } = 1;
        public string PlayerName { get; set; } = "Gu Yue Fang Yuan";
        public string OriginClan { get; set; } = "Gu Yue Clan (古月氏)";
        public string StartingSectorKey { get; set; } = WorldFactory.DefaultSectorKey;
        public float AptitudePercent { get; set; } = 44.0f;
        public TaiwuDisposition Disposition { get; set; } = TaiwuDisposition.Egoist;
        public string VitalGuId { get; set; } = "gu_moonlight_01";

        public NewRunConfig() { }

        public NewRunConfig(
            int runSeed,
            string playerName,
            string originClan,
            string startingSectorKey,
            float aptitudePercent,
            TaiwuDisposition disposition,
            string vitalGuId)
        {
            RunSeed = runSeed;
            PlayerName = playerName;
            OriginClan = originClan;
            StartingSectorKey = startingSectorKey;
            AptitudePercent = aptitudePercent;
            Disposition = disposition;
            VitalGuId = vitalGuId;
        }
    }
}
