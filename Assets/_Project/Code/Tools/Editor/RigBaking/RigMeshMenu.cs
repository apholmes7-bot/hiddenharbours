using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// The gate on ADR 0022 phase 2.
    ///
    /// <para><b>Why the gate is a feature flag and not <c>defineConstraints</c>.</b> Compiling this
    /// code out would hide it from CI, and the failure mode that costs this project most is a
    /// Unity 6000.5 editor API that is obsolete-as-ERROR and only surfaces in a CI compile. Gated
    /// therefore means: it always COMPILES, it is wired to nothing, it writes no assets, and its
    /// owner-facing entry points are off until deliberately switched on. The sprite bake path
    /// (<see cref="RigBaker"/>, <see cref="RigBakeMenu"/>) is not touched by any of it.</para>
    /// </summary>
    public static class RigMeshGate
    {
        const string Pref = "HiddenHarbours.RigMesh.Enabled";
        public const string MenuRoot = "Hidden Harbours/Art/3D Hulls (ADR 0022, experimental)";

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(Pref, false);
            set => EditorPrefs.SetBool(Pref, value);
        }

        [MenuItem(MenuRoot + "/Enable mesh extraction", priority = 200)]
        static void Toggle() => Enabled = !Enabled;

        [MenuItem(MenuRoot + "/Enable mesh extraction", validate = true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuRoot + "/Enable mesh extraction", Enabled);
            return true;
        }
    }

    /// <summary>
    /// Owner-facing verification. Extracts a hull's mesh and reports how far the mesh's own render
    /// is from the rig's — the phase-2 acceptance question, answered in numbers rather than in a
    /// green tick. Writes nothing.
    /// </summary>
    public static class RigMeshMenu
    {
        /// <summary>
        /// The hulls ADR 0022 measured, sourced from <see cref="HullMeshFleet"/> so the paths cannot
        /// drift from what the baker actually bakes. The side dragger is deliberately NOT in
        /// <see cref="RigCatalog"/> — see <see cref="RigMeshExtractor.ExtractFrom"/>.
        ///
        /// <para><b>Why this stayed a SUBSET when the fleet grew to eleven.</b> Verification
        /// rasterises 8 headings twice per hull on the CPU, at the rig's own cell size. That is
        /// seconds for a 12 m boat and minutes for the 110 m tanker (16 px/m, but an enormous cell),
        /// so running the whole fleet would turn an owner-facing sanity check into an editor stall he
        /// would learn not to press. These three are the ones worth his time: the golden master, the
        /// first mesh hull, and the largest hull with a measured baseline. Whole-fleet coverage is a
        /// TEST's job (it can afford the minutes and nobody waits on it) — see
        /// <c>HullMeshFleetBakeTests</c>.</para>
        /// </summary>
        public static readonly (string label, string path, string global)[] Hulls =
            new[] { "lobsterBoat", "sideDragger", "punt" }
                .Select(HullMeshFleet.Get)
                .Select(h => (h.Label, h.ScriptPath, h.GlobalName))
                .ToArray();

        [MenuItem(RigMeshGate.MenuRoot + "/Verify hull meshes against the rigs", priority = 210)]
        public static void VerifyAll() => Verify(Hulls, "ADR 0022 phase-2 verification");

        /// <summary>
        /// <b>The whole fleet against the CPU oracle</b> — headless entry (-executeMethod), because
        /// this is the check that costs minutes and therefore belongs to a machine rather than to the
        /// owner's editor (see <see cref="Hulls"/>).
        ///
        /// <para>It is the acceptance evidence for a hull with no baked sheet, and it is the ONLY
        /// thing that adjudicates a RECONSTRUCTED material table
        /// (<see cref="RigMeshSymbols.Reconstructions"/> — the dory's): the truth side of the compare
        /// is the rig's own <c>render()</c>, which selects colours its own inline way, while the
        /// candidate side goes through the reconstruction. If the reconstruction were wrong, every
        /// lit pixel on the boat would be the wrong colour and this would read tens of percent, not
        /// tenths.</para>
        ///
        /// <para>Pure CPU arithmetic through V8 and managed code — no graphics device — so unlike the
        /// GPU acceptance fixtures this one is meaningful on CI.</para>
        /// </summary>
        public static void VerifyFleetCli()
        {
            var hulls = HullMeshFleet.Hulls.Select(h => (h.Label, h.ScriptPath, h.GlobalName)).ToArray();
            if (!Verify(hulls, $"ADR 0022 phase-6 fleet verification — {hulls.Length} hulls"))
                EditorApplication.Exit(1);
        }

        /// <summary>Returns true when every hull came in under the bar. Reports either way.</summary>
        static bool Verify((string label, string path, string global)[] hulls, string title)
        {
            var report = new StringBuilder($"[rig-mesh] {title}\n");
            bool ok = true;

            foreach (var (label, path, global) in hulls)
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    using IRigScriptHost host = RigScriptHostFactory.Create();
                    var data = RigMeshExtractor.ExtractFrom(host, path, global);
                    var build = RigMeshBuilder.Build(data);
                    double extractMs = sw.Elapsed.TotalMilliseconds;

                    report.Append($"\n  {label}\n    {data}\n    {build}, extracted in {extractMs:F0} ms\n");

                    long sheetBytes = (long)data.W * data.H * 4 * 32 * 4;   // 32 facings × 4 rock
                    report.Append($"    sheet equivalent (32 dir × 4 rock, RGBA32): " +
                                  $"{sheetBytes / 1048576.0:F1} MiB → ratio {sheetBytes / (double)build.BufferBytes:F0}×\n");

                    double worstFaces = 0, worstMesh = 0;
                    for (int dir = 0; dir < 8; dir++)
                    {
                        var view = new RigViewOptions(dir, data.DefaultElev);
                        byte[] truth = host.EvaluateBytes($"{global}.render({view.ToJsArgs()})");
                        var dFaces = RigMeshReferenceRasterizer.Compare(
                            truth, RigMeshReferenceRasterizer.RenderFromFaces(data, view), data.W, data.H);
                        var dMesh = RigMeshReferenceRasterizer.Compare(
                            truth, RigMeshReferenceRasterizer.RenderFromMesh(data, build.Mesh, view), data.W, data.H);
                        worstFaces = Math.Max(worstFaces, dFaces.PercentDiffering);
                        worstMesh = Math.Max(worstMesh, dMesh.PercentDiffering);
                        report.Append($"    dir {dir}: faces {dFaces}\n             mesh  {dMesh}\n");
                    }

                    report.Append($"    WORST — extracted faces (f64): {worstFaces:F4}%   " +
                                  $"built mesh (f32): {worstMesh:F4}%\n");
                    if (worstMesh > 0.5) ok = false;

                    UnityEngine.Object.DestroyImmediate(build.Mesh);
                }
                catch (Exception e)
                {
                    ok = false;
                    report.Append($"\n  {label}: FAILED — {e.Message}\n");
                }
            }

            if (ok) Debug.Log(report.ToString());
            else Debug.LogError(report.ToString());
            return ok;
        }

        [MenuItem(RigMeshGate.MenuRoot + "/Verify hull meshes against the rigs", validate = true)]
        static bool VerifyAllValidate() => RigMeshGate.Enabled;
    }
}
