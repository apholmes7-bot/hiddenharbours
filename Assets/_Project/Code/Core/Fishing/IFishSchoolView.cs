using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// THE SCHOOLS ON SCREEN (the visible-fish arc, owner's ruling 2026-09-05: <i>"I want fish to
    /// actually be visible swimming"</i>) — the same schools <see cref="IFishSchools"/> answers for, asked
    /// over an <b>area</b> instead of a point.
    ///
    /// <para><b>Why this is not just <see cref="IFishSchools.SchoolsAt"/>.</b> That call is deliberately a
    /// CONTAINMENT query: "which schools' areas contain this position" — the right question for a rod in
    /// the water and for a finder reading the water under the hull, and the reason there is no second
    /// search radius in the game. A presenter asks a different question: a school 5 m off the bow is
    /// plainly on screen and must be drawn, even though the camera's centre is outside it. Answering that
    /// with the point query would pop every school in and out at its own rim as the boat crossed it — the
    /// one artefact this seam exists to prevent.</para>
    ///
    /// <para><b>It is still ONE model.</b> This is not a second sim and cannot become one: the producer
    /// answers it from the very same per-<c>(cell, slot)</c> build the point query uses, over a wider span
    /// of cells. A school seen here is bit-for-bit the school <see cref="IFishSchools.SchoolsAt"/> returns
    /// when you drive onto it, which is what makes the owner's honesty invariant hold for the WATER as
    /// well as for the glass: <b>a fish you can see is a fish you can catch</b>, and a species drawn is a
    /// species the resolver would roll. Anything that made this query invent, filter or re-weight fish
    /// would break that in exactly the way a separate bite table would.</para>
    ///
    /// <para><b>Optional by design.</b> A producer that has no cheap area answer simply does not implement
    /// it, and a presenter that finds none draws no fish — the honest empty sea, the same shape as
    /// <see cref="EmptyFishSchools"/>. That keeps the four existing <see cref="IFishSchools"/> readers and
    /// every test fake untouched by this arc.</para>
    ///
    /// <para><b>No allocation on the read path</b> (rule 7) and <b>never saved</b> (rule 5) — both for the
    /// same reasons as <see cref="IFishSchools"/>.</para>
    /// FLAG lead-architect: new Core contract (the visible-fish presenter seam).
    /// </summary>
    public interface IFishSchoolView
    {
        /// <summary>
        /// Every school whose area OVERLAPS <paramref name="viewWorld"/> and whose window is open at
        /// <paramref name="gameSeconds"/> (the <c>IGameClock.TotalSeconds</c> frame).
        /// <paramref name="into"/> is cleared first and returned filled; the return value is how many were
        /// written. A null list is tolerated (nothing is written, the count is still correct).
        ///
        /// <para>Overlap, not containment: a school counts when any part of its disc is inside the rect,
        /// so a shoal at the screen edge is drawn rather than clipped away at its centre.</para>
        ///
        /// <para><b>The producer may cap what it returns</b> to keep the query bounded when the rect is
        /// large or the owner has tuned the cells small (rule 7). A capped read is still honest — it is
        /// fewer of the same schools, never different ones — but it means this count is a
        /// <i>presentation</i> budget and must never be read as "how many fish are here". That question
        /// has one answer, and it is <see cref="IFishSchools.SchoolsAt"/>.</para>
        /// </summary>
        int SchoolsInView(Rect viewWorld, double gameSeconds, List<FishSchool> into);
    }
}
