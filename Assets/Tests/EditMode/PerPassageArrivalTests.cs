using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>WHICH WAY IN.</b> Every region built so far has ONE arrival, because every region built so
    /// far can be entered exactly one way. Nine Mile Creek's mainland breaks that: you can sail into the
    /// wharf, or you can walk in over the tidal bar, and the two land 400 m apart. Arriving on foot and
    /// being put on the wharf deck is not a rough edge — it is the player being teleported across the
    /// region they just spent three minutes walking to.
    ///
    /// <para>So a <c>RegionPassage</c> now NAMES the arrival it lands at, the destination's
    /// <see cref="RegionAnchor"/> authors a table of named arrivals, and the key rides
    /// <see cref="GameServices.PendingArrivalKey"/> between them because App and World both reference
    /// only Core (rule 4). These tests pin the three things that make that additive rather than
    /// dangerous:</para>
    /// <list type="number">
    /// <item><description><b>The fallback is total.</b> No key, no table, a blank row, an unmatched name —
    /// every one of them lands at the region's own point, so no existing region changes behaviour.</description></item>
    /// <item><description><b>The two points fall back independently</b>, which is what lets the bar
    /// landing move the PLAYER without dragging the boat off its berth with them.</description></item>
    /// <item><description><b>⚠ The dock's step-off does NOT follow the way you came in.</b> The one real
    /// hazard in the whole seam: pass the per-passage point to <c>SetDock</c> and walking in over the bar
    /// silently re-points every LATER disembark at the bar landing — so a player who moors at the wharf
    /// steps off it onto a beach 400 m away, and nothing fails.</description></item>
    /// </list>
    ///
    /// <para>The key's own lifetime — published before travel, consumed once on arrival, taken back when
    /// the travel is declined — is <c>PerPassageArrivalPlayTests</c>'s, because it is a claim about a
    /// scene load.</para>
    /// </summary>
    public class PerPassageArrivalTests
    {
        private readonly List<Object> _spawned = new();

        private GameObject New(string n) { var g = new GameObject(n); _spawned.Add(g); return g; }
        private Transform At(string n, Vector3 p) { var g = New(n); g.transform.position = p; return g.transform; }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            GameServices.Reset();
        }

        // =============================================================================================
        //  1. the resolver — pure, and total
        // =============================================================================================

        [Test]
        public void AnEmptyTable_AnswersNothing_WhichIsHowEveryExistingRegionKeepsWorking()
        {
            Assert.IsFalse(RegionAnchor.TryFindArrival(null, "bar", out _),
                "a region that authors no table has one way in, and asking it for a second must not throw");
            Assert.IsFalse(RegionAnchor.TryFindArrival(new NamedArrival[0], "bar", out _));
        }

        [Test]
        public void ANullOrEmptyKey_NeverMatches_EvenAgainstAnAuthoredTable()
        {
            var table = new[] { new NamedArrival("bar", null, At("Landing", Vector3.zero)) };

            Assert.IsFalse(RegionAnchor.TryFindArrival(table, null, out _),
                "a keyless passage means 'the region's own arrival' — it must not fall into the first row");
            Assert.IsFalse(RegionAnchor.TryFindArrival(table, "", out _));
        }

        [Test]
        public void AnUnmatchedKey_FallsBack_RatherThanPickingSomethingClose()
        {
            var table = new[] { new NamedArrival("bar", null, At("Landing", Vector3.zero)) };
            Assert.IsFalse(RegionAnchor.TryFindArrival(table, "wharf", out _),
                "a name the region does not answer to must land at the default, not at whatever is nearest");
        }

        [Test]
        public void MatchingIsCaseInsensitive_BecauseTwoBuildersAuthorTheSameString()
        {
            Transform landing = At("Landing", new Vector3(1f, 2f, 0f));
            var table = new[] { new NamedArrival("bar", null, landing) };

            Assert.IsTrue(RegionAnchor.TryFindArrival(table, "BAR", out NamedArrival found));
            Assert.AreSame(landing, found.DisembarkPoint);
            Assert.IsTrue(RegionAnchor.TryFindArrival(table, "Bar", out _));
        }

        [Test]
        public void ARowWithABlankKey_IsSkipped_NotUsedAsACatchAll()
        {
            // An authored-but-unfinished row is the realistic way a blank key gets into the table. If it
            // matched a blank lookup, every keyless passage into this region would start landing at it.
            Transform stray = At("Unfinished", new Vector3(99f, 99f, 0f));
            var table = new[]
            {
                new NamedArrival("", null, stray),
                new NamedArrival("bar", null, At("Landing", Vector3.zero)),
            };

            Assert.IsFalse(RegionAnchor.TryFindArrival(table, "", out _),
                "a blank key is 'no key', and no key means the region's own arrival");
            Assert.IsTrue(RegionAnchor.TryFindArrival(table, "bar", out NamedArrival found));
            Assert.AreNotSame(stray, found.DisembarkPoint);
        }

        // =============================================================================================
        //  2. the anchor's accessors — the two points fall back INDEPENDENTLY
        // =============================================================================================

        private RegionAnchor MakeAnchor(Vector3 arrival, Vector3 dock, Vector3 disembark)
        {
            var anchor = New("RegionAnchor").AddComponent<RegionAnchor>();
            anchor.Configure("region.test",
                             At("Arrival", arrival), At("Dock", dock), At("Disembark", disembark));
            return anchor;
        }

        [Test]
        public void WithNoTable_EveryKeyResolvesToTheRegionsOwnPoints()
        {
            var anchor = MakeAnchor(new Vector3(101f, 84.5f, 0f), new Vector3(98f, 84.5f, 0f),
                                    new Vector3(98f, 88f, 0f));

            foreach (string key in new[] { null, "", "bar", "wharf" })
            {
                Assert.AreSame(anchor.ArrivalPoint, anchor.ArrivalPointFor(key), $"key '{key}'");
                Assert.AreSame(anchor.DisembarkPoint, anchor.DisembarkPointFor(key), $"key '{key}'");
            }
        }

        [Test]
        public void AWalkInArrival_MovesThePlayerAndLeavesTheBoatWhereItWas()
        {
            // ⭐ The mainland's real case, and the reason the two points fall back separately. You walked
            // across the bar; your boat is not with you. The named arrival sets only the FOOT point.
            var anchor = MakeAnchor(new Vector3(101f, 84.5f, 0f), new Vector3(98f, 84.5f, 0f),
                                    new Vector3(98f, 88f, 0f));
            Transform barLanding = At("BarLanding", new Vector3(58f, -146f, 0f));
            anchor.ConfigureArrivals(new NamedArrival("bar", null, barLanding));

            Assert.AreSame(barLanding, anchor.DisembarkPointFor("bar"),
                "walking in over the bar must put the player ashore at the bar, not on the wharf deck");
            Assert.AreSame(anchor.ArrivalPoint, anchor.ArrivalPointFor("bar"),
                "…and must NOT drag the boat across the region after them — an unset point on a named " +
                "arrival means 'leave this one alone', which is the whole reason it is two fields");
        }

        [Test]
        public void AWaterArrival_MovesTheBoatAndLeavesTheFootLandingWhereItWas()
        {
            // The mirror, so the independence is proved in both directions rather than assumed from one.
            var anchor = MakeAnchor(new Vector3(101f, 84.5f, 0f), new Vector3(98f, 84.5f, 0f),
                                    new Vector3(98f, 88f, 0f));
            Transform southBerth = At("SouthBerth", new Vector3(140f, 40f, 0f));
            anchor.ConfigureArrivals(new NamedArrival("south_mouth", southBerth, null));

            Assert.AreSame(southBerth, anchor.ArrivalPointFor("south_mouth"));
            Assert.AreSame(anchor.DisembarkPoint, anchor.DisembarkPointFor("south_mouth"));
        }

        [Test]
        public void HasArrival_ReportsWhatTheRegionActuallyAnswersTo()
        {
            var anchor = MakeAnchor(Vector3.zero, Vector3.zero, Vector3.zero);
            Assert.IsFalse(anchor.HasArrival("bar"), "before a table is authored the region has one way in");

            anchor.ConfigureArrivals(new NamedArrival("bar", null, At("Landing", Vector3.zero)));
            Assert.IsTrue(anchor.HasArrival("bar"));
            Assert.IsTrue(anchor.HasArrival("BAR"));
            Assert.IsFalse(anchor.HasArrival("wharf"),
                "the coordinator warns on an unanswered key, so this has to be exact — a loose 'yes' " +
                "turns a mis-wired passage back into a silent one");
        }

        // =============================================================================================
        //  3. the coordinator — where the rig actually lands
        // =============================================================================================

        private static readonly Vector3 BoatBerth = new Vector3(101f, 84.5f, 0f);
        private static readonly Vector3 WharfDock = new Vector3(98f, 84.5f, 0f);
        private static readonly Vector3 WharfDeck = new Vector3(98f, 88f, 0f);
        private static readonly Vector3 BarLanding = new Vector3(58f, -146f, 0f);
        private const float DockRadius = 3.5f;

        /// <summary>A real rig — a real switcher over a real boat and player — because the claim under
        /// test is about what the switcher is told, and a fake switcher cannot be told anything wrong.</summary>
        private ControlSwitcher MakeRig(out Transform player, out Transform boat, out RegionAnchor anchor)
        {
            var playerGo = New("Player");
            var walk = playerGo.AddComponent<PlayerWalkController>();   // + Rigidbody2D + SpriteRenderer
            player = playerGo.transform;

            var boatGo = New("Boat");
            var controller = boatGo.AddComponent<BoatController>();     // + Rigidbody2D + CapsuleCollider2D
            var input = boatGo.AddComponent<DevBoatInput>();
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.test"; hull.CameraWorldHeightMeters = 14f; hull.DraughtMeters = 0.3f;
            _spawned.Add(hull);
            controller.SetHull(hull);
            boat = boatGo.transform;

            anchor = MakeAnchor(BoatBerth, WharfDock, WharfDeck);
            anchor.ConfigureArrivals(new NamedArrival("bar", null, At("BarLanding", BarLanding)));

            var switcher = New("Switcher").AddComponent<ControlSwitcher>();
            switcher.Configure(walk, controller, input, null, DockRadius, null);
            return switcher;
        }

        [Test]
        public void ArrivingByTheBar_PutsTheFisherOnTheBar_AndTheBoatAtTheWharf()
        {
            var switcher = MakeRig(out Transform player, out Transform boat, out RegionAnchor anchor);

            RegionTravelCoordinator.ApplyArrival(player, boat, switcher, anchor, "bar");

            Assert.AreEqual(BarLanding, player.position,
                "the fisher walked in over the bar — they arrive standing on it");
            Assert.AreEqual(BoatBerth, boat.position,
                "and the boat, which they did not bring, is where the region parks boats");
        }

        [Test]
        public void ArrivingWithNoKey_IsExactlyWhatItAlwaysWas()
        {
            // The additive claim, driven rather than reasoned about: the four-argument overload every
            // existing caller uses must be byte-for-byte the old behaviour.
            var switcher = MakeRig(out Transform player, out Transform boat, out RegionAnchor anchor);

            RegionTravelCoordinator.ApplyArrival(player, boat, switcher, anchor);

            Assert.AreEqual(WharfDeck, player.position, "no key = the region's own disembark point");
            Assert.AreEqual(BoatBerth, boat.position, "no key = the region's own arrival point");
        }

        [Test]
        public void AnUnknownKey_LandsAtTheDefault_RatherThanNowhere()
        {
            var switcher = MakeRig(out Transform player, out Transform boat, out RegionAnchor anchor);

            RegionTravelCoordinator.ApplyArrival(player, boat, switcher, anchor, "no_such_way_in");

            Assert.AreEqual(WharfDeck, player.position);
            Assert.AreEqual(BoatBerth, boat.position);
        }

        [Test]
        public void SteppingOffTheBoatLater_StillLandsOnTheWharf_NotWhereYouWalkedIn()
        {
            // ⚠ THE TRAP THIS SEAM EXISTS AROUND. You walk in over the bar, then later board your boat at
            // the wharf and step off her. Where do you land? On the wharf — because the dock's step-off is
            // a property of the WHARF, not of the door you happened to come through an hour ago. Feeding
            // the per-passage point into SetDock would break this silently: every disembark for the rest
            // of the visit would fire you 400 m south onto the bar.
            var switcher = MakeRig(out Transform player, out Transform boat, out RegionAnchor anchor);

            RegionTravelCoordinator.ApplyArrival(player, boat, switcher, anchor, "bar");
            Assert.AreEqual(BarLanding, player.position, "precondition: we arrived on the bar");

            // Walk to the boat at her berth and board her.
            player.position = boat.position;
            Assert.IsTrue(switcher.TryInteract(), "board the boat at her berth");
            Assert.AreEqual(ControlMode.OnDeck, switcher.Mode);

            // Stand away from the tiller so E is a step ashore rather than taking the helm, and confirm
            // the boat is genuinely at the dock (which is what makes the tidy landing apply at all).
            player.position = boat.position + new Vector3(0f, 2.5f, 0f);
            Assert.IsFalse(switcher.WithinHelmReach(), "…standing on deck, not at the helm");
            Assert.IsTrue(switcher.InDockZone(), "…and she is lying at the wharf");

            Assert.IsTrue(switcher.TryInteract(), "step ashore");
            Assert.AreEqual(ControlMode.OnFoot, switcher.Mode);
            Assert.AreEqual(WharfDeck, player.position,
                "stepping off the boat at the wharf must land on the WHARF. Landing back at the bar " +
                "would mean the way you entered the region had quietly become the way you leave every " +
                "boat in it");
        }

        // =============================================================================================
        //  4. the relay itself — consume-once
        // =============================================================================================

        [Test]
        public void TheKeyIsConsumedOnce_SoItCannotSteerTheNextCrossingToo()
        {
            GameServices.PendingArrivalKey = "bar";

            Assert.AreEqual("bar", GameServices.ConsumePendingArrivalKey());
            Assert.IsNull(GameServices.PendingArrivalKey,
                "reading the key must take it — a key left standing outlives the crossing that set it");
            Assert.IsNull(GameServices.ConsumePendingArrivalKey(), "and a second read finds nothing");
        }

        [Test]
        public void ResetClearsTheKey_LikeEveryOtherSceneScopedService()
        {
            GameServices.PendingArrivalKey = "bar";
            GameServices.Reset();
            Assert.IsNull(GameServices.PendingArrivalKey,
                "a torn-down rig that leaves a key behind hands it to the next test / the next session");
        }
    }
}
