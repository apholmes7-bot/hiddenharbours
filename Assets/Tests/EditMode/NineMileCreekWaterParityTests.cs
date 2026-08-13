#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>Nine Mile Creek and St Peters must be the same sea.</b>
    ///
    /// <para><b>What went wrong.</b> The displaced water surface (ADR 0023 phase 2) has been the game's
    /// water since the 2026-08-05 retirement of the flat face, and the wiring that gives a region one lived
    /// in exactly ONE place: <c>StPetersBuilder</c>. Nine Mile Creek's builder carried a hand-rolled COPY of
    /// the surrounding water block and that copy was never extended to match — so the banked creek scene has
    /// a <c>WaterSurface</c> and no <c>DisplacedWaterSurface</c> (checked in the scene YAML, not assumed),
    /// and the owner sailed out of one sea and into a different one. That is not a scene defect to be
    /// hand-patched: <b>scene-wired is not builder-wired</b> (#323), and a scene fixed by hand survives
    /// exactly until the next Build click.</para>
    ///
    /// <para>So the wiring now has one home — <see cref="WaterSceneTemplate.ConfigureLandRegionWater"/> —
    /// and these assert the two things a copy could break again: that the shared call produces the whole
    /// stack, and that both builders actually make it.</para>
    ///
    /// <para>Pure editor-object construction: no scene is loaded and none is written.</para>
    /// </summary>
    public class NineMileCreekWaterParityTests
    {
        const string NineMileCreekBuilderPath = "Assets/_Project/Code/App/Editor/NineMileCreekBuilder.cs";
        const string StPetersBuilderPath      = "Assets/_Project/Code/App/Editor/StPetersBuilder.cs";

        GameObject _sea;
        float _creekGradient;

        /// <summary>
        /// Measure the creek's shore BEFORE any sea exists, deliberately.
        ///
        /// <para>Probing it means standing a <c>MainlandTidalTerrain</c> up, and a terrain coming alive
        /// registers itself into <c>GameServices.TidalTerrain</c> — which, since the ordering fix, makes
        /// every live <c>WaterSurface</c> re-bake. Doing it first keeps the measurement from quietly
        /// re-baking the fixture's own sea mid-test.</para>
        /// </summary>
        [SetUp]
        public void SetUp() => _creekGradient = NineMileCreekMaxShoreGradient();

        [TearDown]
        public void TearDown()
        {
            if (_sea != null) Object.DestroyImmediate(_sea);
            _sea = null;
            GameServices.TidalTerrain = null;   // static service: leave nothing for the next fixture
        }

        /// <summary>The shared call, with the creek's own numbers — the exact arguments the builder passes.</summary>
        void WireTheCreeksSea(GameObject sea) =>
            WaterSceneTemplate.ConfigureLandRegionWater(
                sea, NineMileCreekBuilder.NineMileCreekSeaCenter, NineMileCreekBuilder.NineMileCreekSeaSize,
                NineMileCreekBuilder.NineMileCreekHeightResolution,
                NineMileCreekBuilder.NineMileCreekHeightMin, NineMileCreekBuilder.NineMileCreekHeightMax,
                _creekGradient);

        /// <summary>A Sea object shaped like the one both builders make: a renderer to satisfy
        /// <c>[RequireComponent]</c>, and the flat <see cref="WaterSurface"/> already on it (the shared call
        /// configures a surface, it does not invent one).</summary>
        GameObject StandUpASea()
        {
            _sea = new GameObject("Sea", typeof(MeshRenderer), typeof(WaterSurface));
            return _sea;
        }

        // =============================================================================================
        //  1. The shared call produces the WHOLE stack
        // =============================================================================================

        [Test]
        public void TheSharedWiring_GivesTheRegionADisplacedSurfaceOverTheSameRectAsItsFlatOne()
        {
            var sea = StandUpASea();

            WireTheCreeksSea(sea);

            var displaced = sea.GetComponent<DisplacedWaterSurface>();
            Assert.IsNotNull(displaced,
                "the creek's Sea must carry the displaced surface. Without it the region draws the " +
                "pre-displacement flat face while every other region draws the mesh — which is the defect " +
                "the owner reported as 'the water does not render correctly'.");

            // The SAME rect as the flat plane and the height bake. A displaced mesh over a different
            // rectangle is a sea with two different edges.
            Assert.AreEqual(NineMileCreekBuilder.NineMileCreekSeaCenter, displaced.MeshWorldCenter);
            Assert.AreEqual(NineMileCreekBuilder.NineMileCreekSeaSize, displaced.MeshWorldSize);
        }

        [Test]
        public void TheSharedWiring_ReachesTheOwnersGameConfig_OnBOTHSurfaces()
        {
            var sea = StandUpASea();

            WireTheCreeksSea(sea);

            // ⚠ Both banked land scenes carry `_config: {fileID: 0}` on their WaterSurface — the reference
            // the builders assigned did not survive, so the owner's salience knobs have never reached the
            // flat surface in either region. The shared call loads the asset at the moment of writing
            // instead of trusting one the caller loaded pages earlier; this is what says so.
            var so = new UnityEditor.SerializedObject(sea.GetComponent<WaterSurface>());
            Assert.IsNotNull(so.FindProperty("_config").objectReferenceValue,
                "the flat WaterSurface must end up holding the shared GameConfig — it is the one asset " +
                "where the owner tunes how loudly the big wave is marked, and a null here is silent.");

            var dso = new UnityEditor.SerializedObject(sea.GetComponent<DisplacedWaterSurface>());
            Assert.IsNotNull(dso.FindProperty("_config").objectReferenceValue,
                "and so must the displaced one — its exaggeration and shore-band coefficient are read " +
                "live from that block every tick.");
        }

        [Test]
        public void TheSharedWiring_TurnsTheWeatherPaletteOn_AndLeavesTheBaseAnchorUnwiredOnPurpose()
        {
            var sea = StandUpASea();

            WireTheCreeksSea(sea);

            var so = new UnityEditor.SerializedObject(sea.GetComponent<WaterSurface>());
            Assert.IsTrue(so.FindProperty("_weatherPaletteEnabled").boolValue,
                "P1: the sea has moods in every region, not just the first one.");
            Assert.IsNull(so.FindProperty("_baseMoodMaterial").objectReferenceValue,
                "the BASE anchor stays UNWIRED on purpose (ADR 0017) — the calm sea is then the owner's " +
                "own live Water.mat rather than a frozen preset copy of it, so his tuning always shows.");
        }

        [Test]
        public void RunningTheWiringTwice_DoesNotHangASecondDisplacedSurfaceOnTheSea()
        {
            var sea = StandUpASea();
            for (int i = 0; i < 2; i++) WireTheCreeksSea(sea);

            Assert.AreEqual(1, sea.GetComponents<DisplacedWaterSurface>().Length,
                "the owner clicks Build more than once. DisplacedWaterSurface is " +
                "[DisallowMultipleComponent], so a blind AddComponent on the second run would throw " +
                "rather than quietly duplicate — either way the run is spoiled.");
        }

        // =============================================================================================
        //  2. The shore gradient is this COAST's, not the island's
        // =============================================================================================

        [Test]
        public void TheCreeksShoreGradient_IsMeasuredOffItsOwnCoastPlan_NotInheritedFromTheIsland()
        {
            float creek = _creekGradient;

            // Nine Mile Creek's plan stands a DEEP-SHORE CLIFF at the spit's root: the plateau falls from
            // +6 m to the −6 m bay floor across a 3 m plunge. A smoothstep's gradient peaks at 1.5× its
            // mean, so the steepest metre of this coast is 6.0 — twelve times the island's softly-shelving
            // 0.5, which is the value that would have been inherited by copying St Peters' wiring.
            Assert.Greater(creek, StPetersBuilder.ShoreGradient * 4f,
                "a mainland that stands cliffs off the bay floor does not shelve like a beach. Sizing the " +
                "displaced sea's fade band for a beach here is the direction of the error that TEARS " +
                "(the field's own tooltip: overestimating only widens the band).");

            var sea = StandUpASea();
            WireTheCreeksSea(sea);

            Assert.That(sea.GetComponent<DisplacedWaterSurface>().MaxShoreGradient,
                        Is.EqualTo(creek).Within(1e-4f),
                        "and the measured number must actually reach the component.");
        }

        [Test]
        public void ACoastPlanThatDeclaresNoCliffs_IsNotGivenACliffsGradient()
        {
            // The plan is the single source of what each stretch of shore IS. A coast of nothing but beach
            // must not inherit a cliff's steepness because the cliff profile happens to exist in the file.
            var go = new GameObject("SoftCoast", typeof(MainlandTidalTerrain));
            try
            {
                var soft = go.GetComponent<MainlandTidalTerrain>();
                NineMileCreekBuilder.ConfigureNineMileCreekTerrain(soft);
                soft.CoastSectors = new[] { new CoastRunSector(0f, CoastClass.Beach) };

                Assert.That(soft.MaxShoreGradient(),
                            Is.EqualTo(soft.ClassGradient(CoastClass.Beach)).Within(1e-4f),
                            "with only beach authored, the beach's own gradient is the answer.");
                Assert.Less(soft.MaxShoreGradient(), soft.ClassGradient(CoastClass.DeepShoreCliff),
                            "and it must be gentler than the cliff class it does not use.");
            }
            finally { Object.DestroyImmediate(go); }
        }

        // =============================================================================================
        //  3. Both builders actually MAKE the call
        // =============================================================================================

        [Test]
        public void BothLandBuilders_WireTheirSeaThroughTheOneSharedCall()
        {
            // Source-text, on the DisplacedDefaultTests precedent, because the alternative is running a
            // builder — and a builder replaces the open scene. This is the assertion that stops a third
            // hand-rolled copy from being written the next time somebody needs a region's sea: a copy
            // passes every other test in this file, because every other test calls the shared helper
            // directly.
            foreach (string path in new[] { NineMileCreekBuilderPath, StPetersBuilderPath })
            {
                Assert.IsTrue(File.Exists(path), $"{path} not found");
                string src = File.ReadAllText(path);
                Assert.IsTrue(src.IndexOf("WaterSceneTemplate.ConfigureLandRegionWater", StringComparison.Ordinal) >= 0,
                    $"{Path.GetFileName(path)} must wire its Sea through " +
                    "WaterSceneTemplate.ConfigureLandRegionWater. A local copy of that block is how Nine " +
                    "Mile Creek came to be missing the displaced surface at all.");
                Assert.IsFalse(src.IndexOf("AddComponent<HiddenHarbours.Art.DisplacedWaterSurface>",
                                           StringComparison.Ordinal) >= 0,
                    $"{Path.GetFileName(path)} must not hang the displaced surface itself — that is the " +
                    "shared call's job, and a second place that does it is a second place to forget.");
            }
        }

        // ---- helpers ---------------------------------------------------------------------------------

        /// <summary>The creek's steepest shore, measured off the coast plan the builder authors — the same
        /// call the builder makes, so this cannot assert a number the region does not have.</summary>
        static float NineMileCreekMaxShoreGradient()
        {
            var go = new GameObject("NineMileCreekTerrain_Probe", typeof(MainlandTidalTerrain));
            try
            {
                var terrain = go.GetComponent<MainlandTidalTerrain>();
                NineMileCreekBuilder.ConfigureNineMileCreekTerrain(terrain);
                return terrain.MaxShoreGradient();
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
#endif
