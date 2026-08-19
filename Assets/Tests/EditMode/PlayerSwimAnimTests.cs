using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The swimmer's two decisions, both pure: WHICH pose the water state and speed call for, and WHERE
    /// the sea cuts a body that is floating rather than standing.
    ///
    /// <para><b>The second one is the subtle half.</b> Every waterline the game drew before this was a
    /// function of DEPTH, which is right for feet on the bottom and wrong the moment the body floats: a
    /// swimmer is cut at the same place in two metres of water and in twenty. Getting it wrong does not
    /// crash or look obviously broken — the swimmer just sinks as the tide makes — so it is asserted
    /// here against the rig's own measured lanes rather than eyeballed.</para>
    /// </summary>
    public class PlayerSwimAnimTests
    {
        // ---- which pose ---------------------------------------------------------------------------

        [Test]
        public void OutsideTheSwimBand_ThereIsNoPose_SoTheWalkSpriteKeepsTheRenderer()
        {
            // Wading is walking. The three-band model draws its line here and this must not blur it:
            // a fisher in the wade band has their feet on the bottom whatever the speed.
            foreach (float speed in new[] { 0f, 0.2f, 1.5f, 5f })
            {
                Assert.AreEqual(SwimPose.None, PlayerSwimAnimMath.PoseFor(OnFootWaterState.Dry, speed, 0.05f),
                                $"dry at {speed} m/s must not pose");
                Assert.AreEqual(SwimPose.None, PlayerSwimAnimMath.PoseFor(OnFootWaterState.Wade, speed, 0.05f),
                                $"wading at {speed} m/s must keep walking");
            }
        }

        [Test]
        public void InTheSwimBand_MakingWayStrokes_AndHoldingStationTreads()
        {
            Assert.AreEqual(SwimPose.Swim, PlayerSwimAnimMath.PoseFor(OnFootWaterState.Swim, 0.4f, 0.05f));
            Assert.AreEqual(SwimPose.Tread, PlayerSwimAnimMath.PoseFor(OnFootWaterState.Swim, 0f, 0.05f));
        }

        [Test]
        public void TheWholeSwimSpeedRange_Strokes_NotJustTheFastHalf()
        {
            // ⚠️ The regression this exists for. The rig's own note puts swimming at 0.18–0.60 m/s, and
            // CharacterVisualDef.WalkSpeedThreshold is 0.35 — INSIDE that range. Reusing the gait's
            // threshold (the obvious shortcut) would tread-water a fisher moving at 0.2 m/s, i.e. for the
            // slower half of every swim, while they were visibly crossing the bay.
            const float shipped = 0.05f;   // PlayerSwimAnimator's default
            foreach (float speed in new[] { 0.18f, 0.25f, 0.34f, 0.35f, 0.45f, 0.60f })
                Assert.AreEqual(SwimPose.Swim, PlayerSwimAnimMath.PoseFor(OnFootWaterState.Swim, speed, shipped),
                                $"{speed} m/s is inside the swim band's own range and must STROKE");

            Assert.Less(shipped, 0.18f, "the threshold must sit under the whole swim range");
        }

        [Test]
        public void TheThresholdIsExclusive_AndGarbageTreadsRatherThanStroking()
        {
            // At rest and exactly AT the threshold both read as holding station — a swimmer who is not
            // measurably making way treads. NaN treads too: an unknown speed must not start a stroke.
            Assert.AreEqual(SwimPose.Tread, PlayerSwimAnimMath.PoseFor(OnFootWaterState.Swim, 0.05f, 0.05f));
            Assert.AreEqual(SwimPose.Swim, PlayerSwimAnimMath.PoseFor(OnFootWaterState.Swim, 0.0501f, 0.05f));
            Assert.AreEqual(SwimPose.Tread, PlayerSwimAnimMath.PoseFor(OnFootWaterState.Swim, float.NaN, 0.05f));
            Assert.AreEqual(SwimPose.Tread, PlayerSwimAnimMath.PoseFor(OnFootWaterState.Swim, -1f, 0.05f));

            // A negative threshold cannot turn every stillness into a stroke.
            Assert.AreEqual(SwimPose.Tread, PlayerSwimAnimMath.PoseFor(OnFootWaterState.Swim, 0f, -5f));
        }

        [Test]
        public void EachPoseNamesItsOwnClip_AndNoPoseNamesNone()
        {
            Assert.AreEqual(CharacterClip.Swim, PlayerSwimAnimMath.ClipFor(SwimPose.Swim));
            Assert.AreEqual(CharacterClip.Tread, PlayerSwimAnimMath.ClipFor(SwimPose.Tread));
            Assert.AreEqual(CharacterClip.None, PlayerSwimAnimMath.ClipFor(SwimPose.None));
        }

        // ---- where the sea cuts a floating body ----------------------------------------------------

        [Test]
        public void OnlyTheTwoWaterPoses_FloatTheirWaterline()
        {
            Assert.IsTrue(PlayerSubmergeMath.IsFloatingClip(CharacterClip.Swim));
            Assert.IsTrue(PlayerSubmergeMath.IsFloatingClip(CharacterClip.Tread));

            // Sleep is on a mattress and drive is in a seat — neither is in the water at all, and giving
            // them a waterline would clip a sleeping fisher off at the chest.
            Assert.IsFalse(PlayerSubmergeMath.IsFloatingClip(CharacterClip.Sleep));
            Assert.IsFalse(PlayerSubmergeMath.IsFloatingClip(CharacterClip.Drive));
            Assert.IsFalse(PlayerSubmergeMath.IsFloatingClip(CharacterClip.Haul));
            Assert.IsFalse(PlayerSubmergeMath.IsFloatingClip(CharacterClip.None));
        }

        [Test]
        public void TheLaneRowFlipsIntoAUvFractionFromTheCellBOTTOM()
        {
            // The sidecar counts lanes DOWN from the cell top; the shader compares against raw uv.y, which
            // runs UP from the cell bottom. The flip is the whole function, and getting it backwards puts
            // a swimmer's waterline at their ankles.
            const float cell = 88f;

            // The fisher's own measured lanes (OffDeck_mounts.json).
            Assert.AreEqual((88f - 69f) / 88f, PlayerSubmergeMath.FloatingWaterlineFraction(69, cell), 1e-5f,
                            "fisher SWIM: lane 69 of 88");
            Assert.AreEqual((88f - 55f) / 88f, PlayerSubmergeMath.FloatingWaterlineFraction(55, cell), 1e-5f,
                            "fisher TREAD: lane 55 of 88");
        }

        [Test]
        public void TreadingCutsHigherUpTheDrawnBodyThanSwimming()
        {
            // The same invariant the mount table is asserted on, carried through to the number the shader
            // actually receives: standing up in the water puts the line further up the cell.
            float swim = PlayerSubmergeMath.FloatingWaterlineFraction(69, 88f);
            float tread = PlayerSubmergeMath.FloatingWaterlineFraction(55, 88f);
            Assert.Greater(tread, swim, "treading must clip more of the body than swimming");
        }

        [Test]
        public void AMalformedLane_ReadsDryRatherThanPlausiblyWrong()
        {
            // 0 is a pixel-identical passthrough — an unclipped swimmer. That is a visible "no data" and
            // is far safer than a plausible line, which would silently clip the wrong part of the body.
            Assert.AreEqual(0f, PlayerSubmergeMath.FloatingWaterlineFraction(0, 88f), 1e-6f, "lane 0");
            Assert.AreEqual(0f, PlayerSubmergeMath.FloatingWaterlineFraction(-5, 88f), 1e-6f, "negative lane");
            Assert.AreEqual(0f, PlayerSubmergeMath.FloatingWaterlineFraction(88, 88f), 1e-6f, "lane at the cell floor");
            Assert.AreEqual(0f, PlayerSubmergeMath.FloatingWaterlineFraction(200, 88f), 1e-6f, "lane past the cell");
            Assert.AreEqual(0f, PlayerSubmergeMath.FloatingWaterlineFraction(69, 0f), 1e-6f, "no cell height");
            Assert.AreEqual(0f, PlayerSubmergeMath.FloatingWaterlineFraction(69, float.NaN), 1e-6f, "NaN cell");
        }

        [Test]
        public void TheLaneIsReadAgainstTheCellItWasMeasuredIn()
        {
            // ⚠️ The off-deck sheets are 88 px where the locomotion sheets are 92. Reading a lane against
            // the wrong cell is a real, silent error — this pins the size of it so the two cannot be
            // treated as interchangeable.
            float right = PlayerSubmergeMath.FloatingWaterlineFraction(69, 88f);
            float wrong = PlayerSubmergeMath.FloatingWaterlineFraction(69, 92f);
            Assert.AreNotEqual(right, wrong,
                               "the 88 and 92 cells must not give the same answer for one lane row");
            Assert.Greater(Mathf.Abs(right - wrong) * 88f, 1f,
                           "reading the lane against the 92 cell moves the waterline by over a pixel");
        }

        // ---- the shipped mount table, held against the drawn fraction ------------------------------

        [Test]
        public void EveryShippedBuild_FloatsWithItsHeadAboveWater()
        {
            // A floating waterline is not clamped by the neck-deep cap the wade path uses, so nothing
            // else would catch a mount row that drowned a preset. Run the real table through the real
            // function and require every line to leave the head clear.
            var mounts = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterOffDeckMountsDef>(
                "Assets/_Project/Data/Characters/CharacterOffDeckMounts.asset");
            Assert.IsNotNull(mounts, "the off-deck mount table must be committed");

            foreach (var row in mounts.Rows)
            foreach (var (name, lane) in new[] { ("swim", row.Swim.WaterRowLane), ("tread", row.Tread.WaterRowLane) })
            {
                float frac = PlayerSubmergeMath.FloatingWaterlineFraction(lane, 88f);
                Assert.Greater(frac, 0f, $"{row.VisualId} {name}: lane {lane} gave a DRY passthrough");

                // The crown of the tallest build sits near uv.y 0.533 on the 92 cell (PersistentCoreBuilder's
                // measured probe); on the 88 cell that is ~0.558. A line at or above it is a drowned fisher.
                Assert.Less(frac, 0.55f,
                            $"{row.VisualId} {name}: waterline at {frac:0.###} of the cell is at or over " +
                            "the crown — the head must stay above water");
            }
        }
    }
}
