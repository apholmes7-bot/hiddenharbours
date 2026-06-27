using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guard for the Y → sorting-order mapping behind <see cref="YSortSprite"/> — the contract that
    /// makes grass/trees auto-layer with the player: lower world Y draws in front, clamped into a safe band so
    /// it never slips behind ground/water or above the HUD.
    /// </summary>
    public class YSortSpriteTests
    {
        [Test]
        public void OrderFor_AtZeroY_IsBaseOrder()
        {
            Assert.AreEqual(10, YSortSprite.OrderFor(0f, 10f, 4f, 2, 40));
        }

        [Test]
        public void OrderFor_LowerY_SortsInFront()
        {
            // A sprite lower on the screen (smaller Y) must get a HIGHER order (drawn in front).
            int front = YSortSprite.OrderFor(-1f, 10f, 4f, 2, 40);
            int back = YSortSprite.OrderFor(1f, 10f, 4f, 2, 40);
            Assert.Greater(front, back, "Lower Y must sort in front of higher Y.");
            Assert.AreEqual(14, front);   // 10 - (-1)*4
            Assert.AreEqual(6, back);     // 10 - 1*4
        }

        [Test]
        public void OrderFor_MonotonicNonIncreasingInY()
        {
            int prev = int.MaxValue;
            for (float y = -10f; y <= 10f; y += 0.5f)
            {
                int o = YSortSprite.OrderFor(y, 10f, 4f, 2, 40);
                Assert.LessOrEqual(o, prev, "Order must not increase as Y increases.");
                prev = o;
            }
        }

        [Test]
        public void OrderFor_ClampsToSafeBand()
        {
            // Far 'down' (very negative Y) saturates at maxOrder; far 'up' saturates at minOrder — so a
            // Y-sorted sprite can never cross out of the world-decor band into ground/water or the HUD.
            Assert.AreEqual(40, YSortSprite.OrderFor(-100f, 10f, 4f, 2, 40), "Far-down clamps to maxOrder.");
            Assert.AreEqual(2, YSortSprite.OrderFor(100f, 10f, 4f, 2, 40), "Far-up clamps to minOrder.");
        }

        [Test]
        public void OrderFor_PlayerAndGrass_InterleaveOnSameScale()
        {
            // Same parameters (the shared default) → a grass tuft just below the player draws over it, and a
            // tuft just above draws under it. This is the whole point: consistent scale = automatic layering.
            float playerY = 0f;
            int player = YSortSprite.OrderFor(playerY, 10f, 4f, 2, 40);
            int tuftInFront = YSortSprite.OrderFor(playerY - 0.5f, 10f, 4f, 2, 40);
            int tuftBehind = YSortSprite.OrderFor(playerY + 0.5f, 10f, 4f, 2, 40);
            Assert.Greater(tuftInFront, player, "A tuft below the player must draw in front of the player.");
            Assert.Less(tuftBehind, player, "A tuft above the player must draw behind the player.");
        }
    }
}
