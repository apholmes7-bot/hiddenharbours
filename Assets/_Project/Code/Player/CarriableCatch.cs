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

        /// <summary>What is being held. Only meaningful while <see cref="HasItem"/>.</summary>
        public CatchItem Item => _item;

        /// <summary>True while this actually carries a landed catch.</summary>
        public bool HasItem => _hasItem;

        /// <summary>
        /// Make one, holding the given catch, parented under <paramref name="parent"/>.
        ///
        /// <para>The icon is resolved by species id through <see cref="IconRegistry"/>; a species with no
        /// registered icon yields a null sprite, which draws nothing and is survivable — the clam is still
        /// in your hands and still stacks. An invisible catch is a missing-art problem, not a broken
        /// loop.</para>
        /// </summary>
        public static CarriableCatch Create(in CatchItem item, Transform parent)
        {
            var go = new GameObject($"Catch({item.SpeciesId})");
            if (parent != null) go.transform.SetParent(parent, worldPositionStays: false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = IconRegistry.Get(item.SpeciesId);

            var carriable = go.AddComponent<CarriableCatch>();
            carriable._renderer = sr;
            carriable._item = item;
            carriable._hasItem = true;
            return carriable;
        }

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

        /// <summary>0 — a clam in a fist has no directional art, and the null-safe answer is cell 0.</summary>
        public int BakedFacings => 0;

        /// <inheritdoc/>
        public void ShowFacing(int facingIndex) { }

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
