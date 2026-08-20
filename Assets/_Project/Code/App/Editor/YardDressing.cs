#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.World;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// ⭐ <b>THE DOORYARDS STAND UP.</b> Turns a region's authored <see cref="Yard"/> table into a fence
    /// you can see and a <see cref="PropertyBoundary"/> you cannot walk through — from the SAME runs, so
    /// the two cannot disagree.
    ///
    /// <para><b>ONE POLYGON, ALREADY AUTHORED, ASKED A THIRD QUESTION.</b> #604 landed the yard polygon
    /// and made the grass gates read it (the mow line and the wild-grass suppression). This pass adds the
    /// third derivation the lawn ruling names: the fence LINE. Nothing here decides where a boundary is —
    /// <see cref="Yard.FenceRuns"/> is the authored fact with the gates already cut out of it, and this
    /// only decides which pieces stand on it and which way each one faces.</para>
    ///
    /// <para><b>⭐⭐ THE FACING COMES FROM THE OUTWARD NORMAL, THROUGH THE BAKE'S MEASURED BEARINGS.</b>
    /// The yard kit's own gameplay sidecar publishes a facing list that is MIRRORED against its art — ask
    /// it for "E" and you get a panel facing WEST, a plausible picture with no error anywhere. So every
    /// piece here is aimed by taking the compass bearing of the segment's outward normal and asking
    /// <see cref="YardCatalog.FacingFor"/>, which searches the bearings the BAKE measured. The sidecar's
    /// labels are never read, and <see cref="Place"/> refuses to place anything at all if the measured
    /// table is missing.</para>
    ///
    /// <para><b>⭐ PLAN AND PLACE ARE SPLIT, AND THE SPLIT IS WHAT MAKES THIS TESTABLE.</b>
    /// <see cref="PlanOne"/> is pure — a table and a contract in, a list of (piece, position, facing)
    /// out, no AssetDatabase, no GameObject. So a determinism test measures the real arithmetic on the
    /// real yard tables whether or not the kit has been baked, instead of quietly passing on an empty
    /// scene the day the sheets are missing.</para>
    ///
    /// <para><b>⚠ NO COLLISION-LAYER DECISION IS MADE HERE, ON PURPOSE.</b> The boundary's colliders land
    /// on the default layer of a fresh child, exactly as <see cref="PropertyBoundary"/> makes them.
    /// Choosing the layer and the collision matrix is ONE call, made once, in that component — when the
    /// wharf, the shipyard and the cliff foot migrate onto it. Making it here, per caller, is how a game
    /// ends up with four wall systems, which is the thing that component exists to have stopped.</para>
    /// </summary>
    public static class YardDressing
    {
        /// <summary>The scene object every yard's pieces parent under. Rebuilding destroys it whole,
        /// which is what makes a Refresh converge rather than stack a second fence on the first.</summary>
        public const string RootName = "Yards";

        /// <summary>The child a yard's blocking wall hangs on, beside its fence art.</summary>
        public const string BoundaryChildName = "Boundary";

        /// <summary>
        /// How far outside a boundary to probe when deciding which way is OUT (m).
        ///
        /// <para>Big enough to clear any floating-point ambiguity at an edge, small enough that it cannot
        /// cross a yard: the narrowest yard in either region's table is 11 m across, so a quarter-metre
        /// probe from an edge midpoint lands well inside on one side and well outside on the other.</para>
        /// </summary>
        public const float OutwardProbeMetres = 0.25f;

        // ---- the style table ----------------------------------------------------------------------

        /// <summary>Which baked pieces a fence style is built from. A property changes character by
        /// changing one word in its yard row; this is where that word becomes art.</summary>
        public readonly struct Style
        {
            /// <summary>The repeating length. Null for <see cref="YardFence.None"/>.</summary>
            public readonly string PanelKey;

            /// <summary>The corner and gate-jamb post, or null where the style has none.</summary>
            public readonly string PostKey;

            /// <summary>The gate leaf, or null where the style's opening is just an opening.</summary>
            public readonly string GateKey;

            public Style(string panel, string post, string gate)
            {
                PanelKey = panel; PostKey = post; GateKey = gate;
            }

            public bool Builds => !string.IsNullOrEmpty(PanelKey);
        }

        /// <summary>
        /// Style → pieces.
        ///
        /// <para><b>⚠ Post and rail, split rail and wire get NO post, and that is the kit's answer rather
        /// than a gap in this table.</b> The kit's <c>fenceCorner</c> is a PICKET corner (its only variant
        /// is <c>picket</c>) and <c>stonePillar</c> is masonry — putting either on a rail fence would read
        /// as a different fence meeting itself at every turn. A rail fence's own panel carries its end
        /// posts, which is what one actually looks like where it turns.</para>
        ///
        /// <para><b>⚠ Only the picket has a gate LEAF.</b> The kit draws one gate and it is a picket gate.
        /// For every other style the opening is an opening — jambs where the style has posts, and
        /// otherwise the gap itself, which is what a farm gateway looks like. The walk-through is the GAP
        /// in the runs either way, so a missing leaf costs a picture and never a path.</para>
        /// </summary>
        public static Style StyleFor(YardFence fence)
        {
            switch (fence)
            {
                case YardFence.Picket:    return new Style("picketPanel", "fenceCorner", "picketGate");
                case YardFence.PostRail:  return new Style("postRail", null, null);
                case YardFence.SplitRail: return new Style("splitRail", null, null);
                case YardFence.Wire:      return new Style("wireFence", null, null);
                case YardFence.Stone:     return new Style("stoneWall", "stonePillar", null);
                case YardFence.Hedge:     return new Style("hedgeRun", "hedgeCorner", null);
                default:                  return new Style(null, null, null);   // YardFence.None
            }
        }

        /// <summary>Every piece key any style can ask for — what a bake must cover for both regions to
        /// dress completely. Derived from <see cref="StyleFor"/> rather than listed, so a style that
        /// gains a piece cannot be forgotten here.</summary>
        public static IReadOnlyList<string> RequiredPieceKeys()
        {
            var keys = new List<string>();
            foreach (YardFence fence in System.Enum.GetValues(typeof(YardFence)))
            {
                var s = StyleFor(fence);
                Add(keys, s.PanelKey);
                Add(keys, s.PostKey);
                Add(keys, s.GateKey);
            }
            return keys;

            void Add(List<string> into, string key)
            {
                if (!string.IsNullOrEmpty(key) && !into.Contains(key)) into.Add(key);
            }
        }

        // ---- the plan -------------------------------------------------------------------------------

        /// <summary>One piece the plan calls for — where it stands and which baked cell it shows.</summary>
        public readonly struct PlannedPiece
        {
            public readonly string Key;
            public readonly YardKit.Role Role;
            public readonly Vector2 Position;

            /// <summary>The BAKED CELL index. Cell k faces 45k° from north on a healthy bake — measured,
            /// never assumed, and chosen by searching the contract's own bearings.</summary>
            public readonly int Facing;

            /// <summary>The outward bearing this piece was aimed at (degrees clockwise from north), kept
            /// so a test can bound the aiming error instead of re-deriving the choice and finding it
            /// agrees with itself.</summary>
            public readonly float OutwardBearing;

            public PlannedPiece(string key, YardKit.Role role, Vector2 position, int facing,
                                float outwardBearing)
            {
                Key = key; Role = role; Position = position; Facing = facing;
                OutwardBearing = outwardBearing;
            }

            public override string ToString() =>
                $"{Key} d{Facing} at ({Position.x:0.00},{Position.y:0.00}) out {OutwardBearing:0.0}°";
        }

        /// <summary>What one yard's boundary comes to, before a single GameObject exists.</summary>
        public sealed class BoundaryPlan
        {
            public string YardName;
            public YardFence Fence;

            /// <summary>The pieces, in a fixed order: every run's panels and posts in run order, then the
            /// gates. Order is part of the determinism claim, so nothing here sorts or shuffles.</summary>
            public readonly List<PlannedPiece> Pieces = new List<PlannedPiece>();

            /// <summary>The runs the boundary is built from — the same list the art stands on.</summary>
            public List<Vector2[]> Runs = new List<Vector2[]>();

            /// <summary>False for a <see cref="YardFence.None"/> row: no fence AND no wall, because
            /// walling an unfenced lawn is the invisible-wall defect the owner's walk found three of.
            /// </summary>
            public bool Fenced;

            /// <summary>The style asked for a piece the contract does not carry — the kit is unbaked or
            /// the build table lost a row. Named rather than skipped.</summary>
            public readonly List<string> MissingPieces = new List<string>();

            /// <summary>⚠ Segments too short for one panel of their own style, and the metres they leave
            /// bare. Reported rather than swallowed: a silent skip reads as "covered everything" when it
            /// did not.</summary>
            public int OffcutsSkipped;
            public float OffcutMetres;

            /// <summary>Metres of blocking edge the runs describe.</summary>
            public float BlockingMetres;

            public int CountOf(YardKit.Role role)
            {
                int n = 0;
                for (int i = 0; i < Pieces.Count; i++) if (Pieces[i].Role == role) n++;
                return n;
            }

            public override string ToString() =>
                $"{YardName} [{Fence}] {CountOf(YardKit.Role.Panel)} panel(s) + " +
                $"{CountOf(YardKit.Role.Post)} post(s) + {CountOf(YardKit.Role.Gate)} gate(s), " +
                $"{BlockingMetres:0.0} m of wall";
        }

        /// <summary>
        /// <b>The whole arithmetic of one yard's boundary, pure.</b> No AssetDatabase, no GameObject, no
        /// scene — a yard row and a contract in, a deterministic list of placements out.
        /// </summary>
        public static BoundaryPlan PlanOne(in Yard yard, YardKit.Contract contract)
        {
            var plan = new BoundaryPlan { YardName = yard.Name, Fence = yard.Fence };

            var style = StyleFor(yard.Fence);
            if (!style.Builds) return plan;

            var panel = contract?.Find(style.PanelKey);
            if (panel == null || panel.footprintWidthMetres <= 0f)
            {
                plan.MissingPieces.Add(style.PanelKey);
                return plan;
            }

            plan.Runs = yard.FenceRuns();
            if (plan.Runs.Count == 0) return plan;

            plan.Fenced = true;

            var post = string.IsNullOrEmpty(style.PostKey) ? null : contract.Find(style.PostKey);
            if (post == null && !string.IsNullOrEmpty(style.PostKey)) plan.MissingPieces.Add(style.PostKey);

            var gate = string.IsNullOrEmpty(style.GateKey) ? null : contract.Find(style.GateKey);
            if (gate == null && !string.IsNullOrEmpty(style.GateKey)) plan.MissingPieces.Add(style.GateKey);

            foreach (var run in plan.Runs)
            {
                if (run == null || run.Length < 2) continue;

                // ⚠ A run TURNS CORNERS — PerimeterRuns emits every vertex it passes, so one run is a
                // polyline with bends, not a straight line (its own docs say a wall builder splits on the
                // bends itself). A panel is straight, so the tiling is per SEGMENT.
                bool closed = (run[0] - run[run.Length - 1]).sqrMagnitude < 1e-6f;

                for (int s = 0; s + 1 < run.Length; s++)
                    TileSegment(yard, run[s], run[s + 1], panel, contract, plan);

                if (post == null) continue;

                // Interior vertices are corners; the two ENDS of an open run are the jambs of the gate
                // it was cut at. ⚠ A CLOSED run has no ends — its LAST point repeats its first — so the
                // last index is dropped and point 0 is posted ONCE, taking the wrapped segment as its
                // other neighbour. Dropping point 0 instead (the obvious symmetric-looking cut) leaves
                // an unfenced yard's boundary one post short at exactly one corner, which is a mitre
                // gap visible from one side only.
                int first = 0;
                int last = closed ? run.Length - 1 : run.Length;
                for (int v = first; v < last; v++)
                    PlanPost(yard, run, v, closed, post, contract, plan);
            }

            if (gate != null) PlanGates(yard, gate, contract, plan);

            plan.BlockingMetres = RunMetres(plan.Runs);
            return plan;
        }

        /// <summary>Every yard's plan, in table order.</summary>
        public static List<BoundaryPlan> PlanAll(IReadOnlyList<Yard> yards, YardKit.Contract contract)
        {
            var plans = new List<BoundaryPlan>();
            if (yards == null) return plans;
            foreach (var yard in yards) plans.Add(PlanOne(yard, contract));
            return plans;
        }

        // ---- tiling -----------------------------------------------------------------------------------

        /// <summary>
        /// Lay panels end to end along one straight segment.
        ///
        /// <para><b>⌈L/w⌉ panels, never ⌊L/w⌋ and never round().</b> Rounding down or to nearest can leave
        /// the pitch LONGER than a panel — a visible hole in a fence — while rounding up can only make
        /// panels overlap, and an overlap between two lengths of the same repeating art is invisible. The
        /// overlap it buys is under one panel and averages a few per cent.</para>
        ///
        /// <para><b>⚠ A segment shorter than one panel gets NOTHING, and it is COUNTED.</b> Centring a
        /// single panel on it would overhang both ends by up to half a panel — and the segments this
        /// happens to are the offcuts beside a gate, so the overhang would draw straight across the
        /// opening. Measured, no segment in either region's table is short enough today (the shortest is
        /// a 13 m frontage's half-front at 5.6 m against a longest panel of about 3 m), which is why this
        /// is reported rather than solved.</para>
        /// </summary>
        static void TileSegment(in Yard yard, Vector2 a, Vector2 b, YardKit.Entry panel,
                                YardKit.Contract contract, BoundaryPlan plan)
        {
            Vector2 along = b - a;
            float length = along.magnitude;
            if (length <= 1e-4f) return;
            along /= length;

            float width = panel.footprintWidthMetres;
            if (length < width)
            {
                plan.OffcutsSkipped++;
                plan.OffcutMetres += length;
                return;
            }

            Vector2 outward = OutwardOf(yard, a, b);
            float bearing = YardCatalog.BearingOf(outward);
            int facing = YardCatalog.FacingFor(contract, bearing);

            int count = Mathf.CeilToInt(length / width - 1e-4f);
            float pitch = length / count;

            for (int i = 0; i < count; i++)
                plan.Pieces.Add(new PlannedPiece(panel.key, YardKit.Role.Panel,
                                                 a + along * ((i + 0.5f) * pitch), facing, bearing));
        }

        /// <summary>A post at one vertex of a run, facing the way the boundary faces there.</summary>
        static void PlanPost(in Yard yard, Vector2[] run, int vertex, bool closed, YardKit.Entry post,
                             YardKit.Contract contract, BoundaryPlan plan)
        {
            // The face at a bend is the sum of the two segment normals that meet there; at an end it is
            // the one segment's. Summing the NORMALS rather than averaging the BEARINGS is deliberate —
            // the mean of 350° and 10° is 180°, which points a gatepost into the yard.
            Vector2 here = run[vertex];
            Vector2 outward = Vector2.zero;

            if (vertex > 0) outward += OutwardOf(yard, run[vertex - 1], here);
            if (vertex + 1 < run.Length) outward += OutwardOf(yard, here, run[vertex + 1]);
            if (closed && vertex == 0) outward += OutwardOf(yard, run[run.Length - 2], here);

            if (outward.sqrMagnitude < 1e-8f) outward = here - yard.Polygon.Centre;

            float bearing = YardCatalog.BearingOf(outward);
            plan.Pieces.Add(new PlannedPiece(post.key, YardKit.Role.Post, here,
                                             YardCatalog.FacingFor(contract, bearing), bearing));
        }

        /// <summary>The gate leaf, standing in the opening the boundary was cut at.</summary>
        static void PlanGates(in Yard yard, YardKit.Entry gate, YardKit.Contract contract, BoundaryPlan plan)
        {
            foreach (Vector2 authored in yard.Gates)
            {
                // ⚠ Snapped to the boundary, never placed at the authored point. A gate is authored as
                // "the road" or "the village green" — a point that can be tens of metres away — and
                // PerimeterRuns cuts the gap at its NEAREST POINT ON THE PERIMETER. Standing the leaf at
                // the authored point instead would put it out in a field beside a hole in a fence.
                if (!NearestOnBoundary(yard.Polygon, authored, out Vector2 at,
                                       out Vector2 segA, out Vector2 segB)) continue;

                float bearing = YardCatalog.BearingOf(OutwardOf(yard, segA, segB));
                plan.Pieces.Add(new PlannedPiece(gate.key, YardKit.Role.Gate, at,
                                                 YardCatalog.FacingFor(contract, bearing), bearing));
            }
        }

        // ---- geometry ---------------------------------------------------------------------------------

        /// <summary>
        /// The unit normal of segment <c>a→b</c> that points OUT of the yard.
        ///
        /// <para><b>Decided by asking the polygon, not by assuming a winding.</b> Either normal is the
        /// outward one depending on which way the vertices were authored, and a winding assumption is
        /// exactly the kind of thing that holds until somebody writes a yard the other way round and then
        /// faces every fence on that property inward — a mirror, with no error.</para>
        /// </summary>
        public static Vector2 OutwardOf(in Yard yard, Vector2 a, Vector2 b)
        {
            Vector2 along = b - a;
            if (along.sqrMagnitude < 1e-10f) return Vector2.up;
            along.Normalize();

            Vector2 normal = new Vector2(along.y, -along.x);
            Vector2 mid = (a + b) * 0.5f;

            bool rightIsOutside = !yard.Polygon.Contains(mid + normal * OutwardProbeMetres);
            bool leftIsOutside = !yard.Polygon.Contains(mid - normal * OutwardProbeMetres);

            if (rightIsOutside && !leftIsOutside) return normal;
            if (leftIsOutside && !rightIsOutside) return -normal;

            // Both or neither: the segment is not this polygon's boundary in the way we assumed. Fall
            // back to "away from the middle of the yard", which is the honest answer for any convex shape
            // and never worse than a coin toss for a concave one.
            Vector2 away = mid - yard.Polygon.Centre;
            return away.sqrMagnitude < 1e-10f ? normal : away.normalized;
        }

        /// <summary>
        /// The point on the polygon's perimeter nearest <paramref name="p"/>, and the edge it sits on.
        ///
        /// <para>Local rather than a new <c>GroundPolygon</c> member: the polygon already answers "how far
        /// along the perimeter" (<c>ArcLengthOfNearestPoint</c>, which is what cuts the gap), and this
        /// wants the point and its EDGE so the leaf can take that edge's outward normal. Both walk the
        /// same edges and agree by construction.</para>
        /// </summary>
        public static bool NearestOnBoundary(in GroundPolygon polygon, Vector2 p, out Vector2 nearest,
                                             out Vector2 edgeA, out Vector2 edgeB)
        {
            nearest = p; edgeA = Vector2.zero; edgeB = Vector2.zero;
            if (!polygon.IsValid) return false;

            float best = float.MaxValue;
            for (int i = 0; i < polygon.Count; i++)
            {
                polygon.Edge(i, out Vector2 a, out Vector2 b);
                Vector2 ab = b - a;
                float lenSq = ab.sqrMagnitude;
                Vector2 on = lenSq <= 1e-12f
                    ? a
                    : a + ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);

                float d = Vector2.SqrMagnitude(p - on);
                if (d >= best) continue;
                best = d; nearest = on; edgeA = a; edgeB = b;
            }
            return best < float.MaxValue;
        }

        static float RunMetres(List<Vector2[]> runs)
        {
            float total = 0f;
            if (runs == null) return total;
            foreach (var run in runs)
            {
                if (run == null) continue;
                for (int s = 0; s + 1 < run.Length; s++) total += Vector2.Distance(run[s], run[s + 1]);
            }
            return total;
        }

        // ---- the scene ---------------------------------------------------------------------------------

        public sealed class Result
        {
            public GameObject Root;
            public int YardsDressed, YardsUnfenced, Panels, Posts, Gates, Boundaries;
            public float BlockingMetres;
            public int OffcutsSkipped;
            public float OffcutMetres;

            /// <summary>Pieces a style asked for that the contract or the sheets could not supply. A
            /// region dresses with what has landed rather than throwing halfway through a build, so this
            /// is how an unbaked kit reports itself.</summary>
            public readonly List<string> Missing = new List<string>();

            public readonly List<string> Notes = new List<string>();

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.Append($"{YardsDressed} yard(s) fenced ({YardsUnfenced} unfenced by their own row), " +
                          $"{Panels} panel(s) + {Posts} post(s) + {Gates} gate(s), {Boundaries} " +
                          $"boundary component(s) carrying {BlockingMetres:0.0} m of wall");
                if (OffcutsSkipped > 0)
                    sb.Append($"; {OffcutsSkipped} offcut(s) under one panel left bare " +
                              $"({OffcutMetres:0.0} m)");
                if (Missing.Count > 0) sb.Append($"; MISSING: {string.Join(", ", Missing)}");
                foreach (string note in Notes) sb.Append($"; {note}");
                return sb.ToString();
            }
        }

        /// <summary>
        /// Fence every yard in <paramref name="yards"/> and stand its boundary behind the art.
        ///
        /// <para>Deterministic and region-agnostic: the same table in gives the same objects out, in the
        /// same order, with the same names — which is what lets an EditMode test measure the arithmetic
        /// without a region build.</para>
        /// </summary>
        public static Result Place(IReadOnlyList<Yard> yards, Transform parent = null,
                                   string regionLabel = null)
        {
            var result = new Result();
            if (!string.IsNullOrEmpty(regionLabel)) result.Notes.Add(regionLabel);

            var contract = YardKit.Load();
            if (contract == null)
            {
                result.Notes.Add(
                    "no yard contract — the kit bakes to order. Run Hidden Harbours ▸ Art ▸ Bake Yard " +
                    "Kit. Every yard's POLYGON still gates the grass; only the fence is missing.");
                return result;
            }

            if (!YardCatalog.HasMeasuredBearings(contract))
            {
                // Refusing rather than placing: without the measured table there is nothing to aim a
                // panel with except the sidecar's mirrored labels, and taking those would stand every
                // fence in the region facing the wrong way with no error anywhere.
                result.Notes.Add(
                    "the yard contract carries no measured facing table, so no piece can be aimed. " +
                    "Re-bake the kit — YardRegistrationProbe writes cellFaceBearings at every bake, and " +
                    "a contract without them predates the measurement that showed the kit's own sidecar " +
                    "to be mirrored.");
                return result;
            }

            var root = new GameObject(RootName);
            if (parent != null) root.transform.SetParent(parent, worldPositionStays: false);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;
            result.Root = root;

            if (yards == null) return result;

            // ⚠ Both of these are about SCALE. The two tables come to a few hundred pieces, so a
            // per-piece LoadAllAssetsAtPath would re-read ten sheets a few hundred times, and an unbaked
            // kit would report one "missing" line PER PIECE — a few hundred lines of the same three
            // facts, which is how a real finding gets buried. Cache the sprite; report each miss once.
            var sprites = new Dictionary<(string Key, int Facing), Sprite>();
            var missed = new HashSet<string>();

            for (int i = 0; i < yards.Count; i++)
            {
                var yard = yards[i];
                var plan = PlanOne(yard, contract);

                foreach (string missing in plan.MissingPieces)
                    if (missed.Add(missing)) result.Missing.Add(missing);

                if (!plan.Fenced) { result.YardsUnfenced++; continue; }

                var yardGo = new GameObject(yard.Name);
                yardGo.transform.SetParent(root.transform, worldPositionStays: false);
                yardGo.transform.localPosition = Vector3.zero;
                yardGo.transform.localRotation = Quaternion.identity;
                yardGo.transform.localScale = Vector3.one;

                int index = 0;
                foreach (var piece in plan.Pieces)
                {
                    var placement = YardCatalog.Find(contract, piece.Key);

                    var slot = (piece.Key, piece.Facing);
                    if (!sprites.TryGetValue(slot, out Sprite sprite))
                    {
                        sprite = YardCatalog.LoadFacing(placement, piece.Facing);
                        sprites[slot] = sprite;
                    }

                    if (sprite == null)
                    {
                        string miss = $"{piece.Key} d{piece.Facing}";
                        if (missed.Add(miss)) result.Missing.Add(miss);
                        continue;
                    }

                    Stand(placement, sprite, piece.Position, yardGo.transform,
                          $"{piece.Key}_{index++:000}");

                    switch (piece.Role)
                    {
                        case YardKit.Role.Panel: result.Panels++; break;
                        case YardKit.Role.Post: result.Posts++; break;
                        case YardKit.Role.Gate: result.Gates++; break;
                    }
                }

                result.BlockingMetres += StandBoundary(yard, plan.Runs, yardGo.transform);
                result.Boundaries++;
                result.OffcutsSkipped += plan.OffcutsSkipped;
                result.OffcutMetres += plan.OffcutMetres;
                result.YardsDressed++;
            }

            return result;
        }

        /// <summary>
        /// The blocking wall, from the SAME runs the art was just built on.
        ///
        /// <para>⚠ <see cref="PropertyBoundary.Configure"/> is translation-only and now ASSERTS an
        /// identity rotation and unit scale — so the object it lands on is created flat and parented with
        /// <c>worldPositionStays: false</c> under roots that are themselves flat. Parenting a boundary
        /// under a rotated or scaled transform throws rather than silently disagreeing with physics.
        /// </para>
        /// </summary>
        static float StandBoundary(in Yard yard, List<Vector2[]> runs, Transform parent)
        {
            var go = new GameObject(BoundaryChildName);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            // At the yard's own centre, so the stored LOCAL points are yard-sized rather than
            // world-sized — the same reason a region's props are not all children of the origin.
            Vector2 centre = yard.Polygon.Centre;
            go.transform.position = new Vector3(centre.x, centre.y, 0f);

            var boundary = go.AddComponent<PropertyBoundary>();
            boundary.Configure(runs, YardPlan.FenceThicknessMetres,
                               $"{yard.Name}: {yard.Fence} fence — {yard.Reason}");
            return boundary.TotalLengthMetres();
        }

        static void Stand(YardCatalog.Placement piece, Sprite sprite, Vector2 at, Transform parent,
                          string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);

            // The pivot IS the piece's ground centre (measured on intake), so the boundary point is the
            // position — no offset, no rescale. A bottom-centre pivot would bury it, silently.
            go.transform.position = new Vector3(at.x, at.y, 0f);

            YardCatalog.Configure(go, piece, sprite);
        }
    }
}
#endif
