using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>What lighting and shadowing the decor actually COSTS on St Peters</b> — the caster and binder
    /// counts, pinned with headroom, in the shape <c>StPetersGreenOverTests</c> established for the
    /// green-over's own budget.
    ///
    /// <para><b>⚠️ THIS IS A COUNT, NOT A FRAME TIME, AND THE DIFFERENCE MATTERS.</b> Nothing headless can
    /// stand in for the owner's GPU — a St Peters build in CI is a FALSE GREEN for painted content. What
    /// this CAN do is stop the number changing without anyone noticing, which is the failure mode that
    /// actually happened to the grass layer (~590 tufts to several thousand in one change, invisible in
    /// the editor until the frame rate said so on someone else's machine).</para>
    ///
    /// <para><b>Why the caster count is the number worth pinning.</b> A <see cref="SpriteLightBinder"/> is
    /// nearly free at runtime — it writes a property block on enable and then has no Update at all. A
    /// <see cref="SpriteShadow"/> is NOT: each one creates a child <see cref="SpriteRenderer"/> and runs a
    /// <c>LateUpdate</c> every frame to pose it (plus a throttled recompute at 10 Hz). So casters are the
    /// per-frame cost of this work, and the binders are the memory-and-draw-state cost.</para>
    ///
    /// <para><b>🔴 AND THE GRASS CASTS NOTHING.</b> Thousands of tufts each pushing a sheared quad and a
    /// per-frame LateUpdate is a rule-7 violation bought for a shadow the size of the tuft. That is
    /// asserted here rather than left as an intention, because "we decided not to" is exactly the kind of
    /// decision a later planter change undoes by accident.</para>
    /// </summary>
    public class LitDecorCasterBudgetTests
    {
        GameObject _go;
        TidalTerrain _terrain;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TidalTerrain_CasterBudgetTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        ITidalTerrain Terrain() => _terrain;

        // =========================================================================================

        /// <summary>
        /// The shore's shadow casters: what the planter's three-part rule actually selects, counted over
        /// the real scatter. The rule is <i>not algae, not the subtidal fringe, and standing tall enough</i>
        /// — see <c>StPetersWoodsPlanter.ShadowCasterMinHeightM</c> and <c>.SubtidalZone</c>.
        /// </summary>
        [Test]
        public void TheShoresShadowCasters_AreTheEmergentStands_AndStayInsideTheirBudget()
        {
            var defs = LoadDefs();
            Assert.AreEqual(16, defs.Count,
                $"Expected the 16 committed shoreline-plant Defs, found {defs.Count}. The budget below " +
                "is measured against the shipped set.");

            var sites = StPetersShorePlants.Scatter(Terrain());
            Assert.Greater(sites.Count, 0, "sanity: the shore scattered nothing");

            int casters = 0;
            var castingSpecies = new HashSet<string>();
            foreach (var site in sites)
            {
                if (!defs.TryGetValue(site.SpeciesKey, out var def)) continue;
                if (!Casts(def)) continue;
                casters++;
                castingSpecies.Add(site.SpeciesKey);
            }

            // The number CI reports, so a reviewer reads it without running Unity.
            Debug.Log($"[LitDecorCasterBudget] shore plants: {sites.Count} placed, {casters} cast " +
                      $"({castingSpecies.Count} species: {string.Join(", ", castingSpecies.OrderBy(s => s))}).");

            Assert.Greater(casters, 0,
                "NOTHING on the shore casts a shadow. Either the rule excluded every species or the " +
                "Defs lost their heights — a shore with no shadows at all is not the intended outcome.");

            // 🔴 The floor that actually matters: most of what is placed must NOT cast. The shore is
            // dominated by subtidal beds and weed mats, and if that ratio ever inverts the rule has
            // stopped meaning anything.
            Assert.Less(casters, sites.Count / 2,
                $"{casters} of {sites.Count} shoreline plants cast a shadow — more than half. The rule " +
                "is meant to select the tall EMERGENT stands out of a shore that is mostly submerged " +
                "beds and mats; at this ratio it is selecting almost everything, and every caster is a " +
                "child renderer plus a per-frame LateUpdate.");

            Assert.Less(casters, 900,
                $"{casters} shore casters is past the budget this pass was signed off at. Each one is a " +
                "child SpriteRenderer and a per-frame LateUpdate — raise the height floor, narrow the " +
                "zones, or take the number to the owner; do not just move this bound.");
        }

        /// <summary>
        /// 🔴 <b>Eelgrass is the species this rule nearly got wrong</b>, and it is worth its own test
        /// because the mistake was so reasonable. It is no alga — it is a true vascular plant standing
        /// 1.44 m — so an algae-only check waves it straight through. And it lives in the subtidal
        /// fringe, permanently under water, where a hard ground shadow is both physically wrong and drawn
        /// into the seabed band the plant sorts into.
        /// </summary>
        [Test]
        public void NothingInTheSubtidalFringeCasts_IncludingTheTallPlantThatIsNotAnAlga()
        {
            var defs = LoadDefs();

            Assert.IsTrue(defs.TryGetValue("Eelgrass", out var eelgrass),
                "Eelgrass is gone from the committed Defs — this guard is now guarding nothing.");
            Assert.IsFalse(eelgrass.Algae,
                "Eelgrass is recorded as an alga, which means an algae-only rule would exclude it and " +
                "this test no longer demonstrates why the ZONE check exists. Re-check the contract.");
            Assert.GreaterOrEqual(eelgrass.PlantedStandingHeightM,
                StPetersWoodsPlanter.ShadowCasterMinHeightM,
                "Eelgrass is now below the height floor as DRAWN, so the height rule alone would " +
                "exclude it and this test no longer demonstrates why the ZONE check exists.");
            Assert.IsFalse(Casts(eelgrass),
                "🔴 Eelgrass casts a shadow. It stands 1.44 m and is not an alga, so only the SUBTIDAL " +
                "ZONE check stops it — and it is permanently submerged, where a projected ground shadow " +
                "is wrong twice over.");

            foreach (var def in defs.Values)
                if (def.Zone == StPetersWoodsPlanter.SubtidalZone)
                    Assert.IsFalse(Casts(def),
                        $"{def.SpeciesKey} is in the subtidal fringe and casts a shadow.");
        }

        /// <summary>
        /// The grass is the whole reason a caster rule exists at all: it is the biggest population on the
        /// island and the worst possible caster. Asserted against the PLANTER's own source, because the
        /// decision lives there and a later edit is exactly what this is watching for.
        /// </summary>
        [Test]
        public void TheGrassCastsNothing_BecauseThousandsOfTuftsIsTheRuleSevenViolation()
        {
            var tufts = StPetersGrass.Scatter(Terrain());
            Assert.Greater(tufts.Count, 1000,
                "sanity: the grass layer is meant to be the big population this rule protects against");

            string planter = System.IO.File.ReadAllText(
                "Assets/_Project/Code/App/Editor/StPetersWoodsPlanter.cs");
            int grassStart = planter.IndexOf("//  GRASS", System.StringComparison.Ordinal);
            int grassEnd = planter.IndexOf("//  SHORE PLANTS", System.StringComparison.Ordinal);
            Assert.Greater(grassStart, 0, "Could not find the planter's GRASS section to check.");
            Assert.Greater(grassEnd, grassStart, "Could not bound the planter's GRASS section.");

            string grassSection = planter.Substring(grassStart, grassEnd - grassStart);
            StringAssert.DoesNotContain("SpriteShadow", grassSection,
                $"🔴 A SpriteShadow was added to the grass. There are {tufts.Count} tufts, and every one " +
                "would be a child renderer plus a per-frame LateUpdate, bought for a shadow the size of " +
                "a blade of grass. This is the rule-7 violation the caster rule exists to prevent.");
            StringAssert.DoesNotContain("SpriteLightBinder", grassSection,
                "A SpriteLightBinder was added to the grass. The grass has NO baked masks by design " +
                "(flat tufts plus the wind shader), so a binder there would publish nothing and cost a " +
                "component per tuft to do it.");
        }

        /// <summary>
        /// Every shrub carries both a binder and a caster, so the shrub population IS the shrub cost. The
        /// kit ships to order — a species with no sheet on disk is simply not planted — so this measures
        /// what is actually committed rather than what the contract declares.
        /// </summary>
        [Test]
        public void TheShrubLayer_CarriesABinderAndACasterEach_AndStaysInsideItsBudget()
        {
            var baked = StPetersShrubBake.Species
                .Where(s => System.IO.File.Exists(ShrubCatalog.SheetPath(
                    ShrubCatalog.VariantSheetStem(s, StPetersShrubBake.Stage, StPetersShrubBake.Phase),
                    ShrubCatalog.Channel.Albedo)))
                .ToList();

            if (baked.Count == 0)
                Assert.Ignore("No shrub sheets are on disk in this checkout, so there is no shrub layer " +
                              "to budget. (CI fetches LFS, so this should not trip there.)");

            var contract = ShrubCatalog.Load();
            Assert.IsNotNull(contract, "The shrub contract did not load.");
            var habitatOf = contract.Species.ToDictionary(e => e.Key, e => e.Habitat);

            var sites = StPetersShrubs.Scatter(
                Terrain(), baked, s => habitatOf.TryGetValue(s, out string h) ? h : null,
                ShrubCatalog.Variants);

            // 🔴 EVERY PLANTED SPECIES MUST HAVE ITS LIGHT CHANNELS ON DISK. This is the assertion that
            // records the finding this pass turned up: the shrub rig's `_light` and `_calendar` sheets
            // were ALREADY committed — the work needed a consumer, not a re-bake. If a later re-bake
            // drops them, the shrubs go quietly flat while everything around them lights, and nothing
            // else in the suite would notice.
            foreach (string species in baked)
            {
                string stem = ShrubCatalog.VariantSheetStem(
                    species, StPetersShrubBake.Stage, StPetersShrubBake.Phase);

                string light = ShrubCatalog.SheetPath(stem, ShrubCatalog.Channel.Light);
                Assert.IsTrue(System.IO.File.Exists(light),
                    $"{species} is planted but has no light sheet at '{light}', so it draws UNLIT beside " +
                    "lit neighbours. The rig authors these masks — re-bake the kit to emit them.");

                string state = ShrubCatalog.SheetPath(stem, ShrubCatalog.Channel.State);
                Assert.IsTrue(System.IO.File.Exists(state),
                    $"{species} has no calendar sheet at '{state}'. Its RED channel is the no-rim flag " +
                    "for veil pixels — without it a veil rims like a trunk.");

                foreach (string sheet in new[] { light, state })
                    Assert.IsTrue(System.IO.File.Exists(sheet + ".meta"),
                        $"'{sheet}' has no .meta, so Unity will mint one on next import and the GUID " +
                        "every reference resolves through will change under the project.");
            }

            Debug.Log($"[LitDecorCasterBudget] shrubs: {sites.Count} placed from {baked.Count} baked " +
                      "species — each carries one SpriteLightBinder and one SpriteShadow.");

            Assert.Less(sites.Count, 1200,
                $"{sites.Count} shrubs is past the budget this pass was signed off at. EVERY shrub now " +
                "carries a shadow caster (a child renderer plus a per-frame LateUpdate), so this count " +
                "is a per-frame cost in a way it was not before this change.");
        }

        /// <summary>
        /// The light channels the shoreline-plant Defs point at must actually be the ones their own
        /// species baked. A Def wired to a NEIGHBOUR's sheet lights, looks plausible, and is wrong — the
        /// exact class of bug a contract test exists to catch.
        /// </summary>
        [Test]
        public void EveryShorePlantDef_PointsAtItsOwnBakedLightChannels()
        {
            var defs = LoadDefs();
            var seen = new Dictionary<string, string>();

            foreach (var def in defs.Values)
            {
                Assert.IsNotNull(def.LightSheet,
                    $"{def.SpeciesKey}'s Def carries no light sheet, so it draws UNLIT while the rest of " +
                    "the shore lights. Re-run Hidden Harbours ▸ Dev ▸ Build Shore Plant Defs.");
                Assert.IsTrue(def.HasLightChannels, $"{def.SpeciesKey} reports no light channels.");

                string lightPath = AssetDatabase.GetAssetPath(def.LightSheet);
                StringAssert.Contains(def.SpeciesKey, lightPath,
                    $"{def.SpeciesKey}'s Def points at '{lightPath}', which is not its own light sheet. " +
                    "A plant wearing a neighbour's light channel looks plausible and is wrong.");
                StringAssert.EndsWith("_light.png", lightPath,
                    $"{def.SpeciesKey}'s light sheet is '{lightPath}' — not the rig's _light channel.");

                Assert.IsFalse(seen.ContainsKey(lightPath),
                    $"{def.SpeciesKey} and {(seen.TryGetValue(lightPath, out string other) ? other : "?")} " +
                    $"share the light sheet '{lightPath}'. Every species bakes its own.");
                seen[lightPath] = def.SpeciesKey;

                if (def.TideStateSheet != null)
                    StringAssert.EndsWith("_tide.png", AssetDatabase.GetAssetPath(def.TideStateSheet),
                        $"{def.SpeciesKey}'s rim-gate sheet is not the rig's _tide state sheet. The " +
                        "no-rim flag lives in its BLUE channel; reading any other sheet gates on noise.");
            }
        }

        // =========================================================================================
        //  THE WOODS  (owner's casting ruling, 2026-08-05)
        // =========================================================================================

        /// <summary>
        /// 🔴 <b>The caster must be LIVE on the path that ships, not merely present somewhere.</b> This is
        /// the plumbed-but-unread lesson: a component wired into a scene, a prefab or a test fixture proves
        /// nothing about the tree the builder actually plants. So this drives
        /// <see cref="AcadianTreeCatalog.Configure"/> — the ONE method every Acadian tree is built through,
        /// the planter's line 367, the paint tool's and the prefab builder's — and watches a caster appear
        /// on the object that came out.
        ///
        /// <para>The negative control is the first assert: the same GameObject, carrying the same
        /// <see cref="SpriteRenderer"/>, has no shadow until <c>Configure</c> touches it. If that ever
        /// passes trivially — because something else in the fixture attached one — the test below is
        /// measuring itself.</para>
        /// </summary>
        [Test]
        public void ATreeCasts_FromTheOnePlaceATreeIsBuilt()
        {
            var placements = AcadianTreeCatalog.Scan();
            if (placements.Count == 0)
                Assert.Ignore("The Acadian tree kit is not baked in this checkout, so there is no tree to " +
                              "configure. (CI fetches LFS, so this should not trip there.)");

            var placement = placements[0];
            var sprite = AcadianTreeCatalog.LoadVariant(placement, 0);
            if (sprite == null)
                Assert.Ignore($"{placement.Stem} has no sliced variant on disk in this checkout.");

            var go = new GameObject("caster-budget-tree");
            try
            {
                go.AddComponent<SpriteRenderer>();

                // 🔴 NEGATIVE CONTROL. A sprite is not a caster. If this ever fails, the assert below has
                // stopped being evidence of anything.
                Assert.IsNull(go.GetComponent<SpriteShadow>(),
                    "A bare SpriteRenderer already carried a SpriteShadow before Configure ran — this " +
                    "test can no longer tell you that CONFIGURE is what attaches it.");

                AcadianTreeCatalog.Configure(go, placement, sprite, AcadianTreeCatalog.LoadMaterial());

                Assert.IsNotNull(go.GetComponent<SpriteShadow>(),
                    "A configured tree does not cast. Configure is the one place a tree is built — the " +
                    "planter, the Tree Paint Tool and the prefab builder all come through it — so a " +
                    "caster missing HERE is a caster missing from the whole island.");

                // Feet-anchored means the caster rides the TREE's own object: SpriteShadow poses its child
                // from this transform, and the rig's pivot is the trunk foot, so a caster parked anywhere
                // else would anchor its silhouette at the wrong ground point.
                Assert.AreEqual(go, go.GetComponent<SpriteShadow>().gameObject,
                    "The caster is not on the tree's own object, so it would anchor somewhere else.");

                // Re-configuring an existing tree (the paint tool re-dressing one, a prefab rebuild) must
                // not stack a second caster — that is two child renderers and two LateUpdates per tree.
                AcadianTreeCatalog.Configure(go, placement, sprite, AcadianTreeCatalog.LoadMaterial());
                Assert.AreEqual(1, go.GetComponents<SpriteShadow>().Length,
                    "Configure stacked a second SpriteShadow on re-configure — every tree would pay twice.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>
        /// What the woods COST, bounded by the planter's own arithmetic rather than by a number somebody
        /// once measured. Every planted tree carries exactly one caster (<c>Configure</c> is
        /// unconditional — there is no short tree in this kit, the smallest mature cell is 4.8 m), so the
        /// caster count IS the tree count, and the tree count cannot exceed the candidate grid the scatter
        /// walks: the island's own footprint divided by <see cref="StPetersWoods.TreeStep"/>, both sides.
        ///
        /// <para><b>That is the bound worth pinning, because it moves when the knob moves.</b> Tightening
        /// <c>TreeStep</c> — which the owner's 2026-08-01 "add more trees" ask already did once, 6.5 → 5.4 —
        /// raises the ceiling as its inverse SQUARE, and the test says so instead of failing at a literal
        /// that has quietly stopped describing the island.</para>
        /// </summary>
        [Test]
        public void TheWoodsCasterCount_IsOnePerPlantedTree_InsideThePlantersOwnGridBound()
        {
            var placements = AcadianTreeCatalog.Scan();
            if (placements.Count == 0)
                Assert.Ignore("The Acadian tree kit is not baked in this checkout, so nothing plants.");

            // Index exactly as PlantTrees does, so the scatter under test is the scatter that ships.
            var bySpecies = new Dictionary<string, AcadianTreeCatalog.Placement>();
            foreach (var p in placements)
                if (!bySpecies.ContainsKey(p.Species)) bySpecies[p.Species] = p;

            var sites = StPetersWoods.ScatterTrees(
                Terrain(), bySpecies.Keys,
                species => bySpecies.TryGetValue(species, out var pl)
                           ? AcadianTreeCatalog.VariantCount(pl.Entry) : 1);

            // The grid the scatter walks: the island's bounding box stepped by TreeStep, at most one tree
            // per cell. Mirrored from ScatterTrees rather than restated as a number.
            float minX = StPetersBuilder.IslandCenter.x - StPetersBuilder.IslandRadius;
            float minY = StPetersBuilder.IslandCenter.y - StPetersBuilder.IslandRadiusY;
            int nx = Mathf.Max(1, Mathf.CeilToInt(2f * StPetersBuilder.IslandRadius / StPetersWoods.TreeStep));
            int ny = Mathf.Max(1, Mathf.CeilToInt(2f * StPetersBuilder.IslandRadiusY / StPetersWoods.TreeStep));
            int candidateCells = nx * ny;

            // 🔴 THE PREDICTION, not a remembered number. Walk that same grid UNJITTERED and count the
            // cells that are plantable ground inside a stand: that is the tree count up to the jitter
            // shuffling cells across a stand boundary and back. It tracks every knob that decides the
            // count — the step, the island's size, StandThreshold, the tree line, the clearings — so
            // moving any of them moves the bound with it instead of failing at a literal that has quietly
            // stopped describing the island.
            int predicted = 0;
            for (int ix = 0; ix < nx; ix++)
            for (int iy = 0; iy < ny; iy++)
            {
                var p = new Vector2(minX + (ix + 0.5f) * StPetersWoods.TreeStep,
                                    minY + (iy + 0.5f) * StPetersWoods.TreeStep);
                if (!StPetersWoods.IsPlantable(Terrain(), p)) continue;
                if (StPetersWoods.InStand(p, Terrain().ElevationAt(p))) predicted++;
            }

            Debug.Log($"[LitDecorCasterBudget] woods: {sites.Count} trees planted, each carrying one " +
                      $"SpriteShadow — predicted {predicted} from the unjittered grid, out of {nx}×{ny} " +
                      $"= {candidateCells} candidate cells (TreeStep {StPetersWoods.TreeStep} m over the " +
                      $"{2f * StPetersBuilder.IslandRadius}×{2f * StPetersBuilder.IslandRadiusY} m island).");

            Assert.Greater(sites.Count, 0,
                "Nothing planted, so nothing casts — the woods would throw no shadow at all.");
            Assert.Greater(predicted, 0,
                "The unjittered grid predicts no trees at all, so the prediction below is not a bound. " +
                "Either the stand field or the plantable rule has changed shape.");

            Assert.LessOrEqual(sites.Count, candidateCells,
                $"{sites.Count} trees out of a {candidateCells}-cell grid is impossible — the scatter " +
                "plants at most one per cell, so either the grid maths here or the scatter has changed.");

            // The band is wide enough for the jitter (±2.4 m against a 5.4 m step moves boundary cells in
            // and out of a stand) and narrow enough that a silent DOUBLING — the failure that actually
            // happened to the grass layer — cannot land inside it. Every tree is now a child renderer and
            // a per-frame LateUpdate on top of the SpriteRenderer it already was.
            float ratio = sites.Count / (float)predicted;
            Assert.Greater(ratio, 0.6f,
                $"{sites.Count} trees against a predicted {predicted} ({ratio:P0}). The woods have thinned " +
                "well past what the stand field says should be standing — the scatter and the field it " +
                "walks have come apart.");
            Assert.Less(ratio, 1.6f,
                $"{sites.Count} trees against a predicted {predicted} ({ratio:P0}) — and every one of them " +
                "is now a shadow caster. The knobs are StPetersWoods.StandThreshold " +
                $"({StPetersWoods.StandThreshold}) and TreeStep ({StPetersWoods.TreeStep} m, count goes as " +
                "its inverse SQUARE); take the number to the owner before moving this bound.");
        }

        /// <summary>
        /// 🔴 <b>The woods must cast through <c>Configure</c>, not through a line of their own.</b> Asserted
        /// against the PLANTER's own source, the way the grass rule below is: a hand-rolled
        /// <c>AddComponent&lt;SpriteShadow&gt;()</c> in the TREES section would work, would pass every
        /// count above, and would quietly mean a painted tree and a planted tree are different objects
        /// again — which is the exact drift <c>Configure</c> exists to prevent.
        /// </summary>
        [Test]
        public void TheTreeSection_CastsThroughConfigure_NotThroughItsOwnLine()
        {
            string planter = System.IO.File.ReadAllText(
                "Assets/_Project/Code/App/Editor/StPetersWoodsPlanter.cs");
            int start = planter.IndexOf("//  TREES", System.StringComparison.Ordinal);
            int end = planter.IndexOf("//  SHRUBS", System.StringComparison.Ordinal);
            Assert.Greater(start, 0, "Could not find the planter's TREES section to check.");
            Assert.Greater(end, start, "Could not bound the planter's TREES section.");

            string trees = planter.Substring(start, end - start);
            StringAssert.Contains("AcadianTreeCatalog.Configure", trees,
                "The planter no longer builds its trees through AcadianTreeCatalog.Configure, so whatever " +
                "that method attaches — the wind material, the trunk anchor, the reflection, the shadow — " +
                "is no longer what the island gets.");
            StringAssert.DoesNotContain("SpriteShadow", trees,
                "The TREES section attaches a SpriteShadow of its own. It belongs in Configure, where the " +
                "Tree Paint Tool and the prefab builder see it too — otherwise a painted treeline stops " +
                "casting and nobody finds out until a screenshot.");
        }

        /// <summary>
        /// <b>How far the woods' shadows actually reach, measured against the water.</b> A projected shadow
        /// is drawn on the ground, and this one is drawn ABOVE the sea plane in sorting order — so a shadow
        /// that rakes off the shore lies ON the water rather than under it.
        ///
        /// <para><b>This test REPORTS rather than clips.</b> The dawn/dusk reach is physics, not a bug: a
        /// 5.7 m spruce at a sun 11° up throws ~29 m, and the island is only 140 m across. Inventing a
        /// clip here would be designing the boat slice's water-compositing problem from the wrong lane. So
        /// the numbers go in the log for the coordinator, and the assert is confined to the case that
        /// WOULD be a bug: a NOON stub — barely a third of the tree's height — has no business touching
        /// the sea from a tree standing above the tree line.</para>
        /// </summary>
        [Test]
        public void TheShorelineTreesReach_IsMeasured_AndTheNoonStubStaysAshore()
        {
            var placements = AcadianTreeCatalog.Scan();
            if (placements.Count == 0)
                Assert.Ignore("The Acadian tree kit is not baked in this checkout, so nothing plants.");

            var bySpecies = new Dictionary<string, AcadianTreeCatalog.Placement>();
            foreach (var p in placements)
                if (!bySpecies.ContainsKey(p.Species)) bySpecies[p.Species] = p;

            var sites = StPetersWoods.ScatterTrees(
                Terrain(), bySpecies.Keys,
                species => bySpecies.TryGetValue(species, out var pl)
                           ? AcadianTreeCatalog.VariantCount(pl.Entry) : 1);

            var profile = DayNightProfile.CreateDefault();
            var contract = TreeKitCatalog.Load();
            int ppu = contract != null && contract.ppu > 0 ? contract.ppu : 32;

            // Spring high — the most water there ever is, so "reaches water" is asked at its worst.
            float springHigh = StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude;

            // The component's own defaults; the shear is length × the caster sprite's full cell height.
            const float LengthAtNoon = 0.35f, LengthAtHorizon = 5f, MaxLength = 7f;

            // The three hours the owner reads the sun at, resolved once: direction and length depend only
            // on the clock, and only the multiplication by the tree's height is per-tree.
            var hours = new[]
            {
                ("dawn", profile.SunriseHour + 0.5f),
                ("noon", 13f),
                ("dusk", profile.SunsetHour - 0.5f),
            };

            var wet = new Dictionary<string, int> { ["dawn"] = 0, ["noon"] = 0, ["dusk"] = 0 };
            float worstReach = 0f;
            string worstSpecies = null;

            foreach (var (label, hour) in hours)
            {
                float sunElevation = DayNightMath.SunElevation(hour, profile.SunriseHour, profile.SunsetHour);
                Vector2 dir = DayNightMath.ShadowDirection(hour, profile.SunriseHour, profile.SunsetHour,
                                                           profile.ShadowSouthBias, profile.ShadowNoonLift);
                float lenMul = DayNightMath.ShadowLength(sunElevation, LengthAtNoon, LengthAtHorizon,
                                                         MaxLength);
                if (lenMul <= 0f || dir.sqrMagnitude < 1e-6f) continue;
                dir = dir.normalized;

                foreach (var site in sites)
                {
                    if (!bySpecies.TryGetValue(site.Species, out var placement)) continue;

                    float reach = lenMul * (placement.Entry.cellH / (float)ppu);
                    if (reach > worstReach) { worstReach = reach; worstSpecies = site.Species; }

                    if (Terrain().ElevationAt(site.Position + dir * reach) < springHigh) wet[label]++;
                }
            }

            int dawnWet = wet["dawn"], noonWet = wet["noon"], duskWet = wet["dusk"];

            Debug.Log($"[LitDecorCasterBudget] woods vs the waterline over {sites.Count} trees — shadow " +
                      $"TIP lands below spring high ({springHigh} m) on {dawnWet} at dawn, {noonWet} at " +
                      $"noon, {duskWet} at dusk. Longest rake {worstReach:F1} m ({worstSpecies}). The " +
                      "dawn/dusk figures are the shoreline edge case the coordinator asked to see, not a " +
                      "failure: at a low sun a 5–7 m tree genuinely throws 25–30 m, and the shadow sorts " +
                      "above the sea plane.");

            // The bound is a FRACTION, not zero, and deliberately: a single tree standing at the lip of a
            // steep headland can drop below spring high within its own 2 m noon stub, and failing the
            // build over that one tree would be a false red. A tree line that has slipped, or a shadow
            // length that has run away, would put this in the tens of percent, not at one tree.
            int noonBudget = Mathf.Max(1, sites.Count / 50);
            Assert.Less(noonWet, noonBudget,
                $"{noonWet} of {sites.Count} trees lay a NOON shadow on the water. At noon the rake is " +
                $"{LengthAtNoon}× the tree's own height — a stub under the crown — and every tree stands " +
                $"above {StPetersWoods.TreeLineElevation} m with a treeless margin below it. At this many " +
                "it is the tree line, the margin or the shadow length that has moved, not the sun.");
        }

        // =========================================================================================
        //  THE PLAYER
        // =========================================================================================

        /// <summary>
        /// The fisher casts, exactly once, through the host that actually ships —
        /// <see cref="PlayerShadowInstaller.Attach"/>, the same call its <c>Update</c> makes on the
        /// persistent "Player" object. Driving the real method rather than restating the rule is the point:
        /// a guard that re-implements the attachment proves only that the test can attach a component.
        ///
        /// <para><b>Exactly ONE is the assertion, not "at least one".</b> The player is the one caster that
        /// can be reached from two directions at once — a builder line and a self-installing host — and two
        /// SpriteShadows on one fisher is two child renderers drawing the same silhouette at the same
        /// alpha, which reads as one shadow twice as dark and costs twice as much to find out.</para>
        /// </summary>
        [Test]
        public void ThePlayerCastsExactlyOnce_ThroughTheHostThatShips()
        {
            var go = new GameObject(PlayerShadowInstaller.DefaultPlayerObjectName);
            try
            {
                go.AddComponent<SpriteRenderer>();

                // 🔴 NEGATIVE CONTROL — nothing casts until the host runs.
                Assert.IsNull(go.GetComponent<SpriteShadow>(),
                    "The player carried a shadow before the installer ran; this test cannot tell you the " +
                    "installer is what attaches it.");

                var caster = PlayerShadowInstaller.Attach(go);
                Assert.IsNotNull(caster, "The installer refused a player that draws a sprite.");
                Assert.AreEqual(1, go.GetComponents<SpriteShadow>().Length,
                    "The player must carry exactly one caster.");

                // The idempotence that matters: the builder may have got there first, or the host may run
                // twice across a region reload. Neither may stack a second shadow.
                PlayerShadowInstaller.Attach(go);
                PlayerShadowInstaller.Attach(go);
                Assert.AreEqual(1, go.GetComponents<SpriteShadow>().Length,
                    "Re-running the installer stacked a second SpriteShadow — the fisher would draw two " +
                    "silhouettes over each other and pay twice for it.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>
        /// Something that draws nothing casts nothing. Worth a line because the host finds the player BY
        /// NAME, and a name is a weak handle: an object called "Player" that is a pure logic node would
        /// otherwise get a component whose <c>[RequireComponent]</c> would then mint a stray
        /// <see cref="SpriteRenderer"/> on it.
        /// </summary>
        [Test]
        public void TheInstallerRefusesAnythingThatDrawsNothing()
        {
            var go = new GameObject(PlayerShadowInstaller.DefaultPlayerObjectName);
            try
            {
                Assert.IsNull(PlayerShadowInstaller.Attach(go),
                    "The installer attached a caster to an object with no SpriteRenderer.");
                Assert.IsNull(go.GetComponent<SpriteShadow>());
                Assert.IsNull(go.GetComponent<SpriteRenderer>(),
                    "RequireComponent minted a SpriteRenderer on a logic-only object.");
            }
            finally { Object.DestroyImmediate(go); }

            Assert.IsNull(PlayerShadowInstaller.Attach(null),
                "A null player must be a no-op, not a throw — the host polls before the player exists.");
        }

        // =========================================================================================

        /// <summary>The planter's caster rule, mirrored so the tests measure the SAME decision the build
        /// makes. Kept in one place here rather than restated in each test.
        ///
        /// <para>⚠ The height is the one the plant is DRAWN at
        /// (<see cref="ShorePlantDef.PlantedStandingHeightM"/> — after the authored per-species scale),
        /// not the full-growth height the rig baked. A species turned down below the floor stops
        /// casting, which is the whole point of having a floor.</para></summary>
        static bool Casts(ShorePlantDef def) =>
            !def.Algae &&
            def.Zone != StPetersWoodsPlanter.SubtidalZone &&
            def.PlantedStandingHeightM >= StPetersWoodsPlanter.ShadowCasterMinHeightM;

        static Dictionary<string, ShorePlantDef> LoadDefs()
        {
            var defs = new Dictionary<string, ShorePlantDef>();
            foreach (string guid in AssetDatabase.FindAssets(
                         "t:ShorePlantDef", new[] { "Assets/_Project/Data/Decor/ShorePlants" }))
            {
                var def = AssetDatabase.LoadAssetAtPath<ShorePlantDef>(
                    AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) defs[def.SpeciesKey] = def;
            }
            return defs;
        }
    }
}
