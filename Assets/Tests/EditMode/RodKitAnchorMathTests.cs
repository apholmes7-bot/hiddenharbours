using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The kit's pixel→world conversions (<see cref="RodKitAnchorMath"/>) — pinned against hand-worked
    /// values from the REAL kit geometry, because a silently flipped axis or a forgotten PPU is exactly
    /// how an overlay ends up bouncing anti-phase off the wrong lever arm (the overlay-pose lesson).
    /// Cell px are TOP-LEFT origin, screen-down; world is metres, y-up.
    /// </summary>
    public class RodKitAnchorMathTests
    {
        [Test]
        public void CellPxToPivotWorld_TheRodCellsOwnPivot_IsTheOrigin()
        {
            // The rod cell is 112×112 with pivot (56,72) — the grip centre. The pivot itself is (0,0).
            Vector2 w = RodKitAnchorMath.CellPxToPivotWorld(56f, 72f, 56f, 72f, 32f);
            Assert.AreEqual(0f, w.x, 1e-5f);
            Assert.AreEqual(0f, w.y, 1e-5f);
        }

        [Test]
        public void CellPxToPivotWorld_FlipsScreenDownToWorldUp()
        {
            // A point 32 px RIGHT of and 16 px ABOVE the pivot (screen-up = smaller y) at PPU 32:
            // one metre right, half a metre up.
            Vector2 w = RodKitAnchorMath.CellPxToPivotWorld(88f, 56f, 56f, 72f, 32f);
            Assert.AreEqual(1f, w.x, 1e-5f);
            Assert.AreEqual(0.5f, w.y, 1e-5f);
        }

        [Test]
        public void CellPxToPivotWorld_TheCharacterGrip_LandsWhereTheHandIs()
        {
            // The character cell is 64×88, pivot (32,80) — the feet. The kit's baked hold grip sits
            // near (37.3, 61.7): a hair right of centre, ~18 px above the feet — in world, ~0.17 m
            // right, ~0.57 m up. The exact values pin the formula against the real data shape.
            Vector2 w = RodKitAnchorMath.CellPxToPivotWorld(37.3f, 61.7f, 32f, 80f, 32f);
            Assert.AreEqual((37.3f - 32f) / 32f, w.x, 1e-5f);
            Assert.AreEqual((80f - 61.7f) / 32f, w.y, 1e-5f);
            Assert.Greater(w.y, 0f, "a grip above the feet is UP in world space");
        }

        [Test]
        public void OffsetPxToWorld_NegatesScreenDownDy()
        {
            // The bobber's stem-top lineAttach is dy = −6 (screen-UP): +6/32 m in world.
            Vector2 w = RodKitAnchorMath.OffsetPxToWorld(0f, -6f, 32f);
            Assert.AreEqual(0f, w.x, 1e-5f);
            Assert.AreEqual(6f / 32f, w.y, 1e-5f);
            // A fish mouth at dx=12, dy=4 (right + screen-down): right and DOWN in world.
            Vector2 m = RodKitAnchorMath.OffsetPxToWorld(12f, 4f, 32f);
            Assert.AreEqual(0.375f, m.x, 1e-5f);
            Assert.AreEqual(-0.125f, m.y, 1e-5f);
        }

        [Test]
        public void DegeneratePpu_NeverDividesByZero()
        {
            Vector2 w = RodKitAnchorMath.OffsetPxToWorld(32f, 32f, 0f);
            Assert.IsFalse(float.IsNaN(w.x) || float.IsInfinity(w.x));
            Vector2 c = RodKitAnchorMath.CellPxToPivotWorld(1f, 1f, 0f, 0f, -5f);
            Assert.IsFalse(float.IsNaN(c.y) || float.IsInfinity(c.y));
        }
    }
}
