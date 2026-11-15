using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Crew slice R2: the pure voyage calculator, the bunk count and the in-memory roster, on hand-built
    /// specs and roles so each rule is pinned on its own. The shipped data is checked in
    /// <see cref="CrewVoyageContentTests"/>.
    /// </summary>
    public class CrewVoyageLimitsTests
    {
        private const string Skipper = "role.skipper";
        private const string Deckhand = "role.deckhand";
        private const string Winchman = "role.winchman";

        private static StaffRoleBook Roles() => StaffRoleBook.FromPairs(new[]
        {
            (Skipper, new[] { StaffRoleBook.HelmTask, "task.navigate" }, new string[0]),
            (Deckhand, new[] { "task.haul_pots" }, new[] { "work.rod_and_jig" }),
            (Winchman, new[] { "task.winch" }, new[] { "work.otter_trawl" }),
        });

        private static VoyageSpec Spec(int slots, int bunks, TripKind longest = TripKind.Month,
            int minDay = 1, params VoyageWorkRequirement[] work)
            => new VoyageSpec("boat.test", slots, bunks, longest, new[] { minDay, 1, 1, 1 }, work);

        private static VoyageWorkRequirement Trawl(int min, params string[] roles)
            => new VoyageWorkRequirement { WorkId = "work.otter_trawl", MinCrew = min, RequiredRoleIds = roles };

        private static VoyageVerdict Check(VoyageSpec spec, TripKind trip, CrewPolicy policy, string work = null,
            VoyageSupplies supplies = default, params CrewMember[] crew)
            => VoyageLimits.Check(spec, crew, new VoyagePlan(trip, work, supplies), Roles(), policy);

        private static CrewMember P(string id, string role = Skipper) => CrewMember.Player(id, role);
        private static CrewMember H(string id, string role = Deckhand) => CrewMember.Hand(id, role);

        [Test]
        public void SoloSkipper_MayTakeADayTrip()
        {
            var v = Check(Spec(2, 0), TripKind.Day, CrewPolicy.Default, null, default, P("p1"));
            Assert.That(v.Allowed, Is.True, v.ToString());
            Assert.That(v.DayMax, Is.EqualTo(2));
        }

        [Test]
        public void UnknownHull_IsRefused() =>
            Assert.That(VoyageLimits.Check(null, new[] { P("p1") }, new VoyagePlan(TripKind.Day), Roles(),
                CrewPolicy.Default).Reason, Is.EqualTo(VoyageRefusal.UnknownHull));

        [Test]
        public void NoCrew_IsRefused() =>
            Assert.That(Check(Spec(2, 1), TripKind.Day, CrewPolicy.Default).Reason, Is.EqualTo(VoyageRefusal.NoCrew));

        [Test]
        public void DayLimit_IsTheCrewSlots()
        {
            var spec = Spec(2, 4);
            Assert.That(Check(spec, TripKind.Day, CrewPolicy.Default, null, default, P("a"), H("b")).Allowed, Is.True);
            Assert.That(Check(spec, TripKind.Day, CrewPolicy.Default, null, default, P("a"), H("b"), H("c")).Reason,
                Is.EqualTo(VoyageRefusal.OverCrewSlots));
        }

        [Test]
        public void HullWithNoBunk_CannotGoOvernight_EvenWhenItsHonestTripIsLonger()
        {
            var v = Check(Spec(4, 0, TripKind.Month), TripKind.Overnight, CrewPolicy.Default, null, default, P("a"));
            Assert.That(v.Reason, Is.EqualTo(VoyageRefusal.NoBunks));
            Assert.That(v.OvernightMax, Is.EqualTo(0));
        }

        [Test]
        public void TripLongerThanTheHullGoes_IsRefused() =>
            Assert.That(Check(Spec(3, 2, TripKind.Overnight), TripKind.Week, CrewPolicy.Default, null, default, P("a")).Reason,
                Is.EqualTo(VoyageRefusal.TripLongerThanHullAllows));

        [Test]
        public void PlayersNeverShareABunk_EvenWithHandSharingOn()
        {
            var share = CrewPolicy.Default;
            share.HiredHandsShareBunks = true;
            var spec = Spec(4, 1);
            Assert.That(Check(spec, TripKind.Overnight, share, null, default, P("a"), P("b", Deckhand)).Reason,
                Is.EqualTo(VoyageRefusal.PlayersOutnumberBunks));
        }

        [Test]
        public void HiredHandsSharing_IsOnByDefault_AndEachSettingCountsHandPlacesItsWay()
        {
            Assert.That(CrewPolicy.Default.HiredHandsShareBunks, Is.True, "owner ruling 2026-11-15: hands share in watches");
            var spec = Spec(5, 2);
            CrewMember[] crew = { P("a"), H("b"), H("c") };

            var share = CrewPolicy.Default;
            share.HiredHandsShareBunks = true;
            var on = Check(spec, TripKind.Overnight, share, null, default, crew);
            Assert.That(on.Allowed, Is.True, on.ToString());
            Assert.That(on.OvernightMax, Is.EqualTo(3), "players + 2 x (bunks - players) = 1 + 2 x 1");
            Assert.That(Check(spec, TripKind.Overnight, share, null, default, P("a"), H("b"), H("c"), H("d")).Reason,
                Is.EqualTo(VoyageRefusal.CrewOutnumberBunks));

            var off = CrewPolicy.Default;
            off.HiredHandsShareBunks = false;
            var refused = Check(spec, TripKind.Overnight, off, null, default, crew);
            Assert.That(refused.Reason, Is.EqualTo(VoyageRefusal.CrewOutnumberBunks));
            Assert.That(refused.OvernightMax, Is.EqualTo(2));
        }

        [Test]
        public void OvernightMax_FollowsTheFormula_AndNeverExceedsTheCrewSlots()
        {
            var off = CrewPolicy.Default;
            off.HiredHandsShareBunks = false;
            var share = CrewPolicy.Default;
            share.HiredHandsShareBunks = true;
            for (int slots = 0; slots <= 10; slots++)
                for (int bunks = 0; bunks <= 10; bunks++)
                    for (int players = 0; players <= 3; players++)
                    {
                        var spec = Spec(slots, bunks);
                        Assert.That(spec.OvernightMax(off, players), Is.EqualTo(System.Math.Min(slots, bunks)));
                        int own = System.Math.Min(players, bunks);
                        Assert.That(spec.OvernightMax(share, players),
                            Is.EqualTo(System.Math.Min(slots, own + share.HandsPerSharedBunk * (bunks - own))));
                        Assert.That(spec.OvernightMax(share, players), Is.LessThanOrEqualTo(slots));
                        Assert.That(spec.OvernightMax(share, players), Is.LessThanOrEqualTo(bunks * share.HandsPerSharedBunk));
                    }
        }

        [Test]
        public void SomebodyMustBeAbleToSteer() =>
            Assert.That(Check(Spec(2, 0), TripKind.Day, CrewPolicy.Default, null, default, P("a", Deckhand)).Reason,
                Is.EqualTo(VoyageRefusal.NoOneToHelm));

        [Test]
        public void MinimumCrewForTheTripKind_IsEnforced() =>
            Assert.That(Check(Spec(3, 0, TripKind.Day, minDay: 2), TripKind.Day, CrewPolicy.Default, null, default, P("a")).Reason,
                Is.EqualTo(VoyageRefusal.BelowMinimumCrew));

        [Test]
        public void UnknownRoleAndDuplicateMember_AreRefused()
        {
            Assert.That(Check(Spec(3, 0), TripKind.Day, CrewPolicy.Default, null, default, P("a", "role.nobody")).Reason,
                Is.EqualTo(VoyageRefusal.UnknownRole));
            Assert.That(Check(Spec(3, 0), TripKind.Day, CrewPolicy.Default, null, default, P("a"), H("a")).Reason,
                Is.EqualTo(VoyageRefusal.InvalidMember));
        }

        [Test]
        public void Work_MustBeFitted_HaveItsHeadCount_AndItsRoles()
        {
            var spec = Spec(5, 0, TripKind.Day, 1, Trawl(2, Skipper, Winchman));
            var policy = CrewPolicy.Default;
            Assert.That(Check(spec, TripKind.Day, policy, "work.scallop_drag", default, P("a"), H("b", Winchman)).Reason,
                Is.EqualTo(VoyageRefusal.WorkNotFittedToHull));
            Assert.That(Check(spec, TripKind.Day, policy, "work.otter_trawl", default, P("a")).Reason,
                Is.EqualTo(VoyageRefusal.BelowWorkMinimum));
            var missing = Check(spec, TripKind.Day, policy, "work.otter_trawl", default, P("a"), H("b"));
            Assert.That(missing.Reason, Is.EqualTo(VoyageRefusal.MissingRequiredRole));
            Assert.That(missing.Detail, Is.EqualTo(Winchman));
            Assert.That(Check(spec, TripKind.Day, policy, "work.otter_trawl", default, P("a"), H("b", Winchman)).Allowed, Is.True);
        }

        [Test]
        public void ARoleListedTwice_NeedsTwoPeople()
        {
            var spec = Spec(5, 0, TripKind.Day, 1, Trawl(2, Winchman, Winchman, Skipper));
            Assert.That(Check(spec, TripKind.Day, CrewPolicy.Default, "work.otter_trawl", default, P("a"), H("b", Winchman)).Reason,
                Is.EqualTo(VoyageRefusal.MissingRequiredRole));
            Assert.That(Check(spec, TripKind.Day, CrewPolicy.Default, "work.otter_trawl", default,
                P("a"), H("b", Winchman), H("c", Winchman)).Allowed, Is.True);
        }

        [Test]
        public void MonthTrip_IsOpenToASoloPlayerByDefault_AndNeedsAHiredSkipperOnlyWhenSwitchedOn()
        {
            var spec = Spec(6, 6, TripKind.Month);
            Assert.That(CrewPolicy.Default.MonthTripsNeedHiredSkipper, Is.False, "owner ruling 2026-11-15: solo with resupply");
            Assert.That(Check(spec, TripKind.Month, CrewPolicy.Default, null, default, P("a")).Allowed, Is.True);

            var strict = CrewPolicy.Default;
            strict.MonthTripsNeedHiredSkipper = true;
            Assert.That(Check(spec, TripKind.Month, strict, null, default, P("a")).Reason,
                Is.EqualTo(VoyageRefusal.MonthNeedsHiredSkipper));
            Assert.That(Check(spec, TripKind.Month, strict, null, default, P("a", Deckhand), H("s", Skipper)).Allowed,
                Is.True);
        }

        [Test]
        public void Supplies_AreCheckedOnlyWhenTracked()
        {
            var spec = Spec(4, 4);
            var policy = CrewPolicy.Default;
            CrewMember[] crew = { P("a"), H("b") };
            Assert.That(Check(spec, TripKind.Overnight, policy, null, VoyageSupplies.Untracked, crew).Allowed, Is.True);

            float days = policy.NightsFor(TripKind.Overnight) + 1;
            var enough = new VoyageSupplies { StoresPersonDays = 2 * days, WaterPersonDays = 2 * days, IceDays = days, FuelGameDays = days };
            Assert.That(Check(spec, TripKind.Overnight, policy, null, enough, crew).Allowed, Is.True);

            var s = enough; s.StoresPersonDays = 2 * days - 0.5f;
            Assert.That(Check(spec, TripKind.Overnight, policy, null, s, crew).Reason, Is.EqualTo(VoyageRefusal.ShortOfStores));
            s = enough; s.WaterPersonDays = 0f;
            Assert.That(Check(spec, TripKind.Overnight, policy, null, s, crew).Reason, Is.EqualTo(VoyageRefusal.ShortOfWater));
            s = enough; s.IceDays = 0f;
            Assert.That(Check(spec, TripKind.Overnight, policy, null, s, crew).Reason, Is.EqualTo(VoyageRefusal.ShortOfIce));
            s = enough; s.FuelGameDays = 0.5f;
            Assert.That(Check(spec, TripKind.Overnight, policy, null, s, crew).Reason, Is.EqualTo(VoyageRefusal.ShortOfFuel));

            var empty = new VoyageSupplies { StoresPersonDays = 0f, WaterPersonDays = 0f, IceDays = 0f };
            Assert.That(Check(spec, TripKind.Day, policy, null, empty, crew).Allowed, Is.True, "a day trip eats ashore");
        }

        [Test]
        public void TripKindFor_NamesTripsByNights()
        {
            var p = CrewPolicy.Default;
            Assert.That(p.TripKindFor(0), Is.EqualTo(TripKind.Day));
            Assert.That(p.TripKindFor(1), Is.EqualTo(TripKind.Overnight));
            Assert.That(p.TripKindFor(p.OvernightNights), Is.EqualTo(TripKind.Overnight));
            Assert.That(p.TripKindFor(p.OvernightNights + 1), Is.EqualTo(TripKind.Week));
            Assert.That(p.TripKindFor(p.WeekNights + 1), Is.EqualTo(TripKind.Month));
        }

        [Test]
        public void Verdict_IsDeterministic_AndIgnoresCrewOrder()
        {
            var spec = Spec(5, 2, TripKind.Week, 1, Trawl(2, Skipper, Winchman));
            var crew = new List<CrewMember> { P("zed"), H("amy", Winchman), H("bob"), H("cat"), P("dan", Deckhand) };
            var policy = CrewPolicy.Default;
            var first = VoyageLimits.Check(spec, crew, new VoyagePlan(TripKind.Overnight, "work.otter_trawl"), Roles(), policy);
            var rng = new System.Random(1234);
            for (int i = 0; i < 50; i++)
            {
                var shuffled = crew.OrderBy(_ => rng.Next()).ToList();
                var again = VoyageLimits.Check(spec, shuffled, new VoyagePlan(TripKind.Overnight, "work.otter_trawl"), Roles(), policy);
                Assert.That(again.Allowed, Is.EqualTo(first.Allowed));
                Assert.That(again.Reason, Is.EqualTo(first.Reason));
                Assert.That(again.Detail, Is.EqualTo(first.Detail));
                Assert.That(again.OvernightMax, Is.EqualTo(first.OvernightMax));
            }
            Assert.That(first.Reason, Is.EqualTo(VoyageRefusal.PlayersOutnumberBunks).Or.EqualTo(VoyageRefusal.CrewOutnumberBunks));
        }

        // ---- the bunk count -------------------------------------------------------------------

        private static BoatInteriorDef Interior(bool usableLevel, params string[] actions)
        {
            var def = ScriptableObject.CreateInstance<BoatInteriorDef>();
            def.Levels = usableLevel
                ? new[] { new BoatInteriorLevel { Id = "cabin", Outline = new[] { Vector2.zero, Vector2.right, Vector2.up } } }
                : new BoatInteriorLevel[0];
            def.Anchors = actions.Select((a, i) => new BoatInteriorAnchor { Id = "a" + i, Action = a }).ToArray();
            return def;
        }

        [Test]
        public void BunkCount_CountsSleepAnchorsOnly_AndNoneWithoutAUsableLevel()
        {
            var two = Interior(true, BoatInteriorAnchor.ActionSleep, "cook", BoatInteriorAnchor.ActionSleep, "storage");
            var unusable = Interior(false, BoatInteriorAnchor.ActionSleep);
            try
            {
                Assert.That(BunkCount.Of(two), Is.EqualTo(2));
                Assert.That(BunkCount.Of(unusable), Is.EqualTo(0), "the Go-Fast case: Levels: [] builds no bunk");
                Assert.That(BunkCount.Of(null), Is.EqualTo(0));
            }
            finally
            {
                Object.DestroyImmediate(two);
                Object.DestroyImmediate(unusable);
            }
        }

        // ---- the roster ------------------------------------------------------------------------

        private static CrewRoster Roster(CrewPolicy? policy = null)
        {
            var catalog = new VoyageSpecCatalog(new[]
            {
                new VoyageSpec("boat.small", 2, 1, TripKind.Overnight, null, null),
                new VoyageSpec("boat.other", 3, 0, TripKind.Day, null, null),
            });
            CrewPolicy p = policy ?? CrewPolicy.Default;
            return new CrewRoster(() => catalog, Roles, () => p);
        }

        [Test]
        public void Roster_RefusesOverCapacity_WithTheRightReason()
        {
            var roster = Roster();
            Assert.That(roster.TryBoard("b1", "boat.small", P("a")).Allowed, Is.True);
            Assert.That(roster.TryBoard("b1", "boat.small", H("h1")).Allowed, Is.True);
            var full = roster.TryBoard("b1", "boat.small", H("h2"));
            Assert.That(full.Allowed, Is.False);
            Assert.That(full.Reason, Is.EqualTo(VoyageRefusal.OverCrewSlots));
            Assert.That(full.DayMax, Is.EqualTo(2));
            Assert.That(roster.Aboard("b1").Count, Is.EqualTo(2));

            Assert.That(roster.Leave("h1"), Is.True);
            Assert.That(roster.TryBoard("b1", "boat.small", H("h2")).Allowed, Is.True, "a freed slot can be taken");
        }

        [Test]
        public void Roster_RefusesUnknownHullUnknownRoleAndDoubleBoarding()
        {
            var roster = Roster();
            Assert.That(roster.TryBoard("b1", "boat.nope", P("a")).Reason, Is.EqualTo(VoyageRefusal.UnknownHull));
            Assert.That(roster.TryBoard("b1", "boat.small", P("a", "role.nobody")).Reason, Is.EqualTo(VoyageRefusal.UnknownRole));
            Assert.That(roster.TryBoard("", "boat.small", P("a")).Reason, Is.EqualTo(VoyageRefusal.InvalidBoat));
            Assert.That(roster.TryBoard("b1", "boat.small", P("")).Reason, Is.EqualTo(VoyageRefusal.InvalidMember));

            Assert.That(roster.TryBoard("b1", "boat.small", P("a")).Allowed, Is.True);
            var twice = roster.TryBoard("b2", "boat.other", P("a"));
            Assert.That(twice.Reason, Is.EqualTo(VoyageRefusal.AlreadyAboard));
            Assert.That(twice.Detail, Is.EqualTo("b1"));
            Assert.That(roster.TryBoard("b1", "boat.other", H("h")).Reason, Is.EqualTo(VoyageRefusal.BoatHullMismatch));
        }

        [Test]
        public void Roster_TracksWhoIsWhere_AndForgetsEmptyBoats()
        {
            var roster = Roster();
            roster.TryBoard("b1", "boat.small", P("a"));
            Assert.That(roster.TryGetBoatOf("a", out string key), Is.True);
            Assert.That(key, Is.EqualTo("b1"));
            Assert.That(roster.HullOf("b1"), Is.EqualTo("boat.small"));
            Assert.That(roster.Leave("a"), Is.True);
            Assert.That(roster.Leave("a"), Is.False);
            Assert.That(roster.HullOf("b1"), Is.Null);
            Assert.That(roster.Aboard("b1"), Is.Empty);
            Assert.That(roster.TryBoard("b1", "boat.other", P("a")).Allowed, Is.True, "an emptied boat key is free again");
            roster.Clear();
            Assert.That(roster.TryGetBoatOf("a", out _), Is.False);
        }

        [Test]
        public void Roster_CanSail_AppliesTheNightRules()
        {
            var noShare = CrewPolicy.Default;
            noShare.HiredHandsShareBunks = false;
            var roster = Roster(noShare);
            roster.TryBoard("b1", "boat.small", P("a"));
            roster.TryBoard("b1", "boat.small", H("h"));
            Assert.That(roster.CanSail("b1", new VoyagePlan(TripKind.Day)).Allowed, Is.True);
            Assert.That(roster.CanSail("b1", new VoyagePlan(TripKind.Overnight)).Reason, Is.EqualTo(VoyageRefusal.CrewOutnumberBunks));
            Assert.That(roster.CanSail("nobody", new VoyagePlan(TripKind.Day)).Reason, Is.EqualTo(VoyageRefusal.NoCrew));

            var share = CrewPolicy.Default;
            share.HiredHandsShareBunks = true;
            var sharing = Roster(share);
            sharing.TryBoard("b1", "boat.small", P("a"));
            sharing.TryBoard("b1", "boat.small", H("h"));
            // One bunk, the player's: there is no bunk left over for the hand to share.
            Assert.That(sharing.CanSail("b1", new VoyagePlan(TripKind.Overnight)).Reason, Is.EqualTo(VoyageRefusal.CrewOutnumberBunks));
        }

        [Test]
        public void GameServices_CrewIsNeverNull_AndVoyagesDefaultToEmpty()
        {
            GameServices.Reset();
            try
            {
                Assert.That(GameServices.Crew, Is.Not.Null);
                Assert.That(GameServices.Voyages, Is.SameAs(EmptyVoyageCatalog.Instance));
                Assert.That(GameServices.Crew.TryBoard("b1", "boat.dory", P("a")).Reason, Is.EqualTo(VoyageRefusal.UnknownHull),
                    "with no catalog registered every hull is unknown, never guessed");
                Assert.That(GameServices.CrewPolicy.HiredHandsShareBunks, Is.True);
                Assert.That(GameServices.CrewPolicy.MonthTripsNeedHiredSkipper, Is.False);
            }
            finally
            {
                GameServices.Reset();
            }
        }
    }
}
