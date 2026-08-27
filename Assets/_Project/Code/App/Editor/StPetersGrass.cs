#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Art.Editor;   // VillageBuildingCatalog — a building clears its own footprint
using HiddenHarbours.Core;

// The edge band's tier lives in the runtime assembly, beside the slot byte that carries it and the
// renderer that has to read it back — see GrassFieldScatter. Aliased so this file can talk about the
// ground in the same words the field does.
using GrassTier = HiddenHarbours.Art.GrassTier;

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
        /// <c>StPetersDecorTests</c> re-derives it from the kits' own contracts rather than trusting
        /// this sentence.</para>
        ///
        /// <para><b>⚠ THE LARGEST BUILDING ON THIS ISLAND IS NO LONGER A HOUSE.</b> It was the white
        /// farmhouse at 7.68 × 9.94 m → 6.28 m of half-diagonal. Since the owner's 2026-08-11 ruling the
        /// general store is a SHOP rather than a house standing in for one, and its shell is
        /// 8.00 × 10.00 m → <b>6.40 m</b>. 7 m still clears it, with 0.60 m of margin where it had 0.72
        /// — so this number sits nearer its bound than it reads, and the next kit with a bigger shell
        /// moves it.</para>
        ///
        /// <para>⚠ It does NOT need to cover the drawn art, only the ground the building stands on.
        /// A tuft in front of a wall draws IN FRONT of it (lower world Y ⇒ higher sorting order) and
        /// reads as grass growing up against the clapboard, which is what you want.</para>
        /// </summary>
        public const float BuildingClearanceMetres = 7f;

        /// <summary>
        /// Every authored building site on the island — the kit houses, the two SHOPS, and Ginny's
        /// cottage, which is its own sprite rather than a kit entry. Taken from the builders' own
        /// constants so moving a building moves its clearing.
        ///
        /// <para><b>🔴 A SITE MISSING FROM THIS LIST IS SILENT, AND IT SWALLOWED A BUILDING.</b> The post
        /// office was placed on 2026-08-11 and not added here, so the meadow grew straight through its
        /// site — and because a room's floor sits at <c>ShopCatalog.RoomSortingOrder</c> (1), BELOW the
        /// Y-sort band the tufts live in, the grass drew OVER the floor. From outside the shop looked
        /// perfect. The only thing that showed it was rendering the interior reveal and finding the
        /// building had vanished into the meadow. Anything placed on this island gets a row here.</para>
        ///
        /// <para><b>⚠ THE HEARTH CAME OFF THIS LIST ON 2026-08-16, AND THAT IS DELIBERATE.</b> It held
        /// <c>StPetersBuilder.VillageHearthPos</c> while Ginny's cottage stood on it. She moved out to
        /// her own plot in the eastern woods and nothing replaced her, so the meadow closes back over
        /// the empty lot — which is what a vacated site on a reverting island looks like, and the whole
        /// of what "leave it tidy" means here. Her cottage and her three sheds took its place on the
        /// list, 85 m east.</para>
        /// </summary>
        public static Vector2[] BuildingSites
        {
            get
            {
                var keepouts = BuildingKeepouts;
                var sites = new Vector2[keepouts.Length];
                for (int i = 0; i < keepouts.Length; i++) sites[i] = keepouts[i].Position;
                return sites;
            }
        }

        /// <summary>
        /// One building's patch of bare ground: where it stands and how much meadow it holds off.
        ///
        /// <para><b>⭐ THE RADIUS IS PER SITE AS OF 2026-08-19, and the cannery is why.</b> It used to be
        /// one global <see cref="BuildingClearanceMetres"/> for everything, which worked while every
        /// building on the island was a house-sized box: 7 m cleared the biggest (the general store's
        /// 6.40 m) with 0.60 m to spare. The derelict cannery is <b>9.06 m</b> of half-diagonal — so the
        /// global would have had to go to 9.1, and every cottage on the green would have gained two
        /// metres of bald dirt to accommodate one building 170 m away. Taking the larger of the constant
        /// and the building's OWN footprint gives the cannery what it needs and leaves the village
        /// exactly as it was (every house's own reach is under 7 m, so the floor still governs).</para>
        /// </summary>
        public readonly struct MeadowKeepout
        {
            public readonly Vector2 Position;

            /// <summary>Metres of meadow held off, measured from <see cref="Position"/>.</summary>
            public readonly float RadiusMetres;

            /// <summary>What stands here — for a test's failure message, and for the reader.</summary>
            public readonly string What;

            public MeadowKeepout(Vector2 position, float radiusMetres, string what)
            {
                Position = position; RadiusMetres = radiusMetres; What = what;
            }
        }

        /// <summary>Every site, with the ground it actually needs. <see cref="BuildingSites"/> is the
        /// positions of these.</summary>
        public static readonly MeadowKeepout[] BuildingKeepouts = BuildKeepoutList();

        /// <summary>
        /// The meadow keepout a building of this reach needs: its own footprint, or
        /// <see cref="BuildingClearanceMetres"/>, whichever is larger.
        ///
        /// <para>The floor is not slack — a building wants a little bare ground round its walls whatever
        /// its size, and shrinking the small buildings' clearing to their own footprint would be a
        /// visible change nobody asked for.</para>
        /// </summary>
        public static float KeepoutRadiusFor(float footprintHalfDiagonalMetres) =>
            Mathf.Max(BuildingClearanceMetres, footprintHalfDiagonalMetres);

        /// <summary>
        /// The keepout for a build in the village building kit, by key. Falls back to the floor when the
        /// kit is not baked in this working tree.
        ///
        /// <para>⚠ Takes the ALREADY-SCANNED catalog rather than calling <c>Find</c> per build:
        /// <c>Find</c> re-reads and re-parses <c>Buildings.json</c> every call, and on a tree with no
        /// contract each one also logs an error — five red lines in the console for one list.</para>
        /// </summary>
        static MeadowKeepout ForKitBuild(List<VillageBuildingCatalog.Placement> kit,
                                         string buildKey, Vector2 at, string what)
        {
            float reach = 0f;
            if (kit != null)
                foreach (var p in kit)
                    if (p.IsValid && p.Key == buildKey)
                    {
                        reach = StPetersVillage.FootprintRadiusMetres(p);
                        break;
                    }
            return new MeadowKeepout(at, KeepoutRadiusFor(reach), what);
        }

        static MeadowKeepout AtTheFloor(Vector2 at, string what) =>
            new MeadowKeepout(at, BuildingClearanceMetres, what);

        static MeadowKeepout[] BuildKeepoutList()
        {
            // ONE scan for the whole list — see ForKitBuild.
            List<VillageBuildingCatalog.Placement> kit = VillageBuildingCatalog.Scan();

            var sites = new List<MeadowKeepout>
            {
                AtTheFloor(StPetersBuilder.SchoolPos, "school"),
                AtTheFloor(StPetersBuilder.GeneralStorePos, "general store"),  // a SHOP since 2026-08-11
                AtTheFloor(StPetersBuilder.WhiteFarmhousePos, "white farmhouse"),
                AtTheFloor(StPetersBuilder.RedSaltboxPos, "red saltbox"),
                AtTheFloor(StPetersBuilder.SageCottagePos, "sage cottage"),
                AtTheFloor(StPetersShops.PostOfficePos, "post office"),

                // Aunt Ginny's, out on her own plot in the eastern woods.
                AtTheFloor(StPetersGinnyPlot.CottagePos, "Ginny's cottage"),
            };

            // Her sheds come from the plot's OWN data rows rather than being copied across, so a shed
            // that moves — or a fourth one — cannot end up growing a meadow through its floor. That is
            // exactly the failure the 🔴 note above is about.
            foreach (var shed in StPetersGinnyPlot.Sheds)
                sites.Add(ForKitBuild(kit, shed.BuildKey, shed.Position, $"Ginny's {shed.Key}"));

            // The camper on its lot at the back of her land (2026-08-17), by the same rule and derived
            // for the same reason: it is the newest building on the island and therefore exactly the
            // shape of the post-office bug the 🔴 note above is about.
            sites.Add(AtTheFloor(StPetersCamperLot.LotPos, "the camper"));

            // The derelict cannery out by the pier (2026-08-19). It is DERELICT, which is the one thing
            // that could talk someone out of adding it here — weeds through the floor of an abandoned
            // building sound right. They are not: this list is what keeps the MEADOW from growing
            // through a building's SPRITE, and a building's floor sorts below the tuft band whether or
            // not anybody has swept it. The weeds a ruin deserves are drawn INTO its cell by the
            // lifecycle pass, which is where they belong.
            //
            // ⚠ AND IT IS THE ONE SITE THAT NEEDS MORE THAN THE FLOOR: 9.06 m of half-diagonal against a
            // 7 m constant. See MeadowKeepout for why that made the radius per site rather than moving
            // the constant.
            sites.Add(ForKitBuild(kit, StPetersCannery.BuildKey, StPetersCannery.Site, "the cannery"));

            return sites.ToArray();
        }

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
        ///
        /// <para><b>⭐ AND SINCE 2026-08-20, THE YARDS</b> (<see cref="StPetersYards"/>). Wild grass and a
        /// mown lawn are the same plant in a different PLACE, so the tufts stop at the mow line and the
        /// ground's own lawn takes over inside it. <b>This ADDS to the clearance discs; it replaces
        /// nothing.</b> A building with no yard row keeps exactly the disc it always had, and the meadow
        /// outside every authored polygon is unchanged site for site — <c>StPetersYardTests</c> pins that
        /// against a legacy predicate rebuilt from these same constants rather than asking you to believe
        /// it.</para>
        /// </summary>
        public static bool IsPlantableMeadow(ITidalTerrain terrain, Vector2 p)
        {
            if (terrain == null) return false;
            if (terrain.ElevationAt(p) < StPetersShoreMap.GrassFloorElevation) return false;

            // ⚠ Each site's OWN radius, not one constant — see MeadowKeepout. BuildingKeepouts is a
            // static readonly array, so this hot loop reads it once and allocates nothing;
            // BuildingSites (which projects it) deliberately is NOT used here.
            for (int i = 0; i < BuildingKeepouts.Length; i++)
                if (Vector2.Distance(p, BuildingKeepouts[i].Position) < BuildingKeepouts[i].RadiusMetres)
                    return false;

            // The mow line. Cheap where it has to be: every yard opens with its own bounding box, so a
            // candidate site out on the meadow costs four compares per row.
            if (StPetersYards.IsInsideAYard(p)) return false;

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

        /// <summary>Share of EDGE-BAND sites that take a broad clump, and of HEM sites. Both well above
        /// the interior's <see cref="BroadClumpShare"/>, and the reason is the ground rather than the
        /// budget: <b>grass at a cut or trodden edge spreads sideways instead of standing up.</b> The
        /// library already says so — the only art baked for <see cref="HabitatVerge"/>, the trodden
        /// ribbon beside a path, is the two wide low <c>ClumpWide</c> variants. A hem of low spreading
        /// clumps is what the edge of a field actually looks like.
        ///
        /// <para>⭐ It also pays for itself: a wide clump hides twice the ground for one renderer, so
        /// leaning on them is how the band stays a FIELD while carrying fewer tufts — the same trade
        /// that let <see cref="GrassStep"/> stop at 0.85 m instead of 0.80.</para></summary>
        public const float BandBroadClumpShare = 0.70f;

        /// <inheritdoc cref="BandBroadClumpShare"/>
        public const float HemBroadClumpShare = 0.85f;

        /// <summary>Whether this site wants the broadest art its habitat has. A ground decision: the
        /// planter answers WHICH art is broad, from whatever has been baked (the same split of
        /// concerns the habitat tag makes).
        ///
        /// <para><b>⚠ The interior arm is untouched, hash for hash</b> — the edge band is not allowed
        /// to re-roll ground the owner already ratified. Only a site inside the band takes the wider
        /// share, and it drops the field-strength gate with it: how strongly the swathe field runs
        /// through a piece of ground says nothing about whether it is at an EDGE, and a hem that
        /// spread only where the field happened to be strong would be a dotted hem.</para></summary>
        public static bool BroadAt(Vector2 worldPos, float hash01, GrassTier tier)
        {
            if (tier == GrassTier.Interior)
                return FieldStrength(worldPos) >= BroadClumpStrength && hash01 < BroadClumpShare;
            return hash01 < (tier == GrassTier.Hem ? HemBroadClumpShare : BandBroadClumpShare);
        }

        /// <inheritdoc cref="BroadAt(Vector2, float, GrassTier)"/>
        public static bool BroadAt(Vector2 worldPos, float hash01) =>
            BroadAt(worldPos, hash01, GrassTier.Interior);

        // =====================================================================================
        // ⭐⭐ THE EDGE BAND (2026-08-26) — where the field STOPS
        // =====================================================================================
        // The owner walked the retuned meadow and said the COVERAGE was right; what read wrong was
        // every place it ENDED. A field gated on a hard predicate ends in a hard line — a cell either
        // clears ChanceOpen (0.97) or plants nothing — so a tall-tuft field meets the flat compacted-
        // grass splat at a step from near-full cover to bare in one 0.85 m cell. All three of his
        // playtest spots are that one defect wearing different clothes: Ginny's plot edge (a yard's
        // mow line), the sparse field (a swathe-field contour), the tread verges (a walked path).
        //
        // So the gate stops being a step and becomes a RAMP. Every accept probability is multiplied by
        // EdgeFalloff, which is exactly 1 in the interior and eases to 0 at the boundary across a band
        // whose WIDTH itself meanders. Two things about that, and both are load-bearing:
        //
        //   ⚠ A FALLOFF OF CONSTANT WIDTH IS A STRIPE. Ramping over a fixed 3.2 m draws a perfectly
        //     parallel border inside every boundary on the island — round each building, along each
        //     fence, down both sides of each path. The eye reads a constant-width gradient as painted
        //     trim, which is a different artificial edge, not the absence of one. BandWidthAt wanders
        //     it on its own coherent noise so the field's outer envelope advances and retreats.
        //
        //   ⚠ THE INTERIOR IS UNTOUCHED, AND THAT IS THE POINT. EdgeFalloff returns exactly 1 beyond
        //     the band, so every site more than BandWidthAt metres inside the meadow plants exactly
        //     what it planted before — same hash, same roll, same blade, same place. The owner ratified
        //     the interior density; this pass is not allowed to spend it, and
        //     StPetersGrassEdgeBandTests pins that site for site rather than asking you to believe it.
        //
        // The step-down in HEIGHT rides the same distance (see GrassTier): a field that thins toward
        // its edge but stays knee-high to the last blade still ends in a line, just a dotted one.

        /// <summary>How deep the transition band runs (m) before the meander widens or narrows it.
        /// <b>3.2 m — about four grid steps</b>, which is the floor for a ramp that reads as a gradient
        /// rather than as two or three sparse rows. It sits mid-range of the 2.5–4 m the brief allowed
        /// so <see cref="EdgeBandJitter"/> can swing either way without collapsing the band at one end
        /// or doubling it at the other.</summary>
        public const float EdgeBandMetres = 3.2f;

        /// <summary>How far the band's width wanders, as a fraction of <see cref="EdgeBandMetres"/>.
        /// ±35% takes it between about 2.1 m and 4.3 m — the ends of the ruled range. See the section
        /// remarks for why a constant width is its own artefact.</summary>
        public const float EdgeBandJitter = 0.35f;

        /// <summary>Feature size (m) of the band-width meander. <b>Deliberately smaller than the swathe
        /// mosaic's <see cref="SwatheScale"/></b>: the meander has to vary several times along one
        /// building's clearing, or that building simply gets its own constant width and the stripe is
        /// back one scale up.</summary>
        public const float EdgeBandNoiseScale = 11f;

        /// <summary>The widest the band can ever be — the bound the yard rows' bounding-box rejection
        /// has to allow for, so a polygon just outside a site's box cannot be missed. DERIVED, so
        /// re-tuning the jitter cannot silently invalidate that rejection (rule 6).</summary>
        public const float EdgeBandCeilingMetres = EdgeBandMetres * (1f + EdgeBandJitter);

        /// <summary>The share of the band nearest the boundary that plants the SHORT classes — the hem.
        /// 0.30 of a 3.2 m band is a shade under a metre, the "last ~1 m" the brief asked for, and
        /// holding it as a FRACTION means the hem meanders with the band instead of cutting a
        /// constant-width line of its own inside a wandering one.</summary>
        public const float HemFractionOfBand = 0.30f;

        // -------------------------------------------------------------------------------------
        // ⭐⭐ TWO FLOORS, BECAUSE THE TWO KINDS OF EDGE ARE NOT THE SAME EDGE
        // -------------------------------------------------------------------------------------
        // 🔴 THE FIRST CUT OF THIS RAMPED EVERY BOUNDARY TO ZERO, AND IT WAS WRONG TWICE OVER — both
        // caught by measuring, neither by reading the code:
        //
        //   1. THE MEADOW STOPPED READING AS A FIELD. Whole-island coverage fell 82.6% → 76.9%, under
        //      the 80% the owner ratified. A QUARTER of this island's plantable ground lies within a
        //      band of some boundary — the swathe mosaic's contour is intricate, so its perimeter is
        //      enormous — and thinning all of it to nothing spends coverage he had already ruled good.
        //   2. IT DELETED THE VERGE. `HabitatVerge` exists ONLY in the 1.2 m ribbon either side of a
        //      walked tread, which is exactly the ground a ramp-to-zero erases. The island went from a
        //      hem of trodden clumps along both paths to SEVENTEEN tufts. A pass meant to soften an
        //      edge had quietly removed a habitat.
        //
        // ⭐ THE FIX IS NOT A NARROWER BAND, IT IS THE RIGHT SHAPE — and the two failures agree about
        // what that is. Stand at a real mow line: the wild grass is DENSE right up to it, because a
        // mower cuts a line rather than tapering one. What changes across it is HEIGHT. Stand where a
        // field simply peters out into thin ground and the density genuinely does fade.
        //
        // So a CUT / TRODDEN / BUILT edge (<see cref="ClearanceDistanceMetres"/> — mow lines, treads,
        // buildings, the wharf) keeps a high floor and lets the height step-down carry the whole
        // transition, while a FIELD CONTOUR (the swathe mosaic, the grass floor) fades much further.
        // Ramping the first kind to zero does not soften it: it digs a bald moat around every fence
        // and doorstep, which is its own artefact and a more obvious one than the line it replaced.

        /// <summary>Density floor at a CUT edge — a mow line, a walked tread, a building's ground, the
        /// wharf. <b>0.80: a fifth thinner at the line, and no more.</b> The transition here is carried
        /// by the height step-down; see the section note above for the two measured failures that set
        /// this number.</summary>
        public const float ClearanceFalloffFloor = 0.80f;

        /// <summary>Density floor at a FIELD CONTOUR — where the swathe mosaic dips below its own
        /// threshold, or the ground falls under the grass band's floor. <b>0.45</b>, because this edge
        /// really is the field running out. Still not zero: a fixed-width band ramped to nothing reads
        /// as a moat drawn around the thin ground rather than as thin ground.</summary>
        public const float FieldFalloffFloor = 0.45f;

        /// <summary>Half-step (m) of the central difference the contour distances take their gradient
        /// with. Half a grid step: local enough to describe the slope where the site actually stands,
        /// wide enough that a value-noise field's own lattice texture does not dominate it.</summary>
        public const float ContourProbeMetres = GrassStep * 0.5f;

        /// <summary>The answer a contour distance gives when the field is FLAT — "no boundary anywhere
        /// near", which on this island's plateau is the honest answer for the whole interior. A large
        /// finite number rather than <c>float.MaxValue</c> so callers can keep doing arithmetic on
        /// it.</summary>
        public const float FarInsideMetres = 1e6f;

        // The tier itself (<see cref="GrassTier"/>) lives in the RUNTIME assembly beside the slot byte
        // that carries it — this class decides which tier a metre of ground is in, and GrassField draws
        // it. An editor-only enum could not be read back by the thing that has to grow the meadow.

        /// <summary>The transition band's width at a point — <see cref="EdgeBandMetres"/> wandering on
        /// its own coherent noise. <b>Its own salt</b>, so the meander is independent of the swathe
        /// mosaic: a band that narrowed wherever the field already thinned would double the very
        /// correlation the meander exists to break.</summary>
        public static float BandWidthAt(Vector2 worldPos)
        {
            float w = StPetersShoreMap.Wiggle(
                worldPos * (StPetersShoreMap.BandWiggleScale / EdgeBandNoiseScale), salt: 223);
            return EdgeBandMetres * (1f + EdgeBandJitter * Mathf.Clamp(w, -1f, 1f));
        }

        /// <summary>
        /// Metres to the nearest HARD keepout edge — ground <see cref="IsPlantableMeadow"/> refuses for
        /// a reason that has an authored shape. Signed: negative inside a keepout.
        ///
        /// <para><b>Exact, because every one of them is a disc, a segment or a polygon.</b> This walks
        /// the same list the gate walks, in the same order, so a keepout that moves moves its band with
        /// it — and a site missing from <see cref="BuildingKeepouts"/> is one the GATE does not know
        /// about either, rather than a second list to fall out of sync (the 🔴 note on
        /// <see cref="BuildingSites"/> is about exactly that failure).</para>
        /// </summary>
        public static float ClearanceDistanceMetres(Vector2 p)
        {
            float best = FarInsideMetres;

            for (int i = 0; i < BuildingKeepouts.Length; i++)
                best = Mathf.Min(best, Vector2.Distance(p, BuildingKeepouts[i].Position)
                                       - BuildingKeepouts[i].RadiusMetres);

            // The mow lines. Cheap where it has to be, the same way IsInsideAYard is: a site out on the
            // meadow costs four compares per row and never walks a polygon's edges. ⚠ The box is grown
            // by the band's CEILING first — rejecting on the bare box would blind a site standing one
            // band-width OUTSIDE a fence, which is precisely the ground this function exists to find.
            var yards = StPetersYards.Yards;
            for (int i = 0; i < yards.Count; i++)
            {
                var polygon = yards[i].Polygon;
                if (!polygon.IsValid) continue;
                var box = polygon.Bounds;
                if (p.x < box.xMin - EdgeBandCeilingMetres || p.x > box.xMax + EdgeBandCeilingMetres ||
                    p.y < box.yMin - EdgeBandCeilingMetres || p.y > box.yMax + EdgeBandCeilingMetres)
                    continue;
                float d = polygon.DistanceToEdge(p);
                best = Mathf.Min(best, polygon.Contains(p) ? -d : d);
            }

            best = Mathf.Min(best, Vector2.Distance(p, StPetersBuilder.DockZonePos)
                                   - StPetersWoods.DockClearance);
            best = Mathf.Min(best, StPetersShoreMap.DistanceToSegment(
                                       p, StPetersBuilder.BerthFrom, StPetersBuilder.BerthTo)
                                   - StPetersWoods.DockClearance);
            best = Mathf.Min(best, DistanceToWalkedPath(p) - PathBareHalfWidthMetres);

            return best;
        }

        /// <summary>
        /// Metres from a smooth field's threshold contour, to first order:
        /// <c>(value − threshold) / |∇value|</c>. Signed the way the gate is — positive where the field
        /// passes.
        ///
        /// <para><b>Why a gradient rather than a search.</b> Both boundaries this is asked about are
        /// ISO-CONTOURS of continuous fields (the swathe mosaic, the terrain's grass floor), not
        /// authored shapes, so there is no geometry to measure a distance TO. Ring-probing outward for
        /// the first failing sample would cost tens of predicate evaluations per site and quantise the
        /// answer to the probe spacing; one central difference costs four samples and is exact for the
        /// linear part, which over 3 m of a 24 m-scale field is very nearly all of it.</para>
        ///
        /// <para><b>⚠ A FLAT FIELD HAS NO NEARBY CONTOUR, and that is the common case.</b> This island
        /// is a flat-topped plateau, so the elevation gradient inland is zero and the honest answer for
        /// the whole interior is <see cref="FarInsideMetres"/> — not zero, which would band the entire
        /// island, and not a division by zero.</para>
        /// </summary>
        public static float ContourDistance(float value, float threshold,
                                            float east, float west, float north, float south,
                                            float halfStep)
        {
            float v = value - threshold;
            float gx = (east - west) / (2f * halfStep);
            float gy = (north - south) / (2f * halfStep);
            float g = Mathf.Sqrt(gx * gx + gy * gy);
            if (g <= 1e-6f) return v >= 0f ? FarInsideMetres : -FarInsideMetres;
            return Mathf.Clamp(v / g, -FarInsideMetres, FarInsideMetres);
        }

        /// <summary>Metres from the swathe field's own <see cref="SwatheThreshold"/> contour — the edge
        /// of the worn ground the mosaic dips into, which is what the owner's sparse-field spot is made
        /// of.</summary>
        public static float SwatheDistanceMetres(Vector2 p)
        {
            float h = ContourProbeMetres;
            return ContourDistance(
                SwatheField(p), SwatheThreshold,
                SwatheField(p + new Vector2(h, 0f)), SwatheField(p - new Vector2(h, 0f)),
                SwatheField(p + new Vector2(0f, h)), SwatheField(p - new Vector2(0f, h)), h);
        }

        /// <summary>Metres from the grass band's own floor contour — the line where the painted ground
        /// stops reading as grass and the beach takes over.</summary>
        public static float GrassFloorDistanceMetres(ITidalTerrain terrain, Vector2 p)
        {
            if (terrain == null) return -FarInsideMetres;
            float h = ContourProbeMetres;
            return ContourDistance(
                terrain.ElevationAt(p), StPetersShoreMap.GrassFloorElevation,
                terrain.ElevationAt(p + new Vector2(h, 0f)), terrain.ElevationAt(p - new Vector2(h, 0f)),
                terrain.ElevationAt(p + new Vector2(0f, h)), terrain.ElevationAt(p - new Vector2(0f, h)),
                h);
        }

        /// <summary>Metres to the nearest FIELD CONTOUR — where the swathe mosaic dips below its
        /// threshold, or the ground falls under the grass band's floor. The soft kind of edge: this is
        /// the field genuinely running out, not something cutting it.</summary>
        public static float FieldContourDistanceMetres(ITidalTerrain terrain, Vector2 p) =>
            Mathf.Min(SwatheDistanceMetres(p), GrassFloorDistanceMetres(terrain, p));

        /// <summary>How far this ground is from the nearest edge of the field, in metres — the smaller
        /// of the cut edges and the field contours. Positive inside the meadow, negative outside it.
        /// <b>The TIER reads this</b>, because how tall a blade should be depends on how near an edge
        /// it is and not on which kind of edge it is; the DENSITY treats the two differently (see
        /// <see cref="ClearanceFalloffFloor"/>).</summary>
        public static float EdgeDistanceMetres(ITidalTerrain terrain, Vector2 p) =>
            Mathf.Min(ClearanceDistanceMetres(p), FieldContourDistanceMetres(terrain, p));

        /// <summary>
        /// The multiplier on a cell's accept chance — <b>1 in the interior, easing to 0 at the
        /// boundary</b> — and the tier the site's art steps down through on the way.
        ///
        /// <para>One call answers both because the DISTANCE is the expensive half and they read the
        /// same one; asking twice would double the cost of the pass to recompute a number it already
        /// had.</para>
        ///
        /// <para><b>Smoothstep, not a straight ramp.</b> Its derivative is zero at both ends: at the
        /// inner end that makes the band join the interior invisibly (a linear ramp meets full density
        /// at a crease), and at the outer end it leaves the last metre sparse rather than empty — which
        /// is the ground the hem's short blades are there to hold.</para>
        /// </summary>
        public static float EdgeFalloff(ITidalTerrain terrain, Vector2 p, out GrassTier tier)
        {
            float w = Mathf.Max(0.01f, BandWidthAt(p));
            float clearance = ClearanceDistanceMetres(p);
            float contour = FieldContourDistanceMetres(terrain, p);

            // The TIER is about how near ANY edge is — a blade a metre from a fence and a blade a
            // metre from the field's thin ground are both hem-height. The DENSITY is about WHICH edge,
            // which is the whole point of the two floors.
            float d = Mathf.Min(clearance, contour);
            tier = d >= w ? GrassTier.Interior
                 : d <= w * HemFractionOfBand ? GrassTier.Hem
                 : GrassTier.Band;

            // Whichever edge thins this ground more wins — a site caught between a fence and a thin
            // patch is as sparse as the thin patch, not the average of the two.
            return Mathf.Min(Ramp(clearance, w, ClearanceFalloffFloor),
                             Ramp(contour, w, FieldFalloffFloor));
        }

        /// <summary>One boundary's density ramp: 1 beyond the band, easing down to
        /// <paramref name="floor"/> at the boundary itself.
        ///
        /// <para><b>Smoothstep, not a straight line.</b> Its derivative is zero at both ends, so the
        /// band joins the interior invisibly — a linear ramp meets full density at a crease, which is
        /// a fainter version of the very line this removes.</para>
        ///
        /// <para>Below the boundary the answer is 0, but nothing reaches that: a site out there failed
        /// <see cref="IsPlantableMeadow"/> or <see cref="InSwathe"/> long before. It is here so the
        /// function is total rather than because the meadow needs it.</para></summary>
        public static float Ramp(float distance, float bandWidth, float floor)
        {
            if (distance >= bandWidth) return 1f;
            if (distance <= 0f) return 0f;
            return Mathf.Lerp(floor, 1f, Mathf.SmoothStep(0f, 1f, distance / bandWidth));
        }

        /// <inheritdoc cref="EdgeFalloff(ITidalTerrain, Vector2, out GrassTier)"/>
        public static float EdgeFalloff(ITidalTerrain terrain, Vector2 p) =>
            EdgeFalloff(terrain, p, out _);

        /// <summary>Which band of the field's edge this ground is in — the same answer
        /// <see cref="EdgeFalloff(ITidalTerrain, Vector2, out GrassTier)"/> gives, for callers that
        /// want only the tier.</summary>
        public static GrassTier TierAt(ITidalTerrain terrain, Vector2 p)
        {
            EdgeFalloff(terrain, p, out GrassTier tier);
            return tier;
        }

        // =====================================================================================
        // the stand edge — the other hard line, and the same fix
        // =====================================================================================

        /// <summary>
        /// The per-cell chance this ground carries grass, with the WOODS EDGE softened.
        ///
        /// <para><b>⚠ 0.97 to 0.10 across one 0.85 m cell is a hard line too.</b>
        /// <see cref="StPetersWoods.InStand"/> is a threshold on a noise field, so the duff under the
        /// trees used to meet the open meadow at a step of nearly a factor of ten in density — the same
        /// defect as the field's outer edge, drawn round every stand on the island. This blends the two
        /// chances over how much of a small ring is under canopy, so a stand's floor thins into the
        /// meadow the way the stand's own trees already thin into it (<c>InStand</c> tapers its
        /// threshold toward the coast for exactly this reason).</para>
        ///
        /// <para><b>⚠ It is a RING, not a gradient.</b> The stand test folds a noise field and a
        /// shelter taper into one comparison, so there is no single smooth value to differentiate —
        /// sampling the PREDICATE around the site and counting is both simpler and exactly what "how
        /// much of the ground around me is wood" means. The interior of a stand answers 1 and the open
        /// meadow answers 0, so neither of them changes.</para>
        /// </summary>
        public static float ChanceAt(ITidalTerrain terrain, Vector2 p, float elevation) =>
            Mathf.Lerp(ChanceOpen, ChanceWoods, StandFraction(terrain, p, elevation));

        /// <summary>Radius (m) of the ring <see cref="StandFraction"/> counts canopy on. Half the edge
        /// band, so the woods' hem is about as deep as the field's own and the two read as one
        /// language.</summary>
        public const float StandBlendRadiusMetres = EdgeBandMetres * 0.5f;

        /// <summary>How many probes the ring carries. <b>Eight</b> — the ring has to resolve which SIDE
        /// the wood is on, and four would quantise the blend to quarters, which is coarse enough to
        /// read as steps in the very density it is smoothing.</summary>
        public const int StandBlendProbes = 8;

        /// <summary>How much of the ground within <see cref="StandBlendRadiusMetres"/> is under the
        /// reverting mosaic, 0 (open meadow) to 1 (inside a stand). The centre counts as one probe, so
        /// a site standing in a copse too small to fill the ring still reads as mostly wood.</summary>
        public static float StandFraction(ITidalTerrain terrain, Vector2 p, float elevation)
        {
            if (terrain == null) return 0f;

            int inside = StPetersWoods.InStand(p, elevation) ? 1 : 0;
            for (int i = 0; i < StandBlendProbes; i++)
            {
                float a = (Mathf.PI * 2f * i) / StandBlendProbes;
                Vector2 q = p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * StandBlendRadiusMetres;
                if (StPetersWoods.InStand(q, terrain.ElevationAt(q))) inside++;
            }
            return inside / (float)(StandBlendProbes + 1);
        }

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

            /// <summary>How near the field's EDGE this site stands, and so how tall it may be
            /// (<see cref="GrassTier"/>). Like <see cref="Habitat"/> this is a statement about the
            /// GROUND — the planter turns it into a height class and picks art, and a tier with no art
            /// of its own falls back up the tiers rather than planting nothing.</summary>
            public GrassTier Tier;

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

                // ⭐ THE EDGE BAND. The chance the ground would carry at full strength, RAMPED DOWN as
                // the field runs out (EdgeFalloff is exactly 1 beyond the band, so the interior gate is
                // bit-for-bit the one that shipped). Both edges are softened here — the field's outer
                // envelope by the falloff, the woods' by ChanceAt's ring — because a step in density is
                // a drawn line whichever side of it is denser.
                float chance = ChanceAt(terrain, p, e) * EdgeFalloff(terrain, p);
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

                    // ⚠ The TIER is read at the TUFT's point, not the cell's — the same call HabitatAt
                    // makes and for the same reason. A slot offset can carry a blade the better part of
                    // a metre, which on a band this narrow is the difference between the hem and the
                    // band, and a tall blade standing one step outside its tier is exactly the hard
                    // line this pass exists to remove.
                    EdgeFalloff(terrain, q, out GrassTier tier);

                    sites.Add(new GrassTuftSite
                    {
                        Position = q,
                        CellX = ix,
                        CellY = iy,
                        Slot = t,
                        Habitat = HabitatAt(terrain, q),
                        Tier = tier,
                        Roll = HiddenHarbours.Art.GrassFieldScatter.VariantRoll(layout, ix, iy, t),
                        Broad = BroadAt(q, StPetersShoreMap.Hash01(ix * 11 + t, iy, 199), tier),
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
