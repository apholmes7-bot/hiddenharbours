#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenHarbours.App.Editor
{
    public static partial class StPetersLayerRefresh
    {
        public sealed class VillageInputs
        {
            public VillageMove[] Moves;
            public VillageHome[] Homes;
            public VillageYard[] Yards;
            public IReadOnlyList<StPetersVillageDressing.Row> Dressing;
            public LightPostDef[] Lights;
            public VillageStation[] Stations;
            public IReadOnlyDictionary<string, Vector2> Lanes, People;
            public Vector2 Counter;
            public Vector2[] Machines;
            public Quaternion MachineHeading;
            public IVillageDressingAssets DressingAssets;
            public IVillageLightAssets LightAssets;
            public IVillageBoundaryAssets BoundaryAssets;
        }

        public static VillageStation[] VillageStationRows(IReadOnlyList<VillageMove> moves)
        {
            var checkedRows = StPetersVillageStations.Current;
            Vector2 keeper = checkedRows[StPetersRoutines.StationStoreCounter].Position;
            var rows = checkedRows.Values.Where(r => r.Street || r.Id == StPetersRoutines.StationSageDooryard)
                .Select(r => new VillageStation(r.Id, r.Position, StPetersRoutines.HeadingTo(r.Position,
                    r.Id == StPetersRoutines.StationStoreCounter || !r.Street ? StPetersBuilder.VillageGreen : keeper))).ToList();
            void Inside(string id, string key, float lateral)
            {
                var move = moves.Single(m => m.Key == key);
                var room = move.Footprint;
                Vector2 at = move.Position + StPetersRoutines.InsideStandPoint(room, lateral);
                Vector2 door = move.Position + room.DoorWorld;
                rows.Add(new VillageStation(id, at, StPetersRoutines.HeadingTo(door, at)));
            }
            Inside(StPetersRoutines.StationHomeSageA, "sageCottage", -StPetersRoutines.HousemateStrideMetres);
            Inside(StPetersRoutines.StationHomeSageB, "sageCottage", StPetersRoutines.HousemateStrideMetres);
            Inside(StPetersRoutines.StationHomeStore, "generalStore", 0);
            Inside(StPetersRoutines.StationSchoolDesk, "school", 0);
            return rows.ToArray();
        }

        public static VillageInputs ReadVillageInputs()
        {
            StPetersVillageStations.Invalidate();
            var moves = ReadVillageMoves();
            var plan = StPetersVillageYards.Read<VillagePlanDef>("village_plan.st_peters");
            var stands = StPetersVillageStations.Current;
            var people = StPetersInhabitants.People.Where(p => p.Position == stands[StPetersRoutines.LaneSchool].Position ||
                p.Position == stands[StPetersRoutines.StationStoreCounter].Position).ToDictionary(p => p.AssetName, p => p.Position);
            if (people.Count != 2) throw new Refusal("Village refresh needs exactly two moved outdoor keepers.");
            var beams = StPetersMachines.Beams();
            if (beams.Count != StPetersMachines.Row.Count) throw new Refusal("Village refresh needs all three machine beams.");
            return new VillageInputs
            {
                Moves = moves, Homes = ReadNewVillageHomes(), Yards = ReadVillageYards(), Dressing = StPetersVillageDressing.ReadPlan(),
                Lights = plan.LightIds.Select(StPetersVillageYards.Read<LightPostDef>).ToArray(), Stations = VillageStationRows(moves),
                Lanes = new Dictionary<string, Vector2>
                {
                    [StPetersRoutines.LaneSchool] = stands[StPetersRoutines.LaneSchool].Position,
                    [StPetersRoutines.LaneStore] = stands[StPetersRoutines.StationStoreCounter].Position,
                    [StPetersRoutines.LaneSageCottage] = stands[StPetersRoutines.StationSageDooryard].Position,
                },
                People = people, Counter = StPetersBuilder.GeneralStoreCounterPos,
                Machines = Enumerable.Range(0, beams.Count).Select(i => StPetersMachines.StandFor(i, beams)).ToArray(),
                MachineHeading = StPetersMachines.Heading, DressingAssets = new EditorVillageDressingAssets(),
                LightAssets = new EditorVillageLightAssets(), BoundaryAssets = new EditorVillageBoundaryAssets(),
            };
        }

        public sealed class VillagePass
        {
            public SceneYaml Before, After;
            public IReadOnlyList<LayerPatch> Steps;
            public IReadOnlyList<Op> Net;
            public bool IsEmpty => Net.Count == 0;

            public string ToYaml()
            {
                var text = new StringBuilder("step: VillagePass\nchecks:\n- seller ids and wares book preserved\noperations:\n");
                foreach (var op in Net)
                {
                    text.Append("- kind: ").Append(op.Kind).Append("\n  fileID: ").Append(op.FileId).Append("\n  name: ")
                        .Append('"').Append(op.Name.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\"\n");
                    void Block(string key, string value)
                    {
                        if (value == null) return;
                        text.Append("  ").Append(key).Append(": |-\n");
                        foreach (string line in value.Split('\n')) text.Append("    ").Append(line).Append('\n');
                    }
                    Block("before", op.Before); Block("after", op.After);
                }
                return text.ToString();
            }
        }

        public static VillagePass PlanVillage(SceneYaml scene, VillageInputs data)
        {
            var before = scene;
            var steps = new List<LayerPatch>();
            void Add(LayerPatch patch) { scene = patch.Apply(scene); steps.Add(patch); }
            foreach (var p in MoveVillageBuildings(scene, data.Moves)) Add(p);
            Add(NewVillageHomes(scene, data.Homes, data.LightAssets));
            Add(VillageYardBoundaries(scene, data.Yards, data.BoundaryAssets));
            foreach (var p in VillageDressing(scene, data.Dressing, data.DressingAssets)) Add(p);
            Add(VillageLights(scene, data.Lights, data.LightAssets));
            Add(VillageRoutines(scene, data.Stations, data.Lanes));
            Add(VillageInhabitants(scene, data.People));
            Add(VillageCounter(scene, data.Counter));
            foreach (var p in VillageMachines(scene, data.Machines, data.MachineHeading)) Add(p);
            var counter = before.RootNamed("GeneralStoreCounter");
            foreach (var doc in before.ComponentsOf(counter).Where(d => d.ClassId != TransformClass && d.ClassId != SpriteRendererClass))
                if (scene.Get(doc.FileId)?.Text != doc.Text) throw new Refusal("Village pass changed a counter component: " + doc.FileId);
            var net = new List<Op>();
            foreach (var group in steps.SelectMany(p => p.Ops).GroupBy(o => o.FileId))
            {
                string old = before.Get(group.Key)?.Text, next = scene.Get(group.Key)?.Text;
                if (old == next) continue;
                net.Add(new Op(old == null ? OpKind.Add : next == null ? OpKind.Delete : OpKind.Edit,
                    group.Key, group.Last().Name, old, next));
            }
            return new VillagePass { Before = before, After = scene, Steps = steps, Net = net };
        }

        public const string VillagePatchPath = "artifacts/st-peters-village/VillagePass.patch.yaml";
        [MenuItem("Hidden Harbours/World/St Peters Layer Refresh/Write Village Patch (dry run)")]
        public static void WriteVillagePatchBatch() => RunVillage(false);
        [MenuItem("Hidden Harbours/World/St Peters Layer Refresh/Apply Village Patch")]
        static void ApplyVillagePatchMenu()
        {
            if (EditorUtility.DisplayDialog("Village pass", "Apply the village patch to the closed scene file?", "Apply", "Cancel")) RunVillage(true);
        }
        public static void ApplyVillagePatchBatch() => RunVillage(true);
        public static VillagePass RunVillage(bool apply)
        {
            if (apply) for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).path == ScenePath) throw new Refusal("Close the scene before applying the village patch.");
            string text = File.ReadAllText(ScenePath);
            var plan = PlanVillage(SceneYaml.Parse(text), ReadVillageInputs());
            Directory.CreateDirectory(Path.GetDirectoryName(VillagePatchPath));
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(VillagePatchPath, plan.ToYaml(), utf8);
            if (apply && !plan.IsEmpty)
            {
                if (File.ReadAllText(ScenePath) != text) throw new Refusal("Scene changed while planning the village patch.");
                File.WriteAllText(ScenePath, plan.After.Render(), utf8);
            }
            return plan;
        }
    }
}
#endif
