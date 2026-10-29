using HiddenHarbours.Fishing;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class LonglinePlacementTests
    {
        [Test]
        public void WaterAtBothDepthLimits_IsAllowed()
        {
            using var rig = new LonglineTestRig();
            foreach (float depth in new[] { 3f, 40f })
            {
                rig.Terrain.At = _ => -depth;
                Assert.IsTrue(LonglinePlacement.CanPlaceAt(rig.Def, 1, Vector2.zero, new Vector2(100f, 0f),
                    rig.Environment, rig.Terrain, 0d, out var reason));
                Assert.AreEqual(LonglineRefusal.None, reason);
            }
        }

        [Test]
        public void AShoalEnd_IsRefusedWithItsReason()
        {
            using var rig = new LonglineTestRig();
            rig.Terrain.At = p => p.x == 100f ? -2f : -10f;
            Assert.IsFalse(LonglinePlacement.CanPlaceAt(rig.Def, 1, Vector2.zero, new Vector2(100f, 0f),
                rig.Environment, rig.Terrain, 0d, out var reason));
            Assert.AreEqual(LonglineRefusal.EndTooShallow, reason);
        }

        [Test]
        public void AHookOverLand_IsRefusedEvenWithWetEnds()
        {
            using var rig = new LonglineTestRig();
            rig.Def.HooksPerTub = 1;
            rig.Terrain.At = p => p.x == 50f ? 1f : -10f;
            Assert.IsFalse(LonglinePlacement.CanPlaceAt(rig.Def, 1, Vector2.zero, new Vector2(100f, 0f),
                rig.Environment, rig.Terrain, 0d, out var reason));
            Assert.AreEqual(LonglineRefusal.HookOnLand, reason);
        }

        [Test]
        public void AShoalHook_IsRefusedWithItsReason()
        {
            using var rig = new LonglineTestRig();
            rig.Def.HooksPerTub = 1;
            rig.Terrain.At = p => p.x == 50f ? -2f : -10f;
            Assert.IsFalse(LonglinePlacement.CanPlaceAt(rig.Def, 1, Vector2.zero, new Vector2(100f, 0f),
                rig.Environment, rig.Terrain, 0d, out var reason));
            Assert.AreEqual(LonglineRefusal.HookTooShallow, reason);
        }

        [Test]
        public void DepthIsReadAtTheSetMoment()
        {
            using var rig = new LonglineTestRig();
            rig.Environment.LevelAt = time => (float)time;
            Assert.IsTrue(LonglinePlacement.CanPlaceAt(rig.Def, 1, Vector2.zero, new Vector2(100f, 0f),
                rig.Environment, rig.Terrain, 0d, out _));
            Assert.IsFalse(LonglinePlacement.CanPlaceAt(rig.Def, 1, Vector2.zero, new Vector2(100f, 0f),
                rig.Environment, rig.Terrain, 50d, out var reason));
            Assert.AreEqual(LonglineRefusal.EndTooDeep, reason);
        }

        [Test]
        public void MissingWaterOrDryEnd_FailsClosed()
        {
            using var rig = new LonglineTestRig();
            Assert.IsFalse(LonglinePlacement.CanPlaceAt(rig.Def, 1, Vector2.zero, Vector2.one,
                rig.Environment, null, 0d, out var absent));
            Assert.AreEqual(LonglineRefusal.MissingWater, absent);
            rig.Terrain.At = _ => 0f;
            Assert.IsFalse(LonglinePlacement.CanPlaceAt(rig.Def, 1, Vector2.zero, Vector2.one,
                rig.Environment, rig.Terrain, 0d, out var dry));
            Assert.AreEqual(LonglineRefusal.EndOnLand, dry);
        }
    }
}
