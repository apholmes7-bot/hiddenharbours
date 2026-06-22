using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// VS-19 — the apparent-wind pure conversions (ApparentWindReadout): the true wind expressed
    /// RELATIVE to the bow. Mirrors CompassReadoutTests: the angle/arrow/point-of-sail a sailor reads
    /// in a beat are pinned exactly. A cross-check ties the read to BoatKinematics + WindReadout so the
    /// apparent wind, the compass, and the true-wind widget can never disagree on North / which side.
    ///
    /// Convention under test: result is where the wind COMES FROM, off the bow, in [-180, 180):
    /// 0 = dead ahead (headwind), + = from starboard, - = from port, ±180 = dead astern (following).
    /// The wind vector points where the air BLOWS TOWARD, so the source is its reverse.
    /// </summary>
    public class ApparentWindReadoutTests
    {
        private const float Tol = 1e-3f;

        // ---- relative bearing (the core mapping) --------------------------------------------

        [Test]
        public void RelativeBearing_HeadingNorth_ReadsWindOffTheBow()
        {
            // Boat points North (heading 0). Wind vector = where the air blows TOWARD.
            // Wind toward S → blowing FROM the North → dead ahead (headwind) → 0.
            Assert.AreEqual(0f,    ApparentWindReadout.RelativeBearing(0f, new Vector2(0f, -1f)), Tol, "from ahead");
            // Wind toward W → from the East → on the starboard side → +90.
            Assert.AreEqual(90f,   ApparentWindReadout.RelativeBearing(0f, new Vector2(-1f, 0f)), Tol, "from starboard");
            // Wind toward E → from the West → on the port side → -90.
            Assert.AreEqual(-90f,  ApparentWindReadout.RelativeBearing(0f, new Vector2(1f, 0f)),  Tol, "from port");
            // Wind toward N → from the South → dead astern (following) → -180.
            Assert.AreEqual(-180f, ApparentWindReadout.RelativeBearing(0f, new Vector2(0f, 1f)),  Tol, "from astern");
        }

        [Test]
        public void RelativeBearing_RotatesWithHeading()
        {
            // Boat now points East (heading 90). Wind toward S → from the North.
            // Relative to an East-facing bow, a northerly is on the PORT beam → -90.
            Assert.AreEqual(-90f, ApparentWindReadout.RelativeBearing(90f, new Vector2(0f, -1f)), Tol);
            // Same wind, boat pointing West (270): a northerly is now on the STARBOARD beam → +90.
            Assert.AreEqual(90f, ApparentWindReadout.RelativeBearing(270f, new Vector2(0f, -1f)), Tol);
        }

        // ---- arrow (8-point shape channel, boat frame: bow = up) ----------------------------

        [Test]
        public void Arrow_PointsToWhereTheWindComesFrom_InBoatFrame()
        {
            Assert.AreEqual("↑", ApparentWindReadout.Arrow(0f),    "from ahead");
            Assert.AreEqual("↗", ApparentWindReadout.Arrow(45f),   "off the stbd bow");
            Assert.AreEqual("→", ApparentWindReadout.Arrow(90f),   "from starboard");
            Assert.AreEqual("↓", ApparentWindReadout.Arrow(180f),  "from astern");
            Assert.AreEqual("←", ApparentWindReadout.Arrow(-90f),  "from port");
            Assert.AreEqual("↖", ApparentWindReadout.Arrow(-45f),  "off the port bow");
            Assert.AreEqual("↓", ApparentWindReadout.Arrow(-180f), "astern wraps to down");
        }

        // ---- point of sail (the word) -------------------------------------------------------

        [Test]
        public void PointOfSail_NamesHeadAndFollowingWinds()
        {
            Assert.AreEqual("ahead",  ApparentWindReadout.PointOfSail(0f));
            Assert.AreEqual("ahead",  ApparentWindReadout.PointOfSail(10f),  "within the deadband still reads ahead");
            Assert.AreEqual("astern", ApparentWindReadout.PointOfSail(180f));
            Assert.AreEqual("astern", ApparentWindReadout.PointOfSail(-175f), "near dead astern reads astern");
        }

        [Test]
        public void PointOfSail_NamesSideAndSector()
        {
            Assert.AreEqual("stbd bow",     ApparentWindReadout.PointOfSail(45f));
            Assert.AreEqual("port bow",     ApparentWindReadout.PointOfSail(-45f));
            Assert.AreEqual("stbd beam",    ApparentWindReadout.PointOfSail(90f));
            Assert.AreEqual("port beam",    ApparentWindReadout.PointOfSail(-90f));
            Assert.AreEqual("stbd quarter", ApparentWindReadout.PointOfSail(150f));
            Assert.AreEqual("port quarter", ApparentWindReadout.PointOfSail(-150f));
        }

        // ---- calm ---------------------------------------------------------------------------

        [Test]
        public void IsCalm_TrueOnlyForNearZeroWind()
        {
            Assert.IsTrue(ApparentWindReadout.IsCalm(Vector2.zero));
            Assert.IsTrue(ApparentWindReadout.IsCalm(new Vector2(0f, 0.01f)), "a whisper is calm");
            Assert.IsFalse(ApparentWindReadout.IsCalm(new Vector2(0f, 3f)));
        }

        // ---- the full read (arrow + degrees + word) -----------------------------------------

        [Test]
        public void Format_ReadsApparentWind()
        {
            Assert.AreEqual("Apparent ↑ 0° ahead",
                ApparentWindReadout.Format(0f, new Vector2(0f, -1f)), "headwind");
            Assert.AreEqual("Apparent → 90° stbd beam",
                ApparentWindReadout.Format(0f, new Vector2(-1f, 0f)), "wind on the starboard beam");
            Assert.AreEqual("Apparent ↓ 180° astern",
                ApparentWindReadout.Format(0f, new Vector2(0f, 1f)), "following wind");
            // Wind toward SW (blowing FROM the NE) on a North-facing bow → off the starboard bow.
            Assert.AreEqual("Apparent ↗ 45° stbd bow",
                ApparentWindReadout.Format(0f, new Vector2(-1f, -1f)));
        }

        [Test]
        public void Format_ReadsCalm_WhenNoWind()
        {
            Assert.AreEqual("Apparent " + HudStrings.WindCalm + " calm",
                ApparentWindReadout.Format(123f, Vector2.zero));
        }

        // ---- cross-check: one bearing convention across modules ------------------------------

        [Test]
        public void RelativeBearing_ComposesTheCoreHeadingPrimitive()
        {
            // The read must be exactly RelativeBearingDegrees(heading, source-bearing), where the
            // source bearing is the reverse of the (blow-toward) wind vector — i.e. it leans on the
            // shared Core math, never an independent copy that could drift.
            foreach (var v in SpreadVectors())
                foreach (float h in new[] { 0f, 37f, 90f, 215f, 350f })
                {
                    float expected = BoatKinematics.RelativeBearingDegrees(h, BoatKinematics.BearingDegrees(-v));
                    Assert.AreEqual(expected, ApparentWindReadout.RelativeBearing(h, v), Tol,
                        $"apparent must compose the Core primitive for v={v}, h={h}");
                }
        }

        [Test]
        public void ApparentSource_AgreesWithWindReadout_OnNorth()
        {
            // The wind's SOURCE direction (the reverse vector) must read the same cardinal whether you
            // ask WindReadout (true-wind widget) or CompassReadout (the dial) — one convention, no drift.
            foreach (var v in SpreadVectors())
            {
                float sourceBearing = BoatKinematics.BearingDegrees(-v);
                Assert.AreEqual(WindReadout.Cardinal(-v), CompassReadout.Cardinal(sourceBearing),
                    $"source cardinal must agree for {v}");
            }
        }

        private static Vector2[] SpreadVectors() => new[]
        {
            Vector2.up, Vector2.right, Vector2.down, Vector2.left,
            new Vector2(1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f), new Vector2(-1f, 1f),
            new Vector2(1f, -2f), new Vector2(-3f, 1f), new Vector2(2.5f, 4f),
        };
    }
}
