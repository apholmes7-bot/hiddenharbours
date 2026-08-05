using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// What a stretch of shoreline IS — the sector vocabulary the coast is authored in, and the one
    /// discriminator <see cref="TidalTerrain.IslandProfile(float, CoastClass)"/> switches its height
    /// profile on.
    ///
    /// <para><b>A NAMESPACE-level enum, deliberately not nested.</b> The shore map's
    /// <c>ShoreMaterial</c> was nested once and cost a CS0426 across every consumer; the cliff kit's
    /// <c>CliffAssetKind</c> carries the same note. <b>Additive members only</b> — the ordinals are
    /// serialized into every region scene that authors a plan.</para>
    ///
    /// <para><b>The first three are ONE height profile.</b> <see cref="Beach"/>, <see cref="Dune"/> and
    /// <see cref="Access"/> all take the island's original radial chain, unchanged and byte-identical —
    /// they differ in what is PAINTED and PLANTED on them, not in how high they are. That is not a
    /// shortcut: it is what keeps the whole north coast, the sandbar, the clam field and every decor pin
    /// exactly where they were before cliffs existed, so this change can only be read where it is
    /// actually visible.</para>
    /// </summary>
    public enum CoastClass
    {
        /// <summary>The soft intertidal shore — the island's original profile. Walkable at any tide.</summary>
        Beach = 0,

        /// <summary>Beach geometry wearing the dune's clothes: marram above the spring high-water line.
        /// Same height chain as <see cref="Beach"/> (see the type note).</summary>
        Dune = 1,

        /// <summary>A gully through a cliff run — beach geometry, deliberately narrow, so a player can
        /// walk from the clifftop down to the shore. The ONE way onto the foreshore along a cliff coast,
        /// and the thing that makes the ledges reachable at low water.</summary>
        Access = 2,

        /// <summary>A standing wall whose foot sits below the lowest spring water: nothing bares, but the
        /// ground at its base is ordinary shoal rather than deep. The plain cliff.</summary>
        Cliff = 3,

        /// <summary>The deep-shore face — its foot plunges to the harbour floor, so the tide climbs and
        /// hides the rock as it rises and a hull can lie close alongside at any state of tide.</summary>
        DeepShoreCliff = 4,

        /// <summary>A face that stops on a narrow foreshore bench just above the lowest water — drowned
        /// for most of the cycle, walkable near dead low. The strip of ground the tide lends you.</summary>
        LedgeCliff = 5,
    }

    /// <summary>
    /// One authored stretch of coast: the class, and the bearing it STARTS at. A sector runs clockwise
    /// to the next entry's bearing, so the plan is a closed ring and there is no way to author a gap or
    /// an overlap — the defect a from/to pair invites.
    /// </summary>
    [System.Serializable]
    public struct CoastSector
    {
        [Tooltip("Compass bearing (degrees, N = 0, clockwise) this sector STARTS at, measured in the " +
                 "island's ELLIPSE-NORMALISED frame. The sector runs clockwise to the next entry.")]
        public float FromBearing;

        [Tooltip("What this stretch of coast is.")]
        public CoastClass Class;

        public CoastSector(float fromBearing, CoastClass cls)
        {
            FromBearing = fromBearing;
            Class = cls;
        }
    }

    /// <summary>
    /// The pure geometry of a sectored coast — bearings, outward normals, the aspect law, and the
    /// blend that keeps a cliff from ending in a vertical wall ACROSS the shore as well as down it.
    ///
    /// <para><b>Engine-light and total.</b> Every method is a pure function of its arguments (no scene,
    /// no RNG, no time), so the whole plan is EditMode-assertable without loading anything, and the coast
    /// is the same on every machine and every rebuild (CLAUDE.md rule 5).</para>
    ///
    /// <para><b>⚠ THE ASPECT LAW IS A CONSTRAINT ON THE NORMAL, NOT ON THE BEARING — and on an ellipse
    /// those are not the same angle.</b> The cliff kit authors five outward facings (W · SW · S · SE · E)
    /// and no north, because a north-facing wall shows its shadowed back to a ¾ top-down camera. With the
    /// kit's 22.5° snap that permits a seaward normal anywhere in 67.5° → 292.5°, which is 62% of the
    /// COMPASS. On a 120 × 70 m island it is not 62% of the SHORELINE: the outward normal on a wide
    /// island's flank swings toward the short axis faster than the radial bearing does, so the legal arc
    /// is 76.4° → 283.6° of bearing and 55.6% of the coast's length. Anything that reasons about the
    /// cliff budget must use <see cref="OutwardNormalAzimuth"/>, never the bearing.</para>
    /// </summary>
    public static class CoastPlan
    {
        /// <summary>The five outward facings the cliff kit authors, as compass azimuths (N = 0,
        /// clockwise) — W · SW · S · SE · E. Mirrors <c>CliffCatalog.AspectAzimuths</c>, which lives in
        /// an Editor assembly the World module cannot reference; a test asserts the two agree.</summary>
        public static readonly float[] AspectAzimuths = { 270f, 225f, 180f, 135f, 90f };

        /// <summary>Half the gap between adjacent authored aspects (they sit 45° apart), i.e. the worst
        /// snap error the kit can be asked to absorb. A cliff whose normal is further than this from
        /// EVERY authored aspect cannot be drawn honestly.</summary>
        public const float AspectSnapToleranceDegrees = 22.5f;

        /// <summary>True for the three classes that stand as rock. The one place the cliff/not-cliff
        /// split is written down, so the builder, the classifier, the decor and the tests cannot drift.</summary>
        public static bool IsCliff(CoastClass c) =>
            c == CoastClass.Cliff || c == CoastClass.DeepShoreCliff || c == CoastClass.LedgeCliff;

        /// <summary>True where the tide is meant to work exposed rock the player can stand on — the
        /// ledge bench. Kept separate from <see cref="IsCliff"/> because "is rock" and "bares" are
        /// different questions and conflating them is how a ledge test starts passing on a wall.</summary>
        public static bool BaresAtLowWater(CoastClass c) => c == CoastClass.LedgeCliff;

        /// <summary>
        /// Compass bearing (degrees in [0, 360), N = 0, clockwise) of a world position from the island
        /// centre, measured in the ELLIPSE-NORMALISED frame — the same <c>radiusX / radiusY</c>
        /// normalisation <see cref="TidalTerrain.IslandDistance"/> and <c>StPetersShoreMap.IsWeatherCoast</c>
        /// use, so a sector boundary lands at the same PLACE along the coast on both axes instead of
        /// bunching at the long ends.
        /// </summary>
        public static float BearingAt(Vector2 worldPos, Vector2 center, float radiusX, float radiusY)
        {
            Vector2 d = worldPos - center;
            if (radiusY > 0f && radiusX > 0f) d = new Vector2(d.x, d.y * (radiusX / radiusY));
            if (d.sqrMagnitude < 1e-12f) return 0f;                  // dead centre: pick north, arbitrarily
            return Wrap360(Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg);   // atan2(east, north) = compass
        }

        /// <summary>
        /// Compass azimuth of the OUTWARD NORMAL of the plateau-edge ellipse at a bearing — the direction
        /// a cliff standing there would FACE, and therefore the only angle the aspect law may be tested
        /// against.
        ///
        /// <para>The plateau edge is <c>centre + (rx·sin φ, ry·cos φ)</c>; the outward normal of
        /// <c>(x/rx)² + (y/ry)² = 1</c> is <c>(sin φ / rx, cos φ / ry)</c>, which scales to
        /// <c>(ry·sin φ, rx·cos φ)</c>. On a circle that collapses back to the bearing; on a wide
        /// island it does not, which is the whole point.</para>
        /// </summary>
        public static float OutwardNormalAzimuth(float bearingDegrees, float radiusX, float radiusY)
        {
            if (radiusX <= 0f || radiusY <= 0f) return Wrap360(bearingDegrees);
            float r = bearingDegrees * Mathf.Deg2Rad;
            return Wrap360(Mathf.Atan2(radiusY * Mathf.Sin(r), radiusX * Mathf.Cos(r)) * Mathf.Rad2Deg);
        }

        /// <summary>How far (degrees) an azimuth sits from the NEAREST facing the kit authors. The aspect
        /// law is <c>this ≤ <see cref="AspectSnapToleranceDegrees"/></c>.</summary>
        public static float AspectSnapError(float azimuthDegrees)
        {
            float best = 180f;
            for (int i = 0; i < AspectAzimuths.Length; i++)
            {
                float e = Mathf.Abs(Mathf.DeltaAngle(azimuthDegrees, AspectAzimuths[i]));
                if (e < best) best = e;
            }
            return best;
        }

        /// <summary>True where a cliff may honestly stand: the outward normal at this bearing snaps to
        /// one of the kit's five facings within tolerance.</summary>
        public static bool CliffIsLegalAt(float bearingDegrees, float radiusX, float radiusY) =>
            AspectSnapError(OutwardNormalAzimuth(bearingDegrees, radiusX, radiusY))
                <= AspectSnapToleranceDegrees + 1e-3f;

        /// <summary>
        /// Index of the sector a bearing falls in. Sectors are a closed ring ordered clockwise by
        /// <see cref="CoastSector.FromBearing"/>; the last one wraps through north to the first. Returns
        /// −1 only for an empty plan (the caller's cue to fall back to the radial profile).
        /// </summary>
        public static int SectorIndexAt(CoastSector[] sectors, float bearingDegrees)
        {
            if (sectors == null || sectors.Length == 0) return -1;
            float b = Wrap360(bearingDegrees);

            int found = -1;
            for (int i = 0; i < sectors.Length; i++)
            {
                float from = Wrap360(sectors[i].FromBearing);
                float to = Wrap360(sectors[(i + 1) % sectors.Length].FromBearing);
                // The wrapping sector (from > to) owns everything above `from` OR below `to`.
                bool inside = from <= to ? (b >= from && b < to) : (b >= from || b < to);
                if (inside) { found = i; break; }
            }
            // A single-sector plan (from == to) covers the whole ring; so does a plan whose bearings are
            // all equal. Neither is expressible above, so answer the first rather than −1.
            return found >= 0 ? found : 0;
        }

        /// <summary>The class standing at a bearing, unfeathered — what the plan SAYS, before the blend
        /// softens the joins. This is the decider the cliff-share measure and the paint read.</summary>
        public static CoastClass ClassAt(CoastSector[] sectors, float bearingDegrees)
        {
            int i = SectorIndexAt(sectors, bearingDegrees);
            return i < 0 ? CoastClass.Beach : sectors[i].Class;
        }

        /// <summary>
        /// The two classes whose height profiles meet at a bearing, and how much of the way it is to the
        /// PRIMARY one (0.5 exactly on a boundary, 1 in a sector's interior).
        ///
        /// <para><b>⚠ Why this exists at all.</b> A cliff sector's profile drops 6 m of plateau to below
        /// the water in three metres; a beach sector's takes twenty metres to do a seventh of it. Butted
        /// straight together at a sector boundary that is a <b>6.5 m step in the height field ACROSS the
        /// shore</b> — a wall running out to sea, which is not a cliff but a bug: it aliases the seabed
        /// bake, tears the shader's waterline, and puts an invisible one-metre-wide precipice on the
        /// walk. Feathering the two profiles over a few degrees makes a cliff run TAPER into the beach at
        /// each end, which is also what a real headland does.</para>
        ///
        /// <para>The window is angular, so it is a constant number of degrees rather than a distance that
        /// shrinks as you walk seaward. Sectors must be at least <c>2 × featherDegrees</c> wide or they
        /// never reach their own class — asserted, not assumed.</para>
        /// </summary>
        public static void BlendAt(CoastSector[] sectors, float bearingDegrees, float featherDegrees,
                                   out CoastClass primary, out CoastClass secondary, out float weight)
        {
            primary = CoastClass.Beach;
            secondary = CoastClass.Beach;
            weight = 1f;
            if (sectors == null || sectors.Length == 0) return;

            int i = SectorIndexAt(sectors, bearingDegrees);
            primary = sectors[i].Class;
            secondary = primary;
            if (sectors.Length == 1 || featherDegrees <= 0f) return;

            float b = Wrap360(bearingDegrees);
            float from = Wrap360(sectors[i].FromBearing);
            float to = Wrap360(sectors[(i + 1) % sectors.Length].FromBearing);

            float toStart = Wrap360(b - from);          // degrees clockwise since this sector began
            float toEnd = Wrap360(to - b);              // degrees clockwise until it ends

            // Whichever boundary is nearer is the one we blend across.
            bool nearStart = toStart <= toEnd;
            float distance = nearStart ? toStart : toEnd;
            if (distance >= featherDegrees) return;     // interior: the sector's own profile, undiluted

            int neighbour = nearStart
                ? (i - 1 + sectors.Length) % sectors.Length
                : (i + 1) % sectors.Length;
            secondary = sectors[neighbour].Class;

            // 0.5 exactly ON the boundary (so the two sides agree there and the field is continuous),
            // easing to 1 by the full feather.
            weight = 0.5f + 0.5f * Mathf.SmoothStep(0f, 1f, distance / featherDegrees);
        }

        /// <summary>Angular width (degrees) of the sector at an index — the ring's own arithmetic, in one
        /// place, because "how wide is this sector" is asked by the width law, the blend guard and the
        /// coast-length measure alike.</summary>
        public static float SectorWidth(CoastSector[] sectors, int index)
        {
            if (sectors == null || sectors.Length == 0) return 0f;
            if (sectors.Length == 1) return 360f;
            int i = ((index % sectors.Length) + sectors.Length) % sectors.Length;
            float w = Wrap360(sectors[(i + 1) % sectors.Length].FromBearing - sectors[i].FromBearing);
            return w <= 0f ? 360f : w;
        }

        /// <summary>Degrees into [0, 360).</summary>
        public static float Wrap360(float degrees)
        {
            float d = degrees % 360f;
            return d < 0f ? d + 360f : d;
        }
    }
}
