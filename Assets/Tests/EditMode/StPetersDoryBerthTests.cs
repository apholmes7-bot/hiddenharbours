using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.World;
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
    }
}
