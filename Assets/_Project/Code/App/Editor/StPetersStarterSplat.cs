#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// The STARTER splat paint for St Peters (owner request 2026-07-30) — a subtle, deterministic
    /// first pass the owner will repaint over with the Material brush, authored through the SAME
    /// stroke code the brush uses (<see cref="TerrainSplatBrush"/>), so what the menu lays down
    /// and what his hand lays down are one footprint math.
    ///
    /// <para><b>What it paints (with restraint — a starting point, not a finished ground):</b>
    /// first the kit-v2 SHORE BANDS — FORESHORE across the wave-worked sand the tide crosses,
    /// ROCKWEED on intertidal rock up to the neap-high drying line, and above that the weather
    /// coast's LEDGE pavement and TALUS scree, split by slope — then the kit-v3 REEF BEDS as
    /// patches low in the tide (EELGRASS on the lowest ground and into the channel, OYSTERREEF and
    /// MUSSELBED on the sheltered mud above it, IRISHMOSS on the exposed rock below the weed) —
    /// then the v1 features over the top: a worn DIRT path from the village green to the slip, a
    /// second from the village to the bar head, SILT patches hugging the boat channel's edges on
    /// the flats, and a MARSH pocket in a sheltered NW hollow with a thin SEDGE fringe grading into
    /// the meadow.</para>
    ///
    /// <para><b>The beds are what the tide hides.</b> Painting them low in the tide is the entire
    /// mechanism — the sea plane covers ground below the live waterline (ADR 0012), so a bed bares
    /// and drowns on its own twice a day with no bed-specific code anywhere. Their windows are
    /// authored as tide FRACTIONS for the same reason the bands are.</para>
    ///
    /// <para><b>Bands are placed by TIDE STATE, never by elevation literals</b> — spring low, neap
    /// high and spring high all derive from <see cref="StPetersBuilder.TideAmplitude"/> and
    /// <see cref="GameConfig.DefaultNeapAmplitudeFraction"/>, and "steep" derives from the island's
    /// own beach gradient. The owner's 2026-08-01 amplitude ruling (3.5 → 2.2 m) is exactly the
    /// event that would have put a literal-placed shore metres out of position.</para>
    ///
    /// <para><b>Every position derives from builder constants</b> (<see cref="StPetersBuilder"/>'s
    /// village/berth/bar/channel geometry — the island just shrank once already; a literal here
    /// would go stale the next time it moves) and all jitter is <see cref="StPetersShoreMap.Hash01"/>
    /// (no System.Random, no DateTime — rule 5). Re-running the menu reproduces the same paint
    /// bit-for-bit over whatever is there.</para>
    /// </summary>
    public static class StPetersStarterSplat
    {
        // --- Materials (canonical splat indices — TerrainSplatBrush.MaterialNames) -------------
        public const int Silt = 6;
        public const int Dirt = 7;
        public const int Marsh = 8;
        public const int Sedge = 9;
        public const int Foreshore = 10;
        public const int Talus = 11;
        public const int Ledge = 12;
        public const int Rockweed = 13;
        public const int Musselbed = 14;
        public const int Oysterreef = 15;
        public const int Eelgrass = 16;
        public const int Irishmoss = 17;

        // =========================================================================================
        //  THE TIDE, AS THE SHORE SEES IT (kit v2 families — all derived, never authored)
        // =========================================================================================
        // Every threshold below is a TIDE STATE stated as an elevation, the same doctrine
        // StPetersShoreMap's band floors follow. That is what makes this pass survive a future tide
        // ruling: the owner's 2026-08-01 amplitude change (3.5 → 2.2 m) would have silently moved
        // every one of these families metres out of place had they been literals.

        /// <summary>Lowest water of the biggest spring tide — the bottom of everything intertidal.</summary>
        public static float SpringLowWater =>
            StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude;

        /// <summary>Highest water of the biggest spring tide. Above this, ground is never wetted.</summary>
        public static float SpringHighWater =>
            StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude;

        /// <summary>High water on a NEAP week — the ceiling of the weed belt. Ground above this
        /// dries out even on the gentlest fortnight, which is exactly why rockweed stops there.</summary>
        public static float NeapHighWater =>
            StPetersBuilder.TideMean
            + StPetersBuilder.TideAmplitude * GameConfig.DefaultNeapAmplitudeFraction;

        /// <summary>
        /// An elevation stated as a TIDE FRACTION: −1 = spring low, −<see cref="GameConfig.DefaultNeapAmplitudeFraction"/>
        /// = neap low, 0 = mean water, +neap = neap high, +1 = spring high.
        ///
        /// <para>⭐ This is the unit every v3 bed window is authored in, and it is the whole reason
        /// they survive a tide ruling. The owner's 2026-08-01 amplitude change (3.5 → 2.2 m) moved
        /// mean water not at all and spring low by 1.3 m; a bed authored at "−1.4 m" would have gone
        /// from bare-most-days to bare-almost-never without a line of code changing. Authored as a
        /// fraction, the whole zonation rescales with the sea.</para>
        /// </summary>
        public static float TideElevation(float fraction) =>
            StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude * fraction;

        /// <summary>
        /// The island's own characteristic beach gradient: the plateau falls to the reef shelf over
        /// <see cref="StPetersBuilder.IslandFalloff"/> metres of shore. "Steep" for talus can then
        /// mean something honest — steeper than this island's ordinary beach — instead of a number
        /// somebody liked.
        /// </summary>
        public static float BeachGradient =>
            (StPetersBuilder.IslandElevation - StPetersBuilder.ReefShelfInnerElevation)
            / Mathf.Max(StPetersBuilder.IslandFalloff, 1e-4f);

        /// <summary>Ground steeper than the island's OWN mean beach gradient carries scree rather
        /// than pavement.</summary>
        public static float TalusSlopeThreshold => BeachGradient * TalusSlopeFactor;

        /// <summary>The elevation at which the shore is steepest: the middle of the smoothstep
        /// falloff, halfway from the plateau to the reef shelf. Talus is thickest here — a smoothstep
        /// profile's gradient peaks at its midpoint, which is the one place on this coast where a
        /// face is actually shedding.</summary>
        public static float SteepestElevation =>
            (StPetersBuilder.IslandElevation + StPetersBuilder.ReefShelfInnerElevation) * 0.5f;

        // --- Shape tunables for the v2 families (coverage curves, not positions) ------------------
        // ⚠ Both factors are pinned to what the island's profile can actually PRODUCE. A smoothstep
        // falloff's gradient peaks at exactly 1.5× its mean, so "fully scree" at 1.5× is reached at
        // the steepest metre of the coast and nowhere else — set it higher and talus could never
        // exceed half strength anywhere on St Peters, which is how this shipped the first time.
        public const float TalusSlopeFactor = 1.0f;       // × the beach gradient — where scree starts
        public const float TalusSlopeFullFactor = 1.5f;   // × the threshold — smoothstep's peak gradient

        /// <summary>How wide a band edge feathers, in metres. The kit asks for a blend of about a
        /// metre between two materials (README §5 — wider reads as fog, since these are albedo maps
        /// with baked micro-cavity).</summary>
        public const float ShoreFeatherMetres = 1f;
        /// <summary>How far below neap high water the weed belt is densest, as a fraction of the
        /// neap range. The canopy thins toward its own drying ceiling rather than peaking at it.</summary>
        public const float RockweedPeakDrop = 0.25f;

        // Ladder positions (the channel value is BOTH blend weight and ladder step — kit README §2).
        // All base-ish by intent: this is a substrate for the owner to paint over, not a finished
        // ground. _Hi is left for his own emphasis pockets.
        public const float ForeshoreIntensity = 0.5f;    // "a working wave-ripple field"
        public const float RockweedIntensity = 0.55f;    // "a closed olive canopy"
        public const float LedgeIntensity = 0.45f;       // between intact pavement and dissected
        public const float TalusIntensity = 0.5f;        // "a closed apron"

        // =========================================================================================
        //  THE KIT V3 REEF BEDS — the ground the tide hides (owner, 2026-08-06)
        // =========================================================================================
        // Each bed is a TIDE WINDOW (lo → peak → hi, all as fractions of the tide) crossed with a
        // substrate gate and a patchiness field. Nothing here is an elevation literal, for the reason
        // TideElevation spells out.
        //
        // ⭐ WHAT MAKES THEM "HIDDEN BY TIDES" IS THE WINDOW, AND NOTHING ELSE. There is no bed-tide
        // component, no reveal script, no per-bed submerged sorting. Ground below the live waterline
        // is already covered by the Sea plane (ADR 0012's reveal; the splat quad sorts under the sea
        // at −5), so a bed painted low in the tide bares and drowns twice a day for free. That is
        // also why the four windows below ARE the feature: move one and you change what the player
        // can walk to, on which tides.
        //
        // The zonation, from the bottom up (kit README §6 + the PEI shore it is drawn from):
        //   eelgrass   the lowest paintable ground — bares only on a big spring low
        //   oysterreef sheltered mud a little above it
        //   irishmoss  the same depth band but on the EXPOSED rock, below the weed
        //   musselbed  low-to-mid on the sheltered mud — the one a player meets on an ordinary tide
        //
        // ⚠ The paint floor (−1.95 m) sits ABOVE spring low (−2.2 m), so "subtidal" cannot mean
        // "below spring low" — there is no painted ground down there to put a meadow on. It means
        // the lowest strip the painting actually reaches, which is why every peak below is kept
        // above the paint floor; a peak underneath it would cap that bed's coverage at a fraction of
        // its own intensity everywhere, and it would read as a permanently sparse bed.
        public const float EelgrassLoFraction = -1.00f, EelgrassPeakFraction = -0.80f, EelgrassHiFraction = -0.35f;
        public const float OysterLoFraction = -0.95f, OysterPeakFraction = -0.60f, OysterHiFraction = -0.20f;
        public const float IrishmossLoFraction = -0.95f, IrishmossPeakFraction = -0.55f, IrishmossHiFraction = -0.10f;
        public const float MusselLoFraction = -0.80f, MusselPeakFraction = -0.40f, MusselHiFraction = 0.05f;

        /// <summary>
        /// How big a bed is, in metres — the lattice cell of the patchiness field. A bed is a PLACE,
        /// not an elevation: without this term every metre of sheltered mud in the window would be
        /// mussels, which is a carpet, not a bed. 48 m gives features a boat crosses in a few
        /// seconds and a walker meets one of on a low-tide flat.
        /// </summary>
        public const float BedPatchCellMetres = 48f;

        /// <summary>Where a bed starts, on the patch field's −1..1 range. Above 0 so beds are the
        /// minority of eligible ground; the field is bilinear-smoothed hash noise, which concentrates
        /// toward the middle, so this is a stronger filter than the number looks.</summary>
        public const float BedPatchThreshold = 0.12f;

        /// <summary>How far past the threshold the patch reaches full strength — the bed's MARGIN,
        /// and the width of the ground the kit's Reefedge strip is drawn for.</summary>
        public const float BedPatchSoftness = 0.30f;

        // Ladder positions. All four kit base steps are already the look wanted here — "a closed
        // bed", "a working reef, channels open", "a closed meadow", "a closed turf" — so all four sit
        // at base and leave _Hi (thick and hummocked, channels choked, epiphyte crusted) for the
        // owner's own emphasis pockets, the same restraint the v2 families were painted with.
        public const float MusselIntensity = 0.5f;
        public const float OysterIntensity = 0.5f;
        public const float EelgrassIntensity = 0.5f;
        public const float IrishmossIntensity = 0.5f;

        // One hash lane per bed, so retuning one never re-rolls where the others sit.
        private const int SaltEelgrass = 71;
        private const int SaltOyster = 72;
        private const int SaltIrishmoss = 73;
        private const int SaltMussel = 74;

        // --- Stroke tunables (the owner's ask: subtle, low intensity) ---------------------------
        /// <summary>
        /// How wide the walked paths are, in metres — the dirt tread, not the worn band around it.
        ///
        /// <para><b>⭐ 1.5 m, down from 2.5 (owner, first playtest of the green-over: the paths should
        /// be NARROWER).</b> 2.5 m is a cart track; 1.5 m is a line two people have walked single file
        /// for a generation, which is what this island has. The ~1.8 m fisher is the scale reference —
        /// the tread is now a little under his own height across.</para>
        ///
        /// <para><b>⚠ TWO LAYERS READ THIS.</b> It is the splat's brush width AND the width of the bare
        /// tread <see cref="StPetersGrass.PathBareHalfWidthMetres"/> keeps grass off — because a path
        /// the ground paints as dirt and the meadow grows over is not a path. Turning it here narrows
        /// both together; there is no second number.</para>
        /// </summary>
        public const float PathWidthMetres = 1.5f;          // → brush radius 0.75
        public const float PathDabSpacingMetres = 0.75f;
        public const float PathFalloff = 0.5f;
        public const float SlipPathIntensity = 0.35f;       // village green → the slip
        public const float BarPathIntensity = 0.3f;         // village → the bar head
        public const float SiltIntensityMin = 0.3f;
        public const float SiltIntensityMax = 0.5f;
        public const float SiltRadiusMin = 3f;              // blobs 6–12 m across
        public const float SiltRadiusMax = 6f;
        public const float SiltFalloff = 0.65f;
        public const float MarshIntensity = 0.5f;
        public const float MarshRadiusMetres = 5f;          // a ~10 m pocket
        public const float MarshFalloff = 0.6f;
        public const float SedgeIntensity = 0.4f;
        public const float SedgeRadiusMetres = 3f;          // thin fringe dabs
        public const float SedgeFalloff = 0.8f;
        public const int SedgeFringeCount = 10;

        // Hash salts — one lane per feature so a tweak to one never re-rolls another.
        private const int SaltSlipPath = 61;
        private const int SaltBarPath = 62;
        private const int SaltSilt = 63;

        /// <summary>The marsh pocket's target ground: the middle of the upper sand band — just
        /// above the sand floor, below the marram line (upper intertidal, where a salt marsh
        /// actually sits). Derived from the classifier's own floors, never a literal.</summary>
        public static float MarshPocketElevation =>
            (StPetersShoreMap.SandFloorElevation + StPetersShoreMap.MarramFloorElevation) * 0.5f;

        // ============================ THE STROKE PLANS (pure — tested headless) =================

        /// <summary>
        /// The village-green → slip path: a gentle curve east across the plateau to the head of
        /// the dredged slip (<see cref="StPetersBuilder.BerthTo"/>, the shoreline end), with three
        /// hash-jittered bends so it reads as walked, not surveyed.
        /// </summary>
        public static Vector2[] VillageToSlipPath() =>
            BentPath(StPetersBuilder.VillageGreen,
                     new Vector2(StPetersBuilder.BerthTo.x, StPetersBuilder.BerthTo.y),
                     bends: 3, amplitudeMetres: 8f, salt: SaltSlipPath);

        /// <summary>The village → bar-head path (<see cref="StPetersBuilder.SandbarFrom"/> — where
        /// the low-tide walk leaves the island), two gentle bends.</summary>
        public static Vector2[] VillageToBarHeadPath() =>
            BentPath(StPetersBuilder.VillageGreen, StPetersBuilder.SandbarFrom,
                     bends: 2, amplitudeMetres: 5f, salt: SaltBarPath);

        /// <summary>A straight line bent at evenly-spaced interior points by a deterministic
        /// perpendicular offset — the "gentle curve, not a straight line" shape.</summary>
        public static Vector2[] BentPath(Vector2 from, Vector2 to, int bends, float amplitudeMetres, int salt)
        {
            var pts = new Vector2[bends + 2];
            pts[0] = from;
            pts[bends + 1] = to;
            Vector2 dir = (to - from).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            for (int i = 1; i <= bends; i++)
            {
                float t = i / (float)(bends + 1);
                float off = (StPetersShoreMap.Hash01(i, 0, salt) * 2f - 1f) * amplitudeMetres;
                pts[i] = Vector2.Lerp(from, to, t) + perp * off;
            }
            return pts;
        }

        /// <summary>One silt blob of the starter plan.</summary>
        public struct Blob
        {
            public Vector2 Center;
            public float Radius;
            public float Intensity;
        }

        /// <summary>Where the boat channel crosses the bar — the SAME lerp the terrain carves the
        /// gut with (<c>TidalTerrain.ElevationAtZones</c>), so the silt lands on the real feature.</summary>
        public static Vector2 ChannelCrossing() =>
            Vector2.Lerp(StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo,
                         StPetersBuilder.ChannelAlong);

        /// <summary>
        /// Silt patches on the flats flanking the boat channel: three blobs per side, each pushed
        /// past the channel's half-width along the bar (so they HUG the gut's edges rather than
        /// sit in it) and spread across the bar's width, all sized/placed/weighted by hash.
        /// </summary>
        public static Blob[] SiltBlobs()
        {
            Vector2 crossing = ChannelCrossing();
            Vector2 barDir = (StPetersBuilder.SandbarTo - StPetersBuilder.SandbarFrom).normalized;
            Vector2 perp = new Vector2(-barDir.y, barDir.x);
            float acrossMax = StPetersBuilder.SandbarHalfWidth - 16f;   // stay on the flats, off the deep edge

            var blobs = new Blob[6];
            int n = 0;
            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 3; i++)
            {
                int saltSide = SaltSilt + (side > 0 ? 0 : 1);
                float radius = Mathf.Lerp(SiltRadiusMin, SiltRadiusMax,
                                          StPetersShoreMap.Hash01(i, 0, saltSide));
                float along = side * (StPetersBuilder.ChannelHalfWidth + radius + 2f
                                      + StPetersShoreMap.Hash01(i, 1, saltSide) * 6f);
                float across = Mathf.Lerp(-acrossMax, acrossMax,
                                          StPetersShoreMap.Hash01(i, 2, saltSide));
                blobs[n++] = new Blob
                {
                    Center = crossing + barDir * along + perp * across,
                    Radius = radius,
                    Intensity = Mathf.Lerp(SiltIntensityMin, SiltIntensityMax,
                                           StPetersShoreMap.Hash01(i, 3, saltSide)),
                };
            }
            return blobs;
        }

        /// <summary>
        /// Find the marsh pocket: march NORTH-WEST from the island centre — the sheltered side
        /// (the weather coast faces <see cref="StPetersShoreMap.WeatherCoastFacing"/>, SE) — until
        /// the authored ground first drops to <see cref="MarshPocketElevation"/>. Terrain-derived,
        /// so it keeps finding the hollow if the island is resized again.
        /// </summary>
        public static Vector2 FindMarshPocket(Func<Vector2, float> elevationAt)
        {
            Vector2 dir = new Vector2(-1f, 1f).normalized;   // NW — opposite the weather sector
            Vector2 origin = StPetersBuilder.IslandCenter;
            for (float t = 0f; t <= 400f; t += 0.5f)
            {
                Vector2 pos = origin + dir * t;
                if (elevationAt(pos) <= MarshPocketElevation) return pos;
            }
            return origin;   // degenerate terrain — callers treat centre as "not found"
        }

        /// <summary>The sedge fringe: a thin ring of dab centres around the marsh pocket, just
        /// outside its rim, grading it into the meadow.</summary>
        public static Vector2[] SedgeFringe(Vector2 marshCenter)
        {
            var pts = new Vector2[SedgeFringeCount];
            float ringRadius = MarshRadiusMetres + 2f;
            for (int i = 0; i < SedgeFringeCount; i++)
            {
                float ang = i * (2f * Mathf.PI / SedgeFringeCount);
                pts[i] = marshCenter + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * ringRadius;
            }
            return pts;
        }

        // =========================================================================================
        //  THE KIT V2 FAMILIES — placement as a pure function of the ground (testable headless)
        // =========================================================================================

        /// <summary>One texel of ground as the v2 placement rules see it. Everything the four rules
        /// need and nothing they don't, so each rule is a pure function of a struct and can be
        /// tested without a terrain, a texture or an editor.</summary>
        public readonly struct GroundSample
        {
            /// <summary>Metres above chart datum, from the SAME analytic terrain the builder bakes.</summary>
            public readonly float Elevation;
            /// <summary>|∇elevation| in metres per metre.</summary>
            public readonly float Slope;
            /// <summary>The band material standing here — the substrate the family sits ON.</summary>
            public readonly ShoreMaterial Substrate;
            /// <summary>True on the SE weather coast (the hard, eroding one).</summary>
            public readonly bool WeatherCoast;
            /// <summary>True on the sandbar's cobble spine — the low-tide walking line.</summary>
            public readonly bool OnBarSpine;

            public GroundSample(float elevation, float slope,
                ShoreMaterial substrate, bool weatherCoast, bool onBarSpine = false)
            {
                Elevation = elevation;
                Slope = slope;
                Substrate = substrate;
                WeatherCoast = weatherCoast;
                OnBarSpine = onBarSpine;
            }

            /// <summary>Rock underfoot: the shelf platform or the weather coast's cobble. Talus,
            /// ledge and rockweed all sit on the red-bed rock ramps (kit README §5).</summary>
            public bool IsRock =>
                Substrate == ShoreMaterial.Shelf ||
                Substrate == ShoreMaterial.Shingle;

            /// <summary>Sand underfoot: the beach and the rippled flats — the Island sand ramp that
            /// Marram, Sand and Foreshore share (kit README §5).</summary>
            public bool IsSand =>
                Substrate == ShoreMaterial.Sand ||
                Substrate == ShoreMaterial.Ripple;
        }

        /// <summary>
        /// A smooth 0 → 1 → 0 hump: zero at and outside <paramref name="lo"/>/<paramref name="hi"/>,
        /// one at <paramref name="peak"/>, smoothstepped on each side. Both edges are feathered by
        /// construction, which is the whole requirement for a band that must not read as a stripe.
        /// </summary>
        public static float Hump(float value, float lo, float peak, float hi)
        {
            if (value <= lo || value >= hi) return 0f;
            return value < peak
                ? Ramp(lo, peak, value)
                : Ramp(hi, peak, value);
        }

        /// <summary>
        /// A smooth 0→1 ramp as <paramref name="value"/> crosses from <paramref name="edge0"/> to
        /// <paramref name="edge1"/>, clamped outside — HLSL's <c>smoothstep</c>.
        ///
        /// <para>⚠ NOT <c>Mathf.SmoothStep</c>, which is a different function wearing the same name:
        /// it INTERPOLATES BETWEEN its first two arguments by a 0..1 third, so
        /// <c>Mathf.SmoothStep(0.35f, 0.52f, slope)</c> returns something between 0.35 and 0.52 —
        /// never 0, and never 1. Used as a gate it reports "44% scree" on billiard-flat ground.</para>
        /// </summary>
        public static float Ramp(float edge0, float edge1, float value)
        {
            float d = edge1 - edge0;
            if (Mathf.Abs(d) < 1e-6f) return value >= edge1 ? 1f : 0f;
            float t = Mathf.Clamp01((value - edge0) / d);   // edge1 < edge0 ramps DOWN, by design
            return t * t * (3f - 2f * t);
        }

        /// <summary>How much of this texel is scree, 0..1 — the slope term talus and ledge SHARE, so
        /// the two are disjoint by construction rather than by two thresholds kept in step by hand.</summary>
        public static float Steepness(in GroundSample g) =>
            Ramp(TalusSlopeThreshold, TalusSlopeThreshold * TalusSlopeFullFactor, g.Slope);

        /// <summary>
        /// <b>Foreshore</b> — "a working wave-ripple field" (materials.json), the wave-worked sand
        /// the tide crosses twice a day. Sandy substrate only, between spring low and spring high
        /// water, strongest at mean water where the sea spends most of its time.
        /// </summary>
        public static float ForeshoreCoverage(in GroundSample g)
        {
            if (!g.IsSand) return 0f;
            return Hump(g.Elevation, SpringLowWater, StPetersBuilder.TideMean, SpringHighWater);
        }

        /// <summary>
        /// <b>Rockweed</b> — "a closed olive canopy". The intertidal weed belt: rock substrate only
        /// (never open sand — a frond needs something to hold), between spring low and spring high
        /// water, densest just below neap high, because above THAT the belt dries out even on the
        /// gentlest fortnight. Its upper edge is the boundary the kit's Weedline strip draws.
        /// </summary>
        public static float RockweedCoverage(in GroundSample g)
        {
            if (!g.IsRock) return 0f;
            float peak = NeapHighWater - (NeapHighWater - StPetersBuilder.TideMean) * RockweedPeakDrop;
            return Hump(g.Elevation, SpringLowWater, peak, SpringHighWater);
        }

        /// <summary>
        /// <b>Ledge</b> — "intact bevelled pavement … benches, scour pans, weed in the joints". The
        /// exposed rock PLATFORM of the weather coast (the sheltered side is beach and dune, with no
        /// bedrock to bare): rock substrate, FLAT — the pavement half of the slope split it shares
        /// with talus — feathered out at the paint floor below and the meadow above.
        ///
        /// <para>Deliberately NOT confined to a tide band. On this island the whole stretch between
        /// neap high water and the grass floor lies on the falloff, where the gradient never drops
        /// below ~0.47 m/m — a banded ledge was arithmetically incapable of appearing. Flatness is
        /// what actually distinguishes pavement from scree, so flatness is the whole rule, and the
        /// weed belt reaches its own answer by painting OVER this one (see the pass order).</para>
        /// </summary>
        public static float LedgeCoverage(in GroundSample g)
        {
            if (!g.WeatherCoast || !g.IsRock) return 0f;
            return (1f - Steepness(g))
                   * Ramp(StPetersShoreMap.PaintFloorElevation,
                          StPetersShoreMap.PaintFloorElevation + ShoreFeatherMetres, g.Elevation)
                   * Ramp(StPetersShoreMap.GrassFloorElevation,
                          StPetersShoreMap.GrassFloorElevation - ShoreFeatherMetres, g.Elevation);
        }

        /// <summary>
        /// <b>Talus</b> — "a scatter of fallen slabs … a closed apron". Scree at the foot of steep
        /// ground on the eroding weather coast, above the highest water (a wetted apron would be
        /// shingle, which the band ladder already draws), fading out as the meadow closes over.
        /// Steepness is the other half of the ledge split, so the two never both claim a texel.
        ///
        /// <para>The kit is explicit that a plan view at a cliff foot gets Talus, never the cliff
        /// FACE materials (Sandstone/Bank) — a face material's UVs run along and down a wall, so the
        /// plan-projected ground quad cannot address them (the same reason the four edge strips are
        /// imported but not sampled here).</para>
        ///
        /// <para><b>⭐ THIS RULE IS WHY THE COAST RESHAPE NEEDED NO SHADER CHANGE.</b> Coverage is
        /// <see cref="Steepness"/> off the LIVE terrain gradient, so when the cliff sectors landed and
        /// the plateau began falling ~3 m/m instead of the beach's ~0.23, scree appeared under every new
        /// face on its own — and the ledge benches, being flat rock low in the tide, were picked up by
        /// <see cref="LedgeCoverage"/> the same way. The classification reaches the ground through the
        /// height field, which is exactly the seam ADR 0028 exists to provide. Nothing here was retuned
        /// for cliffs; it was already right.</para>
        /// </summary>
        public static float TalusCoverage(in GroundSample g)
        {
            if (!g.WeatherCoast) return 0f;
            // Peaked at the elevation where the profile is genuinely steepest, not at the middle of
            // the band: an apron gathers under the face that is shedding, not at a convenient height.
            float peak = Mathf.Clamp(SteepestElevation,
                SpringHighWater, StPetersShoreMap.GrassFloorElevation);
            return Hump(g.Elevation, SpringHighWater, peak, StPetersShoreMap.GrassFloorElevation)
                   * Steepness(g);
        }

        // =========================================================================================
        //  THE V3 BEDS — habitat (pure, testable) and placement (patchy, deterministic), kept apart
        // =========================================================================================
        // Two different questions, so two different functions:
        //   *Coverage   "is this the right GROUND for this bed?"  — tide window x substrate
        //   PatchGateOf "is there a bed HERE?"                    — a coherent field, thresholded
        // Keeping them apart is what lets the habitat rules stay pure functions of a GroundSample
        // (no world position, no noise) while placement still gets to be patchy. Multiplying them in
        // one function would mean no test could ever ask "is the mussel window right?" without also
        // landing on a texel the patch field happened to like.

        /// <summary>A bed's tide window, in fractions of the tide — see <see cref="TideElevation"/>.</summary>
        public static float BedWindow(float elevation, float loFraction, float peakFraction, float hiFraction) =>
            Hump(elevation, TideElevation(loFraction), TideElevation(peakFraction), TideElevation(hiFraction));

        /// <summary>
        /// <b>Musselbed</b> — "a closed bed" on soft anoxic mud, low-to-mid tide on the SHELTERED
        /// side. This is the bed an ordinary low tide bares, so it is the one the player actually
        /// meets on the flats.
        ///
        /// <para>The sand gate is also what keeps the kit's own prohibition (README §6: "Musselbed
        /// and Rockweed do not belong on the same rock") true by construction rather than by a
        /// second rule that could drift: rockweed needs <c>IsRock</c>, a bed needs <c>IsSand</c>,
        /// and no texel is both. It doubles as the shelter test — the weather coast's band ladder
        /// only ever yields shingle, shelf or grass, so sand underfoot means the sheltered side.</para>
        /// </summary>
        public static float MusselbedCoverage(in GroundSample g) =>
            !g.IsSand ? 0f : BedWindow(g.Elevation, MusselLoFraction, MusselPeakFraction, MusselHiFraction);

        /// <summary>
        /// <b>Oysterreef</b> — "a working reef, channels open" on the same sheltered mud as the
        /// mussels but lower, so it bares on the bigger tides rather than every day.
        /// </summary>
        public static float OysterreefCoverage(in GroundSample g) =>
            !g.IsSand ? 0f : BedWindow(g.Elevation, OysterLoFraction, OysterPeakFraction, OysterHiFraction);

        /// <summary>
        /// <b>Eelgrass</b> — "a closed meadow" on muddy sand, the lowest paintable ground and into
        /// the boat channel. Effectively subtidal: it spends nearly all its life under water and
        /// shows through it, which is the point of putting it here rather than higher.
        /// </summary>
        public static float EelgrassCoverage(in GroundSample g) =>
            !g.IsSand ? 0f : BedWindow(g.Elevation, EelgrassLoFraction, EelgrassPeakFraction, EelgrassHiFraction);

        /// <summary>
        /// <b>Irishmoss</b> — "a closed turf" on red cobble, low on the EXPOSED rock, below the
        /// rockweed belt. The weather coast is explicit here because <see cref="GroundSample.IsRock"/>
        /// is true on the sheltered side too (its band ladder ends in shelf) — and a moss turf is a
        /// thing of the open, scoured shore.
        /// </summary>
        public static float IrishmossCoverage(in GroundSample g) =>
            !g.WeatherCoast || !g.IsRock
                ? 0f
                : BedWindow(g.Elevation, IrishmossLoFraction, IrishmossPeakFraction, IrishmossHiFraction);

        /// <summary>
        /// Is there a bed at this spot? A coherent field over world position, thresholded — the kit's
        /// rule 13 shape ("coverage moves the threshold; it never fades the objects") applied at
        /// region scale. Reuses <see cref="StPetersShoreMap.Wiggle"/> rather than growing a second
        /// noise: the position is pre-scaled so the same tested primitive yields a
        /// <see cref="BedPatchCellMetres"/> lattice instead of its native band-wiggle one.
        ///
        /// <para>Deterministic and seed-free — a pure function of position and salt (rule 5), so
        /// re-running the pass puts every bed back exactly where it was.</para>
        /// </summary>
        public static float BedPatch(Vector2 worldPos, int salt)
        {
            float field = StPetersShoreMap.Wiggle(
                worldPos * (StPetersShoreMap.BandWiggleScale / BedPatchCellMetres), salt);
            return Ramp(BedPatchThreshold, BedPatchThreshold + BedPatchSoftness, field);
        }

        /// <summary>The patch gate for one material — 1 for everything that is not a bed, because
        /// the v2 families are continuous BANDS (a foreshore really is the whole wave-worked zone)
        /// and gating them would punch holes in the shore. Uniform so the paint loop needs no
        /// special case for beds.</summary>
        public static float PatchGateOf(int material, Vector2 worldPos)
        {
            switch (material)
            {
                case Musselbed:  return BedPatch(worldPos, SaltMussel);
                case Oysterreef: return BedPatch(worldPos, SaltOyster);
                case Eelgrass:   return BedPatch(worldPos, SaltEelgrass);
                case Irishmoss:  return BedPatch(worldPos, SaltIrishmoss);
                default:         return 1f;
            }
        }

        /// <summary>True for the four kit-v3 reef beds — the materials that are PLACES rather than
        /// bands, and so the ones <see cref="PatchGateOf"/> actually gates.</summary>
        public static bool IsBed(int material) =>
            material == Musselbed || material == Oysterreef ||
            material == Eelgrass || material == Irishmoss;

        /// <summary>The four v2 families in canonical splat order, with their ladder intensities —
        /// the one list the paint pass and the tests both read.</summary>
        /// <para>ORDER IS THE LAYERING. Each is painted exclusively, so a later family takes the
        /// texels it claims from an earlier one: the bare rock platform goes down, scree covers the
        /// part of it that is falling apart, and the weed belt closes over whatever it reaches.
        /// That is also why ledge needs no tide band of its own — rockweed draws its upper edge.</para>
        public static readonly (int Material, float Intensity, string Name)[] KitV2Families =
        {
            (Foreshore, ForeshoreIntensity, "Foreshore"),
            (Ledge,     LedgeIntensity,     "Ledge"),
            (Talus,     TalusIntensity,     "Talus"),
            (Rockweed,  RockweedIntensity,  "Rockweed"),
        };

        /// <summary>
        /// The four v3 reef beds, LOWEST FIRST — and that order is the zonation. Each is painted
        /// exclusively, so a higher bed takes the overlap from the one below it, which is how a real
        /// shore stacks: eelgrass gives way to the oyster ground, the oyster ground to the mussels.
        /// Irish moss is last and conflicts with none of them — it is the only one on rock.
        ///
        /// <para>They go down AFTER the v2 families, so a bed reads as sitting ON the foreshore
        /// rather than the foreshore closing over it — and after rockweed in particular, so the moss
        /// claims the low rock the weed belt would otherwise drape all the way down.</para>
        /// </summary>
        public static readonly (int Material, float Intensity, string Name)[] KitV3Beds =
        {
            (Eelgrass,   EelgrassIntensity,  "Eelgrass"),
            (Oysterreef, OysterIntensity,    "Oysterreef"),
            (Musselbed,  MusselIntensity,    "Musselbed"),
            (Irishmoss,  IrishmossIntensity, "Irishmoss"),
        };

        /// <summary>Every rule-placed family in PAINT ORDER — v2 bands first, then the v3 beds over
        /// them. <see cref="BuildCoverage"/> returns one map per entry, in this order.</summary>
        public static readonly (int Material, float Intensity, string Name)[] ShoreFamilies =
            BuildShoreFamilies();

        private static (int, float, string)[] BuildShoreFamilies()
        {
            var all = new (int, float, string)[KitV2Families.Length + KitV3Beds.Length];
            KitV2Families.CopyTo(all, 0);
            KitV3Beds.CopyTo(all, KitV2Families.Length);
            return all;
        }

        /// <summary>
        /// HABITAT coverage for one family at one sample — "is this the right ground?" — shared by
        /// the pass and the tests. For the v2 bands this is the whole placement rule. For a v3 bed it
        /// is only half of it: the bed still has to be gated by <see cref="PatchGateOf"/>, or the
        /// window would carpet every eligible metre of the island.
        /// </summary>
        public static float CoverageOf(int material, in GroundSample g)
        {
            switch (material)
            {
                case Foreshore:  return ForeshoreCoverage(g);
                case Talus:      return TalusCoverage(g);
                case Ledge:      return LedgeCoverage(g);
                case Rockweed:   return RockweedCoverage(g);
                case Musselbed:  return MusselbedCoverage(g);
                case Oysterreef: return OysterreefCoverage(g);
                case Eelgrass:   return EelgrassCoverage(g);
                case Irishmoss:  return IrishmossCoverage(g);
                default:         return 0f;
            }
        }

        /// <summary>
        /// Sample the ground at one world position: elevation and slope from the analytic terrain
        /// (central differences over <paramref name="stepMetres"/>), substrate and sector from the
        /// CPU band classifier — the same <see cref="StPetersShoreMap"/> the shader mirrors, so the
        /// paint agrees with the ground it lands on instead of guessing at it.
        /// </summary>
        public static GroundSample SampleGround(ITidalTerrain terrain, Vector2 pos, float stepMetres)
        {
            float step = Mathf.Max(stepMetres, 1e-3f);
            float e = terrain.ElevationAt(pos);
            float dx = (terrain.ElevationAt(pos + new Vector2(step, 0f))
                        - terrain.ElevationAt(pos - new Vector2(step, 0f))) / (2f * step);
            float dy = (terrain.ElevationAt(pos + new Vector2(0f, step))
                        - terrain.ElevationAt(pos - new Vector2(0f, step))) / (2f * step);
            return new GroundSample(
                e, Mathf.Sqrt(dx * dx + dy * dy),
                StPetersShoreMap.MaterialAt(terrain, pos),
                StPetersShoreMap.IsWeatherCoast(pos),
                IsBarSpine(pos, e));
        }

        /// <summary>
        /// The sandbar's cobble spine — delegated to <see cref="StPetersShoreMap.IsBarSpine"/>, the ONE
        /// definition <see cref="StPetersShoreMap.MaterialAt"/> also draws from (hoisted at #391's
        /// review: this predicate used to be restated here, and a restated predicate can drift).
        /// </summary>
        public static bool IsBarSpine(Vector2 pos, float elevation) =>
            StPetersShoreMap.IsBarSpine(pos, elevation);

        /// <summary>
        /// Build one coverage map per rule-placed family over the splat texel grid, in
        /// <see cref="ShoreFamilies"/> order (v2 bands, then v3 beds). One sweep of the terrain feeds
        /// them all (the classifier is the expensive part and none of the rules disagree about the
        /// ground), and the result is a plain float array per family — no Unity types, so a test can
        /// assert on it directly.
        /// </summary>
        public static float[][] BuildCoverage(ITidalTerrain terrain, int width, int height,
            Vector2 worldMin, Vector2 worldSize)
        {
            var maps = new float[ShoreFamilies.Length][];
            for (int f = 0; f < maps.Length; f++) maps[f] = new float[width * height];
            if (terrain == null) return maps;

            // Differentiate over one texel — the grid the paint actually lands on, so the slope
            // term cannot resolve detail the paint has no way to draw.
            float stepMetres = Mathf.Min(worldSize.x / Mathf.Max(width, 1),
                                         worldSize.y / Mathf.Max(height, 1));

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var pos = new Vector2(worldMin.x + (x + 0.5f) / width * worldSize.x,
                                      worldMin.y + (y + 0.5f) / height * worldSize.y);
                GroundSample g = SampleGround(terrain, pos, stepMetres);
                if (g.Substrate == ShoreMaterial.None) continue;   // unpainted seabed

                // ⭐ THE WALKING LINE IS SIGNAGE, NOT SCENERY. The bar's cobble spine reports as
                // Shingle — rock — so rockweed would happily drape the one strip of ground the
                // player reads to decide whether the crossing is on. StPetersShoreMap exempts the
                // spine from the band wiggle for exactly this reason ("a weathered edge on scenery
                // is atmosphere; a weathered edge on signage is a lie"); dressing it in weed is the
                // same lie in a different coat. The flanking sand flats are fair game and still get
                // their foreshore — it is the crest, and only the crest, that stays bare cobble.
                if (g.OnBarSpine) continue;

                int idx = y * width + x;
                for (int f = 0; f < ShoreFamilies.Length; f++)
                {
                    int material = ShoreFamilies[f].Material;
                    // Habitat x placement. PatchGateOf is 1 for the v2 bands, so this multiply is
                    // free for them and is the whole difference between a band and a bed.
                    maps[f][idx] = CoverageOf(material, g) * PatchGateOf(material, pos);
                }
            }
            return maps;
        }

        // ============================ THE MENU / BATCH ENTRY ====================================

        // Filed under Art/ beside the assets it REGENERATES, not under Tools/ beside the brushes:
        // this is a destructive one-shot that replaces hand-painting, and sitting at brush priority
        // made it read like one more brush. The confirm dialog's TITLE tracks this verb — the
        // rename only protects him if the dialog it pops says the same word the menu did; its
        // body text and its fire-only-when-paint-exists condition are unchanged.
        [MenuItem("Hidden Harbours/Art/Regenerate St Peters Starter Splat (replaces hand-painting)",
                  priority = 25)]
        public static void PaintMenu()
        {
            // The pass re-derives the maps from scratch every run (that is what makes it
            // idempotent), so it REPLACES hand-painting rather than adding to it. Only ask when
            // there is something to lose — a first run on blank maps needs no ceremony.
            if (TerrainSplatAssets.AllExist() &&
                !EditorUtility.DisplayDialog("Regenerate St Peters Starter Splat",
                    "This re-derives all five splat maps from the terrain and REPLACES what is in " +
                    "them — including any hand-painting you have done with the Material brush.\n\n" +
                    "Re-run it after re-baking the seabed. Otherwise, cancel and paint by hand.",
                    "Replace the splat maps", "Cancel"))
                return;

            if (Paint())
                Debug.Log("[StPetersStarterSplat] Starter splat painted. Open the Terrain Paint " +
                          "Tool's Material brush to repaint it your way, or rebuild St Peters to " +
                          "see it wired in the scene.");
        }

        /// <summary>Batch entry point for <c>-executeMethod</c> (the seabed re-bake's pattern):
        /// paints the starter splat headlessly, exiting nonzero on failure.</summary>
        public static void PaintStarterSplatFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                if (!Paint()) EditorApplication.Exit(1);
                AssetDatabase.SaveAssets();
            }
            catch (Exception e)
            {
                Debug.LogError("[StPetersStarterSplat] (batch) starter paint threw: " + e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// The whole starter pass, as a function of the buffers and the terrain — no assets, no
        /// Unity objects beyond the terrain interface, so a test can run it twice and compare.
        ///
        /// <para><b>⚠ It CLEARS the buffers first, and that is the point.</b> Every stroke here is
        /// exclusive, and an exclusive stroke lerps its own channel FROM WHATEVER IS THERE — so
        /// painting over a previous pass converges toward the target instead of reproducing it, and
        /// a second run produces different pixels from the first. That is exactly the "accumulate"
        /// failure a re-derivable pass must not have: after the owner re-bakes his seabed, re-running
        /// this has to re-derive the shore cleanly, not blend the new answer into the old one.
        /// Starting from zero makes the output a pure function of the terrain.</para>
        ///
        /// <para>The cost is that a re-run DISCARDS hand-painting. That is why the interactive menu
        /// asks first and the log says so — once the owner has tuned these maps they are his data.</para>
        /// </summary>
        public static void PaintInto(Color[][] layers, int w, int h,
            Vector2 worldMin, Vector2 worldSize, ITidalTerrain terrain)
        {
            for (int t = 0; t < layers.Length; t++)
                System.Array.Clear(layers[t], 0, layers[t].Length);

            float pathRadius = PathWidthMetres * 0.5f;

            // 0) THE RULE-PLACED SHORE — the kit v2 families (foreshore, ledge, talus, rockweed) as
            //    BANDS, then the kit v3 reef beds (eelgrass, oysterreef, musselbed, irishmoss) as
            //    PLACES over them, all from the tide and the band classifier (see the rules above).
            //
            //    ⭐ These go down FIRST, before the v1 features. A dirt path crossing a foreshore
            //    should read as a path, so the features must win — and because an exclusive stroke
            //    only ever lerps its own channel from what is beneath, painting the bands first
            //    leaves the v1 channels' arithmetic untouched: dirt/silt/marsh/sedge still lerp
            //    from 0 and land on exactly the values they did before this pass existed.
            //
            //    The beds sit inside this same loop rather than in a pass of their own, so the
            //    exclusive layering runs in one order: ShoreFamilies IS the stacking, bottom to top.
            float[][] coverage = BuildCoverage(terrain, w, h, worldMin, worldSize);
            for (int f = 0; f < ShoreFamilies.Length; f++)
            {
                var fam = ShoreFamilies[f];
                TerrainSplatBrush.PaintField(layers, w, h,
                    fam.Material, fam.Intensity, coverage[f], exclusive: true);
            }

            // 1) The dirt paths — the green to the slip, the green to the bar head.
            TerrainSplatBrush.PaintPolyline(layers, w, h, worldMin, worldSize,
                VillageToSlipPath(), PathDabSpacingMetres, pathRadius, PathFalloff,
                Dirt, SlipPathIntensity, exclusive: true);
            TerrainSplatBrush.PaintPolyline(layers, w, h, worldMin, worldSize,
                VillageToBarHeadPath(), PathDabSpacingMetres, pathRadius, PathFalloff,
                Dirt, BarPathIntensity, exclusive: true);

            // 2) Silt hugging the boat channel's edges on the flats.
            foreach (Blob blob in SiltBlobs())
                TerrainSplatBrush.Dab(layers, w, h, worldMin, worldSize, blob.Center,
                    blob.Radius, SiltFalloff, Silt, blob.Intensity, 1f, exclusive: true);

            // 3) The marsh pocket in the sheltered NW hollow + its sedge fringe (fringe second,
            //    exclusive, so it eats the pocket's rim into a grade).
            Vector2 marsh = FindMarshPocket(terrain.ElevationAt);
            if (marsh != StPetersBuilder.IslandCenter)
            {
                TerrainSplatBrush.Dab(layers, w, h, worldMin, worldSize, marsh,
                    MarshRadiusMetres, MarshFalloff, Marsh, MarshIntensity, 1f, exclusive: true);
                foreach (Vector2 p in SedgeFringe(marsh))
                    TerrainSplatBrush.Dab(layers, w, h, worldMin, worldSize, p,
                        SedgeRadiusMetres, SedgeFalloff, Sedge, SedgeIntensity, 1f, exclusive: true);
            }
            else Debug.LogWarning("[StPetersStarterSplat] no NW hollow at the marsh elevation — " +
                                  "marsh + sedge skipped.");
        }

        /// <summary>
        /// Author the starter pass into the splat PNGs (creating them blank if absent) via the
        /// shared stroke code, then commit with the linear-data importer. Deterministic and
        /// IDEMPOTENT: the maps are re-derived from the terrain every run, so running it twice
        /// leaves byte-identical PNGs. See <see cref="PaintInto"/> for what that costs.
        /// </summary>
        public static bool Paint()
        {
            RegionDef region = TerrainPaintTool.DefaultRegion();
            if (region == null || !region.HasUsableExtent)
            {
                Debug.LogError("[StPetersStarterSplat] region.st_peters missing or has an unusable " +
                               "extent — nothing to size the splat maps from.");
                return false;
            }

            Vector2 worldSize = region.WorldSizeMeters;
            Vector2 worldMin = region.WorldCenter - worldSize * 0.5f;
            Vector2Int texels = region.SeabedTexels;

            var textures = new Texture2D[TerrainSplatBrush.TextureCount];
            var pixels = new Color[TerrainSplatBrush.TextureCount][];
            if (!TerrainSplatAssets.LoadOrCreate(texels, textures, pixels)) return false;
            int w = textures[0].width, h = textures[0].height;

            // The authored terrain the marsh finder reads — a transient TidalTerrain configured
            // with the canon St Peters zones (the BakeStPetersSeabed pattern), discarded after.
            var go = EditorUtility.CreateGameObjectWithHideFlags("~StarterSplat", HideFlags.HideAndDontSave,
                                                                 typeof(TidalTerrain));
            var terrain = go.GetComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(terrain);

            try
            {
                PaintInto(pixels, w, h, worldMin, worldSize, terrain);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            TerrainSplatAssets.Commit(textures, pixels);

            // Re-feed any open splat surface from the RELOADED assets (Commit reimported them —
            // the old in-memory references are invalid; wire only the fresh loads).
            foreach (var s in Object.FindObjectsByType<HiddenHarbours.Art.TerrainSplatSurface>())
            {
                s.ConfigureSplat(textures[0], textures[1], textures[2], textures[3], textures[4]);
                if (s.isActiveAndEnabled) { s.enabled = false; s.enabled = true; }   // OnEnable → MPB push
            }

            Debug.Log($"[StPetersStarterSplat] painted the starter pass into {TerrainSplatAssets.PathOf(0)} " +
                      $"/B/C/D/E at {w} × {h} texels. Kit-v2 shore bands: foreshore on sand " +
                      $"{SpringLowWater:0.##}..{SpringHighWater:0.##} m, rockweed on rock to neap high " +
                      $"({NeapHighWater:0.##} m), ledge + talus on the weather coast above it (split at " +
                      $"{TalusSlopeThreshold:0.##} m/m, {BeachGradient:0.##} × {TalusSlopeFactor:0.##}). " +
                      $"Kit-v3 reef beds in {BedPatchCellMetres:0.#} m patches: eelgrass " +
                      $"{TideElevation(EelgrassLoFraction):0.##}..{TideElevation(EelgrassHiFraction):0.##} m, " +
                      $"oysterreef {TideElevation(OysterLoFraction):0.##}..{TideElevation(OysterHiFraction):0.##} m " +
                      $"and musselbed {TideElevation(MusselLoFraction):0.##}..{TideElevation(MusselHiFraction):0.##} m " +
                      $"on the sheltered mud, irishmoss " +
                      $"{TideElevation(IrishmossLoFraction):0.##}..{TideElevation(IrishmossHiFraction):0.##} m on the " +
                      $"exposed rock (truncated below by the paint floor at " +
                      $"{StPetersShoreMap.PaintFloorElevation:0.##} m). Every window tops out under high " +
                      "water, so the tide bares and drowns them. " +
                      $"Features over the top: dirt green→slip ({SlipPathIntensity:0.##}) and green→bar " +
                      $"head ({BarPathIntensity:0.##}), {SiltBlobs().Length} silt blobs at the channel, a " +
                      "marsh pocket + sedge fringe NW. Subtle by design — repaint it with the Material brush.");
            return true;
        }
    }
}
#endif
