using System;
using NUnit.Framework;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public class FoamHistoryInitializationStateTests
    {
        [Test] public void RecordingAndCallback_DoNotMarkHistoryReady()
        {
            var state = new FoamHistoryInitializationState();
            var ticket = state.Begin();
            Assert.IsFalse(state.Ready);
            Assert.IsFalse(state.Confirm(ticket, true), "A callback that never recorded a fence cannot complete.");
            state.FenceRecorded(ticket);
            Assert.IsFalse(state.Ready);
            Assert.IsFalse(state.Confirm(ticket, false));
            Assert.IsFalse(state.Ready);
            Assert.IsTrue(state.Confirm(ticket, true));
            Assert.IsTrue(state.Ready);
        }

        [Test] public void AbandonedRecording_Retries_AndRejectsOldCallback()
        {
            var state = new FoamHistoryInitializationState();
            var abandoned = state.Begin();
            var retry = state.Begin();
            Assert.Throws<InvalidOperationException>(() => state.FenceRecorded(abandoned));
            state.FenceRecorded(retry);
            Assert.IsFalse(state.Confirm(abandoned, true));
            Assert.IsTrue(state.Confirm(retry, true));
        }

        [Test] public void UnsubmittedFence_CannotReclearOrBecomeReady()
        {
            var state = new FoamHistoryInitializationState();
            var ticket = state.Begin(); state.FenceRecorded(ticket);
            for (int render = 0; render < 1000; render++)
            {
                Assert.IsFalse(state.Confirm(ticket, false));
                Assert.IsFalse(state.RequiresRecording);
                Assert.Throws<InvalidOperationException>(() => state.Begin());
            }
            Assert.IsFalse(state.Ready);
        }

        [Test] public void CompletedInitialization_DoesNotRecurOnRepeatedRenders()
        {
            var state = new FoamHistoryInitializationState();
            var ticket = state.Begin(); state.FenceRecorded(ticket); state.Confirm(ticket, true);
            for (int render = 0; render < 1000; render++)
            {
                Assert.IsTrue(state.Ready);
                Assert.IsFalse(state.RequiresRecording);
            }
            Assert.Throws<InvalidOperationException>(() => state.Begin());
        }

        [Test] public void Reallocation_RequiresNewInitialization_RejectsPriorFence()
        {
            var state = new FoamHistoryInitializationState();
            var old = state.Begin(); state.FenceRecorded(old); state.Confirm(old, true);
            state.ResetAllocation();
            Assert.IsFalse(state.Ready); Assert.IsTrue(state.RequiresRecording);
            Assert.IsFalse(state.Confirm(old, true));
            var current = state.Begin(); state.FenceRecorded(current);
            Assert.IsFalse(state.Confirm(old, true));
            Assert.IsTrue(state.Confirm(current, true));
        }

        [Test] public void ReallocationWhileFencePending_RejectsLateCompletion()
        {
            var state = new FoamHistoryInitializationState();
            var old = state.Begin(); state.FenceRecorded(old); state.ResetAllocation();
            Assert.IsFalse(state.Confirm(old, true));
            Assert.IsTrue(state.RequiresRecording); Assert.IsFalse(state.Ready);
        }

        [Test] public void CameraLifetimes_AreIndependent()
        {
            var a = new FoamHistoryInitializationState(); var b = new FoamHistoryInitializationState();
            var ta = a.Begin(); a.FenceRecorded(ta); a.Confirm(ta, true);
            var tb = b.Begin(); b.FenceRecorded(tb);
            Assert.IsTrue(a.Ready); Assert.IsFalse(b.Ready);
            b.ResetAllocation(); Assert.IsTrue(a.Ready); Assert.IsFalse(b.Ready);
            a.ResetAllocation(); Assert.IsFalse(a.Ready);
        }

        [Test] public void FailedFence_FailsClosedUntilReallocation()
        {
            var state = new FoamHistoryInitializationState();
            var ticket = state.Begin(); state.FenceRecorded(ticket); state.Fault();
            Assert.IsFalse(state.Confirm(ticket, true)); Assert.IsFalse(state.Ready);
            Assert.IsFalse(state.RequiresRecording);
            Assert.Throws<InvalidOperationException>(() => state.Begin());
            state.ResetAllocation(); Assert.IsTrue(state.RequiresRecording);
        }
    }
}
