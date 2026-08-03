using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The compass port's HEADING-MAPPING drift guard (ADR 0025 S2a) — the handoff's explicit trap:
    /// "the compass card in the rig may use its own internal convention — golden the mapping, don't
    /// eyeball it". Goldens are a Python transcription of <c>compassRig.js</c> :94-98 (helpers) and
    /// :100-104 + :158 (the card-rotation contract: a mark authored at card-direction d lands at
    /// screen angle d − heading, so the heading always reads at the top lubber).
    ///
    /// <para>Also pins the SOURCE convention end to end: the boat heading fed to the compass is
    /// <see cref="BoatKinematics.BearingDegrees"/> of the PHYSICS ROOT's bow — 0 = N, 90 = E,
    /// clockwise — the same canon the rig declares (compassRig.js:3-4), so the two can never
    /// silently disagree about North.</para>
    /// </summary>
    public class CompassHeadingGoldenTests
    {
        private const double Tol = 1e-6;

        [Test]
        public void Helpers_MatchTheRigSource()
        {
            // Python transcription of compassRig.js:96-98 (jsround = floor(x+0.5)).
            Assert.That(CompassRigRender.Norm(0), Is.EqualTo(0.0).Within(Tol));
            Assert.That(CompassRigRender.Norm(22.4), Is.EqualTo(22.399999999999977).Within(Tol));
            Assert.That(CompassRigRender.Norm(-45), Is.EqualTo(315.0).Within(Tol));
            Assert.That(CompassRigRender.Norm(720.25), Is.EqualTo(0.25).Within(Tol));

            Assert.That(CompassRigRender.Cardinal8(0), Is.EqualTo("N"));
            Assert.That(CompassRigRender.Cardinal8(22.4), Is.EqualTo("N"));
            Assert.That(CompassRigRender.Cardinal8(22.5), Is.EqualTo("NE"), "round half-up at the boundary");
            Assert.That(CompassRigRender.Cardinal8(45), Is.EqualTo("NE"));
            Assert.That(CompassRigRender.Cardinal8(337.4), Is.EqualTo("NW"));
            Assert.That(CompassRigRender.Cardinal8(337.5), Is.EqualTo("N"), "wraps through index 8 % 8");
            Assert.That(CompassRigRender.Cardinal8(-45), Is.EqualTo("NW"));

            Assert.That(CompassRigRender.FmtDeg(0), Is.EqualTo("000"));
            Assert.That(CompassRigRender.FmtDeg(22.5), Is.EqualTo("023"));
            Assert.That(CompassRigRender.FmtDeg(45), Is.EqualTo("045"));
            Assert.That(CompassRigRender.FmtDeg(315), Is.EqualTo("315"));
            Assert.That(CompassRigRender.FmtDeg(359.6), Is.EqualTo("360"),
                        "the rig's own quirk — rounded before wrapping; ported faithfully");
        }

        // Card mark rotation: (d, heading) → screen offset at the card rim (r = 96), y-down px.
        // Python transcription through the actual rotate(-heading) matrix (compassRig.js:158).
        private static readonly double[][] MarkGolden =
        {
            //        d     heading   sx                    sy
            new[] {   0.0,    0.0,    0.0,                 -96.0 },
            new[] {  90.0,   90.0,    0.0,                 -96.0 },
            new[] {  45.0,   30.0,   24.846628329842005,  -92.72887932375056 },
            new[] { 180.0,  235.0,  -78.6385962517432,    -55.06333788970044 },
        };

        [Test]
        public void CardMark_LandsAtScreenAngle_DMinusHeading()
        {
            foreach (double[] row in MarkGolden)
            {
                CompassRigRender.MarkScreenOffset(row[0], row[1], 96.0, out double sx, out double sy);
                Assert.That(sx, Is.EqualTo(row[2]).Within(Tol), $"d={row[0]} h={row[1]}");
                Assert.That(sy, Is.EqualTo(row[3]).Within(Tol), $"d={row[0]} h={row[1]}");
            }
        }

        [Test]
        public void TheHeadingMark_AlwaysReadsAtTheTopLubber()
        {
            // The defining property: the mark for the CURRENT heading sits at 12 o'clock (0, −r) —
            // that is what "direct-reading at the lubber line" means. Any sign error in the
            // rotation (the classic CW/CCW flip) puts every non-cardinal heading somewhere else.
            foreach (double h in new[] { 0.0, 37.0, 90.0, 235.0, 359.0 })
            {
                CompassRigRender.MarkScreenOffset(h, h, 96.0, out double sx, out double sy);
                Assert.That(sx, Is.EqualTo(0.0).Within(1e-9), $"heading {h}");
                Assert.That(sy, Is.EqualTo(-96.0).Within(1e-9), $"heading {h}");
            }
            // And a boat swinging to STARBOARD (heading increasing) carries the N mark to PORT
            // (negative x) — the card holds north while the case turns with the hull.
            CompassRigRender.MarkScreenOffset(0.0, 10.0, 96.0, out double nx, out _);
            Assert.That(nx, Is.LessThan(0.0), "N drifts to port as the bow swings starboard");
        }

        [Test]
        public void TheHeadingSource_IsThePhysicsRootBearingCanon()
        {
            // The overlay feeds the compass BoatKinematics.HeadingDegrees — the bearing of the
            // physics root's bow (ActiveBoatProbe reads _boat.transform.up; the counter-rotating
            // visual child never enters the seam). Pin the shared bearing convention here so the
            // compass and the producer can never drift apart: 0 = N (+Y), 90 = E (+X), clockwise.
            Assert.That(BoatKinematics.BearingDegrees(Vector2.up), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(BoatKinematics.BearingDegrees(Vector2.right), Is.EqualTo(90f).Within(1e-4f));
            Assert.That(BoatKinematics.BearingDegrees(Vector2.down), Is.EqualTo(180f).Within(1e-4f));
            Assert.That(BoatKinematics.BearingDegrees(Vector2.left), Is.EqualTo(270f).Within(1e-4f));
            // …and the rig's own cardinal table agrees with the HUD's 16-point table about those.
            Assert.That(CompassRigRender.Cardinal8(BoatKinematics.BearingDegrees(Vector2.up)), Is.EqualTo("N"));
            Assert.That(CompassRigRender.Cardinal8(BoatKinematics.BearingDegrees(Vector2.right)), Is.EqualTo("E"));
            Assert.That(CompassRigRender.Cardinal8(BoatKinematics.BearingDegrees(Vector2.up)),
                        Is.EqualTo(CompassReadout.Cardinal(0f)), "one North across compass reads");
        }

        // ---- NEGATIVE CONTROL ----------------------------------------------------------------------

        [Test]
        public void NegativeControl_AFlippedRotation_BreaksTheMarkGoldens()
        {
            // The classic failure this guard exists for: rotating the card by +heading instead of
            // −heading. Emulate it by negating the heading and prove the goldens move far past
            // tolerance at a non-cardinal heading.
            CompassRigRender.MarkScreenOffset(45.0, -30.0, 96.0, out double sx, out double sy);
            double err = System.Math.Abs(sx - 24.846628329842005) + System.Math.Abs(sy - -92.72887932375056);
            Assert.That(err, Is.GreaterThan(Tol * 1e3), "a CW/CCW flip must be caught");
        }
    }
}
