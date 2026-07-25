using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// A piece of TACKLE — what's tied on the end of the line, as data (ADR 0003). One asset = one
    /// tackle. A cod jig, a string of mackerel feathers, a spoon: the thing the rod <i>works</i>, as
    /// distinct from bait, which the fish <i>eats</i> (design/rod-fishing-v2-brainstorm.md §6.2).
    ///
    /// <para><b>Lives in Fishing, sold from Economy — the TrapDef/PotOffer precedent exactly.</b> The
    /// CAPABILITY (which lure presentation it makes, which fish it draws) is gameplay and belongs here;
    /// the PURCHASE (price, where it's stocked) is economy's <c>TackleOffer</c>, which names this def by
    /// its stable string id only. The asmdefs run one way — Fishing → Economy, never back — so this
    /// split is what keeps a shop able to sell a thing it cannot reference (rule 4).</para>
    ///
    /// <para><b>Why tackle matters, per species.</b> The realism is the mechanic: a mackerel is a
    /// flash-chaser that will hit feathers and barely look at bait, a haddock is fussy and wants
    /// something real on the hook, a cod is a glutton that takes nearly anything, a pollock is a
    /// predator that chases a worked lure. So "what's tied on" is a genuine targeting decision, the
    /// tackle twin of the depth game — and, like depth, it is a soft WEIGHT on the catch roll, never a
    /// filter: the wrong lure catches less, it never catches nothing.</para>
    ///
    /// <para><b>Durability is deliberately absent.</b> Tackle is presence-owned (you have a spoon or you
    /// don't), the <c>GearOffer</c> shape rather than the counted <c>PotOffer</c> one. The owner's
    /// "a blow can cost you your rig" ruling (2026-07-25) would land HERE as a lost-tackle roll when the
    /// line parts — the seam is noted so whoever builds it knows this is the thing that gets lost.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Tackle", fileName = "Tackle")]
    public class TackleDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable, append-only tackle id (e.g. \"tackle.cod_jig\"). A TackleOffer names this id " +
                 "to sell it, and the save records ownership under it. Never reuse or rename (rule 2).")]
        public string Id = "tackle.cod_jig";
        public string DisplayName = "Cod Jig";
        [TextArea]
        public string Flavor = "A hand-length bar of leaded metal. Work it off the bottom and let it flutter.";

        [Header("What it is")]
        [Tooltip("Which lure PRESENTATION this tackle makes — the vocabulary a species' FavoredLures " +
                 "mask is tested against. One tag per tackle: a jig is a spoon-class flutter, a string " +
                 "of feathers is Feather, and so on.")]
        public LureTag Lure = LureTag.Spoon;

        [Tooltip("How this tackle wants to be WORKED. A lure only fishes if you work it, and each kind " +
                 "wants its own tempo and stroke (the numbers live in GameConfig.Jigging). None = it " +
                 "fishes itself and a still rod is fine — which is what BAIT does.")]
        public JigStyle JigStyle = JigStyle.LiftAndDrop;

        [Header("What it draws")]
        [Tooltip("Stable FishSpeciesDef ids this tackle is especially good for. Belt-and-braces beside " +
                 "the species' own FavoredLures mask: EITHER match earns the boost, so a content author " +
                 "can express the pairing from whichever side reads better. Leave empty to rely purely " +
                 "on the lure tag (content validation checks every id names a real species).")]
        public string[] FavorsSpeciesIds = new string[0];
    }
}
