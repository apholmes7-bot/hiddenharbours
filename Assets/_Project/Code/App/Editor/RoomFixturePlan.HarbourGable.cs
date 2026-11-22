#if UNITY_EDITOR
using System.Collections.Generic;

namespace HiddenHarbours.App.Editor
{
    public static partial class RoomFixturePlan
    {
        /// <summary>The harbour gable: its front door wall faces south.</summary>
        public const Compass HarbourGableDoor = Compass.South;

        // Kitchen on the ground floor, bathroom on the storey above.
        static IEnumerable<Row> HarbourGableRows()
        {
            string village = StPetersVillage.RootName;
            foreach (Row row in Kitchen("harbour_gable", village, "harbourGable", "harbourGable", "harbourGable",
                                        HarbourGableDoor, HarbourW, HarbourL))
                yield return row;
            foreach (Row row in Bathroom("harbour_gable", village, "harbourGable", "harbourGableUpper", "harbourGable", 1,
                                         HarbourGableDoor, HarbourW, HarbourL))
                yield return row;
        }

        static Closet HarbourGableCloset() => StandardCloset("harbour_gable", 1, HarbourW, HarbourL);
    }
}
#endif
