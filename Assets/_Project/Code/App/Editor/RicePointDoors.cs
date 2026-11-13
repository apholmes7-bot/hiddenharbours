using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// Authors, in the saved Rice Point scene, any door its plan has gained since the scene was
    /// created. <see cref="LandSceneTemplate.Refresh"/> re-wires the doors a scene already holds and
    /// refuses a plan door it cannot find ("needs explicit authoring"); this is that authoring. It
    /// lays the door's passage and its named arrival exactly as CREATE lays them
    /// (<see cref="LandSceneTemplate.AddDoors"/>, <see cref="LandSceneTemplate.AddAnchor"/>), creates
    /// only what is missing, and touches nothing that is already there, so a second run changes
    /// nothing.
    /// </summary>
    public static class RicePointDoors
    {
        const string RootName = "LandRegion", LogicName = "Logic", DoorsName = "Doors", AnchorName = "Anchor";
        public const string ShoreSuffix = "_shore";

        /// <summary>The key a boat from the South Water asks Rice Point for, and the key Rice Point's door sends there.</summary>
        public const string FromSouthWaterArrivalKey = SouthWaterPlan.ArrivalKeySentToNeighbours;
        public const string ToSouthWaterArrivalKey = SouthWaterPlan.FromRicePointArrivalKey;

        public static LandDoor SouthWaterDoor(LandScenePlanDef plan) =>
            plan.Doors.Single(d => d.OwnArrival == FromSouthWaterArrivalKey);

        public static void Ensure(Scene scene, LandScenePlanDef plan)
        {
            var root = scene.GetRootGameObjects().Single(g => g.name == RootName);
            var logic = root.transform.Find(LogicName);
            var doors = logic.Find(DoorsName);
            var anchor = logic.Find(AnchorName).GetComponent<RegionAnchor>();
            var loader = logic.GetComponentInChildren<RegionSceneLoader>(true);
            if (doors == null || anchor == null || loader == null)
                throw new InvalidOperationException("Rice Point's scene has no doors group, anchor or loader: CREATE it first.");
            foreach (var door in plan.Doors ?? Array.Empty<LandDoor>())
            {
                if (door.Target == null) continue;
                if (doors.Find(door.Id) == null)
                {
                    var passage = WaterSceneTemplate.AddPassage(door.Id, LandScenePlanDef.Project(door.Centre),
                        LandScenePlanDef.Project(door.Size), door.Target, loader, door.TargetArrival);
                    passage.transform.SetParent(doors, true);
                    if (door.OnFootOnly) passage.ConfigureAccess(passage.gameObject.AddComponent<LandPassageAccess>());
                    if (door.Shut) passage.GetComponent<BoxCollider2D>().enabled = false;
                    EditorUtility.SetDirty(passage);
                }
                if (anchor.HasArrival(door.OwnArrival)) continue;
                var arrival = new NamedArrival(door.OwnArrival,
                    door.OnFootOnly ? null : Point(anchor.transform, door.OwnArrival, door.Arrival),
                    Point(anchor.transform, door.OwnArrival + ShoreSuffix, door.Disembark));
                anchor.ConfigureArrivals(anchor.Arrivals.Concat(new[] { arrival }).ToArray());
                EditorUtility.SetDirty(anchor);
            }
        }

        static Transform Point(Transform parent, string name, Vector2 position)
        {
            var point = parent.Find(name);
            if (point == null)
            {
                point = new GameObject(name).transform;
                point.SetParent(parent, false);
            }
            point.position = LandScenePlanDef.Project(position);
            return point;
        }
    }
}
