using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// An authored <b>clam-hole</b> on the St Peters flats — a POSITION (and the species id it yields)
    /// where the player can dig when the tide bares the ground beneath it. world-content authors WHERE the
    /// holes are (this marker, dropped on the sandbar/coast in the region scene); gameplay-systems
    /// implements the DIG (the shovel action, the exposure check, granting the catch). The two meet only at
    /// Core: the spot names its yield by stable id and gameplay reads the tide-exposure of this position via
    /// <see cref="GameServices.TidalTerrain"/> + <see cref="TidalExposure"/> — neither references the other.
    ///
    /// <para><b>"Two squirting holes" tell.</b> The clam betrays itself at low water; a hole is only diggable
    /// while <see cref="IsExposedNow"/> (its ground is above the water surface). The yield is named by id
    /// (<c>fish.soft_shell_clam</c>) so no content is hard-coded — content is data (CLAUDE.md rule 2).</para>
    /// </summary>
    public sealed class ClamSpot : MonoBehaviour
    {
        [Tooltip("Stable FishSpeciesDef id this hole yields when dug (e.g. fish.soft_shell_clam). The dig " +
                 "grants the catch by id; this marker never references the Fishing module.")]
        [SerializeField] private string _yieldFishId = "fish.soft_shell_clam";

        /// <summary>The species id this clam-hole yields (gameplay grants the catch by this id).</summary>
        public string YieldFishId => _yieldFishId;

        /// <summary>The world position of this clam-hole.</summary>
        public Vector2 Position => transform.position;

        /// <summary>
        /// True if this hole's ground is bared at the current tide — the dig gate. Reads the active region's
        /// terrain elevation at this position (<see cref="GameServices.TidalTerrain"/>) and the deterministic
        /// water level for <paramref name="totalSeconds"/> from <paramref name="environment"/>, then asks the
        /// shared <see cref="TidalExposure"/> rule. Returns <c>false</c> (treat as submerged — can't dig)
        /// when no terrain or environment is wired, so a null service never throws.
        /// </summary>
        public bool IsExposedNow(IEnvironmentService environment, double totalSeconds)
        {
            var terrain = GameServices.TidalTerrain;
            if (terrain == null || environment == null) return false;
            float elevation = terrain.ElevationAt(Position);
            return TidalExposure.IsExposed(environment.WaterLevelAt(totalSeconds), elevation);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.85f, 0.78f, 0.55f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, 0.4f);
        }
    }
}
