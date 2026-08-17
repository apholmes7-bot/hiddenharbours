#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>St Peters' woods and wildflowers</b> — where a tree stands, which species it is, and which bloom
    /// grows there. Pure and deterministic, so a test asserts the forest the scene is planted from without
    /// loading anything; <see cref="StPetersWoodsPlanter"/> does the Unity work.
    ///
    /// <para><b>The interior is REVERTING, not forested.</b> §5.1 maps the brief's <i>"marsh, meadows, wild
    /// roses/raspberries"</i> onto <i>"the reverting interior"</i> and its <i>"forest"</i> onto <i>"interior
    /// cover, hiding ruins"</i> — a mosaic of stands and open ground, which is what an island whose twenty
    /// families left actually looks like. So the woods come in STANDS driven by a coherent noise field with
    /// meadow between them, never a uniform fill.</para>
    ///
    /// <para><b>Species are chosen by habitat, from two fields.</b> EXPOSURE (how much salt and wind this
    /// ground takes — a function of how near the coast it is and which way that coast faces) and WETNESS
    /// (coherent noise: hollows hold water). Black spruce and balsam fir take the blasted edges, red spruce
    /// and white cedar the middle ground, and the hardwoods — maple, oak, birch, aspen — only the sheltered
    /// core, with white pine as the emergent that overtops everything. That is the real Acadian gradient,
    /// and it means the island reads as one place rather than a species salad.</para>
    ///
    /// <para><b>⚠ Nine species, and Tamarack is not one of them.</b> The pass-2 tree kit ships nine
    /// (#334); tamarack is held back by ruling and is absent from <c>Trees.json</c> and the placeable set.
    /// Nothing here names a species directly — every one comes from
    /// <c>AcadianTreeCatalog.Scan()</c>, so the held-back species cannot be reached even by accident, and a
    /// later bake that adds it appears here without a code change.</para>
    /// </summary>
    public static class StPetersWoods
    {
        // =====================================================================================
        //  WHERE TREES CAN STAND AT ALL
        // =====================================================================================

        /// <summary>
        /// Trees need ground the sea never touches, and then some. 4.6 m is a shade above the meadow's own
        /// floor (<c>StPetersShoreMap.GrassFloorElevation</c> 4.2) and well above spring high water (2.2
        /// since the 2026-08-01 pacing ruling; 3.5 before it), which leaves a **treeless margin** all round
        /// the island. That margin is not a safety buffer — it is what a salt-blasted coast looks like.
        /// Trees do not grow to the tideline. The smaller swing only widens the margin, so the treeline is
        /// unchanged: it is anchored to the meadow floor, not to the tide.
        /// </summary>
        public const float TreeLineElevation = 4.6f;

        /// <summary>
        /// Radius (m) of the clearing the village stands in, measured from
        /// <see cref="StPetersBuilder.VillageHearthPos"/>. A village in a forest is a village in a clearing;
        /// without this the cottage would be planted through.
        ///
        /// <para>⭐ <b>THIS FOLLOWS THE VILLAGE, AND THE VILLAGE JUST GREW.</b> 34 m was right when the
        /// village WAS the cottage and three props (#345). §5.1 says the village is three clapboard
        /// houses, a one-room school and a general store, and those five buildings are now actually
        /// placed (<see cref="StPetersVillage"/>) — footprints 6.4–7.7 m across, sited in an arc around
        /// the hearth and the green. At 34 m the outermost of them stood 7 m OUTSIDE the clearing, which
        /// means trees and shrubs planted right up to and through its walls. 44 m contains every one of
        /// them with room to walk between.</para>
        ///
        /// <para>⚠️ Do not re-tune this by eye. <c>StPetersVillageTests</c> derives the requirement from
        /// the building contract's own footprints — <see cref="StPetersVillage.RequiredClearingRadius"/>
        /// — and fails with the number to use if a site moves or a re-bake changes a footprint. It stays
        /// a <c>const</c> rather than a computed property on purpose: <see cref="IsPlantable"/> is called
        /// tens of thousands of times per build and must not read a JSON contract to answer.</para>
        /// </summary>
        public const float VillageClearingRadius = 44f;

        /// <summary>
        /// Radius (m) within which the meadow reads as DISTURBED ground — dooryards, old gardens gone
        /// over, the edges of a path — and grows the weedy flowers rather than the open-meadow mix.
        ///
        /// <para>⚠️ This used to be written <c>VillageClearingRadius * 2.2f</c>, which quietly made the
        /// flower bands a function of how big the village's no-plant clearing happened to be. They are
        /// different questions: the clearing is "how much ground do the buildings occupy", the dooryard
        /// is "how far out does human disturbance read". Widening the clearing for the five houses would
        /// have pushed this band out 22 m and re-planted a ring of the island for no reason, so the value
        /// is now its own — pinned at what <c>34 × 2.2</c> produced, the band as it was tuned.</para>
        /// </summary>
        public const float DooryardRadius = 74.8f;

        /// <summary>Radius (m) kept clear around the START spawn on top of the village clearing, so the
        /// first thing the player sees is not a trunk.</summary>
        public const float SpawnClearingRadius = 12f;

        /// <summary>Clearance (m) from the sandbar's centre-line. The crossing is the region's one lesson
        /// and its approach should be open ground — you must be able to see the bar from the island.</summary>
        public const float CrossingClearance = 40f;

        /// <summary>Clearance (m) around the dock and its pier, so the one berth is not planted shut.</summary>
        public const float DockClearance = 30f;

        /// <summary>
        /// Clearance (m) either side of a PAINTED DIRT PATH's centre-line — the walked tread the terrain
        /// painter lays from the green to the slip and from the green to the bar head
        /// (<see cref="StPetersStarterSplat.VillageToSlipPath"/> /
        /// <see cref="StPetersStarterSplat.VillageToBarHeadPath"/>).
        ///
        /// <para><b>⭐ Why this exists at all, and why it is a fix rather than a feature.</b> The GRASS
        /// layer already keeps the meadow off these paths, and says why in its own words: <i>"a path the
        /// ground paints as dirt and the meadow grows over is not a path"</i>
        /// (<see cref="StPetersGrass.PathBareHalfWidthMetres"/>). The woods never got the same rule, so a
        /// spruce could — and did — stand in the middle of the dirt. Nobody noticed while nothing walked
        /// these paths; the moment a villager commutes the island's length on one
        /// (<see cref="StPetersRoutines"/>), a tree in the tread is a person walking through a trunk.</para>
        ///
        /// <para><b>4 m, not 30.</b> This is a footpath, not a berth: the tread itself is 1.5 m
        /// (<see cref="StPetersStarterSplat.PathWidthMetres"/>), and 4 m leaves trunks a stride either side
        /// with the crowns still closing overhead — a lane THROUGH woods, which is what a walked path in
        /// woods looks like. <see cref="DockClearance"/>'s 30 m and
        /// <see cref="CrossingClearance"/>'s 40 m are open-ground rulings about sightlines; this is not one,
        /// and widening it would read as a firebreak.</para>
        /// </summary>
        public const float PathClearance = 4f;

        // =====================================================================================
        //  THE STAND FIELD
        // =====================================================================================

        /// <summary>Feature size (m) of the stand/meadow mosaic — how big a wood or a field is.</summary>
        public const float StandScale = 46f;

        /// <summary>
        /// How much of the eligible interior carries woods. The noise field is symmetric about 0, so a
        /// threshold of 0 would wood half the island; −0.12 tips it past half at the core and, once the
        /// exposure taper below is applied, lands a little over a third overall. A "reverting" island is
        /// mostly open with woods coming back in patches, not the other way round.
        ///
        /// <para>Widened from −0.05 on the owner's 2026-08-01 "add more trees" ask — the stands grow and
        /// gain a few new ones, but the island stays mostly open; the reverting-interior thesis (§5.1)
        /// is a design ruling, not a density accident, so "more trees" densifies the mosaic rather than
        /// closing it. <c>StPetersWoodsTests</c> still pins the wooded fraction under 0.7.</para>
        ///
        /// <para><b>⭐ WIDENED AGAIN to −0.15 on the owner's 2026-08-16 "denser, more forest-like woods"
        /// ruling — and the size of that step is MEASURED, after a bigger one was tried and rejected.</b>
        /// The first draft used −0.20, which took the ambient mosaic from 73 trees to <b>218</b>. Two
        /// things were wrong with that, and neither was the tree count:</para>
        ///
        /// <para>(1) <b>It answered the ruling with the wrong instrument.</b> §5.1's reverting interior is a
        /// design ruling, not a density accident — the island is meant to be mostly open with woods coming
        /// back in patches. Tripling the mosaic buys "more trees" by quietly retiring that thesis.</para>
        ///
        /// <para>(2) <b>It swamped the lots.</b> A uniform 218-tree mosaic plants a woodland lot's ground
        /// too, because <see cref="InStand"/> does not know lots exist — so the authored lots could only
        /// infill what the min-gap rule left, came out at 89 trees between them, and their cores measured no
        /// denser than their fringes. <c>ALotsCoreIsMeasurablyDenserThanItsFringe</c> is what caught it.</para>
        ///
        /// <para>So the mosaic gets a real but modest thickening and the <b>"forest-like" density lives in
        /// <see cref="StPetersWoodlandZones"/>' authored lots</b>, where a closed canopy is bounded, has an
        /// edge that reads, and has a lane cut through it. Before/after numbers are on the PR; the wooded
        /// fraction and the sabotage margin are both still pinned by <c>StPetersWoodsTests</c>.</para>
        /// </summary>
        public const float StandThreshold = -0.15f;

        /// <summary>Grid spacing (m) of the candidate positions inside a stand, before jitter. A touch
        /// under one mature crown width at this kit's scale, so a stand reads closed with the occasional
        /// crown overlap — and it is the one knob that trades how dense the woods look against how many
        /// GameObjects the region carries (rule 7). A whole island of trees is a real cost.
        /// Tightened from 6.5 on the owner's 2026-08-01 "add more trees" ask (~1.4× the trunks per
        /// stand), and from 5.4 to 4.8 on his 2026-08-16 "denser, more forest-like" one (~1.27× again).
        /// <para>⚠ This is the AMBIENT grain and it is deliberately NOT the dense one. A wood that reads
        /// as forest wants trunks closer than this, and closing the whole island to that grain is a draw
        /// call cost paid over ground the player mostly walks past — so the closed canopy lives in
        /// <see cref="StPetersWoodlandZones.LotCoreSpacingMetres"/>, on bounded ground, and this stays the
        /// number the reverting mosaic between the lots is planted at.</para></summary>
        public const float TreeStep = 4.8f;

        /// <summary>Max deterministic offset (m) applied to each candidate, so a stand never reads as
        /// a plantation. Just under half the step, which is as far as it can go without letting two
        /// neighbours swap places.</summary>
        public const float TreeJitter = 2.4f;

        /// <summary>
        /// True where a stand of woods grows. Pure: a coherent noise field whose threshold is raised near
        /// the coast by <see cref="InlandFraction"/>, so the woods thin into a windswept fringe instead of
        /// stopping at a drawn line.
        /// </summary>
        public static bool InStand(Vector2 worldPos, float elevation)
        {
            if (elevation < TreeLineElevation) return false;

            float shelter = InlandFraction(worldPos);

            // The mosaic. Scaled onto the stand's own feature size by re-mapping into the wiggle's lattice.
            float field = StPetersShoreMap.Wiggle(
                worldPos * (StPetersShoreMap.BandWiggleScale / StandScale), salt: 41);

            // Near the coast the field has to clear a HIGHER bar, which thins the fringe rather than
            // cutting it off.
            return field > StandThreshold + (1f - shelter) * 0.9f;
        }

        /// <summary>
        /// True where this ground is UNDER TREES — the ambient mosaic <see cref="InStand"/> grows, plus the
        /// authored woodland lots <see cref="StPetersWoodlandZones"/> declares.
        ///
        /// <para><b>⚠ Deliberately a second function rather than a widening of <see cref="InStand"/>.</b>
        /// The two answer different questions and both are asked. <c>InStand</c> is <i>"is the reverting
        /// mosaic growing here"</i>: it is the thing the stand-field tests measure, the thing the sabotage
        /// arm compares a uniform fill against, and the thing the wooded-fraction ceiling is a ceiling ON —
        /// folding authored lots into it would have moved all three bars for a reason that has nothing to
        /// do with the mosaic. <c>InWoods</c> is <i>"is there a canopy over this metre"</i>, which is what
        /// a lady's slipper on a shaded forest floor actually needs to know.</para>
        /// </summary>
        public static bool InWoods(Vector2 worldPos, float elevation) =>
            InStand(worldPos, elevation) || StPetersWoodlandZones.InALot(worldPos);

        // =====================================================================================
        //  HABITAT
        // =====================================================================================

        /// <summary>
        /// How far inland (in the metres of ELLIPTICAL DISTANCE the terrain measures in) the salt wind
        /// reaches before the ground counts as sheltered. 45 m on the re-ruled 120 × 70 m island keeps
        /// the same core:collar proportion the old 80 m gave the 225 × 130 island (~0.37 of the radius).
        ///
        /// <para>⚠ SCALES WITH THE ISLAND, and the 2026-07-30 shrink is why that is written down: left
        /// at 80 m on a 120 m radius, the collar swallowed the whole interior — measured 21 trees on
        /// the entire island at a 4.6% wooded fraction, i.e. a bald rock. The salt wind's reach is a
        /// FRACTION of the island in this model, not a physical constant, because the stand taper and
        /// the species gradient both key off it and both need a genuine sheltered core to exist.</para>
        /// </summary>
        public const float ExposureDepthMetres = 45f;

        /// <summary>
        /// 0 at the coast, 1 once the ground is <see cref="ExposureDepthMetres"/> inside the plateau edge —
        /// the one field both the species gradient and the stand taper are built on.
        ///
        /// <para><b>⚠ Measured from the plateau EDGE, not from elevation.</b> The first version of this used
        /// elevation as a proxy for distance-from-sea, reasoning that on an island the two are the same
        /// thing. They are not, on THIS island: it is a flat-topped plateau, so the elevation is exactly
        /// <c>IslandElevation</c> everywhere inside the edge and only varies in the 30 m beach band — which
        /// sits below the tree line anyway. The whole interior therefore came back as "sheltered core", the
        /// gradient did nothing, and a build planted 1,114 trees of just four species (585 red maple, 505
        /// white cedar, 12 apiece of two spruces — no fir, oak, birch, aspen or pine at all). Two tests
        /// caught it: one found exposure identical on both coasts, the other found a habitat with no species
        /// it could fall back to.</para>
        /// </summary>
        public static float InlandFraction(Vector2 worldPos)
        {
            float d = HiddenHarbours.World.TidalTerrain.IslandDistance(
                worldPos, StPetersBuilder.IslandCenter,
                StPetersBuilder.IslandRadius, StPetersBuilder.IslandRadiusY);
            return Mathf.Clamp01((StPetersBuilder.IslandRadius - d) / ExposureDepthMetres);
        }

        /// <summary>
        /// How much salt and wind this ground takes, 0 (sheltered core) to 1 (blasted edge), pushed up on the
        /// weather side — which by §5.1 is the south/east. That push is what makes the two sides of the
        /// island read differently in the TREES as well as in the ground.
        /// </summary>
        public static float ExposureAt(Vector2 worldPos)
        {
            float exposure = 1f - InlandFraction(worldPos);
            if (StPetersShoreMap.IsWeatherCoast(worldPos)) exposure = Mathf.Clamp01(exposure * 1.35f);
            return exposure;
        }

        /// <summary>Coherent damp/dry field: hollows hold water, ridges shed it. Its own salt, so it is
        /// independent of the stand mosaic — a wet patch can fall inside a wood or outside it.</summary>
        public static float WetnessAt(Vector2 worldPos) =>
            (StPetersShoreMap.Wiggle(worldPos * (StPetersShoreMap.BandWiggleScale / 38f), salt: 53) + 1f) * 0.5f;

        /// <summary>Above this wetness the ground is a damp hollow — cedar and black spruce country, and
        /// where the blue flag iris grows.</summary>
        public const float WetThreshold = 0.62f;

        /// <summary>Above this exposure only the toughest conifers hold on.</summary>
        public const float ExposedThreshold = 0.55f;

        /// <summary>Below this exposure the hardwoods can get in.</summary>
        public const float ShelteredThreshold = 0.3f;

        /// <summary>
        /// The species PREFERENCE ORDER for a patch of ground, most-wanted first, as kit species keys. The
        /// caller walks it and takes the first species the kit actually baked — so a habitat whose ideal
        /// species is missing degrades to its next-best neighbour instead of leaving a hole, and a species
        /// held back by ruling (tamarack) is simply never named.
        /// </summary>
        public static string[] SpeciesPreference(float exposure, float wetness)
        {
            bool wet = wetness >= WetThreshold;

            if (exposure >= ExposedThreshold)
                // The blasted fringe. Black spruce grows where nothing else will — bog, headland, salt
                // wind — and balsam fir is the other one that takes it.
                return wet
                    ? new[] { "BlackSpruce", "WhiteCedar", "BalsamFir", "RedSpruce" }
                    : new[] { "BlackSpruce", "BalsamFir", "RedSpruce", "WhiteCedar" };

            if (exposure >= ShelteredThreshold)
                // The middle ground: red spruce is the Acadian forest's signature tree, cedar takes the
                // damp.
                return wet
                    ? new[] { "WhiteCedar", "BlackSpruce", "RedSpruce", "BalsamFir" }
                    : new[] { "RedSpruce", "BalsamFir", "WhiteBirch", "WhitePine", "BlackSpruce" };

            // The sheltered core — the only place the hardwoods get in, and where white pine tops out
            // above everything else.
            //
            // ⚠ Every list here ends with the HARDY GENERALISTS (red spruce, balsam fir, black spruce), and
            // that tail is not padding: the caller walks the list and takes the first species the kit
            // actually baked, so a list that names only its ideal species leaves BARE GROUND when those are
            // missing. The sheltered-dry list originally stopped at red spruce and a test that offered only
            // balsam fir found nothing at all — which is exactly what a partially-baked kit would do to the
            // island in play.
            return wet
                ? new[] { "WhiteCedar", "RedMaple", "TremblingAspen", "WhiteBirch",
                          "RedSpruce", "BalsamFir", "BlackSpruce" }
                : new[] { "RedMaple", "RedOak", "WhiteBirch", "TremblingAspen", "WhitePine",
                          "RedSpruce", "BalsamFir", "BlackSpruce" };
        }

        // =====================================================================================
        //  THE SCATTER
        // =====================================================================================

        /// <summary>One planted tree: where, which species, and which drawn variant of it.</summary>
        public struct TreeSite
        {
            public Vector2 Position;
            public string Species;
            /// <summary>Which individual tree of that species — the sheet's variants are different trees,
            /// not poses, so mixing them is what stops a stand reading as one tree copy-pasted.</summary>
            public int Variant;
        }

        /// <summary>
        /// Every tree on the island, deterministically. Pure: the grid, the jitter, the stand field, the
        /// habitat and the variant are all stable hashes of position (no <c>System.Random</c>), so a rebuild
        /// reproduces the forest exactly (CLAUDE.md rule 5).
        /// </summary>
        /// <param name="available">The species the kit actually baked — the caller passes
        /// <c>AcadianTreeCatalog.Scan()</c>'s keys, which is what keeps the held-back tamarack out.</param>
        /// <param name="variantsFor">How many drawn variants a species has, from its contract entry.</param>
        public static List<TreeSite> ScatterTrees(ITidalTerrain terrain,
                                                  IReadOnlyCollection<string> available,
                                                  System.Func<string, int> variantsFor)
        {
            var sites = new List<TreeSite>();
            if (terrain == null || available == null || available.Count == 0) return sites;

            // Only the island's own footprint is worth walking — the bar and the sea carry no trees.
            float minX = StPetersBuilder.IslandCenter.x - StPetersBuilder.IslandRadius;
            float maxX = StPetersBuilder.IslandCenter.x + StPetersBuilder.IslandRadius;
            float minY = StPetersBuilder.IslandCenter.y - StPetersBuilder.IslandRadiusY;
            float maxY = StPetersBuilder.IslandCenter.y + StPetersBuilder.IslandRadiusY;

            int nx = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / TreeStep));
            int ny = Mathf.Max(1, Mathf.CeilToInt((maxY - minY) / TreeStep));

            for (int ix = 0; ix < nx; ix++)
            for (int iy = 0; iy < ny; iy++)
            {
                float cx = minX + (ix + 0.5f) * TreeStep
                           + (StPetersShoreMap.Hash01(ix, iy, 61) * 2f - 1f) * TreeJitter;
                float cy = minY + (iy + 0.5f) * TreeStep
                           + (StPetersShoreMap.Hash01(ix, iy, 67) * 2f - 1f) * TreeJitter;
                var p = new Vector2(cx, cy);

                if (!IsPlantable(terrain, p)) continue;

                float e = terrain.ElevationAt(p);
                if (!InStand(p, e)) continue;

                // A hashed roll into the habitat's list, so a stand is MIXED rather than one species
                // repeated (see PickSpecies).
                string species = PickSpecies(
                    SpeciesPreference(ExposureAt(p), WetnessAt(p)), available,
                    StPetersShoreMap.Hash01(ix, iy, 73));
                if (species == null) continue;

                int variants = Mathf.Max(1, variantsFor != null ? variantsFor(species) : 1);
                sites.Add(new TreeSite
                {
                    Position = p,
                    Species = species,
                    Variant = Mathf.Min(variants - 1,
                                        (int)(StPetersShoreMap.Hash01(ix, iy, 71) * variants)),
                });
            }
            return sites;
        }

        /// <summary>
        /// <b>The island's WOODLAND LOTS, planted.</b> The owner's 2026-08-16 "denser, more forest-like
        /// woods" ruling, on bounded ground: <see cref="StPetersWoodlandZones"/> declares three lots and
        /// <see cref="WoodlandZones.ScatterLots"/> — the same scatter Nine Mile Creek's lots use — closes a
        /// canopy inside each one.
        ///
        /// <para><b>⚠ It INFILLS <paramref name="ambient"/>, so pass the real ambient forest.</b> Every lot
        /// tree keeps <see cref="WoodlandZones.MinTrunkGapMetres"/> from a trunk already standing, which is
        /// what lets a lot be "the mosaic plus more" rather than a second forest overlaid on the first. Pass
        /// an empty list and the lots still plant — denser, and with the occasional trunk inside another
        /// tree's, which is the artefact this argument exists to prevent.</para>
        /// </summary>
        public static List<WoodlandZones.LotTreeSite> ScatterLotTrees(
            ITidalTerrain terrain,
            IReadOnlyCollection<string> available,
            System.Func<string, int> variantsFor,
            IReadOnlyList<TreeSite> ambient)
        {
            var standing = new List<Vector2>();
            if (ambient != null)
                for (int i = 0; i < ambient.Count; i++) standing.Add(ambient[i].Position);

            return WoodlandZones.ScatterLots(
                StPetersWoodlandZones.Zones, terrain,
                // ⚠ The island's own ground rules, unchanged — a lot may not plant on the pier, across the
                // crossing, through the village or down one of its own cut lanes just because a table says
                // "wood here". IsPlantable already asks the zone table about the lanes, so the corridors
                // come out of a lot for free rather than by a second rule.
                p => IsPlantable(terrain, p),
                ExposureAt, WetnessAt, available, variantsFor, standing);
        }

        /// <summary>
        /// Ground a tree may stand on: dry meadow, clear of the village, the spawn, the crossing's approach
        /// and the dock. Public so the flower scatter can share the same clearings — a lupin in the middle
        /// of the pier would be as wrong as a spruce.
        ///
        /// <para><paramref name="floorElevation"/> defaults to the TREE line, which is what trees and
        /// flowers want. ⚠️ A caller with a lower floor of its own must pass it: the shrub layer reaches
        /// a metre further down the beach than the woods do (that is what a heath IS — the ground the
        /// forest gives up), and for one milestone its own floor was dead code because this function
        /// re-rejected everything below the tree line a moment later.</para>
        /// </summary>
        public static bool IsPlantable(ITidalTerrain terrain, Vector2 p, float? floorElevation = null)
        {
            if (terrain.ElevationAt(p) < (floorElevation ?? TreeLineElevation)) return false;

            if (Vector2.Distance(p, StPetersBuilder.VillageHearthPos) < VillageClearingRadius) return false;
            if (Vector2.Distance(p, StPetersBuilder.StartSpawnPos) < SpawnClearingRadius) return false;

            // ⭐ THE WOODLAND ZONE TABLE — and this line is the #551 seam being spent exactly as it was
            // left. It used to read `StPetersGinnyPlot.IsInsideClearing(p)`, asked as ONE predicate on
            // purpose so the dense-forest pass could take it over by name; it has. Ginny's plot is now a
            // row in StPetersWoodlandZones (reading her own file's geometry, not a copy of it) alongside
            // the CUT LANES the owner ruled on 2026-08-16 — the walk out to her dooryard, and one through
            // each of the island's three woodland lots. One predicate, one call site, still.
            if (StPetersWoodlandZones.IsCleared(p)) return false;

            if (StPetersShoreMap.DistanceToSegment(
                    p, StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo) < CrossingClearance)
                return false;

            if (Vector2.Distance(p, StPetersBuilder.DockZonePos) < DockClearance) return false;
            if (StPetersShoreMap.DistanceToSegment(
                    p, StPetersBuilder.BerthFrom, StPetersBuilder.BerthTo) < DockClearance)
                return false;

            // The two painted dirt paths — see PathClearance for why the woods owe them the same courtesy
            // the grass already pays. Cheap: two polylines of five and four points, tested last so the
            // village/spawn/crossing/dock rejections above have already thrown out most candidates.
            if (OnPaintedPath(p)) return false;

            return true;
        }

        /// <summary>Is <paramref name="p"/> within <see cref="PathClearance"/> of either painted dirt
        /// path? The polylines come straight out of <see cref="StPetersStarterSplat"/> — the same points
        /// the painter dabs the dirt along and the same points <see cref="StPetersRoutines"/> walks
        /// villagers down — so the tread, the clearing and the lane can never come apart.</summary>
        public static bool OnPaintedPath(Vector2 p) =>
            NearPolyline(p, StPetersStarterSplat.VillageToSlipPath(), PathClearance) ||
            NearPolyline(p, StPetersStarterSplat.VillageToBarHeadPath(), PathClearance);

        static bool NearPolyline(Vector2 p, Vector2[] points, float clearance)
        {
            if (points == null || points.Length < 2) return false;
            for (int i = 0; i + 1 < points.Length; i++)
                if (StPetersShoreMap.DistanceToSegment(p, points[i], points[i + 1]) < clearance)
                    return true;
            return false;
        }

        // =====================================================================================
        //  FLOWERS
        // =====================================================================================

        /// <summary>One planted bloom: where, which species key, and whether it is a single or a clump.</summary>
        public struct FlowerSite
        {
            public Vector2 Position;
            public string Species;
            /// <summary>True for a clump, false for a single stem. The kit's third tier (<c>Patch</c>) is
            /// deliberately unused — the owner still owes a verdict on it (#215), so planting hundreds of
            /// patches would be committing his taste for him.</summary>
            public bool Clump;
        }

        /// <summary>Grid spacing (m) of flower candidates — much coarser than the trees: blooms are
        /// accents on the meadow, not a carpet.</summary>
        public const float FlowerStep = 13f;
        public const float FlowerJitter = 5.5f;

        /// <summary>Fraction of candidate cells that actually bloom, hashed per cell. Keeps the meadow
        /// mostly grass, which is what makes the flowers read as flowers.</summary>
        public const float FlowerChance = 0.42f;

        /// <summary>
        /// Which bloom belongs on a patch of ground, most-wanted first. The habitats are the real ones:
        /// blue flag iris in the wet, lady's slipper on a shaded forest floor, fireweed on disturbed ground
        /// near the village, and the open-meadow crowd — lupin, buttercup, oxeye daisy, Queen Anne's lace,
        /// goldenrod, wild rose — everywhere else. Wild rose is named in §5.1's own brief for the reverting
        /// interior.
        /// </summary>
        public static string[] FlowerPreference(Vector2 worldPos, bool inWoods, float wetness,
                                                float distanceToVillage)
        {
            if (wetness >= WetThreshold + 0.12f)
                return new[] { "BlueFlag", "Fireweed", "Buttercup" };

            if (inWoods)
                // The provincial flower, and it grows in damp shade under conifers — the one bloom that
                // belongs INSIDE a stand rather than in the field beside it.
                return new[] { "LadySlipper", "BlueFlag", "Fireweed" };

            if (distanceToVillage < DooryardRadius)
                // Disturbed ground: dooryards, old gardens gone over, the edges of a path.
                return new[] { "Fireweed", "LupinPink", "LupinPurple", "QueenAnne", "Buttercup" };

            // The open meadow. Lupin is the iconic one and comes in four colours, so the pick rotates
            // through them on a hash rather than painting the whole island one shade.
            string lupin = LupinFor(worldPos);
            return new[] { lupin, "Buttercup", "OxeyeDaisy", "Goldenrod", "QueenAnne", "WildRose" };
        }

        /// <summary>Which lupin colour a patch takes — a stable hash, so a hillside is one colour and the
        /// next one over is another, the way they actually grow.</summary>
        static string LupinFor(Vector2 worldPos)
        {
            var colours = new[] { "LupinBlue", "LupinPink", "LupinPurple", "LupinWhite" };
            int band = Mathf.FloorToInt((StPetersShoreMap.Wiggle(
                worldPos * (StPetersShoreMap.BandWiggleScale / 60f), salt: 83) + 1f) * 0.5f * colours.Length);
            return colours[Mathf.Clamp(band, 0, colours.Length - 1)];
        }

        /// <summary>
        /// Every bloom on the island, deterministically, sharing the trees' clearings so nothing grows on
        /// the pier or across the crossing.
        /// </summary>
        public static List<FlowerSite> ScatterFlowers(ITidalTerrain terrain,
                                                      IReadOnlyCollection<string> available)
        {
            var sites = new List<FlowerSite>();
            if (terrain == null || available == null || available.Count == 0) return sites;

            float minX = StPetersBuilder.IslandCenter.x - StPetersBuilder.IslandRadius;
            float maxX = StPetersBuilder.IslandCenter.x + StPetersBuilder.IslandRadius;
            float minY = StPetersBuilder.IslandCenter.y - StPetersBuilder.IslandRadiusY;
            float maxY = StPetersBuilder.IslandCenter.y + StPetersBuilder.IslandRadiusY;

            int nx = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / FlowerStep));
            int ny = Mathf.Max(1, Mathf.CeilToInt((maxY - minY) / FlowerStep));

            for (int ix = 0; ix < nx; ix++)
            for (int iy = 0; iy < ny; iy++)
            {
                if (StPetersShoreMap.Hash01(ix, iy, 89) > FlowerChance) continue;

                float cx = minX + (ix + 0.5f) * FlowerStep
                           + (StPetersShoreMap.Hash01(ix, iy, 97) * 2f - 1f) * FlowerJitter;
                float cy = minY + (iy + 0.5f) * FlowerStep
                           + (StPetersShoreMap.Hash01(ix, iy, 101) * 2f - 1f) * FlowerJitter;
                var p = new Vector2(cx, cy);

                if (!IsPlantable(terrain, p)) continue;

                float e = terrain.ElevationAt(p);
                // ⚠ InWoods, not InStand: the lady's slipper belongs on a SHADED FOREST FLOOR, and after
                // the 2026-08-16 density ruling the shadiest floors on the island are the authored lots.
                // Asking the mosaic alone would have left the three closed woods with the open meadow's
                // lupins growing under them.
                string species = PickSpecies(
                    FlowerPreference(p, InWoods(p, e), WetnessAt(p),
                                     Vector2.Distance(p, StPetersBuilder.VillageHearthPos)),
                    available, StPetersShoreMap.Hash01(ix, iy, 107));
                if (species == null) continue;

                sites.Add(new FlowerSite
                {
                    Position = p,
                    Species = species,
                    // Clumps read better in the open; a single stem is right under trees.
                    Clump = StPetersShoreMap.Hash01(ix, iy, 103) < 0.6f,
                });
            }
            return sites;
        }

        // =====================================================================================
        //  shared
        // =====================================================================================

        /// <summary>The first preference the kit actually baked, or null. This is the whole reason
        /// preferences are ORDERED lists rather than single picks: a habitat degrades to its next-best
        /// species instead of leaving bare ground, and nothing here can name a species the kit is holding
        /// back.</summary>
        public static string FirstAvailable(IEnumerable<string> preference,
                                           IReadOnlyCollection<string> available)
            => PickSpecies(preference as string[] ?? System.Linq.Enumerable.ToArray(preference),
                           available, 0f);

        /// <summary>
        /// Pick from a habitat's preference list with a hashed <paramref name="roll"/> in [0,1), biased hard
        /// toward the leader, then walk forward (wrapping) to the first species the kit actually baked.
        ///
        /// <para><b>⚠ Why not just take the leader.</b> The first version did, and the result was an island
        /// of FOUR species out of nine: every patch of a given habitat resolved to the same tree, so a stand
        /// was one species copy-pasted and red oak, birch, aspen, pine and fir never appeared at all — they
        /// sat second and third on lists whose head was always available. A real Acadian stand is MIXED:
        /// maple with birch and aspen through it, spruce with fir. Squaring the roll keeps the habitat's
        /// leader dominant (a bit over a third of picks on an eight-long list) while letting the rest of the
        /// list in behind it — so a wood still reads as *that* habitat's wood, and it still reads as a wood
        /// rather than a plantation.</para>
        ///
        /// <para>The wrap is deliberate too: it means the tail of a list is reachable, so the occasional
        /// spruce stands in a hardwood grove. That happens on real ground.</para>
        /// </summary>
        public static string PickSpecies(string[] preference, IReadOnlyCollection<string> available,
                                        float roll)
        {
            if (preference == null || preference.Length == 0 || available == null) return null;

            int n = preference.Length;
            int start = Mathf.Clamp((int)(roll * roll * n), 0, n - 1);
            for (int i = 0; i < n; i++)
            {
                string s = preference[(start + i) % n];
                if (Has(available, s)) return s;
            }
            return null;
        }

        static bool Has(IReadOnlyCollection<string> set, string value)
        {
            foreach (string s in set) if (s == value) return true;
            return false;
        }
    }
}
#endif
