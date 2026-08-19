using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The swimmer's visual, end to end through the real components: the wading model's own signal goes
    /// on the bus, and the rig's swim / tread clip lands on the renderer and comes off again.
    ///
    /// <para><b>Why the EVENT and not the controller.</b> Reaching the swim band through
    /// <c>PlayerWalkController</c> means real terrain, a real tide and a region loaded to a spot deep
    /// enough to swim — a lot of fixture for a presentation test, and it would fail for a dozen reasons
    /// that are not this feature. The band is published as <c>OnFootWaterStateChanged</c> precisely so
    /// consumers can be driven without it, which is also exactly how the animator is wired.</para>
    ///
    /// <para>⚠️ <b>No <c>EventBus.Clear</c> anywhere here.</b> Clearing a channel unsubscribes every LIVE
    /// component on it, not just this fixture's — <c>WadeSplashEmitter</c> rides this same signal — and
    /// test order is not guaranteed, so a clear here silently disarms whatever runs next. The fixture
    /// tears down by destroying its own object, which unsubscribes exactly one handler.</para>
    ///
    /// <para>Speed is driven with <see cref="IsoCharacterSprite.HoldSpeed"/> rather than by moving a
    /// rigidbody: frames are not time, and a test that advanced real metres would be asserting the
    /// physics step as much as the pose.</para>
    /// </summary>
    public class SwimVisualPlayTests
    {
        const string FisherVisual = "Assets/_Project/Data/Characters/FisherIso.asset";

        GameObject _go;
        IsoCharacterSprite _skin;
        CharacterClipPlayer _clips;
        PlayerSwimAnimator _swim;

        [SetUp]
        public void SetUp()
        {
            var visual = AssetDatabase.LoadAssetAtPath<CharacterVisualDef>(FisherVisual);
            Assert.IsNotNull(visual, $"no character visual at '{FisherVisual}'");
            Assert.IsTrue(visual.HasClip(CharacterClip.Swim), "the fisher's swim clip is not wired");
            Assert.IsTrue(visual.HasClip(CharacterClip.Tread), "the fisher's tread clip is not wired");

            // INACTIVE first: OnEnable subscribes to the bus, and the components must be configured
            // before the first frame reads them.
            _go = new GameObject("SwimVisualPlayTests.Player");
            _go.SetActive(false);
            _go.AddComponent<SpriteRenderer>();

            _skin = _go.AddComponent<IsoCharacterSprite>();
            _skin.Configure(visual);

            _clips = _go.AddComponent<CharacterClipPlayer>();
            _clips.Configure(visual);

            _swim = _go.AddComponent<PlayerSwimAnimator>();
            _go.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        static void EnterBand(OnFootWaterState state, OnFootWaterState previous) =>
            EventBus.Publish(new OnFootWaterStateChanged(state, previous,
                                                         deepening: state > previous, depth: 1.5f));

        [UnityTest]
        public IEnumerator OnDryLand_NothingPosesAndTheWalkSpriteKeepsTheRenderer()
        {
            yield return null;

            Assert.AreEqual(SwimPose.None, _swim.Pose);
            Assert.AreEqual(CharacterClip.None, _clips.Clip, "no clip should have claimed the renderer");
            Assert.IsFalse(_skin.IsSuspended, "the iso skin must still be drawing");
        }

        [UnityTest]
        public IEnumerator Wading_StillWalks_BecauseTheFeetAreOnTheBottom()
        {
            EnterBand(OnFootWaterState.Wade, OnFootWaterState.Dry);
            _skin.HoldSpeed(0.8f);
            yield return null;
            _swim.Tick();

            Assert.AreEqual(SwimPose.None, _swim.Pose, "the wade band must not pose");
            Assert.AreEqual(CharacterClip.None, _clips.Clip);
            Assert.IsFalse(_skin.IsSuspended);
        }

        [UnityTest]
        public IEnumerator EnteringTheSwimBandAtRest_Treads()
        {
            EnterBand(OnFootWaterState.Swim, OnFootWaterState.Wade);
            _skin.HoldSpeed(0f);
            yield return null;
            _swim.Tick();

            Assert.AreEqual(SwimPose.Tread, _swim.Pose);
            Assert.AreEqual(CharacterClip.Tread, _clips.Clip);
            Assert.IsTrue(_skin.IsSuspended, "the clip player must have claimed the renderer");
        }

        [UnityTest]
        public IEnumerator MakingWayInTheSwimBand_Strokes_AndSwapsBackWhenItStops()
        {
            EnterBand(OnFootWaterState.Swim, OnFootWaterState.Wade);
            _skin.HoldSpeed(0.4f);
            yield return null;
            _swim.Tick();
            Assert.AreEqual(CharacterClip.Swim, _clips.Clip, "making way must stroke");

            // ⚠️ A held speed is not visible until the skin's own LateUpdate has taken it, so the frame
            // between HoldSpeed and Tick is load-bearing: without it this reads LAST frame's speed and
            // the pose never changes. (Caught by this test failing on the first run — the product was
            // right and the test was reading a stale number.)
            _skin.HoldSpeed(0f);
            yield return null;
            _swim.Tick();
            Assert.AreEqual(CharacterClip.Tread, _clips.Clip, "holding station must tread");

            // ...and back again, so the swap is not one-way.
            _skin.HoldSpeed(0.4f);
            yield return null;
            _swim.Tick();
            Assert.AreEqual(CharacterClip.Swim, _clips.Clip);
        }

        [UnityTest]
        public IEnumerator TheStrokeActuallyRUNS_RatherThanFreezingOnFrameZero()
        {
            // ⚠️ The regression this exists for: CharacterClipPlayer.Play RESTARTS a clip, so an animator
            // that called it every frame would pin the stroke on frame 0 forever while every other assert
            // here still passed. Play must happen on the pose EDGE only, and Tick is called inside the
            // loop below precisely so a re-Play would show up.
            //
            // Driven in SECONDS through Advance rather than by counting frames: frames are not time, and
            // the clip's own rate is stated in ms (swim is 8 frames at 130 ms = 1.04 s a cycle), so a
            // frame-counted loop would be asserting the editor's step rate as much as the clip.
            //
            // ⚠️ And it watches FRAME, not FramePosition: FramePosition is the DRIVEN path's field and
            // stays 0 for a clock-paced clip, so asserting on it passes and fails for reasons that have
            // nothing to do with the stroke. (That is exactly how this test failed first time round.)
            EnterBand(OnFootWaterState.Swim, OnFootWaterState.Wade);
            _skin.HoldSpeed(0.4f);
            yield return null;
            _swim.Tick();
            Assert.AreEqual(CharacterClip.Swim, _clips.Clip);

            var seen = new System.Collections.Generic.HashSet<int> { _clips.Frame };
            float startElapsed = _clips.Elapsed;
            for (int i = 0; i < 24; i++)      // 24 x 60 ms = 1.44 s, comfortably over one 1.04 s cycle
            {
                _clips.Advance(0.06f);
                _swim.Tick();                 // a re-Play here would reset the stroke every step
                seen.Add(_clips.Frame);
            }

            Assert.Greater(_clips.Elapsed, startElapsed, "the clip's clock never moved");
            Assert.Greater(seen.Count, 1,
                           $"the swim stroke sat on frame {_clips.Frame} for a whole cycle — is Play " +
                           "being called every frame instead of on the pose edge?");
            Assert.AreEqual(CharacterClip.Swim, _clips.Clip, "and it must still be the swim clip");
        }

        [UnityTest]
        public IEnumerator LeavingTheSwimBand_HandsTheRendererBackToTheWalkSprite()
        {
            EnterBand(OnFootWaterState.Swim, OnFootWaterState.Wade);
            _skin.HoldSpeed(0.4f);
            yield return null;
            _swim.Tick();
            Assert.AreEqual(CharacterClip.Swim, _clips.Clip);

            EnterBand(OnFootWaterState.Wade, OnFootWaterState.Swim);
            _swim.Tick();

            Assert.AreEqual(SwimPose.None, _swim.Pose);
            Assert.AreEqual(CharacterClip.None, _clips.Clip, "the clip must be stopped, not left running");
            Assert.IsFalse(_skin.IsSuspended, "the iso skin must have the renderer back");
        }

        [UnityTest]
        public IEnumerator ADifferentClipTakingTheRenderer_IsNotStoppedByLeavingTheWater()
        {
            // The swimmer must only ever stop its OWN clip. A haul (or a boarding vault) that claimed the
            // renderer while the player was in the water would otherwise be cut short by the band change,
            // which reads on screen as the animation snapping.
            EnterBand(OnFootWaterState.Swim, OnFootWaterState.Wade);
            _skin.HoldSpeed(0.4f);
            yield return null;
            _swim.Tick();
            Assert.AreEqual(CharacterClip.Swim, _clips.Clip);

            if (!_clips.Play(CharacterClip.Haul, 0f))
                Assert.Ignore("the fisher's haul clip is not baked in this checkout");

            EnterBand(OnFootWaterState.Dry, OnFootWaterState.Swim);
            _swim.Tick();

            Assert.AreEqual(CharacterClip.Haul, _clips.Clip,
                            "leaving the water stopped somebody else's clip");
        }

        [UnityTest]
        public IEnumerator TheAnimatorUnsubscribesWhenDisabled_SoADeadObjectCannotPose()
        {
            _go.SetActive(false);
            EnterBand(OnFootWaterState.Swim, OnFootWaterState.Wade);
            yield return null;

            Assert.AreEqual(OnFootWaterState.Dry, _swim.WaterState,
                            "a disabled animator must not still be hearing the bus");
            Assert.AreEqual(SwimPose.None, _swim.Pose);
        }
    }
}
