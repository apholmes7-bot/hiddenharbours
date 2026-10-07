using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using Debug = UnityEngine.Debug;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE NUMBERS RIG 10.3 EXPORTS, READ OFF THE RIG, AND FOUR OF ITS FIXES, HELD.</b>
    ///
    /// <para>10.3 exports what 10.2's paint and checks held as literals: its tolerances (<c>TOL</c>), its
    /// dither (<c>DITHER</c>), its ink (<c>INK</c>, <c>INK_ROLES</c>, <c>EYE_WHITE</c>) and its golden aim
    /// (<c>AIM</c>). The bake, the look check and these guards read each in V8, and no copy of one remains
    /// in the port or in rig 10's tests: a copy that comes back fails here, by value, whatever it is
    /// named.</para>
    ///
    /// <para>And four of the intake's measures, held on the ten fresh bakes: each aims inside
    /// <c>AIM.bar_deg</c> (ruled 4.0° for every build); no joint turns a half turn between two frames of a
    /// fishing clip (10.2's rod sockets turned 180° in one frame); and the sleeper lies centred on the
    /// pivot (10.2 laid the pelvis at y = −0.18 m). The girl's face at all eight facings, the fourth, is
    /// <see cref="CharacterSkinnedExportTests.V10_TheGirlPassesTheFaceGateAtEveryFacing"/>, whose host
    /// holds the rig's checks.</para>
    /// </summary>
    public partial class CharacterSkinBakeGuardTests
    {
        /// <summary>The <c>TOL</c> keys the port reads, each where the rig's paint or gates read it:
        /// <c>gate_m</c> (<see cref="CharacterSkinExtractor.GateTolerance9"/>), <c>cull</c>
        /// (<see cref="CharacterSkinExtractor.CullFloor9(IRigScriptHost)"/>), <c>markEdge</c>
        /// (<see cref="CharacterSkinExtractor.ReadMarkCull9(IRigScriptHost)"/>) and the five
        /// <see cref="CharacterSkinExtractor.PaintTolerance9"/> reads. A key the rig adds is a number
        /// the port does not read yet.</summary>
        static readonly string[] PortTolKeys10 =
            { "gate_m", "inside", "area", "cull", "markEdge", "depthScale", "shadeScale", "tieDepth" };

        /// <summary>The roles 10.3's <c>INK_ROLES</c> names: <c>INK[0]</c> and <c>INK[1]</c> (each "material
        /// &lt;name&gt;: …"), the keyline, which is a colour of its own, and <c>iris</c>, a material no rig 10
        /// face draws in.</summary>
        static readonly string[] InkRoleKeys10 = { "0", "1", "keyline", "iris" };

        /// <summary>The charter's bar on a step between two frames (rig 10.3 intake, A4): a joint that
        /// turns this far between neighbours turns the long way round, as 10.2's rod sockets did (180.0°
        /// in one frame of each of the five fishing clips 10.3 re-keyed).</summary>
        const double HalfTurnBarDeg10 = 179.9;

        // =======================================================================================
        // v10 exports 1. the bake reads TOL, DITHER and the ink off the rig
        // =======================================================================================

        /// <summary>
        /// Every number 10.3's <c>TOL</c> holds is one the port reads, and reads off the rig: the
        /// gate, the face and mark culls, and the paint's five (area, inside, the depth quantum, the depth
        /// tie and the shade's rounding). The dither thresholds are <c>DITHER.bayer4</c> under the rule
        /// its <c>threshold</c> states, run here as the rig writes it. The ten fresh defs carry the same
        /// floors and thresholds and the ink <c>INK_ROLES</c> names: wherever a def carries the material of
        /// <c>INK[0]</c> or <c>INK[1]</c> or the eye white's, it is that one colour (<c>INK</c>,
        /// <c>EYE_WHITE</c>), and some def carries each; the keyline is <c>SHADING.keyline</c>, not
        /// <c>INK[0]</c>; and no def carries <c>iris</c>.
        /// </summary>
        [Test]
        public void V10_TheBakeReadsTheRigsExportsOffTheRig()
        {
            IRigScriptHost host = V10Host;
            CharacterRigKit kit = CharacterRigKit.Rig10;
            string g = kit.GlobalName;
            Assert.IsTrue(kit.ExportsNumbers, $"{kit} says it exports no numbers; 10.3 exports TOL, DITHER, INK and AIM.");

            // TOL: every number, each read where the port reads it.
            var tol = new Dictionary<string, double>(StringComparer.Ordinal);
            foreach (string kv in host.EvaluateString(
                         "(function(){var T=" + g + ".TOL;return Object.keys(T).filter(function(k){return typeof T[k]==='number';})" +
                         ".map(function(k){return k+'='+String(T[k]);}).join(',');})()").Split(','))
            {
                string[] p = kv.Split('=');
                Assert.AreEqual(2, p.Length, $"A TOL entry read as '{kv}'.");
                tol[p[0]] = D9(p[1]);
            }
            CollectionAssert.AreEquivalent(PortTolKeys10, tol.Keys,
                $"{kit}'s TOL holds {string.Join(", ", tol.Keys)}; the port reads {string.Join(", ", PortTolKeys10)}. " +
                "Read a new key where the rig's paint or gates do.");
            foreach (KeyValuePair<string, double> t in tol)
                Assert.AreEqual(t.Value, CharacterSkinExtractor.Tol9(host, t.Key), 0d, $"Tol9 reads TOL.{t.Key}.");
            Assert.AreEqual(tol["gate_m"], CharacterSkinExtractor.GateTolerance9(host), 0d, "The gate is TOL.gate_m.");
            Assert.AreEqual(tol["cull"], CharacterSkinExtractor.CullFloor9(host), 0d, "The face cull is TOL.cull.");
            Assert.AreEqual(tol["markEdge"], CharacterSkinExtractor.ReadMarkCull9(host).Edge, 0d, "The marks' edge is TOL.markEdge.");
            RigPaint9.Tolerance paint = CharacterSkinExtractor.PaintTolerance9(host);
            Assert.AreEqual(tol["area"], paint.Area, 0d, "Paint's area is TOL.area.");
            Assert.AreEqual(tol["inside"], paint.Inside, 0d, "Paint's inside is TOL.inside.");
            Assert.AreEqual(tol["depthScale"], paint.DepthScale, 0d, "Paint's depth quantum is TOL.depthScale.");
            Assert.AreEqual(tol["tieDepth"], paint.TieDepth, 0d, "Paint's depth tie is TOL.tieDepth.");
            Assert.AreEqual(tol["shadeScale"], paint.ShadeScale, 0d, "Paint's shade rounding is TOL.shadeScale.");

            // DITHER: the matrix under the rig's own threshold rule, run as written.
            double[,] bayer = CharacterSkinExtractor.Bayer9(host);
            string[] rule = host.EvaluateString(
                "(function(){var D=" + g + ".DITHER,t=new Function('m','return '+D.threshold),o=[];" +
                "for(var x=0;x<4;x++)for(var y=0;y<4;y++)o.push(String(t(D.bayer4[x][y])));return o.join(',');})()").Split(',');
            Assert.AreEqual(16, rule.Length, $"{kit}'s DITHER read as {rule.Length} thresholds.");
            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    Assert.AreEqual(D9(rule[x * 4 + y]), bayer[x, y], 0d, $"The dither threshold at [{x}, {y}].");

            // The ink: INK, EYE_WHITE, SHADING.keyline and INK_ROLES.
            string[] s = host.EvaluateString(
                "(function(){var G=" + g + ",R=G.INK_ROLES;return [G.INK.join(','),String(G.EYE_WHITE),String(G.SHADING.keyline)," +
                "Object.keys(R).map(function(k){return k+'='+String(R[k]).split('|').join('/').split('\\n').join(' ');}).join('|')]" +
                ".join('\\n');})()").Split('\n');
            Assert.AreEqual(4, s.Length, $"{kit}'s ink read as {s.Length} fields.");
            string[] ink = s[0].Split(',');
            Assert.AreEqual(2, ink.Length, $"{kit}'s INK holds {ink.Length} colours.");
            Color32 white = Hex10(s[1], "EYE_WHITE"), keyline = Hex10(s[2], "SHADING.keyline");
            var roles = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string r in s[3].Split('|'))
            {
                int at = r.IndexOf('=');
                Assert.Greater(at, 0, $"An INK_ROLES entry read as '{r}'.");
                roles[r.Substring(0, at)] = r.Substring(at + 1);
            }
            CollectionAssert.AreEquivalent(InkRoleKeys10, roles.Keys,
                $"{kit}'s INK_ROLES names {string.Join(", ", roles.Keys)}. Read what a new role says before baking it.");
            var inkMats = new string[ink.Length];
            for (int i = 0; i < ink.Length; i++)
            {
                Match m = Regex.Match(roles[i.ToString(CultureInfo.InvariantCulture)], @"^material (\w+):");
                Assert.IsTrue(m.Success, $"INK_ROLES[{i}] names no material: '{roles[i.ToString(CultureInfo.InvariantCulture)]}'.");
                inkMats[i] = m.Groups[1].Value;
            }
            Assert.IsFalse(Same10(keyline, Hex10(ink[0], "INK[0]")),
                "INK_ROLES.keyline says SHADING.keyline is a colour of its own, not INK[0], and the two are the same.");

            var carried = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string n in inkMats) carried[n] = new List<string>();
            var report = new StringBuilder();
            foreach (string preset in CharacterSkinExtractor.Presets9(host))
            {
                CharacterSkinDef def = V10Bake(preset).Def;
                Assert.AreEqual((float)tol["cull"], def.FaceCullFloor, $"{preset}: the def's face cull floor.");
                Assert.AreEqual((float)tol["markEdge"], def.FaceMarkEdge, $"{preset}: the def's mark edge.");
                Assert.AreEqual(16, def.Bayer16.Length, $"{preset}: the def's dither.");
                for (int x = 0; x < 4; x++)
                    for (int y = 0; y < 4; y++)
                        Assert.AreEqual((float)bayer[x, y], def.Bayer16[x * 4 + y], $"{preset}: the def's dither at [{x}, {y}].");

                // The rig's own materials for the build: INK_ROLES' two, the eye white's, and iris.
                var names = new StringBuilder();
                foreach (string n in inkMats) names.Append(names.Length == 0 ? "" : ",").Append(JsQuote9(n));
                string[] rig = host.EvaluateString(
                    "(function(){var G=" + g + ",M=G.buildOf(" + JsQuote9(preset) + ").mats;" +
                    "return [[" + names + "].map(function(k){var m=M[k];return m?(m.fixed?'1':'0')+':'+m.ramp[0]:'none';}).join(',')," +
                    "Object.keys(M).filter(function(k){return M[k].fixed&&M[k].ramp[0]===G.EYE_WHITE;}).join(',')," +
                    "M[" + JsQuote9("iris") + "]?'1':'0'].join('|');})()").Split('|');
                Assert.AreEqual(3, rig.Length, $"{preset}: the rig's materials read as {rig.Length} fields.");
                string[] fixedInk = rig[0].Split(',');
                for (int i = 0; i < inkMats.Length; i++)
                {
                    Assert.AreEqual("1:" + ink[i], fixedInk[i],
                        $"{preset}: the rig's material '{inkMats[i]}' (INK_ROLES[{i}]) is not fixed at INK[{i}] {ink[i]}.");
                    if (HoldsOneColour10(def, inkMats[i], Hex10(ink[i], $"INK[{i}]"), $"INK[{i}]")) carried[inkMats[i]].Add(preset);
                }
                Assert.IsNotEmpty(rig[1], $"{preset}: no material of the rig is fixed at EYE_WHITE {s[1]}.");
                foreach (string w in rig[1].Split(','))
                {
                    if (!carried.ContainsKey(w)) carried[w] = new List<string>();
                    if (HoldsOneColour10(def, w, white, "EYE_WHITE")) carried[w].Add(preset);
                }
                Assert.AreEqual("1", rig[2], $"{preset}: INK_ROLES names iris, and the rig's build has no material of that name.");
                foreach (CharacterSkinDef.Material m in def.Materials)
                    Assert.AreNotEqual("iris", m.Name, $"{preset}: the def carries iris, which INK_ROLES says no rig 10 face draws in.");
                Assert.IsTrue(Same10(keyline, def.Keyline),
                    $"{preset}: the def's keyline is {Hex10(def.Keyline)}, the rig's SHADING.keyline {s[2]}.");
                report.Append(' ').Append(preset);
            }
            // A def carries only the materials its faces draw in, so a role's material may be missing from a build
            // (ink2 from builds whose eyes never half-shut in it); each must still be held by some def.
            var held = new StringBuilder();
            foreach (KeyValuePair<string, List<string>> c in carried)
            {
                Assert.IsNotEmpty(c.Value, $"No def of the ten carries '{c.Key}', so nothing holds its colour to the rig.");
                held.Append(held.Length == 0 ? "" : "; ").Append(c.Key).Append(" in ").Append(string.Join(" ", c.Value));
            }
            Debug.Log($"[CharacterSkinBakeGuardTests] {kit}: TOL ({string.Join(", ", PortTolKeys10)}), DITHER, INK " +
                      $"({string.Join(", ", inkMats)}), EYE_WHITE and the keyline read off the rig; the defs hold them:{report}. " +
                      $"The fixed ink, by def: {held}.");
        }

        /// <summary>Whether the def carries the material <paramref name="name"/>; when it does, the material
        /// is one colour, <paramref name="want"/>.</summary>
        static bool HoldsOneColour10(CharacterSkinDef def, string name, Color32 want, string what)
        {
            foreach (CharacterSkinDef.Material m in def.Materials)
            {
                if (!string.Equals(m.Name, name, StringComparison.Ordinal)) continue;
                Assert.AreEqual(1, m.Colors?.Length ?? 0, $"{def.Preset}: the def's '{name}' ({what}) holds {m.Colors?.Length ?? 0} colours.");
                Assert.IsTrue(Same10(want, m.Colors[0]),
                    $"{def.Preset}: the def's '{name}' is {Hex10(m.Colors[0])}, and {what} is {Hex10(want)}.");
                return true;
            }
            return false;
        }

        static Color32 Hex10(string hex, string what)
        {
            Assert.IsTrue(Regex.IsMatch(hex ?? "", "^#[0-9a-fA-F]{6}$"), $"{what} is '{hex}', not #rrggbb.");
            byte H(int at) => byte.Parse(hex.Substring(at, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new Color32(H(1), H(3), H(5), 255);
        }

        static string Hex10(Color32 c) =>
            "#" + c.r.ToString("x2", CultureInfo.InvariantCulture) + c.g.ToString("x2", CultureInfo.InvariantCulture) +
            c.b.ToString("x2", CultureInfo.InvariantCulture) + (c.a == 255 ? "" : " alpha " + c.a.ToString(CultureInfo.InvariantCulture));

        static bool Same10(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        // =======================================================================================
        // v10 exports 2. no copy of an export remains in the port or in rig 10's tests
        // =======================================================================================

        /// <summary>
        /// No copy of a number 10.3 exports remains: in the port (every C# file of the character bake and
        /// of the skin it writes) and in rig 10's tests, no literal, in code or in a JS string, equals a
        /// <c>TOL</c> value, and nowhere those files or the guard bodies rig 10 runs through hold an
        /// <c>INK</c> or <c>EYE_WHITE</c> colour (as hex or as three bytes), a row of <c>DITHER.bayer4</c>,
        /// or <c>AIM</c>'s bearings or heights. Every number is read off the rig here, so a copy is caught
        /// by its value, whatever it is named. Comments are not code and are skipped. The guard bodies'
        /// own tolerances (a blink's seconds, a golden report's rounding) are test arithmetic, not the
        /// rig's paint or gates, so those files are held to the colours and rows alone. One copy stands
        /// by name: <see cref="CharacterSkinExtractor.V9Tolerance"/>, rig 9.2's gate, which 9.2 does not
        /// export.
        /// </summary>
        [Test]
        public void V10_NoCopyOfTheRigsExportsRemainsInThePort()
        {
            IRigScriptHost host = V10Host;
            string g = CharacterRigKit.Rig10.GlobalName;
            string[] e = host.EvaluateString(
                "(function(){var G=" + g + ",T=G.TOL;return [Object.keys(T).filter(function(k){return typeof T[k]==='number';})" +
                ".map(function(k){return String(T[k]);}).join(','),G.INK.concat([G.EYE_WHITE]).join(',')," +
                "G.DITHER.bayer4.map(function(r){return r.join(',');}).join(';'),G.AIM.bearings_deg.join(','),G.AIM.dz_m.join(',')]" +
                ".join('|');})()").Split('|');
            Assert.AreEqual(5, e.Length, $"The rig's exports read as {e.Length} fields.");
            double[] tol = Array.ConvertAll(e[0].Split(','), D9);
            string[] hexes = Array.ConvertAll(e[1].Split(','), h => h.TrimStart('#').ToLowerInvariant());
            var rows = new List<string[]>();
            foreach (string r in e[2].Split(';')) rows.Add(r.Split(','));
            rows.Add(e[3].Split(','));
            rows.Add(e[4].Split(','));

            string root = RigCatalog.RepoRoot;
            List<string> port = Sources10(root, "Assets/_Project/Code/Tools/Editor/RigBaking",
                                          @"^(CharacterSkin.*|CharacterRig.*|RigPaint.*|RigCatalog\.CharacterKit)\.cs$");
            port.AddRange(Sources10(root, "Assets/_Project/Code/Core/Iso", @"^(CharacterSkin.*|CharacterFigure.*)\.cs$"));
            foreach (string must in new[] { "RigPaint9.cs", "CharacterSkinInk9.cs", "CharacterSkinExtractor.V9.cs",
                                            "CharacterSkinAssetBaker.V9.cs", "CharacterRigKit.cs", "CharacterSkinDef.cs" })
                Assert.IsTrue(port.Exists(f => Path.GetFileName(f) == must), $"The port's scan lost {must}; find where it moved.");
            List<string> tests = Sources10(root, "Assets/Tests/EditMode/RigBaking", @"^CharacterSkin.*\.cs$");
            List<string> rig10Tests = tests.FindAll(f => Path.GetFileName(f).Contains(".V10"));
            Assert.IsTrue(rig10Tests.Exists(f => Path.GetFileName(f) == "CharacterSkinBakeGuardTests.V10Exports.cs"),
                "The scan does not hold this file.");
            List<string> bodies = tests.FindAll(f => !rig10Tests.Contains(f));

            var files = new List<(string File, bool TolToo)>();
            foreach (string f in port) files.Add((f, true));
            foreach (string f in rig10Tests) files.Add((f, true));
            foreach (string f in bodies) files.Add((f, false));
            List<string> copies = CopiesOfTheExports10(tol, hexes, rows, files, out int stood);

            Assert.IsEmpty(copies, $"{copies.Count} copies of rig 10.3's exports; read each off the rig:\n  " + string.Join("\n  ", copies));
            Assert.AreEqual(1, stood, $"{nameof(CharacterSkinExtractor.V9Tolerance)} stood {stood} times; it is declared once.");
            Debug.Log($"[CharacterSkinBakeGuardTests] No copy of rig 10.3's exports in {port.Count} port files, {rig10Tests.Count} rig 10 " +
                      $"test files and {bodies.Count} guard-body files (TOL {e[0]}; INK and EYE_WHITE {e[1]}; DITHER {e[2]}; AIM {e[3]} / {e[4]}).");
        }

        /// <summary>Each copy of the rig's exports in <paramref name="files"/>, one line each: in a file
        /// marked <c>TolToo</c>, a literal equal to one of <paramref name="tol"/>; in every file, a colour of
        /// <paramref name="hexes"/> (as hex or as three bytes) or one of <paramref name="rows"/>. Comments
        /// are skipped. The one copy that stands, <see cref="CharacterSkinExtractor.V9Tolerance"/>'s
        /// declaration in <c>CharacterSkinExtractor.V9.cs</c>, is counted in <paramref name="stood"/>.</summary>
        static List<string> CopiesOfTheExports10(double[] tol, string[] hexes, List<string[]> rows,
                                                 IEnumerable<(string File, bool TolToo)> files, out int stood)
        {
            var hex = new Regex("(?<![0-9a-fA-F])(?:" + string.Join("|", hexes) + ")(?![0-9a-fA-F])", RegexOptions.IgnoreCase);
            var bytes = new List<(string Hex, Regex Re)>();
            foreach (string h in hexes)
            {
                var parts = new List<string>();
                for (int k = 0; k < 6; k += 2)
                {
                    int b = int.Parse(h.Substring(k, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    parts.Add("(?:" + b.ToString(CultureInfo.InvariantCulture) + "|0[xX]0*" + b.ToString("x", CultureInfo.InvariantCulture) + ")");
                }
                bytes.Add(("#" + h, new Regex(@"(?<![\w.])" + string.Join(@"\s*,\s*", parts) + @"(?![\w.])", RegexOptions.IgnoreCase)));
            }
            var rowRes = new List<(string Row, Regex Re)>();
            foreach (string[] r in rows)
                rowRes.Add((string.Join(",", r), new Regex(@"(?<![\w.])" + string.Join(@"\s*,\s*", Array.ConvertAll(r, NumberPattern10)) + @"(?![\w.])")));
            var literal = new Regex(@"(?<![\w.])(\d+\.?\d*(?:[eE][+-]?\d+)?|\.\d+(?:[eE][+-]?\d+)?)[fFdDmM]?(?![\w.])");
            var standing = new Regex(@"^\s*public const double " + nameof(CharacterSkinExtractor.V9Tolerance) + @"\s*=\s*\S+;\s*$");

            var copies = new List<string>();
            stood = 0;
            foreach ((string file, bool tolToo) in files)
            {
                string name = Path.GetFileName(file);
                string[] lines = StripComments10(File.ReadAllText(file)).Split('\n');
                for (int k = 0; k < lines.Length; k++)
                {
                    string line = lines[k].TrimEnd('\r');
                    string at = name + ":" + (k + 1).ToString(CultureInfo.InvariantCulture);
                    if (tolToo)
                        foreach (Match m in literal.Matches(line))
                        {
                            if (!double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v)) continue;
                            if (Array.IndexOf(tol, v) < 0) continue;
                            if (name == "CharacterSkinExtractor.V9.cs" && standing.IsMatch(line)) { stood++; continue; }
                            copies.Add($"{at}: {m.Value} is a TOL value: {line.Trim()}");
                        }
                    if (hex.IsMatch(line)) copies.Add($"{at}: an INK or EYE_WHITE colour: {line.Trim()}");
                    foreach ((string h, Regex re) in bytes)
                        if (re.IsMatch(line)) copies.Add($"{at}: {h} as three bytes: {line.Trim()}");
                    foreach ((string r, Regex re) in rowRes)
                        if (re.IsMatch(line)) copies.Add($"{at}: the rig's row {r}: {line.Trim()}");
                }
            }
            return copies;
        }

        /// <summary>The C# files of <paramref name="folder"/> whose names match <paramref name="names"/>.</summary>
        static List<string> Sources10(string root, string folder, string names)
        {
            string dir = Path.Combine(root, folder);
            Assert.IsTrue(Directory.Exists(dir), $"{folder} is gone; find where the port moved.");
            var files = new List<string>();
            foreach (string f in Directory.GetFiles(dir, "*.cs"))
                if (Regex.IsMatch(Path.GetFileName(f), names)) files.Add(f);
            files.Sort(StringComparer.Ordinal);
            Assert.IsNotEmpty(files, $"No file of {folder} matches {names}.");
            return files;
        }

        /// <summary>A pattern for one number as the rig prints it (<c>String(n)</c>), as C# may write it:
        /// with or without a leading zero or trailing zeros, with a type suffix.</summary>
        static string NumberPattern10(string js)
        {
            string body = js.StartsWith("-", StringComparison.Ordinal) ? js.Substring(1) : js;
            string sign = body.Length < js.Length ? "-" : "";
            if (!Regex.IsMatch(body, @"^\d+(\.\d+)?$")) return Regex.Escape(js);
            int dot = body.IndexOf('.');
            string number = dot < 0
                ? body + @"(?:\.0*)?"
                : (body.Substring(0, dot) == "0" ? "0?" : body.Substring(0, dot)) + @"\." + body.Substring(dot + 1) + "0*";
            return sign + number + "[fFdDmM]?";
        }

        /// <summary>C# source with its comments blanked and its strings kept: a copy inside a JS string
        /// the port evaluates is a copy. Line breaks inside a block comment are kept, so lines keep their
        /// numbers.</summary>
        static string StripComments10(string src)
        {
            var sb = new StringBuilder(src.Length);
            int i = 0, n = src.Length;
            while (i < n)
            {
                char c = src[i], d = i + 1 < n ? src[i + 1] : '\0';
                if (c == '/' && d == '/')
                {
                    while (i < n && src[i] != '\n') i++;
                    continue;
                }
                if (c == '/' && d == '*')
                {
                    i += 2;
                    while (i < n && !(src[i] == '*' && i + 1 < n && src[i + 1] == '/'))
                    {
                        if (src[i] == '\n') sb.Append('\n');
                        i++;
                    }
                    i += 2;
                    continue;
                }
                if (c == '@' && d == '"')
                {
                    int j = i + 2;
                    while (j < n)
                    {
                        if (src[j] == '"' && j + 1 < n && src[j + 1] == '"') { j += 2; continue; }
                        if (src[j] == '"') break;
                        j++;
                    }
                    j = Math.Min(j, n - 1);
                    sb.Append(src, i, j - i + 1);
                    i = j + 1;
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    int j = i + 1;
                    while (j < n && src[j] != c && src[j] != '\n')
                    {
                        if (src[j] == '\\') j++;
                        j++;
                    }
                    j = Math.Min(j, n - 1);
                    sb.Append(src, i, j - i + 1);
                    i = j + 1;
                    continue;
                }
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        // =======================================================================================
        // v10 exports 3. each of the ten aims inside AIM.bar_deg
        // =======================================================================================

        /// <summary>
        /// The golden aim on the ten (rig 10.3's <c>AIM</c>: its clip and frame, its targets and share),
        /// on both sides, lands inside <c>AIM.bar_deg</c> (ruled 4.0° for every build, owner): the rig's own
        /// worst, the port's worst on the fresh def, and the committed golden report's "within X°". The
        /// look guard holds the port to the rig within <see cref="GoldenAimToleranceDeg"/>
        /// (<see cref="V10_TheLookPortLandsWhereTheRigsGoldenCheckDoes"/>); this holds both to the bar.
        /// </summary>
        [Test]
        public void V10_EachOfTheTenAimsInsideTheRigsAimBar()
        {
            GuardRig9 guard = Guard10;
            Aim9 aim = AimOf9(guard);
            Assert.IsTrue(aim.Exported, $"{guard.Kit} exports no AIM.");
            Dictionary<string, (double Bar, int Inside)> golden = GoldenLookBars9(guard);
            var report = new StringBuilder();
            foreach (string preset in CharacterSkinExtractor.Presets9(guard.Host))
            {
                Assert.IsTrue(golden.TryGetValue(preset, out (double Bar, int Inside) bar),
                    $"The golden report holds no look check for '{preset}'.");
                LookAims9 a = LookPortAims9(guard, preset, aim);
                string line = string.Format(CultureInfo.InvariantCulture,
                    "{0}: the port's worst {1:0.000}° ({2}), the rig's {3:0.000}°, the report's {4:0.00}°, {5} targets inside",
                    preset, a.Worst, a.WorstAt, a.RigWorst, bar.Bar, a.Inside);
                report.Append("\n  ").Append(line);
                Assert.Greater(a.Inside, 0, $"{line}: no target inside the limits.");
                Assert.LessOrEqual(a.RigWorst, aim.Bar, $"{line}: the rig's own aim is past AIM.bar_deg {R9(aim.Bar)}°.");
                Assert.LessOrEqual(a.Worst, aim.Bar, $"{line}: the port's aim is past AIM.bar_deg {R9(aim.Bar)}°.");
                Assert.LessOrEqual(bar.Bar, aim.Bar, $"{line}: the committed golden report is past AIM.bar_deg {R9(aim.Bar)}°.");
            }
            Debug.Log($"[CharacterSkinBakeGuardTests] {guard.Kit.FileTag} aims inside AIM.bar_deg {R9(aim.Bar)}° ({aim}):{report}");
        }

        // =======================================================================================
        // v10 exports 4. no joint turns a half turn between two frames of a fishing clip
        // =======================================================================================

        /// <summary>
        /// On the ten fresh defs, no bone of a clip in the rig's fishing group (<c>GROUPS.fishing</c>: the
        /// five 10.3 re-keyed, cast, castBack, castRelease, strike and land, and hold, bite and reel) turns
        /// <see cref="HalfTurnBarDeg10"/> or more between two neighbouring frames: the angle between the
        /// def's local rotations, the short way, as the rig's own continuity check measures it. 10.2's rod
        /// sockets turned 180.0° in one frame of each of the five; 10.3's worst step on any bone is about
        /// 101° (the left tool socket in the cast). Neighbours only: the rig authors these clips as segments
        /// of a chain (its <c>SEGMENTS</c>), so the last frame does not play into the first.
        /// </summary>
        [Test]
        public void V10_NoFishingClipStepsAHalfTurn()
        {
            GuardRig9 guard = Guard10;
            string[] fishing = guard.Host.EvaluateString("String(" + guard.G + ".GROUPS.fishing.join(','))").Split(',');
            Assert.IsNotEmpty(fishing[0], $"{guard.Kit}'s GROUPS.fishing is empty.");
            var report = new StringBuilder();
            foreach (string preset in CharacterSkinExtractor.Presets9(guard.Host))
            {
                CharacterSkinDef def = guard.Bake(preset).Def;
                int n = def.Bones.Length;
                double worst = 0;
                string worstAt = "no step";
                foreach (string rigClip in fishing)
                {
                    CharacterSkinDef.SkinClip clip = def.Clips[RigClipIndex9(guard, def, rigClip)];
                    for (int f = 1; f < clip.FrameCount; f++)
                        for (int b = 0; b < n; b++)
                        {
                            double deg = StepDeg10(clip.KeyOf(f - 1, b, n).Rotation, clip.KeyOf(f, b, n).Rotation);
                            if (deg < worst && deg < HalfTurnBarDeg10) continue;
                            string at = string.Format(CultureInfo.InvariantCulture, "'{0}' {1} f{2}→f{3}", rigClip, def.Bones[b].Id, f - 1, f);
                            if (deg >= HalfTurnBarDeg10)
                                Assert.Fail(string.Format(CultureInfo.InvariantCulture,
                                    "{0} {1}: {2:0.0}° in one frame, the long way round (bar {3}°).", preset, at, deg, R9(HalfTurnBarDeg10)));
                            worst = deg;
                            worstAt = at;
                        }
                }
                report.Append("\n  ").Append(string.Format(CultureInfo.InvariantCulture, "{0}: worst {1:0.0}° ({2})", preset, worst, worstAt));
            }
            Debug.Log($"[CharacterSkinBakeGuardTests] {guard.Kit.FileTag} fishing group ({string.Join(", ", fishing)}), the worst step " +
                      $"between neighbouring frames, any bone (bar {R9(HalfTurnBarDeg10)}°):{report}");
        }

        /// <summary>The angle between two rotations, degrees, the short way.</summary>
        static double StepDeg10(Quaternion a, Quaternion b)
        {
            double dot = (double)a.x * b.x + (double)a.y * b.y + (double)a.z * b.z + (double)a.w * b.w;
            double na = Math.Sqrt((double)a.x * a.x + (double)a.y * a.y + (double)a.z * a.z + (double)a.w * a.w);
            double nb = Math.Sqrt((double)b.x * b.x + (double)b.y * b.y + (double)b.z * b.z + (double)b.w * b.w);
            return 2.0 * Math.Acos(Math.Min(1.0, Math.Abs(dot) / (na * nb))) * 180.0 / Math.PI;
        }

        // =======================================================================================
        // v10 exports 5. the sleeper lies centred on the pivot
        // =======================================================================================

        /// <summary>
        /// On every frame of the ten fresh defs' sleep clip, the pelvis lies where rig 10.3 lays it,
        /// centred on the pivot: at y = <c>D.pelvisZ − D.heightM / 2</c> of the build (read off the rig),
        /// so the figure's feet and crown sit half its height either side of the pivot, and on the
        /// figure's centre line (x = 0), within the rig's gate (<c>TOL.gate_m</c>). 10.2 laid every
        /// sleeper's pelvis at y = −0.18 m, so the figure lay off the pivot by 153–244 mm (intake A3).
        /// </summary>
        [Test]
        public void V10_TheSleeperLiesCentredOnThePivot()
        {
            GuardRig9 guard = Guard10;
            double gate = CharacterSkinExtractor.GateTolerance9(guard.Host);
            var report = new StringBuilder();
            foreach (string preset in CharacterSkinExtractor.Presets9(guard.Host))
            {
                double want = D9(guard.Host.EvaluateString(
                    "(function(){var D=" + guard.G + ".buildOf(" + JsQuote9(preset) + ").D;return String(D.pelvisZ-D.heightM/2);})()"));
                CharacterSkinDef def = guard.Bake(preset).Def;
                int pelvis = Array.FindIndex(def.Bones, b => string.Equals(b.Id, "pelvis", StringComparison.Ordinal));
                Assert.GreaterOrEqual(pelvis, 0, $"{preset}: the def has no pelvis bone.");
                CharacterSkinDef.SkinClip clip = def.Clips[RigClipIndex9(guard, def, "sleep")];
                Assert.Greater(clip.FrameCount, 0, $"{preset}: the sleep clip has no frames.");
                var w = new Matrix4x4[def.Bones.Length];
                double dy = 0, dx = 0;
                for (int f = 0; f < clip.FrameCount; f++)
                {
                    CharacterSkinPose.ComposeWorld(clip, f, def.Bones, w);
                    dy = Math.Max(dy, Math.Abs(w[pelvis].m13 - want));
                    dx = Math.Max(dx, Math.Abs((double)w[pelvis].m03));
                }
                string line = string.Format(CultureInfo.InvariantCulture,
                    "{0}: the pelvis at y {1:0.0000} m over {2} frames, off by at most {3:0.0e0} m along and {4:0.0e0} m across",
                    preset, want, clip.FrameCount, dy, dx);
                report.Append("\n  ").Append(line);
                Assert.LessOrEqual(dy, gate, $"{line}: not at pelvisZ - heightM / 2 (gate {R9(gate)} m).");
                Assert.LessOrEqual(dx, gate, $"{line}: off the centre line (gate {R9(gate)} m).");
            }
            Debug.Log($"[CharacterSkinBakeGuardTests] {guard.Kit.FileTag} sleepers, centred on the pivot:{report}");
        }
    }
}
