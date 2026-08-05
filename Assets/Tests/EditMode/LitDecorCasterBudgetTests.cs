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
            Assert.GreaterOrEqual(eelgrass.StandingHeightM, StPetersWoodsPlanter.ShadowCasterMinHeightM,
                "Eelgrass is now below the height floor, so the height rule alone would exclude it and " +
                "this test no longer demonstrates why the ZONE check exists.");
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

        /// <summary>The planter's caster rule, mirrored so the tests measure the SAME decision the build
        /// makes. Kept in one place here rather than restated in each test.</summary>
        static bool Casts(ShorePlantDef def) =>
            !def.Algae &&
            def.Zone != StPetersWoodsPlanter.SubtidalZone &&
            def.StandingHeightM >= StPetersWoodsPlanter.ShadowCasterMinHeightM;

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
