using UnityEngine;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// A bait, as data (ADR 0003) — herring, mackerel, fish scrap. One asset = one bait. Content is data,
    /// not code: add a bait by creating one of these assets, never by hard-coding a price or a preference.
    /// Create via Assets ▸ Create ▸ Hidden Harbours ▸ Bait, save in Data/Bait. Trap-fishing design:
    /// design/fish-and-content.md §3.5b.
    ///
    /// <para><b>Lives in Economy, not Fishing, on purpose.</b> A future bait shop is economy's, and the
    /// asmdefs run one way — <c>HiddenHarbours.Fishing</c> references <c>HiddenHarbours.Economy</c>, never
    /// the reverse. So a <see cref="TrapDef"/> in Fishing can refer to a bait by its stable <c>Id</c>
    /// (a string), and both a trap and a bait-shop offer resolve that id — with no backwards module
    /// dependency. The catch resolver (trap arc Build 3) reads <see cref="FavorsSpeciesIds"/> to
    /// soft-weight which species a baited trap lands.</para>
    ///
    /// <para><b>Price is a greybox placeholder</b> — flagged for economy-sim to tune against the market.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Bait", fileName = "Bait")]
    public class BaitDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable, append-only bait id (e.g. \"bait.herring\"). A trap names this id as its " +
                 "RequiredBaitId. Never reuse or rename.")]
        public string Id = "bait.herring";
        public string DisplayName = "Herring";
        [TextArea] public string Flavor = "Oily, cheap, and irresistible to a lobster. The pot-fisher's staple.";

        [Header("Cost (greybox placeholder — flag for economy-sim tuning)")]
        [Min(0)]
        [Tooltip("Price in ₲ at a neutral market. PLACEHOLDER — economy-sim owns the real balance.")]
        public int Price = 3;

        [Header("What it draws")]
        [Tooltip("Stable FishSpeciesDef ids this bait favours (each must name a real FishSpeciesDef — " +
                 "content validation checks this). The Build 3 resolver soft-weights the catch off this: a " +
                 "trap baited with something a species favours is likelier to land it.")]
        public string[] FavorsSpeciesIds = { "fish.lobster" };
    }
}
