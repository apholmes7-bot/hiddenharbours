using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The helm as a chooser row set</b>: the station's own action ("Take the helm" or "Leave the
    /// helm") first, then every <see cref="IHelmFixture"/> at this boat's wheel that is in reach (owner
    /// ask 2026-11-25: the cabin-light switch at the helm must be reachable without leaving the wheel).
    ///
    /// <para><b>Built only when there is a fixture.</b> <see cref="Gather"/> returns how many fixtures it
    /// found; with none the switcher never opens a list and its helm press is exactly what it was.</para>
    ///
    /// <para><b>Stable rows.</b> Row 0 is always the station; the fixtures follow in ordinal order of
    /// their ids, so a gamepad's down-press lands on the same row whatever order things registered in
    /// (rule 5).</para>
    ///
    /// <para><b>Never registered.</b> The switcher opens it with
    /// <see cref="InteractChooser.OpenUnregistered"/>; a confirm re-checks it live, and a fixture row
    /// re-checks the fixture itself before pressing it.</para>
    ///
    /// <para>Allocation-free after construction: a fixed buffer, an insertion sort, no LINQ.</para>
    /// </summary>
    public sealed class HelmStationChoices : IInteractable, IMultiActionInteractable
    {
        /// <summary>The id the chooser and <see cref="InteractPerformed"/> see for the station row.</summary>
        public const string StationId = "transit.helm_station";

        /// <summary>At most this many fixtures share the helm list with the station row.</summary>
        public const int MaxFixtures = InteractChoiceList.HardCap - 1;

        private readonly IInteractable[] _fixtures = new IInteractable[MaxFixtures];
        private int _fixtureCount;
        private string _stationLabel;
        private Action _station;
        private Vector2 _at;
        private float _reach;
        private float _cosHalfArc;

        /// <summary>How many fixtures were gathered.</summary>
        public int FixtureCount => _fixtureCount;

        /// <summary>
        /// Find this boat's helm fixtures in reach of <paramref name="actor"/>, and remember the station's
        /// own action. Returns the number of fixtures found (0: do not open a list).
        /// </summary>
        /// <param name="candidates">The registrants (<see cref="Interactables.Active"/>).</param>
        /// <param name="boat">The boat he is aboard; a fixture at another boat's helm is not his.</param>
        /// <param name="stationLabel">Row 0's words.</param>
        /// <param name="station">Row 0's action (take or leave the helm).</param>
        /// <param name="stationReach">How far from where he stands the list stays workable (the helm radius).</param>
        public int Gather(IReadOnlyList<IInteractable> candidates, Transform boat, in InteractActor actor,
                          float facingArcDegrees, string stationLabel, Action station, float stationReach)
        {
            Clear();
            if (candidates == null || boat == null || station == null) return 0;

            _stationLabel = stationLabel;
            _station = station;
            _at = actor.Position;
            _reach = Mathf.Max(0f, stationReach);
            _cosHalfArc = InteractArc.CosHalfArc(facingArcDegrees);

            for (int i = 0; i < candidates.Count; i++)
            {
                IInteractable c = candidates[i];
                if (!(c is IHelmFixture hf)) continue;
                if (!IsThisBoat(hf.HelmBoat, boat)) continue;
                if (!InteractResolver.Qualifies(c, actor, actor.Facing, _cosHalfArc, out _)) continue;
                InsertById(c);
            }
            return _fixtureCount;
        }

        /// <summary>Forget the gathered rows.</summary>
        public void Clear()
        {
            for (int i = 0; i < _fixtureCount; i++) _fixtures[i] = null;
            _fixtureCount = 0;
            _station = null;
            _stationLabel = null;
        }

        // ---- IInteractable: the station row's own gates ---------------------------------------------

        public string Id => StationId;
        public string VerbLabel => _stationLabel;
        public Vector2 WorldPosition => _at;
        public float ReachMeters => _reach;
        public int Priority => InteractPriority.Fixture;
        public InteractContext Contexts => InteractContext.OnDeck | InteractContext.AtHelm;
        public bool RequiresFacing => false;
        public bool IsAvailable => _station != null;

        public void Interact(in InteractActor actor) => Interact(actor, 0);

        // ---- IMultiActionInteractable --------------------------------------------------------------

        public int ActionCount => _station != null ? 1 + _fixtureCount : 0;

        public string ActionLabel(int index)
        {
            if (index == 0) return _stationLabel;
            IInteractable f = FixtureAt(index);
            return f?.VerbLabel;
        }

        public bool ActionAvailable(int index)
        {
            if (index == 0) return _station != null;
            IInteractable f = FixtureAt(index);
            return f != null && f.IsAvailable;
        }

        public void Interact(in InteractActor actor, int index)
        {
            if (index == 0) { _station?.Invoke(); return; }
            IInteractable f = FixtureAt(index);
            // Re-checked live: the list was frozen when it opened, the fixture was not.
            if (f == null || !InteractResolver.Qualifies(f, actor, actor.Facing, _cosHalfArc, out _)) return;
            InteractVerb.Perform(f, -1, actor);
        }

        private IInteractable FixtureAt(int index)
        {
            int i = index - 1;
            return i >= 0 && i < _fixtureCount ? _fixtures[i] : null;
        }

        private void InsertById(IInteractable c)
        {
            // More fixtures than rows: keep the first MaxFixtures by id, whatever order they came in.
            if (_fixtureCount >= MaxFixtures)
            {
                if (string.CompareOrdinal(_fixtures[_fixtureCount - 1].Id, c.Id) <= 0) return;
                _fixtures[--_fixtureCount] = null;
            }
            int at = _fixtureCount;
            while (at > 0 && string.CompareOrdinal(_fixtures[at - 1].Id, c.Id) > 0)
            {
                _fixtures[at] = _fixtures[at - 1];
                at--;
            }
            _fixtures[at] = c;
            _fixtureCount++;
        }

        /// <summary>A fixture's boat matches when it is the boat, or either sits under the other (the
        /// board may live on the hull root or on a child of it).</summary>
        private static bool IsThisBoat(GameObject fixtureBoat, Transform boat)
        {
            if (fixtureBoat == null) return false;
            Transform t = fixtureBoat.transform;
            return t == boat || t.IsChildOf(boat) || boat.IsChildOf(t);
        }
    }
}
