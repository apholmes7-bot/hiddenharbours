using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>Every prop a figure holds, as a committed facet mesh (ADR 0053, PR 2)</b> — the
    /// <see cref="HeldPropMeshDef"/>s in <see cref="RigPropMeshBaker.HeldPropFolder"/> against the four
    /// rigs they were baked from: the rod's three tiers, the clam spade, seven fish by the tail and every
    /// bucket state.
    ///
    /// <para><b>The rigs are the oracle.</b> Each state is read through the same reconstructed
    /// <c>held</c> the bake used (<see cref="HeldPropFleet"/>), and the committed def must agree with that
    /// read, so a rig changed without a re-bake reds here by name instead of drawing a stale prop. The
    /// fix is always the same: Hidden Harbours ▸ Dev ▸ 3D Hulls ▸ Bake ALL held props (or
    /// <c>HeldPropBakeCli.BakeAndWireCli</c>), in the commit that changes the rig.</para>
    ///
    /// <para><b>Everything here is CPU</b> — V8 and the asset database, no graphics device — so CI runs
    /// it. Nothing draws these meshes yet (PR 4 presents them), so the checks are on the data; the proof
    /// sheet, each mesh beside today's sprite, is the picture's check.</para>
    /// </summary>
    public class HeldPropBakeTests
    {
        const string Rebake = "Re-bake the held props (Hidden Harbours ▸ Dev ▸ 3D Hulls ▸ Bake ALL held " +
                              "props) in the same commit as the rig.";

        /// <summary>A millimetre, a thirtieth of a pixel at 32 px/m. The rigs put the hand at the origin
        /// exactly (the fish to within 1e-16 m, through its loft's own rings).</summary>
        const float GripTolerance = 1e-3f;

        /// <summary>
        /// The face counts measured off the rigs' own builders, pinned. A reconstruction aimed at the
        /// wrong pose or tier would still agree with itself, and these would not. The buckets are not
        /// pinned: each of their states is checked against its rig's builder alone. Change one only with
        /// its rig, in the commit that re-bakes.
        /// </summary>
        static readonly Dictionary<string, int> PinnedFaces = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["heldprop.rod_cane"] = 12,
            ["heldprop.rod_coast"] = 30,
            ["heldprop.rod_deep"] = 30,
            ["heldprop.shovel"] = 30,
            ["heldprop.fish_cod"] = 87,
            ["heldprop.fish_haddock"] = 87,
            ["heldprop.fish_pollock"] = 87,
            ["heldprop.fish_mackerel"] = 87,
            ["heldprop.fish_bass"] = 87,
            ["heldprop.fish_flounder"] = 88,
            ["heldprop.fish_herring"] = 86,
        };

        /// <summary>The bail is plotted, not built: 45 samples of the rig's arc on the pail and the
        /// tote, and none on the tray, which has no bail.</summary>
        const int BailPoints = 45;

        const string BailLine = "bail";
        const string TrayTier = "tray";

        // ---- the rigs, read the way the bake reads them -------------------------------------------

        /// <summary>One held state as its rig makes it now, read exactly as
        /// <see cref="RigPropMeshBaker.BakeHeld"/> reads it, beside the def committed for it.</summary>
        sealed class RigState
        {
            public HeldProp Prop;
            public HeldPropMeshDef Def;

            /// <summary>Why the rig could not be read, or null.</summary>
            public string Error;

            /// <summary><c>held.faces(v).length</c>, what the def's SourceFaceCount must say.</summary>
            public int RigFaces;

            /// <summary>The extractor's read with the repeated corners dropped: the faces the mesh is
            /// built from, each still painting by its index into the rig's WHOLE table.</summary>
            public RigMeshData Data;

            public Vector3 Grip;

            /// <summary><c>held.lines(v)</c> across the bridge as the bake carries it: ramps @ lines.</summary>
            public string Lines;
        }

        List<RigState> _states;

        /// <summary>Read once per fixture run and never cached past it: a rig edited between two runs in
        /// one editor session must red the second.</summary>
        List<RigState> States => _states ??= ReadTheRigs();

        static List<RigState> ReadTheRigs()
        {
            var states = new List<RigState>();
            foreach (HeldProp p in HeldPropFleet.Expand())
            {
                var s = new RigState { Prop = p, Def = AssetDatabase.LoadAssetAtPath<HeldPropMeshDef>(p.AssetPath) };
                try
                {
                    using IRigScriptHost host = RigScriptHostFactory.Create();
                    s.Data = RigMeshExtractor.ExtractFrom(host, p.ScriptPath, p.GlobalName, p.Extraction);
                    s.RigFaces = (int)host.EvaluateNumber($"{p.HeldCall("faces")}.length");
                    RigPropMeshBaker.DropRepeatedCorners(s.Data.Faces);
                    s.Grip = ReadVector3(host, p.HeldCall("grip"));
                    s.Lines = host.EvaluateString(
                        $"(function(){{var L={p.HeldCall("lines")};" +
                        "return L.ramps.map(function(r){return r.join(',');}).join(';')+'@'+" +
                        "L.lines.map(function(l){return [l.name,l.bias,l.dots?1:0,l.pts.map(function(q){" +
                        "return q.join(',');}).join('/')].join('|');}).join('&');})()");
                }
                catch (Exception e)
                {
                    s.Error = $"{e.GetType().Name}: {e.Message}";
                }
                states.Add(s);
            }
            return states;
        }

        /// <summary>The states whose rig read and whose def is baked. Every other state is a line in
        /// the report and turns the test red.</summary>
        List<RigState> Usable(StringBuilder report, out bool bad)
        {
            bad = false;
            var usable = new List<RigState>();
            foreach (RigState s in States)
            {
                if (s.Error != null)
                {
                    bad = true;
                    report.AppendLine($"  ✗ {s.Prop.PropId}: the rig did not read: {s.Error}");
                }
                else if (s.Def == null || s.Def.Mesh == null)
                {
                    bad = true;
                    report.AppendLine($"  ✗ {s.Prop.PropId}: nothing baked at {s.Prop.AssetPath}.");
                }
                else usable.Add(s);
            }
            return usable;
        }

        // ---- the committed set against the rigs --------------------------------------------------

        [Test]
        public void HeldPropBake_FaceCounts_MatchTheRigs()
        {
            var report = new StringBuilder();
            List<RigState> states = Usable(report, out bool bad);

            // The folder holds the fleet and nothing else: a state its rig stopped making leaves an orphan.
            var expected = new HashSet<string>(States.Select(s => s.Prop.AssetPath), StringComparer.Ordinal);
            if (AssetDatabase.IsValidFolder(RigPropMeshBaker.HeldPropFolder))
                foreach (string guid in AssetDatabase.FindAssets($"t:{nameof(HeldPropMeshDef)}",
                                                                  new[] { RigPropMeshBaker.HeldPropFolder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (expected.Contains(path)) continue;
                    bad = true;
                    report.AppendLine($"  ✗ {path}: no held state of any rig bakes to it, an orphan.");
                }

            foreach (RigState s in states)
            {
                HeldPropMeshDef def = s.Def;
                int verts = s.Data.Faces.Sum(f => f.V.Length);
                int tris = s.Data.Faces.Sum(f => f.V.Length - 2);
                int meshTris = def.Mesh.triangles.Length / 3;

                var wrong = new List<string>();
                if (def.Id != s.Prop.PropId || def.Family != s.Prop.Family || def.Variant != s.Prop.Variant)
                    wrong.Add($"the def names itself {def.Id} ({def.Family}/'{def.Variant}')");
                if (s.Data.Faces.Count != s.RigFaces)
                    wrong.Add($"the extractor read {s.Data.Faces.Count} of the builder's faces");
                if (def.SourceFaceCount != s.RigFaces) wrong.Add($"the def recorded {def.SourceFaceCount} faces");
                if (def.Mesh.vertexCount != verts) wrong.Add($"the mesh has {def.Mesh.vertexCount} verts");
                if (meshTris != tris) wrong.Add($"the mesh has {meshTris} tris");
                if (PinnedFaces.TryGetValue(s.Prop.PropId, out int pinned) && pinned != s.RigFaces)
                    wrong.Add($"the rig now builds {s.RigFaces} faces where {pinned} are pinned");

                report.AppendLine($"  {(wrong.Count == 0 ? "✓" : "✗")} {s.Prop.PropId,-34} rig {s.RigFaces,3} faces → " +
                                  $"{verts,4} verts / {tris,4} tris" +
                                  (wrong.Count == 0 ? "" : $": {string.Join("; ", wrong)}"));
                bad |= wrong.Count > 0;
            }

            foreach (string id in PinnedFaces.Keys)
                if (States.All(s => s.Prop.PropId != id))
                {
                    bad = true;
                    report.AppendLine($"  ✗ {id}: pinned, but no rig makes the state any more.");
                }

            Assert.IsFalse(bad, "The committed held props do not match the rigs they were baked from.\n" +
                                Rebake + "\n\n" + report);
        }

        [Test]
        public void HeldPropBake_RampSlots_UsedOnly_AtMost16()
        {
            Assert.AreEqual(16, HeldPropMeshDef.RampSlots,
                "The facet shader's ramp slots are no longer 16; ADR 0053 and this test's name say 16.");

            var report = new StringBuilder();
            List<RigState> states = Usable(report, out bool bad);
            foreach (RigState s in states)
            {
                HeldPropMeshDef def = s.Def;
                List<RigMaterial> table = s.Data.Materials;

                // Used, in the table's own order: what the faces paint with and nothing else.
                int[] used = s.Data.Faces.Select(f => f.Mat).Distinct().OrderBy(m => m).ToArray();
                string[] names = used.Select(m => m >= 0 && m < table.Count ? table[m].Name : $"#{m}").ToArray();
                var slotOf = new Dictionary<int, int>();
                for (int k = 0; k < used.Length; k++) slotOf[used[k]] = k;

                var wrong = new List<string>();
                if (used.Length == 0 || used.Length > HeldPropMeshDef.RampSlots)
                    wrong.Add($"the faces use {used.Length} ramps");
                if (!def.RampNames.SequenceEqual(names))
                    wrong.Add($"the def keeps [{string.Join(", ", def.RampNames)}]");
                if (def.Ramps.Length != names.Length)
                    wrong.Add($"the def has {def.Ramps.Length} ramps for {names.Length} names");
                else
                    for (int k = 0; k < used.Length; k++)
                        if (used[k] >= 0 && used[k] < table.Count &&
                            (def.Ramps[k].Offset != table[used[k]].Off ||
                             !SameColours(def.Ramps[k].Colors, table[used[k]].Ramp)))
                            wrong.Add($"slot {k} is not the rig's '{names[k]}' ramp");

                // ...and every face paints from its own slot: the mesh's material id, face by face.
                string paint = WrongSlot(def.Mesh, s.Data.Faces, slotOf);
                if (paint != null) wrong.Add(paint);

                report.AppendLine($"  {(wrong.Count == 0 ? "✓" : "✗")} {s.Prop.PropId,-34} {used.Length} of " +
                                  $"{table.Count}: {string.Join(", ", names)}" +
                                  (wrong.Count == 0 ? "" : $": {string.Join("; ", wrong)}"));
                bad |= wrong.Count > 0;
            }

            Assert.IsFalse(bad, "A held prop's ramps are not the ones its faces use, in the rig's order, " +
                                "at most 16.\n" + Rebake + "\n\n" + report);
        }

        [Test]
        public void HeldPropBake_GripAtOrigin()
        {
            var report = new StringBuilder();
            List<RigState> states = Usable(report, out bool bad);
            foreach (RigState s in states)
            {
                bool ok = s.Grip.magnitude <= GripTolerance &&
                          s.Def.GripLocalMeters.magnitude <= GripTolerance &&
                          (s.Def.GripLocalMeters - s.Grip).magnitude <= 1e-6f;
                report.AppendLine($"  {(ok ? "✓" : "✗")} {s.Prop.PropId,-34} rig {Fmt(s.Grip)}, " +
                                  $"committed {Fmt(s.Def.GripLocalMeters)}");
                bad |= !ok;
            }

            Assert.IsFalse(bad,
                "A held prop's grip is not at its mesh origin. The socket pins the ORIGIN to the hand " +
                "(D1), so a grip anywhere else floats the prop off the fist by that much. The rig's pose " +
                "moved the hand: re-aim the held reconstruction's offset, never the presenter.\n\n" + report);
        }

        [Test]
        public void HeldPropBake_SourceSha_MatchesTheRig()
        {
            var report = new StringBuilder();
            bool bad = false;
            var shas = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (RigState s in States)
            {
                HeldProp p = s.Prop;
                if (s.Def == null)
                {
                    bad = true;
                    report.AppendLine($"  ✗ {p.PropId}: nothing baked at {p.AssetPath}.");
                    continue;
                }

                if (!shas.TryGetValue(p.ScriptPath, out string sha))
                    shas[p.ScriptPath] = sha =
                        CharacterSkinExtractor.LfSha256File(Path.Combine(RigCatalog.RepoRoot, p.ScriptPath));

                if (s.Def.SourceRigPath == p.ScriptPath && s.Def.SourceRigSha256 == sha) continue;
                bad = true;
                report.AppendLine($"  ✗ {p.PropId}: baked from {s.Def.SourceRigPath} at " +
                                  $"{s.Def.SourceRigSha256}; the rig is {p.ScriptPath} at {sha}.");
            }

            Assert.IsFalse(bad, "A held prop was baked from a rig that has since changed.\n" + Rebake +
                                "\n\n" + report);
        }

        [Test]
        public void HeldPropBake_PlottedLines_AreTheRigs()
        {
            var report = new StringBuilder();
            List<RigState> states = Usable(report, out bool bad);
            foreach (RigState s in states)
            {
                HeldPropMeshDef def = s.Def;
                var wrong = new List<string>();

                string[] halves = s.Lines.Split('@');
                string[] ramps = halves[0].Length == 0 ? Array.Empty<string>() : halves[0].Split(';');
                string[] lines = halves.Length < 2 || halves[1].Length == 0 ? Array.Empty<string>() : halves[1].Split('&');

                if (def.LineRamps.Length != ramps.Length)
                    wrong.Add($"{def.LineRamps.Length} line ramps, the rig's lines paint from {ramps.Length}");
                else
                    for (int r = 0; r < ramps.Length; r++)
                        if (def.LineRamps[r].Offset != 0 ||
                            !SameColours(def.LineRamps[r].Colors, ramps[r].Split(',').Select(Hex).ToArray()))
                            wrong.Add($"line ramp {r} is not the rig's");

                if (def.Lines.Length != lines.Length)
                    wrong.Add($"{def.Lines.Length} lines, the rig plots {lines.Length}");
                for (int i = 0; i < Math.Min(lines.Length, def.Lines.Length); i++)
                {
                    string why = WrongLine(def.Lines[i], lines[i]);
                    if (why != null) wrong.Add(why);
                }

                // The bail, by count: the pail and the tote plot it, the tray has none.
                if (def.Family == HeldPropMeshDef.BucketFamily)
                {
                    int bail = def.Lines.Where(l => l.Name == BailLine).Sum(l => l.Points?.Length ?? 0);
                    int want = def.BucketTierId == TrayTier ? 0 : BailPoints;
                    if (bail != want) wrong.Add($"the bail has {bail} points where {want} are expected");
                }

                report.AppendLine($"  {(wrong.Count == 0 ? "✓" : "✗")} {s.Prop.PropId,-34} " +
                                  string.Join(", ", def.Lines.Select(l => $"{l.Name} × {l.Points?.Length ?? 0}")) +
                                  (wrong.Count == 0 ? "" : $": {string.Join("; ", wrong)}"));
                bad |= wrong.Count > 0;
            }

            Assert.IsFalse(bad, "A held prop's plotted lines (D2) are not the rig's.\n" + Rebake + "\n\n" + report);
        }

        // ---- what points at the meshes ------------------------------------------------------------

        [Test]
        public void HeldPropBake_IsWired_ToTheToolsAndTheCatchLibrary()
        {
            List<HeldPropMeshDef> defs = States.Select(s => s.Def).ToList();
            Assert.IsTrue(defs.All(d => d != null), $"Not every held state is baked. {Rebake}");
            HeldPropMeshDef[] Family(string family) => defs.Where(d => d.Family == family).ToArray();

            ToolDef[] tools = LoadAll<ToolDef>();
            foreach ((string toolId, string family) in new[]
                     {
                         (ToolDef.RodId, HeldPropMeshDef.RodFamily),
                         (ToolDef.ShovelId, HeldPropMeshDef.ShovelFamily),
                     })
            {
                ToolDef[] matches = tools.Where(t => t.Id == toolId).ToArray();
                Assert.AreEqual(1, matches.Length, $"Expected one ToolDef with the id {toolId}.");
                CollectionAssert.AreEqual(Family(family), matches[0].HeldMeshes,
                    $"{toolId} does not wear every '{family}' state in the rig's order. {Rebake}");
            }

            CatchItemLibrary[] libraries = LoadAll<CatchItemLibrary>();
            Assert.AreEqual(1, libraries.Length, "Expected one CatchItemLibrary under Data/.");
            CatchItemLibrary library = libraries[0];

            foreach (HeldPropMeshDef fish in Family(HeldPropMeshDef.FishFamily))
                Assert.AreSame(fish, library.HeldMeshFor(fish.Variant),
                    $"The catch library's '{fish.Variant}' does not hold {fish.Id}. {Rebake}");

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (HeldPropMeshDef bucket in Family(HeldPropMeshDef.BucketFamily))
            {
                Assert.IsTrue(Enum.TryParse(bucket.BucketFill, true, out CatchFillBand band) &&
                              string.Equals(band.ToString(), bucket.BucketFill, StringComparison.OrdinalIgnoreCase),
                    $"{bucket.Id}: the fill '{bucket.BucketFill}' is not a {nameof(CatchFillBand)}.");
                Assert.IsTrue(seen.Add($"{bucket.BucketTierId}|{band}|{bucket.BucketCatch}"),
                    $"{bucket.Id}: two bucket states share {bucket.BucketTierId}/{band}/{bucket.BucketCatch}.");
                Assert.AreSame(bucket, library.BucketMesh(bucket.BucketTierId, band, bucket.BucketCatch),
                    $"The catch library does not find {bucket.Id} by its tier, band and catch. {Rebake}");
            }
        }

        // ---- the bake's two new steps, without a rig ----------------------------------------------

        [Test]
        public void DropRepeatedCorners_DropsARepeatAfterTheNormal_AndTheWrap()
        {
            Vector3d a = P(0, 0, 0), b = P(1, 0, 0), c = P(1, 1, 0), d = P(0, 1, 0);
            var faces = new List<RigFace>
            {
                Face(0, a, b, c, c),      // a triangle stored as a quad: the fish's nose and fins
                Face(0, a, b, c, d, a),   // a quad that closes on its first corner
                Face(0, a, b, c, d),      // nothing to drop
            };

            Assert.AreEqual(2, RigPropMeshBaker.DropRepeatedCorners(faces));
            CollectionAssert.AreEqual(new[] { a, b, c }, faces[0].V);
            CollectionAssert.AreEqual(new[] { a, b, c, d }, faces[1].V);
            CollectionAssert.AreEqual(new[] { a, b, c, d }, faces[2].V);
        }

        [Test]
        public void DropRepeatedCorners_RefusesARepeatInsideTheNormal()
        {
            Vector3d a = P(0, 0, 0), b = P(1, 0, 0), c = P(1, 1, 0);

            // The rig takes the normal from the first three corners; a repeat there is a zero normal
            // it really shades with, and dropping the repeat would re-light the face.
            Assert.Throws<InvalidOperationException>(() =>
                RigPropMeshBaker.DropRepeatedCorners(new List<RigFace> { Face(0, a, a, b, c) }));
            Assert.Throws<InvalidOperationException>(() =>
                RigPropMeshBaker.DropRepeatedCorners(new List<RigFace> { Face(0, a, b, b, c) }));
            Assert.Throws<InvalidOperationException>(() =>
                RigPropMeshBaker.DropRepeatedCorners(new List<RigFace> { Face(0, a, b, a) }));
        }

        [Test]
        public void UsedRampsOnly_KeepsThePaintedRamps_InTheTablesOrder()
        {
            Vector3d a = P(0, 0, 0), b = P(1, 0, 0), c = P(1, 1, 0);
            var data = new RigMeshData
            {
                Materials = new List<RigMaterial> { Mat("grip"), Mat("blk"), Mat("reel"), Mat("knob") },
                Faces = new List<RigFace> { Face(2, a, b, c), Face(0, a, b, c), Face(2, a, b, c) },
            };

            Assert.AreEqual(2, RigPropMeshBaker.UsedRampsOnly(data, HeldPropMeshDef.RampSlots));
            CollectionAssert.AreEqual(new[] { "grip", "reel" }, data.Materials.Select(m => m.Name).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 0, 1 }, data.Faces.Select(f => f.Mat).ToArray());

            var crowded = new RigMeshData
            {
                Materials = new List<RigMaterial> { Mat("grip"), Mat("blk") },
                Faces = new List<RigFace> { Face(0, a, b, c), Face(1, a, b, c) },
            };
            Assert.Throws<InvalidOperationException>(() => RigPropMeshBaker.UsedRampsOnly(crowded, 1));

            var room = new RigMeshData
            {
                Materials = new List<RigMaterial> { Mat("grip") },
                Faces = new List<RigFace> { Face(0, a, b, c) },
            };
            room.Faces[0].Interior = true;
            Assert.Throws<InvalidOperationException>(() =>
                RigPropMeshBaker.UsedRampsOnly(room, HeldPropMeshDef.RampSlots));
        }

        // ---- helpers ------------------------------------------------------------------------------

        static Vector3d P(double x, double y, double z) => new Vector3d(x, y, z);

        static RigFace Face(int mat, params Vector3d[] v) => new RigFace { V = v, Mat = mat };

        static RigMaterial Mat(string name) => new RigMaterial
        {
            Name = name,
            Ramp = new[] { new Color32(0, 0, 0, 255) },
            RampHex = new[] { "#000000" },
        };

        static T[] LoadAll<T>() where T : UnityEngine.Object =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets/_Project/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(t => t != null)
                .ToArray();

        /// <summary>The first vertex whose material id is not its face's slot, or null. The builder
        /// writes each face's corners in order, face after face.</summary>
        static string WrongSlot(Mesh mesh, List<RigFace> faces, Dictionary<int, int> slotOf)
        {
            var uv = new List<Vector4>();
            mesh.GetUVs(RigMeshBuilder.AttrUvChannel, uv);
            int corners = faces.Sum(f => f.V.Length);
            if (uv.Count != corners) return $"the mesh carries {uv.Count} material ids for {corners} corners";

            int v = 0;
            for (int i = 0; i < faces.Count; i++)
            {
                int want = slotOf[faces[i].Mat];
                for (int k = 0; k < faces[i].V.Length; k++, v++)
                {
                    int got = Mathf.RoundToInt(uv[v].x);
                    if (got != want) return $"face {i} paints from slot {got}, its material is slot {want}";
                }
            }
            return null;
        }

        /// <summary>Why a committed line is not the rig's <c>name|bias|dots|points</c>, or null.</summary>
        static string WrongLine(HeldPropMeshDef.Line line, string rig)
        {
            string[] f = rig.Split('|');
            if (f.Length != 4) return $"the rig's line '{rig}' did not parse";
            if (line.Name != f[0] || line.Dots != (f[2] == "1") ||
                Mathf.Abs(line.DepthBiasMeters - float.Parse(f[1], CultureInfo.InvariantCulture)) > 1e-6f)
                return $"line '{line.Name}' is not the rig's '{f[0]}'";

            string[] pts = f[3].Length == 0 ? Array.Empty<string>() : f[3].Split('/');
            int have = line.Points?.Length ?? 0;
            if (have != pts.Length) return $"'{f[0]}' has {have} points, the rig plots {pts.Length}";
            for (int k = 0; k < pts.Length; k++)
            {
                string[] q = pts[k].Split(',');
                HeldPropMeshDef.LinePoint at = line.Points[k];
                var pos = new Vector3(float.Parse(q[0], CultureInfo.InvariantCulture),
                                      float.Parse(q[1], CultureInfo.InvariantCulture),
                                      float.Parse(q[2], CultureInfo.InvariantCulture));
                if ((at.Position - pos).magnitude > 1e-6f ||
                    at.Ramp != int.Parse(q[3], CultureInfo.InvariantCulture) ||
                    at.Index != int.Parse(q[4], CultureInfo.InvariantCulture) ||
                    at.SecondIndex != int.Parse(q[5], CultureInfo.InvariantCulture))
                    return $"'{f[0]}' point {k} is not the rig's";
            }
            return null;
        }

        /// <summary><c>#rrggbb</c> or <c>#rrggbbaa</c>, as the rigs write their ramps.</summary>
        static Color32 Hex(string hex)
        {
            string h = hex.TrimStart('#');
            uint n = uint.Parse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (h.Length == 6) n = (n << 8) | 0xFF;
            return new Color32((byte)(n >> 24), (byte)(n >> 16), (byte)(n >> 8), (byte)n);
        }

        static bool SameColours(Color32[] a, Color32[] b) =>
            a != null && b != null && a.Length == b.Length &&
            a.Zip(b, (x, y) => x.r == y.r && x.g == y.g && x.b == y.b && x.a == y.a).All(same => same);

        static Vector3 ReadVector3(IRigScriptHost host, string expr)
        {
            string[] parts = host.EvaluateString($"(function(){{var v={expr};return v.join(',');}})()").Split(',');
            if (parts.Length != 3) throw new InvalidOperationException($"{expr} is not [x,y,z].");
            return new Vector3(float.Parse(parts[0], CultureInfo.InvariantCulture),
                               float.Parse(parts[1], CultureInfo.InvariantCulture),
                               float.Parse(parts[2], CultureInfo.InvariantCulture));
        }

        static string Fmt(Vector3 v) =>
            $"({v.x.ToString("0.######", CultureInfo.InvariantCulture)}, " +
            $"{v.y.ToString("0.######", CultureInfo.InvariantCulture)}, " +
            $"{v.z.ToString("0.######", CultureInfo.InvariantCulture)}) m";
    }
}
