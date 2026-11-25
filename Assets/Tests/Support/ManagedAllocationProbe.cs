using System;
using Unity.Profiling;

namespace HiddenHarbours.Tests.Support
{
    /// <summary>
    /// Counts current-thread GC.Alloc events, not bytes or sample durations. Every instance
    /// must observe a retained allocation and an empty scope before it can supply zero proof.
    /// A saturated recorder still reports a nonzero count, so cannot hide an allocation.
    /// </summary>
    public sealed class ManagedAllocationProbe : IDisposable
    {
        private ProfilerRecorder _recorder = new ProfilerRecorder(ProfilerCategory.Memory, "GC.Alloc", 8192,
            ProfilerRecorderOptions.CollectOnlyOnCurrentThread);

        public ManagedAllocationProbe()
        {
            try
            {
                if (!_recorder.Valid)
                    throw new InvalidOperationException("GC.Alloc is unavailable; no managed allocation proof.");
                Begin(); End(); // Warm observation before calibrating it.
                Begin();
                var known = new byte[1024];
                long observed = End();
                GC.KeepAlive(known);
                if (observed <= 0)
                    throw new InvalidOperationException("GC.Alloc missed a known allocation; no managed allocation proof.");
                Begin();
                long empty = End();
                if (empty != 0)
                    throw new InvalidOperationException("The allocation probe's empty scope must allocate nothing.");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Begin() { _recorder.Reset(); _recorder.Start(); }
        public long End() { _recorder.Stop(); return _recorder.Count; }
        public void Dispose() => _recorder.Dispose();

        /// <summary>Byte-only reports must refuse a stub, rather than label its zero as bytes.</summary>
        public static void RequireWorkingThreadByteCounter()
        {
            GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            var known = new byte[1024];
            long observed = GC.GetAllocatedBytesForCurrentThread() - before;
            GC.KeepAlive(known);
            if (observed <= 0)
                throw new InvalidOperationException("The thread byte counter missed a known allocation; no byte report.");
        }
    }
}
