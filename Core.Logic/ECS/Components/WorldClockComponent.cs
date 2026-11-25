using System;
using TaiwuGu.Core;

namespace TaiwuGu.Entities
{
    [Serializable]
    public class WorldClockComponent
    {
        public const int BASE_MONTHLY_AP = 30;
        public const int TERMS_PER_YEAR = 24;

        public int CurrentYear { get; set; } = 1;
        public int CurrentSolarTermIndex { get; set; } = 4; // Chunfen_SpringEquinox
        public int ActionPointsRemaining { get; set; } = BASE_MONTHLY_AP;

        public int RealmBonusAP { get; set; } = 0;
        public int SeasonalBonusAP { get; set; } = 0;
        public int FatiguePenaltyAP { get; set; } = 0;

        public int LastClanRestYear { get; set; } = -1;
        public int LastClanRestTerm { get; set; } = -1;

        public int MonthlyAPBudget => Math.Max(10, BASE_MONTHLY_AP + RealmBonusAP + SeasonalBonusAP - FatiguePenaltyAP);
        public SolarTerm ActiveSolarTerm => (SolarTerm)Math.Clamp(CurrentSolarTermIndex, 1, TERMS_PER_YEAR);

        public string GetSolarTermName() => ActiveSolarTerm switch
        {
            SolarTerm.Lichun_BeginningOfSpring => "Beginning of Spring (立春)",
            SolarTerm.Yushui_RainWater => "Rain Water (雨水)",
            SolarTerm.Jingzhe_AwakeningOfInsects => "Awakening of Insects (惊蛰)",
            SolarTerm.Chunfen_SpringEquinox => "Spring Equinox (春分)",
            SolarTerm.Qingming_PureBrightness => "Pure Brightness (清明)",
            SolarTerm.Guyu_GrainRain => "Grain Rain (谷雨)",
            SolarTerm.Lixia_BeginningOfSummer => "Beginning of Summer (立夏)",
            SolarTerm.Xiaoman_GrainBuds => "Grain Buds (小满)",
            SolarTerm.Mangzhong_GrainInEar => "Grain in Ear (芒种)",
            SolarTerm.Xiazhi_SummerSolstice => "Summer Solstice (夏至)",
            SolarTerm.Xiaoshu_MinorHeat => "Minor Heat (小暑)",
            SolarTerm.Dashu_MajorHeat => "Major Heat (大暑)",
            SolarTerm.Liqiu_BeginningOfAutumn => "Beginning of Autumn (立秋)",
            SolarTerm.Chushu_EndOfHeat => "End of Heat (处暑)",
            SolarTerm.Bailu_WhiteDew => "White Dew (白露)",
            SolarTerm.Qiufen_AutumnEquinox => "Autumn Equinox (秋分)",
            SolarTerm.Hanlu_ColdDew => "Cold Dew (寒露)",
            SolarTerm.Shuangjiang_FrostDescent => "Frost's Descent (霜降)",
            SolarTerm.Lidong_BeginningOfWinter => "Beginning of Winter (立冬)",
            SolarTerm.Xiaoxue_MinorSnow => "Minor Snow (小雪)",
            SolarTerm.Daxue_MajorSnow => "Major Snow (大雪)",
            SolarTerm.Dongzhi_WinterSolstice => "Winter Solstice (冬至)",
            SolarTerm.Xiaohan_MinorCold => "Minor Cold (小寒)",
            SolarTerm.Dahan_MajorCold => "Major Cold (大寒)",
            _ => "Spring Equinox (春分)"
        };

        public string GetSeasonName()
        {
            int index = CurrentSolarTermIndex;
            if (index >= 1 && index <= 6) return "Spring (春)";
            if (index >= 7 && index <= 12) return "Summer (夏)";
            if (index >= 13 && index <= 18) return "Autumn (秋)";
            return "Winter (冬)";
        }

        public float GetSeasonalPathAmplification(DaoPathType path)
        {
            int index = CurrentSolarTermIndex;
            if (index >= 1 && index <= 6 && path == DaoPathType.EnslavementPath) return 1.15f;
            if (index >= 7 && index <= 12 && path == DaoPathType.RefinementPath) return 1.15f;
            if (index >= 13 && index <= 18 && path == DaoPathType.MoonPath) return 1.15f;
            if (index > 18 && path == DaoPathType.BloodPath) return 1.15f;
            return 1.0f;
        }

        public string GetSeasonalAffinityDescription()
        {
            int index = CurrentSolarTermIndex;
            if (index >= 1 && index <= 6) return "Spring: Enslavement Path +15%";
            if (index >= 7 && index <= 12) return "Summer: Refinement Path +15%";
            if (index >= 13 && index <= 18) return "Autumn: Moon Path +15%";
            return "Winter: Blood Path +15%";
        }
    }
}
