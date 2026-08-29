using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>The room, as geometry</b> — extracts a hull's interior (shell AND fit-out) out of
    /// <c>boatInteriorRig.js</c> and hands it back as <see cref="RigFace"/>s in the hull's own frame,
    /// ready to be appended to her mesh. The full-mesh-interiors half of ADR 0038, replacing the
    /// baked sheet per hull at parity.
    ///
    /// <para><b>Why this can work at all, measured before it was written</b>
    /// (<c>docs/design/spikes/interior-geometry-view-dependence.txt</c>):</para>
    /// <list type="number">
    /// <item><b>The geometry is POSE-FREE.</b> Building the same level at roll 6°, pitch 3° and
    /// heave 0.4 m yields byte-identical vertices on every hull and level tested — the rig does not
    /// bake camera state into its face list, so one bake serves every sea.</item>
    /// <item><b>Its facing dependence is pure CULLING.</b> Over eight facings the per-face
    /// appearance histogram is quantised to k ∈ {3, 5, 8} — nothing appears at 1, 2, 4, 6 or 7 —
    /// which is the signature of a face being kept or dropped, not rebuilt.</item>
    /// <item><b>And the one exception is nameable.</b> Every k=3 face carries the <c>cut</c>
    /// material and <c>cut</c> appears at no other k: 129 faces fleet-wide, all of them the rig's
    /// hand-drawn per-facing SECTION LIP. Those are the only faces a single mesh cannot carry, and
    /// they are exactly the ones the facet shader's continuous back-face test replaces.</item>
    /// </list>
    ///
    /// <para>So the rule is: take the union over all eight facings, drop <see cref="SectionLipMaterial"/>,
    /// and let the shader cut. <see cref="InteriorGeometry.Report"/> restates the histogram every run, so a
    /// rig change that breaks the premise shows up as a number rather than as a wrong picture.</para>
    /// </summary>
    public static class BoatInteriorGeometryExtractor
    {
        /// <summary>The rig's per-facing section lip — a bright low strip drawn where a near wall was
        /// culled. It is a substitute FOR geometry, not geometry, and no single mesh can hold all
        /// eight versions of it. Dropped; the shader's own back-face test does the same job
        /// continuously, at every heading rather than at eight.</summary>
        public const string SectionLipMaterial = "cut";

        /// <summary>Facings the rig is built at. Eight, because that is the rig's own vocabulary and
        /// the union over them is what recovers every wall.</summary>
        public const int Facings = 8;

        /// <summary>
        /// The rig's procedural surface detail, transcribed per face. <c>paint()</c> does
        /// <c>if (tex &amp;&amp; uv) fi += tex(uu, vv)</c> — a function of the interpolated uv that
        /// shifts the ramp index by a small INTEGER. Measured, 28.6% of the lobster's wheelhouse
        /// faces carry one and 63.4% of her cuddy do, so a mesh that dropped it would lose most of
        /// the surface of a berth space.
        ///
        /// <para>There are exactly three generators in the rig and they are pure functions of
        /// (u, v), so they transcribe into the facet shader rather than needing to be baked into
        /// geometry. The KIND travels per face; the PERIOD is captured in the rig's closure and is
        /// recovered by probing the function itself, never by reading a literal out of the source.</para>
        /// </summary>
        public enum TexKind
        {
            None = 0,
            /// <summary>plankTex(p): a groove every p in V. <b>−2 in the groove, 0 outside.</b> The
            /// rig also branches on a per-plank hash for 0-or-−1 outside the groove, but that branch
            /// is unreachable — see <see cref="InteriorGeometry"/>'s notes and the shader's, and
            /// note that it is unreachable in the SPRITE path too.</summary>
            Plank = 1,
            /// <summary>boardTex(p): a groove every p in U. −1 in the groove, 0 outside.</summary>
            Board = 2,
            /// <summary>quiltTex(): a 0.20 grid, −1 in either groove, 0 elsewhere. The rig's
            /// per-cell hash would make it +1 in some cells; that branch is unreachable too.</summary>
            Quilt = 3,
        }

        public sealed class InteriorGeometry
        {
            /// <summary>Interior faces in the hull's own frame, ready to append to her face list.
            /// <see cref="RigFace.Interior"/> is true on every one.</summary>
            public List<RigFace> Faces = new List<RigFace>();
            /// <summary>The interior's OWN material table — a separate index space from the hull's,
            /// which is what lets the shader's <c>_RampMetaInterior</c> stay a second table rather
            /// than a widening of the fleet's guarded 16.</summary>
            public List<RigMaterial> Materials = new List<RigMaterial>();
            /// <summary>The rig level names this room covers, in the rig's own order.</summary>
            public List<string> LevelNames = new List<string>();
            /// <summary>Human-readable evidence trail for the bake log — the committed mesh is an
            /// opaque blob, so this is the only reviewable artefact the extraction produces.</summary>
            public string Report = "";
        }

        // ---------------------------------------------------------------------------- the JS side

        /// <summary>
        /// Widen the interior rig's export so the closure's privates are reachable, exactly as
        /// <see cref="RigMeshExtractor.WidenExportedLiteral"/> does for the hull rigs.
        ///
        /// <para>⚠️ <c>LastIndexOf</c>, not <c>IndexOf</c>: these rigs document their own export in a
        /// header comment, and injecting into the prose reports success and then dies later with
        /// "build is not a function" — which reads like the rig lacking the symbol rather than the
        /// shim missing it.</para>
        /// </summary>
        public static string WidenInteriorRig(string source)
        {
            const string marker = "root.BoatInterior = {";
            int at = source.LastIndexOf(marker, StringComparison.Ordinal);
            if (at < 0)
                throw new InvalidOperationException(
                    "boatInteriorRig.js no longer ends with `root.BoatInterior = {` — the extraction " +
                    "shim cannot reach build()/makeMats()/hullEnv(), and a bake must not proceed on a " +
                    "rig it cannot fully address.");
            return source.Insert(at + marker.Length,
                " build:build, makeMats:makeMats, hullEnv:hullEnv, camBasis:camBasis, levelsOf:levelsOf,");
        }

        const string Helpers = @"
globalThis.__HHI = (function(){
  var BI = globalThis.BoatInterior;

  // WHICH GENERATOR, by its own source text. The period is CAPTURED in the closure and is not in
  // the text, so it is probed below rather than read — a literal scraped out of the source would
  // silently be the wrong number the moment a call site passed a different one.
  function kindOf(fn){
    var s = String(fn);
    if (s.indexOf('-2') >= 0)      return 1;   // plankTex: the groove returns -2, uniquely
    if (s.indexOf('0.026') >= 0)   return 2;   // boardTex: its groove half-width
    if (s.indexOf('0.030') >= 0)   return 3;   // quiltTex: its grid groove
    return -1;                                 // UNKNOWN -> the C# side refuses the bake
  }

  // The period, recovered from the function's own behaviour. plank grooves run in v and read -2;
  // board grooves run in u and read -1. Both bands start at 0, so the next band's start IS p.
  function periodOf(fn, kind){
    if (kind === 3) return 0.20;               // quiltTex fixes its grid; nothing to probe
    var inV = (kind === 1), hit = (kind === 1) ? -2 : -1, band = (kind === 1) ? 0.022 : 0.026;
    for (var i = 1; i <= 40000; i++){
      var t = i * 0.00005;
      if (t <= band + 1e-9) continue;          // still inside the band at the origin
      var val = inV ? fn(0, t) : fn(t, 0);
      if (val === hit) return t;
    }
    return -1;
  }

  function extract(hull){
    var levels = BI.levelsOf(hull), out = [], mats = {}, order = [];
    var hist = [0,0,0,0,0,0,0,0,0], lips = 0;

    for (var li = 0; li < levels.length; li++){
      var lvl = levels[li];
      var s = BI.resolve({ hull: hull, level: lvl });
      var env = BI.hullEnv(hull, s.variant);
      if (!env) continue;
      var MM = BI.makeMats(s, env);
      for (var k in MM) if (!mats[k]) { mats[k] = MM[k].ramp; order.push(k); }

      var seen = {}, count = {}, lastDir = {};
      for (var d = 0; d < 8; d++){
        var faces = BI.build(s, env, BI.camBasis({ dir: d }, env));
        for (var i = 0; i < faces.length; i++){
          var f = faces[i];
          if (f.mat === 'cut') { lips++; continue; }
          var key = f.mat + '|';
          for (var j = 0; j < f.v.length; j++)
            key += f.v[j][0].toFixed(5)+','+f.v[j][1].toFixed(5)+','+f.v[j][2].toFixed(5)+';';
          if (seen[key] != null) { if (lastDir[key] !== d) { count[key]++; lastDir[key] = d; } continue; }
          seen[key] = out.length; count[key] = 1; lastDir[key] = d;

          var kind = f.tex ? kindOf(f.tex) : 0;
          var per  = (kind > 0) ? periodOf(f.tex, kind) : 0;
          out.push({ lvl: lvl, mat: f.mat, b: f.b || 0, db: f.db || 0,
                     kind: kind, per: per, v: f.v, uv: (kind > 0 ? f.uv : null), key: key });
        }
      }
      for (var kk in count) hist[Math.min(8, count[kk])]++;
    }
    return { faces: out, mats: mats, order: order, hist: hist, lips: lips, levels: levels };
  }

  // A flat text protocol rather than JSON: no parser dependency on the C# side, and every number
  // crosses as its own token so a malformed row is a parse error rather than a plausible face.
  function emit(hull){
    var r = extract(hull), L = [];
    L.push('LEVELS ' + r.levels.join(' '));
    L.push('HIST ' + r.hist.slice(1).join(' '));   // k=1..8; index 0 is unused and would shift every column
    L.push('LIPS ' + r.lips);
    for (var i = 0; i < r.order.length; i++){
      var n = r.order[i], ramp = r.mats[n];
      L.push('MAT ' + n + ' ' + ramp.join(' '));
    }
    for (var i = 0; i < r.faces.length; i++){
      var f = r.faces[i];
      L.push('F ' + f.lvl + ' ' + f.mat + ' ' + f.b + ' ' + f.db + ' ' + f.kind + ' ' + f.per
             + ' ' + f.v.length);
      for (var j = 0; j < f.v.length; j++){
        var uv = f.uv ? f.uv[j] : null;
        L.push('V ' + f.v[j][0] + ' ' + f.v[j][1] + ' ' + f.v[j][2]
               + ' ' + (uv ? uv[0] : 0) + ' ' + (uv ? uv[1] : 0));
      }
    }
    return L.join('\n');
  }
  return { emit: emit };
})();
";

        // ------------------------------------------------------------------------------ the C# side

        /// <summary>
        /// Extract <paramref name="interiorHullKey"/>'s room from the interior rig already loaded on
        /// <paramref name="host"/>, in the hull's own frame.
        /// </summary>
        /// <param name="host">A host that ALREADY has this hull's exterior rig executed — the
        /// interior rig reads her loft off it (<c>root[meta.sym]</c>) and binds to nothing without it.</param>
        /// <param name="interiorHullKey">The interior rig's own hull key (<c>lobster</c>, <c>cape</c>…).</param>
        /// <param name="hull">The hull's extracted mesh data, for her level vocabulary and her size.</param>
        /// <param name="interiorRigPath">Absolute path to <c>boatInteriorRig.js</c>.</param>
        public static InteriorGeometry Extract(IRigScriptHost host, string interiorHullKey,
                                               RigMeshData hull, string interiorRigPath)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (hull == null) throw new ArgumentNullException(nameof(hull));
            if (!hull.CarriesLevelTags)
                throw new InvalidOperationException(
                    $"{hull.RigKey} publishes no level vocabulary, so there is nothing to tag an " +
                    "interior face WITH. A room whose faces all read level 0 is a room that is never " +
                    "cut away and never hidden — it would draw through her own hull from every angle. " +
                    "Bake the cutaway pass on this rig first.");

            host.Execute(WidenInteriorRig(File.ReadAllText(interiorRigPath)));
            host.Execute(Helpers);

            if (!host.EvaluateBool($"!!globalThis.BoatInterior.hullEnv('{interiorHullKey}', null)"))
                throw new InvalidOperationException(
                    $"the interior rig cannot bind hull '{interiorHullKey}' against the exterior rig on " +
                    "this host. Usual cause: the SHIPPED exterior rig does not publish the `loft` block " +
                    "the interior rig reads (measured true of sportFisherIsoRig2.js, which binds only " +
                    "from the kit's hull-rigs/ copy). That is an upstream ask, not something to work " +
                    "around here — a room built off a different revision of the hull is a room that " +
                    "does not fit her.");

            string payload = host.EvaluateString($"globalThis.__HHI.emit('{interiorHullKey}')");
            return Parse(payload, hull, interiorHullKey);
        }

        static InteriorGeometry Parse(string payload, RigMeshData hull, string hullKey)
        {
            var g = new InteriorGeometry();
            var inv = CultureInfo.InvariantCulture;

            // The room is pushed in front of the hull by her own SIZE. UV0.z is the rig's per-face
            // `db`, which vert() subtracts from clip depth — the spike measured a revealed room
            // surviving at only 20.3% without this, because a hull's near topsides stand between the
            // camera and a cabin sole in a ¾ view, and 97.6% with it. The hull's bounding-sphere
            // DIAMETER is the smallest shift guaranteed to clear her own geometry whatever the
            // heading. Her own db is KEPT on top of it, so ordering WITHIN the room is preserved
            // rather than flattened to one plane.
            double shift = HullDiameter(hull);

            var rampByName = new Dictionary<string, string[]>(StringComparer.Ordinal);
            var matIndexByRamp = new Dictionary<string, int>(StringComparer.Ordinal);
            var matIndexByName = new Dictionary<string, int>(StringComparer.Ordinal);
            int[] hist = new int[9];
            int lips = 0;

            var lines = payload.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Length == 0) continue;
                string[] t = line.Split(' ');
                switch (t[0])
                {
                    case "LEVELS":
                        for (int k = 1; k < t.Length; k++) g.LevelNames.Add(t[k]);
                        break;
                    case "HIST":
                        for (int k = 1; k < t.Length && k <= 8; k++) hist[k] = int.Parse(t[k], inv);
                        break;
                    case "LIPS":
                        lips = int.Parse(t[1], inv);
                        break;
                    case "MAT":
                        rampByName[t[1]] = t.Skip(2).ToArray();
                        break;
                    case "F":
                    {
                        string lvl = t[1], mat = t[2];
                        double b = double.Parse(t[3], inv), db = double.Parse(t[4], inv);
                        int kind = int.Parse(t[5], inv);
                        double per = double.Parse(t[6], inv);
                        int nv = int.Parse(t[7], inv);

                        if (kind < 0)
                            throw new InvalidOperationException(
                                $"{hullKey}/{lvl}: a face carries a procedural texture this extractor " +
                                "does not recognise. The rig has grown a fourth generator; transcribe " +
                                "it into the facet shader and add it to TexKind rather than letting " +
                                "the bake drop the detail silently.");
                        if (kind > 0 && per <= 0)
                            throw new InvalidOperationException(
                                $"{hullKey}/{lvl}: a {(TexKind)kind} face's period could not be " +
                                "probed. Do not bake a guessed period — the pattern would be visibly " +
                                "the wrong scale.");

                        var verts = new Vector3d[nv];
                        var uvs = kind > 0 ? new Vector2[nv] : null;
                        for (int k = 0; k < nv; k++)
                        {
                            string[] vt = lines[++i].Split(' ');
                            verts[k] = new Vector3d(double.Parse(vt[1], inv), double.Parse(vt[2], inv),
                                                    double.Parse(vt[3], inv));
                            if (uvs != null)
                                uvs[k] = new Vector2(float.Parse(vt[4], inv), float.Parse(vt[5], inv));
                        }

                        if (!matIndexByName.TryGetValue(mat, out int matIndex))
                        {
                            if (!rampByName.TryGetValue(mat, out string[] ramp))
                                throw new InvalidOperationException(
                                    $"{hullKey}: face material '{mat}' is not in the rig's own MATS " +
                                    "table. paint() would silently fall back to 'liner'; a bake must not.");
                            // Dedupe by RAMP CONTENT, not by name. Measured: cab=wains,
                            // daylight=glass, flame=screen and iron=panel are byte-identical on every
                            // hull, so four name pairs collapse to four slots with no colour lost —
                            // which is a fifth of the interior's budget recovered for nothing.
                            string key = string.Join(",", ramp);
                            if (!matIndexByRamp.TryGetValue(key, out matIndex))
                            {
                                matIndex = g.Materials.Count;
                                matIndexByRamp[key] = matIndex;
                                g.Materials.Add(new RigMaterial
                                {
                                    Name = mat,
                                    RampHex = ramp,
                                    Ramp = ramp.Select(ParseHex).ToArray(),
                                    Off = 0,
                                });
                            }
                            matIndexByName[mat] = matIndex;
                        }

                        int level = LevelIdFor(hull, lvl, hullKey);
                        g.Faces.Add(new RigFace
                        {
                            V = verts,
                            Mat = matIndex,
                            B = b,
                            Db = db + shift,
                            Level = level,
                            LevelName = lvl,
                            Interior = true,
                            TexKind = kind,
                            TexPeriod = per,
                            Uv = uvs,
                        });
                        break;
                    }
                }
            }

            g.Report = BuildReport(g, hull, hullKey, hist, lips, shift);
            return g;
        }

        /// <summary>
        /// The rig level a room belongs to, read off the HULL's own <c>geometry().ids</c> by name.
        ///
        /// <para>Refused rather than defaulted when the name is absent, for the reason
        /// <see cref="RigMeshBuilder"/> gives about half-tagged face lists: the silent default is
        /// level 0 = <c>hull</c> = never culled, and a room tagged that way stays drawn when the
        /// player is outside her, standing in mid-air over the boat.</para>
        /// </summary>
        static int LevelIdFor(RigMeshData hull, string levelName, string hullKey)
        {
            if (hull.LevelIds.TryGetValue(levelName, out int id)) return id;
            throw new InvalidOperationException(
                $"the interior rig builds a level called '{levelName}' for '{hullKey}', but her " +
                $"exterior rig's geometry().ids does not name it (it has: " +
                $"{string.Join(", ", hull.LevelIds.Keys.OrderBy(s => s, StringComparer.Ordinal))}). " +
                "The two rigs disagree about what rooms this boat has. Do NOT map it to the nearest " +
                "name — take it upstream, because the level id is what the cutaway gate compares " +
                "against and a wrong one opens the wrong room.");
        }

        /// <summary>Bounding-sphere diameter of the hull's own faces, rig metres.</summary>
        static double HullDiameter(RigMeshData hull)
        {
            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
            foreach (RigFace f in hull.Faces)
                foreach (Vector3d v in f.V)
                {
                    if (v.X < minX) minX = v.X; if (v.X > maxX) maxX = v.X;
                    if (v.Y < minY) minY = v.Y; if (v.Y > maxY) maxY = v.Y;
                    if (v.Z < minZ) minZ = v.Z; if (v.Z > maxZ) maxZ = v.Z;
                }
            if (minX > maxX) return 0;
            double dx = maxX - minX, dy = maxY - minY, dz = maxZ - minZ;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        static Color32 ParseHex(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex[0] != '#' || hex.Length < 7)
                throw new FormatException($"interior ramp colour '{hex}' is not #rrggbb.");
            return new Color32(
                Convert.ToByte(hex.Substring(1, 2), 16),
                Convert.ToByte(hex.Substring(3, 2), 16),
                Convert.ToByte(hex.Substring(5, 2), 16), 255);
        }

        static string BuildReport(InteriorGeometry g, RigMeshData hull, string hullKey,
                                  int[] hist, int lips, double shift)
        {
            var inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.Append("[interior-mesh] ").Append(hullKey).Append(": ")
              .Append(g.Faces.Count).Append(" faces, ")
              .Append(g.Materials.Count).Append(" ramps (cap ")
              .Append(HiddenHarbours.Core.HullMeshDef.InteriorRampSlots).Append("), levels ")
              .Append(string.Join("/", g.LevelNames)).AppendLine();

            // The premise, restated every bake. k ∈ {3,5,8} with every k=3 face being the section
            // lip is what makes a single mesh legitimate; a rig change that breaks it must show up
            // here as a number, not later as a wrong picture.
            sb.Append("[interior-mesh]   facing histogram k=1..8: ")
              .Append(string.Join(" ", hist.Skip(1).Select(n => n.ToString(inv))))
              .Append("   section lips dropped: ").Append(lips).AppendLine();
            if (hist[1] + hist[2] + hist[4] + hist[6] + hist[7] > 0)
                sb.AppendLine("[interior-mesh]   ⚠️ faces appear at facing counts outside {3,5,8} — the " +
                              "rig's culling is no longer a pure keep/drop and the union may be wrong.");

            int textured = g.Faces.Count(f => f.TexKind != (int)TexKind.None);
            sb.Append("[interior-mesh]   procedural detail: ").Append(textured).Append('/')
              .Append(g.Faces.Count).Append(" faces (")
              .Append((100.0 * textured / Math.Max(1, g.Faces.Count)).ToString("0.0", inv))
              .Append("%)  ");
            foreach (var grp in g.Faces.Where(f => f.TexKind != (int)TexKind.None)
                                       .GroupBy(f => ((TexKind)f.TexKind, Math.Round(f.TexPeriod, 4)))
                                       .OrderBy(gr => gr.Key.Item1).ThenBy(gr => gr.Key.Item2))
                sb.Append(grp.Key.Item1).Append('(').Append(grp.Key.Item2.ToString("0.###", inv))
                  .Append(")×").Append(grp.Count()).Append(' ');
            sb.AppendLine();

            int roomTris = g.Faces.Sum(f => Math.Max(0, f.V.Length - 2));
            // hull.Faces is still HULL-ONLY here: the room is appended by the baker after this
            // returns, so no subtraction is needed and one would silently drop real hull faces.
            int hullTris = hull.Faces.Sum(f => Math.Max(0, f.V.Length - 2));
            sb.Append("[interior-mesh]   tris: hull ").Append(hullTris).Append(" + room ")
              .Append(roomTris).Append(" = ").Append(hullTris + roomTris).Append("  (+")
              .Append((100.0 * roomTris / Math.Max(1, hullTris)).ToString("0.0", inv))
              .Append("% on the hull)").AppendLine();
            sb.Append("[interior-mesh]   depth shift ").Append(shift.ToString("0.###", inv))
              .Append(" m (hull bounding-sphere diameter), her own db kept on top")
              .AppendLine();
            return sb.ToString();
        }
    }
}
