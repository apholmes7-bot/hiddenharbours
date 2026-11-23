using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>One good on a store shelf that the player buys where it sits</b> (buying at the shelf, slice 1;
    /// the owner's ruling: the player walks up to the item, its price appears, and accepting buys it). An
    /// <see cref="IInteractable"/> at a <see cref="ShopShelfSlot"/> with a
    /// <see cref="ShopShelfSlot.ListingId"/>: its label is the good's name and price in the one interaction
    /// bubble (several goods in reach open the chooser), and the press buys ONE through the store's Core
    /// <see cref="IShelfTill"/>. The till takes the money, puts the good where that kind of good goes and
    /// takes one item off the shelf with its <see cref="IslandShelvesChanged"/>; this class never names the
    /// Economy module (rule 4).
    ///
    /// <para><b>What the player hears.</b> Every outcome is a line in the player's own speech bubble
    /// (<see cref="InnerVoiceCue"/>, the same design language as the Notebook and the chooser): bought, the
    /// shelf is empty, he cannot afford it, his hands are full. The words are data
    /// (<c>InnerVoiceLineDef</c> assets), not code.</para>
    ///
    /// <para><b>Cheap.</b> The label is rebuilt only when the shelf count or the price changes (the owner
    /// calls <see cref="Refresh"/> on a shelf change); the interact popup reads a cached string.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShelfGood : MonoBehaviour, IInteractable
    {
        /// <summary>The label while there is stock: name, then price. Loc seam.</summary>
        public const string BuyLabelFormat = "Buy {0} — {1}";

        /// <summary>The label on an empty shelf: name, then why. Loc seam.</summary>
        public const string SoldOutLabelFormat = "{0} — sold out";

        private string _id = "";
        private string _shopId = "";
        private string _listingId = "";
        private float _reach = 1f;
        private bool _needsFreeHand;
        private System.Func<bool> _inside;

        private bool _quoted;
        private string _name = "";
        private int _price = -1;
        private bool _soldOut;
        private string _label = "";

        /// <summary>The listing this good sells (<c>supply.bread</c>).</summary>
        public string ListingId => _listingId;

        /// <summary>The last quote's shelf count was zero.</summary>
        public bool SoldOut => _soldOut;

        /// <summary>Wires the good. <paramref name="inside"/> answers whether the player is in the store,
        /// downstairs (the good is not offered from outside or upstairs).</summary>
        public void Configure(string id, string shopId, string listingId, float reachMeters, bool needsFreeHand,
                              System.Func<bool> inside)
        {
            _id = id ?? "";
            _shopId = shopId ?? "";
            _listingId = listingId ?? "";
            _reach = Mathf.Max(0f, reachMeters);
            _needsFreeHand = needsFreeHand;
            _inside = inside;
            _quoted = false;
            _price = -1;
            _label = "";
        }

        // ---- the interact seam ---------------------------------------------------------------

        /// <inheritdoc/>
        public string Id => _id;

        /// <inheritdoc/>
        public Vector2 WorldPosition => transform.position;

        /// <inheritdoc/>
        public float ReachMeters => _reach;

        /// <summary>A fixture: the shelf is bolted to the room.</summary>
        public int Priority => InteractPriority.Fixture;

        /// <inheritdoc/>
        public InteractContext Contexts => InteractContext.OnFoot;

        /// <inheritdoc/>
        public bool RequiresFacing => false;

        /// <summary>Offered while the player is in the store downstairs and the till knows this good. An empty
        /// shelf is still offered: pressing it says so (the cozy refusal).</summary>
        public bool IsAvailable => _quoted && (_inside == null || _inside());

        /// <inheritdoc/>
        public string VerbLabel => _label;

        /// <inheritdoc/>
        public void Interact(in InteractActor actor) => Press(GameServices.Clock?.TotalSeconds ?? 0.0);

        private void OnEnable() => Interactables.Register(this);
        private void OnDisable() => Interactables.Unregister(this);

        /// <summary>Re-reads the till's quote at <paramref name="now"/> and rebuilds the label if the name,
        /// price or sold-out state moved. Called by the shelf's owner on a change, not per frame.</summary>
        public void Refresh(double now)
        {
            if (!ShelfTills.TryGet(_shopId, out IShelfTill till) || !till.TryQuote(_listingId, now, out ShelfQuote q))
            {
                _quoted = false;
                _label = "";
                return;
            }
            bool changed = !_quoted || q.Price != _price || q.SoldOut != _soldOut
                           || !string.Equals(q.DisplayName, _name, System.StringComparison.Ordinal);
            _quoted = true;
            if (!changed) return;
            _name = q.DisplayName ?? "";
            _price = q.Price;
            _soldOut = q.SoldOut;
            _label = LabelFor(q);
        }

        /// <summary>The words in the bubble for a quote.</summary>
        public static string LabelFor(in ShelfQuote q)
            => q.SoldOut ? string.Format(SoldOutLabelFormat, q.DisplayName)
                         : string.Format(BuyLabelFormat, q.DisplayName, MoneyFormat.Balance(q.Price));

        /// <summary>
        /// Buys one at <paramref name="now"/>, says the outcome in the player's bubble, and returns it. A good
        /// carried off in a hand (<see cref="ShopShelfSlot.NeedsFreeHand"/>) is refused before any money
        /// moves when both hands are taken. Public so a test drives it without input.
        /// </summary>
        public ShelfBuyResult Press(double now)
        {
            ShelfBuyResult result;
            if (!ShelfTills.TryGet(_shopId, out IShelfTill till)) result = ShelfBuyResult.NotSold;
            else if (_needsFreeHand && !HasFreeHand()) result = ShelfBuyResult.HandsFull;
            else result = till.TryBuyOne(_listingId, now);

            string cue = CueFor(result);
            if (cue != null) EventBus.Publish(new InnerVoiceCue(cue));
            Refresh(now);
            return result;
        }

        /// <summary>The bubble line an outcome is said with (null: say nothing).</summary>
        public static string CueFor(ShelfBuyResult result)
        {
            switch (result)
            {
                case ShelfBuyResult.Bought: return InnerVoiceCues.ShelfBought;
                case ShelfBuyResult.SoldOut: return InnerVoiceCues.ShelfSoldOut;
                case ShelfBuyResult.TooDear: return InnerVoiceCues.ShelfTooDear;
                case ShelfBuyResult.HandsFull: return InnerVoiceCues.ShelfHandsFull;
                default: return null;
            }
        }

        // One free hand is enough. A carrier without two hands (a test double, an older rig) is asked whether
        // it carries anything at all.
        private static bool HasFreeHand()
        {
            ICarrier carrier = GameServices.Hands;
            if (carrier == null) return true;
            if (carrier is IHandsCarrier hands) return hands.Slots.Left == null || hands.Slots.Right == null;
            return !carrier.IsCarrying;
        }
    }
}
