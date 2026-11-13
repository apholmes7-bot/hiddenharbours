using System;
using System.Collections.Generic;

namespace HiddenHarbours.World
{
    /// <summary>Intended route medium. Road currently describes walking, not vehicle transport.</summary>
    public enum RegionSeamMode { Sea, Foot, Road }

    /// <summary>An audit snapshot of an existing region's build availability and named arrivals.</summary>
    public sealed class RegionSeamNode
    {
        public string SceneName { get; }
        public bool Available { get; }
        public IReadOnlyList<string> ArrivalKeys { get; }

        public RegionSeamNode(string sceneName, bool available, IEnumerable<string> arrivalKeys)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) throw new ArgumentException("Scene name is required.", nameof(sceneName));
            SceneName = sceneName;
            Available = available;
            ArrivalKeys = Array.AsReadOnly(new List<string>(arrivalKeys ?? Array.Empty<string>()).ToArray());
        }

        public bool HasNamedArrival(string key)
        {
            for (int i = 0; i < ArrivalKeys.Count; i++)
                if (string.Equals(ArrivalKeys[i], key, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }

    /// <summary>Read-only passage facts. Enabled includes the trigger and loader wiring in the snapshot.</summary>
    public sealed class RegionSeamEdge
    {
        public string Id { get; }
        public string From { get; }
        public string To { get; }
        public string ArrivalKey { get; }
        public RegionSeamMode Mode { get; }
        public bool Enabled { get; }

        public RegionSeamEdge(string id, string from, string to, string arrivalKey, RegionSeamMode mode, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Passage identity is required.", nameof(id));
            Id = id;
            From = from ?? "";
            To = to ?? "";
            ArrivalKey = arrivalKey ?? "";
            Mode = mode;
            Enabled = enabled;
        }
    }

    /// <summary>
    /// Pure graph inspection over snapshots of RegionDef, LandDoor and committed scene facts.
    /// No loader, scene, save, clock, random source or registration. Not wired into gameplay.
    /// Reachability is structural: a missing named arrival still transfers through the existing
    /// default fallback, so it is reported separately rather than silently removing that edge.
    /// Terrain, tide, hull clearance and player progression need their own proofs.
    /// </summary>
    public sealed class RegionSeamGraph
    {
        private readonly Dictionary<string, RegionSeamNode> _nodes = new(StringComparer.Ordinal);
        public IReadOnlyList<RegionSeamEdge> Edges { get; }

        public RegionSeamGraph(IEnumerable<RegionSeamNode> nodes, IEnumerable<RegionSeamEdge> edges)
        {
            foreach (var node in nodes ?? throw new ArgumentNullException(nameof(nodes)))
                _nodes.Add(node.SceneName, node);
            var ordered = new List<RegionSeamEdge>(edges ?? throw new ArgumentNullException(nameof(edges)));
            ordered.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var edge in ordered)
                if (!ids.Add(edge.Id)) throw new ArgumentException("Duplicate passage identity: " + edge.Id, nameof(edges));
            Edges = ordered.AsReadOnly();
        }

        public IReadOnlyList<string> ReachableFrom(string start)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal);
            if (start == null || !_nodes.TryGetValue(start, out var node) || !node.Available)
                return Array.Empty<string>();
            var pending = new Queue<string>();
            pending.Enqueue(start);
            visited.Add(start);
            while (pending.Count > 0)
            {
                string current = pending.Dequeue();
                foreach (var edge in Edges)
                    if (edge.From == current && CanTraverse(edge) && visited.Add(edge.To)) pending.Enqueue(edge.To);
            }
            var result = new List<string>(visited);
            result.Sort(StringComparer.Ordinal);
            return result.AsReadOnly();
        }

        public IReadOnlyList<string> UnavailableTargets()
            => Select(edge => edge.Enabled && (!_nodes.TryGetValue(edge.To, out var node) || !node.Available));

        public IReadOnlyList<string> MissingNamedArrivals()
            => Select(edge => !string.IsNullOrEmpty(edge.ArrivalKey) &&
                _nodes.TryGetValue(edge.To, out var node) && node.Available && !node.HasNamedArrival(edge.ArrivalKey));

        /// <summary>Enabled traversable edges without an enabled return of the same intended medium.
        /// This is region-level pairing; it does not certify that two physical bands align.</summary>
        public IReadOnlyList<string> UnpairedEdges()
            => Select(edge => CanTraverse(edge) && !HasReturn(edge));

        private bool HasReturn(RegionSeamEdge edge)
        {
            foreach (var other in Edges)
                if (other.From == edge.To && other.To == edge.From && other.Mode == edge.Mode && CanTraverse(other)) return true;
            return false;
        }

        private bool CanTraverse(RegionSeamEdge edge)
            => edge.Enabled && _nodes.TryGetValue(edge.From, out var source) && source.Available &&
                _nodes.TryGetValue(edge.To, out var target) && target.Available;

        private IReadOnlyList<string> Select(Func<RegionSeamEdge, bool> predicate)
        {
            var result = new List<string>();
            foreach (var edge in Edges) if (predicate(edge)) result.Add(edge.Id);
            return result.AsReadOnly();
        }
    }
}
