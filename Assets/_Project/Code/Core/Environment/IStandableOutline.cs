using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Where a standable deck is DRAWN</b>, for the presentation code that needs the picture rather than
    /// the footing: the calm mirror (GameConfig.WaterMirrorStrength) asks it so a deck top, which faces the
    /// sky, is never mirrored in the water, and the face below it is mirrored about the real waterline.
    ///
    /// <para>An optional companion of <see cref="IStandableSurface"/>: a surface that does not implement it
    /// simply has no outline, and nothing about walking changes either way. The outline is the deck's top as
    /// it appears on screen, in world units, which is not always the footprint: the kit-built wharves and
    /// floats draw their deck <see cref="IsoGround.HeightScale"/> x its elevation up the screen of the plan
    /// footprint, while a cell-built pier is drawn where its footprint is. Each surface knows which, so each
    /// answers for itself.</para>
    ///
    /// <para>Presentation only: never read by the simulation, never saved. No allocation: the caller owns
    /// the buffers.</para>
    /// </summary>
    public interface IStandableOutline
    {
        /// <summary>
        /// Write the deck top's DRAWN outline (a convex polygon, world units, corners in order) into
        /// <paramref name="drawn"/>, with the deck's elevation (metres above chart datum) at each corner in
        /// <paramref name="deckElevation"/>. Returns how many corners were written (0 = no outline right now,
        /// at most the buffers' length).
        /// </summary>
        int GetDrawnOutline(Vector2[] drawn, float[] deckElevation);
    }

    /// <summary>The outline arithmetic every <see cref="IStandableOutline"/> shares, pure for EditMode tests.</summary>
    public static class StandableOutline
    {
        /// <summary>How far up the screen a deck at <paramref name="deckElevation"/> is drawn from its plan
        /// footprint, on the kit's convention (elevation above chart datum x <see cref="IsoGround.HeightScale"/>).</summary>
        public static float Lift(float deckElevation) => deckElevation * IsoGround.HeightScale;

        /// <summary>A plan rectangle's four corners (south-west, south-east, north-east, north-west), each
        /// lifted by <paramref name="lift"/>, all at <paramref name="deckElevation"/>. Returns the count written.</summary>
        public static int Rect(Rect plan, float lift, float deckElevation, Vector2[] drawn, float[] elevation)
        {
            if (drawn == null || elevation == null || drawn.Length < 4 || elevation.Length < 4) return 0;
            drawn[0] = new Vector2(plan.xMin, plan.yMin + lift);
            drawn[1] = new Vector2(plan.xMax, plan.yMin + lift);
            drawn[2] = new Vector2(plan.xMax, plan.yMax + lift);
            drawn[3] = new Vector2(plan.xMin, plan.yMax + lift);
            elevation[0] = elevation[1] = elevation[2] = elevation[3] = deckElevation;
            return 4;
        }

        /// <summary>A brow from <paramref name="from"/> to <paramref name="to"/> (plan) of half-width
        /// <paramref name="halfWidth"/>, each end lifted on the kit's convention by its own deck elevation.</summary>
        public static int Segment(Vector2 from, float fromElevation, Vector2 to, float toElevation, float halfWidth,
                                  Vector2[] drawn, float[] elevation)
        {
            if (drawn == null || elevation == null || drawn.Length < 4 || elevation.Length < 4) return 0;
            Vector2 span = to - from;
            if (span.sqrMagnitude <= 1e-6f) return 0;
            Vector2 side = new Vector2(-span.y, span.x).normalized * Mathf.Max(0f, halfWidth);
            Vector2 liftFrom = new Vector2(0f, Lift(fromElevation)), liftTo = new Vector2(0f, Lift(toElevation));
            drawn[0] = from - side + liftFrom; elevation[0] = fromElevation;
            drawn[1] = to - side + liftTo; elevation[1] = toElevation;
            drawn[2] = to + side + liftTo; elevation[2] = toElevation;
            drawn[3] = from + side + liftFrom; elevation[3] = fromElevation;
            return 4;
        }
    }
}
