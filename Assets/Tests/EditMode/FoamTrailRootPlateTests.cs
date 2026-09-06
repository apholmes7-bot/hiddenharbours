using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE FOAM TRAIL, PHOTOGRAPHED</b> — the owner, 2026-09-04: <i>"the foam seem to come from the
    /// cetnre of a boat when turning and not accuratly from the stern."</i> Two figures, two arms: the
    /// trail laid at the hull's ORIGIN (as shipped) against the trail laid at her TRANSOM.
    ///
    /// <para>Driven through the SHIPPED advect pass, one blit per step, exactly as
    /// <c>IsoFacetHullFeature</c> drives it — the buffer these plates read back is the buffer the sea draws
    /// from. <b>The clocks are stopped by construction</b>: this fixture owns the whole time axis (it
    /// passes its own dt and decay per step and never reads <c>_Time</c>), so two runs of one arm are
    /// identical and every difference between arms is the change under test.</para>
    ///
    /// <para><b>Fenced:</b> the trail's WIDTH is untouched here and stays the hull's beam — that is
    /// register row 26 / PR 11b.</para>
    /// </summary>
    public class FoamTrailRootPlateTests
    {
        const string OutDir = "artifacts/foam-trail";

        const float Extent = 48f;          // m across the buffer window
        const float Dt = 1f / 30f;
        const float CoverHalfLife = 6f;    // IsoFacetHullFeature._foamHalfLifeSeconds
        const float FreshHalfLife = 4f;    // IsoFacetHullFeature._foamAgeHalfLifeSeconds

        // The cape, from her shipped defs.
        const float SternOffset = 6.45f;   // CapeIslanderIsoHullMesh.WakeSternOffsetMeters
        const float HalfBeam = 2.4f;       // .WatertightHalfBeamMeters
        const float ElevDeg = 40f;         // .ElevationDeg
        const float SpeedKn = 8f;

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — no graphics device; the advect pass needs a GPU.");
        }

        static List<(Vector2 pos, Vector2 bow)> Track(bool turning, int steps)
        {
            var path = new List<(Vector2, Vector2)>(steps);
            float speed = SpeedKn * 0.5144f;
            var pos = new Vector2(Extent * 0.30f, Extent * 0.30f);
            float heading = 0f;
            float turnPerStep = turning ? (Mathf.PI * 0.5f) / steps : 0f;   // 90 deg over the run
            for (int i = 0; i < steps; i++)
            {
                var bow = new Vector2(-Mathf.Sin(heading), Mathf.Cos(heading));
                path.Add((pos, bow));
                pos += bow * speed * Dt;
                heading += turnPerStep;
            }
            return path;
        }

        static Vector2 Point((Vector2 pos, Vector2 bow) at, bool rootAtStern)
            => rootAtStern ? FoamBuffer.SternWorld(at.pos, at.bow, SternOffset, ElevDeg) : at.pos;

        /// <param name="rootAtStern">false = the shipped behaviour (laid at the hull's ORIGIN).</param>
        static Color[] RunArm(bool turning, bool rootAtStern, int steps, out int res)
        {
            Shader shader = Shader.Find("Hidden/HiddenHarbours/FoamBufferAdvect");
            Assert.IsNotNull(shader, "the advect shader must exist");

            res = FoamBuffer.ResolutionForExtent(Extent);
            Vector2 origin = FoamBuffer.WorldCellOrigin(new Vector2(Extent * 0.5f, Extent * 0.5f), Extent);

            var mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            var a = new RenderTexture(res, res, 0, RenderTextureFormat.ARGBFloat) { filterMode = FilterMode.Point };
            var b = new RenderTexture(res, res, 0, RenderTextureFormat.ARGBFloat) { filterMode = FilterMode.Point };
            a.Create(); b.Create();
            foreach (RenderTexture t in new[] { a, b })
            {
                RenderTexture keep = RenderTexture.active;
                RenderTexture.active = t; GL.Clear(false, true, Color.clear); RenderTexture.active = keep;
            }

            var seg = new Vector4[FoamBuffer.MaxInjectors];
            var shape = new Vector4[FoamBuffer.MaxInjectors];
            List<(Vector2 pos, Vector2 bow)> path = Track(turning, steps);
            Vector2 previous = Point(path[0], rootAtStern);

            RenderTexture src = a, dst = b;
            for (int i = 0; i < steps; i++)
            {
                Vector2 here = Point(path[i], rootAtStern);
                seg[0] = new Vector4(previous.x, previous.y, here.x, here.y);
                shape[0] = new Vector4(HalfBeam, 0.35f, 1f, 0f);   // x = radius, y = amount, z = vigour
                previous = here;

                mat.SetTexture(FoamShaderIds.Prev, src);
                mat.SetVector(FoamShaderIds.BufferWorld, new Vector4(origin.x, origin.y, Extent, 1f / Extent));
                mat.SetVector(FoamShaderIds.Resolution, new Vector4(res, res, 1f / res, 1f / res));
                mat.SetVector(FoamShaderIds.Shift, Vector4.zero);
                mat.SetFloat(FoamShaderIds.Decay, FoamBuffer.DecayFactor(CoverHalfLife, Dt));
                mat.SetFloat(FoamShaderIds.AgeDecay, FoamBuffer.DecayFactor(FreshHalfLife, Dt));
                mat.SetVectorArray(FoamShaderIds.InjectSeg, seg);
                mat.SetVectorArray(FoamShaderIds.InjectShape, shape);
                mat.SetVector(FoamShaderIds.SurfDeposit, Vector4.zero);   // no surf — this is the wake
                mat.SetVector("_BlitScaleBias", new Vector4(1f, 1f, 0f, 0f));

                var cmd = new CommandBuffer { name = "HH foam root plate" };
                cmd.SetRenderTarget(dst);
                cmd.ClearRenderTarget(false, true, Color.clear);
                cmd.DrawProcedural(Matrix4x4.identity, mat, 0, MeshTopology.Triangles, 3, 1);
                Graphics.ExecuteCommandBuffer(cmd);
                cmd.Release();
                (src, dst) = (dst, src);
            }

            var readback = new Texture2D(res, res, TextureFormat.RGBAFloat, false, true);
            RenderTexture prevActive = RenderTexture.active;
            RenderTexture.active = src;
            readback.ReadPixels(new Rect(0, 0, res, res), 0, 0);
            readback.Apply();
            RenderTexture.active = prevActive;
            Color[] px = readback.GetPixels();

            Object.DestroyImmediate(readback);
            Object.DestroyImmediate(mat);
            a.Release(); b.Release();
            Object.DestroyImmediate(a); Object.DestroyImmediate(b);
            return px;
        }

        [Test]
        public void ThePlatePair_TheTurnAndTheStraight_RootedAtTheTransom()
        {
            RequireAGraphicsDevice();
            Directory.CreateDirectory(OutDir);
            const int steps = 260;                 // ~8.7 s of track at 30 Hz

            var report = new StringBuilder();
            report.AppendLine("figure | centroid shift, origin-laid -> transom-laid (m)");
            var rows = new List<Color[][]>();
            int res = 0;

            foreach (bool turning in new[] { true, false })
            {
                Color[] shipped = RunArm(turning, rootAtStern: false, steps, out res);
                Color[] rooted = RunArm(turning, rootAtStern: true, steps, out _);

                rows.Add(new[] { Paint(shipped), Paint(rooted) });

                float shift = Vector2.Distance(Centroid(shipped, res), Centroid(rooted, res));
                string fig = turning ? "90 deg turn" : "straight 8 kn";
                report.AppendLine($"{fig,-14} | {shift,5:0.00}");

                // The trail must actually MOVE — a plate whose two arms are the same is a dead control.
                Assert.Greater(shift, 1.0f,
                    $"{fig}: the whole trail shifted only {shift:0.00} m between arms — if the root did not " +
                    "move, this plate is photographing the same sea twice");
            }

            File.WriteAllText(Path.Combine(OutDir, "FOAM-ROOT.txt"), report.ToString());
            Debug.Log("[foam-root] the plates, measured\n" + report);

            string sheet = Path.Combine(Directory.GetCurrentDirectory(), OutDir, "SHEET-foam-root.png");
            WriteSheet(sheet, rows, res);
            Assert.IsTrue(File.Exists(sheet), "the sheet must be written — the FILE is the evidence");
        }

        /// <summary>Where the drawn foam actually sits, weighted by how much of it there is.</summary>
        static Vector2 Centroid(Color[] buffer, int res)
        {
            double sx = 0, sy = 0, w = 0;
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                float c = buffer[y * res + x].r;
                if (c <= 0.02f) continue;
                sx += x * c; sy += y * c; w += c;
            }
            if (w <= 0) return Vector2.zero;
            return new Vector2((float)(sx / w), (float)(sy / w)) / FoamBuffer.CellsPerUnit;
        }

        static Color[] Paint(Color[] buffer)
        {
            var px = new Color[buffer.Length];
            for (int i = 0; i < buffer.Length; i++)
            {
                float f = Mathf.Clamp01(buffer[i].r);
                px[i] = new Color(f, f, f, 1f);
            }
            return px;
        }

        /// <summary>Rows x 2 columns: row 0 the 90 deg turn, row 1 the straight run; columns are
        /// laid-at-the-origin then laid-at-the-transom.</summary>
        static void WriteSheet(string path, List<Color[][]> rows, int res)
        {
            const int gap = 6;
            int w = res * 2 + gap;
            int h = res * rows.Count + gap * (rows.Count - 1);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.10f, 0.10f, 0.11f, 1f);

            for (int r = 0; r < rows.Count; r++)
            for (int c = 0; c < 2; c++)
            {
                Color[] img = rows[r][c];
                int ox = c * (res + gap);
                int oy = (rows.Count - 1 - r) * (res + gap);
                for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                    px[(oy + y) * w + ox + x] = img[y * res + x];
            }
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
    }
}
