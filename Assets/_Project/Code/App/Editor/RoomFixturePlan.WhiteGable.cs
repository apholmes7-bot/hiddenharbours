#if UNITY_EDITOR
using System.Collections.Generic;

namespace HiddenHarbours.App.Editor
{
    public static partial class RoomFixturePlan
    {
        /// <summary>The white gable: its front door wall faces south.</summary>
        public const Compass WhiteGableDoor = Compass.South;

        // Kitchen on the ground floor, bathroom on the storey above.
        static IEnumerable<Row> WhiteGableRows()
        {
            string village = StPetersVillage.RootName;
            foreach (Row row in Kitchen("white_gable", village, "whiteGable", "whiteGable", "whiteGable",
                                        WhiteGableDoor, WhiteGableW, WhiteGableL))
                yield return row;
            foreach (Row row in Bathroom("white_gable", village, "whiteGable", "whiteGableUpper", "whiteGable", 1,
                                         WhiteGableDoor, WhiteGableW, WhiteGableL))
                yield return row;
        }

        static Closet WhiteGableCloset() => StandardCloset("white_gable", 1, WhiteGableW, WhiteGableL);
    }
}
#endif
