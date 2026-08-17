using UnityEngine;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// One place the player can live, as data (ADR 0003) — the id its deed is recorded under, what it
    /// costs, and whether it costs anything at all. The <see cref="LicenseDef"/> pattern applied to a
    /// home: content is data, never a price hard-coded in a builder (rules 2 and 6).
    /// Create via Assets ▸ Create ▸ Hidden Harbours ▸ Home, save in <c>Data/Homes</c>.
    ///
    /// <para><b>⭐ THIS ASSET IS WHERE THE OWNER'S TWO OPEN DECISIONS LIVE.</b> Both were shipped on
    /// documented defaults and both are one field on one asset, so flipping either is an owner action
    /// with no code change and no rebuild of anything but the region:</para>
    /// <list type="number">
    ///   <item><see cref="Acquisition"/> — <b>starter home vs purchasable</b>. Default
    ///   <see cref="Mode.Purchasable"/>.</item>
    ///   <item><see cref="Variant"/> — <b>Bantam vs Clipper</b>. Default the Clipper, the one you can
    ///   stand up in. Every number the lot places by (footprint, spacing, clearing, doorstep) re-derives
    ///   from the chosen variant's own sidecar, so this really is one field.</item>
    /// </list>
    ///
    /// <para><b>The ceiling is deliberate.</b> No comfort score, no decor slots, no storage, no upgrade
    /// path — <c>design/progression-and-housing.md</c> §4 describes all of those and they are the shape
    /// this grows into when housing's phase arrives (rule 8). What ships is: a home has an id, a price,
    /// and a way of being acquired. <see cref="Core.HomeDeeds"/> holds the answer to "is it yours".</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Home", fileName = "Home")]
    public class HomeDef : ScriptableObject
    {
        /// <summary>How a home comes to be yours.</summary>
        public enum Mode
        {
            /// <summary>You buy it: walk up with <see cref="Price"/> in your purse and the deed is
            /// yours. The default, and the one that leaves the St Peters opening arc (#288) alone.</summary>
            Purchasable = 0,

            /// <summary>It is yours from the first morning — the deed is granted when the region is
            /// placed and the door never asks for money.
            ///
            /// <para><b>⚠ FLIPPING TO THIS DOES NOT MOVE THE OPENING SPAWN, on purpose.</b> Where the
            /// player wakes up is the opening arc's business and rewiring it unasked is out of scope.
            /// Setting this makes the camper OWNED from the start; if the owner also wants to WAKE UP
            /// there, that is a second, separate decision on <c>StPetersBuilder.StartSpawnPos</c>.</para></summary>
            StarterHome = 1,
        }

        [Header("Identity")]
        [Tooltip("Stable, append-only home id (e.g. \"home.ginny_lot_camper\"). The deed is persisted " +
                 "under this id — never reuse or rename it, or a saved game forgets it owns the place.")]
        public string Id = "home.ginny_lot_camper";

        [Tooltip("Player-facing name. Flavour only; the id is canonical.")]
        public string DisplayName = "The camper on Ginny's lot";

        [TextArea]
        public string Flavor = "A second-hand travel trailer, parked on the back of your aunt's land. " +
                               "Not much, but the door locks and the rain stays outside.";

        [Header("Acquisition")]
        [Tooltip("OWNER DECISION 1. Purchasable (default): you buy it at the door for the price below. " +
                 "StarterHome: it is yours from the first morning and the price is ignored. Either way " +
                 "the camper stands on its lot — only the deed changes.")]
        public Mode Acquisition = Mode.Purchasable;

        [Min(0)]
        [Tooltip("Price in ₲. Ignored when Acquisition is StarterHome. The economy-owned tunable — no " +
                 "magic number in code (rule 6).")]
        public int Price = 900;

        [Header("Which camper")]
        [Tooltip("OWNER DECISION 2. The camper rig's own variant key: \"clipper\" (26 ft, 2.00 m — you " +
                 "stand up in it; the default) or \"bantam\" (16 ft, 1.80 m — you stoop). The lot reads " +
                 "the chosen variant's gameplay sidecar for every dimension it places by, so this one " +
                 "field moves the footprint, the spacing, the clearing and the doorstep together.")]
        public string Variant = "clipper";
    }
}
