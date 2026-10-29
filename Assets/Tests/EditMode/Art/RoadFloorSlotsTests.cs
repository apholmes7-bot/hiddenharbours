using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Art;
using NUnit.Framework;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// NMC H4: the road floor's slot plan. The baked chunks in view are wanted nearest first up to the pool's capacity
    /// (the rest are its overflow); loads start at most so many a frame; a slot is freed from the chunk wanted longest
    /// ago, never from a loading one, and every evicted chunk is named, so the index is cleared before its slot is
    /// reused. Pure: no engine call.
    /// </summary>
    public class RoadFloorSlotsTests
    {
        const int W = 10, H = 8;
        readonly List<RoadFloorSlots.Load> _loads = new List<RoadFloorSlots.Load>();
        readonly List<int> _evicted = new List<int>();

        static bool[] AllBaked() => Enumerable.Repeat(true, W * H).ToArray();
        static int C(int x, int y) => y * W + x;

        [Test]
        public void TheChunksInView_AreWantedNearestFirst_AndLoadIntoFreeSlots()
        {
            var slots = new RoadFloorSlots(W, H, AllBaked(), 8);
            slots.Want(1, 2, 2, 4, 3, 3.5f, 3.0f, 99, _loads, _evicted);

            Assert.AreEqual(6, _loads.Count, "a view of 3 x 2 chunks did not load its six");
            Assert.IsEmpty(_evicted, "free slots remained, yet a chunk was evicted");
            Assert.AreEqual(0, slots.Overflow, "six chunks fit eight slots");
            Assert.AreEqual(new[] { C(3, 2), C(3, 3), C(2, 2), C(4, 2), C(2, 3), C(4, 3) }, _loads.Select(l => l.Chunk).ToArray(),
                "the loads are not nearest the view's centre (3.5, 3.0) first, the grid's order breaking ties");
            Assert.AreEqual(6, _loads.Select(l => l.Slot).Distinct().Count(), "two loads share a slot");
            foreach (var l in _loads)
            {
                Assert.AreEqual(RoadFloorSlots.State.Loading, slots.StateOf(l.Chunk));
                Assert.AreEqual(l.Slot, slots.SlotOf(l.Chunk));
            }
            Assert.AreEqual(6, slots.Held);
        }

        [Test]
        public void ChunksTheBakeDoesNotHold_AreNeverWanted_AndTheViewIsClippedToTheGrid()
        {
            bool[] baked = AllBaked();
            baked[C(0, 0)] = false;
            var slots = new RoadFloorSlots(W, H, baked, 8);
            slots.Want(1, -3, -3, 0, 0, 0.5f, 0.5f, 99, _loads, _evicted);
            Assert.IsEmpty(_loads, "a chunk the bake does not hold, or one off the grid, was loaded");
            Assert.IsFalse(slots.Pending);
        }

        [Test]
        public void LoadsAreThrottled_AndThePlanSaysItIsNotDone()
        {
            var slots = new RoadFloorSlots(W, H, AllBaked(), 8);
            slots.Want(1, 2, 2, 4, 3, 3.5f, 3.0f, 2, _loads, _evicted);
            Assert.AreEqual(2, _loads.Count, "more loads started than the throttle allows");
            Assert.IsTrue(slots.Pending, "chunks in view were left absent and the plan did not say so");
            slots.Want(2, 2, 2, 4, 3, 3.5f, 3.0f, 2, _loads, _evicted);
            Assert.AreEqual(2, _loads.Count, "the next frame did not start the next two");
            slots.Want(3, 2, 2, 4, 3, 3.5f, 3.0f, 2, _loads, _evicted);
            Assert.AreEqual(2, _loads.Count);
            Assert.IsFalse(slots.Pending, "every chunk in view is loading, yet the plan is not done");
        }

        [Test]
        public void AViewWiderThanThePool_HoldsItsNearest_AndCountsTheRest()
        {
            var slots = new RoadFloorSlots(W, H, AllBaked(), 4);
            slots.Want(1, 0, 0, 3, 2, 2.0f, 1.5f, 99, _loads, _evicted);
            Assert.AreEqual(4, _loads.Count, "the pool of four did not fill");
            Assert.AreEqual(8, slots.Overflow, "twelve chunks in view, four held: eight are the ground");
            Assert.AreEqual(new[] { C(1, 1), C(2, 1), C(1, 0), C(2, 0) }, _loads.Select(l => l.Chunk).ToArray(),
                "the four held are not the four nearest the centre");
        }

        [Test]
        public void ASlotIsFreedFromTheChunkWantedLongestAgo_AndTheEvictedAreNamed()
        {
            var slots = new RoadFloorSlots(W, H, AllBaked(), 2);
            slots.Want(1, 0, 0, 0, 0, 0.5f, 0.5f, 99, _loads, _evicted);
            slots.Loaded(C(0, 0));
            slots.Want(2, 1, 0, 1, 0, 1.5f, 0.5f, 99, _loads, _evicted);
            slots.Loaded(C(1, 0));
            int oldSlot = slots.SlotOf(C(0, 0));

            slots.Want(3, 2, 0, 2, 0, 2.5f, 0.5f, 99, _loads, _evicted);
            Assert.AreEqual(new[] { C(0, 0) }, _evicted.ToArray(), "the chunk wanted longest ago (frame 1) was not the one evicted");
            Assert.AreEqual(RoadFloorSlots.State.Absent, slots.StateOf(C(0, 0)));
            Assert.AreEqual(-1, slots.SlotOf(C(0, 0)), "an evicted chunk still names its slot");
            Assert.AreEqual(1, _loads.Count);
            Assert.AreEqual(oldSlot, _loads[0].Slot, "the new chunk did not take the evicted chunk's slot");
            Assert.AreEqual(RoadFloorSlots.State.Resident, slots.StateOf(C(1, 0)), "the chunk wanted at frame 2 was evicted too");
        }

        [Test]
        public void ALoadingChunk_IsNeverEvicted_SoASlotsMapsAreAlwaysItsChunks()
        {
            var slots = new RoadFloorSlots(W, H, AllBaked(), 1);
            slots.Want(1, 0, 0, 0, 0, 0.5f, 0.5f, 99, _loads, _evicted);   // c(0,0) loading, not yet loaded
            slots.Want(2, 5, 5, 5, 5, 5.5f, 5.5f, 99, _loads, _evicted);
            Assert.IsEmpty(_evicted, "a loading chunk was evicted: its maps would land in another chunk's slot");
            Assert.IsEmpty(_loads);
            Assert.IsTrue(slots.Pending, "the chunk in view waits for a slot, and the plan did not say so");

            slots.Loaded(C(0, 0));
            slots.Want(3, 5, 5, 5, 5, 5.5f, 5.5f, 99, _loads, _evicted);
            Assert.AreEqual(new[] { C(0, 0) }, _evicted.ToArray(), "once loaded and out of view, the chunk was not freed");
            Assert.AreEqual(C(5, 5), _loads.Single().Chunk);
        }

        [Test]
        public void AChunkWantedThisFrame_IsNeverEvictedForAnother()
        {
            var slots = new RoadFloorSlots(W, H, AllBaked(), 2);
            slots.Want(1, 0, 0, 1, 0, 1.0f, 0.5f, 99, _loads, _evicted);
            slots.Loaded(C(0, 0));
            slots.Loaded(C(1, 0));
            slots.Want(2, 0, 0, 2, 0, 1.0f, 0.5f, 99, _loads, _evicted);   // three in view, two slots: both wanted now
            Assert.IsEmpty(_evicted, "a chunk wanted this frame was evicted for a farther one");
            Assert.IsEmpty(_loads);
            Assert.AreEqual(1, slots.Overflow);
        }

        [Test]
        public void ARefusedChunk_FreesItsSlot_AndIsNotTriedAgain()
        {
            var slots = new RoadFloorSlots(W, H, AllBaked(), 2);
            slots.Want(1, 0, 0, 0, 0, 0.5f, 0.5f, 99, _loads, _evicted);
            slots.LoadFailed(C(0, 0));
            Assert.AreEqual(RoadFloorSlots.State.Failed, slots.StateOf(C(0, 0)));
            Assert.AreEqual(0, slots.Held, "a refused chunk kept its slot");
            slots.Want(2, 0, 0, 0, 0, 0.5f, 0.5f, 99, _loads, _evicted);
            Assert.IsEmpty(_loads, "a refused chunk was loaded again (every frame would retry it)");
            Assert.Throws<System.InvalidOperationException>(() => slots.Loaded(C(0, 0)), "a chunk not loading was marked loaded");
        }

        [Test]
        public void TheSameViews_PlanTheSameLoads()
        {
            string Plan()
            {
                var slots = new RoadFloorSlots(W, H, AllBaked(), 5);
                var log = new List<string>();
                for (int f = 1; f <= 6; f++)
                {
                    slots.Want(f, f - 1, 1, f + 1, 3, f + 0.5f, 2.5f, 3, _loads, _evicted);
                    log.Add(string.Join(",", _loads.Select(l => l.Chunk + ">" + l.Slot)) + "|" + string.Join(",", _evicted));
                    foreach (var l in _loads.ToArray()) slots.Loaded(l.Chunk);
                }
                return string.Join(";", log);
            }
            Assert.AreEqual(Plan(), Plan(), "the same views planned differently");
        }
    }
}
