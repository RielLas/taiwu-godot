using System;
using TaiwuGu.Core;

namespace TaiwuGu.Psychology
{
    public enum CrimeSignature
    {
        CleanIncisions,     // Clean moonblade slashes, severed flesh, pooled blood
        ScorchedCrater,     // Blown apart by aperture self-detonation, ash, charred soil
        BeastRemains,       // Dismembered beast carcass
        FramedWildBeast,    // Forged beast claw scratches & gnawed bones
        FramedRivalClan     // Planted rival clan jade tokens / Xiong Clan blade marks
    }

    public enum CrimeTamperChoice
    {
        Cleanse,             // 焚尸灭迹: Erases somatic & ghost traces; leaves overt empty void
        FrameWildBeasts,     // 伪造兽袭: Alters clues to wild beast signatures; diverts investigators to Beast Dens
        FrameRivalClan       // 栽赃嫁祸: Leaves rival clan signatures; redirects clan war hostility
    }

    [Serializable]
    public class CrimeSceneData
    {
        public Vector2Int GridPos;
        public string VictimName = "";
        public CrimeSignature Signature;
        public int LingeringSoulQiTerms; // Default 2 Solar Terms before dissipating
        public bool IsTamperedClean;
        public string FramedFaction = "";     // Name of framed faction (if any)

        public CrimeSceneData()
        {
            LingeringSoulQiTerms = 2;
            IsTamperedClean = false;
            FramedFaction = "";
        }

        public CrimeSceneData(Vector2Int pos, string victim, CrimeSignature sig, int terms = 2)
        {
            GridPos = pos;
            VictimName = victim;
            Signature = sig;
            LingeringSoulQiTerms = terms;
            IsTamperedClean = false;
            FramedFaction = "";
        }
    }
}
