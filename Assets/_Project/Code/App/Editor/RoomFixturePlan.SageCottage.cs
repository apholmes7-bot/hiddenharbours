#if UNITY_EDITOR
using System.Collections.Generic;

namespace HiddenHarbours.App.Editor
{
    public static partial class RoomFixturePlan
    {
        /// <summary>The village sage cottage: its front door wall faces south.</summary>
        public const Compass SageCottageDoor = Compass.South;

        static IEnumerable<Row> SageCottageRows()
        {
            string village = StPetersVillage.RootName;
            foreach (Row row in Kitchen("sage_cottage", village, "sageCottage", "sageCottage", null, SageCottageDoor, SageW, SageL))
                yield return row;
            foreach (Row row in Bathroom("sage_cottage", village, "sageCottage", "sageCottage", null, 0, SageCottageDoor, SageW, SageL))
                yield return row;
        }

        static Closet SageCottageCloset() => StandardCloset("sage_cottage", 0, SageW, SageL);
    }
}
#endif
