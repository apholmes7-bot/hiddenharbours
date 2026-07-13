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

        [Header("Art (greybox placeholders until art-pipeline authors trap/buoy art)")]
        [Tooltip("The trap/pot sprite (the thing that sits on the seabed). Placeholder for now — a dedicated " +
                 "trap sprite is an art-pipeline handoff.")]
        public Sprite TrapSprite;
        [Tooltip("The surface buoy marking the set trap (what the player gaffs to haul). Lobster ≠ crab.")]
        public Sprite BuoySprite;

        [Header("Capacity & catch")]
        [Min(1)]
        [Tooltip("How many hold units of catch a full soak yields (the pot's capacity). Greybox placeholder.")]
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
        [Tooltip("How long the trap must soak before it's worth hauling, in in-game hours. Greybox.")]
        public float SoakHours = 12f;
        [Tooltip("Shallowest depth (m) the trap fishes well. Below this the set is too shoal. Greybox.")]
        public float MinSoakDepthMeters = 3f;
        [Tooltip("Deepest depth (m) the trap fishes well. Beyond this it's out of the species' band. Greybox.")]
        public float MaxSoakDepthMeters = 40f;
    }
}
