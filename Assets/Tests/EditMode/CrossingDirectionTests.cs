using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Pins the Cove↔Greywick crossing so it READS TRUE on the canon map — Port Greywick lies WEST of
    /// Coddle Cove ("PORT GREYWICK ——+—— CODDLE COVE"). The owner-playtest gap was "sail south but Greywick
    /// is north." These guard the directional intent at the builders' shared constants (no scene needed):
    /// you SAIL WEST to cross, ARRIVE FROM THE EAST in Greywick (and continue west to the wharf), and the
    /// return trip leaves EAST so you come home to the cove dock FROM THE WEST. The boat's heading is
    /// preserved across the hop (RegionTravelCoordinator repositions only), so a west crossing → arriving
    /// heading west falls straight out of these passage placements.
    ///
    /// Scene geometry (boat not sailing through the wharf, the actual sprites/headings) is play-mode/manual:
    /// the owner must re-run BOTH "Build Greybox Scene" and "Build Greywick Scene" and re-test.
    /// </summary>
    public class CrossingDirectionTests
    {
        [Test]
        public void CoveToGreywick_Passage_IsOnTheWestEdge()
        {
            // Greywick is west → the cove's crossing passage sits on the WEST edge of the open water.
            var p = GreyboxBuilder.ToGreywickPassagePos;
            Assert.Less(p.x, -10f, "the Cove→Greywick passage must be well to the WEST (you sail west to cross)");
            Assert.Greater(Mathf.Abs(p.x), Mathf.Abs(p.y),
                "and west must be the dominant axis — a WEST-edge band, not the old south one");
        }

        [Test]
        public void GreywickToCove_ReturnPassage_IsOnTheEastEdge()
        {
            // The cove lies east of Greywick → the return passage sits on the EAST edge: sail east to go home.
            var p = GreywickBuilder.ToCovePassagePos;
            Assert.Greater(p.x, 8f, "the Greywick→Cove return passage must be to the EAST (sail east back to the cove)");
            Assert.Greater(Mathf.Abs(p.x), Mathf.Abs(p.y), "east must be the dominant axis");
        }

        [Test]
        public void GreywickArrival_IsEastOfItsDock_EnterFromTheEast()
        {
            // You cross heading west and enter Greywick from the EAST, so the arrival sits east of the wharf
            // head and you continue WEST onto the deck.
            Assert.Greater(GreywickBuilder.ArrivalPos.x, GreywickBuilder.DockZonePos.x,
                "the Greywick arrival must be EAST of the dock zone (enter from the east, continue west to the wharf)");
        }

        [Test]
        public void CoveReturnArrival_IsWestOfTheCoveDock_ArriveFromTheWest()
        {
            // Coming home from Greywick (which is west), you approach the cove dock FROM THE WEST.
            Assert.Less(GreyboxBuilder.CoveArrivalPos.x, GreyboxBuilder.CoveDockZonePos.x,
                "the cove return arrival must be WEST of the cove dock (you come home from the west)");
        }

        [Test]
        public void CoveReturnArrival_IsWithinDockRadius_SoEDisembarks()
        {
            // The same disembark invariant the Greywick side guards (#52): the return arrival must park the
            // boat within the cove dock radius, or E can't disembark when you get home.
            float d = Vector2.Distance(GreyboxBuilder.CoveArrivalPos, GreyboxBuilder.CoveDockZonePos);
            Assert.LessOrEqual(d, GreyboxBuilder.CoveDockZoneRadius,
                $"cove return arrival ({GreyboxBuilder.CoveArrivalPos}) is {d:0.00} m from the dock zone " +
                $"({GreyboxBuilder.CoveDockZonePos}); must be within {GreyboxBuilder.CoveDockZoneRadius} m.");
        }
    }
}
