using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>HER BERTH BEATS THE STANDOFF</b> — owner ruling, 2026-09-08.
    ///
    /// <para><b>The defect, measured on a real editor slot rather than argued.</b> In play at St Peters
    /// there is exactly ONE <see cref="BoatController"/> — the persistent dory — and she sits at
    /// <c>ArrivalPos</c> (213.50, −5.80), not at the (213.50, 4.25) the scene and the builder bank.
    /// <c>RegionTravelCoordinator.OnActiveSceneChanged</c> → <c>ApplyArrival</c> parks the boat on the
    /// region's arrival point on EVERY entry, and St Peters' arrival point is the alongside berth plus a
    /// 2 m standoff — on the moored cape islander's own berth line. She is 12.9 m long, so her outline
    /// spans x 205.05..217.95 and simply CONTAINS the parked dory: a signed gap of <b>−3.25 m</b>, wholly
    /// inside, and invisible behind her sprite. <b>The standoff was sized against a POINT, not a
    /// HULL.</b></para>
    ///
    /// <para><b>Why three PRs missed it.</b> #677, #707 and #790 all moved or measured the BANKED berth —
    /// a position <c>ApplyArrival</c> overwrote on the way in. #790's table is correct arithmetic about a
    /// place the game does not use. The lesson is one line: <i>ask where the boat is in PLAY, not where
    /// the scene banks her.</i></para>
    ///
    /// <para><b>What the fix is.</b> The region remembers the pose its own scene laid the boat in — the
    /// one moment it exists, before <c>PersistentObject</c>'s Awake has promoted her out of the scene for
    /// good — and every later arrival returns her to it, position AND heading. A region first entered BY
    /// SEA never records one and falls through to the arrival point exactly as it always has.</para>
    /// </summary>
    public class ArrivalParksHerAtHerBerthTests
    {
        private readonly List<Object> _spawned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            GameServices.Reset();
        }

        // ---- the rule ---------------------------------------------------------------------------

        /// <summary>Where the region laid her, she returns to — the whole pose, not just the spot.</summary>
        [Test]
        public void WithABerthRemembered_SheIsParkedThere_PoseAndAll()
        {
            var rig = Build();
            AuthorHerInto(rig, RegionScene, new Vector3(213.50f, 4.25f, 0f), Quaternion.Euler(0f, 0f, 90f));
            rig.Anchor.RememberBoatBerth(rig.Boat, RegionScene);

            rig.Boat.SetPositionAndRotation(new Vector3(-400f, 900f, 0f), Quaternion.identity);  // off sailing
            RegionTravelCoordinator.ApplyArrival(rig.Player, rig.Boat, rig.Switcher, rig.Anchor);

            Assert.AreEqual(213.50f, rig.Boat.position.x, 1e-4f, "she is back at her berth, not the standoff");
            Assert.AreEqual(4.25f, rig.Boat.position.y, 1e-4f);
            Assert.AreEqual(90f, rig.Boat.eulerAngles.z, 0.5f,
                "…lying on the heading the region laid her on. A berth is a position AND a heading, and " +
                "leaving the second to the identity is what put a 4.5 m boat athwart this fairway on " +
                "2026-09-02.");
        }

        /// <summary>
        /// ⚠ The fallback, and it is the larger half of this change: a region that authors no berth
        /// behaves EXACTLY as it always has. Nine Mile Creek and West Water are first entered by sea, so
        /// they never record one.
        /// </summary>
        [Test]
        public void WithNoBerthRemembered_SheIsParkedAtTheArrivalPoint_ExactlyAsBefore()
        {
            var rig = Build();
            Assert.IsFalse(rig.Anchor.HasAuthoredBoatBerth, "harness: nothing was remembered");

            rig.Boat.position = new Vector3(-400f, 900f, 0f);
            RegionTravelCoordinator.ApplyArrival(rig.Player, rig.Boat, rig.Switcher, rig.Anchor);

            Assert.AreEqual(ArrivalSpot.x, rig.Boat.position.x, 1e-4f, "the arrival point, to 1e-4");
            Assert.AreEqual(ArrivalSpot.y, rig.Boat.position.y, 1e-4f);
        }

        /// <summary>
        /// ⚠ <b>Only the FIRST remembering takes.</b> Without this, the arrival that has just parked her
        /// on the standoff would record the standoff as her berth on the next entry, and the defect would
        /// re-seal itself one hop later — silently, and looking exactly like a fix.
        /// </summary>
        [Test]
        public void ASecondRemembering_CannotOverwriteTheBerthWithTheStandoff()
        {
            var rig = Build();
            AuthorHerInto(rig, RegionScene, new Vector3(213.50f, 4.25f, 0f), Quaternion.Euler(0f, 0f, 90f));
            rig.Anchor.RememberBoatBerth(rig.Boat, RegionScene);

            // …and now she has been parked on the standoff, and the region is asked again.
            AuthorHerInto(rig, RegionScene, ArrivalSpot, Quaternion.identity);
            rig.Anchor.RememberBoatBerth(rig.Boat, RegionScene);

            Assert.AreEqual(4.25f, rig.Anchor.AuthoredBoatBerthPosition.y, 1e-4f,
                "the berth is still the berth — a later call must not record where an arrival parked her");
        }

        // ---- ⭐ what "AUTHORED" has to mean, and the defect that taught it -----------------------

        /// <summary>
        /// ⭐⭐ <b>A boat nobody authored into this region has NO berth — she falls through to the arrival
        /// point exactly as today.</b>
        ///
        /// <para><b>This is the case four PlayMode fixtures caught on 2026-09-08</b>
        /// (<c>PerPassageArrivalPlayTests</c> ×2, <c>WestWaterSailPlayTests</c> ×2), all with the same
        /// tell: the boat arrived at <b>(0, 0, 0)</b>, 131.7 m off the mark. The first draft of this rule
        /// remembered "wherever she was the first time we saw her" — and in a fixture, and in any boot
        /// order where the boat exists before her transform is applied, that is the ORIGIN. The region
        /// recorded (0,0,0) as her berth and every later arrival parked her there, keyed or not, because
        /// a berth WAS remembered and the fallback was never reached.</para>
        ///
        /// <para><b>The fix is not a guard against the origin</b> — a boat legitimately authored at
        /// (0,0,0) would then be refused and the rule would still be a guess. It is a SOURCE: the berth
        /// is the pose <see cref="PersistentObject"/> recorded in its own Awake, the instant before
        /// promotion, together with the scene that serialized it. A boat a fixture merely spawned carries
        /// no such record.</para>
        ///
        /// <para>And it is proved HERE, in EditMode, where <c>ApplyArrival</c> is a pure static — so this
        /// case never again needs a PlayMode run to hold it.</para>
        /// </summary>
        [Test]
        public void ABoatNobodyAuthored_RemembersNoBerth_AndLandsAtTheArrivalPoint()
        {
            var rig = Build();                       // her boat carries no PersistentObject at all
            rig.Boat.position = Vector3.zero;        // …and sits at the origin, as the fixtures' did

            rig.Anchor.RememberBoatBerth(rig.Boat, RegionScene);

            Assert.IsFalse(rig.Anchor.HasAuthoredBoatBerth,
                "an unauthored boat at the origin must NOT become this region's berth — that is the " +
                "defect the four PlayMode fixtures caught");

            RegionTravelCoordinator.ApplyArrival(rig.Player, rig.Boat, rig.Switcher, rig.Anchor);
            Assert.AreEqual(ArrivalSpot.x, rig.Boat.position.x, 1e-4f, "so she lands at the arrival point");
            Assert.AreEqual(ArrivalSpot.y, rig.Boat.position.y, 1e-4f);
        }

        /// <summary>A boat ANOTHER region serialized is not this region's mooring either — the scene name
        /// is checked, not merely the presence of a record.</summary>
        [Test]
        public void ABoatAuthoredInAnotherRegion_IsNotThisRegionsBerth()
        {
            var rig = Build();
            AuthorHerInto(rig, "SomeOtherRegion", new Vector3(213.50f, 4.25f, 0f), Quaternion.identity);

            rig.Anchor.RememberBoatBerth(rig.Boat, RegionScene);

            Assert.IsFalse(rig.Anchor.HasAuthoredBoatBerth,
                "the berth belongs to the scene that laid her, not to whichever region asks first");
        }

        /// <summary>The berth is the SERIALIZED pose, not wherever she is standing when the region is
        /// asked — so a boot order that moves her before the first activation cannot poison it.</summary>
        [Test]
        public void TheBerthIsTheAuthoredPose_NotWhereverSheIsStandingNow()
        {
            var rig = Build();
            AuthorHerInto(rig, RegionScene, new Vector3(213.50f, 4.25f, 0f), Quaternion.Euler(0f, 0f, 90f));
            rig.Boat.position = new Vector3(-999f, -999f, 0f);   // something moved her before we were asked

            rig.Anchor.RememberBoatBerth(rig.Boat, RegionScene);

            Assert.IsTrue(rig.Anchor.HasAuthoredBoatBerth);
            Assert.AreEqual(4.25f, rig.Anchor.AuthoredBoatBerthPosition.y, 1e-4f,
                "the pose the SCENE serialized, not the one she was standing in");
        }

        // ---- the St Peters numbers, against the hull she was parked inside ----------------------

        /// <summary>
        /// ⭐ The defect and the fix in one measurement, on the region's own constants: at the ARRIVAL
        /// POINT the dory is inside the cape's outline; at her BERTH she is clear of it by more than the
        /// fendering gap. The negative control is the shipped behaviour, so this cannot pass on it.
        /// </summary>
        [Test]
        public void AtStPeters_TheArrivalPointIsInsideTheCape_AndTheBerthIsClearOfHer()
        {
            HullFootprint cape = HullFootprint.FromHeading(
                new Vector2(StPetersBuilder.DockZonePos.x, StPetersBuilder.DockZonePos.y),
                StPetersArrivalOpening.BerthHeadingDegrees(),
                StPetersBuilder.ArrivalHullLengthMetres, StPetersBuilder.ArrivalHullHalfBeamMetres);

            HullFootprint atArrival = Dory(StPetersBuilder.ArrivalPos);
            HullFootprint atBerth = Dory(StPetersBuilder.DoryMooredPos);

            float gapAtArrival = cape.SignedGapTo(atArrival);
            float gapAtBerth = cape.SignedGapTo(atBerth);

            Assert.Less(gapAtArrival, 0f,
                $"the negative control: parked on ArrivalPos the dory is {gapAtArrival:0.000} m INTO the " +
                "moored cape. If this ever goes positive the standoff has been widened and this whole " +
                "fixture is measuring a defect that no longer exists.");
            Assert.IsTrue(cape.Contains(atArrival.Center),
                "…her centre is inside the cape's outline, which is what made her invisible on the plate");

            Assert.Greater(gapAtBerth, StPetersBuilder.AlongsideFenderGapMetres,
                $"at her berth she clears the cape by {gapAtBerth:0.000} m, and a moored boat is owed at " +
                $"least the fendering gap ({StPetersBuilder.AlongsideFenderGapMetres:0.00} m)");

            Debug.Log($"[her-berth] St Peters: ArrivalPos {StPetersBuilder.ArrivalPos} gives " +
                      $"{gapAtArrival:F3} m (INSIDE her); the banked berth " +
                      $"{StPetersBuilder.DoryMooredPos} gives {gapAtBerth:F3} m.");
        }

        /// <summary>The two places are genuinely different — pinned so a future edit that quietly makes
        /// the banked berth equal the arrival point cannot pass the case above by collapsing it.</summary>
        [Test]
        public void TheBankedBerthAndTheArrivalPoint_AreNotTheSamePlace()
        {
            float apart = Vector3.Distance(StPetersBuilder.DoryMooredPos, StPetersBuilder.ArrivalPos);
            Assert.Greater(apart, 1f,
                $"the berth and the standoff are {apart:0.000} m apart; if they ever collapse together " +
                "this fix has nothing left to choose between");
        }

        // ---- the rig ----------------------------------------------------------------------------

        private const string RegionScene = "TestRegionScene";
        private static readonly Vector3 ArrivalSpot = new Vector3(50f, -7f, 0f);
        private static readonly Vector3 LandingSpot = new Vector3(50f, -2f, 0f);

        private static HullFootprint Dory(Vector3 at) => HullFootprint.FromHeading(
            new Vector2(at.x, at.y), StPetersBuilder.DoryMooredHeadingDegrees,
            StPetersBuilder.DoryLengthMetres, StPetersBuilder.DoryHalfBeamMetres);

        private struct Rig
        {
            public Transform Player, Boat;
            public ControlSwitcher Switcher;
            public RegionAnchor Anchor;
        }

        private Rig Build()
        {
            var playerGo = New("Player");
            var walk = playerGo.AddComponent<PlayerWalkController>();

            var boatGo = New("Boat");
            var controller = boatGo.AddComponent<BoatController>();   // + Rigidbody2D + BoatMooring
            var input = boatGo.AddComponent<DevBoatInput>();
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            _spawned.Add(hull);
            hull.Id = "boat.dory"; hull.LengthMeters = 4.5f; hull.CameraWorldHeightMeters = 14f;
            controller.SetHull(hull);

            var anchorGo = New("RegionAnchor");
            var anchor = anchorGo.AddComponent<RegionAnchor>();
            anchor.Configure("region.test", At("Arrival", ArrivalSpot), At("Dock", ArrivalSpot),
                             At("Landing", LandingSpot));

            var switcher = New("Switcher").AddComponent<ControlSwitcher>();
            switcher.Configure(walk, controller, input, null, 3.5f, null);

            return new Rig
            {
                Player = playerGo.transform, Boat = boatGo.transform,
                Switcher = switcher, Anchor = anchor,
            };
        }

        /// <summary>Give her the record a region scene's own serialization would have left on her —
        /// <see cref="PersistentObject"/>'s Awake-time capture, which EditMode never runs.</summary>
        private static void AuthorHerInto(Rig rig, string sceneName, Vector3 pos, Quaternion rot)
        {
            var persistent = rig.Boat.GetComponent<PersistentObject>()
                             ?? rig.Boat.gameObject.AddComponent<PersistentObject>();
            persistent.ConfigureAuthoredPose(sceneName, pos, rot);
            rig.Boat.SetPositionAndRotation(pos, rot);
        }

        private Transform At(string name, Vector3 pos) => New(name, pos).transform;

        private GameObject New(string name) => New(name, Vector3.zero);

        private GameObject New(string name, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            _spawned.Add(go);
            return go;
        }
    }
}
