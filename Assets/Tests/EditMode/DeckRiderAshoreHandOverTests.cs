using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The ashore hand-over's pure rules, held without a frame: the switch (the owner's ruling of
    /// 2026-09-19, decision 1), the reasons she is not shown ashore (decision 4 and the NEITHER rule),
    /// and when a refused figure may ask the facet-id pool again (decision 3, option (i)). Each bar is
    /// written out here as a literal and is never read back from the code, so a guard that passes cannot
    /// be passing because the code moved its own bar. The frames themselves (one drawer at every
    /// hand-over) are PlayMode's: <c>DeckRiderMeshPresenterPlayTests</c>.
    /// </summary>
    public class DeckRiderAshoreHandOverTests
    {
        private const string ShippedConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";

        /// <summary>Shown poses stepped past a refusal: far more than any hand-over takes, so a latch that
        /// leaked after a few frames would be seen.</summary>
        private const int ManyFrames = 100;

        /// <summary>The rider's re-seat count at one seating, and at the one after it.</summary>
        private const int FirstSeat = 1;
        private const int SecondSeat = 2;

        private const string RiderDisabled = "ashore — the rider is disabled, so nothing poses her each frame";
        private const string NoRiderChild =
            "ashore — no rider child is wired, so the rider cannot tell a deck from the shore";
        private const string NoBody = "ashore — there is no body sprite to stand in for";
        private const string Inside = "inside something (a cab, or a helm that hides its pilot): she draws neither";
        private const string Clip = "ashore — a clip is playing, and the sprite draws every clip";
        private const string Wading = "ashore — wading, and the sprite draws her in the water";
        private const string Swimming = "ashore — swimming, and the sprite draws her in the water";

        // ---- the switch ---------------------------------------------------------------------------

        [Test]
        public void TheAshoreSwitch_IsLiveOnlyWithMeshCharacterOnToo()
        {
            Assert.IsFalse(DeckRiderMeshPresenter.AshoreSwitchOn(null), "no config: off");

            GameConfig config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                config.MeshCharacter = false;
                config.MeshCharacterAshore = false;
                Assert.IsFalse(DeckRiderMeshPresenter.AshoreSwitchOn(config), "both off: off");

                config.MeshCharacterAshore = true;
                Assert.IsFalse(DeckRiderMeshPresenter.AshoreSwitchOn(config),
                               "the ashore switch alone, MeshCharacter off: off");

                config.MeshCharacter = true;
                config.MeshCharacterAshore = false;
                Assert.IsFalse(DeckRiderMeshPresenter.AshoreSwitchOn(config),
                               "MeshCharacter alone: off ashore (aboard is not this switch's)");

                config.MeshCharacterAshore = true;
                Assert.IsTrue(DeckRiderMeshPresenter.AshoreSwitchOn(config), "both on: on");
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void TheAshoreSwitch_IsOffInTheCode_AndOffInTheShippedAsset()
        {
            Assert.IsFalse(GameConfig.DefaultMeshCharacterAshore, "the code's default is OFF");

            GameConfig fresh = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                Assert.IsFalse(fresh.MeshCharacterAshore, "a fresh config starts OFF");
            }
            finally
            {
                Object.DestroyImmediate(fresh);
            }

            GameConfig shipped = AssetDatabase.LoadAssetAtPath<GameConfig>(ShippedConfigPath);
            Assert.IsNotNull(shipped, "the shipped config loads from " + ShippedConfigPath);
            Assert.IsFalse(shipped.MeshCharacterAshore,
                           "the shipped asset ships the switch OFF: turning it on is the owner's playtest");
        }

        // ---- why she is not shown ashore ----------------------------------------------------------

        [Test]
        public void WhyNotShownAshore_IsNull_OnlyWhenEveryFenceIsClear()
        {
            Assert.IsNull(DeckRiderMeshPresenter.WhyNotShownAshore(
                              riderLive: true, hasRiderChild: true, hasBody: true, bodyShownOnRoot: true,
                              clipPlaying: false, water: OnFootWaterState.Dry),
                          "a live rider, wired, with a body shown on the root, no clip, dry: she is shown ashore");
        }

        [Test]
        public void WhyNotShownAshore_EachFenceAlone_NamesItself()
        {
            Assert.AreEqual(RiderDisabled, DeckRiderMeshPresenter.WhyNotShownAshore(
                false, true, true, true, false, OnFootWaterState.Dry), "the rider disabled");
            Assert.AreEqual(NoRiderChild, DeckRiderMeshPresenter.WhyNotShownAshore(
                true, false, true, true, false, OnFootWaterState.Dry), "no rider child wired");
            Assert.AreEqual(NoBody, DeckRiderMeshPresenter.WhyNotShownAshore(
                true, true, false, true, false, OnFootWaterState.Dry), "no body sprite");
            Assert.AreEqual(Inside, DeckRiderMeshPresenter.WhyNotShownAshore(
                true, true, true, false, false, OnFootWaterState.Dry),
                "the body not shown on the root (a cab, a helm that hides its pilot): NEITHER");
            Assert.AreEqual(Clip, DeckRiderMeshPresenter.WhyNotShownAshore(
                true, true, true, true, true, OnFootWaterState.Dry), "a clip playing: the sprite draws it");
            Assert.AreEqual(Wading, DeckRiderMeshPresenter.WhyNotShownAshore(
                true, true, true, true, false, OnFootWaterState.Wade), "wading: the sprite draws her");
            Assert.AreEqual(Swimming, DeckRiderMeshPresenter.WhyNotShownAshore(
                true, true, true, true, false, OnFootWaterState.Swim), "swimming: the sprite draws her");
        }

        [Test]
        public void WhyNotShownAshore_WithEveryFenceDown_NamesTheFirst()
        {
            Assert.AreEqual(RiderDisabled, DeckRiderMeshPresenter.WhyNotShownAshore(
                false, false, false, false, true, OnFootWaterState.Swim),
                "every fence down: the reason is the first, the rider that poses her");
        }

        // ---- when a refused figure may ask again --------------------------------------------------

        [Test]
        public void Arrivals_TheFirstShownPoseIsAnArrival_AndARepeatIsNot()
        {
            var arrivals = new DeckRiderMeshPresenter.AshoreArrivals();
            Assert.IsTrue(arrivals.Step(true, FirstSeat), "the first pose shown ashore is an arrival");
            Assert.IsFalse(arrivals.Step(true, FirstSeat), "the same seat, shown again: not an arrival");
        }

        [Test]
        public void Arrivals_AHiddenPoseIsNeverAnArrival()
        {
            var arrivals = new DeckRiderMeshPresenter.AshoreArrivals();
            Assert.IsFalse(arrivals.Step(false, FirstSeat), "hidden (aboard, a clip, the water): no arrival");
            Assert.IsFalse(arrivals.Step(false, SecondSeat), "hidden, re-seated: still no arrival");
        }

        [Test]
        public void Arrivals_AReSeatWhileShown_IsAnArrival()
        {
            // A region's arrival re-states OnFoot while she is already standing ashore.
            var arrivals = new DeckRiderMeshPresenter.AshoreArrivals();
            arrivals.Step(true, FirstSeat);
            Assert.IsTrue(arrivals.Step(true, SecondSeat), "shown, re-seated since: an arrival");
            Assert.IsFalse(arrivals.Step(true, SecondSeat), "and the pose after it is not");
        }

        [Test]
        public void Arrivals_HiddenThenShown_IsAnArrival()
        {
            // A landing, a clip's end, a wade's end: hidden, then shown on the same seat.
            var arrivals = new DeckRiderMeshPresenter.AshoreArrivals();
            arrivals.Step(true, FirstSeat);
            arrivals.Step(false, FirstSeat);
            Assert.IsTrue(arrivals.Step(true, FirstSeat), "shown again after a hidden pose: an arrival");
        }

        [Test]
        public void Arrivals_ARefusalHolds_OverShownAndHiddenPoses_UntilTheNextArrival()
        {
            var arrivals = new DeckRiderMeshPresenter.AshoreArrivals();
            arrivals.Step(true, FirstSeat);
            arrivals.Refuse();
            Assert.IsTrue(arrivals.Refused, "the pool said no");

            for (int i = 0; i < ManyFrames; i++)
            {
                Assert.IsFalse(arrivals.Step(true, FirstSeat), "shown pose " + i + " is no arrival");
                Assert.IsTrue(arrivals.Refused, "shown pose " + i + ": the refusal holds, she does not re-ask");
            }

            Assert.IsFalse(arrivals.Step(false, FirstSeat), "a hidden pose is no arrival");
            Assert.IsTrue(arrivals.Refused, "and does not clear the refusal");

            Assert.IsTrue(arrivals.Step(true, FirstSeat), "shown again: an arrival");
            Assert.IsFalse(arrivals.Refused, "the arrival clears the refusal, so she asks again");
        }

        [Test]
        public void Arrivals_AReSeat_ClearsARefusal()
        {
            var arrivals = new DeckRiderMeshPresenter.AshoreArrivals();
            arrivals.Step(true, FirstSeat);
            arrivals.Refuse();
            Assert.IsTrue(arrivals.Step(true, SecondSeat), "a region's re-seat while shown is an arrival");
            Assert.IsFalse(arrivals.Refused, "which clears the refusal");
        }

        [Test]
        public void Arrivals_Reset_ClearsTheRefusal_AndReArmsTheArrival()
        {
            var arrivals = new DeckRiderMeshPresenter.AshoreArrivals();
            arrivals.Step(true, FirstSeat);
            arrivals.Refuse();

            arrivals.Reset();
            Assert.IsFalse(arrivals.Refused, "reset: no refusal");
            Assert.IsTrue(arrivals.Step(true, FirstSeat), "reset: the next shown pose is an arrival");
        }
    }
}
