#if UNITY_EDITOR
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The CPU ground classifier for Nine Mile Creek</b> — the mainland's answer to
    /// <see cref="StPetersShoreMap"/>: which of the kit's six band materials stands on a metre of this
    /// coast, what the splat shader's band ladder must be pushed, and (the part the island has no way to
    /// express) which stretches of shore are ROCK because the COAST PLAN says so.
    ///
    /// <para><b>⭐ THE BAND LADDER IS ST PETERS', BY REFERENCE, AND THAT IS THE POINT.</b> Every floor in
    /// that ladder is a TIDE STATE — the beach toe is "dry for most of an ordinary tide", the marram floor
    /// is "above neap high and below spring high", the grass floor is "above the highest water with a
    /// margin". Nine Mile Creek authors St Peters' tide <i>verbatim</i> (mean 0, amplitude 2.2 m, and
    /// <see cref="NineMileCreekMainland"/> §2 explains at length why it has to: the tidal bar is ONE bar
    /// spanning the seam between the two regions). Same tide ⇒ same tide states ⇒ same ladder. Restating
    /// the six numbers here would be the "a builder with a comment and a test with a literal" anti-pattern
    /// this region's own geography file opens by warning against — and it would break something real: a
    /// player who walks the bar across the seam must not watch the sand/ripple boundary jump under their
    /// feet halfway.</para>
    ///
    /// <para><b>⚠ SO THE COUPLING IS GUARDED, NOT ASSUMED.</b> <see cref="SharesTheIslandsTide"/> is the
    /// condition under which the reference above is legitimate, and
    /// <c>NineMileCreekShoreMapTests</c> asserts it. The day anyone gives this region a tide of its own,
    /// that test goes red and says what has to happen: the ladder forks, because it is no longer the same
    /// set of tide states.</para>
    ///
    /// <para><b>⭐ WHAT THE MAINLAND DOES BETTER THAN THE ISLAND: rock is a PLAN, not a bearing.</b>
    /// St Peters splits its coast into "weather" and "sheltered" by taking a bearing on an ellipse, because
    /// an island is a shape you can be outside of. A mainland is a SIDE, and this one already declares what
    /// each stretch of it IS — <see cref="NineMileCreekMainland.CoastSectors"/> names Beach, Dune, Access,
    /// LedgeCliff, Cliff and DeepShoreCliff by distance along the run. So
    /// <see cref="IsRockCoastAt"/> asks the plan instead of guessing from a compass bearing, and the ground
    /// paint lands on the rock the terrain actually stands up (<c>MainlandTidalTerrain.CoastClassAt</c> is
    /// the ONE decider the cliff walls read too — see its own note).</para>
    ///
    /// <para><b>⚠ AND THAT IS WHY THE SHADER'S SECTOR IS NEUTRALISED.</b> The splat shader carries one
    /// island-shaped sector split (<c>_IslandCenter</c> + <c>_WeatherFacing</c>) and no way to read a coast
    /// run, so it CANNOT reproduce the plan above. Rather than feed it a bearing that would be wrong
    /// somewhere along 596 m of coastline, the whole region is pushed onto ONE vocabulary — the sheltered
    /// ladder (grass · marram · sand · ripple · shelf) — and the rock is carried by PAINT, which is where
    /// this region's rock information actually lives. See <see cref="SectorAnchor"/>.</para>
    /// </summary>
    public static class NineMileCreekShoreMap
    {
        // =================================================================================================
        //  1. THE BAND LADDER — St Peters' tide states, referenced rather than restated (see the type note)
        // =================================================================================================

        /// <summary>The lowest ground the painting reaches. Below this the splat quad clips itself away
        /// entirely (the shader's <c>clip(alpha)</c>) and the Sea plane is what you see.</summary>
        public static float PaintFloorElevation => StPetersShoreMap.PaintFloorElevation;

        /// <inheritdoc cref="PaintFloorElevation"/>
        public static float RippleFloorElevation => StPetersShoreMap.RippleFloorElevation;
        /// <inheritdoc cref="PaintFloorElevation"/>
        public static float SandFloorElevation => StPetersShoreMap.SandFloorElevation;
        /// <inheritdoc cref="PaintFloorElevation"/>
        public static float MarramFloorElevation => StPetersShoreMap.MarramFloorElevation;
        /// <inheritdoc cref="PaintFloorElevation"/>
        public static float GrassFloorElevation => StPetersShoreMap.GrassFloorElevation;
        /// <inheritdoc cref="PaintFloorElevation"/>
        public static float ShingleFloorElevation => StPetersShoreMap.ShingleFloorElevation;

        // The meander. Look-only noise that offsets the band LOOKUP, never the height — the same
        // "perturb the picture, never the ground" rule St Peters' bullseye fix established. Shared for the
        // same reason the floors are: it is the grain of one coastline that runs across both regions.
        /// <inheritdoc cref="PaintFloorElevation"/>
        public static float BandWiggleMetres => StPetersShoreMap.BandWiggleMetres;
        /// <inheritdoc cref="PaintFloorElevation"/>
        public static float BandWiggleScale => StPetersShoreMap.BandWiggleScale;
        /// <inheritdoc cref="PaintFloorElevation"/>
        public static float BandDetailMetres => StPetersShoreMap.BandDetailMetres;
        /// <inheritdoc cref="PaintFloorElevation"/>
        public static float BandDetailScale => StPetersShoreMap.BandDetailScale;

        /// <summary>
        /// The condition under which sharing the island's ladder is legitimate: the two regions author the
        /// SAME tide. Stated as a predicate rather than as a comment so a test can hold it, because the
        /// whole ladder above is a set of tide states and nothing else.
        ///
        /// <para>⚠ If this ever goes false the fix is NOT to relax the test — it is to give this region its
        /// own ladder, re-derived from its own tide, and to re-check the seam the crossing spans.</para>
        /// </summary>
        public static bool SharesTheIslandsTide =>
            Mathf.Approximately(NineMileCreekMainland.TideMean, StPetersBuilder.TideMean) &&
            Mathf.Approximately(NineMileCreekMainland.TideAmplitude, StPetersBuilder.TideAmplitude);

        // =================================================================================================
        //  2. THE TIDE, AS THE SHORE SEES IT — this region's own, never the island's
        // =================================================================================================
        // The LADDER is shared (above) because it is a set of tide states and the tides are equal. The tide
        // STATES themselves are read from this region's own constants, so if the two ever fork, everything
        // below moves with Nine Mile Creek and only the guarded ladder complains.

        /// <summary>Lowest water of the biggest spring tide — the bottom of everything intertidal.</summary>
        public static float SpringLowWater => NineMileCreekMainland.SpringLowWater;

        /// <summary>Highest water of the biggest spring tide. Above this, ground is never wetted.</summary>
        public static float SpringHighWater => NineMileCreekMainland.SpringHighWater;

        /// <summary>High water on a NEAP week — the ceiling of the weed belt. Ground above this dries out
        /// even on the gentlest fortnight, which is exactly why rockweed stops there.</summary>
        public static float NeapHighWater =>
            NineMileCreekMainland.TideMean
            + NineMileCreekMainland.TideAmplitude * GameConfig.DefaultNeapAmplitudeFraction;

        /// <summary>
        /// An elevation stated as a TIDE FRACTION: −1 = spring low, 0 = mean water, +1 = spring high.
        ///
        /// <para>⭐ The unit every window in <see cref="NineMileCreekStarterSplat"/> is authored in, and the
        /// reason they survive a tide ruling. The owner's 2026-08-01 amplitude change moved mean water not
        /// at all and spring low by 1.3 m; a bed authored at "−1.4 m" would have gone from bare-most-days
        /// to bare-almost-never without a line of code changing.</para>
        /// </summary>
        public static float TideElevation(float fraction) =>
            NineMileCreekMainland.TideMean + NineMileCreekMainland.TideAmplitude * fraction;

        // =================================================================================================
        //  3. THE SHADER'S SECTOR, NEUTRALISED — one vocabulary over the whole region
        // =================================================================================================

        /// <summary>
        /// How many region-widths SEAWARD of the region the sector anchor is placed. Large enough that
        /// every metre of the region lies within a few degrees of due-landward of it, so the bearing the
        /// shader takes is pinned hard negative everywhere and the sheltered ladder wins with no possible
        /// pocket of weather-coast vocabulary in a corner.
        ///
        /// <para>⚠ NOT trusted — MEASURED. <c>NineMileCreekShoreMapTests</c> walks the region's corners and
        /// edges, reproduces the shader's own bearing arithmetic, and asserts every sample resolves
        /// sheltered with margin. A factor that stopped being big enough would otherwise show up as a
        /// wedge of cobble in one corner of the fields, which reads as a painting bug.</para>
        /// </summary>
        public const float SectorAnchorRegionWidths = 4f;

        /// <summary>
        /// Where the shader's sector split is anchored: far out to seaward (EAST — this coast's water is
        /// east), so the ENTIRE region lies on the landward side of it and reads as one vocabulary.
        ///
        /// <para>Derived from the region rectangle rather than typed, so re-sizing the region keeps the
        /// anchor the same number of region-widths offshore instead of quietly dragging it inland.</para>
        /// </summary>
        public static Vector2 SectorAnchor =>
            NineMileCreekMainland.RegionWorldCenter +
            new Vector2(NineMileCreekMainland.RegionWorldSize.x * SectorAnchorRegionWidths, 0f);

        /// <summary>The direction the sector's "weather" half faces: seaward (east). With
        /// <see cref="SectorAnchor"/> far out that way, every point in the region bears back LANDWARD from
        /// it, the shader's <c>dot(bearing, facing)</c> is ≈ −1 throughout, and its <c>ws</c> term — the
        /// weather-coast weight — stays pinned at zero.</summary>
        public static readonly Vector2 SectorFacing = new Vector2(1f, 0f);

        /// <summary>A mainland is not an ellipse, so there is no aspect to normalise: 1 leaves the shader's
        /// bearing arithmetic measuring plain world direction.</summary>
        public const float SectorAspect = 1f;

        /// <summary>How far the sector boundary is feathered. Kept at the island's figure so the number the
        /// test measures its margin against is the shipped one — though with the anchor four region-widths
        /// offshore there is no boundary inside this region for it to feather.</summary>
        public static float SectorFeather => StPetersShoreMap.SectorFeather;

        /// <summary>
        /// The shader's own sector term, on the CPU: the (aspect-normalised) bearing from
        /// <see cref="SectorAnchor"/> to a world point, dotted with <see cref="SectorFacing"/>. Positive
        /// means weather coast, negative means sheltered.
        ///
        /// <para>Mirrors <c>HiddenHarboursTerrainSplat.shader</c>'s block verbatim so the test that asserts
        /// "sheltered everywhere" is checking the arithmetic the GPU will actually run, not a paraphrase of
        /// it.</para>
        /// </summary>
        public static float SectorBearingAt(Vector2 worldPos)
        {
            Vector2 d = worldPos - SectorAnchor;
            d = new Vector2(d.x, d.y * SectorAspect);
            if (d.sqrMagnitude < 1e-8f) return 0f;
            return Vector2.Dot(d.normalized, SectorFacing.normalized);
        }

        // =================================================================================================
        //  4. WHICH GROUND IS ROCK — the coast PLAN, not a bearing
        // =================================================================================================

        /// <summary>True where <paramref name="coast"/> is one of the plan's standing-rock classes. The
        /// three cliff classes differ in where their FOOT sits (below the lowest water, on the bay floor,
        /// or on a bench); all three are rock in plan view, which is the only question the ground paint
        /// asks.</summary>
        public static bool IsRockCoast(CoastClass coast) =>
            coast == CoastClass.Cliff ||
            coast == CoastClass.DeepShoreCliff ||
            coast == CoastClass.LedgeCliff;

        /// <summary>
        /// Is the shore at <paramref name="worldPos"/> a rock coast? Asks the terrain's own classifier, so
        /// this and the cliff walls and the walk gate cannot disagree about where the rock is.
        ///
        /// <para>⚠ <c>CoastClassAt</c> projects onto the coast RUN and answers for every position in the
        /// region, including ground hundreds of metres inland. That is correct for this use — a stretch of
        /// coast's class describes the shore in front of it — and harmless, because every rock family is
        /// also gated by a tide window that inland ground is far above.</para>
        /// </summary>
        public static bool IsRockCoastAt(MainlandTidalTerrain terrain, Vector2 worldPos) =>
            terrain != null && IsRockCoast(terrain.CoastClassAt(worldPos));

        // =================================================================================================
        //  5. MADE GROUND — the ground that was built rather than weathered
        // =================================================================================================

        /// <summary>
        /// True inside one of the region's FILLS — the harbour shoal, the spit's red-earth yard, the two
        /// wharf decks and the crib breakwater (<see cref="NineMileCreekMainland.Fills"/>).
        ///
        /// <para>⭐ The wharf at Nine Mile Creek stands on MADE GROUND, and that is the single most
        /// important reading of the owner's photographs (<see cref="NineMileCreekMainland"/> §8). Made
        /// ground does not grow marram and does not weather into a dune: it is red earth, gravel and rock
        /// armour. So the natural shore families are held OFF it and the worked-ground families are held
        /// ON it, which is what makes the yard read as a yard instead of as a beach that happens to be
        /// flat.</para>
        ///
        /// <para>Tested against the flat part of each fill only (its <c>HalfSize</c> rectangle), not its
        /// falloff skirt: the skirt is where made ground grades back into the natural shore, and both
        /// vocabularies belong there.</para>
        /// </summary>
        public static float MadeGroundWeight(Vector2 worldPos) =>
            ZoneWeight(worldPos, NineMileCreekMainland.Fills);

        /// <summary>How much of this ground is one of the region's CARVES — the barachois lagoon behind
        /// the spit and the marsh pool south of Wharf Road. Sheltered, muddy, brackish ground: the beds and
        /// the marsh belong here and the wave-worked foreshore does not.</summary>
        public static float PondWeight(Vector2 worldPos) =>
            ZoneWeight(worldPos, NineMileCreekMainland.Carves);

        /// <summary>
        /// A zone's membership as a SMOOTH weight: 1 across its flat rectangle, easing to 0 across its own
        /// <see cref="MainlandZone.Falloff"/> metres, strongest zone wins.
        ///
        /// <para><b>⚠ THE FALLOFF IS THE ZONE'S OWN, and that is the whole point.</b> The first capture of
        /// this pass painted these gates as hard rectangle tests, and the result was a barachois with
        /// STRAIGHT EDGES and a wharf yard cut out with a ruler — because the terrain eases each zone in
        /// over 6–16 m while the paint stopped dead at the rectangle. Feathering over the metres the ground
        /// itself eases over is what puts the painted edge on the real edge.</para>
        /// </summary>
        public static float ZoneWeight(Vector2 p, MainlandZone[] zones)
        {
            if (zones == null) return 0f;
            float best = 0f;
            for (int i = 0; i < zones.Length; i++)
            {
                MainlandZone z = zones[i];
                float d = MainlandTidalTerrain.DistanceOutsideRect(p, z.Center, z.HalfSize);
                float w = d <= 0f ? 1f : MainlandTidalTerrain.SmoothFalloff(d, Mathf.Max(z.Falloff, 1e-3f));
                if (w > best) best = w;
            }
            return best;
        }

        /// <summary>Convenience predicate — "is this mostly made ground?". The PAINT uses
        /// <see cref="MadeGroundWeight"/>; this is for callers asking a yes/no question.</summary>
        public static bool IsMadeGround(Vector2 worldPos) => MadeGroundWeight(worldPos) >= 0.5f;

        /// <inheritdoc cref="IsMadeGround"/>
        public static bool IsPondGround(Vector2 worldPos) => PondWeight(worldPos) >= 0.5f;

        // =================================================================================================
        //  6. THE BAND TABLE — what stands here, before any paint goes down
        // =================================================================================================

        /// <summary>
        /// The band material at <paramref name="worldPos"/>, or <see cref="ShoreMaterial.None"/> where the
        /// ground is below the paint floor. The SHELTERED ladder everywhere, because that is the one
        /// vocabulary the shader is pushed (see the type note) — the rock this coast really has arrives as
        /// paint, on top.
        ///
        /// <para>Reads a WIGGLED elevation for the same reason St Peters does — an analytic profile draws
        /// perfect contours otherwise — but clamps the lookup to the paint floor so the meander can move a
        /// metre of ground BETWEEN bands and never out of the painting.</para>
        /// </summary>
        public static ShoreMaterial MaterialAt(ITidalTerrain terrain, Vector2 worldPos)
        {
            if (terrain == null) return ShoreMaterial.None;

            float e = terrain.ElevationAt(worldPos);
            if (e < PaintFloorElevation) return ShoreMaterial.None;   // raw elevation: the footprint is a budget

            float look = Mathf.Max(PaintFloorElevation,
                                   e + StPetersShoreMap.Wiggle(worldPos) * BandWiggleMetres
                                     + StPetersShoreMap.Wiggle(
                                           worldPos * (BandWiggleScale / BandDetailScale), 7)
                                       * BandDetailMetres);
            return StPetersShoreMap.ShelteredBand(look);
        }
    }
}
#endif
