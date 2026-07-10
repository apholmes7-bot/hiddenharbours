using System;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The authored index of every <see cref="AmbientFleetDef"/> the self-installing
    /// <see cref="AmbientFleetPresenter"/> must find at boot — the exact
    /// <c>FishSpeciesLibrary</c>/<c>IconLibrary</c> pattern: fleet Defs live in <c>Data/Boats</c> (one
    /// entity per file, ADR 0003), this one asset gathers the refs and lives in <c>Resources</c> so a
    /// <see cref="RuntimeInitializeOnLoadMethod"/> host can load it with no scene or builder wiring.
    ///
    /// <para>Author it once, keep it append-only: a new region's fleet = a new
    /// <see cref="AmbientFleetDef"/> asset + one entry here. Pure content metadata — never serialized
    /// into a save, no determinism concern. Create via Assets ▸ Create ▸ Hidden Harbours ▸ Ambient
    /// Fleet Library, save at <c>Resources/AmbientFleetLibrary</c>.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Ambient Fleet Library", fileName = "AmbientFleetLibrary")]
    public sealed class AmbientFleetLibrary : ScriptableObject
    {
        /// <summary>Resources path (no extension) the presenter loads the library from at boot.</summary>
        public const string ResourcesPath = "AmbientFleetLibrary";

        [Tooltip("Every ambient fleet in the game, one Def per region (Data/Boats). Null entries are skipped.")]
        public AmbientFleetDef[] Fleets = Array.Empty<AmbientFleetDef>();
    }
}
