using NUnit.Framework;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The S4.5 HUD-yields-the-helm mapping (<see cref="HudHelmSuppressionRule"/>) — owner ask 1.
    /// The rule is DATA-DRIVEN off the resolved <see cref="HelmFit"/>, never a hull name, and the
    /// negative control is real on the shipped fleet: the two skiff consoles author a dome compass,
    /// the two pilothouse consoles author NONE — so "hide the HUD heading trio" must track the
    /// mounted compass, or a Novi/Cape helm (and every tiller boat) loses its only heading read.
    /// </summary>
    public class HudHelmSuppressionTests
    {
        private static HudHelmSuppression Derive(bool helmCard, bool dash, CompassMount compass,
                                                 bool focused, bool expanded)
            => HudHelmSuppressionRule.Derive(helmCard, dash, compass, focused, expanded);

        [Test]
        public void OnFoot_TheHudIsUntouched()
        {
            HudHelmSuppression s = Derive(false, false, CompassMount.None, false, false);
            Assert.That(s.MoveNavCluster, Is.False);
            Assert.That(s.HideCompassCluster, Is.False);
            Assert.That(s.HideForBigPanel, Is.False);
        }

        [Test]
        public void AnyHelmCard_MovesTheNavCluster_OffTheCardsBottomCentreAnchor()
        {
            // Every helm card (tiller, lever, dash) anchors bottom-centre — where the VS-19 cluster
            // lived. The overlap rule moves the cluster whenever any of them is up.
            Assert.That(Derive(true, false, CompassMount.None, false, false).MoveNavCluster, Is.True,
                        "a tiller card is a helm card too");
            Assert.That(Derive(true, true, CompassMount.None, false, false).MoveNavCluster, Is.True);
            Assert.That(Derive(false, false, CompassMount.None, false, false).MoveNavCluster, Is.False,
                        "…and ashore nothing moves");
        }

        [Test]
        public void ADashWithACompass_HidesTheHudHeadingTrio_TheDuplicationRule()
        {
            // The skiffs' shipped fit: dome compass mounted → the HUD compass/ribbon/needle would be
            // a duplicate read over the dash. It hides in BOTH dash scales (small and focused).
            Assert.That(Derive(true, true, CompassMount.Dome, false, false).HideCompassCluster, Is.True);
            Assert.That(Derive(true, true, CompassMount.Dome, true, false).HideCompassCluster, Is.True);
            // The flush Ritchie (a pilothouse upgrade) is a mounted compass too.
            Assert.That(Derive(true, true, CompassMount.Flush, false, false).HideCompassCluster, Is.True);
        }

        [Test]
        public void NegativeControl_ADashWithoutACompass_KeepsTheHudHeadingTrio()
        {
            // The wheelhouses' SHIPPED fit (NoviHelm/CapeIslanderHelm author DefaultCompass: None):
            // no dash compass → the HUD cluster is the only heading read and MUST survive.
            Assert.That(Derive(true, true, CompassMount.None, false, false).HideCompassCluster, Is.False,
                        "a compass-less dash keeps the HUD heading trio");
        }

        [Test]
        public void NegativeControl_ATillerBoat_KeepsTheWholeCluster()
        {
            // A tiller hull has NO dash at all — nothing it could duplicate. The cluster moves
            // (overlap rule) but never duplication-hides.
            HudHelmSuppression s = Derive(true, false, CompassMount.None, false, false);
            Assert.That(s.HideCompassCluster, Is.False, "no dash → no duplication → the trio stays");
            Assert.That(s.MoveNavCluster, Is.True);
        }

        [Test]
        public void ABigPanel_HidesTheClusterAndTheCatchCard()
        {
            Assert.That(Derive(true, true, CompassMount.None, true, false).HideForBigPanel, Is.True,
                        "a FOCUSED helm card spans most of the screen");
            Assert.That(Derive(true, true, CompassMount.None, false, true).HideForBigPanel, Is.True,
                        "an EXPANDED instrument is centre-screen");
            Assert.That(Derive(true, true, CompassMount.None, false, false).HideForBigPanel, Is.False,
                        "the small card costs only the move, never the reads");
        }

        [Test]
        public void Sabotage_ACompassBlindMapping_IsDetected()
        {
            // The permanent arm (the #406 style): the compass-hide decision must DEPEND on the
            // mounted compass. If someone re-keys it on hull name or hardcodes it, the two derives
            // below stop differing — and either every wheelhouse/tiller boat loses its only heading
            // read, or the duplicate cluster comes back over the skiffs' dash.
            bool withCompass = Derive(true, true, CompassMount.Dome, false, false).HideCompassCluster;
            bool without = Derive(true, true, CompassMount.None, false, false).HideCompassCluster;
            if (withCompass == without)
                Assert.Fail("SABOTAGE NOT DETECTED: the HUD suppression mapping no longer reads the " +
                            "mounted compass — it would either strip a compass-less helm of its only " +
                            "heading read or park a duplicate cluster over the dash.");
        }
    }
}
