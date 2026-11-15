using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// What one kind of work (a fishery, or cargo) needs aboard this hull: a head count and the roles
    /// somebody must hold. This is the METHOD minimum — what the gear needs to work at all — not a
    /// comfortable crew. A player alone covers any role that is not listed.
    /// </summary>
    [Serializable]
    public sealed class VoyageWorkRequirement
    {
        [Tooltip("work.<snake_case>, e.g. work.lobster_pots, work.otter_trawl, work.cargo. Some role must " +
                 "name it in StaffRoleDef.WorkIds.")]
        public string WorkId = "";

        [Tooltip("The fewest people aboard for this work to run on this hull.")]
        [Min(1)] public int MinCrew = 1;

        [Tooltip("Roles somebody aboard must hold for this work (role.* ids). One person per listed role.")]
        public string[] RequiredRoleIds = Array.Empty<string>();
    }

    /// <summary>
    /// <b>One hull's crew and trip-length record, as data</b> (crew slice R2; ADR 0003). The hull keeps
    /// what it already owns — <c>BoatHullDef.CrewSlots</c> is the day limit and the hull's interior
    /// <c>sleep</c> anchors are the bunks — and this record adds only what the hull does not say: the
    /// minimum crew by trip kind, the longest trip the hull honestly makes, and what each kind of work
    /// needs aboard.
    ///
    /// <para><b>Why a sibling asset and not fields on <c>BoatHullDef</c>.</b> (1) The hull assets are
    /// among the most edited files in the repo (fuel, trim, helm and rig lanes all touch them), and a
    /// separate record keeps crew tuning out of their merges. (2) The calculator and the roster are Core,
    /// and Core cannot read a Boats type (rule 4); this record is Core and names its hull by id.
    /// (3) It authors NO maximum: a second copy of the crew slots or the bunk count would drift the day a
    /// berth is added (PR 1228 adds second berths and must change nothing here).</para>
    ///
    /// <para>One asset per hull under <c>Data/Boats/Voyages/</c>; id <c>voyage.&lt;hull&gt;</c>, stable
    /// and append-only. Real-world figures and their sources are in
    /// <c>docs/design/crew-roles-and-trip-limits.md</c>.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Boat Voyage", fileName = "BoatVoyage")]
    public sealed class BoatVoyageDef : ScriptableObject
    {
        [Tooltip("Stable id, append-only: voyage.<hull>, e.g. voyage.cape_islander.")]
        public string Id = "";

        [Tooltip("The BoatHullDef id this record is for, e.g. boat.cape_islander.")]
        public string HullId = "";

        [Tooltip("The longest trip this hull honestly makes (shelter, tanks, hold), before bunks are " +
                 "counted. The calculator also refuses any night aboard a hull with no bunk.")]
        public TripKind LongestTrip = TripKind.Day;

        [Header("Minimum crew to operate, by trip kind")]
        [Tooltip("Fewest aboard for a day trip. 1 on every hull: the player must be able to play alone.")]
        [Min(1)] public int MinCrewDay = 1;
        [Min(1)] public int MinCrewOvernight = 1;
        [Min(1)] public int MinCrewWeek = 1;
        [Min(1)] public int MinCrewMonth = 1;

        [Header("Work this hull is set up for")]
        public VoyageWorkRequirement[] Work = Array.Empty<VoyageWorkRequirement>();

        [Header("For the owner (not read by code)")]
        [Tooltip("The real-world crew this hull's class carries, from the audit's tables.")]
        [TextArea(2, 4)] public string RealWorldCrew = "";

        [Tooltip("What really limits her trips, in a phrase.")]
        public string LimitedBy = "";

        /// <summary>The minimum authored for a trip kind (never below 1).</summary>
        public int MinCrewFor(TripKind trip)
        {
            switch (trip)
            {
                case TripKind.Day: return Math.Max(1, MinCrewDay);
                case TripKind.Overnight: return Math.Max(1, MinCrewOvernight);
                case TripKind.Week: return Math.Max(1, MinCrewWeek);
                default: return Math.Max(1, MinCrewMonth);
            }
        }

        /// <summary>The plain spec the calculator reads, joined with the two numbers the hull owns.</summary>
        public VoyageSpec ToSpec(int crewSlots, int bunks)
        {
            var mins = new[]
            {
                MinCrewFor(TripKind.Day), MinCrewFor(TripKind.Overnight),
                MinCrewFor(TripKind.Week), MinCrewFor(TripKind.Month),
            };
            return new VoyageSpec(HullId, crewSlots, bunks, LongestTrip, mins, Work);
        }
    }
}
