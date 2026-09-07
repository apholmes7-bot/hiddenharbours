#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐⭐ <b>A BOAT AT THE WALL IS IN THE WATER — the owner's playtest, 2026-09-06, 06:11 at the Nine
    /// Mile Creek north wall: <i>"boats arent touching but not sitting in water"</i>.</b>
    ///
    /// <para><b>What was measured before any code moved</b> (on the owner's own scene and the committed
    /// <c>logCrib</c> sheet, with no engine at all): the face pieces stand at y 84.6235 and the sheet's
    /// facing-4 ink runs 2.625 units below its pivot, so the wall's picture covered the sea from y
    /// <b>82.00</b> to its lip at <b>87.00</b> — five units of drawn timber standing over the water at
    /// <b>every</b> state of the tide. The five hulls at the wall lie at y 84.00-84.50. So the sea's drawn
    /// edge was 2.0-2.5 units below every one of them at mean water, and #753's ride then carried them
    /// another 1.685 units up the wall on the flood and down on the ebb — a gap that <b>moved by 3.37
    /// units across the tide and was never once positive</b>. That number names the hypothesis: the hull
    /// rides and the water's drawn edge does not.</para>
    ///
    /// <para><b>What this fixture holds.</b> Not that a hull's waterline and the wall's COINCIDE — they
    /// must not, and a test that demanded it would be wrong. A hull lying <c>d</c> metres off the wall
    /// draws <c>d</c> units nearer the camera, so her waterline sits below the wall's by exactly
    /// <c>d − deck × HeightScale</c>. What must hold is that this offset is <b>hers alone and does not
    /// move with the tide</b> — the signature of one rule ridden by both — and that it is positive, which
    /// is what "she is in the water rather than on a shelf" means.</para>
    ///
    /// <para><b>And it can see the defect.</b> <see cref="TheUncutFaceIsTheDefect_AndThisFixtureSeesIt"/>
    /// runs the identical measurement against the face as main draws it (uncut, its sea edge pinned at the
    /// sprite's own foot) and reports the same table failing. A guard that cannot fail on the picture it
    /// was written about is a mirror.</para>
    /// </summary>
    public class NineMileCreekQuayFaceWaterlineTests
    {
        private GameObject _terrainGo;
        private MainlandTidalTerrain _terrain;

        /// <summary>Spring low, mean and spring high — the three states the charter names, taken from the
        /// region rather than typed.</summary>
        private static IEnumerable<(string Name, float Level)> TideStates => new[]
        {
            ("spring low", NineMileCreekMainland.SpringLowWater),
            ("mean water", NineMileCreekMainland.TideMean),
            ("spring high", NineMileCreekMainland.SpringHighWater),
        };

        [SetUp]
        public void SetUp()
        {
            _terrainGo = new GameObject("TidalTerrain");
            _terrain = _terrainGo.AddComponent<MainlandTidalTerrain>();
            NineMileCreekBuilder.ConfigureNineMileCreekTerrain(_terrain);
        }

        [TearDown]
        public void TearDown()
        {
            if (_terrainGo != null) Object.DestroyImmediate(_terrainGo);
            _terrainGo = null;
            _terrain = null;
        }

        // -----------------------------------------------------------------------------------------
        //  The two pictures, side by side: the wall as it is drawn now, and the wall as main draws it
        // -----------------------------------------------------------------------------------------

        /// <summary>The north wall's own face course — the run the fleet lies against.</summary>
        private static NineMileCreekDressing.FacePiece NorthWallPiece() =>
            NineMileCreekDressing.FacePieces()
                .First(p => p.Wall == NineMileCreekDressing.NorthWallRun);

        /// <summary>Where the sea's drawn edge meets the north wall at this state of tide, in world
        /// units — the row above which the wall is drawn and below which the water is.</summary>
        private float DrawnSeaEdgeAtTheWall(float seaLevel, bool faceRidesTheTide)
        {
            NineMileCreekDressing.FacePiece piece = NorthWallPiece();
            float lipY = piece.Lip.y;

            if (faceRidesTheTide)
                return TidalFaceWaterline.WaterlineWorldY(
                    lipY, NineMileCreekDressing.FaceLipElevation(piece, _terrain), seaLevel);

            // ⚠️ MAIN'S PICTURE. Nothing cuts the sprite, so the highest row of sea anybody can see at
            // the wall is the row the wall's own ink stops on — its foot, DrawnFaceDropMetres below the
            // lip, which is 1.40 m of rig mud below the LOWEST water this coast has (#737 put it there
            // deliberately so no bare gap could open under the wall). Measured on the committed sheet
            // the ink stops 5.001 units under the lip against this 5.056, a 1.8 px chamfer; the code's
            // number is used because it is the one a change would move.
            return lipY - NineMileCreekQuayFace.DrawnFaceDropMetres;
        }

        /// <summary>Every owner who lies at the wall, in berth order.</summary>
        private static List<BoatOwnerDef> WallOwners() =>
            NineMileCreekMooredFleet.LoadOwners()
                .Where(o => o != null && o.Moorage == BoatMoorage.QuayWall)
                .OrderBy(o => o.BerthIndex)
                .ToList();

        /// <summary>Where this owner's hull lies in plan — her OWN standoff, which is her half-beam plus
        /// a fender since #751.</summary>
        private static Vector2 BerthOf(BoatOwnerDef owner) =>
            NineMileCreekMainland.BerthPos(owner.BerthIndex, NineMileCreekMooredFleet.HalfBeamOf(owner));

        /// <summary>
        /// The row this hull's DRAWN waterline sits on at this state of tide: her plan line, plus the ride
        /// the shipped code gives her. Composed out of <see cref="TidalRide"/> exactly as
        /// <c>HullTideRide</c> composes it — her waterline is the sea until the bed takes her, and a metre
        /// of it draws <see cref="IsoGround.HeightScale"/> up-screen — and against her BAKED waterline of
        /// 0, which is what <c>BoatHullSkinner</c> installs and what a plain plan point means.
        /// </summary>
        private float HullWaterlineY(BoatOwnerDef owner, float seaLevel)
        {
            Vector2 berth = BerthOf(owner);
            float bed = _terrain.ElevationAt(berth);
            float draught = owner.Boat != null ? owner.Boat.DraughtMeters : 0f;
            return berth.y + TidalRide.ScreenRise(TidalRide.Waterline(seaLevel, bed, draught), 0f);
        }

        /// <summary>
        /// The tolerance: ONE PIXEL of the drawn wall, from the pack's own grid
        /// (<see cref="NineMileCreekQuayFace.PackPixelMetres"/>) rather than a decimal chosen to make a
        /// test pass. Anything finer than a pixel of the face cannot be seen and must not be asserted.
        ///
        /// <para>⚠️ It is taken from the QUOTED grid rather than from the sliced sprite because this
        /// assembly deliberately cannot see <c>IsoPackSprites</c> — <c>NineMileCreekStationTests</c> says
        /// so in as many words, and that blindness is what keeps the region's own copy of the pack's
        /// numbers honest rather than circular.</para>
        /// </summary>
        private static float OnePixel() => NineMileCreekQuayFace.PackPixelMetres;

        // -----------------------------------------------------------------------------------------
        //  The guards
        // -----------------------------------------------------------------------------------------

        /// <summary>
        /// ⭐⭐ <b>ONLY THE RUNS THAT DRAW A FACE AT THIS CAMERA MAY BE CUT — and three of this wharf's
        /// six do not.</b>
        ///
        /// <para>The whole cut rests on one claim: below an east–west course's lip there is nothing but
        /// wall, so a row of its pixels IS an elevation. On a NORTH–SOUTH run that claim is false. Screen
        /// x is world x, so such a wall projects to a line and has no drawn face at all; what the course
        /// contributes is its DECK and its built END, standing at their own plan northings. Measured on
        /// the committed sheet, facings 2 and 6 draw <b>4.156 units below their pivot</b> against facing
        /// 4's 2.625 — and the extra 1.53 is PLAN, not height. A world-y cut applied there would eat the
        /// southern edge of the apron's own deck: a worse defect than the one being fixed, and one that
        /// would render perfectly.</para>
        ///
        /// <para>So the apron's east and west faces and the wharf head keep the shipped picture. That is
        /// not a gap in the fix — it is what "don't promise a face there" means.</para>
        /// </summary>
        [Test]
        public void OnlyTheRunsThatDrawAFaceAtThisCameraAreCut()
        {
            var expected = new Dictionary<string, bool>
            {
                { NineMileCreekDressing.NorthWallRun, true },     // seaward SOUTH — the mooring face
                { NineMileCreekDressing.ApronSouthRun, true },    // seaward SOUTH — the seaward corner
                { NineMileCreekDressing.BreakwaterRun, true },    // seaward SOUTH — the arm's outer side
                { NineMileCreekDressing.WestWallRun, false },     // seaward EAST  — a line at this camera
                { NineMileCreekDressing.ApronWestRun, false },    // seaward WEST  — likewise
                { NineMileCreekDressing.QuayHeadRun, false },     // seaward EAST  — likewise
            };

            var report = new StringBuilder();
            var wrong = new List<string>();
            var seen = new HashSet<string>();

            foreach (var piece in NineMileCreekDressing.FacePieces())
            {
                Vector2 seaward = NineMileCreekDressing.PlanDirectionOf(piece.Heading);
                bool cut = NineMileCreekQuayFace.DrawsAFaceAtThisCamera(seaward);
                if (seen.Add(piece.Wall))
                    report.AppendLine(
                        $"  {piece.Wall,-14} seaward ({seaward.x:+0.0;-0.0}, {seaward.y:+0.0;-0.0})  " +
                        (cut ? "draws a face — the sea may climb it"
                             : "a line at this camera — left whole"));

                Assert.IsTrue(expected.ContainsKey(piece.Wall),
                    $"a run named '{piece.Wall}' has appeared and nobody has said which way it looks — " +
                    "decide before it ships, because a wrong answer renders perfectly");
                if (cut != expected[piece.Wall])
                    wrong.Add($"{piece.Wall}: seaward {seaward} says cut={cut}, " +
                              $"expected {expected[piece.Wall]}");
            }

            Debug.Log("[quay-face-waterline] the six runs:\n" + report);
            Assert.IsEmpty(wrong, string.Join("\n", wrong) + "\n" + report);
            Assert.AreEqual(expected.Count, seen.Count,
                "every run this wharf draws must be accounted for here:\n" + report);
        }

        /// <summary>
        /// ⚠️ <b>H2, REFUTED ONCE AND MEASURED — no wall hull takes the ground.</b> The charter's second
        /// hypothesis was that the fleet was simply sitting on the basin bed, and it matters here beyond
        /// ruling itself out: a grounded hull's picture STOPS falling, so every "the gap does not move"
        /// assertion below would be measuring the mud instead of the rule.
        ///
        /// <para>⚠️ It also corrects a number this project carried for a day. #753 measured the wall fleet
        /// grounding at water −0.303 m over a bed at <c>BasinBedElevation</c>, because the berth trench did
        /// not reach the berths. #751 re-cut the seabed and it does now: the bed under every wall berth
        /// reads about −4.19 m, which carries a 1.30 m hull through spring low with 0.7 m to spare.</para>
        /// </summary>
        [Test]
        public void NoWallHullTakesTheGroundAtSpringLow()
        {
            var owners = WallOwners();
            Assert.IsNotEmpty(owners, "nobody lies at the wall — there is nothing to measure");

            float low = NineMileCreekMainland.SpringLowWater;
            var report = new StringBuilder($"Measured on the built terrain at spring low ({low:0.00} m):\n");
            var aground = new List<string>();

            foreach (var o in owners)
            {
                Vector2 berth = BerthOf(o);
                float bed = _terrain.ElevationAt(berth);
                float draught = o.Boat != null ? o.Boat.DraughtMeters : 0f;
                bool takesTheGround = TidalRide.IsAground(low, bed, draught);
                report.AppendLine(
                    $"  berth {o.BerthIndex,2}  {o.Id,-28} at {berth}  bed {bed,7:0.00} m  " +
                    $"draught {draught:0.00} m  keel {low - draught,7:0.00} m  " +
                    $"{(takesTheGround ? "AGROUND" : "afloat")}");
                if (takesTheGround) aground.Add(o.Id);
            }

            Debug.Log("[quay-face-waterline] " + report);
            Assert.IsEmpty(aground,
                "these hulls sit on the bottom at spring low, so their pictures stop falling with the " +
                "tide and no waterline rule can be measured against them:\n" + report);
        }

        /// <summary>
        /// ⭐⭐ <b>THE GUARD. At every state of the tide, a hull at the wall meets the water BELOW the line
        /// the wall meets it on, by an amount that is hers and does not move.</b>
        ///
        /// <para>Both halves matter and they fail differently. <b>Positive</b> is "she is in the water":
        /// her waterline is nearer the camera than the wall's, which is what lying <c>d</c> metres off it
        /// means. <b>Constant</b> is "one rule": the offset is <c>d − deck × HeightScale</c>, a pure
        /// geometry of her berth, and any term that rides while the other does not shows up here as
        /// movement — 3.37 units of it across this range, which is the whole tidal ride.</para>
        /// </summary>
        [Test]
        public void EveryWallHullMeetsTheWaterBelowTheWallsOwnWaterline_ByTheSameAmountAtEveryTide()
        {
            var owners = WallOwners();
            Assert.IsNotEmpty(owners, "nobody lies at the wall — there is nothing to measure");

            float tolerance = OnePixel();
            float deck = NineMileCreekDressing.FaceLipElevation(NorthWallPiece(), _terrain);
            var report = new StringBuilder(
                $"The wall's lip is y {NorthWallPiece().Lip.y:0.000} at {deck:0.00} m; " +
                $"one pixel is {tolerance:0.0000} units.\n");
            var failures = new List<string>();

            foreach (var o in owners)
            {
                float standoff = NineMileCreekWharf.MooringEdgeY - BerthOf(o).y;
                float expected = standoff - deck * IsoGround.HeightScale;
                report.AppendLine($"  berth {o.BerthIndex,2}  {o.Id,-28} standoff {standoff:0.00} m " +
                                  $"⇒ she should meet it {expected:+0.000;-0.000} below the wall's line");

                foreach (var (name, level) in TideStates)
                {
                    float wall = DrawnSeaEdgeAtTheWall(level, faceRidesTheTide: true);
                    float hull = HullWaterlineY(o, level);
                    float gap = wall - hull;
                    report.AppendLine(
                        $"      {name,-12} sea {level,5:0.00} m   wall's edge y {wall:0.000}   " +
                        $"her waterline y {hull:0.000}   gap {gap:+0.000;-0.000}");

                    if (gap <= 0f)
                        failures.Add($"{o.Id} at {name}: she is drawn {-gap:0.000} units ABOVE the " +
                                     "water's edge on the wall — a model on a shelf");
                    if (Mathf.Abs(gap - expected) > tolerance)
                        failures.Add($"{o.Id} at {name}: the gap is {gap:0.000} where her berth says " +
                                     $"{expected:0.000} — {Mathf.Abs(gap - expected) / tolerance:0.0} px " +
                                     "out, so the wall and the hull are not riding one rule");
                }
            }

            Debug.Log("[quay-face-waterline] " + report);
            Assert.IsEmpty(failures, string.Join("\n", failures) + "\n\n" + report);
        }

        /// <summary>
        /// ⭐⭐ <b>AND THE SAME MEASUREMENT, AGAINST THE PICTURE MAIN DRAWS.</b> The face uncut, its sea
        /// edge pinned at the sprite's own foot: every hull is above the water at every state of the tide,
        /// and the amount she is above it by MOVES with the tide instead of standing still. This is the
        /// owner's screenshot, in numbers — and it is here so that the guard above is known to be capable
        /// of failing on the defect it was written about rather than being a mirror of the code.
        /// </summary>
        [Test]
        public void TheUncutFaceIsTheDefect_AndThisFixtureSeesIt()
        {
            var owners = WallOwners();
            Assert.IsNotEmpty(owners, "nobody lies at the wall — there is nothing to measure");

            var report = new StringBuilder(
                "The face as main draws it: one static sprite, its foot " +
                $"{NineMileCreekQuayFace.DrawnFaceDropMetres:0.000} units below the lip, at every tide.\n");
            float widest = 0f, narrowest = float.PositiveInfinity;

            foreach (var o in owners)
            {
                report.AppendLine($"  berth {o.BerthIndex,2}  {o.Id}");
                foreach (var (name, level) in TideStates)
                {
                    float wall = DrawnSeaEdgeAtTheWall(level, faceRidesTheTide: false);
                    float hull = HullWaterlineY(o, level);
                    float gap = wall - hull;
                    report.AppendLine(
                        $"      {name,-12} sea {level,5:0.00} m   sea's edge y {wall:0.000}   " +
                        $"her waterline y {hull:0.000}   gap {gap:+0.000;-0.000}");

                    Assert.Less(gap, 0f,
                        $"{o.Id} at {name} was expected to be drawn ABOVE the uncut wall's sea edge — " +
                        "if this ever stops being true the defect has been fixed somewhere else and " +
                        "this fixture is no longer measuring what it claims:\n" + report);

                    widest = Mathf.Max(widest, -gap);
                    narrowest = Mathf.Min(narrowest, -gap);
                }
            }

            float swing = 2f * NineMileCreekMainland.TideAmplitude * IsoGround.HeightScale;
            report.AppendLine(
                $"Worst {widest:0.000} units of daylight under a hull, best {narrowest:0.000}, and the " +
                $"gap moves {swing:0.000} units across the range — the whole of the tidal ride, which is " +
                "the term the water's picture was not taking.");
            Debug.Log("[quay-face-waterline] " + report);

            Assert.Greater(widest, swing * 0.5f,
                "the uncut wall should hide the sea from a hull by more than half a tide's ride — if it " +
                "does not, the geometry this fixture was written against has changed:\n" + report);
        }
    }
}
#endif

