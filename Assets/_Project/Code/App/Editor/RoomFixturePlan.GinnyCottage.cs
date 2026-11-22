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

        // Ginny's cottage: the player's home, so its kitchen and bathroom are both placed. Re-laid out after the
        // owner's look (2026-11-22: "crowded ... make it feel more homelike"; the north wall is no longer a
        // strict rule, a piece goes on whichever wall reads best). Three corners and walking room between:
        //
        // THE KITCHEN CORNER is the north-west one, left of the door as you come in. The cooking line runs
        // along the LEFT (north) wall from the front corner, every piece seen from the front: the prep counter
        // in the corner, the kitchen sink under the front window, the stove last, 0.25 m short of the sea
        // chest. Standing at any piece's front, that piece is the one offered: the press reaches from his
        // transform, 0.7 m up the screen from his feet, so a piece on a side wall north of the doorway would
        // lose to the corner counter above it. That is why the cold corner is the south-west one, across the
        // doorway: the tall fridge in the corner on the front (west) wall, its top well clear of the doorway on
        // screen, and the microwave's low cabinet round the corner on the south wall, east of the fridge door's
        // swing. (Beside the fridge on the front wall the fridge's picture drew over it.) Nothing is offered
        // from the doorway.
        //
        // THE TABLE AND ITS TWO CHAIRS (the shared furniture) are the sitting and eating middle, with open floor
        // between them and the kitchen line, the doorway lane and the cold corner.
        //
        // THE WASHROOM is the closet in the back-right (south-east) corner, 2.05 m across by 2.9 m along, its
        // front end 0.1 m short of the dresser; the room sheet draws its partition. All three pieces stand along
        // the RIGHT (south) wall: the shower in the south-east corner (opening north, clear of the east gable
        // window), then the toilet, then the basin by the closet's front end. The tall shower is beside the
        // toilet, never in front of it, and the toilet stands clear of the shower's opening.
        // Each piece is 0.9 m or more from the next; standing just inside the closet door the player reaches
        // each piece with that piece the nearest (the toilet's seat reach is 1.4 m on its prefab).
        const float GinnyClosetFront = -1.10f, GinnyClosetInner = -1.25f;

        static IEnumerable<Row> GinnyCottageRows()
        {
            string root = StPetersGinnyPlot.RootName;
            yield return Ginny(root, Kind.PrepCounter, new Vector2(2.70f, 3.225f), Wall.Left);
            yield return Ginny(root, Kind.Stove, new Vector2(2.70f, 1.275f), Wall.Left);
            yield return Ginny(root, Kind.Microwave, new Vector2(-2.775f, 1.95f), Wall.Right);
            yield return Ginny(root, Kind.KitchenSink, new Vector2(2.70f, 2.225f), Wall.Left);
            yield return Ginny(root, Kind.Fridge, new Vector2(-2.62f, 3.385f), Wall.Front);

            yield return Ginny(root, Kind.Shower, new Vector2(-2.575f, -3.20f), Wall.Right);
            yield return Ginny(root, Kind.Toilet, new Vector2(-2.775f, -2.30f), Wall.Right);
            yield return Ginny(root, Kind.BathroomSink, new Vector2(-2.725f, -1.40f), Wall.Right);
        }

        static Row Ginny(string root, Kind k, Vector2 at, Wall wall) =>
            new Row("ginny_cottage", root, StPetersGinnyPlot.CottageKey, "sageCottage",
                    StPetersInteriors.GinnyCottagePlanKey, 0, k, at, GinnyCottageDoor, wall, placed: true);

        static Closet GinnyCottageCloset() =>
            new Closet("ginny_cottage", 0,
                       Rect.MinMaxRect(-SageW * 0.5f, -SageL * 0.5f, GinnyClosetInner, GinnyClosetFront),
                       -2.40f, -1.60f);
    }
}
#endif
