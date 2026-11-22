using System.IO;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The dwelling bake (ADR 0060 section 2): its four refusals, each with a sabotage, the room
    /// sidecar reading, the tag rules, and the committed asset against a fresh extraction.
    /// </summary>
    public class DwellingMeshBakeTests
    {
        static DwellingRigFleet.MeshDwelling Clipper
        {
            get
            {
                Assert.IsTrue(DwellingRigFleet.TryGetMeshBaked("clipper", out DwellingRigFleet.MeshDwelling d),
                              "the clipper is no longer a mesh-baked dwelling");
                return d;
            }
        }

        static string Repo(string relative) => Path.Combine(RigCatalog.RepoRoot, relative);

        static string RoomJson => File.ReadAllText(Repo(Clipper.RoomSidecarPath));

        static DwellingRoomSidecar ReadRoom(string json) => DwellingRoomSidecar.Read(json, "test");

        // =============================================================================================
        //  THE FOUR REFUSALS, EACH WITH ITS SABOTAGE
        // =============================================================================================

        [Test]
        public void Refusal_MoreThan24InteriorRamps()
        {
            Assert.IsNull(DwellingMeshBakeRules.InteriorRampProblem("clipper", HullMeshDef.InteriorRampSlots));
            string problem = DwellingMeshBakeRules.InteriorRampProblem("clipper", HullMeshDef.InteriorRampSlots + 1);
            Assert.IsNotNull(problem, "25 interior ramps must be refused");
            StringAssert.Contains(HullMeshDef.InteriorRampSlots.ToString(), problem);
            Assert.Throws<System.InvalidOperationException>(
                () => RigRoomExtraction.RequireInteriorRampsFit("x", HullMeshDef.InteriorRampSlots + 1));
        }

        [Test]
        public void Refusal_AnchorVerbNotInTheVerbTable()
        {
            Assert.IsNull(DwellingMeshBakeRules.FirstRoomRefusal(ReadRoom(RoomJson)),
                          "the committed room sidecar must read clean before the sabotage means anything");

            string sabotaged = RoomJson.Replace("\"action\": \"sleep_save\"", "\"action\": \"juggle\"");
            Assert.AreNotEqual(RoomJson, sabotaged, "the sabotage did not land: no sleep_save action to replace");
            string problem = DwellingMeshBakeRules.FirstRoomRefusal(ReadRoom(sabotaged));
            Assert.IsNotNull(problem, "an anchor verb outside the verb table must be refused");
            StringAssert.Contains("juggle", problem);
        }

        [Test]
        public void Refusal_FloorPolygonThatDoesNotClose()
        {
            DwellingRoomSidecar room = ReadRoom(RoomJson);
            Assert.IsNull(DwellingMeshBakeRules.FloorProblem(room.Levels));

            DwellingLevel[] levels = room.Levels.ToArray();
            levels[0].Floor = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f) };
            Assert.IsNotNull(DwellingMeshBakeRules.FloorProblem(levels), "a two-point floor must be refused");

            levels[0].Floor = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(2f, 0f) };
            Assert.IsNotNull(DwellingMeshBakeRules.FloorProblem(levels), "a zero-area floor must be refused");
        }

        [Test]
        public void Refusal_ShellStampThatDoesNotMatchTheShellRig()
        {
            DwellingRigFleet.TryGetDwelling(Clipper.Key, out DwellingRigFleet.Dwelling shell);
            byte[] shellBytes = File.ReadAllBytes(Repo(shell.ScriptPath));
            DwellingRoomSidecar room = ReadRoom(RoomJson);
            Assert.IsNull(DwellingMeshBakeRules.ShellStampProblem(room.ShellRigSha, shellBytes, shell.ScriptPath),
                          "the committed room sidecar's shell stamp must match the committed shell rig");

            string wrong = new string('0', 64);
            string problem = DwellingMeshBakeRules.ShellStampProblem(wrong, shellBytes, shell.ScriptPath);
            Assert.IsNotNull(problem, "a stale shell stamp must be refused");

            byte[] edited = shellBytes.Concat(new byte[] { (byte)'\n', (byte)'/', (byte)'/' }).ToArray();
            Assert.IsNotNull(DwellingMeshBakeRules.ShellStampProblem(room.ShellRigSha, edited, shell.ScriptPath),
                             "an edited shell rig must no longer match the stamp");
        }

        [Test]
        public void Refusal_RoomStampThatDoesNotMatchTheRoomRig()
        {
            byte[] roomBytes = File.ReadAllBytes(Repo(Clipper.RoomScriptPath));
            DwellingRoomSidecar room = ReadRoom(RoomJson);
            Assert.IsNull(DwellingMeshBakeRules.RoomStampProblem(room.RoomRigSha, roomBytes, Clipper.RoomScriptPath));
            Assert.IsNotNull(DwellingMeshBakeRules.RoomStampProblem(new string('0', 64), roomBytes,
                                                                    Clipper.RoomScriptPath));
        }

        // =============================================================================================
        //  THE ROOM SIDECAR
        // =============================================================================================

        [Test]
        public void RoomSidecar_ReadsTheClipperRoom()
        {
            DwellingRoomSidecar room = ReadRoom(RoomJson);
            CollectionAssert.IsEmpty(room.Errors);
            Assert.AreEqual(2, room.Levels.Length);
            Assert.AreEqual("saloon", room.Levels[0].LevelId, "the step_out level is the main level");
            Assert.AreEqual("head", room.Levels[1].LevelId);
            Assert.AreEqual(1, room.Levels[0].Tag);
            Assert.AreEqual(2, room.Levels[1].Tag);
            foreach (DwellingLevel l in room.Levels)
            {
                Assert.AreEqual(DwellingMeshBakeRules.ShellLidTag, l.LidTag, l.LevelId);
                Assert.IsTrue(l.Enclosed, l.LevelId);
            }
            Assert.AreEqual("entry", room.Threshold.Id);
            Assert.AreEqual("saloon", room.Threshold.LevelId);
            Assert.IsTrue(room.Threshold.IsPublished);
            Assert.AreEqual(14, room.Anchors.Length);
            Assert.IsTrue(room.Anchors.Any(a => a.HasDefId), "no anchor carried a def id");
            Assert.IsTrue(room.Anchors.Any(a => a.LevelId == "head"), "no fixture sits in the head");
        }

        [Test]
        public void RoomSidecar_ReadIsDeterministic()
        {
            DwellingRoomSidecar a = ReadRoom(RoomJson), b = ReadRoom(RoomJson);
            Assert.AreEqual(JsonUtility.ToJson(new Wrap(a)), JsonUtility.ToJson(new Wrap(b)));
        }

        [System.Serializable]
        sealed class Wrap
        {
            public DwellingLevel[] Levels;
            public DwellingThreshold Threshold;
            public DwellingFixtureAnchor[] Anchors;
            public Wrap(DwellingRoomSidecar r) { Levels = r.Levels; Threshold = r.Threshold; Anchors = r.Anchors; }
        }

        [Test]
        public void RoomSidecar_MissingFieldIsANamedError()
        {
            string broken = RoomJson.Replace("\"WALKABLE\"", "\"WALKABLE_GONE\"");
            DwellingRoomSidecar room = ReadRoom(broken);
            Assert.IsNotNull(DwellingMeshBakeRules.FirstRoomRefusal(room));
        }

        // =============================================================================================
        //  THE TAG RULES AND THE SHARED ROOM EXTRACTION
        // =============================================================================================

        [Test]
        public void ShellTag_AboveTheFloorIsTheLid_AtOrBelowIsExterior()
        {
            Assert.AreEqual(DwellingMeshBakeRules.ShellLidTag, DwellingMeshBakeRules.ShellTagFor(1.5, 0.7));
            Assert.AreEqual(DwellingMeshBakeRules.ExteriorTag, DwellingMeshBakeRules.ShellTagFor(0.7, 0.7));
            Assert.AreEqual(DwellingMeshBakeRules.ExteriorTag, DwellingMeshBakeRules.ShellTagFor(0.2, 0.7));
        }

        [Test]
        public void OverlapsLevel_UsesTheFloorBoundsGrownByTheWall()
        {
            var floor = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            Assert.IsTrue(DwellingMeshBakeRules.OverlapsLevel(new Vector2(0.2f, 0.2f), new Vector2(0.4f, 0.4f), floor, 0.04f));
            Assert.IsTrue(DwellingMeshBakeRules.OverlapsLevel(new Vector2(1.02f, 0.2f), new Vector2(1.03f, 0.4f), floor, 0.04f),
                          "a partition standing in the wall thickness belongs to the level");
            Assert.IsFalse(DwellingMeshBakeRules.OverlapsLevel(new Vector2(1.2f, 0.2f), new Vector2(1.4f, 0.4f), floor, 0.04f));
        }

        [Test]
        public void MaterialTable_DedupesRampsByContent()
        {
            var t = new RigRoomExtraction.MaterialTable();
            int a = t.IndexFor("liner", new[] { "#101010", "#202020" });
            int b = t.IndexFor("leaf", new[] { "#101010", "#202020" });
            int c = t.IndexFor("wood", new[] { "#303030", "#404040" });
            Assert.AreEqual(a, b, "two names with one ramp share a slot");
            Assert.AreNotEqual(a, c);
            Assert.AreEqual(2, t.Materials.Count);
            Assert.IsTrue(t.TryGetIndex("leaf", out int leaf));
            Assert.AreEqual(a, leaf);
            Assert.IsFalse(t.TryGetIndex("glass", out _));
        }

        // =============================================================================================
        //  THE COMMITTED ASSET
        // =============================================================================================

        [Test]
        public void CommittedClipper_IsUsableAndMatchesItsSidecar()
        {
            var def = AssetDatabase.LoadAssetAtPath<DwellingMeshDef>(Clipper.MeshAssetPath);
            Assert.IsNotNull(def, $"{Clipper.MeshAssetPath} is missing: run Bake ALL dwelling meshes");
            Assert.IsNull(def.FirstProblem());
            Assert.AreEqual("dwelling.camper_clipper", def.Id);
            Assert.IsNotNull(def.Mesh);
            Assert.LessOrEqual(def.Ramps.Length, HullMeshDef.HullRampSlots);
            Assert.LessOrEqual(def.InteriorRamps.Length, HullMeshDef.InteriorRampSlots);
            Assert.IsTrue(def.AzimuthCounterClockwise);

            DwellingRoomSidecar room = ReadRoom(RoomJson);
            CollectionAssert.AreEqual(room.Levels.Select(l => l.LevelId), def.Levels.Select(l => l.LevelId));
            CollectionAssert.AreEqual(room.Anchors.Select(a => a.AnchorId), def.Anchors.Select(a => a.AnchorId));
            CollectionAssert.AreEqual(room.Anchors.Select(a => a.Verb), def.Anchors.Select(a => a.Verb));
            Assert.AreEqual(room.Threshold.Id, def.Threshold.Id);
        }

        [Test]
        public void CommittedClipper_IsFreshAgainstTheRigs()
        {
            var def = AssetDatabase.LoadAssetAtPath<DwellingMeshDef>(Clipper.MeshAssetPath);
            Assert.IsNotNull(def, $"{Clipper.MeshAssetPath} is missing: run Bake ALL dwelling meshes");

            DwellingMeshAssetBaker.DwellingExtraction x = DwellingMeshAssetBaker.Extract(Clipper);
            Assert.AreEqual(x.Data.TriangleCount * 3, (int)def.Mesh.GetIndexCount(0),
                            "the committed mesh is stale against the rigs: re-bake");
            Assert.AreEqual(x.Data.VertexCount, def.Mesh.vertexCount);
            Assert.AreEqual(x.Data.Materials.Count, def.Ramps.Length);
            Assert.AreEqual(x.Data.InteriorMaterials.Count, def.InteriorRamps.Length);
            Assert.Greater(x.SecondaryFaces, 0, "no room face was copied into the head level");
        }

        [Test]
        public void Extraction_IsDeterministic()
        {
            DwellingMeshAssetBaker.DwellingExtraction a = DwellingMeshAssetBaker.Extract(Clipper);
            DwellingMeshAssetBaker.DwellingExtraction b = DwellingMeshAssetBaker.Extract(Clipper);
            Assert.AreEqual(a.Data.Faces.Count, b.Data.Faces.Count);
            for (int i = 0; i < a.Data.Faces.Count; i++)
            {
                RigFace fa = a.Data.Faces[i], fb = b.Data.Faces[i];
                Assert.AreEqual(fa.Mat, fb.Mat, $"face {i}");
                Assert.AreEqual(fa.Level, fb.Level, $"face {i}");
                Assert.AreEqual(fa.Db, fb.Db, $"face {i}");
                CollectionAssert.AreEqual(fa.V.Select(v => v.ToString()), fb.V.Select(v => v.ToString()), $"face {i}");
            }
        }
    }
}
