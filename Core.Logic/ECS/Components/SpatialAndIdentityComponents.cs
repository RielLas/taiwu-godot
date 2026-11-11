using System;
using TaiwuGu.Core;

namespace TaiwuGu.Entities
{
    [Serializable]
    public class PositionComponent
    {
        public GridCoord Coord { get; set; }
        public string SectorKey { get; set; } = string.Empty;

        public PositionComponent() { }

        public PositionComponent(GridCoord coord, string sectorKey = "")
        {
            Coord = coord;
            SectorKey = sectorKey;
        }

        public PositionComponent(int x, int y, string sectorKey = "")
        {
            Coord = new GridCoord(x, y);
            SectorKey = sectorKey;
        }
    }

    [Serializable]
    public class PlayerTagComponent
    {
        public bool IsPlayer { get; set; } = true;
    }

    [Serializable]
    public class OwnerComponent
    {
        public ulong OwnerEntityId { get; set; }
        public bool IsEquipped { get; set; }
        public int EquipSlot { get; set; } = -1;

        public OwnerComponent() { }

        public OwnerComponent(ulong ownerEntityId, bool isEquipped = false, int equipSlot = -1)
        {
            OwnerEntityId = ownerEntityId;
            IsEquipped = isEquipped;
            EquipSlot = equipSlot;
        }
    }
}
