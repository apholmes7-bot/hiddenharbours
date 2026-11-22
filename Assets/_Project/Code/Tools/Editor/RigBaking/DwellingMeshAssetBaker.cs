#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>Turns a parked dwelling's shell and room rigs into a committed <see cref="DwellingMeshDef"/>
    /// (ADR 0060 section 2, option B).</b> The third door onto the facet renderer, beside
    /// <see cref="RigMeshAssetBaker"/> and <see cref="VehicleMeshAssetBaker"/>: the mesh is built by the
    /// same <see cref="RigMeshBuilder"/>, the room follows the same extraction rules
    /// (<see cref="RigRoomExtraction"/>), and nothing about the shader changes.
    ///
    /// <list type="number">
    ///   <item><b>The shell</b> is the rig's own <c>build()</c> with its stale <c>buildInterior()</c>
    ///   prefix removed. The prefix is measured, not transcribed: the bake builds
    ///   <c>buildInterior()</c> on its own, asserts that <c>build()</c> starts with exactly those faces,
    ///   and drops that many. The room rig supersedes that prefix (its <c>_confirm.shell_layout</c>).</item>
    ///   <item><b>The room</b> is the room rig's <c>faces()</c> as the union over the eight facings of
    ///   its own <c>cutaway(dir).keep</c>, with the ceiling (the rig's <c>lid</c>, always culled) and
    ///   the section caps (<c>capOf</c>, the analogue of the boats' <c>cut</c> lip) dropped. The facet
    ///   shader's back-face test does the sectioning continuously.</item>
    ///   <item><b>The tags.</b> Shell faces above the published floor take the shell lid tag, faces
    ///   at or below it are exterior (tag 0). Every room face takes the main level's tag; a room face
    ///   that overlaps a secondary level (the wet bath) is ALSO emitted with that level's tag, so either
    ///   level, when cut, shows a closed room. See <see cref="DwellingMeshBakeRules.ShellLidTag"/>.</item>
    ///   <item><b>The room data</b> (levels, threshold, fixtures) is read off the room sidecar by
    ///   <see cref="DwellingRoomSidecar"/>, after its shell stamp is checked against the shell rig.</item>
    /// </list>
    ///
    /// <para><b>Non-destructive re-bakes:</b> the asset is refreshed in place (same guid); only its mesh
    /// sub-asset is replaced. ⚠️ <c>docs/art/rigs/**</c> is read-only here.</para>
    /// </summary>
    public static class DwellingMeshAssetBaker
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        [MenuItem(RigMeshGate.MenuRoot + "/Bake ALL dwelling meshes", priority = 231)]
        public static void BakeAll() => BakeAllInternal();

        [MenuItem(RigMeshGate.MenuRoot + "/Bake ALL dwelling meshes", validate = true)]
        static bool BakeAllValidate() => RigMeshGate.Enabled;

        /// <summary>Headless entry (-executeMethod).</summary>
        public static void BakeAllCli()
        {
            try
            {
                int failed = BakeAllInternal();
                if (failed > 0)
                {
                    Debug.LogError($"[rig-dwelling] CLI bake FAILED: {failed} dwelling(s) did not bake.");
                    EditorApplication.Exit(1);
                    return;
                }
                Debug.Log("[rig-dwelling] CLI bake OK.");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[rig-dwelling] CLI bake FAILED: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Returns the number that failed; reports every dwelling either way.</summary>
        static int BakeAllInternal()
        {
            var report = new StringBuilder($"[rig-dwelling] bake: {DwellingRigFleet.MeshBaked.Count} dwelling(s)\n");
            var failures = new List<string>();
            foreach (DwellingRigFleet.MeshDwelling d in DwellingRigFleet.MeshBaked)
            {
                try
                {
                    DwellingMeshDef def = Bake(d, out DwellingExtraction x);
                    report.Append($"  ok {d.Key} -> {def.Id}\n").Append(x.Report);
                }
                catch (Exception e)
                {
                    failures.Add(d.Key);
                    report.Append($"  FAILED {d.Key}: {e.Message}\n");
                }
            }
            if (failures.Count == 0) Debug.Log(report.ToString());
            else Debug.LogError(report.Append($"\n  {failures.Count} FAILED: {string.Join(", ", failures)}").ToString());
            return failures.Count;
        }

        // =============================================================================================
        //  EXTRACTION (no asset database: the freshness test calls this too)
        // =============================================================================================

        /// <summary>Everything one dwelling's rigs and sidecars say, ready to build and write.</summary>
        public sealed class DwellingExtraction
        {
            public RigMeshData Data;
            public DwellingRoomSidecar Room;
            public AzimuthConvention Azimuth;
            public int ShellFaces, RoomFaces, SecondaryFaces;
            public int ShellTris, RoomTris, SecondaryTris;
            public int TexturedFaces, SmoothNormalFaces, FlatFaces;
            public List<string> PolishedMaterials = new List<string>();
            public int[] FacingHistogram = new int[9];
            public int DroppedLids, DroppedCaps;
            public double DepthShift;
            public string Report = "";
        }

        /// <summary>
        /// Run both rigs in a fresh host and extract one dwelling. Throws with the named reason on every
        /// refusal (ADR 0060 section 2).
        /// </summary>
        public static DwellingExtraction Extract(in DwellingRigFleet.MeshDwelling d)
        {
            if (!DwellingRigFleet.TryGetDwelling(d.Key, out DwellingRigFleet.Dwelling shell))
                throw new InvalidOperationException(
                    $"'{d.Key}' is mesh-baked but not in DwellingRigFleet.Dwellings, so it has no shell rig or sidecar.");

            string repo = RigCatalog.RepoRoot;
            string shellRigFull = Path.Combine(repo, shell.ScriptPath);
            string roomRigFull = Path.Combine(repo, d.RoomScriptPath);
            byte[] shellBytes = File.ReadAllBytes(shellRigFull);
            byte[] roomBytes = File.ReadAllBytes(roomRigFull);

            // --- the room sidecar, checked BEFORE anything is read off it ------------------------------
            DwellingRoomSidecar room = DwellingRoomSidecar.Read(
                File.ReadAllText(Path.Combine(repo, d.RoomSidecarPath)), d.RoomSidecarPath);
            Refuse(d, DwellingMeshBakeRules.ShellStampProblem(room.ShellRigSha, shellBytes, shell.ScriptPath));
            Refuse(d, DwellingMeshBakeRules.RoomStampProblem(room.RoomRigSha, roomBytes, d.RoomScriptPath));
            Refuse(d, DwellingMeshBakeRules.FirstRoomRefusal(room));

            // --- the floor height, from the SHELL's own sidecar (ADR 0060: shell faces are tagged from
            //     the shell's own published data), and it must agree with the room's --------------------
            double soleZ = ReadShellSoleZ(d, shell);
            if (Math.Abs(soleZ - room.SoleZ) > 1e-4)
                Refuse(d, $"the shell sidecar puts the floor at {soleZ} m and the room sidecar at {room.SoleZ} m. " +
                          "The two rigs disagree about where the floor is; a face tagged against either would " +
                          "be wrong for the other.");

            using IRigScriptHost host = RigScriptHostFactory.Create();
            host.Execute(WidenShellRig(File.ReadAllText(shellRigFull), shell.GlobalName));
            host.Execute(File.ReadAllText(roomRigFull));
            host.Execute(Helpers(shell.GlobalName, d.RoomGlobalName));

            var x = new DwellingExtraction { Room = room };
            x.Azimuth = MeasureAzimuth(host, d, room);

            var data = new RigMeshData
            {
                RigKey = d.Key,
                GlobalName = shell.GlobalName,
                SourceFaceExpression = d.ViewOptions,
            };
            ReadCellAndShade(host, d, data);

            // --- the shell ---------------------------------------------------------------------------
            var shellTable = new RigRoomExtraction.MaterialTable();
            var shellFaces = new List<RigFace>();
            ParseShell(d, host.EvaluateString($"globalThis.__HHD.shell({d.ViewOptions})"), soleZ,
                       shellTable, shellFaces, x);
            if (shellTable.Materials.Count > HullMeshDef.HullRampSlots)
                Refuse(d, $"the shell paints {shellTable.Materials.Count} ramps and the hull table holds " +
                          $"{HullMeshDef.HullRampSlots}.");

            // --- the room ----------------------------------------------------------------------------
            x.DepthShift = RigRoomExtraction.BoundingDiameter(shellFaces);
            var roomTable = new RigRoomExtraction.MaterialTable();
            var roomFaces = new List<RigFace>();
            ParseRoom(d, host.EvaluateString($"globalThis.__HHD.room({d.ViewOptions})"), room, x.DepthShift,
                      roomTable, roomFaces, x);
            Refuse(d, DwellingMeshBakeRules.InteriorRampProblem(d.Key, roomTable.Materials.Count));

            if (x.TexturedFaces > 0 && string.IsNullOrEmpty(d.DeferredSurfaceDetail))
                Refuse(d, $"{x.TexturedFaces} faces carry a procedural texture the facet shader does not have. " +
                          "Dropping it in silence is not allowed: declare DeferredSurfaceDetail on the fleet entry " +
                          "with the reason, or carry the generator.");

            data.Faces.AddRange(shellFaces);
            data.Faces.AddRange(roomFaces);
            data.Materials = shellTable.Materials;
            data.InteriorMaterials = roomTable.Materials;
            var ids = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (DwellingLevel l in room.Levels) ids[l.LevelId] = l.Tag;
            ids[DwellingMeshBakeRules.ShellLidLevelId] = DwellingMeshBakeRules.ShellLidTag;
            data.LevelIds = ids;
            x.Data = data;
            x.Report = BuildReport(d, x, shellTable, roomTable);
            return x;
        }

        static void Refuse(in DwellingRigFleet.MeshDwelling d, string problem)
        {
            if (problem != null) throw new InvalidOperationException($"{d.Key}: {problem}");
        }

        static double ReadShellSoleZ(in DwellingRigFleet.MeshDwelling d, in DwellingRigFleet.Dwelling shell)
        {
            object doc = DeckSidecarJson.Parse(File.ReadAllText(Path.Combine(RigCatalog.RepoRoot, shell.SidecarPath)));
            var soles = DeckSidecarJson.AsArray(DeckSidecarJson.Member(doc, "SOLE"));
            if (soles == null || soles.Count == 0 ||
                !DeckSidecarJson.TryDouble(DeckSidecarJson.Member(soles[0], "z"), out double z))
                throw new InvalidOperationException(
                    $"{d.Key}: {shell.SidecarPath} publishes no SOLE[0].z, so no shell face can be tagged " +
                    "above or below the floor. A room tagged level 0 is never cut and never hidden (ADR 0041).");
            return z;
        }

        /// <summary>
        /// Widen the shell rig's export so its closure's <c>build</c>, <c>buildInterior</c> and
        /// <c>makeMats</c> are reachable, as <see cref="BoatInteriorGeometryExtractor.WidenInteriorRig"/>
        /// does for the room rig. <c>LastIndexOf</c>, because these rigs describe their own export in a
        /// header comment.
        /// </summary>
        public static string WidenShellRig(string source, string globalName)
        {
            string marker = $"root.{globalName} = {{";
            int at = source.LastIndexOf(marker, StringComparison.Ordinal);
            if (at < 0)
                throw new InvalidOperationException(
                    $"the shell rig no longer installs `{marker}`, so the bake cannot reach build(), " +
                    "buildInterior() and makeMats(). A bake must not proceed on a rig it cannot address.");
            return source.Insert(at + marker.Length,
                " build:build, buildInterior:buildInterior, makeMats:makeMats,");
        }

        static string Helpers(string shellGlobal, string roomGlobal) => @"
globalThis.__HHD = (function(){
  var C = globalThis." + shellGlobal + @", R = globalThis." + roomGlobal + @";
  function key(f){ var k = f.mat + '|';
    for (var j = 0; j < f.v.length; j++) k += f.v[j][0].toFixed(6)+','+f.v[j][1].toFixed(6)+','+f.v[j][2].toFixed(6)+';';
    return k; }
  function num(x){ return (typeof x === 'number' && x === x) ? x : 0; }
  function verts(L, f){ for (var j = 0; j < f.v.length; j++) L.push('V ' + f.v[j][0] + ' ' + f.v[j][1] + ' ' + f.v[j][2]); }

  function shell(opts){
    var s = C.resolve(opts), L = [];
    var inner = []; C.buildInterior(inner, s);
    var all = C.build(s);
    for (var i = 0; i < inner.length; i++)
      if (key(inner[i]) !== key(all[i]))
        throw new Error('build() no longer begins with buildInterior() (face ' + i + '); the stale room prefix cannot be removed by count');
    var faces = all.slice(inner.length), MM = C.makeMats(s), seen = {}, order = [];
    for (var i = 0; i < faces.length; i++){
      var m = faces[i].mat;
      if (!MM[m]) throw new Error('shell face material ' + m + ' is not in makeMats(); paint() would fall back to skin');
      if (!seen[m]) { seen[m] = 1; order.push(m); }
    }
    L.push('SWING ' + num(s.swing));
    L.push('INNER ' + inner.length);
    for (var i = 0; i < order.length; i++)
      L.push('MAT ' + order[i] + ' ' + num(MM[order[i]].polish) + ' ' + MM[order[i]].ramp.join(' '));
    for (var i = 0; i < faces.length; i++){
      var f = faces[i];
      L.push('F ' + f.mat + ' ' + num(f.b) + ' ' + num(f.db) + ' ' + (f.tex ? 1 : 0) + ' ' + (f.vn ? 1 : 0)
             + ' ' + (f.flat ? 1 : 0) + ' ' + f.v.length);
      verts(L, f);
    }
    return L.join('\n');
  }

  function room(opts){
    var faces = R.faces(opts), L = [], hist = [0,0,0,0,0,0,0,0,0], lids = 0, caps = 0, keeps = [];
    for (var d = 0; d < 8; d++) keeps.push(R.cutaway(d).keep);
    for (var k in R.MATS) L.push('MAT ' + k + ' 0 ' + R.MATS[k].join(' '));
    L.push('WALL ' + R.HEAD.wall);
    var kept = [];
    for (var i = 0; i < faces.length; i++){
      var f = faces[i];
      if (f.lid) { lids++; continue; }
      if (f.capOf) { caps++; continue; }
      var n = 0; for (var d = 0; d < 8; d++) if (keeps[d](f)) n++;
      hist[n]++;
      if (n > 0) kept.push(f);
    }
    L.push('HIST ' + hist.join(' '));
    L.push('DROPPED ' + lids + ' ' + caps);
    for (var i = 0; i < kept.length; i++){
      var f = kept[i];
      L.push('F ' + f.mat + ' ' + num(f.b) + ' ' + num(f.db) + ' ' + (f.tex ? 1 : 0) + ' 0 0 ' + f.v.length);
      verts(L, f);
    }
    return L.join('\n');
  }

  function cell(){
    var sh = C.loft.shade, B = [];
    for (var i = 0; i < 4; i++) for (var j = 0; j < 4; j++) B.push(sh.BAYER[i][j]);
    var sameCell = R.W === C.W && R.H === C.H && R.pivot.x === C.pivot.x && R.pivot.y === C.pivot.y && R.PX === C.PX;
    return ['CELL', C.W, C.H, C.pivot.x, C.pivot.y, C.PX, C.defaultElev, sameCell ? 1 : 0].join(' ')
      + '\nSHADE ' + sh.GAIN + ' ' + sh.BIAS + ' ' + sh.LN.join(' ') + ' ' + sh.KEY
      + '\nBAYER ' + B.join(' ');
  }

  // Two screen readings at a quarter turn: the nose's screen x and the curb side's screen y.
  function azimuth(opts){
    var o = C.project(2, [0, 0, 0]), n = C.project(2, [0, 1, 0]), c = C.project(2, [1, 0, 0]);
    return (n.x - o.x) + ' ' + (c.y - o.y);
  }
  return { shell: shell, room: room, cell: cell, azimuth: azimuth };
})();
";

        static void ReadCellAndShade(IRigScriptHost host, in DwellingRigFleet.MeshDwelling d, RigMeshData data)
        {
            string[] lines = host.EvaluateString("globalThis.__HHD.cell()").Split('\n');
            string[] c = lines[0].Split(' ');
            data.W = int.Parse(c[1], Inv);
            data.H = int.Parse(c[2], Inv);
            data.PivotX = double.Parse(c[3], Inv);
            data.PivotY = double.Parse(c[4], Inv);
            data.PxPerMetre = int.Parse(c[5], Inv);
            data.DefaultElev = double.Parse(c[6], Inv);
            if (c[7] != "1")
                Refuse(d, "the room rig's cell (W, H, pivot, PX) is not the shell's. One mesh has one cell, and a " +
                          "room drawn on another cell would not sit inside its shell.");

            string[] s = lines[1].Split(' ');
            data.Gain = double.Parse(s[1], Inv);
            data.Bias = double.Parse(s[2], Inv);
            data.LightN = new Vector3d(double.Parse(s[3], Inv), double.Parse(s[4], Inv), double.Parse(s[5], Inv));
            data.Keyline = RigRoomExtraction.ParseHex(s[6]);

            string[] b = lines[2].Split(' ');
            if (b.Length != 17) Refuse(d, $"the rig's BAYER holds {b.Length - 1} entries, not 16.");
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    data.Bayer[i, j] = double.Parse(b[1 + i * 4 + j], Inv);
            data.BayerWasExported = true;
        }

        static void ParseShell(in DwellingRigFleet.MeshDwelling d, string payload, double soleZ,
                               RigRoomExtraction.MaterialTable table, List<RigFace> faces, DwellingExtraction x)
        {
            string[] lines = payload.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string[] t = lines[i].Split(' ');
                switch (t[0])
                {
                    case "SWING":
                        if (double.Parse(t[1], Inv) != 0.0)
                            Refuse(d, $"the shell resolves with its door at swing {t[1]}. The mesh bakes the door " +
                                      "shut; the moving leaf is a facet prop (ADR 0060 section 4).");
                        break;
                    case "MAT":
                        table.IndexFor(t[1], t.Skip(3).ToArray());
                        if (double.Parse(t[2], Inv) != 0.0 && !x.PolishedMaterials.Contains(t[1]))
                            x.PolishedMaterials.Add(t[1]);
                        break;
                    case "F":
                    {
                        RigFace f = ReadFace(t, lines, ref i, table, d, "shell");
                        if (t[4] == "1") x.TexturedFaces++;
                        if (t[5] == "1") x.SmoothNormalFaces++;
                        if (t[6] == "1") x.FlatFaces++;
                        double cz = 0;
                        foreach (Vector3d v in f.V) cz += v.Z;
                        cz /= f.V.Length;
                        f.Level = DwellingMeshBakeRules.ShellTagFor(cz, soleZ);
                        f.LevelName = f.Level == DwellingMeshBakeRules.ShellLidTag
                            ? DwellingMeshBakeRules.ShellLidLevelId : "exterior";
                        faces.Add(f);
                        x.ShellFaces++;
                        x.ShellTris += f.V.Length - 2;
                        break;
                    }
                }
            }
        }

        static void ParseRoom(in DwellingRigFleet.MeshDwelling d, string payload, DwellingRoomSidecar room,
                              double shift, RigRoomExtraction.MaterialTable table, List<RigFace> faces,
                              DwellingExtraction x)
        {
            var ramps = new Dictionary<string, string[]>(StringComparer.Ordinal);
            float wall = 0f;
            string[] lines = payload.Split('\n');
            DwellingLevel main = room.Levels[0];
            for (int i = 0; i < lines.Length; i++)
            {
                string[] t = lines[i].Split(' ');
                switch (t[0])
                {
                    case "MAT":
                        ramps[t[1]] = t.Skip(3).ToArray();
                        break;
                    case "WALL":
                        wall = float.Parse(t[1], Inv);
                        if (!(wall > 0f))
                            Refuse(d, "the room rig publishes no HEAD.wall thickness, so a secondary level's " +
                                      "partitions cannot be found to draw with it.");
                        break;
                    case "HIST":
                        for (int k = 1; k < t.Length && k <= 9; k++) x.FacingHistogram[k - 1] = int.Parse(t[k], Inv);
                        break;
                    case "DROPPED":
                        x.DroppedLids = int.Parse(t[1], Inv);
                        x.DroppedCaps = int.Parse(t[2], Inv);
                        break;
                    case "F":
                    {
                        string mat = t[1];
                        if (!table.TryGetIndex(mat, out _))
                        {
                            if (!ramps.TryGetValue(mat, out string[] ramp))
                                Refuse(d, $"room face material '{mat}' is not in the room rig's MATS. Its paint() " +
                                          "would fall back to 'liner'; a bake must not.");
                            table.IndexFor(mat, ramp);
                        }
                        RigFace f = ReadFace(t, lines, ref i, table, d, "room");
                        if (t[4] == "1") x.TexturedFaces++;
                        f.Db += shift;
                        f.Interior = true;
                        f.Level = main.Tag;
                        f.LevelName = main.LevelId;
                        faces.Add(f);
                        x.RoomFaces++;
                        x.RoomTris += f.V.Length - 2;

                        PlanBounds(f, out Vector2 min, out Vector2 max);
                        for (int l = 1; l < room.Levels.Length; l++)
                        {
                            DwellingLevel level = room.Levels[l];
                            if (!DwellingMeshBakeRules.OverlapsLevel(min, max, level.Floor, wall)) continue;
                            faces.Add(new RigFace
                            {
                                V = f.V, Mat = f.Mat, B = f.B, Db = f.Db, Interior = true,
                                Level = level.Tag, LevelName = level.LevelId,
                            });
                            x.SecondaryFaces++;
                            x.SecondaryTris += f.V.Length - 2;
                        }
                        break;
                    }
                }
            }
        }

        static RigFace ReadFace(string[] t, string[] lines, ref int i, RigRoomExtraction.MaterialTable table,
                                in DwellingRigFleet.MeshDwelling d, string what)
        {
            if (!table.TryGetIndex(t[1], out int mat))
                Refuse(d, $"{what} face material '{t[1]}' has no ramp.");
            int nv = int.Parse(t[7], Inv);
            if (nv < 3) Refuse(d, $"a {what} face has {nv} vertices.");
            var verts = new Vector3d[nv];
            for (int k = 0; k < nv; k++)
            {
                string[] vt = lines[++i].Split(' ');
                verts[k] = new Vector3d(double.Parse(vt[1], Inv), double.Parse(vt[2], Inv), double.Parse(vt[3], Inv));
            }
            return new RigFace
            {
                V = verts,
                Mat = mat,
                B = double.Parse(t[2], Inv),
                Db = double.Parse(t[3], Inv),
            };
        }

        static void PlanBounds(RigFace f, out Vector2 min, out Vector2 max)
        {
            min = new Vector2(float.MaxValue, float.MaxValue);
            max = new Vector2(float.MinValue, float.MinValue);
            foreach (Vector3d v in f.V)
            {
                var p = new Vector2((float)v.X, (float)v.Y);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
        }

        /// <summary>
        /// The azimuth convention from two screen readings at a quarter turn: the nose's screen x
        /// (west = counter-clockwise) and the curb side's screen y (north = counter-clockwise). They must
        /// agree with each other and with the room sidecar's stated convention.
        /// </summary>
        static AzimuthConvention MeasureAzimuth(IRigScriptHost host, in DwellingRigFleet.MeshDwelling d,
                                                DwellingRoomSidecar room)
        {
            string[] a = host.EvaluateString($"globalThis.__HHD.azimuth({d.ViewOptions})").Split(' ');
            double noseDx = double.Parse(a[0], Inv), curbDy = double.Parse(a[1], Inv);
            AzimuthConvention byNose = noseDx > 0 ? AzimuthConvention.Clockwise : AzimuthConvention.CounterClockwise;
            AzimuthConvention byCurb = curbDy > 0 ? AzimuthConvention.Clockwise : AzimuthConvention.CounterClockwise;
            if (Math.Abs(noseDx) < 1e-6 || Math.Abs(curbDy) < 1e-6 || byNose != byCurb)
                Refuse(d, $"the azimuth readings disagree or vanish (nose dx {noseDx:F2} px, curb dy {curbDy:F2} px " +
                          "at a quarter turn). Render it beside a registered reference before baking.");
            if (!string.IsNullOrEmpty(room.Convention) &&
                !string.Equals(room.Convention, byNose.ToString(), StringComparison.Ordinal))
                Refuse(d, $"the room sidecar says {room.Convention} and the rig measures {byNose}.");
            return byNose;
        }

        static string BuildReport(in DwellingRigFleet.MeshDwelling d, DwellingExtraction x,
                                  RigRoomExtraction.MaterialTable shell, RigRoomExtraction.MaterialTable room)
        {
            var sb = new StringBuilder();
            int total = x.ShellTris + x.RoomTris + x.SecondaryTris;
            sb.Append($"      tris: shell {x.ShellTris} + room {x.RoomTris} + secondary levels {x.SecondaryTris} = {total}\n");
            sb.Append($"      faces: shell {x.ShellFaces}, room {x.RoomFaces}, secondary copies {x.SecondaryFaces}\n");
            sb.Append($"      ramps: shell {shell.Materials.Count}/{HullMeshDef.HullRampSlots}, room " +
                      $"{room.Materials.Count}/{HullMeshDef.InteriorRampSlots}\n");
            sb.Append($"      room facing histogram k=0..8: {string.Join(" ", x.FacingHistogram)}; dropped " +
                      $"{x.DroppedLids} ceiling and {x.DroppedCaps} section-cap faces\n");
            sb.Append($"      levels: {string.Join(", ", x.Room.Levels.Select(l => $"{l.LevelId}={l.Tag} lid {l.LidTag}"))}, " +
                      $"{DwellingMeshBakeRules.ShellLidLevelId}={DwellingMeshBakeRules.ShellLidTag}\n");
            sb.Append($"      depth shift {x.DepthShift.ToString("0.###", Inv)} m; azimuth {x.Azimuth} (measured)\n");
            sb.Append($"      fidelity not carried: {x.TexturedFaces} textured faces, {x.SmoothNormalFaces} smooth-normal " +
                      $"faces, {x.FlatFaces} flat faces, polish on [{string.Join(", ", x.PolishedMaterials)}]" +
                      (x.TexturedFaces > 0 ? $"\n      deferred, by declaration: {d.DeferredSurfaceDetail}" : "") + "\n");
            return sb.ToString();
        }

        // =============================================================================================
        //  WRITING
        // =============================================================================================

        /// <summary>Extract and write one dwelling's def.</summary>
        public static DwellingMeshDef Bake(in DwellingRigFleet.MeshDwelling d, out DwellingExtraction x)
        {
            x = Extract(d);
            RigMeshBuild build = RigMeshBuilder.Build(x.Data, $"{ToPascal(d.Key)}DwellingMesh", (byte[])null);

            EnsureFolder(Path.GetDirectoryName(d.MeshAssetPath).Replace('\\', '/'));
            DwellingMeshDef def = LoadOrCreate(d.MeshAssetPath);

            DwellingRigFleet.TryGetDwelling(d.Key, out DwellingRigFleet.Dwelling shell);
            RigMeshData data = x.Data;
            def.Id = d.MeshId;
            def.SourceRigPath = shell.ScriptPath;
            def.SourceRoomRigPath = d.RoomScriptPath;
            def.SourceFaceBuilder = d.ViewOptions;
            def.Ramps = data.Materials.Select(m => m.ToRamp()).ToArray();
            def.InteriorRamps = data.InteriorMaterials.Select(m => m.ToRamp()).ToArray();
            def.LightN = data.LightN.ToVector3();
            def.Gain = (float)data.Gain;
            def.Bias = (float)data.Bias;
            var bayer = new float[DwellingMeshDef.DitherMatrixLength];
            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    bayer[i * 4 + j] = (float)data.Bayer[i, j];
            def.Bayer16 = bayer;
            def.Keyline = data.Keyline;
            def.PivotPx = new Vector2((float)data.PivotX, (float)data.PivotY);
            def.PxPerMetre = data.PxPerMetre;
            def.CellW = data.W;
            def.CellH = data.H;
            def.ElevationDeg = (float)data.DefaultElev;
            def.AzimuthCounterClockwise = x.Azimuth == AzimuthConvention.CounterClockwise;
            def.ZeroHeadingDegrees = 0f;
            def.Levels = x.Room.Levels;
            def.Threshold = x.Room.Threshold;
            def.Anchors = x.Room.Anchors;

            SwapMesh(def, build.Mesh, d.MeshAssetPath);

            // Reload from disk before trusting the reference: a mid-bake import can invalidate the
            // in-memory object, and a def written with a dead mesh ref looks fine until it is drawn.
            AssetDatabase.ImportAsset(d.MeshAssetPath, ImportAssetOptions.ForceUpdate);
            DwellingMeshDef reloaded = AssetDatabase.LoadAssetAtPath<DwellingMeshDef>(d.MeshAssetPath);
            string problem = reloaded == null ? "the written asset does not load" : reloaded.FirstProblem();
            if (problem != null)
                throw new InvalidOperationException($"{d.Key}: the written def at {d.MeshAssetPath} is not usable: {problem}");

            Debug.Log($"[rig-dwelling] {d.MeshAssetPath}: {build} -> {reloaded.Id}, usable.");
            return reloaded;
        }

        static DwellingMeshDef LoadOrCreate(string path)
        {
            var asset = AssetDatabase.LoadAssetAtPath<DwellingMeshDef>(path);
            if (asset != null) return asset;
            if (File.Exists(path))
                throw new InvalidOperationException(
                    $"{path} exists on disk but did not load as a DwellingMeshDef, so this bake was about to " +
                    "recreate it and reset every field. Usual cause: a stale Library/. Reimport, then bake.");
            asset = ScriptableObject.CreateInstance<DwellingMeshDef>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        static void SwapMesh(DwellingMeshDef owner, Mesh fresh, string path)
        {
            Mesh old = owner.Mesh;
            owner.Mesh = fresh;
            if (old != null)
            {
                AssetDatabase.RemoveObjectFromAsset(old);
                UnityEngine.Object.DestroyImmediate(old, allowDestroyingAssets: true);
            }
            AssetDatabase.AddObjectToAsset(fresh, owner);
            EditorUtility.SetDirty(owner);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        static string ToPascal(string key) =>
            string.IsNullOrEmpty(key) ? key : char.ToUpperInvariant(key[0]) + key.Substring(1);
    }
}
#endif
