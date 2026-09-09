using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// 🔴 <b>A region's wind character, as DATA (CLAUDE.md rule 2, owner ruling 2026-09-09).</b> One
    /// asset per region, a stable append-only <see cref="Id"/>, and nothing about how a place blows
    /// hard-coded in C#.
    ///
    /// <para>Until this existed the only wind in the game was <see cref="WindProfile.CoddleCove"/>, a
    /// static in code — which was fine while there was one greybox region and is not fine now that
    /// the sea-state ladder is fully reachable: an open headland and a sheltered inlet should not
    /// have the same weather, and the difference is authoring, not programming.</para>
    ///
    /// <para>⚠️ <b>What is per-region and what is NOT.</b> A region owns its prevailing direction,
    /// its liveliness, and — the new one — how hard a passing weather system blows THERE
    /// (<see cref="WindProfile.SystemStrength"/>, its exposure). It does <b>not</b> own the system's
    /// timing or shape: those are <c>WeatherModel.SystemChangeHours</c> and
    /// <c>WeatherModel.SystemShape</c>, one stream per world seed shared by every region. Otherwise
    /// Nine Mile Creek could blow a gale while St Peters, ten kilometres away on the same island, lay
    /// glass. <c>WindSystemIsAWorldFactTests</c> is the guard.</para>
    ///
    /// <para>⚠️ <b>An unassigned reference falls back to <see cref="WindProfile.CoddleCove"/> BY NAME,
    /// never to a zero profile.</b> A zeroed struct is not a gentle default — it is a dead calm
    /// forever, with `CalmMaxStrength = 0` clamping every wind to nothing. Serialized fields that
    /// were never authored read zero (memory: <c>a-serialized-field-absent-from-the-asset-reads-zero</c>),
    /// so the fallback is explicit and it logs once. <see cref="Resolve"/> is the only door.</para>
    ///
    /// <para>Lives in Core because two lanes read it — World authors it on <c>RegionDef</c>,
    /// Environment samples it — and neither may reference the other (rule 4).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Wind Profile", fileName = "WindProfile")]
    public class WindProfileDef : ScriptableObject
    {
        [Tooltip("Stable id, append-only: wind.<region_snake_case>. Never renamed, never reused.")]
        public string Id = "wind.coddle_cove";

        [Tooltip("What this wind reads as in one phrase — for the authoring list, not the player.")]
        public string DisplayName = "Coddle Cove — a gentle, lively south-westerly";

        [Tooltip("The region's wind character. ⚠ The weather SYSTEM's timing and shape are NOT here: " +
                 "they are world-level on WeatherModel, shared by every region. Only SystemStrength " +
                 "— how hard a system blows HERE — is local.")]
        public WindProfile Profile = WindProfile.CoddleCove;

        /// <summary>
        /// The profile a caller should actually sample, given a possibly-null asset. ⚠️ <b>Use this
        /// rather than <c>def.Profile</c>:</b> an unassigned reference and an unauthored asset both
        /// have to land on <see cref="WindProfile.CoddleCove"/>, and a zeroed struct has to be caught
        /// rather than sampled. A dead-calm-forever region is a bug that looks like weather.
        /// </summary>
        /// <param name="def">The region's asset, or null when none is wired.</param>
        /// <param name="context">Named in the one-time warning so the missing wiring is findable.</param>
        public static WindProfile Resolve(WindProfileDef def, string context)
        {
            if (def == null)
            {
                WarnOnce($"[WindProfileDef] No wind profile for {context}; falling back to " +
                         "WindProfile.CoddleCove. Wire a WindProfileDef on the RegionDef.");
                return WindProfile.CoddleCove;
            }
            if (!IsAuthored(def.Profile))
            {
                WarnOnce($"[WindProfileDef] '{def.name}' ({context}) deserialized as a ZERO profile — " +
                         "a dead calm forever, not a gentle one. Falling back to " +
                         "WindProfile.CoddleCove; author the asset's fields.");
                return WindProfile.CoddleCove;
            }
            return def.Profile;
        }

        /// <summary>Whether a profile was authored at all. <c>CalmMaxStrength</c> is the tell: it is a
        /// ceiling, so zero is never a legitimate authoring — it clamps every wind to nothing.</summary>
        public static bool IsAuthored(in WindProfile p) => p.CalmMaxStrength > 0f;

        // One line per distinct message, not one per frame: this is sampled on the weather tick.
        private static readonly System.Collections.Generic.HashSet<string> Warned =
            new System.Collections.Generic.HashSet<string>();

        private static void WarnOnce(string message)
        {
            if (Warned.Add(message)) Debug.LogWarning(message);
        }
    }
}
