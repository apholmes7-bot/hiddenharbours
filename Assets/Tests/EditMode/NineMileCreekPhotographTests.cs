using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE WHARF AS THE PHOTOGRAPH SHOWS IT</b> — the owner's satellite view of Nine Mile Creek
    /// (2026-08-10) traced into the region's own metres: the floating dock inside the basin, the slipway
    /// off the apron, the rock armour where made ground meets open water, and the building lots along
    /// Wharf Road with the boatyard among them.
    ///
    /// <para><b>Every bound is DERIVED from <see cref="NineMileCreekMainland"/>, never copied out of
    /// it</b> — the file-wide convention here, and what makes this a check rather than a second copy of
    /// the plan. Where a literal appears it is because a test needs to say what a metre MEANS (a hull's
    /// beam, a walking lane), not because geography was restated.</para>
    ///
    /// <para><b>⭐ THE LOAD-BEARING TEST IN THIS FILE IS <see cref="TheGrownGroundTouchesNoPassageAndNotTheBar"/>.</b>
    /// The photograph pass grows made ground, and the standing constraint on it was that the seam-pinned
    /// tidal bar and every passage stay exactly where they are. That is not a thing you can eyeball once
    /// and trust: a fill is a rectangle and a passage is a point, and the day somebody widens the spit by
    /// another twenty metres the sea door quietly ends up standing on dry land with nothing failing.</para>
    /// </summary>
    public class NineMileCreekPhotographTests
    {
        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            GameServices.Reset();
        }

        private MainlandTidalTerrain MakeCreekTerrain()
        {
            var go = new GameObject("TidalTerrain");
            _spawned.Add(go);
            var terrain = go.AddComponent<MainlandTidalTerrain>();
            NineMileCreekBuilder.ConfigureNineMileCreekTerrain(terrain);
            return terrain;
        }

        private static Rect RectOfZone(MainlandZone z) =>
            new Rect(z.Center.x - z.HalfSize.x, z.Center.y - z.HalfSize.y,
                     z.HalfSize.x * 2f, z.HalfSize.y * 2f);

        /// <summary>Every fill the region raises, as world rectangles — what "is this on made ground?"
        /// is really asking.</summary>
        private static IEnumerable<Rect> Fills() =>
            NineMileCreekMainland.Fills.Select(RectOfZone);

        // =============================================================================================
        //  1. THE GROUND GREW, AND ONLY WHERE IT WAS ALLOWED TO
        // =============================================================================================

        [Test]
        public void TheSpitGrewEastOnly_AndStaysOnTheHarbourShoal()
        {
            Rect spit = RectOfZone(NineMileCreekMainland.SpitFill);
            Rect shoal = RectOfZone(NineMileCreekMainland.HarbourShoalFill);

            // East, because that is where the shoal already reaches and where the photograph puts the
            // yard. The spit's own north and south edges are the ones the coast plan is authored around.
            Assert.That(spit.yMin, Is.EqualTo(96f).Within(1e-3f),
                "the spit's south edge must not move — the north wall is built off it");
            Assert.That(spit.yMax, Is.EqualTo(140f).Within(1e-3f),
                "the spit must not grow NORTH: past y = 140 it would put made ground in the beach and " +
                "dune sectors the coast plan authors, which is a coastline change and a different PR");
            Assert.That(spit.xMax, Is.LessThanOrEqualTo(shoal.xMax),
                "the spit's new east end must stand on the harbour shoal, not out over the bay floor");
        }

        [Test]
        public void TheGrownGroundStandsAboveSpringHighWater()
        {
            var terrain = MakeCreekTerrain();
            Rect spit = RectOfZone(NineMileCreekMainland.SpitFill);

            // Sampled across the NEW eastern ground specifically — the old spit was already proved.
            for (float x = 158f; x <= spit.xMax - 1f; x += 4f)
                for (float y = spit.yMin + 2f; y <= spit.yMax - 2f; y += 6f)
                {
                    float e = terrain.ElevationAt(new Vector2(x, y));
                    Assert.That(e, Is.GreaterThan(NineMileCreekMainland.SpringHighWater),
                        $"the spit's new ground at ({x},{y}) is {e:0.00} m — it floods at spring high");
                }
        }

        /// <summary>
        /// ⭐ THE CONSTRAINT THE WHOLE PASS WAS BUILT UNDER: the crossing and every door stay put. Checked
        /// as "no fill has grown over them" rather than by restating their coordinates, so it keeps
        /// holding when the geography is re-tuned for some other reason.
        /// </summary>
        [Test]
        public void TheGrownGroundTouchesNoPassageAndNotTheBar()
        {
            var doors = new (string Name, Vector2 At)[]
            {
                ("the sea door to the West Water",
                 new Vector2(NineMileCreekMainland.ToWestWaterPassagePos.x,
                             NineMileCreekMainland.ToWestWaterPassagePos.y)),
                ("the crossing to St Peters",
                 new Vector2(NineMileCreekMainland.ToStPetersPassagePos.x,
                             NineMileCreekMainland.ToStPetersPassagePos.y)),
                ("the return to Coddle Cove",
                 new Vector2(NineMileCreekMainland.ToCovePassagePos.x,
                             NineMileCreekMainland.ToCovePassagePos.y)),
                ("the walk-in arrival off the bar",
                 new Vector2(NineMileCreekMainland.WalkArrivalPos.x,
                             NineMileCreekMainland.WalkArrivalPos.y)),
            };

            foreach (var (name, at) in doors)
                foreach (Rect fill in Fills())
                    Assert.IsFalse(fill.Contains(at),
                        $"{name} at {at} now stands on made ground ({fill}). A door on dry land is a door " +
                        "that never fires, and the standing constraint on the photograph pass is that the " +
                        "bar and every passage do not move.");

            // …and the bar itself, sampled along its authored line.
            for (float t = 0f; t <= 1f; t += 0.05f)
            {
                Vector2 on = Vector2.Lerp(NineMileCreekMainland.BarFrom, NineMileCreekMainland.BarTo, t);
                foreach (Rect fill in Fills())
                    Assert.IsFalse(fill.Contains(on),
                        $"the tidal bar at {on} runs through made ground ({fill}) — the crossing is " +
                        "seam-pinned to St Peters and cannot be built over.");
            }
        }

        // =============================================================================================
        //  2. THE FLOATING DOCK — the photograph's finger run
        // =============================================================================================

        [Test]
        public void TheFloatRunLiesInTheBasin_ClearOfTheWallAndTheBreakwater()
        {
            float y = NineMileCreekMainland.FloatRunY;
            Rect deck = NineMileCreekWharf.DeckFootprint();

            Assert.That(y, Is.LessThan(NineMileCreekWharf.MooringEdgeY),
                "the float lies IN the basin, south of the mooring edge");
            Assert.That(y, Is.GreaterThan(NineMileCreekWharf.BreakwaterFootprint().yMax),
                "the float must lie inside the breakwater, not on top of it");

            // A boat lies alongside the float on either side; the fleet at the WALL lies off the wall.
            // The two must not be trying to occupy the same water. A lobster boat's beam is about 4 m.
            const float beam = 4f;
            float floatBoatsNorthEdge = y + beam;
            float wallBoatsSouthEdge = NineMileCreekMainland.FirstBerthPos.y - beam * 0.5f;
            Assert.That(floatBoatsNorthEdge, Is.LessThan(wallBoatsSouthEdge),
                $"a boat on the float's north side reaches y={floatBoatsNorthEdge:0.#} and a boat at the " +
                $"wall reaches down to y={wallBoatsSouthEdge:0.#} — they overlap");

            Assert.That(NineMileCreekMainland.FloatRunWestX, Is.GreaterThan(deck.xMin));
            Assert.That(NineMileCreekMainland.FloatRunEastX, Is.LessThan(deck.xMax),
                "the run must stay inside the basin the quay encloses");
        }

        [Test]
        public void TheGangwayReachesFromTheApronsFaceToTheFloat()
        {
            Vector2 shore = NineMileCreekMainland.GangwayShoreEnd;
            Vector2 afloat = NineMileCreekMainland.GangwayFloatEnd;
            Rect apron = NineMileCreekWharf.ApronFootprint();

            Assert.That(shore.x, Is.EqualTo(apron.xMax).Within(1e-3f),
                "the brow leaves from the apron's own east face — derived, so re-siting the wall takes " +
                "it along instead of leaving it reaching for a wall that moved");
            Assert.That(shore.y, Is.EqualTo(afloat.y).Within(1e-3f),
                "a gangway runs across, not along");
            Assert.That(Vector2.Distance(shore, afloat), Is.GreaterThan(2f).And.LessThan(30f),
                "a brow that short is a step and one that long is a bridge");
        }

        [Test]
        public void EveryFloatCleatSitsOnTheRun()
        {
            var cleats = NineMileCreekWharf.FloatCleatPositions();
            Assert.IsNotEmpty(cleats, "a float you cannot tie to is a raft");

            foreach (Vector2 c in cleats)
            {
                Assert.That(c.y, Is.EqualTo(NineMileCreekMainland.FloatRunY).Within(1e-3f));
                Assert.That(c.x, Is.GreaterThanOrEqualTo(NineMileCreekMainland.FloatRunWestX));
                Assert.That(c.x, Is.LessThanOrEqualTo(NineMileCreekMainland.FloatRunEastX),
                    "a cleat past the end of the float is a cleat in the water");
            }
        }

        // =============================================================================================
        //  3. THE SLIPWAY AND THE ARMOUR
        // =============================================================================================

        [Test]
        public void TheSlipwayRunsFromTheApronsFaceIntoTheBasin()
        {
            Vector2 head = NineMileCreekMainland.SlipwayHeadPos;
            Vector2 toe = NineMileCreekMainland.SlipwayToePos;
            Rect apron = NineMileCreekWharf.ApronFootprint();

            Assert.That(head.x, Is.EqualTo(apron.xMax).Within(1e-3f),
                "the ramp starts at the service wall's own face");
            Assert.That(toe.x, Is.GreaterThan(head.x), "and falls east, into the basin");
            Assert.That(head.y, Is.GreaterThan(NineMileCreekWharf.BreakwaterFootprint().yMax),
                "the ramp must not run out over the breakwater");
            Assert.That(head.y, Is.LessThan(NineMileCreekMainland.FloatRunY),
                "…nor under the floating dock");
        }

        [Test]
        public void TheShoreArmourCoversBothExposedFacesAndNothingElse()
        {
            var blocks = NineMileCreekWharf.ShoreArmourBlocks();
            Assert.IsNotEmpty(blocks, "the photograph shows rubble wherever made ground meets open water");

            float headX = NineMileCreekWharf.DeckFootprint().xMax;
            float spitX = RectOfZone(NineMileCreekMainland.SpitFill).xMax;

            foreach (var b in blocks)
                Assert.That(Mathf.Approximately(b.Position.x, headX) ||
                            Mathf.Approximately(b.Position.x, spitX), Is.True,
                    $"armour at {b.Position} is on neither exposed face (the wharf head at x={headX:0.#} " +
                    $"nor the spit's east edge at x={spitX:0.#})");

            // The seaward tip of each run is battered, exactly as the breakwater's is.
            Assert.That(blocks.Count(b => b.Name == "end"), Is.EqualTo(2),
                "two runs, two battered ends");
        }

        // =============================================================================================
        //  4. THE LOTS ALONG WHARF ROAD
        // =============================================================================================

        [Test]
        public void TheBoatyardStandsOnMadeGround_ClearOfTheRoadAndTheQuay()
        {
            Rect yard = NineMileCreekMainland.ShipyardFootprint();
            Rect spit = RectOfZone(NineMileCreekMainland.SpitFill);

            Assert.That(spit.Contains(new Vector2(yard.xMin, yard.yMin)) &&
                        spit.Contains(new Vector2(yard.xMax, yard.yMax)), Is.True,
                $"Harris & Sons ({yard}) must stand entirely on the spit ({spit}) — the whole reason the " +
                "made ground grew east is that a smallYard needs 27 m and there was not 27 m free");

            Assert.IsFalse(yard.Overlaps(NineMileCreekWharf.DeckFootprint()),
                "the yard must not stand on the quay deck");

            float toRoad = NineMileCreekMainland.DistanceToRoute(
                NineMileCreekMainland.WharfRoad, new Vector2(yard.center.x, yard.center.y));
            Assert.That(toRoad, Is.GreaterThan(NineMileCreekMainland.RoadHalfWidth + yard.height * 0.5f),
                "Wharf Road's cleared corridor runs through the yard");
        }

        [Test]
        public void EveryOwnerOnTheRegisterHasALotOnTheYard_OutOfTheWorkingSites()
        {
            Rect spit = RectOfZone(NineMileCreekMainland.SpitFill);
            var owners = NineMileCreekMooredFleet.LoadOwners();

            foreach (var owner in owners)
            {
                Vector2 lot = NineMileCreekMainland.OwnerShedLot(owner.LotIndex);
                Assert.IsTrue(spit.Contains(lot),
                    $"{owner.Id}'s shed lot {owner.LotIndex} at {lot} is off the made ground");
                Assert.IsFalse(NineMileCreekMainland.IsWorkingSiteInTheWay(lot),
                    $"{owner.Id}'s shed lot {owner.LotIndex} at {lot} stands on one of the plan's own " +
                    "working sites — the row is supposed to STEP AROUND them, not be nudged off them");
            }
        }

        /// <summary>
        /// ⭐ The register cannot outgrow the yard. This is the test that would have caught the eighth
        /// owner: the shed walk yields seven lots on this spit (the shanty row, then east past the bait
        /// shed and the trap store), and an eighth <c>LotIndex</c> falls through to the row's clamp — two
        /// sheds in one place, drawn as one.
        /// </summary>
        [Test]
        public void TheRegisterFitsTheLotsTheYardAffords()
        {
            int afforded = NineMileCreekMainland.OwnerShedLotCount;
            var owners = NineMileCreekMooredFleet.LoadOwners();

            Assert.That(owners.Count, Is.LessThanOrEqualTo(afforded),
                $"{owners.Count} owners are authored and the yard affords {afforded} shed lots. The " +
                "extra owner's lot falls to the row's clamp and lands on top of a neighbour. Either the " +
                "spit grows or the register does not.");

            foreach (var owner in owners)
                Assert.That(owner.LotIndex, Is.LessThan(afforded),
                    $"{owner.Id} claims lot {owner.LotIndex} and the row only reaches {afforded - 1}");
        }

        [Test]
        public void NoTwoOwnerShedLotsShareGround()
        {
            var lots = NineMileCreekMainland.OwnerShedLots();

            for (int i = 0; i < lots.Count; i++)
                for (int j = i + 1; j < lots.Count; j++)
                    Assert.That(Vector2.Distance(lots[i], lots[j]),
                        Is.GreaterThanOrEqualTo(NineMileCreekMainland.WharfShedRadius * 2f),
                        $"shed lots {i} and {j} overlap at {lots[i]} / {lots[j]}");
        }

        [Test]
        public void EveryReservedLotNamesThePresetItIsWaitingOn()
        {
            var lots = NineMileCreekLots.ReservedLots();
            Assert.IsNotEmpty(lots, "the photograph shows buildings no kit bakes; they must still be named");

            foreach (var lot in lots)
            {
                Assert.IsNotEmpty(lot.Name, "a reserved lot with no name is a gap nobody can act on");
                Assert.IsNotEmpty(lot.Reason,
                    $"'{lot.Name}' must say what it is for — a lot that cannot say that is the one to cut");
                Assert.That(lot.Radius, Is.GreaterThan(0f), $"'{lot.Name}' reserves no ground");
            }
        }

        /// <summary>
        /// ⭐ No two claimed lots stand on each other — reserved OR an owner's. This is the test that
        /// would have caught the bait cluster: sited on the shed row it landed two metres off the owners'
        /// own lots at x = 164 and 178, which draws as one building and reads as a bug nobody can name.
        /// Reserved ground is only worth claiming if the claims are exclusive.
        /// </summary>
        [Test]
        public void NoTwoClaimedLotsStandOnEachOther()
        {
            var all = new List<(string Name, Vector2 At, float Radius)>();
            foreach (var lot in NineMileCreekLots.ReservedLots())
                all.Add((lot.Name, lot.Position, lot.Radius));
            foreach (var lot in NineMileCreekLots.OwnerLots(NineMileCreekMooredFleet.LoadOwners()))
                all.Add((lot.Name, lot.Position, lot.Radius));

            for (int i = 0; i < all.Count; i++)
                for (int j = i + 1; j < all.Count; j++)
                {
                    float need = all[i].Radius + all[j].Radius;
                    Assert.That(Vector2.Distance(all[i].At, all[j].At), Is.GreaterThanOrEqualTo(need),
                        $"'{all[i].Name}' at {all[i].At} and '{all[j].Name}' at {all[j].At} overlap — " +
                        $"they need {need:0.#} m between them");
                }
        }

        [Test]
        public void EveryClaimedLotStandsOnTheYard_ClearOfTheRoadAndTheQuay()
        {
            Rect spit = RectOfZone(NineMileCreekMainland.SpitFill);
            Rect quay = NineMileCreekWharf.DeckFootprint();

            var all = NineMileCreekLots.ReservedLots()
                .Concat(NineMileCreekLots.OwnerLots(NineMileCreekMooredFleet.LoadOwners()));

            foreach (var lot in all)
            {
                Assert.IsTrue(spit.Contains(lot.Position),
                    $"'{lot.Name}' at {lot.Position} is off the made ground");
                Assert.IsFalse(quay.Contains(lot.Position),
                    $"'{lot.Name}' at {lot.Position} stands on the quay deck");
                Assert.That(NineMileCreekMainland.DistanceToRoute(
                        NineMileCreekMainland.WharfRoad, lot.Position),
                    Is.GreaterThan(NineMileCreekMainland.RoadHalfWidth),
                    $"'{lot.Name}' at {lot.Position} stands in Wharf Road's cleared corridor");
            }
        }

        // =============================================================================================
        //  5. THE LAWS THE PASS INHERITED AND MAY NOT BREAK
        // =============================================================================================

        /// <summary>#451's guarantee, re-checked after the fittings table grew a gangway and a run of
        /// float cleats: the bollard you can see beside a berth is still there for every berth.</summary>
        [Test]
        public void EveryBerthStillHasSomethingToTieTo()
        {
            var fittings = NineMileCreekWharf.Fittings();
            float half = NineMileCreekMainland.BerthSpacingMetres * 0.5f;

            for (int i = 0; i < NineMileCreekWharf.BerthCount; i++)
            {
                Vector2 berth = NineMileCreekWharf.BerthPos(i);
                bool served = fittings.Any(f =>
                    HiddenHarbours.Art.Editor.WharfKitCatalog.IsMooringFitting(f.Name) &&
                    Mathf.Abs(f.Position.x - berth.x) <= half &&
                    Mathf.Abs(f.Position.y - NineMileCreekWharf.MooringEdgeY) <= 2f);

                Assert.IsTrue(served,
                    $"berth {i} at {berth} has no mooring fitting within half a berth of it — the " +
                    "photograph pass added fittings and must not have displaced the berths' own");
            }
        }

        [Test]
        public void TheFleetPlacerLeavesThePlayersBerthEmpty()
        {
            int player = NineMileCreekMooredFleet.PlayerBerthIndex();
            Vector2 berth = NineMileCreekWharf.BerthPos(player);

            Assert.That(Vector2.Distance(berth, new Vector2(NineMileCreekMainland.DockZonePos.x,
                                                            NineMileCreekMainland.DockZonePos.y)),
                Is.LessThan(NineMileCreekMainland.DockZoneRadius),
                "the berth the placer keeps clear must be the one the player actually docks in — it is " +
                "DERIVED from the dock zone, so this is the derivation being right rather than a restatement");
        }

        [Test]
        public void AMooredBoatLiesAlongTheWall_NotAcrossIt()
        {
            float heading = NineMileCreekMooredFleet.MooredHeadingDegrees();
            float face = NineMileCreekWharf.MooringFaceHeadingDegrees;

            float between = Mathf.Abs(Mathf.DeltaAngle(heading, face));
            Assert.That(between, Is.EqualTo(90f).Within(1e-2f),
                $"a boat moored alongside lies PARALLEL to the wall: her bow at {heading}° against a " +
                $"mooring face looking {face}° should be ninety degrees off it, not {between}°");
        }
    }
}
