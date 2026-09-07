using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Art;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>One clam, or one fish, in the fisher's hands</b> — the owner's third ruling of 2026-08-13: "a
    /// clam can be in hand but needs to be placed in a container to stack — same with fish or other
    /// items."
    ///
    /// <para><b>What this replaces.</b> A dug clam used to go straight from the sand into an abstract
    /// 20-unit list. Now it goes into your hand, and it is not stowed until you put it somewhere that
    /// stacks — the <see cref="ClamBucket"/> first. One clam at a time, because you have one pair of
    /// hands, which is the whole physical premise (diegetic-ui-and-inventory.md §4.1).</para>
    ///
    /// <para><b>The item is CARRIED here, not copied.</b> The <see cref="CatchItem"/> — species, weight,
    /// value, and critically its <c>Freshness</c> stamp — is taken whole at the moment of landing and
    /// handed on whole when it stacks. Nothing is re-rolled and no clock is re-read in between, so a clam
    /// that sat in your hand while you walked the bar is exactly as old as the game says it is. Re-stamping
    /// on the stack would quietly reset the spoil clock every time you filled a bucket.</para>
    ///
    /// <para><b>Spawned, not authored.</b> Unlike a fuel can or a tool, nothing places one of these in a
    /// scene: <see cref="CarryHands"/> makes one when a catch lands and destroys it when the catch stacks.
    /// It therefore carries no Def and no builder wiring — <see cref="Create"/> is the only way one comes
    /// into existence, and the icon is resolved by species id through Core's <see cref="IconRegistry"/>
    /// (the same indirection the HUD's catch card uses, so no lane references a species Def to draw
    /// one).</para>
    ///
    /// <para><b>Why it is not an <c>IInteractable</c>.</b> A carriable normally also answers the press, so
    /// E puts it down. A catch deliberately does not: putting a clam on the sand is not a thing the ruling
    /// asks for and would be an item with nowhere to live (no world persistence — §4.3, its own future
    /// ADR). The only thing you can do with a catch in your hands is stack it, and the CONTAINER is the
    /// candidate that offers that. Hands are freed by stacking, not by dropping.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CarriableCatch : MonoBehaviour, ICarriable, ICarryAnchored, IHandLoad
    {
        [SerializeField] private SpriteRenderer _renderer;

        private CatchItem _item;
        private bool _hasItem;

        // ---- the art, resolved once at pickup ---------------------------------------------------
        private CatchItemLibrary _art;
        private string _kind;
        private int _facings;

        /// <summary>What is being held. Only meaningful while <see cref="HasItem"/>.</summary>
        public CatchItem Item => _item;

        /// <summary>True while this actually carries a landed catch.</summary>
        public bool HasItem => _hasItem;

        /// <summary>
        /// Make one, holding the given catch, parented under <paramref name="parent"/>.
        ///
        /// <para><b>The art is the animal, when there is one.</b> <paramref name="art"/> resolves the
        /// catch's species to a visual kind and hands back the rig's HELD pose — a lobster gripped
        /// across the back, a clutch of clams in a fist — baked around the grip so it pins to a hand
        /// anchor. Crustaceans carry eight facings and turn with the carrier; a handful has one and
        /// does not, which is a fact about the rig rather than a decision made here.</para>
        ///
        /// <para><b>The icon is the fallback, and it is a real one.</b> With no library wired, or for a
        /// species whose kind bakes no held pose, this falls back to <see cref="IconRegistry"/> exactly
        /// as it always did. A species with neither yields a null sprite, which draws nothing and is
        /// survivable — the clam is still in your hands and still stacks. An invisible catch is a
        /// missing-art problem, not a broken loop.</para>
        /// </summary>
        public static CarriableCatch Create(in CatchItem item, Transform parent,
                                            CatchItemLibrary art = null)
        {
            var go = new GameObject($"Catch({item.SpeciesId})");
            if (parent != null) go.transform.SetParent(parent, worldPositionStays: false);

            var sr = go.AddComponent<SpriteRenderer>();

            var carriable = go.AddComponent<CarriableCatch>();
            carriable._renderer = sr;
            carriable._item = item;
            carriable._hasItem = true;
            carriable._art = art;
            carriable._kind = art != null ? art.KindFor(item.SpeciesId) : null;
            carriable._facings = art != null ? art.HeldFacingsFor(carriable._kind) : 0;

            // Draw something NOW rather than waiting for the first ShowFacing: a carrier only turns
            // what it knows turns, so a one-facing handful would otherwise never be told to draw.
            sr.sprite = carriable.HeldSprite(0) ?? IconRegistry.Get(item.SpeciesId);
            return carriable;
        }

        /// <summary>This kind's held sprite at one facing, or null when it bakes none.</summary>
        private Sprite HeldSprite(int facing)
            // Frame 0 — the still hold. The rig animates the held pose over two frames and both are
            // wired in the library, so a presenter that wants the animal breathing in the hand can
            // flip them with no data change; a carried object does not earn an Update() for it here.
            => _art != null && _facings > 0 ? _art.HeldSprite(_kind, facing, 0) : null;

        /// <summary>
        /// Hand the catch on and empty this. Returns the item that was held.
        ///
        /// <para>Called by whatever stacked it, immediately before this object is destroyed. Emptying
        /// FIRST is deliberate: for the instant between the container accepting the item and this object
        /// going away, the hands must not still be reporting a catch they no longer have.</para>
        /// </summary>
        public CatchItem Take()
        {
            _hasItem = false;
            return _item;
        }

        // ---- ICarriable -----------------------------------------------------------------------------

        /// <summary>The KIND — the species id (<c>fish.soft_shell_clam</c>), so a gate can ask "is a clam
        /// in your hands" with exactly the machinery that asks it of a shovel.</summary>
        public string DefId => _item.SpeciesId;

        /// <summary>
        /// A CATCH, for the hand-slot rule (<see cref="HandSlots"/>) — the one classification Core cannot
        /// work out for itself, because this class lives in the Player lane where Core cannot see it.
        ///
        /// <para>It is what makes "one catch at a time" and "a fish in one hand, the rod in the other"
        /// two different rules rather than one confused one: without it a landed clam would be classified
        /// a <see cref="HandLoad.Tool"/> and would refuse to share a pair of hands with the shovel that
        /// dug it.</para>
        /// </summary>
        // Fully qualified deliberately: the property and the enum share a name, so the bare
        // form leans on C#'s "Color Color" resolution — legal, and one rename away from being
        // a confusing error message instead.
        public HandLoad HandLoad => HiddenHarbours.Core.HandLoad.Catch;

        /// <summary>A landed catch is always liftable — it is already in your hands by the time this
        /// exists.</summary>
        public bool IsCarriable => true;

        /// <summary>
        /// Which hand-prop row holds it: a handful of shellfish, or a fish by the gill
        /// (<see cref="CarryPropKeys.ForCatch"/>). Derived from the category the content already stamps
        /// on the item, so a new species holds correctly the day it lands with no wiring.
        ///
        /// <para>Null once the catch has been handed on — <see cref="Take"/> empties this object a beat
        /// before it is destroyed, and for that beat it must not still claim to be a fish.</para>
        /// </summary>
        public string HandPropKey => _hasItem ? CarryPropKeys.ForCatch(_item.Category) : null;

        /// <inheritdoc/>
        public Transform Transform => transform;

        /// <summary>
        /// How many facings this catch's held art turns through: 8 for an animal the rig lofts as a
        /// solid (a lobster, a rock crab), 1 for a handful of shellfish the rig draws with no camera,
        /// and 0 when there is no held art at all — which is still the honest answer for a species on
        /// the icon fallback, and keeps the carrier from asking it to turn.
        /// </summary>
        public int BakedFacings => _facings;

        /// <inheritdoc/>
        public void ShowFacing(int facingIndex)
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null) return;
            Sprite s = HeldSprite(facingIndex);
            if (s != null) _renderer.sprite = s;      // never blank a good sprite with a missing cell
        }

        /// <inheritdoc/>
        public void RideSortingBand(int sortingLayerId, int sortingOrder)
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null) return;
            _renderer.sortingLayerID = sortingLayerId;
            _renderer.sortingOrder = sortingOrder;
        }

        /// <summary>Nothing to remember — a catch has no Y-sort of its own to stand down (it never lies on
        /// the ground) and no hands to bind to (only one thing ever creates it).</summary>
        public void OnLifted(ICarrier carrier) { }

        /// <inheritdoc/>
        public void OnPlaced() { }
    }
}
