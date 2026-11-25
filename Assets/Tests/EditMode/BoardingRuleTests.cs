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
    /// railed, the working boats the owner named are ladder-only to climb, no restricted hull traps a
    /// swimmer, and the small boats are still climb-anywhere. These are the owner's dials: changing a
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
            Assert.IsFalse(BoardingRuleMath.IsClimbRestricted(0f, false, false, Limit), "the dory");
            Assert.IsFalse(BoardingRuleMath.IsClimbRestricted(0.95f, false, false, Limit), "a console skiff");
            Assert.IsFalse(BoardingRuleMath.IsClimbRestricted(Limit, false, false, Limit), "AT the limit is still 'at or under'");

            var access = BoardingRuleMath.EvaluateClimb(0.2f, false, false, null, new Vector2(40f, 40f), Limit, Reach);
            Assert.AreEqual(BoardingAccess.Anywhere, access);
            Assert.IsTrue(BoardingRuleMath.Allows(access));
        }

        [Test]
        public void AHighHull_IsRestricted_EvenWithNoRail()
        {
            Assert.IsTrue(BoardingRuleMath.IsClimbRestricted(Limit + 0.01f, false, false, Limit));
            Assert.IsTrue(BoardingRuleMath.IsClimbRestricted(6.13f, false, false, Limit), "the tanker's wall");
        }

        [Test]
        public void ARailedHull_IsRestricted_HoweverLowSheIs()
        {
            Assert.IsTrue(BoardingRuleMath.IsClimbRestricted(0f, true, false, Limit), "a low railed yacht");
        }

        [Test]
        public void ALadderOnlyHull_IsClimbRestricted_HoweverLowSheMeasures()
        {
            // Owner, 2026-11-13: the cape islander and the lobster boats "would definitely be too tall
            // to climb". Where the measured gunwale is under the limit, the data flag carries the ruling.
            Assert.IsTrue(BoardingRuleMath.IsClimbRestricted(0.72f, false, true, Limit));
            var ladder = new[] { Point(0f, -6f) };
            Assert.AreEqual(BoardingAccess.AtAPoint,
                BoardingRuleMath.EvaluateClimb(0.72f, false, true, ladder, new Vector2(0f, -7f), Limit, Reach));
            Assert.AreEqual(BoardingAccess.FindAPoint,
                BoardingRuleMath.EvaluateClimb(0.72f, false, true, ladder, new Vector2(2.4f, 0f), Limit, Reach));
            Assert.AreEqual(BoardingAccess.NoPoint,
                BoardingRuleMath.EvaluateClimb(0.72f, false, true, null, new Vector2(0f, -7f), Limit, Reach));
        }

        [Test]
        public void GoingOverTheSide_IsStoppedByRailsOnly_NeverByHeight()
        {
            var ladder = new[] { Point(0f, -6f) };
            Vector2 amidships = new Vector2(2.4f, 0f);
            Assert.AreEqual(BoardingAccess.Anywhere,
                BoardingRuleMath.EvaluateOverTheSide(false, ladder, amidships, Reach),
                "an unrailed working deck is jumped from anywhere, ladder or no ladder");
            Assert.AreEqual(BoardingAccess.Anywhere,
                BoardingRuleMath.EvaluateOverTheSide(false, null, amidships, Reach));
            Assert.AreEqual(BoardingAccess.FindAPoint,
                BoardingRuleMath.EvaluateOverTheSide(true, ladder, amidships, Reach), "rails stop you jumping in");
            Assert.AreEqual(BoardingAccess.AtAPoint,
                BoardingRuleMath.EvaluateOverTheSide(true, ladder, new Vector2(0f, -5.5f), Reach));
            Assert.AreEqual(BoardingAccess.NoPoint,
                BoardingRuleMath.EvaluateOverTheSide(true, null, amidships, Reach));
        }

        [Test]
        public void ARailedHull_RefusesAwayFromHerLadder_AndAllowsAtIt()
        {
            var points = new[] { Point(0f, -12.79f) };

            var atTheLadder = BoardingRuleMath.EvaluateClimb(1.95f, true, false, points, new Vector2(0.4f, -13.5f), Limit, Reach);
            Assert.AreEqual(BoardingAccess.AtAPoint, atTheLadder);
            Assert.IsTrue(BoardingRuleMath.Allows(atTheLadder));

            var amidships = BoardingRuleMath.EvaluateClimb(1.95f, true, false, points, new Vector2(3.2f, 0f), Limit, Reach);
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
                    BoardingRuleMath.EvaluateClimb(0f, true, false, points, new Vector2(2f, 5f), Limit, Reach), kind.ToString());
            }
        }

        [Test]
        public void ARestrictedHullWithNoPoints_RefusesEverywhere_WharfOrGangwayOnly()
        {
            foreach (var points in new[] { null, new BoardingPoint[0] })
            {
                var access = BoardingRuleMath.EvaluateClimb(6.13f, true, false, points, Vector2.zero, Limit, Reach);
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
                var a = BoardingRuleMath.EvaluateClimb(1.4f, i % 2 == 0, false, points, p, Limit, Reach);
                var b = BoardingRuleMath.EvaluateClimb(1.4f, i % 2 == 0, false, points, p, Limit, Reach);
                Assert.AreEqual(a, b);
            }
        }

        [Test]
        public void NotANumber_NeverOpensAWall()
        {
            // A NaN freeboard reads as 0 (low), so only the rail can restrict — and it still does.
            Assert.IsTrue(BoardingRuleMath.IsClimbRestricted(float.NaN, true, false, Limit));
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
        public void ASwimmer_CannotClimbAShipsWall_ThatCarriesNoLadder()
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
        public void ASwimmer_ClimbsALadderOnlyWorkingBoat_OnlyAtHerLadder()
        {
            var r = Build(0.72f, railed: false, new[] { Point(0f, 60f) }, inTheWater: true);
            Assert.IsTrue(r.Switcher.CanInteract(), "low and unrailed: climb-anywhere until the flag is set");
            r.Deck.ClimbAboardOnlyAtBoardingPoints = true;
            AssertRefused(r, BoardingAccess.FindAPoint, "ladder-only, and her ladder is 60 m away");

            var atHer = SwimmerInDeckFrame(r);
            r.Deck.BoardingPoints = new[] { Point(atHer.x, atHer.y) };
            Assert.AreEqual(BoardingAccess.AtAPoint, r.Switcher.ClimbAccessFromHere());
            AssertBoards(r, "at her stern ladder");
        }

        [Test]
        public void OnDeck_AHighOrLadderOnlyWorkingBoat_IsStillLeftOverTheSideAnywhere()
        {
            var r = Build(2.3f, railed: false, new[] { Point(0f, 60f) }, inTheWater: false);
            r.Deck.ClimbAboardOnlyAtBoardingPoints = true;
            Assert.AreEqual(BoardingAccess.Anywhere, r.Switcher.OverTheSideAccessFromHere(),
                "only climbing ABOARD is limited to the ladder on the fishing hulls; jumping in is not");
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

        /// <summary>Rails block the side: no climbing aboard AND no going over it except at a point.</summary>
        static readonly string[] RailedHulls =
        {
            "boat.tanker", "boat.coastal_packet", "boat.motor_yacht_84", "boat.expedition_yacht_120",
        };

        /// <summary>Hulls with no deck Def at all (never measured): the rule leaves them climb-anywhere.
        /// Listed so a NEW hull without a deck is a decision somebody made, not an accident.</summary>
        static readonly string[] UnmeasuredHulls = { "boat.sloop_30", "boat.sloop_88" };

        /// <summary>Climb-restricted with NO boarding point: boarded from a wharf or gangway only.
        /// Owner, 2026-11-13: "Boardinnf some boats from dock only is too hard maybe all boats need a
        /// ladder" — so this list is the EXCEPTIONS to that ruling and is meant to be empty, and now
        /// it is. The coastal packet came off it when she got her stern ladder point; the tanker, the
        /// last hull on it, came off on the 2026-11-14 ruling that gave her two pilot ladder points
        /// amidships (see <see cref="TheTanker_IsBoardedFromTheWater_AtHerTwoPilotLadders_AndNowhereElse"/>).
        /// Naming a hull here goes against the owner's ruling.</summary>
        static readonly string[] WharfOnlyHulls = { };

        /// <summary>How far outside her walk box a swimmer treads water when a test asks "can this
        /// point be reached from the water" — the nearest of the stand-offs the cape islander proof
        /// uses.</summary>
        const float SwimmerStandOffMetres = 0.5f;

        /// <summary>Owner, 2026-11-13: still climb-anywhere.</summary>
        static readonly string[] SmallBoats =
        {
            "boat.dory", "boat.dory_outboard", "boat.punt", "boat.punt_upgraded", "boat.sport_skiff",
            "boat.sport_skiff_mk2", "boat.sport_skiff_twin", "boat.console_skiff", "boat.zodiac_frc",
            "boat.zodiac_hurricane",
        };

        /// <summary>Owner, 2026-11-13: "Side dragger and stern trawler, cape islander and lobster boats
        /// should have stern or side ladders. They would definitely be too tall to climb".</summary>
        static bool IsARuledWorkingBoat(string id)
            => id == "boat.side_dragger" || id == "boat.stern_trawler" || id == "boat.stern_trawler_mk2"
               || id == "boat.cape_islander" || id.StartsWith("boat.lobster_");

        /// <summary>The boats a campaign player boards from the water in the intro and the early
        /// quests: these get the full switcher proof at their stern ladder.</summary>
        static bool IsACapeIslanderOrLobsterBoat(string id)
            => id == "boat.cape_islander" || id.StartsWith("boat.lobster_");

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
                foreach (string field in new[] { "BoardingFreeboardMetres:", "RailsBlockTheSide:",
                                               "ClimbAboardOnlyAtBoardingPoints:", "BoardingPoints:" })
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
                "tanker, packet and both yachts are railed; no fishing hull is (going over the side stays " +
                "allowed on them). If the owner changed a hull's rail flag, update this table in the same PR.");
        }

        [Test]
        public void TheSmallBoats_AreStillClimbAnywhere()
        {
            var hulls = ShippedHulls();
            foreach (string id in SmallBoats)
            {
                var deck = DeckOf(hulls.Single(h => h.Id == id));
                Assert.AreEqual(BoardingAccess.Anywhere, deck.ClimbAccessAt(new Vector2(999f, 999f), Limit, Reach),
                    $"{id} (gunwale {deck.BoardingFreeboardMetres} m) must stay climb-anywhere");
                Assert.AreEqual(BoardingAccess.Anywhere, deck.OverTheSideAccessAt(new Vector2(999f, 999f), Reach), id);
            }
        }

        [Test]
        public void NoRestrictedHull_TrapsASwimmer_ExceptTheWharfOnlyOnes()
        {
            var problems = new List<string>();
            foreach (var hull in ShippedHulls())
            {
                var deck = DeckOf(hull);
                if (deck == null) continue;
                bool restricted = BoardingRuleMath.IsClimbRestricted(deck.BoardingFreeboardMetres,
                    deck.RailsBlockTheSide, deck.ClimbAboardOnlyAtBoardingPoints, Limit);
                bool wharfOnly = WharfOnlyHulls.Contains(hull.Id);
                if (restricted && deck.BoardingPoints.Length == 0 && !wharfOnly)
                    problems.Add($"{hull.Id}: climb-restricted (gunwale {deck.BoardingFreeboardMetres} m) with no " +
                                 "boarding point — a swimmer beside her can never get back aboard");
                if (wharfOnly && (!restricted || deck.BoardingPoints.Length > 0))
                    problems.Add($"{hull.Id}: listed wharf-only but she now has a way up — update the table");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        /// <summary>
        /// Owner, 2026-11-13: "Boardinnf some boats from dock only is too hard maybe all boats need a
        /// ladder". No hull may be boardable ONLY from a dock: each one is climb-anywhere, or has at
        /// least one boarding point a swimmer can actually get to — the water just outside her walk box
        /// at that point is within the point's reach, and the rule answers AtAPoint there. There is no
        /// exception left: <see cref="WharfOnlyHulls"/> is asserted empty (the tanker was the last name
        /// on it, until her pilot ladders). Per-hull data, never a code rule: he said "maybe".
        /// </summary>
        [Test]
        public void EveryHull_CanBeBoardedFromTheWater()
        {
            Assert.IsEmpty(WharfOnlyHulls,
                "no hull is dock-only any more (the tanker got her two pilot ladders on 2026-11-14). " +
                "Adding a hull here goes against the owner's 2026-11-13 ruling.");

            var problems = new List<string>();
            int climbAnywhere = 0, byAPoint = 0, exceptions = 0;
            foreach (var hull in ShippedHulls())
            {
                var deck = DeckOf(hull);
                if (deck == null) { climbAnywhere++; continue; }   // unmeasured: the rule never restricts her

                // Far from any point she could carry: Anywhere here means she is climb-anywhere.
                Vector2 farAway = deck.WalkCenter + deck.WalkHalfExtents * 3f + new Vector2(Reach, Reach) * 3f;
                if (deck.ClimbAccessAt(farAway, Limit, Reach) == BoardingAccess.Anywhere) { climbAnywhere++; continue; }

                bool reachable = false;
                foreach (var p in deck.BoardingPoints)
                {
                    Vector2 water = NearestWaterBeside(deck, p.DeckLocalMeters);
                    if (deck.ClimbAccessAt(water, Limit, Reach) == BoardingAccess.AtAPoint) { reachable = true; break; }
                }

                if (WharfOnlyHulls.Contains(hull.Id))
                {
                    exceptions++;
                    if (reachable)
                        problems.Add($"{hull.Id}: named as the dock-only exception but she now has a way up " +
                                     "from the water — take her off WharfOnlyHulls");
                    continue;
                }
                if (reachable) { byAPoint++; continue; }
                problems.Add(deck.BoardingPoints.Length == 0
                    ? $"{hull.Id}: too tall to climb (top of side {deck.BoardingFreeboardMetres} m) and no boarding " +
                      "point — she can only be boarded from a dock. Author one point on her deck asset " +
                      "(stern by default)."
                    : $"{hull.Id}: has boarding points but none can be reached from the water outside her hull " +
                      $"(a swimmer {SwimmerStandOffMetres} m off her side is out of reach of all of them)");
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
            Assert.AreEqual(0, exceptions, "no hull is boarded from a dock only");
            Assert.Greater(climbAnywhere, 0, "the small boats did not load");
            Assert.Greater(byAPoint, 0, "the laddered boats did not load");
        }

        /// <summary>The water spot a swimmer would use for a point: straight out through the nearest
        /// edge of her walk box (the box bounds every deck area, so outside it is never on her deck),
        /// then <see cref="SwimmerStandOffMetres"/> further. A point already outside the box (a ladder
        /// hung off the transom) is its own water spot.</summary>
        static Vector2 NearestWaterBeside(BoatDeckDef deck, Vector2 point)
        {
            Vector2 off = point - deck.WalkCenter;
            float insideX = deck.WalkHalfExtents.x - Mathf.Abs(off.x);
            float insideY = deck.WalkHalfExtents.y - Mathf.Abs(off.y);
            if (insideX < 0f || insideY < 0f) return point;
            if (insideY <= insideX)
                return point + new Vector2(0f, (off.y < 0f ? -1f : 1f) * (insideY + SwimmerStandOffMetres));
            return point + new Vector2((off.x < 0f ? -1f : 1f) * (insideX + SwimmerStandOffMetres), 0f);
        }

        /// <summary>The hull that came off the dock-only list on the 2026-11-13 ruling: a stern ladder
        /// point at her transom, read from her own aft-deck outline. She is railed, so the same point is
        /// also where her crew go over the side.</summary>
        [Test]
        public void TheCoastalPacket_IsClimbedAndLeft_AtHerSternLadder()
        {
            var deck = DeckOf(ShippedHulls().Single(h => h.Id == "boat.coastal_packet"));
            var ladder = deck.BoardingPoints.Single(p => p.Id == "stern_ladder");
            Assert.AreEqual(BoardingPointKind.Ladder, ladder.Kind);

            // On her transom: the aftmost edge of her own deck outline, on the centreline.
            float transomY = deck.WalkCenter.y - deck.WalkHalfExtents.y;
            Assert.AreEqual(transomY, ladder.DeckLocalMeters.y, 0.01f, "the ladder hangs at her transom");
            Assert.AreEqual(deck.WalkCenter.x, ladder.DeckLocalMeters.x, 0.01f, "on her centreline");

            Vector2 astern = ladder.DeckLocalMeters + new Vector2(0f, -SwimmerStandOffMetres);
            Assert.AreEqual(BoardingAccess.AtAPoint, deck.ClimbAccessAt(astern, Limit, Reach));
            Assert.AreEqual(BoardingAccess.AtAPoint, deck.OverTheSideAccessAt(ladder.DeckLocalMeters, Reach),
                "railed: her ladder is also her way off");

            Vector2 amidships = deck.WalkCenter + new Vector2(deck.WalkHalfExtents.x + SwimmerStandOffMetres, 0f);
            Assert.AreEqual(BoardingAccess.FindAPoint, deck.ClimbAccessAt(amidships, Limit, Reach),
                "her 3 m side is still not climbed away from the ladder");
            Assert.AreEqual(BoardingAccess.FindAPoint, deck.OverTheSideAccessAt(amidships, Reach));
        }

        [Test]
        public void TheWorkingBoatsTheOwnerNamed_AreLadderOnlyToClimb_ButStillLeftOverTheSide()
        {
            int seen = 0;
            foreach (var hull in ShippedHulls().Where(h => IsARuledWorkingBoat(h.Id)))
            {
                seen++;
                var deck = DeckOf(hull);
                Assert.IsNotNull(deck, hull.Id);
                Assert.IsTrue(deck.BoardingPoints.Any(p => p.Kind == BoardingPointKind.Ladder),
                    $"{hull.Id}: the ruling gives her a stern or side ladder");
                Vector2 amidships = deck.WalkCenter + new Vector2(deck.WalkHalfExtents.x + 1f, 0f);
                var ladder = deck.BoardingPoints.First(p => p.Kind == BoardingPointKind.Ladder);
                bool ladderIsAmidships =
                    (ladder.DeckLocalMeters - amidships).magnitude <= BoardingRuleMath.ReachOf(ladder, Reach)
                    || (ladder.DeckLocalMeters - new Vector2(-amidships.x, amidships.y)).magnitude
                       <= BoardingRuleMath.ReachOf(ladder, Reach);
                if (!ladderIsAmidships)
                    Assert.AreEqual(BoardingAccess.FindAPoint, deck.ClimbAccessAt(amidships, Limit, Reach),
                        $"{hull.Id}: her side is too tall to climb away from the ladder");
                Assert.AreEqual(BoardingAccess.AtAPoint, deck.ClimbAccessAt(ladder.DeckLocalMeters, Limit, Reach), hull.Id);
                Assert.AreEqual(BoardingAccess.Anywhere, deck.OverTheSideAccessAt(amidships, Reach),
                    $"{hull.Id}: going over the side stays allowed on the fishing hulls (open question in the PR)");
            }
            Assert.AreEqual(23, seen, "side dragger, two stern trawlers, cape islander, lobster boat, 18 variants");
        }

        /// <summary>
        /// ⭐ The proof the intro and the early quests rest on: a swimmer beside the cape islander or any
        /// lobster boat gets back aboard through the new stern ladder — the REAL deck asset on the
        /// switcher's own rig, the swimmer just astern of the ladder, the offer live. And away from the
        /// ladder, in the water at her quarter-length, the same press is refused with "Find the ladder".
        /// </summary>
        [Test]
        public void ASwimmer_BoardsTheCapeIslanderAndEveryLobsterBoat_AtHerSternLadder()
        {
            int seen = 0;
            foreach (var hull in ShippedHulls().Where(h => IsACapeIslanderOrLobsterBoat(h.Id)))
            {
                seen++;
                var real = DeckOf(hull);
                var r = Build(0f, false, null, inTheWater: true, withDeckDef: false);
                BoatDeckAreas.Write(r.BoatGo, real);
                var ladder = real.BoardingPoints.First(p => p.Id == "stern_ladder");
                float heading = DeckWalkController.DrawnHeadingDegreesOf(r.BoatGo.transform);
                float elevation = DeckWalkController.BakeElevationDegreesOf(r.BoatGo.transform);

                bool boarded = false;
                foreach (float astern in new[] { 0.5f, 1.0f, 1.5f })
                {
                    Vector2 spot = ladder.DeckLocalMeters + new Vector2(0f, -astern);
                    r.PlayerGo.transform.position = r.BoatGo.transform.position
                        + (Vector3)DeckAreaMath.DeckToWorld(spot, 0f, heading, elevation);
                    Assert.AreEqual(BoardingAccess.AtAPoint, r.Switcher.ClimbAccessFromHere(),
                        $"{hull.Id}: {astern} m astern of her stern ladder");
                    if (r.Switcher.WithinBoardReach() && r.Switcher.CanInteract()) { boarded = true; break; }
                }
                Assert.IsTrue(boarded, $"{hull.Id}: no spot astern of her ladder offers the climb");

                Vector2 abeam = real.WalkCenter + new Vector2(real.WalkHalfExtents.x + 1f, 0f);
                r.PlayerGo.transform.position = r.BoatGo.transform.position
                    + (Vector3)DeckAreaMath.DeckToWorld(abeam, 0f, heading, elevation);
                Assert.AreEqual(BoardingAccess.FindAPoint, r.Switcher.ClimbAccessFromHere(), $"{hull.Id}: abeam");
                Assert.IsFalse(r.Switcher.CanInteract(), $"{hull.Id}: her side is too tall to climb");

                TearDown(); SetUp();
            }
            Assert.AreEqual(20, seen, "cape islander, lobster boat and the 18 lobster variants");
        }

        /// <summary>Seat ruling, 2026-11-14: the tanker is boarded from the water at two pilot ladders
        /// amidships, one each side, and nowhere else. No stern ladder (a ten metre climb). She is
        /// railed, so the same two points are where her crew go over the side.</summary>
        [Test]
        public void TheTanker_IsBoardedFromTheWater_AtHerTwoPilotLadders_AndNowhereElse()
        {
            var tanker = DeckOf(ShippedHulls().Single(h => h.Id == "boat.tanker"));
            Assert.Greater(tanker.BoardingFreeboardMetres, Limit, "her wall alone restricts her");
            CollectionAssert.AreEquivalent(new[] { "pilot_ladder_starboard", "pilot_ladder_port" },
                tanker.BoardingPoints.Select(p => p.Id).ToArray(), "two pilot ladders and no other way up");

            var starboard = tanker.BoardingPoints.Single(p => p.Id == "pilot_ladder_starboard");
            var port = tanker.BoardingPoints.Single(p => p.Id == "pilot_ladder_port");
            Assert.Greater(starboard.DeckLocalMeters.x, 0f, "starboard is +x");
            Assert.Less(port.DeckLocalMeters.x, 0f, "port is -x");
            Assert.AreEqual(starboard.DeckLocalMeters.x, -port.DeckLocalMeters.x, 0.01f, "a mirrored pair");
            Assert.AreEqual(starboard.DeckLocalMeters.y, port.DeckLocalMeters.y, 0.01f, "at the same station");

            foreach (var p in tanker.BoardingPoints)
            {
                Assert.AreEqual(BoardingPointKind.Ladder, p.Kind, p.Id);
                // Amidships, at her side: in the middle third of her length, at or beyond her rail.
                Vector2 off = p.DeckLocalMeters - tanker.WalkCenter;
                Assert.Less(Mathf.Abs(off.y), tanker.WalkHalfExtents.y / 3f, $"{p.Id}: amidships");
                Assert.GreaterOrEqual(Mathf.Abs(off.x), tanker.WalkHalfExtents.x - 0.01f, $"{p.Id}: at her side");

                Vector2 water = NearestWaterBeside(tanker, p.DeckLocalMeters);
                Assert.AreEqual(BoardingAccess.AtAPoint, tanker.ClimbAccessAt(water, Limit, Reach),
                    $"{p.Id}: a swimmer at the foot of the ladder climbs it");
                Assert.AreEqual(BoardingAccess.AtAPoint, tanker.OverTheSideAccessAt(p.DeckLocalMeters, Reach),
                    $"{p.Id}: her crew go over the side here");
            }

            // Nowhere else: off her stern, off her bow, and at her side well forward of the ladders.
            var elsewhere = new[]
            {
                tanker.WalkCenter + new Vector2(0f, -(tanker.WalkHalfExtents.y + SwimmerStandOffMetres)),
                tanker.WalkCenter + new Vector2(0f, tanker.WalkHalfExtents.y + SwimmerStandOffMetres),
                tanker.WalkCenter + new Vector2(tanker.WalkHalfExtents.x + SwimmerStandOffMetres, tanker.WalkHalfExtents.y * 0.5f),
                tanker.WalkCenter + new Vector2(-(tanker.WalkHalfExtents.x + SwimmerStandOffMetres), -tanker.WalkHalfExtents.y * 0.5f),
            };
            foreach (Vector2 spot in elsewhere)
            {
                Assert.AreEqual(BoardingAccess.FindAPoint, tanker.ClimbAccessAt(spot, Limit, Reach),
                    $"{spot}: no ladder here, the swimmer is sent to find one");
                Assert.AreEqual(BoardingAccess.FindAPoint, tanker.OverTheSideAccessAt(spot, Reach), $"{spot}");
            }
        }

        [Test]
        public void TheYachts_AreClimbedAndLeft_AtTheirSternLadder_AndNowhereElse()
        {
            foreach (string id in new[] { "boat.motor_yacht_84", "boat.expedition_yacht_120" })
            {
                var deck = DeckOf(ShippedHulls().Single(h => h.Id == id));
                Assert.IsNotEmpty(deck.BoardingPoints, id);
                foreach (var p in deck.BoardingPoints)
                {
                    Assert.AreEqual(BoardingAccess.AtAPoint, deck.ClimbAccessAt(p.DeckLocalMeters, Limit, Reach), $"{id}/{p.Id}");
                    Assert.AreEqual(BoardingAccess.AtAPoint, deck.OverTheSideAccessAt(p.DeckLocalMeters, Reach), $"{id}/{p.Id}");
                }
                Vector2 amidships = deck.WalkCenter + new Vector2(deck.WalkHalfExtents.x, 0f);
                Assert.AreEqual(BoardingAccess.FindAPoint, deck.ClimbAccessAt(amidships, Limit, Reach),
                    $"{id}: amidships at her rail there is no ladder");
                Assert.AreEqual(BoardingAccess.FindAPoint, deck.OverTheSideAccessAt(amidships, Reach), id);
            }
        }
    }
}
