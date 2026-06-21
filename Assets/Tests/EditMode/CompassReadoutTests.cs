using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// VS-19 — the heading compass + set-&-drift pure conversions (CompassReadout), built on the Core
    /// heading seam (ADR 0007). Mirrors HudFormatTests: the strings/glyphs a sailor reads in under a
    /// second are pinned exactly. A cross-check ties the compass to WindReadout so heading and wind can
    /// never disagree on North (the seam's whole point — one dial for both).
    /// </summary>
    public class CompassReadoutTests
    {
        // ---- cardinal (16-point, from degrees) ----------------------------------------------

        [Test]
        public void Cardinal_MapsBearingToCompassPoint()
        {
            Assert.AreEqual("N",   CompassReadout.Cardinal(0f));
            Assert.AreEqual("E",   CompassReadout.Cardinal(90f));
            Assert.AreEqual("S",   CompassReadout.Cardinal(180f));
            Assert.AreEqual("W",   CompassReadout.Cardinal(270f));
            Assert.AreEqual("NE",  CompassReadout.Cardinal(45f));
            Assert.AreEqual("NNE", CompassReadout.Cardinal(22.5f));
            Assert.AreEqual("ENE", CompassReadout.Cardinal(67.5f));
        }

        [Test]
        public void Cardinal_WrapsAnyInput()
        {
            Assert.AreEqual("N",  CompassReadout.Cardinal(360f), "360 wraps to N");
            Assert.AreEqual("N",  CompassReadout.Cardinal(359f), "359 rounds back to N");
            Assert.AreEqual("NW", CompassReadout.Cardinal(-45f), "negative bearings wrap");
        }

        // ---- degrees (zero-padded 3-digit) --------------------------------------------------

        [Test]
        public void Degrees_ZeroPadsToThreeDigits()
        {
            Assert.AreEqual("045°", CompassReadout.Degrees(45f));
            Assert.AreEqual("005°", CompassReadout.Degrees(5f));
            Assert.AreEqual("359°", CompassReadout.Degrees(359f));
            Assert.AreEqual("000°", CompassReadout.Degrees(0f));
        }

        [Test]
        public void Degrees_WrapsAnyInput()
        {
            Assert.AreEqual("000°", CompassReadout.Degrees(360f), "360 wraps to 000");
            Assert.AreEqual("359°", CompassReadout.Degrees(-1f), "negative wraps");
        }

        // ---- heading arrow (8-point shape channel) ------------------------------------------

        [Test]
        public void HeadingArrow_PointsTheWayTheBowFaces()
        {
            Assert.AreEqual("↑", CompassReadout.HeadingArrow(0f),   "N");
            Assert.AreEqual("↗", CompassReadout.HeadingArrow(45f),  "NE");
            Assert.AreEqual("→", CompassReadout.HeadingArrow(90f),  "E");
            Assert.AreEqual("↘", CompassReadout.HeadingArrow(135f), "SE");
            Assert.AreEqual("↓", CompassReadout.HeadingArrow(180f), "S");
            Assert.AreEqual("↙", CompassReadout.HeadingArrow(225f), "SW");
            Assert.AreEqual("←", CompassReadout.HeadingArrow(270f), "W");
            Assert.AreEqual("↖", CompassReadout.HeadingArrow(315f), "NW");
            Assert.AreEqual("↑", CompassReadout.HeadingArrow(359f), "wraps back to N");
        }

        // ---- ribbon (the rose tape) ---------------------------------------------------------

        [Test]
        public void Ribbon_CentresTheHeading_WithTheRoseScrollingPast()
        {
            // Centre column = the heading; N/E/S/W are letters, intercardinals '+', '·' between.
            Assert.AreEqual("W····+····N····+····E", CompassReadout.Ribbon(0f),   "N centred");
            Assert.AreEqual("N····+····E····+····S", CompassReadout.Ribbon(90f),  "E centred");
            Assert.AreEqual("E····+····S····+····W", CompassReadout.Ribbon(180f), "S centred");
        }

        [Test]
        public void Ribbon_IsTheRequestedWidth()
        {
            Assert.AreEqual(21, CompassReadout.Ribbon(37f).Length, "default width is 21");
            Assert.AreEqual(11, CompassReadout.Ribbon(123f, 11).Length, "honours a custom width");
        }

        // ---- set & drift (course-over-ground vs heading: the crabbing read) -----------------

        [Test]
        public void SetAndDrift_ReadsOnTrack_WhenBowAndCourseAgree()
        {
            Assert.AreEqual("COG 000°  on track", CompassReadout.SetAndDrift(0f, 0f, 3f));
            Assert.AreEqual("COG 090°  on track", CompassReadout.SetAndDrift(90f, 90f, 3f));
            Assert.AreEqual("COG 002°  on track", CompassReadout.SetAndDrift(0f, 2f, 3f),
                "a hair of leeway (≤ deadband) still reads on track");
        }

        [Test]
        public void SetAndDrift_ShowsTheSet_WhenCrabbing()
        {
            // Course clockwise of the heading = being set to STARBOARD; counter-clockwise = to PORT.
            Assert.AreEqual("COG 045°  → 45° stbd", CompassReadout.SetAndDrift(0f, 45f, 3f));
            Assert.AreEqual("COG 315°  ← 45° port", CompassReadout.SetAndDrift(0f, 315f, 3f));
            Assert.AreEqual("COG 040°  → 30° stbd", CompassReadout.SetAndDrift(10f, 40f, 5f));
        }

        [Test]
        public void SetAndDrift_HasNoCourse_AtRest()
        {
            Assert.AreEqual("COG —", CompassReadout.SetAndDrift(0f, 0f, 0.1f),
                "below the underway threshold there is no steady course");
            Assert.AreEqual("COG —", CompassReadout.SetAndDrift(90f, 270f, 0f),
                "dead in the water → no course (no jittery bearing)");
        }

        // ---- the pinned cross-check: compass and wind agree on North ------------------------

        [Test]
        public void Compass_AgreesWithWindReadout_OnNorth()
        {
            // For a spread of directions, the compass (from the seam's bearing) and WindReadout's
            // independent cardinal/arrow must agree — they share one convention by design (ADR 0007).
            foreach (var v in new[]
            {
                Vector2.up, Vector2.right, Vector2.down, Vector2.left,
                new Vector2(1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f), new Vector2(-1f, 1f),
                new Vector2(1f, -2f), new Vector2(-3f, 1f),
            })
            {
                float bearing = BoatKinematics.BearingDegrees(v);
                Assert.AreEqual(WindReadout.Cardinal(v), CompassReadout.Cardinal(bearing),
                    $"cardinal must agree for {v}");
                Assert.AreEqual(WindReadout.ArrowGlyph(v), CompassReadout.HeadingArrow(bearing),
                    $"arrow must agree for {v}");
            }
        }
    }
}
