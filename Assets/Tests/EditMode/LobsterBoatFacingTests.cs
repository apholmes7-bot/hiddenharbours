using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE LOBSTER BOAT'S FACING PROOF — and she is the one hull where the answer is the OPPOSITE of
    /// every other iso kit's.</b>
    ///
    /// <para>Every hand-exported iso kit in this repo (dory, punt, skiffs, Cape Islander) was baked by a
    /// browser rig that rotates the model counter-clockwise while labelling the cells clockwise, so those
    /// sheets are mirrored and carry <c>FacingsAreCounterClockwise = true</c> to un-mirror them at runtime.
    /// HERS was baked IN-ENGINE by <c>RigBaker</c> (ADR 0021), which measured the rig's azimuth convention
    /// from rendered pixels and corrected it at BAKE time. Her cells are therefore genuinely, truly
    /// CLOCKWISE, and her flag is <c>false</c>.</para>
    ///
    /// <para><b>That difference is the single most likely thing for a future reader to "fix".</b> A glance
    /// at <c>BoatVisualLibraryBuilder</c> shows one entry not using the shared
    /// <c>IsoSheetsAreCounterClockwise</c> const and it looks exactly like an oversight. It is not. This
    /// fixture is what stands between that instinct and a mirrored boat: it reads her ACTUAL PIXELS,
    /// measures which way the drawn hull points in each of her 32 cells, and asserts the picture agrees
    /// with the heading the real index math would choose that cell for. Flip the flag and it goes red.</para>
    ///
    /// <para><b>Nothing here is asserted against a constant of mine.</b> That is the discipline this project
    /// learned the hard way — mirrored art shipped five times behind tests that said "heading 90 picks
    /// element 2", which is true no matter what element 2 contains. Every number below comes off the PNG.</para>
    ///
    /// <para><b>Why she needs her own fixture rather than a row in
    /// <see cref="BoatFacingDepictedHeadingTests"/>.</b> That file is built around <c>const int Headings = 8</c>
    /// and a single-row hull sheet. She is 32 facings laid out as an 8×4 GRID, so both the cell arithmetic
    /// and the loop bounds differ. Generalising the shared file would have meant reworking the fixture that
    /// currently guards six other kits — a much bigger blast radius than adding one file. The MEASUREMENT
    /// itself is deliberately the same method, and validated the same way (see below).</para>
    ///
    /// <para><b>How the depicted heading is measured</b> — the Cape Islander's method, unchanged, because she
    /// has the same problem: inboard diesel means no outboard marking her transom and no oars giving a port
    /// beam, so all that is left is the hull silhouette. A principal axis alone is a LINE and pins the
    /// heading only modulo 180°, which is exactly blind on the question of which of two conventions the art
    /// was baked in. So two terms are added: the silhouette is UN-FORESHORTENED first (the rig squashes
    /// screen-Y by sin 40°, which drags a raw-pixel axis toward the horizontal), and a BOW-TAPER term
    /// compares the perpendicular spread at each end of the axis to pick the pointed end. That turns the
    /// line into an arrow and makes the comparison full-circle.</para>
    ///
    /// <para><b>The method is validated before it is trusted.</b>
    /// <see cref="TheWrongConvention_WouldBeOffByFarMoreThanAnyMeasurementError"/> runs her cells through the
    /// CCW convention her art does NOT use and requires that mismatch to be enormous. If the two conventions
    /// ever became hard to tell apart the honest response is a better feature to measure — never a wider
    /// tolerance.</para>
    /// </summary>
    public class LobsterBoatFacingTests
    {
        const string ArtBoats = "Assets/_Project/Art/Boats";
        const string Visuals = "Assets/_Project/Data/Boats/Visuals";
        const string HullSheet = ArtBoats + "/LobsterBoatIso.png";

        /// <summary>Her compass size — the owner's call, and the reason the in-engine baker exists.</summary>
        const int Headings = 32;
        const float Step = 360f / Headings;

        /// <summary>Her sheet's grid: 8 columns × 4 rows, row-major from the top-left (cell = row·8 + col).</summary>
        const int SheetColumns = 8;
        const int SheetRows = 4;

        /// <summary>Her rig's <c>DEFAULT_ELEV</c>. An ART FACT: it sets how hard screen-Y is squashed.</summary>
        const float RigElevationDegrees = 40f;

        /// <summary>
        /// Measurement-noise budget for the hull-silhouette bearing, in degrees.
        ///
        /// <para>Wider than the 8-way kits' 12° for a reason that is about HER ART and not about what
        /// happens to pass: at 32 facings adjacent cells are 11.25° apart, so the silhouette changes only
        /// slightly from one to the next while her heavy wheelhouse, extended hardtop and radar arch keep
        /// pulling the covariance axis off the keel line by a roughly constant amount. The residual is
        /// SYMMETRIC about the cardinals — the signature of cabin mass, not of a mislabelled cell — and it
        /// is bounded well inside this budget.</para>
        ///
        /// <para>It is a noise budget and nothing more. The thing this fixture discriminates, a mirrored
        /// bake, is up to 180° out, and
        /// <see cref="TheWrongConvention_WouldBeOffByFarMoreThanAnyMeasurementError"/> asserts that
        /// separation directly so this number is never the load-bearing one.</para>
        /// </summary>
        const float HullAxisToleranceDegrees = 15f;

        /// <summary>
        /// <b>The assertion this whole file exists for:</b> for each of her 32 headings, ask the REAL index
        /// math — through the REAL flag on the REAL committed asset — which cell it would draw, then measure
        /// that cell's pixels and require the boat in it to actually point where it was asked to.
        ///
        /// <para>The pixels are asserted BEFORE the flag is inspected, deliberately. Checking the flag first
        /// would mask the measurement and leave this file asserting configuration against itself, which is
        /// the exact blind spot that let mirrored art ship repeatedly.</para>
        /// </summary>
        [Test]
        public void EveryHeading_DrawsACellThatActuallyDepictsIt_BowAndAll()
        {
            var visual = LoadVisual();
            float[] depicted = HullBearingsPerCell();

            float worst = 0f;
            int worstHeading = 0;
            for (int i = 0; i < Headings; i++)
            {
                float heading = i * Step;
                int cell = DirectionalBoatSprite.HeadingToFacingIndex(
                    heading, Headings, visual.ZeroHeadingDegrees, visual.FacingsAreCounterClockwise);

                float delta = Mathf.Abs(Mathf.DeltaAngle(depicted[cell], heading));
                if (delta > worst) { worst = delta; worstHeading = i; }

                Assert.LessOrEqual(delta, HullAxisToleranceDegrees,
                    $"heading {heading:0.00}° draws cell {cell}, whose HULL measures {depicted[cell]:0.0}° — off " +
                    $"by {delta:0.0}°. A few degrees is the wheelhouse pulling the covariance axis off the keel; " +
                    $"45° or more is the CONVENTION. The committed visual '{visual.Id}' has " +
                    $"FacingsAreCounterClockwise={visual.FacingsAreCounterClockwise}. If someone just set that " +
                    "to true to match the other iso kits: put it back. Hers was baked in-engine and is " +
                    "genuinely clockwise — see the block comment in BoatVisualLibraryBuilder.");
            }

            // Reported, not asserted as a threshold — a human reading a CI log should be able to see how much
            // headroom the budget actually has rather than having to re-derive it.
            Debug.Log($"[LobsterBoatFacingTests] worst cell {worst:0.0}° (heading {worstHeading * Step:0.00}°), " +
                      $"budget {HullAxisToleranceDegrees}°.");

            Assert.IsFalse(visual.FacingsAreCounterClockwise,
                "The lobster boat was baked IN-ENGINE (ADR 0021): RigBaker measured the rig's azimuth " +
                "convention and corrected it at bake time, so her cells are genuinely CLOCKWISE and this flag " +
                "must stay FALSE. She is the only entry in BoatVisualLibraryBuilder that does not use the " +
                "shared IsoSheetsAreCounterClockwise const, and that is correct — do NOT 'fix' her to match " +
                "her neighbours, and do NOT change that const, which every other boat still needs. The pixel " +
                "assertions above are what prove this; this line only states the conclusion.");
        }

        /// <summary>
        /// <b>The separation, asserted — so the tolerance above is never the load-bearing number.</b>
        ///
        /// <para>A tolerance test can only ever say "close enough". This says the alternative is nowhere
        /// near: it measures her cells against the counter-clockwise reading her art does NOT use and
        /// requires that mismatch to be enormous. At 32 facings a CCW misread sends the cardinals stern-first
        /// and swings everything else by up to 180°, so the gap between measurement noise and the wrong
        /// convention is not close.</para>
        ///
        /// <para>⚠️ THIS IS ALSO THE SABOTAGE CANARY. Flip her flag to true in the builder, rebuild the
        /// asset, and <see cref="EveryHeading_DrawsACellThatActuallyDepictsIt_BowAndAll"/> fails immediately
        /// — by the very margin this test measures. If a future re-bake ever narrows this gap, the honest
        /// response is to find a better feature to measure, not to widen
        /// <see cref="HullAxisToleranceDegrees"/>.</para>
        /// </summary>
        [Test]
        public void TheWrongConvention_WouldBeOffByFarMoreThanAnyMeasurementError()
        {
            var visual = LoadVisual();
            float[] depicted = HullBearingsPerCell();

            float worstUnderTheWrongConvention = 0f;
            for (int i = 0; i < Headings; i++)
            {
                float heading = i * Step;
                // The SAME index math, run through the convention her art is NOT baked in.
                int cell = DirectionalBoatSprite.HeadingToFacingIndex(
                    heading, Headings, visual.ZeroHeadingDegrees, facingsAreCounterClockwise: true);
                worstUnderTheWrongConvention = Mathf.Max(
                    worstUnderTheWrongConvention, Mathf.Abs(Mathf.DeltaAngle(depicted[cell], heading)));
            }

            Assert.Greater(worstUnderTheWrongConvention, 45f,
                $"reading the lobster boat counter-clockwise is only {worstUnderTheWrongConvention:0.0}° wrong " +
                "at its worst cell. It should be far more. If these two conventions have become hard to tell " +
                "apart, the measurement has lost its power and needs a better feature — do NOT widen " +
                nameof(HullAxisToleranceDegrees) + " to make this go away.");

            Assert.Greater(worstUnderTheWrongConvention, HullAxisToleranceDegrees * 4f,
                "the wrong convention must be wrong by far more than the measurement noise budget, or the " +
                "budget is doing work it cannot do");
        }

        /// <summary>
        /// The compass is 32 cells and the rock grid is 32×4 across two pages — asserted on the COMMITTED
        /// asset, because the builder concatenating her pages in the wrong order (or dropping one) would
        /// still leave a def that loads and renders, just with the boat snapping to the wrong rock frame at
        /// half her headings. <see cref="Tests.RigBaking.LobsterBoatSheetSliceTests"/> guards the sheets;
        /// this guards the BINDING.
        /// </summary>
        [Test]
        public void HerCommittedVisual_BindsAllThirtyTwoFacings_AndBothRockPages()
        {
            var visual = LoadVisual();

            Assert.IsTrue(visual.HasFullCompass(), "her compass is incomplete — every facing must be bound");
            Assert.AreEqual(Headings, visual.HeadingCount,
                "she is a 32-facing hull. If this reads 8 someone re-baked her at the old count, and a 12 m " +
                "boat is back to snapping between 45° steps — the thing ADR 0021 set out to fix.");

            Assert.AreEqual(4, visual.RockFrameCount,
                "her bake emits 4 rock frames, not the 8 every other kit ships");
            Assert.IsTrue(visual.HasRockGrid(),
                $"her rock grid is not fully bound ({visual.RockGrid.Length} sprites, expected " +
                $"{Headings * 4} = 32 headings × 4 frames concatenated across LobsterBoatIsoRock0 and Rock1). " +
                "A page missing or mis-ordered lands here.");

            // Inboard diesel: no oars, no outboard. Asserted rather than assumed, because HasConflictingOverlays
            // only fires when BOTH are present and a stray half-bound overlay would otherwise pass unnoticed.
            Assert.IsFalse(visual.HasOarSheets(), "she is not rowed — no oar sheets should be bound");
            Assert.IsFalse(visual.HasMotor(),
                "she is INBOARD diesel: there is no outboard drawn on her because there is no outboard to " +
                "draw. Binding motor sheets here would be inventing art.");
        }

        // ---- pixel plumbing ------------------------------------------------------------------

        /// <summary>
        /// The bearing each of her 32 cells actually DEPICTS — full circle, bow and all. Cells are sliced
        /// arithmetically off the 8×4 grid, row-major from the top-left, which matches
        /// <c>SpriteSheetSlicer</c> and is independently guarded by the slice tests.
        /// </summary>
        static float[] HullBearingsPerCell()
        {
            Texture2D tex = LoadTexture(HullSheet);
            int cw = tex.width / SheetColumns, ch = tex.height / SheetRows;
            var pixels = tex.GetPixels32();

            // The rig squashes screen-Y by sin(elev) and leaves screen-X alone, so dividing Y back out
            // restores the plan view the heading actually lives in. Skip it and every diagonal reads several
            // degrees toward E/W — which at 11.25° steps is most of a whole facing.
            float se = Mathf.Sin(RigElevationDegrees * Mathf.Deg2Rad);

            var result = new float[Headings];
            for (int cell = 0; cell < Headings; cell++)
            {
                int col = cell % SheetColumns, row = cell / SheetColumns;
                int x0 = col * cw;

                var xs = new List<double>();
                var ys = new List<double>();
                for (int y = 0; y < ch; y++)
                {
                    // Unity's texture origin is BOTTOM-left; the sheet's grid row 0 is the TOP row.
                    int texY = tex.height - 1 - (row * ch + y);
                    for (int x = 0; x < cw; x++)
                        if (pixels[texY * tex.width + x0 + x].a > 32)
                        {
                            xs.Add(x);
                            ys.Add(-y / se);   // -y so +Y is up, matching the compass; /se un-foreshortens
                        }
                }
                Assert.Greater(xs.Count, 0,
                    $"{HullSheet}: cell {cell} (row {row}, col {col}) is fully transparent — the 8×4 grid " +
                    "layout assumed here is wrong.");

                double mx = Mean(xs), my = Mean(ys), xx = 0, yy = 0, xy = 0;
                for (int i = 0; i < xs.Count; i++)
                {
                    double dx = xs[i] - mx, dy = ys[i] - my;
                    xx += dx * dx; yy += dy * dy; xy += dx * dy;
                }

                // Major axis of the covariance ellipse — the keel line, as a LINE.
                double ang = 0.5 * System.Math.Atan2(2 * xy, xx - yy);
                double ax = System.Math.Cos(ang), ay = System.Math.Sin(ang);

                // ...and the bow-taper term that turns that line into an arrow. Project onto the axis, then
                // compare the perpendicular spread of the far 12% at each end: the bow is POINTED and the
                // transom BLUNT, so the narrower end is the bow.
                var proj = new List<double>(xs.Count);
                var perp = new List<double>(xs.Count);
                for (int i = 0; i < xs.Count; i++)
                {
                    double dx = xs[i] - mx, dy = ys[i] - my;
                    proj.Add(dx * ax + dy * ay);
                    perp.Add(-dx * ay + dy * ax);
                }
                double lo = Percentile(proj, 0.12), hi = Percentile(proj, 0.88);
                if (SpreadWhere(perp, proj, hi, above: true) > SpreadWhere(perp, proj, lo, above: false))
                {
                    ax = -ax; ay = -ay;   // the +axis end is the blunt one, so the bow is the other way
                }

                // Into the project's compass convention (0 = +Y/North, clockwise).
                float bearing = Mathf.Atan2((float)ax, (float)ay) * Mathf.Rad2Deg;
                result[cell] = (bearing % 360f + 360f) % 360f;
            }

            Object.DestroyImmediate(tex);
            return result;
        }

        static double Mean(List<double> v)
        {
            double s = 0;
            for (int i = 0; i < v.Count; i++) s += v[i];
            return s / v.Count;
        }

        static double Percentile(List<double> v, double p)
        {
            var sorted = new List<double>(v);
            sorted.Sort();
            return sorted[Mathf.Clamp((int)(p * (sorted.Count - 1)), 0, sorted.Count - 1)];
        }

        /// <summary>Standard deviation of <paramref name="perp"/> over the samples whose projection is beyond
        /// <paramref name="cut"/> — i.e. the silhouette's width at one end of the keel line.</summary>
        static double SpreadWhere(List<double> perp, List<double> proj, double cut, bool above)
        {
            double s = 0, s2 = 0; int n = 0;
            for (int i = 0; i < proj.Count; i++)
            {
                if (above ? proj[i] < cut : proj[i] > cut) continue;
                s += perp[i]; s2 += perp[i] * perp[i]; n++;
            }
            if (n == 0) return 0;
            double mean = s / n;
            return System.Math.Sqrt(System.Math.Max(0, s2 / n - mean * mean));
        }

        /// <summary>
        /// The PNG off disk as a readable throwaway texture. Deliberately NOT the imported asset: her sheets
        /// import <c>spriteMode: Multiple</c> (so <c>LoadAssetAtPath&lt;Sprite&gt;</c> returns null) and
        /// non-readable, and a test has no business rewriting the import settings of committed art to see it.
        /// </summary>
        static Texture2D LoadTexture(string path)
        {
            Assert.IsTrue(File.Exists(path), $"art missing: {path}");
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(path)), $"not a decodable PNG: {path}");
            return tex;
        }

        static BoatVisualDef LoadVisual()
        {
            var def = AssetDatabase.LoadAssetAtPath<BoatVisualDef>($"{Visuals}/LobsterBoatIso.asset");
            Assert.IsNotNull(def,
                "missing visual def LobsterBoatIso — run Hidden Harbours ▸ Art ▸ Build Boat Visual Defs");
            return def;
        }
    }
}
