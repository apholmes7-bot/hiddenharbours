#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    public static partial class RoomFixturePlan
    {
        /// <summary>Ginny's cottage (the sage room under the Ginny plot, not turned with the village):
        /// its front door wall faces west.</summary>
        public const Compass GinnyCottageDoor = Compass.West;

        // Ginny's cottage: the player's home, so its kitchen and bathroom are both placed. Laid out as three
        // ground-floor rooms after the owner's look (2026-11-22: "there should be maybe three rooms on the first
        // floor, the entry/living room area, the kitchen and the bathroom"). The front (+y) half is the
        // living room the door opens into; a partition at y -0.3..0 closes off the back half, and an east-west
        // partition at x -1.25..-0.95 splits that into the kitchen (north) and the washroom (south). The walls
        // are StPetersInteriors.UpperLevelFor("ginnyCottage").GroundWalls; each room has its own doorway off
        // the living room (the kitchen's at x -0.55..0.65, straight ahead from the front door; the washroom's
        // at x -2.75..-1.55).
        //
        // THE KITCHEN: the cooking line runs along the LEFT (north) wall from the partition: the tall fridge
        // first (nothing stands north of it to hide), then the prep counter, the kitchen sink and the stove.
        // The microwave's low cabinet stands on the BACK (east) wall at the kitchen's south end, well down the
        // screen from the line. The press reaches from his transform, 0.7 m up the screen from his feet, so a
        // piece on the back wall any closer under the line loses to the line above it when he stands at its
        // front; and it stays far enough from the washroom partition that nothing in the washroom offers it.
        //
        // THE WASHROOM: the shower stands on the BACK (east) wall, 0.3 m off the south wall and clear of the east
        // gable window, opening west; the toilet and then the basin stand along the RIGHT (south) wall towards
        // the door, with a walking gap between the toilet and the shower. The shower's solid tray and panel are
        // low; above them it is a glass screen in a thin frame, which is all that rises over the kitchen's
        // south end behind the partition. A piece against the south wall is pressed from beside it or from a
        // step north of it (the press reaches from his transform, 0.7 m up the screen from his feet), which is
        // why the toilet keeps clear of the shower: standing at the shower's front, the shower is the nearer.
        const float GinnyWashroomFront = -0.30f, GinnyWashroomInner = -1.25f;
        const float GinnyWashroomDoorFrom = -2.75f, GinnyWashroomDoorTo = -1.55f;

        static IEnumerable<Row> GinnyCottageRows()
        {
            string root = StPetersGinnyPlot.RootName;
            yield return Ginny(root, Kind.PrepCounter, new Vector2(2.70f, -1.47f), Wall.Left);
            yield return Ginny(root, Kind.Stove, new Vector2(2.70f, -3.295f), Wall.Left);
            yield return Ginny(root, Kind.Microwave, new Vector2(-0.30f, -3.50f), Wall.Back);
            yield return Ginny(root, Kind.KitchenSink, new Vector2(2.70f, -2.42f), Wall.Left);
            yield return Ginny(root, Kind.Fridge, new Vector2(2.66f, -0.67f), Wall.Left);

            yield return Ginny(root, Kind.Shower, new Vector2(-2.275f, -3.30f), Wall.Back);
            yield return Ginny(root, Kind.Toilet, new Vector2(-2.775f, -1.775f), Wall.Right);
            yield return Ginny(root, Kind.BathroomSink, new Vector2(-2.725f, -1.15f), Wall.Right);
        }

        static Row Ginny(string root, Kind k, Vector2 at, Wall wall) =>
            new Row("ginny_cottage", root, StPetersGinnyPlot.CottageKey, "sageCottage",
                    StPetersInteriors.GinnyCottagePlanKey, 0, k, at, GinnyCottageDoor, wall, placed: true);

        static Closet GinnyCottageCloset() =>
            new Closet("ginny_cottage", 0,
                       Rect.MinMaxRect(-SageW * 0.5f, -SageL * 0.5f, GinnyWashroomInner, GinnyWashroomFront),
                       GinnyWashroomDoorFrom, GinnyWashroomDoorTo, doorInFront: true);
    }
}
#endif
