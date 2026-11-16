using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One loose material a bucket or tub is filled with</b>, by its bulk density (island supply slice S1).
    /// One asset per material under <c>Data/Resources/IslandSupply/Bulk/</c>, id <c>bulk.snake_case</c>.
    /// A fill is <c>min(volume x density, mass limit)</c>
    /// (<see cref="SupplyPlanMath.BulkLoadKg(CarrierDef,BulkMaterialDef,out LoadDimension)"/>).
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Island Supply/Bulk Material", fileName = "BulkMaterial")]
    public sealed class BulkMaterialDef : ScriptableObject
    {
        [Tooltip("Stable id, `bulk.snake_case`. Append-only.")]
        public string Id;

        [Tooltip("The name people read.")]
        public string DisplayName;

        [Tooltip("Bulk density as loaded, kg per litre (= tonnes per cubic metre).")]
        [Min(0.001f)] public float DensityKgPerLitre = 1f;

        [Tooltip("Where the number came from.")]
        [TextArea] public string Basis;
    }
}
