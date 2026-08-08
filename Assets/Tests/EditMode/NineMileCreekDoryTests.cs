using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App;                 // CameraFollow — the framing these tests are measured against
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;                // SortingBands — the one place the sorting axis is partitioned
using HiddenHarbours.Economy;             // MarketId — the creek's buyer must say which market it is

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// §7.2's exit condition, as arithmetic: <i>"you arrive off the sandbar, sell, SEE THE DORY, and
    /// understand she is the next rung."</i>
    ///
    /// <para><b>"Visible from arrival" is a claim about a camera, so it is checked against one.</b> The
    /// on-foot framing is 9 m of world height at the project's design aspect — sixteen metres across —
    /// and the player is handed that frame centred on the disembark point the moment
    /// <c>RegionTravelCoordinator</c> puts them there. A dory outside it is a dory the player does not
    /// see, whatever a comment says about her being "by the wharf". These tests read the rectangle off
    /// <see cref="CameraFollow"/> rather than restating it, so a framing change re-checks her.</para>
    ///
    /// <para>The rest is the difference between dressing that works and dressing that breaks the level:
    /// she must not hang over the water, and she must not park across the only route off the quay.</para>
    /// </summary>
    public class NineMileCreekDoryTests
    {
        // ---- 1. she is on the working ground you cross, and nothing hides her ----------------------
        //
        // ⚠ "SHE IS IN THE ARRIVAL FRAME" IS RETIRED FOR THIS REGION — flagged for the owner, not quietly
        // dropped. §7.2's exit condition was written for a 120 × 120 m island where the wharf was the only
        // dry ground in shot, so "arrive, sell, SEE THE DORY" happened inside one 16 × 9 m frame because
        // nearly the whole region fitted in one. The mainland cannot do that: you come ashore from the bar
        // 260 m south of the wharf, and the derelict lies on the hard 41 m from where a boat lands. No
        // 9 m-tall frame holds both, and contorting the wharf or the hard to keep the sentence literally
        // true would be bending the place to fit a test.
        //
        // The three claims below are what the mainland CAN honestly promise, and each is derived.

        [Test]
        public void SheLiesOnTheMadeSpit_TheWorkingGroundYouWalkThrough()
        {
            Rect spit = NineMileCreekWharf.FootprintOf(NineMileCreekMainland.SpitFill);
            Rect hull = NineMileCreekDory.HullBounds();

            Assert.IsTrue(spit.Contains(NineMileCreekDory.HaulOutPos),
                $"the derelict at {NineMileCreekDory.HaulOutPos} must lie on the made spit {spit} — hauled " +
                "out among the trap stacks is where a wreck sits on a working wharf");

            foreach (var corner in new[]
                     {
                         new Vector2(hull.xMin, hull.yMin), new Vector2(hull.xMax, hull.yMin),
                         new Vector2(hull.xMin, hull.yMax), new Vector2(hull.xMax, hull.yMax),
                     })
                Assert.IsTrue(spit.Contains(corner),
                    $"her hull reaches {corner}, off the made ground — a boat HAULED OUT is entirely out " +
                    "of the water, or she is a boat adrift");
        }

        [Test]
        public void SheIsHighAndDry_AtEveryTide()
        {
            // Hauled out means hauled out. Really a guard on the spit's fill still reaching her: if the
            // made ground were ever trimmed, the derelict would be afloat and nothing else would say so.
            var go = new GameObject("TidalTerrain");
            try
            {
                var terrain = go.AddComponent<HiddenHarbours.World.MainlandTidalTerrain>();
                NineMileCreekBuilder.ConfigureNineMileCreekTerrain(terrain);

                float ground = terrain.ElevationAt(NineMileCreekDory.HaulOutPos);
                Assert.Greater(ground, NineMileCreekMainland.SpringHighWater,
                    $"she lies on {ground:0.00} m against a spring high of " +
                    $"{NineMileCreekMainland.SpringHighWater:0.00} m — the derelict is floating");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SheIsNearerTheYardThatSellsHerThanAnythingElseOnTheCreek()
        {
            // "The boat you are SHOWN is the boat you are SOLD." On the island those were the same three
            // metres of quay and the claim was free; on the mainland it has to be made, and it is what
            // stops the derelict becoming scenery at one end of the spit while her price tag sits at the
            // other.
            float toYard = Vector2.Distance(NineMileCreekDory.HaulOutPos, NineMileCreekBuilder.DoryYardPos);

            foreach (var site in NineMileCreekBuilder.CreeksideBuildingSites)
            {
                if (Vector2.Distance(site, NineMileCreekBuilder.DoryYardPos) < 1e-3f) continue;   // the yard itself
                Assert.Less(toYard, Vector2.Distance(NineMileCreekDory.HaulOutPos, site),
                    $"the derelict is closer to the site at {site} than to the yard that sells her — the " +
                    "hull and her price tag have come apart");
            }

            Assert.Greater(toYard, NineMileCreekBuilder.CreeksideBuildingRadius,
                "…but the yard must not be built ON her");
        }

        [Test]
        public void TheArrivalFrameIsTheRealOnFootFraming_NotAGenerousOne()
        {
            Assert.AreEqual(CameraFollow.OnFootWorldHeightMeters,
                            NineMileCreekDory.ArrivalViewHeightMetres, 1e-4f,
                "the sightline must be measured against the camera the player actually gets");

            float aspect = CameraFollow.ReferenceWidthPx / (float)CameraFollow.ReferenceHeightPx;
            Assert.AreEqual(NineMileCreekDory.ArrivalViewHeightMetres * aspect,
                            NineMileCreekDory.ArrivalViewWidthMetres, 1e-4f);

            Assert.Less(NineMileCreekDory.ViewSafeFraction, 1f,
                "the safe box must be strictly INSIDE the frame — the camera eases toward its target and " +
                "the pixel-perfect crop can shave the edges, so a hull on the boundary is not a promise");
            Assert.Greater(NineMileCreekDory.ViewSafeFraction, 0.5f,
                "…but not so small that it stops meaning 'on screen'");

            // Belt and braces: the safe box is inside the full one.
            Rect full = NineMileCreekDory.ArrivalView(), safe = NineMileCreekDory.ArrivalSafeView();
            Assert.GreaterOrEqual(safe.xMin, full.xMin);
            Assert.LessOrEqual(safe.xMax, full.xMax);
            Assert.GreaterOrEqual(safe.yMin, full.yMin);
            Assert.LessOrEqual(safe.yMax, full.yMax);
        }

        [Test]
        public void NothingBuiltOnTheCreekStandsInTheWayOfHer()
        {
            foreach (var site in NineMileCreekBuilder.CreeksideBuildingSites)
                Assert.IsTrue(
                    NineMileCreekDory.SightlineIsClear(NineMileCreekDory.HaulOutPos, site,
                                                       NineMileCreekBuilder.CreeksideBuildingRadius),
                    $"the building at {site} sits across the line from the arrival point to the dory. " +
                    "Being in frame is not the same as being visible — move the building, or move her");
        }

        [Test]
        public void TheSegmentDistanceIsASegment_NotAnInfiniteLine()
        {
            // The guard that makes the occlusion test mean anything: something BEHIND the player must not
            // count as an occluder just because it is on the same line.
            Vector2 a = new Vector2(0f, 0f), b = new Vector2(10f, 0f);
            Assert.AreEqual(5f, NineMileCreekDory.DistancePointToSegment(new Vector2(-5f, 0f), a, b), 1e-4f,
                "a point beyond the near end is measured to that end, not to the line");
            Assert.AreEqual(3f, NineMileCreekDory.DistancePointToSegment(new Vector2(5f, 3f), a, b), 1e-4f,
                "a point beside the middle is measured perpendicular");
            Assert.AreEqual(0f, NineMileCreekDory.DistancePointToSegment(a, a, a), 1e-4f,
                "a degenerate segment does not divide by zero");
        }

        // ---- 2. she is solid, and she does not block the yard ---------------------------------------

        [Test]
        public void SheIsSolid_ButSheDoesNotCloseTheWayPast()
        {
            // She is off the quay now, so the lane that matters is the one along the spit rather than the
            // one between her and the mooring edge. The claim is the same claim: a hull parked across the
            // only route is a wall with a boat painted on it.
            Rect box = NineMileCreekDory.ColliderBounds();
            Rect spit = NineMileCreekWharf.FootprintOf(NineMileCreekMainland.SpitFill);

            Assert.GreaterOrEqual(box.yMin - spit.yMin, NineMileCreekDory.WalkingLaneMetres,
                $"only {box.yMin - spit.yMin:0.00} m between her and the spit's seaward edge");
            Assert.GreaterOrEqual(spit.yMax - box.yMax, NineMileCreekDory.WalkingLaneMetres,
                $"only {spit.yMax - box.yMax:0.00} m between her and the shanty row behind her");

            Assert.Less(box.width, NineMileCreekDory.HullBounds().width,
                "her collision is her planking, not her flare");
        }

        [Test]
        public void SheKeepsOffTheRoad()
        {
            // Wharf Road runs the length of the spit to the wharf front; a derelict parked in the
            // carriageway is a derelict the player walks through.
            float toRoad = NineMileCreekMainland.DistanceToRoute(NineMileCreekMainland.WharfRoad,
                                                                 NineMileCreekDory.HaulOutPos);
            Assert.Greater(toRoad, NineMileCreekMainland.RoadHalfWidth,
                $"she lies {toRoad:0.0} m from Wharf Road's centre-line, inside its " +
                $"{NineMileCreekMainland.RoadHalfWidth} m cleared corridor");
        }

        [Test]
        public void SheLayersWithTheWorld_RatherThanSittingOnADeckThatIsNoLongerDrawn()
        {
            // Her ancestor took a fixed order one above the wharf-kit deck band, because she was parked on
            // 48 sprites and had to out-draw the row under her. The quay is GROUND now and draws nothing,
            // so a fixed order would freeze her in the stack — she Y-sorts like every other object on the
            // coast, and her serialized order is only the pre-Play default.
            Assert.GreaterOrEqual(NineMileCreekDory.SortingOrder, SortingBands.DecorFloor,
                "she must sit in the decor band, not under it with the sea and the seabed");
            Assert.Greater(NineMileCreekDory.SortingOrder, NineMileCreekWharf.SortingOrderMax,
                "…and above the wharf's own structure band, which is where Phase B's ISO quay will draw");
        }

        // ---- 3. the facing is derived, and the sheet backs it up ----------------------------------

        [Test]
        public void HerFacingIsOneTheSheetActuallyHas()
        {
            Assert.GreaterOrEqual(NineMileCreekDory.HullFacingIndex, 0);
            Assert.Less(NineMileCreekDory.HullFacingIndex, NineMileCreekDory.HullFacingCount);

            int imported = NineMileCreekDory.ImportedFacingCount();
            if (imported == 0)
                Assert.Ignore("DoryIso.png has not imported in this environment — the facing count " +
                              "cannot be checked against pixels here. CI checks it out of LFS.");

            Assert.AreEqual(NineMileCreekDory.HullFacingCount, imported,
                "the sheet was re-baked at a different facing count, so the derived index no longer means " +
                "what it was derived to mean. Re-derive it; do not nudge it until she looks right");
        }

        // ---- 4. the creek's buyer says which market it is ------------------------------------------

        [Test]
        public void TheCreeksBuyerIsNotLeftOnTheDefaultMarket()
        {
            Assert.AreEqual(MarketId.NineMileCreek, NineMileCreekBuilder.CreekMarket);
            Assert.AreNotEqual(default(MarketId), NineMileCreekBuilder.CreekMarket,
                "Market defaults to MarketId.Cove, and a creek buyer left on it quietly quotes the HOME " +
                "COVE's demand and price level — the sale still pays out, the glut still bites, and the " +
                "one thing this outlet exists to be is silently untrue");
        }

        [Test]
        public void SellingHappensOnTheWorkingSpit_NotUpTheHill()
        {
            // ⚠ RE-DERIVED. The old claim was "the till is within a stall's reach (4 m) of the quay",
            // which on a 120 m island was the same sentence as "you arrive and you SELL" — the whole
            // region fitted in one camera frame. It cannot be that on an 84 m wharf: a fish buyer stands
            // on the apron among the parked trucks, which is 13 m back from the mooring edge, and pulling
            // him onto the quay's face to satisfy a number would put a till where the fleet lands fish.
            //
            // What must still be TRUE is the beat: selling is part of arriving, not an errand. So it is
            // measured against the thing that would break it — the walk to TOWN, which is the trip the
            // region deliberately makes you take for a licence and a rod.
            Rect spit = NineMileCreekWharf.FootprintOf(NineMileCreekMainland.SpitFill);
            Vector2 truck = NineMileCreekBuilder.FishBuyerPos;
            Vector2 landed = NineMileCreekBuilder.DisembarkPos;

            Assert.IsTrue(spit.Contains(truck),
                $"the buyer at {truck} must stand on the working spit {spit} — a till anywhere else is an " +
                "errand, whatever the distance says");

            float toTill = Vector2.Distance(landed, truck);
            float toTown = Vector2.Distance(landed, NineMileCreekBuilder.HarbourmasterPos);
            Assert.Less(toTill, toTown * 0.25f,
                $"stepping off the boat, the till is {toTill:0} m away and the town is {toTown:0} m. " +
                "Selling has to be a fraction of the walk the region charges you for a licence, or the " +
                "wharf has stopped being where the money is");
        }

        [Test]
        public void TheBuyerStandsAtHisOwnTruck()
        {
            var wendell = NineMileCreekPeople.People.First(p => p.AssetName == "WendellArsenault");
            float toTruck = Vector2.Distance(wendell.Position, NineMileCreekBuilder.FishBuyerPos);

            Assert.LessOrEqual(toTruck, StallGate.DefaultRange,
                "the man and the till have come apart — a player who can talk to him but not sell to him " +
                "has met a decoration");
            Assert.Greater(toTruck, 0.5f, "…and he is standing in front of it, not inside it");
        }

        [Test]
        public void NeitherPersonIsStandingInsideTheDory()
        {
            Rect box = NineMileCreekDory.ColliderBounds();
            foreach (var person in NineMileCreekPeople.People)
                Assert.IsFalse(box.Contains(person.Position),
                    $"{person.AssetName} is standing inside the derelict's hull");
        }
    }
}
