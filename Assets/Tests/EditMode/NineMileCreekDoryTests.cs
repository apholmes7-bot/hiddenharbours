using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App;                 // CameraFollow — the framing these tests are measured against
using HiddenHarbours.App.Editor;
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
        // ---- 1. you cannot arrive here and not see her --------------------------------------------

        [Test]
        public void SheIsInsideTheFrameThePlayerIsHandedOnArrival()
        {
            Rect safe = NineMileCreekDory.ArrivalSafeView();

            Assert.IsTrue(NineMileCreekDory.IsVisibleFromArrival(NineMileCreekDory.HaulOutPos),
                $"the dory at {NineMileCreekDory.HaulOutPos} is outside the frame the player gets when " +
                $"they land at {NineMileCreekDory.ArrivalStandingPos} ({safe}). §7.2's exit condition is " +
                "that they SEE her — if she needs to be walked to before she is on screen, she is not the " +
                "rung, she is a discovery");
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

        // ---- 2. she is on the quay, not in the water, and not across it ----------------------------

        [Test]
        public void SheLiesOnTheQuay_WithNothingHangingOverTheWater()
        {
            Rect deck = NineMileCreekWharf.DeckFootprint();
            Rect hull = NineMileCreekDory.HullBounds();

            Assert.IsTrue(deck.Contains(NineMileCreekDory.HaulOutPos),
                "canon puts her lying AT THE WHARF, and the wharf is the only ground inside the arrival " +
                "frame — so her keel has to be on the deck");

            Assert.GreaterOrEqual(hull.yMin, deck.yMin,
                "her stern must not hang over the mooring edge — that is a boat in the water, not a boat " +
                "hauled out");
            Assert.LessOrEqual(hull.yMax, deck.yMax, "…nor over the north curb");

            // The one overhang that is RIGHT: a boat is dragged out bow-first, so her bow runs past the
            // quay root onto the beach. West only.
            Assert.Less(hull.xMin, deck.xMin,
                "her bow should run up past the root onto the sand — that is what hauled out looks like");
            Assert.LessOrEqual(hull.xMax, deck.xMax,
                "but her stern must stay on the concrete, not out over the harbour");
        }

        [Test]
        public void SheIsSolid_ButSheDoesNotCloseTheQuay()
        {
            Rect deck = NineMileCreekWharf.DeckFootprint();
            Rect box = NineMileCreekDory.ColliderBounds();

            float southLane = box.yMin - deck.yMin;
            float northLane = deck.yMax - box.yMax;

            Assert.GreaterOrEqual(southLane, NineMileCreekDory.WalkingLaneMetres,
                $"only {southLane:0.00} m between her and the mooring edge — she has parked across the " +
                "wharf and the player cannot get past her to the buyer at its root");
            Assert.GreaterOrEqual(northLane, NineMileCreekDory.WalkingLaneMetres,
                $"only {northLane:0.00} m between her and the north curb");

            Assert.Less(box.width, NineMileCreekDory.HullBounds().width,
                "her collision is her planking, not her flare");
        }

        [Test]
        public void SheDrawsOnTopOfTheConcreteSheIsParkedOn()
        {
            Assert.Greater(NineMileCreekDory.SortingOrder, NineMileCreekWharf.SortingOrderMax,
                "a hull cut in half by a row of the deck it is standing on is the failure this guards");
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
        public void TheBuyersTruckIsWithinAStallsReachOfTheQuay()
        {
            Rect deck = NineMileCreekWharf.DeckFootprint();
            Vector2 truck = NineMileCreekBuilder.FishBuyerPos;

            // Nearest point of the deck to the truck — where a player standing on the planks would be.
            var onDeck = new Vector2(Mathf.Clamp(truck.x, deck.xMin, deck.xMax),
                                     Mathf.Clamp(truck.y, deck.yMin, deck.yMax));

            Assert.LessOrEqual(Vector2.Distance(truck, onDeck), StallGate.DefaultRange,
                "§7.2's exit is 'you arrive off the sandbar, SELL'. If the till is further from the quay " +
                "than a stall's reach, selling is a walk inland before it is a beat");
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
