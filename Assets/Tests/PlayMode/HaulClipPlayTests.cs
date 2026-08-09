using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The deck haul drawn by the rig's <c>haul</c> clip, live</b> — through the real component
    /// lifecycle, the real <see cref="EventBus"/> subscription and a real
    /// <see cref="IsoCharacterSprite"/> writing cells underneath.
    ///
    /// <para>These are PlayMode because every claim is about a live renderer with TWO components that
    /// could write it: the EditMode suite proves the snapshot → frame mapping, and this proves the
    /// hand-off actually happens — that the iso driver stands down for the duration, that it is
    /// released again on the far side, and that the subscription is really made (an
    /// <c>OnEnable</c> claim, which EditMode cannot make at all).</para>
    ///
    /// <para>The load-bearing one is <see cref="TheHeaveMovesWithTheRope_NotWithTheClock"/>, and it is
    /// driven in <b>SECONDS</b> rather than in frames for the usual reason — a headless
    /// <c>yield return null</c> is not time — but here for a second reason too: the whole claim is that
    /// REAL TIME PASSING MOVES NOTHING. The haul's cycle is keyed to <c>line01</c>, so a fisher holding
    /// a rope that is not moving must be holding still; a clip left on the clock would run its 8 frames
    /// at 8.3 fps and heave at a stationary pot. Counting frames could not tell the two apart.</para>
    /// </summary>
    public class HaulClipPlayTests
    {
        private const int ClipFrames = 8;             // rev 6.2's ANIMS: haul is 8 f × 120 ms, looping
        private const float ClipFps = 1000f / 120f;
        private const int Directions = 8;

        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<TrapHaulStateChanged>();
            foreach (var o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        private Sprite MakeSprite()
        {
            var tex = new Texture2D(4, 8);
            _spawned.Add(tex);
            var s = Sprite.Create(tex, new Rect(0, 0, 4, 8), new Vector2(0.5f, 0.1f), 32f);
            _spawned.Add(s);
            return s;
        }

        private Sprite[] Sheet(int count)
        {
            var set = new Sprite[count];
            for (int i = 0; i < set.Length; i++) set[i] = MakeSprite();
            return set;
        }

        /// <summary>A skin with a body to walk in and, optionally, the rig's heave baked.</summary>
        private CharacterVisualDef Skin(bool withHaulClip)
        {
            var d = ScriptableObject.CreateInstance<CharacterVisualDef>();
            _spawned.Add(d);
            d.Id = "visual.haul_clip_play";
            d.FacingCount = Directions;
            d.IdleFrameCount = 4; d.IdleSheet = Sheet(Directions * 4);
            d.WalkFrameCount = 6; d.WalkSheet = Sheet(Directions * 6);
            if (withHaulClip)
                d.HaulClip = new CharacterClipSheets
                { FrameCount = ClipFrames, FramesPerSecond = ClipFps, Loops = true, Sheet = Sheet(Directions * ClipFrames) };
            return d;
        }

        private struct Rig
        {
            public GameObject Go;
            public SpriteRenderer Renderer;
            public IsoCharacterSprite Iso;
            public CharacterClipPlayer Clip;
            public PlayerHaulAnimator Haul;
        }

        /// <summary>The fisher as the builder assembles her: one renderer, the iso skin driving it, and
        /// the clip seam plus the haul animator beside it. The iso driver's own LateUpdate is left
        /// running — it is the thing that must stand down, so it has to be able to write.</summary>
        private Rig Build(bool withHaulClip = true)
        {
            var go = new GameObject("Fisher");
            _spawned.Add(go);
            var sr = go.AddComponent<SpriteRenderer>();
            var iso = go.AddComponent<IsoCharacterSprite>();
            CharacterVisualDef def = Skin(withHaulClip);
            iso.Configure(def);

            var clip = go.AddComponent<CharacterClipPlayer>();   // reads the iso skin's own def
            var haul = go.AddComponent<PlayerHaulAnimator>();
            haul.Configure(Sheet(8));                            // the flat sheet is wired either way

            return new Rig { Go = go, Renderer = sr, Iso = iso, Clip = clip, Haul = haul };
        }

        private static void Publish(TrapHaulPhase phase, float line01, float buoyX = 5f, float buoyY = 0f)
            => EventBus.Publish(new TrapHaulStateChanged(
                   new TrapHaulState(phase, 0.5f, line01, false, buoyX, buoyY)));

        /// <summary>Let real seconds pass, and report how many actually did.</summary>
        private static IEnumerator Wait(float seconds, System.Action<float> spent = null)
        {
            float t = 0f;
            while (t < seconds) { yield return null; t += Time.deltaTime; }
            spent?.Invoke(t);
        }

        // -------------------------------------------------------------------------------------------

        /// <summary>⭐ The load-bearing claim. The hands move with the ROPE: hold the line still across
        /// real seconds and the heave must not budge a frame. A clip left on the clock would have run
        /// its whole 0.96 s cycle — more than once — at a pot that never moved.</summary>
        [UnityTest]
        public IEnumerator TheHeaveMovesWithTheRope_NotWithTheClock()
        {
            var r = Build();
            yield return null;

            Publish(TrapHaulPhase.Hauling, 0.30f);
            Publish(TrapHaulPhase.Hauling, 0.40f);          // a gaining line → the pull cycle
            Assert.AreEqual(HaulPose.Pull, r.Haul.Pose);
            Assert.AreEqual(CharacterClip.Haul, r.Clip.Clip, "the rig's heave never took the renderer");
            Assert.IsTrue(r.Clip.IsDriven, "the heave is on a clock instead of on the line");

            int frameWhenTheRopeStopped = r.Clip.Frame;
            Sprite cellWhenTheRopeStopped = r.Renderer.sprite;

            // Two and a half of the clip's own cycles of REAL TIME, with no snapshot to move the line.
            float spent = 0f;
            yield return Wait(ClipFrames / ClipFps * 2.5f, s => spent = s);

            Assert.Greater(spent, ClipFrames / ClipFps,
                $"only {spent:0.00} s of real time passed — the test never got to make its point");
            Assert.AreEqual(frameWhenTheRopeStopped, r.Clip.Frame,
                $"{spent:0.00} s of clock moved the heave from frame {frameWhenTheRopeStopped} to " +
                $"{r.Clip.Frame} — the fisher is hauling at a rope that is standing still");
            Assert.AreEqual(cellWhenTheRopeStopped, r.Renderer.sprite, "…and it reached the renderer");

            // The rope moving is the only thing that moves her.
            Publish(TrapHaulPhase.Hauling, 0.55f);
            Assert.AreNotEqual(frameWhenTheRopeStopped, r.Clip.Frame,
                "line gained and the hands did not follow it");
        }

        /// <summary>Exactly one driver writes the sprite. The iso skin is suspended for the whole haul —
        /// not merely overwritten every frame, which is the bug this hand-off exists to prevent.</summary>
        [UnityTest]
        public IEnumerator ForTheWholeHaul_TheIsoSkinStandsDown_AndIsGivenItBack()
        {
            var r = Build();
            yield return null;
            Assert.IsFalse(r.Iso.IsSuspended, "nothing has claimed the renderer yet");

            Publish(TrapHaulPhase.Hauling, 0.20f);
            Assert.IsTrue(r.Iso.IsSuspended, "the iso driver is still writing cells under the heave");

            Sprite duringTheHaul = r.Renderer.sprite;
            yield return Wait(0.4f);
            Assert.AreEqual(duringTheHaul, r.Renderer.sprite,
                "something else wrote the renderer while the heave owned it");

            Publish(TrapHaulPhase.Surfaced, 1f);
            Assert.IsFalse(r.Iso.IsSuspended, "the pot surfaced — the iso driver never got the renderer back");
            Assert.IsFalse(r.Clip.IsPlaying);

            // …and it really does take it up again rather than merely being allowed to.
            yield return null;
            yield return null;
            Assert.IsNotNull(r.Renderer.sprite, "the fisher is drawing nothing at all after her haul");
        }

        /// <summary>The subscription itself — an <c>OnEnable</c> claim, which is why this is PlayMode.
        /// And disabling mid-haul must not leave a heaving fisher frozen on the deck.</summary>
        [UnityTest]
        public IEnumerator TheAnimatorIsSubscribed_AndLetsGoWhenDisabled()
        {
            var r = Build();
            yield return null;

            Publish(TrapHaulPhase.Hauling, 0.25f);
            Assert.AreEqual(CharacterClip.Haul, r.Clip.Clip, "the bus never reached the animator");

            r.Haul.enabled = false;
            yield return null;
            Assert.IsFalse(r.Clip.IsPlaying, "disabled mid-haul and left the heave stuck on the renderer");
            Assert.IsFalse(r.Iso.IsSuspended, "…still holding the iso driver down, too");
        }

        /// <summary>⚠️ The A/B, live. A kit whose heave was never baked draws exactly what it drew before
        /// this swap: the owner's flat sheet, mirrored toward the buoy. Same discipline as the boarding
        /// slice's <c>WithNoBoardingArt_TheMoveIsUnchanged</c>.</summary>
        [UnityTest]
        public IEnumerator WithNoHaulArt_TheDeckHaulIsUnchanged()
        {
            var r = Build(withHaulClip: false);
            yield return null;

            Publish(TrapHaulPhase.Hauling, 0.10f);
            Publish(TrapHaulPhase.Hauling, 0.20f);

            Assert.IsFalse(r.Clip.IsPlaying, "there is no heave baked — nothing should have played");
            Assert.AreEqual(HaulPose.Pull, r.Haul.Pose, "…but the deck haul still animates");
            Assert.IsTrue(r.Iso.IsSuspended, "…through the same claim it always used");
            Assert.IsTrue(r.Renderer.flipX, "…and the flat sheet still mirrors toward the buoy");

            Publish(TrapHaulPhase.Idle, 0f);
            Assert.IsFalse(r.Iso.IsSuspended, "the walk sprite gets the renderer back exactly as before");
            Assert.IsFalse(r.Renderer.flipX);
        }
    }
}
