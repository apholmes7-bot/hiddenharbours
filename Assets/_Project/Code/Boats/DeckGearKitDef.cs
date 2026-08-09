using System;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The kinds of working furniture the deck-loop kit draws. The names are the kit's own
    /// (<c>deckGearRig.js</c> <c>KINDS</c>) so a reader can go straight from an asset to the rig that
    /// measured it; the enum exists only so a Def can be authored without typing a string.
    /// </summary>
    public enum DeckGearKind
    {
        /// <summary>A lidded bin of salt bait.</summary>
        BaitBin = 0,
        /// <summary>An open bait box.</summary>
        BaitBox = 1,
        /// <summary>The chopping board bait is cut on.</summary>
        ChopBoard = 2,
        /// <summary>The banding bench keepers are worked at.</summary>
        BandingTable = 3,
        /// <summary>A section of washboard carrying the hydraulic hauler and its davit.</summary>
        HaulerStation = 4,
    }

    /// <summary>
    /// One kind of deck furniture, as the kit measured it: where a person STANDS to work it, which way
    /// they face, the world metre their hands work at, and the deck a planner must keep clear for them —
    /// plus the eight drawn facings, when they have been baked.
    ///
    /// <para>Every number here is <c>DeckGear.STATIONS[kind]</c>, and it belongs to the FURNITURE rather
    /// than to any hull that carries it: a banding bench reserves the same footprint on a Cape Islander as
    /// on a lobster boat. That is why the stations live on the KIT asset and the mounts live on the hull's
    /// (see <see cref="BoatDeckGearDef"/>).</para>
    /// </summary>
    [Serializable]
    public sealed class DeckGearKitEntry
    {
        [Tooltip("Which piece of furniture this entry measures.")]
        public DeckGearKind Kind = DeckGearKind.BaitBox;

        [Tooltip("Where the operator stands RELATIVE TO THE GEAR, in the gear's own local metres before " +
                 "its bearing is applied (DeckGear.STATIONS[kind].stand).")]
        public Vector3 StandLocalMeters = Vector3.zero;

        [Tooltip("Facing steps to ADD to the gear's own dir to get the operator's (STATIONS[kind].turn). " +
                 "The hauler is the one station whose operator faces OUTBOARD — its turn is 4, a half " +
                 "turn — because the warp comes in over the rail and the drum is worked from inboard of " +
                 "it. Every other station's operator faces the gear, turn 0.")]
        public int Turn;

        [Tooltip("The world metre above the hull frame's z=0 that the hands work at " +
                 "(STATIONS[kind].workZ). ⚠️ A WORLD METRE, never a body fraction: a bench belongs to the " +
                 "deck, not to whoever is standing at it. This is what a clip is handed as opts.workZ, " +
                 "and what a load worked at this station SITS on.")]
        public float WorkZMeters = 0.5f;

        [Tooltip("Deck a planner must reserve for the operator, metres (x across, y along) " +
                 "(STATIONS[kind].clear) — BEFORE it packs any pots onto the deck.")]
        public Vector2 ClearMeters = new Vector2(0.6f, 0.6f);

        [Tooltip("The eight drawn facings of this piece, element [dir], dir 0 = the kit's first cell. " +
                 "EMPTY OR SHORT = this kind has no art and the presenter draws its greybox stand-in " +
                 "instead — the deck-gear bake may land after the code that places it, and a half-wired " +
                 "sheet must never index a stale cell.")]
        public Sprite[] Facings = Array.Empty<Sprite>();

        /// <summary>The station as the pure placement maths wants it. Built fresh per call — a
        /// <see cref="DeckGearPlacement.Station"/> is a readonly value and caching one would be one more
        /// place for a facing to go stale (#457).</summary>
        public DeckGearPlacement.Station Station()
            => new DeckGearPlacement.Station(StandLocalMeters, Turn, WorkZMeters, ClearMeters);

        /// <summary>True when this kind's facings are COMPLETE for <paramref name="facingCount"/> — the
        /// all-or-nothing rule every sheet in the repo keeps. A short sheet is dropped whole.</summary>
        public bool HasArt(int facingCount)
        {
            if (Facings == null || Facings.Length < facingCount) return false;
            for (int i = 0; i < facingCount; i++) if (Facings[i] == null) return false;
            return true;
        }

        /// <summary>The cell for facing step <paramref name="dir"/>, or null when this kind is unbaked.
        /// Wrapped negative-safe so a caller may hand in a raw composed dir.</summary>
        public Sprite CellFor(int dir, int facingCount)
        {
            if (!HasArt(facingCount)) return null;
            int n = facingCount <= 0 ? 1 : facingCount;
            int d = dir % n;
            if (d < 0) d += n;
            return Facings[d];
        }
    }

    /// <summary>
    /// <b>The deck-loop kit, as data</b> — one asset describing the working furniture every hull in the
    /// fleet shares, so no hull's asset restates a bench's footprint and no C# file hard-codes one.
    ///
    /// <para><b>Where the numbers come from.</b> <c>docs/art/rigs/deck-loop-kit/Art/deckGearRig.js</c>,
    /// its <c>STATIONS</c> table, measured in a V8 host at rig defaults (the same route
    /// <c>deckLoopKit.contract.json</c> took). The rig is the source; this asset is a transcription of it
    /// and nothing else. If the two ever disagree, the rig wins and this is re-imported.</para>
    ///
    /// <para><b>⚠️ The kit turns the OTHER WAY from the fleet's boats</b> — its own contract says so in
    /// as many words: the azimuth convention is CLOCKWISE where the registered reference is
    /// counter-clockwise, and "do NOT apply the fleet CounterClockwise correction to this kit — it mirrors
    /// all eight cells". <see cref="FacingsAreCounterClockwise"/> carries that fact as DATA so the
    /// presenter reads it off the asset rather than a constant deciding it for every kit forever.</para>
    ///
    /// <para><b>Art may be absent and that is a supported state.</b> The kit's sheets bake on their own
    /// schedule; until they land every entry's <see cref="DeckGearKitEntry.Facings"/> is empty,
    /// <see cref="DeckGearKitEntry.HasArt"/> is false, and the presenter draws greybox stand-ins at
    /// exactly the same measured positions. The geometry is the point; the picture follows.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Deck Gear Kit", fileName = "DeckGearKit")]
    public sealed class DeckGearKitDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (CLAUDE.md §5): type.snake_case.")]
        public string Id = "deckgearkit.deck_loop_kit";

        [Tooltip("The rig this was measured from, for the reader who needs to check a number.")]
        public string SourceRig = "docs/art/rigs/deck-loop-kit/Art/deckGearRig.js";

        [Header("How the kit was BAKED (art facts — not feel knobs)")]
        [Tooltip("Drawn facings per kind. The kit bakes 8; the maths is general so a later re-bake at a " +
                 "different count drops in with no code change.")]
        [Min(1)] public int FacingCount = 8;

        [Tooltip("⚠️ FALSE for this kit, and the kit's own contract says so. Its cells step -45° per dir " +
                 "(CLOCKWISE) where the fleet's registered reference steps +45°. Applying the fleet's " +
                 "counter-clockwise correction here mirrors all eight cells. Carried as data because the " +
                 "next kit may differ and a constant here would silently mirror it.")]
        public bool FacingsAreCounterClockwise = false;

        [Header("The furniture (one entry per kind the kit draws)")]
        [Tooltip("Stations and sheets per kind, transcribed from DeckGear.STATIONS. A kind absent from " +
                 "this list simply cannot be placed — absence is data, not a missing import.")]
        public DeckGearKitEntry[] Entries = Array.Empty<DeckGearKitEntry>();

        /// <summary>The entry for <paramref name="kind"/>, or null when this kit does not carry it.
        /// Linear over a list of five — no allocation, no dictionary to keep in step.</summary>
        public DeckGearKitEntry EntryFor(DeckGearKind kind)
        {
            if (Entries == null) return null;
            for (int i = 0; i < Entries.Length; i++)
                if (Entries[i] != null && Entries[i].Kind == kind) return Entries[i];
            return null;
        }
    }
}
