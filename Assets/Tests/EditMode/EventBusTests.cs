using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.EditMode
{
    public class EventBusTests
    {
        private readonly struct Message
        {
            public readonly int Value;
            public Message(int value) => Value = value;
        }
        private readonly struct Nested { }
        private static int _delivered;
        private static object _allocationSink;
        private static void Count(Message message) => _delivered += message.Value;

        [SetUp]
        public void SetUp() { EventBus.Clear<Message>(); EventBus.Clear<Nested>(); }

        [TearDown]
        public void TearDown() { EventBus.Clear<Message>(); EventBus.Clear<Nested>(); }

        private static void Expect(string message) =>
            LogAssert.Expect(LogType.Exception, new Regex("^IOException: " + Regex.Escape(message) + "(?:\\r?\\n|$)"));

        [Test]
        public void Publish_EmptyChannel_ReturnsAndHasNoSubscribers()
        {
            EventBus.Publish(new Message(7));
            Assert.That(EventBus.HasSubscribers<Message>(), Is.False);
        }

        [Test]
        public void Publish_DeliversSynchronouslyInRegistrationOrder_WithPayload()
        {
            var seen = new List<int>();
            EventBus.Subscribe<Message>(m => seen.Add(m.Value));
            EventBus.Subscribe<Message>(m => seen.Add(m.Value + 1));
            EventBus.Publish(new Message(7));
            seen.Add(99);
            CollectionAssert.AreEqual(new[] { 7, 8, 99 }, seen);
        }

        [Test]
        public void Publish_ThrowingFirstHandler_LogsAndRunsLaterHandlers_AndReturns()
        {
            var seen = new List<int>();
            EventBus.Subscribe<Message>(_ => throw new IOException("first"));
            EventBus.Subscribe<Message>(_ => seen.Add(1));
            EventBus.Subscribe<Message>(_ => seen.Add(2));
            Expect("first");
            EventBus.Publish(default(Message));
            seen.Add(3);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, seen);
        }

        [Test]
        public void Publish_TwoFailingHandlers_LogsEachAndCompletes()
        {
            var seen = new List<int>();
            EventBus.Subscribe<Message>(_ => throw new IOException("first"));
            EventBus.Subscribe<Message>(_ => seen.Add(1));
            EventBus.Subscribe<Message>(_ => throw new IOException("middle"));
            EventBus.Subscribe<Message>(_ => seen.Add(2));
            Expect("first"); Expect("middle");
            EventBus.Publish(default(Message));
            seen.Add(3);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, seen);
        }

        [Test]
        public void Publish_CombinedDelegate_IsolatesEachLeaf()
        {
            var seen = new List<int>();
            Action<Message> combined = _ => throw new IOException("combined");
            combined += _ => seen.Add(1);
            EventBus.Subscribe(combined);
            Expect("combined");
            EventBus.Publish(default(Message));
            seen.Add(2);
            CollectionAssert.AreEqual(new[] { 1, 2 }, seen);
        }

        [Test]
        public void Publish_NestedFailure_DoesNotAbortOuterHandlerOrOuterTail()
        {
            var seen = new List<int>();
            EventBus.Subscribe<Nested>(_ => { seen.Add(2); throw new IOException("nested"); });
            EventBus.Subscribe<Nested>(_ => seen.Add(3));
            EventBus.Subscribe<Message>(_ =>
            {
                seen.Add(1);
                EventBus.Publish(default(Nested));
                seen.Add(4);
            });
            EventBus.Subscribe<Message>(_ => seen.Add(5));
            Expect("nested");
            EventBus.Publish(default(Message));
            seen.Add(6);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6 }, seen);
        }

        [Test]
        public void Publish_ReentrantSameChannel_IsDepthFirst()
        {
            var seen = new List<int>();
            EventBus.Subscribe<Message>(m =>
            {
                seen.Add(m.Value);
                if (m.Value == 1) EventBus.Publish(new Message(2));
                seen.Add(m.Value + 10);
            });
            EventBus.Subscribe<Message>(m => seen.Add(m.Value + 20));
            EventBus.Publish(new Message(1));
            CollectionAssert.AreEqual(new[] { 1, 2, 12, 22, 11, 21 }, seen);
        }

        [Test]
        public void Subscribe_DuringFailingHandler_OuterSnapshotStays_NestedSeesAddition() => MutateDuringPublish(0, true);
        [Test]
        public void Unsubscribe_DuringFailingHandler_OuterSnapshotStays_NestedSeesRemoval() => MutateDuringPublish(1, true);
        [Test]
        public void Clear_DuringFailingHandler_OuterSnapshotStays_NestedSeesEmpty() => MutateDuringPublish(2, true);
        [Test]
        public void Subscribe_DuringHandler_OuterSnapshotStays_NestedSeesAddition() => MutateDuringPublish(0, false);
        [Test]
        public void Unsubscribe_DuringHandler_OuterSnapshotStays_NestedSeesRemoval() => MutateDuringPublish(1, false);
        [Test]
        public void Clear_DuringHandler_OuterSnapshotStays_NestedSeesEmpty() => MutateDuringPublish(2, false);

        private static void MutateDuringPublish(int mutation, bool fail)
        {
            var seen = new List<int>();
            bool? hasAfterMutation = null;
            Action<Message> tail = m => seen.Add(20 + m.Value);
            Action<Message> added = m => seen.Add(30 + m.Value);
            EventBus.Subscribe<Message>(m =>
            {
                seen.Add(10 + m.Value);
                if (m.Value != 1) return;
                if (mutation == 0) EventBus.Subscribe(added);
                else if (mutation == 1) EventBus.Unsubscribe(tail);
                else EventBus.Clear<Message>();
                hasAfterMutation = EventBus.HasSubscribers<Message>();
                EventBus.Publish(new Message(2));
                if (fail) throw new IOException("mutation");
            });
            EventBus.Subscribe(tail);
            if (fail) Expect("mutation");
            EventBus.Publish(new Message(1));
            seen.Add(99);
            int[][] expected = { new[] { 11, 12, 22, 32, 21, 99 }, new[] { 11, 12, 21, 99 }, new[] { 11, 21, 99 } };
            CollectionAssert.AreEqual(expected[mutation], seen);
            Assert.That(hasAfterMutation, Is.EqualTo(mutation != 2));
        }

        [Test]
        public void SubscribeAndUnsubscribe_Null_AreNoOps()
        {
            EventBus.Subscribe<Message>(null); EventBus.Unsubscribe<Message>(null);
            Assert.That(EventBus.HasSubscribers<Message>(), Is.False);
            _delivered = 0;
            EventBus.Subscribe<Message>(Count);
            EventBus.Subscribe<Message>(null); EventBus.Unsubscribe<Message>(null);
            EventBus.Publish(new Message(1));
            Assert.That(_delivered, Is.EqualTo(1));
            Assert.That(EventBus.HasSubscribers<Message>(), Is.True);
        }

        [Test]
        public void Subscribe_Duplicates_RunOncePerRegistration()
        {
            _delivered = 0;
            EventBus.Subscribe<Message>(Count); EventBus.Subscribe<Message>(Count);
            EventBus.Publish(new Message(1));
            Assert.That(_delivered, Is.EqualTo(2));
        }

        [Test]
        public void Publish_DuplicateFailures_LogEachOccurrence_AndRemainSubscribed()
        {
            int calls = 0;
            Action<Message> failure = _ => { calls++; throw new IOException("duplicate"); };
            EventBus.Subscribe(failure); EventBus.Subscribe(failure);
            Expect("duplicate"); Expect("duplicate");
            EventBus.Publish(default(Message));
            Expect("duplicate"); Expect("duplicate");
            EventBus.Publish(default(Message));
            Assert.That(calls, Is.EqualTo(4));
            Assert.That(EventBus.HasSubscribers<Message>(), Is.True);
        }

        [Test]
        public void Unsubscribe_RemovesLastMatchingOccurrence()
        {
            var seen = new List<int>();
            Action<Message> a = _ => seen.Add(1);
            Action<Message> b = _ => seen.Add(2);
            EventBus.Subscribe(a); EventBus.Subscribe(b); EventBus.Subscribe(a);
            EventBus.Unsubscribe(a);
            EventBus.Publish(default(Message));
            CollectionAssert.AreEqual(new[] { 1, 2 }, seen);
        }

        [Test]
        public void Unsubscribe_CombinedDelegate_RemovesLastMatchingSubsequence()
        {
            var seen = new List<int>();
            Action<Message> a = _ => seen.Add(1), b = _ => seen.Add(2), c = _ => seen.Add(3);
            EventBus.Subscribe(a + b + c + a + b);
            EventBus.Unsubscribe(a + b);
            EventBus.Publish(default(Message));
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, seen);
        }

        [Test]
        public void Unsubscribe_MissingHandlerOrSubsequence_LeavesChannelAlone()
        {
            var seen = new List<int>();
            Action<Message> a = _ => seen.Add(1), b = _ => seen.Add(2), absent = _ => seen.Add(3);
            EventBus.Subscribe(a + b);
            EventBus.Unsubscribe(absent); EventBus.Unsubscribe(b + a);
            EventBus.Publish(default(Message));
            CollectionAssert.AreEqual(new[] { 1, 2 }, seen);
        }

        [Test]
        public void Clear_AndLastRemoval_UpdateHasSubscribers_AndAllowReuse()
        {
            _delivered = 0;
            EventBus.Subscribe<Message>(Count);
            EventBus.Unsubscribe<Message>(Count);
            Assert.That(EventBus.HasSubscribers<Message>(), Is.False);
            EventBus.Publish(new Message(100));
            EventBus.Subscribe<Message>(Count);
            EventBus.Clear<Message>();
            Assert.That(EventBus.HasSubscribers<Message>(), Is.False);
            EventBus.Publish(new Message(100));
            EventBus.Subscribe<Message>(Count);
            EventBus.Publish(new Message(1));
            Assert.That(_delivered, Is.EqualTo(1));
            Assert.That(EventBus.HasSubscribers<Message>(), Is.True);
        }

        [Test]
        public void Clear_OneChannel_DoesNotClearAnother()
        {
            int calls = 0;
            EventBus.Subscribe<Nested>(_ => calls++);
            EventBus.Subscribe<Message>(Count);
            EventBus.Clear<Message>();
            EventBus.Publish(default(Nested));
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(EventBus.HasSubscribers<Nested>(), Is.True);
        }

        [Test]
        public void Publish_WarmedStableSubscribers_AllocatesZeroBusBytes()
        {
            EventBus.Subscribe<Message>(Count); EventBus.Subscribe<Message>(Count);
            for (int i = 0; i < 10000; i++) EventBus.Publish(new Message(1));
            MeasurePublishAllocation(); // warm the measurement method/JIT too
            _delivered = 0;
            long bytes = MeasurePublishAllocation();
            Assert.That(_delivered, Is.EqualTo(200000));
            Assert.That(bytes, Is.EqualTo(0L), "bus-only stable dispatch must allocate zero bytes");
        }

        // Keep NUnit's boxed assertion arguments out of the measured method, including JIT hoisting.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static long MeasurePublishAllocation()
        {
            GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100000; i++) EventBus.Publish(new Message(1));
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        [Test]
        public void AllocationCounter_DetectsExplicitAllocation()
        {
            GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            _allocationSink = new byte[1024];
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(bytes, Is.GreaterThanOrEqualTo(1024));
            GC.KeepAlive(_allocationSink);
            _allocationSink = null;
        }
    }
}
