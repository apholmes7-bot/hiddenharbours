using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>One race region's existing deterministic wind, downwind m/s, at absolute game seconds.
    /// Implementations must be pure in (worldSeed, gameTime), with no wall-clock or current-time fallback.
    /// The composition root supplies the region/clock-scale adapter; Boats never references Environment.</summary>
    public interface IRaceWindSource
    {
        Vector2 WindAt(int worldSeed, double gameTime);
    }
}
