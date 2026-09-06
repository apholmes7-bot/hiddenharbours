using System.Collections.Generic;
using System.IO;
using System.Text;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE WAKE FADING, PHOTOGRAPHED</b> — register row 28. The owner, 2026-09-06: <i>"it doesnt
    /// widen over time and fade away."</i> #747 was the widening. Two arms, one shader: the buffer
    /// stored in the shipped 8-bit target against the 16-bit one, at the PC-first <b>60 fps</b>.
    ///
    /// <para>🔴 <b>Why this fixture had to exist at all, and what it says about the last one.</b>
    /// <c>FoamTrailRootPlateTests</c> — the harness this borrows its shape from — ping-pongs the advect
    /// pass through <c>RenderTextureFormat.ARGBFloat</c>. That is a perfectly good rig for asking where
    /// a trail is LAID, which is what 11a and 11b needed. It is structurally unable to show row 28,
    /// because a float target has no 8-bit rounding in it: the defect lives in the STORE, so the store
    /// is the thing under test and the arms here are two real render-target formats. A fixture that
    /// cannot represent the defect will photograph a clean sea and call it evidence.</para>
    ///
    /// <para>The clocks are stopped by construction: this fixture owns its whole time axis (it passes
    /// its own dt and decay per step and never reads <c>_Time</c>), so a run is reproducible and every
    /// difference between arms is the change under test. Skipped, loudly, with no graphics device.</para>
    /// </summary>
    public class FoamFadePlateTests
    {
        const string OutDir = "artifacts/foam-fade";

        const float Extent = 96f;           // IsoFacetHullFeature._foamWindowMeters, the shipped window
        const float Dt = 1f / 60f;          // the PC-first baseline — the frame rate row 28 is about
        const float CoverHalfLife = 6f;     // IsoFacetHullFeature._foamHalfLifeSeconds
        const float FreshHalfLife = 4f;     // IsoFacetHullFeature._foamAgeHalfLifeSeconds
        const float Threshold = 0.12f;      // HiddenHarboursWater.shader _WakeFoamThreshold

        // The cape, from her shipped defs (as PR 11a/11b used her).
        const float SternOffset = 6.40f;    // CapeIslanderIsoHullMesh.WakeSternOffsetMeters
        const float HalfBeam = 2.4f;        // .WatertightHalfBeamMeters
        const float ElevDeg = 40f;          // .ElevationDeg
        const float SpeedKn = 8f;

        static RenderTextureFormat Fixed => FoamBuffer.SelectFormat(SystemInfo.SupportsRenderTextureFormat);
        const RenderTextureFormat Shipped = RenderTextureFormat.RG16;

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — no graphics device; the advect pass needs a GPU.");
        }

        /// <summary>A straight run up the middle of the window, the hull leaving at the top.</summary>
        static List<(Vector2 pos, Vector2 bow)> Track(int steps)
        {
            var path = new List<(Vector2, Vector2)>(steps);
            float speed = SpeedKn * 0.5144f;
            var pos = new Vector2(Extent * 0.5f, Extent * 0.06f);
            var bow = new Vector2(0f, 1f);
            for (int i = 0; i < steps; i++)
            {
                path.Add((pos, bow));
                pos += bow * speed * Dt;
            }
            return path;
        }

        /// <summary>
        /// Drive the SHIPPED advect pass for <paramref name="steps"/> injecting frames and then
        /// <paramref name="tailSteps"/> frames of nothing but decay, with the ping-pong stored in
        /// <paramref name="store"/>. Returns the buffer's two channels per texel.
        /// </summary>
        /// <param name="shiftEvery">0 = never scroll. Otherwise scroll one whole cell every this many
        /// frames — the cell law's own move, exercised through the format under test. ⚠️ It applies to
        /// the TAIL frames only. A scroll that ran while the hull was still injecting would move the
        /// content laid on frame 1 by the whole run and the content laid on the last frame by nothing,
        /// so the two arms would differ by a smear rather than by a translation, and a guard comparing
        /// them would be comparing two different pictures.</param>
        static Color[] RunArm(RenderTextureFormat store, int steps, int tailSteps, out int res,
                              int shiftEvery = 0)
        {
            Shader shader = Shader.Find("Hidden/HiddenHarbours/FoamBufferAdvect");
            Assert.IsNotNull(shader, "the advect shader must exist");

            res = FoamBuffer.ResolutionForExtent(Extent);
            Vector2 origin = FoamBuffer.WorldCellOrigin(new Vector2(Extent * 0.5f, Extent * 0.5f), Extent);

            var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            var a = new RenderTexture(res, res, 0, store) { filterMode = FilterMode.Point };
            var b = new RenderTexture(res, res, 0, store) { filterMode = FilterMode.Point };
            a.Create(); b.Create();
            foreach (RenderTexture t in new[] { a, b })
            {
                RenderTexture keep = RenderTexture.active;
                RenderTexture.active = t; GL.Clear(false, true, Color.clear); RenderTexture.active = keep;
            }

            var seg = new Vector4[FoamBuffer.MaxInjectors];
            var shape = new Vector4[FoamBuffer.MaxInjectors];
            List<(Vector2 pos, Vector2 bow)> path = Track(steps);
            Vector2 previous = FoamBuffer.SternWorld(path[0].pos, path[0].bow, SternOffset, ElevDeg);

            RenderTexture src = a, dst = b;
            for (int i = 0; i < steps + tailSteps; i++)
            {
                bool injecting = i < steps;
                if (injecting)
                {
                    Vector2 here = FoamBuffer.SternWorld(path[i].pos, path[i].bow, SternOffset, ElevDeg);
                    seg[0] = new Vector4(previous.x, previous.y, here.x, here.y);
                    // amount = _depositPerSecond * dt at full churn; vigour 1 marks the freshness clock.
                    shape[0] = new Vector4(HalfBeam, 2.5f * Dt, 1f, 0f);
                    previous = here;
                }
                else
                {
                    // 🔴 THE TAIL: the hull has gone. Nothing but scroll and decay from here — which is
                    // the whole of what the owner asked for and the whole of what row 28 broke.
                    seg[0] = Vector4.zero;
                    shape[0] = Vector4.zero;
                }

                int shift = !injecting && shiftEvery > 0 && i % shiftEvery == 0 ? 1 : 0;
                mat.SetTexture(FoamShaderIds.Prev, src);
                mat.SetVector(FoamShaderIds.BufferWorld, new Vector4(origin.x, origin.y, Extent, 1f / Extent));
                mat.SetVector(FoamShaderIds.Resolution, new Vector4(res, res, 1f / res, 1f / res));
                mat.SetVector(FoamShaderIds.Shift, new Vector4(shift, 0f, 0f, 0f));
                mat.SetFloat(FoamShaderIds.Decay, FoamBuffer.DecayFactor(CoverHalfLife, Dt));
                mat.SetFloat(FoamShaderIds.AgeDecay, FoamBuffer.DecayFactor(FreshHalfLife, Dt));
                mat.SetVectorArray(FoamShaderIds.InjectSeg, seg);
                mat.SetVectorArray(FoamShaderIds.InjectShape, shape);
                mat.SetVector(FoamShaderIds.SurfDeposit, Vector4.zero);   // no surf — this is the wake
                mat.SetVector("_BlitScaleBias", new Vector4(1f, 1f, 0f, 0f));

                var cmd = new CommandBuffer { name = "HH foam fade plate" };
                cmd.SetRenderTarget(dst);
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.DrawProcedural(Matrix4x4.identity, mat, 0, MeshTopology.Triangles, 3, 1);
                Graphics.ExecuteCommandBuffer(cmd);
                cmd.Release();
                (src, dst) = (dst, src);
            }

            Color[] px = ReadBack(src, res);
            Object.DestroyImmediate(mat);
            a.Release(); b.Release();
            Object.DestroyImmediate(a); Object.DestroyImmediate(b);
            return px;
        }

        /// <summary>Read a two-channel target of ANY format back as floats: copy through an ARGBFloat
        /// intermediate first, so the readback path is identical for every arm and cannot itself be
        /// what the arms differ by.</summary>
        static Color[] ReadBack(RenderTexture source, int res)
        {
            var copy = new RenderTexture(res, res, 0, RenderTextureFormat.ARGBFloat)
                { filterMode = FilterMode.Point };
            copy.Create();
            Graphics.Blit(source, copy);

            var readback = new Texture2D(res, res, TextureFormat.RGBAFloat, false, true);
            RenderTexture keep = RenderTexture.active;
            RenderTexture.active = copy;
            readback.ReadPixels(new Rect(0, 0, res, res), 0, 0);
            readback.Apply();
            RenderTexture.active = keep;
            Color[] px = readback.GetPixels();

            Object.DestroyImmediate(readback);
            copy.Release();
            Object.DestroyImmediate(copy);
            return px;
        }

        static int DrawnTexels(Color[] buf)
        {
            int n = 0;
            foreach (Color c in buf) if (c.r >= Threshold) n++;
            return n;
        }

        static float PeakCoverage(Color[] buf)
        {
            float m = 0f;
            foreach (Color c in buf) m = Mathf.Max(m, c.r);
            return m;
        }

        /// <summary>How far astern of the hull the DRAWN trail still reaches, in metres. The hull runs
        /// up +y, so this walks down from her last position looking for the last row that draws.</summary>
        static float DrawnTailMetres(Color[] buf, int res, float hullY)
        {
            int hullRow = Mathf.Clamp(Mathf.RoundToInt(hullY * FoamBuffer.CellsPerUnit), 0, res - 1);
            for (int y = 0; y <= hullRow; y++)
            {
                bool drawn = false;
                for (int x = 0; x < res && !drawn; x++)
                    if (buf[y * res + x].r >= Threshold) drawn = true;
                if (drawn) return (hullRow - y) / FoamBuffer.CellsPerUnit;
            }
            return 0f;
        }

        // ==== THE SHEET =============================================================================

        /// <summary>
        /// 🔴 <b>THE PLATES AND THEIR NUMBERS.</b> Three figures, shipped against fixed:
        /// <list type="number">
        /// <item><b>the run</b> — 20 s of cape at 8 kn through the shipped 96 m window;</item>
        /// <item><b>the tail</b> — the same buffer 30 s after she has gone, nothing but decay;</item>
        /// <item><b>the clock</b> — the freshness channel of figure 1, which is the colour the wake
        /// draws in.</item>
        /// </list>
        /// The acceptance is figure 2: with the hull gone for 30 s the sea must be clean.
        /// </summary>
        [Test]
        public void ThePlateSheet_TheRunTheTailAndTheClock_ShippedAgainstFixed()
        {
            RequireAGraphicsDevice();
            Directory.CreateDirectory(OutDir);

            const int runSteps = 1200;     // 20 s at 60 fps
            const int tailSteps = 1800;    // 30 s of nothing but decay
            float hullY = Track(runSteps)[runSteps - 1].pos.y;

            var report = new StringBuilder();
            report.AppendLine($"foam buffer, {Extent:0} m window, cape at {SpeedKn:0} kn, {1f / Dt:0} fps");
            report.AppendLine($"  the fixed arm's format on this device: {Fixed} " +
                              $"({FoamBuffer.BytesPerTexel(Fixed)} bytes/texel, " +
                              $"shipped {Shipped} = {FoamBuffer.BytesPerTexel(Shipped)})");
            report.AppendLine();

            int res;
            Color[] runShipped = RunArm(Shipped, runSteps, 0, out res);
            Color[] runFixed = RunArm(Fixed, runSteps, 0, out _);
            Color[] tailShipped = RunArm(Shipped, runSteps, tailSteps, out _);
            Color[] tailFixed = RunArm(Fixed, runSteps, tailSteps, out _);

            report.AppendLine("figure            | arm     | drawn texels | peak coverage | drawn tail astern");
            void Row(string fig, string arm, Color[] buf) => report.AppendLine(
                $"{fig,-17} | {arm,-7} | {DrawnTexels(buf),12} | {PeakCoverage(buf),13:0.0000} | " +
                $"{DrawnTailMetres(buf, res, hullY),13:0.0} m");
            Row("1 the run 20 s", "shipped", runShipped);
            Row("1 the run 20 s", "fixed", runFixed);
            Row("2 +30 s tail", "shipped", tailShipped);
            Row("2 +30 s tail", "fixed", tailFixed);

            File.WriteAllText(Path.Combine(OutDir, "FOAM-FADE.txt"), report.ToString());
            Debug.Log("[foam-fade] the plates, measured\n" + report);

            var rows = new List<Color[][]>
            {
                new[] { PaintCoverage(runShipped), PaintCoverage(runFixed) },
                new[] { PaintCoverage(tailShipped), PaintCoverage(tailFixed) },
                new[] { PaintAge(runShipped), PaintAge(runFixed) },
            };
            string sheet = Path.Combine(Directory.GetCurrentDirectory(), OutDir, "SHEET-foam-fade.png");
            WriteSheet(sheet, rows, res);
            Assert.IsTrue(File.Exists(sheet), "the sheet must be written — the FILE is the evidence");

            // 🔴 THE ACCEPTANCE. Thirty seconds is five coverage half-lives: 2^-5 = 0.031, well under
            // the compose's 0.12. The sea the hull left must be clean water.
            Assert.AreEqual(0, DrawnTexels(tailFixed),
                $"Thirty seconds after the hull has gone, {DrawnTexels(tailFixed)} texels still draw " +
                $"(peak {PeakCoverage(tailFixed):0.0000}). The wake has to fade away on its own decay " +
                "— that is the owner's sentence and the whole of this PR.");
            Assert.Greater(DrawnTexels(tailShipped), 1000,
                "DEAD CONTROL: on the shipped 8-bit target the same 30 s of decay must leave the trail " +
                "essentially untouched. If it no longer does, the two arms have converged and the " +
                "figure above is photographing the same sea twice.");
            Assert.AreEqual(PeakCoverage(runShipped), PeakCoverage(tailShipped), 0.01f,
                "...and 'essentially untouched' is the point: the shipped arm's peak must not have " +
                "moved across 1800 frames of pure decay.");

            // The run itself must still LOOK like a wake in both arms — the fix is a fade, not a delete.
            Assert.Greater(DrawnTexels(runFixed), 5000,
                "The fixed arm still has to draw a wake while the hull is working; if the trail is " +
                "gone at 20 s the decay has been over-applied, not fixed.");
        }

        /// <summary>
        /// The cell law, exercised through the NEW format: a whole-cell scroll must stay an exact copy.
        /// Point filtering plus an integer <c>Load</c> is what guarantees it, and neither depends on the
        /// format — but "does not depend on" is a claim, and this is the measurement of it.
        /// </summary>
        [Test]
        public void AWholeCellScroll_IsStillAnExactCopy_InTheNewFormat()
        {
            RequireAGraphicsDevice();
            const int steps = 300;
            const int tail = 200;

            int res;
            Color[] still = RunArm(Fixed, steps, tail, out res);
            Color[] scrolled = RunArm(Fixed, steps, tail, out _, shiftEvery: 1);

            // Both arms took the SAME number of decay steps and differ only by the scroll, so the two
            // pictures must be the same picture, translated. The fragment reads its source at
            // texel + shift, so the content travels the other way.
            int moved = tail;
            int compared = 0, exact = 0;
            float worst = 0f;
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res - moved; x++)
            {
                // Only the still arm's content that also survived the scroll can be compared, and only
                // where the still arm actually has foam.
                float s = still[y * res + x + moved].r;
                if (s < 0.02f) continue;
                float t = scrolled[y * res + x].r;
                compared++;
                float d = Mathf.Abs(s - t);
                worst = Mathf.Max(worst, d);
                if (d <= 1e-6f) exact++;
            }

            TestContext.WriteLine($"{Fixed}: {compared} texels of overlap, {exact} bit-identical, " +
                                  $"worst difference {worst:0.0000000}");
            Assert.Greater(compared, 2000, "The two arms must actually overlap, or nothing was compared.");
            Assert.LessOrEqual(worst, 1e-6f,
                "A whole-cell scroll has to be an EXACT copy in the new format too. A difference here " +
                "means the wider target has picked up filtering or a conversion the 8-bit one did not, " +
                "and the wake would smear into a smudge over a few seconds of pan.");
        }

        /// <summary>Same seed, same clock, same track — same buffer. The format change must not have
        /// introduced anything the run does not carry itself.</summary>
        [Test]
        public void TwoRunsOfTheFixedArm_AreIdentical()
        {
            RequireAGraphicsDevice();
            const int steps = 300;

            int res;
            Color[] first = RunArm(Fixed, steps, 60, out res);
            Color[] second = RunArm(Fixed, steps, 60, out _);

            float worst = 0f;
            for (int i = 0; i < first.Length; i++)
            {
                worst = Mathf.Max(worst, Mathf.Abs(first[i].r - second[i].r));
                worst = Mathf.Max(worst, Mathf.Abs(first[i].g - second[i].g));
            }
            TestContext.WriteLine($"{Fixed}: worst difference between two runs {worst:0.0000000}");
            Assert.AreEqual(0f, worst, 0f,
                "Two runs of one arm must be bit-identical — the fixture owns its whole time axis, so " +
                "any difference is state leaking in from outside it.");
        }

        // ==== painting ==============================================================================

        /// <summary>The coverage channel as the compose sees it: below the threshold is clean water.</summary>
        static Color[] PaintCoverage(Color[] buffer)
        {
            var px = new Color[buffer.Length];
            for (int i = 0; i < buffer.Length; i++)
            {
                float c = Mathf.Clamp01(buffer[i].r);
                // Draw the sub-threshold foam dim rather than black, so a plate can show a trail dying
                // rather than only the moment it crosses out of the compose.
                float v = c >= Threshold ? Mathf.Lerp(0.45f, 1f, Mathf.InverseLerp(Threshold, 1f, c))
                                         : c * 1.5f;
                px[i] = new Color(v, v, v, 1f);
            }
            return px;
        }

        /// <summary>The freshness channel through #724's walk: white at the churn, into the sea's blues
        /// as it ages. Drawn only where the coverage draws, because that is where the colour is seen.
        /// </summary>
        static Color[] PaintAge(Color[] buffer)
        {
            var foam = new Color(0.96f, 0.98f, 0.99f);
            var shallow = new Color(0.42f, 0.70f, 0.76f);
            var mid = new Color(0.13f, 0.29f, 0.42f);
            var px = new Color[buffer.Length];
            for (int i = 0; i < buffer.Length; i++)
            {
                if (buffer[i].r < Threshold) { px[i] = new Color(0.05f, 0.07f, 0.10f, 1f); continue; }
                float age01 = Mathf.Clamp01(1f - buffer[i].g);
                float t = HiddenHarbours.Core.WakeFoamAgeing.Knots(age01, 0.12f, 0.45f, 0.85f);
                px[i] = HiddenHarbours.Core.WakeFoamAgeing.Ramp3(t, foam, shallow, mid);
            }
            return px;
        }

        /// <summary>Rows x 2 columns, shipped then fixed. Halved on the way out — the window is 768
        /// texels square and three rows of two at full size is a sheet nobody opens.</summary>
        static void WriteSheet(string path, List<Color[][]> rows, int res)
        {
            const int gap = 6;
            int half = res / 2;
            int w = half * 2 + gap;
            int h = half * rows.Count + gap * (rows.Count - 1);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.10f, 0.10f, 0.11f, 1f);

            for (int r = 0; r < rows.Count; r++)
            for (int c = 0; c < 2; c++)
            {
                Color[] img = rows[r][c];
                int ox = c * (half + gap);
                int oy = (rows.Count - 1 - r) * (half + gap);
                for (int y = 0; y < half; y++)
                for (int x = 0; x < half; x++)
                {
                    // 2x2 box down-sample: a point-sampled halving of a torn mat drops half the lace.
                    Color s = img[(y * 2) * res + x * 2] + img[(y * 2) * res + x * 2 + 1]
                            + img[(y * 2 + 1) * res + x * 2] + img[(y * 2 + 1) * res + x * 2 + 1];
                    px[(oy + y) * w + ox + x] = new Color(s.r * 0.25f, s.g * 0.25f, s.b * 0.25f, 1f);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
