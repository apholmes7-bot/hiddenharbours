using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐ <b>THE SAIL-IN</b> — a region is toggled on the way travel actually toggles one, and its water has
    /// to end up drawing that region's own seabed.
    ///
    /// <para><b>The defect.</b> A region's height source registers itself into
    /// <c>GameServices.TidalTerrain</c> in <c>OnEnable</c>; the region's water BAKES that terrain into its
    /// height map in <c>OnEnable</c>. Under the persistent-core travel model a region's roots are switched
    /// on one at a time, in hierarchy order — so which of those two ran first was decided by where two
    /// objects happened to sit in the Hierarchy window. Bake first and the water samples a terrain that is
    /// not there, silently falls back to its distance-to-land estimate, and in a region with no land
    /// tilemap, collider mask or shore fence authored that estimate is a CONSTANT. A flat sea: no coast
    /// reveal, no depth gradient, no foam following the shore. Reported, correctly, as "the water does not
    /// render correctly".</para>
    ///
    /// <para><b>The order here is the BROKEN one, deliberately.</b> The Sea root is created before the
    /// terrain root, which is precisely what the committed St Peters scene carries (Sea at root 0,
    /// TidalTerrain at root 12). A fixture that used the intended order would pass with the fix removed and
    /// prove nothing. (Sabotage-checked: delete the
    /// <c>GameServices.TidalTerrainChanged</c> subscription in <c>WaterSurface.OnEnable</c> and
    /// <see cref="AfterAReturnTrip_TheWaterDrawsTheRegionsOwnSeabed"/> goes red on the flat field.)</para>
    ///
    /// <para><b>Why PlayMode and not EditMode.</b> Nothing registers in edit mode — the terrains are not
    /// <c>[ExecuteAlways]</c> — so the whole enable/register/bake sequence only exists when something drives
    /// it. <c>WaterSeabedBakeOrderTests</c> pins the component's re-bake in isolation; this pins it through
    /// a real <see cref="RegionTravelCoordinator"/> switching real scene roots.</para>
    ///
    /// <para><b>The fixtures are a MODEL of a region's shape, not any region's numbers</b> — a PlayMode
    /// assembly cannot reference the editor assembly the region plans live in, so the metres here are
    /// deliberately arbitrary (the <c>WestWaterSailPlayTests</c> convention). Nine Mile Creek's real extent,
    /// bake range and measured shore gradient are asserted in <c>NineMileCreekWaterParityTests</c>, where
    /// the editor assembly is available. What this file proves is the RIG.</para>
    ///
    /// <para><b>No graphics device needed and none used</b> — nothing here renders; the assertions read the
    /// baked bytes on the CPU. <b>Two scenes, not one</b>: the coordinator deactivates the roots of the
    /// scene it leaves, and travelling out of the runner's own scene would switch the runner off mid-assert.
    /// <b>No frame-count-as-time assertions</b> — the <c>yield return null</c>s let scene events flush.</para>
    /// </summary>
    public class RegionWaterBakePlayTests
    {
        const string HarbourSceneName = "RegionWaterBake_Harbour";
        const string CreekSceneName   = "RegionWaterBake_Creek";

        // Model geometry, inside the WaterSurface component's own default bake rect (160 × 120 m centred on
        // the origin at −4…6 m) so the fixture needs no editor-only configuration to frame it.
        static readonly Vector2 ShoalCentre     = new Vector2(0f, 0f);
        static readonly Vector2 ShoalHalfSize   = new Vector2(20f, 15f);
        const float ShoalElevation = 1.5f;      // bares and floods — the harbour shoal
        const float BayFloorElevation = -4f;    // the open bay, at the bake range's floor
        static readonly Vector2 OutInTheBay = new Vector2(70f, 50f);   // clear of the shoal and its falloff

        /// <summary>Two quantisation steps of the R8 bake over its 10 m range — the claim is "these are
        /// different ground", never "the byte round-tripped".</summary>
        const float Quantisation = (6f - BayFloorElevation) / 255f * 2f;

        Scene _origin, _harbour, _creek;
        RegionDef _creekRegion;
        GameObject _coordinatorGo;
        WaterSurface _creekWater;
        RectTidalTerrain _creekTerrain;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();

            _origin = SceneManager.GetActiveScene();
            _harbour = SceneManager.CreateScene(HarbourSceneName);
            _creek = SceneManager.CreateScene(CreekSceneName);

            _creekRegion = ScriptableObject.CreateInstance<RegionDef>();
            _creekRegion.Id = "region.water_bake_fixture";
            _creekRegion.SceneName = CreekSceneName;

            // ⚠ THE SEA FIRST, THE TERRAIN SECOND. This is the adversarial order — the one the shipped St
            // Peters scene carries and the one every builder comment warns against — because a fixture in
            // the intended order cannot fail.
            var seaGo = new GameObject("Sea", typeof(MeshRenderer), typeof(WaterSurface));
            SceneManager.MoveGameObjectToScene(seaGo, _creek);
            _creekWater = seaGo.GetComponent<WaterSurface>();

            // ⚠ CONFIGURE BEFORE ENABLE — the builders' convention, and load-bearing here for a reason
            // worth writing down. A terrain registers itself in OnEnable, and since the ordering fix that
            // registration is what makes the water bake. Standing one up already-active and configuring it
            // afterwards therefore bakes the DEFAULTS: an empty zone list over a −4 m floor, which reads as
            // −4 m at the shoal and looks exactly like the bug under test. (Measured, on CI's first run of
            // this fixture: "Expected 1.5 ± 0.078, but was −4.0".) The real builders cannot hit this — they
            // configure before the scene is ever saved, so a shipped terrain is always configured by the
            // time anything enables it — but a fixture that builds its region live can, and did.
            var terrainGo = new GameObject("TidalTerrain");
            terrainGo.SetActive(false);
            SceneManager.MoveGameObjectToScene(terrainGo, _creek);
            _creekTerrain = terrainGo.AddComponent<RectTidalTerrain>();
            _creekTerrain.Configure(BayFloorElevation, new[]
            {
                new RectTidalTerrain.LandZone
                {
                    Center = ShoalCentre, HalfSize = ShoalHalfSize,
                    Elevation = ShoalElevation, Falloff = 12f,
                },
            });
            terrainGo.SetActive(true);

            // The region's binding point, authored the way a builder authors one.
            var anchorGo = new GameObject("CreekRegionAnchor");
            anchorGo.SetActive(false);                    // configure BEFORE enable, like the builder
            SceneManager.MoveGameObjectToScene(anchorGo, _creek);
            var anchor = anchorGo.AddComponent<RegionAnchor>();
            var arrival = new GameObject("Arrival");
            arrival.transform.SetParent(anchorGo.transform, worldPositionStays: false);
            anchor.Configure(_creekRegion.Id, arrival.transform, dockZone: null, disembarkPoint: null);
            anchor.ConfigureExtent(Vector2.zero, new Vector2(160f, 120f));   // the sea plane's own rect
            anchorGo.SetActive(true);

            // Stand in the HARBOUR before the coordinator starts listening, so its first event is the
            // crossing under test rather than our own setup.
            SceneManager.SetActiveScene(_harbour);

            // ⚠ THE COORDINATOR LIVES OUTSIDE BOTH REGION SCENES, exactly as the real one does — it rides
            // the persistent core and is DontDestroyOnLoad. Left in a region scene it deactivates ITSELF on
            // the first crossing (its own handler switches off the roots of the scene it is leaving, and it
            // is one of them), OnDisable unsubscribes, and every crossing after the first is silently
            // unhandled: the region you left never switches off, so its terrain never relinquishes.
            // (Measured, on CI's first run of this fixture: "Expected null, but was <TidalTerrain>".) A
            // fixture that travels ONCE cannot see this — which is why the older travel fixtures, whose
            // coordinators do sit in a region scene, have never had to care.
            _coordinatorGo = new GameObject("RegionTravelCoordinator");
            _coordinatorGo.SetActive(false);
            SceneManager.MoveGameObjectToScene(_coordinatorGo, _origin);
            var coordinator = _coordinatorGo.AddComponent<RegionTravelCoordinator>();
            coordinator.Configure(null, null, null, null);
            _coordinatorGo.SetActive(true);               // OnEnable subscribes
        }

        [UnityTearDown]
        public IEnumerator TearDownScenes()
        {
            // Stop listening BEFORE the scenes come apart — a coordinator still subscribed would react to
            // the teardown's own active-scene change and toggle roots that are being destroyed.
            if (_coordinatorGo != null) Object.DestroyImmediate(_coordinatorGo);
            if (_creekRegion != null) Object.DestroyImmediate(_creekRegion);

            if (_origin.IsValid() && _origin.isLoaded) SceneManager.SetActiveScene(_origin);
            // ⚠ PUT THE WORLD BACK. A PlayMode run shares one player loop: a resident region hands every
            // test that follows a tide and a seabed it never asked for, and reds a different file.
            if (_creek.IsValid() && _creek.isLoaded) yield return SceneManager.UnloadSceneAsync(_creek);
            if (_harbour.IsValid() && _harbour.isLoaded) yield return SceneManager.UnloadSceneAsync(_harbour);

            GameServices.Reset();
        }

        /// <summary>Cross into a region the way the loader does: make its scene active, which is what raises
        /// <c>activeSceneChanged</c> for the coordinator to act on.</summary>
        IEnumerator TravelTo(Scene scene)
        {
            SceneManager.SetActiveScene(scene);
            yield return null;      // let the scene event flush — NOT a measurement of time
        }

        void AssertTheSeabedIsLive(string when)
        {
            Assert.AreSame(_creekTerrain, GameServices.TidalTerrain,
                $"{when}: the region's own terrain must be the registered height source.");

            Assert.AreSame(_creekTerrain, _creekWater.BakedTerrain,
                $"{when}: the water must be drawing the region's terrain. Null here means it baked before " +
                "the terrain registered and fell back to the distance-to-land estimate — which, with no " +
                "land sources authored, is a FLAT seabed over the whole region.");

            Assert.IsTrue(_creekWater.TryReadBakedElevation(ShoalCentre, out float shoal),
                          $"{when}: there must be a bake to read.");
            Assert.IsTrue(_creekWater.TryReadBakedElevation(OutInTheBay, out float bay));

            Assert.That(shoal, Is.EqualTo(ShoalElevation).Within(Quantisation),
                        $"{when}: the shoal must read at the height the terrain says.");
            Assert.That(bay, Is.EqualTo(BayFloorElevation).Within(Quantisation),
                        $"{when}: the open bay must read as the deep floor.");
            Assert.Greater(shoal - bay, 1f,
                $"{when}: shoal and bay must differ by real metres. A FLAT bake satisfies nothing here, " +
                "and a flat bake is exactly what the owner was sailing into.");
        }

        // =============================================================================================

        [UnityTest]
        public IEnumerator OnTheFirstArrival_TheWaterDrawsTheRegionsOwnSeabed()
        {
            yield return TravelTo(_creek);
            AssertTheSeabedIsLive("first arrival");
        }

        /// <summary>
        /// ⭐ THE ONE THAT MATTERS — the return trip, which is the shape of every visit after the first.
        ///
        /// <para>Leaving deactivates the region's roots, so its terrain relinquishes the accessor and its
        /// water throws its bake away. Coming back switches those roots on one at a time, Sea first, and
        /// this is the moment the bake used to be taken against nothing.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator AfterAReturnTrip_TheWaterDrawsTheRegionsOwnSeabed()
        {
            yield return TravelTo(_creek);
            yield return TravelTo(_harbour);

            // Sanity on the leaving half: the region really did go away, so what follows is a genuine
            // re-activation rather than a scene that never switched off.
            Assert.IsNull(GameServices.TidalTerrain,
                "leaving a region must relinquish its height source — otherwise this test is not " +
                "exercising a re-activation at all.");

            yield return TravelTo(_creek);
            AssertTheSeabedIsLive("return trip");
        }

        [UnityTest]
        public IEnumerator AcrossSeveralCrossings_TheSeabedStaysTheRegionsOwn()
        {
            // The owner does not sail once. Three round trips, because a fix that leaks a subscription or
            // caches the wrong terrain survives one crossing and fails on the third.
            for (int i = 0; i < 3; i++)
            {
                yield return TravelTo(_creek);
                AssertTheSeabedIsLive($"crossing {i + 1}");
                yield return TravelTo(_harbour);
            }
        }
    }
}
