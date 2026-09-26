using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// One allocation of one camera's two persistent histories. Both imports always retain their
    /// contents. A real write pass initializes both before the first advect/deposit pass reads them.
    /// The CPU only retires initialization after its in-command-stream fence has passed.
    /// </summary>
    internal sealed class FoamHistoryInitialization
    {
        internal readonly FoamHistoryInitializationState State = new FoamHistoryInitializationState();
        GraphicsFence _fence;
        FoamHistoryInitializationState.Ticket _ticket;
        bool _reportedPending, _reportedFault;

        internal bool Ready => State.Ready;
        internal bool RequiresRecording => State.RequiresRecording;

        internal void ResetAllocation()
        {
            State.ResetAllocation();
            _fence = default;
            _reportedPending = _reportedFault = false;
        }

        // Called before advancing the camera's origin, clock or ping-pong selection.
        // No busy wait, fence wait command, readback, or cache mutation.
        internal bool CanRecord()
        {
            if (!SystemInfo.supportsGraphicsFence)
            {
                State.Fault();
                if (!_reportedFault)
                    Debug.LogError("Foam history disabled: this device cannot confirm initialization with a graphics fence.");
                _reportedFault = true;
                return false;
            }
            if (State.Status == FoamHistoryInitializationState.Stage.WaitingForFence)
            {
                try { State.Confirm(_ticket, _fence.passed); }
                catch (Exception error)
                {
                    State.Fault();
                    Debug.LogError("Foam history initialization fence failed; history stays disabled until reallocation. " + error.Message);
                    _reportedFault = true;
                }
                if (State.Status == FoamHistoryInitializationState.Stage.WaitingForFence && !_reportedPending)
                {
                    Debug.LogWarning("Foam history awaiting initialization completion; repeated renders defer foam without clearing or reading uncertain history.");
                    _reportedPending = true;
                }
            }
            return State.Ready || State.RequiresRecording;
        }

        sealed class PassData
        {
            internal TextureHandle A, B;
            internal FoamHistoryInitialization Owner;
            internal FoamHistoryInitializationState.Ticket Ticket;
        }

        internal void Record(RenderGraph graph, TextureHandle a, TextureHandle b)
        {
            if (!RequiresRecording) return;
            _ticket = State.Begin();
            using var builder = graph.AddUnsafePass<PassData>("HH Initialize Foam History", out var data);
            data.A = a;
            data.B = b;
            data.Owner = this;
            data.Ticket = _ticket;
            // This is functional initialization, not a boundary/profiling workaround. Both textures
            // are full writes, so the subsequent advect read/write must follow these commands.
            builder.UseTexture(a, AccessFlags.WriteAll);
            builder.UseTexture(b, AccessFlags.WriteAll);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc((PassData pass, UnsafeGraphContext context) =>
            {
                if (!pass.Owner.State.IsCurrent(pass.Ticket) ||
                    pass.Owner.State.Status != FoamHistoryInitializationState.Stage.Recording)
                    throw new InvalidOperationException("Foam allocation changed before initialization executed.");
                // The pinned Raster/Compute wrappers do not expose CreateGraphicsFence. An Unsafe
                // pass is required for these actual clears and fence; all resources are declared.
                var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                CoreUtils.SetRenderTarget(cmd, (RTHandle)pass.A, ClearFlag.Color, Color.clear);
                CoreUtils.SetRenderTarget(cmd, (RTHandle)pass.B, ClearFlag.Color, Color.clear);
                pass.Owner._fence = cmd.CreateGraphicsFence(GraphicsFenceType.CPUSynchronisation,
                    SynchronisationStageFlags.AllGPUOperations);
                pass.Owner.State.FenceRecorded(pass.Ticket);
            });
        }
    }
}
