using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>May this crew take this hull on this trip?</b> (crew slice R2). A pure function of its inputs:
    /// no clock, no randomness, no services, no Unity — the same question always gets the same answer,
    /// whatever order the crew is listed in (rule 5).
    ///
    /// <para><b>The rules</b> (audit §2.2, owner ruling 2026-11-15):</para>
    /// <list type="bullet">
    /// <item>By day, at most <c>BoatHullDef.CrewSlots</c> aboard.</item>
    /// <item>Any night aboard needs a bunk. Players never share one. Hired hands have one each unless
    /// <see cref="CrewPolicy.HiredHandsShareBunks"/> is ON (open question Q3, shipped OFF).</item>
    /// <item>At least the record's minimum for the trip kind, and somebody who can take the helm.</item>
    /// <item>No longer than the hull honestly goes; a month only with a hired skipper aboard while
    /// <see cref="CrewPolicy.MonthTripsNeedHiredSkipper"/> is ON (open question Q5).</item>
    /// <item>Planned work must be fitted to the hull, with its head count and its required roles.</item>
    /// <item>Stores, water, ice and fuel only when the caller tracks them (null = not checked).</item>
    /// </list>
    ///
    /// <para>The first rule broken is the reason given, in the order above (bunks before the hull's
    /// honest trip length, so an open boat says "no bunks", the plainest true thing).</para>
    /// </summary>
    public static class VoyageLimits
    {
        public static VoyageVerdict Check(VoyageSpec spec, IReadOnlyList<CrewMember> crew, in VoyagePlan plan,
            StaffRoleBook roles, in CrewPolicy policy)
        {
            if (spec == null) return VoyageVerdict.No(VoyageRefusal.UnknownHull, "", 0, 0);
            roles ??= StaffRoleBook.Empty;
            int dayMax = spec.DayMax;
            int nightMax = spec.OvernightMax(policy);
            VoyageVerdict No(VoyageRefusal r, string detail = "") => VoyageVerdict.No(r, detail, dayMax, nightMax);

            int count = crew != null ? crew.Count : 0;
            if (count == 0) return No(VoyageRefusal.NoCrew);

            // Members: ids present, nobody twice, every role real. Checked in a sorted order so the
            // detail named is the same whatever order the crew came in.
            var sorted = SortedById(crew);
            for (int i = 0; i < sorted.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(sorted[i].MemberId)) return No(VoyageRefusal.InvalidMember);
                if (i > 0 && string.Equals(sorted[i].MemberId, sorted[i - 1].MemberId, StringComparison.Ordinal))
                    return No(VoyageRefusal.InvalidMember, sorted[i].MemberId);
            }
            foreach (var m in sorted)
                if (!roles.Contains(m.RoleId)) return No(VoyageRefusal.UnknownRole, m.RoleId ?? "");

            if (count > dayMax) return No(VoyageRefusal.OverCrewSlots);
            if (count < spec.MinCrew(plan.Trip)) return No(VoyageRefusal.BelowMinimumCrew);
            if (!AnyHolds(sorted, roles, StaffRoleBook.HelmTask, hiredOnly: false)) return No(VoyageRefusal.NoOneToHelm);

            if (plan.Trip != TripKind.Day)
            {
                if (spec.Bunks <= 0) return No(VoyageRefusal.NoBunks);
                if (plan.Trip > spec.LongestTrip) return No(VoyageRefusal.TripLongerThanHullAllows);
                int players = 0;
                foreach (var m in sorted) if (m.Kind == CrewMemberKind.Player) players++;
                int hands = count - players;
                if (players > spec.Bunks) return No(VoyageRefusal.PlayersOutnumberBunks);
                int free = spec.Bunks - players;
                int handPlaces = policy.HiredHandsShareBunks ? free * Math.Max(1, policy.HandsPerSharedBunk) : free;
                if (hands > handPlaces) return No(VoyageRefusal.CrewOutnumberBunks);
            }

            if (!string.IsNullOrEmpty(plan.WorkId))
            {
                if (!spec.TryGetWork(plan.WorkId, out var work)) return No(VoyageRefusal.WorkNotFittedToHull, plan.WorkId);
                if (count < Math.Max(1, work.MinCrew)) return No(VoyageRefusal.BelowWorkMinimum, plan.WorkId);
                string missing = MissingRole(sorted, work.RequiredRoleIds);
                if (missing != null) return No(VoyageRefusal.MissingRequiredRole, missing);
            }

            if (plan.Trip == TripKind.Month && policy.MonthTripsNeedHiredSkipper
                && !AnyHolds(sorted, roles, StaffRoleBook.HelmTask, hiredOnly: true))
                return No(VoyageRefusal.MonthNeedsHiredSkipper);

            var s = plan.Supplies;
            int nights = policy.NightsFor(plan.Trip);
            float days = nights + 1;
            if (plan.Trip != TripKind.Day)
            {
                // A day trip eats and drinks ashore and lands its catch the same day.
                if (s.StoresPersonDays.HasValue && s.StoresPersonDays.Value < count * days) return No(VoyageRefusal.ShortOfStores);
                if (s.WaterPersonDays.HasValue && s.WaterPersonDays.Value < count * days) return No(VoyageRefusal.ShortOfWater);
                if (s.IceDays.HasValue && s.IceDays.Value < days) return No(VoyageRefusal.ShortOfIce);
            }
            if (s.FuelGameDays.HasValue && s.FuelGameDays.Value < days) return No(VoyageRefusal.ShortOfFuel);

            return VoyageVerdict.Yes(dayMax, nightMax);
        }

        /// <summary>The first required role (in its authored order) that no one aboard holds, matching one
        /// person to each listed role so two listings of a role need two people. Null when all are held.</summary>
        private static string MissingRole(List<CrewMember> crew, string[] required)
        {
            if (required == null || required.Length == 0) return null;
            var used = new bool[crew.Count];
            foreach (string role in required)
            {
                if (string.IsNullOrWhiteSpace(role)) continue;
                bool found = false;
                for (int i = 0; i < crew.Count; i++)
                {
                    if (used[i] || !string.Equals(crew[i].RoleId, role, StringComparison.Ordinal)) continue;
                    used[i] = true;
                    found = true;
                    break;
                }
                if (!found) return role;
            }
            return null;
        }

        private static bool AnyHolds(List<CrewMember> crew, StaffRoleBook roles, string task, bool hiredOnly)
        {
            foreach (var m in crew)
                if ((!hiredOnly || m.Kind == CrewMemberKind.HiredHand) && roles.HasTask(m.RoleId, task)) return true;
            return false;
        }

        private static List<CrewMember> SortedById(IReadOnlyList<CrewMember> crew)
        {
            var list = new List<CrewMember>(crew.Count);
            for (int i = 0; i < crew.Count; i++) list.Add(crew[i]);
            list.Sort((a, b) =>
            {
                int c = string.CompareOrdinal(a.MemberId, b.MemberId);
                if (c != 0) return c;
                c = a.Kind.CompareTo(b.Kind);
                return c != 0 ? c : string.CompareOrdinal(a.RoleId, b.RoleId);
            });
            return list;
        }
    }
}
