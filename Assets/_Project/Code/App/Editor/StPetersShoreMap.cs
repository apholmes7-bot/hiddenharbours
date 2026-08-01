#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// The six ground materials the shoreline-ISO v8 kit bakes, plus <see cref="None"/> for "leave this
    /// cell unpainted". These are the kit's own rows — <see cref="StPetersShoreMap.MaterialName"/> maps
    /// each one onto the string <c>ShorelineIso2Catalog.GroundMaterials</c> publishes, and a test asserts
    /// the two vocabularies still agree so a re-bake that renames a row cannot pass silently.
    /// </summary>
    public enum ShoreMaterial
    {
        None = 0,
        Grass,
        Marram,
        Sand,
        Shingle,
        Ripple,
        Shelf,
    }

    /// <summary>
    /// <b>Which ground material stands at a given metre of St Peters</b> — the pure, deterministic decision
    /// that turns the region's authored <see cref="ITidalTerrain"/> into the shoreline-ISO v8 vocabulary
    /// (grass / marram / sand / shingle / ripple / shelf), picks each cell's fringe overlay, and scatters the
    /// reef's rock tells. <see cref="StPetersShorePainter"/> is the only caller; it does the Unity work.
    ///
    /// <para><b>Why the classifier is a separate, engine-light class.</b> Everything here is a pure function
    /// of world position and the authored terrain — no scene, no editor API, no <c>System.Random</c> — so an
    /// EditMode test can assert the coast the scene is painted from without loading anything, exactly the way
    /// <see cref="StPetersBuilder.ScatterClamHoles"/> is testable. A rebuild reproduces the same coast to the
    /// tile (CLAUDE.md rule 5).</para>
    ///
    /// <para><b>The coast is not uniform, and that is the spec.</b>
    /// <c>docs/design/scene-sizing-and-world-scale.md</c> §5.1 maps the owner's brief onto this footprint and
    /// splits the perimeter in two: <i>"Red sandstone cliffs + sea caves → the south/east <b>weather</b>
    /// coast"</i> and <i>"Beaches, sandbar, clam flats → the intertidal <b>west/north</b>"</i>. So the bands
    /// below are chosen by ELEVATION (which tide state washes this ground) crossed with SECTOR (which way the
    /// ground faces the weather) — a scoured cobble-and-platform coast facing south-east, a soft
    /// dune-and-flats coast facing west-north. §5.3 note 2 asks for exactly this distinction to be drawn in
    /// the land.</para>
    ///
    /// <para><b>No water, ever.</b> The kit bakes none and neither does this (ADR 0010/0012/0023): every
    /// material is authored to read right dry AND submerged, the shader owns the waterline, foam and swash,
    /// and the tide sweeps over whatever is painted. There is no wet-edge material to choose because there is
    /// no wet-edge tile — which is also why the bands are keyed on elevation rather than on "is it wet now".</para>
    /// </summary>
    public static class StPetersShoreMap
    {
        // =====================================================================================
        //  AUTHORED BANDS (single source of truth shared with StPetersShoreMapTests)
        // =====================================================================================
        // Read against St Peters' tide: mean 0, amplitude 2.2 m, so spring water swings -2.2 .. +2.2 m and
        // neap only +/-0.99 m (GameConfig.NeapAmplitudeFraction 0.45). Every threshold below is a TIDE
        // STATE stated as an elevation, not a taste: "above the highest water", "washed at spring but not
        // at neap", "bares only in the bottom half".
        //
        // ⚠ THE BAND FLOORS DELIBERATELY DID NOT MOVE when the amplitude fell 3.5 -> 2.2 m (owner's
        // tide-pacing ruling, 2026-08-01) — they were AUDITED instead, and every property each one claims
        // still holds: grass is still above the highest spring water, marram is still dry all neap week and
        // washed on the springs, sand is still dry for most of an ordinary tide, ripple still bares only in
        // the bottom half, and the -4 m harbour floor is still entirely below the paint floor. Only the
        // BarSpineFloorElevation scaled, because it alone is defined against the CREST rather than against
        // the tide (see its note). What did change is proportion, not character: with the highest water
        // 1.3 m lower, the sea-touched part of the marram band narrows and the permanently-dry dune belt
        // above it widens by ~2 m of elevation (~9 m of shore on the beach's 0.23 m/m gradient). Marram on
        // a dry dune is still marram, so this reads as a wider dune rather than a wrong one — but it is a
        // visible change to the painted coast, and the owner should eyeball it after re-running the builder.

        /// <summary>
        /// Below this elevation nothing is painted — the cell is left empty and the shader's depth-graded
        /// water is all you see. Painted ground is ground that either bares or lies shallow enough to
        /// read through the column once absorption is on (ADR 0027 num 7); below this a tile would be a
        /// tile nobody ever sees, paid for in scene bytes. The flat −4.0 m harbour floor is entirely
        /// below it, so the deep sea stays unpainted by construction rather than by a special case.
        ///
        /// <para>⚠ THIS ONE HAD TO MOVE with the 2026-08-01 amplitude ruling, and the test caught it: at
        /// −2.6 m it sat BELOW the new spring low water (−2.2 m), which
        /// <c>StPetersShoreMapTests.TheBandsAreOrderedAndEachOneIsATideState</c> forbids — a paint floor
        /// under the lowest water is paint spent on seabed that is never once uncovered. The value is the
        /// MIDPOINT between spring low water and the ripple floor, which is exactly where −2.6 sat in the
        /// old ±3.5 world (−3.5 and −1.7 → −2.6). At ±2.2 that is −1.95. Note it could NOT simply be
        /// scaled: 0.6286 × −2.6 = −1.63 would have risen ABOVE the ripple floor and broken the ladder.</para>
        /// </summary>
        public const float PaintFloorElevation = -1.95f;

        /// <summary>Above the highest water of the biggest spring tide (+2.2 m) with a margin: ground the
        /// sea never touches, so it carries the reverting meadow §5.1 calls for ("marsh, meadows, wild
        /// roses/raspberries → the reverting interior"). The margin widened from 0.7 m to 2.0 m when the
        /// amplitude fell to 2.2 — it is a floor for ground the sea never reaches, so a larger margin only
        /// makes the claim safer.</summary>
        public const float GrassFloorElevation = 4.2f;

        /// <summary>Marram binds the sand that only the big tides reach. 1.6 m sits above neap high water
        /// (0.99 m) and below spring high (2.2 m), so this band is dry all neap week and washed on the
        /// springs — which is precisely where marram grows. It used to sit hard against neap high (1.575 m)
        /// and now sits roughly midway between the two, so the washed part of the band is narrower and the
        /// dry dune above it wider; the property the band is defined by is unchanged.</summary>
        public const float MarramFloorElevation = 1.6f;

        /// <summary>The beach proper: dry for most of an ordinary tide, covered near high water. −0.4 m is
        /// the same level §5.1a picks out for the berth gate (a 0.6 m draught clears a −1.0 m bed above
        /// −0.4 m), so the beach toe and the slip's working depth are one decision.</summary>
        public const float SandFloorElevation = -0.4f;

        /// <summary>The rippled low-water flats — ground that only bares in the bottom half of the tide.
        /// −1.7 m sits just under the reef shelf's outer lip (−1.5 m), so the shelf reads as ripple on the
        /// sheltered coast and the drop-off past it gets a thin scoured hem.</summary>
        public const float RippleFloorElevation = -1.7f;

        /// <summary>The weather coast's storm beach: cobble from the low-water platform right up over
        /// ordinary high water. Shares the beach toe with <see cref="SandFloorElevation"/> because it is the
        /// same tide level wearing the other coast's material.</summary>
        public const float ShingleFloorElevation = -0.4f;

        /// <summary>
        /// Half-width (m) of the sandbar's COBBLE SPINE. The world docs call the bar "a cobble-and-sand
        /// bar" (§6.0), so the walking line down its crest is shingle and the flats either side are sand →
        /// ripple: a path you can read, with the digging ground beside it. 8 m of spine inside a 30 m
        /// half-width bar leaves ~22 m of flat each side for the clam field.
        /// </summary>
        public const float BarSpineHalfWidth = 8f;

        /// <summary>The spine only reads as a path where it is actually the crest. Below this the bar has
        /// already fallen away into its flanks, so the cobble stops and the sand starts.
        ///
        /// <para>⚠ This is the ONE band floor defined against <c>StPetersBuilder.SandbarCrestElevation</c>
        /// rather than against the tide, so it is the one that SCALED with the 2026-08-01 amplitude ruling:
        /// 0.6 → 0.377, holding 0.6/1.4 = 0.4286 of the crest exactly (equivalently 0.6 × 2.2/3.5). Left at
        /// 0.6 against the new 0.88 m crest the spine would have covered only the top third of the bar's
        /// rise instead of the top half, and the cobble path down the crossing would have visibly thinned
        /// for no reason anyone chose.</para></summary>
        public const float BarSpineFloorElevation = 0.377f;

        /// <summary>
        /// The direction the WEATHER coast faces: south-east. Every point whose (ellipse-normalised)
        /// bearing from the island centre lies in this half — the arc from north-east clockwise through
        /// east and south to south-west — is weather coast; the complement (west through north) is the
        /// sheltered coast that carries the beaches, the flats and the bar (§5.1).
        /// </summary>
        public static readonly Vector2 WeatherCoastFacing = new Vector2(1f, -1f);

        // --- THE LOOK-ONLY WIGGLE (the bullseye fix) ------------------------------------------------
        // ⚠ The island's terrain is an ANALYTIC profile — a smooth function of elliptical distance — so
        // every material boundary derived from it straight is a perfect concentric ellipse, and the first
        // render of this coast came out looking like a target rather than an island. Real shorelines
        // meander: a dune runs out into a hollow, a cobble tongue reaches up a gully, the marram grows in
        // patches. So the band LOOKUP is offset by a small coherent noise before it picks a material.
        //
        // ⭐ This is LOOK ONLY and it is important that it stays that way. The noise never touches the
        // terrain, so it never moves the water level, the walkability gate, the clam field, the tide
        // window or the reef's depth gate — it only changes which of six drawn materials a metre of
        // already-decided ground gets. Same shape as the shoreline's own #177 wiggle (organic-shoreline):
        // perturb the picture, never the height. Two consequences held deliberately:
        //   · the PAINT FLOOR reads the RAW elevation, so the painted footprint (and its budget) is
        //     unchanged and the outer edge stays clean where it meets the shader's own wiggly waterline;
        //   · the SANDBAR is exempt entirely — its cobble spine is a path and its channel is a gut, and
        //     both are things the player reads to decide where to walk. Signage does not get weathered.

        /// <summary>Metres of elevation the coarse octave shifts the band lookup by, at most. On the
        /// beach's ~0.23 m/m gradient that is about ±3.5 m of boundary movement — a meander you can see
        /// without any band losing its identity.</summary>
        public const float BandWiggleMetres = 0.8f;

        /// <summary>Feature size (m) of the coarse octave — the wavelength of the meander.</summary>
        public const float BandWiggleScale = 16f;

        /// <summary>A finer octave on top, for the metre-scale raggedness that stops a meandering edge
        /// from reading as a drawn curve.</summary>
        public const float BandDetailMetres = 0.3f;
        public const float BandDetailScale  = 6f;

        /// <summary>
        /// How far the weather/sheltered boundary is feathered, as a fraction of the bearing dot product.
        /// Without it the two vocabularies meet along a dead-straight diagonal and the band widths step
        /// visibly where it crosses the coast; ±0.12 interleaves them over roughly ±7° of bearing, which
        /// is how a real coast changes character — gradually, and in patches.
        /// </summary>
        public const float SectorFeather = 0.12f;

        // =====================================================================================
        //  MATERIAL VOCABULARY
        // =====================================================================================

        /// <summary>
        /// The kit row name for a material — the string <c>ShorelineIso2Catalog.GroundIndex</c> /
        /// <c>FringeIndex</c> take. Kept as an explicit table rather than <c>ToString().ToLower()</c> so a
        /// renamed enum member is a compile error here instead of a silent lookup throw at bake time.
        /// </summary>
        public static string MaterialName(ShoreMaterial m)
        {
            switch (m)
            {
                case ShoreMaterial.Grass:   return "grass";
                case ShoreMaterial.Marram:  return "marram";
                case ShoreMaterial.Sand:    return "sand";
                case ShoreMaterial.Shingle: return "shingle";
                case ShoreMaterial.Ripple:  return "ripple";
                case ShoreMaterial.Shelf:   return "shelf";
                default:                    return null;
            }
        }

        /// <summary>
        /// How high a material sits in the lapping order: the HIGHER material laps onto the lower one, and
        /// the fringe overlay is stamped on the LOWER cell (the kit contract's own words —
        /// <c>Fringe.png</c>: "stamp over the LOWER material's ground tile; the higher material laps onto
        /// it"). Deliberately NOT the sheet's row order: the sheet lists ripple before shingle, but a cobble
        /// storm beach sits ABOVE the rippled flats it fronts, so the physical stack is
        /// grass &gt; marram &gt; sand &gt; shingle &gt; ripple &gt; shelf.
        /// </summary>
        public static int Rank(ShoreMaterial m)
        {
            switch (m)
            {
                case ShoreMaterial.Grass:   return 6;
                case ShoreMaterial.Marram:  return 5;
                case ShoreMaterial.Sand:    return 4;
                case ShoreMaterial.Shingle: return 3;
                case ShoreMaterial.Ripple:  return 2;
                case ShoreMaterial.Shelf:   return 1;
                default:                    return 0;   // None — lower than everything, laps onto nothing
            }
        }

        /// <summary>Only the three SOFT materials carry a fringe overlay (the kit bakes three rows, not
        /// six) — a cobble or bedrock edge is hard and needs none.</summary>
        public static bool HasFringe(ShoreMaterial m) =>
            m == ShoreMaterial.Grass || m == ShoreMaterial.Marram || m == ShoreMaterial.Sand;

        // =====================================================================================
        //  THE CLASSIFIER
        // =====================================================================================

        /// <summary>
        /// True where the ground faces the WEATHER (south/east, §5.1). The Y offset is divided by the
        /// island's aspect before the bearing is taken — the same <c>radiusX / radiusY</c> normalisation
        /// <see cref="HiddenHarbours.World.TidalTerrain.IslandDistance"/> uses — so on a 450 × 260 m ellipse
        /// the sector boundary lands at the same *place along the coast* on both axes instead of bunching at
        /// the long ends.
        /// </summary>
        public static bool IsWeatherCoast(Vector2 worldPos)
        {
            Vector2 d = worldPos - StPetersBuilder.IslandCenter;
            if (StPetersBuilder.IslandRadiusY > 0f && StPetersBuilder.IslandRadius > 0f)
                d = new Vector2(d.x, d.y * (StPetersBuilder.IslandRadius / StPetersBuilder.IslandRadiusY));
            if (d.sqrMagnitude < 1e-6f) return false;   // dead centre: meadow either way

            // Compare BEARINGS (both normalised) so the threshold is an angle and the feather below is a
            // constant number of degrees rather than something that shrinks with distance from the centre.
            float bearing = Vector2.Dot(d.normalized, WeatherCoastFacing.normalized);
            return bearing > Wiggle(worldPos) * SectorFeather;
        }

        /// <summary>
        /// The material standing at <paramref name="worldPos"/>, or <see cref="ShoreMaterial.None"/> where
        /// the ground is too deep to paint. Composed in the order the geography reads: the sandbar has its
        /// own cobble-and-sand vocabulary, and everywhere else the elevation band is picked from the
        /// sheltered or the weather table by sector.
        /// </summary>
        public static ShoreMaterial MaterialAt(ITidalTerrain terrain, Vector2 worldPos)
        {
            if (terrain == null) return ShoreMaterial.None;

            float e = terrain.ElevationAt(worldPos);

            // ⭐ The paint floor reads the RAW elevation, never the wiggled one: the footprint is a
            // budget decision, and the outer edge is where the painting hands off to the shader's own
            // wiggly waterline — neither wants a second source of raggedness.
            if (e < PaintFloorElevation) return ShoreMaterial.None;

            // THE BAR — a cobble walking line with sand flats either side ("a cobble-and-sand bar", §6.0).
            // Tested before the sector split because the bar leaves the island's WEST end and would
            // otherwise just inherit the sheltered table, losing the path read entirely.
            //
            // ⭐ EXEMPT FROM THE WIGGLE, on purpose. The spine is a path and the channel is a boat gut:
            // both are things the player reads off the ground to decide where to put their feet or their
            // hull. A weathered edge on scenery is atmosphere; a weathered edge on signage is a lie.
            float dBar = DistanceToSegment(worldPos, StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo);
            if (dBar <= StPetersBuilder.SandbarHalfWidth)
            {
                if (IsBarSpine(worldPos, e)) return ShoreMaterial.Shingle;
                return ShelteredBand(e);
            }

            // The island's coast: pick the band from a WIGGLED elevation so the rings meander.
            //
            // ⚠ CLAMPED to the paint floor, and that clamp is load-bearing. Both band tables carry their
            // OWN floor guard (they are public and callable on their own), so handing them a wiggled
            // elevation that dipped below the floor made them answer None — and the footprint quietly lost
            // 1,239 cells to a ragged hem the paint decision had already ruled in. The wiggle may move a
            // metre of ground BETWEEN bands; it may never move it out of the painting.
            float look = Mathf.Max(PaintFloorElevation,
                                   e + Wiggle(worldPos) * BandWiggleMetres
                                     + Wiggle(worldPos * (BandWiggleScale / BandDetailScale), 7)
                                       * BandDetailMetres);
            return IsWeatherCoast(worldPos) ? WeatherBand(look) : ShelteredBand(look);
        }

        /// <summary>
        /// Coherent value noise in [-1, 1] over world position, at <see cref="BandWiggleScale"/> metres per
        /// lattice cell. Bilinear between hashed lattice corners with a smoothstep ease, so the field
        /// MEANDERS instead of speckling — per-cell white noise would dither every band boundary into
        /// salt-and-pepper, which is the opposite of the organic edge this is for.
        ///
        /// <para>Pure: a stable hash of the lattice corner, no <c>System.Random</c>, so the coast is the
        /// same on every machine and every rebuild (rule 5).</para>
        /// </summary>
        public static float Wiggle(Vector2 worldPos, int salt = 3)
        {
            float fx = worldPos.x / BandWiggleScale;
            float fy = worldPos.y / BandWiggleScale;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = Mathf.SmoothStep(0f, 1f, fx - x0);
            float ty = Mathf.SmoothStep(0f, 1f, fy - y0);

            float c00 = Hash01(x0,     y0,     salt);
            float c10 = Hash01(x0 + 1, y0,     salt);
            float c01 = Hash01(x0,     y0 + 1, salt);
            float c11 = Hash01(x0 + 1, y0 + 1, salt);

            float bottom = Mathf.Lerp(c00, c10, tx);
            float top    = Mathf.Lerp(c01, c11, tx);
            return Mathf.Lerp(bottom, top, ty) * 2f - 1f;
        }

        /// <summary>The WEST/NORTH coast: grass meadow, a marram dune band, beach sand, the rippled flats,
        /// then a scoured hem at the drop-off. The soft coast the clams and the crossing live on.</summary>
        public static ShoreMaterial ShelteredBand(float elevation)
        {
            if (elevation < PaintFloorElevation)      return ShoreMaterial.None;
            if (elevation >= GrassFloorElevation)     return ShoreMaterial.Grass;
            if (elevation >= MarramFloorElevation)    return ShoreMaterial.Marram;
            if (elevation >= SandFloorElevation)      return ShoreMaterial.Sand;
            if (elevation >= RippleFloorElevation)    return ShoreMaterial.Ripple;
            return ShoreMaterial.Shelf;
        }

        /// <summary>The SOUTH/EAST weather coast: the same meadow cap, then cobble all the way down to the
        /// low-water mark and bare rock platform below it. No dune and no beach — the sea takes the sand
        /// away on the side it pounds, which is what makes this coast read as the hard one (§5.1/§5.3).</summary>
        public static ShoreMaterial WeatherBand(float elevation)
        {
            if (elevation < PaintFloorElevation)      return ShoreMaterial.None;
            if (elevation >= GrassFloorElevation)     return ShoreMaterial.Grass;
            if (elevation >= ShingleFloorElevation)   return ShoreMaterial.Shingle;
            return ShoreMaterial.Shelf;
        }

        /// <summary>
        /// Which of <c>Ground.png</c>'s four columns this cell takes. The columns are NOT four art variants
        /// — the contract calls them <c>"gx 0..3 (adjacent world tiles — seams invisible)"</c>, four samples
        /// of a noise field that is a pure function of global pixel coords. So the column must be keyed on
        /// the cell's own world X for the field to stay continuous: neighbours then butt the way the rig
        /// drew them, and a run of ground never repeats. Keying it on a hash instead would scramble a
        /// seamless field into four interchangeable tiles and put a visible seam on every cell boundary.
        /// </summary>
        public static int GroundVariant(int cellX)
        {
            int n = ShorelineIso2Variants;
            return ((cellX % n) + n) % n;   // floor-mod: negative world X still lands in 0..n-1
        }

        /// <summary>Mirrors <c>ShorelineIso2Catalog.GroundVariants</c> without dragging the Art.Editor
        /// dependency into a pure classifier. A test asserts the two agree.</summary>
        public const int ShorelineIso2Variants = 4;

        // =====================================================================================
        //  FRINGE AUTOTILING
        // =====================================================================================

        /// <summary>
        /// The fringe piece to stamp on a cell whose lapping neighbours are described by the eight booleans
        /// (true = that neighbour carries the HIGHER material that laps onto this cell). Returns
        /// <c>null</c> for "no fringe here".
        ///
        /// <para>Pure and total, so the whole autotiler is one testable table: two ADJACENT cardinals →
        /// the INNER corner that wraps around them; one cardinal → the matching EDGE; a diagonal alone →
        /// the OUTER corner. Opposite cardinals with no adjacent pair (a one-cell gully) fall to the north
        /// edge — deterministic, and the case is a metre wide.</para>
        ///
        /// <para><b>⚠ The compass letters are the KIT's claim, not a measurement</b> (the standing warning
        /// on <c>ShorelineIso2Catalog</c> — this repo has shipped mislabelled compass art in five kits and
        /// nothing here has been checked against rendered pixels). The ORDER of the contract's columns is
        /// what is guaranteed, and that is what this returns. If a fringe reads the wrong way round
        /// in-scene the fix is the label→column map in the catalog, never a renumbered slice and never a
        /// fudge here.</para>
        /// </summary>
        public static string FringePiece(bool n, bool e, bool s, bool w,
                                        bool ne, bool se, bool sw, bool nw)
        {
            // Inner corners first: they cover the most of the cell, so a corner beats either of its edges.
            if (n && e) return "inNE";
            if (s && e) return "inSE";
            if (s && w) return "inSW";
            if (n && w) return "inNW";

            if (n) return "edN";
            if (e) return "edE";
            if (s) return "edS";
            if (w) return "edW";

            // Only a diagonal touches this cell — the higher material rounds an outside corner.
            if (ne) return "coNE";
            if (se) return "coSE";
            if (sw) return "coSW";
            if (nw) return "coNW";

            return null;
        }

        // =====================================================================================
        //  THE REEF'S ROCK TELLS
        // =====================================================================================

        /// <summary>One placed rock: where it stands and which <c>ShoreIsoSprites</c> slice it is.</summary>
        public struct RockSite
        {
            public Vector2 Position;
            /// <summary>A name from <c>ShorelineIsoCatalog.RockSprites</c> — <c>reef</c>, the sea stacks
            /// <c>s</c>/<c>m</c>/<c>l</c>, or the slab boulders <c>bs</c>/<c>bm</c>/<c>bl</c>.</summary>
            public string Sprite;
        }

        /// <summary>
        /// How many sea stacks ring the island. §5.1a asks to "keep a couple of the ShoreIsoSprites sea
        /// stacks ON the shelf as the visible tell — the reef should be legible from the deck before it is
        /// legible from the depth sounder". Nine around a ~1.1 km coastline is one every ~120 m: sparse
        /// enough to read as a tell rather than as a rock field (St Peters is the gentlest tutorial in the
        /// game and authors no new hazard — the shelf's DEPTH is the gate, these are how you see it).
        /// </summary>
        public const int SeaStackCount = 9;

        /// <summary>Low reef sprites — the barely-breaking rock the stacks stand among.</summary>
        public const int ReefPatchCount = 14;

        /// <summary>Slab boulders, on the weather coast's cobble where a sandstone coast sheds them.</summary>
        public const int BoulderCount = 22;

        /// <summary>Clearance (m) kept around the berth's centre-line and the sandbar's, on top of their own
        /// half-widths. The berth is the ONE door in the reef (§5.1a) and the bar is the crossing: putting
        /// rock in either would author a hazard the docs explicitly do not want ("no rocks", §6.0 hazards).</summary>
        public const float RockClearance = 6f;

        /// <summary>
        /// The island's rock, scattered deterministically around the reef ring and the weather coast. Pure:
        /// each site is a stable hash of its index (no <c>System.Random</c>), and every site is checked
        /// against the authored terrain and the two crossings, so a rebuild reproduces the same rocks and
        /// none of them ever lands in the slip or on the walking bar (CLAUDE.md rule 5).
        /// </summary>
        public static List<RockSite> ScatterRocks(ITidalTerrain terrain)
        {
            var sites = new List<RockSite>();
            if (terrain == null) return sites;

            // The authored shelf band, in the elliptical-distance units TidalTerrain measures in: it starts
            // where the beach ends and runs one shelf-width out (TidalTerrain.IslandProfile).
            float beachEnd = StPetersBuilder.IslandRadius + StPetersBuilder.IslandFalloff;
            float shelfEnd = beachEnd + StPetersBuilder.ReefShelfWidth;

            // Sea stacks + reef patches stand ON the shelf — inset a little from both lips so a stack never
            // straddles the beach toe or tips over the drop-off.
            AddRing(sites, terrain, SeaStackCount, beachEnd + 4f, shelfEnd - 3f, salt: 11,
                    sprites: new[] { "s", "m", "l" });
            AddRing(sites, terrain, ReefPatchCount, beachEnd + 2f, shelfEnd - 1f, salt: 23,
                    sprites: new[] { "reef" });

            // Boulders: the weather coast's cobble, from the low-water platform up over the storm beach.
            AddRing(sites, terrain, BoulderCount,
                    StPetersBuilder.IslandRadius + 4f, beachEnd + 2f, salt: 37,
                    sprites: new[] { "bs", "bm", "bl" }, weatherCoastOnly: true);

            return sites;
        }

        /// <summary>Interior erratic attempts — most are rejected by the gates below; the survivors are
        /// the dozen-odd glacial boulders a field coast actually carries.</summary>
        public const int FieldRockAttempts = 36;

        /// <summary>Exposure above which open ground earns an erratic outright; below it only the
        /// occasional one (a quarter of rolls) lands, so the barrens read stonier than the meadow.</summary>
        public const float FieldRockExposure = 0.35f;

        /// <summary>
        /// The island's INTERIOR rock — glacial erratics on the open high ground, exposure-biased so the
        /// wind-scoured barrens read stonier than the sheltered meadow (owner's 2026-08-01 "add rocks"
        /// ask). Pure and hashed like <see cref="ScatterRocks"/>; shares the trees' clearings via
        /// <see cref="StPetersWoods.IsPlantable"/> so no boulder blocks the village, the spawn, the
        /// crossing's approach or the dock, and stays out of the stands — an erratic under a closed
        /// canopy is invisible ground cost.
        /// </summary>
        public static List<RockSite> ScatterFieldRocks(ITidalTerrain terrain)
        {
            var sites = new List<RockSite>();
            if (terrain == null) return sites;

            float aspect = StPetersBuilder.IslandRadiusY > 0f
                ? StPetersBuilder.IslandRadiusY / StPetersBuilder.IslandRadius
                : 1f;

            for (int i = 0; i < FieldRockAttempts; i++)
            {
                // Area-uniform interior sample: sqrt on the radial roll, aspect-corrected — the same
                // ellipse the terrain measures in, inset from the beach band.
                float theta = Hash01(i, 199, 1) * 2f * Mathf.PI;
                float d = Mathf.Sqrt(Hash01(i, 199, 2)) * (StPetersBuilder.IslandRadius - 8f);
                var pos = StPetersBuilder.IslandCenter
                          + new Vector2(d * Mathf.Cos(theta), d * Mathf.Sin(theta) * aspect);

                // Open, high, plantable-clear ground only. The grass band's floor: an erratic sits in
                // the field, not on the beach (the beach already has the boulder ring).
                if (!StPetersWoods.IsPlantable(terrain, pos, GrassFloorElevation)) continue;
                float e = terrain.ElevationAt(pos);
                if (StPetersWoods.InStand(pos, e)) continue;

                // Exposure bias: barrens almost always, meadow only on a low roll.
                float exposure = StPetersWoods.ExposureAt(pos);
                if (exposure < FieldRockExposure && Hash01(i, 199, 4) > 0.25f) continue;

                // Small boulders common, big ones rare — the squared roll leans on "bs".
                float roll = Hash01(i, 199, 3);
                string sprite = roll * roll < 0.45f ? "bs" : roll * roll < 0.8f ? "bm" : "bl";
                sites.Add(new RockSite { Position = pos, Sprite = sprite });
            }
            return sites;
        }

        /// <summary>
        /// Walk a ring of the island at a hashed elliptical distance and bearing, keeping the sites that
        /// pass the terrain and clearance tests. The bearing→position map is the inverse of
        /// <see cref="HiddenHarbours.World.TidalTerrain.IslandDistance"/>'s normalisation, so a site placed
        /// at elliptical distance <c>D</c> genuinely measures <c>D</c> when the terrain samples it.
        /// </summary>
        static void AddRing(List<RockSite> sites, ITidalTerrain terrain, int count,
                            float innerDistance, float outerDistance, int salt,
                            string[] sprites, bool weatherCoastOnly = false)
        {
            if (count <= 0 || sprites == null || sprites.Length == 0) return;
            if (outerDistance <= innerDistance) return;

            float aspect = StPetersBuilder.IslandRadiusY > 0f
                ? StPetersBuilder.IslandRadiusY / StPetersBuilder.IslandRadius
                : 1f;

            for (int i = 0; i < count; i++)
            {
                // Bearing: evenly spaced, then jittered inside its own slot so the ring never reads as a
                // clock face and two rings never line up with each other (the salt separates them).
                float slot = 2f * Mathf.PI / count;
                float theta = (i + Hash01(i, salt, 1)) * slot;
                float d = Mathf.Lerp(innerDistance, outerDistance, Hash01(i, salt, 2));

                var pos = StPetersBuilder.IslandCenter
                          + new Vector2(d * Mathf.Cos(theta), d * Mathf.Sin(theta) * aspect);

                if (weatherCoastOnly && !IsWeatherCoast(pos)) continue;

                // Never above the paint floor's ceiling of usefulness, never on a crossing.
                float e = terrain.ElevationAt(pos);
                if (e < PaintFloorElevation) continue;
                if (DistanceToSegment(pos, StPetersBuilder.BerthFrom, StPetersBuilder.BerthTo)
                    <= StPetersBuilder.BerthHalfWidth + RockClearance) continue;
                if (DistanceToSegment(pos, StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo)
                    <= StPetersBuilder.SandbarHalfWidth + RockClearance) continue;

                sites.Add(new RockSite
                {
                    Position = pos,
                    Sprite = sprites[Mathf.Min(sprites.Length - 1,
                                               (int)(Hash01(i, salt, 3) * sprites.Length))],
                });
            }
        }

        // =====================================================================================
        //  shared pure helpers
        // =====================================================================================

        /// <summary>
        /// The sandbar's cobble spine — the low-tide WALKING LINE, signage the player reads to decide
        /// whether the crossing is on. This is the ONE definition: <see cref="MaterialAt"/> draws it and
        /// <c>StPetersStarterSplat</c> keeps the shore families off it, both through here, so the drawn
        /// spine and the exempted spine can never drift apart (hoisted at #391's review — the predicate
        /// used to live twice).
        /// </summary>
        public static bool IsBarSpine(Vector2 worldPos, float elevation) =>
            DistanceToSegment(worldPos, StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo)
                <= BarSpineHalfWidth
            && elevation >= BarSpineFloorElevation;

        /// <summary>Shortest distance from <paramref name="p"/> to the segment a→b (the same measure
        /// <see cref="HiddenHarbours.World.TidalTerrain"/> uses for the bar and the berth, so a material
        /// boundary lands exactly on the feature the terrain authored).</summary>
        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 <= 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return Vector2.Distance(p, a + t * ab);
        }

        /// <summary>A stable 0..1 hash of two integers + a salt — the same FNV-1a shape
        /// <see cref="StPetersBuilder"/> uses for the clam field, so the scatter is a pure function of its
        /// index and a rebuild reproduces it exactly (no <c>System.Random</c>, rule 5).</summary>
        public static float Hash01(int x, int y, int salt)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)x) * 16777619u;
                h = (h ^ (uint)y) * 16777619u;
                h = (h ^ (uint)salt) * 16777619u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
                return (h & 0xFFFFFF) / (float)0x1000000;
            }
        }
    }
}
#endif
