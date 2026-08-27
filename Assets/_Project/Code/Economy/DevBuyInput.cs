using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// PLACEHOLDER INPUT (the book it opens is real): press P while ON FOOT and within reach of a stall
    /// to open that seller's wares book — browse the listings, see prices and what you can afford, and
    /// Confirm to buy through the vendors' existing seams. The instant-buy seams
    /// (<see cref="Shipwright.TryBuy()"/> etc.) remain for tests/automation.
    ///
    /// <para><b>⚠️ Its retirement is already scheduled.</b> The book is meant to be opened by a PERSON —
    /// a clerk behind the counter, through a dialogue row — because a book with nobody holding it is a
    /// menu. This key exists only until those clerks land, and retires with
    /// <see cref="BuyPointInstaller"/> and <see cref="DevSellInput"/> when they do, returning P to the
    /// key ledger.</para>
    ///
    /// <para><b>It opens by SELLER, not by GameObject.</b> That is the inversion
    /// (<see cref="CatalogListing"/>): stock is content tagged with a seller id, so this reads the id off
    /// whichever vendor component sits on this stall and asks for that seller's book. It publishes the
    /// SAME <c>CatalogViewRequested</c> a dialogue row publishes, so the dev key and the conversation
    /// reach the book through one door — and retiring this key removes a caller, not a path.</para>
    /// </summary>
    public class DevBuyInput : MonoBehaviour
    {
        [Tooltip("On-foot + in-range gate: P only opens the book when the walking player is at this stall.")]
        [SerializeField] private StallReach _reach = new StallReach();

        private void OnEnable() => _reach.Enable();
        private void OnDisable() => _reach.Disable();

        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.pKey.wasPressedThisFrame) return;

            if (!_reach.CanInteract(transform.position))
            {
                if (_reach.OnFoot) Debug.Log("[Buy] Too far - step up to the stall to browse.");
                return;
            }

            string seller = SellerIdOn(gameObject);
            if (string.IsNullOrEmpty(seller))
            {
                Debug.LogWarning($"[Buy] '{name}' has no vendor carrying a seller id, so nothing here is " +
                                 "in a book. Give its vendor component a _sellerId and tag the listings " +
                                 "that seller stocks.");
                return;
            }

            EventBus.Publish(new CatalogViewRequested(seller, "", ""));
        }

        /// <summary>
        /// The seller id of whichever vendor component sits on this stall, or empty when none of them
        /// carries one.
        ///
        /// <para>Vendor-agnostic in the same way the old screen was: the same driver serves the Punt
        /// shed, the dory yard, the harbourmaster and the general store, because it asks every vendor
        /// kind and takes the first answer. A stall whose vendors all stack one seller id — which is what
        /// a counter IS — gives the same answer whichever one replies.</para>
        /// </summary>
        public static string SellerIdOn(GameObject stall)
        {
            if (stall == null) return "";

            string id = Id(stall.GetComponent<Shipwright>()?.SellerId);
            if (id.Length == 0) id = Id(stall.GetComponent<GearShop>()?.SellerId);
            if (id.Length == 0) id = Id(stall.GetComponent<PotShop>()?.SellerId);
            if (id.Length == 0) id = Id(stall.GetComponent<BaitShop>()?.SellerId);
            if (id.Length == 0) id = Id(stall.GetComponent<SupplyShop>()?.SellerId);
            if (id.Length == 0) id = Id(stall.GetComponent<InstrumentShop>()?.SellerId);
            if (id.Length == 0) id = Id(stall.GetComponent<LicenseVendor>()?.SellerId);
            return id;
        }

        private static string Id(string sellerId) => string.IsNullOrEmpty(sellerId) ? "" : sellerId;
    }
}
