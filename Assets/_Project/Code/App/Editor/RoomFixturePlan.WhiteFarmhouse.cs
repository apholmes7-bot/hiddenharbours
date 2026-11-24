#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    public static partial class RoomFixturePlan
    {
        /// <summary>The white farmhouse, set on a diagonal: its front door wall faces south-west.</summary>
        public const Compass WhiteFarmhouseDoor = Compass.SouthWest;

        // The farmhouse is a family house: a bath along the back wall instead of a shower, so its closet is
        // wider and deeper (2.3 x 2.6 m) and the toilet and basin stand along the right wall.
        const float FarmClosetWidth = 2.30f, FarmClosetDepth = 2.60f;

        static IEnumerable<Row> WhiteFarmhouseRows()
        {
            foreach (Row row in Kitchen("white_farmhouse", StPetersVillage.RootName, "whiteFarmhouse", "whiteFarmhouse",
                                        null, WhiteFarmhouseDoor, FarmW, FarmL))
                yield return row;

            float hw = FarmW * 0.5f, hl = FarmL * 0.5f;
            // The bath stands toward the closet's inner end, so the floor in front of its middle is clear of
            // the toilet; the basin's back is flush with the right wall (it is 0.45 m deep).
            yield return Farm(Kind.Bath, new Vector2(-hw + 1.40f, -hl + 0.70f), Wall.Back);
            yield return Farm(Kind.Toilet, new Vector2(-hw + 0.65f, -hl + 1.55f), Wall.Right);
            yield return Farm(Kind.BathroomSink, new Vector2(-hw + 0.525f, -hl + 2.15f), Wall.Right);
        }

        static Row Farm(Kind k, Vector2 at, Wall wall) =>
            new Row("white_farmhouse", StPetersVillage.RootName, "whiteFarmhouse", "whiteFarmhouse", null, 0, k, at,
                    WhiteFarmhouseDoor, wall);

        static Closet WhiteFarmhouseCloset() =>
            new Closet("white_farmhouse", 0,
                       new Rect(-FarmW * 0.5f, -FarmL * 0.5f, FarmClosetWidth, FarmClosetDepth),
                       -FarmL * 0.5f + 1.40f, -FarmL * 0.5f + 2.20f);
    }
}
#endif
