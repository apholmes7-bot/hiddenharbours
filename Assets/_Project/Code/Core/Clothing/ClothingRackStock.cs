using System;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>How many of each garment a rack has left</b> (store clothing, pass 1). Starts at the rack's
    /// authored stock; each garment runs out on its own. Session-only: the save keeps no rack count yet
    /// (docs/design/store-clothing.md, "Save"), so this is never written.
    /// </summary>
    public sealed class ClothingRackStock
    {
        private readonly ClothingDef[] _garments;
        private readonly int[] _left;

        public ClothingRackStock(ClothingRackDef rack)
        {
            Rack = rack;
            ClothingRackLine[] lines = rack != null && rack.Lines != null ? rack.Lines : Array.Empty<ClothingRackLine>();
            _garments = new ClothingDef[lines.Length];
            _left = new int[lines.Length];
            for (int i = 0; i < lines.Length; i++)
            {
                _garments[i] = lines[i].Clothing;
                _left[i] = lines[i].Stock > 0 ? lines[i].Stock : 0;
            }
        }

        /// <summary>The rack these counts belong to.</summary>
        public ClothingRackDef Rack { get; }

        /// <summary>How many lines the rack has.</summary>
        public int LineCount => _garments.Length;

        /// <summary>The garment on line <paramref name="line"/>, or null.</summary>
        public ClothingDef GarmentAt(int line) => line >= 0 && line < _garments.Length ? _garments[line] : null;

        /// <summary>True when the rack offers <paramref name="def"/> at all (in stock or not).</summary>
        public bool Stocks(ClothingDef def) => LineOf(def) >= 0;

        /// <summary>How many of <paramref name="def"/> are left; 0 when sold out or not offered.</summary>
        public int Left(ClothingDef def)
        {
            int line = LineOf(def);
            return line >= 0 ? _left[line] : 0;
        }

        /// <summary>Takes one <paramref name="def"/> off the rack. False, changing nothing, when it is not
        /// offered or sold out. Every other garment's count is untouched.</summary>
        public bool TryTakeOne(ClothingDef def)
        {
            int line = LineOf(def);
            if (line < 0 || _left[line] <= 0) return false;
            _left[line]--;
            return true;
        }

        private int LineOf(ClothingDef def)
        {
            if (def == null) return -1;
            for (int i = 0; i < _garments.Length; i++)
                if (ReferenceEquals(_garments[i], def)) return i;
            return -1;
        }
    }
}
