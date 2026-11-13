using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>Only ladders, and rails stop you jumping in</b> (owner playtest, 2026-11-13: <i>"boarding the
    /// tanker player can easily climb up massive walls which is wrong, should only be ladders, railings
    /// should stop players from jumping in, maybe different on the fishing vessels"</i>).
    ///
    /// <para>Three layers, each guarding a different way this can rot:</para>
    /// <list type="bullet">
    /// <item><b>The rule</b> (<see cref="BoardingRuleMath"/>) — pure, so it is pinned exhaustively.</item>
    /// <item><b>The wiring</b> — the <see cref="ControlSwitcher"/> actually asks the rule, from the
    /// water only, and a hull nobody has measured keeps the old behaviour.</item>
    /// <item><b>The data</b> — every shipped hull carries the values, the hulls the seat named are
    /// railed, and the small boats are still climb-anywhere. These are the owner's dials: changing a
    /// hull's rail flag in her deck asset is a deliberate edit, and the table here is what makes it one
    /// (update the row in the same PR).</item>
    /// </list>
    /// </summary>
    public class BoardingRuleTests
    {
        const string DataBoats = "Assets/_Project/Data/Boats";

        // The shipped limits, read from a fresh GameConfig rather than retyped (rule 6).
        static float Limit, Reach;

        private readonly List<Object> _spawned = new();

        [OneTimeSetUp]
        public void ReadTheShippedLimits()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            Limit = config.ClimbAnywhereMaxFreeboardMetres;
            Reach = config.BoardingPointReachMetres;
            Object.DestroyImmediate(config);
        }

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            var config = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(config);
            GameServices.Config = config;
            InteractionGate.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveBoatChanged>();
            InteractionGate.Reset();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        static BoardingPoint Point(float x, float y, float reach = 0f,
                                   BoardingPointKind kind = BoardingPointKind.Ladder)
            => new BoardingPoint { Id = "p", Kind = kind, DeckLocalMeters = new Vector2(x, y), ReachMetres = reach };

        // ================= 1. the rule ===========================================================

        [Test]
        public void TheShippedLimits_AreTheSeatsLeans()
        {
            Assert.AreEqual(1.0f, Limit, "climb-anywhere tops out at 1.0 m of freeboard");
            Assert.Greater(Reach, 0f, "a boarding point with no reach could never be used");
        }

        [Test]
        public void ALowUnrailedHull_IsClimbAnywhere_AsBefore()
        {
            Assert.IsFalse(BoardingRuleMath.IsRestricted(0f, false, Limit), "the dory");
            Assert.IsFalse(BoardingRuleMath.IsRestricted(0.95f, false, Limit), "the side dragger");
            Assert.IsFalse(BoardingRuleMath.IsRestricted(Limit, false, Limit), "AT the limit is still 'at or under'");

            var access = BoardingRuleMath.Evaluate(0.2f, false, null, new Vector2(40f, 40f), Limit, Reach);
            Assert.AreEqual(BoardingAccess.Anywhere, access);
            Assert.IsTrue(BoardingRuleMath.Allows(access));
        }

        [Test]
        public void AHighHull_IsRestricted_EvenWithNoRail()
        {
            Assert.IsTrue(BoardingRuleMath.IsRestricted(Limit + 0.01f, false, Limit));
            Assert.IsTrue(BoardingRuleMath.IsRestricted(6.13f, false, Limit), "the tanker's wall");
        }

        [Test]
        public void ARailedHull_IsRestricted_HoweverLowSheIs()
        {
            Assert.IsTrue(BoardingRuleMath.IsRestricted(0f, true, Limit), "an offshore lobster boat");
        }

        [Test]
        public void ARailedHull_RefusesAwayFromHerLadder_AndAllowsAtIt()
        {
            var points = new[] { Point(0f, -12.79f) };

            var atTheLadder = BoardingRuleMath.Evaluate(1.95f, true, points, new Vector2(0.4f, -13.5f), Limit, Reach);
            Assert.AreEqual(BoardingAccess.AtAPoint, atTheLadder);
            Assert.IsTrue(BoardingRuleMath.Allows(atTheLadder));

            var amidships = BoardingRuleMath.Evaluate(1.95f, true, points, new Vector2(3.2f, 0f), Limit, Reach);
            Assert.AreEqual(BoardingAccess.FindAPoint, amidships);
            Assert.IsFalse(BoardingRuleMath.Allows(amidships), "a railed hull refuses the climb away from a ladder");
        }

        [Test]
        public void AGateAndAPlatform_AllowJustAsALadderDoes()
        {
            foreach (BoardingPointKind kind in System.Enum.GetValues(typeof(BoardingPointKind)))
            {
                var points = new[] { Point(2f, 5f, 0f, kind) };
                Assert.AreEqual(BoardingAccess.AtAPoint,
                    BoardingRuleMath.Evaluate(0f, true, points, new Vector2(2f, 5f), Limit, Reach), kind.ToString());
            }
        }

        [Test]
        public void ARestrictedHullWithNoPoints_RefusesEverywhere_WharfOrGangwayOnly()
        {
            foreach (var points in new[] { null, new BoardingPoint[0] })
            {
                var access = BoardingRuleMath.Evaluate(6.13f, true, points, Vector2.zero, Limit, Reach);
                Assert.AreEqual(BoardingAccess.NoPoint, access);
                Assert.IsFalse(BoardingRuleMath.Allows(access));
            }
        }

        [Test]
        public void APointsOwnReach_BeatsTheDefault_AndZeroMeansTheDefault()
        {
            Vector2 at = new Vector2(0f, Reach + 1f);        // just outside the default reach of (0,0)
            Assert.AreEqual(-1, BoardingRuleMath.PointWithinReach(new[] { Point(0f, 0f) }, at, Reach));
            Assert.AreEqual(0, BoardingRuleMath.PointWithinReach(new[] { Point(0f, 0f, Reach + 2f) }, at, Reach));
            Assert.AreEqual(Reach, BoardingRuleMath.ReachOf(Point(0f, 0f), Reach));
        }

        [Test]
        public void TheNearestPointInReachWins_WhateverTheAuthoringOrder()
        {
            var points = new[] { Point(0f, 1.5f), Point(0f, 0.2f), Point(0f, 90f) };
            Assert.AreEqual(1, BoardingRuleMath.PointWithinReach(points, Vector2.zero, Reach));
        }

        [Test]
        public void TheRuleIsAPureFunction_SameInputsSameAnswer()
        {
            var points = new[] { Point(1f, -4f), Point(-1f, 6f, 0.5f) };
            for (int i = 0; i < 64; i++)
            {
                var p = new Vector2(Mathf.Sin(i * 0.7f) * 5f, Mathf.Cos(i * 1.3f) * 8f);
                var a = BoardingRuleMath.Evaluate(1.4f, i % 2 == 0, points, p, Limit, Reach);
                var b = BoardingRuleMath.Evaluate(1.4f, i % 2 == 0, points, p, Limit, Reach);
                Assert.AreEqual(a, b);
            }
        }

        [Test]
        public void NotANumber_NeverOpensAWall()
        {
            // A NaN freeboard reads as 0 (low), so only the rail can restrict — and it still does.
            Assert.IsTrue(BoardingRuleMath.IsRestricted(float.NaN, true, Limit));
            Assert.AreEqual(-1, BoardingRuleMath.PointWithinReach(
                new[] { Point(float.NaN, 0f) }, Vector2.zero, Reach), "a NaN point is in reach of nothing");
        }

        // ================= 2. the wiring =========================================================

        private sealed class Rig
        {
            public ControlSwitcher Switcher;
            public GameObject PlayerGo, BoatGo;
            public BoatDeckDef Deck;
        }

        private GameObject NewGo(string name, Vector3 pos)
        {
            var g = new GameObject(name);
            g.transform.position = pos;
            _spawned.Add(g);
            return g;
        }

        /// <summary>BoardingMoveTests' greybox rig — a player 2.3 m off a hull — plus a deck Def carrying
        /// the boarding data (null = an unmeasured hull), and the "in the water" seam set as asked.</summary>
        private Rig Build(float freeboard, bool railed, BoardingPoint[] points, bool inTheWater,
                          bool withDeckDef = true)
        {
            var playerGo = NewGo("Player", new Vector3(0f, -11.5f, 0f));
            var walk = playerGo.AddComponent<PlayerWalkController>();
            playerGo.AddComponent<DeckWalkController>().enabled = false;

            var boatGo = NewGo("Boat", new Vector3(0f, -13.8f, 0f));
            var boat = boatGo.AddComponent<BoatController>();
            var input = boatGo.AddComponent<DevBoatInput>();
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.test_hull"; hull.CameraWorldHeightMeters = 14f;
            _spawned.Add(hull);
            boat.SetHull(hull);
            walk.enabled = true; boat.enabled = false; input.enabled = false;

            BoatDeckDef deck = null;
            if (withDeckDef)
            {
                deck = ScriptableObject.CreateInstance<BoatDeckDef>();
                deck.BoardingFreeboardMetres = freeboard;
                deck.RailsBlockTheSide = railed;
                deck.BoardingPoints = points ?? new BoardingPoint[0];
                _spawned.Add(deck);
                BoatDeckAreas.Write(boatGo, deck);
            }

            var dock = NewGo("DockZone", new Vector3(0f, -120f, 0f));
            var disembark = NewGo("Disembark", new Vector3(0f, -118.5f, 0f));
            var sw = NewGo("Switcher", Vector3.zero).AddComponent<ControlSwitcher>();
            sw.Configure(walk, boat, input, dock.transform, 3f, disembark.transform);
            sw.ConfigureBoardingMove(enabled: true, approachSpeed: 3f, approachMaxSeconds: 0.8f,
                                     vaultSeconds: 0.3f, vaultHopMeters: 0.35f);
            sw.ConfigureInTheWater(() => inTheWater);
            return new Rig { Switcher = sw, PlayerGo = playerGo, BoatGo = boatGo, Deck = deck };
        }

        /// <summary>Where the rig's swimmer is in the hull's own frame — asked of the same projection
        /// the switcher uses, so a point "at the swimmer" is at the swimmer whatever the bake angle.</summary>
        private static Vector2 SwimmerInDeckFrame(Rig r)
            => DeckAreaMath.WorldToDeck(
                (Vector2)(r.PlayerGo.transform.position - r.BoatGo.transform.position), 0f,
                DeckWalkController.DrawnHeadingDegreesOf(r.BoatGo.transform),
                DeckWalkController.BakeElevationDegreesOf(r.BoatGo.transform));

        static void AssertRefused(Rig r, BoardingAccess expected, string why)
        {
            Assert.IsTrue(r.Switcher.WithinBoardReach(), "the rig stands her within reach of the hull");
            Assert.AreEqual(expected, r.Switcher.ClimbAccessFromHere(), why);
            Assert.IsFalse(r.Switcher.CanInteract(), why + " (no offer)");
            Assert.IsFalse(r.Switcher.BeginInteract(), why + " (the press)");
            Assert.IsFalse(r.Switcher.IsBoardingMove, why + " (no move started)");
            Assert.IsFalse(r.Switcher.TryInteract(), why + " (nor the instant transition)");
            Assert.AreEqual(ControlMode.OnFoot, r.Switcher.Mode);
        }

        static void AssertBoards(Rig r, string why)
        {
            Assert.IsTrue(r.Switcher.CanInteract(), why + " (the offer)");
            Assert.IsTrue(r.Switcher.BeginInteract(), why + " (the press)");
        }

        [Test]
        public void ASwimmer_CannotClimbARailedHull_AwayFromHerLadder()
        {
            var r = Build(0.3f, railed: true, new[] { Point(0f, 60f) }, inTheWater: true);
            AssertRefused(r, BoardingAccess.FindAPoint, "railed, and her ladder is 60 m away");
        }

        [Test]
        public void ASwimmer_ClimbsARailedHull_AtHerLadder()
        {
            var r = Build(0.3f, railed: true, null, inTheWater: true);
            r.Deck.BoardingPoints = new[] { Point(0f, 60f), default };
            var atHer = SwimmerInDeckFrame(r);
            r.Deck.BoardingPoints[1] = Point(atHer.x, atHer.y);

            Assert.AreEqual(BoardingAccess.AtAPoint, r.Switcher.ClimbAccessFromHere());
            AssertBoards(r, "within reach of her ladder");
        }

        [Test]
        public void ASwimmer_CannotClimbTheTankersWall_WhichHasNoLadderYet()
        {
            var r = Build(6.13f, railed: true, null, inTheWater: true);
            AssertRefused(r, BoardingAccess.NoPoint, "six metres of steel and no ladder");
        }

        [Test]
        public void ASwimmer_CannotClimbAHighHull_EvenUnrailed()
        {
            var r = Build(Limit + 0.5f, railed: false, null, inTheWater: true);
            AssertRefused(r, BoardingAccess.NoPoint, "over the climb-anywhere freeboard");
        }

        [Test]
        public void ASwimmer_StillClimbsALowUnrailedHull_Anywhere()
        {
            var r = Build(0.2f, railed: false, null, inTheWater: true);
            Assert.AreEqual(BoardingAccess.Anywhere, r.Switcher.ClimbAccessFromHere());
            AssertBoards(r, "the dory and the small fishing boats are climb-anywhere, as before");
        }

        [Test]
        public void FromAWharf_EvenTheTankerIsBoarded()
        {
            var r = Build(6.13f, railed: true, null, inTheWater: false);
            Assert.AreEqual(BoardingAccess.Anywhere, r.Switcher.ClimbAccessFromHere(),
                "the rule is about the water side only");
            AssertBoards(r, "dry-shod at a wharf or gangway");
        }

        [Test]
        public void AnUnmeasuredHull_KeepsTheOldBehaviour_SoNothingStrands()
        {
            var r = Build(0f, false, null, inTheWater: true, withDeckDef: false);
            Assert.AreEqual(BoardingAccess.Anywhere, r.Switcher.ClimbAccessFromHere());
            AssertBoards(r, "no deck Def means nobody has said she is railed");
        }

        [Test]
        public void TheOwnersDials_AreLive_FlipTheRailAndTheAnswerFlips()
        {
            var r = Build(0.3f, railed: false, null, inTheWater: true);
            Assert.IsTrue(r.Switcher.CanInteract());
            r.Deck.RailsBlockTheSide = true;
            Assert.IsFalse(r.Switcher.CanInteract(), "the rail flag is read live from the Def");
            r.Deck.RailsBlockTheSide = false;
            r.Deck.BoardingFreeboardMetres = 3f;
            Assert.IsFalse(r.Switcher.CanInteract(), "and so is the freeboard");
            GameServices.Config.ClimbAnywhereMaxFreeboardMetres = 4f;
            Assert.IsTrue(r.Switcher.CanInteract(), "and so is the limit in GameConfig");
        }

        [Test]
        public void OnDeck_TheRailIsOnlyHersToLeaveByAtAPoint()
        {
            // The rider's deck position in a fresh rig is the deck origin; the rule is asked there.
            var r = Build(0.3f, railed: true, new[] { Point(0f, 60f) }, inTheWater: false);
            Assert.AreEqual(BoardingAccess.FindAPoint, r.Switcher.OverTheSideAccessFromHere(),
                "railed, away from her gate: no going over the side");

            r.Deck.BoardingPoints = new[] { Point(0f, 0f, 0f, BoardingPointKind.Gate) };
            Assert.AreEqual(BoardingAccess.AtAPoint, r.Switcher.OverTheSideAccessFromHere());

            r.Deck.BoardingPoints = new BoardingPoint[0];
            Assert.AreEqual(BoardingAccess.NoPoint, r.Switcher.OverTheSideAccessFromHere(),
                "railed with no gate at all: never over the side");

            r.Deck.RailsBlockTheSide = false;
            Assert.AreEqual(BoardingAccess.Anywhere, r.Switcher.OverTheSideAccessFromHere(),
                "an open fishing boat keeps the two-press exit everywhere");
        }

        // ================= 3. the data ===========================================================

        static readonly string[] RailedHulls =
        {
            "boat.tanker", "boat.coastal_packet", "boat.motor_yacht_84", "boat.expedition_yacht_120",
            "boat.lobster_offshore_hardtop_fundy", "boat.lobster_offshore_hardtop_newfoundland",
            "boat.lobster_offshore_hardtop_northumberland", "boat.lobster_offshore_open_fundy",
            "boat.lobster_offshore_open_newfoundland", "boat.lobster_offshore_open_northumberland",
        };

        /// <summary>Hulls with no deck Def at all (never measured): the rule leaves them climb-anywhere.
        /// Listed so a NEW hull without a deck is a decision somebody made, not an accident.</summary>
        static readonly string[] UnmeasuredHulls = { "boat.sloop_30", "boat.sloop_88" };

        /// <summary>Restricted hulls that ship with a water-reaching ladder, platform or gate.</summary>
        static readonly string[] HullsWithAWayUp = { "boat.motor_yacht_84", "boat.expedition_yacht_120" };

        static List<BoatHullDef> ShippedHulls()
        {
            var hulls = AssetDatabase.FindAssets("t:BoatHullDef", new[] { DataBoats })
                .Select(g => AssetDatabase.LoadAssetAtPath<BoatHullDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(h => h != null).OrderBy(h => h.Id).ToList();
            Assert.GreaterOrEqual(hulls.Count, 40, "the fleet's hull Defs did not load — wrong folder?");
            return hulls;
        }

        static BoatDeckDef DeckOf(BoatHullDef hull) => hull.Visual != null ? hull.Visual.Deck : null;

        [Test]
        public void EveryShippedHull_CarriesTheBoardingValues()
        {
            var problems = new List<string>();
            foreach (var hull in ShippedHulls())
            {
                var deck = DeckOf(hull);
                if (deck == null)
                {
                    if (!UnmeasuredHulls.Contains(hull.Id))
                        problems.Add($"{hull.Id}: no deck Def, so no boarding data — give her one, or list " +
                                     "her as unmeasured here on purpose");
                    continue;
                }
                if (UnmeasuredHulls.Contains(hull.Id))
                    problems.Add($"{hull.Id}: now HAS a deck Def — take her off the unmeasured list and " +
                                 "decide her rail flag");

                string text = System.IO.File.ReadAllText(AssetDatabase.GetAssetPath(deck));
                foreach (string field in new[] { "BoardingFreeboardMetres:", "RailsBlockTheSide:", "BoardingPoints:" })
                    if (!text.Contains(field))
                        problems.Add($"{hull.Id}: {deck.name}.asset does not serialize {field} — the value " +
                                     "would be a silent default, not a decision");

                if (deck.BoardingFreeboardMetres < 0f || float.IsNaN(deck.BoardingFreeboardMetres))
                    problems.Add($"{hull.Id}: freeboard {deck.BoardingFreeboardMetres}");
                if (deck.BoardingPoints == null) { problems.Add($"{hull.Id}: null BoardingPoints"); continue; }

                var ids = new HashSet<string>();
                foreach (var p in deck.BoardingPoints)
                {
                    if (string.IsNullOrEmpty(p.Id) || !ids.Add(p.Id))
                        problems.Add($"{hull.Id}: boarding point id '{p.Id}' is empty or repeated");
                    if (!System.Enum.IsDefined(typeof(BoardingPointKind), p.Kind))
                        problems.Add($"{hull.Id}/{p.Id}: unknown kind {(int)p.Kind}");
                    if (p.ReachMetres < 0f) problems.Add($"{hull.Id}/{p.Id}: negative reach");
                    // A point must be ON her: inside her walk box grown by its own reach.
                    Vector2 off = p.DeckLocalMeters - deck.WalkCenter;
                    float reach = BoardingRuleMath.ReachOf(p, Reach);
                    if (Mathf.Abs(off.x) > deck.WalkHalfExtents.x + reach
                        || Mathf.Abs(off.y) > deck.WalkHalfExtents.y + reach)
                        problems.Add($"{hull.Id}/{p.Id}: at {p.DeckLocalMeters}, off the hull — nobody on her " +
                                     "deck could reach it");
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void TheRailedHulls_AreExactlyTheOnesTheSeatNamed()
        {
            var railed = ShippedHulls().Where(h => DeckOf(h) != null && DeckOf(h).RailsBlockTheSide)
                                       .Select(h => h.Id).OrderBy(s => s).ToArray();
            CollectionAssert.AreEqual(RailedHulls.OrderBy(s => s).ToArray(), railed,
                "tanker, packet, both yachts and the six offshore lobster boats are railed; nothing else is. " +
                "If the owner changed a hull's rail flag, update this table in the same PR.");
        }

        [Test]
        public void EveryUnrailedHull_IsLowEnoughToClimbAnywhere()
        {
            foreach (var hull in ShippedHulls())
            {
                var deck = DeckOf(hull);
                if (deck == null || deck.RailsBlockTheSide) continue;
                Assert.AreEqual(BoardingAccess.Anywhere,
                    deck.BoardingAccessAt(new Vector2(999f, 999f), Limit, Reach),
                    $"{hull.Id} (freeboard {deck.BoardingFreeboardMetres} m) is unrailed, so she must stay " +
                    "climb-anywhere — the dory and the fishing boats behave as they did");
            }
        }

        [Test]
        public void TheTanker_HasNoWayUpFromTheWaterYet()
        {
            var tanker = DeckOf(ShippedHulls().Single(h => h.Id == "boat.tanker"));
            Assert.Greater(tanker.BoardingFreeboardMetres, Limit, "her wall alone restricts her");
            Assert.IsEmpty(tanker.BoardingPoints, "slice 2 authors her pilot ladder; until then, a wharf");
            Assert.AreEqual(BoardingAccess.NoPoint, tanker.BoardingAccessAt(Vector2.zero, Limit, Reach));
        }

        [Test]
        public void TheYachts_AreClimbedAtTheirSternLadder_AndNowhereElse()
        {
            foreach (string id in HullsWithAWayUp)
            {
                var deck = DeckOf(ShippedHulls().Single(h => h.Id == id));
                Assert.IsNotEmpty(deck.BoardingPoints, id);
                foreach (var p in deck.BoardingPoints)
                    Assert.AreEqual(BoardingAccess.AtAPoint,
                        deck.BoardingAccessAt(p.DeckLocalMeters, Limit, Reach), $"{id}/{p.Id}");
                Assert.AreEqual(BoardingAccess.FindAPoint,
                    deck.BoardingAccessAt(deck.WalkCenter + new Vector2(deck.WalkHalfExtents.x, 0f), Limit, Reach),
                    $"{id}: amidships at her rail there is no ladder");
            }
        }
    }
}
