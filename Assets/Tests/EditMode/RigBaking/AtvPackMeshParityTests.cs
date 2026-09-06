using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// ⭐⭐ <b>THE PARITY: does the extracted mesh draw the machine the rig draws?</b>
    ///
    /// <para>The truth side is the rig's own <c>render(dir, opts)</c>. The candidate side is the
    /// SAME extraction the bake used, rasterised by the repo's CPU oracle — first as the extracted
    /// FACE list (f64) and then as the BUILT MESH (f32), so a residual can be attributed to the
    /// extraction or to the float32 vertex buffer rather than argued about.</para>
    ///
    /// <para><b>Why it earns its place on this drop in particular.</b> The ATV rig exports no
    /// <c>MATS</c>, so its ramp table is RECONSTRUCTED
    /// (<c>RigMeshSymbols.Reconstructions["atvIsoRig.js"]</c>) — and this is the only thing that
    /// adjudicates a reconstruction. The truth side selects its colours the rig's own inline way;
    /// the candidate side goes through the reconstruction. If the reconstruction were wrong — a
    /// dropped ramp, or the wrong key first, which the face packer resolves to index 0 — every lit
    /// pixel on the machine would be the wrong colour and this would read in whole percent, not in
    /// hundredths. The Dually's identical gap was found exactly this way.</para>
    ///
    /// <para>⚠️ <b>The body has to be NAMED on both sides.</b> <c>AtvIso.resolve</c> falls back to
    /// the quad, silently and pixel-for-pixel, so a truth render without the body would compare all
    /// three machines against a quad — two of them would read enormous and the third would pass for
    /// the wrong reason. The <c>opts</c> come from the fleet's own <c>ViewOptions</c>, which is the
    /// same string the azimuth probe uses.</para>
    ///
    /// <para>Pure CPU arithmetic through V8 and managed code — no graphics device — so this is
    /// meaningful on CI, unlike the GPU acceptance fixtures.</para>
    /// </summary>
    public class AtvPackMeshParityTests
    {
        /// <summary>The bar <c>RigMeshMenu.Verify</c> holds the whole hull fleet to. Applied here to
        /// the FACES-versus-MESH comparison, which is the one it is really about: what the float32
        /// vertex buffer costs against the f64 face list it was built from.</summary>
        const double MaxPercentDiffering = 0.5;

        const string PlateDir = "docs/art/spikes/atv-pack";

        static string Full(string repoRelative) => Path.Combine(RigCatalog.RepoRoot, repoRelative);

        public static readonly string[] Keys = { "enduro250", "trike200", "utilityQuad" };

        /// <summary>
        /// ⚠️ The rig's <c>render</c> takes <c>(dir, opts)</c> and this has to carry BOTH the view
        /// and the body. <see cref="RigViewOptions.ToJsArgs"/> writes only the view, so the body is
        /// merged in here — from the vehicle's own <c>ViewOptions</c>, never re-typed.
        /// </summary>
        static string TruthArgs(in RigViewOptions view, string bodyOpts)
        {
            string args = view.ToJsArgs();                       // "dir,{elev:…,roll:…,…}"
            int brace = args.IndexOf('{');
            string dir = args.Substring(0, brace);
            string opts = args.Substring(brace);
            // {body:'quad'} + {elev:…} → {elev:…,body:'quad'}; the rig reads whichever it finds.
            string inner = bodyOpts.Trim().TrimStart('{').TrimEnd('}');
            // ⚠️ `outline:true` — the oracle draws its 1-px keyline UNCONDITIONALLY and these rigs
            // are RINGLESS by default (ADR 0031). Asking the rig for its OWN outline puts both
            // sides in one style; see the doc on EachBodysMeshDrawsWhatHerRigDraws for the 351-px
            // measurement that made it necessary.
            return dir + opts.Insert(opts.Length - 1, "," + inner + ",outline:true");
        }

        /// <summary>
        /// ⭐⭐ <b>Three claims, each answering one question, and none of them a threshold on a
        /// number whose causes are mixed.</b>
        ///
        /// <list type="number">
        ///   <item><b>The silhouette is EXACT.</b> Every pixel the rig paints, the mesh paints. A
        ///   dropped face, a face in the wrong place, or a body pick that quietly became the quad
        ///   fails here and nowhere else.</item>
        ///   <item><b>Every colour the mesh gets wrong is in the SAME RAMP as the rig's.</b> This is
        ///   the reconstruction's adjudication: a table that dropped a ramp, or put the wrong key
        ///   first (the packer resolves an unknown material to index 0), paints a face in a
        ///   completely different colour — a DIFFERENT ramp — and fails. What survives is a shade
        ///   step within the right ramp, which is what a texture the oracle does not model does.</item>
        ///   <item><b>The float32 vertex buffer costs nothing</b> — faces (f64) against mesh (f32),
        ///   at the fleet's own 0.5% bar.</item>
        /// </list>
        ///
        /// <para>⚠️⚠️ <b>The truth side is rendered with <c>outline:true</c>, and that is a
        /// correction rather than a convenience.</b> Measured 2026-09-06: the CPU oracle draws its
        /// 1-px keyline <b>unconditionally</b> — it predates ADR 0031 — while these rigs are
        /// RINGLESS by default (<c>KEYLINE_DEFAULT = false</c>, as the Dually's is). Compared
        /// against a bare rig render the mesh therefore paints a perfect one-pixel border the rig
        /// does not: <b>351 surplus pixels at E on the bike, and ZERO missing ones</b>. That is the
        /// oracle's ring, not the bake's — the hull rigs draw theirs unconditionally too, which is
        /// why the hull fleet has never seen it. Asking the RIG for its own outline puts both sides
        /// in the same style and keeps the comparison a statement about the extracted data.</para>
        ///
        /// <para><b>What is left over, and why it is not a defect.</b> The oracle models
        /// <c>{ramp, off}</c> and a uniform Bayer; it does not model procedural <c>tex</c>, which
        /// this pack uses heavily — <c>lugTex</c> on every tyre's tread and <c>wearTex</c> on the
        /// weathered plastics. Those shift a pixel one step along its own ramp, which is exactly
        /// what claim 2 permits and claim 2 alone would catch if it were anything else.</para>
        /// </summary>
        [Test]
        public void EachBodysMeshDrawsWhatHerRigDraws([ValueSource(nameof(Keys))] string key)
        {
            VehicleRigFleet.Vehicle v = VehicleRigFleet.Get(key);

            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigMeshData data = RigMeshExtractor.ExtractFrom(host, v.ScriptPath, v.GlobalName,
                                                            hull: v.Extraction);
            RigMeshBuild build = RigMeshBuilder.Build(data);

            try
            {
                // Every colour any ramp can produce, as packed RGB → the set of ramps carrying it.
                // Built from the RECONSTRUCTED table, so claim 2 is a statement about that table.
                var ramps = new Dictionary<int, HashSet<int>>();
                for (int m = 0; m < data.Materials.Count; m++)
                    foreach (Color32 c in data.Materials[m].Ramp)
                    {
                        int packed = (c.r << 16) | (c.g << 8) | c.b;
                        if (!ramps.TryGetValue(packed, out HashSet<int> owners))
                            ramps[packed] = owners = new HashSet<int>();
                        owners.Add(m);
                    }
                int keyline = (data.Keyline.r << 16) | (data.Keyline.g << 8) | data.Keyline.b;

                var report = new StringBuilder($"[atv-parity] {key} — {data}\n    {build}\n");
                int missing = 0, surplus = 0, shared = 0, shade = 0, foreign = 0, ties = 0;
                string firstForeign = null;
                double worstF32 = 0;
                var foreignPixels = new List<(int dir, int pixel, int rig, int mesh)>();
                int firstOfThisDir = 0;

                for (int dir = 0; dir < 8; dir++)
                {
                    int dTie = 0;
                    var view = new RigViewOptions(dir, data.DefaultElev);
                    byte[] truth = host.EvaluateBytes(
                        $"{v.GlobalName}.render({TruthArgs(view, v.Extraction.ViewOptions)})");
                    byte[] mesh = RigMeshReferenceRasterizer.RenderFromMesh(data, build.Mesh, view);
                    byte[] faces = RigMeshReferenceRasterizer.RenderFromFaces(data, view);

                    RigPixelDiff f32 = RigMeshReferenceRasterizer.Compare(faces, mesh, data.W, data.H);
                    worstF32 = Math.Max(worstF32, f32.PercentDiffering);

                    int dMissing = 0, dSurplus = 0, dShade = 0, dForeign = 0;
                    for (int i = 0; i < data.W * data.H; i++)
                    {
                        bool t = truth[i * 4 + 3] != 0, m2 = mesh[i * 4 + 3] != 0;
                        if (t && !m2) { dMissing++; continue; }
                        if (!t && m2) { dSurplus++; continue; }
                        if (!t) continue;
                        shared++;

                        int ct = (truth[i * 4] << 16) | (truth[i * 4 + 1] << 8) | truth[i * 4 + 2];
                        int cm = (mesh[i * 4] << 16) | (mesh[i * 4 + 1] << 8) | mesh[i * 4 + 2];
                        if (ct == cm) continue;

                        // Same ramp = a shade step (a texture the oracle does not model, or the
                        // edge-darkening's own −2). A different ramp = the table resolved a face to
                        // the wrong material, which is the failure this whole fixture exists for.
                        bool sameRamp =
                            ramps.TryGetValue(ct, out HashSet<int> a) &&
                            ramps.TryGetValue(cm, out HashSet<int> b) && a.Overlaps(b);
                        if (sameRamp || ct == keyline || cm == keyline) { dShade++; continue; }

                        // ⭐⭐ NOT YET A FAILURE — a foreign ramp has one other possible cause, and
                        // it is one the rasteriser's own instrumentation exists to name: the RIGHT
                        // face losing a DEPTH TIE to a coplanar neighbour of a different material.
                        // Established below from the paint trace rather than tolerated here.
                        foreignPixels.Add((dir, i, ct, cm));
                    }

                    // Ask the oracle WHICH faces reached each foreign pixel, and whether any of them
                    // belongs to the ramp the rig painted from. If one does, the two renderers chose
                    // different winners at a tie — both faces are there, in the right place, shading
                    // correctly, and the pixel is not a statement about the reconstruction. If NONE
                    // does, nothing that reached the pixel could have painted the rig's colour, and
                    // that IS the reconstruction (or the geometry) being wrong.
                    if (foreignPixels.Count > firstOfThisDir)
                    {
                        var probe = new RigPaintTrace();
                        for (int k = firstOfThisDir; k < foreignPixels.Count; k++)
                            probe.Probe.Add(foreignPixels[k].pixel);
                        RigMeshReferenceRasterizer.RenderFromMesh(data, build.Mesh, view, null, probe);

                        for (int k = firstOfThisDir; k < foreignPixels.Count; k++)
                        {
                            (int d2, int px, int rigCol, int meshCol) = foreignPixels[k];
                            bool reachedByThatRamp = false;
                            foreach (RigPaintTrace.Sample s in probe.Samples)
                            {
                                if (s.Pixel != px || s.Mat < 0 || s.Mat >= data.Materials.Count) continue;
                                foreach (Color32 c in data.Materials[s.Mat].Ramp)
                                    if (((c.r << 16) | (c.g << 8) | c.b) == rigCol)
                                    { reachedByThatRamp = true; break; }
                                if (reachedByThatRamp) break;
                            }

                            if (reachedByThatRamp) { dTie++; continue; }
                            dForeign++;
                            firstForeign ??= $"dir {d2} px {px % data.W},{px / data.W}: " +
                                             $"rig #{rigCol:x6} vs mesh #{meshCol:x6}, and NO face " +
                                             "that reached that pixel belongs to a ramp carrying the " +
                                             "rig's colour";
                        }
                    }
                    firstOfThisDir = foreignPixels.Count;

                    missing += dMissing; surplus += dSurplus; shade += dShade;
                    foreign += dForeign; ties += dTie;
                    report.Append($"    dir {dir}: {dMissing} missing, {dSurplus} surplus, " +
                                  $"{dShade} shade-step, {dTie} depth-tie, {dForeign} FOREIGN-RAMP" +
                                  $"   |   f32 {f32}\n");
                }

                report.Append($"    TOTALS over 8 facings — missing {missing}, surplus {surplus}, " +
                              $"shade-step {shade} of {shared} shared px " +
                              $"({(shared == 0 ? 0 : 100.0 * shade / shared):F2}%), " +
                              $"depth-tie {ties}, foreign-ramp {foreign}. " +
                              $"Worst f32 residual {worstF32:F4}%.\n");
                Debug.Log(report.ToString());

                Assert.That(missing, Is.Zero,
                    $"'{key}': the mesh MISSES {missing} pixels the rig paints, over 8 facings. Her " +
                    "extraction dropped geometry, put it somewhere else, or — the silent one on this " +
                    "rig — resolved to a different body: AtvIso.resolve falls back to the QUAD for an " +
                    "unknown pick, at zero pixels' difference from a real quad.");

                Assert.That(foreign, Is.Zero,
                    $"'{key}': {foreign} pixels are painted from a ramp NOTHING that reached them " +
                    $"belongs to — first at {firstForeign}.\n" +
                    "⚠️ This rig exports no MATS, so RigMeshSymbols RECONSTRUCTS the ramp table. A " +
                    "dropped ramp, or the wrong key first (the face packer resolves an unknown " +
                    "material name to index 0), paints whole faces in another material's colours. " +
                    "This count EXCLUDES the two causes that are not that, and both are established " +
                    "from the oracle's own paint trace rather than tolerated: a shade step inside " +
                    "the right ramp (a procedural texture the oracle does not model), and a DEPTH " +
                    $"TIE where a coplanar neighbour of another material also reached the pixel " +
                    $"({ties} of those over 8 facings). What is left has no face behind it.");

                Assert.That(worstF32, Is.LessThanOrEqualTo(MaxPercentDiffering),
                    $"'{key}': the built mesh draws {worstF32:F4}% differently from the face list it " +
                    "was built FROM — that is the float32 vertex buffer alone, and it is over the " +
                    "bar the whole hull fleet is held to.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(build.Mesh);
            }
        }

        // =============================================================================================
        //  THE PLATES — written from the RIG, which is what a reviewer has to judge against
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The bike parked against the bike ridden, side by side</b> — trap 1 made visible.
        ///
        /// <para>A reviewer cannot see from a face count that the rig's default is a machine on its
        /// side stand. They can see it here, and the plate is what makes "the rest pose carries
        /// <c>stand:0</c>" a decision somebody checked rather than a line in a table.</para>
        /// </summary>
        [Test]
        public void PlateTheBikeParkedAgainstTheBikeRidden()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            Widen(host);

            byte[] parked = host.EvaluateBytes(
                "AtvIso.render(3,{body:'dirtbike',paint:'teal',weather:0.3})");
            byte[] ridden = host.EvaluateBytes(
                "AtvIso.render(3,{body:'dirtbike',paint:'teal',weather:0.3,stand:0})");

            RigPixelDiff diff = RigMeshReferenceRasterizer.Compare(parked, ridden, 256, 192);
            Debug.Log($"[atv-plate] enduro parked vs ridden at SE: {diff}");

            Assert.That(diff.PercentDiffering, Is.GreaterThan(1.0),
                "the parked and ridden bikes now draw the same. If the rig's default stopped being " +
                "stand 1, the rest pose's stand:0 has lost its reason — check before removing it.");

            WritePlate("enduro-parked-vs-ridden.png", 256, 192, parked, ridden);
        }

        /// <summary>The quad with her racks, hitch and winch against the bare machine — the fitted
        /// parts are BUILD variants, not poses, so the bake takes one of them and this is which.</summary>
        [Test]
        public void PlateTheQuadFittedAgainstTheQuadBare()
        {
            using IRigScriptHost host = RigScriptHostFactory.Create();
            Widen(host);

            byte[] fitted = host.EvaluateBytes(
                "AtvIso.render(3,{body:'quad',paint:'sage',weather:0.42,rackF:true,rackR:true," +
                "hitch:true,winch:true})");
            byte[] bare = host.EvaluateBytes(
                "AtvIso.render(3,{body:'quad',paint:'sage',weather:0.42,rackF:false,rackR:false," +
                "hitch:false,winch:false})");

            RigPixelDiff diff = RigMeshReferenceRasterizer.Compare(fitted, bare, 256, 192);
            Debug.Log($"[atv-plate] quad fitted vs bare at SE: {diff}");

            Assert.That(diff.PercentDiffering, Is.GreaterThan(1.0), "the fittings draw nothing.");
            WritePlate("quad-fitted-vs-bare.png", 256, 192, fitted, bare);
        }

        /// <summary>
        /// Each body's mesh at rest in all eight facings, the rig's render above and the extracted
        /// mesh's below — the picture behind the percentage. One strip per body.
        /// </summary>
        [Test]
        public void PlateEachBodyAgainstHerRigInEightFacings([ValueSource(nameof(Keys))] string key)
        {
            VehicleRigFleet.Vehicle v = VehicleRigFleet.Get(key);

            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigMeshData data = RigMeshExtractor.ExtractFrom(host, v.ScriptPath, v.GlobalName,
                                                            hull: v.Extraction);
            RigMeshBuild build = RigMeshBuilder.Build(data);
            try
            {
                var cells = new byte[16][];
                for (int dir = 0; dir < 8; dir++)
                {
                    var view = new RigViewOptions(dir, data.DefaultElev);
                    cells[dir] = host.EvaluateBytes(
                        $"{v.GlobalName}.render({TruthArgs(view, v.Extraction.ViewOptions)})");
                    cells[8 + dir] =
                        RigMeshReferenceRasterizer.RenderFromMesh(data, build.Mesh, view);
                }
                WriteGrid($"{key}-rig-over-mesh-8dir.png", data.W, data.H, 8, cells);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(build.Mesh);
            }
        }

        // =============================================================================================
        //  helpers
        // =============================================================================================

        static void Widen(IRigScriptHost host)
        {
            VehicleRigFleet.Vehicle v = VehicleRigFleet.Get("enduro250");
            host.Execute(RigMeshExtractor.WidenExportedLiteral(
                File.ReadAllText(Full(v.ScriptPath)), v.GlobalName,
                Array.Empty<string>(), v.ScriptPath));
        }

        static void WritePlate(string name, int w, int h, params byte[][] cells) =>
            WriteGrid(name, w, h, cells.Length, cells);

        /// <summary>
        /// One PNG, <paramref name="columns"/> wide and as many rows as the cells fill, on an opaque
        /// slate so a reviewer can see a transparent silhouette against something.
        ///
        /// <para>⚠️ Written with <c>TextureFormat.RGBA32</c> and <c>linear: false</c>. A UNorm target
        /// hands back LINEAR bytes and every colour in the plate comes out wrong — the trap the
        /// render fixtures carry, in its simplest costume.</para>
        /// </summary>
        static void WriteGrid(string name, int w, int h, int columns, byte[][] cells)
        {
            // ⚠️ THE PICTURE NEEDS A DEVICE; THE MEASUREMENT DOES NOT — and the measurement is the
            // part that has to survive CI. Every assertion in this fixture is pure CPU (the rig
            // through V8, the oracle through managed code), so a machine with no graphics device
            // still adjudicates the reconstructed ramp table; only the plate is skipped. Gating the
            // whole fixture on a device would throw the evidence away to keep the illustration.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.Log($"[atv-plate] {name} NOT written — no graphics device. The measurements " +
                          "above ran and are the point; the plate is the illustration.");
                return;
            }

            int rows = (cells.Length + columns - 1) / columns;
            var tex = new Texture2D(w * columns, h * rows, TextureFormat.RGBA32, false, false);
            try
            {
                var px = new Color32[w * columns * h * rows];
                var slate = new Color32(24, 28, 32, 255);
                for (int i = 0; i < px.Length; i++) px[i] = slate;

                for (int c = 0; c < cells.Length; c++)
                {
                    byte[] cell = cells[c];
                    if (cell == null) continue;
                    int ox = (c % columns) * w;
                    // ⚠️ Texture2D rows run BOTTOM-UP and a rig cell runs top-down, so the row is
                    // flipped here rather than "fixed" later — a plate drawn upside down reads as
                    // an art defect and costs a cycle every time.
                    int oy = (rows - 1 - c / columns) * h;
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            int s = (y * w + x) * 4;
                            if (cell[s + 3] == 0) continue;
                            px[(oy + (h - 1 - y)) * (w * columns) + ox + x] =
                                new Color32(cell[s], cell[s + 1], cell[s + 2], 255);
                        }
                }

                tex.SetPixels32(px);
                tex.Apply(false, false);

                Directory.CreateDirectory(Full(PlateDir));
                string path = Path.Combine(Full(PlateDir), name);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log($"[atv-plate] wrote {PlateDir}/{name} ({w * columns}×{h * rows})");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }
    }
}
