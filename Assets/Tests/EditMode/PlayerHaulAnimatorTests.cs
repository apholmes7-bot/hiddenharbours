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

        // ---- the rig's haul CLIP (pass 6.2) --------------------------------------------------------
        //
        // The kit says `haul` replaces the flat PlayerHaul sheet, and it is the better picture: eight
        // DRAWN facings instead of one mirrored pose. What must NOT change is when each pose shows — the
        // snapshot → pose read is the deck haul's behaviour and it is untouched — and that the cycle
        // stays keyed to the LINE rather than to a clock.

        private const int ClipFrames = 8;      // characterIsoRig6.js rev 6.2: haul is 8 f × 120 ms, loops
        private const int MeasuredStrainFrame = 5;   // tension peaks at 0.90 there (sidecar clipPins)
        private const int MeasuredEaseFrame = 0;     // …and is 0 there, the slack reach

        private CharacterVisualDef ClipSkin()
        {
            var def = ScriptableObject.CreateInstance<CharacterVisualDef>();
            _spawned.Add(def);
            def.Id = "visual.haul_clip_test";
            def.FacingCount = 8;

            var sheet = new Sprite[8 * ClipFrames];
            for (int i = 0; i < sheet.Length; i++) sheet[i] = MakeSprite();
            def.HaulClip = new CharacterClipSheets
            { FrameCount = ClipFrames, FramesPerSecond = 1000f / 120f, Loops = true, Sheet = sheet };
            return def;
        }

        /// <summary>The fisher with the clip seam beside her — and, unless <paramref name="withClipArt"/>
        /// is false, a skin that has the heave baked. The flat sheet is wired either way, because the
        /// point of the fallback is that it is still there.</summary>
        private (PlayerHaulAnimator anim, SpriteRenderer sr, CharacterClipPlayer clip,
                 CharacterVisualDef def, Sprite[] frames, Sprite walk) MakeClipAnimator(bool withClipArt = true)
        {
            var go = new GameObject("Player");
            _spawned.Add(go);
            var sr = go.AddComponent<SpriteRenderer>();
            var clip = go.AddComponent<CharacterClipPlayer>();
            CharacterVisualDef def = withClipArt ? ClipSkin() : null;
            if (def != null) clip.Configure(def);

            var frames = new Sprite[8];
            for (int i = 0; i < frames.Length; i++) frames[i] = MakeSprite();
            Sprite walk = MakeSprite();
            sr.sprite = walk;
            sr.flipX = false;

            var anim = go.AddComponent<PlayerHaulAnimator>();
            anim.Configure(frames);
            return (anim, sr, clip, def, frames, walk);
        }

        // ---- the rate of work survives a differently-baked sheet -----------------------------------

        [Test]
        public void TheTunedQuantityIsCyclesPerHaul_NotFramesPerLine()
        {
            // What the owner tuned is a RATE OF WORK: 24 frames per line over a 6-frame cycle is four
            // heaves a pot. The clip is EIGHT frames, so the rate carries across and the frame count
            // comes from the art…
            Assert.AreEqual(4f, PlayerHaulAnimMath.CyclesPerHaul(24f, 6), 1e-5f);
            Assert.AreEqual(32f, PlayerHaulAnimMath.HeavePosition(1f, 4f, ClipFrames), 1e-4f,
                "four heaves over an 8-frame clip is 32 frames of line, not 24");

            // …and this is the trap it exists to stop. Handing the 8-frame clip the raw 24 would be the
            // same rope hauled by a visibly slower fisher — three heaves a pot instead of four — and
            // nothing else in the suite would notice.
            Assert.AreEqual(3f, PlayerHaulAnimMath.CyclesPerHaul(24f, ClipFrames), 1e-5f);
        }

        [Test]
        public void TheHeavePosition_BreathesWithTheTakeRate_AndIsSafeOnRubbish()
        {
            // The same line gained is always the same fraction of a heave, whatever the clip's length —
            // so a fast take runs the hands fast, with no timer to drift off the rope.
            Assert.AreEqual(0f, PlayerHaulAnimMath.HeavePosition(0f, 4f, ClipFrames), 1e-4f);
            Assert.AreEqual(16f, PlayerHaulAnimMath.HeavePosition(0.5f, 4f, ClipFrames), 1e-4f);
            Assert.AreEqual(8f, PlayerHaulAnimMath.HeavePosition(0.5f, 4f, 4), 1e-4f);

            Assert.AreEqual(0f, PlayerHaulAnimMath.HeavePosition(-1f, 4f, ClipFrames), 1e-4f, "negative line");
            Assert.AreEqual(0f, PlayerHaulAnimMath.HeavePosition(0.5f, -4f, ClipFrames), 1e-4f, "negative rate");
            Assert.AreEqual(0f, PlayerHaulAnimMath.HeavePosition(0.5f, 4f, 0), 1e-4f, "a clip with no frames");
            Assert.AreEqual(0f, PlayerHaulAnimMath.CyclesPerHaul(24f, 0), 1e-5f, "a cycle with no frames");
            Assert.AreEqual(0f, PlayerHaulAnimMath.CyclesPerHaul(-5f, 6), 1e-5f, "a negative rate");
        }

        // ---- the swap itself -----------------------------------------------------------------------

        [Test]
        public void WithTheClipBaked_TheHeaveIsTheRigsClip_KeyedToTheLine()
        {
            var (anim, sr, clip, def, frames, _) = MakeClipAnimator();

            // Line 0, nothing moved yet → EASE, which the clip has no pose for: hold its slackest frame.
            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Hauling, 0f, buoyX: 5f));
            Assert.AreEqual(HaulPose.Ease, anim.Pose);
            Assert.AreEqual(CharacterClip.Haul, clip.Clip, "the rig's heave took the renderer, not the sheet");
            Assert.IsTrue(clip.IsDriven, "…and it is paced by the line, not by a clock");
            Assert.AreEqual(MeasuredEaseFrame, clip.Frame, "the pawl holds → the slack frame of the heave");

            // Line coming in → the cycle, keyed to line01. 0.10 × 4 heaves × 8 frames = 3.2 → frame 3.
            // The SAME snapshot showed frame 2 on the 6-frame sheet: the rate of work is what carried
            // across, not the frame index.
            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Hauling, 0.10f, buoyX: 5f));
            Assert.AreEqual(HaulPose.Pull, anim.Pose);
            Assert.AreEqual(3, clip.Frame, "the heave is keyed to the line at four cycles a pot");
            Assert.AreEqual(2, PlayerHaulAnimMath.CycleFrame(0.10f, 24f, 6),
                            "…where the legacy 6-frame sheet showed frame 2 for the same line");

            // Line slipping back → STRAIN, held on the rig's most-loaded frame.
            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Hauling, 0.06f, buoyX: 5f));
            Assert.AreEqual(HaulPose.Strain, anim.Pose);
            Assert.AreEqual(MeasuredStrainFrame, clip.Frame, "the rope fights → the loaded frame of the heave");

            // Whatever it drew, it drew from the CLIP and never from the flat sheet.
            CollectionAssert.DoesNotContain(frames, sr.sprite, "a legacy sheet frame reached the renderer");
            Assert.AreEqual(def.ClipSpriteFor(CharacterClip.Haul, clip.FacingRow, clip.Frame), sr.sprite);
        }

        [Test]
        public void TheClipIsAimedAtTheBuoy_NotMirroredTowardIt()
        {
            var (anim, sr, clip, def, _, _) = MakeClipAnimator();

            // A pot due EAST of the fisher. The clip draws all eight facings, so she turns to it.
            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Hauling, 0.2f, buoyX: 6f, buoyY: 0f));
            Assert.AreEqual(def.ClipFacingRowFor(CharacterClip.Haul, 90f), clip.FacingRow,
                            "the heave faces the pot she is working");
            Assert.IsFalse(sr.flipX, "an 8-facing clip is aimed, never mirrored");

            // …and a pot due NORTH is a different row, not the same row flipped.
            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Hauling, 0.3f, buoyX: 0f, buoyY: 6f));
            Assert.AreEqual(def.ClipFacingRowFor(CharacterClip.Haul, 0f), clip.FacingRow);
            Assert.IsFalse(sr.flipX);
        }

        [Test]
        public void HaulEnd_TakesTheClipOff_AndGivesTheWalkSpriteBack()
        {
            var (anim, sr, clip, _, _, walk) = MakeClipAnimator();

            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Hauling, 0.2f, buoyX: 5f));
            Assert.AreNotEqual(walk, sr.sprite, "the heave took the renderer over");

            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Surfaced, 1f));
            Assert.AreEqual(HaulPose.None, anim.Pose);
            Assert.IsFalse(clip.IsPlaying, "the pot surfaced — the heave let the renderer go");
            Assert.AreEqual(walk, sr.sprite, "…exactly where it found it");
            Assert.IsFalse(sr.flipX);
        }

        /// <summary>⚠️ The boarding move shares this one seam. A haul that ends mid-vault must not stop
        /// the clip the VAULT started, or the fisher drops back to walk frames in mid-air.</summary>
        [Test]
        public void AVaultOwnsTheBody_AndTheHeaveNeitherFightsItNorStopsIt()
        {
            var (anim, _, clip, def, _, _) = MakeClipAnimator();
            def.BoardClip = new CharacterClipSheets
            { FrameCount = 10, FramesPerSecond = 1000f / 90f, Loops = false, Sheet = MakeSheet(8 * 10) };

            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Hauling, 0.2f, buoyX: 5f));
            Assert.AreEqual(CharacterClip.Haul, clip.Clip);

            // A vault begins over the top of a live haul (E at the rail while a pot is on the line).
            Assert.IsTrue(clip.Play(CharacterClip.Board, 0f, 0.55f, holdOnFinish: true));

            // The heave stands aside rather than seeking somebody else's clip about.
            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Hauling, 0.4f, buoyX: 5f));
            Assert.AreEqual(CharacterClip.Board, clip.Clip, "the heave stole the body back mid-vault");
            Assert.AreEqual(0, clip.Frame, "…or seeked the vault's own clip about");

            // And the haul ending does not take the vault's clip down with it.
            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Idle, 0f));
            Assert.AreEqual(CharacterClip.Board, clip.Clip, "the ending haul yanked the vault off the renderer");

            // When the vault is done the next snapshot takes the heave back on its own.
            clip.Stop();
            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Hauling, 0.5f, buoyX: 5f));
            Assert.AreEqual(CharacterClip.Haul, clip.Clip, "the heave never came back after the vault");
            Assert.IsTrue(clip.IsDriven);
        }

        /// <summary>The A/B. A kit with no baked heave draws exactly what it drew before this swap — the
        /// same discipline as the boarding slice's <c>WithNoBoardingArt_TheMoveIsUnchanged</c>.</summary>
        [Test]
        public void WithNoHaulClip_TheDeckHaulIsUnchanged()
        {
            var (anim, sr, clip, _, frames, walk) = MakeClipAnimator(withClipArt: false);

            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Hauling, 0f, buoyX: 5f));
            Assert.IsFalse(clip.IsPlaying, "there is no heave to play");
            Assert.AreEqual(HaulPose.Ease, anim.Pose);
            Assert.AreEqual(frames[7], sr.sprite, "the flat sheet's EASE frame, exactly as before");
            Assert.IsTrue(sr.flipX, "…mirrored toward the buoy, exactly as before");

            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Hauling, 0.10f, buoyX: 5f));
            Assert.AreEqual(frames[2], sr.sprite, "the flat sheet's cycle frame for this line, as before");

            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Hauling, 0.06f, buoyX: 5f));
            Assert.AreEqual(frames[6], sr.sprite, "the flat sheet's STRAIN frame, as before");

            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Idle, 0f));
            Assert.AreEqual(walk, sr.sprite, "and it hands the walk sprite back the way it always did");
            Assert.IsFalse(sr.flipX);
        }

        [Test]
        public void ASkinWhoseHeaveIsHalfBaked_FallsBackRatherThanDrawingHoles()
        {
            // The all-or-nothing gate reaches all the way out to the presenter: a short sheet is not a
            // partial animation, it is no animation, and the flat sheet is still a picture.
            var (anim, sr, clip, def, frames, _) = MakeClipAnimator();
            def.HaulClip.Sheet = MakeSheet(8 * ClipFrames - 1);

            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Hauling, 0f, buoyX: 5f));       // primes the line
            anim.OnHaulStateChanged(Snap(TrapHaulPhase.Hauling, 0.10f, buoyX: 5f));    // …now it is gaining
            Assert.IsFalse(clip.IsPlaying, "a hole in the sheet must not reach the renderer");
            Assert.AreEqual(frames[2], sr.sprite, "…the flat sheet drew it instead");
        }

        private Sprite[] MakeSheet(int count)
        {
            var set = new Sprite[count];
            for (int i = 0; i < set.Length; i++) set[i] = MakeSprite();
            return set;
        }
    }
}
