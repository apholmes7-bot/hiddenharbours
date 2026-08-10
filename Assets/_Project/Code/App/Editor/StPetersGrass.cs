#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The grass layer</b> — wind-reactive tufts over the meadow, the ground cover the splat shader's
    /// grass BAND only paints and this layer makes move. Pure and deterministic, reusing the woods'
    /// habitat fields (<see cref="StPetersWoods"/>) so grass, shrubs and trees agree about the ground;
    /// <see cref="StPetersWoodsPlanter"/> does the Unity work.
    ///
    /// <para><b>Two systems, one meadow.</b> The splat ground's grass band (elevation ≥
    /// <see cref="StPetersShoreMap.GrassFloorElevation"/>) is the painted LOOK of grassland and STAYS
    /// exactly what it is: the static base layer. These tufts are the moving layer on top of it — they
    /// sway on the shared wind (<c>_WindWorld</c>, published by <c>GrassWindBridge</c>) and bend under
    /// the player (<c>GrassFootstep</c>). The floor below is therefore the BAND's floor, not the tree
    /// or shrub line: tufts belong wherever the ground already reads as grass.</para>
    ///
    /// <para><b>⭐ THE GREEN-OVER (2026-08-05): FIELDS, NOT SWATHES.</b> This layer used to lay ~590
    /// tufts in sweeps with worn ground between, which read as an island with some grass on it. The
    /// owner asked for one that reads GREEN — grass over most of the grassy island. So the grain got
    /// finer (<see cref="GrassStep"/>), the coverage gate got more generous
    /// (<see cref="SwatheThreshold"/>), and the worn ground that survives is now there because the
    /// field says so rather than because the scatter was sparse.</para>
    ///
    /// <para><b>⭐ AND SPECIES BY HABITAT, WHICH IS WHY THE DENSITY IS AFFORDABLE AT ALL.</b> A field
    /// this dense drawn from three tuft silhouettes reads as wallpaper the moment the player walks
    /// along it. Every site now resolves a HABITAT — dune by the sand, fringe at the splat boundary,
    /// straw-tinted headland where the wind scours, lush sward inland — and the planter draws from the
    /// grass library's variants carrying that tag (<c>GrassLibraryCatalog</c>). The habitat is decided
    /// HERE, from the same painted ground everything else on this island reads, and the art is chosen
    /// THERE, from whatever has been baked. Neither knows the other's list.</para>
    ///
    /// <para><b>⭐⭐ THE GROUND-COVER RETUNE (2026-08-05, the owner's first playtest of the green-over).
    /// </b> He stood in it and asked for a MEADOW: <i>"the whole screen covered in grass almost"</i>,
    /// with the tree-filled areas excepted and the paths NARROWER. The green-over's 3,780 tufts at a
    /// 2.2 m grain cover about a sixth of the ground they stand on — enough to say "there is grass
    /// here", not enough to say "this is a field". Three things changed together, and they only work
    /// together:</para>
    /// <list type="number">
    /// <item><b>The grain halved</b> (<see cref="GrassStep"/> 2.2 → 1.0 m) and every distance that
    /// scatters a tuft AROUND its cell — <see cref="GrassJitter"/> and
    /// <see cref="TuftSpreadMetres"/> — scaled with it. A jitter as wide as the step re-randomises the
    /// grid into a Poisson field, which is the one arrangement that leaves holes at any density; the
    /// ratios are held so the field stays EVEN as it gets dense.</item>
    /// <item><b>The worn ground stopped being noise and became the PATHS.</b> A near-continuous meadow
    /// buries a 2.5 m dirt track, so grass now reads the same two walked lines the splat paints
    /// (<see cref="DistanceToWalkedPath"/>) and leaves their tread bare with a trodden VERGE either
    /// side. The paths got narrower at the same time — one number, in the splat, that both layers
    /// read.</item>
    /// <item><b>The meadow got its own clearings</b> (<see cref="IsPlantableMeadow"/>). It used to
    /// borrow the TREES' — a 44 m village disc, a 40 m crossing sightline, a spawn disc — which at a
    /// sixth of coverage nobody could see and at full coverage would have left roughly a third of the
    /// island bald. Grass is ankle-high: it blocks no sightline and it grows up to a doorstep. Same
    /// move, same reason, as <c>StPetersShorePlants.IsPlantableShore</c>.</item>
    /// </list>
    ///
    /// <para>Every hash is position/index-stable (rule 5): a rebuild reproduces the meadow exactly, and
    /// a re-run with the same inputs converges rather than piling on.</para>
    /// </summary>
    public static class StPetersGrass
    {
        /// <summary>Grid spacing (m) of candidate sites. <b>0.85 m, down from 2.2 (and 4.0 before
        /// that).</b> The step sets the grain of the field, and the tuft count goes as its inverse
        /// SQUARE — so this is the one number to turn if the island has to get cheaper, and
        /// <c>StPetersDecorTests</c> re-derives its prediction from it rather than pinning a literal.
        ///
        /// <para><b>⚠ MEASURED, not picked — and the intuition here is wrong.</b> The library's tufts
        /// are 32 px at 32 ppu, a metre wide, so a metre grid ought to cover a metre of ground. It does
        /// not: the jitter that stops the field reading as rows also scatters it off the lattice, and a
        /// metre grid measured <b>73%</b> covered. Grass has to OVERLAP to read as a field. 0.85 m,
        /// with 1–2 tufts per cell and the wide 64 px clumps carrying the cores, is where the
        /// measurement clears the bar the owner set. Do not argue this number in the abstract: turn it,
        /// re-run <c>StPetersGreenOverTests</c>, and read what the island measures.</para></summary>
        public const float GrassStep = 0.85f;

        /// <summary>How far a site may wander from its cell centre. <b>Held at 0.41 × the step</b> — the
        /// ratio the green-over shipped (0.9 / 2.2). ⚠ This is not decoration: a jitter approaching the
        /// step turns a grid into a Poisson field, and a Poisson field of discs needs roughly three
        /// times the density of an even one to reach the same coverage. Scale it with the step or the
        /// retune buys holes.</summary>
        public const float GrassJitter = GrassStep * 0.41f;

        /// <summary>Radius (m) of the ring a cell's 2nd tuft is offset into. Also held at the
        /// green-over's ratio (0.7 / 2.2 ≈ 0.32 of the step) for the reason above — it used to be a
        /// literal 0.7 m, which at a 1 m grain would have thrown a cell's tufts into its neighbours'
        /// and undone the evenness the step buys.</summary>
        public const float TuftSpreadMetres = GrassStep * 0.32f;

        /// <summary>Feature size (m) of the swathe/worn-ground mosaic — smaller than the stands' 46 m:
        /// a field's texture, not a forest's.</summary>
        public const float SwatheScale = 24f;

        /// <summary>The coverage field (symmetric about 0) must clear this for ground to carry grass.
        /// <b>−0.78, down from −0.62 (and −0.15 before that).</b> Measured on the field itself: −0.15
        /// passed 51%, −0.62 passed 88%, −0.78 passes ~94%. What survives is worn ground the field
        /// genuinely dips into — hollows and bald patches — because the PATHS are now carved
        /// explicitly (<see cref="DistanceToWalkedPath"/>) instead of being hoped for out of this
        /// noise. Raising it back toward 0 is how the island gets patchy again;
        /// <c>StPetersDecorTests</c> keeps it honest by requiring the gate still reject something.
        /// </summary>
        public const float SwatheThreshold = -0.78f;

        /// <summary>Per-cell chance on open meadow — very nearly every swathe cell carries grass. The
        /// last 3% is what stops a machine-even field reading as AstroTurf; the swathe field above is
        /// where actual bald ground comes from.</summary>
        public const float ChanceOpen = 0.97f;

        /// <summary>Per-cell chance under a stand — shade-starved sparse, so the woods floor reads as
        /// duff with the odd tuft, not a lawn under trees.
        ///
        /// <para><b>⚠ 0.10, down from 0.30, and that is a HOLD not a cut.</b> The owner excepted
        /// "tree-filled areas" from the green-over by name. A per-cell chance is a chance per CELL, so
        /// leaving it at 0.30 while the grid went 2.2 → 1.0 m would have made the woods floor 4.8×
        /// denser — the one place he asked to keep clear. 0.10 holds the duff at about 1.5× its old
        /// absolute density, which is a forest floor that has thickened slightly, not a lawn.</para>
        /// </summary>
        public const float ChanceWoods = 0.10f;

        /// <summary>Tuft scale range, hashed per site (mirrors the GrassClump prefab's 0.85–1.25).</summary>
        public const float ScaleMin = 0.85f;
        public const float ScaleMax = 1.25f;

        /// <summary>The straw the coast bleaches toward — multiplied over the sprite's own gradient
        /// (the grass shader multiplies vertex colour), so the drawn shading survives the tint.</summary>
        public static readonly Color StrawTint = new Color(0.86f, 0.78f, 0.52f, 1f);

        // =====================================================================================
        // habitat
        // =====================================================================================

        /// <summary>The habitat tags this island resolves, matching the grass library's own vocabulary.
        /// Nothing here is a sprite name — the planter matches on the tag and takes whatever has been
        /// baked with it.</summary>
        public const string HabitatSward = "sward";
        public const string HabitatMeadow = "meadow";
        public const string HabitatFringe = "fringe";
        public const string HabitatDune = "dune";
        public const string HabitatHeadland = "headland";

        /// <summary>The trodden band either side of a walked path. ⭐ The library has declared and baked
        /// this tag since #425 (the wide low <c>ClumpWide</c> pair carry <c>meadow,verge</c>) and nothing
        /// on the island had ever resolved to it — because until the paths were carved there was no
        /// path EDGE to resolve. Now there is.</summary>
        public const string HabitatVerge = "verge";

        // =====================================================================================
        // ⭐ THIN POOLS AND THEIR ALLIES (the 2026-08-06 variety retune)
        // =====================================================================================
        // The owner stood in the retuned meadow and said the wide "saggy" clumps read as the SAME
        // sprite tiled. He was reading the library, not the scatter: of 29 baked variants only FIVE
        // are two cells wide, and the two that carry meadow (ClumpWideA/B) were the entire broad pool
        // for the island's cores — about two fifths of the ground, drawn from two silhouettes, four
        // with the mirror. The VERGE was worse: it is baked with exactly those two variants and
        // nothing else, so every metre of the ribbon either side of both walked paths was one of two
        // sprites.
        //
        // ⚠ THE HONEST FIX IS MORE BAKES — retag the saltmeadow pair `meadow,verge` in the rig, or
        // draw more wide clumps. Both are `docs/art/rigs/**`, the art-director's lane, and this pass
        // deliberately does not reach into it. What IS in this lane is which baked art a piece of
        // ground is allowed to wear, so a pool too thin to read as a field borrows from a documented
        // ALLY instead.
        //
        // This is the same failure the catalog's own "fall back to the whole library rather than paint
        // nothing" guard exists for, caught one step earlier and far more narrowly: an ally list, in
        // order, rather than the whole library.

        /// <summary>How many distinct variants a pool needs before it reads as a field rather than a
        /// pattern. <b>Four</b> — with the 50/50 mirror that is eight silhouettes, which at this grain
        /// is where the eye stops matching neighbours up. Below it the pool borrows (see
        /// <see cref="AlliesFor"/>); at or above it nothing is borrowed and the habitat wears exactly
        /// what was baked for it.</summary>
        public const int MinHabitatVariety = 4;

        /// <summary>
        /// Which habitats a thin pool may borrow art from, in order, until it clears
        /// <see cref="MinHabitatVariety"/>. Ordered by how alike the GROUND is, so the first borrow is
        /// always the least surprising one.
        ///
        /// <para><b>⚠ An ally lends only art of a height class the thin pool ALREADY has</b> (the
        /// chooser enforces it). That is what keeps a borrow from changing the habitat's read: the
        /// verge is trodden ground and its two baked variants are both SHORT, so it borrows the
        /// meadow's short blades and never its tall timothy — grass beside a walked track that stands
        /// knee-high is not a verge. Same reason the headland, which is wind-cropped, borrows only the
        /// sward's short carpet.</para>
        ///
        /// <para>Deliberately NOT a cycle: each list is walked once, front to back, and stops the
        /// moment the pool is varied enough. A habitat missing from this map simply never borrows.</para>
        /// </summary>
        public static string[] AlliesFor(string habitat) => habitat switch
        {
            // Trodden meadow is still meadow; the dune's wide saltmeadow is the only other low wide
            // art baked, and it is what the verge's BROAD pool needs (its two allies are the same
            // two ClumpWide sprites it already has).
            HabitatVerge => new[] { HabitatMeadow, HabitatDune },
            // The meadow's normal pool is 17 deep and borrows nothing. Its BROAD pool is the two
            // ClumpWide, and the dune's saltmeadow pair is the only other wide grass on the island —
            // saltmeadow hay grows in coastal meadows, so this is the ally the art already suggests.
            HabitatMeadow => new[] { HabitatDune, HabitatFringe },
            HabitatSward => new[] { HabitatMeadow },
            HabitatFringe => new[] { HabitatMeadow, HabitatDune },
            HabitatDune => new[] { HabitatMeadow, HabitatFringe },
            // Scoured open ground reads as a cropped version of the inland carpet.
            HabitatHeadland => new[] { HabitatSward, HabitatMeadow },
            _ => System.Array.Empty<string>(),
        };

        /// <summary>How far (m) from the grass band's edge still counts as FRINGE. One grid step plus a
        /// little: the fringe variants are the low wide ones whose whole job is to hide the splat
        /// boundary where the painted grass hands over to bare ground, so the band has to be at least
        /// as wide as the gap between two sites or the boundary shows through it.</summary>
        public const float FringeBandMetres = 2.6f;

        /// <summary>How far (m) from sand or the marram band still counts as DUNE. Wider than the fringe
        /// band because marram grows back off the beach, not just at its lip.</summary>
        public const float DuneBandMetres = 5f;

        /// <summary>Exposure above which open ground reads as scoured HEADLAND. 0.68 puts the rim just
        /// outside the woods' own WET/EXPOSED marks (0.62 / 0.55) — a coastal band about 14 m deep on
        /// this island, which is a headland. It was 0.5 in the first cut of the green-over and that
        /// classified 38% of the island as headland: on a 240 × 140 m ellipse an exposure ring is a
        /// large fraction of the whole, so this threshold is far more sensitive than it looks.</summary>
        public const float HeadlandExposure = 0.68f;

        /// <summary>
        /// Which habitat a site belongs to.
        ///
        /// <para><b>⚠ THE BOUNDARY TEST COMES FIRST, AND WHAT IT MEETS DECIDES WHICH EDGE IT IS.</b>
        /// The first cut of this asked "is there sand nearby?" before "am I on an edge?", and dune
        /// swallowed the fringe entirely — measured: <b>zero</b> fringe sites on the whole island,
        /// because the grass band's seaward edge always has the marram band within reach, so every
        /// boundary site answered "dune" before the fringe test ever ran. Both edges exist and they
        /// look different: grass giving out onto SAND wears marram, grass giving out onto cobble or a
        /// worn clearing wears low spreading fringe blades. So: find the edge, then ask what is on the
        /// other side of it.</para>
        ///
        /// <para><b>Read off the same painted ground as everything else.</b>
        /// <see cref="StPetersShoreMap.MaterialAt"/> is the splat the owner sees, so the grass agrees
        /// with the floor it stands on by construction — there is no second map of where the beach is.</para>
        /// </summary>
        public static string HabitatAt(ITidalTerrain terrain, Vector2 worldPos)
        {
            if (terrain == null) return HabitatMeadow;

            // THE VERGE COMES FIRST, because it is the most LOCAL claim on a piece of ground: the tread
            // is a metre and a half across and it crosses every other band on its way to the water.
            // Grass beside a track is trodden grass whether the track is crossing meadow or headland.
            if (DistanceToWalkedPath(worldPos) < PathBareHalfWidthMetres + PathVergeMetres)
                return HabitatVerge;

            // On an EDGE? Then which edge — the beach, or bare ground.
            if (!AllGrassWithin(terrain, worldPos, FringeBandMetres))
                return NearMaterial(terrain, worldPos, DuneBandMetres,
                                    ShoreMaterial.Sand,
                                    ShoreMaterial.Marram)
                    ? HabitatDune
                    : HabitatFringe;

            // HEADLAND — scoured open ground. The dry look is the straw tint (see TintAt); this picks
            // the wind-cropped silhouette to carry it.
            if (StPetersWoods.ExposureAt(worldPos) >= HeadlandExposure) return HabitatHeadland;

            // Inland: the short sward carpets, the taller meadow stands in it. Split on the swathe
            // field's own strength so the two interleave in patches instead of alternating per cell.
            return SwatheField(worldPos) > 0.15f ? HabitatMeadow : HabitatSward;
        }

        /// <summary>True when every probe within <paramref name="radius"/> is still on the grass band —
        /// i.e. this site is INSIDE the field rather than on its edge. Four axis probes plus the centre:
        /// enough to catch a boundary at this grain, and cheap enough to run per site (rule 7).</summary>
        public static bool AllGrassWithin(ITidalTerrain terrain, Vector2 p, float radius)
        {
            if (StPetersShoreMap.MaterialAt(terrain, p) != ShoreMaterial.Grass)
                return false;
            for (int i = 0; i < 4; i++)
            {
                Vector2 q = p + Probe(i) * radius;
                if (StPetersShoreMap.MaterialAt(terrain, q) != ShoreMaterial.Grass)
                    return false;
            }
            return true;
        }

        /// <summary>True when any probe within <paramref name="radius"/> lands on one of
        /// <paramref name="wanted"/>.</summary>
        public static bool NearMaterial(ITidalTerrain terrain, Vector2 p, float radius,
                                        params ShoreMaterial[] wanted)
        {
            for (int i = 0; i < 4; i++)
            {
                var m = StPetersShoreMap.MaterialAt(terrain, p + Probe(i) * radius);
                for (int w = 0; w < wanted.Length; w++) if (m == wanted[w]) return true;
            }
            return false;
        }

        static Vector2 Probe(int i) =>
            i == 0 ? Vector2.right : i == 1 ? Vector2.left : i == 2 ? Vector2.up : Vector2.down;

        // =====================================================================================
        // the walked paths
        // =====================================================================================
        // ⭐ THE ONE DEFINITION, TWO CONSUMERS. StPetersStarterSplat paints these lines as dirt; this
        // layer keeps grass off them. Both read the SAME polylines and the SAME width, the way the
        // sandbar's spine is drawn and exempted through one StPetersShoreMap.IsBarSpine — because a
        // path the ground calls dirt and the grass grows over is not a path, it is a bug you find in a
        // screenshot. Narrow the splat's PathWidthMetres and the meadow's bare tread narrows with it.

        /// <summary>Half the splat's own path width: inside this, no grass at all — this is the tread,
        /// the strip walked down to the dirt. DERIVED, never restated (rule 6).</summary>
        public static float PathBareHalfWidthMetres => StPetersStarterSplat.PathWidthMetres * 0.5f;

        /// <summary>How far past the tread the ground still reads as TRODDEN (m). One grid step and a
        /// bit: the verge variants are the low wide clumps, and the band has to be at least as wide as
        /// the gap between two sites or the trodden edge is a dotted line rather than a hem.</summary>
        public const float PathVergeMetres = 1.2f;

        static Vector2[][] _walkedPaths;

        /// <summary>The island's walked lines — the village green to the slip, and the village to the
        /// bar head. Cached because <see cref="Scatter"/> asks tens of thousands of times and each call
        /// allocates; safe to cache because both are pure functions of authored constants and a stable
        /// hash (rule 5), so they are the same array every time they are built.</summary>
        public static Vector2[][] WalkedPaths
        {
            get
            {
                // == null, not ??=: these are plain arrays so coalescing would be safe here, but the
                // house rule is one shape for every lazy field (Unity's fake-null defeats ??).
                if (_walkedPaths == null)
                    _walkedPaths = new[]
                    {
                        StPetersStarterSplat.VillageToSlipPath(),
                        StPetersStarterSplat.VillageToBarHeadPath(),
                    };
                return _walkedPaths;
            }
        }

        /// <summary>Metres from the nearest walked path's centre-line.</summary>
        public static float DistanceToWalkedPath(Vector2 p)
        {
            float best = float.MaxValue;
            var paths = WalkedPaths;
            for (int i = 0; i < paths.Length; i++)
            {
                var pts = paths[i];
                for (int s = 0; s + 1 < pts.Length; s++)
                {
                    float d = StPetersShoreMap.DistanceToSegment(p, pts[s], pts[s + 1]);
                    if (d < best) best = d;
                }
            }
            return best;
        }

        // =====================================================================================
        // the meadow's own clearings
        // =====================================================================================

        /// <summary>
        /// How far grass is kept off a village building's site (m).
        ///
        /// <para>The buildings pivot at their footprint CENTRE, so this has to cover the largest
        /// footprint's HALF-DIAGONAL — the owner may re-face a building and a quarter-turned one is
        /// deeper than a face-on one, the same argument the village's own spacing is solved against.
        /// The M1 kit's largest is the white farmhouse at 7.68 × 9.94 m → 6.28 m of half-diagonal, so
        /// 7 m clears it with a little to spare and <c>StPetersDecorTests</c> re-derives that from
        /// <c>Buildings.json</c> rather than trusting this sentence.</para>
        ///
        /// <para>⚠ It does NOT need to cover the drawn art, only the ground the building stands on.
        /// A tuft in front of a wall draws IN FRONT of it (lower world Y ⇒ higher sorting order) and
        /// reads as grass growing up against the clapboard, which is what you want.</para>
        /// </summary>
        public const float BuildingClearanceMetres = 7f;

        /// <summary>Every authored building site on the island — the five kit buildings plus Ginny's
        /// cottage, which is its own sprite rather than a kit entry. Taken from
        /// <see cref="StPetersBuilder"/>'s own constants so moving a house moves its clearing.</summary>
        public static readonly Vector2[] BuildingSites =
        {
            StPetersBuilder.CottagePos,
            StPetersBuilder.SchoolPos,
            StPetersBuilder.GeneralStorePos,
            StPetersBuilder.WhiteFarmhousePos,
            StPetersBuilder.RedSaltboxPos,
            StPetersBuilder.SageCottagePos,
        };

        /// <summary>
        /// Ground a TUFT may stand on.
        ///
        /// <para><b>⚠ Deliberately NOT <see cref="StPetersWoods.IsPlantable"/>, and this is the
        /// retune's third change.</b> That gate's radii are cut for TREES on the plateau: a 44 m
        /// village disc (five houses do not occupy 6,000 m²), a 40 m clearance either side of the
        /// crossing so you can SEE the bar over the treetops, a spawn disc so the first thing you look
        /// at is not a trunk. Every one of those is a reason about things that stand two storeys high.
        /// Grass is ankle-deep: it blocks no sightline, it grows up to a doorstep, and you are meant to
        /// wake up standing in it. At a sixth of coverage those discs were invisible; at the density
        /// the owner asked for they would have left about a third of the island bald, with a hard edge
        /// where the meadow started. <c>StPetersShorePlants.IsPlantableShore</c> made exactly this
        /// move for exactly this reason ("that one's radii are cut for trees … it would strip the whole
        /// intertidal in front of the village") — this is the same call on the other side of the beach.
        /// </para>
        ///
        /// <para>What genuinely stays clear: the ground the buildings occupy, the dock and its berth
        /// (a wharf is not a lawn — borrowed from <see cref="StPetersWoods.DockClearance"/> so there is
        /// one number for it), and the tread of the walked paths.</para>
        /// </summary>
        public static bool IsPlantableMeadow(ITidalTerrain terrain, Vector2 p)
        {
            if (terrain == null) return false;
            if (terrain.ElevationAt(p) < StPetersShoreMap.GrassFloorElevation) return false;

            for (int i = 0; i < BuildingSites.Length; i++)
                if (Vector2.Distance(p, BuildingSites[i]) < BuildingClearanceMetres) return false;

            if (Vector2.Distance(p, StPetersBuilder.DockZonePos) < StPetersWoods.DockClearance)
                return false;
            if (StPetersShoreMap.DistanceToSegment(
                    p, StPetersBuilder.BerthFrom, StPetersBuilder.BerthTo) < StPetersWoods.DockClearance)
                return false;

            if (DistanceToWalkedPath(p) < PathBareHalfWidthMetres) return false;

            return true;
        }

        // =====================================================================================
        // the field
        // =====================================================================================

        /// <summary>The coverage field, symmetric about 0 — where grass sweeps and where it wears
        /// through. One evaluation, used by both the gate and the habitat split, so they cannot
        /// disagree about where a sweep is strongest.</summary>
        public static float SwatheField(Vector2 worldPos) =>
            StPetersShoreMap.Wiggle(
                worldPos * (StPetersShoreMap.BandWiggleScale / SwatheScale), salt: 157);

        /// <summary>True where the swathe field says grass grows (before the per-cell chance).</summary>
        public static bool InSwathe(Vector2 worldPos) => SwatheField(worldPos) > SwatheThreshold;

        /// <summary>
        /// The per-tuft tint: green in shelter, bleaching toward straw with exposure, with a small
        /// hashed brightness jitter so a swathe is not one flat colour. Deterministic — the same
        /// position always tints the same (the paint tool's own rule, kept).
        /// </summary>
        public static Color TintAt(Vector2 worldPos, float hash01)
        {
            float straw = Mathf.Clamp01(StPetersWoods.ExposureAt(worldPos) * 0.8f);
            Color baseTint = Color.Lerp(Color.white, StrawTint, straw);
            float v = Mathf.Lerp(0.9f, 1.1f, hash01);
            return new Color(Mathf.Clamp01(baseTint.r * v),
                             Mathf.Clamp01(baseTint.g * v),
                             Mathf.Clamp01(baseTint.b * v), 1f);
        }

        /// <summary>How many tufts an accepted cell plants — denser where the swathe field is
        /// strongest, so a sweep of grass has a thick heart and thin margins.
        ///
        /// <para><b>⚠ Capped at 2, down from 3.</b> The cap is per CELL and the cell is now a metre
        /// across: three tufts inside a metre is a bouquet, not a field, and the extra one buys almost
        /// no coverage because it lands inside its neighbours. Density comes from the GRID now
        /// (<see cref="GrassStep"/>); this only shades it.</para></summary>
        public static int TuftsAt(Vector2 worldPos, float hash01)
        {
            float strength = FieldStrength(worldPos);
            return 1 + Mathf.Min(MaxTuftsPerCell - 1, (int)(strength * (1.5f + hash01)));
        }

        /// <summary>The structural ceiling on <see cref="TuftsAt"/> — the most any one candidate cell
        /// can ever plant. Named so <c>StPetersDecorTests</c> can derive its "the grid walk itself is
        /// wrong" bound from it instead of restating a literal that a re-tune would falsify.</summary>
        public const int MaxTuftsPerCell = 2;

        /// <summary>How far into the swathe a metre of ground is, 0 at the gate and 1 at the field's
        /// ceiling. Hoisted because three decisions now read it — how many tufts, whether the site
        /// takes a BROAD clump, and (through <see cref="TuftsAt"/>) the density prediction the tests
        /// re-derive.</summary>
        public static float FieldStrength(Vector2 worldPos) =>
            Mathf.InverseLerp(SwatheThreshold, 1f, SwatheField(worldPos));

        /// <summary>
        /// Field strength above which a site asks for the BROADEST art its habitat has — the library's
        /// wide 64 px clumps rather than a single 32 px tuft.
        ///
        /// <para><b>⭐ This is the retune's cheapest metre of coverage.</b> A wide clump covers twice
        /// the ground for one SpriteRenderer, so leaning on them in the cores is what lets the grid
        /// stop at 1.0 m instead of 0.8. It is deliberately not ALL of the core — a field drawn from
        /// two clump silhouettes is the wallpaper the habitat split exists to prevent — so the roll
        /// below mixes them with singles.</para>
        /// </summary>
        public const float BroadClumpStrength = 0.30f;

        /// <summary>Share of qualifying core sites that actually take a broad clump. Measured: 0.55 at
        /// a 0.35 strength gate put 39% of the island on clumps; 0.60 at 0.30 is as far as it goes
        /// before the two <c>ClumpWide</c> silhouettes (four, with the mirror) start to repeat.</summary>
        public const float BroadClumpShare = 0.60f;

        /// <summary>Whether this site wants the broadest art its habitat has. A ground decision: the
        /// planter answers WHICH art is broad, from whatever has been baked (the same split of
        /// concerns the habitat tag makes).</summary>
        public static bool BroadAt(Vector2 worldPos, float hash01) =>
            FieldStrength(worldPos) >= BroadClumpStrength && hash01 < BroadClumpShare;

        // =====================================================================================
        // the scatter
        // =====================================================================================

        /// <summary>
        /// The field's SHAPE for this island — where the scatter grid sits, how fine it is, and how far a
        /// site may wander off it. Handed to <see cref="HiddenHarbours.Art.GrassFieldScatter"/>, which owns
        /// the arithmetic that turns a cell index into a world point.
        ///
        /// <para><b>⚠ The grid is the RUNTIME's, not a private one.</b> The bake gates each site at exactly
        /// the point the renderer will later draw it, because both ask the same function. A twinned copy of
        /// the jitter on this side would be a second definition of the meadow, and the first re-tune would
        /// put grass on a footpath this pass believed it had kept clear.</para>
        /// </summary>
        public static HiddenHarbours.Art.GrassFieldLayout FieldLayout(int seed = 0)
        {
            float minX = StPetersBuilder.IslandCenter.x - StPetersBuilder.IslandRadius;
            float maxX = StPetersBuilder.IslandCenter.x + StPetersBuilder.IslandRadius;
            float minY = StPetersBuilder.IslandCenter.y - StPetersBuilder.IslandRadiusY;
            float maxY = StPetersBuilder.IslandCenter.y + StPetersBuilder.IslandRadiusY;

            return new HiddenHarbours.Art.GrassFieldLayout
            {
                OriginX = minX,
                OriginY = minY,
                CellSize = GrassStep,
                JitterMetres = GrassJitter,
                SpreadMetres = TuftSpreadMetres,
                CellsX = Mathf.Max(1, Mathf.CeilToInt((maxX - minX) / GrassStep)),
                CellsY = Mathf.Max(1, Mathf.CeilToInt((maxY - minY) / GrassStep)),
                Slots = MaxTuftsPerCell,
                Seed = seed,
            };
        }

        /// <summary>One planted tuft: where, what KIND of ground it is on, how big, and its tint.</summary>
        public struct GrassTuftSite
        {
            public Vector2 Position;

            /// <summary>Which grid cell and which of that cell's sites this is. Carried so the FIELD bake
            /// can put this site's byte in the right place without walking the island a second time — one
            /// definition of the meadow, not two.</summary>
            public int CellX, CellY, Slot;

            /// <summary>A habitat TAG (<see cref="HabitatAt"/>), not a sprite index. The planter picks
            /// art carrying this tag from the grass library, so adding a variant is a bake and never a
            /// change here.</summary>
            public string Habitat;

            /// <summary>0..2 — a stable per-site roll the planter uses to pick between the variants
            /// that match the habitat, so the same site always draws the same blade.</summary>
            public int Roll;

            /// <summary>Ask the planter for the BROADEST art this habitat carries (a wide clump rather
            /// than a single tuft). See <see cref="BroadAt"/>; a habitat with no wide bake simply plants
            /// a normal tuft.</summary>
            public bool Broad;

            /// <summary>Draw the sprite mirrored. Free variety — the tufts pivot bottom-CENTRE, the
            /// wind is world-space and the shader's bend reads sprite <c>uv.y</c>, so a mirrored tuft
            /// leans and bends exactly like an unmirrored one while doubling the silhouettes the eye
            /// has to tell apart. At a 1 m grain that is the difference between a field and a
            /// pattern.</summary>
            public bool Mirror;

            public float Scale;
            public Color Tint;
        }

        /// <summary>
        /// Every grass tuft on the island, deterministically. Shares the trees' clearings (nothing grows
        /// on the pier or across the crossing's approach) with the GRASS BAND's own floor — the ground
        /// the splat shader already paints green is exactly the ground that gets blades.
        /// </summary>
        public static List<GrassTuftSite> Scatter(ITidalTerrain terrain, int seed = 0)
        {
            var sites = new List<GrassTuftSite>();
            if (terrain == null) return sites;

            // ⚠ THE POSITIONS COME FROM THE RUNTIME SCATTER, not from arithmetic restated here. This walk
            // and GrassField's derive-at-load must agree about where every blade stands or the bake gates
            // one point and the renderer draws another; asking one function is how they cannot disagree.
            var layout = FieldLayout(seed);

            for (int ix = 0; ix < layout.CellsX; ix++)
            for (int iy = 0; iy < layout.CellsY; iy++)
            {
                var p = HiddenHarbours.Art.GrassFieldScatter.CellCentre(layout, ix, iy);

                // The MEADOW's own clearings (see IsPlantableMeadow) — the buildings, the wharf and the
                // walked tread. The floor inside it is the grass BAND's, not the tree line: the shrub
                // layer's lesson, that a borrowed floor throws away the beach-top metre this layer
                // exists to cover.
                if (!IsPlantableMeadow(terrain, p)) continue;

                if (!InSwathe(p)) continue;

                float e = terrain.ElevationAt(p);
                float chance = StPetersWoods.InStand(p, e) ? ChanceWoods : ChanceOpen;
                if (StPetersShoreMap.Hash01(ix, iy, 173) > chance) continue;

                int tufts = TuftsAt(p, StPetersShoreMap.Hash01(ix, iy, 179));
                for (int t = 0; t < tufts && t < layout.Slots; t++)
                {
                    // Sub-site offsets hashed on (cell, site-index), inside a ring small enough that the
                    // cluster reads as one clump of growth.
                    var q = HiddenHarbours.Art.GrassFieldScatter.SlotPosition(layout, ix, iy, t);

                    // The offset can spill across a clearing edge or under the band floor — each tuft
                    // re-passes the gate itself, so the invariants hold per BLADE, not per cell.
                    if (!IsPlantableMeadow(terrain, q)) continue;

                    float h = HiddenHarbours.Art.GrassFieldScatter.ShapeRoll(layout, ix, iy, t);
                    sites.Add(new GrassTuftSite
                    {
                        Position = q,
                        CellX = ix,
                        CellY = iy,
                        Slot = t,
                        Habitat = HabitatAt(terrain, q),
                        Roll = HiddenHarbours.Art.GrassFieldScatter.VariantRoll(layout, ix, iy, t),
                        Broad = BroadAt(q, StPetersShoreMap.Hash01(ix * 11 + t, iy, 199)),
                        Mirror = HiddenHarbours.Art.GrassFieldScatter.Mirrored(layout, ix, iy, t),
                        Scale = Mathf.Lerp(ScaleMin, ScaleMax, h),
                        Tint = TintAt(q, h),
                    });
                }
            }
            return sites;
        }

        // =====================================================================================
        // the measurement (the owner's acceptance criterion, as a number)
        // =====================================================================================

        /// <summary>
        /// The ground one tuft hides, as a WIDTH in metres — the library's own nominal tuft, 32 px at
        /// <c>GrassLibraryCatalog.Ppu</c> 32. Not a taste: it is the size of the art, restated here
        /// only so the measure below has something to count with, and <c>StPetersGreenOverTests</c>
        /// checks it against the committed manifest.
        /// </summary>
        public const float TuftWidthMetres = 1f;

        /// <summary>How DEEP a tuft's footprint runs, as a fraction of its width. A tuft is drawn
        /// standing up: most of its pixels are above the ground it is planted on, and only the splay
        /// at its base actually hides ground behind it. 1.0 would claim the sprite's whole square,
        /// which for a 2 m clump is a 2 m-deep claim it does not earn.</summary>
        public const float TuftFootprintDepthFraction = 0.7f;

        /// <summary>Spacing (m) of the measure's sample lattice. A quarter of a tuft: fine enough that
        /// a gap the size of one missing tuft registers.</summary>
        public const float CoverageSampleStep = TuftWidthMetres * 0.25f;

        /// <summary>
        /// <b>The owner's ask, as a fraction.</b> Walk the meadow — every metre this layer is allowed
        /// to plant on, outside the woods — and report how much of it falls under a tuft's own
        /// footprint. 1.0 is a lawn; the green-over measured about a sixth, which is the "some grass on
        /// it" the owner asked to replace.
        ///
        /// <para><b>The footprint is a BOX, not a disc, and its width comes from the ART.</b> Both
        /// matter and the first cut of this got both wrong: it modelled every tuft as a 0.5 m disc,
        /// which is 0.785 m² for a sprite that is a metre across, and it counted the 64 px CLUMPS —
        /// two fifths of the field, and the whole reason a 1 m grid is affordable — as if they were
        /// half their real width. Measured on the same island that reads as a meadow, the disc model
        /// reported 66% and the honest one 80%+: a measure that pessimistic does not fail safe, it
        /// fails by demanding renderers the picture does not need.</para>
        ///
        /// <para><paramref name="widthMetres"/> is how the ART gets in. This class cannot read the
        /// grass library (it is a pure classifier, and the library is a manifest on disk), so a caller
        /// that HAS resolved each site to a baked variant passes its width; a caller that has not gets
        /// <see cref="TuftWidthMetres"/> for everything, which is the pessimistic reading. The split is
        /// the habitat tag's, again: the ground here, the art there.</para>
        ///
        /// <para>Derived, not asserted: it walks the SAME gate the scatter plants through, so it moves
        /// with every knob rather than needing a re-pin. The stands are excluded because the woods
        /// floor is deliberately duff (<see cref="ChanceWoods"/>) — including it would report the one
        /// place the owner asked to keep clear as a failure.</para>
        /// </summary>
        public static float MeadowCoverage(ITidalTerrain terrain, List<GrassTuftSite> sites,
                                           System.Func<GrassTuftSite, float> widthMetres = null)
        {
            if (terrain == null || sites == null || sites.Count == 0) return 0f;

            // Every tuft as the ground box it hides, and the widest of them sets the bucket size so a
            // sample only ever has to test its own 3×3 neighbourhood — a straight scan would be tens of
            // thousands of tufts against hundreds of thousands of samples.
            var boxes = new List<(Vector2 centre, float halfW, float halfD)>(sites.Count);
            float widest = TuftWidthMetres;
            foreach (var s in sites)
            {
                float w = (widthMetres != null ? widthMetres(s) : TuftWidthMetres) * s.Scale;
                boxes.Add((s.Position, w * 0.5f, w * TuftFootprintDepthFraction * 0.5f));
                if (w > widest) widest = w;
            }

            float cell = widest;
            var buckets = new Dictionary<(int, int), List<int>>();
            for (int i = 0; i < boxes.Count; i++)
            {
                var key = (Mathf.FloorToInt(boxes[i].centre.x / cell),
                           Mathf.FloorToInt(boxes[i].centre.y / cell));
                if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = new List<int>();
                list.Add(i);
            }

            float minX = StPetersBuilder.IslandCenter.x - StPetersBuilder.IslandRadius;
            float maxX = StPetersBuilder.IslandCenter.x + StPetersBuilder.IslandRadius;
            float minY = StPetersBuilder.IslandCenter.y - StPetersBuilder.IslandRadiusY;
            float maxY = StPetersBuilder.IslandCenter.y + StPetersBuilder.IslandRadiusY;

            int meadow = 0, covered = 0;
            for (float x = minX; x <= maxX; x += CoverageSampleStep)
            for (float y = minY; y <= maxY; y += CoverageSampleStep)
            {
                var p = new Vector2(x, y);
                if (!IsPlantableMeadow(terrain, p)) continue;
                if (StPetersWoods.InStand(p, terrain.ElevationAt(p))) continue;
                meadow++;

                int cx = Mathf.FloorToInt(x / cell), cy = Mathf.FloorToInt(y / cell);
                bool hit = false;
                for (int dx = -1; dx <= 1 && !hit; dx++)
                for (int dy = -1; dy <= 1 && !hit; dy++)
                {
                    if (!buckets.TryGetValue((cx + dx, cy + dy), out var list)) continue;
                    for (int i = 0; i < list.Count; i++)
                    {
                        var b = boxes[list[i]];
                        if (Mathf.Abs(b.centre.x - x) <= b.halfW &&
                            Mathf.Abs(b.centre.y - y) <= b.halfD) { hit = true; break; }
                    }
                }
                if (hit) covered++;
            }

            return meadow == 0 ? 0f : (float)covered / meadow;
        }
    }
}
#endif
