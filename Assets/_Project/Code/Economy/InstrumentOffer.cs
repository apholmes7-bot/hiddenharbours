using UnityEngine;

namespace HiddenHarbours.Economy
{
    /// <summary>
    /// One purchasable HELM INSTRUMENT, as data (ADR 0003) — a stable id, a display name, and a price.
    /// The depth sounder is the first (ADR 0025 S2); the fish finder, radar, GPS and compasses are the
    /// same shape when their slices land. Content is data, not code — add an instrument to a chandlery
    /// by creating one of these assets, never by hard-coding a price.
    ///
    /// <para><b>Why not a <see cref="GearOffer"/>.</b> Gear is a presence-only wallet: you own a rod or
    /// you don't, and you carry it between boats. An instrument is <b>bolted into one hull's dash</b> —
    /// buying a sounder for the skiff does nothing for the punt — so it is recorded per hull
    /// (<c>InstrumentLocker</c>) and needs its own vendor. That is exactly the split
    /// <see cref="SupplyDef"/> made from gear for a different reason (counted vs. presence).</para>
    ///
    /// <para><b>Lane boundary.</b> This is purchase economics only. Whether a given hull's console can
    /// actually TAKE the instrument, and what it then shows, is the Boats lane's
    /// (<c>BoatEquipment.EffectiveFit</c>) — Economy never references it. Create via
    /// Assets ▸ Create ▸ Hidden Harbours ▸ Instrument Offer, save in Data/Instruments.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Instrument Offer", fileName = "InstrumentOffer")]
    public class InstrumentOffer : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable, append-only instrument id (e.g. \"instrument.depth_sounder\"). Recorded against " +
                 "the hull it is fitted to; the Boats lane maps it to a helm fit. Never reuse or rename.")]
        public string Id = "instrument.depth_sounder";

        [Tooltip("Player-facing name shown in the buy UI. Flavour only; the id is canonical.")]
        public string DisplayName = "Depth Sounder";

        [TextArea] public string Flavor = "Flush-mount transducer and glass. Tells you what's under you.";

        [Header("Cost")]
        [Min(0)]
        [Tooltip("Price in ₲. The economy-owned tunable — what the chandlery charges (no magic number " +
                 "in code). PLACEHOLDER until the owner rules on it.")]
        public int Price = 140;
    }
}
