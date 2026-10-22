using System;
using HiddenHarbours.Boats;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class HullBodyDampingTests
    {
        [TestCase(4f)] [TestCase(7f)] [TestCase(891.9f)]
        public void FreeHull_InUniformCurrent_SettlesWithTheWater(float mass)
        {
            var current = new Vector2(.32f, .0056f);
            var velocity = Vector2.zero;
            const float dt = .02f;
            for (int i = 0; i < 15000; i++)
                velocity = (velocity + BoatController.BodyDampingCurrentForce(current, mass, .2f) / mass * dt)
                    / (1f + .2f * dt);
            Assert.That((velocity - current).magnitude, Is.LessThan(.00002f),
                "a free body must travel with the stream, independent of hull mass");
        }

        [Test]
        public void StillWater_EveryStepIsBitIdenticalToTheOriginalIntegrator()
        {
            var oldVelocity = new Vector2(2.314f, -.17f);
            var velocity = oldVelocity;
            const float mass = 48.75f, dt = .02f;
            for (int i = 0; i < 15000; i++)
            {
                var force = new Vector2((i % 11) - 5f, (i % 7) - 3f);
                oldVelocity = (oldVelocity + force / mass * dt) / (1f + .2f * dt);
                var correctedForce = force + BoatController.BodyDampingCurrentForce(Vector2.zero, mass, .2f);
                velocity = (velocity + correctedForce / mass * dt) / (1f + .2f * dt);
                Assert.That(BitConverter.SingleToInt32Bits(velocity.x),
                    Is.EqualTo(BitConverter.SingleToInt32Bits(oldVelocity.x)), "x bits at step " + i);
                Assert.That(BitConverter.SingleToInt32Bits(velocity.y),
                    Is.EqualTo(BitConverter.SingleToInt32Bits(oldVelocity.y)), "y bits at step " + i);
            }
        }

        [Test]
        public void UndampedBody_ReceivesNoCurrentCorrection()
            => Assert.That(BoatController.BodyDampingCurrentForce(new Vector2(.32f, .0056f), 7f, 0f),
                Is.EqualTo(Vector2.zero));

        [Test]
        public void Trim_DriftingWithCurrent_HasNoBodyDampingAcceleration()
        {
            const float current = .32f, groundSpeed = .32f;
            Assert.That(HullTrimMath.DriveAcceleration(0f, 7f, .2f, groundSpeed - current), Is.Zero);
        }
    }
}
