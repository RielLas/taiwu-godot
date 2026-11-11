using System.Collections.Generic;
using TaiwuGu.Core;
using TaiwuGu.Entities;

namespace Taiwu_Godot.Scripts.UI
{
    /// <summary>Read-only projection of the cached event state handed to the cultivation page.</summary>
    public readonly record struct CultivationSnapshot(
        string CultivatorName,
        int Rank,
        int Substage,
        string SubstageName,
        string MembraneName,
        float AptitudePercent,
        int PrimevalStones,
        int HumanPathDaoMarks,
        int Age,
        int LifespanYears,
        int RemainingLifespanYears,
        string SeasonalAffinity,
        float CurrentSea,
        float MaxSea,
        float WallWashingProgress,
        float WallDurability,
        float MaxWallDurability);

    /// <summary>Read-only projection of the aperture's loadout and reserve.</summary>
    public readonly record struct GuWormsSnapshot(
        IReadOnlyList<GuWormComponent> Equipped,
        IReadOnlyList<GuWormComponent> Reserve);

    /// <summary>Read-only projection of the innate Human Path dao marks.</summary>
    public readonly record struct DaoMarksSnapshot(
        int HumanPathDaoMarks,
        float AptitudePercent);

    /// <summary>Read-only projection of the six Southern Border factions.</summary>
    public readonly record struct FactionsSnapshot(
        IReadOnlyList<FactionData> Factions);
}
