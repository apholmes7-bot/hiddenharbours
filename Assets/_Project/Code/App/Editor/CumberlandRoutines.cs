using System;
using System.Linq;
using System.IO;
using HiddenHarbours.Art;
using HiddenHarbours.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenHarbours.App.Editor
{
    /// <summary>Phase B entry points. Tables and people belong to the region's saved scene.</summary>
    public static class CumberlandRoutines
    {
        public const string RootName = "HeronViewGolf";
        public const float TalkRadiusMetres = 1.8f;

        [MenuItem("Hidden Harbours/Regions/Cumberland/Build Golf Stations and Lanes")]
        public static void Build()
        {
            var plan = CumberlandBuilder.LoadPlan();
            Scene scene = SceneManager.GetActiveScene();
            if (plan == null || scene.path != plan.ScenePath)
                throw new InvalidOperationException("Open the Cumberland scene first.");
            CumberlandCourse course = CumberlandCourse.Read();
            GameObject root = scene.GetRootGameObjects().SingleOrDefault(g => g.name == RootName);
            if (root == null) root = new GameObject(RootName);
            RoutineStations stations = root.GetComponent<RoutineStations>();
            if (stations == null) stations = root.AddComponent<RoutineStations>();
            RoutineLanes lanes = root.GetComponent<RoutineLanes>();
            if (lanes == null) lanes = root.AddComponent<RoutineLanes>();
            Configure(course, stations, lanes);
            EditorUtility.SetDirty(stations); EditorUtility.SetDirty(lanes);
            EditorSceneManager.MarkSceneDirty(scene);
        }

        public static void Configure(CumberlandCourse course, RoutineStations stations, RoutineLanes lanes)
        {
            stations.Configure(course.Stations);
            lanes.Configure(course.Nodes, course.Parents, course.NodeNames, course.ViaStart, course.ViaCount, course.Via);
            string problem = lanes.Validate();
            if (!string.IsNullOrEmpty(problem)) throw new InvalidOperationException(problem);
        }

        [MenuItem("Hidden Harbours/Regions/Cumberland/Place Approved Golfers")]
        public static void PlaceGolfers()
        {
            Build();
            var root = SceneManager.GetActiveScene().GetRootGameObjects().Single(g => g.name == RootName);
            var stations = root.GetComponent<RoutineStations>(); var lanes = root.GetComponent<RoutineLanes>();
            foreach (HeronViewRoster.Visitor person in HeronViewRoster.Visitors)
            {
                var routine = AssetDatabase.LoadAssetAtPath<RoutineDef>(HeronViewRoutinesBuilder.Folder + "/" + person.AssetName + "Routine.asset");
                if (routine == null || !routine.IsWalkable || !routine.Npc.HasBakedBody)
                    throw new InvalidOperationException("Approved golfer needs a routine and baked build: " + person.NpcId);
                GameObject go = root.transform.Cast<Transform>().Select(t => t.gameObject).SingleOrDefault(g => g.name == person.AssetName);
                if (go == null) { go = new GameObject(person.AssetName); go.transform.SetParent(root.transform, false); }
                go.transform.position = stations.Stations.Single(s => s.Id == CumberlandCourse.Clubhouse).Position;
                SpriteRenderer sprite = go.GetComponent<SpriteRenderer>();
                if (sprite == null) sprite = go.AddComponent<SpriteRenderer>();
                NpcBodyDresser.Dress(go, sprite, routine.Npc, "", null, Color.white, 180f);
                Interactable talk = go.GetComponent<Interactable>();
                if (talk == null) talk = go.AddComponent<Interactable>();
                talk.Configure(routine.Npc);
                YSortSprite sort = go.GetComponent<YSortSprite>();
                if (sort == null) sort = go.AddComponent<YSortSprite>();
                sort.Dynamic = true;
                StPetersRoutines.WireLivingGrass(go);
                VillagerRoutine walker = go.GetComponent<VillagerRoutine>();
                if (walker == null) walker = go.AddComponent<VillagerRoutine>();
                walker.Configure(routine, stations, lanes, sprite);
                EditorUtility.SetDirty(go);
            }
            DialoguePresenter presenter = root.GetComponentInChildren<DialoguePresenter>();
            if (presenter == null) presenter = root.AddComponent<DialoguePresenter>();
            WorldInteractor interactor = root.GetComponent<WorldInteractor>();
            if (interactor == null) interactor = root.AddComponent<WorldInteractor>();
            interactor.Configure(null, presenter, root.GetComponentsInChildren<Interactable>(true), TalkRadiusMetres);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        public static void BuildCli() => HeronViewRoutinesBuilder.RunCli(() => {
            var scene = EditorSceneManager.OpenScene(CumberlandBuilder.LoadPlan().ScenePath, OpenSceneMode.Single);
            Build();
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Golf tables did not save.");
            Debug.Log("[HeronViewGolf] TABLES OK");
        });

        internal static void BuildAndSave()
        {
            Build();
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))
                throw new IOException("Golf tables did not save.");
        }

        public static void PlaceGolfersCli() => HeronViewRoutinesBuilder.RunCli(() => {
            var scene = EditorSceneManager.OpenScene(CumberlandBuilder.LoadPlan().ScenePath, OpenSceneMode.Single);
            PlaceGolfers();
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Golfers did not save.");
            Debug.Log("[HeronViewGolf] GOLFERS OK");
        });
    }
}
