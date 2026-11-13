using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE FIGURE KNOWS IT IS A BIG ONE (owner's ask 2026-11-13: "the player sprite should have an
    /// exclamation for a big fish too"). Pins the logic of the exclamation over the fisher and the
    /// longer hook-set pose: only the big-one cue raises them, once per hook, the mark clears after its
    /// time, and one <c>GameConfig.FishFeel</c> bool switches both off. That the rod loop raises the cue
    /// only for a big one, once per hook, is pinned in <see cref="FishFeelTests"/>. The LOOK is not
    /// testable here — it is owed an owner plate.
    /// </summary>
    public class BigOneReactionTests
    {
        private const float Eps = 1e-4f;
        private static FishFeelSettings D => FishFeelSettings.Default;

        private readonly List<Object> _spawned = new();
        private GameObject _go;
        private BigOneReactionPresenter _mark;
        private PlayerFishingAnimator _anim;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            EventBus.Clear<InnerVoiceCue>();
            _go = new GameObject("Fisher");
            _spawned.Add(_go);
            _anim = _go.AddComponent<PlayerFishingAnimator>();   // RequireComponent adds the renderer
            _mark = _go.AddComponent<BigOneReactionPresenter>();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<InnerVoiceCue>();
            GameServices.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private static InnerVoiceCue BigOne => new InnerVoiceCue(InnerVoiceCues.BigOneHooked);

        private void UseConfig(FishFeelSettings feel)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(config);
            config.FishFeel = feel;
            GameServices.Config = config;
        }

        private static FishingState State(FishingPhase phase)
            => new FishingState(phase, 0f, 0f, null, null, FishCategory.InshoreGroundfish, 0f,
                                depth01: 0f, slackWindowOpen: false, rodBend01: 0f,
                                fishOffsetX: 0f, fishOffsetY: 0f,
                                castCharge01: 0f, castAimX: 0f, castAimY: 0f, rigDepthM: 0f);

        // ── the clock ────────────────────────────────────────────────────────────────────────────

        [Test]
        public void TheClock_Starts_RunsItsTime_AndClears()
        {
            FishFeelSettings s = D;
            var clock = new BigOneReactionClock();
            Assert.IsFalse(clock.Active);

            Assert.IsTrue(clock.Trigger(in s));
            Assert.IsTrue(clock.Active);

            clock.Tick(s.BigOneMarkSeconds * 0.5f, in s);
            Assert.IsTrue(clock.Active, "still up half way through");

            clock.Tick(s.BigOneMarkSeconds * 0.5f + Eps, in s);
            Assert.IsFalse(clock.Active, "cleared at the end of its time");
            Assert.AreEqual(0f, clock.Elapsed, Eps);
        }

        [Test]
        public void TheClock_RefusesASecondStart_WhileItIsUp()
        {
            FishFeelSettings s = D;
            var clock = new BigOneReactionClock();
            clock.Trigger(in s);
            clock.Tick(s.BigOneMarkSeconds * 0.5f, in s);

            Assert.IsFalse(clock.Trigger(in s), "one hook, one mark");
            Assert.AreEqual(s.BigOneMarkSeconds * 0.5f, clock.Elapsed, Eps, "the refused start did not rewind it");
        }

        [Test]
        public void TheClock_NeverStarts_WhenSwitchedOff_OrGivenNoTime()
        {
            FishFeelSettings off = D;
            off.BigOneReactionEnabled = false;
            var clock = new BigOneReactionClock();
            Assert.IsFalse(clock.Trigger(in off));
            Assert.IsFalse(clock.Active);

            FishFeelSettings noTime = D;
            noTime.BigOneMarkSeconds = 0f;
            Assert.IsFalse(clock.Trigger(in noTime));
            Assert.IsFalse(clock.Active);
        }

        [Test]
        public void TheClock_ClearsAtOnce_WhenSwitchedOffMidShow()
        {
            FishFeelSettings s = D;
            var clock = new BigOneReactionClock();
            clock.Trigger(in s);
            s.BigOneReactionEnabled = false;
            clock.Tick(0.01f, in s);
            Assert.IsFalse(clock.Active);
        }

        // ── the shapes ───────────────────────────────────────────────────────────────────────────

        [Test]
        public void TheMark_PopsPastItsSize_ThenSettles()
        {
            FishFeelSettings s = D;
            Assert.Greater(s.BigOneMarkPopSeconds, 0f);
            Assert.AreEqual(0f, BigOneReactionMath.MarkScale(0f, in s), Eps);
            Assert.AreEqual(s.BigOneMarkPopScale, BigOneReactionMath.MarkScale(s.BigOneMarkPopSeconds, in s), Eps);
            Assert.AreEqual(1f, BigOneReactionMath.MarkScale(s.BigOneMarkPopSeconds * 2f, in s), Eps);
            Assert.AreEqual(1f, BigOneReactionMath.MarkScale(s.BigOneMarkSeconds, in s), Eps);

            s.BigOneMarkPopSeconds = 0f;
            Assert.AreEqual(1f, BigOneReactionMath.MarkScale(0f, in s), Eps, "no pop time = full size at once");
        }

        [Test]
        public void TheMark_IsSolid_ThenFadesOutByTheEndOfItsTime()
        {
            FishFeelSettings s = D;
            Assert.Less(s.BigOneMarkFadeSeconds, s.BigOneMarkSeconds);
            Assert.AreEqual(1f, BigOneReactionMath.MarkAlpha(0f, in s), Eps);
            Assert.AreEqual(1f, BigOneReactionMath.MarkAlpha(s.BigOneMarkSeconds - s.BigOneMarkFadeSeconds, in s), Eps);
            Assert.AreEqual(0.5f, BigOneReactionMath.MarkAlpha(s.BigOneMarkSeconds - s.BigOneMarkFadeSeconds * 0.5f, in s), 1e-3f);
            Assert.AreEqual(0f, BigOneReactionMath.MarkAlpha(s.BigOneMarkSeconds, in s), Eps);
        }

        [Test]
        public void TheShippedDefaults_ShowAMarkAndHoldThePose()
        {
            FishFeelSettings s = D;
            Assert.IsTrue(s.BigOneReactionEnabled);
            Assert.Greater(s.BigOneMarkSeconds, 0f);
            Assert.Greater(s.BigOneMarkHeightMetres, 0f);
            Assert.Greater(s.BigOneMarkOffsetMetres.y, 0f, "above the figure's feet");
            Assert.Greater(s.BigOneMarkColor.a, 0f);
            Assert.Greater(BigOneReactionMath.StrikeHoldSeconds(in s), 0f);

            s.BigOneReactionEnabled = false;
            Assert.AreEqual(0f, BigOneReactionMath.StrikeHoldSeconds(in s), Eps, "the one switch also drops the pose hold");
        }

        // ── the exclamation over the figure ──────────────────────────────────────────────────────

        [Test]
        public void OnlyTheBigOneCue_RaisesTheMark()
        {
            _mark.OnInnerVoiceCue(new InnerVoiceCue(InnerVoiceCues.FeedingSea));
            Assert.IsFalse(_mark.Showing, "another moment's cue is not hers");
            Assert.IsNull(_mark.Mark, "nothing is built until a big one");

            _mark.OnInnerVoiceCue(BigOne);
            Assert.IsTrue(_mark.Showing);
            Assert.IsNotNull(_mark.Mark);
            Assert.IsTrue(_mark.Mark.enabled);
            Assert.IsNotNull(_mark.Mark.sprite);
            Assert.AreSame(_go.transform, _mark.Mark.transform.parent, "it rides the figure");
            Assert.AreEqual(D.BigOneMarkOffsetMetres.x, _mark.Mark.transform.localPosition.x, Eps);
            Assert.AreEqual(D.BigOneMarkOffsetMetres.y, _mark.Mark.transform.localPosition.y, Eps);
        }

        [Test]
        public void TheMark_ClearsAfterItsTime_AndTheNextBigOneRaisesItAgain()
        {
            _mark.OnInnerVoiceCue(BigOne);
            _mark.Advance(D.BigOneMarkSeconds * 0.5f);
            Assert.IsTrue(_mark.Showing);
            Assert.IsTrue(_mark.Mark.enabled);
            Assert.AreEqual(D.BigOneMarkHeightMetres, _mark.Mark.transform.localScale.y, Eps, "settled at its data size");

            _mark.Advance(D.BigOneMarkSeconds);
            Assert.IsFalse(_mark.Showing);
            Assert.IsFalse(_mark.Mark.enabled, "gone after its time");

            _mark.Advance(1f);
            Assert.IsFalse(_mark.Mark.enabled, "and it stays gone");

            _mark.OnInnerVoiceCue(BigOne);
            Assert.IsTrue(_mark.Showing, "the next hook's big one shows again");
        }

        [Test]
        public void ARepeatedCue_DoesNotRestartTheMark()
        {
            _mark.OnInnerVoiceCue(BigOne);
            _mark.Advance(D.BigOneMarkSeconds * 0.75f);
            _mark.OnInnerVoiceCue(BigOne);
            _mark.Advance(D.BigOneMarkSeconds * 0.5f);
            Assert.IsFalse(_mark.Showing, "it ran its ONE time from the first cue");
        }

        [Test]
        public void TheMark_AnswersTheBus_NotACallerWhoKnowsIt()
        {
            EventBus.Subscribe<InnerVoiceCue>(_mark.OnInnerVoiceCue);   // what OnEnable does in play
            try
            {
                EventBus.Publish(new InnerVoiceCue(InnerVoiceCues.FeedingSea));
                Assert.IsFalse(_mark.Showing);
                EventBus.Publish(BigOne);
                Assert.IsTrue(_mark.Showing);
            }
            finally { EventBus.Unsubscribe<InnerVoiceCue>(_mark.OnInnerVoiceCue); }
        }

        [Test]
        public void TheOwnersSwitch_Off_ShowsNothing()
        {
            FishFeelSettings off = D;
            off.BigOneReactionEnabled = false;
            UseConfig(off);

            _mark.OnInnerVoiceCue(BigOne);
            Assert.IsFalse(_mark.Showing);
            Assert.IsNull(_mark.Mark);
        }

        [Test]
        public void TheMark_ReadsItsSizeAndPlaceFromGameConfig()
        {
            FishFeelSettings feel = D;
            feel.BigOneMarkOffsetMetres = new Vector2(0.25f, 2.5f);
            feel.BigOneMarkHeightMetres = 0.6f;
            feel.BigOneMarkPopSeconds = 0f;
            UseConfig(feel);

            _mark.OnInnerVoiceCue(BigOne);
            Assert.AreEqual(0.25f, _mark.Mark.transform.localPosition.x, Eps);
            Assert.AreEqual(2.5f, _mark.Mark.transform.localPosition.y, Eps);
            Assert.AreEqual(0.6f, _mark.Mark.transform.localScale.y, Eps);
        }

        // ── the figure's pose ────────────────────────────────────────────────────────────────────

        [Test]
        public void TheFigure_RemembersTheBigOne_ForThatFightOnly()
        {
            _anim.OnFishingStateChanged(new FishingStateChanged(State(FishingPhase.Bite)));
            _anim.OnInnerVoiceCue(new InnerVoiceCue(InnerVoiceCues.FeedingSea));
            Assert.IsFalse(_anim.BigOneHooked);

            _anim.OnInnerVoiceCue(BigOne);   // raised at the hook-set, before the first fight state
            Assert.IsTrue(_anim.BigOneHooked);

            _anim.OnFishingStateChanged(new FishingStateChanged(State(FishingPhase.FightDeep)));
            _anim.OnFishingStateChanged(new FishingStateChanged(State(FishingPhase.FightSurface)));
            Assert.IsTrue(_anim.BigOneHooked, "held through the whole fight");

            _anim.OnFishingStateChanged(new FishingStateChanged(State(FishingPhase.Landed)));
            Assert.IsFalse(_anim.BigOneHooked, "the next fish starts ordinary");
        }

        [Test]
        public void TheLongerStrike_IsTheStrikePoseHeld_ThenTheReel()
        {
            // The animator adds StrikeHoldSeconds to the strike window it hands this mapping.
            float strike = 0.5f, held = strike + BigOneReactionMath.StrikeHoldSeconds(D);
            Assert.Greater(held, strike);
            float between = (strike + held) * 0.5f;
            Assert.AreEqual(FishingPose.Reel, PlayerFishingAnimMath.PoseFor(FishingPhase.FightDeep, between, strike));
            Assert.AreEqual(FishingPose.Strike, PlayerFishingAnimMath.PoseFor(FishingPhase.FightDeep, between, held));
            Assert.AreEqual(FishingPose.Reel, PlayerFishingAnimMath.PoseFor(FishingPhase.FightDeep, held + Eps, held));
        }
    }
}
