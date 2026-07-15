using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The authored index of every <see cref="SeaweedDef"/> the self-installing
    /// <see cref="SeaweedPresenter"/> must find at boot — the exact <c>AmbientFleetLibrary</c> /
    /// <c>FishSpeciesLibrary</c> pattern: bed Defs live in <c>Data/Decor</c> (one entity per file,
    /// ADR 0003), this one asset gathers the refs and lives in <c>Resources</c> so a
    /// <see cref="RuntimeInitializeOnLoadMethod"/> host can load it with no scene or builder wiring.
    ///
    /// <para>Author it once, keep it append-only: a new region's weed = a new <see cref="SeaweedDef"/>
    /// asset + one entry here. Pure content metadata — never serialized into a save, no determinism
    /// concern. Create via Assets ▸ Create ▸ Hidden Harbours ▸ Seaweed Library, save at
    /// <c>Resources/SeaweedLibrary</c>.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Seaweed Library", fileName = "SeaweedLibrary")]
    public sealed class SeaweedLibrary : ScriptableObject
    {
        /// <summary>Resources path (no extension) the presenter loads the library from at boot.</summary>
        public const string ResourcesPath = "SeaweedLibrary";

        [Tooltip("Every seaweed bed in the game, one Def per region (Data/Decor). Null entries are skipped.")]
        public SeaweedDef[] Beds = Array.Empty<SeaweedDef>();
    }
}
