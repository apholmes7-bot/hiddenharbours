using UnityEngine;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>A tool you pick up, carry, use and put back</b> — the rod, the clam shovel. One asset per tool,
    /// stable append-only id (rule 2).
    ///
    /// <para><b>Why this is a new Def and not a field on <c>GearOffer</c>.</b> The two answer different
    /// questions and always have — <see cref="PlayerGear"/>'s own remarks draw the line: the gear's
    /// ECONOMIC side (price, the purchase record, the shop's flavour text) is Economy's, and its
    /// CAPABILITY is gameplay-systems'. A <c>GearOffer</c> is a thing on a shelf with a price; a
    /// <see cref="ToolDef"/> is a thing in the world with a weight and a grip. Merging them would put a
    /// price on an object the player found on a beach and a hand-grip on a shop listing.
    /// <see cref="GearId"/> is the link between the two, which is all the coupling either one needs.</para>
    ///
    /// <para><b>Why there is no sprite here.</b> Presentation is wired by the builder that stands the tool
    /// in the world, exactly as the clam hole's sprite is — one place resolves art off disk, and a Def
    /// that hand-carried a sub-sprite reference would be authoring the internalID/spriteID hazard into
    /// YAML for no gain. When art-pipeline bakes the tool rigs (<c>shovelIsoRig.js</c> is in
    /// <c>docs/art/rigs/</c> but not yet in <c>RigCatalog</c>), the directional art lands the way every
    /// other kit's does and this Def does not change.</para>
    ///
    /// <para><b>What is NOT here.</b> No reach and no carry rule — reach is the instance's business
    /// (<see cref="CarriableTool"/>'s serialized tunable, matching <c>CarriableFuelContainer</c>), and the
    /// carry rule is <c>CarryMath</c>'s. No capability logic: what a tool ENABLES is asked as "is this in
    /// your hands", by the gate that cares, through <c>Core.CarriedItem</c>.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Tool", fileName = "Tool")]
    public class ToolDef : ScriptableObject
    {
        /// <summary>The rod's stable tool id. ⚠️ This is the <c>ICarriable.DefId</c> a gate matches on —
        /// NOT <see cref="PlayerGear.RodId"/>, which is the OWNERSHIP record. Holding and owning are
        /// different facts and the two id spaces are deliberately separate.</summary>
        public const string RodId = "tool.rod";

        /// <summary>The clam shovel's stable tool id. See <see cref="RodId"/> on why this is not
        /// <see cref="PlayerGear.ShovelId"/>.</summary>
        public const string ShovelId = "tool.shovel";

        [Tooltip("Stable id, type.snake_case, APPEND-ONLY (tool.rod / tool.shovel). This is what a gate " +
                 "matches on when it asks 'is a shovel in your hands' — the KIND, shared by every " +
                 "instance, never a per-instance id.")]
        public string Id;

        [Tooltip("What the game calls it in a refusal or a pick-up line ('clam shovel').")]
        public string DisplayName;

        [Tooltip("The owned-gear id this tool corresponds to (gear.rod / gear.shovel) — the link to the " +
                 "shop's GearOffer and the save's owned list. Empty means the tool has no ownership " +
                 "record at all, which is legal: a thing you found is still a thing you can hold.")]
        public string GearId;

        [Tooltip("One line for the pick-up message and later the tool rack's label. Greybox copy.")]
        [TextArea(2, 4)]
        public string Flavor;

        [Tooltip("Which of the character rig's HAND-PROP rows holds this tool — 'rodTrail' for the rod; " +
                 "Core.CarryPropKeys names all seven. EMPTY is legal and is the honest answer for the " +
                 "SHOVEL: the rig bakes no spade row, so the carrier keeps its own single offset for it. " +
                 "⚠️ Do not point the shovel at 'gaff' to fill the gap — that row is a 1.55 m pole's " +
                 "grip and a spade is 1.04 m.")]
        public string HandPropKey;
    }
}
