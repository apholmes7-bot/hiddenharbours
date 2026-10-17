using System;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Reduced, deterministic integration of shipped force helpers, not a Box2D acceptance run.
    /// Inertia is fitted to L6/L10's first 12 seconds (45.62 / 832.16); no waves or collisions.
    /// Load full authored hull/polar assets. Bars are the owner's Phase B rulings, independent
    /// of tuning. The engine integration fixture separately checks the controller applies the terms.
    /// </summary>
    public class SloopMotionModelTests
    {
        internal static BoatHullDef Hull(int size)
            => AssetDatabase.LoadAssetAtPath<BoatHullDef>($"Assets/_Project/Data/Boats/Sloop{size}.asset");

        [TestCase(30, 1f, 13f)] [TestCase(30, .75f, 20f)]
        [TestCase(30, .5f, 25f)] [TestCase(30, .25f, 30f)]
        [TestCase(88, 1f, 13f)] [TestCase(88, .75f, 20f)]
        [TestCase(88, .5f, 25f)] [TestCase(88, .25f, 30f)]
        public void Tack_LeavesIronsWithinProposedBar(int size, float helm, float barSeconds)
        {
            var result = Run(size, helm, false, false, 150f);
            Assert.That(result.Escaped, Is.True, "a permanent stall is not a slow pay-off");
            Assert.That(result.Hang, Is.LessThan(barSeconds), "bar is an owner ruling, not read from the tuning");
        }

        [TestCase(30)] [TestCase(88)]
        public void CentredHelm_DeadHeadToWindPaysOffWithinTwentySeconds(int size)
        {
            var result = Run(size, 0f, false, true, 60f);
            Assert.That(result.Escaped, Is.True);
            Assert.That(result.Hang, Is.LessThan(20f));
        }

        [TestCase(30, 800f)] [TestCase(88, 15000f)]
        public void AuthoredSloop_HasTheProposedOptIn(int size, float payOff)
        {
            var h = Hull(size);
            Assert.That(h.IronsPayOffTorque, Is.EqualTo(payOff));
            Assert.That(h.RudderFullAuthorityWayMs, Is.EqualTo(2f));
            Assert.That(h.SailYawReferenceWindKn, Is.EqualTo(10f));
        }

        [Test]
        public void UnconfiguredHull_OptsOutOfPayOff()
        {
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            try
            {
                Assert.That(hull.IronsPayOffTorque, Is.Zero);
                Assert.That(SailDrive.IronsPayOffTorque(true, 20f, 1f, 0f, hull.IronsPayOffTorque,
                    hull.RudderFullAuthorityWayMs, 10f, hull.SailYawReferenceWindKn), Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(hull); }
        }

        public struct Result
        {
            public float Hang, Distance, Twa, WayKn, ThroughWaterKn, TargetKn;
            public bool Escaped;
        }

        internal static Result Run(int size, float helm, bool unmanned, bool deadHead, float seconds)
        {
            var h = Hull(size);
            Assert.That(h != null, Is.True); Assert.That(h.HasSailPlan, Is.True);
            float mass = h.MassKg / 100f, inertia = size == 30 ? 45.62f : 832.16f;
            var windMs = new Vector2(1.19f, 4.28f); var current = new Vector2(.32f, .0056f);
            float from = BoatKinematics.BearingDegrees(-windMs);
            float heading = unmanned ? 260f : deadHead ? from : from - 55f;
            float speed = deadHead || unmanned ? 0f : size == 30 ? 2.314f : 3.634f;
            Vector2 velocity = unmanned ? Vector2.zero : Forward(heading) * speed + current;
            Vector2 pos = Vector2.zero;
            float spin = 0f; bool entered = false; var result = new Result();
            const float dt = .02f;
            for (int step = 0; step < Mathf.RoundToInt(seconds / dt); step++)
            {
                Vector2 fwd = Forward(heading), through = velocity - current;
                float way = Vector2.Dot(through, fwd);
                SailWind wind = SailDrive.ResolveWind(windMs, fwd, velocity);
                bool irons = SailDrive.IsInIrons(wind.TrueAngleDeg, wind.ApparentAngleDeg,
                    h.NoGoTrueWindDeg, h.SpriteIronsApparentDeg);
                float target = SailDrive.TargetSpeedKn(h.SailPolar, wind.TrueAngleDeg, wind.TrueKn, h.NoGoTrueWindDeg);
                float thrust = SailDrive.ThrustFor(SailDrive.ToMetresPerSecond(target),
                    SailDrive.LinearResistance(h.ForwardDrag, mass, .2f, .01f));
                Vector2 along = fwd * way;
                Vector2 force = (fwd * thrust - along * h.ForwardDrag
                    - (through - along) * h.LateralDrag + windMs * h.WindExposure) * .01f;
                float torque = BoatController.RudderTorque(helm, h.RudderAuthority, way, h.RudderFullAuthorityWayMs)
                    + SailDrive.IronsPayOffTorque(irons, wind.TrueAngleDeg, helm, way, h.IronsPayOffTorque,
                        h.RudderFullAuthorityWayMs, wind.TrueKn, h.SailYawReferenceWindKn);
                spin = (spin + torque * .01f / inertia * dt) / (1f + 2.5f * dt);
                heading -= spin * Mathf.Rad2Deg * dt;
                velocity = (velocity + force / mass * dt) / (1f + .2f * dt);
                pos += velocity * dt;
                if (irons) { entered = true; result.Hang += dt; }
                result.Twa = wind.TrueAngleDeg; result.WayKn = SailDrive.ToKnots(way);
                result.ThroughWaterKn = SailDrive.ToKnots(through.magnitude);
                result.TargetKn = target; result.Distance = pos.magnitude;
                if (entered && !irons && !unmanned) { result.Escaped = true; break; }
            }
            return result;
        }
        internal static Vector2 Forward(float heading)
            => new Vector2(Mathf.Sin(heading * Mathf.Deg2Rad), Mathf.Cos(heading * Mathf.Deg2Rad));
    }
}
