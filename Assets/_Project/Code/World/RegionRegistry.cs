using System;
using System.Collections.Generic;

namespace HiddenHarbours.World
{
    /// <summary>
    /// A lookup of <see cref="RegionDef"/>s by stable id — the pure, engine-light core of the
    /// scene-load path (CLAUDE.md §5: keep logic testable POCOs). Built from whatever regions a scene
    /// wires in; ignores nulls, blank ids, and duplicate ids (first wins) so authoring mistakes
    /// degrade gracefully rather than throwing at runtime.
    /// </summary>
    public sealed class RegionRegistry
    {
        private readonly Dictionary<string, RegionDef> _byId = new(StringComparer.Ordinal);
        private readonly List<RegionDef> _all = new();

        public RegionRegistry(IEnumerable<RegionDef> regions)
        {
            if (regions == null) return;
            foreach (var r in regions)
            {
                if (r == null || string.IsNullOrEmpty(r.Id)) continue;
                if (_byId.ContainsKey(r.Id)) continue; // first registration wins
                _byId.Add(r.Id, r);
                _all.Add(r);
            }
        }

        public IReadOnlyList<RegionDef> All => _all;
        public int Count => _all.Count;

        public bool Contains(string id) => !string.IsNullOrEmpty(id) && _byId.ContainsKey(id);

        public bool TryGet(string id, out RegionDef region)
        {
            if (!string.IsNullOrEmpty(id)) return _byId.TryGetValue(id, out region);
            region = null;
            return false;
        }

        /// <summary>The region with this id, or null if unknown.</summary>
        public RegionDef Get(string id) => TryGet(id, out var r) ? r : null;
    }

    /// <summary>
    /// Pure decisions for moving between regions, separated from the Unity scene API so they are unit
    /// testable without play mode. The <see cref="RegionSceneLoader"/> is a thin wrapper that does the
    /// actual additive <c>SceneManager</c> work guided by these.
    /// </summary>
    public static class RegionTravel
    {
        /// <summary>A region can be loaded if it exists and names a scene.</summary>
        public static bool CanLoad(RegionDef to) => to != null && to.HasScene;

        /// <summary>True if we are already standing in the target region's scene (don't reload it).</summary>
        public static bool IsAlreadyHere(string currentSceneName, RegionDef to)
            => to != null
               && !string.IsNullOrEmpty(currentSceneName)
               && string.Equals(currentSceneName, to.SceneName, StringComparison.Ordinal);

        /// <summary>The decision a passage makes: load the target only if we can and we're not there.</summary>
        public static bool ShouldLoad(string currentSceneName, RegionDef to)
            => CanLoad(to) && !IsAlreadyHere(currentSceneName, to);
    }
}
