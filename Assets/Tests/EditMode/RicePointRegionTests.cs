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
    public sealed class RicePointRegionTests
    {
        const string ProofSource = "Assets/_Project/Scenes/RicePointRefreshSource.unity";
        const string ProofCopy = "Assets/_Project/Scenes/RicePointRefreshCopy.unity";
        const string ProofFolder = "Assets/_Project/Scenes/RicePointRefreshProof";
        const string CreateProofScene = "Assets/_Project/Scenes/RicePointCreateProof.unity";
        const string CreateProofFolder = "Assets/_Project/Scenes/RicePointCreatePlan";
        EditorBuildSettingsScene[] _buildList;
        SceneSetup[] _sceneSetup;
        LandScenePlanDef _proofPlan;

        [TearDown]
        public void RemoveDisposableRefreshScenes()
        {
            if (_sceneSetup == null) return;
            var setup = _sceneSetup;
            _sceneSetup = null;
            var buildList = _buildList;
            _proofPlan = null;
            _buildList = null;
            Action[] cleanup = {
                () => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single),
                () => AssetDatabase.DeleteAsset(ProofCopy),
                () => AssetDatabase.DeleteAsset(ProofSource),
                () => AssetDatabase.DeleteAsset(CreateProofScene),
                () => EditorBuildSettings.scenes = buildList,
                // The scenes reference the proofs' regions, so their folders go after them.
                () => AssetDatabase.DeleteAsset(ProofFolder),
                () => AssetDatabase.DeleteAsset(CreateProofFolder),
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
        public void RicePointRefresh_DisposableSceneCopy_PreservesMovedOwnerVisual()
        {
            Assert.That(System.IO.File.Exists(ProofSource) || System.IO.File.Exists(ProofCopy)
                || System.IO.Directory.Exists(ProofFolder), Is.False,
                "A previous disposable proof must be inspected before reuse.");
            _sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            _buildList = EditorBuildSettings.scenes;
            Assert.That(AssetDatabase.CreateFolder("Assets/_Project/Scenes", "RicePointRefreshProof"), Is.Not.Empty);
            _proofPlan = SaveProofPlan(ProofFolder, "RicePointRefreshSource");
            LandSceneTemplate.Create(_proofPlan);
            Assert.That(AssetDatabase.CopyAsset(ProofSource, ProofCopy), Is.True);
            _proofPlan.Region.SceneName = "RicePointRefreshCopy";
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
        public void RicePointCreate_PlanOnlyOnDisk_SavesPassagesRegionsHullAndFish()
        {
            Assert.That(System.IO.File.Exists(CreateProofScene) || System.IO.Directory.Exists(CreateProofFolder), Is.False,
                "A previous disposable proof must be inspected before reuse.");
            _sceneSetup = EditorSceneManager.GetSceneManagerSetup();
            _buildList = EditorBuildSettings.scenes;
            Assert.That(AssetDatabase.CreateFolder("Assets/_Project/Scenes", "RicePointCreatePlan"), Is.Not.Empty);
            SaveProofPlan(CreateProofFolder, "RicePointCreateProof");
            // The batch CREATE's flow: the plan comes from disk, and nothing static holds it across the new scene.
            var handed = AssetDatabase.LoadAssetAtPath<LandScenePlanDef>(CreateProofFolder + "/Plan.asset");
            LandSceneTemplate.Create(handed);
            Debug.Log("[rice-point] CREATE proof: the new scene unloaded the plan handed in: " + (handed == null));
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
        public void LandSceneTemplate_Create_RefusesAPlanThatIsNotAnAsset()
        {
            var p = Object.Instantiate(RicePointBuilder.LoadPlan());
            try
            {
                var refused = Assert.Throws<InvalidOperationException>(() => LandSceneTemplate.Create(p));
                Assert.That(refused.Message, Does.Contain("saved as an asset"));
            }
            finally { Object.DestroyImmediate(p); }
        }

        // A Single-mode scene open destroys in-memory objects, so each proof's plan and region are
        // disposable assets, as the real plan and region are.
        static LandScenePlanDef SaveProofPlan(string folder, string sceneName)
        {
            var plan = Object.Instantiate(RicePointBuilder.LoadPlan());
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
        public void RicePointRegion_IsData_IdSizeSceneAndTide()
        {
            var p = RicePointBuilder.LoadPlan(); p.Validate();
            var r = p.Region;
            Assert.That(r.Id, Is.EqualTo("region.rice_point")); Assert.That(r.SceneName, Is.EqualTo("RicePoint"));
            Assert.That(r.WorldCenter, Is.EqualTo(Vector2.zero)); Assert.That(r.WorldSizeMeters, Is.EqualTo(new Vector2(760, 360)));
            Assert.That(r.SeabedPixelsPerMetre, Is.EqualTo(2));
            var neighbour = AssetDatabase.LoadAssetAtPath<RegionDef>("Assets/_Project/Data/Regions/NineMileCreek.asset");
            Assert.That(r.TideMeanLevel, Is.EqualTo(neighbour.TideMeanLevel));
            Assert.That(r.TideAmplitude, Is.EqualTo(neighbour.TideAmplitude));
            Assert.That(r.TidePhaseHours, Is.EqualTo(neighbour.TidePhaseHours));
            var source = GroundFileIntake.ReadRicePointFolder(RicePointBuilder.GroundFolder);
            var field = p.Ground.Field;
            for (int row = 0; row < source.Height; row++)
                for (int col = 0; col < source.Width; col++)
                    Assert.That(field.ElevationAt(field.TexelToWorld(col, source.Height - 1 - row)),
                        Is.EqualTo(source.Decode(source.Heights[row * source.Width + col])).Within(0.001));
        }

        [Test]
        public void RicePointDoors_EachIsGuarded_AndNamesItsArrival()
        {
            var p = Object.Instantiate(RicePointBuilder.LoadPlan());
            var root = new GameObject("DoorsTest");
            try
            {
                var loader = root.AddComponent<RegionSceneLoader>();
                LandSceneTemplate.AddDoors(p, root.transform, loader);
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
                }
                foreach (var d in p.Doors) d.Target = null;
                var absent = new GameObject("AbsentNeighbour"); absent.transform.SetParent(root.transform);
                LandSceneTemplate.AddDoors(p, absent.transform, loader);
                Assert.That(absent.GetComponentsInChildren<RegionPassage>(), Is.Empty);
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(p); }
        }

        [Test]
        public void LandSceneTemplate_TwoPlans_TwoFrames()
        {
            var p = RicePointBuilder.LoadPlan();
            var other = Object.Instantiate(p); other.Region = Object.Instantiate(p.Region);
            other.Region.Id = "region.land_template_test"; other.Region.WorldSizeMeters = new Vector2(80, 60);
            other.Doors = Array.Empty<LandDoor>(); other.PlayerStart = new Vector2(4, 5);
            var a = new GameObject("PlanA"); var b = new GameObject("PlanB");
            try
            {
                var first = LandSceneTemplate.AddAnchor(p, a.transform);
                var second = LandSceneTemplate.AddAnchor(other, b.transform);
                Assert.That(first.WorldBounds.size, Is.EqualTo(new Vector2(760, 360)));
                Assert.That(second.WorldBounds.size, Is.EqualTo(new Vector2(80, 60)));
                Assert.That(first.RegionId, Is.Not.EqualTo(second.RegionId));
                Assert.That(first.Arrivals.Length, Is.EqualTo(3)); Assert.That(second.Arrivals, Is.Empty);
                foreach (var d in p.Doors.Where(d => d.OnFootOnly))
                    Assert.That(first.Arrivals.Single(x => x.Key == d.OwnArrival).ArrivalPoint, Is.Null, "Walking does not move the boat");
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); Object.DestroyImmediate(other.Region); Object.DestroyImmediate(other); }
        }

        [Test]
        public void RicePointContent_AllReferencesAndImportSettingsResolve()
        {
            var p = RicePointBuilder.LoadPlan(); p.Validate();
            var ids = AssetDatabase.FindAssets("t:RegionDef", new[] { "Assets/_Project/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<RegionDef>(AssetDatabase.GUIDToAssetPath(g)).Id).ToArray();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
            foreach (var d in p.Doors) Assert.That(ids, Does.Contain(d.Target.Id));
            Assert.That(p.Ground.Field.ElevationAt(p.PlayerStart), Is.GreaterThan(p.Region.TideMeanLevel + p.Region.TideAmplitude));
            Assert.That(p.Ground.Field.ElevationAt(p.BoatStart), Is.EqualTo(-2).Within(0.001));
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
        public void RicePointPassagePolicy_RejectsMissingControllerAndWrongPolicy()
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
