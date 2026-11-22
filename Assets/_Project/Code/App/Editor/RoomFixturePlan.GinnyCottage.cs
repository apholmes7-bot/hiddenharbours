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

        // Ginny's cottage, the R1 pilot (re-planned after its first editor look): a longer, shallower
        // closet in the back-right corner, 2.05 m across by 2.9 m along, its front end stopping 0.1 m short
        // of the dresser. Re-planned a second time from the scene's colliders (no editor): the toilet at the
        // front end of the right wall, the shower behind it, the basin on the back wall. Standing just inside
        // the closet door the player reaches each piece with that piece the nearest (the toilet's seat reach
        // is 1.4 m on its prefab for this). Each piece is 0.9 m or more from the next.
        const float GinnyClosetFront = -1.10f, GinnyClosetInner = -1.25f;

        static IEnumerable<Row> GinnyCottageRows()
        {
            string root = StPetersGinnyPlot.RootName;
            foreach (Row row in Kitchen("ginny_cottage", root, StPetersGinnyPlot.CottageKey, "sageCottage",
                                        StPetersInteriors.GinnyCottagePlanKey, GinnyCottageDoor, SageW, SageL))
                yield return row;

            // The bathroom is the placed pilot.
            yield return Ginny(root, Kind.Shower, new Vector2(-2.575f, -2.85f), Wall.Right);
            yield return Ginny(root, Kind.Toilet, new Vector2(-2.775f, -1.475f), Wall.Right);
            yield return Ginny(root, Kind.BathroomSink, new Vector2(-1.925f, -3.50f), Wall.Back);
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
