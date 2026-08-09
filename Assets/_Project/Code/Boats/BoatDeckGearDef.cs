using System;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// One piece of the kit's furniture, bolted to one hull: WHICH kind, WHERE on her deck, and which way
    /// round. The station that goes with it lives on the kit asset — a bench's footprint belongs to the
    /// bench, not to the boat it is screwed to.
    /// </summary>
    [Serializable]
    public sealed class BoatDeckGearMount
    {
        [Tooltip("Which piece of the kit stands here.")]
        public DeckGearKind Kind = DeckGearKind.BandingTable;

        [Tooltip("Where its own pivot sits in HULL-LOCAL METRES — the sidecars' frame, unchanged: origin " +
                 "amidships / keel-bottom / centreline, +x starboard, +y bow, +z up. The hauler's mount " +
                 "is the hull rig's own HAULER; the rest are measured against her DECK polygon.")]
        public Vector3 MountLocalMeters = Vector3.zero;

        [Tooltip("Which way the piece itself faces, in 45° steps of the kit's turntable. The operator's " +
                 "facing is DERIVED from this plus the station's turn plus the hull's drawn heading, " +
                 "every frame — never stored (#457).")]
        public int Dir;
    }

    /// <summary>
    /// Where pots stack on one surface of one hull, and how many she will take there.
    ///
    /// <para>The capacity is <c>TrapIso.CAPS</c> for this hull and this surface — a lobster boat takes 5
    /// on the deck and 2 on the washboard where a Cape Islander takes 3 and 2, and a wharf takes 6 and
    /// none. <b>0 is a real answer</b> meaning "you cannot stack there", not a missing value to fall back
    /// from.</para>
    /// </summary>
    [Serializable]
    public sealed class BoatDeckStackBase
    {
        [Tooltip("Which surface of the hull this base stands on. The deck fills first; the washboard is " +
                 "the overflow a working boat actually uses once her deck is full.")]
        public DeckStackSurface Surface = DeckStackSurface.Deck;

        [Tooltip("Where the BOTTOM pot's pivot sits, in hull-local metres (the sidecars' frame).")]
        public Vector3 BaseLocalMeters = Vector3.zero;

        [Tooltip("Which way the stack faces, in 45° steps — the bearing the per-tier slew is rotated by, " +
                 "so a stack leans along the hull rather than across her.")]
        public int Dir;

        [Tooltip("How many pots this surface takes — TrapIso.CAPS[hull][surface]. RIG DATA: read it, " +
                 "never restate it in code. 0 means this surface will not take a pot at all.")]
        [Min(0)] public int Capacity;
    }

    /// <summary>
    /// <b>One hull's working deck, as data</b> — where her gear is bolted, where her pots stack and how
    /// many she takes. The asset that makes the stern-deck loop work on EVERY hull instead of on the one
    /// whose rectangle somebody typed into a C# file.
    ///
    /// <para><b>This is the DeckWalkController lesson, applied.</b> A single hard-coded deck rectangle was
    /// right for exactly one boat and quietly wrong for the rest; the fix was authored polygons per hull
    /// (<see cref="BoatDeckDef"/>), and this is that same fix for the gear that stands on them. Nothing in
    /// the presenter knows a mount, a capacity or a tier step — every one of them arrives from here or
    /// from the kit, and a hull with no asset simply has no deck gear, which is honest rather than
    /// invented.</para>
    ///
    /// <para><b>Where the numbers come from.</b> Mounts against each hull's own gameplay sidecar
    /// (<c>docs/art/rigs/gameplay/&lt;rig&gt;.gameplay.json</c> — her DECK polygon and WASHBOARD strips)
    /// and, for the hauler, her rig's own <c>HAULER</c> block. Capacities from
    /// <c>docs/art/rigs/deck-loop-kit/Art/trapIsoRig.js</c>'s <c>CAPS</c>. The rigs are the source; this
    /// asset transcribes them.</para>
    ///
    /// <para><b>Tier steps are NOT here</b>, deliberately: how far the next pot up sits is a property of
    /// the POT — cones nest at 0.52 m where a wooden bow-top needs 0.40 — so it rides the trap's own Def
    /// (<c>TrapIso.STACK</c> per build). A step on this asset would make a hull's data lie about a pot she
    /// had never seen.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Boat Deck Gear", fileName = "BoatDeckGear")]
    public sealed class BoatDeckGearDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (CLAUDE.md §5): type.snake_case, e.g. deckgear.lobster_boat.")]
        public string Id = "";

        [Tooltip("The hull rig these mounts were measured against, for the reader checking a number.")]
        public string SourceRig = "";

        [Header("The gear bolted to this hull")]
        [Tooltip("One entry per piece of furniture on this deck. Empty = this hull carries no deck gear, " +
                 "which is the honest answer for an open boat.")]
        public BoatDeckGearMount[] Mounts = Array.Empty<BoatDeckGearMount>();

        [Header("Where her pots stack")]
        [Tooltip("One entry per stacking surface. The deck fills before the washboard; a surface with " +
                 "capacity 0 is one you cannot stack on at all.")]
        public BoatDeckStackBase[] StackBases = Array.Empty<BoatDeckStackBase>();

        /// <summary>The mount for <paramref name="kind"/>, or null when this hull does not carry it. A
        /// hull with no hauler station hand-hauls, and that is data saying so.</summary>
        public BoatDeckGearMount MountFor(DeckGearKind kind)
        {
            if (Mounts == null) return null;
            for (int i = 0; i < Mounts.Length; i++)
                if (Mounts[i] != null && Mounts[i].Kind == kind) return Mounts[i];
            return null;
        }

        /// <summary>The stack base on <paramref name="surface"/>, or null when this hull offers none
        /// there.</summary>
        public BoatDeckStackBase StackBaseFor(DeckStackSurface surface)
        {
            if (StackBases == null) return null;
            for (int i = 0; i < StackBases.Length; i++)
                if (StackBases[i] != null && StackBases[i].Surface == surface) return StackBases[i];
            return null;
        }

        /// <summary>This hull's capacity on <paramref name="surface"/> — 0 when she offers no base there,
        /// which is the same answer as a base that takes nothing and is meant to be.</summary>
        public int CapacityOf(DeckStackSurface surface)
        {
            BoatDeckStackBase b = StackBaseFor(surface);
            return b != null ? Mathf.Max(0, b.Capacity) : 0;
        }

        /// <summary>Every pot this hull can stack, across every surface she offers.</summary>
        public int TotalStackCapacity()
            => SternDeckLoop.TotalStackCapacity(CapacityOf(DeckStackSurface.Deck),
                                                CapacityOf(DeckStackSurface.Washboard));

        /// <summary>True when this hull works her pots at a hydraulic hauler rather than by hand — the
        /// fact that decides whether the deck draws the kit's <c>hauler</c> clip or leaves the hand-haul
        /// to the fisher's own animator. Read off the DATA, never off a hull name.</summary>
        public bool HasHaulerStation() => MountFor(DeckGearKind.HaulerStation) != null;
    }
}
