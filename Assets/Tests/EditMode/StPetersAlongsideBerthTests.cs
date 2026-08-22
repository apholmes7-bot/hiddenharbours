using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>SHE LIES ALONGSIDE</b> — the owner's playtest call of 2026-08-22, held as geometry.
    ///
    /// <para>The owner watched the opening after the arrival fix and reported that the boat finishes
    /// with her <b>bow sitting on the planks</b>. It was never a pilot bug: the berth was a point on the
    /// pier's own centre-line one metre off its head, so a 12.9 m hull lying on the channel's axis
    /// reached x = 208.6, which is 5.4 m inside a deck that runs 183 → 214. She was doing exactly what
    /// the region asked her to.</para>
    ///
    /// <para><b>What these tests are for.</b> The old numbers were literals — (215, 0), (213, 0),
    /// (217, 0) — and literals are what let a berth go on describing a pier it no longer fits. Every
    /// number in the new berth is derived from the wharf's own footprint, the wharf's own ladder and
    /// the arrival hull's own def, so these tests re-derive the same way and fail <i>with the number to
    /// use</i>. Nothing here asserts a coordinate.</para>
    ///
    /// <para><b>⚠ Berthing her alongside took a dredge as well as a coordinate, and that is the part
    /// worth understanding.</b> The dredged approach's flat bottom is ±4 m about y = 0 and the pier is
    /// ±3 m — one metre of dredged water beside each face, against a hull that needs 4.8 m of beam
    /// there. Measured on the terrain this branch inherits, the ground under an alongside berth reads
    /// −1.94 m at y = −7 against −2.20 m of water at spring low: she took the ground on both faces.
    /// The answer is a short <b>berth pocket</b> rather than a wider channel, because the channel's
    /// width is the region's draught gate — and re-gating the fleet is a ruling, not a side effect of
    /// an arrival fix. <see cref="TheBerthPocketDigsTheBerthAndNothingElse"/> holds the second half of
    /// that sentence.</para>
    ///
    /// <para><see cref="StPetersEastBerthTests"/> owns the channel that feeds this berth and the
    /// fleet ladder it gates; <see cref="StPetersLayoutTests"/> owns the rest of the region's layout.</para>
    /// </summary>
    public class StPetersAlongsideBerthTests
    {
        private const string CapeIslanderDefPath  = "Assets/_Project/Data/Boats/CapeIslander.asset";
        private const string CapeIslanderMeshPath =
            "Assets/_Project/Data/Boats/HullMeshes/CapeIslanderIsoHullMesh.asset";

        private TidalTerrain _terrain;
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TidalTerrain_AlongsideBerthTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            GameServices.Reset();
        }

        // =============================================================================================
        //  the sources of truth
        // =============================================================================================

        /// <summary>Her footprint at the berth: length along the pier's axis, beam across it. Built from
        /// the berth and the hull's own dimensions, never from a remembered rectangle.</summary>
        private static Rect HullAtTheBerth()
        {
            Vector3 berth = StPetersBuilder.DockZonePos;
            float halfLength = StPetersBuilder.ArrivalHullLengthMetres * 0.5f;
            float halfBeam   = StPetersBuilder.ArrivalHullHalfBeamMetres;
            return Rect.MinMaxRect(berth.x - halfLength, berth.y - halfBeam,
                                   berth.x + halfLength, berth.y + halfBeam);
        }

        private static float SpringLowWater() =>
            StPetersBuilder.TideMean - StPetersBuilder.TideAmplitude;

        // =============================================================================================
        // 1. 🔴 THE BUG ITSELF — she must not be lying on the wharf
        // =============================================================================================

        /// <summary>
        /// 🔴 <b>The owner's report, as an assertion.</b> A hull's footprint at the berth and the deck's
        /// footprint are two rectangles in the same world, and a boat that overlaps the planks she is
        /// tied to is the thing that was seen on screen. Pinned so it cannot come back — a later change
        /// to the deck's length, the ladder's station or the hull's size re-runs the whole derivation
        /// and this fails the moment any of them puts her back on the timber.
        /// </summary>
        [Test]
        public void HerHullLiesOffThePlanks_NotOnThem()
        {
            Rect deck = StPetersWharf.DeckFootprint();
            Rect hull = HullAtTheBerth();

            Assert.IsFalse(hull.Overlaps(deck),
                $"the hull lies over {hull} and the planks are {deck} — she is berthed ON the wharf, " +
                "which is exactly what the owner watched happen. A berth is BESIDE a pier, not through it.");

            // …and she is on the mooring side of it, where the pier's gear actually is.
            Assert.Less(hull.yMax, StPetersWharf.MooringFaceY + 1e-4f,
                $"her inboard rail is at y = {hull.yMax:F2}, on or past the wharf face " +
                $"({StPetersWharf.MooringFaceY:F2}) — she must lie OUTBOARD of the face she is tied to");

            Debug.Log($"[st-peters/alongside] deck {deck}, hull {hull} — clear by " +
                      $"{StPetersWharf.MooringFaceY - hull.yMax:F2} m.");
        }

        /// <summary>
        /// The fendering gap is the ruled 0.3–0.5 m, and it is the gap between HER RAIL and the wharf
        /// face — not between the wharf and her centre-line, which is the measurement that made the old
        /// berth look reasonable while she sat on the deck.
        /// </summary>
        [Test]
        public void SheLiesAFenderOffTheFace_MeasuredFromHerRail()
        {
            float gap = StPetersWharf.MooringFaceY - StPetersBuilder.AlongsideGunwaleY;
            Assert.That(gap, Is.InRange(0.3f, 0.5f),
                $"she lies {gap:F2} m off the wharf face. Alongside means fendered against it: closer " +
                "and she is rubbing timber, further and stepping across stops being a step.");
        }

        // =============================================================================================
        // 2. she lies ALONG the thing she is tied to
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>A berth heading is parallel to the face.</b> The arrival used to read her heading off
        /// the CHANNEL's axis, on the reasoning that a boat lies along the water she came up — true of a
        /// mooring in a fairway, false of a boat made fast to something. Read off the pier, it stays
        /// right if the channel is ever re-cut at an angle to it.
        ///
        /// <para>The two agree today, because the pier was built down the channel's line, and the second
        /// assertion holds them together: if they ever part company this fails and says which one the
        /// berth follows.</para>
        /// </summary>
        [Test]
        public void SheLiesParallelToTheFaceSheIsTiedTo()
        {
            float pierAxis = ArrivalPilot.CompassOf(StPetersWharf.AxisInward());
            Assert.AreEqual(0f,
                Mathf.Abs(ArrivalPilot.Wrap180(StPetersArrivalOpening.BerthHeadingDegrees() - pierAxis)),
                1f,
                $"she lies on {StPetersArrivalOpening.BerthHeadingDegrees():F1}° against a wharf face " +
                $"running {pierAxis:F1}° — a hull alongside lies ALONG the face, or her fenders and her " +
                "lines are describing different boats");

            float channelAxis = ArrivalPilot.CompassOf(
                StPetersBuilder.ApproachTo - StPetersBuilder.ApproachFrom);
            Assert.AreEqual(0f, Mathf.Abs(ArrivalPilot.Wrap180(pierAxis - channelAxis)), 1f,
                $"the pier runs {pierAxis:F1}° and the channel that serves it runs {channelAxis:F1}°. " +
                "That is allowed — but the berth follows the PIER, so if you meant to re-cut the " +
                "channel at an angle, check that she can still be steered onto this face.");

            // And her bow points IN, not out to sea: she came up the channel and stopped.
            Assert.Less(StPetersWharf.AxisInward().x, 0f,
                "the pier's inward axis must run from the head toward the root (west, at St Peters) — " +
                "she carries her approach direction into the berth rather than turning end-for-end in a " +
                "16 m cut");
        }

        // =============================================================================================
        // 3. the step ashore
        // =============================================================================================

        /// <summary>
        /// You step off onto PLANKS, and you step off HER RAIL. The old disembark was 1.5 m from the
        /// berth's centre-line — which, on a hull with a 2.4 m half-beam, is a point inside the boat.
        /// </summary>
        [Test]
        public void TheStepAshoreIsOnThePlanks_AndWithinReachOfHerRail()
        {
            Vector2 ashore = new Vector2(StPetersBuilder.DisembarkPos.x, StPetersBuilder.DisembarkPos.y);
            Rect deck = StPetersWharf.DeckFootprint();

            Assert.IsTrue(deck.Contains(ashore),
                $"the player is put down at ({ashore.x:F2}, {ashore.y:F2}), which is off the deck " +
                $"{deck} — the first step of a new game must land on timber, not in the berth");

            float fromRail = Mathf.Abs(ashore.y - StPetersBuilder.AlongsideGunwaleY);
            Assert.LessOrEqual(fromRail, StPetersBuilder.StepAshoreMetres + 1e-4f,
                $"the landing is {fromRail:F2} m from her rail, past the {StPetersBuilder.StepAshoreMetres:F2} m " +
                "step-ashore pattern — measure it from the GUNWALE, which is the thing a person actually " +
                "steps over, not from the hull's centre-line or the pier's");

            // Abreast of her, not at the far end of the pier.
            Rect hull = HullAtTheBerth();
            Assert.That(ashore.x, Is.InRange(hull.xMin, hull.xMax),
                $"the landing is at x = {ashore.x:F2}, outside the hull's own {hull.xMin:F2}..{hull.xMax:F2} — " +
                "you step ashore beside the boat, not past her bow");

            // …and it is beside the LADDER, which is the fitting the berth is derived from.
            Assert.AreEqual(StPetersWharf.LadderPosition().x, ashore.x, 1e-4f,
                "the landing must be abreast of the ladder — the berth is sited on it, so a landing " +
                "anywhere else means the two have come apart");
        }

        /// <summary>The sail-home arrival still lands INSIDE the dock zone. ControlSwitcher.InDockZone()
        /// is a pure distance test, so a metre too far is a boat you can never step off (#52).</summary>
        [Test]
        public void TheSailHomeArrivalLandsInsideTheDockZone()
        {
            float d = Vector2.Distance(
                new Vector2(StPetersBuilder.ArrivalPos.x, StPetersBuilder.ArrivalPos.y),
                new Vector2(StPetersBuilder.DockZonePos.x, StPetersBuilder.DockZonePos.y));
            Assert.LessOrEqual(d, StPetersBuilder.DockZoneRadius,
                $"the arrival parks {d:F2} m off the berth against a {StPetersBuilder.DockZoneRadius:F1} m " +
                "dock zone");

            // She parks SEAWARD of the berth — she has not been asked to run up past it and back.
            Assert.Greater(StPetersBuilder.ArrivalPos.x, StPetersBuilder.DockZonePos.x,
                "the arrival must park seaward of the berth: the pier's inward axis runs west, so a " +
                "boat parked inshore of her own berth has been driven through it");
        }

        // =============================================================================================
        // 4. ⭐ SHE FLOATS THERE — the 2026-08-19 ruling, at the new berth
        // =============================================================================================

        /// <summary>
        /// 🔴 <b>The east dock always has water</b> (owner, 2026-08-19; <c>docs/design/scene-sizing-and-
        /// world-scale.md</c> §5.1a "OVERRULED for the east dock"). St Peters' rule is the OPPOSITE of
        /// Nine Mile Creek's channel-wet/berths-bare: this berth is the game's front door and it is wet
        /// at every state of tide.
        ///
        /// <para>⚠ Asserted over her WHOLE FOOTPRINT, not at the berth point. A hull alongside lies
        /// across the shoulder of the cut she is in; the berth's centre can be at the design depth while
        /// her outboard chine is on ground that bares. That is precisely how this berth failed before
        /// the pocket was cut — the centre read −4.00 m and the far side read −1.94 m.</para>
        /// </summary>
        [Test]
        public void SheFloatsAtSpringLowOverHerWholeFootprint_NotJustAtTheBerthPoint()
        {
            float springLow = SpringLowWater();
            float draught   = StPetersBuilder.ArrivalHullDraughtMetres;
            float ukc       = StPetersBuilder.BerthUnderKeelClearance;
            Rect hull = HullAtTheBerth();

            float worstBed = float.MinValue; Vector2 worstAt = Vector2.zero;
            for (float x = hull.xMin; x <= hull.xMax + 1e-3f; x += 0.25f)
            for (float y = hull.yMin; y <= hull.yMax + 1e-3f; y += 0.25f)
            {
                float bed = _terrain.ElevationAt(new Vector2(x, y));
                if (bed > worstBed) { worstBed = bed; worstAt = new Vector2(x, y); }
            }

            float depth = springLow - worstBed;
            Assert.GreaterOrEqual(depth, draught + ukc - 1e-3f,
                $"the shallowest ground under her is {worstBed:F2} m at ({worstAt.x:F2}, {worstAt.y:F2}), " +
                $"leaving {depth:F2} m of water at spring low ({springLow:F2} m) against her " +
                $"{draught:F2} m draught plus {ukc:F2} m under-keel clearance. The east dock is ruled wet " +
                $"at EVERY state of tide — dredge the berth pocket to {springLow - draught - ukc:F2} m or " +
                "move the berth to water that is already there.");

            Debug.Log($"[st-peters/alongside] shallowest bed under her {worstBed:F2} m → {depth:F2} m of " +
                      $"water at spring low; she draws {draught:F2} m + {ukc:F2} m.");
        }

        /// <summary>
        /// ⚠ <b>The journey, not the feature.</b> A berth pocket that holds her and does not JOIN the
        /// channel is a hole she cannot be driven into. Walked from the channel's inner waypoint —
        /// which is the berth itself, since the fairway's last mark is derived from the dock zone —
        /// back out along the approach, demanding her draught the whole way at the lowest water.
        /// </summary>
        [Test]
        public void TheDredgedWaterIsCONTINUOUS_FromTheChannelIntoTheBerth()
        {
            float springLow = SpringLowWater();
            float need = StPetersBuilder.ArrivalHullDraughtMetres;

            Vector2 berth = new Vector2(StPetersBuilder.DockZonePos.x, StPetersBuilder.DockZonePos.y);
            Vector2 mouth = StPetersBuilder.ApproachFrom;

            float worst = float.MaxValue; Vector2 worstAt = Vector2.zero;
            int steps = Mathf.CeilToInt(Vector2.Distance(berth, mouth) * 4f);
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(berth, mouth, i / (float)steps);
                float d = springLow - _terrain.ElevationAt(p);
                if (d < worst) { worst = d; worstAt = p; }
            }

            Assert.GreaterOrEqual(worst, need - 1e-3f,
                $"running from the berth out to the channel mouth, the water pinches to {worst:F2} m at " +
                $"({worstAt.x:F1}, {worstAt.y:F1}) against her {need:F2} m draught at spring low. The " +
                "berth pocket and the approach must be ONE piece of dredged water — a pocket that does " +
                "not join the channel is a berth she cannot be driven into.");

            // The fairway's inner mark IS the berth — one statement about where she stops, not two.
            Vector2[] route = StPetersArrivalOpening.Route();
            Assert.AreEqual(0f, Vector2.Distance(route[route.Length - 1], berth), 1e-4f,
                "the entrance channel's inner waypoint must BE the berth — the route is read off the " +
                "buoyed fairway, so a berth authored anywhere else means she is conned to one place and " +
                "tied up at another");

            Debug.Log($"[st-peters/alongside] berth → channel mouth carries {worst:F2} m at its worst at " +
                      $"spring low; she draws {need:F2} m.");
        }

        // =============================================================================================
        // 5. ⭐ the pocket digs the BERTH — and nothing the region already decided
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The additive guarantee, again.</b> Same shape as
        /// <c>StPetersEastBerthTests.TheDredgeMovesTheChannelAndNothingElse</c> and for the same reason:
        /// compared against a terrain configured exactly as the region is and then had the berth pocket
        /// switched OFF, so the only difference between the two is the feature under test.
        ///
        /// <para>The tightest of these by a long way is the deck's south lip. It is the +1.09 m shoal
        /// beside the pier, 6.79 m from this pocket's nearest end against a half-width of
        /// <see cref="StPetersBuilder.BerthPocketHalfWidth"/> — so a pocket lengthened toward the shore,
        /// or widened, digs out ground the pier is standing on, and the deck's height is measured off
        /// that ground.</para>
        /// </summary>
        [Test]
        public void TheBerthPocketDigsTheBerthAndNothingElse()
        {
            var beforeGo = new GameObject("TidalTerrain_WithoutBerthPocket");
            try
            {
                var before = beforeGo.AddComponent<TidalTerrain>();
                StPetersBuilder.ConfigureTidalTerrain(before);
                var so = new SerializedObject(before);
                so.FindProperty("_berthPocketHalfWidth").floatValue = 0f;   // the feature, switched off
                so.ApplyModifiedPropertiesWithoutUndo();

                Rect deck = StPetersWharf.DeckFootprint();
                var probes = new (string what, Vector2 at)[]
                {
                    ("the pier root",            new Vector2(StPetersWharf.RootCellX, 0f)),
                    ("the pier root north edge", new Vector2(StPetersWharf.RootCellX, StPetersWharf.MaxCellY)),
                    ("the pier root south edge", new Vector2(StPetersWharf.RootCellX, StPetersWharf.MinCellY)),
                    ("the beach slip's head",    StPetersBuilder.BerthTo),
                    ("the deck's north lip",     new Vector2(deck.center.x, deck.yMax + 1f)),
                    ("the deck's south lip",     new Vector2(deck.center.x, deck.yMin - 1f)),
                    ("the deck's east lip",      new Vector2(deck.xMax + 1f, 0f)),
                    ("the beach above the slip", new Vector2(StPetersBuilder.BerthTo.x - 4f, 0f)),
                    ("the slip's own bed",       new Vector2(StPetersBuilder.BerthTo.x + 5f, 0f)),
                    ("the reef ring, north",     new Vector2(215f,  40f)),
                    ("the reef ring, south",     new Vector2(215f, -40f)),
                    ("the sandbar",              new Vector2(-200f, 0f)),
                    ("the dory's mooring",       new Vector2(StPetersBuilder.DoryMooredPos.x,
                                                             StPetersBuilder.DoryMooredPos.y)),
                };

                foreach ((string what, Vector2 at) in probes)
                    Assert.AreEqual(before.ElevationAt(at), _terrain.ElevationAt(at), 1e-5f,
                        $"{what} at ({at.x:F1}, {at.y:F1}) MOVED when the berth pocket was cut — it is " +
                        "outside the pocket and must not have. Shorten it, narrow it, or move the berth.");

                // The consequence those probes exist to protect: the planks stay clear of the tide.
                float root = _terrain.ElevationAt(new Vector2(StPetersWharf.RootCellX, 0f));
                Assert.Greater(root, StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude,
                    $"the pier root sits at {root:F2} m against spring high water — the deck height is " +
                    "MEASURED off this ground, so a berth that dug it out floods the wharf");
            }
            finally
            {
                Object.DestroyImmediate(beforeGo);
            }
        }

        /// <summary>
        /// The pocket's own clearance from the nearest thing it must not disturb, stated as a number
        /// rather than left to the probe test to discover. Fails <i>before</i> the elevation test does,
        /// and says which constant to pull.
        /// </summary>
        [Test]
        public void ThePocketStaysClearOfTheGroundThePierStandsOn()
        {
            Rect deck = StPetersWharf.DeckFootprint();
            var mustNotReach = new (string what, Vector2 at)[]
            {
                ("the deck's south lip", new Vector2(deck.center.x, deck.yMin - 1f)),
                ("the deck's north lip", new Vector2(deck.center.x, deck.yMax + 1f)),
                ("the pier root",        new Vector2(StPetersWharf.RootCellX, 0f)),
                ("the deck's east lip",  new Vector2(deck.xMax + 1f, 0f)),
            };

            foreach ((string what, Vector2 at) in mustNotReach)
            {
                float d = DistanceToSegment(at, StPetersBuilder.BerthPocketFrom,
                                                StPetersBuilder.BerthPocketTo);
                Assert.Greater(d, StPetersBuilder.BerthPocketHalfWidth,
                    $"{what} lies {d:F2} m from the berth pocket's centre-line against its " +
                    $"{StPetersBuilder.BerthPocketHalfWidth:F2} m half-width — the cut reaches it. " +
                    "Narrow StPetersBuilder.BerthPocketShoulderMetres or shorten the pocket.");
            }

            // The flat has to hold her WHOLE beam, or she touches on one side as the tide falls.
            Assert.GreaterOrEqual(StPetersBuilder.BerthPocketThalwegHalfWidth,
                                  StPetersBuilder.ArrivalHullHalfBeamMetres,
                "the pocket's flat bottom is narrower than the hull's half-beam — half of her is on the " +
                "shoulder, which is the shape of the bug this pocket was cut to fix");

            // And it is dug to the same depth as the channel that feeds it.
            Assert.AreEqual(StPetersBuilder.ApproachBedElevation, StPetersBuilder.BerthPocketBedElevation,
                            1e-4f,
                "the berth is dredged to a different depth from its own approach — a berth shallower " +
                "than the channel is one you can reach and cannot lie in");
        }

        // =============================================================================================
        // 6. the mirrors — a const cannot load an asset
        // =============================================================================================

        /// <summary>
        /// <see cref="StPetersBuilder.ArrivalHullLengthMetres"/> and
        /// <see cref="StPetersBuilder.ArrivalHullHalfBeamMetres"/> exist only because the berth
        /// arithmetic has to be a compile-time expression. They are safe exactly while this holds, and
        /// the whole berth is sized off them — so a re-measured hull that forgets them silently berths
        /// the wrong boat.
        /// </summary>
        [Test]
        public void TheHullMirrorsStillMatchHerOwnDefAndSidecar()
        {
            var hull = AssetDatabase.LoadAssetAtPath<HiddenHarbours.Boats.BoatHullDef>(CapeIslanderDefPath);
            Assert.IsNotNull(hull, $"the arrival hull's def must exist at {CapeIslanderDefPath}");
            Assert.AreEqual(StPetersBuilder.ArrivalHullId, hull.Id,
                "the def at the arrival hull's path is not the hull the region names");
            Assert.AreEqual(hull.LengthMeters, StPetersBuilder.ArrivalHullLengthMetres, 1e-4f,
                $"the region berths a {StPetersBuilder.ArrivalHullLengthMetres:F2} m hull but her def " +
                $"says {hull.LengthMeters:F2} m. Update StPetersBuilder.ArrivalHullLengthMetres — the " +
                "berth pocket's length is cut from it.");

            var mesh = AssetDatabase.LoadAssetAtPath<HullMeshDef>(CapeIslanderMeshPath);
            Assert.IsNotNull(mesh, $"the arrival hull's mesh sidecar must exist at {CapeIslanderMeshPath}");
            Assert.AreEqual(mesh.WatertightHalfBeamMeters, StPetersBuilder.ArrivalHullHalfBeamMetres, 1e-4f,
                $"the region lies her {StPetersBuilder.ArrivalHullHalfBeamMetres:F2} m off the face but " +
                $"her sidecar reports a {mesh.WatertightHalfBeamMeters:F2} m half-beam. Update " +
                "StPetersBuilder.ArrivalHullHalfBeamMetres — the fendering gap and the pocket's flat " +
                "bottom are both measured from it.");
        }

        // =============================================================================================
        //  helpers
        // =============================================================================================

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 <= 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
            return Vector2.Distance(p, a + t * ab);
        }
    }
}
