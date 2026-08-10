using UnityEngine;
using HiddenHarbours.Core;   // CharacterVisualDef — who stands aboard

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// How well a boat's owner is doing. <b>The tier is a LABEL, not a size</b> — the ground their shed
    /// reserves is <see cref="BoatOwnerDef.ShedFootprintMetres"/>, an owner-tunable number on the asset
    /// (rule 6). What the tier buys is an ORDERING the content tests hold the sizes to: a prosperous
    /// owner's shed is never smaller than a struggling one's.
    ///
    /// <para>Declared low-to-high and never renumbered — the ordering IS the invariant.</para>
    /// </summary>
    public enum BoatOwnerProsperity
    {
        /// <summary>One boat, a lean season, a shed that is really a lean-to.</summary>
        Scraping = 0,
        /// <summary>Pays their way. The middle of any real wharf's register.</summary>
        Steady = 1,
        /// <summary>A good run of seasons — the shed has grown a bay.</summary>
        Doing_Well = 2,
        /// <summary>The wharf's big fish: the largest shed on the yard, and it shows.</summary>
        Prosperous = 3,
    }

    /// <summary>
    /// <b>ONE BOAT'S OWNER — the network behind a moored hull.</b> The owner's ruling (2026-08-10):
    /// <i>"Each moored BOAT has its own owner NETWORK, and owners differ in success — a more successful
    /// owner has a LARGER building."</i> This is that, as data: a name off the PEI register, how well they
    /// are doing, the boat they keep, the buoy scheme their gear is painted in, the shed lot on the yard
    /// that is theirs, and who is aboard her.
    ///
    /// <para><b>Content is data, not code (ADR 0003, rule 2).</b> One entity per file under
    /// <c>Data/Boats/Owners</c>, <c>type.snake_case</c> ids, append-only. Adding a fourteenth fisher to
    /// Nine Mile Creek is a new asset and a re-run of the region builder — never a code change.</para>
    ///
    /// <para><b>⚠ THE BUOY SCHEME IS AN OWNERSHIP MARK, AND IT IS THE SCARCE THING.</b> The deck-loop kit
    /// bakes exactly EIGHT buoy paint schemes (<c>Art/Sprites/DeckGear/Buoys</c>). Two fishers whose gear
    /// is painted the same way cannot be told apart on the water, which is the one job the mark has — so
    /// a scheme is claimed by at most one owner and the content tests enforce it. <b>That caps a
    /// wharf's authored register at eight</b>, which is a real constraint on the owner's ~16-boat vision
    /// and is reported rather than worked around.</para>
    ///
    /// <para><b>⚠ THE SCHEME KEYS ARE NAMED AFTER HULLS AND DO NOT MEAN HULLS.</b> The kit's eight cells
    /// are keyed <c>Dory · Punt · CapeIslander · LobsterBoat · SideDragger · SternTrawler ·
    /// CoastalPacket · Tanker</c> because that is the hull each scheme was drawn against. They are
    /// <i>paint schemes</i>. An owner whose scheme is <c>Tanker</c> does not keep a tanker — and could
    /// not, because the harbour's 1.6 m shoal would not float one.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Boat Owner", fileName = "BoatOwner")]
    public class BoatOwnerDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (CLAUDE.md §5): type.snake_case, e.g. owner.arsenault_leo.")]
        public string Id = "owner.";

        [Tooltip("The name on the register — how the wharf refers to them.")]
        public string DisplayName = "";

        [Tooltip("The boat they keep. Read for BOTH halves of the presentation: her Visual is what gets " +
                 "drawn, and her DraughtMeters is what the harbour's shoal is allowed to float.")]
        public BoatHullDef Boat;

        [Header("Their mark")]
        [Tooltip("Which of the deck-loop kit's eight buoy paint schemes their gear wears — the key of a " +
                 "cell in the kit's 'buoy' family. Claimed by at most one owner in a region (see the " +
                 "class note): two fishers painting the same is two fishers you cannot tell apart.")]
        public string BuoyScheme = "";

        [Header("Standing on the wharf")]
        [Tooltip("How well they are doing. A LABEL — what it buys is the ordering the content tests hold " +
                 "ShedFootprintMetres to, never a size computed from it.")]
        public BoatOwnerProsperity Prosperity = BoatOwnerProsperity.Steady;

        [Tooltip("The ground their shed reserves on the yard, in metres square — the owner's tunable " +
                 "(rule 6), and the number a more successful owner's LARGER building actually is. The " +
                 "region's plan owns WHERE the lots are; this owns how big this one is.")]
        [Min(1f)] public float ShedFootprintMetres = 6f;

        [Tooltip("Which reserved shed lot on the region's yard is theirs (0-based, into the region " +
                 "plan's own lot row). One lot per owner — the content tests hold that too.")]
        [Min(0)] public int LotIndex = 0;

        [Header("Where she lies")]
        [Tooltip("Which berth along the mooring edge is theirs (0-based, into the region's berth table). " +
                 "⚠ Never the berth the player docks in — the region's tests derive which one that is " +
                 "and refuse it.")]
        [Min(0)] public int BerthIndex = 1;

        [Header("Who is aboard (no routine — a figure on a deck, ruled 2026-08-10)")]
        [Tooltip("The skipper standing on her deck while she lies at her berth. Null = nobody aboard, " +
                 "which is a legitimate authoring choice (they are ashore) and never an error.")]
        public CharacterVisualDef Skipper;

        // ⚠ ONE FIGURE PER HULL, AND IT IS A SEAM LIMIT RATHER THAN A TASTE ONE. A second crew member
        // would have to stand somewhere ELSE on the deck, and putting a figure at an arbitrary deck point
        // means projecting a rig-local metre through the hull's own iso projection. The renderer does
        // exactly that (IsoFacetHullRenderer.PosedMesh.TransformPoint) but the CORE seam Boats talks to it
        // through — IHullMeshRenderer — publishes only SetDeckOccupant(rigMeters, active) and no way to
        // read the projected point back. Re-deriving the projection here would be a second copy of the
        // one thing this repo has already paid for twice (the iso GAIN, the animator PHASE), so the
        // second figure waits on a Core accessor and is a reported gap, not a guess.

        /// <summary>True when this owner can actually be presented: they keep a boat, and that boat has
        /// something to draw. The gate the region's fleet placer skips on, loudly.</summary>
        public bool IsPresentable() => Boat != null && Boat.Visual != null;

        /// <summary>How many people this owner puts on her deck — never more than the hull has crew
        /// slots for, because a boat with more aboard than she berths is one somebody mis-authored.</summary>
        public int AboardCount()
        {
            int wanted = Skipper != null ? 1 : 0;
            return Boat != null ? Mathf.Min(wanted, Mathf.Max(0, Boat.CrewSlots)) : 0;
        }
    }
}
