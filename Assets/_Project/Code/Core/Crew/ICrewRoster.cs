using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Who is aboard which boat, in which role</b> (crew slice R2) — players and hired hands alike.
    /// A boat is named by a caller-chosen key (an owned-boat id, a fleet slot) and is on the roster as
    /// one hull while anyone is aboard. Boarding enforces the DAY limit, <c>BoatHullDef.CrewSlots</c>;
    /// the night rules (bunks) and everything else are asked with <see cref="CanSail"/>, because a crew
    /// that may stand on a deck at the wharf may still not sleep aboard.
    ///
    /// <para><b>In memory only.</b> Not saved in this slice (a later save bump owns that). Never null on
    /// <see cref="GameServices.Crew"/>.</para>
    /// FLAG lead-architect: new Core contract (crew).
    /// </summary>
    public interface ICrewRoster
    {
        /// <summary>Put <paramref name="member"/> aboard <paramref name="boatKey"/> (a <paramref name="hullId"/>
        /// hull). Refused, with the reason, when the hull has no voyage record, the role is unknown, the
        /// person is already aboard a boat, the boat is on the roster as another hull, or she is full.</summary>
        VoyageVerdict TryBoard(string boatKey, string hullId, CrewMember member);

        /// <summary>Take this person off whatever boat they are on. False when they were on none.</summary>
        bool Leave(string memberId);

        /// <summary>Everyone aboard this boat, in boarding order. Empty for an unknown boat.</summary>
        IReadOnlyList<CrewMember> Aboard(string boatKey);

        /// <summary>The hull id this boat is on the roster as, or null when nobody is aboard.</summary>
        string HullOf(string boatKey);

        /// <summary>The boat this person is aboard, or false.</summary>
        bool TryGetBoatOf(string memberId, out string boatKey);

        /// <summary>May the crew aboard now take her on this trip? (<see cref="VoyageLimits"/>.)</summary>
        VoyageVerdict CanSail(string boatKey, in VoyagePlan plan);

        /// <summary>Everyone ashore, every boat off the roster.</summary>
        void Clear();
    }

    /// <summary>
    /// The in-memory <see cref="ICrewRoster"/>. Reads the voyage records, the roles and the policy through
    /// functions, so the live <see cref="GameServices"/> values are used at each call and a test passes its
    /// own.
    /// </summary>
    public sealed class CrewRoster : ICrewRoster
    {
        private sealed class Boat
        {
            public string HullId;
            public readonly List<CrewMember> Members = new List<CrewMember>();
        }

        private readonly Func<IVoyageCatalog> _catalog;
        private readonly Func<StaffRoleBook> _roles;
        private readonly Func<CrewPolicy> _policy;
        private readonly Dictionary<string, Boat> _boats = new Dictionary<string, Boat>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _boatOf = new Dictionary<string, string>(StringComparer.Ordinal);
        private StaffRoleBook _shippedRoles;

        /// <summary>The live roster: <see cref="GameServices.Voyages"/>, the shipped roles under
        /// <c>Resources/Staff</c> (loaded on first use) and <see cref="GameServices.CrewPolicy"/>.</summary>
        public CrewRoster() : this(null, null, null) { }

        public CrewRoster(Func<IVoyageCatalog> catalog, Func<StaffRoleBook> roles, Func<CrewPolicy> policy)
        {
            _catalog = catalog ?? (() => GameServices.Voyages);
            _roles = roles ?? (() => _shippedRoles ??= StaffRoleBook.FromResources());
            _policy = policy ?? (() => GameServices.CrewPolicy);
        }

        public VoyageVerdict TryBoard(string boatKey, string hullId, CrewMember member)
        {
            if (string.IsNullOrWhiteSpace(boatKey)) return VoyageVerdict.No(VoyageRefusal.InvalidBoat, "", 0, 0);
            IVoyageCatalog catalog = _catalog() ?? EmptyVoyageCatalog.Instance;
            if (!catalog.TryGet(hullId, out var spec) || spec == null)
                return VoyageVerdict.No(VoyageRefusal.UnknownHull, hullId ?? "", 0, 0);
            CrewPolicy policy = _policy();
            // The hull's overnight ceiling (an all-hired crew); CanSail reports the figure for this crew's players.
            int dayMax = spec.DayMax, nightMax = spec.OvernightMax(policy);
            VoyageVerdict No(VoyageRefusal r, string detail) => VoyageVerdict.No(r, detail, dayMax, nightMax);

            if (string.IsNullOrWhiteSpace(member.MemberId)) return No(VoyageRefusal.InvalidMember, "");
            StaffRoleBook roles = _roles() ?? StaffRoleBook.Empty;
            if (!roles.Contains(member.RoleId)) return No(VoyageRefusal.UnknownRole, member.RoleId ?? "");
            if (_boatOf.TryGetValue(member.MemberId, out string current))
                return No(VoyageRefusal.AlreadyAboard, current);

            _boats.TryGetValue(boatKey, out var boat);
            if (boat != null && !string.Equals(boat.HullId, spec.HullId, StringComparison.Ordinal))
                return No(VoyageRefusal.BoatHullMismatch, boat.HullId);
            int aboard = boat != null ? boat.Members.Count : 0;
            if (aboard + 1 > dayMax) return No(VoyageRefusal.OverCrewSlots, "");

            if (boat == null)
            {
                boat = new Boat { HullId = spec.HullId };
                _boats.Add(boatKey, boat);
            }
            boat.Members.Add(member);
            _boatOf.Add(member.MemberId, boatKey);
            return VoyageVerdict.Yes(dayMax, nightMax);
        }

        public bool Leave(string memberId)
        {
            if (string.IsNullOrEmpty(memberId) || !_boatOf.TryGetValue(memberId, out string key)) return false;
            _boatOf.Remove(memberId);
            if (_boats.TryGetValue(key, out var boat))
            {
                boat.Members.RemoveAll(m => string.Equals(m.MemberId, memberId, StringComparison.Ordinal));
                if (boat.Members.Count == 0) _boats.Remove(key);
            }
            return true;
        }

        public IReadOnlyList<CrewMember> Aboard(string boatKey)
            => !string.IsNullOrEmpty(boatKey) && _boats.TryGetValue(boatKey, out var boat)
                ? boat.Members.ToArray() : Array.Empty<CrewMember>();

        public string HullOf(string boatKey)
            => !string.IsNullOrEmpty(boatKey) && _boats.TryGetValue(boatKey, out var boat) ? boat.HullId : null;

        public bool TryGetBoatOf(string memberId, out string boatKey)
        {
            boatKey = null;
            return !string.IsNullOrEmpty(memberId) && _boatOf.TryGetValue(memberId, out boatKey);
        }

        public VoyageVerdict CanSail(string boatKey, in VoyagePlan plan)
        {
            if (string.IsNullOrWhiteSpace(boatKey) || !_boats.TryGetValue(boatKey, out var boat))
                return VoyageVerdict.No(VoyageRefusal.NoCrew, "", 0, 0);
            IVoyageCatalog catalog = _catalog() ?? EmptyVoyageCatalog.Instance;
            catalog.TryGet(boat.HullId, out var spec);
            CrewPolicy policy = _policy();
            return VoyageLimits.Check(spec, boat.Members, plan, _roles() ?? StaffRoleBook.Empty, policy);
        }

        public void Clear()
        {
            _boats.Clear();
            _boatOf.Clear();
        }
    }
}
