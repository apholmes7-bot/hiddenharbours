#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// The STARTER splat paint for <b>Nine Mile Creek</b> — the mainland's answer to
    /// <see cref="StPetersStarterSplat"/>, and the pass that closes the owner's report that "sailing into
    /// NMC … the terrain is not painted". A subtle, deterministic first ground the owner will repaint over
    /// with the Material brush, authored through the SAME stroke code the brush uses
    /// (<see cref="TerrainSplatBrush"/>), so what the menu lays down and what his hand lays down are one
    /// footprint math.
    ///
    /// <para><b>What it paints, bottom to top:</b> the NATURAL SHORE first — foreshore across the
    /// wave-worked sand of the south beach and the bar's landing, and on the plan's rock sectors the ledge
    /// pavement, the talus apron and the rockweed belt — then the REEF BEDS low in the tide (eelgrass in
    /// the barachois and the gut, oysters and the mussel beds on the sheltered flats this fishery works,
    /// irish moss on the exposed rock) — and finally the WORKED GROUND over the top: the spit's red-earth
    /// yard, gravel on the breakwater and the two rock-armour runs, and the two ponds' silt, marsh and
    /// sedge.</para>
    ///
    /// <para><b>⭐ THE PLACE IS A WORKING WHARF ON MADE GROUND, and the paint says so.</b> The natural
    /// shore families are held OFF the fills and the worked families held ON them
    /// (<see cref="NineMileCreekShoreMap.IsMadeGround"/>) — because a yard bulldozed out onto a shoal does
    /// not grow marram and does not weather into a beach. That one gate is what stops the wharf reading as
    /// a flat bit of foreshore with sheds on it.</para>
    ///
    /// <para><b>⭐ ROCK COMES FROM THE COAST PLAN, NOT FROM A BEARING.</b> Every rock family gates on
    /// <see cref="NineMileCreekShoreMap.IsRockCoastAt"/>, which asks
    /// <c>MainlandTidalTerrain.CoastClassAt</c> — the same decider that stands the cliff walls up. So the
    /// painted rock lands on the rock the terrain actually has, and re-tuning the coast plan moves both
    /// together. See <see cref="NineMileCreekShoreMap"/> for why the shader's own sector is neutralised
    /// instead.</para>
    ///
    /// <para><b>Every window is a TIDE FRACTION and every position derives from
    /// <see cref="NineMileCreekMainland"/></b> — the region's geography file, which is where the owner
    /// re-tunes this coast. All jitter is <see cref="StPetersShoreMap.Hash01"/> (no <c>System.Random</c>,
    /// no <c>DateTime</c> — rule 5), so re-running the menu reproduces the same paint bit for bit over
    /// whatever is there.</para>
    ///
    /// <para><b>⚠ NO WORLD SEED, and that is deliberate rather than an omission.</b> These maps are
    /// COMMITTED AUTHORED DATA — five PNGs in <c>Data/Terrain</c> the owner then paints over by hand — not
    /// a runtime simulation. Mixing <c>worldSeed</c> into them would make the committed asset belong to one
    /// save file, and re-deriving it after a re-bake would depend on which game was last open. The
    /// scatter is a pure function of world POSITION, exactly as St Peters' is.</para>
    ///
    /// <para><b>⚠ ROADS ARE NOT PAINTED HERE.</b> The road kit's own arc owns them
    /// (<see cref="NineMileCreekMainland.BarRoad"/> / <c>WharfRoad</c> / <c>ThroughRoad</c> / the gully
    /// path). This pass lays the GROUND those roads will be drawn on and stops there.</para>
    /// </summary>
    public static class NineMileCreekStarterSplat
    {
        // --- Materials (canonical splat indices — TerrainSplatBrush.MaterialNames) ---------------------
        public const int Sand = 2;
        public const int Shingle = 3;
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

        /// <summary>The splat stem for this region — <c>NineMileCreekSplatA…E.png</c> in
        /// <c>Data/Terrain</c>. Stems are APPEND-ONLY and belong beside the region they name (the same
        /// discipline the seabed bakes follow); this is the one #522 added the overloads for.</summary>
        public const string SplatBaseName = "NineMileCreekSplat";

        // =================================================================================================
        //  THE TIDE AND THE GROUND — everything derived, nothing typed
        // =================================================================================================

        /// <inheritdoc cref="NineMileCreekShoreMap.SpringLowWater"/>
        public static float SpringLowWater => NineMileCreekShoreMap.SpringLowWater;
        /// <inheritdoc cref="NineMileCreekShoreMap.SpringHighWater"/>
        public static float SpringHighWater => NineMileCreekShoreMap.SpringHighWater;
        /// <inheritdoc cref="NineMileCreekShoreMap.NeapHighWater"/>
        public static float NeapHighWater => NineMileCreekShoreMap.NeapHighWater;
        /// <inheritdoc cref="NineMileCreekShoreMap.TideElevation"/>
        public static float TideElevation(float fraction) => NineMileCreekShoreMap.TideElevation(fraction);

        /// <summary>
        /// This coast's OWN characteristic beach gradient: the fields fall to the shelf's inner edge over
        /// <see cref="NineMileCreekMainland.ShoreFalloff"/> metres of shore. "Steep" for talus can then mean
        /// something honest — steeper than this coast's ordinary beach — instead of a number somebody liked.
        /// </summary>
        public static float BeachGradient =>
            (NineMileCreekMainland.LandElevation - NineMileCreekMainland.ShelfInnerElevation)
            / Mathf.Max(NineMileCreekMainland.ShoreFalloff, 1e-4f);

        /// <summary>Ground steeper than this coast's OWN mean beach gradient carries scree rather than
        /// pavement.</summary>
        public static float TalusSlopeThreshold => BeachGradient * TalusSlopeFactor;

        // ⚠ Both factors are pinned to what the profile can actually PRODUCE. A smoothstep falloff's
        // gradient peaks at exactly 1.5× its mean, so "fully scree" at 1.5× is reached where a face is
        // genuinely shedding and nowhere else — set it higher and talus could never exceed half strength
        // anywhere, which is how St Peters' first pass shipped.
        public const float TalusSlopeFactor = 1.0f;       // × the beach gradient — where scree starts
        public const float TalusSlopeFullFactor = 1.5f;   // × the threshold — smoothstep's peak gradient

        /// <summary>How wide a band edge feathers, in metres. The kit asks for a blend of about a metre
        /// between two materials (README §5 — wider reads as fog, since these are albedo maps with baked
        /// micro-cavity).</summary>
        public const float ShoreFeatherMetres = 1f;

        /// <summary>How far below neap high water the weed belt is densest, as a fraction of the neap
        /// range. The canopy thins toward its own drying ceiling rather than peaking at it.</summary>
        public const float RockweedPeakDrop = 0.25f;

        // Ladder positions (a channel value is BOTH blend weight and ladder step — kit README §2). All
        // base-ish by intent: this is a substrate for the owner to paint over, not a finished ground, and
        // _Hi is left for his own emphasis pockets.
        public const float ForeshoreIntensity = 0.5f;    // "a working wave-ripple field"
        public const float RockweedIntensity = 0.55f;    // "a closed olive canopy"
        public const float LedgeIntensity = 0.45f;       // between intact pavement and dissected
        public const float TalusIntensity = 0.5f;        // "a closed apron"

        // =================================================================================================
        //  THE REEF BEDS — the ground the tide hides
        // =================================================================================================
        // Each bed is a TIDE WINDOW (lo → peak → hi, all fractions) crossed with a ground gate and a
        // patchiness field. Painting them low in the tide is the ENTIRE mechanism: the sea plane covers
        // ground below the live waterline (ADR 0012), so a bed bares and drowns on its own twice a day with
        // no bed-specific code anywhere.
        //
        // ⭐ THE MUSSEL BED IS THIS REGION'S FISHERY, not decoration. Nine Mile Creek's fleet works mussels
        // (the wharf photographs and the fleet plan both), so its window is the widest of the four and the
        // one an ordinary low tide bares — the bed a player actually meets on the flats.
        //
        // ⚠ The paint floor (−1.95 m) sits ABOVE spring low (−2.2 m), so "subtidal" cannot mean "below
        // spring low" — there is no painted ground down there to put a meadow on. Every peak below is kept
        // above the paint floor; a peak underneath it would cap that bed's coverage at a fraction of its own
        // intensity everywhere and read as a permanently sparse bed.
        public const float EelgrassLoFraction = -1.00f, EelgrassPeakFraction = -0.80f, EelgrassHiFraction = -0.30f;
        public const float OysterLoFraction = -0.95f, OysterPeakFraction = -0.60f, OysterHiFraction = -0.20f;
        public const float IrishmossLoFraction = -0.95f, IrishmossPeakFraction = -0.55f, IrishmossHiFraction = -0.10f;
        public const float MusselLoFraction = -0.85f, MusselPeakFraction = -0.40f, MusselHiFraction = 0.10f;

        /// <summary>How big a bed is, in metres — the lattice cell of the patchiness field. A bed is a
        /// PLACE, not an elevation: without this term every metre of sheltered mud in the window would be
        /// mussels, which is a carpet, not a bed.</summary>
        public const float BedPatchCellMetres = 48f;

        /// <summary>Where a bed starts, on the patch field's −1..1 range. Above 0 so beds are the minority
        /// of eligible ground.</summary>
        public const float BedPatchThreshold = 0.12f;

        /// <summary>How far past the threshold the patch reaches full strength — the bed's MARGIN.</summary>
        public const float BedPatchSoftness = 0.30f;

        public const float MusselIntensity = 0.5f;
        public const float OysterIntensity = 0.5f;
        public const float EelgrassIntensity = 0.5f;
        public const float IrishmossIntensity = 0.5f;

        // =================================================================================================
        //  THE WORKED GROUND — what people built and what the ponds left
        // =================================================================================================

        /// <summary>The spit's red-earth yard. "A worn dirt yard" — the ground the shanty row, the trap
        /// stacks, the buyers' trucks and the boatyard stand on.</summary>
        public const float YardDirtIntensity = 0.55f;

        /// <summary>The wharf DECKS and the breakwater crest read as gravel/ballast rather than earth: a
        /// crib breakwater is log boxes filled with stone, and a quay deck is hardcore.</summary>
        public const float WharfGravelIntensity = 0.5f;

        /// <summary>The rock armour along the two exposed faces — coarser and stronger than the deck.</summary>
        public const float ArmourIntensity = 0.65f;
        /// <summary>How far (m) either side of an armour line the rubble reaches.</summary>
        public const float ArmourHalfWidthMetres = 4f;
        public const float ArmourDabSpacingMetres = 1.5f;
        public const float ArmourFalloff = 0.55f;

        /// <summary>The barachois floor: red mud at low water — "the brightest thing in the owner's first
        /// photograph, and free P1 texture from one carve".</summary>
        public const float PondSiltIntensity = 0.5f;

        /// <summary>Salt marsh around a pond's rim — the upper-intertidal ground a barachois margin
        /// actually carries.</summary>
        public const float PondMarshIntensity = 0.5f;
        /// <summary>A thin sedge fringe grading the marsh into the fields above it.</summary>
        public const float PondSedgeIntensity = 0.4f;
        /// <summary>How far above a pond's rim marsh gives way to sedge, in metres of elevation.</summary>
        public const float PondFringeMetres = 0.6f;

        // One hash lane per feature, so retuning one never re-rolls where the others sit. Distinct from
        // St Peters' lanes (61-63, 71-74) — same primitive, different region.
        private const int SaltEelgrass = 81;
        private const int SaltOyster = 82;
        private const int SaltIrishmoss = 83;
        private const int SaltMussel = 84;

        // =================================================================================================
        //  SHAPE HELPERS (pure — the same two St Peters uses, so the coasts agree about what a band is)
        // =================================================================================================

        /// <summary>A smooth 0 → 1 → 0 hump: zero at and outside lo/hi, one at peak, smoothstepped each
        /// side. Both edges feather by construction, which is the whole requirement for a band that must
        /// not read as a stripe.</summary>
        public static float Hump(float value, float lo, float peak, float hi) =>
            StPetersStarterSplat.Hump(value, lo, peak, hi);

        /// <summary>HLSL's <c>smoothstep</c> — a smooth 0→1 ramp across edge0..edge1, clamped outside.
        /// ⚠ NOT <c>Mathf.SmoothStep</c>, which interpolates BETWEEN its first two arguments and, used as
        /// a gate, reports "44% scree" on billiard-flat ground.</summary>
        public static float Ramp(float edge0, float edge1, float value) =>
            StPetersStarterSplat.Ramp(edge0, edge1, value);

        // =================================================================================================
        //  ONE TEXEL OF GROUND, AS THE PLACEMENT RULES SEE IT
        // =================================================================================================

        /// <summary>Everything the rules need and nothing they don't, so each rule is a pure function of a
        /// struct and can be tested without a terrain, a texture or an editor.</summary>
        public readonly struct GroundSample
        {
            /// <summary>Metres above chart datum, from the SAME analytic terrain the builder bakes.</summary>
            public readonly float Elevation;
            /// <summary>|∇elevation| in metres per metre.</summary>
            public readonly float Slope;
            /// <summary>The band material standing here — the substrate the family sits ON.</summary>
            public readonly ShoreMaterial Substrate;
            /// <summary>True where the COAST PLAN stands rock in front of this ground (one of the three
            /// cliff classes). The mainland's replacement for the island's weather-coast bearing.</summary>
            public readonly bool RockCoast;
            /// <summary>How much of this ground is one of the region's fills — the spit, the decks, the
            /// shoal, the breakwater. Ground that was built, not weathered. A WEIGHT rather than a flag,
            /// so the painted edge feathers over the metres the terrain itself eases over.</summary>
            public readonly float MadeGround;
            /// <summary>How much of this ground is the barachois or the marsh pool — sheltered, muddy,
            /// brackish. <inheritdoc cref="MadeGround"/></summary>
            public readonly float PondGround;
            /// <summary>True on the tidal bar's cobble spine — the low-tide walking line to St Peters.</summary>
            public readonly bool OnBarSpine;

            public GroundSample(float elevation, float slope, ShoreMaterial substrate,
                bool rockCoast, float madeGround = 0f, float pondGround = 0f, bool onBarSpine = false)
            {
                Elevation = elevation;
                Slope = slope;
                Substrate = substrate;
                RockCoast = rockCoast;
                MadeGround = Mathf.Clamp01(madeGround);
                PondGround = Mathf.Clamp01(pondGround);
                OnBarSpine = onBarSpine;
            }

            /// <summary>How much of this texel is natural, un-built shore — the ground the sea and the
            /// weather made. Every natural family multiplies by this, which is the one rule that keeps the
            /// wharf from reading as a beach.</summary>
            public float NaturalShore => 1f - MadeGround;

            /// <summary>Soft ground — sand, mud, flats. The plan says this stretch of coast is not rock.</summary>
            public bool IsSoftCoast => !RockCoast;
        }

        /// <summary>How much of this texel is scree, 0..1 — the slope term talus and ledge SHARE, so the
        /// two are disjoint by construction rather than by two thresholds kept in step by hand.</summary>
        public static float Steepness(in GroundSample g) =>
            Ramp(TalusSlopeThreshold, TalusSlopeThreshold * TalusSlopeFullFactor, g.Slope);

        // =================================================================================================
        //  THE NATURAL SHORE (pure functions of a GroundSample — testable headless)
        // =================================================================================================

        /// <summary>
        /// <b>Foreshore</b> — "a working wave-ripple field": the wave-worked sand the tide crosses twice a
        /// day. The south beach, the bar's landing and the north beach. Soft coast only, off the made
        /// ground and out of the ponds (a barachois is a lagoon, not a surf-worked strand), between spring
        /// low and spring high water, strongest at mean water where the sea spends most of its time.
        /// </summary>
        public static float ForeshoreCoverage(in GroundSample g)
        {
            if (!g.IsSoftCoast) return 0f;
            return Hump(g.Elevation, SpringLowWater, NineMileCreekMainland.TideMean, SpringHighWater)
                   * g.NaturalShore * (1f - g.PondGround);
        }

        /// <summary>
        /// <b>Rockweed</b> — "a closed olive canopy". The intertidal weed belt on the plan's rock sectors
        /// (never open sand — a frond needs something to hold), between spring low and spring high water,
        /// densest just below neap high because above THAT the belt dries out even on the gentlest
        /// fortnight. Its upper edge is the boundary the kit's Weedline strip draws.
        /// </summary>
        public static float RockweedCoverage(in GroundSample g)
        {
            if (!g.RockCoast) return 0f;
            float peak = NeapHighWater
                         - (NeapHighWater - NineMileCreekMainland.TideMean) * RockweedPeakDrop;
            return Hump(g.Elevation, SpringLowWater, peak, SpringHighWater) * g.NaturalShore;
        }

        /// <summary>
        /// <b>Ledge</b> — "intact bevelled pavement … benches, scour pans, weed in the joints". The exposed
        /// rock PLATFORM of the cliff coast: rock sectors, FLAT — the pavement half of the slope split it
        /// shares with talus — feathered out at the paint floor below and the meadow above.
        ///
        /// <para>Deliberately NOT confined to a tide band: flatness is what distinguishes pavement from
        /// scree, and the weed belt reaches its own answer by painting OVER this one (see the pass order).
        /// The LedgeCliff sector's bench, which bares only on the big tides, is exactly the ground this
        /// finds.</para>
        /// </summary>
        public static float LedgeCoverage(in GroundSample g)
        {
            if (!g.RockCoast) return 0f;
            return g.NaturalShore * (1f - Steepness(g))
                   * Ramp(NineMileCreekShoreMap.PaintFloorElevation,
                          NineMileCreekShoreMap.PaintFloorElevation + ShoreFeatherMetres, g.Elevation)
                   * Ramp(NineMileCreekShoreMap.GrassFloorElevation,
                          NineMileCreekShoreMap.GrassFloorElevation - ShoreFeatherMetres, g.Elevation);
        }

        /// <summary>
        /// <b>Talus</b> — "a scatter of fallen slabs … a closed apron". Scree at the foot of steep ground on
        /// the cliff coast, above the highest water (a wetted apron would be shingle, which the band ladder
        /// already draws), fading out as the meadow closes over. Steepness is the other half of the ledge
        /// split, so the two never both claim a texel.
        ///
        /// <para>The kit is explicit that a plan view at a cliff foot gets Talus, never the cliff FACE
        /// materials (Sandstone/Bank) — a face material's UVs run along and down a wall, so the
        /// plan-projected ground quad cannot address them.</para>
        /// </summary>
        public static float TalusCoverage(in GroundSample g)
        {
            if (!g.RockCoast) return 0f;
            float peak = Mathf.Clamp(
                (SpringHighWater + NineMileCreekShoreMap.GrassFloorElevation) * 0.5f,
                SpringHighWater, NineMileCreekShoreMap.GrassFloorElevation);
            return Hump(g.Elevation, SpringHighWater, peak, NineMileCreekShoreMap.GrassFloorElevation)
                   * Steepness(g) * g.NaturalShore;
        }

        // =================================================================================================
        //  THE BEDS — habitat (pure) and placement (patchy), kept apart
        // =================================================================================================
        // Two different questions, so two different functions:
        //   *Coverage   "is this the right GROUND for this bed?"  — tide window × ground gate
        //   PatchGateOf "is there a bed HERE?"                    — a coherent field, thresholded
        // Keeping them apart is what lets the habitat rules stay pure functions of a GroundSample while
        // placement still gets to be patchy.

        /// <summary>A bed's tide window, in fractions of the tide.</summary>
        public static float BedWindow(float elevation, float loFraction, float peakFraction, float hiFraction) =>
            Hump(elevation, TideElevation(loFraction), TideElevation(peakFraction), TideElevation(hiFraction));

        /// <summary><b>Musselbed</b> — "a closed bed" on soft mud, low-to-mid tide. ⭐ This region's own
        /// fishery, and the bed an ordinary low tide bares. Off the made ground: a mussel bed under a wharf
        /// deck is a bed nobody can see and nobody built.</summary>
        public static float MusselbedCoverage(in GroundSample g) =>
            !g.IsSoftCoast
                ? 0f
                : BedWindow(g.Elevation, MusselLoFraction, MusselPeakFraction, MusselHiFraction) * g.NaturalShore;

        /// <summary><b>Oysterreef</b> — "a working reef, channels open" on the same sheltered mud as the
        /// mussels but lower, so it bares on the bigger tides rather than every day.</summary>
        public static float OysterreefCoverage(in GroundSample g) =>
            !g.IsSoftCoast
                ? 0f
                : BedWindow(g.Elevation, OysterLoFraction, OysterPeakFraction, OysterHiFraction) * g.NaturalShore;

        /// <summary><b>Eelgrass</b> — "a closed meadow" on muddy sand: the lowest paintable ground, the
        /// barachois floor and the bar's gut. Effectively subtidal — it spends nearly all its life under
        /// water and shows through it, which is the point of putting it here rather than higher.</summary>
        public static float EelgrassCoverage(in GroundSample g) =>
            !g.IsSoftCoast
                ? 0f
                : BedWindow(g.Elevation, EelgrassLoFraction, EelgrassPeakFraction, EelgrassHiFraction) * g.NaturalShore;

        /// <summary><b>Irishmoss</b> — "a closed turf" on red cobble, low on the exposed rock and below the
        /// rockweed belt. A moss turf is a thing of the open, scoured shore, so it takes the plan's rock
        /// sectors and nothing else.</summary>
        public static float IrishmossCoverage(in GroundSample g) =>
            !g.RockCoast
                ? 0f
                : BedWindow(g.Elevation, IrishmossLoFraction, IrishmossPeakFraction,
                            IrishmossHiFraction) * g.NaturalShore;

        /// <summary>
        /// Is there a bed at this spot? A coherent field over world position, thresholded — the kit's rule
        /// 13 shape ("coverage moves the threshold; it never fades the objects") at region scale. Reuses
        /// <see cref="StPetersShoreMap.Wiggle"/> rather than growing a second noise: the position is
        /// pre-scaled so the same tested primitive yields a <see cref="BedPatchCellMetres"/> lattice.
        ///
        /// <para>Deterministic and seed-free — a pure function of position and salt (rule 5).</para>
        /// </summary>
        public static float BedPatch(Vector2 worldPos, int salt)
        {
            float field = StPetersShoreMap.Wiggle(
                worldPos * (NineMileCreekShoreMap.BandWiggleScale / BedPatchCellMetres), salt);
            return Ramp(BedPatchThreshold, BedPatchThreshold + BedPatchSoftness, field);
        }

        /// <summary>The patch gate for one material — 1 for everything that is not a bed, because the shore
        /// families are continuous BANDS (a foreshore really is the whole wave-worked zone) and gating them
        /// would punch holes in the coast. Uniform so the paint loop needs no special case.</summary>
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

        /// <summary>True for the four reef beds — the materials that are PLACES rather than bands, and so
        /// the ones <see cref="PatchGateOf"/> actually gates.</summary>
        public static bool IsBed(int material) =>
            material == Musselbed || material == Oysterreef ||
            material == Eelgrass || material == Irishmoss;

        // =================================================================================================
        //  THE WORKED GROUND (pure rules too — the yard, the decks, the ponds)
        // =================================================================================================

        /// <summary>
        /// <b>Dirt</b> — the spit's red-earth yard, and only the yard. Gated on the SpitFill's own
        /// rectangle rather than on "made ground" generally, because the other fills are structures: the
        /// decks and the breakwater are ballast and stone (<see cref="WharfGravelCoverage"/>), and the
        /// harbour shoal is under water.
        /// </summary>
        public static float YardDirtCoverage(Vector2 worldPos) =>
            ZoneWeightOf(worldPos, NineMileCreekMainland.SpitFill);

        /// <summary>
        /// <b>Shingle</b> — the built stone: the two wharf decks and the crib breakwater's crest. Gravel
        /// ballast under the planking and log boxes filled with rock, which is what the reference
        /// photographs unmistakably show.
        /// </summary>
        public static float WharfGravelCoverage(Vector2 worldPos) =>
            Mathf.Max(ZoneWeightOf(worldPos, NineMileCreekMainland.NorthWallFill),
                Mathf.Max(ZoneWeightOf(worldPos, NineMileCreekMainland.WestWallFill),
                          ZoneWeightOf(worldPos, NineMileCreekMainland.BreakwaterFill)));

        /// <summary>One zone's membership as a smooth weight — 1 across its flat rectangle, easing to 0
        /// across its OWN falloff, which is the same easing the terrain gives it. See
        /// <see cref="NineMileCreekShoreMap.ZoneWeight"/> for why this is not a rectangle test.</summary>
        public static float ZoneWeightOf(Vector2 p, MainlandZone z) =>
            NineMileCreekShoreMap.ZoneWeight(p, new[] { z });

        /// <summary>
        /// <b>Silt</b> — a pond's floor: the red mudflat a barachois bares at low water. The ground below
        /// the sand floor inside either carve, feathered at its upper edge so it grades into the marsh
        /// around the rim rather than ending on a line.
        /// </summary>
        public static float PondSiltCoverage(in GroundSample g) =>
            g.PondGround * Ramp(NineMileCreekShoreMap.SandFloorElevation,
                                NineMileCreekShoreMap.SandFloorElevation - ShoreFeatherMetres, g.Elevation);

        /// <summary>
        /// <b>Marsh</b> — the salt marsh around a pond's rim: the upper-intertidal band between the mud and
        /// the fields, which is where a salt marsh actually sits.
        /// </summary>
        public static float PondMarshCoverage(in GroundSample g) =>
            g.PondGround * Hump(g.Elevation,
                                NineMileCreekShoreMap.SandFloorElevation - ShoreFeatherMetres,
                                NineMileCreekShoreMap.SandFloorElevation + PondFringeMetres,
                                NineMileCreekShoreMap.MarramFloorElevation);

        /// <summary>
        /// <b>Sedge</b> — the thin fringe above the marsh, grading the pond into the meadow. Painted after
        /// the marsh and exclusively, so it eats the pocket's rim into a grade.
        /// </summary>
        public static float PondSedgeCoverage(in GroundSample g) =>
            g.PondGround * Hump(g.Elevation,
                                NineMileCreekShoreMap.SandFloorElevation + PondFringeMetres,
                                NineMileCreekShoreMap.MarramFloorElevation,
                                NineMileCreekShoreMap.GrassFloorElevation);

        // =================================================================================================
        //  THE FAMILY TABLES — order IS the layering
        // =================================================================================================

        /// <summary>The natural shore, in paint order. Each is painted exclusively, so a later family takes
        /// the texels it claims from an earlier one: the bare rock platform goes down, scree covers the part
        /// of it that is falling apart, and the weed belt closes over whatever it reaches. That is also why
        /// ledge needs no tide band of its own — rockweed draws its upper edge.</summary>
        public static readonly (int Material, float Intensity, string Name)[] ShoreBands =
        {
            (Foreshore, ForeshoreIntensity, "Foreshore"),
            (Ledge,     LedgeIntensity,     "Ledge"),
            (Talus,     TalusIntensity,     "Talus"),
            (Rockweed,  RockweedIntensity,  "Rockweed"),
        };

        /// <summary>The four beds, LOWEST FIRST — and that order is the zonation. Each is painted
        /// exclusively, so a higher bed takes the overlap from the one below it, which is how a real shore
        /// stacks. They go down AFTER the bands so a bed reads as sitting ON the foreshore, and after
        /// rockweed in particular so the moss claims the low rock the weed belt would otherwise drape all
        /// the way down.</summary>
        public static readonly (int Material, float Intensity, string Name)[] ReefBeds =
        {
            (Eelgrass,   EelgrassIntensity,  "Eelgrass"),
            (Oysterreef, OysterIntensity,    "Oysterreef"),
            (Musselbed,  MusselIntensity,    "Musselbed"),
            (Irishmoss,  IrishmossIntensity, "Irishmoss"),
        };

        /// <summary>The ponds' own ground, low to high — silt floor, marsh rim, sedge fringe. Painted over
        /// the natural shore because a barachois is a feature ON this coast, not a stretch of it.</summary>
        public static readonly (int Material, float Intensity, string Name)[] PondGround =
        {
            (Silt,  PondSiltIntensity,  "Silt"),
            (Marsh, PondMarshIntensity, "Marsh"),
            (Sedge, PondSedgeIntensity, "Sedge"),
        };

        /// <summary>Every rule-placed family in PAINT ORDER — the natural shore, then the beds over it,
        /// then the ponds' ground over both. <see cref="BuildCoverage"/> returns one map per entry, in this
        /// order.</summary>
        public static readonly (int Material, float Intensity, string Name)[] Families = BuildFamilies();

        private static (int, float, string)[] BuildFamilies()
        {
            var all = new (int, float, string)[ShoreBands.Length + ReefBeds.Length + PondGround.Length];
            ShoreBands.CopyTo(all, 0);
            ReefBeds.CopyTo(all, ShoreBands.Length);
            PondGround.CopyTo(all, ShoreBands.Length + ReefBeds.Length);
            return all;
        }

        /// <summary>
        /// HABITAT coverage for one family at one sample — "is this the right ground?" — shared by the pass
        /// and the tests. For the bands and the pond ground this is the whole placement rule; for a bed it
        /// is only half of it (the bed still has to be gated by <see cref="PatchGateOf"/>, or the window
        /// would carpet every eligible metre of the coast).
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
                case Silt:       return PondSiltCoverage(g);
                case Marsh:      return PondMarshCoverage(g);
                case Sedge:      return PondSedgeCoverage(g);
                default:         return 0f;
            }
        }

        // =================================================================================================
        //  SAMPLING THE GROUND
        // =================================================================================================

        /// <summary>
        /// Sample the ground at one world position: elevation and slope from the analytic terrain (central
        /// differences over <paramref name="stepMetres"/>), the band material from
        /// <see cref="NineMileCreekShoreMap"/>, and the rock/made/pond gates from the region's own plan — so
        /// the paint agrees with the ground it lands on instead of guessing at it.
        /// </summary>
        public static GroundSample SampleGround(MainlandTidalTerrain terrain, Vector2 pos, float stepMetres)
        {
            float step = Mathf.Max(stepMetres, 1e-3f);
            float e = terrain.ElevationAt(pos);
            float dx = (terrain.ElevationAt(pos + new Vector2(step, 0f))
                        - terrain.ElevationAt(pos - new Vector2(step, 0f))) / (2f * step);
            float dy = (terrain.ElevationAt(pos + new Vector2(0f, step))
                        - terrain.ElevationAt(pos - new Vector2(0f, step))) / (2f * step);
            return new GroundSample(
                e, Mathf.Sqrt(dx * dx + dy * dy),
                NineMileCreekShoreMap.MaterialAt(terrain, pos),
                NineMileCreekShoreMap.IsRockCoastAt(terrain, pos),
                NineMileCreekShoreMap.MadeGroundWeight(pos),
                NineMileCreekShoreMap.PondWeight(pos),
                IsBarSpine(pos, e));
        }

        /// <summary>
        /// The tidal bar's cobble spine — the low-tide walking line across to St Peters.
        ///
        /// <para>⭐ THE WALKING LINE IS SIGNAGE, NOT SCENERY, and it is the same bar on both sides of the
        /// seam, so it takes the same spine geometry St Peters authors (half-width and crest fraction
        /// alike — <see cref="NineMileCreekMainland"/> §6 keeps the two halves mirror images on purpose).
        /// The spine reports as rock, so rockweed would happily drape the one strip of ground the player
        /// reads to decide whether the crossing is on. Dressing it in weed is a lie in a different coat.</para>
        /// </summary>
        public static bool IsBarSpine(Vector2 worldPos, float elevation) =>
            elevation >= StPetersShoreMap.BarSpineFloorElevation &&
            MainlandTidalTerrain.DistanceToSegment(
                worldPos, NineMileCreekMainland.BarFrom, NineMileCreekMainland.BarTo)
                <= StPetersShoreMap.BarSpineHalfWidth;

        /// <summary>
        /// Build one coverage map per rule-placed family over the splat texel grid, in
        /// <see cref="Families"/> order. One sweep of the terrain feeds them all (the classifier is the
        /// expensive part and none of the rules disagree about the ground), and the result is a plain float
        /// array per family — no Unity types, so a test can assert on it directly.
        /// </summary>
        public static float[][] BuildCoverage(MainlandTidalTerrain terrain, int width, int height,
            Vector2 worldMin, Vector2 worldSize)
        {
            var maps = new float[Families.Length][];
            for (int f = 0; f < maps.Length; f++) maps[f] = new float[width * height];
            if (terrain == null) return maps;

            // Differentiate over one texel — the grid the paint actually lands on, so the slope term
            // cannot resolve detail the paint has no way to draw.
            float stepMetres = Mathf.Min(worldSize.x / Mathf.Max(width, 1),
                                         worldSize.y / Mathf.Max(height, 1));

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var pos = new Vector2(worldMin.x + (x + 0.5f) / width * worldSize.x,
                                      worldMin.y + (y + 0.5f) / height * worldSize.y);
                GroundSample g = SampleGround(terrain, pos, stepMetres);
                if (g.Substrate == ShoreMaterial.None) continue;   // below the paint floor — the quad clips
                if (g.OnBarSpine) continue;                        // signage does not get weathered

                int idx = y * width + x;
                for (int f = 0; f < Families.Length; f++)
                {
                    int material = Families[f].Material;
                    // Habitat × placement. PatchGateOf is 1 for everything that is not a bed, so this
                    // multiply is free for the bands and is the whole difference between a band and a bed.
                    maps[f][idx] = CoverageOf(material, g) * PatchGateOf(material, pos);
                }
            }
            return maps;
        }

        /// <summary>The worked-ground maps — the yard's dirt and the built stone — built over the same
        /// grid. Position-only rules, so they need no terrain sample and are kept in their own sweep.</summary>
        public static float[][] BuildWorkedGround(int width, int height, Vector2 worldMin, Vector2 worldSize)
        {
            var dirt = new float[width * height];
            var gravel = new float[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var pos = new Vector2(worldMin.x + (x + 0.5f) / width * worldSize.x,
                                      worldMin.y + (y + 0.5f) / height * worldSize.y);
                int idx = y * width + x;
                dirt[idx] = YardDirtCoverage(pos);
                gravel[idx] = WharfGravelCoverage(pos);
            }
            return new[] { dirt, gravel };
        }

        // =================================================================================================
        //  THE PASS
        // =================================================================================================

        /// <summary>
        /// The whole starter pass, as a function of the buffers and the terrain — no assets, no Unity
        /// objects beyond the terrain component, so a test can run it twice and compare.
        ///
        /// <para><b>⚠ It CLEARS the buffers first, and that is the point.</b> Every stroke here is
        /// exclusive, and an exclusive stroke lerps its own channel FROM WHATEVER IS THERE — so painting
        /// over a previous pass converges toward the target instead of reproducing it, and a second run
        /// produces different pixels from the first. That is exactly the "accumulate" failure a
        /// re-derivable pass must not have: after the owner re-bakes his seabed, re-running this has to
        /// re-derive the shore cleanly, not blend the new answer into the old one. Starting from zero makes
        /// the output a pure function of the terrain.</para>
        ///
        /// <para>The cost is that a re-run DISCARDS hand-painting. That is why the interactive menu asks
        /// first and the log says so — once the owner has tuned these maps they are his data.</para>
        /// </summary>
        public static void PaintInto(Color[][] layers, int w, int h,
            Vector2 worldMin, Vector2 worldSize, MainlandTidalTerrain terrain)
        {
            for (int t = 0; t < layers.Length; t++)
                Array.Clear(layers[t], 0, layers[t].Length);

            // 1) THE RULE-PLACED GROUND — the natural shore, the beds over it, the ponds over both.
            //    Families IS the stacking, bottom to top, and every one is painted exclusively.
            float[][] coverage = BuildCoverage(terrain, w, h, worldMin, worldSize);
            for (int f = 0; f < Families.Length; f++)
            {
                var fam = Families[f];
                TerrainSplatBrush.PaintField(layers, w, h,
                    fam.Material, fam.Intensity, coverage[f], exclusive: true);
            }

            // 2) THE WORKED GROUND, over the top. ⭐ Last, and that is the reading of the place: this is a
            //    wharf built out onto a shoal, so the made ground WINS against whatever shore it was put
            //    on. A yard that let the foreshore show through would be a yard the sea had taken back.
            float[][] worked = BuildWorkedGround(w, h, worldMin, worldSize);
            TerrainSplatBrush.PaintField(layers, w, h, Dirt, YardDirtIntensity, worked[0], exclusive: true);
            TerrainSplatBrush.PaintField(layers, w, h, Shingle, WharfGravelIntensity, worked[1], exclusive: true);

            // 3) THE ROCK ARMOUR — the rubble line wherever made ground meets open water. Two runs, and
            //    only two, because those are the only faces of this wharf that are EXPOSED (the wharf head
            //    and the spit's east edge); everything else is sheltered by the breakwater or is landward.
            //    Strokes rather than a field: an armour run is a LINE the plan authors as a pair of points.
            foreach (Vector2[] run in new[] { NineMileCreekMainland.WharfHeadArmour,
                                              NineMileCreekMainland.SpitEastArmour })
                TerrainSplatBrush.PaintPolyline(layers, w, h, worldMin, worldSize,
                    run, ArmourDabSpacingMetres, ArmourHalfWidthMetres, ArmourFalloff,
                    Shingle, ArmourIntensity, exclusive: true);
        }

        /// <summary>
        /// Author the starter pass into this region's splat PNGs (creating them blank if absent) via the
        /// shared stroke code, then commit with the linear-data importer. Deterministic and IDEMPOTENT: the
        /// maps are re-derived from the terrain every run, so running it twice leaves byte-identical PNGs.
        /// See <see cref="PaintInto"/> for what that costs.
        /// </summary>
        public static bool Paint()
        {
            RegionDef region = NineMileCreekRegion();
            if (region == null || !region.HasUsableExtent)
            {
                Debug.LogError("[NineMileCreekStarterSplat] region.nine_mile_creek missing or has an " +
                               "unusable extent — nothing to size the splat maps from.");
                return false;
            }

            Vector2 worldSize = region.WorldSizeMeters;
            Vector2 worldMin = region.WorldCenter - worldSize * 0.5f;
            Vector2Int texels = region.SeabedTexels;

            var textures = new Texture2D[TerrainSplatBrush.TextureCount];
            var pixels = new Color[TerrainSplatBrush.TextureCount][];
            // ⚠ THE STEM IS THE WHOLE POINT of #522's overloads: without it every entry point here
            // resolves to the St Peters constant and this pass paints over the ISLAND'S maps, at the
            // island's resolution, on the island's world frame.
            if (!TerrainSplatAssets.LoadOrCreate(texels, textures, pixels, SplatBaseName)) return false;
            int w = textures[0].width, h = textures[0].height;

            // The authored terrain, as a transient component configured with the canon Nine Mile Creek
            // plan (the seabed bake's pattern), discarded after.
            var go = EditorUtility.CreateGameObjectWithHideFlags(
                "~NMCStarterSplat", HideFlags.HideAndDontSave, typeof(MainlandTidalTerrain));
            var terrain = go.GetComponent<MainlandTidalTerrain>();
            NineMileCreekMainland.ConfigureTerrain(terrain);

            try
            {
                PaintInto(pixels, w, h, worldMin, worldSize, terrain);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }

            TerrainSplatAssets.Commit(textures, pixels, SplatBaseName);

            // Re-feed any open splat surface from the RELOADED assets (Commit reimported them — the old
            // in-memory references are invalid; wire only the fresh loads).
            // ⚠ The no-arg overload, matching StPetersStarterSplat: the FindObjectsSortMode one is
            // OBSOLETE in 6000.5 and this repo's CI treats obsolete-as-error.
            foreach (var s in Object.FindObjectsByType<HiddenHarbours.Art.TerrainSplatSurface>())
            {
                s.ConfigureSplat(textures[0], textures[1], textures[2], textures[3], textures[4]);
                if (s.isActiveAndEnabled) { s.enabled = false; s.enabled = true; }   // OnEnable → MPB push
            }

            Debug.Log($"[NineMileCreekStarterSplat] painted the starter pass into " +
                      $"{TerrainSplatAssets.PathOf(0, SplatBaseName)} /B/C/D/E at {w} × {h} texels. " +
                      $"Shore bands: foreshore on the soft coast {SpringLowWater:0.##}..{SpringHighWater:0.##} m, " +
                      $"rockweed on the plan's rock sectors to neap high ({NeapHighWater:0.##} m), ledge + " +
                      $"talus split at {TalusSlopeThreshold:0.##} m/m ({BeachGradient:0.##} × " +
                      $"{TalusSlopeFactor:0.##}). Reef beds in {BedPatchCellMetres:0.#} m patches: eelgrass " +
                      $"{TideElevation(EelgrassLoFraction):0.##}..{TideElevation(EelgrassHiFraction):0.##} m, " +
                      $"oysterreef {TideElevation(OysterLoFraction):0.##}..{TideElevation(OysterHiFraction):0.##} m, " +
                      $"musselbed {TideElevation(MusselLoFraction):0.##}..{TideElevation(MusselHiFraction):0.##} m " +
                      $"on the sheltered flats, irishmoss " +
                      $"{TideElevation(IrishmossLoFraction):0.##}..{TideElevation(IrishmossHiFraction):0.##} m on " +
                      $"the exposed rock. Worked ground over the top: the spit's dirt yard, gravel on both " +
                      $"wharf decks and the breakwater, rubble on the two armour runs, and the barachois + " +
                      "marsh pool as silt/marsh/sedge. Subtle by design — repaint it with the Material brush.");
            return true;
        }

        /// <summary>This region's def, by id — the extent and the texel grid the maps are sized from.</summary>
        public static RegionDef NineMileCreekRegion()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:RegionDef"))
            {
                var r = AssetDatabase.LoadAssetAtPath<RegionDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (r != null && r.Id == RegionId) return r;
            }
            return null;
        }

        /// <summary>The region id this pass paints. Ids are append-only and stable (CLAUDE.md §5).</summary>
        public const string RegionId = "region.nine_mile_creek";

        // ============================ THE MENU / BATCH ENTRY ====================================

        // Filed under Art/ beside the assets it REGENERATES, not under Tools/ beside the brushes: this is a
        // destructive one-shot that replaces hand-painting. The confirm dialog's TITLE tracks this verb.
        [MenuItem("Hidden Harbours/Art/Regenerate Nine Mile Creek Starter Splat (replaces hand-painting)",
                  priority = 26)]
        public static void PaintMenu()
        {
            // The pass re-derives the maps from scratch every run (that is what makes it idempotent), so it
            // REPLACES hand-painting rather than adding to it. Only ask when there is something to lose — a
            // first run on blank maps needs no ceremony.
            if (TerrainSplatAssets.AllExist(SplatBaseName) &&
                !EditorUtility.DisplayDialog("Regenerate Nine Mile Creek Starter Splat",
                    "This re-derives all five Nine Mile Creek splat maps from the terrain and REPLACES " +
                    "what is in them — including any hand-painting you have done with the Material " +
                    "brush.\n\nRe-run it after re-baking the seabed. Otherwise, cancel and paint by hand.",
                    "Replace the splat maps", "Cancel"))
                return;

            if (Paint())
                Debug.Log("[NineMileCreekStarterSplat] Starter splat painted. Open the Terrain Paint " +
                          "Tool's Material brush to repaint it your way, or rebuild Nine Mile Creek to " +
                          "see it wired in the scene.");
        }

        /// <summary>Batch entry point for <c>-executeMethod</c> (the seabed re-bake's pattern): paints the
        /// starter splat headlessly, exiting nonzero on failure.</summary>
        public static void PaintStarterSplatFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                if (!Paint()) EditorApplication.Exit(1);
                AssetDatabase.SaveAssets();
                Debug.Log("[NineMileCreekStarterSplat] (batch) starter paint complete.");
            }
            catch (Exception e)
            {
                Debug.LogError("[NineMileCreekStarterSplat] (batch) starter paint threw: " + e);
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
