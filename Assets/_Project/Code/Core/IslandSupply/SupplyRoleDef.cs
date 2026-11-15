using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One supply run: who goes, with what, how often, and how it meets the tide</b> (island supply slice
    /// S1). One asset per role under <c>Data/Resources/IslandSupply/Roles/</c>, id <c>supplyrole.snake_case</c>.
    ///
    /// <para>The crew are proposals until the owner casts them (<see cref="CrewIsPlaceholder"/>). Speeds are
    /// metres per REAL second, the same unit the routines and vehicles move in; the maths turns them into game
    /// hours through <c>GameConfig.SecondsPerDay</c>. Nothing walks or drives this yet: the routine leg is S3,
    /// the tide gate S2.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Island Supply/Supply Role", fileName = "SupplyRole")]
    public sealed class SupplyRoleDef : ScriptableObject
    {
        [Tooltip("Stable id, `supplyrole.snake_case`. Append-only.")]
        public string Id;

        [Tooltip("The name people read.")]
        public string DisplayName;

        [Tooltip("The NpcDef ids who make the run.")]
        public List<string> CrewNpcIds = new List<string>();

        [Tooltip("True while the crew is a proposal the owner has not ruled on.")]
        public bool CrewIsPlaceholder = true;

        [Tooltip("People who cross the bar on each leg (a quad rider is 1, the foot pair 2). Counts crossings.")]
        [Min(1)] public int People = 1;

        [Tooltip("Every carrier the party takes, by CarrierDef id. Repeat an id for two of the same.")]
        public List<string> CarrierIds = new List<string>();

        [Tooltip("How the run meets the tide.")]
        public SupplyTripMode Mode;

        [Tooltip("Days between runs (2 = every second day, 7 = weekly). 0 = as needed, not counted as capacity.")]
        [Min(0)] public int CadenceDays;

        [Tooltip("The run goes on market day (GameConfig.MarketDayIndex).")]
        public bool OnMarketDay;

        [Tooltip("Speed on the sandbar, metres per real second, loaded.")]
        [Min(0.01f)] public float SandSpeedMetresPerSecond = 1.4f;

        [Tooltip("Speed on the island path and the mainland road, metres per real second, loaded.")]
        [Min(0.01f)] public float RoadSpeedMetresPerSecond = 1.4f;

        [Tooltip("Deepest water the party may take through the gut, metres.")]
        [Min(0f)] public float MaxWadeDepthMetres = 0.3f;

        [Tooltip("Game hours in town loading and doing errands before the return.")]
        [Min(0f)] public float LoadingHours = 0.5f;

        [Tooltip("Where the numbers came from, and why this run exists.")]
        [TextArea] public string Basis;
    }
}
