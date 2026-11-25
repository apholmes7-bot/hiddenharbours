#if UNITY_EDITOR
using System.Collections.Generic;

namespace HiddenHarbours.App.Editor
{
    public static partial class RoomFixturePlan
    {
        /// <summary>The harbour gable: its front door wall faces south.</summary>
        public const Compass HarbourGableDoor = Compass.South;

        /// <summary>The harbour gable's plan frame. Its door is in the cross gable on the long (+X) eave
        /// wall, so the frame with the door at <c>+y</c> is the room turned a quarter: the room's
        /// length runs across it and its width runs front to back (InteriorFrame, and the serialized
        /// BuildingInterior the placement step reads).</summary>
        public const float HarbourFrameW = HarbourL, HarbourFrameL = HarbourW;

        // Kitchen on the ground floor, bathroom on the storey above.
        static IEnumerable<Row> HarbourGableRows()
        {
            string village = StPetersVillage.RootName;
            foreach (Row row in Kitchen("harbour_gable", village, "harbourGable", "harbourGable", "harbourGable",
                                        HarbourGableDoor, HarbourFrameW, HarbourFrameL))
                yield return row;
            foreach (Row row in Bathroom("harbour_gable", village, "harbourGable", "harbourGableUpper", "harbourGable", 1,
                                         HarbourGableDoor, HarbourFrameW, HarbourFrameL))
                yield return row;
        }

        static Closet HarbourGableCloset() => StandardCloset("harbour_gable", 1, HarbourFrameW, HarbourFrameL);
    }
}
#endif
