using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// Registers each wired region's player-facing display name into the Core
    /// <see cref="RegionDisplayNames"/> lookup at boot — the "world registrar" that seam was designed for
    /// (Core's <see cref="RegionDisplayNames"/> doc-comment: "the world owns <see cref="RegionDef"/> and
    /// registers the scene-name/id → display-name mappings here at boot"). The UI's crossing fade-card
    /// (which only sees a scene name) then resolves "St Peters Island" / "Port Greywick" without
    /// referencing the World module.
    ///
    /// <para>Registers BOTH keys per region — the stable id (<c>region.st_peters</c>) and the scene name
    /// (<c>StPeters</c>) — because callers key off either (the fade overlay uses the scene name; gameplay
    /// uses the id). <see cref="RegionDisplayNames.Register"/> is idempotent and first-wins, so placing this
    /// in several region scenes is safe. Pure presentation metadata — nothing saved, no determinism
    /// concern.</para>
    /// </summary>
    public sealed class RegionDisplayNameRegistrar : MonoBehaviour
    {
        [Tooltip("Regions whose scene-name + id → display-name mappings to register at boot.")]
        [SerializeField] private RegionDef[] _regions = new RegionDef[0];

        private void Awake() => RegisterAll();

        /// <summary>Register every wired region's display name under both its id and its scene name.</summary>
        public void RegisterAll()
        {
            if (_regions == null) return;
            foreach (var r in _regions)
            {
                if (r == null || string.IsNullOrWhiteSpace(r.DisplayName)) continue;
                RegionDisplayNames.Register(r.Id, r.DisplayName);
                RegionDisplayNames.Register(r.SceneName, r.DisplayName);
            }
        }
    }
}
