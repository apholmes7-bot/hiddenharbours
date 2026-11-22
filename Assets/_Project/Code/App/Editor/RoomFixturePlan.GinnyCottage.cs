#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    public static partial class RoomFixturePlan
    {
        /// <summary>Ginny's cottage (her own 7.2 × 9.1 m room under the Ginny plot, not turned with the
        /// village): its front door wall faces west.</summary>
        public const Compass GinnyCottageDoor = Compass.West;

        // Ginny's cottage: the player's home, so its kitchen and bathroom are both placed. Laid out as three
        // ground-floor rooms after the owner's look (2026-11-22: "there should be maybe three rooms on the first
        // floor, the entry/living room area, the kitchen and the bathroom"), in a house enlarged to 7.2 × 9.1 m
        // so the stair runs along the north wall and the washroom has real depth. The front (+y) half is the
        // living room the door opens into; a partition at y 0..0.3 closes off the back half, and an east-west
        // partition at x -0.6..-0.3 splits that into the kitchen (north) and the washroom (south). The walls
        // are StPetersInteriors.UpperLevelFor("ginnyCottage").GroundWalls; each room has its own doorway off
        // the living room (the kitchen's at x -0.1..1.25, the washroom's at x -2.45..-0.95: each wider than he is, whose
        // round footing spans 1.09 m north-south once the ground's depth scale is undone).
        //
        // THE KITCHEN: the whole line runs along the LEFT (north) wall from the partition: the tall fridge
        // first (nothing stands north of it to hide), then the prep counter, the kitchen sink, the stove and,
        // in the back corner, the microwave's low cabinet. Nothing stands on the back wall, so nothing in the
        // washroom behind the partition rises in front of any of it.
        //
        // THE WASHROOM: the shower stands on the BACK (east) wall in the south-east corner, opening west; the
        // toilet and then the basin stand along the RIGHT (south) wall towards the door, with a walking gap
        // between the toilet and the shower. 2.7 m deep from the south wall to the partition, so he stands
        // clear of all three with floor to spare. The shower's solid tray and panel are low; above them it is
        // a glass screen in a thin frame. A piece against the south wall is pressed from beside it or from a
        // step north of it (the press reaches from his transform, 0.7 m up the screen from his feet), which is
        // why the toilet keeps clear of the shower: standing at the shower's front, the shower is the nearer.
        const float GinnyWashroomFront = 0.00f, GinnyWashroomInner = -0.60f;
        const float GinnyWashroomDoorFrom = -2.45f, GinnyWashroomDoorTo = -0.95f;

        static IEnumerable<Row> GinnyCottageRows()
        {
            string root = StPetersGinnyPlot.RootName;
            yield return Ginny(root, Kind.PrepCounter, new Vector2(3.00f, -1.25f), Wall.Left);
            yield return Ginny(root, Kind.Stove, new Vector2(3.00f, -3.175f), Wall.Left);
            yield return Ginny(root, Kind.Microwave, new Vector2(3.075f, -3.85f), Wall.Left);
            yield return Ginny(root, Kind.KitchenSink, new Vector2(3.00f, -2.25f), Wall.Left);
            yield return Ginny(root, Kind.Fridge, new Vector2(2.96f, -0.40f), Wall.Left);

            yield return Ginny(root, Kind.Shower, new Vector2(-2.575f, -3.825f), Wall.Back);
            yield return Ginny(root, Kind.Toilet, new Vector2(-3.075f, -2.40f), Wall.Right);
            yield return Ginny(root, Kind.BathroomSink, new Vector2(-3.025f, -1.50f), Wall.Right);
        }

        static Row Ginny(string root, Kind k, Vector2 at, Wall wall) =>
            new Row("ginny_cottage", root, StPetersGinnyPlot.CottageKey, StPetersInteriors.GinnyCottagePlanKey,
                    StPetersInteriors.GinnyCottagePlanKey, 0, k, at, GinnyCottageDoor, wall, placed: true);

        static Closet GinnyCottageCloset() =>
            new Closet("ginny_cottage", 0,
                       Rect.MinMaxRect(-GinnyW * 0.5f, -GinnyL * 0.5f, GinnyWashroomInner, GinnyWashroomFront),
                       GinnyWashroomDoorFrom, GinnyWashroomDoorTo, doorInFront: true);
    }
}
#endif
