#if UNITY_EDITOR
using System.Collections.Generic;

namespace HiddenHarbours.App.Editor
{
    public static partial class RoomFixturePlan
    {
        /// <summary>The school: its front door wall faces south.</summary>
        public const Compass SchoolDoor = Compass.South;

        // A staff kitchenette (prep and a microwave, no stove: the hearth heats the room, it is not a
        // cooker) and no indoor bathroom. The one-room school gets an outhouse outside, placed by the
        // outhouse slice, not by this table.
        static IEnumerable<Row> SchoolRows() =>
            Kitchen("school", StPetersVillage.RootName, "school", "school", null, SchoolDoor, SchoolW, SchoolL, stove: false);
    }
}
#endif
