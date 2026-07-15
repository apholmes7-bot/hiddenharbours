using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// A baited trap/pot, as data (ADR 0003) — the lobster pot and the crab pot of the trap-fishing arc.
    /// One asset = one trap kind. Content is data, not code: add a trap by creating one of these assets,
    /// never by hard-coding a soak time or a catch list. Create via
    /// Assets ▸ Create ▸ Hidden Harbours ▸ Trap, save in Data/Traps. Full loop design:
    /// design/fish-and-content.md §3.5b (set a baited trap → buoy → soak → gaff the buoy to haul).
    ///
    /// <para><b>Scope (trap arc Build 2 — content only).</b> This Def is the trap's <i>data</i>: what it
    /// can catch, what bait it wants, how long and how deep it soaks. The <b>behaviour</b> — placement,
    /// soak timing, haul, the catch resolver that weights the roll off <see cref="AllowedCatchFishIds"/> and
    /// the loaded bait — is Builds 3/4 and is NOT here. There is deliberately <b>no cost field</b>: buying
    /// or building a trap is a separate economy offer (a Shipwright/gear offer owned by economy-sim), not a
    /// property of the trap itself.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Trap", fileName = "Trap")]
    public class TrapDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable, append-only trap id (e.g. \"trap.lobster\"). Never reuse or rename.")]
        public string Id = "trap.lobster";
        public string DisplayName = "Lobster Pot";

        [Header("Art (the owner's painted trap kit drops in here — data, not code)")]
        [Tooltip("The pot DRY — how she reads once picked empty and squared away on the deck (and the trap's " +
                 "identity sprite anywhere one image must stand for the kind). Wood bow-top for the lobster " +
                 "pot, wire mesh for the crab pot.")]
        public Sprite TrapSprite;
        [Tooltip("The surface buoy marking the set trap (what the player gaffs to haul). Ratified canon: buoy " +
                 "COLOUR says WHOSE gear it is — the player's is the YELLOW float. (The world buoy itself is " +
                 "drawn by the Boats-lane presenter off the Core icon key \"buoy.player\"; this slot keeps the " +
                 "trap's own data complete for any per-kind consumer.)")]
        public Sprite BuoySprite;
        [Tooltip("The pot WET — hauled up dripping, rockweed and barnacles still on her (shown on the deck " +
                 "while she still holds animals). Empty = the dry TrapSprite stands in. Append-only art slot " +
                 "for the owner's *Wet.png states.")]
        public Sprite TrapSpriteWet;

        [Header("Splash (the haul-break burst — pooled one-shot FX, data not code)")]
        [Tooltip("The splash flipbook played ONCE at the buoy when this pot breaks the surface at haul-end, " +
                 "and when a fresh set hits the water (T). Sheet cells in order; empty = no splash (the " +
                 "greybox behaviour). Pivot = the artist's surface point, so frame 0 sits on the waterline.")]
        public Sprite[] SplashBurstFrames = System.Array.Empty<Sprite>();
        [Min(1f)]
        [Tooltip("Frames per second the splash burst plays at (the artist's brief: ~14-18; 16 reads right). " +
                 "A feel knob, not code.")]
        public float SplashBurstFps = 16f;

        [Header("Capacity & catch")]
        [Min(1)]
        [Tooltip("How many ANIMALS a fully soaked pot comes up with (the pot's capacity — one hold unit " +
                 "each). The pot FILLS with the soak: one animal the moment she's ready (SoakHours), up " +
                 "to this many by HoursToFullPot. Raise for a bigger pot; 1 = the old single-catch pot.")]
        public int CapacityUnits = 4;
        [Tooltip("The FishSpeciesDef ids this trap can take (explicit allow-list). The Build 3 resolver rolls " +
                 "the catch from these; each must name a real FishSpeciesDef (content validation checks this).")]
        public string[] AllowedCatchFishIds = { "fish.lobster" };
        [Tooltip("The BaitDef id this trap must be baited with. Names a real BaitDef (validated). The bait's " +
                 "FavorsSpeciesIds soft-weight the Build 3 catch roll.")]
        public string RequiredBaitId = "bait.herring";

        [Header("Deck work (Build 7 — the post-haul pick/sort/band/bait minigame)")]
        [Tooltip("The deck-work ruleset this pot uses once hauled aboard (pick / sort / band / bait — " +
                 "DeckWorkDef, data not code). Append-only opt-in: leave EMPTY and the haul lands the " +
                 "catch instantly, exactly the pre-Build-7 behaviour (older content and tests unchanged).")]
        public DeckWorkDef DeckWork;

        [Header("Soak (greybox placeholders — flag for economy-sim / gameplay tuning)")]
        [Min(0f)]
        [Tooltip("How long the trap must soak before it's worth hauling, in in-game hours. A pot hauled " +
                 "before this comes up EMPTY; at this mark she's ready with her FIRST animal. Greybox.")]
        public float SoakHours = 12f;
        [Min(0f)]
        [Tooltip("How long (in-game hours, from placement) until the pot is expected FULL — the soak-to-" +
                 "fill curve's far end: at SoakHours she holds 1 animal, by here she holds CapacityUnits, " +
                 "each further slot filling on its own deterministic per-pot roll between the two marks. " +
                 "Longer = a slower fill (leave her down to earn the full pot); anything ≤ SoakHours = " +
                 "full the moment she's ready. The owner's main multi-catch pacing knob.")]
        public float HoursToFullPot = 36f;
        [Tooltip("Shallowest depth (m) the trap fishes well. Below this the set is too shoal. Greybox.")]
        public float MinSoakDepthMeters = 3f;
        [Tooltip("Deepest depth (m) the trap fishes well. Beyond this it's out of the species' band. Greybox.")]
        public float MaxSoakDepthMeters = 40f;
    }
}
