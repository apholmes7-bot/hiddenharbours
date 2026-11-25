using HiddenHarbours.Core;
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

        [Tooltip("What kind of thing this is when it is put away (Core.StowageRule). Rod = only a rod rack " +
                 "or a locker flagged for rods takes it, never a fish hold, cooler or crate. General = no " +
                 "storage rule of its own. Append-only int values.")]
        public StowageClass Stowage = StowageClass.General;

        [Tooltip("Which of the character rig's HAND-PROP rows holds this tool — 'rodTrail' for the rod; " +
                 "Core.CarryPropKeys names all seven. EMPTY is legal and is the honest answer for the " +
                 "SHOVEL: the rig bakes no spade row, so the carrier keeps its own single offset for it. " +
                 "⚠️ Do not point the shovel at 'gaff' to fill the gap — that row is a 1.55 m pole's " +
                 "grip and a spade is 1.04 m.")]
        public string HandPropKey;

        [Tooltip("This tool in a hand as facet MESHES (ADR 0053): every held state its rig bakes, in the " +
                 "rig's own order — a rod's three tiers, the spade's one state. Which tier shows is the " +
                 "holder's to pick by the tier its own data names, so none is chosen here. Written by " +
                 "the held-prop bake. Drawn on the mesh figure's hand where DrawHeldMeshOnFigure says so.")]
        public HeldPropMeshDef[] HeldMeshes = System.Array.Empty<HeldPropMeshDef>();

        [Tooltip("ADR 0053: while the figure is a MESH and its clip aims this tool on the frame drawn, draw " +
                 "the held mesh (HeldMeshVariant) on the figure's hand socket instead of the tool's sprite. " +
                 "Off keeps the sprite everywhere. While a mesh figure draws him the tool's old sprite never " +
                 "shows (HeldRodMesh.SpriteMayDraw); it is drawn only while his body is a sprite.")]
        public bool DrawHeldMeshOnFigure;

        [Tooltip("ADR 0053 D1: on a mesh-figure clip that aims NO tool (idle, walk, run, a moving angler's " +
                 "gait), draw the held mesh at its rig's own held pose on the mount bone instead of the " +
                 "tool's sprite: the rod he carries, and the rod he holds while the hold pose yields to his " +
                 "gait. Needs DrawHeldMeshOnFigure. Off keeps the sprite on those clips.")]
        public bool DrawCarriedMeshOnFigure;

        [Tooltip("Which of HeldMeshes the figure holds, by the def's Variant: the rod's 'cane' is the tier " +
                 "the rod kit's sprites are wired with today. Empty matches a single-state tool's empty " +
                 "variant.")]
        public string HeldMeshVariant = "";

        [Tooltip("The figure bone the held mesh grips at: Core.HeldPropSockets names the rig's two tool " +
                 "mounts ('tool_R', 'tool_L'). The rigs aim a held tool from the right hand.")]
        public string HeldMeshMountBone = HeldPropSockets.RightHandTool;

        [Tooltip("The aim (the rig's tool pitch, degrees above the horizon) the held mesh is drawn at in his " +
                 "hand on a clip that aims no tool (DrawCarriedMeshOnFigure: idle, walk, run). Carried upright " +
                 "and a little forward so the rod reads in his hand from every heading: the fishing pitch (56) " +
                 "points it along the camera's line of sight from the front, where it reads as a stub.")]
        [Range(-90f, 90f)] public float CarriedPitchDeg = 65f;

        [Tooltip("The carried aim's yaw (the rig's tool yaw, degrees, + toward his right): out from his side so " +
                 "the blank clears his leg and shoulder.")]
        [Range(-180f, 180f)] public float CarriedYawDeg = 35f;

        [Tooltip("On a clip that aims no tool, carry the held mesh in whichever hand is nearer the camera " +
                 "(HeldMeshMountBone or CarriedOffHandMountBone), so the hand holding it is the one you can " +
                 "see. Off keeps it in HeldMeshMountBone at every heading, where facing away from that hand " +
                 "hides the hand and leaves the tool standing behind him. A clip that aims the tool always " +
                 "grips at HeldMeshMountBone: the rig aims a tool from that hand.")]
        public bool CarryInNearerHand;

        [Tooltip("The other hand's tool mount for the carried placement (CarryInNearerHand). In this hand the " +
                 "carried yaw is mirrored, so the tool still angles out from the side it is held on.")]
        public string CarriedOffHandMountBone = HeldPropSockets.LeftHandTool;

        [Tooltip("How much nearer the camera, metres, the other side must be before the carried tool changes " +
                 "hands (CarryInNearerHand), so it never flickers between two hands at about the same depth.")]
        [Min(0f)] public float CarriedHandSwapDepthMetres = 0.05f;

        [Tooltip("The bone whose depth stands for the mount hand's SIDE of him when choosing the nearer hand " +
                 "(CarryInNearerHand): the shoulder, which does not swing. The hands swing through each other " +
                 "on the walk, so their own depth would pass the tool from hand to hand every step. Empty uses " +
                 "HeldMeshMountBone itself.")]
        public string CarriedSideBone = "";

        [Tooltip("The other side's bone for the same choice (CarriedSideBone). Empty uses CarriedOffHandMountBone.")]
        public string CarriedOffSideBone = "";

        [Tooltip("The other hand takes the carried tool only while he faces at least this far away from the " +
                 "camera: his facing's share along the camera's depth, -1 straight at the camera, 0 side-on, " +
                 "1 straight away. Facing the camera both hands show, and the mount hand's tool angles out " +
                 "clear of him; the other hand's would cross his body. -1 lets the nearer hand take it at " +
                 "every heading.")]
        [Range(-1f, 1f)] public float CarriedOffHandFacingAwayMin = -1f;

        [Header("Stowed on his back (mesh figure)")]
        [Tooltip("When he carries this tool but is not using it, draw its held mesh slung on the figure's " +
                 "back mount (StowMountBone) instead of in his hand. 'Not using it' is read from state only: " +
                 "the clip drawn is not one of CarriedInHandClips (rowing, the helm, a carry, swimming), or " +
                 "the other hand holds something, or the tool was tucked away for a catch. A clip that aims " +
                 "the tool (the fishing performance) always puts it in his hand. Needs DrawCarriedMeshOnFigure.")]
        public bool StowOnBack;

        [Tooltip("While he swims (CarryHands.ToolsStowed), keep the slung mesh on his back. Off hides it in the " +
                 "swim band as before. The held mesh has no waterline cut of its own, so a slung tool's lower " +
                 "end can show over his submerged body while he treads water.")]
        public bool StowWhileSwimming = true;

        [Tooltip("The figure bone the stowed mesh rides: the rig's slung-rod socket, fixed on the chest, " +
                 "whose +y is the slung rod's axis (diagonal, up over one shoulder).")]
        public string StowMountBone = "back";

        [Tooltip("Where the grip sits along the slung axis from the back mount, metres: negative puts the " +
                 "grip low on his back so the length of the tool rides across it rather than standing above " +
                 "his head.")]
        public float StowGripAlongMetres = -0.4f;

        [Tooltip("How far the stowed mesh stands off his back, metres, away from the chest, so the blank " +
                 "does not sink into the torso.")]
        public float StowStandOffMetres;

        [Tooltip("Mesh-figure clips on which a carried tool that the clip does not aim stays in his hand " +
                 "(the free-handed gait). On every other clip that aims no tool it is stowed on his back.")]
        public string[] CarriedInHandClips = { "idle", "walk", "run" };

        [Tooltip("Seconds the mesh takes to move between his hand and his back (the draw and the stow). " +
                 "The rig bakes no draw clip, so this is a short blend of the pose; 0 is a clean swap.")]
        [Min(0f)] public float StowBlendSeconds = 0.2f;

        /// <summary>True when <paramref name="clipKey"/> is one of <see cref="CarriedInHandClips"/>.
        /// Allocation-free.</summary>
        public bool CarriesInHandOn(string clipKey)
        {
            if (string.IsNullOrEmpty(clipKey) || CarriedInHandClips == null) return false;
            foreach (string k in CarriedInHandClips)
                if (string.Equals(k, clipKey, System.StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>Resolve a held state by its authored variant. Missing states return null so the
        /// caller can keep its sprite; array order never selects a different tier. A single-state
        /// tool uses the empty variant recorded by its def.</summary>
        public HeldPropMeshDef HeldMeshFor(string variant)
        {
            if (variant == null || HeldMeshes == null) return null;
            foreach (HeldPropMeshDef mesh in HeldMeshes)
                if (mesh != null && string.Equals(mesh.Variant, variant, System.StringComparison.Ordinal))
                    return mesh;
            return null;
        }
    }
}
