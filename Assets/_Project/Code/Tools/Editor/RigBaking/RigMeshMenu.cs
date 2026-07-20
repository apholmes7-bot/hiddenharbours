using System;
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
        /// <summary>The hulls ADR 0022 measured. The side dragger is deliberately NOT in
        /// <see cref="RigCatalog"/> — see <see cref="RigMeshExtractor.ExtractFrom"/>.</summary>
        public static readonly (string label, string path, string global)[] Hulls =
        {
            ("lobster boat (12 m)", "docs/art/rigs/lobsterBoatIsoRig.js", "LobsterBoatIso"),
            ("side dragger (25 m)", "docs/art/rigs/sideDraggerIsoRig.js", "SideDraggerIso"),
            ("punt (golden master)", "docs/art/rigs/puntIsoRig.js", "PuntIso"),
        };

        [MenuItem(RigMeshGate.MenuRoot + "/Verify hull meshes against the rigs", priority = 210)]
        public static void VerifyAll()
        {
            var report = new StringBuilder("[rig-mesh] ADR 0022 phase-2 verification\n");
            bool ok = true;

            foreach (var (label, path, global) in Hulls)
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
        }

        [MenuItem(RigMeshGate.MenuRoot + "/Verify hull meshes against the rigs", validate = true)]
        static bool VerifyAllValidate() => RigMeshGate.Enabled;
    }
}
