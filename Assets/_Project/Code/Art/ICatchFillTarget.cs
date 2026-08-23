using System.Collections.Generic;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>Something that DRAWS a container's contents</b> — the seam between the Core-facing bridge
    /// (<see cref="HoldCatchFillSource"/>) and whichever picture a particular container has.
    ///
    /// <para><b>Why this exists, and what it fixes.</b> The bridge used to be welded to
    /// <see cref="CatchFillRenderer"/> by a <c>RequireComponent</c> — the tote's presenter, which draws
    /// individual item sprites on the tote rig's measured slot points. That is the right picture for a
    /// tote and it is the WRONG one for a pail: the bucket rig bakes its fills as whole states (empty /
    /// few / half / full / brim × fish / shell / crust × eight facings) and publishes no slot anchors at
    /// all. So a bucket wired to the bridge resolved a tote geometry it does not have, drew nothing, and
    /// the fish you had just put in it was invisible — the container was full and looked empty, with no
    /// error anywhere.</para>
    ///
    /// <para>One interface, two implementors, one bridge: <see cref="CatchFillRenderer"/> for containers
    /// with slot geometry, <see cref="BucketFillPresenter"/> for containers with baked fill states. A
    /// third kind of container art needs a third implementor and nothing else.</para>
    ///
    /// <para><b>Plain data only.</b> Nothing here knows about holds, species or fishing: the bridge reads
    /// Core and hands over visual kinds, a fraction, a facing and a spoil level. That is what keeps the
    /// art lane out of the gameplay lanes (rule 4) and what makes every implementor testable with three
    /// method calls and no scene.</para>
    /// </summary>
    public interface ICatchFillTarget
    {
        /// <summary>The art table this target draws from — the bridge maps <c>speciesId → kind</c> with
        /// the SAME asset the target draws with, so the two can never disagree about what a cod is. Null
        /// is legal and means "not wired yet"; the bridge then has nothing to map and says so.</summary>
        CatchItemLibrary Library { get; }

        /// <summary>The container's contents: visual kinds in landed order, plus 0..1 fullness. The list
        /// is COPIED by the implementor — callers may reuse theirs.</summary>
        void SetContents(IReadOnlyList<string> kinds, float fill01);

        /// <summary>The container sprite's facing row (0..7).</summary>
        void SetDirection(int direction);

        /// <summary>Rot 0..1 — the published spoil input.</summary>
        void SetSpoil(float spoil01);
    }
}
