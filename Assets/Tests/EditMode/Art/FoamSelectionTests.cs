using System;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public class FoamSelectionTests
    {
        static FoamCandidate Point(string key, float x, float y, float radius = 1)
        {
            var point = new Vector2(x, y);
            return new FoamCandidate(new FoamInjection(point, point, radius, 1, 1), key, 0);
        }

        [Test]
        public void FoamSelection_UsesSweptAndDispersedCameraFootprints()
        {
            var window = new Rect(-1, -1, 2, 2);
            var slots = new FoamSelectionSlot[FoamBuffer.MaxInjectors];
            var sweep = new FoamInjection(new Vector2(-5, 0), new Vector2(5, 0), 0.1f, 1, 1);
            // A bent old track reaches the camera although the hull and both sweep endpoints do not.
            var track = new FoamDispersal(new Vector2(8, 8), new Vector2(8, 4),
                new Vector2(4, 4), new Vector2(0, 4), new Vector2(-4, 4), new Vector2(-8, 4),
                1, 3.1f, 1, 0.2f, 0.5f);
            var dispersed = new FoamInjection(new Vector2(8, 9), new Vector2(8, 8), 1, 1, 1, track);
            var candidates = new[]
            {
                Point("remote", 50, 50),
                new FoamCandidate(sweep, "sweep", 1),
                new FoamCandidate(dispersed, "dispersed", 2),
                Point("radius", 1.5f, 0),
                Point("corner-miss", 1.8f, 1.8f), // in expanded AABB, outside the round capsule
                Point("tangent", 2, 0),
                new FoamCandidate(new FoamInjection(new Vector2(-4, 0), new Vector2(0, 4),
                    0.1f, 1, 1), "diagonal-miss", 6)
            };
            Assert.AreEqual(4, FoamSelection.Select(candidates, candidates.Length, window, slots, out int eligible));
            Assert.AreEqual(4, eligible);
            var keys = new string[4];
            for (int i = 0; i < keys.Length; i++) keys[i] = slots[i].Candidate.StableKey;
            CollectionAssert.AreEquivalent(new[] { "sweep", "dispersed", "radius", "tangent" }, keys);
            Assert.IsFalse(FoamSelection.Footprint(new FoamInjection(dispersed.From, dispersed.To,
                dispersed.Radius, 1, 1), window, out _), "Inactive dispersal must not retain old reach");
            var wideEdge = new FoamDispersal(track.Node0, track.Node1, track.Node2, track.Node3,
                track.Node4, track.Node5, 1, 2, 1, 100, 0.5f);
            Assert.IsFalse(FoamSelection.Footprint(new FoamInjection(dispersed.From, dispersed.To,
                1, 1, 1, wideEdge), window, out _), "Edge softness cannot exceed the shader envelope");
        }

        [Test]
        public void FoamSelection_StableTiesIgnoreRegistrationOrder()
        {
            var candidates = new FoamCandidate[12];
            for (int i = 0; i < candidates.Length; i++)
                candidates[i] = Point("scene/hull-" + i.ToString("D2"), i % 2 == 0 ? -2 : 2, 0);
            var slots = new FoamSelectionSlot[FoamBuffer.MaxInjectors];
            var window = new Rect(-10, -10, 20, 20);
            // Rotate and reverse registration order while keeping the scene identities unchanged.
            for (int pass = 0; pass < candidates.Length * 2; pass++)
            {
                if (pass == candidates.Length) Array.Reverse(candidates);
                FoamCandidate first = candidates[0];
                Array.Copy(candidates, 1, candidates, 0, candidates.Length - 1);
                candidates[candidates.Length - 1] = first;
                Assert.AreEqual(8, FoamSelection.Select(candidates, candidates.Length, window, slots, out _));
                for (int i = 0; i < 8; i++)
                    Assert.AreEqual("scene/hull-" + i.ToString("D2"), slots[i].Candidate.StableKey);
            }
        }

        [Test]
        public void FoamSelection_OverflowPreservesEightSlotBudget()
        {
            var candidates = new FoamCandidate[128];
            for (int i = 0; i < candidates.Length; i++)
                candidates[i] = Point("hull-" + i, candidates.Length - i, 0, 0);
            var window = new Rect(-200, -200, 400, 400);
            var slots = new FoamSelectionSlot[16]; // oversized output must still honour the shader cap
            Assert.AreEqual(8, FoamSelection.Select(candidates, candidates.Length, window, slots, out int eligible));
            Assert.AreEqual(candidates.Length, eligible);
            for (int i = 0; i < 8; i++) Assert.AreEqual(i + 1, slots[i].Candidate.Injection.To.x);
            var small = new FoamSelectionSlot[2];
            Assert.AreEqual(2, FoamSelection.Select(candidates, candidates.Length, window, small, out eligible));
            Assert.AreEqual(128, eligible);
            Assert.AreEqual(1, small[0].Candidate.Injection.To.x);
            Assert.AreEqual(2, small[1].Candidate.Injection.To.x);
            Assert.AreEqual(0, FoamSelection.Select(candidates, 0, window, slots, out eligible));
            Assert.AreEqual(0, eligible, "Reused output cannot resurrect last frame's candidates");
            Assert.AreEqual(0, FoamSelection.Select(candidates, 128, window, null, out _));
            Assert.AreEqual(0, FoamSelection.Select(candidates, 128, window, Array.Empty<FoamSelectionSlot>(), out _));
            // Warm the exact overflowing path, then measure only selection (no NUnit/array allocations).
            FoamSelection.Select(candidates, 128, window, slots, out _);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100; i++) FoamSelection.Select(candidates, 128, window, slots, out _);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0, allocated);
        }

        [Test]
        public void FoamSelection_CamerasAreIndependent()
        {
            var candidates = new FoamCandidate[20];
            for (int i = 0; i < 10; i++)
            {
                candidates[i] = Point("west-" + i, i, 0, 0);
                candidates[i + 10] = Point("east-" + i, 100 + i, 0, 0);
            }
            var west = new Rect(-10, -10, 30, 20);
            var east = new Rect(90, -10, 30, 20);
            var westSlots = new FoamSelectionSlot[8];
            var eastSlots = new FoamSelectionSlot[8];
            Assert.AreEqual(8, FoamSelection.Select(candidates, 20, east, eastSlots, out int eastEligible));
            Assert.AreEqual(8, FoamSelection.Select(candidates, 20, west, westSlots, out int westEligible));
            Assert.AreEqual(10, westEligible);
            Assert.AreEqual(10, eastEligible);
            for (int i = 0; i < 8; i++)
            {
                StringAssert.StartsWith("west-", westSlots[i].Candidate.StableKey);
                StringAssert.StartsWith("east-", eastSlots[i].Candidate.StableKey);
            }
            string eastFirst = eastSlots[0].Candidate.StableKey;
            FoamSelection.Select(candidates, 20, east, westSlots, out _); // reverse camera order/reuse scratch
            Assert.AreEqual(eastFirst, westSlots[0].Candidate.StableKey);
            FoamSelection.Select(candidates, 20, west, westSlots, out _);
            Assert.AreEqual(eastFirst, eastSlots[0].Candidate.StableKey);
            // Overlapping cameras may both select the very same unconsumed deposit.
            Assert.AreEqual(8, FoamSelection.Select(candidates, 20, west, eastSlots, out _));
            for (int i = 0; i < 8; i++)
                Assert.AreEqual(westSlots[i].Candidate.StableKey, eastSlots[i].Candidate.StableKey);
        }
    }
}
