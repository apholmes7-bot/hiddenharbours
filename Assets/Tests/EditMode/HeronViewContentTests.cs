using System;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class HeronViewContentTests
    {
        [Test]
        public void HeronViewContent_AllEightDefsResolve_AndKeepTheApprovedWords()
        {
            var course = CumberlandCourse.Read();
            var host = new GameObject("GolfContentCheck");
            try
            {
                var stations = host.AddComponent<RoutineStations>(); var lanes = host.AddComponent<RoutineLanes>();
                CumberlandRoutines.Configure(course, stations, lanes);
                var routines = CumberlandRoutines.LoadRoutines();
                Assert.That(routines.Length, Is.EqualTo(8));
                var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
                foreach (var person in HeronViewRoster.Visitors)
                {
                    var routine = routines.Single(r => r.Id == person.RoutineId);
                    var npc = routine.Npc;
                    Assert.That(routine.RegionId, Is.EqualTo(HeronViewRoster.RegionId));
                    Assert.That(npc.Id, Is.EqualTo(person.NpcId));
                    Assert.That(npc.DisplayName, Is.EqualTo(person.Name));
                    Assert.That(npc.Kind, Is.EqualTo(InteractKind.Talk));
                    Assert.That(npc.Build.Id, Is.EqualTo(person.BuildId));
                    Assert.That(npc.HasBakedBody, Is.True);
                    Assert.That(npc.Dialogue.Id, Is.EqualTo(person.DialogueId));
                    Assert.That(npc.Dialogue.FirstLines, Is.EqualTo(new[] { person.Line }));
                    Assert.That(npc.Dialogue.Lines(true), Is.EqualTo(new[] { person.Line }));
                    foreach (string id in new[] { npc.Id, routine.Id, npc.Build.Id, npc.Dialogue.Id })
                        Assert.That(ids.Add(id), Is.True, id);
                    Assert.That(HeronViewGolferColours.TryLook(npc.Build, out var look, out string why), Is.True, why);
                    Assert.That(look.Skin, Is.SameAs(npc.Build.Visual.Skin));
                    Assert.That(RoutinePlanner.Build(routine, stations, lanes, 37, out _, out string problem), Is.Not.Null, problem);
                }
                Assert.That(ids.Count, Is.EqualTo(32));
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void HeronViewAssets_BuildKeepsAllThirtyTwoFiles()
        {
            var paths = HeronViewRoster.Visitors.SelectMany(p => new[] {
                HeronViewGolfAssetsBuilder.Builds + "/" + p.AssetName + "Build.asset",
                HeronViewGolfAssetsBuilder.Dialogues + "/" + p.AssetName + "First.asset",
                HeronViewRoutinesBuilder.NpcFolder + "/" + p.AssetName + ".asset",
                HeronViewRoutinesBuilder.Folder + "/" + p.AssetName + "Routine.asset"
            }).ToArray();
            var before = paths.Select(System.IO.File.ReadAllBytes).ToArray();
            HeronViewGolfAssetsBuilder.Build();
            for (int i = 0; i < paths.Length; i++)
                Assert.That(System.IO.File.ReadAllBytes(paths[i]), Is.EqualTo(before[i]), paths[i]);
        }

        [Test]
        public void RoutineOutOfSight_OffByDefault_StPetersUnchanged()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/StPeters.unity", OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var stations = roots.SelectMany(r => r.GetComponentsInChildren<RoutineStations>(true)).Single();
                var lanes = roots.SelectMany(r => r.GetComponentsInChildren<RoutineLanes>(true)).Single();
                Assert.That(stations.Stations.All(s => !s.OutOfSight), Is.True);
                foreach (var routine in StPetersRoutines.LoadRoutines())
                foreach (int seed in new[] { 0, 37, 9281 })
                {
                    var plan = RoutinePlanner.Build(routine, stations, lanes, seed, out _, out string problem);
                    Assert.That(plan, Is.Not.Null, problem);
                    var unmarked = new RoutinePlan(plan.DepartureHours, plan.Waypoints, plan.RouteStart, plan.RouteCount,
                        plan.RouteLengthMetres, plan.Activities, plan.StandHeadingDegrees, plan.ExitAtMetres,
                        plan.EnterAtMetres, plan.WalkSpeedMetresPerSecond, plan.Interruptible, plan.CatchUpSpeedMetresPerSecond);
                    for (int minute = 0; minute < 1440; minute++)
                        Assert.That(plan.SampleAt(minute / 60f, 75f), Is.EqualTo(unmarked.SampleAt(minute / 60f, 75f)), routine.Id);
                }
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
