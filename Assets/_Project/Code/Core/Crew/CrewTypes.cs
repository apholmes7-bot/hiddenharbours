using System;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>How long a boat is going out</b> (crew slice R2). Ordered, so "longer than" is a plain
    /// comparison. How many nights each kind stands for is the owner's to tune in
    /// <see cref="CrewPolicy"/>; the kinds themselves are the vocabulary.
    ///
    /// <para><b>Append-only.</b> Never renumber: a <see cref="BoatVoyageDef"/> serializes the integer.</para>
    /// </summary>
    public enum TripKind
    {
        /// <summary>Out and home before night. Nobody sleeps aboard.</summary>
        Day = 0,
        /// <summary>One to a few nights aboard (<see cref="CrewPolicy.OvernightNights"/>).</summary>
        Overnight = 1,
        /// <summary>About a week aboard (<see cref="CrewPolicy.WeekNights"/>).</summary>
        Week = 2,
        /// <summary>About a month aboard (<see cref="CrewPolicy.MonthNights"/>).</summary>
        Month = 3,
    }

    /// <summary>Who a crew member is. Players and hired hands fill the same roles; the difference that
    /// matters to a voyage is the bunk rule (players never share a bed: owner ruling 2026-11-15).
    /// Append-only.</summary>
    public enum CrewMemberKind
    {
        Player = 0,
        HiredHand = 1,
    }

    /// <summary>
    /// <b>One person aboard, and the role they hold</b> (crew slice R2). A plain value: the roster
    /// stores these, the calculator reads them. <see cref="MemberId"/> is the person (a player's
    /// session id, a hired hand's staff id); <see cref="RoleId"/> is a <see cref="StaffRoleDef"/> id.
    /// </summary>
    [Serializable]
    public struct CrewMember : IEquatable<CrewMember>
    {
        public string MemberId;
        public CrewMemberKind Kind;
        public string RoleId;

        public CrewMember(string memberId, CrewMemberKind kind, string roleId)
        {
            MemberId = memberId;
            Kind = kind;
            RoleId = roleId;
        }

        public static CrewMember Player(string memberId, string roleId)
            => new CrewMember(memberId, CrewMemberKind.Player, roleId);

        public static CrewMember Hand(string memberId, string roleId)
            => new CrewMember(memberId, CrewMemberKind.HiredHand, roleId);

        public bool Equals(CrewMember other) => string.Equals(MemberId, other.MemberId, StringComparison.Ordinal)
            && Kind == other.Kind && string.Equals(RoleId, other.RoleId, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is CrewMember other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = MemberId != null ? StringComparer.Ordinal.GetHashCode(MemberId) : 0;
                h = h * 31 + (int)Kind;
                return h * 31 + (RoleId != null ? StringComparer.Ordinal.GetHashCode(RoleId) : 0);
            }
        }

        public override string ToString() => MemberId + " (" + Kind + ", " + RoleId + ")";
    }

    /// <summary>
    /// <b>Why a voyage or a boarding was refused</b> (crew slice R2): the plain reason code a UI turns
    /// into a sentence. <see cref="None"/> means allowed.
    ///
    /// <para><b>Append-only.</b> Add members at the END; never renumber (a later save or UI table may
    /// store the integer).</para>
    /// </summary>
    public enum VoyageRefusal
    {
        None = 0,
        /// <summary>No voyage record for this hull, so nobody can say what she can carry.</summary>
        UnknownHull = 1,
        /// <summary>Nobody aboard.</summary>
        NoCrew = 2,
        /// <summary>A member with no id, or the same person listed twice.</summary>
        InvalidMember = 3,
        /// <summary>A member holds a role id that no <see cref="StaffRoleDef"/> defines.</summary>
        UnknownRole = 4,
        /// <summary>More aboard than the hull has crew slots (<c>BoatHullDef.CrewSlots</c>), day or night.</summary>
        OverCrewSlots = 5,
        /// <summary>Fewer aboard than the voyage record's minimum for this trip kind.</summary>
        BelowMinimumCrew = 6,
        /// <summary>Nobody aboard holds a role that can take the helm.</summary>
        NoOneToHelm = 7,
        /// <summary>A trip with a night in it, on a hull with no bunk.</summary>
        NoBunks = 8,
        /// <summary>The trip is longer than this hull honestly goes (<see cref="BoatVoyageDef.LongestTrip"/>).</summary>
        TripLongerThanHullAllows = 9,
        /// <summary>More players than bunks. Players never share a bed (owner ruling 2026-11-15).</summary>
        PlayersOutnumberBunks = 10,
        /// <summary>More people than places to sleep once the players have theirs.</summary>
        CrewOutnumberBunks = 11,
        /// <summary>The planned work (a fishery, cargo) is not one this hull is set up for.</summary>
        WorkNotFittedToHull = 12,
        /// <summary>Fewer aboard than the planned work needs.</summary>
        BelowWorkMinimum = 13,
        /// <summary>The planned work needs a role nobody aboard holds. The detail names the role.</summary>
        MissingRequiredRole = 14,
        /// <summary>A month trip with no hired skipper aboard (<see cref="CrewPolicy.MonthTripsNeedHiredSkipper"/>).</summary>
        MonthNeedsHiredSkipper = 15,
        /// <summary>Not enough food aboard for everyone for the whole trip.</summary>
        ShortOfStores = 16,
        /// <summary>Not enough fresh water aboard for everyone for the whole trip.</summary>
        ShortOfWater = 17,
        /// <summary>Not enough ice to keep the catch for the whole trip.</summary>
        ShortOfIce = 18,
        /// <summary>Not enough fuel for the whole trip.</summary>
        ShortOfFuel = 19,
        /// <summary>The person is already aboard a boat; they must leave it first.</summary>
        AlreadyAboard = 20,
        /// <summary>The boat is already on the roster as a different hull.</summary>
        BoatHullMismatch = 21,
        /// <summary>No boat key was given.</summary>
        InvalidBoat = 22,
    }

    /// <summary>
    /// <b>The answer</b>: allowed or not, the plain reason if not, and the two limits that applied.
    /// <see cref="Detail"/> names the thing the reason is about (a role id, a member id) or is empty.
    /// </summary>
    public readonly struct VoyageVerdict
    {
        public readonly bool Allowed;
        public readonly VoyageRefusal Reason;
        public readonly string Detail;
        /// <summary>The most people aboard by day (the hull's crew slots). 0 when the hull is unknown.</summary>
        public readonly int DayMax;
        /// <summary>The most people aboard overnight under the policy in force. 0 when the hull is unknown.</summary>
        public readonly int OvernightMax;

        public VoyageVerdict(bool allowed, VoyageRefusal reason, string detail, int dayMax, int overnightMax)
        {
            Allowed = allowed;
            Reason = reason;
            Detail = detail ?? "";
            DayMax = dayMax;
            OvernightMax = overnightMax;
        }

        public static VoyageVerdict Yes(int dayMax, int overnightMax)
            => new VoyageVerdict(true, VoyageRefusal.None, "", dayMax, overnightMax);

        public static VoyageVerdict No(VoyageRefusal reason, string detail, int dayMax, int overnightMax)
            => new VoyageVerdict(false, reason, detail, dayMax, overnightMax);

        public override string ToString() => Allowed ? "allowed" : Reason + (Detail.Length > 0 ? " (" + Detail + ")" : "");
    }

    /// <summary>
    /// <b>What is aboard to live on</b> — the optional seam for stores, water, ice and fuel (crew slice R2).
    /// Each is nullable: <c>null</c> means "this system does not exist yet / not tracked", and the
    /// calculator then does not check it. Stores, water and an ice consumer arrive in slice R3; fuel exists
    /// but burns in real seconds, so a caller that wants it checked converts the tank to game days first.
    /// </summary>
    public struct VoyageSupplies
    {
        /// <summary>Food aboard, in person-days. Null = not tracked.</summary>
        public float? StoresPersonDays;
        /// <summary>Fresh water aboard, in person-days. Null = not tracked.</summary>
        public float? WaterPersonDays;
        /// <summary>Days the ice aboard keeps the catch. Null = not tracked.</summary>
        public float? IceDays;
        /// <summary>Game days of running the tank holds. Null = not tracked.</summary>
        public float? FuelGameDays;

        public static VoyageSupplies Untracked => default;
    }

    /// <summary>The question put to <see cref="VoyageLimits"/>: how long, and what work (a
    /// <c>work.*</c> id, or empty for none).</summary>
    public struct VoyagePlan
    {
        public TripKind Trip;
        public string WorkId;
        public VoyageSupplies Supplies;

        public VoyagePlan(TripKind trip, string workId = null, VoyageSupplies supplies = default)
        {
            Trip = trip;
            WorkId = workId;
            Supplies = supplies;
        }
    }
}
