#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>What kind of boundary a property is enclosed with. Data, not art: the placement pass
    /// resolves each of these to the yard kit's own pieces, and a property changes its character by
    /// changing this one word.</summary>
    public enum YardFence
    {
        /// <summary>No fence. The mow line alone says where the property ends — which is what most of
        /// a rural road actually looks like, and the reason this is a value rather than an absence.
        /// </summary>
        None,

        /// <summary>Painted pickets. The village front, and the one that reads as KEPT.</summary>
        Picket,

        /// <summary>Post and rail. A working property, a bigger frontage, no paint to keep up.</summary>
        PostRail,

        /// <summary>Split rail — the oldest and the cheapest, and what a place that has been let go
        /// still has standing.</summary>
        SplitRail,

        /// <summary>Page wire on posts. Keeps a dog in and a deer out; a working yard, not a front.
        /// </summary>
        Wire,

        /// <summary>Fieldstone. What the ground gave up when it was cleared, stacked at the edge of the
        /// land it came out of.</summary>
        Stone,

        /// <summary>A clipped hedge instead of a fence. Blocks the same ground, reads as care.</summary>
        Hedge,
    }

    /// <summary>
    /// <b>ONE YARD — the ground a property holds, and what encloses it.</b> A row in a region's yard
    /// table, and the single authored fact the lawn, the wild-grass suppression and the fence all read.
    ///
    /// <para><b>⭐ AUTHORED ONCE, DERIVED THREE WAYS</b> (lead-architect's lawn ruling, 2026-08-20). The
    /// polygon is the mow line, the grass gate and the fence line — not three shapes that have to be
    /// kept in agreement, one shape asked three questions. The failure this prevents is the one that is
    /// invisible until you walk it: a lawn painted to one edge, wild grass cleared to another, and a
    /// fence standing on a third.</para>
    ///
    /// <para><b>The geometry is DERIVED from what the yard faces</b>, not authored as an angle. A
    /// dooryard is between the house and the road, so the row states the house it belongs to, how big
    /// the yard is, and what it faces; <see cref="Facing"/> does the arithmetic. Move the road and the
    /// yard turns to follow it — which is exactly what a magic rotation number cannot do (rule 6).</para>
    /// </summary>
    public readonly struct Yard
    {
        /// <summary>Stable key — how a test, a builder or a later ownership system names this yard.
        /// </summary>
        public readonly string Name;

        /// <summary>The building this yard belongs to, in world XY. The same point the region's site
        /// list carries, read from the builder's own constant rather than copied, so moving a building
        /// moves its yard.</summary>
        public readonly Vector2 Owner;

        /// <summary>The ground that building reserves at any facing (m) — its footprint half-diagonal.
        /// The yard must CONTAIN this disc or grass grows through the house; <c>YardContentTests</c>
        /// fails rather than trusting the author's eye.</summary>
        public readonly float OwnerRadiusMetres;

        /// <summary>The boundary itself.</summary>
        public readonly GroundPolygon Polygon;

        /// <summary>What stands on the boundary.</summary>
        public readonly YardFence Fence;

        /// <summary>Where the fence OPENS, as points near the boundary — usually the thing the path
        /// arrives from. Each is snapped to its nearest point on the perimeter, so authoring the road
        /// centre or the village green is enough and no gate coordinate is ever hand-fitted to a corner.
        /// </summary>
        public readonly Vector2[] Gates;

        /// <summary>Why this yard is this shape. Prose, carried in the data, for the same reason
        /// <c>WoodlandZone.Reason</c> is.</summary>
        public readonly string Reason;

        public Yard(string name, Vector2 owner, float ownerRadiusMetres, GroundPolygon polygon,
                    YardFence fence, Vector2[] gates, string reason)
        {
            Name = name;
            Owner = owner;
            OwnerRadiusMetres = ownerRadiusMetres;
            Polygon = polygon;
            Fence = fence;
            Gates = gates ?? System.Array.Empty<Vector2>();
            Reason = reason;
        }

        /// <summary>Is this ground inside the yard? The mow line, asked as a predicate.</summary>
        public bool Contains(Vector2 p) => Polygon.Contains(p);

        /// <summary>Ground held, m².</summary>
        public float AreaMetresSquared => Polygon.Area;

        /// <summary>
        /// The fence line, broken at the gates — open runs in world XY, ready for both the art and the
        /// <see cref="PropertyBoundary"/> behind it.
        ///
        /// <para>A <see cref="YardFence.None"/> yard returns nothing: no fence, no wall, and the mow line
        /// carries the whole statement. That is a real look on this coast and not a hole in the data.
        /// </para>
        /// </summary>
        public List<Vector2[]> FenceRuns(float gateWidthMetres = YardPlan.GateWidthMetres) =>
            Fence == YardFence.None
                ? new List<Vector2[]>()
                : Polygon.PerimeterRuns(Gates, gateWidthMetres);

        /// <summary>
        /// <b>A dooryard: a rectangle between a building and whatever it faces.</b> The factory nearly
        /// every row uses — see the type remarks for why the shape is derived rather than authored.
        /// </summary>
        /// <param name="name">Stable key.</param>
        /// <param name="owner">The building's site.</param>
        /// <param name="ownerRadiusMetres">Its footprint half-diagonal.</param>
        /// <param name="sizeMetres">Frontage ACROSS × depth from the back boundary to the front one.</param>
        /// <param name="frontToward">What the yard faces — the road, the green, the path, the quay.</param>
        /// <param name="fence">What encloses it.</param>
        /// <param name="reason">Why this shape.</param>
        /// <param name="backSetMetres">How far the building stands in from the BACK boundary. Defaults to
        /// its own footprint plus a metre and a half of walking room, so a yard cannot be authored with
        /// its house half outside it.</param>
        /// <param name="gates">Points the fence opens at; defaults to one gate facing
        /// <paramref name="frontToward"/>.</param>
        public static Yard Facing(string name, Vector2 owner, float ownerRadiusMetres, Vector2 sizeMetres,
                                  Vector2 frontToward, YardFence fence, string reason,
                                  float backSetMetres = -1f, Vector2[] gates = null)
        {
            Vector2 forward = frontToward - owner;
            forward = forward.sqrMagnitude < 1e-6f ? Vector2.up : forward.normalized;

            float backSet = backSetMetres >= 0f ? backSetMetres : ownerRadiusMetres + 1.5f;
            Vector2 centre = owner + forward * (sizeMetres.y * 0.5f - backSet);

            var polygon = GroundPolygon.Rectangle(centre, sizeMetres, centre + forward);
            var gateList = gates ?? new[] { centre + forward * (sizeMetres.y * 0.5f) };

            return new Yard(name, owner, ownerRadiusMetres, polygon, fence, gateList, reason);
        }

        public override string ToString() =>
            $"{Name} [{Fence}] {AreaMetresSquared:0} m2, {Gates.Length} gate(s)";
    }

    /// <summary>
    /// <b>What a region's yards DO, region-agnostically.</b> The table lives per region
    /// (<see cref="StPetersYards"/>, <see cref="NineMileCreekYards"/>); the questions asked of it live
    /// here, so both regions answer them the same way — the arrangement <see cref="WoodlandZones"/>
    /// already makes for woodland.
    /// </summary>
    public static class YardPlan
    {
        /// <summary>How wide the fence opens at a gate (m). A gate is walked through, so it has to clear
        /// the player and read as an opening at 32 px/m; 1.8 m is two pickets' worth of gap and the
        /// width the kit's own gate pillars are spaced for.</summary>
        public const float GateWidthMetres = 1.8f;

        /// <summary>How thick the blocking wall behind a fence is (m). Derived from the one system that
        /// owns walls, never restated — see <see cref="PropertyBoundary.DefaultThicknessMetres"/>.
        /// </summary>
        public const float FenceThicknessMetres = PropertyBoundary.DefaultThicknessMetres;

        /// <summary>
        /// Is <paramref name="p"/> inside ANY yard in the table?
        ///
        /// <para>⚠ Hot: the grass passes ask this tens of thousands of times per build. Each yard opens
        /// with its bounding box (<see cref="GroundPolygon.Contains"/>), so a point out on the meadow
        /// costs four compares per row.</para>
        /// </summary>
        public static bool IsInsideAny(IReadOnlyList<Yard> yards, Vector2 p)
        {
            if (yards == null) return false;
            for (int i = 0; i < yards.Count; i++)
                if (yards[i].Contains(p)) return true;
            return false;
        }

        /// <summary>The yard called <paramref name="name"/>, or a hard failure naming what IS in the
        /// table. Same contract as <see cref="WoodlandZones.Require"/>: a typo'd key is a build error,
        /// not a silent no-op.</summary>
        public static Yard Require(IReadOnlyList<Yard> yards, string name)
        {
            if (yards != null)
                for (int i = 0; i < yards.Count; i++)
                    if (yards[i].Name == name) return yards[i];

            var names = new List<string>();
            if (yards != null) for (int i = 0; i < yards.Count; i++) names.Add(yards[i].Name);
            throw new KeyNotFoundException(
                $"[YardPlan] no yard named '{name}'. The table holds: {string.Join(", ", names)}");
        }

        /// <summary>Does the table hold a yard for the building at <paramref name="site"/>? The question
        /// the grass gate asks to decide whether a site keeps its old clearance disc or has a polygon
        /// now.</summary>
        public static bool HasYardFor(IReadOnlyList<Yard> yards, Vector2 site, float tolerance = 0.01f)
        {
            if (yards == null) return false;
            for (int i = 0; i < yards.Count; i++)
                if (Vector2.SqrMagnitude(yards[i].Owner - site) <= tolerance * tolerance) return true;
            return false;
        }

        /// <summary>Total metres of fence the table asks for — the bill, and the number that says at a
        /// glance whether a pass built anything.</summary>
        public static float TotalFenceMetres(IReadOnlyList<Yard> yards)
        {
            if (yards == null) return 0f;
            float total = 0f;
            for (int i = 0; i < yards.Count; i++)
                foreach (var run in yards[i].FenceRuns())
                    for (int s = 0; s + 1 < run.Length; s++)
                        total += Vector2.Distance(run[s], run[s + 1]);
            return total;
        }

        /// <summary>Do two yards claim the same ground? Sampled on the polygons' own corners and edge
        /// midpoints — enough to catch the real mistake (two neighbours authored on top of each other)
        /// without pretending to be a general polygon intersection.</summary>
        public static bool Overlap(in Yard a, in Yard b)
        {
            if (!a.Polygon.Bounds.Overlaps(b.Polygon.Bounds)) return false;
            return AnyProbeInside(a.Polygon, b.Polygon) || AnyProbeInside(b.Polygon, a.Polygon);
        }

        static bool AnyProbeInside(in GroundPolygon probeSource, in GroundPolygon target)
        {
            for (int i = 0; i < probeSource.Count; i++)
            {
                probeSource.Edge(i, out Vector2 x, out Vector2 y);
                if (target.Contains(x)) return true;
                if (target.Contains((x + y) * 0.5f)) return true;
            }
            return target.Contains(probeSource.Centre);
        }
    }
}
#endif
