#if UNITY_EDITOR
using System.Collections.Generic;

namespace HiddenHarbours.App.Editor
{
    public static partial class RoomFixturePlan
    {
        /// <summary>The red saltbox: its front door wall faces west.</summary>
        public const Compass RedSaltboxDoor = Compass.West;

        // Its furniture already fills the back-right corner, so the closet is the front-right one.
        static IEnumerable<Row> RedSaltboxRows()
        {
            string village = StPetersVillage.RootName;
            foreach (Row row in Kitchen("red_saltbox", village, "redSaltbox", "redSaltbox", null, RedSaltboxDoor, RedW, RedL))
                yield return row;
            foreach (Row row in Bathroom("red_saltbox", village, "redSaltbox", "redSaltbox", null, 0, RedSaltboxDoor, RedW, RedL, front: true))
                yield return row;
        }

        static Closet RedSaltboxCloset() => StandardCloset("red_saltbox", 0, RedW, RedL, front: true);
    }
}
#endif
