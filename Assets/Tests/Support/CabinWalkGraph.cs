using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using UnityEngine;

namespace HiddenHarbours.Tests.Support
{
    // =============================================================================================
    // ⭐ Phase B, 2026-09-19, C6 — THE WALK GRAPH the fleet's cabin guards stand on.
    //
    // The 2026-09-18 charter's guard, by name: "every hull with an Interior link has a reachable door
    // from its boarding stand (a guard never asks the code for its own bar); the sport fisher's
    // flybridge has a way down". This file is the ground those guards walk. It answers ONE question
    // — from where the game stands her, which of her doors can she walk to, and by what path — and it
    // answers it from the DEF and the SIDECAR, never from the code's radius:
    //   • a door's band is the def's measured clear width, halved (the ADR 0038 band), read HERE;
    //   • a deck step is a `type: step` obstruction the gameplay sidecar's `_notes` measure;
    //   • a stair or ladder is a placed BoatInteriorRoute the importer landed from the sidecar;
    //   • a level is walked on a 0.02 m raster of its sole, less every footprint that blocks.
    // What it DOES borrow from production is what a guard must share with the game to mean anything:
    // where the game stands her (DeckWalkController's seed and ChooseTheFloorNearest), which levels
    // a picture draws (ICabinFloors), and what a route end IS (ClassifyRouteEnd). Those are the
    // premises; the bars are the def's.
    //
    // Nodes (the Phase A prototype `scratchpad/c6_guard.py`, ported one rule at a time):
    //   D(area, loaded)  outside, on a walkable deck area; loaded = her cell rows have been read
    //   I(area)          inside a room with no way off it but its door: she walks the deck polygons
    //   L(level, comp)   inside, on a walkable level, in one standable component of its sole
    // Deck edges: plan contact within the def's floor tolerance AND the heights agree there within
    // it — OR the deck sidecar measures a step on that contact whose top matches the rise.
    // =============================================================================================

    /// <summary>
    /// ⭐ Phase B, 2026-09-19, C6 — which of her levels a picture draws, frozen at one moment: the
    /// walk graph's view of an <see cref="ICabinFloors"/>. The guard takes TWO snapshots of the live
    /// cabin — before and after <see cref="ICabinFloors.EnsureCells"/> — because the game asks
    /// "which floors are rooms?" both before her cell rows have loaded (at the helm, at the board
    /// spot) and after (once she has been through a door), and the answers may differ.
    /// Every mutator refuses: a snapshot is a premise, not a cabin.
    /// </summary>
    public sealed class FrozenFloors : ICabinFloors
    {
        private readonly bool[] _drawn;

        public FrozenFloors(BoatInteriorDef def, bool[] drawn)
        {
            Def = def;
            _drawn = drawn ?? Array.Empty<bool>();
        }

        /// <summary>What <paramref name="live"/> answers now, level by level.</summary>
        public static FrozenFloors Snapshot(ICabinFloors live)
        {
            bool alive = live is UnityEngine.Object o ? o != null : live != null;
            BoatInteriorDef def = alive ? live.Def : null;
            int count = def != null && def.Levels != null ? def.Levels.Length : 0;
            var drawn = new bool[count];
            for (int i = 0; i < count; i++) drawn[i] = live.IsDrawnLevel(i);
            return new FrozenFloors(def, drawn);
        }

        /// <summary>A cabin whose every usable level is a drawn room — the unit tests' stub.</summary>
        public static FrozenFloors EveryUsableLevelDrawn(BoatInteriorDef def)
        {
            int count = def != null && def.Levels != null ? def.Levels.Length : 0;
            var drawn = new bool[count];
            for (int i = 0; i < count; i++) drawn[i] = def.Levels[i] != null && def.Levels[i].IsUsable();
            return new FrozenFloors(def, drawn);
        }

        public BoatInteriorDef Def { get; }
        public bool IsInside => false;
        public int Level => 0;
        public bool IsDrawnLevel(int level) => level >= 0 && level < _drawn.Length && _drawn[level];
        public void EnsureCells() { }
        public bool TryEnter(int level) => false;
        public bool TryExit() => false;
        public bool TryGoToLevel(int level) => false;
    }

    public enum CabinNodeKind { Deck, Inside, Level }

