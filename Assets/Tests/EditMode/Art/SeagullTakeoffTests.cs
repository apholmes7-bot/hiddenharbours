using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <b>A gull takes off from where it sat, from rest, facing the way it goes</b> (owner, 2026-11-15, on
    /// the shore clip: the take-off must start at the perch point, accelerate from rest, and the sprite must
    /// face the way it travels).
    ///
    /// <para>🔴 <b>What these would have caught.</b> The rejoin ramp PULLED the drawn position a fraction of
    /// the way to the wheel every step, so the bird was already moving at 2-4 m/s on its first frame; the
    /// chain from <c>takeoff</c> into <c>fly</c> clamped the altitude up to the fly band's 6 m floor in one
    /// step - a 3.5 m jump up the screen, logged at 207 m/s on the clip's own state table; and the sprite kept
    /// the heading it had stood on while the pull carried it off sideways. Every case below steps the pure
    /// state machine at the clip's 60 fps against a wheel slot well away from the bird, which is the case the
    /// clip showed.</para>
    /// </summary>
    public class SeagullTakeoffTests
    {
        const double FrameMs = 1000.0 / 60.0;

        static SeagullBehaviour Behaviour()
        {
            var def = Resources.Load<SeagullVisualDef>(SeagullVisualDef.ResourcesPath);
            Assert.IsNotNull(def, "the committed SeagullVisualDef did not load");
            Assert.IsNotNull(def.Behaviour, "the def produced no behaviour table");
            return def.Behaviour;
        }

        /// <summary>A wheel slot 12 m off, cruising at 9 m, flying away from the bird's own heading.</summary>
        static readonly SeagullFlockMath.SeagullFlockBird Wheel = new SeagullFlockMath.SeagullFlockBird(
            -8.0, 9.0, 9.0, 2.5, 3, SeagullFlockMath.SeagullFlockAnim.Glide, 1, 1.0);
        const double CentreX = 0.0, CentreY = 0.0;

        struct Sample { public double X, Y, Alt, Heading, FreeX, FreeY, FreeAlt; public int Dir; public string State; }

        /// <summary>A gull standing at (3, -2) facing east, told to go, stepped until it is on the wheel.</summary>
        static List<Sample> Departure(SeagullBehaviour b, out SeagullSimBird start)
        {
            var bird = new SeagullSimBird { X = 3.0, Y = -2.0, Heading = Math.PI * 0.5, IntentState = -1 };
            SeagullStateMachine.Enter(ref bird, b.IndexOf(SeagullStates.Stand), b);
            start = bird;
            Assert.IsTrue(SeagullStateMachine.CommandDepart(ref bird, b), "a standing gull could not take off");

            var trace = new List<Sample> { Snap(b, bird) };
            int limit = (int)Math.Ceiling(b.Flock.RegroupSeconds * 1000.0 / FrameMs) + 120;
            for (int i = 0; i < limit && !bird.FlockBound; i++)
            {
                SeagullStateMachine.Step(ref bird, FrameMs, b, in Wheel, CentreX, CentreY);
                trace.Add(Snap(b, bird));
            }
            Assert.IsTrue(bird.FlockBound, "the departure never handed the bird to the wheel");
            return trace;
        }

        static Sample Snap(SeagullBehaviour b, in SeagullSimBird s) => new Sample
        { X = s.X, Y = s.Y, Alt = s.AltitudeMetres, Heading = s.Heading, Dir = s.Dir, State = b.IdOf(s.State),
          FreeX = s.FreeX, FreeY = s.FreeY, FreeAlt = s.FreeAltitudeMetres };

        static double ScreenStep(Sample a, Sample c)
        {
            double h = IsoGround.HeightScale;
            return Math.Sqrt((c.X - a.X) * (c.X - a.X) + (c.Y + c.Alt * h - a.Y - a.Alt * h)
                                                       * (c.Y + c.Alt * h - a.Y - a.Alt * h));
        }

        [Test]
        public void TheTakeoffStartsWhereTheBirdSat()
        {
            var b = Behaviour();
            List<Sample> t = Departure(b, out SeagullSimBird start);
            Assert.AreEqual(start.X, t[0].X, 1e-12, "the take-off moved the bird before it had stepped");
            Assert.AreEqual(start.Y, t[0].Y, 1e-12);
            Assert.AreEqual(0.0, t[0].Alt, 1e-12);

            // One frame in, the bird is still within a hair of its spot: the take-off row at full speed for a
            // whole frame is the most it could have covered, and from rest it covers a small fraction of that.
            var row = b.Row(b.IndexOf(SeagullStates.Takeoff));
            double fullFrame = Math.Max(row.SpeedMetresPerSecond, row.ClimbMetresPerSecond) * FrameMs / 1000.0;
            Assert.Less(ScreenStep(t[0], t[1]), 0.25 * fullFrame,
                $"the first frame of the take-off covered {ScreenStep(t[0], t[1]):F4} m - a bird leaving from " +
                $"rest covers a small part of the {fullFrame:F4} m the row's full speed would carry it");
        }

        [Test]
        public void TheTakeoffAcceleratesFromRest()
        {
            var b = Behaviour();
            List<Sample> t = Departure(b, out _);
            int takeoffFrames = 0;
            while (takeoffFrames + 1 < t.Count && t[takeoffFrames + 1].State == SeagullStates.Takeoff) takeoffFrames++;
            Assert.Greater(takeoffFrames, 4, "the take-off lasted too few frames to see it accelerate");
            for (int i = 2; i <= takeoffFrames; i++)
                Assert.GreaterOrEqual(ScreenStep(t[i - 1], t[i]), ScreenStep(t[i - 2], t[i - 1]) - 1e-9,
                    $"the take-off slowed down at frame {i}: a bird leaving the ground gathers speed");
        }

        /// <summary>
        /// No frame of the whole departure jumps. The bound is what the motion can honestly ask for: twice the
        /// fastest row speed (the from-rest ease peaks at 2x its mean) plus the departure blend's steepest
        /// rate (1.5 / ramp) times every distance it closes.
        /// </summary>
        /// <summary>
        /// The bird's own path keeps its velocity across the take-off -> fly chain. The take-off ends at its
        /// exit velocity (twice the row's mean) and the fly row's speed and climb ease in from there, so the
        /// change in velocity over the chain frame is of the same order as the take-off's own per-frame
        /// acceleration - not the step from the exit velocity to the cruise speed the clip showed (ground speed
        /// doubling, climb stopping, in one frame).
        /// </summary>
        [Test]
        public void TheHandoverIntoFlyKeepsTheVelocity()
        {
            var b = Behaviour();
            List<Sample> t = Departure(b, out _);
            int k = 1;
            while (k < t.Count && t[k].State == SeagullStates.Takeoff) k++;
            Assert.Less(k + 1, t.Count, "the departure never chained out of the take-off");

            double[] V(int i) => new[] { (t[i].FreeX - t[i - 1].FreeX) / FrameMs, (t[i].FreeY - t[i - 1].FreeY) / FrameMs,
                                         (t[i].FreeAlt - t[i - 1].FreeAlt) / FrameMs };
            double Dv(int i) { double[] a = V(i - 1), c = V(i); return Math.Sqrt((c[0] - a[0]) * (c[0] - a[0]) +
                                       (c[1] - a[1]) * (c[1] - a[1]) + (c[2] - a[2]) * (c[2] - a[2])); }

            double accel = 0.0;   // the take-off's own largest per-frame change in velocity
            for (int i = 3; i < k; i++) accel = Math.Max(accel, Dv(i));
            Assert.Greater(accel, 0.0, "the take-off never accelerated");
            for (int i = k; i <= k + 1; i++)
                Assert.LessOrEqual(Dv(i), 3.0 * accel,
                    $"frame {i} ({t[i - 1].State} -> {t[i].State}) changed the bird's velocity by " +
                    $"{Dv(i) * 1000.0:F3} m/s in one frame; the take-off itself accelerates by at most " +
                    $"{accel * 1000.0:F3} m/s per frame");
        }

        [Test]
        public void NoFrameOfTheDepartureJumps()
        {
            var b = Behaviour();
            List<Sample> t = Departure(b, out SeagullSimBird start);
            double fastest = 0.0;
            foreach (string id in new[] { SeagullStates.Takeoff, SeagullStates.Fly, SeagullStates.Glide })
            {
                var row = b.Row(b.IndexOf(id));
                fastest = Math.Max(fastest, Math.Abs(row.SpeedMetresPerSecond) + Math.Abs(row.ClimbMetresPerSecond));
            }
            double rampS = b.Flock.RegroupSeconds;
            double close = Math.Abs(CentreX + Wheel.X - start.X) + Math.Abs(CentreY + Wheel.Y - start.Y)
                         + Math.Abs(Wheel.Z) + fastest * rampS;
            double boundPerFrame = (2.0 * fastest + 1.5 * close / rampS) * FrameMs / 1000.0;

            double worst = 0.0; int at = -1;
            for (int i = 1; i < t.Count; i++)
            {
                double s = ScreenStep(t[i - 1], t[i]);
                if (s > worst) { worst = s; at = i; }
            }
            Assert.LessOrEqual(worst, boundPerFrame,
                $"frame {at} ({t[at - 1].State} -> {t[at].State}) moved the drawn bird {worst:F3} m up/across the " +
                $"screen in one frame; nothing in the motion asks for more than {boundPerFrame:F3} m");
        }

        [Test]
        public void TheSpriteFacesTheWayTheBirdTravels()
        {
            var b = Behaviour();
            List<Sample> t = Departure(b, out _);
            double eps = SeagullStateMachine.AltitudeEpsilonMetres;
            int checkedSteps = 0;
            for (int i = 1; i < t.Count - 1; i++)   // the last sample is the wheel's own pose
            {
                double dx = t[i].X - t[i - 1].X, dy = t[i].Y - t[i - 1].Y;
                if (dx * dx + dy * dy <= eps * eps) continue;
                checkedSteps++;
                int travelDir = SeagullFlockMath.DirOf(Math.Atan2(dx, dy));
                Assert.AreEqual(travelDir, t[i].Dir,
                    $"frame {i}: the bird travelled toward sheet row {travelDir} but was drawn facing {t[i].Dir}");
            }
            Assert.Greater(checkedSteps, 10, "too few moving frames to judge the facing");
        }

        [Test]
        public void TheDepartureIsDeterministic()
        {
            var b = Behaviour();
            List<Sample> a = Departure(b, out _), c = Departure(b, out _);
            Assert.AreEqual(a.Count, c.Count);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].X, c[i].X, 0.0, "frame " + i);
                Assert.AreEqual(a[i].Y, c[i].Y, 0.0, "frame " + i);
                Assert.AreEqual(a[i].Alt, c[i].Alt, 0.0, "frame " + i);
                Assert.AreEqual(a[i].Dir, c[i].Dir, "frame " + i);
            }
        }

        [Test]
        public void TheEasesStartAtRestAndKeepTheRowsTotals()
        {
            Assert.AreEqual(0.0, SeagullStateMachine.DepartureBlend(0.0), 0.0);
            Assert.AreEqual(1.0, SeagullStateMachine.DepartureBlend(1.0), 0.0);
            Assert.AreEqual(0.5, SeagullStateMachine.DepartureBlend(0.5), 1e-12);
            Assert.AreEqual(0.0, SeagullStateMachine.DepartureEase(0.0), 0.0, "the take-off starts from rest");
            // Mean of the ease over the state is 1: the row's sidecar climb (0 -> 1.2 m) and travel survive.
            const int n = 1000;
            double sum = 0.0;
            for (int i = 0; i < n; i++) sum += SeagullStateMachine.DepartureEase((i + 0.5) / n);
            Assert.AreEqual(1.0, sum / n, 1e-9, "the from-rest ease must keep the row's total climb and travel");
        }
    }
}
