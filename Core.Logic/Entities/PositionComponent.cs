using System;
using TaiwuGu.Core;

namespace TaiwuGu.Entities
{
    /// <summary>
    /// Discrete spatial position component for entities inhabiting the tile map.
    /// </summary>
    [Serializable]
    public class PositionComponent
    {
        public GridCoord Coord { get; set; }
        public int CurrentSectorId { get; set; } = 0;

        public PositionComponent() { }
        public PositionComponent(GridCoord coord, int sectorId = 0)
        {
            Coord = coord;
            CurrentSectorId = sectorId;
        }

        public PositionComponent(int x, int y, int sectorId = 0)
        {
            Coord = new GridCoord(x, y);
            CurrentSectorId = sectorId;
        }
    }
}