    /// <summary>One place she can be, as the walk graph counts places (see the file header).</summary>
    public readonly struct CabinNode : IEquatable<CabinNode>
    {
        public readonly CabinNodeKind Kind;
        /// <summary>The deck area index (Deck, Inside) or the level index (Level); −1 is nowhere.</summary>
        public readonly int Index;
        /// <summary>The standable component of the level's sole (Level only; −1 otherwise).</summary>
        public readonly int Component;
        /// <summary>Her cell rows have been read (always true inside).</summary>
        public readonly bool Loaded;

        private CabinNode(CabinNodeKind kind, int index, int component, bool loaded)
        {
            Kind = kind;
            Index = index;
            Component = component;
            Loaded = loaded;
        }

        public static CabinNode Deck(int area, bool loaded) => new CabinNode(CabinNodeKind.Deck, area, -1, loaded);
        public static CabinNode Inside(int area) => new CabinNode(CabinNodeKind.Inside, area, -1, true);
        public static CabinNode OnLevel(int level, int component)
            => new CabinNode(CabinNodeKind.Level, level, component, true);
        public static CabinNode Nowhere => new CabinNode(CabinNodeKind.Deck, -1, -1, false);

        public bool IsSomewhere => Index >= 0 && (Kind != CabinNodeKind.Level || Component >= 0);

        /// <summary>The same place, whether or not her cells have loaded.</summary>
        public bool SamePlace(CabinNode other)
            => Kind == other.Kind && Index == other.Index && Component == other.Component;

        public bool Equals(CabinNode other) => SamePlace(other) && Loaded == other.Loaded;
        public override bool Equals(object obj) => obj is CabinNode other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)Kind;
                hash = hash * 397 ^ Index;
                hash = hash * 397 ^ Component;
                return hash * 2 + (Loaded ? 1 : 0);
            }
        }

        public static bool operator ==(CabinNode a, CabinNode b) => a.Equals(b);
        public static bool operator !=(CabinNode a, CabinNode b) => !a.Equals(b);
    }

    public enum CabinHopKind { Start, DeckEdge, DoorIn, DoorOut, DoorInAndOut, Route }

    /// <summary>One step of a walk: how she came to <see cref="To"/> from <see cref="From"/>.</summary>
    public sealed class CabinHop
    {
        public readonly CabinNode From;
        public readonly CabinNode To;
        public readonly CabinHopKind Kind;
        /// <summary>The link (DeckEdge), door (Door*) or route (Route) index; −1 for Start.</summary>
        public readonly int Index;
        /// <summary>Route only: 0 = she steps on at its FromPoint, 1 = at its ToPoint; −1 otherwise.</summary>
        public readonly int NearEnd;
        public readonly string Label;

        public CabinHop(CabinNode from, CabinNode to, CabinHopKind kind, int index, int nearEnd, string label)
        {
            From = from;
            To = to;
            Kind = kind;
            Index = index;
            NearEnd = nearEnd;
            Label = label ?? "";
        }
    }

    /// <summary>
    /// One of her doors, as the def measures it. <see cref="Index"/> 0 is the main door
    /// (<see cref="BoatInteriorDef.Door"/>); k ≥ 1 is <c>AdditionalDoors[k − 1]</c>.
    /// ⚠ The band is the def's clear width halved, computed HERE — never read back from
    /// <see cref="BoatCabinThreshold"/>, or the guard would be asking the code for its own bar.
    /// </summary>
    public sealed class CabinDoorSpec
    {
        public readonly int Index;
        public readonly string Name;
        public readonly BoatInteriorDoor Door;

        public CabinDoorSpec(int index, BoatInteriorDoor door)
        {
            Index = index;
            Door = door;
            string id = door != null ? door.Id : "";
            Name = (index == 0 ? "Door:" : "+Door:") + id;
        }

        public bool IsMain => Index == 0;
        public Vector3 Threshold => Door != null ? Door.ThresholdPoint : Vector3.zero;
        public float ClearWidth => Door != null ? Door.ClearWidthMeters : 0f;
        public bool IsMeasured => ClearWidth > 0f;
        public float BandRadius => ClearWidth * 0.5f;
    }

    /// <summary>
    /// Two walkable deck areas she can step between, and where: <see cref="OnA"/> on area
    /// <see cref="A"/>, <see cref="OnB"/> on area <see cref="B"/> (A &lt; B). <see cref="Why"/> is
    /// "level" for a flush contact or "step {id} {top}" for a step the sidecar measures.
    /// </summary>
    public sealed class CabinDeckLink
    {
        public readonly int A;
        public readonly int B;
        public Vector2 OnA { get; internal set; }
        public Vector2 OnB { get; internal set; }
        public string Why { get; internal set; }

        public CabinDeckLink(int a, int b, Vector2 onA, Vector2 onB, string why)
        {
            A = a;
            B = b;
            OnA = onA;
            OnB = onB;
            Why = why ?? "";
        }

        public bool IsStep => Why.StartsWith("step", StringComparison.Ordinal);
        public int Other(int area) => area == A ? B : A;
        public Vector2 On(int area) => area == A ? OnA : OnB;
        public Vector2 OnOther(int area) => area == A ? OnB : OnA;
    }

    /// <summary>
    /// A level's sole on a <see cref="Resolution"/> raster: a cell is standable when its centre is
    /// inside the level's outline and inside no footprint that blocks
    /// (<see cref="BoatCabinWalkMath.Blocks"/>), and the standable cells are labelled into
    /// 4-connected components. The fill is a scanline over the same even-odd crossing expression
    /// <see cref="DeckAreaMath.Contains(Vector2[],Vector2)"/> evaluates, so a cell agrees with the
    /// point test; the prototype's grid is reproduced cell for cell (origin, size, label order).
    /// </summary>
    public sealed class CabinLevelGrid
    {
        public const double Resolution = 0.02;

        public readonly BoatInteriorLevel Level;
        public readonly double X0;
        public readonly double Y0;
        public readonly int Nx;
        public readonly int Ny;

        private readonly int[] _labels;
        private readonly List<int> _sizes = new List<int>();

        public CabinLevelGrid(BoatInteriorLevel level)
        {
            Level = level;
            Vector2[] outline = level != null && level.IsUsable() ? level.Outline : null;
            if (outline == null)
            {
                _labels = Array.Empty<int>();
                return;
            }

            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
            foreach (Vector2 v in outline)
            {
                minX = Math.Min(minX, v.x);
                minY = Math.Min(minY, v.y);
                maxX = Math.Max(maxX, v.x);
                maxY = Math.Max(maxY, v.y);
            }
            X0 = minX - Resolution;
            Y0 = minY - Resolution;
            Nx = (int)((maxX - X0) / Resolution) + 3;
            Ny = (int)((maxY - Y0) / Resolution) + 3;

            var open = new bool[Nx * Ny];
            var crossings = new List<double>();
            Paint(open, outline, true, crossings);
            if (level.Obstructions != null)
                foreach (BoatInteriorObstruction o in level.Obstructions)
                    if (o != null && BoatCabinWalkMath.Blocks(o)) Paint(open, o.Footprint, false, crossings);

            _labels = new int[Nx * Ny];
            for (int i = 0; i < _labels.Length; i++) _labels[i] = -1;
            var queue = new int[Nx * Ny];
            for (int seed = 0; seed < open.Length; seed++)
            {
                if (!open[seed] || _labels[seed] >= 0) continue;
                int label = _sizes.Count;
                int head = 0, tail = 0;
                queue[tail++] = seed;
                _labels[seed] = label;
                while (head < tail)
                {
                    int at = queue[head++];
                    int row = at / Nx, col = at - row * Nx;
                    if (row + 1 < Ny) Visit(at + Nx);
                    if (row > 0) Visit(at - Nx);
                    if (col + 1 < Nx) Visit(at + 1);
                    if (col > 0) Visit(at - 1);
                }
                _sizes.Add(tail);

                void Visit(int next)
                {
                    if (!open[next] || _labels[next] >= 0) return;
                    _labels[next] = label;
                    queue[tail++] = next;
                }
            }
        }

        public int ComponentCount => _sizes.Count;
        public int SizeOf(int component) => component >= 0 && component < _sizes.Count ? _sizes[component] : 0;

        public int LabelAt(int ix, int iy)
            => ix >= 0 && ix < Nx && iy >= 0 && iy < Ny ? _labels[iy * Nx + ix] : -1;

        public Vector2 CentreOf(int ix, int iy)
            => new Vector2((float)(X0 + (ix + 0.5) * Resolution), (float)(Y0 + (iy + 0.5) * Resolution));

        /// <summary>The cell whose square holds <paramref name="p"/>, if it is on the raster.</summary>
        public bool TryCellOf(Vector2 p, out int ix, out int iy)
        {
            ix = (int)Math.Floor((p.x - X0) / Resolution);
            iy = (int)Math.Floor((p.y - Y0) / Resolution);
            return ix >= 0 && ix < Nx && iy >= 0 && iy < Ny;
        }

        /// <summary>The component of the labelled cell centre nearest <paramref name="p"/> within
        /// three cells, or −1 — the prototype's <c>comp_at</c>, truncation and tie order included.</summary>
        public int ComponentAt(Vector2 p)
        {
            int col = (int)((p.x - X0) / Resolution), row = (int)((p.y - Y0) / Resolution);
            int best = -1;
            double bestSqr = 1e9;
            for (int r = row - 3; r <= row + 3; r++)
            {
                for (int c = col - 3; c <= col + 3; c++)
                {
                    if (r < 0 || r >= Ny || c < 0 || c >= Nx) continue;
                    int label = _labels[r * Nx + c];
                    if (label < 0) continue;
                    double dx = X0 + (c + 0.5) * Resolution - p.x, dy = Y0 + (r + 0.5) * Resolution - p.y;
                    double sqr = dx * dx + dy * dy;
                    if (sqr < bestSqr)
                    {
                        best = label;
                        bestSqr = sqr;
                    }
                }
            }
            return best;
        }

        /// <summary>Every cell centre of <paramref name="component"/> within
        /// <paramref name="radius"/> of <paramref name="centre"/>, row by row.</summary>
        public List<Vector2> ComponentPoints(int component, Vector2 centre, float radius)
        {
            var points = new List<Vector2>();
            ForEachCellWithin(component, centre, radius, p => { points.Add(p); return false; });
            return points;
        }

        public bool AnyComponentPointWithin(int component, Vector2 centre, float radius)
            => ForEachCellWithin(component, centre, radius, p => true);

        private bool ForEachCellWithin(int component, Vector2 centre, float radius, Func<Vector2, bool> stop)
        {
            if (component < 0 || _labels.Length == 0) return false;
            double r = radius, rr = r * r;
            int colFrom = Math.Max(0, (int)Math.Floor((centre.x - r - X0) / Resolution) - 1);
            int colTo = Math.Min(Nx - 1, (int)Math.Ceiling((centre.x + r - X0) / Resolution) + 1);
            int rowFrom = Math.Max(0, (int)Math.Floor((centre.y - r - Y0) / Resolution) - 1);
            int rowTo = Math.Min(Ny - 1, (int)Math.Ceiling((centre.y + r - Y0) / Resolution) + 1);
            for (int iy = rowFrom; iy <= rowTo; iy++)
            {
                for (int ix = colFrom; ix <= colTo; ix++)
                {
                    if (_labels[iy * Nx + ix] != component) continue;
                    double x = X0 + (ix + 0.5) * Resolution, y = Y0 + (iy + 0.5) * Resolution;
                    double dx = x - centre.x, dy = y - centre.y;
                    if (dx * dx + dy * dy > rr) continue;
                    if (stop(new Vector2((float)x, (float)y))) return true;
                }
            }
            return false;
        }

        // The scanline form of the even-odd test: a cell centre is inside when an odd number of the
        // row's edge crossings lie strictly to its right — the same straddle test and the same
        // `xi + t·(xj − xi)` as DeckAreaMath.Contains, evaluated once per row instead of per cell.
        private void Paint(bool[] cells, Vector2[] polygon, bool value, List<double> crossings)
        {
            if (polygon == null || polygon.Length < 3) return;
            int n = polygon.Length;
            double minY = double.PositiveInfinity, maxY = double.NegativeInfinity;
            foreach (Vector2 v in polygon)
            {
                minY = Math.Min(minY, v.y);
                maxY = Math.Max(maxY, v.y);
            }
            int rowFrom = Math.Max(0, (int)Math.Floor((minY - Y0) / Resolution) - 1);
            int rowTo = Math.Min(Ny - 1, (int)Math.Ceiling((maxY - Y0) / Resolution) + 1);
            for (int iy = rowFrom; iy <= rowTo; iy++)
            {
                double py = Y0 + (iy + 0.5) * Resolution;
                crossings.Clear();
                for (int i = 0; i < n; i++)
                {
                    Vector2 here = polygon[i], prev = polygon[i == 0 ? n - 1 : i - 1];
                    double xi = here.x, yi = here.y, xj = prev.x, yj = prev.y;
                    if ((yi > py) == (yj > py)) continue;
                    double t = (py - yi) / (yj - yi);
                    crossings.Add(xi + t * (xj - xi));
                }
                if (crossings.Count == 0) continue;
                crossings.Sort();
                int colFrom = Math.Max(0, (int)Math.Floor((crossings[0] - X0) / Resolution) - 1);
                int colTo = Math.Min(Nx - 1,
                                     (int)Math.Ceiling((crossings[crossings.Count - 1] - X0) / Resolution) + 1);
                int atOrLeft = 0;
                while (atOrLeft < crossings.Count && crossings[atOrLeft] <= X0 + (colFrom + 0.5) * Resolution - 1.0)
                    atOrLeft++;
                for (int ix = colFrom; ix <= colTo; ix++)
                {
                    double px = X0 + (ix + 0.5) * Resolution;
                    while (atOrLeft < crossings.Count && crossings[atOrLeft] <= px) atOrLeft++;
                    if (((crossings.Count - atOrLeft) & 1) == 1) cells[iy * Nx + ix] = value;
                }
            }
        }
    }

    /// <summary>The result of one breadth-first walk: every node she reached, how, and which doors.</summary>
    public sealed class CabinWalk
    {
        public readonly CabinWalkGraph Graph;
        public readonly CabinNode Start;

        private readonly Dictionary<CabinNode, CabinHop> _cameBy = new Dictionary<CabinNode, CabinHop>();
        private readonly List<CabinNode> _nodes = new List<CabinNode>();
        private readonly Dictionary<int, CabinNode> _reached = new Dictionary<int, CabinNode>();

        internal CabinWalk(CabinWalkGraph graph, CabinNode start)
        {
            Graph = graph;
            Start = start;
            _cameBy[start] = new CabinHop(start, start, CabinHopKind.Start, -1, -1, "start");
            _nodes.Add(start);
        }

        public IReadOnlyList<CabinNode> Nodes => _nodes;

        internal bool TryAdd(CabinHop hop)
        {
            if (!hop.To.IsSomewhere || _cameBy.ContainsKey(hop.To)) return false;
            _cameBy[hop.To] = hop;
            _nodes.Add(hop.To);
            return true;
        }

        /// <summary>The FIRST node the walk stood on inside the door's band (setdefault).</summary>
        internal void MarkReached(int door, CabinNode by)
        {
            if (!_reached.ContainsKey(door)) _reached[door] = by;
        }

        public bool Reaches(int door) => _reached.ContainsKey(door);
        public bool TryGetReacher(int door, out CabinNode node) => _reached.TryGetValue(door, out node);

        public CabinHop HopInto(CabinNode node) => _cameBy.TryGetValue(node, out CabinHop hop) ? hop : null;

        /// <summary>The hops from <see cref="Start"/> to <paramref name="node"/>, Start excluded;
        /// null when the walk never reached it.</summary>
        public List<CabinHop> PathTo(CabinNode node)
        {
            if (!_cameBy.ContainsKey(node)) return null;
            var path = new List<CabinHop>();
            CabinNode at = node;
            for (int guard = 0; guard <= _nodes.Count; guard++)
            {
                CabinHop hop = _cameBy[at];
                if (hop.Kind == CabinHopKind.Start) break;
                path.Add(hop);
                at = hop.From;
            }
            path.Reverse();
            return path;
        }

        /// <summary>"route x->L:house_sole#0 | door out Door:main->D:cockpit*", or "(at the start)".</summary>
        public string Describe(CabinNode node)
        {
            List<CabinHop> path = PathTo(node);
            if (path == null) return "(never reached)";
            if (path.Count == 0) return "(at the start " + Graph.Name(Start) + ")";
            var parts = new List<string>();
            foreach (CabinHop hop in path) parts.Add(hop.Label + "->" + Graph.Name(hop.To));
            return string.Join(" | ", parts);
        }

        public string DescribeNodes()
        {
            var names = new List<string>();
            foreach (CabinNode n in _nodes) names.Add(Graph.Name(n));
            names.Sort(StringComparer.Ordinal);
            return string.Join(", ", names);
        }
    }

    /// <summary>
    /// ⭐ Phase B, 2026-09-19, C6 — one hull's walk graph over its Interior def and deck def (see the
    /// file header). Build it once per hull; <see cref="Walk"/> it from each stand.
    /// </summary>
    public sealed class CabinWalkGraph
    {
        /// <summary>The disk sampling step about a door or a route end (the prototype's RES).</summary>
        public const double SampleStep = 0.02;
        /// <summary>The outline sampling step (the prototype's outline_pts).</summary>
        public const double OutlineStep = 0.02;
        /// <summary>The area-interior sampling step the flush-contact search uses (grid_in).</summary>
        public const double ContactGridStep = 0.05;

        public readonly BoatInteriorDef Def;
        public readonly BoatDeckDef Deck;
        public readonly string DeckSidecarPath;
        public readonly IReadOnlyList<CabinDoorSpec> Doors;
        public readonly IReadOnlyList<CabinDeckLink> Links;
        /// <summary>What the graph could not read, for a failure message to name.</summary>
        public readonly IReadOnlyList<string> Notes;

        private readonly List<int>[] _linksOf;
        private readonly CabinLevelGrid[] _grids;

        public CabinWalkGraph(BoatInteriorDef def, BoatDeckDef deck, string projectRoot)
        {
            Def = def ?? throw new ArgumentNullException(nameof(def));
            Deck = deck;
            var notes = new List<string>();

            var doors = new List<CabinDoorSpec> { new CabinDoorSpec(0, def.Door) };
            if (def.AdditionalDoors != null)
                for (int k = 0; k < def.AdditionalDoors.Length; k++)
                    doors.Add(new CabinDoorSpec(k + 1, def.AdditionalDoors[k]));
            Doors = doors;

            _grids = new CabinLevelGrid[def.Levels != null ? def.Levels.Length : 0];
            _linksOf = new List<int>[AreaCount];
            for (int i = 0; i < _linksOf.Length; i++) _linksOf[i] = new List<int>();

            DeckSidecarPath = deck != null && !string.IsNullOrEmpty(deck.SourceSidecar) && projectRoot != null
                ? Path.Combine(projectRoot, deck.SourceSidecar).Replace('\\', '/')
                : "";
            if (deck == null) notes.Add("the hull has no deck def");
            else if (DeckSidecarPath.Length == 0) notes.Add("the deck def names no sidecar, so no step is read");
            else if (!File.Exists(DeckSidecarPath)) notes.Add("the deck sidecar is missing: " + DeckSidecarPath);

            Links = LinkTheDecks(notes);
            for (int l = 0; l < Links.Count; l++)
            {
                _linksOf[Links[l].A].Add(l);
                _linksOf[Links[l].B].Add(l);
            }
            Notes = notes;
        }

        public float Tolerance => Def.FloorTolerance;
        public float Reach => Def.RouteEndReach;
        public int AreaCount => Deck != null && Deck.Areas != null ? Deck.Areas.Length : 0;
        public DeckArea AreaAt(int i) => i >= 0 && i < AreaCount ? Deck.Areas[i] : null;
        public string AreaId(int i) => AreaAt(i) != null ? AreaAt(i).Id : "?";
        public IReadOnlyList<int> LinksOf(int area)
            => area >= 0 && area < _linksOf.Length ? (IReadOnlyList<int>)_linksOf[area] : Array.Empty<int>();

        /// <summary>A deck area she can walk: usable, and a DECK (a washboard is not a floor).</summary>
        public static bool IsWalkable(DeckArea a) => a != null && a.IsUsable() && a.Kind == DeckAreaKind.Deck;

        public BoatInteriorLevel LevelAt(int i)
            => Def.Levels != null && i >= 0 && i < Def.Levels.Length ? Def.Levels[i] : null;

        /// <summary>The level whose sole is nearest <paramref name="z"/> among the usable levels a
        /// picture draws — BoatInterior.LevelIndexAtHeight's rule, restated (strict &lt; on ties).</summary>
        public int LevelAtHeight(float z, ICabinFloors floors)
        {
            int best = -1;
            float bestGap = 0f;
            for (int i = 0; i < (Def.Levels != null ? Def.Levels.Length : 0); i++)
            {
                BoatInteriorLevel level = Def.Levels[i];
                if (level == null || !level.IsUsable() || floors == null || !floors.IsDrawnLevel(i)) continue;
                float gap = Mathf.Abs(level.SoleZMeters - z);
                if (best < 0 || gap < bestGap)
                {
                    best = i;
                    bestGap = gap;
                }
            }
            return best;
        }

        /// <summary>A level she walks as a floor: usable, with a takeable route (DeckWalkController's
        /// WalkableLevel premise). A drawn level without one is a room she stands in on the deck.</summary>
        public bool Walkable(int level, ICabinFloors floors)
        {
            BoatInteriorLevel l = LevelAt(level);
            return l != null && l.IsUsable() && DeckWalkController.CarriesATakeableRoute(floors, Deck, l.Id);
        }

        public CabinLevelGrid Grid(int level)
        {
            if (level < 0 || level >= _grids.Length) return null;
            return _grids[level] ??= new CabinLevelGrid(Def.Levels[level]);
        }

        /// <summary>The walkable deck area <see cref="BoatDeckDef.SeatNearest"/> seats
        /// <paramref name="hullLocal"/> on (height included), or −1.</summary>
        public int SeatNearestArea(Vector3 hullLocal)
        {
            if (Deck == null) return -1;
            int hint = -1;
            Deck.SeatNearest(hullLocal, ref hint, out _);
            return hint;
        }

        /// <summary>The nearest point of the area to <paramref name="p"/>: p itself inside it.</summary>
        public static Vector2 NearestOn(DeckArea a, Vector2 p, out float sqr)
        {
            if (DeckAreaMath.Contains(a.Outline, p))
            {
                sqr = 0f;
                return p;
            }
            return DeckAreaMath.ClosestPointOnOutline(a.Outline, p, out sqr);
        }

        public static float HeightOn(DeckArea a, Vector2 p) => DeckAreaMath.HeightAt(a.HeightPlane, p);

        /// <summary>
        /// The points of walkable area <paramref name="area"/> within <paramref name="radius"/> of
        /// <paramref name="centre"/> whose deck height is within the tolerance of
        /// <paramref name="height"/> — a disk raster, the area's nearest point, and its outline
        /// sampled every 2 cm (the prototype's disk_samples, then its height filter).
        /// </summary>
        public List<Vector2> DeckPointsNear(int area, Vector2 centre, float radius, float height)
        {
            var found = new List<Vector2>();
            DeckArea a = AreaAt(area);
            if (!IsWalkable(a) || radius <= 0f) return found;
            double r = radius, rr = r * r, cx = centre.x, cy = centre.y;
            float tol = Tolerance;

            void Keep(Vector2 p)
            {
                if (Mathf.Abs(HeightOn(a, p) - height) <= tol) found.Add(p);
            }

            int count = (int)Math.Ceiling((2.0 * r + SampleStep) / SampleStep);
            for (int iy = 0; iy < count; iy++)
            {
                double y = cy - r + iy * SampleStep;
                for (int ix = 0; ix < count; ix++)
                {
                    double x = cx - r + ix * SampleStep;
                    double dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy > rr) continue;
                    var p = new Vector2((float)x, (float)y);
                    if (DeckAreaMath.Contains(a.Outline, p)) Keep(p);
                }
            }

            Vector2 q = NearestOn(a, centre, out _);
            if (SqrDistance(q, cx, cy) <= rr + 1e-9) Keep(q);

            Vector2[] poly = a.Outline;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 from = poly[i == 0 ? poly.Length - 1 : i - 1], to = poly[i];
                Vector2 nearest = DeckAreaMath.ClosestPointOnSegment(from, to, centre);
                if (SqrDistance(nearest, cx, cy) > rr + 1e-6) continue;
                foreach (Vector2 p in OutlineSamples(from, to))
                    if (SqrDistance(p, cx, cy) <= rr) Keep(p);
            }
            return found;
        }

        /// <summary>
        /// ⭐ The walk, breadth first from <paramref name="start"/>. <paramref name="pre"/> answers
        /// "which levels are drawn" for a node on the deck before her cells have loaded;
        /// <paramref name="post"/> answers it everywhere else (see <see cref="FrozenFloors"/>).
        /// </summary>
        public CabinWalk Walk(CabinNode start, ICabinFloors pre, ICabinFloors post)
        {
            var walk = new CabinWalk(this, start);
            if (!start.IsSomewhere) return walk;
            var queue = new Queue<CabinNode>();
            queue.Enqueue(start);

            void Go(CabinNode from, CabinNode to, CabinHopKind kind, int index, int nearEnd, string label)
            {
                if (walk.TryAdd(new CabinHop(from, to, kind, index, nearEnd, label))) queue.Enqueue(to);
            }

            while (queue.Count > 0)
            {
                CabinNode n = queue.Dequeue();
                if (n.Kind == CabinNodeKind.Level) WalkFromLevel(walk, n, post, Go);
                else WalkFromDeck(walk, n, n.Kind == CabinNodeKind.Deck && !n.Loaded ? pre : post, post, Go);
            }
            return walk;
        }

        private delegate void GoTo(CabinNode from, CabinNode to, CabinHopKind kind, int index, int nearEnd,
                                   string label);

        private void WalkFromDeck(CabinWalk walk, CabinNode n, ICabinFloors floors, ICabinFloors post, GoTo go)
        {
            int i = n.Index;
            bool inside = n.Kind == CabinNodeKind.Inside;

            // The deck she can step to from here, flush or up a measured step.
            foreach (int l in LinksOf(i))
            {
                CabinDeckLink link = Links[l];
                int k = link.Other(i);
                go(n, inside ? CabinNode.Inside(k) : CabinNode.Deck(k, n.Loaded), CabinHopKind.DeckEdge, l, -1,
                   link.IsStep ? link.Why : "deck");
            }

            // Every measured door whose band she can stand in from this area, at its sill.
            foreach (CabinDoorSpec door in Doors)
            {
                if (!door.IsMeasured) continue;
                Vector3 t = door.Threshold;
                List<Vector2> band = DeckPointsNear(i, new Vector2(t.x, t.y), door.BandRadius, t.z);
                if (band.Count == 0) continue;
                walk.MarkReached(door.Index, n);
                if (inside)
                {
                    go(n, CabinNode.Deck(i, true), CabinHopKind.DoorOut, door.Index, -1, "door out " + door.Name);
                    continue;
                }
                int lvl = Math.Max(0, LevelAtHeight(t.z, post));
                BoatInteriorLevel level = LevelAt(lvl);
                if (level == null || !level.IsUsable()) continue;
                if (Walkable(lvl, post))
                {
                    CabinLevelGrid grid = Grid(lvl);
                    var components = new SortedSet<int>();
                    foreach (Vector2 p in band) components.Add(grid.ComponentAt(DeckWalkController.SeatOnLevel(level, p)));
                    foreach (int c in components)
                        if (c >= 0)
                            go(n, CabinNode.OnLevel(lvl, c), CabinHopKind.DoorIn, door.Index, -1, "door in " + door.Name);
                }
                else
                {
                    go(n, CabinNode.Deck(i, true), CabinHopKind.DoorInAndOut, door.Index, -1,
                       "door in+out " + door.Name);
                }
            }
            if (inside) return;   // a room with no way off it takes no stairs

            // Every placed route whose near end is a place on this deck she can stand in reach of.
            BoatInteriorRoute[] routes = Def.Routes ?? Array.Empty<BoatInteriorRoute>();
            for (int ri = 0; ri < routes.Length; ri++)
            {
                BoatInteriorRoute r = routes[ri];
                if (r == null || !r.Placed) continue;
                for (int end = 0; end < 2; end++)
                {
                    string nearLevel = end == 0 ? r.FromLevel : r.ToLevel, farLevel = end == 0 ? r.ToLevel : r.FromLevel;
                    Vector3 np = end == 0 ? r.FromPoint : r.ToPoint, fp = end == 0 ? r.ToPoint : r.FromPoint;
                    if (DeckWalkController.ClassifyRouteEnd(floors, Deck, nearLevel, np, out _)
                        != DeckWalkController.RouteEnd.Deck) continue;
                    if (DeckPointsNear(i, new Vector2(np.x, np.y), Reach, np.z).Count == 0) continue;
                    CabinNode to = FarEndOf(farLevel, fp, floors, post, -1, n.Loaded);
                    if (to.IsSomewhere)
                        go(n, to, CabinHopKind.Route, ri, end, "route " + r.Id);
                }
            }
        }

        private void WalkFromLevel(CabinWalk walk, CabinNode n, ICabinFloors post, GoTo go)
        {
            int j = n.Index, c = n.Component;
            BoatInteriorLevel level = LevelAt(j);
            CabinLevelGrid grid = Grid(j);
            if (level == null || grid == null) return;

            foreach (CabinDoorSpec door in Doors)
            {
                if (!door.IsMeasured) continue;
                Vector3 t = door.Threshold;
                if (Mathf.Abs(level.SoleZMeters - t.z) > Tolerance) continue;
                List<Vector2> band = grid.ComponentPoints(c, new Vector2(t.x, t.y), door.BandRadius);
                if (band.Count == 0) continue;
                walk.MarkReached(door.Index, n);
                var areas = new SortedSet<int>();
                foreach (Vector2 p in band) areas.Add(SeatNearestArea(new Vector3(p.x, p.y, level.SoleZMeters)));
                foreach (int area in areas)
                    if (area >= 0)
                        go(n, CabinNode.Deck(area, true), CabinHopKind.DoorOut, door.Index, -1, "door out " + door.Name);
            }

            BoatInteriorRoute[] routes = Def.Routes ?? Array.Empty<BoatInteriorRoute>();
            for (int ri = 0; ri < routes.Length; ri++)
            {
                BoatInteriorRoute r = routes[ri];
                if (r == null || !r.Placed) continue;
                for (int end = 0; end < 2; end++)
                {
                    string nearLevel = end == 0 ? r.FromLevel : r.ToLevel, farLevel = end == 0 ? r.ToLevel : r.FromLevel;
                    Vector3 np = end == 0 ? r.FromPoint : r.ToPoint, fp = end == 0 ? r.ToPoint : r.FromPoint;
                    if (DeckWalkController.ClassifyRouteEnd(post, Deck, nearLevel, np, out int ni)
                        != DeckWalkController.RouteEnd.Room || ni != j) continue;
                    if (Mathf.Abs(level.SoleZMeters - np.z) > Tolerance
                        || !grid.AnyComponentPointWithin(c, new Vector2(np.x, np.y), Reach)) continue;
                    CabinNode to = FarEndOf(farLevel, fp, post, post, j, true);
                    if (to.IsSomewhere)
                        go(n, to, CabinHopKind.Route, ri, end, "route " + r.Id);
                }
            }
        }

        // Where a route's far end puts her: on a walkable level's component, inside a room she walks
        // on the deck, or on the deck — or nowhere (the end is not a place; the same level again).
        private CabinNode FarEndOf(string farLevel, Vector3 fp, ICabinFloors floors, ICabinFloors post,
                                   int fromLevel, bool loaded)
        {
            DeckWalkController.RouteEnd kind = DeckWalkController.ClassifyRouteEnd(floors, Deck, farLevel, fp, out int fi);
            if (kind == DeckWalkController.RouteEnd.None) return CabinNode.Nowhere;
            if (kind == DeckWalkController.RouteEnd.Room)
            {
                BoatInteriorLevel level = LevelAt(fi);
                if (fi == fromLevel || level == null || !level.IsUsable()) return CabinNode.Nowhere;
                Vector2 seat = DeckWalkController.SeatOnLevel(level, new Vector2(fp.x, fp.y));
                return Walkable(fi, post)
                    ? CabinNode.OnLevel(fi, Grid(fi).ComponentAt(seat))
                    : CabinNode.Inside(SeatNearestArea(new Vector3(seat.x, seat.y, level.SoleZMeters)));
            }
            return CabinNode.Deck(SeatNearestArea(fp), loaded);
        }

        // ---- the stands: where the game puts her ---------------------------------------------------

        /// <summary>
        /// Where boarding lands her at drawn heading <paramref name="heading"/>: the switcher's board
        /// spot, named at <paramref name="boardHeight"/>, seeded onto the floor at that height as
        /// ControlSwitcher.Board → DeckWalkController.SnapToFloorAtHeight does.
        /// </summary>
        public CabinNode BoardStand(float heading, Vector2 boardLocal, float boardHeight, float elevation)
        {
            if (Deck == null || !Deck.HasWalkableDeck()) return CabinNode.Nowhere;
            Vector2 named = DeckAreaMath.WorldToDeck(boardLocal, boardHeight, 0f, elevation);
            Vector2 relative = DeckAreaMath.DeckToWorld(named, boardHeight, heading, elevation);
            int hint = -1;
            DeckWalkController.SeedDeckLocalOnFloorPure(relative, heading, elevation, Deck, boardHeight,
                                                        Def.FloorTolerance, ref hint, out _);
            return hint >= 0 ? CabinNode.Deck(hint, false) : CabinNode.Nowhere;
        }

        /// <summary>
        /// Where leaving the helm stands her. A measured station: the floor nearest it, room or deck,
        /// by <see cref="DeckWalkController.ChooseTheFloorNearest"/> before her cells load. No station:
        /// the switcher's helm offset seeded onto the deck, twice, the second from the first's area.
        /// </summary>
        public CabinNode HelmStand(float heading, Vector2 helmLocal, float elevation, ICabinFloors pre,
                                   ICabinFloors post)
        {
            if (Deck == null) return CabinNode.Nowhere;
            if (Deck.HasHelmStation)
            {
                int room = DeckWalkController.ChooseTheFloorNearest(Deck, pre, Deck.HelmStationLocalMeters, false,
                                                                    out Vector2 roomSeat, out bool measured,
                                                                    out _, out int deckArea, out _);
                if (room >= 0)
                {
                    BoatInteriorLevel level = LevelAt(room);
                    return Walkable(room, post)
                        ? CabinNode.OnLevel(room, Grid(room).ComponentAt(roomSeat))
                        : CabinNode.Inside(SeatNearestArea(new Vector3(roomSeat.x, roomSeat.y, level.SoleZMeters)));
                }
                return measured && deckArea >= 0 ? CabinNode.Deck(deckArea, false) : CabinNode.Nowhere;
            }
            if (!Deck.HasWalkableDeck()) return CabinNode.Nowhere;
            Vector2 named = DeckAreaMath.WorldToDeck(helmLocal, 0f, 0f, elevation);
            Vector2 relative = DeckAreaMath.DeckToWorld(named, 0f, heading, elevation);
            int first = -1;
            DeckWalkController.SeedDeckLocalPure(relative, heading, elevation, Deck, false, ref first, out _);
            int second = first;
            DeckWalkController.SeedDeckLocalPure(relative, heading, elevation, Deck, false, ref second, out _);
            return second >= 0 ? CabinNode.Deck(second, false) : CabinNode.Nowhere;
        }

        public string Name(CabinNode n)
        {
            if (!n.IsSomewhere) return "nowhere";
            switch (n.Kind)
            {
                case CabinNodeKind.Level:
                    BoatInteriorLevel level = LevelAt(n.Index);
                    return "L:" + (level != null ? level.Id : "?") + "#" + n.Component.ToString(CultureInfo.InvariantCulture);
                case CabinNodeKind.Inside:
                    return "I:" + AreaId(n.Index);
                default:
                    return "D:" + AreaId(n.Index) + (n.Loaded ? "*" : "");
            }
        }

        /// <summary>The graph in a few lines, for a failure message: its links and its doors.</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            sb.Append("links: ");
            if (Links.Count == 0) sb.Append("none");
            for (int l = 0; l < Links.Count; l++)
            {
                if (l > 0) sb.Append(", ");
                sb.Append(AreaId(Links[l].A)).Append('~').Append(AreaId(Links[l].B))
                  .Append(" (").Append(Links[l].Why).Append(')');
            }
            sb.Append("; doors: ");
            for (int d = 0; d < Doors.Count; d++)
            {
                if (d > 0) sb.Append(", ");
                CabinDoorSpec door = Doors[d];
                sb.Append(door.Name).Append(string.Format(CultureInfo.InvariantCulture, " T=({0:0.00}, {1:0.00}, {2:0.00}) CW={3:0.00}",
                                                          door.Threshold.x, door.Threshold.y, door.Threshold.z, door.ClearWidth));
            }
            foreach (string note in Notes) sb.Append("; ").Append(note);
            return sb.ToString();
        }

        // ---- the deck's links ----------------------------------------------------------------------

        private List<CabinDeckLink> LinkTheDecks(List<string> notes)
        {
            var walkable = new List<int>();
            for (int i = 0; i < AreaCount; i++)
                if (IsWalkable(Deck.Areas[i])) walkable.Add(i);

            var byPair = new SortedDictionary<long, CabinDeckLink>();
            for (int x = 0; x < walkable.Count; x++)
            {
                for (int y = x + 1; y < walkable.Count; y++)
                {
                    int i = walkable[x], k = walkable[y];
                    // The prototype's order: from i's samples onto k first, then from k's onto i.
                    if (TryFlushContact(i, k, out Vector2 onI, out Vector2 onK)
                        || TryFlushContact(k, i, out onK, out onI))
                        byPair[PairKey(i, k)] = new CabinDeckLink(i, k, onI, onK, "level");
                }
            }

            float tol = Tolerance, tolSqr = tol * tol;
            foreach (DeckStep step in ReadSteps(notes))
            {
                foreach (int i in walkable)
                {
                    if (!string.Equals(AreaId(i), step.DeckId, StringComparison.Ordinal)) continue;
                    Vector2 qi = NearestOn(Deck.Areas[i], step.Centre, out float di);
                    if (di > tolSqr) continue;
                    foreach (int k in walkable)
                    {
                        if (k == i || string.Equals(AreaId(k), step.DeckId, StringComparison.Ordinal)) continue;
                        Vector2 qk = NearestOn(Deck.Areas[k], step.Centre, out float dk);
                        if (dk > tolSqr) continue;
                        float rise = HeightOn(Deck.Areas[k], qk) - HeightOn(Deck.Areas[i], qi);
                        if (Mathf.Abs(rise - step.Top) > tol) continue;
                        int a = Math.Min(i, k), b = Math.Max(i, k);
                        string why = string.Format(CultureInfo.InvariantCulture, "step {0} {1:0.00}", step.StepId, step.Top);
                        Vector2 onA = a == i ? qi : qk, onB = a == i ? qk : qi;
                        if (byPair.TryGetValue(PairKey(a, b), out CabinDeckLink existing))
                        {
                            existing.Why = why;
                            existing.OnA = onA;
                            existing.OnB = onB;
                        }
                        else
                        {
                            byPair[PairKey(a, b)] = new CabinDeckLink(a, b, onA, onB, why);
                        }
                    }
                }
            }
            return new List<CabinDeckLink>(byPair.Values);
        }

        private static long PairKey(int a, int b) => (long)a * 65536L + b;

        // From <from>'s samples (its outline every 2 cm and its interior every 5 cm, inside <to>'s box
        // grown by the tolerance) onto <to>: a sample within the tolerance of <to> in plan whose
        // heights agree there. The contact is the hit nearest the hits' mean.
        private bool TryFlushContact(int from, int to, out Vector2 onFrom, out Vector2 onTo)
        {
            onFrom = onTo = default;
            DeckArea a = Deck.Areas[from], b = Deck.Areas[to];
            float tol = Tolerance, tolSqr = tol * tol;
            Vector4 box = DeckAreaMath.BoundsOf(b.Outline);
            box = new Vector4(box.x - tol, box.y - tol, box.z + tol, box.w + tol);

            var hitsFrom = new List<Vector2>();
            var hitsTo = new List<Vector2>();
            foreach (Vector2 p in ContactSamples(a, box))
            {
                Vector2 q = NearestOn(b, p, out float sqr);
                if (sqr > tolSqr) continue;
                if (Mathf.Abs(HeightOn(a, p) - HeightOn(b, q)) > tol) continue;
                hitsFrom.Add(p);
                hitsTo.Add(q);
            }
            if (hitsFrom.Count == 0) return false;

            double mx = 0, my = 0;
            foreach (Vector2 p in hitsFrom)
            {
                mx += p.x;
                my += p.y;
            }
            mx /= hitsFrom.Count;
            my /= hitsFrom.Count;
            int best = 0;
            double bestSqr = double.PositiveInfinity;
            for (int h = 0; h < hitsFrom.Count; h++)
            {
                double sqr = SqrDistance(hitsFrom[h], mx, my);
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = h;
                }
            }
            onFrom = hitsFrom[best];
            onTo = hitsTo[best];
            return true;
        }

        private static IEnumerable<Vector2> ContactSamples(DeckArea a, Vector4 box)
        {
            Vector2[] poly = a.Outline;
            for (int i = 0; i < poly.Length; i++)
                foreach (Vector2 p in OutlineSamples(poly[i == 0 ? poly.Length - 1 : i - 1], poly[i]))
                    if (InBox(p, box)) yield return p;

            // grid_in: arange(min + step/2, max, step) on each axis, kept inside the polygon.
            Vector4 own = DeckAreaMath.BoundsOf(poly);
            double sx = own.x + ContactGridStep / 2.0, sy = own.y + ContactGridStep / 2.0;
            int nx = Math.Max(0, (int)Math.Ceiling((own.z - sx) / ContactGridStep));
            int ny = Math.Max(0, (int)Math.Ceiling((own.w - sy) / ContactGridStep));
            int colFrom = Math.Max(0, (int)Math.Floor((box.x - sx) / ContactGridStep) - 1);
            int colTo = Math.Min(nx - 1, (int)Math.Ceiling((box.z - sx) / ContactGridStep) + 1);
            int rowFrom = Math.Max(0, (int)Math.Floor((box.y - sy) / ContactGridStep) - 1);
            int rowTo = Math.Min(ny - 1, (int)Math.Ceiling((box.w - sy) / ContactGridStep) + 1);
            for (int iy = rowFrom; iy <= rowTo; iy++)
            {
                for (int ix = colFrom; ix <= colTo; ix++)
                {
                    var p = new Vector2((float)(sx + ix * ContactGridStep), (float)(sy + iy * ContactGridStep));
                    if (InBox(p, box) && DeckAreaMath.Contains(poly, p)) yield return p;
                }
            }
        }

        // outline_pts: the edge from → to at max(2, ⌊len/step⌋ + 1) evenly spaced points, ends included.
        private static IEnumerable<Vector2> OutlineSamples(Vector2 from, Vector2 to)
        {
            double fx = from.x, fy = from.y, dx = (double)to.x - fx, dy = (double)to.y - fy;
            int n = Math.Max(2, (int)(Math.Sqrt(dx * dx + dy * dy) / OutlineStep) + 1);
            double dt = 1.0 / (n - 1);
            for (int k = 0; k < n; k++)
            {
                double t = k == n - 1 ? 1.0 : k * dt;
                yield return new Vector2((float)(fx + dx * t), (float)(fy + dy * t));
            }
        }

        private static bool InBox(Vector2 p, Vector4 box) => p.x >= box.x && p.x <= box.z && p.y >= box.y && p.y <= box.w;

        private static double SqrDistance(Vector2 p, double x, double y)
        {
            double dx = p.x - x, dy = p.y - y;
            return dx * dx + dy * dy;
        }

        // ---- the sidecar's measured steps ----------------------------------------------------------

        private readonly struct DeckStep
        {
            public readonly string DeckId;
            public readonly string StepId;
            public readonly Vector2 Centre;
            public readonly float Top;

            public DeckStep(string deckId, string stepId, Vector2 centre, float top)
            {
                DeckId = deckId;
                StepId = stepId;
                Centre = centre;
                Top = top;
            }
        }

        // The deck sidecar's `DECK[]._notes[]` entries of kind "obstruction" and type "step": a
        // two-number footprint box and a numeric `height_above_floor_m.top`. The deck the note sits
        // under is the LOW side; the step's top is the rise to the area across it.
        private List<DeckStep> ReadSteps(List<string> notes)
        {
            var steps = new List<DeckStep>();
            if (DeckSidecarPath.Length == 0 || !File.Exists(DeckSidecarPath)) return steps;
            object root;
            try
            {
                root = MiniJson.Parse(File.ReadAllText(DeckSidecarPath));
            }
            catch (Exception e)
            {
                notes.Add("the deck sidecar did not parse (" + e.Message + ")");
                return steps;
            }
            List<object> decks = MiniJson.List(root, "DECK");
            if (decks == null) return steps;
            foreach (object deck in decks)
            {
                List<object> deckNotes = MiniJson.List(deck, "_notes");
                if (deckNotes == null) continue;
                string low = MiniJson.String(deck, "id", "");
                foreach (object entry in deckNotes)
                {
                    if (!(entry is Dictionary<string, object> note)) continue;
                    if (MiniJson.String(note, "kind") != "obstruction" || MiniJson.String(note, "type") != "step") continue;
                    Dictionary<string, object> footprint = MiniJson.Dict(note, "footprint");
                    if (!TwoNumbers(MiniJson.List(footprint, "x"), out double x0, out double x1)
                        || !TwoNumbers(MiniJson.List(footprint, "y"), out double y0, out double y1)
                        || !TryNumber(MiniJson.Dict(note, "height_above_floor_m"), "top", out double top)) continue;
                    steps.Add(new DeckStep(low, MiniJson.String(note, "id", ""),
                                           new Vector2((float)((x0 + x1) / 2.0), (float)((y0 + y1) / 2.0)), (float)top));
                }
            }
            return steps;
        }

        private static bool TwoNumbers(List<object> values, out double first, out double second)
        {
            first = second = 0;
            if (values == null || values.Count != 2 || !(values[0] is double a) || !(values[1] is double b)) return false;
            first = a;
            second = b;
            return true;
        }

        private static bool TryNumber(Dictionary<string, object> node, string key, out double value)
        {
            value = 0;
            if (node == null || !node.TryGetValue(key, out object v) || !(v is double d)) return false;
            value = d;
            return true;
        }
    }
}
