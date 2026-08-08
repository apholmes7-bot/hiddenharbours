using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The CLIP axis of the character sheets — the third dimension beside gait and stance, and the one
    /// that had no runtime path at all until the pass-6.2 art drop commissioned one. A gait comes from
    /// measured speed and a stance from context; a clip is STARTED, runs on its own clock, and ends.
    ///
    /// <para>Three claims carry the weight. First, <b>the A/B</b>: a def with no clip art must answer
    /// exactly what it answered before clips existed, and every clip query must be safe on it — a
    /// character whose kit skipped the boarding bake keeps drawing whatever drew it. Second, <b>a clip
    /// does not have to bake eight facings</b>: the pass-6.2 kit does, but a ladder climb is often
    /// authored at one or two, and the row snap must resolve against the CLIP's own count or a 2-row
    /// sheet gets asked for row 5. Third, <b>scaled playback</b>: when a move's duration and a clip's
    /// natural duration disagree, the CLIP stretches — the boarding vault is owner-approved feel and
    /// retuning it to suit the art would be the tail wagging the dog.</para>
    /// </summary>
    public class CharacterClipTests
    {
        const int Directions = 8;

        static Sprite[] Sheet(int count)
        {
            var set = new Sprite[count];
            var tex = new Texture2D(4, 4);
            for (int i = 0; i < set.Length; i++)
                set[i] = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.1f), 32f);
            return set;
        }

        /// <summary>A def with the body wired and, optionally, the four clips the 6.2 kit bakes.</summary>
        static CharacterVisualDef NewDef(bool withClips)
        {
            var d = ScriptableObject.CreateInstance<CharacterVisualDef>();
            d.Id = "visual.clip_test";
            d.FacingCount = Directions;
            d.IdleFrameCount = 6; d.IdleSheet = Sheet(Directions * 6);
            d.WalkFrameCount = 8; d.WalkSheet = Sheet(Directions * 8);

            if (!withClips) return d;

            d.BoardClip = new CharacterClipSheets
            { FrameCount = 10, FramesPerSecond = 1000f / 90f, Loops = false, Sheet = Sheet(Directions * 10) };
            d.BoardDownClip = new CharacterClipSheets
            { FrameCount = 6, FramesPerSecond = 1000f / 95f, Loops = false, Sheet = Sheet(Directions * 6) };
            d.HaulClip = new CharacterClipSheets
            { FrameCount = 8, FramesPerSecond = 1000f / 120f, Loops = true, Sheet = Sheet(Directions * 8) };
            d.LadderDownClip = new CharacterClipSheets
            { FrameCount = 10, FramesPerSecond = 1000f / 110f, Loops = true, Sheet = Sheet(Directions * 10) };
            return d;
        }

        // ---- the A/B: a def with no clip art is exactly the def it always was ----------------------

        [Test]
        public void WithNoClipArt_EveryClipQueryIsSafe_AndNothingCanPlay()
        {
            var d = NewDef(withClips: false);

            foreach (CharacterClip clip in new[]
                     { CharacterClip.None, CharacterClip.Board, CharacterClip.BoardDown,
                       CharacterClip.Haul, CharacterClip.LadderDown })
            {
                Assert.IsFalse(d.HasClip(clip), $"{clip} must not read as wired on a bare def");
                Assert.IsNull(d.ClipSpriteFor(clip, 0, 0), $"{clip} must hand back no cell");
                Assert.GreaterOrEqual(d.ClipFrameCount(clip), 1, $"{clip} frame count is never 0");
                Assert.GreaterOrEqual(d.ClipFacingCount(clip), 1, $"{clip} facing count is never 0");
            }

            // The gait/stance answers are untouched — this is the axis that must not disturb the others.
            Assert.IsTrue(d.HasGait(CharacterGait.Idle));
            Assert.IsTrue(d.HasGait(CharacterGait.Walk));
            Assert.AreEqual(CharacterStance.Free, d.Playable(CharacterStance.Free, CharacterGait.Walk).Stance);
        }

        [Test]
        public void ClipNone_IsNeverWired_AndHasNoBlock()
        {
            var d = NewDef(withClips: true);
            Assert.IsNull(d.ClipSheetsFor(CharacterClip.None));
            Assert.IsFalse(d.HasClip(CharacterClip.None));
        }

        // ---- all-or-nothing, the same gate every other sheet goes through --------------------------

        [Test]
        public void AClipIsWired_OnlyWhenItsSheetIsComplete()
        {
            var d = NewDef(withClips: true);
            Assert.IsTrue(d.HasClip(CharacterClip.Board), "a full 8×10 sheet is wired");

            // One frame short — dropped WHOLE rather than half-played, or the clip would index a stale
            // cell partway through the vault.
            d.BoardClip.Sheet = Sheet(Directions * 10 - 1);
            Assert.IsFalse(d.HasClip(CharacterClip.Board));

            // Right length, one hole in it — same answer, for the same reason.
            d.BoardClip.Sheet = Sheet(Directions * 10);
            d.BoardClip.Sheet[37] = null;
            Assert.IsFalse(d.HasClip(CharacterClip.Board));
        }

        [Test]
        public void TheRigsOwnLoopFlags_ReachTheDef()
        {
            var d = NewDef(withClips: true);
            // ANIMS declares board/boardDown oneShot; haul and ladderDown are cycles.
            Assert.IsFalse(d.ClipLoops(CharacterClip.Board));
            Assert.IsFalse(d.ClipLoops(CharacterClip.BoardDown));
            Assert.IsTrue(d.ClipLoops(CharacterClip.Haul));
            Assert.IsTrue(d.ClipLoops(CharacterClip.LadderDown));
        }

        // ---- the "must not assume 8 facings" claim -------------------------------------------------

        [Test]
        public void AClipInheritsTheDefsFacingCount_WhenItDeclaresNone()
        {
            var d = NewDef(withClips: true);
            Assert.AreEqual(0, d.LadderDownClip.FacingCount, "the 6.2 kit bakes all eight, so 0 = inherit");
            Assert.AreEqual(Directions, d.ClipFacingCount(CharacterClip.LadderDown));

            // Inheriting means the clip row and the body row agree, heading for heading.
            for (float h = -720f; h <= 720f; h += 17f)
                Assert.AreEqual(d.FacingRowFor(h), d.ClipFacingRowFor(CharacterClip.LadderDown, h),
                                $"clip row and body row disagreed at heading {h}");
        }

        [Test]
        public void AReducedFacingClip_SnapsAgainstItsOwnRowCount_AndNeverIndexesPastItsSheet()
        {
            // The case the presenter must not assume away: a kit that authors the ladder climb at TWO
            // facings (a climber faces the ladder, so the other six rows are wasted texture). Nothing in
            // code changes — the clip block says 2 and every clip query re-solves against it.
            var d = NewDef(withClips: true);
            d.LadderDownClip.FacingCount = 2;
            d.LadderDownClip.FrameCount = 10;
            d.LadderDownClip.Sheet = Sheet(2 * 10);

            Assert.AreEqual(2, d.ClipFacingCount(CharacterClip.LadderDown));
            Assert.IsTrue(d.HasClip(CharacterClip.LadderDown), "2×10 is COMPLETE for a 2-facing clip");
            Assert.AreEqual(Directions, d.FacingCount, "the BODY is still eight — only the clip is reduced");

            // Every heading on the compass lands inside the two rows the sheet actually has, and every
            // resulting cell exists.
            for (float h = -720f; h <= 720f; h += 11f)
            {
                int row = d.ClipFacingRowFor(CharacterClip.LadderDown, h);
                Assert.That(row, Is.InRange(0, 1), $"heading {h} snapped to row {row} of a 2-row sheet");
                for (int f = 0; f < 10; f++)
                    Assert.IsNotNull(d.ClipSpriteFor(CharacterClip.LadderDown, row, f));
            }
        }

        [Test]
        public void ClipRowsAndFrames_Wrap_AndAreNegativeSafe()
        {
            var d = NewDef(withClips: true);
            // A clip started on one facing and re-aimed mid-play must never index out of range.
            Assert.AreSame(d.ClipSpriteFor(CharacterClip.Haul, 0, 0),
                           d.ClipSpriteFor(CharacterClip.Haul, Directions, 8));
            Assert.IsNotNull(d.ClipSpriteFor(CharacterClip.Haul, -1, -1));
            Assert.IsNotNull(d.ClipSpriteFor(CharacterClip.Haul, -9, -17));
        }

        // ---- the frame maths: natural rate, scaled playback, loop vs one-shot ----------------------

        [Test]
        public void AtItsNaturalRate_AClipRunsAtItsBakedMilliseconds()
        {
            // haul: 8 frames × 120 ms = 0.96 s per cycle.
            const int frames = 8;
            const float fps = 1000f / 120f;
            Assert.AreEqual(0.96f, CharacterClipMath.NaturalDurationSeconds(frames, fps), 1e-3f);

            Assert.AreEqual(0, CharacterClipMath.FrameFor(0f, 0f, frames, fps, loops: true));
            Assert.AreEqual(1, CharacterClipMath.FrameFor(0.13f, 0f, frames, fps, loops: true));
            Assert.AreEqual(7, CharacterClipMath.FrameFor(0.95f, 0f, frames, fps, loops: true));
            Assert.AreEqual(0, CharacterClipMath.FrameFor(0.97f, 0f, frames, fps, loops: true),
                            "a looping clip wraps at its own duration");
        }

        [Test]
        public void ScaledToAMove_TheWholeClipSpansTheMovesDuration_NotItsOwn()
        {
            // THE RULE: board is 10 f × 90 ms = 0.9 s, the vault is 0.55 s of owner-approved feel. The
            // clip is compressed into the move — the move is never stretched into the clip.
            const int frames = 10;
            const float natural = 1000f / 90f;
            const float vault = 0.55f;

            Assert.AreEqual(0, CharacterClipMath.FrameFor(0f, vault, frames, natural, loops: false));
            Assert.AreEqual(0, CharacterClipMath.FrameFor(vault * 0.05f, vault, frames, natural, false));
            Assert.AreEqual(5, CharacterClipMath.FrameFor(vault * 0.5f, vault, frames, natural, false),
                            "half way through the vault is half way through the clip");
            Assert.AreEqual(9, CharacterClipMath.FrameFor(vault * 0.99f, vault, frames, natural, false));

            // At the same elapsed time the UNSCALED clip is well behind — which is exactly the drift the
            // scaling exists to remove.
            Assert.AreEqual(3, CharacterClipMath.FrameFor(vault * 0.5f, 0f, frames, natural, false));
        }

        [Test]
        public void AOneShotHoldsItsLastFrame_AndALoopWraps()
        {
            const int frames = 10;
            const float vault = 0.55f;

            // Past the end, a one-shot CLAMPS. A board clip that wrapped would put the fisher back on
            // the wharf for a frame at the top of her own vault.
            Assert.AreEqual(9, CharacterClipMath.FrameFor(vault, vault, frames, 0f, loops: false));
            Assert.AreEqual(9, CharacterClipMath.FrameFor(vault * 10f, vault, frames, 0f, loops: false));

            // A cycle keeps going.
            Assert.AreEqual(0, CharacterClipMath.FrameFor(vault, vault, frames, 0f, loops: true));
            Assert.AreEqual(5, CharacterClipMath.FrameFor(vault * 2.5f, vault, frames, 0f, loops: true));
        }

        [Test]
        public void IsFinished_IsTrueOnlyForAOneShotThatHasPlayedOut()
        {
            const int frames = 10;
            const float natural = 1000f / 90f;   // 0.9 s
            const float vault = 0.55f;

            Assert.IsFalse(CharacterClipMath.IsFinished(0.3f, vault, frames, natural, loops: false));
            Assert.IsTrue(CharacterClipMath.IsFinished(vault, vault, frames, natural, loops: false));
            Assert.IsTrue(CharacterClipMath.IsFinished(0.95f, 0f, frames, natural, loops: false),
                          "unscaled, it ends at its own 0.9 s");

            // A cycle is never finished — the thing driving it decides when to stop.
            Assert.IsFalse(CharacterClipMath.IsFinished(999f, vault, frames, natural, loops: true));
        }

        [Test]
        public void TheFrameMathsIsTotal_OnEveryDegenerateInput()
        {
            // No input may throw or answer out of range: a zero frame count, a zero rate, a negative or
            // NaN clock. Every one of these is reachable from a half-authored asset.
            Assert.AreEqual(0, CharacterClipMath.FrameFor(1f, 0f, 0, 10f, loops: true));
            Assert.AreEqual(0, CharacterClipMath.FrameFor(1f, 0f, 8, 0f, loops: true));
            Assert.AreEqual(0, CharacterClipMath.FrameFor(-5f, 0.5f, 8, 10f, loops: true));
            Assert.AreEqual(0, CharacterClipMath.FrameFor(float.NaN, 0.5f, 8, 10f, loops: false));
            Assert.AreEqual(0f, CharacterClipMath.NaturalDurationSeconds(0, 10f));
            Assert.AreEqual(0f, CharacterClipMath.NaturalDurationSeconds(8, 0f));
            Assert.IsTrue(CharacterClipMath.IsFinished(0f, 0f, 8, 0f, loops: false),
                          "a clip with no playable duration is done on arrival rather than stuck");
        }

        [Test]
        public void TheDefsClipRates_AreTheRigsMilliseconds()
        {
            // The counts and rates ARE characterIsoRig6.js rev 6.2's ANIMS table. A drift here is a
            // sheet playing at the wrong speed, which nothing else in the suite would notice.
            var d = NewDef(withClips: true);
            Assert.AreEqual(10, d.ClipFrameCount(CharacterClip.Board));
            Assert.AreEqual(1000f / 90f, d.ClipFramesPerSecond(CharacterClip.Board), 1e-3f);
            Assert.AreEqual(6, d.ClipFrameCount(CharacterClip.BoardDown));
            Assert.AreEqual(1000f / 95f, d.ClipFramesPerSecond(CharacterClip.BoardDown), 1e-3f);
            Assert.AreEqual(8, d.ClipFrameCount(CharacterClip.Haul));
            Assert.AreEqual(1000f / 120f, d.ClipFramesPerSecond(CharacterClip.Haul), 1e-3f);
            Assert.AreEqual(10, d.ClipFrameCount(CharacterClip.LadderDown));
            Assert.AreEqual(1000f / 110f, d.ClipFramesPerSecond(CharacterClip.LadderDown), 1e-3f);
        }
    }
}
