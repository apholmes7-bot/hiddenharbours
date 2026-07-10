using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The deck hauler's ANIMATION mapping (the owner's PlayerHaul sheet): haul snapshot → pose → frame.
    /// The pure maths (<see cref="PlayerHaulAnimMath"/>) is pinned directly — line gaining plays the
    /// hand-over-hand cycle keyed to line01 (the cycle breathes with the take rate for free), line slipping
    /// shows STRAIN, a held line shows EASE, any non-hauling phase hands the renderer back — and the
    /// component is driven through its public bus handler (the TrapDeckGating convention, no play-mode
    /// lifecycle): it faces the worked buoy via flipX and restores the walk sprite exactly on haul end.
    /// </summary>
    public class PlayerHaulAnimatorTests
    {
        private const float Eps = 1e-5f;

        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- the pure pose mapping --------------------------------------------------------------

        [Test]
        public void PoseFor_NonHaulingPhases_AreNone()
        {
            Assert.AreEqual(HaulPose.None, PlayerHaulAnimMath.PoseFor(TrapHaulPhase.Idle, 0.1f, Eps));
            Assert.AreEqual(HaulPose.None, PlayerHaulAnimMath.PoseFor(TrapHaulPhase.Surfaced, 0.1f, Eps));
            Assert.AreEqual(HaulPose.None, PlayerHaulAnimMath.PoseFor(TrapHaulPhase.Empty, -0.1f, Eps));
        }

        [Test]
        public void PoseFor_LineMotion_MapsToPullStrainEase()
        {
            Assert.AreEqual(HaulPose.Pull, PlayerHaulAnimMath.PoseFor(TrapHaulPhase.Hauling, 0.02f, Eps),
                "line coming IN → the hand-over-hand pull cycle");
            Assert.AreEqual(HaulPose.Strain, PlayerHaulAnimMath.PoseFor(TrapHaulPhase.Hauling, -0.02f, Eps),
                "line SLIPPING back (fighting a drop) → the strain frame");
            Assert.AreEqual(HaulPose.Ease, PlayerHaulAnimMath.PoseFor(TrapHaulPhase.Hauling, 0f, Eps),
                "line holding still (the pawl has it) → the ease frame");
            Assert.AreEqual(HaulPose.Ease, PlayerHaulAnimMath.PoseFor(TrapHaulPhase.Hauling, Eps * 0.5f, Eps),
                "a sub-epsilon wiggle reads as holding still, not a pull");
        }

        // ---- the cycle keyed to the line hauled ---------------------------------------------------

        [Test]
        public void CycleFrame_AdvancesWithTheLine_AndWraps()
        {
            // 24 frames per full haul, a 6-frame cycle: each 1/24 of line is one frame; 6/24 wraps to 0.
            Assert.AreEqual(0, PlayerHaulAnimMath.CycleFrame(0f, 24f, 6));
            Assert.AreEqual(1, PlayerHaulAnimMath.CycleFrame(1.2f / 24f, 24f, 6));
            Assert.AreEqual(5, PlayerHaulAnimMath.CycleFrame(5.5f / 24f, 24f, 6));
            Assert.AreEqual(0, PlayerHaulAnimMath.CycleFrame(6.0f / 24f, 24f, 6), "the cycle wraps hand-over-hand");
            Assert.AreEqual(2, PlayerHaulAnimMath.CycleFrame(14.9f / 24f, 24f, 6));
        }

        [Test]
        public void CycleFrame_BreathesWithTheTakeRate()
        {
            // The SAME line gained always advances the same number of frames — so a fast take (big lift)
            // runs the hands fast and the calm wind-in strolls them, with no timer to drift.
            int before = PlayerHaulAnimMath.CycleFrame(0.30f, 24f, 6);
            int afterSmallTake = PlayerHaulAnimMath.CycleFrame(0.30f + 1f / 24f, 24f, 6);
            Assert.AreEqual((before + 1) % 6, afterSmallTake, "1/24 of line = exactly one frame of hands");
        }

        [Test]
        public void CycleFrame_DegenerateInputs_AreSafe()
        {
            Assert.AreEqual(0, PlayerHaulAnimMath.CycleFrame(-0.5f, 24f, 6), "negative line clamps");
            Assert.AreEqual(0, PlayerHaulAnimMath.CycleFrame(0.5f, 24f, 0), "no cycle frames → 0");
            Assert.AreEqual(0, PlayerHaulAnimMath.CycleFrame(0.5f, 0f, 6), "0 frames-per-line → the first frame");
        }

        // ---- facing the buoy ----------------------------------------------------------------------

        [Test]
        public void FlipXFor_PointsTheRopeSideAtTheBuoy()
        {
            // The owner's art pulls at the sprite's LEFT: a buoy to the RIGHT mirrors the sprite.
            Assert.IsTrue(PlayerHaulAnimMath.FlipXFor(+3f, artRopeSideIsLeft: true), "buoy right → flip");
            Assert.IsFalse(PlayerHaulAnimMath.FlipXFor(-3f, artRopeSideIsLeft: true), "buoy left → drawn side");
            Assert.IsFalse(PlayerHaulAnimMath.FlipXFor(0f, artRopeSideIsLeft: true), "dead-on keeps the drawn art");
            Assert.IsFalse(PlayerHaulAnimMath.FlipXFor(+3f, artRopeSideIsLeft: false), "right-drawn art inverts the rule");
            Assert.IsTrue(PlayerHaulAnimMath.FlipXFor(-3f, artRopeSideIsLeft: false));
        }

        // ---- the component, driven through the bus handler ----------------------------------------

        private (PlayerHaulAnimator anim, SpriteRenderer sr, Sprite[] frames, Sprite walk) MakeAnimator()
        {
            var go = new GameObject("Player");
            _spawned.Add(go);
            var sr = go.AddComponent<SpriteRenderer>();
            var frames = new Sprite[8];
            for (int i = 0; i < frames.Length; i++) frames[i] = MakeSprite();
            Sprite walk = MakeSprite();
            sr.sprite = walk;                       // the walk sprite owns the renderer before the haul
            sr.flipX = false;
            var anim = go.AddComponent<PlayerHaulAnimator>();
            anim.Configure(frames);
            return (anim, sr, frames, walk);
        }

        private Sprite MakeSprite()
        {
            var tex = new Texture2D(4, 8);
            _spawned.Add(tex);
            var s = Sprite.Create(tex, new Rect(0, 0, 4, 8), new Vector2(0.5f, 0f), 32f);
            _spawned.Add(s);
            return s;
        }

        private static TrapHaulStateChanged Snap(TrapHaulPhase phase, float line01, float buoyX = 0f, float buoyY = 0f)
            => new TrapHaulStateChanged(new TrapHaulState(phase, 0.5f, line01, false, buoyX, buoyY));

        [Test]
        public void HaulSnapshots_DriveCycleStrainEase_AndFaceTheBuoy()
        {
            var (anim, sr, frames, _) = MakeAnimator();

            // First live snapshot (line 0): nothing has moved yet → EASE, facing the buoy off to the right.
            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Hauling, 0f, buoyX: 5f));
            Assert.AreEqual(HaulPose.Ease, anim.Pose);
            Assert.AreEqual(frames[7], sr.sprite, "the pawl holds → the EASE frame (7)");
            Assert.IsTrue(sr.flipX, "buoy to the right → the rope side mirrors toward it");

            // Line coming in → the pull cycle, keyed to line01 (0.10 × 24 frames/line → frame 2).
            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Hauling, 0.10f, buoyX: 5f));
            Assert.AreEqual(HaulPose.Pull, anim.Pose);
            Assert.AreEqual(frames[2], sr.sprite, "line gaining → the hand-over-hand cycle frame for this line");

            // Line slipping back (held through the drop) → the STRAIN frame.
            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Hauling, 0.06f, buoyX: 5f));
            Assert.AreEqual(HaulPose.Strain, anim.Pose);
            Assert.AreEqual(frames[6], sr.sprite, "the rope fights back → the STRAIN frame (6)");
        }

        [Test]
        public void HaulEnd_RestoresTheWalkSprite_Exactly()
        {
            var (anim, sr, frames, walk) = MakeAnimator();

            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Hauling, 0f, buoyX: 5f));
            Assert.AreNotEqual(walk, sr.sprite, "the haul took the renderer over");
            Assert.IsTrue(sr.flipX);

            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Idle, 0f));
            Assert.AreEqual(HaulPose.None, anim.Pose);
            Assert.AreEqual(walk, sr.sprite, "haul over → the walk sprite is handed back");
            Assert.IsFalse(sr.flipX, "…with its original facing");
        }

        [Test]
        public void SurfacedAndEmpty_AlsoHandTheRendererBack()
        {
            var (anim, sr, _, walk) = MakeAnimator();

            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Hauling, 0.2f, buoyX: -4f));
            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Surfaced, 1f));
            Assert.AreEqual(walk, sr.sprite, "surfacing ends the animation");

            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Hauling, 0.1f, buoyX: -4f));
            Assert.IsFalse(sr.flipX, "buoy to the LEFT → the drawn (left) rope side, no mirror");
            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Empty, 1f));
            Assert.AreEqual(walk, sr.sprite, "an empty pot ends the animation too");
        }

        [Test]
        public void NoFramesWired_TheAnimatorIsInert()
        {
            var go = new GameObject("Player");
            _spawned.Add(go);
            var sr = go.AddComponent<SpriteRenderer>();
            var anim = go.AddComponent<PlayerHaulAnimator>();   // no Configure — no art imported

            Assert.DoesNotThrow(() => anim.OnHaulStateChanged(Snap(TrapHaulPhase.Hauling, 0.3f, buoyX: 2f)));
            Assert.IsNull(sr.sprite, "without art the renderer is untouched (null-safe greybox rule)");
        }
    }
}
