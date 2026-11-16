using System;
using System.Collections.Generic;
using TaiwuGu.Core;

namespace TaiwuGu.Entities
{
    /// <summary>
    /// One persisted merchant lot. Fodder is sold in bundles, Gu worms are minted
    /// as first-class entities on purchase, and rough ore has unlimited stock.
    /// </summary>
    [Serializable]
    public class CaravanStockEntry
    {
        public string Id { get; set; } = string.Empty;
        public CaravanStockKind Kind { get; set; }
        public string GuId { get; set; } = string.Empty;
        public GuDietType Diet { get; set; }
        public int Rank { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public int BaseUnitPrice { get; set; }
        public int Quantity { get; set; }
        public int Remaining { get; set; }
        public bool IsSeasonal { get; set; }
        public CaravanOreTier OreTier { get; set; }
    }

    /// <summary>
    /// Additive v3.0.0 world singleton. The current term's stock and the gambling
    /// sequence both persist so a reload cannot reroll the merchant or a cut.
    /// </summary>
    [Serializable]
    public class CaravanSessionComponent
    {
        public int RestockYear { get; set; }
        public int RestockTerm { get; set; }
        public string SectorKey { get; set; } = string.Empty;
        public int StockSeed { get; set; }
        public bool HasStock { get; set; }
        public int GambleSequence { get; set; }
        public List<CaravanStockEntry> Stock { get; set; } = new();
    }
}
