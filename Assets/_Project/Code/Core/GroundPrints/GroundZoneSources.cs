using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>"What ground is this?"</b> The world answers with a terrain zone NAME (the TerrainPlan
    /// vocabulary: <c>grass</c>, <c>sand</c>, <c>mud</c>, <c>shingle</c>...), and whoever asks maps the
    /// name onto what it cares about through data (<see cref="GroundPrintSettings.Zones"/> for prints).
    /// A region's ground registers itself through <see cref="GroundZoneSources"/>; deterministic,
    /// authored, never saved.
    /// </summary>
    public interface IGroundZoneSource
    {
        /// <summary>The zone name at <paramref name="worldPos"/>; false where this source has no answer
        /// (outside it, or an excluded cell such as a building or road).</summary>
        bool TryGetZone(Vector2 worldPos, out string zone);
    }

    /// <summary>The registry of live <see cref="IGroundZoneSource"/>s (scene-scoped, like
    /// <see cref="StandableSurfaces"/>). The first source with an answer wins.</summary>
    public static class GroundZoneSources
    {
        private static readonly List<IGroundZoneSource> Sources = new List<IGroundZoneSource>(2);

        public static int Count => Sources.Count;

        public static void Register(IGroundZoneSource source)
        {
            if (source != null && !Sources.Contains(source)) Sources.Add(source);
        }

        public static void Unregister(IGroundZoneSource source) => Sources.Remove(source);

        public static void Clear() => Sources.Clear();

        public static bool TryGetZoneNow(Vector2 worldPos, out string zone)
        {
            for (int i = 0; i < Sources.Count; i++)
                if (Sources[i] != null && Sources[i].TryGetZone(worldPos, out zone)) return true;
            zone = null;
            return false;
        }
    }
}
