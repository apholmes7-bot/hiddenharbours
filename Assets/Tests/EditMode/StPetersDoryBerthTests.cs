using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.World;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE STARTER DORY'S BERTH — surveyed, not chosen by eye.</b>
    ///
    /// <para>The owner watched the opening on 2026-09-02 and said the demo dory is in the way of the
    /// arriving boat. She had been moved once for that same complaint already (#677, 2026-08-27) and it
    /// came back, so this file answers it in metres.</para>
    ///
    /// <para><b>⚠ What the measurement actually found.</b> The sailing line was never the problem: over
    /// the real passage the cape passed the 2026-08-27 berth with 4.97 m of clear water
    /// (<c>ArrivalOverRealTerrainPlayTests</c>). What WAS wrong is that she had no heading at all —
    /// <c>PersistentCoreBuilder</c> set her position and never her rotation, so a 4.5 m boat lay bow-north
    /// ATHWART an east–west fairway. Nothing had caught it because every clearance in this region
    /// modelled her as a CIRCLE of her 0.85 m half-beam, 1.40 m short of her own stern. She now lies
    /// ALONGSIDE the pier's north face at the pilehead, and these are the pins that hold her there.</para>
    /// </summary>
    public class StPetersDoryBerthTests
    {
        private TidalTerrain _terrain;
        private GameObject _go;

        private const string GameConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";

        /// <summary>
        /// ⭐ The owner's boarding reach, read from the <b>shipped asset</b>.
        ///
        /// <para>⚠ This was a hard-coded <c>3.5f</c> mirror until 2026-09-03, and it had to be: the real
        /// number was <c>ControlSwitcher._boardReach</c>, a serialized <i>private</i> field, so there was
        /// nothing a test could name. PR 2 made it <see cref="GameConfig.BoardReachMetres"/> — and
        /// reading the asset is the entire point of having done that. A mirror left behind would go on
        /// asserting 3.5 the moment the owner tuned the reach to anything else, and this test would
        /// report the berth as accessible on a number the game no longer uses.</para>
        /// </summary>
        private static float BoardReachMetres
        {
            get
            {
                var config = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigAssetPath);
                Assert.IsNotNull(config, $"the shipped {GameConfigAssetPath} must exist — this berth's " +
                                         "accessibility is measured against the owner's own reach");
                return config.BoardReachMetres;
            }
        }

        /// <summary>The owner's step-ashore reach, read from the same shipped asset and for the same
        /// reason. ⚠ It is measured from her WALKABLE deck edge, not her outline — a hull's walking strip
        /// is set in from her rail — so a berth's step-ashore column is
        /// <c>(half-beam − walkable half-beam) + the gap to the planks</c>.</summary>
        private static float StepAshoreReachMetres
        {
            get
            {
                var config = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigAssetPath);
                Assert.IsNotNull(config, $"the shipped {GameConfigAssetPath} must exist");
                return config.StepAshoreReachMetres;
            }
        }

        /// <summary>Her authored walkable half-beam — the strip a fisher may stand on, inset from her
        /// rail. Read off her own deck data rather than restated here.</summary>
        private static float WalkableHalfBeamMetres
        {
            get
            {
                var deck = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatDeckDef>(
                    "Assets/_Project/Data/Boats/Decks/DoryIso.asset");
                Assert.IsNotNull(deck, "the starter dory's authored deck must exist");
                return deck.WalkHalfExtents.x;
            }
        }

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TidalTerrain_DoryBerthTest");
            _terrain = _go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            GameServices.Reset();
        }

        /// <summary>Water level at a point in the swing: −1 is spring low, +1 spring high.</summary>
        private static float Water(float t) =>
            StPetersBuilder.TideMean + StPetersBuilder.TideAmplitude * t;

        private static float SpringLow => Water(-1f);

        private static BoatHullDef Dory =>
            UnityEditor.AssetDatabase.LoadAssetAtPath<BoatHullDef>("Assets/_Project/Data/Boats/Dory.asset");

        /// <summary>Her outline, as the region authors it.</summary>
        private static HullFootprint HerBerth() => HullFootprint.FromHeading(
            new Vector2(StPetersBuilder.DoryMooredPos.x, StPetersBuilder.DoryMooredPos.y),
            StPetersBuilder.DoryMooredHeadingDegrees,
            StPetersBuilder.DoryLengthMetres, StPetersBuilder.DoryHalfBeamMetres);

        /// <summary>The SHALLOWEST seabed anywhere under her outline — the point that grounds her.
        /// Sampled on a grid rather than at her centre, which is the whole lesson of this file.</summary>
        private float WorstBedUnder(HullFootprint her)
        {
            float worst = float.NegativeInfinity;
            const int alongSteps = 12, abeamSteps = 6;
            for (int i = 0; i <= alongSteps; i++)
            {
                float along = Mathf.Lerp(-her.HalfLength, her.HalfLength, i / (float)alongSteps);
                for (int j = 0; j <= abeamSteps; j++)
                {
                    float abeam = Mathf.Lerp(-her.HalfBeam, her.HalfBeam, j / (float)abeamSteps);
                    worst = Mathf.Max(worst, _terrain.ElevationAt(
                        her.Center + her.BowDirection * along + her.StarboardDirection * abeam));
                }
            }
            return worst;
        }

        // =============================================================================================
        //  1. the mirrors — a hull's own numbers, copied into a const, held equal to their source
        // =============================================================================================

        [Test]
        public void HerLengthMirrorStillMatchesHerDef()
        {
            Assert.IsNotNull(Dory, "the starting dory's def must exist");
            Assert.AreEqual(Dory.LengthMeters, StPetersBuilder.DoryLengthMetres, 1e-4f,
                $"the region measures her berth against a {StPetersBuilder.DoryLengthMetres:F2} m boat " +
                $"but her def says {Dory.LengthMeters:F2} m. Update StPetersBuilder.DoryLengthMetres — " +
                "every clearance she is part of is computed from it.");
        }

        /// <summary>
        /// ⭐ She lies ALONGSIDE — parallel to the pier — and not athwart it. This is the pin for the
        /// actual 2026-09-02 defect: before it, she had no authored heading at all and took the
        /// identity, which is bow due north, which is across this pier and across this fairway.
        /// </summary>
        [Test]
        public void SheLiesAlongsideThePier_NotAcrossIt()
        {
            float pier = Mathf.Atan2(StPetersWharf.AxisInward().x,
                                     StPetersWharf.AxisInward().y) * Mathf.Rad2Deg;
            float across = Mathf.Abs(Mathf.Sin((StPetersBuilder.DoryMooredHeadingDegrees - pier)
                                               * Mathf.Deg2Rad));
            Assert.Less(across, 1e-3f,
                $"she lies on {StPetersBuilder.DoryMooredHeadingDegrees:F1}° against a pier on " +
                $"{pier:F1}° — that is a boat moored ACROSS her own wharf. A moored heading is half of " +
                "where a boat is; leaving it to the identity is what put her athwart the fairway.");

            // …and the builder must actually hand that heading to the persistent core, or the constant
            // is a claim about a scene nobody made.
            Assert.Greater(Mathf.Abs(StPetersBuilder.DoryMooredHeadingDegrees), 1e-3f,
                "this pier runs east–west, so an alongside heading here can never be 0° (north). A 0 " +
                "here means the derivation has fallen back to the identity again.");
        }

        // =============================================================================================
        //  2. ⭐ the three things a berth has to be
        // =============================================================================================

        /// <summary>⭐ She floats — at the worst water this region has, under EVERY part of her.</summary>
        [Test]
        public void SheFloatsAtHerBerth_AtEveryStateOfTheTide_UnderHerWholeOutline()
        {
            HullFootprint her = HerBerth();
            float bed = WorstBedUnder(her);
            float depth = SpringLow - bed;

            Assert.Greater(depth, Dory.DraughtMeters,
                $"the shallowest ground under her outline is {bed:F2} m, which leaves {depth:F2} m of " +
                $"water at spring low against a {Dory.DraughtMeters:F2} m draught. ⚠ Measured under her " +
                "whole hull, not at her centre: the note this replaced read the bed at one point and " +
                "reported it as a boat's clearance, which is the same circle-for-a-hull mistake that " +
                "put her athwart the fairway.");

            Debug.Log($"[dory-berth] she lies at ({her.Center.x:F2}, {her.Center.y:F2}) on " +
                      $"{StPetersBuilder.DoryMooredHeadingDegrees:F0}°, outline x " +
                      $"{her.Center.x - her.HalfLength:F2}..{her.Center.x + her.HalfLength:F2}, y " +
                      $"{her.Center.y - her.HalfBeam:F2}..{her.Center.y + her.HalfBeam:F2}; worst bed " +
                      $"{bed:F2} m → {depth:F2} m at spring low against {Dory.DraughtMeters:F2} m draught.");
        }

        /// <summary>
        /// ⭐ She is ACCESSIBLE — the owner's word. Her outline is within boarding reach of the pier's
        /// planks, so you step off the deck straight aboard rather than swimming to your own boat.
        /// </summary>
        [Test]
        public void SheIsBoardableFromThePlanks()
        {
            HullFootprint her = HerBerth();
            Rect deck = StPetersWharf.DeckFootprint();

            float best = float.MaxValue;
            Vector2 from = Vector2.zero;
            foreach (var cell in StPetersWharf.DeckCellsBackToFront())
            {
                var p = new Vector2(cell.x + 0.5f, cell.y + 0.5f);
                float d = her.DistanceTo(p);
                if (d < best) { best = d; from = p; }
            }

            Assert.Less(best, BoardReachMetres,
                $"the nearest deck cell ({from.x:F1}, {from.y:F1}) is {best:F2} m from her outline, " +
                $"against a {BoardReachMetres:F2} m boarding reach. The owner asked for her to be " +
                "ACCESSIBLE; a berth you cannot step onto is not that.");

            Debug.Log($"[dory-berth] nearest deck cell ({from.x:F1}, {from.y:F1}) is {best:F2} m off " +
                      $"her outline (reach {BoardReachMetres:F2} m); deck is x " +
                      $"{deck.xMin}..{deck.xMax}, y {deck.yMin}..{deck.yMax}.");
        }

        /// <summary>
        /// ⭐ …and she is not UNDER the pier. <c>StPetersVillageTests</c> states this as
        /// <c>DoryMooredPos.x &gt; HeadCellX</c>, which is a proxy that only works while she lies out on
        /// the channel's centre-line. This is the honest version — her OUTLINE against the deck's — and
        /// it is what has to hold now that she lies alongside a face rather than off the head.
        /// </summary>
        [Test]
        public void HerOutlineClearsThePlanks_SoSheIsAMooringYouCanSee()
        {
            HullFootprint her = HerBerth();
            Rect deck = StPetersWharf.DeckFootprint();

            float best = float.MaxValue;
            const int steps = 120;
            for (int i = 0; i <= steps; i++)
            {
                float tx = Mathf.Lerp(deck.xMin, deck.xMax, i / (float)steps);
                float ty = Mathf.Lerp(deck.yMin, deck.yMax, i / (float)steps);
                best = Mathf.Min(best, her.DistanceTo(new Vector2(tx, deck.yMin)));
                best = Mathf.Min(best, her.DistanceTo(new Vector2(tx, deck.yMax)));
                best = Mathf.Min(best, her.DistanceTo(new Vector2(deck.xMin, ty)));
                best = Mathf.Min(best, her.DistanceTo(new Vector2(deck.xMax, ty)));
            }

            Assert.Greater(best, 0f,
                "her outline overlaps the pier deck — a mooring under the planks is a mooring you " +
                "cannot see. She is supposed to lie OFF the face by the fendering gap.");

            Assert.AreEqual(StPetersBuilder.AlongsideFenderGapMetres, best, 0.02f,
                $"she lies {best:F2} m off the planks where the fendering gap says " +
                $"{StPetersBuilder.AlongsideFenderGapMetres:F2} m. The gap is the whole reason the berth " +
                "is derived rather than nudged, so a drift here means a term went missing.");
        }

        // =============================================================================================
        //  3. the survey — kept, because the next person to move her should not have to rebuild this rig
        // =============================================================================================

        /// <summary>
        /// 📏 The seabed's cross-section north of the channel's centre-line, off the pier head, and the
        /// berth she was moved from measured beside the one she was moved to. Logged, not asserted:
        /// the numbers are the finding.
        /// </summary>
        [Test]
        public void TheWaterNorthOfTheFairway_IsSurveyed()
        {
            var said = new System.Text.StringBuilder(
                $"[dory-berth] seabed north of the fairway (spring low = {SpringLow:F2} m; a " +
                $"{Dory.DraughtMeters:F2} m dory needs a bed below {SpringLow - Dory.DraughtMeters:F2} m):\n");

            foreach (float x in new[] { 209f, 211.5f, 213.5f, 215f, 218f })
            {
                said.Append($"  x = {x,6:F1}:");
                for (float y = 0f; y <= 8.01f; y += 1f)
                    said.Append($"  y{y:F0}={_terrain.ElevationAt(new Vector2(x, y)),6:F2}");
                said.Append('\n');
            }
            said.Append("  the shoulder, at x = 213.5:");
            for (float y = 3.5f; y <= 7.01f; y += 0.25f)
                said.Append($"  {y:F2}={_terrain.ElevationAt(new Vector2(213.5f, y)):F2}");
            said.Append('\n');

            foreach ((string what, Vector2 at, float heading) in new[]
                     {
                         ("#677's berth, athwart (215.00, 3.15)", new Vector2(215f, 3.15f), 0f),
                         ("…the same berth, laid alongside",      new Vector2(215f, 3.15f), 270f),
                         ("TODAY: north face at the pilehead",
                          new Vector2(StPetersBuilder.DoryMooredPos.x, StPetersBuilder.DoryMooredPos.y),
                          StPetersBuilder.DoryMooredHeadingDegrees),
                     })
            {
                var her = HullFootprint.FromHeading(at, heading, StPetersBuilder.DoryLengthMetres,
                                                    StPetersBuilder.DoryHalfBeamMetres);
                float bed = WorstBedUnder(her);
                said.Append(
                    $"  {what,-38} worst bed {bed,6:F2} → {SpringLow - bed,5:F2} m at spring low; " +
                    $"outline y {her.Center.y - Extent(her, Vector2.up),5:F2}.." +
                    $"{her.Center.y + Extent(her, Vector2.up),5:F2}\n");
            }

            Debug.Log(said.ToString());
            Assert.Pass("a survey, not a claim");
        }

        /// <summary>Half her extent projected on an axis — for reporting the outline's span.</summary>
        private static float Extent(HullFootprint her, Vector2 axis) =>
            her.HalfLength * Mathf.Abs(Vector2.Dot(axis, her.BowDirection))
            + her.HalfBeam * Mathf.Abs(Vector2.Dot(axis, her.StarboardDirection));

        // =============================================================================================
        //  4. ⭐ THE ARRIVAL'S OWN PATH — she is not in the way of the boat she came in on (2026-09-07)
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The cape's COMMANDED path, walked, against the dory's outline.</b>
        ///
        /// <para><b>Why this is a path and not a point.</b> The owner has now said three times that the
        /// demo dory is in the way of the arriving boat (2026-08-27, 09-02, 09-07). Every answer before
        /// this one measured the berth against a POSITION — the arrival's, the dock zone's — and a boat
        /// is not where she stops, she is everywhere she goes on the way. So this walks the route the
        /// region actually authors, with the last mark swapped for the approach GATE exactly as
        /// <see cref="BerthingPilot"/>'s constructor swaps it, then the gate's station, then the berth,
        /// and holds the dory's outline against the cape's at every pose.</para>
        ///
        /// <para><b>⚠ What this is NOT.</b> It is the path she is COMMANDED along, not the one she
        /// sails: a 12.9 m hull on a 17.7 m turning circle cuts inside her own route at the corners, and
        /// clearance on the chart is not clearance in the water — that is the 2026-08-27 lesson in one
        /// sentence. The sailed track is measured every frame by
        /// <c>ArrivalOverRealTerrainPlayTests.SheClearsEveryMarkOnTheWayIn_AndTheMooredDory</c>, and that
        /// is the number a berth is chosen on. This guard is the cheap, always-run half: it cannot pass
        /// a berth the chart already condemns, and it needs no GPU to say so.</para>
        /// </summary>
        [Test]
        public void SheIsClearOfTheArrivalsCommandedPath()
        {
            HullFootprint her = HerBerth();
            float worst = WorstGapOverTheApproach(her, out Vector2 where, out float capeHeading);

            Assert.Greater(worst, StPetersBuilder.AlongsideFenderGapMetres,
                $"the arrival's outline comes within {worst:F2} m of the dory's on the commanded path — " +
                $"nearest with the cape at ({where.x:F2}, {where.y:F2}) on {capeHeading:F0}°. A moored " +
                $"boat is owed at least the fendering gap " +
                $"({StPetersBuilder.AlongsideFenderGapMetres:F2} m) of clear water from a hull that is " +
                "being steered past her.");

            Debug.Log($"[dory-berth] the arrival's commanded path clears her by {worst:F2} m " +
                      $"outline-to-outline (nearest with the cape at ({where.x:F2}, {where.y:F2}) on " +
                      $"{capeHeading:F0}°).");
        }

        /// <summary>
        /// 📏 <b>The berth survey the owner chooses from.</b> Every candidate against the four things a
        /// berth for this dory has to be: clear of the arrival, a short walk from where the player steps
        /// ashore, wet at spring low, and alongside something you can step onto. Logged, not asserted —
        /// the numbers are the finding, and the row that ships is
        /// <see cref="StPetersBuilder.DoryMooredPos"/>.
        /// </summary>
        [Test]
        public void TheBerthCandidates_AreSurveyed()
        {
            Rect deck = StPetersWharf.DeckFootprint();
            float faceY = StPetersWharf.NorthFaceY + StPetersBuilder.AlongsideFenderGapMetres
                          + StPetersBuilder.DoryHalfBeamMetres;
            float southY = StPetersWharf.MooringFaceY - StPetersBuilder.AlongsideFenderGapMetres
                           - StPetersBuilder.DoryHalfBeamMetres;
            var ashore = new Vector2(StPetersBuilder.DisembarkPos.x, StPetersBuilder.DisembarkPos.y);

            HullFootprint cape = HullFootprint.FromHeading(
                new Vector2(StPetersBuilder.DockZonePos.x, StPetersBuilder.DockZonePos.y),
                StPetersArrivalOpening.BerthHeadingDegrees(),
                StPetersBuilder.ArrivalHullLengthMetres, StPetersBuilder.ArrivalHullHalfBeamMetres);

            var rows = new (string what, Vector2 at)[]
            {
                ("TODAY — north face, at the pilehead",
                 new Vector2(StPetersBuilder.DoryMooredPos.x, StPetersBuilder.DoryMooredPos.y)),
                ("A — north face, one hull west of the head",
                 new Vector2(deck.xMax - 0.5f - StPetersBuilder.DoryLengthMetres, faceY)),
                ("B — north face, abreast the ladder",
                 new Vector2(StPetersWharf.LadderPosition().x, faceY)),
                ("C — north face, two hulls west of the head",
                 new Vector2(deck.xMax - 0.5f - 2f * StPetersBuilder.DoryLengthMetres, faceY)),
                // Why the working face is not an option: derived from where the cape's own bow lies,
                // so it stays the honest 'just west of her' spot if her berth ever moves.
                ("D — SOUTH face, just west of the cape",
                 new Vector2(cape.BowPoint.x - StPetersBuilder.DoryLengthMetres, southY)),
            };

            var said = new System.Text.StringBuilder();
            said.Append($"[dory-berth] candidates (spring low {SpringLow:F2} m, draught ");
            said.Append($"{Dory.DraughtMeters:F2} m, fender gap ");
            said.Append($"{StPetersBuilder.AlongsideFenderGapMetres:F2} m). 'clear' = outline-to-outline ");
            said.Append("over the arrival's COMMANDED path; 'toCape' = to her berthed outline; 'walk' = ");
            said.Append("from the disembark point to the dory's outline; 'planks' = to the wharf deck.");
            said.Append('\n');
            said.Append($"  {"berth",-44}{"clear",8}{"toCape",8}{"walk",8}{"bed",8}{"depth",8}" +
                        $"{"planks",8}{"stepOff",9}");
            said.Append('\n');

            foreach ((string what, Vector2 at) in rows)
            {
                HullFootprint her = HullFootprint.FromHeading(
                    at, StPetersBuilder.DoryMooredHeadingDegrees,
                    StPetersBuilder.DoryLengthMetres, StPetersBuilder.DoryHalfBeamMetres);
                float bed = WorstBedUnder(her);
                said.Append($"  {what,-44}{WorstGapOverTheApproach(her, out _, out _),8:F2}");
                said.Append($"{cape.SignedGapTo(her),8:F2}{her.DistanceTo(ashore),8:F2}");
                // ⭐ column (d): is there a step ashore from her DECK? The reach is measured from
                // her walkable strip, which is inset from her rail — so the span it must cover is
                // that inset plus the water between her rail and the planks.
                float toPlanks = GapToTheDeck(her, deck);
                float stepOff = (StPetersBuilder.DoryHalfBeamMetres - WalkableHalfBeamMetres) + toPlanks;
                said.Append($"{bed,8:F2}{SpringLow - bed,8:F2}{toPlanks,8:F2}");
                said.Append($"{stepOff,6:F2}{(stepOff <= StepAshoreReachMetres ? " ok" : " NO"),3}");
                said.Append('\n');
            }

            Debug.Log(said.ToString());
            Assert.Pass("a survey, not a claim");
        }

        /// <summary>
        /// The arrival's COMMANDED path as poses — the region's own route with the berth swapped for the
        /// gate (<see cref="BerthingPilot"/>'s own substitution), the gate's station, and the berth. The
        /// heading is the leg's, and it is swept through at each mark: a hull standing still while her
        /// bow comes round is the worst case for a stern that swings, and it costs nothing.
        /// </summary>
        private static List<(Vector2 at, float heading)> CommandedApproach()
        {
            Vector2[] route = StPetersArrivalOpening.Route();
            Vector2 berthPos = StPetersArrivalOpening.Berth();
            float berthHeading = StPetersArrivalOpening.BerthHeadingDegrees();
            BerthPilot.Settings alongside = BerthPilot.Settings.Default;
            BerthPilot.Berth berth = BerthPilot.Berth.FromShorePoint(
                berthPos, berthHeading, StPetersArrivalOpening.StepAshore(),
                StPetersBuilder.ArrivalHullLengthMetres);

            var marks = new List<Vector2>(route);
            marks[marks.Count - 1] = BerthPilot.Gate(berth, alongside);
            marks.Add(berthPos + berth.Seaward * alongside.GateStandoffMetres);
            marks.Add(berthPos);

            var poses = new List<(Vector2, float)>();
            for (int i = 0; i + 1 < marks.Count; i++)
            {
                Vector2 a = marks[i], b = marks[i + 1];
                float leg = i >= marks.Count - 3 ? berthHeading : ArrivalPilot.CompassOf(b - a);
                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector2.Distance(a, b) / 0.5f));
                for (int s = 0; s <= steps; s++) poses.Add((Vector2.Lerp(a, b, s / (float)steps), leg));
                if (i + 2 >= marks.Count) continue;
                float next = i + 1 >= marks.Count - 3
                    ? berthHeading : ArrivalPilot.CompassOf(marks[i + 2] - b);
                for (int s = 0; s <= 24; s++) poses.Add((b, Mathf.LerpAngle(leg, next, s / 24f)));
            }
            return poses;
        }

        /// <summary>The tightest outline-to-outline gap between the arrival and <paramref name="her"/>
        /// anywhere on the commanded path, and where the cape was when it happened.</summary>
        private static float WorstGapOverTheApproach(HullFootprint her, out Vector2 where,
                                                     out float capeHeading)
        {
            where = Vector2.zero;
            capeHeading = 0f;
            float worst = float.MaxValue;
            foreach ((Vector2 at, float heading) in CommandedApproach())
            {
                HullFootprint cape = HullFootprint.FromHeading(
                    at, heading, StPetersBuilder.ArrivalHullLengthMetres,
                    StPetersBuilder.ArrivalHullHalfBeamMetres);
                float gap = cape.SignedGapTo(her);
                if (gap >= worst) continue;
                worst = gap; where = at; capeHeading = heading;
            }
            return worst;
        }

        /// <summary>How far her outline lies off the wharf deck's edge — the fendering gap, where she is
        /// lying against a face.</summary>
        private static float GapToTheDeck(HullFootprint her, Rect deck)
        {
            float best = float.MaxValue;
            const int steps = 240;
            for (int i = 0; i <= steps; i++)
            {
                float tx = Mathf.Lerp(deck.xMin, deck.xMax, i / (float)steps);
                float ty = Mathf.Lerp(deck.yMin, deck.yMax, i / (float)steps);
                best = Mathf.Min(best, her.DistanceTo(new Vector2(tx, deck.yMin)));
                best = Mathf.Min(best, her.DistanceTo(new Vector2(tx, deck.yMax)));
                best = Mathf.Min(best, her.DistanceTo(new Vector2(deck.xMin, ty)));
                best = Mathf.Min(best, her.DistanceTo(new Vector2(deck.xMax, ty)));
            }
            return best;
        }
    }
}
