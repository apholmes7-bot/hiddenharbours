using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// ⭐ <b>THE ONE THING THAT STOPS A PLAYER WALKING WHERE THEY MAY NOT.</b> A list of world runs — open
    /// polylines — turned into blocking colliders. A fence VISUALISES one of these; it does not own one.
    /// So does a quay edge, a yard gate, a shipyard hoarding and (when that pass comes) the foot of a
    /// cliff.
    ///
    /// <para><b>⚠ THIS EXISTS BECAUSE THERE WERE ALREADY THREE.</b> The owner's 2026-08-19 walk found the
    /// wharf with NO wall (you walk off the quay into the basin), the shipyard's wall in the WRONG PLACE,
    /// and cliffs he wants walled that have nothing at all. Each of those grew its own collider code in
    /// its own builder, which is why each is broken in its own way and why fixing one fixes none of the
    /// others. A fence-shaped fourth would have made it worse. <b>Every new blocking edge in this game
    /// goes through this component</b>, and the wharf/shipyard/cliff pass moves the existing three onto
    /// it rather than adding a fifth.</para>
    ///
    /// <para><b>The runs are the authored fact; the colliders are derived.</b> Points are stored in LOCAL
    /// space, so moving or re-parenting the object moves its walls with it, and <see cref="Rebuild"/> is
    /// idempotent — it destroys what it made before making it again (ADR 0019's converge-don't-stack rule
    /// at component scale).</para>
    ///
    /// <para><b>⚠ ONE COLLIDER PER SEGMENT, NEVER ONE COLLIDER WITH MANY PATHS.</b> Several paths on one
    /// <see cref="PolygonCollider2D"/> turn their overlaps into HOLES — and every corner of every run is
    /// an overlap, so a single-collider version leaks the player through precisely the corners.
    /// <c>ShopPlacement</c> learned this on the shop walls; it is restated here because the shape of the
    /// bug (a player who "sometimes" slips through) reads as a physics glitch rather than as a modelling
    /// mistake.</para>
    ///
    /// <para><b>No lifecycle magic on purpose.</b> Nothing here builds on <c>OnEnable</c>: EditMode never
    /// raises it, and a component that silently builds itself at a default size is the trap
    /// <c>[ExecuteAlways]</c> builders keep falling into. A builder calls <see cref="Configure"/>, the
    /// colliders exist from that moment, and the scene carries them.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PropertyBoundary : MonoBehaviour
    {
        /// <summary>One open polyline of blocking edge, in the component's LOCAL space.</summary>
        [Serializable]
        public sealed class Run
        {
            [Tooltip("Local-space points. Two or more; consecutive pairs each become one wall quad.")]
            public Vector2[] Points = Array.Empty<Vector2>();

            public Run() { }
            public Run(IReadOnlyList<Vector2> points)
            {
                Points = new Vector2[points.Count];
                for (int i = 0; i < points.Count; i++) Points[i] = points[i];
            }
        }

        /// <summary>The child every generated collider hangs on, so a rebuild has one thing to destroy
        /// and the runs' own object stays whatever the builder parented it to.</summary>
        public const string CollidersChildName = "Walls";

        /// <summary>Default wall thickness (m). A wall wants to be thicker than the fastest thing that
        /// crosses it moves in one physics step, or a tunnelling body passes straight through; 0.35 m is
        /// comfortably over the player's per-step travel and still thin enough that a fence reads as a
        /// line rather than a kerb.</summary>
        public const float DefaultThicknessMetres = 0.35f;

        [SerializeField] List<Run> _runs = new List<Run>();
        [SerializeField] float _thicknessMetres = DefaultThicknessMetres;

        /// <summary>What this boundary is FOR, in words, carried into the scene. Not read by any code —
        /// it is here because the failure mode is a wall in a place nobody can explain, and the fix
        /// starts with knowing who put it there.</summary>
        [SerializeField] string _reason = "";

        /// <summary>The runs as stored — local space. Read-only to callers: mutate through
        /// <see cref="Configure"/> so the colliders can never disagree with the points.</summary>
        public IReadOnlyList<Run> Runs => _runs;

        /// <summary>Wall thickness in metres.</summary>
        public float ThicknessMetres => _thicknessMetres;

        /// <summary>Why this boundary exists.</summary>
        public string Reason => _reason;

        /// <summary>How many wall quads the current runs describe — the number of colliders
        /// <see cref="Rebuild"/> will make, computable without making them.</summary>
        public int SegmentCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _runs.Count; i++)
                {
                    var pts = _runs[i]?.Points;
                    if (pts != null && pts.Length >= 2) n += pts.Length - 1;
                }
                return n;
            }
        }

        /// <summary>
        /// Adopt <paramref name="worldRuns"/> and rebuild the colliders. World points in, because every
        /// caller has world geometry (a yard polygon, a quay line, a cliff foot) and converting in one
        /// place is how the conversion cannot be got wrong in five.
        /// </summary>
        public void Configure(IEnumerable<IReadOnlyList<Vector2>> worldRuns, float thicknessMetres = DefaultThicknessMetres,
                              string reason = null)
        {
            _runs.Clear();
            _thicknessMetres = Mathf.Max(0.01f, thicknessMetres);
            if (reason != null) _reason = reason;

            Vector2 origin = transform.position;
            if (worldRuns != null)
            {
                foreach (var run in worldRuns)
                {
                    if (run == null || run.Count < 2) continue;
                    var local = new Vector2[run.Count];
                    for (int i = 0; i < run.Count; i++) local[i] = run[i] - origin;
                    _runs.Add(new Run(local));
                }
            }

            Rebuild();
        }

        /// <summary>
        /// Regenerate the colliders from the stored runs. Destroys what it made last time first, so
        /// calling it twice leaves one wall rather than two.
        /// </summary>
        [ContextMenu("Rebuild Walls")]
        public void Rebuild()
        {
            Transform existing = transform.Find(CollidersChildName);
            if (existing != null) DestroyChild(existing.gameObject);

            if (SegmentCount == 0) return;

            var wallsGo = new GameObject(CollidersChildName);
            wallsGo.transform.SetParent(transform, worldPositionStays: false);
            wallsGo.transform.localPosition = Vector3.zero;

            float half = _thicknessMetres * 0.5f;

            for (int r = 0; r < _runs.Count; r++)
            {
                var pts = _runs[r]?.Points;
                if (pts == null || pts.Length < 2) continue;

                for (int s = 0; s + 1 < pts.Length; s++)
                {
                    Vector2 a = pts[s], b = pts[s + 1];
                    Vector2 along = b - a;
                    float len = along.magnitude;
                    if (len <= 1e-4f) continue;                       // a duplicated point is not a wall
                    along /= len;
                    Vector2 side = new Vector2(-along.y, along.x) * half;

                    // ⚠ Each quad runs half a thickness PAST both ends. Butt-ended quads meet at a corner
                    // point and leave a wedge of nothing on the outside of the turn — a pinhole exactly
                    // where a player pressed into a corner is trying to get through. Overlapping is free
                    // here because each quad is its own collider (see the type remarks).
                    Vector2 ext = along * half;
                    Vector2 p0 = a - ext + side, p1 = b + ext + side;
                    Vector2 p2 = b + ext - side, p3 = a - ext - side;

                    var collider = wallsGo.AddComponent<PolygonCollider2D>();
                    collider.pathCount = 1;
                    collider.SetPath(0, new[] { p0, p1, p2, p3 });
                }
            }
        }

        /// <summary>The runs in WORLD space — what a fence placer walks to put art on the wall, and what
        /// a test measures.</summary>
        public List<Vector2[]> WorldRuns()
        {
            Vector2 origin = transform.position;
            var runs = new List<Vector2[]>(_runs.Count);
            for (int r = 0; r < _runs.Count; r++)
            {
                var pts = _runs[r]?.Points;
                if (pts == null || pts.Length < 2) continue;
                var world = new Vector2[pts.Length];
                for (int i = 0; i < pts.Length; i++) world[i] = pts[i] + origin;
                runs.Add(world);
            }
            return runs;
        }

        /// <summary>Total metres of blocking edge — the fence bill, and the one number that says at a
        /// glance whether a boundary got built at all.</summary>
        public float TotalLengthMetres()
        {
            float total = 0f;
            for (int r = 0; r < _runs.Count; r++)
            {
                var pts = _runs[r]?.Points;
                if (pts == null) continue;
                for (int s = 0; s + 1 < pts.Length; s++) total += Vector2.Distance(pts[s], pts[s + 1]);
            }
            return total;
        }

        static void DestroyChild(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }

#if UNITY_EDITOR
        /// <summary>Draw the runs when selected. A misplaced wall is invisible in play and obvious here —
        /// which is exactly the shipyard defect from the owner's walk.</summary>
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.95f, 0.75f, 0.2f, 0.9f);
            Vector3 origin = transform.position;
            for (int r = 0; r < _runs.Count; r++)
            {
                var pts = _runs[r]?.Points;
                if (pts == null) continue;
                for (int s = 0; s + 1 < pts.Length; s++)
                    Gizmos.DrawLine(origin + (Vector3)pts[s], origin + (Vector3)pts[s + 1]);
            }
        }
#endif
    }
}
