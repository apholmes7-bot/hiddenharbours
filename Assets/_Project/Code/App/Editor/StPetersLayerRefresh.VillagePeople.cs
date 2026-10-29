#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.World;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    public static partial class StPetersLayerRefresh
    {
        public readonly struct VillageStation
        {
            public readonly string Id;
            public readonly Vector2 Position;
            public readonly float Heading;
            public VillageStation(string id, Vector2 position, float heading)
            { Id = id; Position = position; Heading = heading; }
        }

        static readonly string[] VillageStationIds =
        {
            StPetersRoutines.StationStoreCounter, StPetersRoutines.StationStoreCustomerA,
            StPetersRoutines.StationStoreCustomerB, StPetersRoutines.StationStoreCustomerC,
            StPetersRoutines.StationSageDooryard, StPetersRoutines.StationHomeSageA,
            StPetersRoutines.StationHomeSageB, StPetersRoutines.StationHomeStore, StPetersRoutines.StationSchoolDesk,
        };

        public static LayerPatch VillageRoutines(SceneYaml scene, IReadOnlyList<VillageStation> stations,
            IReadOnlyDictionary<string, Vector2> lanePositions)
        {
            if (stations.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() != stations.Count ||
                stations.Any(s => !VillageStationIds.Contains(s.Id)))
                throw new Refusal("Village routine patch contains duplicate or held stations.");
            if (lanePositions.Keys.Any(k => k != StPetersRoutines.LaneStore && k != StPetersRoutines.LaneSchool && k != StPetersRoutines.LaneSageCottage))
                throw new Refusal("Village routine patch contains a held lane node.");
            var root = scene.RootNamed(StPetersRoutines.RootName);
            var rootTr = scene.TransformOf(root);
            RequireIdentity(rootTr, StPetersRoutines.RootName);
            var patch = new LayerPatch("VillageRoutines", StPetersRoutines.RootName, root.FileId);
            var stationGo = scene.ChildNamed(rootTr, RoutineStations.ObjectName);
            var stationDoc = scene.ComponentsOf(stationGo).Single(d => d.IsScript(typeof(RoutineStations).FullName));
            var lines = stationDoc.Lines.ToArray();
            foreach (var station in stations)
            {
                var indexes = Enumerable.Range(0, lines.Length).Where(i => lines[i] == "  - Id: " + station.Id).ToArray();
                if (indexes.Length != 1) throw new Refusal("Village station is missing or duplicated: " + station.Id);
                int start = indexes[0], end = start + 1;
                while (end < lines.Length && !lines[end].StartsWith("  - Id:", StringComparison.Ordinal) &&
                    !lines[end].StartsWith("  _", StringComparison.Ordinal)) end++;
                void Write(string field, string value)
                {
                    int at = Enumerable.Range(start + 1, end - start - 1).Single(i => lines[i].StartsWith("    " + field + ":", StringComparison.Ordinal));
                    lines[at] = "    " + field + ": " + value;
                }
                Write("Position", Vec2Yaml(station.Position));
                Write("StandHeadingDegrees", F(station.Heading));
            }
            patch.Edit(stationDoc, string.Join("\n", lines), "RoutineStations positions and headings");
            var laneGo = scene.ChildNamed(rootTr, RoutineLanes.ObjectName);
            var laneDoc = scene.ComponentsOf(laneGo).Single(d => d.IsScript(typeof(RoutineLanes).FullName));
            var names = WallFieldBlock(laneDoc.Text, "_nodeNames").Split('\n').Skip(1).Select(l => l.Substring(4)).ToArray();
            var positions = WallFieldBlock(laneDoc.Text, "_nodePositions").Split('\n');
            if (positions.Length != names.Length + 1 || names.Distinct(StringComparer.Ordinal).Count() != names.Length)
                throw new Refusal("Village lane names and positions disagree.");
            foreach (var row in lanePositions)
            {
                int index = Array.IndexOf(names, row.Key);
                if (index < 0) throw new Refusal("Village lane node is missing: " + row.Key);
                positions[index + 1] = "  - " + Vec2Yaml(row.Value);
            }
            patch.Edit(laneDoc, WithWallFieldBlock(laneDoc.Text, "_nodePositions", string.Join("\n", positions)), "RoutineLanes moved nodes");
            return patch.Seal(scene);
        }

        static void VillageStand(LayerPatch patch, SceneYaml scene, Doc go, Vector2 position)
        {
            if (float.IsNaN(position.x) || float.IsNaN(position.y) || float.IsInfinity(position.x) || float.IsInfinity(position.y))
                throw new Refusal("Village position is not finite.");
            var tr = scene.TransformOf(go);
            patch.Edit(tr, SetField(tr.Text, "m_LocalPosition", Vec3Yaml(position.x, position.y, 0)), go.Field("m_Name") + " position");
            var sorter = scene.ComponentsOf(go).SingleOrDefault(d => d.IsScript(typeof(YSortSprite).FullName));
            if (sorter == null) return;
            foreach (var sr in scene.ComponentsOf(go).Where(d => d.ClassId == SpriteRendererClass))
                patch.Edit(sr, SetField(sr.Text, "m_SortingOrder", Int(SortingOrderFor(sorter, position.y))), go.Field("m_Name") + " sort");
        }

        public static LayerPatch VillageInhabitants(SceneYaml scene, IReadOnlyDictionary<string, Vector2> positions)
        {
            var root = scene.RootNamed(StPetersInhabitants.RootName);
            var tr = scene.TransformOf(root);
            RequireIdentity(tr, StPetersInhabitants.RootName);
            var patch = new LayerPatch("VillageInhabitants", StPetersInhabitants.RootName, root.FileId);
            foreach (var row in positions) VillageStand(patch, scene, scene.ChildNamed(tr, row.Key), row.Value);
            return patch.Seal(scene);
        }

        public static LayerPatch VillageCounter(SceneYaml scene, Vector2 position)
        {
            var root = scene.RootNamed("GeneralStoreCounter");
            var patch = new LayerPatch("VillageCounter", "GeneralStoreCounter", root.FileId);
            VillageStand(patch, scene, root, position);
            return patch.Seal(scene);
        }

        public static List<LayerPatch> VillageMachines(SceneYaml scene, IReadOnlyList<Vector2> positions, Quaternion heading)
        {
            if (positions.Count != StPetersMachines.Row.Count) throw new Refusal("Village machine row has the wrong count.");
            var patches = new List<LayerPatch>();
            for (int i = 0; i < positions.Count; i++)
            {
                string name = StPetersMachines.Row[i].Name;
                var root = scene.RootNamed(name);
                var tr = scene.TransformOf(root);
                var patch = new LayerPatch("VillageMachines", name, root.FileId);
                string text = SetField(tr.Text, "m_LocalPosition", Vec3Yaml(positions[i].x, positions[i].y, 0));
                text = SetField(text, "m_LocalRotation", "{x: " + F(heading.x) + ", y: " + F(heading.y) + ", z: " + F(heading.z) + ", w: " + F(heading.w) + "}");
                patch.Edit(tr, text, name + " position and heading");
                patches.Add(patch.Seal(scene));
            }
            return patches;
        }
    }
}
#endif
