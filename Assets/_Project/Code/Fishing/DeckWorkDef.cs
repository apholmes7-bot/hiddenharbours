using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>Which greybox silhouette a deck animal wears until the owner's art lands. The rock-crab
    /// sprite slot is empty project-wide, so BOTH shapes ship as simple code-built silhouettes — a rule's
    /// <see cref="SpeciesDeckRule.AnimalSprite"/> replaces the silhouette with no code change.</summary>
    public enum DeckAnimalShape
    {
        /// <summary>Long body, tail fan, two fat claws — the lobster read.</summary>
        Lobster = 0,
        /// <summary>Wide oval, legs out both sides — the crab read.</summary>
        Crab = 1,
    }

    /// <summary>
    /// The post-haul <b>deck-work</b> rules, as data (ADR 0003) — the owner's pick / sort / band / bait
    /// minigame that starts when a hauled pot lands on the deck (trap-fishing arc Build 7). One asset =
    /// one ruleset; a <see cref="TrapDef"/> opts in by referencing one (a trap without one keeps the old
    /// instant-land haul). Create via Assets ▸ Create ▸ Hidden Harbours ▸ Deck Work; lives in Data/Traps.
    ///
    /// <para><b>Everything the owner might tune is here (rules 2+6).</b> The honest-fishery sort rules
    /// (legal minimum size, berried hens go back) and the size distributions are per-species DATA in
    /// <see cref="SpeciesRules"/> — never code. The feel numbers (hold lengths, nip chances, splash arc,
    /// deck layout) are fields with tooltips. The pot's wet/dry sprites are slots the owner's painted art
    /// drops into with no code change (greybox falls back to the TrapDef's sprite, then a code-built box).</para>
    ///
    /// <para><b>Determinism note (rule 5).</b> Nothing here rolls anything — these are the parameters the
    /// pure <see cref="DeckWork"/> derivations read. Per-animal size / berried / nip streams are hashed
    /// off the SAME seed lineage as the trap's catch (worldSeed + instanceId + placement time), never off
    /// Time or the wave field.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Deck Work", fileName = "DeckWork")]
    public class DeckWorkDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable, append-only id (e.g. \"deckwork.pot\"). Never reuse or rename.")]
        public string Id = "deckwork.pot";
        public string DisplayName = "Pot deck work";

        [Header("Pot art (owner's painted wet/dry states drop in here — no code change)")]
        [Tooltip("The pot as it lands on deck, dripping (shown while she still holds animals). Empty = " +
                 "the TrapDef's TrapSprite, else a code-built greybox box.")]
        public Sprite PotSpriteWet;
        [Tooltip("The pot dried off on deck (shown once she's been picked empty). Empty = same fallback " +
                 "as the wet slot.")]
        public Sprite PotSpriteDry;

        [Header("Working reach (walk-to, diegetic — no menus)")]
        [Min(0.1f)]
        [Tooltip("How close (m) the deck-walking player must stand to the pot / a keeper to work it. " +
                 "Forgiving on a small deck; tighten it and you must really stand over the work.")]
        public float WorkReachMeters = 1.2f;

        [Header("The grab (pick an animal out — HOLD, resolve on release)")]
        [Min(0.01f)]
        [Tooltip("Shortest hold (s) that counts as a grab at all — releases quicker than this do nothing " +
                 "(no attempt, no nip roll).")]
        public float QuickGrabSeconds = 0.15f;
        [Min(0.05f)]
        [Tooltip("Hold length (s) that reads as a FULL, careful grab — the care read: hold this long " +
                 "before releasing and the nip chance eases to its careful floor. Between quick and full, " +
                 "care scales linearly.")]
        public float FullGrabSeconds = 0.9f;
        [Range(0f, 1f)]
        [Tooltip("Nip chance for a RUSHED grab (released right at the quick-grab threshold) — the P5 " +
                 "teeth. A nip costs only time: recoil, the animal stays in the pot, try again.")]
        public float NipChanceRushed01 = 0.55f;
        [Range(0f, 1f)]
        [Tooltip("Nip chance for a FULL, careful grab — the reward for patience. Keep a little above 0 " +
                 "so even a careful hand gets caught now and then (cozy, but with teeth).")]
        public float NipChanceCareful01 = 0.06f;
        [Min(0f)]
        [Tooltip("How long (s) a nip locks the hands out — the recoil beat before the next try.")]
        public float NipRecoilSeconds = 0.8f;
        [Min(0f)]
        [Tooltip("How far (m) the grabbed animal visibly LIFTS out of the pot as the hold matures — the " +
                 "diegetic care read (no meter): a full lift means a full, safe grab. Visual only.")]
        public float GrabLiftMeters = 0.35f;

        [Header("Banding a keeper (second short hold — a keeper only counts once banded)")]
        [Min(0.05f)]
        [Tooltip("How long (s) the banding hold takes. It completes at this mark (no care roll — banding " +
                 "is deft, not dangerous) and the keeper lands in the hold, sellable.")]
        public float BandSeconds = 0.7f;

        [Header("Re-baiting the emptied pot (physical deck work; consumes one bait from stock)")]
        [Min(0.05f)]
        [Tooltip("How long (s) the baiting hold takes. Completing it consumes ONE of the trap's required " +
                 "bait from the locker (SaveData.BaitStock) and the pot reads ready to set (T).")]
        public float BaitSeconds = 1.0f;

        [Header("Splash-out (shorts and berried hens go back over the side)")]
        [Min(0.05f)]
        [Tooltip("How long (s) the little over-the-side arc plays. Visual only — the sort outcome is " +
                 "already decided.")]
        public float SplashOutSeconds = 0.7f;
        [Min(0f)]
        [Tooltip("How far (m) the returned animal arcs from the deck to the water. Visual only.")]
        public float SplashOutDistanceMeters = 1.6f;

        [Header("Deck layout (where the work sits on the boat — world-axis offsets from the boat root)")]
        [Tooltip("Where the hauled pot lands on the deck, as an offset (m) from the boat's position. Keep " +
                 "its magnitude inside the deck's shortest half-extent so it reads on the hull at every " +
                 "drawn heading (the snap-directional deck turns; a small offset stays aboard).")]
        public Vector2 PotDeckOffset = new Vector2(0f, 0.55f);
        [Tooltip("Where the FIRST picked keeper waits for banding, as an offset (m) from the pot.")]
        public Vector2 KeeperRowOffset = new Vector2(0.45f, -0.25f);
        [Min(0f)]
        [Tooltip("Spacing (m) between waiting keepers along the row.")]
        public float KeeperSpacingMeters = 0.3f;

        [Header("Species sort rules (the honest fishery — data, not code)")]
        [Tooltip("One rule per species this pot can land. An animal with no rule is always a keeper " +
                 "(nothing gates it back) — content validation requires a rule for every species the " +
                 "TrapDefs that use this ruleset can catch.")]
        public SpeciesDeckRule[] SpeciesRules = System.Array.Empty<SpeciesDeckRule>();

        /// <summary>Find the sort rule for a species id (ordinal-ignore-case, the id convention).
        /// Returns false when the species has no rule — the caller treats it as an always-keeper.</summary>
        public bool TryGetRule(string speciesId, out SpeciesDeckRule rule)
        {
            if (SpeciesRules != null && !string.IsNullOrEmpty(speciesId))
            {
                for (int i = 0; i < SpeciesRules.Length; i++)
                {
                    if (string.Equals(SpeciesRules[i].SpeciesId, speciesId, System.StringComparison.OrdinalIgnoreCase))
                    {
                        rule = SpeciesRules[i];
                        return true;
                    }
                }
            }
            rule = default;
            return false;
        }
    }

    /// <summary>
    /// The per-species deck-sort rule: the legal gauge and the deterministic size/berried distributions.
    /// A row of a <see cref="DeckWorkDef"/> (data, not code — tune the fishery here).
    /// </summary>
    [System.Serializable]
    public struct SpeciesDeckRule
    {
        [Tooltip("The FishSpeciesDef id this rule applies to (e.g. \"fish.lobster\").")]
        public string SpeciesId;

        [Tooltip("The legal minimum size (mm — carapace for lobster, shell width for crab). Anything " +
                 "under goes back over the side, value zero (the honest-fishery read).")]
        public float MinKeepSizeMm;

        [Tooltip("Smallest size (mm) the deterministic per-animal size roll can produce.")]
        public float SizeMinMm;

        [Tooltip("Largest size (mm) the roll can produce. Keep the window straddling MinKeepSizeMm so " +
                 "both shorts and keepers actually occur.")]
        public float SizeMaxMm;

        [Tooltip("Can this species come up BERRIED (carrying eggs)? Lobster hens do; a berried animal " +
                 "always goes back regardless of size.")]
        public bool CanBeBerried;

        [Range(0f, 1f)]
        [Tooltip("Chance an animal of this species is berried (only read when CanBeBerried). " +
                 "Deterministic per animal — same trap, same haul, same hen.")]
        public float BerriedChance01;

        [Tooltip("Greybox silhouette shape while no sprite is authored (lobster vs crab must read apart).")]
        public DeckAnimalShape Shape;

        [Tooltip("The owner's painted animal sprite — drops in over the code-built silhouette, no code " +
                 "change. Empty = silhouette.")]
        public Sprite AnimalSprite;
    }
}
