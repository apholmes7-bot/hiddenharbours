using NUnit.Framework;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>Which lamps a hull is allowed to burn, given how she is lying</b> — the rule of the road, as
    /// a pure function (<see cref="BoatLamps.ShowsWhen"/>), pinned with no scene and no GPU.
    ///
    /// <para><b>Why this is worth its own file.</b> Before PR 2 there was no regime at all: the one
    /// lamp-bearing hull in the game was always under way, so "show everything" was accidentally
    /// correct. The moment the fleet's lamp tables landed, that same code would have put sidelights,
    /// mastheads and burning searchlights on the seven boats made fast to the Nine Mile Creek wharf
    /// and on every hull in the review anchorage, all night — a wharf full of boats each claiming to
    /// be under way. This is the rule that stops it, and it is small enough to be read.</para>
    /// </summary>
    public class BoatLampRegimeTests
    {
        // Occupancy governs only the CABIN GLOW; the navigation-light rows pass the same value
        // throughout so that the one lamp it actually decides stays visible rather than accidental.
        const bool Aboard = true;
        const bool NobodyAboard = false;

        [Test]
        public void UnderWayShowsTheLightsThatSayUnderWay()
        {
            Assert.IsTrue(BoatLamps.ShowsWhen(HullLampKind.PortSidelight, VesselWay.UnderWay, Aboard));
            Assert.IsTrue(BoatLamps.ShowsWhen(HullLampKind.StarboardSidelight, VesselWay.UnderWay, Aboard));
            Assert.IsTrue(BoatLamps.ShowsWhen(HullLampKind.SternLight, VesselWay.UnderWay, Aboard));
            Assert.IsTrue(BoatLamps.ShowsWhen(HullLampKind.Masthead, VesselWay.UnderWay, Aboard));
            Assert.IsTrue(BoatLamps.ShowsWhen(HullLampKind.RangeLight, VesselWay.UnderWay, Aboard),
                          "a range light is a second masthead: it says under way with the first");
        }

        [Test]
        public void AndNotTheAnchorLightThatContradictsThem()
        {
            Assert.IsFalse(BoatLamps.ShowsWhen(HullLampKind.AnchorLight, VesselWay.UnderWay, Aboard),
                           "a boat making way is not at anchor, and showing both says both");
        }

        [Test]
        public void MooredShowsTheAnchorLightAndNothingElseThatNavigates()
        {
            Assert.IsTrue(BoatLamps.ShowsWhen(HullLampKind.AnchorLight, VesselWay.Moored, Aboard));

            Assert.IsFalse(BoatLamps.ShowsWhen(HullLampKind.PortSidelight, VesselWay.Moored, Aboard),
                           "a boat lying still showing sidelights is claiming to be moving — the one " +
                           "lie in this feature that could actually mislead somebody");
            Assert.IsFalse(BoatLamps.ShowsWhen(HullLampKind.StarboardSidelight, VesselWay.Moored, Aboard));
            Assert.IsFalse(BoatLamps.ShowsWhen(HullLampKind.SternLight, VesselWay.Moored, Aboard));
            Assert.IsFalse(BoatLamps.ShowsWhen(HullLampKind.Masthead, VesselWay.Moored, Aboard));
            Assert.IsFalse(BoatLamps.ShowsWhen(HullLampKind.RangeLight, VesselWay.Moored, Aboard));
        }

        [Test]
        public void TheCabinGlowIsNotANavigationLight_AndOccupancyGovernsIt_NotTheRegime()
        {
            // Nobody takes a bearing off a lit window, so the rule of the road has nothing to say about
            // it. What decides it is whether anyone is actually there.
            Assert.IsTrue(BoatLamps.ShowsWhen(HullLampKind.CabinGlow, VesselWay.UnderWay, NobodyAboard),
                          "a boat under way has somebody at her wheel by definition — her wheelhouse is " +
                          "lit whether or not anyone has gone below");
            Assert.IsTrue(BoatLamps.ShowsWhen(HullLampKind.CabinGlow, VesselWay.Moored, Aboard),
                          "and a boat at her berth with somebody aboard is lit");

            Assert.IsFalse(BoatLamps.ShowsWhen(HullLampKind.CabinGlow, VesselWay.Moored, NobodyAboard),
                           "but an EMPTY boat at her berth is dark inside. Seven identical lit " +
                           "wheelhouses along a wharf at two in the morning is a row of lanterns, not a " +
                           "harbour; the skipper standing on her deck has not gone below.");
        }

        [Test]
        public void UnderWayIsTheDefaultAndThatIsLoadBearing()
        {
            // A hull whose root answers nothing gets default(VesselWay), and it has to be UnderWay:
            // that is exactly what every lamp-bearing hull in the game did before the regime existed,
            // the arrival's Cape Islander among them, and she is the shipped control. If this ever
            // becomes Moored, every unlabelled boat in the game goes dark at once.
            Assert.AreEqual(VesselWay.UnderWay, default(VesselWay));
            Assert.AreEqual(0, (int)VesselWay.UnderWay);
        }

        [Test]
        public void EveryLampKindHasAnAnswerInBothRegimes()
        {
            // A new kind added to the enum without a thought here would fall into the "not the anchor
            // light" branch and burn at a berth. Sweeping the enum makes that a decision rather than a
            // default: if this ever fails to compile or a new kind reads wrongly, the rule needs a row.
            foreach (HullLampKind kind in System.Enum.GetValues(typeof(HullLampKind)))
            {
                bool ever = BoatLamps.ShowsWhen(kind, VesselWay.UnderWay, Aboard)
                         || BoatLamps.ShowsWhen(kind, VesselWay.UnderWay, NobodyAboard)
                         || BoatLamps.ShowsWhen(kind, VesselWay.Moored, Aboard)
                         || BoatLamps.ShowsWhen(kind, VesselWay.Moored, NobodyAboard);
                Assert.IsTrue(ever, $"{kind} is shown in no state at all, so it can never light");
            }
        }
    }
}
