using NUnit.Framework;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The region-transition fade's pure logic (the cover that turns the VS-22 scene-cut into a voyage):
    /// the fade-in curve, the "is this a real arrival" decision, and the arrival-card title. The
    /// MonoBehaviour glue (self-install, the activeSceneChanged hook, the Canvas) is play-mode only and
    /// the owner re-verifies it in Unity; these pin the maths that guarantee no stuck-black.
    /// </summary>
    public class RegionFadeTests
    {
        // ---- AlphaAfter: flashes black (1), clears to transparent (0) ------------------------

        [Test]
        public void AlphaAfter_FadesFromBlackToClear()
        {
            Assert.AreEqual(1f, RegionFade.AlphaAfter(0f, 0.6f), 1e-6f, "flashes fully black at the start");
            Assert.AreEqual(0.5f, RegionFade.AlphaAfter(0.3f, 0.6f), 1e-6f, "half-way through it is half-cleared");
            Assert.AreEqual(0f, RegionFade.AlphaAfter(0.6f, 0.6f), 1e-6f, "fully clear at the end");
        }

        [Test]
        public void AlphaAfter_ClampsAndNeverSticksBlack()
        {
            Assert.AreEqual(0f, RegionFade.AlphaAfter(10f, 0.6f), 1e-6f, "past the end stays clear (never stuck black)");
            Assert.AreEqual(1f, RegionFade.AlphaAfter(-1f, 0.6f), 1e-6f, "a negative elapsed clamps to the start");
            Assert.AreEqual(0f, RegionFade.AlphaAfter(0f, 0f), 1e-6f, "a zero/negative duration is instantly clear");
            Assert.AreEqual(0f, RegionFade.AlphaAfter(0.1f, -1f), 1e-6f, "negative duration clamps clear, no divide blow-up");
        }

        [Test]
        public void AlphaAfter_IsMonotonicNonIncreasing()
        {
            float prev = 2f;
            for (float t = 0f; t <= 1.2f; t += 0.05f)
            {
                float a = RegionFade.AlphaAfter(t, 0.6f);
                Assert.LessOrEqual(a, prev + 1e-6f, $"alpha must never rise as the fade clears (t={t})");
                Assert.That(a, Is.InRange(0f, 1f), "alpha stays in [0,1]");
                prev = a;
            }
        }

        // ---- ShouldCover: a real arrival, in either direction --------------------------------

        [Test]
        public void ShouldCover_TrueOnlyForARealNewArrival()
        {
            Assert.IsTrue(RegionFade.ShouldCover("Greybox", "Greywick"), "Cove → Greywick fades");
            Assert.IsTrue(RegionFade.ShouldCover("Greywick", "Greybox"), "Greywick → Cove fades (both directions)");
            Assert.IsFalse(RegionFade.ShouldCover("Greywick", "Greywick"), "same scene is a no-op — no fade");
            Assert.IsFalse(RegionFade.ShouldCover("Greybox", ""), "an unnamed/boot target does not fade");
            Assert.IsFalse(RegionFade.ShouldCover("Greybox", null), "a null target does not fade");
            Assert.IsTrue(RegionFade.ShouldCover("", "Greywick"), "first activation into a named region fades");
            Assert.IsTrue(RegionFade.ShouldCover(null, "Greywick"), "null previous still fades into a named region");
        }

        // ---- ArrivalTitle: a readable card from a scene name ---------------------------------

        [Test]
        public void ArrivalTitle_PrettifiesTheSceneName()
        {
            Assert.AreEqual("Coddle Cove", RegionFade.ArrivalTitle("CoddleCove"), "splits camelCase");
            Assert.AreEqual("Port Greywick", RegionFade.ArrivalTitle("Port_Greywick"), "splits underscores");
            Assert.AreEqual("Region 1", RegionFade.ArrivalTitle("Region-1"), "splits hyphens and digits");
            Assert.AreEqual("Greywick", RegionFade.ArrivalTitle("Greywick"), "a single word is unchanged");
            Assert.AreEqual("Greybox", RegionFade.ArrivalTitle("Greybox"), "the greybox cove scene reads as-is");
            Assert.AreEqual("", RegionFade.ArrivalTitle(""), "empty in, empty out");
            Assert.AreEqual("", RegionFade.ArrivalTitle(null), "null in, empty out (never throws)");
        }
    }
}
