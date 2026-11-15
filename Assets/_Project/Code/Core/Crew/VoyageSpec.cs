using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Everything the voyage calculator needs to know about one hull</b> (crew slice R2), as a plain
    /// immutable object: no asset references, so a test builds one in a line. Made by
    /// <see cref="BoatVoyageDef.ToSpec"/> with the hull's crew slots and its bunk count
    /// (<see cref="BunkCount.Of"/>).
    /// </summary>
    public sealed class VoyageSpec
    {
        public readonly string HullId;
        /// <summary><c>BoatHullDef.CrewSlots</c>: the most aboard, day or night. Never below 0.</summary>
        public readonly int CrewSlots;
        /// <summary>Separate sleep places (<see cref="BunkCount.Of"/>). Never below 0.</summary>
        public readonly int Bunks;
        public readonly TripKind LongestTrip;

        private readonly int[] _minCrew;
        private readonly VoyageWorkRequirement[] _work;

        public VoyageSpec(string hullId, int crewSlots, int bunks, TripKind longestTrip,
            int[] minCrewByTrip, VoyageWorkRequirement[] work)
        {
            HullId = hullId ?? "";
            CrewSlots = Math.Max(0, crewSlots);
            Bunks = Math.Max(0, bunks);
            LongestTrip = longestTrip;
            _minCrew = new int[4];
            for (int i = 0; i < _minCrew.Length; i++)
                _minCrew[i] = Math.Max(1, minCrewByTrip != null && i < minCrewByTrip.Length ? minCrewByTrip[i] : 1);
            _work = work != null ? (VoyageWorkRequirement[])work.Clone() : Array.Empty<VoyageWorkRequirement>();
        }

        /// <summary>The fewest aboard for this trip kind (never below 1).</summary>
        public int MinCrew(TripKind trip)
        {
            int i = (int)trip;
            return i >= 0 && i < _minCrew.Length ? _minCrew[i] : _minCrew[_minCrew.Length - 1];
        }

        public IReadOnlyList<VoyageWorkRequirement> Work => _work;

        public bool TryGetWork(string workId, out VoyageWorkRequirement requirement)
        {
            requirement = null;
            if (string.IsNullOrEmpty(workId)) return false;
            foreach (var w in _work)
                if (w != null && string.Equals(w.WorkId, workId, StringComparison.Ordinal))
                {
                    requirement = w;
                    return true;
                }
            return false;
        }

        /// <summary>The most aboard by day: the crew slots.</summary>
        public int DayMax => CrewSlots;

        /// <summary>The most aboard overnight under <paramref name="policy"/>: a bunk each, never more
        /// than the crew slots. With hand bunk-sharing ON, hands may double up in the bunks left after
        /// the players (who never share), so the most is every bunk shared.</summary>
        public int OvernightMax(in CrewPolicy policy)
        {
            int sleepers = policy.HiredHandsShareBunks ? Bunks * Math.Max(1, policy.HandsPerSharedBunk) : Bunks;
            return Math.Min(CrewSlots, sleepers);
        }

        /// <summary>The longest trip she can make now: <see cref="LongestTrip"/>, or a day trip when she
        /// has no bunk.</summary>
        public TripKind LongestTripNow => Bunks > 0 ? LongestTrip : TripKind.Day;
    }

    /// <summary>
    /// <b>Where the voyage records are looked up by hull id</b> (crew slice R2). The Boats module
    /// implements it (it can join a record to the hull's crew slots and bunks); Core reads it through
    /// <see cref="GameServices.Voyages"/>.
    /// FLAG lead-architect: new Core contract (crew).
    /// </summary>
    public interface IVoyageCatalog
    {
        bool TryGet(string hullId, out VoyageSpec spec);
    }

    /// <summary>No records: every hull is unknown. What <see cref="GameServices.Voyages"/> reads until a
    /// catalog is registered.</summary>
    public sealed class EmptyVoyageCatalog : IVoyageCatalog
    {
        public static readonly EmptyVoyageCatalog Instance = new EmptyVoyageCatalog();

        public bool TryGet(string hullId, out VoyageSpec spec)
        {
            spec = null;
            return false;
        }
    }

    /// <summary>A catalog over specs already built, keyed by hull id. The Boats catalog fills one; a
    /// test builds one directly.</summary>
    public sealed class VoyageSpecCatalog : IVoyageCatalog
    {
        private readonly Dictionary<string, VoyageSpec> _specs = new Dictionary<string, VoyageSpec>(StringComparer.Ordinal);

        public VoyageSpecCatalog(IEnumerable<VoyageSpec> specs)
        {
            if (specs == null) return;
            foreach (var s in specs)
                if (s != null && !string.IsNullOrEmpty(s.HullId) && !_specs.ContainsKey(s.HullId))
                    _specs.Add(s.HullId, s);
        }

        public int Count => _specs.Count;

        public bool TryGet(string hullId, out VoyageSpec spec)
        {
            spec = null;
            return !string.IsNullOrEmpty(hullId) && _specs.TryGetValue(hullId, out spec);
        }
    }

    /// <summary>
    /// <b>How many separate places to sleep a hull has</b> — the ONE function every crew number reads
    /// bunks through (crew slice R2). A bunk is a <c>sleep</c> anchor on the hull's interior, counted
    /// exactly the way <c>BoatInteriorInstaller</c> builds one <c>BoatBunk</c> per anchor; an interior
    /// with no usable level (the Go-Fast's <c>Levels: []</c>) builds none, so it counts none.
    ///
    /// <para>When the second berths land (PR 1228 adds <c>sleep</c> anchors), every limit moves with
    /// them and no number here or in a voyage record changes.</para>
    /// </summary>
    public static class BunkCount
    {
        public static int Of(BoatInteriorDef interior)
        {
            if (interior == null || !interior.HasInterior() || interior.Anchors == null) return 0;
            int n = 0;
            foreach (var a in interior.Anchors)
                if (a != null && string.Equals(a.Action, BoatInteriorAnchor.ActionSleep, StringComparison.Ordinal))
                    n++;
            return n;
        }
    }
}
