using System;
using System.Linq;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Fishing;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class CumberlandRegionTests
    {
        const string Scenes = "Assets/_Project/Scenes/";
        string _proofSuffix;
        string ProofName(string stem) => stem + "_" + _proofSuffix;
        string ProofSource => Scenes + ProofName("CumberlandRefreshSource") + ".unity";
        string ProofCopy => Scenes + ProofName("CumberlandRefreshCopy") + ".unity";
        string ProofFolder => Scenes + ProofName("CumberlandRefreshProof");
        string CreateProofScene => Scenes + ProofName("CumberlandCreateProof") + ".unity";
        string CreateProofFolder => Scenes + ProofName("CumberlandCreatePlan");
        EditorBuildSettingsScene[] _buildList;
        SceneSetup[] _sceneSetup;
        LandScenePlanDef _proofPlan;

        [SetUp]
        public void UseFreshProofPaths() => _proofSuffix = Guid.NewGuid().ToString("N");

        [TearDown]
        public void RestoreEditorAfterDisposableProof()
        {
            if (_sceneSetup == null) return;
            var setup = _sceneSetup;
            _sceneSetup = null;
            var buildList = _buildList;
            _proofPlan = null;
            _buildList = null;
            Action[] cleanup = {
                () => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single),
                () => EditorBuildSettings.scenes = buildList,
                // Retain the proof files for inspection. This fixture never deletes project files.
                () => { if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
            };
            // Each remaining step runs even when an earlier cleanup throws. Reset fixture state
            // before any scene operation, so a failure cannot repeat in the following test.
            void RunCleanup(int index)
            {
                if (index == cleanup.Length) return;
                try { cleanup[index](); }
                finally { RunCleanup(index + 1); }
            }
            RunCleanup(0);
        }

        [Test]
        public void CumberlandRefresh_DisposableSceneCopy_PreservesMovedOwnerVisual()
        {
            Assert.That(System.IO.File.Exists(ProofSource) || System.IO.File.Exists(ProofCopy)
                || System.IO.Directory.Exists(ProofFolder), Is.False,
                "A previous disposable proof must be inspected before reuse.");
            _sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            _buildList = EditorBuildSettings.scenes;
            StartFromAnEmptyScene();
            Assert.That(AssetDatabase.CreateFolder("Assets/_Project/Scenes", ProofName("CumberlandRefreshProof")), Is.Not.Empty);
            _proofPlan = SaveProofPlan(ProofFolder, ProofName("CumberlandRefreshSource"));
            LandSceneTemplate.Create(_proofPlan);
            Assert.That(System.IO.File.Exists(ProofSource), Is.True, "CREATE returned without saving its scene.");
            Assert.That(AssetDatabase.CopyAsset(ProofSource, ProofCopy), Is.True);
            _proofPlan.Region.SceneName = ProofName("CumberlandRefreshCopy");
            var copy = EditorSceneManager.OpenScene(ProofCopy, OpenSceneMode.Single);
            var visual = copy.GetRootGameObjects().Single(g => g.name == "LandRegion")
                .transform.Find("Visuals/ReviewCamera");
            var moved = visual.position + new Vector3(7, -3, 0);
            visual.position = moved;
            var identity = GlobalObjectId.GetGlobalObjectIdSlow(visual);
            EditorSceneManager.MarkSceneDirty(copy);
            Assert.That(EditorSceneManager.SaveScene(copy), Is.True);
            Assert.That(_proofPlan != null, Is.True, "The proof plan must survive opening its copy.");
            Assert.That(_proofPlan.Region != null, Is.True, "The proof region must survive opening its copy.");
            Assert.That(AssetDatabase.Contains(_proofPlan), Is.True);
            Assert.That(AssetDatabase.Contains(_proofPlan.Region), Is.True);
            Assert.That(AssetDatabase.Contains(_proofPlan.Ground), Is.True);
            Assert.That(AssetDatabase.Contains(_proofPlan.Source), Is.True);
            Assert.That(AssetDatabase.Contains(_proofPlan.DevHull), Is.True);
            LandSceneTemplate.Refresh(_proofPlan);
            copy = EditorSceneManager.OpenScene(ProofCopy, OpenSceneMode.Single);
            visual = copy.GetRootGameObjects().Single(g => g.name == "LandRegion")
                .transform.Find("Visuals/ReviewCamera");
            Assert.That(visual.position, Is.EqualTo(moved));
            Assert.That(GlobalObjectId.GetGlobalObjectIdSlow(visual), Is.EqualTo(identity));
        }

        [Test]
        public void CumberlandCreate_PlanOnlyOnDisk_SavesPassagesRegionsHullAndFish()
        {
            Assert.That(System.IO.File.Exists(CreateProofScene) || System.IO.Directory.Exists(CreateProofFolder), Is.False,
                "A previous disposable proof must be inspected before reuse.");
            _sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            _buildList = EditorBuildSettings.scenes;
            StartFromAnEmptyScene();
            Assert.That(AssetDatabase.CreateFolder("Assets/_Project/Scenes", ProofName("CumberlandCreatePlan")), Is.Not.Empty);
            SaveProofPlan(CreateProofFolder, ProofName("CumberlandCreateProof"));
            // The batch CREATE's flow: the plan comes from disk, and nothing static holds it across the new scene.
            var handed = AssetDatabase.LoadAssetAtPath<LandScenePlanDef>(CreateProofFolder + "/Plan.asset");
            LandSceneTemplate.Create(handed);
            Debug.Log("[cumberland] CREATE proof: the new scene unloaded the plan handed in: " + (handed == null));
            Assert.That(System.IO.File.Exists(CreateProofScene), Is.True, "CREATE returned without saving its scene.");
            var roots = EditorSceneManager.OpenScene(CreateProofScene, OpenSceneMode.Single).GetRootGameObjects();
            var plan = AssetDatabase.LoadAssetAtPath<LandScenePlanDef>(CreateProofFolder + "/Plan.asset");
            plan.Validate();
            var neighbour = AssetDatabase.LoadAssetAtPath<RegionDef>("Assets/_Project/Data/Regions/NineMileCreek.asset");
            var passages = roots.SelectMany(g => g.GetComponentsInChildren<RegionPassage>(true)).ToArray();
            Assert.That(passages.Length, Is.EqualTo(3));
            foreach (var door in plan.Doors)
            {
                var passage = passages.Single(p => p.name == door.Id);
                Assert.That(passage.Target != null && passage.Target == neighbour, Is.True, door.Id + " lost its target.");
                Assert.That(Saved(passage, "_loader").Single() != null, Is.True, door.Id + " lost its loader.");
                Assert.That(passage.ArrivalKey, Is.EqualTo(door.TargetArrival));
            }
            var registrar = roots.SelectMany(g => g.GetComponentsInChildren<RegionDisplayNameRegistrar>(true)).Single();
            Assert.That(Saved(registrar, "_regions"), Is.EqualTo(new Object[] { plan.Region }));
            var loaders = roots.SelectMany(g => g.GetComponentsInChildren<RegionSceneLoader>(true)).ToArray();
            Assert.That(loaders.Length, Is.EqualTo(2));
            foreach (var loader in loaders)
                Assert.That(Saved(loader, "_regions"), Is.EqualTo(new Object[] { plan.Region, neighbour }));
            var boat = roots.SelectMany(g => g.GetComponentsInChildren<BoatController>(true)).Single();
            var hold = roots.SelectMany(g => g.GetComponentsInChildren<ShipHold>(true)).Single();
            Assert.That(boat.Hull == plan.DevHull && hold.Hull == plan.DevHull, Is.True, "The dev boat lost its hull.");
            var fishing = roots.SelectMany(g => g.GetComponentsInChildren<FishingController>(true)).Single();
            Assert.That(plan.DevFish.Length > 0 && plan.DevFish.All(f => f != null), Is.True);
            Assert.That(Saved(fishing, "_regionFish"), Is.EqualTo(plan.DevFish));
        }

        [Test]
        public void CumberlandCreate_RefusesAPlanThatIsNotAnAsset()
        {
            var p = Object.Instantiate(CumberlandBuilder.LoadPlan());
            try
            {
                var refused = Assert.Throws<InvalidOperationException>(() => LandSceneTemplate.Create(p));
                Assert.That(refused.Message, Does.Contain("saved as an asset"));
            }
            finally { Object.DestroyImmediate(p); }
        }

        // The runner's scene can be an Untitled scene that an earlier test changed. CREATE asks to save changed
        // scenes, and in batch mode that prompt cancels, so CREATE would return without making its scene. Each
        // proof starts from an empty scene; the teardown restores the runner's setup.
        static void StartFromAnEmptyScene() =>
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // A Single-mode scene open destroys in-memory objects, so each proof's plan and region are
        // disposable assets, as the real plan and region are.
        static LandScenePlanDef SaveProofPlan(string folder, string sceneName)
        {
            var plan = Object.Instantiate(CumberlandBuilder.LoadPlan());
            plan.Region = Object.Instantiate(plan.Region);
            plan.Region.SceneName = sceneName;
            AssetDatabase.CreateAsset(plan.Region, folder + "/Region.asset");
            AssetDatabase.CreateAsset(plan, folder + "/Plan.asset");
            AssetDatabase.SaveAssets();
            return plan;
        }

        // What the saved scene holds, read as the serializer reads it.
        static Object[] Saved(Component component, string field)
        {
            var property = new SerializedObject(component).FindProperty(field);
            return Enumerable.Range(0, property.isArray ? property.arraySize : 1)
                .Select(i => (property.isArray ? property.GetArrayElementAtIndex(i) : property).objectReferenceValue).ToArray();
        }

        [Test]
        public void CumberlandRegion_IsData_IdSizeSceneAndTide()
        {
            var p = CumberlandBuilder.LoadPlan(); p.Validate();
            var r = p.Region;
            Assert.That(r.Id, Is.EqualTo("region.cumberland")); Assert.That(r.SceneName, Is.EqualTo("Cumberland"));
            Assert.That(r.WorldCenter, Is.EqualTo(Vector2.zero)); Assert.That(r.WorldSizeMeters, Is.EqualTo(new Vector2(760, 560)));
            Assert.That(r.SeabedPixelsPerMetre, Is.EqualTo(2));
            var neighbour = AssetDatabase.LoadAssetAtPath<RegionDef>("Assets/_Project/Data/Regions/NineMileCreek.asset");
            Assert.That(r.TideMeanLevel, Is.EqualTo(neighbour.TideMeanLevel));
            Assert.That(r.TideAmplitude, Is.EqualTo(neighbour.TideAmplitude));
            Assert.That(r.TidePhaseHours, Is.EqualTo(neighbour.TidePhaseHours));
            var source = GroundFileIntake.ReadCumberlandFolder(CumberlandBuilder.GroundFolder);
            var field = p.Ground.Field;
            for (int row = 0; row < source.Height; row++)
                for (int col = 0; col < source.Width; col++)
                    Assert.That(field.ElevationAt(field.TexelToWorld(col, source.Height - 1 - row)),
                        Is.EqualTo(source.Decode(source.Heights[row * source.Width + col])).Within(0.001));
        }

        [Test]
        public void CumberlandDoors_EachIsGuarded_AndNamesItsArrival()
        {
            var p = Object.Instantiate(CumberlandBuilder.LoadPlan());
            var root = new GameObject("DoorsTest");
            try
            {
                var loader = root.AddComponent<RegionSceneLoader>();
                LandSceneTemplate.AddDoors(p, root.transform, loader);
                var anchor = LandSceneTemplate.AddAnchor(p, root.transform);
                Assert.That(root.GetComponentsInChildren<RegionPassage>().Length, Is.EqualTo(3));
                foreach (var d in p.Doors)
                {
                    var go = root.transform.Find(d.Id);
                    var passage = go.GetComponent<RegionPassage>();
                    Assert.That(passage.Target.Id, Is.EqualTo("region.nine_mile_creek"));
                    Assert.That(passage.ArrivalKey, Is.EqualTo(d.TargetArrival));
                    Assert.That(go.GetComponent<BoxCollider2D>().size, Is.EqualTo(d.Size));
                    Assert.That(go.GetComponent<BoxCollider2D>().isTrigger, Is.True);
                    Assert.That(go.GetComponent<LandPassageAccess>() != null, Is.EqualTo(d.OnFootOnly));
                    Assert.That((Vector2)go.position, Is.EqualTo(d.Centre));
                    var arrival = anchor.Arrivals.Single(a => a.Key == d.OwnArrival);
                    Assert.That((Vector2)arrival.DisembarkPoint.position, Is.EqualTo(d.Disembark));
                    if (d.OnFootOnly) Assert.That(arrival.ArrivalPoint, Is.Null);
                    else Assert.That((Vector2)arrival.ArrivalPoint.position, Is.EqualTo(d.Arrival));
                }
                foreach (var d in p.Doors) d.Target = null;
                var absent = new GameObject("AbsentNeighbour"); absent.transform.SetParent(root.transform);
                LandSceneTemplate.AddDoors(p, absent.transform, loader);
                Assert.That(absent.GetComponentsInChildren<RegionPassage>(), Is.Empty);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(p); }
        }

        [Test]
        public void CumberlandTemplate_RicePointAndCumberland_KeepSeparateFrames()
        {
            var p = CumberlandBuilder.LoadPlan();
            var other = RicePointBuilder.LoadPlan();
            var a = new GameObject("PlanA"); var b = new GameObject("PlanB");
            try
            {
                var first = LandSceneTemplate.AddAnchor(p, a.transform);
                var second = LandSceneTemplate.AddAnchor(other, b.transform);
                Assert.That(first.WorldBounds.size, Is.EqualTo(new Vector2(760, 560)));
                Assert.That(second.WorldBounds.size, Is.EqualTo(new Vector2(760, 360)));
                Assert.That(first.RegionId, Is.Not.EqualTo(second.RegionId));
                Assert.That(first.Arrivals.Length, Is.EqualTo(3)); Assert.That(second.Arrivals.Length, Is.EqualTo(3));
                foreach (var d in p.Doors.Where(d => d.OnFootOnly))
                    Assert.That(first.Arrivals.Single(x => x.Key == d.OwnArrival).ArrivalPoint, Is.Null, "Walking does not move the boat");
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }

        [Test]
        public void CumberlandContent_AllReferencesAndImportSettingsResolve()
        {
            var p = CumberlandBuilder.LoadPlan(); p.Validate();
            var ids = AssetDatabase.FindAssets("t:RegionDef", new[] { "Assets/_Project/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<RegionDef>(AssetDatabase.GUIDToAssetPath(g)).Id).ToArray();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
            foreach (var d in p.Doors) Assert.That(ids, Does.Contain(d.Target.Id));
            Assert.That(p.Ground.Field.ElevationAt(p.PlayerStart), Is.GreaterThan(p.Region.TideMeanLevel + p.Region.TideAmplitude));
            Assert.That(p.Ground.Field.ElevationAt(p.BoatStart), Is.EqualTo(-6).Within(0.001));
            Assert.That(Vector2.Distance(p.PlayerStart, p.BoatStart), Is.LessThan(3.5));
            foreach (var t in new[] { p.Ground.HeightTexture, p.Ground.StillLevelTexture })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t));
                Assert.That(importer.isReadable, Is.True); Assert.That(importer.sRGBTexture, Is.False);
                Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.GetPlatformTextureSettings("Standalone").format, Is.EqualTo(TextureImporterFormat.R16));
            }
        }

        [Test]
        public void CumberlandPassagePolicy_RejectsMissingControllerAndWrongPolicy()
        {
            var go = new GameObject("AccessTest");
            try
            {
                var passage = go.AddComponent<RegionPassage>();
                Assert.That(passage.TravelAllowed, Is.True, "Legacy passages have no policy");
                passage.ConfigureAccess(go.AddComponent<LandDevCoreRoots>());
                Assert.That(passage.TravelAllowed, Is.False, "Wrong policy fails closed");
                passage.ConfigureAccess(go.AddComponent<LandPassageAccess>());
                Assert.That(passage.TravelAllowed, Is.False, "No control mode is not permission to walk");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
