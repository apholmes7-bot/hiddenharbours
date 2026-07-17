using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE TEST THAT WAS MISSING — and the whole reason the mirrored art shipped.</b>
    ///
    /// <para>Everything around this was already tested, and all of it passed while the boats drew backwards.
    /// <see cref="DirectionalBoatSpriteTests"/> asserts the heading→index mapping against ITSELF ("heading
    /// 90 picks element 2") — true, and true no matter what element 2 contains. The slice tests assert rects
    /// and pivots — true, and true no matter which way the boat in the rect points. Not one assertion ever
    /// said <b>cell i actually DEPICTS heading 45·i</b>. That single missing sentence is the bug: the iso
    /// rigs bake counter-clockwise and label clockwise, so every iso hull drew −2·heading off — square-on at
    /// N/S (which is why it hid), full beam at the diagonals, and stern-first at E/W.</para>
    ///
    /// <para>So these tests read the ACTUAL PIXELS and measure which way each drawn boat points, then assert
    /// the picture agrees with the heading the code would choose it for. They are the only thing in the suite
    /// that can tell the art and the code apart, and they are deliberately end-to-end: they run the real
    /// <see cref="DirectionalBoatSprite.HeadingToFacingIndex"/> through the real
    /// <see cref="BoatVisualDef.FacingsAreCounterClockwise"/> off the real committed asset. Flip that flag on
    /// any kit and these go red.</para>
    ///
    /// <para><b>Both conventions are pinned here on purpose.</b> The 6 iso kits are CCW; the older hand-drawn
    /// <c>FishingBoat_*</c> compass is CW and always was correct. That disagreement is exactly why the fix
    /// had to be per-artwork data rather than a blanket mirror — a global flip would have fixed the iso kits
    /// and silently broken the fishing boat and the whole ambient fleet that shares her facings. Testing only
    /// one lineage would let them diverge again in the dark, so both are asserted, together.</para>
    ///
    /// <para><b>How the depicted heading is measured.</b> Every kit is baked by the same orthographic rig at
    /// a fixed 40° elevation (<c>DEFAULT_ELEV</c>), so a compass bearing <c>b</c> projects to the screen
    /// direction <c>(sin b, cos b · sin 40°)</c> — foreshortened vertically, never rotated. Pick a feature on
    /// the boat whose true bearing is known relative to the bow, measure its alpha-weighted centroid offset,
    /// and the best-matching bearing of the 8 is the heading the cell depicts. Both features used are
    /// OFFSET-FREE (they need no fudge constant):
    /// <list type="bullet">
    ///   <item>the OUTBOARD hangs on the transom, so it marks the STERN — bearing = heading + 180;</item>
    ///   <item>port oar MINUS starboard oar is the PORT BEAM by symmetry — bearing = heading − 90.</item>
    /// </list>
    /// Both score a 1.000 match against the measurement, so the assertion is not marginal.</para>
    ///
    /// <para>Textures are read as BYTES off disk into a throwaway <see cref="Texture2D"/> rather than by
    /// flipping <c>isReadable</c> on committed art — the sheets import <c>spriteMode: Multiple</c>, so
    /// <c>LoadAssetAtPath&lt;Sprite&gt;</c> would return null anyway and the import settings are not this
    /// test's to change. Cells are sliced arithmetically (sheet ÷ columns × 8 headings), which matches the
    /// slicer and is independently guarded by the slice tests.</para>
    /// </summary>
    public class BoatFacingDepictedHeadingTests
    {
        const string ArtBoats = "Assets/_Project/Art/Boats";
        const string Visuals = "Assets/_Project/Data/Boats/Visuals";

        const int Headings = 8;
        const float Step = 360f / Headings;

        /// <summary>The rigs' fixed camera elevation (every iso rig's <c>DEFAULT_ELEV</c>). An ART FACT: it
        /// sets how hard the vertical axis is foreshortened, and nothing else in the measurement.</summary>
        const float RigElevationDegrees = 40f;

        /// <summary>Bearing of the transom-hung outboard relative to the bow: dead astern.</summary>
        const float SternBearingOffset = 180f;

        /// <summary>Bearing of (port oar − starboard oar) relative to the bow: the port beam.</summary>
        const float PortBeamBearingOffset = -90f;

        // ---- the iso kits: baked CCW, every cell measured ------------------------------------

        [TestCase("ConsoleSkiff", "SkiffMotorLower-Work.png", OutboardMotorMath.SteerColumns)]
        [TestCase("SportSkiffSingle", "SkiffMotorLower-Sport.png", OutboardMotorMath.SteerColumns)]
        [TestCase("SportSkiffTwin", "SkiffMotorLower-Sport.png", OutboardMotorMath.SteerColumns)]
        [TestCase("PuntIsoBasic", "PuntMotorLower-Basic.png", OutboardMotorMath.SteerColumns)]
        [TestCase("PuntIsoUpgraded", "PuntMotorLower-Upgraded.png", OutboardMotorMath.SteerColumns)]
        public void PoweredIsoKit_CellChosenForHeading_ActuallyDepictsThatHeading(
            string visualName, string motorSheet, int columns)
        {
            var visual = LoadVisual(visualName);
            // Dead-ahead steer column: the engine on the centreline, so its offset is pure stern.
            Vector2[] stern = CentroidsPerHeading($"{ArtBoats}/{motorSheet}", columns, columns / 2);

            AssertEveryHeadingIsDepicted(visual, stern, SternBearingOffset, visualName + " outboard/transom");
        }

        [Test]
        public void Dory_CellChosenForHeading_ActuallyDepictsThatHeading()
        {
            var visual = LoadVisual("DoryIso");
            // The dory is ROWED — no engine to mark her stern. Port-minus-starboard oar is the port beam by
            // symmetry, which needs no assumption about where along the boat the oars sit. Column 8 = the
            // resting/shipped pose (both oars stowed, maximally symmetric).
            const int restingColumn = 8;
            Vector2[] port = CentroidsPerHeading($"{ArtBoats}/DoryOarPort.png", visual.OarColumnCount, restingColumn);
            Vector2[] star = CentroidsPerHeading($"{ArtBoats}/DoryOarStar.png", visual.OarColumnCount, restingColumn);

            var beam = new Vector2[Headings];
            for (int i = 0; i < Headings; i++) beam[i] = port[i] - star[i];

            AssertEveryHeadingIsDepicted(visual, beam, PortBeamBearingOffset, "DoryIso port-minus-starboard oar",
                                         subtractMean: false);   // a difference already has the common offset out
        }

        // ---- the OTHER lineage: the hand-drawn compass, CW and correct -----------------------

        /// <summary>
        /// The <c>FishingBoat_*</c> compass is 8 separate hand-drawn files and is labelled CORRECTLY, so its
        /// flag must stay false. She carries no outboard and no oars, so the centroid features above do not
        /// exist; instead each cell's hull is measured by the PRINCIPAL AXIS of its silhouette (the keel line),
        /// which must lie along the heading the cell is chosen for.
        ///
        /// <para>An axis is a line, so this pins the heading modulo 180° — it cannot tell bow from stern. That
        /// is a real limit and it is stated rather than papered over; a taper/skew bow-detector was tried and
        /// is not trustworthy on this art (the cabin dominates the silhouette). It is still fully decisive for
        /// the thing this file exists to guard: mirroring this compass would swing the four DIAGONALS by 90°
        /// (the NE cell's axis would read 135° instead of 45°), which this catches immediately. Bow-vs-stern
        /// on the cardinals was verified by eye against the raw PNGs at the time of the fix — E's flared bow
        /// and mast point right.</para>
        /// </summary>
        [Test]
        public void FishingBoatCompass_IsClockwise_CellChosenForHeadingLiesAlongIt()
        {
            var visual = LoadVisual("FishingBoat");

            // The PIXELS first, then the flag: this order matters. If the flag guard ran first it would mask
            // the measurement, and this file would be back to asserting configuration against itself — the
            // exact self-referential blind spot that let the mirrored art ship in the first place.
            for (int i = 0; i < Headings; i++)
            {
                float heading = i * Step;
                int cell = DirectionalBoatSprite.HeadingToFacingIndex(
                    heading, Headings, visual.ZeroHeadingDegrees, visual.FacingsAreCounterClockwise);

                string path = $"{ArtBoats}/FishingBoat_{CompassSuffix(cell)}.png";
                float axis = PrincipalAxisBearing(LoadTexture(path));

                // Compare modulo 180: the keel is a line, not an arrow.
                float delta = Mathf.Abs(Mathf.DeltaAngle(axis * 2f, heading * 2f)) * 0.5f;
                Assert.LessOrEqual(delta, 12f,
                    $"Heading {heading}° picks cell {cell} ({path}), whose hull axis measures {axis:0.0}° — " +
                    $"off by {delta:0.0}°. The compass is drawn clockwise; if this fails the convention flag " +
                    "for this artwork is wrong (or the art was re-drawn).");
            }

            Assert.IsFalse(visual.FacingsAreCounterClockwise,
                "The hand-drawn FishingBoat compass is CLOCKWISE and correct — it must NOT be mirrored. " +
                "It is shared with the ambient fleet; flipping this breaks every boat on the horizon.");
        }

        // ---- the shared assertion ------------------------------------------------------------

        /// <summary>
        /// The whole point, in one place: for each of the 8 headings, ask the REAL index math (through the
        /// visual's REAL convention flag) which cell it would draw, then measure that cell's pixels and assert
        /// the boat in it actually points where it was asked to.
        /// </summary>
        void AssertEveryHeadingIsDepicted(BoatVisualDef visual, Vector2[] featurePerCell, float featureBearingOffset,
                                          string what, bool subtractMean = true)
        {
            Vector2 mean = Vector2.zero;
            if (subtractMean)
            {
                // The feature's constant screen lift (its height above the waterline projects to a fixed
                // vertical offset the rig applies identically to every heading) has to come out before the
                // direction means anything. Over 8 evenly-spaced headings the heading-dependent part sums to
                // zero exactly, so the mean IS that constant — no fitted fudge.
                for (int i = 0; i < Headings; i++) mean += featurePerCell[i];
                mean /= Headings;
            }

            for (int i = 0; i < Headings; i++)
            {
                float heading = i * Step;
                int cell = DirectionalBoatSprite.HeadingToFacingIndex(
                    heading, Headings, visual.ZeroHeadingDegrees, visual.FacingsAreCounterClockwise);

                Vector2 measured = featurePerCell[cell] - mean;
                float depicted = BestMatchingHeading(measured, featureBearingOffset);

                Assert.AreEqual(heading, depicted, 1e-3f,
                    $"{what}: heading {heading}° draws cell {cell}, but that cell DEPICTS a boat heading " +
                    $"{depicted}°. The picture and the heading disagree — this is the counter-clockwise bake " +
                    $"(cell i shows −45·i). visual '{visual.Id}' has FacingsAreCounterClockwise=" +
                    $"{visual.FacingsAreCounterClockwise}; it looks wrong.");
            }
        }

        /// <summary>
        /// Which of the 8 headings best explains a measured feature offset. The rig is orthographic at a fixed
        /// elevation, so bearing <c>b</c> projects to <c>(sin b, cos b · sin(elev))</c>; both the prediction
        /// and the measurement are normalised before the dot product, because the prediction is SHORTER near
        /// N/S than near E/W and an un-normalised score would quietly bias every diagonal toward E/W.
        /// </summary>
        static float BestMatchingHeading(Vector2 measured, float featureBearingOffset)
        {
            float se = Mathf.Sin(RigElevationDegrees * Mathf.Deg2Rad);
            Vector2 m = measured.normalized;

            float best = 0f, bestScore = float.NegativeInfinity;
            for (int j = 0; j < Headings; j++)
            {
                float candidate = j * Step;
                float b = (candidate + featureBearingOffset) * Mathf.Deg2Rad;
                Vector2 predicted = new Vector2(Mathf.Sin(b), Mathf.Cos(b) * se).normalized;

                float score = Vector2.Dot(m, predicted);
                if (score > bestScore) { bestScore = score; best = candidate; }
            }
            return best;
        }

        // ---- pixel plumbing ------------------------------------------------------------------

        /// <summary>
        /// Alpha-weighted centroid of one cell per heading row, in cell-local pixels with +Y UP (image rows
        /// run down). Sheets are heading-rows × <paramref name="columns"/>, sliced arithmetically.
        /// </summary>
        static Vector2[] CentroidsPerHeading(string sheetPath, int columns, int column)
        {
            Texture2D tex = LoadTexture(sheetPath);
            int cw = tex.width / columns, ch = tex.height / Headings;
            var pixels = tex.GetPixels32();

            var result = new Vector2[Headings];
            for (int row = 0; row < Headings; row++)
            {
                double sx = 0, sy = 0; long n = 0;
                for (int y = 0; y < ch; y++)
                {
                    // Unity's texture origin is BOTTOM-left; the sheet's heading row 0 is the TOP row.
                    int texY = tex.height - 1 - (row * ch + y);
                    for (int x = 0; x < cw; x++)
                    {
                        if (pixels[texY * tex.width + column * cw + x].a <= 32) continue;
                        sx += x; sy += -y; n++;   // -y so the vector is +Y up, matching the compass
                    }
                }
                Assert.Greater(n, 0, $"{sheetPath}: heading row {row}, column {column} is fully transparent — " +
                                     "there is nothing to measure, so the sheet layout assumed here is wrong.");
                result[row] = new Vector2((float)(sx / n), (float)(sy / n));
            }
            Object.DestroyImmediate(tex);
            return result;
        }

        /// <summary>
        /// Compass bearing of the principal axis of a silhouette (the direction of greatest spread — for a
        /// boat, the keel line). Modulo 180: this is a line, with no bow/stern sense.
        /// </summary>
        static float PrincipalAxisBearing(Texture2D tex)
        {
            var pixels = tex.GetPixels32();
            double sx = 0, sy = 0; long n = 0;
            for (int y = 0; y < tex.height; y++)
                for (int x = 0; x < tex.width; x++)
                    if (pixels[y * tex.width + x].a > 32) { sx += x; sy += y; n++; }
            Assert.Greater(n, 0, "fully transparent sprite — nothing to measure");

            double mx = sx / n, my = sy / n, xx = 0, yy = 0, xy = 0;
            for (int y = 0; y < tex.height; y++)
                for (int x = 0; x < tex.width; x++)
                {
                    if (pixels[y * tex.width + x].a <= 32) continue;
                    double dx = x - mx, dy = y - my;
                    xx += dx * dx; yy += dy * dy; xy += dx * dy;
                }
            Object.DestroyImmediate(tex);

            // Major-axis angle of the covariance ellipse, then into the compass convention (0 = +Y, CW).
            double ang = 0.5 * System.Math.Atan2(2 * xy, xx - yy);
            float bearing = Mathf.Atan2((float)System.Math.Cos(ang), (float)System.Math.Sin(ang)) * Mathf.Rad2Deg;
            return (bearing % 360f + 360f) % 360f;
        }

        /// <summary>
        /// The PNG off disk as a readable throwaway texture. Deliberately NOT the imported asset: these sheets
        /// import spriteMode Multiple (so LoadAssetAtPath&lt;Sprite&gt; is null) and non-readable, and a test
        /// has no business rewriting the import settings of committed art to see it.
        /// </summary>
        static Texture2D LoadTexture(string path)
        {
            Assert.IsTrue(File.Exists(path), $"art missing: {path}");
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(path)), $"not a decodable PNG: {path}");
            return tex;
        }

        static BoatVisualDef LoadVisual(string name)
        {
            var def = AssetDatabase.LoadAssetAtPath<BoatVisualDef>($"{Visuals}/{name}.asset");
            Assert.IsNotNull(def, $"missing visual def {name} — run Hidden Harbours ▸ Art ▸ Build Boat Visual Defs");
            Assert.AreEqual(Headings, def.HeadingCount, $"{name} is not an 8-way compass");
            return def;
        }

        static string CompassSuffix(int i) =>
            new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" }[i];
    }
}
