using System;
using NUnit.Framework;
#if UNITY_5_3_OR_NEWER
using Unity.Profiling;
#endif

namespace HiddenHarbours.Tests.Art.EditMode
{
    // GC.GetAllocatedBytesForCurrentThread is a zero-returning stub in the tested Unity editor.
    // Raw GC.Alloc samples count events (their Value is nanoseconds, NOT allocated bytes).
    // No SumAllSamplesInFrame: we need an immediate, scoped current-thread observation.
    internal sealed class FoamTransportAllocationProbe : IDisposable
    {
#if UNITY_5_3_OR_NEWER
        private ProfilerRecorder _recorder=new ProfilerRecorder(ProfilerCategory.Memory,"GC.Alloc",8192,
            ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
        internal FoamTransportAllocationProbe()
        {
            Assert.AreEqual(true,_recorder.Valid,"GC.Alloc must be available; an unavailable counter is not zero allocation.");
            Begin(); End(); // Warm the observation path before any tested scope.
        }
        internal void Begin() { _recorder.Reset(); _recorder.Start(); }
        internal long End() { _recorder.Stop(); return _recorder.Count; }
        public void Dispose() => _recorder.Dispose();
#else
        // Only the independent headless harness uses the host runtime's byte counter.
        private long _before;
        internal void Begin() => _before=GC.GetAllocatedBytesForCurrentThread();
        internal long End() => GC.GetAllocatedBytesForCurrentThread()-_before;
        public void Dispose() { }
#endif
        internal void RequirePositiveControl()
        {
            Begin(); var knownAllocation=new byte[256]; long observed=End();
            GC.KeepAlive(knownAllocation);
            Assert.Greater(observed,0,"A known allocation must produce GC.Alloc events in the same recorder used for the zero proof.");
            Begin(); long empty=End();
            Assert.AreEqual(0,empty,"The recorder's own empty scope must allocate nothing.");
        }
    }
}
