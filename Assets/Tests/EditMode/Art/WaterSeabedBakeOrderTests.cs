using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>The sea must draw the region's real bottom no matter which order the region woke up in.</b>
    ///
    /// <para><b>The defect these exist for.</b> A region's terrain registers itself into
    /// <see cref="GameServices.TidalTerrain"/> in <c>OnEnable</c>, and the region's water bakes that terrain
    /// into its height map in <c>OnEnable</c>. Under the root-toggle travel model a region's roots are
    /// switched on one at a time in hierarchy order, so which of those two runs first was decided by nothing
    /// more durable than where two objects happen to sit in the Hierarchy window. Bake first and the water
    /// samples a terrain that is not there, silently falls back to its distance-to-land estimate, and — in a
    /// region that authors no land tilemap, collider mask or shore fence, which is every analytic region —
    /// that estimate is a CONSTANT. A flat seabed: no coast reveal, no depth gradient, no foam following the
    /// shore. It fails silently and it looks like a rendering bug, which is exactly how it was reported.</para>
    ///
    /// <para><b>It was not hypothetical.</b> The committed St Peters scene carries its Sea at root index 0
    /// and its TidalTerrain at root index 12 — the order the builders' own comments say must not happen.
    /// Three separate builders restate "create the terrain BEFORE the sea" as load-bearing, which is the
    /// tell: a rule that has to be repeated everywhere is enforced nowhere.</para>
    ///
    /// <para><b>What is pinned here.</b> That a LATE registration is picked up
    /// (<see cref="GameServices.TidalTerrainChanged"/> → re-bake), that the re-bake costs nothing in the
    /// order that was already right, and that a surface which has gone away stops listening. Pure component
    /// construction — no scene, no graphics device, so this runs on CI.</para>
    /// </summary>
    public class WaterSeabedBakeOrderTests
    {
        /// <summary>A seabed with SHAPE — a shoal at the origin falling away to a deep floor. The shape is
        /// the whole point: a flat bake and a real bake are indistinguishable over flat ground, so the
        /// double has to have somewhere shallow and somewhere deep to tell them apart.</summary>
        private sealed class ShoalTerrain : ITidalTerrain
        {
            public readonly float ShoalElevation = 1.5f;
            public readonly float FloorElevation = -6f;
            /// <summary>Half-width (m) of the flat shoal at the origin.</summary>
            public readonly float ShoalHalfWidth = 30f;

            public int Samples;   // proves the bake actually re-sampled rather than reusing a texture

            public float ElevationAt(Vector2 worldPos)
            {
                Samples++;
                return Mathf.Abs(worldPos.x) <= ShoalHalfWidth && Mathf.Abs(worldPos.y) <= ShoalHalfWidth
                    ? ShoalElevation
                    : FloorElevation;
            }
        }

        private static readonly Vector2 OnTheShoal = new Vector2(0f, 0f);
        private static readonly Vector2 OutInTheBay = new Vector2(300f, 200f);

        private GameObject _go;
        private WaterSurface _surface;

        [SetUp]
        public void SetUp()
        {
            // A clean accessor per test: this is a STATIC service and a leftover registration from a
            // neighbouring fixture would decide the first bake for us.
            GameServices.TidalTerrain = null;
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _surface = null;
            GameServices.TidalTerrain = null;
        }

        /// <summary>
        /// Stand a sea up the way a region does, over a 760 × 560 m rect with an elevation range that
        /// brackets the double's field.
        ///
        /// <para><see cref="WaterSurface"/> is <c>[RequireComponent(typeof(Renderer))]</c> and Renderer is
        /// abstract, so the concrete MeshRenderer goes on at construction — the same fixture shape
        /// <c>StPetersShorelineRenderTests</c> uses. It is <c>[ExecuteAlways]</c>, so adding it HERE is what
        /// fires OnEnable and takes the first bake; that is not a side effect of the test, it is the moment
        /// under test.</para>
        /// </summary>
        private void StandUpTheSea()
        {
            _go = new GameObject("Sea_BakeOrderTest", typeof(MeshRenderer), typeof(WaterSurface));
            _surface = _go.GetComponent<WaterSurface>();

            // Nine Mile Creek's own frame, so the numbers below are the region's rather than a default's.
            // Written straight through SerializedObject rather than through the builder's helper: this
            // fixture lives in the ART test assembly, which deliberately does not reference App.Editor —
            // the mechanism under test is the component's, not any builder's.
            var so = new SerializedObject(_surface);
            so.FindProperty("_heightWorldCenter").vector2Value = Vector2.zero;
            so.FindProperty("_heightWorldSize").vector2Value = new Vector2(760f, 560f);
            so.FindProperty("_heightResolution").intValue = 96;
            so.FindProperty("_heightMin").floatValue = -6f;
            so.FindProperty("_heightMax").floatValue = 6f;
            so.ApplyModifiedPropertiesWithoutUndo();

            // The rect/range only reach the bake through a re-bake, and OnEnable's already happened — so
            // re-enable to take a first bake against the frame this test actually cares about.
            _go.SetActive(false);
            _go.SetActive(true);
        }

        // =============================================================================================
        //  THE FIX: a terrain that registers LATE is still the seabed the water draws
        // =============================================================================================

        [Test]
        public void ASeaThatWokeBeforeItsTerrain_RebakesWhenTheTerrainRegisters()
        {
            StandUpTheSea();

            // (1) The broken order, reproduced: the water is already up and nothing is registered.
            Assert.IsNull(_surface.BakedTerrain,
                "with no terrain registered the sea must know it is drawing the FALLBACK — a bake that " +
                "claimed a terrain here would be claiming one that does not exist.");

            // (2) The terrain arrives — the region's own root finally switching on.
            var terrain = new ShoalTerrain();
            GameServices.TidalTerrain = terrain;

            // (3) …and the sea is now drawing IT, without anything re-enabling the surface.
            Assert.AreSame(terrain, _surface.BakedTerrain,
                "a terrain that registered after the water woke must still become the water's seabed. If " +
                "this is null the re-bake on GameServices.TidalTerrainChanged is gone, and every region " +
                "whose Sea sorts above its TidalTerrain is drawing a flat sea again.");
            Assert.Greater(terrain.Samples, 0, "the re-bake must actually sample the terrain.");
        }

        /// <summary>
        /// ⚠ THE DEFECT ITSELF, asserted rather than assumed — and the reason a late registration is worth
        /// any of this. With no land tilemap, collider mask or shore fence authored (every analytic region),
        /// every cell's distance-to-nearest-land is infinite, the depth curve saturates at <c>_maxDepth</c>,
        /// and the "seabed" is ONE NUMBER over the whole region. That is what the owner was sailing into.
        ///
        /// <para>Kept in its own test on purpose: the fallback resolves land sources by searching the open
        /// scene, so a stray <c>Tilemap</c> left behind by a neighbouring fixture would give it a gradient
        /// and red this — without saying anything about the ordering fix, which the two tests either side
        /// pin without touching the fallback's shape.</para>
        /// </summary>
        [Test]
        public void TheFallbackBake_IsFlat_WhichIsWhyALateRegistrationMatters()
        {
            StandUpTheSea();

            Assert.IsTrue(_surface.TryReadBakedElevation(OnTheShoal, out float flatShoal));
            Assert.IsTrue(_surface.TryReadBakedElevation(OutInTheBay, out float flatBay));
            Assert.That(flatShoal, Is.EqualTo(flatBay).Within(0.05f),
                "the distance-to-land fallback over a region with no land sources is a CONSTANT — no coast " +
                "reveal, no depth gradient, no foam following the shore. (A stray Tilemap in the open test " +
                "scene would also fail this; see the doc note.)");
        }

        [Test]
        public void TheRebakedField_HasTheTerrainsSHAPE_NotAFlatOne()
        {
            StandUpTheSea();
            var terrain = new ShoalTerrain();
            GameServices.TidalTerrain = terrain;

            Assert.IsTrue(_surface.TryReadBakedElevation(OnTheShoal, out float shoal),
                "there must be a bake to read.");
            Assert.IsTrue(_surface.TryReadBakedElevation(OutInTheBay, out float bay));

            // The acceptance the owner's report reduces to: the shoal is shallower than the bay, in the
            // texture the shader samples. Tolerance is one quantisation step of the 12 m range over 8 bits
            // (~4.7 cm), doubled — the claim is "these are different ground", not "the byte round-tripped".
            const float Quantisation = (6f - -6f) / 255f * 2f;
            Assert.That(shoal, Is.EqualTo(terrain.ShoalElevation).Within(Quantisation),
                "the wharf shoal must read as the terrain says it is.");
            Assert.That(bay, Is.EqualTo(terrain.FloorElevation).Within(Quantisation),
                "the open bay must read as the deep floor.");
            Assert.Greater(shoal - bay, 1f,
                "shoal and bay must differ by real metres — a FLAT field passes every other assertion in " +
                "this file and fails this one, which is the point of it.");
        }

        [Test]
        public void ASeaThatWokeAFTERItsTerrain_TakesTheTerrainOnTheFirstBake()
        {
            // The order the builders intend. Nothing here needs the fix — it is here so the fix cannot be
            // "corrected" into breaking the case that already worked.
            var terrain = new ShoalTerrain();
            GameServices.TidalTerrain = terrain;

            StandUpTheSea();

            Assert.AreSame(terrain, _surface.BakedTerrain,
                "the ordinary order must still bake the terrain on enable.");
        }

        // =============================================================================================
        //  THE COST: rule 7 — a re-bake is a full grid of ElevationAt calls, so it happens once
        // =============================================================================================

        [Test]
        public void TheSameTerrainReRegistering_DoesNotRebake()
        {
            var terrain = new ShoalTerrain();
            GameServices.TidalTerrain = terrain;
            StandUpTheSea();

            int afterFirstBake = terrain.Samples;
            Assert.Greater(afterFirstBake, 0);

            GameServices.TidalTerrain = null;        // the teardown half of a hop
            GameServices.TidalTerrain = terrain;     // …and the same region back again

            Assert.AreEqual(afterFirstBake, terrain.Samples,
                "re-registering the terrain the sea is ALREADY drawing must cost nothing. A 96² grid is " +
                "9,216 ElevationAt calls and a region hop would pay it twice for no change at all.");
        }

        [Test]
        public void ClearingTheAccessor_KeepsTheLastGoodBakeRatherThanReBakingTheFallback()
        {
            var terrain = new ShoalTerrain();
            GameServices.TidalTerrain = terrain;
            StandUpTheSea();

            // Travel clears the accessor when the region being LEFT switches its roots off, and only then
            // does the arriving region register. Re-baking in that gap would spend a full grid producing
            // the fallback one frame before throwing it away.
            GameServices.TidalTerrain = null;

            Assert.AreSame(terrain, _surface.BakedTerrain,
                "a null registration is a teardown, not a statement that this region has no seabed.");
            Assert.IsTrue(_surface.TryReadBakedElevation(OnTheShoal, out float shoal));
            Assert.That(shoal, Is.EqualTo(terrain.ShoalElevation).Within(0.1f),
                "and the last good bake must still be what the shader reads.");
        }

        // =============================================================================================
        //  THE LEAK: a static event outlives the objects that subscribe to it
        // =============================================================================================

        [Test]
        public void ASurfaceThatHasGoneAway_StopsListening()
        {
            StandUpTheSea();
            Object.DestroyImmediate(_go);
            _go = null;

            // A surface still subscribed after destruction re-bakes into a destroyed renderer — which
            // throws, on a hop, in the middle of somebody else's region. (Sabotage-checked: deleting the
            // unsubscribe in WaterSurface.OnDisable turns this red.)
            Assert.DoesNotThrow(() => GameServices.TidalTerrain = new ShoalTerrain(),
                "a destroyed WaterSurface must have unhooked itself from GameServices.TidalTerrainChanged.");
        }

        [Test]
        public void ADisabledSurface_DoesNotRebake()
        {
            StandUpTheSea();
            _go.SetActive(false);

            var terrain = new ShoalTerrain();
            GameServices.TidalTerrain = terrain;

            Assert.AreEqual(0, terrain.Samples,
                "a region that is toggled OFF must not bake anything — its own OnEnable does that when it " +
                "comes back, and a region hop enables one region while several others sit switched off.");
        }
    }
}
