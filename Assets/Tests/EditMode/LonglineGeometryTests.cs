using HiddenHarbours.Fishing;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class LonglineGeometryTests
    {
        [Test]
        public void TubsGiveTheirHookCount_WithEqualInteriorGaps()
        {
            using var rig = new LonglineTestRig();
            rig.Def.HooksPerTub = 4;
            Assert.IsTrue(LonglineGeometry.TryBuild(rig.Def, 2, Vector2.zero, new Vector2(90f, 0f),
                out Vector2[] hooks, out LonglineRefusal reason));
            Assert.AreEqual(LonglineRefusal.None, reason);
            Assert.AreEqual(8, hooks.Length);
            for (int i = 0; i < 8; i++)
            {
                Assert.AreEqual(10f * (i + 1), hooks[i].x, 0.00001f);
                Assert.AreEqual(0f, hooks[i].y);
            }
        }

        [Test]
        public void ALineLongerThanThePaidOutTubs_IsRefused()
        {
            using var rig = new LonglineTestRig();
            Assert.IsFalse(LonglineGeometry.TryBuild(rig.Def, 1, Vector2.zero, new Vector2(101f, 0f),
                out Vector2[] hooks, out LonglineRefusal reason));
            Assert.AreEqual(LonglineRefusal.LineTooLong, reason);
            Assert.IsEmpty(hooks);
        }

        [Test]
        public void InvalidTubCountsAndEnds_AreRefused()
        {
            using var rig = new LonglineTestRig();
            foreach (int tubs in new[] { 0, 4 })
            {
                Assert.IsFalse(LonglineGeometry.TryValidate(rig.Def, tubs, Vector2.zero, Vector2.one, out _, out var reason));
                Assert.AreEqual(LonglineRefusal.InvalidTubCount, reason);
            }
            Assert.IsFalse(LonglineGeometry.TryValidate(rig.Def, 1, Vector2.zero, Vector2.zero, out _, out var same));
            Assert.AreEqual(LonglineRefusal.InvalidEnds, same);
            Assert.IsFalse(LonglineGeometry.TryValidate(rig.Def, 1, Vector2.zero, new Vector2(float.NaN, 0f), out _, out var nan));
            Assert.AreEqual(LonglineRefusal.InvalidEnds, nan);
        }

        [Test]
        public void ReversingTheEnds_ReversesTheSameHooks()
        {
            var a = new Vector2(10f, 20f);
            var b = new Vector2(50f, 70f);
            for (int i = 0; i < 4; i++)
                Assert.Less(Vector2.Distance(LonglineGeometry.HookPosition(a, b, 4, i),
                    LonglineGeometry.HookPosition(b, a, 4, 3 - i)), 0.00001f);
        }
    }
}
