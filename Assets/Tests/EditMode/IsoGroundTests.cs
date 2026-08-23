using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>WORLD XY IS THE SQUASHED GROUND PLANE, AND THE CHARACTER ROWS ARE GROUND BEARINGS.</b>
    ///
    /// <para>The second half of that sentence is a MEASUREMENT, not an assumption. A standalone V8 harness
    /// reproduced the committed <c>Fisher_idle.png</c> byte-for-byte (1 130 496 bytes identical) and then
    /// matched each committed row against a continuous turntable angle: row 1, the one labelled NE, matches
    /// at exactly <b>45.00°</b> of ground azimuth at distance <b>0</b>, with a clean monotone bowl either
    /// side. The rival reading — that the rows are evenly spaced in world-XY/screen bearings, putting the
    /// first diagonal at 57.26° — scores 142 370. Same answer from the rig source in one line
    /// (<c>characterIsoRig6.js</c>: <c>th = -dir*Math.PI/4</c>, a plain vertical-axis turntable, squashed
    /// only afterwards at projection).</para>
    ///
    /// <para>So a world-space velocity has to be un-squashed before it picks a row. These tests pin that,
    /// pin the constant against the Art-side definition it mirrors, and — the part that matters — pin the
    /// ROW, because a bearing that is 12° out still picks the right cell most of the time and only shows up
    /// as the wrong picture near a boundary.</para>
    /// </summary>
    public class IsoGroundTests
    {
        // The two headings used below sit in the two bands where the squash used to flip the cell.
        // Derived, not guessed: tan(ground) = sin40 * tan(world), so ground 22.5° is world 32.79° and
        // ground 67.5° is world 75.10°. Anything between a bucket edge and its world image drew the
        // NEIGHBOURING facing.
        const float InsideTheFirstBand = 28f;    // world bearing; ground bearing ~18.9°
        const float InsideTheSecondBand = 71f;   // world bearing; ground bearing ~61.8°

        static Vector2 WorldUnit(float worldBearingDeg) =>
            new Vector2(Mathf.Sin(worldBearingDeg * Mathf.Deg2Rad),
                        Mathf.Cos(worldBearingDeg * Mathf.Deg2Rad));

        // ---- the constant ---------------------------------------------------------------------------

        [Test]
        public void GroundDepthScale_IsPinnedToTheArtSideDefinition()
        {
            // Core references nothing (rule 4), so it cannot read SpriteLightMath and carries the number
            // instead. This is the seam that keeps the copy honest — the BuildingInterior precedent.
            Assert.AreEqual(SpriteLightMath.CameraElevationDeg, IsoGround.CameraElevationDegrees, 1e-6f,
                "the shared bake camera's elevation must be ONE number, not two that agree today");
            Assert.AreEqual(SpriteLightMath.GroundDepthScale, IsoGround.GroundDepthScale, 1e-6f,
                "Core's copy of the depth squash must track Art's definition");
            Assert.AreEqual(SpriteLightMath.HeightScale, IsoGround.HeightScale, 1e-6f,
                "and so must its copy of the HEIGHT scale, which is the other half of the same camera");
        }

        [Test]
        public void HeightAndDepth_AreDifferentNumbers_AndNeitherStandsInForTheOther()
        {
            // 0.766 against 0.643. The two are close enough to look interchangeable in a diff and 19%
            // apart on screen, which is the whole reason they are named separately: a storey height run
            // through the depth squash lands a fifth of a storey low, and reads as "the art is a bit off"
            // rather than as a bug. (InteriorLevelLayout.UpperLevelY is the consumer that would.)
            Assert.Greater(IsoGround.HeightScale, IsoGround.GroundDepthScale,
                "under a camera BELOW 45° a metre of height draws taller than a metre of depth");
            Assert.AreEqual(1f, IsoGround.HeightScale * IsoGround.HeightScale +
                                IsoGround.GroundDepthScale * IsoGround.GroundDepthScale, 1e-5f,
                "and the two are one camera's sin/cos, not two independently tuned numbers");
        }

        // ---- the bearing ----------------------------------------------------------------------------

        [Test]
        public void Bearing_IsExactOnTheCardinals_WhereThereIsNothingToUnSquash()
        {
            Assert.AreEqual(0f, IsoGround.BearingDegrees(new Vector2(0f, 1f)), 1e-3f, "N");
            Assert.AreEqual(90f, IsoGround.BearingDegrees(new Vector2(1f, 0f)), 1e-3f, "E");
            Assert.AreEqual(180f, Mathf.Abs(IsoGround.BearingDegrees(new Vector2(0f, -1f))), 1e-3f, "S");
            Assert.AreEqual(-90f, IsoGround.BearingDegrees(new Vector2(-1f, 0f)), 1e-3f, "W");
            Assert.AreEqual(0f, IsoGround.BearingDegrees(Vector2.zero), 1e-6f, "a zero delta is safe");
        }

        [Test]
        public void Bearing_ReadsATrueGroundDiagonalAs45_AndAWorldDiagonalAsSomethingElse()
        {
            // A character walking equal GROUND metres north and east moves (d, 0.643d) in world units.
            var groundDiagonal = new Vector2(1f, IsoGround.GroundDepthScale);
            Assert.AreEqual(45f, IsoGround.BearingDegrees(groundDiagonal), 1e-3f,
                "equal ground metres north and east IS north-east");

            // …and the reverse: a 45° world step is a much more northerly walk than it looks.
            Assert.AreEqual(32.73f, IsoGround.BearingDegrees(new Vector2(1f, 1f)), 0.02f,
                "a world-XY diagonal is only 32.7° of ground bearing");
            Assert.AreNotEqual(45f, Mathf.Round(IsoGround.BearingDegrees(new Vector2(1f, 1f))),
                "if this ever reads 45 the un-squash has been removed");
        }

        [Test]
        public void Bearing_FromOnePointToAnother_IsTheDeltasBearing()
        {
            var from = new Vector2(-3f, 7f);
            var to = from + new Vector2(1f, IsoGround.GroundDepthScale);
            Assert.AreEqual(45f, IsoGround.BearingDegrees(from, to), 1e-3f,
                "the two-point overload is what anything STATING a facing should use");
        }

        // ---- the presenter's read -------------------------------------------------------------------

        [Test]
        public void GroundHeadingFor_HoldsTheFallbackBelowMinSpeed_AndMeasuresItInWORLDUnits()
        {
            const float held = 123f;
            Assert.AreEqual(held, IsoCharacterMath.GroundHeadingFor(Vector2.zero, 0.05f, held), 1e-4f,
                "standing still keeps the last facing");
            Assert.AreEqual(held, IsoCharacterMath.GroundHeadingFor(new Vector2(0f, 0.04f), 0.05f, held),
                1e-4f, "0.04 world units/s of northward jitter is still under a 0.05 threshold — the " +
                       "un-squash must not be applied before the test, or it would clear it at 0.062");
            Assert.AreEqual(0f, IsoCharacterMath.GroundHeadingFor(new Vector2(0f, 1f), 0.05f, held), 1e-3f,
                "a real step reads its bearing");
        }

        [Test]
        public void GroundHeadingFor_AndHeadingFor_AgreeOnlyOnTheCardinals()
        {
            foreach (float cardinal in new[] { 0f, 90f, 180f, 270f })
            {
                Vector2 v = WorldUnit(cardinal);
                Assert.AreEqual(IsoCharacterMath.HeadingFor(v, 0.01f, 999f),
                                IsoCharacterMath.GroundHeadingFor(v, 0.01f, 999f), 1e-3f,
                                $"{cardinal}° is its own ground bearing");
            }

            Vector2 off = WorldUnit(InsideTheFirstBand);
            Assert.AreEqual(9.13f,
                IsoCharacterMath.HeadingFor(off, 0.01f, 999f) -
                IsoCharacterMath.GroundHeadingFor(off, 0.01f, 999f), 0.05f,
                "off the cardinals the two readings genuinely differ");
        }

        // ---- the row, which is the thing the player actually sees -----------------------------------

        [Test]
        public void FacingRow_NoLongerShowsTheNeighbouringFacing_InTheBandsTheSquashUsedToMisread()
        {
            var def = ScriptableObject.CreateInstance<CharacterVisualDef>();
            try
            {
                def.FacingCount = 8;
                def.ZeroHeadingDegrees = 0f;
                def.FacingsAreCounterClockwise = false;   // rows as labelled; the corrected character bake

                foreach (var (worldBearing, expectedRow, wrongRow) in
                         new[] { (InsideTheFirstBand, 0, 1), (InsideTheSecondBand, 1, 2) })
                {
                    Vector2 v = WorldUnit(worldBearing);
                    float ground = IsoCharacterMath.GroundHeadingFor(v, 0.01f, 999f);
                    float naive = IsoCharacterMath.HeadingFor(v, 0.01f, 999f);

                    Assert.AreEqual(expectedRow, def.FacingRowFor(ground),
                        $"walking world-{worldBearing}° is a ground bearing of {ground:F1}°, " +
                        $"which row {expectedRow} depicts");
                    Assert.AreEqual(wrongRow, def.FacingRowFor(naive),
                        "…and the un-corrected read picks the NEIGHBOURING row — this half of the " +
                        "assertion is what stops the test passing vacuously if the bands move");
                }
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }

        // ---- the plane that is NOT squashed ---------------------------------------------------------

        [Test]
        public void TheDeckFrameIsNotSquashed_AndKeepsTheRawBearing()
        {
            // DeckWalkController's frame is metres of REAL deck on both axes (x abeam to starboard,
            // y along the keel), so un-squashing it would introduce the very error this work removes.
            Assert.AreEqual(45f, DeckRiderFacingMath.DeckBearing(new Vector2(1f, 1f), 0.01f, 999f), 1e-3f,
                "a rider walking equally forward and to starboard is looking 45° off the bow");
        }
    }
}
