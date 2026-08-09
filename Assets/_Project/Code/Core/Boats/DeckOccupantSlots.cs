using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The things standing on one hull's deck that she has to draw over</b> — a FIXED array of
    /// occupant slots, claimed and released like the UI's drag session (first claim wins, an owner
    /// token, no dynamic list).
    ///
    /// <para><b>Why this exists.</b> A sprite on a deck is sorted above the hull's whole-object slot,
    /// so a wheelhouse in front of it can never cover it: sorting is per OBJECT and "is the boat
    /// between the camera and this?" is per PIXEL (owner playtest 2026-08-07, "sprites visible
    /// THROUGH closed cabins"). Only the hull can answer it, because only her facet pass holds her
    /// depth. She answers it by splitting her own alpha at the occupant's depth — and #461 shipped
    /// that for exactly ONE occupant, which is what stopped the stern-deck loop (#474): a trap on the
    /// stern and a fisher amidships sit at different depths, and one plane cannot be right for both.
    /// Hull geometry between them is "in front" of the far one and "behind" the near one.</para>
    ///
    /// <para><b>The protocol is PUBLISH THEN CONSULT, in that order, every frame.</b>
    /// <see cref="Set"/> tells the hull where your feet are; <see cref="OccluderId"/> then hands back
    /// the id to discard against, which depends on where you stand RELATIVE TO the other occupants
    /// (your rank by depth) and therefore cannot be known until you have published. Consulting a
    /// slot you have not set this frame gets you last frame's rank — harmless and self-correcting for
    /// a consumer that writes both every frame, which is what every consumer should do.</para>
    ///
    /// <para><b>Claims are held, not taken per frame.</b> Claim when you start standing there (a
    /// rider boarding, a piece of gear being placed), release when you stop. A slot re-issued under a
    /// live holder would silently move somebody else's occlusion onto them, which is why every call
    /// carries the owner token it was claimed with and a mismatched one is ignored.</para>
    ///
    /// <para><b>Overflow is loud and never silent.</b> A claim beyond <see cref="Capacity"/> is
    /// REFUSED — the earlier claims keep their slots — and the implementation logs it. The refused
    /// occupant simply draws un-occluded, which is the picture that shipped before any of this
    /// existed; it is never half-occluded and never steals a slot.</para>
    /// </summary>
    public interface IDeckOccupantSlots
    {
        /// <summary>How many occupants this hull can hide at once. Fixed for the life of the build —
        /// see <c>IsoFacetHullRenderer.DeckOccupantSlots</c> for the measurement behind the
        /// number.</summary>
        int Capacity { get; }

        /// <summary>How many slots are claimed AND standing right now. The number the hull's own
        /// shader gate is written from, and the one a caller checks before assuming it got in.</summary>
        int ActiveCount { get; }

        /// <summary>
        /// Take a slot, or <b>-1</b> when every slot is held (logged, never silent). Hold the
        /// returned index and pass it back to <see cref="Set"/>, <see cref="OccluderId"/> and
        /// <see cref="Release"/> along with the same <paramref name="owner"/>.
        /// </summary>
        int Claim(object owner);

        /// <summary>Give a slot back. Ignored unless <paramref name="owner"/> is the token the slot
        /// was claimed with — a stale holder must not be able to evict a live one.</summary>
        void Release(int slot, object owner);

        /// <summary>
        /// <b>This occupant is standing HERE</b> — their feet in the hull's own rig metres (+X
        /// starboard, +Y toward the bow, +Z up from the keel: the frame the deck polygons and every
        /// fitting pivot already speak), or <paramref name="active"/> false while they are aboard but
        /// not to be hidden (mid-air, mid-swap, ashore for a frame).
        ///
        /// <para>Cheap and idempotent, safe to write every LateUpdate with no allocation (rule 7).</para>
        /// </summary>
        void Set(int slot, object owner, Vector3 rigLocalMeters, bool active);

        /// <summary>
        /// The LOWEST id an occludable sprite in this slot must discard against, already divided by
        /// 255 for the shader — and <b>0 whenever there is nothing to hide behind</b> (slot not
        /// claimed, not standing, or the hull is not live). Pair it with
        /// <see cref="OccluderIdTop"/>: the sprite is hidden across that whole range, because every
        /// band nearer than this occupant hides them too.
        /// </summary>
        float OccluderId(int slot);

        /// <summary>The top of this hull's fore-id block, /255 — the upper bound of every occupant's
        /// discard range, and what stops one boat's ids cutting holes in another boat's crew. 0 when
        /// the hull holds no block.</summary>
        float OccluderIdTop { get; }
    }

    /// <summary>
    /// <b>The partition rule the hull's alpha encodes</b> — the CPU half of what the facet shader
    /// counts per pixel, kept here as pure maths so the two halves cannot drift into disagreeing.
    ///
    /// <para><b>The theorem the whole encoding rests on:</b> for any hull pixel at view depth
    /// <c>z</c> and any occupant <c>k</c>,
    /// <c>Band(z) &gt;= RankOf(depth[k])</c> <b>if and only if</b> <c>z &lt; depth[k]</c>.</para>
    ///
    /// <para>That equivalence is why a single 8-bit channel can answer for a dozen occupants at
    /// once. The pixels in front of an occupant are always the pixels in front of every DEEPER
    /// occupant too, so the regions NEST — and consecutive band ids reproduce nesting exactly, which
    /// no per-occupant id could. It is also why nothing has to be sorted: the band is a count, and a
    /// count does not care what order it was taken in.</para>
    ///
    /// <para>Both functions read only the ACTIVE occupants' depths, packed into the first
    /// <c>count</c> entries of the array the caller hands in — no allocation, so the drawer can call
    /// it every LateUpdate (rule 7).</para>
    /// </summary>
    public static class DeckOccupantPartition
    {
        /// <summary>
        /// How many occupants a hull pixel at <paramref name="pixelViewDepth"/> is IN FRONT OF — the
        /// number the facet shader counts per fragment, and the index into the hull's fore block
        /// (0 = behind everybody, which is the hull's own base id).
        ///
        /// <para>Strictly less-than, so the planking under an occupant's own boots — at exactly
        /// their depth — stays BEHIND them rather than being drawn over them.</para>
        /// </summary>
        public static int Band(float pixelViewDepth, float[] activeDepths, int count)
        {
            if (activeDepths == null) return 0;
            int band = 0;
            int n = count < activeDepths.Length ? count : activeDepths.Length;
            for (int i = 0; i < n; i++) if (pixelViewDepth < activeDepths[i]) band++;
            return band;
        }

        /// <summary>
        /// An occupant's RANK: how many occupants stand at or deeper than
        /// <paramref name="occupantViewDepth"/>, counting themselves — so it is never 0 for an
        /// occupant who is in the array, and the lowest band that hides them is exactly this.
        ///
        /// <para>At-or-deeper rather than strictly-deeper, so a TIE hands both occupants the same
        /// rank. That is not a tolerance, it is the truth: no pixel can be in front of one of two
        /// things standing at the same depth, so no band can separate them.</para>
        /// </summary>
        public static int RankOf(float occupantViewDepth, float[] activeDepths, int count)
        {
            if (activeDepths == null) return 0;
            int rank = 0;
            int n = count < activeDepths.Length ? count : activeDepths.Length;
            for (int i = 0; i < n; i++) if (activeDepths[i] >= occupantViewDepth) rank++;
            return rank;
        }
    }

    /// <summary>
    /// The <see cref="IDeckOccupantSlots"/> of a hull that cannot hide anything: a sprite hull (a
    /// flat sheet has no depth to split), a greybox boat, a test fake. Every claim is refused, every
    /// id is 0 — which is exactly the shader's inert state, so a caller written against the real
    /// thing draws un-occluded here instead of branching on it.
    /// </summary>
    public sealed class NoDeckOccupantSlots : IDeckOccupantSlots
    {
        /// <summary>The one instance. Stateless, so sharing it is free and safe.</summary>
        public static readonly NoDeckOccupantSlots Instance = new NoDeckOccupantSlots();

        private NoDeckOccupantSlots() { }

        public int Capacity => 0;
        public int ActiveCount => 0;

        /// <summary>Always refused — and SILENTLY, unlike a real hull's overflow. Standing on a
        /// sprite hull is not a mistake to be shouted about; it is most of the fleet.</summary>
        public int Claim(object owner) => -1;

        public void Release(int slot, object owner) { }
        public void Set(int slot, object owner, Vector3 rigLocalMeters, bool active) { }
        public float OccluderId(int slot) => 0f;
        public float OccluderIdTop => 0f;
    }
}
