#if UNITY_EDITOR
using System.Collections.Generic;

namespace HiddenHarbours.App.Editor
{
    public static partial class RoomFixturePlan
    {
        /// <summary>The cream saltbox: its front door wall faces south.</summary>
        public const Compass CreamSaltboxDoor = Compass.South;

        // Kitchen on the ground floor, bathroom on the storey above.
        static IEnumerable<Row> CreamSaltboxRows()
        {
            string village = StPetersVillage.RootName;
            foreach (Row row in Kitchen("cream_saltbox", village, "creamSaltbox", "creamSaltbox", "creamSaltbox",
                                        CreamSaltboxDoor, CreamW, CreamL))
                yield return row;
            foreach (Row row in Bathroom("cream_saltbox", village, "creamSaltbox", "creamSaltboxUpper", "creamSaltbox", 1,
                                         CreamSaltboxDoor, CreamW, CreamL))
                yield return row;
        }

        static Closet CreamSaltboxCloset() => StandardCloset("cream_saltbox", 1, CreamW, CreamL);
    }
}
#endif
