using System;

namespace HiddenHarbours.Art
{
    // CPU ownership only. Recording a callback is not proof that the GPU ran it.
    internal sealed class FoamHistoryInitializationState
    {
        internal enum Stage { Required, Recording, WaitingForFence, Ready, Faulted }
        internal Stage Status { get; private set; } = Stage.Required;
        internal int Generation { get; private set; } = 1;
        internal int Attempt { get; private set; }
        internal bool Ready => Status == Stage.Ready;
        internal bool RequiresRecording => Status == Stage.Required || Status == Stage.Recording;

        internal readonly struct Ticket
        {
            internal readonly int Generation, Attempt;
            internal Ticket(int generation, int attempt) { Generation = generation; Attempt = attempt; }
        }

        internal Ticket Begin()
        {
            if (!RequiresRecording) throw new InvalidOperationException("History initialization is already pending or complete.");
            Status = Stage.Recording;
            return new Ticket(Generation, ++Attempt);
        }

        internal bool IsCurrent(Ticket ticket) => ticket.Generation == Generation && ticket.Attempt == Attempt;

        internal void FenceRecorded(Ticket ticket)
        {
            if (!IsCurrent(ticket) || Status != Stage.Recording)
                throw new InvalidOperationException("Stale or duplicate foam initialization callback.");
            Status = Stage.WaitingForFence;
        }

        internal bool Confirm(Ticket ticket, bool fencePassed)
        {
            if (!IsCurrent(ticket) || Status != Stage.WaitingForFence || !fencePassed) return false;
            Status = Stage.Ready;
            return true;
        }

        internal void Fault() { Status = Stage.Faulted; }

        internal void ResetAllocation()
        {
            ++Generation;
            Attempt = 0;
            Status = Stage.Required;
        }
    }
}
