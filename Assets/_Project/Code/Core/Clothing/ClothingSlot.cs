namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Where a garment is worn</b> (store clothing, pass 1). One garment per slot. Unity serializes an
    /// enum as its integer, so a <see cref="ClothingDef"/> asset stores the number: values are append-only
    /// and a shipped value is never renumbered or reused.
    /// </summary>
    public enum ClothingSlot
    {
        Head = 0,
        Top = 1,
        Legs = 2,
        Feet = 3,
        Outerwear = 4,
        Hands = 5,
    }

    /// <summary>The slot list in the order the try-on page lists it and the look is layered (outermost
    /// last, so an outer garment's stand-in colours win over the layer beneath).</summary>
    public static class ClothingSlots
    {
        /// <summary>How many slots there are; one past the largest <see cref="ClothingSlot"/> value.</summary>
        public const int Count = 6;

        /// <summary>Head, top, legs, feet, outerwear, hands.</summary>
        public static readonly ClothingSlot[] Order =
        {
            ClothingSlot.Head, ClothingSlot.Top, ClothingSlot.Legs,
            ClothingSlot.Feet, ClothingSlot.Outerwear, ClothingSlot.Hands,
        };

        /// <summary>True for a slot this build knows (a hand-edited asset can hold any integer).</summary>
        public static bool IsValid(ClothingSlot slot) => (int)slot >= 0 && (int)slot < Count;

        /// <summary>The slot's place in <see cref="Order"/>, or -1 for an unknown slot.</summary>
        public static int OrderOf(ClothingSlot slot)
        {
            for (int i = 0; i < Order.Length; i++)
                if (Order[i] == slot) return i;
            return -1;
        }
    }
}
