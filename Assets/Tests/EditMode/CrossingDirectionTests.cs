using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Pins the Cove↔Nine Mile Creek crossing so it READS TRUE on the canon map — Nine Mile Creek lies WEST of
    /// Coddle Cove ("NINE MILE CREEK ——+—— CODDLE COVE"). The owner-playtest gap was "sail south but Nine Mile Creek
    /// is north." These guard the directional intent at the builders' shared constants (no scene needed):
    /// you SAIL WEST to cross, ARRIVE FROM THE EAST in Nine Mile Creek (and continue west to the wharf), and the
    /// return trip leaves EAST so you come home to the cove dock FROM THE WEST. The boat's heading is
    /// preserved across the hop (RegionTravelCoordinator repositions only), so a west crossing → arriving
    /// heading west falls straight out of these passage placements. (Since NMC H2 Nine Mile Creek's own arrival
    /// is an alongside berth, which lays her on the berth's heading instead: see its case below.)
    ///
    /// Scene geometry (boat not sailing through the wharf, the actual sprites/headings) is play-mode/manual:
    /// the owner must re-run BOTH "Build Greybox Scene" and "Build Nine Mile Creek Scene" and re-test.
    /// </summary>
    public class CrossingDirectionTests
    {
        [Test]
        public void CoveToNineMileCreek_Passage_IsOnTheWestEdge()
        {
            // Nine Mile Creek is west → the cove's crossing passage sits on the WEST edge of the open water.
            var p = GreyboxBuilder.ToNineMileCreekPassagePos;
            Assert.Less(p.x, -10f, "the Cove→Nine Mile Creek passage must be well to the WEST (you sail west to cross)");
            Assert.Greater(Mathf.Abs(p.x), Mathf.Abs(p.y),
                "and west must be the dominant axis — a WEST-edge band, not the old south one");
        }

        [Test]
        public void NineMileCreekToCove_ReturnPassage_IsOnTheEastEdge()
        {
            // The cove lies east of Nine Mile Creek → the return passage sits on the EAST edge: sail east to go home.
            var p = NineMileCreekBuilder.ToCovePassagePos;
            Assert.Greater(p.x, 8f, "the Nine Mile Creek→Cove return passage must be to the EAST (sail east back to the cove)");
            Assert.Greater(Mathf.Abs(p.x), Mathf.Abs(p.y), "east must be the dominant axis");
        }

        /// <summary>Replaces <c>NineMileCreekArrival_IsEastOfItsDock_EnterFromTheEast</c> (NMC H2). The arrival is now
        /// an alongside berth on G3's north face (<see cref="NineMileCreekGroundPlan"/>): she is laid abreast of the
        /// dock zone on the berth's own heading, not parked east of it on the heading she sailed in on, so "east of
        /// the dock" no longer holds. What still does is that the way in is from the east and she lies ALONG the
        /// face, not end-on to it.</summary>
        [Test]
        public void NineMileCreekArrival_LiesAlongsideItsDock_OffTheNorthFace()
        {
            Vector3 arrival = NineMileCreekBuilder.ArrivalPos, dock = NineMileCreekBuilder.DockZonePos;

            Assert.Greater(NineMileCreekBuilder.ToCovePassagePos.x, arrival.x,
                "the way in from the cove must be EAST of the arrival: you still enter from the east");
            Assert.Less(arrival.y, dock.y, "she lies SOUTH of the dock zone: off the north face, in the basin");
            Assert.AreEqual(dock.x, arrival.x, 1e-3f, "…and abreast of it: alongside, not end-on");
            float heading = NineMileCreekGroundPlan.BerthHeadingDegrees * Mathf.Deg2Rad;
            Assert.Greater(Mathf.Abs(Mathf.Sin(heading)), 0.99f, "her heading runs east-west, along the face");
        }

        // ---- ⭐ THE OTHER crossing: the tidal bar out to St Peters ---------------------------------

        [Test]
        public void TheBarToStPeters_LeavesTheMainlandShoreHeadingESE()
        {
            // The overhead is the layout truth: St Peters lies offshore to the SOUTH-EAST, and the bar
            // leaves the mainland shore heading east-south-east to reach it. Measured off the plan's own
            // bar rather than restated, so a re-aim re-checks this.
            Vector2 run = NineMileCreekMainland.BarTo - NineMileCreekMainland.BarFrom;

            Assert.Greater(run.x, 0f, "the bar runs OUT TO SEA, and the sea is east");
            Assert.Less(run.y, 0f, "…and southward, because the island lies off the SOUTH-east");
            Assert.Greater(Mathf.Abs(run.x), Mathf.Abs(run.y),
                "east must be the dominant axis: ESE, not SSE. A bar running mostly south would leave the " +
                "region through its own coast instead of crossing the bay");
        }

        [Test]
        public void TheSeamBand_SitsBeyondTheBarsTip_SoYouCrossItLEAVING()
        {
            Vector2 tip = NineMileCreekMainland.BarTo;
            Vector2 band = NineMileCreekMainland.ToStPetersPassagePos;
            Vector2 axis = (NineMileCreekMainland.BarTo - NineMileCreekMainland.BarFrom).normalized;

            Assert.Greater(Vector2.Dot(band - tip, axis), 0f,
                "the passage band must lie PAST the tip along the bar's own axis — a band short of it " +
                "fires while you are still walking your own region's half of the crossing");
        }

        [Test]
        public void TheWalkInArrival_LandsYouAtTheSeam_NotBackAshore()
        {
            // The mainland owns 305 m of the 610 m crossing. You are handed over mid-flats and walk the
            // rest; an arrival back at the landing would skip the half this region exists to carry.
            Vector2 landing = NineMileCreekMainland.CoastPoints[2];   // ⭐ THE BAR LANDING
            Vector2 arrival = NineMileCreekMainland.WalkArrivalPos;

            Assert.Less(Vector2.Distance(arrival, NineMileCreekMainland.BarTo), 20f,
                $"the walk-in arrival {arrival} must land you at the SEAM ({NineMileCreekMainland.BarTo}), " +
                "which is where the other region handed you over");
            Assert.Greater(Vector2.Distance(arrival, landing), 100f,
                "…and emphatically not ashore: the walk in IS this region's half of the crossing");
        }

        [Test]
        public void CoveReturnArrival_IsWestOfTheCoveDock_ArriveFromTheWest()
        {
            // Coming home from Nine Mile Creek (which is west), you approach the cove dock FROM THE WEST.
            Assert.Less(GreyboxBuilder.CoveArrivalPos.x, GreyboxBuilder.CoveDockZonePos.x,
                "the cove return arrival must be WEST of the cove dock (you come home from the west)");
        }

        [Test]
        public void CoveReturnArrival_IsWithinDockRadius_SoEDisembarks()
        {
            // The same disembark invariant the Nine Mile Creek side guards (#52): the return arrival must park the
            // boat within the cove dock radius, or E can't disembark when you get home.
            float d = Vector2.Distance(GreyboxBuilder.CoveArrivalPos, GreyboxBuilder.CoveDockZonePos);
            Assert.LessOrEqual(d, GreyboxBuilder.CoveDockZoneRadius,
                $"cove return arrival ({GreyboxBuilder.CoveArrivalPos}) is {d:0.00} m from the dock zone " +
                $"({GreyboxBuilder.CoveDockZonePos}); must be within {GreyboxBuilder.CoveDockZoneRadius} m.");
        }
    }
}
