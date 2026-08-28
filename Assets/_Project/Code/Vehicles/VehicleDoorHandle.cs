using System;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Vehicles
{
    /// <summary>
    /// <b>One handle on a machine</b> — the thing the player presses to work a door, a hood, a
    /// tilting cab or a trailer's landing gear.
    ///
    /// <para><b>One per HANDLE the art drew, not one per moving part.</b> A reefer publishes a single
    /// <c>doors</c> crank and has two leaves; a trailer publishes one <c>gear</c> crank and has shoes
    /// and legs. The group decides what moves (<see cref="VehicleDoorGroup.Slots"/>), so the player
    /// reaches for the handle in the picture and the fittings follow.</para>
    ///
    /// <para><b>A registration, not a key</b> — the same pressure valve <see cref="VehicleDoor"/>
    /// takes, and for the same reason: the dev-key ledger is spent, so anything that wants the
    /// interact press registers for it and is resolved on distance, priority and facing.</para>
    ///
    /// <para><b>Cross-module through Core only</b> (rule 4): this names <see cref="VehicleDoors"/>,
    /// which is its own module's, and Core's interaction seam. Nothing else.</para>
    ///
    /// <para>⚠️ <b>The reach point is the ART's request and it is not validated.</b> Every sidecar in
    /// the pack says so in its own <c>_interact_notes</c> — "a request, not a promise", and several
    /// add "NOT tested against ground colliders". This component honours it as published and does not
    /// move anybody: whether the player can stand there is the world's business, and a handle that
    /// teleported them would be inventing an answer the art declined to give.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleDoorHandle : MonoBehaviour, IInteractable
    {
        [Tooltip("How close (m) the player must stand to work this handle. The same forgiving reach " +
                 "the driver's door takes — tight enough that a curb-side handle cannot be worked " +
                 "from the street side.")]
        [SerializeField, Min(0f)] private float _reachMeters = 1.5f;

        private VehicleDoors _doors;
        private string _vehicleId = "";
        private string _groupId = "";
        private int _firstSlot = -1;
        private string _openLabel = "Open";
        private string _shutLabel = "Close";
        private Vector2 _reachLocal;
        private bool _hasReach;

        /// <summary>Wire this handle to one group. Called by the skinner, which is the only thing
        /// that knows both the def and the live root.</summary>
        public void Configure(VehicleDoors doors, in VehicleDoorGroup group, string vehicleId)
        {
            _doors = doors;
            _groupId = group.Id ?? "";
            _reachLocal = group.ReachPointLocal;
            _hasReach = group.HasReachPoint;
            _vehicleId = vehicleId ?? "";
            _firstSlot = group.Slots != null && group.Slots.Length > 0
                ? doors.IndexOfSlot(group.Slots[0])
                : -1;
            LabelsFor(_groupId, group.Work, out _openLabel, out _shutLabel);
        }

        /// <summary>
        /// What the handle offers, in words. The ids are the art's own and they do not rhyme across
        /// the pack — the van calls her rear pair <c>barn</c> and the trailer kit calls its own
        /// <c>doors</c> — so each is named here rather than derived from the string.
        ///
        /// <para>⚠️ An unlabelled id falls back to plain "Open"/"Close" rather than throwing. A new
        /// handle arriving from an art drop should be VISIBLE and slightly bare, not fatal: the bake
        /// already refuses a group the art does not publish, so what reaches here is real.</para>
        /// </summary>
        private static void LabelsFor(string groupId, VehicleDoorWork work,
                                      out string open, out string shut)
        {
            switch (groupId)
            {
                case "slide": open = "Slide the door open"; shut = "Slide the door shut"; break;
                case "barn":
                case "doors": open = "Open the rear doors"; shut = "Close the rear doors"; break;
                case "hood": open = "Open the hood"; shut = "Close the hood"; break;
                case "tilt": open = "Tilt the cab forward"; shut = "Lower the cab"; break;
                case "rollup": open = "Roll the shutter up"; shut = "Roll the shutter down"; break;
                case "gate": open = "Lower the liftgate"; shut = "Stow the liftgate"; break;
                case "gear":
                    open = "Wind the legs up"; shut = "Wind the legs down"; break;
                default:
                    open = work == VehicleDoorWork.LandingGear ? "Wind up" : "Open";
                    shut = work == VehicleDoorWork.LandingGear ? "Wind down" : "Close";
                    break;
            }
        }

        /// <summary>
        /// ⚠️ <b>Computed live, never latched</b> — the #556 trap. <c>AddComponent</c> on a live
        /// GameObject runs <c>OnEnable</c> (and so the registration) BEFORE the caller has said which
        /// vehicle and which handle this is, so an id cached on first read would name every handle in
        /// the game "unassigned" for the rest of its life. <see cref="VehicleDoor"/> carries the same
        /// guard for the same reason.
        ///
        /// <para>The per-instance suffix is load-bearing too: ids must be unique among live
        /// registrants, and a laydown with four trailers on it has four <c>gear</c> cranks.</para>
        /// </summary>
        public string Id =>
            string.IsNullOrEmpty(_vehicleId) || string.IsNullOrEmpty(_groupId)
                ? $"vehicle.unassigned.handle#{GetEntityId()}"
                : $"{_vehicleId}.{_groupId}#{GetEntityId()}";

        /// <summary>Reads live, so the offer names what pressing would actually do — a half-open
        /// door offers to finish opening, because that is where its target already points.</summary>
        public string VerbLabel =>
            _doors != null && _doors.Openness(_firstSlot) > 0.5f ? _shutLabel : _openLabel;

        /// <summary>The art's point, through the live root — so it travels with the machine and
        /// swings as she turns, rather than being sampled once at install.</summary>
        public Vector2 WorldPosition
        {
            get
            {
                Vector3 world = transform.TransformPoint(new Vector3(_reachLocal.x, _reachLocal.y, 0f));
                return new Vector2(world.x, world.y);
            }
        }

        public float ReachMeters => _reachMeters;

        public InteractContext Contexts => InteractContext.OnFoot;

        /// <summary>A fixture: a handle is bolted to the machine and you walk up to it. Deliberately
        /// the DEFAULT rung, so a pail at your feet still outranks a hood catch you are standing
        /// near — the thing in your hands or at your feet is the more specific answer to "act".</summary>
        public int Priority => InteractPriority.Fixture;

        /// <summary>False: a handle is a small thing on a big machine and hunting for the facing
        /// band would be fussy (P5). Distance already separates the two sides of a cab.</summary>
        public bool RequiresFacing => false;

        /// <summary>
        /// Offered only when there is somewhere to stand and nothing already moving.
        ///
        /// <para>⚠️ <b>No reach point means no handle</b>, and that is a real case rather than a
        /// defensive check: the trailer kit publishes its <c>couple</c> point as PROSE, because the
        /// act belongs to the tractor. A handle at (0, 0) would sit inside the machine's own
        /// centreline and be pressable through her.</para>
        ///
        /// <para>Refusing while something is travelling is what stops a player scrubbing a door back
        /// and forth mid-sweep — and, on a trailer, what makes the crank's duration mean something.</para>
        /// </summary>
        public bool IsAvailable => _hasReach && _doors != null && !_doors.IsMoving;

        public void Interact(in InteractActor actor)
        {
            if (_doors == null) return;
            _doors.ToggleGroup(_groupId);
        }

        private void OnEnable() => Interactables.Register(this);
        private void OnDisable() => Interactables.Unregister(this);
    }
}
