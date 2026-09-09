using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐⭐ <b>THE ARRIVAL BOAT'S DECK IS HER PLANKING, NOT A SQUARE ROUND HER.</b> The guard for the
    /// owner's 2026-09-09 playtest: <i>"the player can walk on some parts of the deck but some of the deck
    /// seems to extend over water, it seems to change as the boat rotates."</i>
    ///
    /// <para><b>What was wrong, measured on <c>origin/main</c> 063e96b0 before the fix.</b>
    /// <c>ArrivalOpening</c> registered its deck as an axis-aligned <see cref="Rect"/> of side = the
    /// hull's <c>LengthMeters</c> — 12.9 m for the cape islander, <b>166.41 m²</b> — while her authored
    /// walkable deck, projected the way her artwork is drawn, is <b>23.94 m²</b>. At the three headings
    /// the St Peters arrival actually flies, <b>144.7 / 147.7 / 144.2 m² of open water read as her
    /// deck</b>: 86.7–88.8% of everything the game called "aboard", reaching <b>7.1–7.8 m</b> from the
    /// nearest plank. Being world-axis it did not turn with her, so the water that counted as deck swept
    /// round the hull as she came about — the owner's second clause. It was not even purely generous:
    /// 0.01 m² of her real foredeck fell OUTSIDE it on the inbound leg.
    ///
    /// <para><b>⭐ EVERY ASSERTION HERE PRINTS THE RIVAL READER'S ANSWER.</b> The two candidates were the
    /// standable SQUARE and the walk's authored POLYGON, and a failure that says only "point (x, y) was
    /// wrong" cannot tell them apart. So each message carries both answers at the same point and the same
    /// heading, and <see cref="TheSquareThisReplaced_IsStillThisMuchWater"/> keeps measuring the square on
    /// its own, so the number that justified the change stays in the run rather than in a PR body.</para>
    ///
    /// <para><b>The headings are the region's, not typed.</b> They come off the same waypoints
    /// <see cref="StPetersArrivalOpening.Route"/> hands the opening and the same
    /// <see cref="StPetersArrivalOpening.BerthHeadingDegrees"/> her berth is read from — ask where the
    /// boat is IN PLAY, not where a fixture puts her.</para>
    /// </summary>
    public class ArrivalDeckStandableTests
    {
        private const string DeckAssetPath   = "Assets/_Project/Data/Boats/Decks/CapeIslanderIso.asset";
        private const string VisualAssetPath = "Assets/_Project/Data/Boats/Visuals/CapeIslanderIso.asset";
        private const string BoatAssetPath   = "Assets/_Project/Data/Boats/CapeIslander.asset";

        /// <summary>The deck's freeboard as <c>ArrivalOpening</c> states it. Only the ELEVATION uses it,
        /// and the elevation law is unchanged by this PR; it is here so the surface answers a real
        /// height.</summary>
        private const float Freeboard = 0.9f;

        /// <summary>Somewhere in the approach with nothing else registered — the surface under test is the
        /// only thing that can answer, so a hit is hers.</summary>
        private static readonly Vector2 Centre = new Vector2(211f, -3f);

        private BoatDeckDef _deck;
        private float _elevationDegrees;
        private float _lengthMeters;

        [SetUp]
        public void LoadTheRealHull()
        {
            _deck = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatDeckDef>(DeckAssetPath);
            Assert.IsNotNull(_deck, $"the cape's authored deck is missing at {DeckAssetPath}");
            Assert.IsTrue(_deck.HasWalkableDeck(),
                          "the cape must carry a walkable deck or this guard is measuring the fallback");

            var visual = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatVisualDef>(VisualAssetPath);
            Assert.IsNotNull(visual, $"the cape's visual def is missing at {VisualAssetPath}");
            _elevationDegrees = BoatInteriorInstaller.BakeElevationDegrees(visual);
            Assert.AreNotEqual(DeckAreaMath.PlanViewElevationDegrees, _elevationDegrees,
                               "⚠ the cape is drawn by a baked camera; a plan-view elevation here would mean " +
                               "this fixture is measuring an un-foreshortened deck the player never walks");

            var boat = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatHullDef>(BoatAssetPath);
            Assert.IsNotNull(boat, $"the cape's hull def is missing at {BoatAssetPath}");
            _lengthMeters = boat.LengthMeters;
        }

        /// <summary>The three headings her passage actually takes: the inbound leg off the landfall, the
        /// turn onto the approach, and the way she lies alongside.</summary>
        private static (string name, float degrees)[] Headings()
        {
            Vector2[] route = StPetersArrivalOpening.Route();
            Assert.GreaterOrEqual(route.Length, 3, "the entrance channel must publish a route to fly");
            return new[]
            {
                ("the inbound leg", ArrivalPilot.CompassOf(route[1] - route[0])),
                ("the turn",        ArrivalPilot.CompassOf(route[2] - route[1])),
                ("alongside",       StPetersArrivalOpening.BerthHeadingDegrees()),
            };
        }

        /// <summary>The shipped registrant, wired the way <c>ArrivalOpening</c> wires it: the deck comes
        /// off the BOAT, because the skinner writes it and the skinner runs after the spawn.</summary>
        private ArrivalDeck DeckAt(float heading, BoatDeckDef deck)
        {
            var boat = new GameObject("ArrivalBoat_fixture");
            if (deck != null) boat.AddComponent<BoatDeckAreas>().Configure(deck);
            var surface = new ArrivalDeck(boat, _lengthMeters, Freeboard);
            surface.MoveTo(Centre, heading, _elevationDegrees);
            return surface;
        }

        /// <summary>The OLD reader, kept alive here as the RIVAL so every failure can name which one was
        /// wrong: a world-axis square of side = her length, about her centre.</summary>
        private bool TheSquareSays(Vector2 worldPos)
        {
            float half = Mathf.Max(0.5f, _lengthMeters * 0.5f);
            return new Rect(Centre.x - half, Centre.y - half, half * 2f, half * 2f).Contains(worldPos);
        }

        // ---------------------------------------------------------------------------------------------

        /// <summary>⭐ Every point of her AUTHORED deck is somewhere she reads as standing — the half that
        /// stops a "fix" from being a fix by shrinking the answer to nothing. It sweeps the OUTLINE
        /// vertices too, deliberately: <see cref="DeckAreaMath.Contains"/>'s own doc says a point exactly
        /// on an edge may read either way, and a player pressed into a rail is put exactly there by
        /// <see cref="BoatDeckDef.ClampToWalkable"/>. 11–15 of these points were refused before
        /// <c>IsOverWalkableDeck</c> grew its 1 mm outline skin.</summary>
        [Test]
        public void EveryAuthoredDeckPoint_IsStandable()
        {
            foreach ((string name, float heading) in Headings())
            {
                ArrivalDeck surface = DeckAt(heading, _deck);
                Assert.IsTrue(surface.HasAuthoredDeck, $"{name}: the fixture wired no deck onto the boat");

                int tested = 0;
                for (int i = 0; i < _deck.Areas.Length; i++)
                {
                    DeckArea a = _deck.Areas[i];
                    if (a == null || a.Kind != DeckAreaKind.Deck || !a.IsUsable()) continue;
                    for (float x = a.Bounds.x; x <= a.Bounds.z; x += 0.05f)
                    for (float y = a.Bounds.y; y <= a.Bounds.w; y += 0.05f)
                    {
                        var q = new Vector2(x, y);
                        if (!DeckAreaMath.Contains(a.Outline, a.Bounds, q)) continue;
                        float h = DeckAreaMath.HeightAt(a.HeightPlane, q);
                        Vector2 world = Centre + DeckAreaMath.DeckToWorld(q, h, heading, _elevationDegrees);
                        tested++;
                        if (surface.TryGetDeckElevation(world, out _)) continue;

                        Assert.Fail($"{name} ({heading:F1}°): the POLYGON reader refused a point that is ON " +
                                    $"her '{a.Id}' — deck ({q.x:F2}, {q.y:F2}) at height {h:F2} m draws at " +
                                    $"world ({world.x:F2}, {world.y:F2}). The rival SQUARE reader says " +
                                    $"{TheSquareSays(world)} there. A polygon reader that refuses her own " +
                                    "planking is the projection or the containment test — not the square.");
                    }
                }
                Assert.Greater(tested, 500, $"{name}: too few deck points sampled to mean anything");
            }
        }

        /// <summary>⭐⭐ THE OWNER'S DEFECT. Nothing OFF her drawn deck reads as standable — swept over the
        /// whole extent of the square that used to answer, so a return to it fails here loudly.</summary>
        [Test]
        public void NothingOffHerDrawnDeck_IsStandable()
        {
            float half = _lengthMeters * 0.5f;

            foreach ((string name, float heading) in Headings())
            {
                ArrivalDeck surface = DeckAt(heading, _deck);
                int wetButDry = 0;
                var first = new StringBuilder();

                for (float dx = -half; dx <= half; dx += 0.1f)
                for (float dy = -half; dy <= half; dy += 0.1f)
                {
                    var world = new Vector2(Centre.x + dx, Centre.y + dy);
                    if (_deck.IsOverWalkableDeck(world - Centre, heading, _elevationDegrees, out _)) continue;
                    if (!surface.TryGetDeckElevation(world, out _)) continue;

                    wetButDry++;
                    if (first.Length == 0)
                        first.Append($"first at world ({world.x:F2}, {world.y:F2}), " +
                                     $"{Mathf.Sqrt(dx * dx + dy * dy):F2} m from her pivot — " +
                                     $"SQUARE says {TheSquareSays(world)}, POLYGON says False");
                }

                Assert.AreEqual(0, wetButDry,
                    $"{name} ({heading:F1}°): {wetButDry * 0.01f:F2} m² of open water reads as her deck. " +
                    $"{first}. The square answered True over 166.41 m² against her 23.94 m² of drawn deck; " +
                    "if SQUARE is True and POLYGON is False at that point, the surface is back on the envelope.");
            }
        }

        /// <summary>⭐ The number that justified the change, kept in the run. It measures what the OLD
        /// reader would still answer: not a regression guard, the evidence.</summary>
        [Test]
        public void TheSquareThisReplaced_IsStillThisMuchWater()
        {
            float half = _lengthMeters * 0.5f;

            foreach ((string name, float heading) in Headings())
            {
                int square = 0, polygon = 0, squareOnly = 0, polygonOnly = 0;
                for (float dx = -half - 1f; dx <= half + 1f; dx += 0.05f)
                for (float dy = -half - 1f; dy <= half + 1f; dy += 0.05f)
                {
                    var world = new Vector2(Centre.x + dx, Centre.y + dy);
                    bool sq = TheSquareSays(world);
                    bool pg = _deck.IsOverWalkableDeck(world - Centre, heading, _elevationDegrees, out _);
                    if (sq) square++;
                    if (pg) polygon++;
                    if (sq && !pg) squareOnly++;
                    if (pg && !sq) polygonOnly++;
                }

                const float cell = 0.05f * 0.05f;
                Debug.Log($"[ArrivalDeck] {name} ({heading:F1}°): SQUARE {square * cell:F2} m², her drawn " +
                          $"DECK {polygon * cell:F2} m², water the square called deck {squareOnly * cell:F2} m² " +
                          $"({100f * squareOnly / Mathf.Max(1, square):F1}% of it), deck the square called " +
                          $"water {polygonOnly * cell:F2} m².");

                Assert.Greater(squareOnly * cell, 100f,
                    $"{name}: the square is supposed to be the thing that was WRONG — if it now agrees with " +
                    "her planking, either the deck asset or the projection has moved and this whole guard " +
                    "is measuring something else.");
            }
        }

        /// <summary>⭐ A hull the rigs have never measured keeps the square, and keeps it exactly. Absence
        /// is data: the fix must not make an unmeasured hull's passenger start swimming.</summary>
        [Test]
        public void AHullWithNoAuthoredDeck_KeepsTheSquare()
        {
            float half = _lengthMeters * 0.5f;

            ArrivalDeck surface = DeckAt(37f, null);
            Assert.IsFalse(surface.HasAuthoredDeck, "the fixture wired a deck it was supposed to leave off");

            Assert.IsTrue(surface.TryGetDeckElevation(Centre + new Vector2(half - 0.2f, half - 0.2f),
                                                      out float deck),
                          "an unmeasured hull must keep the shipped envelope, corner and all");
            Assert.IsFalse(surface.TryGetDeckElevation(Centre + new Vector2(half + 0.2f, 0f), out _),
                           "…and must still stop at its edge");
            Assert.AreEqual(Freeboard, deck, 1e-4f,
                            "the ELEVATION law is untouched here: water level (0 with no environment) + freeboard");
        }

        /// <summary>⭐ ONE READER. Whatever the WALK will let her stand on, the standing test calls deck —
        /// asked of the two shipped methods at the same points, in the same frame, at the same heading.
        /// This is the property the square could never have: the walk's bounds and the standing test were
        /// two different shapes, and where two answers differ, one of them is a person on the sea.</summary>
        [Test]
        public void TheStandingTestAndTheWalkClamp_AreTheSameShape()
        {
            foreach ((string name, float heading) in Headings())
            {
                int sampled = 0;
                for (float x = -3f; x <= 3f; x += 0.05f)
                for (float y = -7f; y <= 7f; y += 0.05f)
                {
                    var q = new Vector2(x, y);
                    int hint = -1;
                    Vector2 clamped = _deck.ClampToWalkable(q, ref hint, out float h);
                    if ((clamped - q).sqrMagnitude > 1e-8f) continue;      // the walk would move her: not deck

                    Vector2 world = DeckAreaMath.DeckToWorld(q, h, heading, _elevationDegrees);
                    sampled++;
                    Assert.IsTrue(_deck.IsOverWalkableDeck(world, heading, _elevationDegrees, out float back),
                                  $"{name} ({heading:F1}°): the WALK will stand her at deck ({q.x:F2}, {q.y:F2}) " +
                                  $"— height {h:F2} m, drawn at world ({world.x:F2}, {world.y:F2}) — and the " +
                                  "standing test calls it water. One reader, or somebody stands on the sea. " +
                                  $"(The rival SQUARE would have said {TheSquareSays(Centre + world)}.)");

                    // ⚠ NOT an equality. A raised foredeck 2.3 m over the sole genuinely DRAWS across the
                    // sole behind it, so one screen point is over two decks and the reader returns the
                    // HIGHER — measured on the cape, up to 2.26 m at her turning headings. What must never
                    // happen is the reader answering LOWER than the deck she is actually standing on.
                    Assert.GreaterOrEqual(back, h - 1e-3f,
                        $"{name}: the standing test put her BELOW the planking the walk stands her on " +
                        $"({back:F2} m against {h:F2} m) — the overlap tie is being broken the wrong way");
                }
                Assert.Greater(sampled, 500, $"{name}: too few walkable points to mean anything");
            }
        }
    }
}
