using System;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The authored index of every <see cref="FuelSpeedProfileDef"/>, so the live fuel meter can find
    /// a hull's fuel curve without the hull Def naming it. The <see cref="AmbientFleetLibrary"/>
    /// pattern exactly: the profiles live in <c>Data/Boats/FuelSpeed</c> (one entity per file,
    /// ADR 0003), this one asset gathers the refs and lives in <c>Resources</c>.
    ///
    /// <para>Append-only: a new hull = a new profile asset + one entry here. A hull with no entry is
    /// not an error; her tank keeps the old throttle curve. Pure content metadata, never saved.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Fuel Speed Profile Library", fileName = "FuelSpeedProfileLibrary")]
    public sealed class FuelSpeedProfileLibrary : ScriptableObject
    {
        /// <summary>Resources path (no extension) the meter loads the library from.</summary>
        public const string ResourcesPath = "FuelSpeedProfileLibrary";

        [Tooltip("Every hull's fuel speed profile (Data/Boats/FuelSpeed). Null entries are skipped.")]
        public FuelSpeedProfileDef[] Profiles = Array.Empty<FuelSpeedProfileDef>();

        private static FuelSpeedProfileLibrary s_shared;

        /// <summary>The one library, loaded on first ask. Null only if the asset is missing, which
        /// leaves every tank on the throttle curve.</summary>
        public static FuelSpeedProfileLibrary Shared
        {
            get
            {
                if (s_shared == null) s_shared = Resources.Load<FuelSpeedProfileLibrary>(ResourcesPath);
                return s_shared;
            }
        }

        /// <summary>
        /// The profile bound to the hull with this stable id, or null. Matched by ID, not by asset
        /// reference, so a runtime copy of a hull Def still finds her curve. A linear scan with no
        /// allocation; callers hold the answer per hull instead of asking per tick.
        /// </summary>
        public FuelSpeedProfileDef Find(string hullId)
        {
            if (string.IsNullOrEmpty(hullId) || Profiles == null) return null;
            for (int i = 0; i < Profiles.Length; i++)
            {
                FuelSpeedProfileDef p = Profiles[i];
                if (p != null && p.Hull != null && string.Equals(p.Hull.Id, hullId, StringComparison.Ordinal))
                    return p;
            }
            return null;
        }
    }
}
