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
    /// <para><b>Into the bag (slice 2).</b> A good configured with a <see cref="ShoppingBagDef"/> goes into
    /// the paper bag in his hand through the Core <see cref="IShoppingBagHands"/> (<see cref="GameServices.BagHands"/>):
    /// a full bag, or both hands taken with no bag among them, refuses before any money moves.</para>
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

        /// <summary>A chooser row in a shelf section while there is stock: name, price, then how many are
        /// left on the shelf. Only glyphs the chooser's pixel face draws (no dash, no dot: a missing glyph
        /// draws nothing). Loc seam.</summary>
        public const string ChoiceLabelFormat = "{0}: {1}, {2} left";

        /// <summary>A chooser row in a shelf section with nothing left. The chooser adds its own reason to a
        /// row that cannot be picked ("(not now)"), so the row says only "out": the longer "sold out" ran past
        /// the chooser's widest line and was cut (store sections look, 2026-11-25). Loc seam.</summary>
        public const string ChoiceSoldOutFormat = "{0}: out";

        private string _id = "";
        private string _shopId = "";
        private string _listingId = "";
        private float _reach = 1f;
        private bool _needsFreeHand;
        private System.Func<bool> _inside;
        private ShoppingBagDef _bag;

        private bool _quoted;
        private string _name = "";
        private int _price = -1;
        private bool _soldOut;
        private int _onShelf = -1;
        private string _label = "";
        private string _choiceLabel = "";
        private bool _inSection;

        /// <summary>The listing this good sells (<c>supply.bread</c>).</summary>
        public string ListingId => _listingId;

        /// <summary>The last quote's shelf count was zero.</summary>
        public bool SoldOut => _soldOut;

        /// <summary>Items on the shelf at the last quote (-1 before the first).</summary>
        public int OnShelf => _onShelf;

        /// <summary>The last quote's price (-1 before the first).</summary>
        public int Price => _price;

        /// <summary>This good's row in its section's chooser: name, price and how many are left, or the
        /// sold-out words. Cached; rebuilt only when the quote moves.</summary>
        public string ChoiceLabel => _choiceLabel;

        /// <summary>True when a <see cref="ShelfSection"/> sells this good: it is then not offered on its
        /// own (it is not registered as a thing to press), only as a row of its section.</summary>
        public bool InSection => _inSection;

        /// <summary>Hands this good to a section: it leaves the interact registry and is bought only
        /// through the section's chooser row (<see cref="Press"/> is unchanged).</summary>
        public void PutInSection()
        {
            _inSection = true;
            Interactables.Unregister(this);
            if (_quoted) _choiceLabel = ChoiceLabelFor(new ShelfQuote(_listingId, _name, _price, _onShelf));
        }

        /// <summary>Wires the good. <paramref name="inside"/> answers whether the player is in the store,
        /// downstairs (the good is not offered from outside or upstairs).</summary>
        public void Configure(string id, string shopId, string listingId, float reachMeters, bool needsFreeHand,
                              System.Func<bool> inside, ShoppingBagDef bag = null)
        {
            _bag = bag;
            _id = id ?? "";
            _shopId = shopId ?? "";
            _listingId = listingId ?? "";
            _reach = Mathf.Max(0f, reachMeters);
            _needsFreeHand = needsFreeHand;
            _inside = inside;
            _quoted = false;
            _price = -1;
            _onShelf = -1;
            _label = "";
            _choiceLabel = "";
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

        private void OnEnable()
        {
            if (!_inSection) Interactables.Register(this);
        }
        private void OnDisable() => Interactables.Unregister(this);

        /// <summary>Re-reads the till's quote at <paramref name="now"/> and rebuilds the label if the name,
        /// price or sold-out state moved. Called by the shelf's owner on a change, not per frame.</summary>
        public void Refresh(double now)
        {
            if (!ShelfTills.TryGet(_shopId, out IShelfTill till) || !till.TryQuote(_listingId, now, out ShelfQuote q))
            {
                _quoted = false;
                _label = "";
                _choiceLabel = "";
                return;
            }
            bool changed = !_quoted || q.Price != _price || q.SoldOut != _soldOut
                           || !string.Equals(q.DisplayName, _name, System.StringComparison.Ordinal);
            // The count moves only the section's row (the single good's label never shows it).
            bool countMoved = q.OnShelf != _onShelf;
            _quoted = true;
            if (!changed && !countMoved) return;
            _name = q.DisplayName ?? "";
            _price = q.Price;
            _soldOut = q.SoldOut;
            _onShelf = q.OnShelf;
            if (changed) _label = LabelFor(q);
            if (_inSection) _choiceLabel = ChoiceLabelFor(q);
        }

        /// <summary>The words in the bubble for a quote.</summary>
        public static string LabelFor(in ShelfQuote q)
            => q.SoldOut ? string.Format(SoldOutLabelFormat, q.DisplayName)
                         : string.Format(BuyLabelFormat, q.DisplayName, MoneyFormat.Balance(q.Price));

        /// <summary>The words on a section's chooser row for a quote: name, price and how many are left, or
        /// the sold-out words.</summary>
        public static string ChoiceLabelFor(in ShelfQuote q)
            => q.SoldOut ? string.Format(ChoiceSoldOutFormat, q.DisplayName)
                         : string.Format(ChoiceLabelFormat, q.DisplayName, MoneyFormat.Balance(q.Price), q.OnShelf);

        /// <summary>
        /// Buys one at <paramref name="now"/>, says the outcome in the player's bubble, and returns it. A good
        /// carried off in a hand (<see cref="ShopShelfSlot.NeedsFreeHand"/>) is refused before any money
        /// moves when both hands are taken. Public so a test drives it without input.
        /// </summary>
        public ShelfBuyResult Press(double now)
        {
            ShelfBuyResult result;
            bool bagged = false;
            IShoppingBagHands bagHands = _bag != null ? GameServices.BagHands : null;
            if (!ShelfTills.TryGet(_shopId, out IShelfTill till)) result = ShelfBuyResult.NotSold;
            else if (_needsFreeHand && !HasFreeHand()) result = ShelfBuyResult.HandsFull;
            else if (bagHands != null)
            {
                // Asked before any money moves: a full bag, or no hand for a new one, refuses the good.
                ShoppingBagRefusal room = bagHands.CanBag(_bag);
                if (room == ShoppingBagRefusal.BagFull) result = ShelfBuyResult.BagFull;
                else if (room == ShoppingBagRefusal.BagElsewhere) result = ShelfBuyResult.BagElsewhere;
                else if (room == ShoppingBagRefusal.HandsFull) result = ShelfBuyResult.HandsFull;
                else
                {
                    // One write for the purchase and the bag it went into.
                    ISaveService save = GameServices.Save;
                    using (save?.DeferWrites())
                    {
                        result = till.TryBuyOne(_listingId, now);
                        // No game loaded (NoSave): bought into his supplies, as the counter always did.
                        if (result == ShelfBuyResult.Bought && room == ShoppingBagRefusal.None)
                        {
                            bagged = bagHands.TryBag(_bag, _listingId) == ShoppingBagRefusal.None;
                            if (bagged) save?.Save();
                        }
                    }
                }
            }
            else result = till.TryBuyOne(_listingId, now);

            string cue = bagged ? InnerVoiceCues.ShelfBagged : CueFor(result);
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
                case ShelfBuyResult.BagFull: return InnerVoiceCues.ShelfBagFull;
                case ShelfBuyResult.BagElsewhere: return InnerVoiceCues.ShelfBagElsewhere;
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
