using System;
using System.Collections.Generic;
using TaiwuGu.Core;

namespace TaiwuGu.Core
{
    public static class CombatContextProvider
    {
        public static Func<CultivatorData>? CultivatorResolver;
        public static Func<IReadOnlyList<GuWormData>>? GuResolver;
    }

    public static class StateContextProvider
    {
        public static Func<GameStateContext>? ContextResolver;
    }

    public static class MapContextProvider
    {
        public static Func<WorldMapState>? WorldMapResolver;
    }

    public static class FactionContextProvider
    {
        public static Func<WorldFactionState>? FactionResolver;
    }
}
