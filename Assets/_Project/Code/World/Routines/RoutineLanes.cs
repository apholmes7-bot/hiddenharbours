using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>The region's LANES</b> — the walkable network its villagers keep to, standing in the scene as
    /// one object the builder writes and the villagers read. The maths that walks it is
    /// <see cref="RoutineLaneTree"/>; this is the data.
    ///
    /// <para><b>Why lanes at all, rather than walking straight at the destination.</b> A villager who
    /// crossed the meadow diagonally reads as a marker sliding across a map; a villager who comes down the
    /// lane, turns at the green and goes up the dirt path reads as a person. It also keeps them off ground
    /// nobody meant them to be on — through a garden, across a foreshore, over a wall. The lanes are
    /// DERIVED from what the region already has: at St Peters two of them are literally the dirt paths the
    /// terrain painter already paints (<c>StPetersStarterSplat.VillageToSlipPath</c> /
    /// <c>VillageToBarHeadPath</c>), so a villager on them is walking on ground the world already draws as
    /// walked.</para>
    ///
    /// <para><b>Flat parallel arrays, on purpose.</b> Unity does not serialize a container inside a
    /// container, so an array of "edges each with their own polyline" either needs a wrapper class per edge
    /// or silently loses the inner arrays. Flattening is honest about that, costs nothing to read, and
    /// makes the whole table one glance in the inspector. <see cref="Validate"/> is what stops the parallel
    /// arrays drifting out of step.</para>
    ///
    /// <para><b>Builder-wired, not scene-wired</b> — the same rule as <see cref="RoutineStations"/> and
    /// everything else in <c>StPeters.unity</c>.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoutineLanes : MonoBehaviour
    {
        /// <summary>The conventional name of the object the builder hangs this on.</summary>
        public const string ObjectName = "RoutineLanes";

        [Tooltip("Junction positions, in world metres. Node 0 is conventionally the root (the green).")]
        [SerializeField] private Vector2[] _nodePositions = System.Array.Empty<Vector2>();

        [Tooltip("Each node's parent index, or −1 for the root. Exactly one −1 (see RoutineLaneTree).")]
        [SerializeField] private int[] _nodeParents = System.Array.Empty<int>();

        [Tooltip("Each node's name, for diagnostics and for stations to attach by. Ordinal-matched.")]
        [SerializeField] private string[] _nodeNames = System.Array.Empty<string>();

        [Tooltip("Where this node's edge-to-parent bend points start in the flattened Via array.")]
        [SerializeField] private int[] _viaStart = System.Array.Empty<int>();

        [Tooltip("How many bend points this node's edge-to-parent has. 0 = a straight edge.")]
        [SerializeField] private int[] _viaCount = System.Array.Empty<int>();

        [Tooltip("All edges' bend points, flattened, each run in CHILD→PARENT order (RoutineLaneTree " +
                 "reverses them when the walk goes the other way).")]
        [SerializeField] private Vector2[] _via = System.Array.Empty<Vector2>();

        public Vector2[] NodePositions => _nodePositions ?? System.Array.Empty<Vector2>();
        public int[] NodeParents => _nodeParents ?? System.Array.Empty<int>();
        public string[] NodeNames => _nodeNames ?? System.Array.Empty<string>();
        public int[] ViaStart => _viaStart ?? System.Array.Empty<int>();
        public int[] ViaCount => _viaCount ?? System.Array.Empty<int>();
        public Vector2[] Via => _via ?? System.Array.Empty<Vector2>();

        /// <summary>How many junctions this region declares.</summary>
        public int NodeCount => _nodePositions != null ? _nodePositions.Length : 0;

        /// <summary>Wire the table in one call (the region builder).</summary>
        public void Configure(Vector2[] nodePositions, int[] nodeParents, string[] nodeNames,
                              int[] viaStart, int[] viaCount, Vector2[] via)
        {
            _nodePositions = nodePositions ?? System.Array.Empty<Vector2>();
            _nodeParents = nodeParents ?? System.Array.Empty<int>();
            _nodeNames = nodeNames ?? System.Array.Empty<string>();
            _viaStart = viaStart ?? System.Array.Empty<int>();
            _viaCount = viaCount ?? System.Array.Empty<int>();
            _via = via ?? System.Array.Empty<Vector2>();
        }

        /// <summary>Index of a junction by name, or −1. Ordinal — names are data, not display text.</summary>
        public int IndexOfNode(string name)
        {
            if (string.IsNullOrEmpty(name) || _nodeNames == null) return -1;
            for (int i = 0; i < _nodeNames.Length; i++)
                if (string.Equals(_nodeNames[i], name, System.StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>
        /// Everything that has to be true for this table to be walkable, in one call: the parallel arrays
        /// are the same length, the via slices are inside the via array, and the parent table is a tree.
        /// Returns an empty string when sound, otherwise the reason — so a builder and a test can report
        /// the SAME sentence and there is one copy of the rules.
        /// </summary>
        public string Validate()
        {
            int n = NodeCount;
            if (n == 0) return "no lane nodes at all";
            if (NodeParents.Length != n) return $"{n} node position(s) but {NodeParents.Length} parent(s)";
            if (NodeNames.Length != n) return $"{n} node position(s) but {NodeNames.Length} name(s)";
            if (ViaStart.Length != n) return $"{n} node position(s) but {ViaStart.Length} via-start(s)";
            if (ViaCount.Length != n) return $"{n} node position(s) but {ViaCount.Length} via-count(s)";

            for (int i = 0; i < n; i++)
            {
                if (ViaCount[i] < 0) return $"node {i} has a negative via count";
                if (ViaCount[i] == 0) continue;
                if (ViaStart[i] < 0 || ViaStart[i] + ViaCount[i] > Via.Length)
                    return $"node {i}'s via slice [{ViaStart[i]}, {ViaStart[i] + ViaCount[i]}) " +
                           $"runs outside the {Via.Length}-point via array";
                if (NodeParents[i] == RoutineLaneTree.NoParent)
                    return $"the ROOT (node {i}) carries {ViaCount[i]} bend point(s) — bends live on an " +
                           "edge, and the root has no edge to its parent";
            }

            if (!RoutineLaneTree.IsTree(NodeParents, out int offender))
                return offender >= 0 && offender < n
                    ? $"not a tree: node {offender} ('{NodeNames[offender]}') does not reach a single root"
                    : "not a tree: there is not exactly one root";

            return string.Empty;
        }

#if UNITY_EDITOR
        /// <summary>Draw the lanes in the Scene view — the same reason <see cref="RoutineStations"/> draws
        /// its stations: a lane through a wall walks perfectly and looks like nothing at all.</summary>
        void OnDrawGizmosSelected()
        {
            if (NodeCount == 0 || NodeParents.Length != NodeCount) return;

            Gizmos.color = new Color(0.95f, 0.85f, 0.40f, 0.9f);
            for (int i = 0; i < NodeCount; i++)
            {
                int p = NodeParents[i];
                if (p < 0 || p >= NodeCount) continue;

                Vector2 from = NodePositions[i];
                int start = ViaStart[i], count = ViaCount[i];
                for (int k = 0; k < count && start + k < Via.Length; k++)
                {
                    Gizmos.DrawLine(from, Via[start + k]);
                    from = Via[start + k];
                }
                Gizmos.DrawLine(from, NodePositions[p]);
            }

            Gizmos.color = new Color(0.40f, 0.75f, 0.95f, 0.9f);
            for (int i = 0; i < NodeCount; i++) Gizmos.DrawWireSphere(NodePositions[i], 0.9f);
        }
#endif
    }
}
