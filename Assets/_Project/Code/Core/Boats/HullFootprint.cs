using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// ⭐ <b>A hull's PLAN OUTLINE — the rectangle her length and beam actually cover, lying on the
    /// heading she actually lies on.</b> Pure, static, deterministic, NaN-safe.
    ///
    /// <para><b>Why this exists.</b> Clearance between two boats was being measured centre-to-centre
    /// with a single radius each — a hull as a CIRCLE. That model cannot see the thing a skipper's eye
    /// sees first: a 12.9 m hull swinging onto a berth sweeps her bow and stern through water her
    /// centre never enters, and a 4.5 m dory lying athwart a fairway reaches into it with her stern
    /// while her centre sits politely to one side. A circle of the half-beam understates such a hull by
    /// (length/2 − half-beam) in the one direction that matters. Every "she passes clear" number this
    /// repo printed was therefore measured on a boat that does not exist.</para>
    ///
    /// <para><b>WORLD metres, deliberately un-projected.</b> This is a question about water — does this
    /// hull reach into the place that one occupies — so it is answered in the frame the physics and the
    /// seabed live in, not the frame the artwork is drawn in. The ¾ squash is a PER-ARTWORK drawing
    /// concern (ADR 0042) and applying it here would shorten a hull along the view axis and report
    /// clearance she has not got. Un-projected is also the conservative reading: it is the longer boat.
    /// Call sites that want the drawn silhouette project the RESULT; they do not project the hull.</para>
    ///
    /// <para><b>The parameters are the repo's own two spellings, on purpose.</b> Length comes whole
    /// (<c>BoatHullDef.LengthMeters</c>) and beam comes halved (<c>HullMeshDef.WatertightHalfBeamMeters</c>,
    /// <c>StPetersBuilder.ArrivalHullHalfBeamMetres</c>) because that is how they are authored. Taking
    /// them in the shape they are stored in means no call site does arithmetic on the way in, which is
    /// where a factor of two goes missing.</para>
    /// </summary>
    public readonly struct HullFootprint
    {
        /// <summary>Where her centre lies, in world metres.</summary>
        public readonly Vector2 Center;

        /// <summary>Unit vector along the keel, toward the bow. (A 2D hull's bow is <c>transform.up</c>.)</summary>
        public readonly Vector2 BowDirection;

        /// <summary>Half her length, along the keel.</summary>
        public readonly float HalfLength;

        /// <summary>Half her beam, abeam.</summary>
        public readonly float HalfBeam;

        private HullFootprint(Vector2 center, Vector2 bowDirection, float halfLength, float halfBeam)
        {
            Center = center;
            BowDirection = bowDirection;
            HalfLength = halfLength;
            HalfBeam = halfBeam;
        }

        // ---- construction ---------------------------------------------------------------------------

        /// <summary>
        /// Her outline from the direction her bow points — the primitive, because a direction vector
        /// cannot be read in the wrong angular convention. A degenerate or non-finite direction falls
        /// back to NORTH, which is the identity rotation a builder-placed hull carries.
        /// </summary>
        public static HullFootprint FromBowDirection(Vector2 center, Vector2 bowDirection,
                                                     float lengthMeters, float halfBeamMeters)
        {
            Vector2 c = Safe(center);
            Vector2 bow = Safe(bowDirection);
            float mag = bow.magnitude;
            bow = mag > 1e-6f ? bow / mag : new Vector2(0f, 1f);
            return new HullFootprint(c, bow,
                                     Mathf.Max(0f, Safe(lengthMeters) * 0.5f),
                                     Mathf.Max(0f, Safe(halfBeamMeters)));
        }

        /// <summary>
        /// Her outline from a COMPASS heading — 0 = north, 90 = east, clockwise: the convention
        /// <c>ArrivalPilot.CompassOf</c> and <c>DeckAreaMath.DeckToWorld</c> both speak.
        /// </summary>
        public static HullFootprint FromHeading(Vector2 center, float compassHeadingDegrees,
                                                float lengthMeters, float halfBeamMeters)
        {
            float rad = Safe(compassHeadingDegrees) * Mathf.Deg2Rad;
            return FromBowDirection(center, new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)),
                                    lengthMeters, halfBeamMeters);
        }

        // ---- the outline itself ---------------------------------------------------------------------

        /// <summary>Unit vector abeam to STARBOARD — the bow turned 90° clockwise.</summary>
        public Vector2 StarboardDirection => new Vector2(BowDirection.y, -BowDirection.x);

        /// <summary>The point of her bow.</summary>
        public Vector2 BowPoint => Center + BowDirection * HalfLength;

        /// <summary>The point of her stern.</summary>
        public Vector2 SternPoint => Center - BowDirection * HalfLength;

        /// <summary>Her four corners, bow-starboard first and then round. </summary>
        public Vector2 Corner(int index)
        {
            Vector2 along = BowDirection * HalfLength;
            Vector2 abeam = StarboardDirection * HalfBeam;
            switch (((index % 4) + 4) % 4)
            {
                case 0:  return Center + along + abeam;   // starboard bow
                case 1:  return Center - along + abeam;   // starboard quarter
                case 2:  return Center - along - abeam;   // port quarter
                default: return Center + along - abeam;   // port bow
            }
        }

        /// <summary>Where a world point falls in her own frame: x abeam to starboard, y along the keel
        /// toward the bow. The one place the rotation is undone, so no caller repeats it.</summary>
        public Vector2 ToHullFrame(Vector2 worldPoint)
        {
            Vector2 d = Safe(worldPoint) - Center;
            return new Vector2(Vector2.Dot(d, StarboardDirection), Vector2.Dot(d, BowDirection));
        }

        /// <summary>Is this world point on or inside her outline?</summary>
        public bool Contains(Vector2 worldPoint)
        {
            Vector2 local = ToHullFrame(worldPoint);
            return Mathf.Abs(local.x) <= HalfBeam && Mathf.Abs(local.y) <= HalfLength;
        }

        /// <summary>
        /// Distance from a world point to her outline, in metres — <b>0 anywhere inside her</b>. This is
        /// the number "within reach of the hull" wants, as opposed to the distance to her root, which a
        /// 12.9 m boat makes meaningless from her own stern.
        /// </summary>
        public float DistanceTo(Vector2 worldPoint)
        {
            Vector2 local = ToHullFrame(worldPoint);
            float outAbeam = Mathf.Abs(local.x) - HalfBeam;
            float outAlong = Mathf.Abs(local.y) - HalfLength;
            if (outAbeam <= 0f && outAlong <= 0f) return 0f;
            float a = Mathf.Max(outAbeam, 0f), b = Mathf.Max(outAlong, 0f);
            return Mathf.Sqrt(a * a + b * b);
        }

        /// <summary>The nearest point ON her outline (or the query point itself, if it is inside).</summary>
        public Vector2 ClosestPoint(Vector2 worldPoint)
        {
            Vector2 local = ToHullFrame(worldPoint);
            float x = Mathf.Clamp(local.x, -HalfBeam, HalfBeam);
            float y = Mathf.Clamp(local.y, -HalfLength, HalfLength);
            return Center + StarboardDirection * x + BowDirection * y;
        }

        // ---- hull against hull ------------------------------------------------------------------------

        /// <summary>
        /// ⭐ <b>The clear water between two hulls</b>, in metres: positive is the gap between their
        /// outlines, <b>negative is how deep they are INTO each other</b> along the axis that separates
        /// them least. Signed on purpose — a test that clamps at zero can say "they touched" but not
        /// "she was a metre and a half through her", and the second is the sentence that gets a berth
        /// moved.
        ///
        /// <para>Exact for two rectangles. Overlap is decided by the separating-axis theorem over the
        /// four edge normals (which, for boxes, are the only axes that can separate them); the gap for
        /// a disjoint pair is the true minimum, because the closest pair of points between two disjoint
        /// convex polygons always has a VERTEX at one end.</para>
        /// </summary>
        public float SignedGapTo(in HullFootprint other)
        {
            // Separating-axis pass: the largest per-axis gap. > 0 on any axis ⇒ disjoint.
            float widest = float.NegativeInfinity;
            for (int i = 0; i < 4; i++)
            {
                Vector2 axis = i == 0 ? BowDirection
                             : i == 1 ? StarboardDirection
                             : i == 2 ? other.BowDirection
                                      : other.StarboardDirection;
                float gap = Mathf.Abs(Vector2.Dot(other.Center - Center, axis))
                            - Radius(axis) - other.Radius(axis);
                if (gap > widest) widest = gap;
            }
            if (widest <= 0f) return widest;      // overlapping: the depth, exact for boxes

            // Disjoint: the true minimum, vertex against the other outline, both ways.
            float best = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                float d = other.DistanceTo(Corner(i));
                if (d < best) best = d;
                d = DistanceTo(other.Corner(i));
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>The clear water between two hulls, floored at 0 where they overlap.</summary>
        public float DistanceTo(in HullFootprint other) => Mathf.Max(0f, SignedGapTo(other));

        /// <summary>Half her extent projected onto an axis — the SAT support radius.</summary>
        private float Radius(Vector2 axis) =>
            HalfLength * Mathf.Abs(Vector2.Dot(axis, BowDirection))
            + HalfBeam * Mathf.Abs(Vector2.Dot(axis, StarboardDirection));

        // ---- NaN discipline (the repo's house rule for pure math) ------------------------------------

        private static float Safe(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0f : v;
        private static Vector2 Safe(Vector2 v) => new Vector2(Safe(v.x), Safe(v.y));

        public override string ToString() =>
            $"hull {HalfLength * 2f:F2} m × {HalfBeam * 2f:F2} m at ({Center.x:F2}, {Center.y:F2}) " +
            $"heading {Mathf.Atan2(BowDirection.x, BowDirection.y) * Mathf.Rad2Deg:F0}°";
    }
}
