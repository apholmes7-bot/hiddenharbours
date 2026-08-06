using System.Collections.Generic;
using System.IO;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// The CPU rasteriser behind <see cref="CliffProofMenu"/>. Draws a real St Peters cliff run as
    /// pixels, shaded through <see cref="CliffLightMath"/> — the shader's tested twin — so the sheets
    /// answer the same question the GPU would, on a machine that has no GPU.
    ///
    /// <para><b>What it draws, and why that IS the proof.</b> The wall is rasterised column by column
    /// down its own surface. Each column's world position comes from the same
    /// <see cref="CliffWallGeometry"/> the builder uses, and each texel's tangent normal is derived by
    /// FINITE DIFFERENCE across the displaced surface. That last part is what makes the form pass
    /// visible: with displacement off the surface is smooth, every normal is straight out of the wall,
    /// and the face shades as one flat value — a plane. Turn it on and the silhouette breaks up and the
    /// shading follows it, which is exactly the "the wall is not a plane" claim the kit's §5 makes.</para>
    ///
    /// <para><b>What it deliberately does NOT do.</b> It does not sample the baked albedo, normal or
    /// mask channels. Those are the RIG's form pass, they are gitignored, and a proof that needs them
    /// cannot run on CI. The sheets isolate the ENGINE's half of the contract — the geometry this PR
    /// adds and the live lighting it is fed into — which is the half that can regress here.</para>
    /// </summary>
    public static class CliffProofRenderer
    {
        static readonly Color32 Background = new Color32(18, 20, 26, 255);
        static readonly Color32 Divider = new Color32(70, 76, 88, 255);
        static readonly Color32 BrowInk = new Color32(255, 214, 120, 255);
        static readonly Color32 Rock = new Color32(176, 118, 92, 255);   // sandstone, roughly

        /// <summary>Rows down the face — the kit's own subdivision, so the raster resolves exactly what
        /// the mesh does and no more.</summary>
        const int Rows = 96;

        // =============================================================================================
        //  SHEET 1 — the wall is not a plane
        // =============================================================================================

        /// <summary>
        /// The same run of coast twice: the plane on top, the profile-displaced wall below. Shaded at a
        /// fixed high sun so the only thing that differs between the panels is the geometry.
        /// </summary>
        public static string FormVsPlane(List<CliffWallSample> run, Texture2D profile, int ppm,
                                         string path)
        {
            Bounds2 b = Measure(run);
            int w = Mathf.CeilToInt(b.Width * ppm) + 8;
            int panel = Mathf.CeilToInt(b.Height * ppm) + 8;
            int h = panel * 2 + 3;

            var px = Fill(w, h, Background);
            var noon = CliffLightMath.SunWorld(new Vector2(0f, -1f), 0.92f);

            float planeRange = DrawRun(px, w, h, 0, panel, run, null, ppm, b, noon, 0.92f);
            for (int x = 0; x < w; x++) Set(px, w, h, x, panel + 1, Divider);
            float formRange = DrawRun(px, w, h, panel + 3, panel, run, profile, ppm, b, noon, 0.92f);

            Write(px, w, h, path);
            return $"form-vs-plane: {Path.GetFileName(path)} {w}x{h}px at {ppm} px/m. " +
                   $"Shading spread across the face — plane {planeRange:F4}, displaced {formRange:F4}. " +
                   (profile == null
                       ? "NO PROFILE ON DISK: both panels are the plane, so the sheet proves nothing " +
                         "until the kit is baked."
                       : formRange > planeRange * 1.5f
                           ? "The displaced wall shades over a WIDER range than the plane — the form " +
                             "pass reaches the silhouette and the light answers it."
                           : "⚠ The displaced wall shades no wider than the plane — the displacement is " +
                             "not reaching the surface normals.");
        }

        // =============================================================================================
        //  SHEET 2 — the same rock, all day
        // =============================================================================================

        /// <summary>
        /// One face, drawn once per hour across the cycle. The claim under proof is the one
        /// <c>CliffLightMath.SkyFloor</c> exists for: the wall changes visibly through the day and NEVER
        /// reaches black at night, so ADR 0013's overlay still has something to moon-lift.
        /// </summary>
        public static string DayCycle(List<CliffWallSample> run, Texture2D profile, int ppm,
                                      float[] hours, float sunrise, float sunset,
                                      float southBias, float noonLift, string path)
        {
            Bounds2 b = Measure(run);
            int cell = Mathf.CeilToInt(b.Width * ppm) + 6;
            int h = Mathf.CeilToInt(b.Height * ppm) + 8;
            int w = cell * hours.Length + (hours.Length - 1);

            var px = Fill(w, h, Background);
            var report = new System.Text.StringBuilder("daycycle: ");
            float darkest = 1f, brightest = 0f;

            for (int i = 0; i < hours.Length; i++)
            {
                float hour = hours[i];
                float elevation = DayNightMath.SunElevation(hour, sunrise, sunset);
                Vector2 ground = DayNightMath.SunDirection(hour, sunrise, sunset, southBias, noonLift);
                Vector3 sun = CliffLightMath.SunWorld(ground, elevation);

                int x0 = i * (cell + 1);
                float mean = DrawRun(px, w, h, 4, h - 8, run, profile, ppm, b, sun, elevation,
                                     x0 + 3, out float lo, out float hi);
                darkest = Mathf.Min(darkest, lo);
                brightest = Mathf.Max(brightest, hi);
                report.Append($"{hour:00}h e={elevation:+0.00;-0.00} shade {lo:F3}..{hi:F3} (mean {mean:F3})  ");

                if (i < hours.Length - 1)
                    for (int y = 0; y < h; y++) Set(px, w, h, x0 + cell, y, Divider);
            }

            Write(px, w, h, path);
            report.AppendLine();
            report.AppendLine($"  {Path.GetFileName(path)} {w}x{h}px, {hours.Length} hours.");
            report.AppendLine(darkest > 0.01f
                ? $"  ⭐ the darkest texel all night is {darkest:F4}, ABOVE zero — the night wall still " +
                  "has something for ADR 0013's moonlight lift to act on (CliffLightMath.SkyFloor)."
                : $"  ⚠ a texel reached {darkest:F4}: the wall goes black before the overlay does, and a " +
                  "moonlit cliff will read as a hole in the world.");
            report.AppendLine($"  brightest {brightest:F4}; noon-to-midnight ratio " +
                              $"{(darkest > 1e-6f ? brightest / darkest : float.PositiveInfinity):F1}x — " +
                              "a wall that did not change through the day would read 1.0x.");
            return report.ToString();
        }

        // =============================================================================================
        //  the raster
        // =============================================================================================

        /// <summary>Draw a panel and hand back the SPREAD of shading across it — high on a wall with
        /// real form, near zero on a plane, which is the number the form sheet's claim rests on.</summary>
        static float DrawRun(Color32[] px, int w, int h, int top, int height,
                             List<CliffWallSample> run, Texture2D profile, int ppm, Bounds2 b,
                             Vector3 sunWorld, float sunElevation)
        {
            DrawRun(px, w, h, top, height, run, profile, ppm, b, sunWorld, sunElevation, 4,
                    out float lo, out float hi);
            return hi - lo;
        }

        /// <summary>
        /// Rasterise one run into a panel. Returns the mean shade and reports the range — the numbers
        /// the sheets' claims are actually made on, because "it looks different" is not evidence.
        /// </summary>
        static float DrawRun(Color32[] px, int w, int h, int top, int height,
                             List<CliffWallSample> run, Texture2D profile, int ppm, Bounds2 b,
                             Vector3 sunWorld, float sunElevation, int left,
                             out float lo, out float hi)
        {
            lo = 1f; hi = 0f;
            double total = 0; int counted = 0;

            // The surface, as a grid of world points — displaced exactly the way the mesh is.
            int cols = run.Count;
            var grid = new Vector2[cols, Rows + 1];
            float along = 0f;
            for (int c = 0; c < cols; c++)
            {
                if (c > 0) along += Vector2.Distance(run[c - 1].BrowPlan, run[c].BrowPlan);
                CliffWallSample s = run[c];
                Vector2 brow = s.BrowPlan, toe = CliffWallGeometry.ToeScreen(in s);
                Vector2 outward = CliffWallGeometry.OutwardPlan(in s);
                float u = CliffWallGeometry.TileU(along, CliffCatalogMetresS);

                for (int r = 0; r <= Rows; r++)
                {
                    float t = (float)r / Rows;
                    Vector2 p = Vector2.Lerp(brow, toe, t);
                    if (profile != null)
                    {
                        float grey = profile.GetPixelBilinear(u, 1f - t).r;
                        p += outward * CliffWallGeometry.ProfileMetresFromGrey(grey, ProfileMetres);
                    }
                    grid[c, r] = p;
                }
            }

            var bakeL = new Vector3(-0.60f, -0.55f, 0.58f);   // the S aspect's key — the shader's default
            for (int c = 0; c < cols; c++)
            for (int r = 0; r <= Rows; r++)
            {
                // The tangent normal by FINITE DIFFERENCE across the displaced surface: an undisplaced
                // wall gives (0,0,1) everywhere and shades as one value, which is the plane read.
                float ds = c > 0 && c < cols - 1
                    ? (grid[c + 1, r] - grid[c - 1, r]).magnitude -
                      (run[c + 1].BrowPlan - run[c - 1].BrowPlan).magnitude
                    : 0f;
                float dt = r > 0 && r < Rows
                    ? (grid[c, r + 1] - grid[c, r - 1]).magnitude -
                      (Vector2.Lerp(run[c].BrowPlan, CliffWallGeometry.ToeScreen(run[c]), (r + 1f) / Rows) -
                       Vector2.Lerp(run[c].BrowPlan, CliffWallGeometry.ToeScreen(run[c]), (r - 1f) / Rows)).magnitude
                    : 0f;
                var n = new Vector3(-ds * SlopeGain, -dt * SlopeGain, 1f).normalized;

                float shade = CliffLightMath.ShadeWall(WallAzimuthFor(run[c]), BatterFor(run[c]),
                                                       new Vector2(sunWorld.x, sunWorld.y), sunElevation,
                                                       n, ao: 0.85f, maskKey: 0.7f,
                                                       bakeLightTangent: bakeL);
                lo = Mathf.Min(lo, shade); hi = Mathf.Max(hi, shade);
                total += shade; counted++;

                var col = new Color32((byte)Mathf.Clamp(Rock.r * shade, 0, 255),
                                      (byte)Mathf.Clamp(Rock.g * shade, 0, 255),
                                      (byte)Mathf.Clamp(Rock.b * shade, 0, 255), 255);

                // Fill the CELL this vertex opens — to the next station across and the next row down —
                // rather than plotting the vertex. Stations are 0.25 m apart and the sheets are drawn at
                // 24 px/m, so plotting vertices draws a comb with 4 px of background between every
                // column and the face never reads as a face.
                int x = left + Mathf.RoundToInt((grid[c, r].x - b.MinX) * ppm);
                int y = top + height - 1 - Mathf.RoundToInt((grid[c, r].y - b.MinY) * ppm);
                int xNext = c < cols - 1
                    ? left + Mathf.RoundToInt((grid[c + 1, r].x - b.MinX) * ppm)
                    : x + 1;
                int yNext = r < Rows
                    ? top + height - 1 - Mathf.RoundToInt((grid[c, r + 1].y - b.MinY) * ppm)
                    : y + 1;

                for (int yy = Mathf.Min(y, yNext); yy <= Mathf.Max(y, yNext); yy++)
                    for (int xx = Mathf.Min(x, xNext); xx <= Mathf.Max(x, xNext); xx++)
                        Set(px, w, h, xx, yy, col);

                // The brow line in ink — the silhouette is the whole claim of the form sheet, and it
                // must be legible independently of how the face happens to shade.
                if (r == 0)
                    for (int xx = Mathf.Min(x, xNext); xx <= Mathf.Max(x, xNext); xx++)
                        Set(px, w, h, xx, y, BrowInk);
            }
            return counted == 0 ? 0f : (float)(total / counted);
        }

        // The catalog lives in an Editor assembly this file CAN see, but the two numbers are hoisted so
        // the raster reads as arithmetic rather than as a chain of lookups.
        static float CliffCatalogMetresS => HiddenHarbours.Art.Editor.CliffCatalog.FaceMetresS;
        static float ProfileMetres => HiddenHarbours.Art.Editor.CliffCatalog.ProfileMetres;

        /// <summary>How hard the finite-differenced slope is turned into a normal. A proof-sheet
        /// exaggeration, stated rather than hidden: the displacement is ±1.15 m over 12 m of shore, which
        /// is a real but shallow relief, and at 24 px/m an honest normal would be invisible.</summary>
        const float SlopeGain = 12f;

        static float WallAzimuthFor(CliffWallSample s) =>
            CliffWallGeometry.AzimuthOf(CliffWallGeometry.OutwardPlan(in s));

        static float BatterFor(CliffWallSample s) => CliffWallGeometry.BatterDegrees(in s);

        // =============================================================================================
        //  pixels
        // =============================================================================================

        struct Bounds2
        {
            public float MinX, MinY, MaxX, MaxY;
            public float Width => Mathf.Max(1f, MaxX - MinX);
            public float Height => Mathf.Max(1f, MaxY - MinY);
        }

        static Bounds2 Measure(List<CliffWallSample> run)
        {
            var b = new Bounds2
            {
                MinX = float.MaxValue, MinY = float.MaxValue,
                MaxX = float.MinValue, MaxY = float.MinValue,
            };
            foreach (CliffWallSample s in run)
            {
                Vector2 toe = CliffWallGeometry.ToeScreen(in s);
                foreach (Vector2 p in new[] { s.BrowPlan, toe })
                {
                    b.MinX = Mathf.Min(b.MinX, p.x); b.MaxX = Mathf.Max(b.MaxX, p.x);
                    b.MinY = Mathf.Min(b.MinY, p.y); b.MaxY = Mathf.Max(b.MaxY, p.y);
                }
            }
            // Room for the ±1.15 m of displacement either side of the undisplaced surface.
            b.MinX -= 1.5f; b.MaxX += 1.5f; b.MinY -= 1.5f; b.MaxY += 1.5f;
            return b;
        }

        static Color32[] Fill(int w, int h, Color32 c)
        {
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = c;
            return px;
        }

        static void Set(Color32[] px, int w, int h, int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            px[y * w + x] = c;
        }

        static void Write(Color32[] px, int w, int h, string path)
        {
            // A raster is authored top-row-first and Texture2D reads bottom-up — every baker in this
            // repo flips once on the way in, and a proof sheet that skipped it would be upside down.
            var flipped = new Color32[px.Length];
            for (int y = 0; y < h; y++)
                System.Array.Copy(px, y * w, flipped, (h - 1 - y) * w, w);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false);
            try
            {
                tex.SetPixels32(flipped);
                tex.Apply(false, false);
                File.WriteAllBytes(path, tex.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(tex); }
        }
    }
}
