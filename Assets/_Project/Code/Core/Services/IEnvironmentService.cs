using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Produces the sea's mood (tide, wind, sea state, fog, current) deterministically from
    /// the world seed and the clock. Because it is deterministic, the environment is
    /// recomputed rather than saved (docs/architecture/tech-architecture.md §1, §6).
    /// </summary>
    public interface IEnvironmentService
    {
        int WorldSeed { get; }

        /// <summary>The tide profile of the active region. Set when a region is entered.</summary>
        TideProfile ActiveTideProfile { get; set; }

        /// <summary>Full environment snapshot for the active region at the current time.</summary>
        EnvironmentSample Sample();

        /// <summary>Tide height (m rel. datum) at an arbitrary time, for the active region —
        /// e.g. to draw a tide table / forecast.</summary>
        float TideHeightAt(double totalSeconds);
    }
}
