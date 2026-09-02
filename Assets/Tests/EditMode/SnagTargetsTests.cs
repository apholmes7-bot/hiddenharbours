using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The Core registry of things in the water that drift can foul on (<see cref="SnagTargets"/>):
    /// the seam through which the ambient fleet's buoys and lying-to hulls reach the drifting
    /// seaweed without either module naming the other. Pins the contract a publisher and a reader
    /// both lean on — set moves rather than duplicates, remove keeps the rest reachable, a blank id is
    /// refused, and the version bumps on every real change.
    /// </summary>
    public class SnagTargetsTests
    {
        [SetUp] public void SetUp() => SnagTargets.Clear();
        [TearDown] public void TearDown() => SnagTargets.Clear();

        [Test]
        public void Set_AddsThenMovesInPlace_NeverDuplicates()
        {
            SnagTargets.Set("fleet.a.b0.s0", new Vector2(1f, 2f), 0f);
            SnagTargets.Set("fleet.a.b0.s0", new Vector2(3f, 4f), 0f);

            Assert.AreEqual(1, SnagTargets.Count, "re-setting an id moves it; it must not appear twice");
            Assert.IsTrue(SnagTargets.TryGet("fleet.a.b0.s0", out var t));
            Assert.AreEqual(new Vector2(3f, 4f), t.Position);
        }

        [Test]
        public void Remove_KeepsEveryOtherEntryReachable_ByIdAndByIndex()
        {
            SnagTargets.Set("a", new Vector2(0f, 0f), 0f);
            SnagTargets.Set("b", new Vector2(1f, 0f), 0.5f);
            SnagTargets.Set("c", new Vector2(2f, 0f), 1f);

            Assert.IsTrue(SnagTargets.Remove("a"), "a was there");
            Assert.IsFalse(SnagTargets.Remove("a"), "a is gone — a second remove is a no-op");
            Assert.AreEqual(2, SnagTargets.Count);

            // The swap-remove moved 'c' into a's slot: the id index must follow it.
            Assert.IsTrue(SnagTargets.TryGet("c", out var c));
            Assert.AreEqual(1f, c.RadiusMeters, 1e-6f);
            Assert.IsTrue(SnagTargets.TryGet("b", out var b));
            Assert.AreEqual(0.5f, b.RadiusMeters, 1e-6f);

            bool sawB = false, sawC = false;
            for (int i = 0; i < SnagTargets.Active.Count; i++)
            {
                sawB |= SnagTargets.Active[i].Id == "b";
                sawC |= SnagTargets.Active[i].Id == "c";
            }
            Assert.IsTrue(sawB && sawC, "both survivors are still in the live list");
        }

        [Test]
        public void BlankId_IsRefused_AndNegativeRadiusClampsToZero()
        {
            SnagTargets.Set(null, Vector2.zero, 0f);
            SnagTargets.Set("", Vector2.zero, 0f);
            Assert.AreEqual(0, SnagTargets.Count, "an id that could never be removed must never be added");

            SnagTargets.Set("h", Vector2.zero, -3f);
            Assert.IsTrue(SnagTargets.TryGet("h", out var h));
            Assert.AreEqual(0f, h.RadiusMeters, "a hull cannot have a negative beam");
        }

        [Test]
        public void Version_BumpsOnEveryRealChange_AndOnlyThen()
        {
            int v0 = SnagTargets.Version;
            SnagTargets.Set("a", Vector2.zero, 0f);
            int v1 = SnagTargets.Version;
            Assert.Greater(v1, v0, "an add is a change");

            SnagTargets.Remove("nobody");
            Assert.AreEqual(v1, SnagTargets.Version, "removing an unknown id changes nothing");

            SnagTargets.Set("a", Vector2.one, 0f);
            Assert.Greater(SnagTargets.Version, v1, "a move is a change a reader must see");

            int v2 = SnagTargets.Version;
            SnagTargets.Clear();
            Assert.Greater(SnagTargets.Version, v2);
            int v3 = SnagTargets.Version;
            SnagTargets.Clear();
            Assert.AreEqual(v3, SnagTargets.Version, "clearing an empty registry is not a change");
        }
    }
}
