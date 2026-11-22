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

        // Ginny's cottage: the player's home, so its kitchen and bathroom are both placed.
        //
        // The kitchen is the standard run on the LEFT wall, which in this cottage is the NORTH wall: prep,
        // stove, then the microwave nearest the door, in the front half between the sea chest and the front
        // wall. The north wall's back half is the village cottage's bed prop, so the run cannot go there.
        //
        // The north run is full, so the kitchen sink and the fridge stand on the FRONT (west, door) wall,
        // one each side of the doorway, each centred 0.4 m or more outside the doorway lane. The sink is north
        // of the door, between the lane and the microwave (0.225 m clear of it), so it reads as part of the run.
        // The fridge is south of the door with nothing beside it: its door hangs on its right as you face it,
        // which there is the doorway side (0.55 m to the door's edge, the rig asks 0.2 m), and the 0.75 m in
        // front of it is open floor short of the chairs. Neither backs onto a south wall.
        //
        // The bathroom stays in its closet in the back-right (south-east) corner, 2.05 m across by 2.9 m
        // along, its front end 0.1 m short of the dresser. The shower and the toilet stand against the BACK
        // wall, which is the east gable: the shower in the corner, 0.05 m off the south wall so it backs onto
        // the gable, the toilet beside it under the gable window (a low piece). The closet's run of east wall
        // is only 1.75 m (its inner partition cannot pass x -1.2, the doorway-lane limit, and the chimney
        // breast holds the middle of the gable), so the basin cannot join them there: the three planning
        // footprints need 1.85 m with no gap at all. The basin takes the toilet's old place at the front of
        // the right (south) wall, on SouthWallExceptions, until the closet can move to the north-east corner
        // (that needs the shared ground bed prop gone, a furniture change of its own). Standing just inside
        // the closet door the player reaches each piece with that piece the nearest (the toilet's seat reach
        // is 1.4 m on its prefab for this). Each piece is 0.9 m or more from the next.
        const float GinnyClosetFront = -1.10f, GinnyClosetInner = -1.25f;

        static IEnumerable<Row> GinnyCottageRows()
        {
            string root = StPetersGinnyPlot.RootName;
            foreach (Row row in Kitchen("ginny_cottage", root, StPetersGinnyPlot.CottageKey, "sageCottage",
                                        StPetersInteriors.GinnyCottagePlanKey, GinnyCottageDoor, SageW, SageL))
                yield return Placed(row);
            yield return Ginny(root, Kind.KitchenSink, new Vector2(1.75f, 3.425f), Wall.Front);
            yield return Ginny(root, Kind.Fridge, new Vector2(-1.60f, 3.385f), Wall.Front);

            yield return Ginny(root, Kind.Shower, new Vector2(-2.525f, -3.30f), Wall.Back);
            yield return Ginny(root, Kind.Toilet, new Vector2(-1.60f, -3.375f), Wall.Back);
            yield return Ginny(root, Kind.BathroomSink, new Vector2(-2.725f, -1.55f), Wall.Right);
        }

        static Row Ginny(string root, Kind k, Vector2 at, Wall wall) =>
            new Row("ginny_cottage", root, StPetersGinnyPlot.CottageKey, "sageCottage",
                    StPetersInteriors.GinnyCottagePlanKey, 0, k, at, GinnyCottageDoor, wall, placed: true);

        /// <summary>A template row, marked placed.</summary>
        static Row Placed(Row r) =>
            new Row(r.BuildingKey, r.BuildingRoot, r.Building, r.RoomKey, r.PlanKey, r.Level, r.Kind, r.At,
                    r.Door, r.Wall, placed: true);

        static Closet GinnyCottageCloset() =>
            new Closet("ginny_cottage", 0,
                       Rect.MinMaxRect(-SageW * 0.5f, -SageL * 0.5f, GinnyClosetInner, GinnyClosetFront),
                       -2.40f, -1.60f);
    }
}
#endif
