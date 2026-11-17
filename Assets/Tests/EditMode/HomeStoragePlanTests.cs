using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using Kind = HiddenHarbours.App.Editor.HomeStoragePlan.Kind;
using Row = HiddenHarbours.App.Editor.HomeStoragePlan.Row;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Storage with memory, as content (owner 2026-11-17): the plan table (<see cref="HomeStoragePlan"/>) and
    /// the Defs it names. Every id a unique <c>storage.*</c> save key (against the fixed ids too, and against
    /// any rack already placed in a scene or prefab); every Def on disk with the owner's capacities; the camper
    /// and the shed gated on the camper's deed and Ginny's open; Ginny's two pieces inside the player's room
    /// upstairs, clear of the furniture, the partitions, his door and each other.
    /// </summary>
    public class HomeStoragePlanTests
    {
        const float Eps = 1e-3f;
        const string StowageFolder = "Assets/_Project/Data/Stowage";
        const string KitchenFolder = "Assets/_Project/Data/Resources/Kitchen/Storage";

        static Rect RectOf(Vector2 centre, Vector2 size) =>
            new Rect(centre.x - size.x * 0.5f, centre.y - size.y * 0.5f, size.x, size.y);

        static bool Overlap(Rect a, Rect b) =>
            a.xMin < b.xMax - Eps && b.xMin < a.xMax - Eps && a.yMin < b.yMax - Eps && b.yMin < a.yMax - Eps;

        static Dictionary<string, T> DefsIn<T>(string folder, System.Func<T, string> id) where T : Object
        {
            var byId = new Dictionary<string, T>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder }))
            {
                var def = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null && !string.IsNullOrEmpty(id(def))) byId[id(def)] = def;
            }
            return byId;
        }

        static Dictionary<string, StowageDef> Stowage() => DefsIn<StowageDef>(StowageFolder, d => d.Id);
        static Dictionary<string, KitchenStorageDef> Kitchen() => DefsIn<KitchenStorageDef>(KitchenFolder, d => d.Id);
        static Dictionary<string, FreezerDef> Freezers() => DefsIn<FreezerDef>(StowageFolder, d => d.Id);

        /// <summary>The deed a row's Def names, whichever kind of Def it is.</summary>
        static string DeedOf(Row row, Dictionary<string, StowageDef> s, Dictionary<string, FreezerDef> f,
                             Dictionary<string, KitchenStorageDef> k)
            => HomeStoragePlan.IsStowage(row.Kind) ? s[row.DefId].DeedHomeId
             : HomeStoragePlan.IsCatchFreezer(row.Kind) ? f[row.DefId].DeedHomeId
             : k[row.DefId].DeedHomeId;

        // ---- ids --------------------------------------------------------------------------------------

        [Test]
        public void EveryIdIsAUniqueStorageId_IncludingTheFixedOnes()
        {
            var pattern = new Regex(@"^storage\.[a-z0-9_]+(\.[a-z0-9_]+)*$");
            var seen = new HashSet<string> { StorageIds.BoatHold, StorageIds.GinnyFreezer };
            Assert.IsTrue(pattern.IsMatch(StorageIds.BoatHold));
            Assert.IsTrue(pattern.IsMatch(StorageIds.GinnyFreezer));
            Assert.IsTrue(pattern.IsMatch(StorageIds.CamperFreezer));
            foreach (Row row in HomeStoragePlan.Rows)
            {
                Assert.IsTrue(pattern.IsMatch(row.StorageId), $"'{row.StorageId}' is not storage.snake_case");
                Assert.IsTrue(StorageIds.IsStorageId(row.StorageId));
                Assert.IsTrue(seen.Add(row.StorageId), $"'{row.StorageId}' is used twice");
            }
            Assert.IsTrue(seen.Contains(StorageIds.CamperFreezer), "the camper's freezer row is its fixed code id");
        }

        [Test]
        public void NoTwoPlacedRacksShareAStorageId()
        {
            // Content validation over what is already placed: a rack's _id in a scene or prefab. Two placed
            // containers under one id would share one save row. Streamed line by line (scenes are large).
            var lineId = new Regex(@"^\s*_id:\s*(storage\.[a-z0-9_.]+)\s*$");
            var where = new Dictionary<string, string>();
            var faults = new List<string>();
            string root = Path.Combine(Application.dataPath, "_Project");
            foreach (string path in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
            {
                if (!path.EndsWith(".unity") && !path.EndsWith(".prefab")) continue;
                foreach (string line in File.ReadLines(path))
                {
                    if (line.IndexOf("_id: storage.", System.StringComparison.Ordinal) < 0) continue;
                    Match m = lineId.Match(line);
                    if (!m.Success) continue;
                    string id = m.Groups[1].Value;
                    if (id == StorageIds.BoatHold || id == StorageIds.GinnyFreezer || id == StorageIds.CamperFreezer)
                        faults.Add($"{path}: '{id}' is a fixed code id, never authored on a rack");
                    else if (where.TryGetValue(id, out string first))
                        faults.Add($"'{id}' is placed in both {first} and {path}");
                    else
                        where[id] = path;
                }
            }
            Assert.IsEmpty(faults, string.Join("\n", faults));
        }

        [Test]
        public void NoTwoPlacedToolsShareAnAuthoredId()
        {
            // The save remembers a stowed tool by its authored _id (the rack binds back to THE rod with that id),
            // so two placed tools under one id would both answer one save row. Every CarriableTool component in a
            // scene or prefab (its script guid; stripped prefab-instance stubs carry no fields and are skipped) is
            // streamed block by block; an empty id is per-instance and never remembered, so it is not counted. A
            // prefab's own id counts as one placement (every instance of it would carry that id).
            const string CarriableToolGuid = "aceb7d90ce7a4064b6a93401c5449217";
            var toolId = new Regex(@"^  _id:\s*(\S*)\s*$");
            var where = new Dictionary<string, string>();
            var faults = new List<string>();
            string root = Path.Combine(Application.dataPath, "_Project");
            foreach (string path in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
            {
                if (!path.EndsWith(".unity") && !path.EndsWith(".prefab")) continue;
                bool inBlock = false, isTool = false;
                string id = null;
                foreach (string line in File.ReadLines(path))
                {
                    if (line.StartsWith("--- ", System.StringComparison.Ordinal))
                    {
                        Claim(path, isTool ? id : null, where, faults);
                        inBlock = line.StartsWith("--- !u!114 &", System.StringComparison.Ordinal) &&
                                  !line.EndsWith(" stripped", System.StringComparison.Ordinal);
                        isTool = false;
                        id = null;
                        continue;
                    }
                    if (!inBlock) continue;
                    if (line.StartsWith("  m_Script:", System.StringComparison.Ordinal))
                        isTool = line.IndexOf(CarriableToolGuid, System.StringComparison.Ordinal) >= 0;
                    else if (line.StartsWith("  _id:", System.StringComparison.Ordinal))
                    {
                        Match m = toolId.Match(line);
                        if (m.Success) id = m.Groups[1].Value;
                    }
                }
                Claim(path, isTool ? id : null, where, faults);
            }
            Assert.IsEmpty(faults, string.Join("\n", faults));
            Assert.IsTrue(where.ContainsKey("tool.st_peters.rod"),
                          "the scan finds the placed rod (a scan that finds nothing proves nothing)");
        }

        static void Claim(string path, string id, Dictionary<string, string> where, List<string> faults)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (where.TryGetValue(id, out string first))
                faults.Add($"tool id '{id}' is placed in both {first} and {path}");
            else
                where[id] = path;
        }

        // ---- defs and capacities ----------------------------------------------------------------------

        [Test]
        public void EveryRowsDefIsOnDisk_OfTheRightKind()
        {
            Dictionary<string, StowageDef> stowage = Stowage();
            Dictionary<string, KitchenStorageDef> kitchen = Kitchen();
            Dictionary<string, FreezerDef> freezers = Freezers();
            foreach (Row row in HomeStoragePlan.Rows)
            {
                if (HomeStoragePlan.IsStowage(row.Kind))
                {
                    Assert.IsTrue(stowage.TryGetValue(row.DefId, out StowageDef def), $"{row.StorageId}: no StowageDef '{row.DefId}'");
                    StowageAccepts want = row.Kind == Kind.RodRack ? StowageAccepts.Rods : StowageAccepts.General;
                    Assert.AreEqual(want, def.Accepts, $"{row.DefId}: a {row.Kind} takes {want}");
                }
                else if (HomeStoragePlan.IsCatchFreezer(row.Kind))
                {
                    Assert.IsTrue(freezers.ContainsKey(row.DefId), $"{row.StorageId}: no FreezerDef '{row.DefId}'");
                    Assert.AreEqual(StorageIds.CamperFreezer, row.StorageId, "the catch freezer answers its fixed id");
                    Assert.IsFalse(kitchen.ContainsKey("kitchen_storage.camper_freezer"),
                                   "the camper's freezer is a catch hold, never also a food store");
                }
                else
                {
                    Assert.IsTrue(kitchen.TryGetValue(row.DefId, out KitchenStorageDef def), $"{row.StorageId}: no KitchenStorageDef '{row.DefId}'");
                    if (row.Kind == Kind.Pantry) Assert.AreEqual(KitchenStorageKind.Pantry, def.Kind);
                    else Assert.AreEqual(KitchenStorageKind.Fridge, def.Kind, $"{row.DefId}: a powered cold store");
                }
            }
        }

        [Test]
        public void TheCapacitiesAreTheOwnersAsk()
        {
            Dictionary<string, StowageDef> s = Stowage();
            Dictionary<string, KitchenStorageDef> k = Kitchen();
            Assert.AreEqual(2, s["stowage.ginny_rod_rack"].Capacity);
            Assert.AreEqual(6, s["stowage.ginny_gear_locker"].Capacity);
            Assert.AreEqual(4, s["stowage.camper_rod_rack"].Capacity);
            Assert.AreEqual(10, s["stowage.camper_gear_locker"].Capacity);
            Assert.AreEqual(8, s["stowage.shed_rod_rack"].Capacity);
            Assert.AreEqual(12, s["stowage.shed_gear_locker"].Capacity);
            Dictionary<string, FreezerDef> f = Freezers();
            Assert.AreEqual(40, f["freezer.camper_freezer"].CapacityUnits);
            Assert.AreEqual(16, k["kitchen_storage.camper_fridge"].CapacityItems);
            Assert.AreEqual(8, k["kitchen_storage.camper_pantry"].CapacityItems);

            // "more storage with the camper and small shed which hold more rods, bigger freezer"
            Assert.Greater(s["stowage.camper_rod_rack"].Capacity, s["stowage.ginny_rod_rack"].Capacity);
            Assert.Greater(s["stowage.shed_rod_rack"].Capacity, s["stowage.camper_rod_rack"].Capacity);
            var go = new GameObject("FreezerDefault");
            try
            {
                int ginnys = go.AddComponent<GinnyFreezer>().CapacityUnits;
                Assert.Greater(f["freezer.camper_freezer"].CapacityUnits, ginnys, "bigger than Ginny's freezer");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheCamperAndShedOpenWithTheCampersDeed_GinnysNeedsNone()
        {
            Dictionary<string, StowageDef> s = Stowage();
            Dictionary<string, KitchenStorageDef> k = Kitchen();
            Dictionary<string, FreezerDef> f = Freezers();
            foreach (Row row in HomeStoragePlan.Rows)
            {
                string deed = DeedOf(row, s, f, k);
                string want = row.BuildingKey == "ginny_cottage" ? "" : HomeStoragePlan.CamperDeedHomeId;
                Assert.AreEqual(want, deed ?? "", $"{row.DefId} ({row.BuildingKey})");
            }
            foreach (var def in s.Values.Where(d => d.Id == "stowage.rod_rack"))
                Assert.IsEmpty(def.DeedHomeId ?? "", "the boat's rod rack needs no deed");
            foreach (var def in k.Values.Where(d => !d.Id.StartsWith("kitchen_storage.camper_")))
                Assert.IsEmpty(def.DeedHomeId ?? "", $"{def.Id}: a galley store aboard needs no deed");
        }

        [Test]
        public void TheCampersDeedIsAShippedHome()
        {
            string[] guids = AssetDatabase.FindAssets("t:HomeDef");
            var ids = new HashSet<string>();
            foreach (string guid in guids)
            {
                Object def = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                var so = new SerializedObject(def);
                SerializedProperty id = so.FindProperty("Id");
                if (id != null) ids.Add(id.stringValue);
            }
            Assert.IsTrue(ids.Contains(HomeStoragePlan.CamperDeedHomeId), "the deed the camper's stores name must exist");
        }

        // ---- Ginny's: where they stand ----------------------------------------------------------------

        static IEnumerable<Row> Placed() => HomeStoragePlan.Rows.Where(r => r.HasPosition);

        [Test]
        public void GinnysTwoPiecesArePlaced_AndOnlyThey()
        {
            CollectionAssert.AreEquivalent(
                new[] { "storage.stpeters.ginny_rod_rack", "storage.stpeters.ginny_gear_locker" },
                Placed().Select(r => r.StorageId).ToArray());
            foreach (Row row in Placed())
            {
                Assert.AreEqual(1, row.Level, $"{row.StorageId}: his room is upstairs");
                Assert.IsTrue(StPetersInteriors.HasUpperLevel(row.PlanKey), $"{row.StorageId}: no upper plan '{row.PlanKey}'");
            }
        }

        [Test]
        public void GinnysPiecesStandInHisRoom_ClearOfTheWalls()
        {
            // The player's room upstairs, read off the plan's own partitions: from the landing partition's
            // room face to the left wall, from the bedroom partition's front face to the front wall.
            StPetersInteriors.UpperLevelPlan plan = StPetersInteriors.UpperLevelFor(StPetersInteriors.GinnyCottagePlanKey);
            float wall = StPetersInteriors.WallThicknessMetres;
            float hw = RoomFixturePlan.SageW * 0.5f, hl = RoomFixturePlan.SageL * 0.5f;
            float landing = plan.Partitions.Max(p => p.X1 < 0f ? p.X1 : float.MinValue);
            float between = plan.Partitions.Where(p => p.X1 > 0f).Max(p => p.Y1);
            var room = Rect.MinMaxRect(landing, between, hw - wall, hl - wall);

            foreach (Row row in Placed())
            {
                Rect r = RectOf(row.At, HomeStoragePlan.Footprint(row.Kind));
                Assert.IsTrue(r.xMin >= room.xMin - Eps && r.xMax <= room.xMax + Eps &&
                              r.yMin >= room.yMin - Eps && r.yMax <= room.yMax + Eps,
                              $"{row.StorageId} {r} is outside the player's room {room}");
                foreach (var p in plan.Partitions)
                    Assert.IsFalse(Overlap(r, Rect.MinMaxRect(p.X0, p.Y0, p.X1, p.Y1)),
                                   $"{row.StorageId} stands in the partition: {p.Why}");
            }
        }

        [Test]
        public void GinnysPiecesClearTheFurniture_HisDoor_TheDoorwayLane_AndEachOther()
        {
            StPetersInteriors.UpperLevelPlan plan = StPetersInteriors.UpperLevelFor(StPetersInteriors.GinnyCottagePlanKey);
            float halfDoor = StPetersInteriors.DoorwayWidthMetres * 0.5f, hl = RoomFixturePlan.SageL * 0.5f;
            InteriorKit.Contract contract = InteriorKit.Load();
            List<Row> placed = Placed().ToList();

            // His door is the gap in the landing partition between its front and middle runs; the floor in
            // front of it, a door's width into the room, is kept clear so he can walk in.
            var landing = plan.Partitions.Where(p => p.X1 < 0f).OrderBy(p => p.Y0).ToList();
            float doorFrom = landing[1].Y1, doorTo = landing[2].Y0;
            var doorLane = Rect.MinMaxRect(landing[0].X1, doorFrom, landing[0].X1 + StPetersInteriors.DoorwayWidthMetres, doorTo);

            foreach (Row row in placed)
            {
                Rect r = RectOf(row.At, HomeStoragePlan.Footprint(row.Kind));
                Assert.IsFalse(Overlap(r, doorLane), $"{row.StorageId} blocks the way in at his door");
                bool inLane = Mathf.Abs(row.At.x) < halfDoor + 0.5f && row.At.y > hl - 2.0f;
                Assert.IsFalse(inLane, $"{row.StorageId} at {row.At} is parked in the (plugged) doorway lane");

                if (contract?.props != null && contract.props.Length > 0)
                    foreach (var f in plan.Furnishings)
                    {
                        InteriorKit.Entry prop = InteriorKit.FindProp(contract, f.PropKey);
                        if (prop == null) continue;
                        float w = prop.propFootprintWidth, d = prop.propFootprintDepth;
                        int q = ((f.FacingOffset % 8) + 8) % 8;
                        Vector2 size = q % 2 != 0 ? Vector2.one * Mathf.Sqrt(w * w + d * d)
                                     : q == 2 || q == 6 ? new Vector2(d, w) : new Vector2(w, d);
                        Assert.IsFalse(Overlap(r, RectOf(f.RoomMetres, size)),
                                       $"{row.StorageId} at {row.At} overlaps the {f.PropKey} at {f.RoomMetres}");
                    }
            }
            for (int i = 0; i < placed.Count; i++)
            for (int j = i + 1; j < placed.Count; j++)
                Assert.IsFalse(Overlap(RectOf(placed[i].At, HomeStoragePlan.Footprint(placed[i].Kind)),
                                       RectOf(placed[j].At, HomeStoragePlan.Footprint(placed[j].Kind))),
                               $"{placed[i].StorageId} and {placed[j].StorageId} overlap");
        }
    }
}
