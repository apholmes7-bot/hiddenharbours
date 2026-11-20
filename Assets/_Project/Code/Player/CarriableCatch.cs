using UnityEngine;
using UnityEngine.Rendering;
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

        // ---- the contrast line on a small fish (fish art pass 2, section 2.3) ----------------------
        // Darker copies of the held sprite one sprite pixel to the side, behind it, so a fish drawn a
        // dozen pixels long reads against his clothes and the deck. Made once at pickup, only for a fish
        // drawn no longer than GameConfig.HeldFishKeylineMaxLengthPx; empty otherwise. The sorting group
        // keeps the copies and the fish one unit in the body's band, so a copy can never sort under the
        // body it is meant to stand out from.
        private SpriteRenderer[] _keylines = System.Array.Empty<SpriteRenderer>();
        private SortingGroup _group;

        /// <summary>What is being held. Only meaningful while <see cref="HasItem"/>.</summary>
        public CatchItem Item => _item;

        /// <summary>True while this actually carries a landed catch.</summary>
        public bool HasItem => _hasItem;

        /// <summary>The catch art kind this draws as ('cod_sm', 'clam'), or null with no art table. For
        /// tests / tooling.</summary>
        public string ArtKind => _kind;

        /// <summary>How many contrast-line copies this catch draws behind it (0 for a fish drawn long enough
        /// to read on its own, a shellfish, or a catch on the icon fallback). For tests / tooling.</summary>
        public int KeylineCount => _keylines.Length;

        /// <summary>One contrast-line copy, by index, or null. For tests / tooling.</summary>
        public SpriteRenderer Keyline(int i) => i >= 0 && i < _keylines.Length ? _keylines[i] : null;

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
            // SIZED: the size rung of the species closest to this catch's weight, so a 2 kg cod is held
            // as the small cod drawing and a 12 kg one as the large. A rung with no held sheet falls
            // back to the species' plain kind rather than to the icon.
            string kind = art != null ? art.SizedKindFor(item.SpeciesId, item.WeightKg) : null;
            if (art != null && art.HeldFacingsFor(kind) == 0) kind = art.KindFor(item.SpeciesId);
            carriable._kind = kind;
            carriable._facings = art != null ? art.HeldFacingsFor(carriable._kind) : 0;

            // Draw something NOW rather than waiting for the first ShowFacing: a carrier only turns
            // what it knows turns, so a one-facing handful would otherwise never be told to draw.
            sr.sprite = carriable.HeldSprite(0) ?? IconRegistry.Get(item.SpeciesId);
            carriable.AddKeylines(art != null ? art.HeldLengthPxFor(carriable._kind) : 0);
            return carriable;
        }

        /// <summary>
        /// <b>The contrast line on a small fish.</b> A fish whose held drawing is measured at
        /// <see cref="GameServices.HeldFishKeylineMaxLengthPx"/> or shorter gets one darker copy of itself
        /// behind it per <see cref="GameServices.HeldFishKeylineOffsetsPx"/> entry, moved that many sprite
        /// pixels, so a thin dark line runs along its edge. The copy is the fish's own colours multiplied by
        /// <see cref="GameServices.HeldFishKeylineShade"/>: a step down its own ramp, never a colour of its
        /// own, and never the interact outline (no lift, no grown silhouette). An unmeasured drawing (0), a
        /// catch with no held art, or a line turned off in the config draws nothing extra.
        /// </summary>
        private void AddKeylines(int drawnLengthPx)
        {
            float maxPx = GameServices.HeldFishKeylineMaxLengthPx;
            if (_facings <= 0 || drawnLengthPx <= 0 || maxPx <= 0f || drawnLengthPx > maxPx) return;
            Vector2[] offsets = GameServices.HeldFishKeylineOffsetsPx;
            Sprite sprite = _renderer != null ? _renderer.sprite : null;
            if (offsets == null || offsets.Length == 0 || sprite == null || sprite.pixelsPerUnit <= 0f) return;

            float shade = Mathf.Clamp01(GameServices.HeldFishKeylineShade);
            var tint = new Color(shade, shade, shade, 1f);
            _keylines = new SpriteRenderer[offsets.Length];
            for (int i = 0; i < offsets.Length; i++)
            {
                var go = new GameObject("Keyline");
                go.transform.SetParent(transform, worldPositionStays: false);
                go.transform.localPosition = offsets[i] / sprite.pixelsPerUnit;
                var line = go.AddComponent<SpriteRenderer>();
                line.sprite = sprite;
                line.color = tint;
                _keylines[i] = line;
            }
            _group = gameObject.AddComponent<SortingGroup>();
            RideSortingBand(_renderer.sortingLayerID, _renderer.sortingOrder);
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
            if (s == null) return;                     // never blank a good sprite with a missing cell
            _renderer.sprite = s;
            for (int i = 0; i < _keylines.Length; i++)
                if (_keylines[i] != null) _keylines[i].sprite = s;
        }

        /// <inheritdoc/>
        public void RideSortingBand(int sortingLayerId, int sortingOrder)
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null) return;
            _renderer.sortingLayerID = sortingLayerId;
            _renderer.sortingOrder = sortingOrder;
            if (_group == null) return;
            // The group takes the body's band; inside it the copies draw one step under the fish.
            _group.sortingLayerID = sortingLayerId;
            _group.sortingOrder = sortingOrder;
            for (int i = 0; i < _keylines.Length; i++)
            {
                if (_keylines[i] == null) continue;
                _keylines[i].sortingLayerID = sortingLayerId;
                _keylines[i].sortingOrder = sortingOrder - 1;
            }
        }

        /// <summary>Nothing to remember — a catch has no Y-sort of its own to stand down (it never lies on
        /// the ground) and no hands to bind to (only one thing ever creates it).</summary>
        public void OnLifted(ICarrier carrier) { }

        /// <inheritdoc/>
        public void OnPlaced() { }
    }
}
