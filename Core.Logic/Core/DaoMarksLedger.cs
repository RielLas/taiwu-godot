using System;

namespace TaiwuGu.Core
{
    public enum DaoPathType
    {
        MoonPath,
        StrengthPath,
        RefinementPath,
        EnslavementPath,
        BloodPath,
        HumanPath
    }

    /// <summary>
    /// Canon Dao Marks Ledger.
    /// Dao marks permanently engrave onto the cultivator's body and aperture,
    /// providing linear and compounding percentage amplifications to path-specific Gu.
    /// In mortal bodies (Rank 1-5), external path Dao marks are 0; only innate Human Path Dao marks exist.
    /// </summary>
    [Serializable]
    public class DaoMarksLedger
    {
        public int HumanPathMarks = 4; // Innate human path dao marks (capped <= 10 for normal mortals; 50-100 for 10 Extreme Physiques)
        public int MoonPathMarks = 0;
        public int StrengthPathMarks = 0;
        public int RefinementPathMarks = 0;
        public int EnslavementPathMarks = 0;
        public int BloodPathMarks = 0;

        /// <summary>
        /// Each Dao mark provides +0.1% amplification to path techniques (1000 marks = +100%).
        /// Human Path Dao marks provide cultivation efficiency bonuses (+2% washing rate, +1% battering impact per mark).
        /// </summary>
        public float GetPathAmplificationMultiplier(DaoPathType path)
        {
            int marks = path switch
            {
                DaoPathType.HumanPath => HumanPathMarks,
                DaoPathType.MoonPath => MoonPathMarks,
                DaoPathType.StrengthPath => StrengthPathMarks,
                DaoPathType.RefinementPath => RefinementPathMarks,
                DaoPathType.EnslavementPath => EnslavementPathMarks,
                DaoPathType.BloodPath => BloodPathMarks,
                _ => 0
            };

            return 1.0f + (marks * 0.001f);
        }

        public void AddMarks(DaoPathType path, int count)
        {
            switch (path)
            {
                case DaoPathType.HumanPath: HumanPathMarks += count; break;
                case DaoPathType.MoonPath: MoonPathMarks += count; break;
                case DaoPathType.StrengthPath: StrengthPathMarks += count; break;
                case DaoPathType.RefinementPath: RefinementPathMarks += count; break;
                case DaoPathType.EnslavementPath: EnslavementPathMarks += count; break;
                case DaoPathType.BloodPath: BloodPathMarks += count; break;
            }
        }
    }
}
