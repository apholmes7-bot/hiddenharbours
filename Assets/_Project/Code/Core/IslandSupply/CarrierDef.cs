using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One thing goods are carried in or on</b>, with its real capacity by weight AND by volume (island
    /// supply slice S1; owner 2026-11-15: "They should have adequate storage tools, wagon, cart, pail, bags ...
    /// Accurate storage size."). One asset per carrier under <c>Data/Resources/IslandSupply/Carriers/</c>, id
    /// <c>carrier.snake_case</c>.
    ///
    /// <para><see cref="CapacityKg"/> is the PRACTICAL load (a wagon on sand carries less than its sticker);
    /// <see cref="RatedKg"/> keeps the maker's rating beside it so the derating stays visible. A load fits only
    /// when both limits hold (<see cref="SupplyPlanMath.Fit(CarrierDef,double,double)"/>). Data only: no
    /// prop, vehicle or machine reads it yet (S5, S6).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Island Supply/Carrier", fileName = "Carrier")]
    public sealed class CarrierDef : ScriptableObject
    {
        [Tooltip("Stable id, `carrier.snake_case`. Append-only.")]
        public string Id;

        [Tooltip("The name the player reads.")]
        public string DisplayName;

        [Tooltip("What kind of carrier this is.")]
        public CarrierKind Kind;

        [Tooltip("Practical load limit, kg of contents (not counting the carrier's own tare).")]
        [Min(0f)] public float CapacityKg;

        [Tooltip("The maker's rating, kg. At or above CapacityKg; the gap is the derating for sand or hands.")]
        [Min(0f)] public float RatedKg;

        [Tooltip("Load volume limit, litres.")]
        [Min(0f)] public float CapacityLitres;

        [Tooltip("The carrier's own empty mass, kg (matters when it is itself a load: a drum on a pallet).")]
        [Min(0f)] public float TareKg;

        [Tooltip("Where the numbers came from.")]
        [TextArea] public string Basis;
    }
}
