#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE SHORE OF NINE MILE CREEK — what grows and what stands on the tidal coast.</b> The mainland's
    /// answer to <see cref="StPetersShorePlants"/>, and the third pass of this region's terrain arc after
    /// the displaced water (#520) and the painted ground (#527). Pure and deterministic: every decision
    /// here is a function of the authored plan and a hash of a POSITION, so the coast is the same on every
    /// machine and every rebuild (CLAUDE.md rule 5). <see cref="NineMileCreekShorePainter"/> does the Unity
    /// work.
    ///
    /// <para><b>⭐ THE LADDER IS ST PETERS', BY REFERENCE — the same arrangement
    /// <see cref="NineMileCreekShoreMap"/> makes for the paint, for the same reason.</b> Every floor in the
    /// planting ladder is a TIDE STATE, this region authors St Peters' tide verbatim
    /// (<see cref="NineMileCreekMainland"/> §2 explains at length why it has to), and restating six numbers
    /// would be the anti-pattern this region's own geography file opens by warning against. The coupling is
    /// GUARDED rather than assumed — <see cref="NineMileCreekShoreMap.SharesTheIslandsTide"/> is the
    /// condition under which the reference is legitimate and <c>NineMileCreekShoreTests</c> holds it.</para>
    ///
    /// <para><b>⭐ AND TWO OF THE SIX FLOORS ARE THIS COAST'S OWN GEOGRAPHY, WHICH IS WHY THE SHARED
    /// NUMBERS MEAN SOMETHING HERE.</b> St Peters derived its mid-intertidal and subtidal floors from the
    /// REEF SHELF that rings the island (−1.0 m inner, −2.6 m outer). Nine Mile Creek has no reef — it has
    /// a foreshore shelf, and <see cref="NineMileCreekMainland.ShelfInnerElevation"/> /
    /// <see cref="NineMileCreekMainland.ShelfOuterElevation"/> are −1.0 m and −2.6 m. The same two numbers,
    /// reached from this region's own authored apron rather than inherited. A test asserts the coincidence
    /// rather than this paragraph claiming it, so the day either coast re-shelves, the ladder is told.</para>
    ///
    /// <para><b>⚠ THE SCATTER IS SEEDED BY POSITION, NOT BY <c>worldSeed</c>, AND THAT IS DELIBERATE.</b>
    /// The handoff asks for a layout that moves when the world seed moves. It cannot and it should not:
    /// <c>worldSeed</c> is a RUNTIME save field (<c>SaveData.WorldSeed</c>) with no reader in any editor
    /// assembly, and this pass BAKES a scene the owner commits — a decor layout that varied with a save's
    /// seed would make the committed scene disagree with itself between saves, and would break the law that
    /// the owner's one Build click reproduces the same region. <c>StPetersRoutines.PreviewSeed</c> already
    /// records the same finding in the same words: "fixed rather than random so two builder runs report the
    /// same numbers". What the request is actually reaching for — that the layout is a hash and not an
    /// accident — is what <see cref="ScatterSalt"/> is, and the determinism test drives that axis.</para>
    /// </summary>
    public static class NineMileCreekShore
    {
        // =================================================================================================
        //  1. THE TIDAL STAIRCASE — St Peters' tide states, referenced (see the type note)
        // =================================================================================================

        /// <summary>Mean high water springs. Ground above this is never wet.</summary>
        public static float HighWaterElevation => NineMileCreekMainland.SpringHighWater;

        /// <summary>Mean low water springs. Ground below this is never exposed.</summary>
        public static float LowWaterElevation => NineMileCreekMainland.SpringLowWater;

        /// <inheritdoc cref="StPetersShorePlants.HighMarshFloorElevation"/>
        public static float HighMarshFloorElevation => StPetersShorePlants.HighMarshFloorElevation;

        /// <inheritdoc cref="StPetersShorePlants.LowMarshFloorElevation"/>
        public static float LowMarshFloorElevation => StPetersShorePlants.LowMarshFloorElevation;

        /// <summary>
        /// The mid-intertidal floor — the rockweed band's bottom, and THIS COAST'S OWN SHELF LIP
        /// (<see cref="NineMileCreekMainland.ShelfInnerElevation"/>, −1.0 m). Below it the ground is the
        /// foreshore apron, which spends most of the cycle under a metre of water and is the fringe; above
        /// it is the beach's own lower face, bared and covered every tide. Identical to St Peters' figure,
        /// and a test says so rather than this comment.
        /// </summary>
        public static float MidIntertidalFloorElevation => NineMileCreekMainland.ShelfInnerElevation;

        /// <summary>
        /// 🔴 The seaward limit of planting — the shelf's OUTER lip
        /// (<see cref="NineMileCreekMainland.ShelfOuterElevation"/>, −2.6 m), where the apron drops away to
        /// the −6 m bay. Past it the water column is deeper than the seabed reads through at any tide AND
        /// the painted ground has already stopped (<see cref="NineMileCreekShoreMap.PaintFloorElevation"/>,
        /// −1.95 m), so a bed there would be an invisible draw call.
        /// </summary>
        public static float SubtidalFloorElevation => NineMileCreekMainland.ShelfOuterElevation;

        /// <inheritdoc cref="StPetersShorePlants.ShallowSeeThroughDepthM"/>
        public const float ShallowSeeThroughDepthM = StPetersShorePlants.ShallowSeeThroughDepthM;

        /// <summary>The kit's five zone keys, in the order the contract publishes them.</summary>
        public const string ZoneFringe = StPetersShorePlants.ZoneFringe;
        public const string ZoneMid = StPetersShorePlants.ZoneMid;
        public const string ZoneLowMarsh = StPetersShorePlants.ZoneLowMarsh;
        public const string ZoneHighMarsh = StPetersShorePlants.ZoneHighMarsh;
        public const string ZoneUpland = StPetersShorePlants.ZoneUpland;

        /// <summary>
        /// Which tidal zone a metre of this coast is, or null where nothing plants — too deep below, or
        /// above the upland into the FIELDS' own band.
        ///
        /// <para><b>⭐ THE UPLAND STOPS AT THE GRASS FLOOR, AND THAT IS THE SEAM WITH #538.</b>
        /// <c>NineMileCreekFields.IsGrassGround</c> starts the meadow at exactly
        /// <see cref="NineMileCreekShoreMap.GrassFloorElevation"/> (4.2 m), so the two passes ABUT at one
        /// elevation and cannot both claim a metre. The handoff's standing worry — "the two passes must not
        /// double-plant a band" — is answered by construction rather than by a clearance radius, and
        /// <c>NineMileCreekShoreTests</c> measures the seam from both sides.</para>
        /// </summary>
        public static string ZoneAt(float elevation)
        {
            if (elevation < SubtidalFloorElevation) return null;              // deep water: nothing yet
            if (elevation < MidIntertidalFloorElevation) return ZoneFringe;   // the foreshore apron
            if (elevation < LowMarshFloorElevation) return ZoneMid;           // the rockweed band
            if (elevation < HighMarshFloorElevation) return ZoneLowMarsh;
            if (elevation < HighWaterElevation) return ZoneHighMarsh;
            if (elevation < NineMileCreekShoreMap.GrassFloorElevation) return ZoneUpland;
            return null;                                                       // the fields: #538's job
        }

        // =================================================================================================
        //  2. THE FOUR TREATMENTS — what KIND of shore a metre is
        // =================================================================================================
        // ⭐ THE THING THE ISLAND HAS NO WAY TO SAY. St Peters splits its coast into "weather" and
        // "sheltered" by taking a bearing on an ellipse, because an island is a shape you can be outside
        // of. This mainland already DECLARES what each stretch of it is, and it carries two ponds and a
        // breakwater besides — so the planting reads the plan, exactly as NineMileCreekShoreMap's paint
        // does, and gets four kinds of shore instead of two.

        /// <summary>What kind of shore a metre of Nine Mile Creek is — the decider every species list
        /// below is keyed on.</summary>
        public enum ShoreTreatment
        {
            /// <summary>The open coast: the beaches, the dunes, the ledges. St Peters' own mix.</summary>
            Ordinary = 0,
            /// <summary>Sheltered, muddy, brackish: behind the spit and inside the breakwater. The
            /// barachois, the gut through the bar, and the harbour basin.</summary>
            Quiet = 1,
            /// <summary>Wave-beaten rock: the outer face of the crib breakwater, and the two standing-rock
            /// sectors the coast plan authors north of the crossing.</summary>
            Exposed = 2,
            /// <summary>Standing FRESH water — the marsh pool, which the sea cannot reach (see
            /// <see cref="MarshPoolIsFresh"/>). The one place in either region cattail honestly belongs.</summary>
            Fresh = 3,
        }

        /// <summary>Zone membership weight above which a pond's own treatment takes over. Half, so the
        /// treatment boundary lands on the terrain's own half-eased edge rather than at the rectangle —
        /// the lesson <see cref="NineMileCreekShoreMap.ZoneWeight"/> records about straight-edged paint.</summary>
        public const float PondTreatmentWeight = 0.5f;

        /// <summary>
        /// How far (m) seaward of the breakwater's crest the exposed treatment reaches. The crib's own
        /// armour toe plus a couple of metres of the scoured bottom in front of it — past that the ground
        /// is open bay and already below <see cref="SubtidalFloorElevation"/>.
        /// </summary>
        public const float BreakwaterExposedReachMetres = 10f;

        /// <summary>
        /// ⭐ <b>THE MARSH POOL IS FRESH AND THE BARACHOIS IS NOT — measured off the plan, not assumed.</b>
        ///
        /// <para><see cref="NineMileCreekMainland"/> §7 calls both carves "freshwater-side features", and
        /// for one of them that is not what the geometry says. The BARACHOIS reaches east past the
        /// coastline into the notch (its rectangle's east edge is seaward of the coast run at that
        /// latitude), which is precisely the "drained through the coastline's notch" the plan describes —
        /// so it is brackish, and it gets the quiet salt treatment. The MARSH POOL sits behind an unbroken
        /// sill of +6 m plateau: no tide reaches it at any state, so it is standing fresh water.</para>
        ///
        /// <para>Both claims are re-derived from the authored polyline by <c>NineMileCreekShoreTests</c>
        /// rather than restated, so moving either pond or the coastline moves the verdict with it.</para>
        /// </summary>
        public static bool MarshPoolIsFresh => !PondReachesTheSea(NineMileCreekMainland.MarshPoolCarve);

        /// <inheritdoc cref="MarshPoolIsFresh"/>
        public static bool BarachoisIsBrackish => PondReachesTheSea(NineMileCreekMainland.BarachoisCarve);

        /// <summary>Does a carve's flat rectangle reach seaward of the authored coastline anywhere? The
        /// question "can the tide get in", asked of the plan.</summary>
        public static bool PondReachesTheSea(MainlandZone pond)
        {
            // Walk the pond's own rectangle and ask the coast run which side of it each corner-to-corner
            // sample falls on. Project() hands back a NEGATIVE seaward for inland ground, so any positive
            // sample is a mouth.
            const int Samples = 24;
            for (int i = 0; i <= Samples; i++)
            for (int j = 0; j <= Samples; j++)
            {
                var p = new Vector2(
                    pond.Center.x + Mathf.Lerp(-pond.HalfSize.x, pond.HalfSize.x, i / (float)Samples),
                    pond.Center.y + Mathf.Lerp(-pond.HalfSize.y, pond.HalfSize.y, j / (float)Samples));
                MainlandCoast.Project(NineMileCreekMainland.CoastPoints, p, out _, out float seaward);
                if (seaward > 0f) return true;
            }
            return false;
        }

        /// <summary>
        /// The treatment in force at a world position. Order matters: the ponds are the most specific
        /// claim, then the breakwater's own outer face, then the coast plan's rock, then the open coast.
        /// </summary>
        public static ShoreTreatment TreatmentAt(MainlandTidalTerrain terrain, Vector2 p)
        {
            if (NineMileCreekShoreMap.ZoneWeight(p, MarshPoolOnly) >= PondTreatmentWeight)
                return MarshPoolIsFresh ? ShoreTreatment.Fresh : ShoreTreatment.Quiet;

            if (NineMileCreekShoreMap.ZoneWeight(p, BarachoisOnly) >= PondTreatmentWeight)
                return ShoreTreatment.Quiet;

            if (OnTheBreakwatersOuterFace(p)) return ShoreTreatment.Exposed;

            // Inside the harbour: sheltered by the crib arm and the two walls. The shoal IS the basin's
            // water, so this is the bottom you look down at from the deck.
            if (InTheHarbourBasin(p)) return ShoreTreatment.Quiet;

            // The gut through the bar: a channel that holds water as the flats bare — sheltered by the
            // ridge either side of it, and the plan's own quiet feature out on the crossing.
            if (InTheGut(p)) return ShoreTreatment.Quiet;

            // A LEDGE is rock the tide hands back twice a day — sheltered enough for the ordinary mix.
            // The two TALL classes are the weather face, and they are the ones that get the exposed one.
            if (terrain != null)
            {
                CoastClass cls = terrain.CoastClassAt(p);
                if (cls == CoastClass.Cliff || cls == CoastClass.DeepShoreCliff)
                    return ShoreTreatment.Exposed;
            }

            return ShoreTreatment.Ordinary;
        }

        static readonly MainlandZone[] MarshPoolOnly = { NineMileCreekMainland.MarshPoolCarve };
        static readonly MainlandZone[] BarachoisOnly = { NineMileCreekMainland.BarachoisCarve };
        static readonly MainlandZone[] HarbourShoalOnly = { NineMileCreekMainland.HarbourShoalFill };

        /// <summary>Seaward (SOUTH) of the crib breakwater and within
        /// <see cref="BreakwaterExposedReachMetres"/> of it — the face the bay actually hits.</summary>
        public static bool OnTheBreakwatersOuterFace(Vector2 p)
        {
            MainlandZone bw = NineMileCreekMainland.BreakwaterFill;
            if (p.y > bw.Center.y) return false;                       // the harbour side
            float d = MainlandTidalTerrain.DistanceOutsideRect(p, bw.Center, bw.HalfSize);
            return d <= BreakwaterExposedReachMetres;
        }

        /// <summary>Inside the harbour shoal's own rectangle — the basin's bottom, sheltered by the crib
        /// arm to the south and the two walls to the north and west.</summary>
        public static bool InTheHarbourBasin(Vector2 p) =>
            NineMileCreekShoreMap.ZoneWeight(p, HarbourShoalOnly) >= PondTreatmentWeight;

        /// <summary>Inside the gut cut through this region's half of the bar — the channel that keeps
        /// water as the flats around it bare.</summary>
        public static bool InTheGut(Vector2 p)
        {
            // ⚠ Through a LOCAL, not the const directly: 0 = ABSENT is a case the plan's own tooltip
            // authors, but the shipped value is 15, so comparing the const inline folds the guard away
            // and Roslyn reports the `return` unreachable (CS0162).
            float half = NineMileCreekMainland.GutHalfWidth;
            if (half <= 0f) return false;
            Vector2 crossing = Vector2.Lerp(NineMileCreekMainland.BarFrom, NineMileCreekMainland.BarTo,
                                            NineMileCreekMainland.GutAlong);
            float along = MainlandTidalTerrain.DistanceAlongAxisFrom(
                p, NineMileCreekMainland.BarFrom, NineMileCreekMainland.BarTo, crossing);
            if (along > half) return false;
            // ...and still ON the bar, not out in the open bay beside it.
            return MainlandTidalTerrain.DistanceToSegment(
                       p, NineMileCreekMainland.BarFrom, NineMileCreekMainland.BarTo)
                   <= NineMileCreekMainland.BarHalfWidth;
        }

        // =================================================================================================
        //  3. WHO GROWS WHERE
        // =================================================================================================

        /// <summary>
        /// The species a zone plants under a given treatment, by the kit's own keys.
        ///
        /// <para><b>Deliberately a SUBSET of the sixteen, per treatment</b> — the same argument St Peters
        /// makes about a botanical garden, sharpened by the fact that this coast has four kinds of shore
        /// and they should not all look alike. The splits are the ones a Maritimer would notice:</para>
        /// <list type="bullet">
        /// <item><description><b>Exposed</b> drops EELGRASS and keeps the wracks. Eelgrass is a soft-bottom
        /// species; it does not hold on a wave-beaten rock face, and putting a meadow of it at the foot of
        /// a crib breakwater is the kind of mistake the eye does not catch and a fisherman does.</description></item>
        /// <item><description><b>Quiet</b> drops the wracks' exposed member (bladderwrack) and leads with
        /// eelgrass and the marsh — a barachois is a mud lagoon, not a surf platform.</description></item>
        /// <item><description><b>Fresh</b> is CATTAIL's, and only here. St Peters holds it back from its
        /// automatic pass for a stated reason — "it wants standing FRESH water and this island has none
        /// authored" — and the marsh pool is the first standing fresh water either region has. The species
        /// has been baked and Def'd since the kit landed and has never been placed.</description></item>
        /// </list>
        /// </summary>
        public static string[] SpeciesFor(string zone, ShoreTreatment treatment)
        {
            switch (treatment)
            {
                case ShoreTreatment.Exposed:
                    // The wave-beaten face: wrack and moss on rock, and no soft-bottom bed anywhere.
                    if (zone == ZoneFringe) return new[] { "IrishMoss", "SugarKelp" };
                    if (zone == ZoneMid) return new[] { "Bladderwrack", "KnottedWrack", "IrishMoss" };
                    if (zone == ZoneLowMarsh) return new[] { "Glasswort" };
                    if (zone == ZoneHighMarsh) return new[] { "BlackRush" };
                    return new[] { "MarramGrass", "Bayberry" };

                case ShoreTreatment.Quiet:
                    // The lagoon and the basin: eelgrass on the mud, marsh up the margin.
                    if (zone == ZoneFringe) return new[] { "Eelgrass", "SeaLettuce" };
                    if (zone == ZoneMid) return new[] { "Eelgrass", "SeaLettuce", "KnottedWrack" };
                    if (zone == ZoneLowMarsh) return new[] { "Cordgrass", "Glasswort" };
                    if (zone == ZoneHighMarsh) return new[] { "SaltmeadowHay", "Threesquare", "BlackRush" };
                    return new[] { "SweetFern", "Bayberry" };

                case ShoreTreatment.Fresh:
                    // Standing fresh water: a cattail stand in it, and the freshwater margin above.
                    if (zone == ZoneFringe || zone == ZoneMid) return new[] { "Cattail" };
                    if (zone == ZoneLowMarsh) return new[] { "Cattail", "Threesquare" };
                    if (zone == ZoneHighMarsh) return new[] { "Threesquare", "BlackRush" };
                    return new[] { "SweetFern", "Bayberry" };

                default:
                    // The open coast — St Peters' own mix, because it is the same coast.
                    return StPetersShorePlants.ZoneSpecies[zone];
            }
        }

        // =================================================================================================
        //  4. THE SCATTER
        // =================================================================================================

        /// <summary>
        /// ⭐ <b>THE SCATTER'S SEED AXIS.</b> Every hash below is salted with this, so a test can move the
        /// whole layout by one integer and prove the arrangement is a hash rather than an accident — which
        /// is what the handoff's "a different seed moves it" is actually asking for. See the type note for
        /// why it is NOT <c>worldSeed</c>. Fixed in the shipped build so two Build clicks agree.
        /// </summary>
        public const int ScatterSalt = 0;

        /// <inheritdoc cref="StPetersShorePlants.PlantStep"/>
        public const float PlantStep = StPetersShorePlants.PlantStep;
        /// <inheritdoc cref="StPetersShorePlants.PlantJitter"/>
        public const float PlantJitter = StPetersShorePlants.PlantJitter;
        /// <inheritdoc cref="StPetersShorePlants.PatchScale"/>
        public const float PatchScale = StPetersShorePlants.PatchScale;
        /// <inheritdoc cref="StPetersShorePlants.PatchThreshold"/>
        public const float PatchThreshold = StPetersShorePlants.PatchThreshold;
        /// <inheritdoc cref="StPetersShorePlants.ScaleMin"/>
        public const float ScaleMin = StPetersShorePlants.ScaleMin;
        /// <inheritdoc cref="StPetersShorePlants.ScaleMax"/>
        public const float ScaleMax = StPetersShorePlants.ScaleMax;

        /// <summary>
        /// How much of a zone's base chance survives each treatment. A sheltered lagoon carpets; a
        /// wave-beaten face is scoured and carries less; a working basin is scoured by propellers rather
        /// than by weather and carries least of all.
        /// </summary>
        public static float TreatmentDensity(ShoreTreatment treatment, bool inBasin) =>
            treatment == ShoreTreatment.Fresh ? 0.85f :
            treatment == ShoreTreatment.Quiet ? (inBasin ? 0.35f : 1.0f) :
            treatment == ShoreTreatment.Exposed ? 0.62f : 1f;

        /// <inheritdoc cref="StPetersShorePlants.ChanceFor"/>
        public static float ChanceFor(string zone) => StPetersShorePlants.ChanceFor(zone);

        /// <inheritdoc cref="StPetersShorePlants.DepthFade"/>
        public static float DepthFade(float elevation)
        {
            float depthAtMeanTide = NineMileCreekMainland.TideMean - elevation;
            if (depthAtMeanTide <= ShallowSeeThroughDepthM) return 1f;
            float far = NineMileCreekMainland.TideMean - SubtidalFloorElevation;
            return Mathf.Clamp01(1f - (depthAtMeanTide - ShallowSeeThroughDepthM)
                                      / Mathf.Max(0.01f, far - ShallowSeeThroughDepthM));
        }

        /// <summary>One placed plant: where, which species, which zone and treatment it was placed for,
        /// how big.</summary>
        public struct ShorePlantSite
        {
            public Vector2 Position;
            /// <summary>A kit species key — <c>KnottedWrack</c>, <c>Eelgrass</c>… The planter resolves it
            /// to the matching <c>ShorePlantDef</c>; nothing here knows a sprite.</summary>
            public string SpeciesKey;
            public string Zone;
            public ShoreTreatment Treatment;
            public float Scale;
        }

        /// <summary>
        /// Every shore plant on this coast, deterministically. Swept over the region rectangle, because
        /// the two ponds sit well inland of the coast run and the subtidal band reaches well out past it.
        /// </summary>
        public static List<ShorePlantSite> Scatter(MainlandTidalTerrain terrain, int salt = ScatterSalt)
        {
            var sites = new List<ShorePlantSite>();
            if (terrain == null) return sites;

            Vector2 c = NineMileCreekMainland.RegionWorldCenter;
            Vector2 s = NineMileCreekMainland.RegionWorldSize;
            float minX = c.x - s.x * 0.5f, maxX = c.x + s.x * 0.5f;
            float minY = c.y - s.y * 0.5f, maxY = c.y + s.y * 0.5f;

            int nx = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / PlantStep));
            int ny = Mathf.Max(1, Mathf.CeilToInt((maxY - minY) / PlantStep));

            for (int ix = 0; ix < nx; ix++)
            for (int iy = 0; iy < ny; iy++)
            {
                float cx = minX + (ix + 0.5f) * PlantStep
                           + (StPetersShoreMap.Hash01(ix, iy, 211 + salt) * 2f - 1f) * PlantJitter;
                float cy = minY + (iy + 0.5f) * PlantStep
                           + (StPetersShoreMap.Hash01(ix, iy, 223 + salt) * 2f - 1f) * PlantJitter;
                var p = new Vector2(cx, cy);

                float e = terrain.ElevationAt(p);
                string zone = ZoneAt(e);
                if (zone == null) continue;

                if (!IsPlantableShore(p)) continue;

                float patch = StPetersShoreMap.Wiggle(
                    p * (NineMileCreekShoreMap.BandWiggleScale / PatchScale), salt: 227 + salt);
                if (patch <= PatchThreshold) continue;

                ShoreTreatment treatment = TreatmentAt(terrain, p);
                float chance = ChanceFor(zone) * TreatmentDensity(treatment, InTheHarbourBasin(p));
                if (zone == ZoneFringe) chance *= DepthFade(e);
                if (StPetersShoreMap.Hash01(ix, iy, 229 + salt) > chance) continue;

                string[] species = SpeciesFor(zone, treatment);
                if (species == null || species.Length == 0) continue;
                int pick = Mathf.Min(species.Length - 1,
                                     (int)(StPetersShoreMap.Hash01(ix, iy, 233 + salt) * species.Length));

                sites.Add(new ShorePlantSite
                {
                    Position = p,
                    SpeciesKey = species[pick],
                    Zone = zone,
                    Treatment = treatment,
                    Scale = Mathf.Lerp(ScaleMin, ScaleMax,
                                       StPetersShoreMap.Hash01(ix, iy, 239 + salt)),
                });
            }
            return sites;
        }

        // =================================================================================================
        //  5. WHAT MUST STAY CLEAR — the working water, and the ground that was BUILT
        // =================================================================================================

        /// <summary>
        /// ⭐ <b>THE MADE-GROUND GATE IS NOT THE FIELDS' GATE, and the difference is the harbour's
        /// water.</b>
        ///
        /// <para><c>NineMileCreekFields</c> refuses <see cref="NineMileCreekShoreMap.IsMadeGround"/>
        /// wholesale, which is right for a hedgerow: all five fills are the wharf plan and a hedge belongs
        /// on none of them. It is WRONG for weed. The harbour shoal is a fill — it is the ground that
        /// raises the bay to the ruled −1.6 m gate — but it is not a thing you walk on: it is the BASIN'S
        /// BOTTOM, four metres of water over it at high tide, and a sheltered harbour bottom that grows
        /// nothing at all is a bug rather than a restraint.</para>
        ///
        /// <para>So the shore pass refuses the fills you STAND on — those authored above the highest water
        /// (the yard at 3.6 m, the two decks at 3.0 m, the crib crest at 3.4 m) — and accepts the one
        /// authored below it. Derived from each fill's own elevation against this region's own spring high
        /// water, so adding a sixth fill classifies itself.</para>
        /// </summary>
        public static bool OnBuiltGround(Vector2 p)
        {
            foreach (MainlandZone z in NineMileCreekMainland.Fills)
            {
                if (z.Elevation <= NineMileCreekMainland.SpringHighWater) continue;   // it is water
                float d = MainlandTidalTerrain.DistanceOutsideRect(p, z.Center, z.HalfSize);
                float w = d <= 0f ? 1f : MainlandTidalTerrain.SmoothFalloff(d, Mathf.Max(z.Falloff, 1e-3f));
                if (w >= PondTreatmentWeight) return true;
            }
            return false;
        }

        /// <summary>Clearance (m) kept around the water the fleet actually uses — the berth line, the
        /// float run, the slipway and the boat ramp. Weed across a berth is weed a propeller has already
        /// cut; drawing it there would say "nobody works here".</summary>
        public const float WorkingWaterClearance = 4f;

        /// <summary>
        /// The shore's own clearings. Deliberately NOT <c>NineMileCreekFields.IsPlantable</c>: that one's
        /// radii are cut for hedgerows on the plateau and it refuses every fill, which would strip the
        /// whole basin. What actually has to stay clear on a working shore is what is USED — the built
        /// ground, the berths and the float, the slipway and the ramp, the roads where they reach the
        /// water, and the crossing you walk.
        /// </summary>
        public static bool IsPlantableShore(Vector2 p)
        {
            if (OnBuiltGround(p)) return false;

            // ⚠ MADE GROUND WINS (#527's rule): the roads reach the shore at the wharf yard and the spit,
            // and shore treatment ABUTS them rather than overpainting them. Asked of the road pass's own
            // published half-widths, so widening a road steps the weed back instead of growing it down
            // the middle of the carriageway.
            if (NineMileCreekFields.OnAnyCarriageway(p, RoadVergeMetres)) return false;

            // The berths, and the float run the small craft lie against.
            for (int i = 0; i < NineMileCreekMainland.BerthCount; i++)
                if (Vector2.Distance(p, NineMileCreekMainland.BerthPos(i)) < WorkingWaterClearance)
                    return false;
            if (MainlandTidalTerrain.DistanceToSegment(
                    p,
                    new Vector2(NineMileCreekMainland.FloatRunWestX, NineMileCreekMainland.FloatRunY),
                    new Vector2(NineMileCreekMainland.FloatRunEastX, NineMileCreekMainland.FloatRunY))
                < WorkingWaterClearance) return false;

            // The slipway and the boat ramp: a hull is dragged up both of them.
            if (MainlandTidalTerrain.DistanceToSegment(
                    p, NineMileCreekMainland.SlipwayHeadPos, NineMileCreekMainland.SlipwayToePos)
                < NineMileCreekMainland.SlipwayHalfWidthMetres + WorkingWaterClearance) return false;
            if (MainlandTidalTerrain.DistanceToSegment(
                    p,
                    new Vector2(NineMileCreekMainland.BoatRampHeadPos.x, NineMileCreekMainland.BoatRampHeadPos.y),
                    new Vector2(NineMileCreekMainland.BoatRampToePos.x, NineMileCreekMainland.BoatRampToePos.y))
                < WorkingWaterClearance) return false;

            // Where a boat stops with fish aboard.
            if (Vector2.Distance(p, new Vector2(NineMileCreekMainland.DockZonePos.x,
                                                NineMileCreekMainland.DockZonePos.y))
                < NineMileCreekMainland.DockZoneRadius + WorkingWaterClearance) return false;

            // ⭐ THE CROSSING IS A WALKING LINE, and weed laid over it would read as "do not cross" —
            // St Peters' own ruling about its half of the same bar, applied to this half. The GUT is
            // deliberately exempt: it is the boat channel cut THROUGH the bar, nobody walks it, and it is
            // the plan's own quiet water.
            if (!InTheGut(p) &&
                MainlandTidalTerrain.DistanceToSegment(
                    p, NineMileCreekMainland.BarFrom, NineMileCreekMainland.BarTo)
                < NineMileCreekMainland.BarHalfWidth) return false;

            return true;
        }

        /// <summary>How far off a carriageway the weed starts — the fields' own verge, shared so the two
        /// passes cannot disagree about how wide a road's clear ground is.</summary>
        public const float RoadVergeMetres = NineMileCreekFields.RoadVergeMetres;

        // =================================================================================================
        //  6. THE COAST'S ROCK — the tell, where the plan says there is rock to shed it
        // =================================================================================================

        /// <summary>One placed rock: where it stands and which <c>ShoreIsoSprites</c> slice it is.</summary>
        public struct RockSite
        {
            public Vector2 Position;
            /// <summary>A name from <c>ShorelineIsoCatalog.RockSprites</c>.</summary>
            public string Sprite;
        }

        /// <summary>Candidate sites walked along the coast run. One every ~2 m of the 596 m run, most of
        /// which are refused — the survivors are the scattered boulders a red-sandstone coast actually
        /// sheds at the foot of its banks.</summary>
        public const int RockAttempts = 300;

        /// <summary>How far seaward of the shoreline a rock may lie: out to the foot of the plunge and a
        /// little past, which is the low-water platform under a ledge or a bluff.</summary>
        public const float RockSeawardReachMetres = 18f;

        /// <summary>Chance a candidate on a rock sector lands, against the chance on a soft one. A cliff
        /// coast sheds boulders; a beach has the odd erratic and no more.</summary>
        public const float RockChanceOnRock = 0.55f;
        /// <inheritdoc cref="RockChanceOnRock"/>
        public const float RockChanceOnSoft = 0.06f;

        /// <summary>
        /// The coast's rock, scattered deterministically along the authored run. Pure — each site is a
        /// stable hash of its index, and every one is checked against the terrain and the working water,
        /// so a rebuild reproduces the same rocks and none lands in a berth or on the walking bar.
        ///
        /// <para>Walked ALONG THE RUN rather than over the region rectangle, because that is what a
        /// mainland coast is: the rock belongs to the shore, not to the bay.</para>
        /// </summary>
        public static List<RockSite> ScatterRocks(MainlandTidalTerrain terrain, int salt = ScatterSalt)
        {
            var sites = new List<RockSite>();
            if (terrain == null) return sites;

            float runLength = NineMileCreekMainland.CoastRunLength;

            for (int i = 0; i < RockAttempts; i++)
            {
                float along = (i + 0.5f) / RockAttempts * runLength;
                Vector2 on = MainlandCoast.PositionAt(NineMileCreekMainland.CoastPoints, along);
                float azimuth = MainlandCoast.OutwardNormalAzimuthAt(
                    NineMileCreekMainland.CoastPoints, along);
                float a = azimuth * Mathf.Deg2Rad;
                var normal = new Vector2(Mathf.Sin(a), Mathf.Cos(a));

                // Somewhere out on the foreshore in front of that metre of shore, with a little jitter
                // ALONG the run too so the line of them does not read as a fence.
                float out_ = StPetersShoreMap.Hash01(i, 1, 307 + salt) * RockSeawardReachMetres;
                float sideways = (StPetersShoreMap.Hash01(i, 2, 311 + salt) * 2f - 1f) * PlantStep;
                var tangent = new Vector2(normal.y, -normal.x);
                Vector2 p = on + normal * out_ + tangent * sideways;

                float e = terrain.ElevationAt(p);
                // On the foreshore: between the shelf's outer lip and the top of the intertidal. A rock
                // above the highest water is a field erratic and belongs to the fields pass.
                if (e < SubtidalFloorElevation || e > HighWaterElevation) continue;
                if (!IsPlantableShore(p)) continue;

                bool rock = NineMileCreekShoreMap.IsRockCoastAt(terrain, p);
                float chance = rock ? RockChanceOnRock : RockChanceOnSoft;
                if (StPetersShoreMap.Hash01(i, 3, 313 + salt) > chance) continue;

                // Small boulders common, big ones rare — the squared roll leans on the small slab, the
                // same shape St Peters' own scatter uses.
                float roll = StPetersShoreMap.Hash01(i, 4, 317 + salt);
                string sprite = rock
                    ? (roll * roll < 0.55f ? "bs" : roll < 0.9f ? "bm" : "bl")
                    : (roll < 0.7f ? "bs" : "bm");

                sites.Add(new RockSite { Position = p, Sprite = sprite });
            }
            return sites;
        }
    }
}
#endif
