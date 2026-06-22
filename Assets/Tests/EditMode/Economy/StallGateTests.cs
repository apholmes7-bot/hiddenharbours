using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.Economy
{
    /// <summary>
    /// The stall proximity gate — selling at the Fish Buyer (B) and buying at the Shipwright (P): the
    /// action fires only when the player is ON FOOT and within reach of the stall, never from anywhere
    /// and never while aboard. Pins the in-range gate (the runtime player-resolution is greybox glue).
    /// </summary>
    public class StallGateTests
    {
        [Test]
        public void InRange_IsInclusiveAtTheBoundary()
        {
            Assert.IsTrue(StallGate.InRange(Vector2.zero, new Vector2(2f, 0f), 4f), "well inside is in range");
            Assert.IsTrue(StallGate.InRange(Vector2.zero, new Vector2(4f, 0f), 4f), "exactly at the radius counts");
            Assert.IsFalse(StallGate.InRange(Vector2.zero, new Vector2(4.01f, 0f), 4f), "just past the radius is out");
        }

        [Test]
        public void CanInteract_RequiresInRange()
        {
            var stall = new Vector2(10f, 5f);
            Assert.IsTrue(StallGate.CanInteract(true, new Vector2(11f, 5f), stall, 4f),
                "on foot, 1 m from the stall → can interact");
            Assert.IsFalse(StallGate.CanInteract(true, new Vector2(16f, 5f), stall, 4f),
                "on foot, 6 m away (range 4) → too far, do nothing");
        }

        [Test]
        public void CanInteract_RequiresOnFoot()
        {
            var stall = new Vector2(10f, 5f);
            Assert.IsTrue(StallGate.CanInteract(true, stall, stall, 4f),
                "on foot AND right at the stall → yes");
            Assert.IsFalse(StallGate.CanInteract(false, stall, stall, 4f),
                "aboard, even standing on the stall → no (selling/buying is an on-foot action)");
        }
    }
}
