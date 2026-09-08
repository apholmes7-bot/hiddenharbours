using System.Text;
using HiddenHarbours.Art;       // WaterAbsorption: the shader's own C# twin for row 10
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// 🔴 <b>REGISTER ROWS 9 + 10, ANSWERED WITHOUT AN EDITOR SLOT.</b>
    ///
    /// <para>The water shader's wet edge is one line —
    /// <c>clip(_WaterLevel - lerp(_HeightMin, _HeightMax, tex2D(_HeightTex, uv).r))</c> — and every term
    /// in it is a number. So <b>where the drawn waterline sits relative to the sim's true contour is
    /// arithmetic, not a photograph</b>: no camera, no palette, no noise floor, and — the thing that
    /// killed the first sweep — <b>no <c>_Time</c></b>. Two arms measured here differ only in the knob
    /// that was moved, which is the property the rendered sweep could not achieve.</para>
    ///
    /// <para><b>THE HYPOTHESIS ON TRIAL, and the answer.</b> Two quantizations were suspects. The
    /// SPATIAL one — Nine Mile Creek derives 1520 texels and <c>WaterSurface</c> silently clamps to 256,
    /// which over 760 m is 2.97 m per texel (register row 31) — and the VALUE one, the <c>R8</c>
    /// texture's 4.71 cm per code over the region's -6..+6 m range. <b>Measured, it is the VALUE
    /// quantum, by a factor of twenty-four</b>, and the thing that decides is the TIDE:</para>
    ///
    /// <para>Every quantum in this chain is a quantum of ELEVATION; what the player sees is a distance
    /// along the GROUND, and the exchange rate is the seabed's slope. Nine Mile Creek's beach falls 7 m
    /// in 24 m (slope 0.32); the shelf the spring low tide exposes falls 1.6 m in 60 m (slope 0.035).
    /// So half a code is 7 cm of edge at mean water and <b>67 cm at spring low</b> — and the shipped
    /// 256 grid draws an edge 2.1 cm ragged at spring high and <b>28.7 cm ragged at spring low</b>,
    /// of which the spatial grid contributes 1.2 cm. Lifting the clamp to 1520 leaves 13.5 cm standing.
    /// <b>A finer grid is not row 9's fix; a deeper texture is.</b></para>
    ///
    /// <para><b>⚠️ WHAT THIS IS NOT.</b> It measures the CLIPPED edge — the geometric waterline the
    /// height chain produces. It does not carry <c>_ShoreNoise</c> (which perturbs a cosmetic
    /// <c>depthC</c>, never <c>clip()</c>), the swash's edge shift, or the chop warp; and the bilinear
    /// filter is modelled (<see cref="SeabedBakeMath.SampleBilinear01"/>) rather than run on a GPU. It
    /// can therefore CONFIRM a comb in the height chain and give its size, and it can show a knob makes
    /// no difference — it cannot prove no other layer also combs. That is still the question the
    /// register asked first.</para>
    /// </summary>
    public class SeabedBakeCombTests
    {
        // ---- Nine Mile Creek, as shipped (NineMileCreekMainland / MainlandTidalTerrain) ---------------
        const float RegionX = 760f, RegionY = 560f;
        const float HeightMin = -6f;      // BayFloorElevation
        const float HeightMax = 6f;       // LandElevation
        const float LandElevation = 6f;
        const float ShoreFalloff = 24f;   // the beach band the waterline sweeps
        const float ShelfWidth = 60f;     // the shallow shelf beyond the beach
        const float ShelfInner = -1f;
        const float ShelfOuter = -2.6f;
        const float BayFloor = -6f;
        const int ShippedGrid = 256;      // what WaterSurface.BakeHeightMapIfNeeded clamps to
        const int RequestedGrid = 1520;   // what the region derives (2 px/m over 760 m)

        // NineMileCreekMainland's authored tide (St Peters', exactly).
        const float TideMean = 0f, TideAmplitude = 2.2f;

        /// <summary>⚠️ NOT a constant any more — the state under test. The tide decides WHICH PART OF
        /// THE PROFILE the waterline sits on, and the profile's slope there is what converts every
        /// quantum in the height chain into metres of drawn edge. The failed sweep shot spring low.
        /// </summary>
        static float WaterLevel = TideMean;

        /// <summary>Arc-length step along the contour. Well under the smallest tooth on trial.</summary>
        const float Walk = 0.10f;

        static readonly Vector2 WorldMin = new Vector2(0f, 0f);
        static readonly Vector2 WorldSize = new Vector2(RegionX, RegionY);

        /// <summary>⚠️ <see cref="WaterLevel"/> is STATE, and NUnit promises no order. Without this a
        /// test that happens to run after the spring-low one measures a bared shelf while claiming to
        /// measure mean water — a fixture reporting the previous test's world.</summary>
        [SetUp]
        public void ResetTheTide() => WaterLevel = TideMean;

        // =============================================================================================
        //  The shore: a smooth authored coast, running N-S with the sea to the EAST (Nine Mile Creek's
        //  own orientation, and the one the failed sweep's transect got 90 degrees wrong).
        //
        //  ⭐ IT IS SMOOTH BY CONSTRUCTION — three sines whose shortest wavelength is 14 m, five texels
        //  of the SHIPPED grid. Nothing in the input has structure at the texel scale, so any comb this
        //  fixture reports was manufactured by the bake, not fed to it. That is the whole argument.
        // =============================================================================================
        static float ShoreX(float y) => 380f + 18f * Mathf.Sin(y / 70f)
                                             + 7f * Mathf.Sin(y / 23f + 1.1f)
                                             + 2f * Mathf.Sin(y / 14f + 0.4f);

        static float ShoreSlopeDx(float y) => 18f / 70f * Mathf.Cos(y / 70f)
                                            + 7f / 23f * Mathf.Cos(y / 23f + 1.1f)
                                            + 2f / 14f * Mathf.Cos(y / 14f + 0.4f);

        /// <summary>Metres seaward of the shoreline — perpendicular distance to a curve given as
        /// x = ShoreX(y), which for a smooth graph is the along-x offset foreshortened by its slope.
        /// Any residual is a SMOOTH function of position and so cannot manufacture a tooth.</summary>
        static float Seaward(Vector2 p)
            => (p.x - ShoreX(p.y)) / Mathf.Sqrt(1f + ShoreSlopeDx(p.y) * ShoreSlopeDx(p.y));

        /// <summary>MainlandTidalTerrain.Lerped — smoothstep across one band, holding its outer value
        /// beyond it.</summary>
        static float Lerped(float d, float flatRadius, float falloff, float inner, float outer)
        {
            if (d <= flatRadius) return inner;
            if (falloff <= 0f) return outer;
            return Mathf.Lerp(inner, outer, Mathf.SmoothStep(0f, 1f, (d - flatRadius) / falloff));
        }

        /// <summary>
        /// MainlandTidalTerrain.ShoreProfile, soft coast, with Nine Mile Creek's own constants: the
        /// whole chain shore -> beach -> shelf -> drop-off -> floor.
        ///
        /// <para><b>⭐ The WHOLE chain matters, and truncating it to the beach was an error this fixture
        /// made once.</b> The beach falls 7 m in 24 m; the shelf falls 1.6 m in 60 m. Their slopes differ
        /// by an order of magnitude, and the drawn edge's error is a quantum DIVIDED BY that slope — so
        /// the same height texture draws a clean edge at high water and a ragged one at low.</para>
        /// </summary>
        static float ShoreProfile(float seaward)
        {
            if (seaward <= 0f) return LandElevation;
            float beachEnd = ShoreFalloff;
            if (seaward <= beachEnd)
                return Lerped(seaward, 0f, ShoreFalloff, LandElevation, ShelfInner);
            float shelfEnd = beachEnd + ShelfWidth;
            if (seaward <= shelfEnd)
                return Lerped(seaward, beachEnd, ShelfWidth, ShelfInner, ShelfOuter);
            return Lerped(seaward, shelfEnd, ShoreFalloff, ShelfOuter, BayFloor);
        }

        static float TrueElevation(Vector2 p) => ShoreProfile(Seaward(p));

        /// <summary>Seaward distance at which the true seabed reaches the water level — solved once,
        /// because the profile depends only on that distance.</summary>
        static float TrueSeawardAtWaterline()
        {
            float lo = 0f, hi = ShoreFalloff + ShelfWidth + ShoreFalloff;
            for (int i = 0; i < 60; i++)
            {
                float mid = 0.5f * (lo + hi);
                if (ShoreProfile(mid) > WaterLevel) lo = mid; else hi = mid;
            }
            return 0.5f * (lo + hi);
        }

        /// <summary>The true seabed's slope at the waterline (m of elevation per m of ground) — what
        /// converts a value-quantization jog into a lateral one.</summary>
        static float SlopeAtWaterline()
        {
            float s = TrueSeawardAtWaterline();
            return Mathf.Abs(ShoreProfile(s + 0.05f) - ShoreProfile(s - 0.05f)) / 0.1f;
        }

        // =============================================================================================
        //  The bake, with the VALUE depth as a knob. At codeCount = 255 it must reproduce the shipped
        //  SeabedBakeMath byte for byte (a test below pins that), which is what makes the deeper arms
        //  a measurement of the shipped chain rather than of a parallel one.
        // =============================================================================================
        static float[] BakeAtDepth(int res, int codeCount)
        {
            float span = HeightMax - HeightMin;
            var f = new float[res * res];
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                var p = new Vector2(WorldMin.x + (x + 0.5f) / res * WorldSize.x,
                                    WorldMin.y + (y + 0.5f) / res * WorldSize.y);
                float t = (TrueElevation(p) - HeightMin) / span;
                // codeCount <= 0 means NO value quantization — the "what if the texture were exact" arm.
                f[y * res + x] = codeCount > 0
                    ? Mathf.Clamp(Mathf.RoundToInt(t * codeCount), 0, codeCount) / (float)codeCount
                    : t;
            }
            return f;
        }

        static float DrawnElevation(float[] grid, int res, Vector2 p)
        {
            float tx = (p.x - WorldMin.x) / WorldSize.x * res - 0.5f;
            float ty = (p.y - WorldMin.y) / WorldSize.y * res - 0.5f;
            int x0 = Mathf.FloorToInt(tx), y0 = Mathf.FloorToInt(ty);
            float fx = tx - x0, fy = ty - y0;
            float C(int x, int y) => grid[Mathf.Clamp(y, 0, res - 1) * res + Mathf.Clamp(x, 0, res - 1)];
            float bottom = Mathf.Lerp(C(x0, y0), C(x0 + 1, y0), fx);
            float top = Mathf.Lerp(C(x0, y0 + 1), C(x0 + 1, y0 + 1), fx);
            return Mathf.Lerp(HeightMin, HeightMax, Mathf.Lerp(bottom, top, fy));
        }

        /// <summary>
        /// 🔴 <b>THE DEVIATION SIGNAL.</b> Walk the TRUE contour at a uniform arc length; at each step
        /// march PERPENDICULAR to it and find where the DRAWN seabed crosses the water level. The signed
        /// distance between the two is the deviation, indexed by arc length — the exact shape
        /// <see cref="ShoreCombMath"/> was proved against.
        /// </summary>
        static float[] Deviations(float[] grid, int res, float yFrom, float yTo)
        {
            float sWaterline = TrueSeawardAtWaterline();
            var devs = new System.Collections.Generic.List<float>(8192);

            for (float y = yFrom; y < yTo; )
            {
                float dx = ShoreSlopeDx(y);
                float inv = 1f / Mathf.Sqrt(1f + dx * dx);
                // Outward (seaward) unit normal of the graph x = ShoreX(y).
                var n = new Vector2(inv, -dx * inv);
                var p0 = new Vector2(ShoreX(y), y) + n * sWaterline;

                // ⚠️ BOTH CROSSINGS ARE FOUND ON THE SAME RAY, and the deviation is the difference
                // between them. Measuring the drawn edge against p0 itself looked equivalent and was
                // not: p0 sits at an exact perpendicular distance from the shore curve, while the baked
                // field is built from Seaward(), a first-order distance whose curvature error is a
                // smooth 16 cm bias here. Its own dead control caught that — a 4096 grid with exact
                // values still read 176 mm off. Differencing two level sets of the SAME ray cancels
                // every smooth error in where the ray was put, which is the only kind this can have.
                float tTrue = CrossingAlong(t => TrueElevation(p0 + n * t));
                float tDrawn = CrossingAlong(t => DrawnElevation(grid, res, p0 + n * t));
                if (!float.IsNaN(tTrue) && !float.IsNaN(tDrawn)) devs.Add(tDrawn - tTrue);

                y += Walk * inv;      // uniform ARC length along the contour, not uniform y
            }
            return devs.ToArray();
        }

        /// <summary>Where a seabed function crosses the water level along a ray, in metres from the
        /// ray's origin; NaN if it does not cross within reach. Bracket then bisect — the function is
        /// monotonic across the beach, so one bracket is enough.</summary>
        static float CrossingAlong(System.Func<float, float> elevationAt)
        {
            const float Reach = 12f;   // at low water on the shelf the drawn edge can be METRES out
            float a = -Reach, b = Reach;
            float fa = elevationAt(a) - WaterLevel, fb = elevationAt(b) - WaterLevel;
            if (fa * fb > 0f) return float.NaN;
            for (int i = 0; i < 40; i++)
            {
                float m = 0.5f * (a + b);
                float fm = elevationAt(m) - WaterLevel;
                if (fa * fm <= 0f) { b = m; fb = fm; } else { a = m; fa = fm; }
            }
            return 0.5f * (a + b);
        }

        static float Rms(float[] v)
        {
            if (v == null || v.Length == 0) return 0f;
            double m = 0; foreach (float x in v) m += x; m /= v.Length;
            double s = 0; foreach (float x in v) s += (x - m) * (x - m);
            return Mathf.Sqrt((float)(s / v.Length));
        }

        // =============================================================================================

        /// <summary>
        /// The parameterized bake must reproduce <see cref="SeabedBakeMath"/> exactly at 255 codes, or
        /// every deeper arm below is measuring a fixture of its own invention rather than the shipped
        /// chain. This is the join between the two.
        /// </summary>
        [Test]
        public void TheFixturesBake_IsTheShippedBake_AtEightBits()
        {
            const int res = 64;
            float[] mine = BakeAtDepth(res, SeabedBakeMath.CodeCount);
            byte[] shipped = SeabedBakeMath.Bake(TrueElevation, res, WorldMin, WorldSize,
                                                 HeightMin, HeightMax);
            for (int i = 0; i < mine.Length; i++)
                Assert.AreEqual(shipped[i] / (float)SeabedBakeMath.CodeCount, mine[i], 1e-6f,
                    $"texel {i}: the fixture's encode must BE WaterSurface's encode, not a copy of it");

            var p = new Vector2(371.3f, 244.9f);
            Assert.AreEqual(SeabedBakeMath.DrawnElevationAt(shipped, res, p, WorldMin, WorldSize,
                                                            HeightMin, HeightMax),
                            DrawnElevation(mine, res, p), 1e-4f,
                            "...and so must the bilinear read that turns it back into metres");
        }

        /// <summary>
        /// 🔴 <b>THE ANSWER TO ROW 9 — and it is the TIDE that decides, not the texture alone.</b>
        ///
        /// <para>Every quantum in the height chain is a quantum of ELEVATION. What the player sees is a
        /// distance along the ground, and the exchange rate between them is the seabed's slope. Nine Mile
        /// Creek's beach falls 7 m in 24 m; its shelf falls 1.6 m in 60 m. So the same texture that draws
        /// a clean edge at high water draws a ragged one at low, and the failed sweep shot spring low.</para>
        ///
        /// <para>The shore fed in has no structure finer than 14 m — five texels of the shipped grid — so
        /// every tooth reported here was manufactured by the bake, not handed to it.</para>
        /// </summary>
        [Test]
        public void TheDrawnWaterline_RAGGEDNESS_IsAQuantumDividedByTheSeabedSlope()
        {
            var report = new StringBuilder();
            report.AppendLine("ROWS 9 + 10 - the wet edge, measured from the height chain (NO render, no _Time)");
            report.AppendLine($"  region {RegionX} x {RegionY} m; height range {HeightMin}..{HeightMax} m " +
                              $"= {SeabedBakeMath.MetresPerCode(HeightMin, HeightMax) * 100f:0.00} cm per code");
            report.AppendLine($"  tide: mean {TideMean} m, spring amplitude {TideAmplitude} m " +
                              "(NineMileCreekMainland, = St Peters')");
            report.AppendLine();
            report.AppendLine("tide          level  waterline  slope  | grid  texel  |  edge RMS  period   " +
                              "8-bit budget");

            var rms = new System.Collections.Generic.Dictionary<string, float>();
            var period = new System.Collections.Generic.Dictionary<string, float>();

            foreach (var (label, level) in new[]
                     { ("spring HIGH", TideMean + TideAmplitude),
                       ("mean",        TideMean),
                       ("spring LOW",  TideMean - TideAmplitude) })
            {
                WaterLevel = level;
                float sw = TrueSeawardAtWaterline();
                float slope = SlopeAtWaterline();
                float budget = SeabedBakeMath.LateralJitterFromValueQuantum(HeightMin, HeightMax, slope);

                foreach (int res in new[] { ShippedGrid, 512, RequestedGrid })
                foreach (int codes in new[] { SeabedBakeMath.CodeCount, 0 })
                {
                    float[] devs = Deviations(BakeAtDepth(res, codes), res, 60f, 500f);
                    float r = Rms(devs);
                    float pr = ShoreCombMath.DominantPeriodMetres(devs, Walk);
                    string key = $"{label}|{res}|{codes}";
                    rms[key] = r; period[key] = pr;

                    report.AppendLine(
                        $"{label,-11} {level,6:0.00} {sw,9:0.0} m {slope,6:0.000} | " +
                        $"{res,4} {SeabedBakeMath.MetresPerTexel(RegionX, res),6:0.00} m | " +
                        $"{r * 100f,8:0.0} cm {pr,7:0.00} m   " +
                        (codes > 0 ? $"{budget * 100f,4:0} cm (8-bit)" : "   (exact values)"));
                }
                report.AppendLine();
            }

            report.AppendLine("  'waterline' is how far offshore the water meets the ground at that tide;");
            report.AppendLine("  'budget' is half a code converted to ground distance by the local slope -");
            report.AppendLine("  everything a wider texture format could ever buy back.");
            TestContext.WriteLine(report.ToString());

            // ---- the claims -------------------------------------------------------------------------
            float low8 = rms["spring LOW|256|255"], high8 = rms["spring HIGH|256|255"];
            Assert.Greater(low8, high8 * 3f,
                "⭐ THE FINDING: the SAME height texture must draw a far worse edge at low water than at " +
                "high, because the shelf it exposes is an order of magnitude flatter than the beach. If " +
                "this does not hold, the raggedness is not a quantum over a slope and this whole reading " +
                "of rows 9 + 10 is wrong.");

            Assert.Greater(low8, 0.10f,
                "At spring low the drawn edge must miss the true contour by more than 10 cm - the scale " +
                "the owner could actually see (the plate frame is 4.2 cm per pixel).");

            Assert.Less(rms["spring LOW|1520|255"], low8,
                "DEAD CONTROL ON ROW 31's FIX: the grid the region ASKED for must beat the one the clamp " +
                "gives it, or lifting the clamp buys nothing.");

            Assert.Greater(period["spring LOW|256|0"], period["spring LOW|512|0"] * 1.5f,
                "The comb's PERIOD must scale with the texel pitch - halving the pitch must roughly halve " +
                "the tooth. That is what names the GRID as the source of the spacing, without this " +
                "fixture having to predict the constant of proportionality.");
        }

        /// <summary>
        /// 🔴 <b>THE 8-BIT QUANTUM IS THE CAUSE — the claim, pinned where it can be falsified.</b>
        ///
        /// <para>⚠️ An earlier version of this test asserted the OPPOSITE and passed, because it ran at
        /// MEAN tide only. There the beach is steep, the value quantum is worth 7 cm, and the spatial
        /// grid's 3.8 cm looks comparable — so "widening the format buys nothing" read as true and
        /// would have shipped. It is false. The register's symptom was seen at low water, and the same
        /// two arms 2.2 m lower disagree by a factor of twenty-four. <b>A knob sweep run at one state
        /// of the world measures that state, not the knob</b> — which is the same lesson the failed
        /// rendered sweep taught with <c>_Time</c>.</para>
        /// </summary>
        [Test]
        public void AtLowWater_TheEightBitTexture_IsAlmostAllOfTheRaggedness()
        {
            WaterLevel = TideMean - TideAmplitude;              // spring low: the shelf is bared
            float slope = SlopeAtWaterline();

            float eight = Rms(Deviations(BakeAtDepth(ShippedGrid, SeabedBakeMath.CodeCount),
                                         ShippedGrid, 60f, 500f));
            float exact = Rms(Deviations(BakeAtDepth(ShippedGrid, 0), ShippedGrid, 60f, 500f));
            float sixteen = Rms(Deviations(BakeAtDepth(ShippedGrid, 65535), ShippedGrid, 60f, 500f));

            TestContext.WriteLine(
                $"  spring low, shipped 256 grid, seabed slope {slope:0.000} m/m\n" +
                $"    R8   (255 codes, shipped): {eight * 100f,6:0.0} cm RMS from the true contour\n" +
                $"    R16  (65535 codes)       : {sixteen * 100f,6:0.0} cm\n" +
                $"    exact values             : {exact * 100f,6:0.0} cm\n" +
                $"  so the VALUE quantum owns {(eight - exact) / eight * 100f:0} % of the error, and a " +
                "16-bit height texture recovers essentially all of it");

            Assert.Greater(eight, exact * 5f,
                "⭐ THE CAUSE: at low water the 8-bit value grid must dominate the raggedness. If the " +
                "exact-valued bake were comparable, the fix would be a finer GRID and this reading is wrong.");

            Assert.Less(sixteen, eight * 0.25f,
                "⭐ THE FIX, PROVED BEFORE IT IS PROPOSED: sixteen bits of height must take most of the " +
                "error away. A row that proposes widening a texture without this number is asking the " +
                "owner to spend memory on a guess (CLAUDE.md rule 7).");

            Assert.Less(Mathf.Abs(sixteen - exact), 0.01f,
                "...and it must land essentially ON the exact-valued bake, or 16 bits is not enough either.");
        }

        /// <summary>
        /// 🔴 <b>ROW 10 — "the deep/shallow boundary is a wall".</b> The shipped material posterizes
        /// transmission into six bands (<c>_AbsorptionBands 6</c>, <c>_Turbidity 0.25</c>,
        /// <c>_UseSeabedTex 1</c>), and a posterized ramp does not have a soft edge: each band boundary
        /// is a HARD step, always, by construction. So the question row 10 actually asks is not "what
        /// makes the edge hard" but <b>where the six steps land on the ground and how far apart they
        /// are</b> — six steps spread over 140 m read as terracing; two crowded into 3 m read as a wall.
        ///
        /// <para>Reported per tide, because the same six depths land at wildly different distances
        /// depending on which part of the profile the water is over — the same law row 9 turned on.</para>
        /// </summary>
        [Test]
        public void TheAbsorptionBands_LandWhereTheSeabedIsSTEEP_AndThatIsRow10sWall()
        {
            var sigma = WaterAbsorption.Sigma(0.25f, WaterAbsorption.DefaultRatio);   // the shipped material
            const float Bands = 6f;
            Assert.IsTrue(WaterAbsorption.IsActive(sigma),
                "the shipped material must actually run the absorption block, or row 10's wall is " +
                "somewhere else entirely and this test is measuring a layer that is switched off");

            // Depth at which the RED channel's banded transmission steps down, by bisection on the twin.
            float BandDepth(int k)
            {
                float lo = 0f, hi = 40f;
                for (int i = 0; i < 60; i++)
                {
                    float mid = 0.5f * (lo + hi);
                    float t = WaterAbsorption.BandTransmission(
                                  WaterAbsorption.Transmission(sigma, mid), Bands).x;
                    if (t > (Bands - k - 0.5f) / Bands) lo = mid; else hi = mid;
                }
                return 0.5f * (lo + hi);
            }

            var report = new StringBuilder();
            report.AppendLine("ROW 10 - where the six transmission bands land on the ground");
            report.AppendLine($"  sigma {sigma.x:0.000}/m (red), path 2d, {Bands:0} bands");
            report.AppendLine();
            report.AppendLine("tide          | band   depth   offshore | gap to the previous step");

            var tightest = new System.Collections.Generic.Dictionary<string, float>();
            float worstGap = float.MaxValue; string worstAt = "";
            foreach (var (label, level) in new[]
                     { ("spring HIGH", TideMean + TideAmplitude),
                       ("spring LOW",  TideMean - TideAmplitude) })
            {
                WaterLevel = level;
                float prev = float.NaN;
                for (int k = 1; k <= (int)Bands; k++)
                {
                    float d = BandDepth(k);
                    // How far seaward the water is that deep: the profile is monotonic offshore.
                    float lo = 0f, hi = 400f;
                    for (int i = 0; i < 60; i++)
                    {
                        float mid = 0.5f * (lo + hi);
                        if (level - ShoreProfile(mid) < d) lo = mid; else hi = mid;
                    }
                    float offshore = 0.5f * (lo + hi);
                    bool reached = offshore < 399f;
                    float gap = float.IsNaN(prev) ? float.NaN : offshore - prev;
                    report.AppendLine($"{label,-13} | {k,4}  {d,6:0.00} m " +
                                      (reached ? $"{offshore,8:0.0} m |" : "     --- |") +
                                      (float.IsNaN(gap) || !reached ? "" : $" {gap,7:0.0} m"));
                    if (reached && !float.IsNaN(gap) && gap < worstGap) { worstGap = gap; worstAt = $"{label} band {k}"; }
                    if (reached) prev = offshore;
                    if (reached && !float.IsNaN(gap) &&
                        gap < (tightest.TryGetValue(label, out float t) ? t : float.MaxValue))
                        tightest[label] = gap;
                }
                report.AppendLine();
            }
            report.AppendLine($"  tightest pair of steps: {worstGap:0.0} m apart ({worstAt})");
            TestContext.WriteLine(report.ToString());

            float high = tightest["spring HIGH"], low = tightest["spring LOW"];

            Assert.Less(high, 2f,
                "⭐ ROW 10's WALL: at spring high, two of the six band boundaries must land within a " +
                "couple of metres of each other. Four hard steps stacked inside six metres of ground IS " +
                "the 'pale shallows meet black deep water at a hard edge' the owner reported - the ramp " +
                "does not fade, it terraces, and the terraces crowd.");

            Assert.Greater(low, high * 2f,
                "⭐ AND IT IS THE SAME LAW AS ROW 9: the steps crowd where the SEABED IS STEEP. At " +
                "spring high the waterline is on the 0.44 beach and the first four bands stack into six " +
                "metres; at spring low it is on the 0.035 shelf and the same four spread over twelve. " +
                "One quantum divided by one slope, showing up as a comb at the edge and a wall behind it.");

            Assert.Greater(worstGap, 0.05f,
                $"DEAD CONTROL: the tightest pair ({worstGap:0.00} m, {worstAt}) must be a real distance. " +
                "Two boundaries at the same place would mean the bisection is not resolving them and the " +
                "whole table is one number repeated.");
        }

        /// <summary>
        /// 🔴 <b>THE FIX, MEASURED AT THE FORMAT THAT NOW SHIPS.</b> Everything above establishes that
        /// the 8-bit value quantum is rows 9 + 10's cause. This is the claim the fix makes: at
        /// <c>R16</c> the drawn waterline lands on the sim's contour at EVERY tide, including the spring
        /// low that showed 28.7 cm.
        /// </summary>
        [Test]
        public void TheShippedR16Bake_DrawsTheTrueContour_AtEveryTide()
        {
            int r8 = SeabedBakeMath.CodeCountFor(TextureFormat.R8);
            int r16 = SeabedBakeMath.CodeCountFor(TextureFormat.R16);
            var report = new StringBuilder();
            report.AppendLine("THE FIX - drawn waterline vs the sim's contour, shipped 256 grid");
            report.AppendLine("tide          slope  |     R8      R16    exact  |  memory 256 sq");

            float worst16 = 0f, best8 = float.MaxValue, worstGapToExact = 0f, lowGain = 0f;
            foreach (var (label, level) in new[]
                     { ("spring HIGH", TideMean + TideAmplitude),
                       ("mean",        TideMean),
                       ("spring LOW",  TideMean - TideAmplitude) })
            {
                WaterLevel = level;
                float a = Rms(Deviations(BakeAtDepth(ShippedGrid, r8), ShippedGrid, 60f, 500f));
                float b = Rms(Deviations(BakeAtDepth(ShippedGrid, r16), ShippedGrid, 60f, 500f));
                float x = Rms(Deviations(BakeAtDepth(ShippedGrid, 0), ShippedGrid, 60f, 500f));
                report.AppendLine($"{label,-11} {SlopeAtWaterline(),6:0.000} | {a * 100f,6:0.0} cm " +
                                  $"{b * 100f,6:0.0} cm {x * 100f,6:0.0} cm |");
                worst16 = Mathf.Max(worst16, b);
                best8 = Mathf.Min(best8, a - b);      // the per-tide GAIN, never a cross-tide compare
                worstGapToExact = Mathf.Max(worstGapToExact, Mathf.Abs(b - x));
                if (label == "spring LOW") lowGain = a / Mathf.Max(b, 1e-6f);
            }
            int px = ShippedGrid * ShippedGrid;
            report.AppendLine($"                            |                   | " +
                              $"{px * SeabedBakeMath.BytesPerTexel(TextureFormat.R8) / 1024} KB -> " +
                              $"{px * SeabedBakeMath.BytesPerTexel(TextureFormat.R16) / 1024} KB");
            TestContext.WriteLine(report.ToString());

            Assert.Less(worstGapToExact, 0.005f,
                "⭐ THE FIX'S CLAIM, and it is the exact one: R16 must land ON the exact-valued bake at " +
                "EVERY tide - it removes the whole value quantum and nothing less. It does NOT claim a " +
                "clean edge everywhere: what remains at mean tide is 3.8 cm of SPATIAL error from the " +
                "2.97 m texel pitch, which is register row 31's ground and not this fix's.");

            Assert.Greater(lowGain, 10f,
                "⭐ AND AT THE TIDE THE SYMPTOM WAS SEEN AT: spring low must improve by more than ten " +
                "times (28.7 cm -> 1.2 cm measured). That is the number the owner is being asked to " +
                "spend 64 KB per sea on.");

            Assert.GreaterOrEqual(best8, -1e-4f,
                "DEAD CONTROL: R16 must be no worse than R8 AT EVERY TIDE, compared tide by tide. " +
                "⚠️ The first version of this guard compared R16's worst tide against R8's best and " +
                "failed on a correct fix - mean tide's 3.8 cm is SPATIAL error, which R16 does not " +
                "claim to touch, so the comparison was between two different defects.");
        }

        /// <summary>
        /// The format policy: prefer <c>R16</c>, fall back to <c>R8</c>, and never answer "none". A
        /// policy that can refuse has no shipped behaviour — the twin of
        /// <c>FoamBufferFadeTests</c>'s guard on the foam buffer's preference, one row earlier.
        /// </summary>
        [Test]
        public void TheHeightFormatPolicy_PrefersSixteenBits_AndAlwaysAnswers()
        {
            Assert.AreEqual(TextureFormat.R16, SeabedBakeMath.SelectFormat(f => true),
                "a device that supports everything must get the widest format in the preference");
            Assert.AreEqual(TextureFormat.R8, SeabedBakeMath.SelectFormat(f => false),
                "⭐ a device that supports NOTHING must still be handed the last entry - the fallback is " +
                "what makes this a policy rather than a wish");
            Assert.AreEqual(TextureFormat.R8,
                            SeabedBakeMath.SelectFormat(f => f != TextureFormat.R16),
                "and a device missing exactly R16 must fall through to it, not skip past");
            Assert.AreEqual(TextureFormat.R8, SeabedBakeMath.SelectFormat(null),
                "a null probe is a device that cannot be asked, which is the fallback case too");

            Assert.AreEqual(255, SeabedBakeMath.CodeCountFor(TextureFormat.R8));
            Assert.AreEqual(65535, SeabedBakeMath.CodeCountFor(TextureFormat.R16));
            Assert.AreEqual(2 * SeabedBakeMath.BytesPerTexel(TextureFormat.R8),
                            SeabedBakeMath.BytesPerTexel(TextureFormat.R16),
                "the memory line the owner is owed (rule 7): sixteen bits costs exactly twice eight");

            // The general encode must BE the shipped 8-bit one where they overlap, or the deeper path
            // is a second implementation that can drift.
            for (float e = -6f; e <= 6f; e += 0.137f)
                Assert.AreEqual(SeabedBakeMath.Encode(e, -6f, 6f),
                                SeabedBakeMath.EncodeCode(e, -6f, 6f, SeabedBakeMath.CodeCount),
                                $"encode disagreement at {e:0.000} m");
        }

        /// <summary>
        /// 🔴 <b>ROW 10's PROPOSAL TABLE — what each band count would look like on the ground.</b>
        ///
        /// <para>The band DEPTHS are fixed by sigma; the count decides how many there are and therefore
        /// how far apart they land. Six is the shipped pixel-art choice. <b>More bands is less
        /// pixel-art, not more correct</b> — this table is for the owner to choose from, and nothing
        /// here moves the number (rule 6).</para>
        ///
        /// <para>The metric is the TIGHTEST pair of steps at spring high, where the waterline is on the
        /// steep beach and the bands crowd: that is the number that reads as "a wall" rather than as
        /// terracing.</para>
        /// </summary>
        [Test]
        public void TheAbsorptionBandCount_TabulatedForTheOwner()
        {
            var sigma = WaterAbsorption.Sigma(0.25f, WaterAbsorption.DefaultRatio);
            WaterLevel = TideMean + TideAmplitude;                 // spring high: the crowded case

            float BandDepth(int k, float bands)
            {
                float lo = 0f, hi = 40f;
                for (int i = 0; i < 60; i++)
                {
                    float mid = 0.5f * (lo + hi);
                    float t = WaterAbsorption.BandTransmission(
                                  WaterAbsorption.Transmission(sigma, mid), bands).x;
                    if (t > (bands - k - 0.5f) / bands) lo = mid; else hi = mid;
                }
                return 0.5f * (lo + hi);
            }

            float Offshore(float depth)
            {
                float lo = 0f, hi = 400f;
                for (int i = 0; i < 60; i++)
                {
                    float mid = 0.5f * (lo + hi);
                    if (WaterLevel - ShoreProfile(mid) < depth) lo = mid; else hi = mid;
                }
                return 0.5f * (lo + hi);
            }

            var report = new StringBuilder();
            report.AppendLine("ROW 10 - the band count, at SPRING HIGH (the steep beach, the crowded case)");
            report.AppendLine("  bands | steps in the first 30 m | tightest pair | first step at");
            float shippedTightest = 0f;

            foreach (float bands in new[] { 0f, 4f, 6f, 8f, 12f, 16f })
            {
                if (bands < 1f)
                {
                    report.AppendLine($"  {0,5} |  (continuous - no steps)  |          --   | " +
                                      "the ramp fades instead of terracing");
                    continue;
                }
                float prev = float.NaN, tight = float.MaxValue, first = float.NaN;
                int inThirty = 0;
                for (int k = 1; k <= (int)bands; k++)
                {
                    float off = Offshore(BandDepth(k, bands));
                    if (off >= 399f) break;
                    if (float.IsNaN(first)) first = off;
                    if (off - Offshore(0f) < 30f) inThirty++;
                    if (!float.IsNaN(prev)) tight = Mathf.Min(tight, off - prev);
                    prev = off;
                }
                if (Mathf.Approximately(bands, 6f)) shippedTightest = tight;
                report.AppendLine($"  {bands,5:0} | {inThirty,23} | {tight,10:0.0} m | {first,10:0.0} m" +
                                  (Mathf.Approximately(bands, 6f) ? "   <- SHIPPED" : ""));
            }
            report.AppendLine();
            report.AppendLine("  Fewer bands = wider terraces, each one a harder edge. More bands = a");
            report.AppendLine("  smoother fall but less of the posterised pixel-art read. 0 = continuous.");
            TestContext.WriteLine(report.ToString());

            Assert.Less(shippedTightest, 2f,
                "the shipped six must still crowd to under two metres at spring high — that is row 10's " +
                "wall, and if it stops being true this table is describing a sea that changed.");
        }

        /// <summary>
        /// 🔴 <b>THE DEAD CONTROL.</b> Fed a grid fine enough to be irrelevant, the whole pipeline must
        /// report a clean edge. Without this, a fixture that reported "combed" for every input would
        /// look exactly like a confirmed hypothesis.
        /// </summary>
        [Test]
        public void AFineEnoughGrid_DrawsTheContourItWasGiven()
        {
            float[] devs = Deviations(BakeAtDepth(4096, 0), 4096, 200f, 260f);
            float rms = Rms(devs);
            TestContext.WriteLine($"  a 4096 grid with exact values: {rms * 1000f:0.00} mm RMS from the " +
                                  "true contour");
            Assert.Less(rms, 0.01f,
                "DEAD CONTROL: with the grid and the value depth both taken out of the way the drawn " +
                "edge must land on the true contour. A fixture that still reports a comb here is " +
                "measuring its own arithmetic, and every number above it is worthless.");
        }
    }
}
