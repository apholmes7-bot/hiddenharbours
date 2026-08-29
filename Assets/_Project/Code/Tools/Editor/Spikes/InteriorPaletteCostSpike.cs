// Hidden Harbours — FULL MESH INTERIORS, PR 1: the palette wall, measured.
//
// The Otter ruling says a cap widens only with a MEASURED uniform cost. This spike is that
// measurement. It does NOT hand-write two shaders and compare them — it generates both candidates
// from the SHIPPED HiddenHarboursIsoFacet.shader by targeted text transforms, so the only
// difference between the arms is the change under test, and a transform that fails to apply is a
// hard error rather than a silently identical arm.
//
// Run headless:
//   Unity.exe -batchmode -quit -projectPath <worktree> \
//     -executeMethod HiddenHarbours.Tools.Editor.Spikes.InteriorPaletteCostSpike.Cli \
//     -spikeOut <abs path>
// (-quit is REQUIRED with -executeMethod, and FORBIDDEN with -runTests.)

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.Editor.Spikes
{
    /// <summary>
    /// Measures what each candidate interior-palette design actually costs, against the shipped
    /// facet shader as the control arm.
    ///
    /// <para><b>The three arms.</b> BASE is the shipped shader — which now CARRIES the scoped
    /// interior table, the design that was chosen. PRE reverses that change back out and is the
    /// control: its gate-off program must compile to the same bytes as BASE's. WIDEN replaces
    /// <c>float4 _RampMeta[16]</c> with <c>[48]</c> — the smallest widening that actually fits the
    /// measured fleet worst case (33 slots on the tanker; 24 and 32 both fail), and the design that
    /// was REJECTED, kept measurable here so reopening the question does not mean re-deriving it.</para>
    ///
    /// <para><b>What the numbers said.</b> Widening is byte-identical in the compiled program — it
    /// costs 512 B of constant buffer, not instructions — but every hull pays it in every frame.
    /// The shipped design costs nothing at all until a cut is live, because its table lives inside
    /// <c>#ifdef HH_LEVEL_GATE</c>; and it leaves the fleet's 16-slot law, guarded in three bake
    /// suites and load-bearing for the road fleet's #668 slot-reuse ruling, exactly where it was.</para>
    /// </summary>
    public static class InteriorPaletteCostSpike
    {
        const string ShippedShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursIsoFacet.shader";
        const string SpikeFolder = "Assets/_Project/Art/Shaders/_PaletteSpike";
        const string DefaultOut = "interior-palette-cost.txt";

        /// <summary>The fleet worst case this spike exists to clear, measured off the rigs and the
        /// committed hull defs by the V8 census (tanker: 13 hull ramps + 21 interior). 32 does not
        /// fit it; 48 does, with room for the fit-out to grow.</summary>
        public const int WidenedSlots = 48;

        /// <summary>Interior-only table size for the SCOPED arm — the measured worst interior
        /// (22, lobster) plus headroom, and deliberately NOT a number the hull can spend.</summary>
        public const int InteriorSlots = 24;

        [MenuItem("Hidden Harbours/Dev/Spikes/Interior Palette — Cost Of The Two Designs")]
        public static void Run()
        {
            string report = Measure();
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, DefaultOut);
            File.WriteAllText(path, report);
            Debug.Log("[InteriorPaletteCostSpike] wrote " + path + "\n" + report);
        }

        public static void Cli()
        {
            string outPath = null;
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], "-spikeOut", StringComparison.Ordinal)) outPath = args[i + 1];
            try
            {
                string report = Measure();
                if (string.IsNullOrEmpty(outPath))
                    outPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, DefaultOut);
                File.WriteAllText(outPath, report);
                Debug.Log("[InteriorPaletteCostSpike] OK -> " + outPath + "\n" + report);
            }
            catch (Exception e)
            {
                Debug.LogError("[InteriorPaletteCostSpike] FAILED: " + e);
            }
        }

        // ------------------------------------------------------------------------- the transforms

        /// <summary>One targeted edit to the shipped source. <see cref="Apply"/> throws if the
        /// anchor is absent or ambiguous, so an arm can never silently be a copy of the control.</summary>
        readonly struct Edit
        {
            public readonly string Find, Replace, Why;
            public Edit(string find, string replace, string why) { Find = find; Replace = replace; Why = why; }
        }

        static string Apply(string src, string arm, params Edit[] edits)
        {
            foreach (Edit e in edits)
            {
                int first = src.IndexOf(e.Find, StringComparison.Ordinal);
                if (first < 0)
                    throw new InvalidOperationException(
                        $"{arm}: anchor not found ({e.Why}). The shipped shader moved under this " +
                        $"spike; re-read it rather than nudging the anchor.\n---\n{e.Find}\n---");
                int second = src.IndexOf(e.Find, first + 1, StringComparison.Ordinal);
                if (second >= 0)
                    throw new InvalidOperationException(
                        $"{arm}: anchor is AMBIGUOUS ({e.Why}) — matches at {first} and {second}. " +
                        "A transform that patches the wrong one measures nothing.");
                src = src.Substring(0, first) + e.Replace + src.Substring(first + e.Find.Length);
            }
            return src;
        }

        const string MetaAnchor = "float4 _RampMeta[16];";
        static string Widen(string src) => Apply(
            Rename(src, "IsoFacetWiden"), "WIDEN",
            new Edit(MetaAnchor, $"float4 _RampMeta[{WidenedSlots}];",
                     "the _RampMeta declaration"));

        /// <summary>
        /// The gate-OFF program's size before full-mesh interiors, measured on this machine with
        /// this compiler at 27081bc4, immediately before the change: <b>vertex 1362, fragment
        /// 1878</b>; gate ON was 1610 / 2282.
        ///
        /// <para><b>Why a recorded number and not a generated control arm.</b> An arm that strips
        /// the change back out has to hand-copy shader text, and it goes stale on the next edit to
        /// the file it is copying — which is exactly how it failed the first time it was run. The
        /// property being proved is anyway structural: the change is 38 inserted lines and zero
        /// deleted ones, every insertion is inside <c>#ifdef HH_LEVEL_GATE</c> or in an
        /// <c>#ifdef/#else</c> whose <c>#else</c> branch is character-identical to the lines it
        /// replaced. This number is the measurement that confirms it, and section 3 reports the
        /// delta rather than asserting it, because a compiler upgrade may legitimately move it.</para>
        /// </summary>
        public static readonly (int vert, int frag) PreInteriorGateOff = (1362, 1878);
        public static readonly (int vert, int frag) PreInteriorGateOn = (1610, 2282);

        static string Rename(string src, string leaf) => Apply(src, "RENAME",
            new Edit("Shader \"HiddenHarbours/IsoFacet\"", $"Shader \"HiddenHarbours/_Spike/{leaf}\"",
                     "the Shader declaration"));

        // ---------------------------------------------------------------------------- the report

        public static string Measure()
        {
            var sb = new StringBuilder();
            var inv = CultureInfo.InvariantCulture;
            sb.AppendLine("INTERIOR PALETTE — THE COST OF THE TWO DESIGNS (full mesh interiors, PR 1)");
            sb.AppendLine("Both arms generated from the shipped facet shader by targeted transforms.");
            sb.AppendLine("Unity " + Application.unityVersion + " · " + SystemInfo.graphicsDeviceType
                          + " · " + SystemInfo.graphicsDeviceName);
            sb.AppendLine();

            string repo = Directory.GetParent(Application.dataPath).FullName;
            string shippedAbs = Path.Combine(repo, ShippedShaderPath.Replace('/', Path.DirectorySeparatorChar));
            string shipped = File.ReadAllText(shippedAbs).Replace("\r\n", "\n");

            // ---------- 1. the uniform arithmetic, which is the thing the Otter ruling names -----
            sb.AppendLine("1. UNIFORM COST — exact, not sampled. A float4[N] is 16N bytes of constant buffer.");
            sb.AppendLine();
            sb.AppendLine("   arm      _RampMeta   interior table   ramp uniform bytes   delta vs shipped");
            sb.AppendLine("   " + new string('-', 74));
            int baseBytes = 16 * 16;
            int widenBytes = 16 * WidenedSlots;
            int scopedOff = 16 * 16;
            int scopedOn = 16 * 16 + 16 * InteriorSlots;
            string Delta(int b) => (b - baseBytes >= 0 ? "+" : "") + (b - baseBytes).ToString(inv);
            sb.AppendLine($"   BASE          16            -          {baseBytes,5}            {"0",8}");
            sb.AppendLine($"   WIDEN     {WidenedSlots,5}            -          {widenBytes,5}            {Delta(widenBytes),8}  (ALL hulls, always)");
            sb.AppendLine($"   SCOPED off    16            -          {scopedOff,5}            {"0",8}  (byte-identical)");
            sb.AppendLine($"   SCOPED on     16       {InteriorSlots,5}          {scopedOn,5}            {Delta(scopedOn),8}  (only while a cut is live)");
            sb.AppendLine();
            sb.AppendLine("   WIDEN is paid by every hull in every frame, because _RampMeta is outside the");
            sb.AppendLine("   gate's #ifdef and every hull material carries it. SCOPED's extra table lives");
            sb.AppendLine("   inside #ifdef HH_LEVEL_GATE, and IsoFacetHullRenderer.ApplyCutawayKeyword only");
            sb.AppendLine("   enables that keyword while a cut is actually live — so on a harbour of boats");
            sb.AppendLine("   nobody is inside, SCOPED costs literally nothing and WIDEN costs 512 B each.");
            sb.AppendLine();

            // ---------- 2. does each arm actually compile, on this machine's real target ---------
            sb.AppendLine("2. COMPILE — every arm, both keyword states, on the shipped target.");
            sb.AppendLine();

            string spikeAbs = Path.Combine(repo, SpikeFolder.Replace('/', Path.DirectorySeparatorChar));
            var built = new List<(string arm, string assetPath, string shaderName)>();
            try
            {
                Directory.CreateDirectory(spikeAbs);
                Write(spikeAbs, "IsoFacetWiden.shader", Widen(shipped));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                built.Add(("BASE  ", ShippedShaderPath, "HiddenHarbours/IsoFacet"));
                built.Add(("WIDEN ", SpikeFolder + "/IsoFacetWiden.shader", "HiddenHarbours/_Spike/IsoFacetWiden"));

                foreach (var (arm, assetPath, shaderName) in built)
                {
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                    if (shader == null) { sb.AppendLine($"   {arm}  COULD NOT LOAD {assetPath}"); continue; }

                    var msgs = ShaderUtil.GetShaderMessages(shader);
                    int errs = msgs.Count(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error);
                    int warns = msgs.Length - errs;

                    var mat = new Material(shader);
                    int passes = mat.passCount > 0 ? mat.passCount : 1;
                    try
                    {
                        for (int p = 0; p < passes; p++) ShaderUtil.CompilePass(mat, p, true);
                        msgs = ShaderUtil.GetShaderMessages(shader);
                        errs = msgs.Count(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error);
                        warns = msgs.Length - errs;
                    }
                    finally { UnityEngine.Object.DestroyImmediate(mat); }

                    sb.AppendLine($"   {arm}  name={shaderName,-38} passes={passes}  errors={errs}  warnings={warns}");
                    foreach (var m in msgs.Take(6))
                        sb.AppendLine($"            [{m.severity}] {m.message} {m.messageDetails}".TrimEnd());
                }
                sb.AppendLine();

                // ---------- 3. the compiled program itself, per keyword state --------------------
                sb.AppendLine("3. COMPILED PROGRAM BYTES — per keyword state. The number that would show a");
                sb.AppendLine("   widened uniform array costing registers or instructions rather than just bytes.");
                sb.AppendLine();
                sb.AppendLine("   arm      keywords              vertex B    fragment B   ok");
                sb.AppendLine("   " + new string('-', 68));
                foreach (var (arm, assetPath, _) in built)
                {
                    var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                    if (shader == null) continue;
                    foreach (string[] kw in new[] { Array.Empty<string>(), new[] { "HH_LEVEL_GATE" } })
                    {
                        string label = kw.Length == 0 ? "(gate off)" : "HH_LEVEL_GATE";
                        var r = CompiledBytes(shader, kw);
                        sb.AppendLine($"   {arm}  {label,-20} {r.vert,9} {r.frag,13}   {r.note}");
                    }
                }
                sb.AppendLine("   Recorded before the change, same machine and compiler, at 27081bc4:");
                sb.AppendLine($"     gate off  vertex {PreInteriorGateOff.vert}  fragment {PreInteriorGateOff.frag}");
                sb.AppendLine($"     gate on   vertex {PreInteriorGateOn.vert}  fragment {PreInteriorGateOn.frag}");
                sb.AppendLine("   The gate-OFF row above must still match it: that is the identity proof, and it");
                sb.AppendLine("   is the whole claim that a hull nobody is aboard pays nothing for this feature.");
                sb.AppendLine();
            }
            finally
            {
                if (Directory.Exists(spikeAbs))
                {
                    AssetDatabase.DeleteAsset(SpikeFolder);
                    if (Directory.Exists(spikeAbs)) Directory.Delete(spikeAbs, true);
                    string meta = spikeAbs + ".meta";
                    if (File.Exists(meta)) File.Delete(meta);
                    AssetDatabase.Refresh();
                }
            }

            // ---------- 4. the draw-call arm the handoff actually named --------------------------
            sb.AppendLine("4. THE SUB-MESH VARIANT OF CANDIDATE (b), for completeness.");
            sb.AppendLine();
            sb.AppendLine("   Scoping the second table by MATERIAL SLOT instead of by the interior tag costs");
            sb.AppendLine("   one extra draw per hull that has interior geometry visible, and splits the hull");
            sb.AppendLine("   into two sub-meshes. Structural, not sampled: every committed hull mesh is");
            sb.AppendLine("   subMeshCount = 1 today (measured by the S0 spike, 24/24), and IsoFacetHullRenderer");
            sb.AppendLine("   draws one material per hull. It is strictly worse than selecting on a tag the");
            sb.AppendLine("   vertex data already carries, so it is reported rather than built.");
            sb.AppendLine();

            return sb.ToString();
        }

        static void Write(string folder, string name, string text) =>
            File.WriteAllText(Path.Combine(folder, name), text.Replace("\n", System.Environment.NewLine));

        /// <summary>
        /// Compiled vertex/fragment blob sizes for one keyword set, via the editor's own variant
        /// compiler. Reached by REFLECTION on purpose: <c>ShaderData.Pass.CompileVariant</c> has had
        /// several overloads across editor versions, and a wrong signature would cost a whole Unity
        /// cycle to discover. Reports "n/a" rather than throwing when the API is not as expected —
        /// a missing convenience must not take the uniform arithmetic (section 1) down with it.
        /// </summary>
        static (string vert, string frag, string note) CompiledBytes(Shader shader, string[] keywords)
        {
            try
            {
                // Resolved BY NAME, not by a static reference: this type has moved namespace across
                // editor versions (it is NOT UnityEditor.Rendering.ShaderData in 6000.5), and a
                // compile-time reference to the wrong one costs a whole Unity cycle to discover.
                Type sdType = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.GetName().Name.StartsWith("UnityEditor", StringComparison.Ordinal))
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                    .FirstOrDefault(t => t.Name == "ShaderData" && t.IsPublic);
                if (sdType == null) return ("n/a", "n/a", "ShaderData type not found");

                // Two accessors exist across versions: ShaderUtil.GetShaderData(shader) and the
                // static ShaderData.From(shader). Try both rather than guess.
                object sd =
                    typeof(ShaderUtil).GetMethod("GetShaderData", BindingFlags.Public | BindingFlags.Static,
                                                 null, new[] { typeof(Shader) }, null)
                                      ?.Invoke(null, new object[] { shader })
                    ?? sdType.GetMethod("From", BindingFlags.Public | BindingFlags.Static,
                                        null, new[] { typeof(Shader) }, null)
                             ?.Invoke(null, new object[] { shader });
                if (sd == null)
                    return ("n/a", "n/a", "no accessor: tried ShaderUtil.GetShaderData and "
                                          + sdType.FullName + ".From");

                // Each step names what it actually found when it fails, so ONE more run settles the
                // shape rather than another round of guessing at API names.
                object sub = sdType.GetMethod("GetSubshader")?.Invoke(sd, new object[] { 0 });
                if (sub == null)
                    return ("n/a", "n/a", "no GetSubshader; ShaderData has: " + Names(sdType));
                object pass = sub.GetType().GetMethod("GetPass")?.Invoke(sub, new object[] { 0 });
                if (pass == null)
                    return ("n/a", "n/a", "no GetPass; Subshader has: " + Names(sub.GetType()));

                MethodInfo cv = pass.GetType().GetMethods()
                    .Where(m => m.Name == "CompileVariant")
                    .OrderBy(m => m.GetParameters().Length).FirstOrDefault();
                if (cv == null)
                    return ("n/a", "n/a", "no CompileVariant; Pass has: " + Names(pass.GetType()));

                string Size(object shaderTypeValue)
                {
                    object[] argv = cv.GetParameters().Select(p =>
                    {
                        Type t = p.ParameterType;
                        if (t.Name == "ShaderType") return shaderTypeValue;
                        if (t == typeof(string[])) return keywords;
                        if (t.Name == "ShaderCompilerPlatform") return Enum.Parse(t, "D3D");
                        if (t == typeof(BuildTarget)) return BuildTarget.StandaloneWindows64;
                        if (t.Name == "GraphicsTier") return Enum.Parse(t, "Tier1");
                        if (t == typeof(bool)) return false;
                        return t.IsValueType ? Activator.CreateInstance(t) : null;
                    }).ToArray();

                    object info = cv.Invoke(pass, argv);
                    if (info == null) return "null";
                    Type it = info.GetType();
                    bool ok = (bool)(it.GetProperty("Success")?.GetValue(info) ?? false);
                    var blob = it.GetProperty("ShaderData")?.GetValue(info) as byte[];
                    return ok ? (blob?.Length.ToString(CultureInfo.InvariantCulture) ?? "0") : "FAIL";
                }

                Type stType = cv.GetParameters().First(p => p.ParameterType.Name == "ShaderType").ParameterType;
                return (Size(Enum.Parse(stType, "Vertex")), Size(Enum.Parse(stType, "Fragment")), "ok");
            }
            catch (Exception e)
            {
                return ("n/a", "n/a", e.GetType().Name + ": " + e.Message.Split('\n')[0]);
            }
        }

        /// <summary>Public instance/static member names of a type, for a diagnostic that has to be
        /// actionable on the first read — an API-shape guess costs a whole Unity cycle.</summary>
        static string Names(Type t) => string.Join(" ", t.GetMembers(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(m => m.Name).Distinct().OrderBy(s => s, StringComparer.Ordinal).Take(24));
    }
}
