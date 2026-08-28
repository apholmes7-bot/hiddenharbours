using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// <b>THE OTHER HALF OF THE COUNTER</b> — the clerk's sell verb, answered.
    ///
    /// <para>A conversation publishes <c>Core.CounterSellRequested</c> with a seller id; this finds the
    /// sell components already standing on that seller's counter, hands them the player's hold through
    /// the seam they have always used, and reports back with <c>Core.CounterSellReported</c>. The
    /// dialogue side speaks the outcome in the seller's own bubble (owner ruling R7, 2026-08-27).</para>
    ///
    /// <para><b>No new sell economics, and no new screen.</b> Every figure comes from the same
    /// <see cref="FishBuyer"/> at the same <see cref="Market"/> quoting the same <c>SellPricing</c> the
    /// counter has quoted since #356 — the store's channel pays deliberately less than the creek, and it
    /// still does, because this touches none of it. <see cref="SellScreen"/> is untouched and keeps its
    /// own callers.</para>
    ///
    /// <para><b>It reports FACTS, never words</b> — the payout and how much left the hold. The sentences
    /// are authored on the option asset in the seller's voice, because the economy never writes dialogue
    /// (<see cref="FeeFronted"/>'s rule, kept).</para>
    ///
    /// <para><b>Static, with no scene presence, on purpose.</b> There is nothing to place, nothing to
    /// wire and nothing to bank: the counter it sells over is resolved per request from the seller id, by
    /// the same <see cref="BuyCatalog.ArmsFor"/> lookup the wares book uses, so the two can never
    /// disagree about whose counter they are standing at. It is not a dev driver and does not retire with
    /// them: it is a permanent seam that a conversation reaches through.</para>
    /// </summary>
    public static class CounterSellDesk
    {
        private static bool _installed;

        /// <summary>True while the desk is listening. Exposed so a test can assert the install rather
        /// than assume it — a negative assertion that passes because nothing was ever wired is the
        /// failure this codebase has been bitten by before.</summary>
        public static bool IsInstalled => _installed;

        /// <summary>The seller of the most recent request, or empty. Diagnostics only.</summary>
        public static string LastSellerId { get; private set; } = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Install()
        {
            if (_installed) return;
            _installed = true;
            EventBus.Subscribe<CounterSellRequested>(OnRequested);
        }

        /// <summary>Stand the desk down. For tests — a PlayMode fixture that installs its own must be
        /// able to leave the bus as it found it (⚠ never <c>EventBus.Clear</c>, which would drop every
        /// other listener on the channel too).</summary>
        public static void Uninstall()
        {
            if (!_installed) return;
            _installed = false;
            EventBus.Unsubscribe<CounterSellRequested>(OnRequested);
        }

        private static void OnRequested(CounterSellRequested request)
        {
            LastSellerId = request.SellerId ?? "";

            WharfSellPoint counter = CounterFor(request.SellerId);
            if (counter == null)
            {
                // Not an error: a seller with no sell components simply does not buy, and the row should
                // not have been authored on them. Reported as a sale of nothing so the conversation says
                // its empty-pail line and ends, rather than hanging on an answer that never comes.
                Debug.LogWarning(
                    $"[Counter] '{request.SellerId}' was asked to take a catch, but no WharfSellPoint " +
                    "stands on that seller's counter. Either the seller id is not wired onto this " +
                    "counter's vendors yet (the region scene may predate the builder that wires it), or " +
                    "the sell row belongs on a different person.");
                EventBus.Publish(new CounterSellReported(request.SellerId, 0, 0));
                return;
            }

            int paid = counter.SellOverTheCounter();
            EventBus.Publish(new CounterSellReported(request.SellerId, paid, counter.LastUnitsSold));
        }

        /// <summary>
        /// The sell point on a seller's counter, or null.
        ///
        /// <para>Resolved through the vendors, not by a field of its own: a counter IS the GameObject its
        /// vendor components stand on (the general store stacks five of them plus a Market, a FishBuyer
        /// and this), so the seller id already on those vendors names the sell stack beside them. That
        /// keeps one authored fact — <c>_sellerId</c> — answering both "whose book is this?" and "who
        /// takes my catch?", instead of a second field that can drift from the first.</para>
        /// </summary>
        public static WharfSellPoint CounterFor(string sellerId)
        {
            if (string.IsNullOrEmpty(sellerId)) return null;

            BuyCatalog.SellerArms arms = BuyCatalog.ArmsFor(sellerId);
            return On(arms.Gear) ?? On(arms.Bait) ?? On(arms.Supply) ?? On(arms.Instrument)
                   ?? On(arms.License) ?? On(arms.Pot) ?? On(arms.Shipwright);
        }

        /// <summary>⚠ Explicit <c>!= null</c> against Unity's overloaded <c>==</c>: a destroyed component
        /// is fake-null, and <c>?.</c> sails straight past it (the ResolvePlayer lesson).</summary>
        private static WharfSellPoint On(Component vendor)
        {
            if (vendor == null) return null;
            WharfSellPoint point = vendor.GetComponent<WharfSellPoint>();
            return point != null ? point : null;
        }
    }
}
