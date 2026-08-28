using System;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Vehicles
{
    /// <summary>
    /// <b>How open every worked opening on one machine is</b> — the state the picture is posed from,
    /// and the thing a handle actually moves.
    ///
    /// <para><b>Openness is per FITTING, targets are per HANDLE.</b> The art publishes one
    /// <c>doors</c> crank and a reefer has two leaves; one <c>gear</c> crank and a trailer has shoes
    /// and legs. Pressing the handle sets a target on every slot in its
    /// <see cref="VehicleDoorGroup"/>, and each slot walks to it. Keeping the state per fitting is
    /// what lets a half-open pair be drawn honestly rather than averaged.</para>
    ///
    /// <para><b>It holds a number, not a picture.</b> Nothing here touches a renderer:
    /// <c>VehicleMeshDriver</c> reads these each LateUpdate and poses the leaves, exactly as it reads
    /// the controller for steer and odometer. That split is why an EditMode test can open a door and
    /// assert the pose without a camera.</para>
    ///
    /// <para>⚠️ <b>Deterministic and allocation-free</b> (rules 5 and 7): the two arrays are sized
    /// once at <see cref="Configure"/>, the walk is a clamped step by <c>dt</c>, and no frame
    /// allocates. It is a PRESENTATION state — the sim never reads it, so nothing here belongs in a
    /// save.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleDoors : MonoBehaviour
    {
        private VehicleMeshDef _def;
        private VehicleFitment[] _fitments = Array.Empty<VehicleFitment>();

        /// <summary>Where each fitting is, 0 shut to 1 fully open. Index-parallel to
        /// <see cref="_fitments"/> so the driver can read it without a lookup.</summary>
        private float[] _open = Array.Empty<float>();

        /// <summary>Where each fitting is going. Equal to <see cref="_open"/> at rest.</summary>
        private float[] _target = Array.Empty<float>();

        /// <summary>Seconds for a full sweep, per fitting — resolved from the group that works it,
        /// so a hand crank and a door leaf can be paced differently without the walk knowing which
        /// is which.</summary>
        private float[] _seconds = Array.Empty<float>();

        /// <summary>Wire this machine's openings up. Safe to call again on a re-skin: everything is
        /// rebuilt from the def, and a door that was open stays open only if it still exists.</summary>
        public void Configure(VehicleMeshDef def)
        {
            _def = def;
            _fitments = def != null && def.Wheels != null ? def.Wheels : Array.Empty<VehicleFitment>();

            if (_open.Length != _fitments.Length)
            {
                _open = new float[_fitments.Length];
                _target = new float[_fitments.Length];
                _seconds = new float[_fitments.Length];
            }

            // Every fitting gets a pace, whether or not a handle works it: the default keeps a leaf
            // that somehow gets a target from snapping, and a wheel's entry is simply never read.
            for (int i = 0; i < _fitments.Length; i++)
                _seconds[i] = GameServices.VehicleDoorSweepSeconds;

            if (def == null || def.DoorGroups == null) return;
            for (int g = 0; g < def.DoorGroups.Length; g++)
            {
                VehicleDoorGroup group = def.DoorGroups[g];
                if (group.Slots == null) continue;
                float seconds = group.Work == VehicleDoorWork.LandingGear
                    ? GameServices.VehicleGearCrankSeconds
                    : GameServices.VehicleDoorSweepSeconds;

                for (int sl = 0; sl < group.Slots.Length; sl++)
                {
                    int i = IndexOfSlot(group.Slots[sl]);
                    if (i >= 0) _seconds[i] = seconds;
                }
            }
        }

        /// <summary>The fitting index for a slot, or −1. ⚠️ Callers must treat −1 as "this machine
        /// has no such opening" and do nothing — never as index 0, which would work the wrong
        /// leaf.</summary>
        public int IndexOfSlot(string slot)
        {
            for (int i = 0; i < _fitments.Length; i++)
                if (string.Equals(_fitments[i].Slot, slot, StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>How open one fitting is, 0..1. Out-of-range reads 0 — a fitting nobody has is
        /// shut, which is what the body mesh already draws.</summary>
        public float Openness(int index) =>
            index >= 0 && index < _open.Length ? _open[index] : 0f;

        public float Openness(string slot) => Openness(IndexOfSlot(slot));

        /// <summary>Where one fitting is headed, 0..1.</summary>
        public float TargetOf(int index) =>
            index >= 0 && index < _target.Length ? _target[index] : 0f;

        /// <summary>True while anything is still travelling — what a handle asks before it lets the
        /// player press it again, and what a coupling refuses on.</summary>
        public bool IsMoving
        {
            get
            {
                for (int i = 0; i < _open.Length; i++)
                    if (!Mathf.Approximately(_open[i], _target[i])) return true;
                return false;
            }
        }

        /// <summary>Send every fitting a handle works toward <paramref name="target01"/>. Unknown id
        /// does nothing and says so — a handle that silently works nothing is the failure the bake's
        /// own group check exists to prevent, and this is the runtime half of it.</summary>
        public bool SetGroupTarget(string groupId, float target01)
        {
            if (_def == null || !_def.TryGetDoorGroup(groupId, out VehicleDoorGroup group)) return false;
            if (group.Slots == null) return false;

            float t = Mathf.Clamp01(target01);
            for (int i = 0; i < group.Slots.Length; i++)
            {
                int at = IndexOfSlot(group.Slots[i]);
                if (at >= 0) _target[at] = t;
            }
            return true;
        }

        /// <summary>Work a handle the other way. Reads the group's FIRST slot to decide which way
        /// that is, so a pair caught half-open moves together rather than scissoring.</summary>
        public bool ToggleGroup(string groupId)
        {
            if (_def == null || !_def.TryGetDoorGroup(groupId, out VehicleDoorGroup group)) return false;
            if (group.Slots == null || group.Slots.Length == 0) return false;

            int first = IndexOfSlot(group.Slots[0]);
            if (first < 0) return false;
            return SetGroupTarget(groupId, _target[first] > 0.5f ? 0f : 1f);
        }

        /// <summary>Shut everything at once, with no travel — what a freshly placed machine wants,
        /// and what a re-skin wants so a door does not sweep open from nowhere.</summary>
        public void SnapAllShut()
        {
            for (int i = 0; i < _open.Length; i++) { _open[i] = 0f; _target[i] = 0f; }
        }

        private void Update() => Advance(Time.deltaTime);

        /// <summary>One step of the walk — public so an EditMode test drives the production path at
        /// a chosen dt rather than waiting on a player loop that is not running.</summary>
        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;

            for (int i = 0; i < _open.Length; i++)
            {
                float target = _target[i];
                if (Mathf.Approximately(_open[i], target)) { _open[i] = target; continue; }

                // A full sweep takes _seconds[i] whatever the sweep IS — see
                // GameConfig.DefaultVehicleDoorSweepSeconds for why that is a feel choice rather
                // than a physical claim about a 42° flap and a 255° barn leaf.
                float step = deltaSeconds / Mathf.Max(0.05f, _seconds[i]);
                _open[i] = Mathf.MoveTowards(_open[i], target, step);
            }
        }
    }
}
