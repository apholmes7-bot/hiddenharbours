using System;
using System.IO;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HiddenHarbours.App.Editor
{
    /// <summary>CREATE once. REFRESH rewires existing objects; it never replaces owner art or transforms.</summary>
    public static class LandSceneTemplate
    {
        const string RootName = "LandRegion";
        const string LogicName = "Logic";
        const string VisualName = "Visuals";

        public static void Create(LandScenePlanDef plan)
        {
            plan.Validate();
            if (File.Exists(plan.ScenePath))
                throw new InvalidOperationException("Land scene already exists; use REFRESH Logic.");
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            PreflightArt(plan);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject(RootName);
            var logic = Child(root.transform, LogicName);
            logic.gameObject.AddComponent<RegionLogicRoot>();
            var visuals = Child(root.transform, VisualName);

            // Inactive configuration is the NMC splat lesson: ExecuteAlways must see its final frame.
            var ground = Child(logic, "Ground").gameObject;
            ground.SetActive(false);
            ground.AddComponent<PaintedTidalTerrain>().Map = plan.Ground;
            ground.SetActive(true);
            var splat = Child(visuals, "SurfacePaint").gameObject;
            splat.SetActive(false);
            splat.AddComponent<TerrainSplatSurface>();
            var sea = Child(visuals, "Sea").gameObject;
            sea.SetActive(false);
            sea.AddComponent<SpriteRenderer>();
            sea.AddComponent<WaterSurface>();
            ConfigureSurfaces(plan, splat, sea);
            splat.SetActive(true); sea.SetActive(true);

            var camera = Child(visuals, "ReviewCamera").gameObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraFollow.OrthoSizeForWorldHeight(CameraFollow.OnFootWorldHeightMeters);
            camera.backgroundColor = plan.Backdrop;
            camera.transform.position = (Vector3)LandScenePlanDef.Project(plan.PlayerStart) + Vector3.back * 10f;
            camera.gameObject.AddComponent<AudioListener>();
            ArtCameraSetup.ConfigurePixelPerfect(camera.gameObject);

            var loader = WaterSceneTemplate.AddSceneLoader(plan.Region.SceneName, Targets(plan));
            loader.transform.SetParent(logic, false);
            var registrar = Child(logic, "DisplayNames").gameObject.AddComponent<RegionDisplayNameRegistrar>();
            WaterSceneTemplate.SetRefArray(registrar, "_regions", new Object[] { plan.Region });
            AddAnchor(plan, logic);
            AddDoors(plan, Child(logic, "Doors"), loader);
            AddClosedEdges(plan, Child(logic, "ClosedEdges"));
            AddDevCore(plan, camera, scene);
            WaterSceneTemplate.SaveAndRegister(scene, plan.ScenePath);
        }

        public static RegionAnchor AddAnchor(LandScenePlanDef plan, Transform logic)
        {
            var anchor = Child(logic, "Anchor").gameObject.AddComponent<RegionAnchor>();
            var defaultArrival = Point(anchor.transform, "DefaultArrival", plan.BoatStart);
            var defaultShore = Point(anchor.transform, "DefaultShore", plan.PlayerStart);
            anchor.Configure(plan.Region.Id, defaultArrival, defaultArrival, defaultShore);
            anchor.ConfigureExtent(Vector2.zero, plan.WorldSize);
            anchor.ConfigureArrivals((plan.Doors ?? Array.Empty<LandDoor>()).Select(d => new NamedArrival(
                d.OwnArrival, d.OnFootOnly ? null : Point(anchor.transform, d.OwnArrival, d.Arrival),
                Point(anchor.transform, d.OwnArrival + "_shore", d.Disembark))).ToArray());
            return anchor;
        }

        public static void AddDoors(LandScenePlanDef plan, Transform doors, RegionSceneLoader loader)
        {
            foreach (var door in plan.Doors ?? Array.Empty<LandDoor>())
            {
                // Only this scene's half. An absent neighbour never produces a dangling passage.
                if (door.Target == null) continue;
                var passage = WaterSceneTemplate.AddPassage(door.Id, LandScenePlanDef.Project(door.Centre),
                    LandScenePlanDef.Project(door.Size), door.Target, loader, door.TargetArrival);
                passage.transform.SetParent(doors, true);
                if (door.OnFootOnly) passage.ConfigureAccess(passage.gameObject.AddComponent<LandPassageAccess>());
            }
        }

        public static void Refresh(LandScenePlanDef plan)
        {
            plan.Validate();
            var scene = SceneManager.GetActiveScene();
            if (scene.path != plan.ScenePath) throw new InvalidOperationException("Open the plan's scene before REFRESH.");
            var root = scene.GetRootGameObjects().Single(g => g.name == RootName).transform;
            var logic = root.Find(LogicName);
            var terrain = logic.Find("Ground").GetComponent<PaintedTidalTerrain>();
            if (terrain.Map != plan.Ground) { terrain.Map = plan.Ground; EditorUtility.SetDirty(terrain); }
            var loader = logic.GetComponentInChildren<RegionSceneLoader>(true);
            WaterSceneTemplate.SetRefArray(loader, "_regions", Targets(plan));
            WaterSceneTemplate.SetRefArray(logic.GetComponentInChildren<RegionDisplayNameRegistrar>(true),
                "_regions", new Object[] { plan.Region });
            foreach (var door in plan.Doors ?? Array.Empty<LandDoor>())
            {
                var placed = logic.Find("Doors/" + door.Id);
                if (placed == null)
                {
                    if (door.Target != null) throw new InvalidOperationException("Door needs explicit authoring: " + door.Id);
                    continue;
                }
                var passage = placed.GetComponent<RegionPassage>();
                WaterSceneTemplate.SetRef(passage, "_target", door.Target);
                WaterSceneTemplate.SetRef(passage, "_loader", loader);
                WaterSceneTemplate.SetString(passage, "_arrivalKey", door.TargetArrival);
            }
            // No destroy/recreate, no re-paint, no transform resets, no new file IDs on a no-op refresh.
            EditorSceneManager.SaveScene(scene);
        }

        static RegionDef[] Targets(LandScenePlanDef plan) => new[] { plan.Region }
            .Concat((plan.Doors ?? Array.Empty<LandDoor>()).Select(d => d.Target)).Where(r => r != null).Distinct().ToArray();

        static void PreflightArt(LandScenePlanDef plan)
        {
            Required<Material>("Assets/_Project/Art/Materials/Water.mat");
            Required<Material>("Assets/_Project/Art/Materials/TerrainSplat.mat");
            Required<Sprite>("Assets/_Project/Art/Tilesets/Water/SeaTile.png");
            TerrainArrayAssets.LoadDetailRequired(); TerrainArrayAssets.LoadRelightRequired();
        }

        static T Required<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null) throw new InvalidOperationException("Missing land scene input: " + path);
            return asset;
        }

        static void ConfigureSurfaces(LandScenePlanDef plan, GameObject paint, GameObject sea)
        {
            var splat = paint.GetComponent<TerrainSplatSurface>();
            splat.Configure(Vector2.zero, plan.WorldSize, Required<Material>("Assets/_Project/Art/Materials/TerrainSplat.mat"),
                TerrainSplatSurface.DefaultSortingOrder);
            splat.ConfigureHeightMap(plan.Ground.HeightTexture, plan.Ground.MinElevation, plan.Ground.MaxElevation);
            var b = plan.HeightBands;
            splat.ConfigureBands(plan.PaintFloor, b[0], b[1], b[2], b[3], b[4], 0, 1, 0, 1);
            splat.ConfigureDetail(TerrainArrayAssets.LoadDetailRequired(), null);
            var relight = TerrainArrayAssets.LoadRelightRequired();
            splat.ConfigureRelight(relight.Normal, relight.Light, relight.Detail, relight.Ramp);
            var s = plan.SurfacePaint;
            splat.ConfigureSplat(s[0], s[1], s[2], s[3], s[4], s[5]);
            var sr = sea.GetComponent<SpriteRenderer>();
            sr.sprite = Required<Sprite>("Assets/_Project/Art/Tilesets/Water/SeaTile.png");
            sr.sharedMaterial = Required<Material>("Assets/_Project/Art/Materials/Water.mat");
            sr.sortingOrder = SortingBands.Sea;
            WaterSceneTemplate.ConfigureLandRegionWater(sea, Vector2.zero, plan.WorldSize,
                plan.Region.SeabedTexels.x, plan.Ground.MinElevation, plan.Ground.MaxElevation, plan.MaxShoreGradient);
            sea.GetComponent<WaterSurface>().ConfigurePaintedHeightMap(plan.Ground.HeightTexture,
                Vector2.zero, plan.WorldSize, plan.Ground.MinElevation, plan.Ground.MaxElevation);
        }

        static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false); return child;
        }
        static Transform Point(Transform parent, string name, Vector2 position)
        {
            var point = Child(parent, name); point.position = LandScenePlanDef.Project(position); return point;
        }

        static void AddClosedEdges(LandScenePlanDef plan, Transform parent)
        {
            var half = plan.Region.WorldSizeMeters * 0.5f;
            Edge(parent, "West", new Vector2(-half.x - plan.EdgeThickness * 0.5f, 0),
                new Vector2(plan.EdgeThickness, plan.Region.WorldSizeMeters.y));
            Edge(parent, "South", new Vector2(0, -half.y - plan.EdgeThickness * 0.5f),
                new Vector2(plan.Region.WorldSizeMeters.x, plan.EdgeThickness));
            Edge(parent, "North", new Vector2(0, half.y + plan.EdgeThickness * 0.5f),
                new Vector2(plan.Region.WorldSizeMeters.x, plan.EdgeThickness));
            // East stays physically closed just beyond the travel bands. Until NR1 it cannot leak off-map.
            Edge(parent, "East", new Vector2(half.x + plan.EdgeThickness * 0.5f, 0),
                new Vector2(plan.EdgeThickness, plan.Region.WorldSizeMeters.y));
        }
        static void Edge(Transform parent, string name, Vector2 centre, Vector2 size)
        {
            var edge = Point(parent, name, centre).gameObject.AddComponent<BoxCollider2D>();
            edge.size = LandScenePlanDef.Project(size);
        }

        static void AddDevCore(LandScenePlanDef plan, Camera review, Scene scene)
        {
            var before = scene.GetRootGameObjects();
            var handle = PersistentCoreBuilder.Build(new PersistentCoreBuilder.Params {
                Config = Required<GameConfig>(WaterSceneTemplate.GameConfigAsset), StartDory = plan.DevHull,
                RegionFish = plan.DevFish, Square = Required<Sprite>("Assets/_Project/Art/Sprites/Square.png"),
                CameraBackground = plan.Backdrop, PlayerStartPos = LandScenePlanDef.Project(plan.PlayerStart),
                BoatMooredPos = LandScenePlanDef.Project(plan.BoatStart), BoatMooredHeadingDegrees = plan.BoatHeading,
                TideGatedWalk = true, CurrentSceneName = plan.Region.SceneName,
                TideMean = plan.Region.TideMeanLevel, TideAmplitude = plan.Region.TideAmplitude, TidePhaseHours = plan.Region.TidePhaseHours
            });
            var roots = scene.GetRootGameObjects().Except(before).ToArray();
            // Travel activates destination roots before Start. Keep the inactive core one level
            // below an active envelope so that root activation cannot awaken a second rig.
            var envelope = new GameObject("DevCoreEnvelope");
            var dev = Child(envelope.transform, "DevCore").gameObject; dev.SetActive(false);
            dev.AddComponent<LandDevCoreRoots>();
            foreach (var root in roots) root.transform.SetParent(dev.transform, true);
            var gameRoot = new SerializedObject(handle.ServicesRoot.GetComponent<GameRoot>());
            gameRoot.FindProperty("_bootToTitle").boolValue = false;
            gameRoot.ApplyModifiedPropertiesWithoutUndo();
            var boat = new SerializedObject(handle.Boat);
            boat.FindProperty("_tideGatedCrossing").boolValue = true;
            boat.ApplyModifiedPropertiesWithoutUndo();
            WaterSceneTemplate.SetRefArray(handle.Loader, "_regions", Targets(plan));
            foreach (var fishing in dev.GetComponentsInChildren<FishingController>(true))
                WaterSceneTemplate.SetString(fishing, "_regionId", plan.Region.Id);
            var bootstrap = new GameObject("DevRegionBootstrap").AddComponent<DevRegionBootstrap>();
            bootstrap.Configure(dev, review);
        }
    }
}
