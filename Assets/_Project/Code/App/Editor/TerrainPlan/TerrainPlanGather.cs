using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>A point the frozen sources carry: typed from the game's own members, or read off the scene by name.</summary>
    public readonly struct TerrainPlanPlace
    {
        /// <summary>The building's id (b_school …), or "" for a point.</summary>
        public readonly string Id;

        /// <summary>The point, when the game's members give it.</summary>
        public readonly PlanPoint At;

        /// <summary>The scene item it is read from: the one item named <see cref="Name"/> under the root <see cref="Root"/>.</summary>
        public readonly string Root, Name;

        TerrainPlanPlace(string id, PlanPoint at, string root, string name)
        {
            Id = id ?? ""; At = at; Root = root; Name = name;
        }

        /// <summary>A point the game's members give (a builder constant, a nav route's waypoint).</summary>
        public static TerrainPlanPlace Typed(string id, PlanPoint at) => new TerrainPlanPlace(id, at, null, null);

        /// <summary>The scene item named <paramref name="name"/> under <paramref name="root"/> (the root itself when
        /// <paramref name="name"/> is null).</summary>
        public static TerrainPlanPlace InScene(string id, string root, string name = null) =>
            new TerrainPlanPlace(id, default, root ?? throw new ArgumentNullException(nameof(root)), name ?? root);

        public bool FromScene => Root != null;

        public override string ToString() =>
            FromScene ? "scene " + Root + "/" + Name : "(" + TerrainPlanSourcesJson.Num(At.X) + ", " + TerrainPlanSourcesJson.Num(At.Y) + ")";
    }

    /// <summary>
    /// St Peters' places, in the order the frozen sources carry them (terrain pass 9's feature table): each read off the
    /// scene by root and name, or typed from the builder member it names. One table for the editor and the headless
    /// check, so the check runs the editor's own lookups.
    /// </summary>
    public static class StPetersPlaces
    {
        /// <summary>One place: its id, and either the member it is typed from or the scene item it is read from.</summary>
        public readonly struct Spec
        {
            public readonly string Id, Member, Root, Name;

            public Spec(string id, string member, string root = null, string name = null)
            {
                Id = id; Member = member ?? ""; Root = root; Name = name ?? root;
            }

            public bool FromScene => Root != null;
        }

        /// <summary>The root whose parts' extent is the wharf's footprint.</summary>
        public const string WharfRoot = "StPetersWharf";

        public static readonly Spec[] Buildings =
        {
            new Spec("b_school", "StPetersBuilder.SchoolPos"),
            new Spec("b_general", "StPetersBuilder.GeneralStorePos"),
            new Spec("b_white", "StPetersBuilder.WhiteFarmhousePos"),
            new Spec("b_red", "StPetersBuilder.RedSaltboxPos"),
            new Spec("b_sage", "StPetersBuilder.SageCottagePos"),
            new Spec("b_post", null, "IslandShops", "postOffice"),
            new Spec("b_cannery", null, "StPetersCannery"),
            new Spec("b_ginnys", null, "GinnyPlot", "CamperLot"),
            new Spec("b_ginnys", null, "GinnyPlot", "GinnyFreezer"),
            new Spec("b_village", "StPetersBuilder.VillageHearthPos"),
            new Spec("b_start", "StPetersBuilder.StartSpawnPos"),
            new Spec("b_wet", "StPetersBuilder.WetBucketPos"),
        };

        public static readonly Spec[] DockPoints =
        {
            new Spec("pt_dock", "StPetersBuilder.DockZonePos"),
            new Spec("pt_disembark", "StPetersBuilder.DisembarkPos"),
            new Spec("pt_arrival", "StPetersBuilder.ArrivalPos"),
            new Spec("pt_dory", "StPetersBuilder.DoryMooredPos"),
        };

        public static readonly Spec[] Passages =
        {
            new Spec("pass_creek", "StPetersBuilder.ToNineMileCreekPassagePos"),
            new Spec("pass_water", "StPetersBuilder.ToWestWaterPassagePos"),
            new Spec("pass_water", "StPetersBuilder.ToEastWaterPassagePos"),
            new Spec("pass_arrival", null, "StPetersEastWaterArrival"),
        };

        /// <summary>The builder's lines a PathDef names in its LineSource.</summary>
        public static readonly string[] Lines = { "StPetersStarterSplat.VillageToBarHeadPath", "StPetersStarterSplat.VillageToSlipPath" };

        /// <summary>A place from its spec: read off the scene, or typed through <paramref name="member"/>.</summary>
        public static TerrainPlanPlace Place(Spec s, Func<string, PlanPoint> member) =>
            s.FromScene ? TerrainPlanPlace.InScene(s.Id, s.Root, s.Name) : TerrainPlanPlace.Typed(s.Id, member(s.Member));
    }

    /// <summary>
    /// What the gatherer reads, as plain values: the grid, the scene's text, today's committed maps as their files'
    /// bytes, and the builder's shapes and points. The editor fills it from the engine
    /// (<see cref="StPetersTerrainPlan.GatherInput"/>); a headless run fills it from the same files and the prototype's
    /// typed constants, and the two must assemble the same sources.
    /// </summary>
    public sealed class TerrainPlanGatherInput
    {
        public TerrainPlanGrid Grid;

        /// <summary>The region scene's YAML (placed things by root, the wharf, the points read by name).</summary>
        public string SceneText;

        /// <summary>The root whose parts' extent is the wharf's footprint.</summary>
        public string WharfRoot = "";

        /// <summary>Today's height map (an 8-bit grayscale PNG) and the range it decodes on.</summary>
        public byte[] HeightPng;
        public float HeightMin, HeightMax;

        /// <summary>Today's splat A to E (8-bit RGBA PNGs): the 20 slots today's paint is read from.</summary>
        public byte[][] SplatPngs;

        public PlanShaderBands Bands = new PlanShaderBands();
        public bool StampFleet;

        public readonly List<TerrainPlanPlace> Buildings = new List<TerrainPlanPlace>();
        public readonly List<TerrainPlanPlace> DockPoints = new List<TerrainPlanPlace>();
        public readonly List<TerrainPlanPlace> Passages = new List<TerrainPlanPlace>();

        public PlanCapsule BerthSlip, ApproachCut, Pocket, Sandbar, BarGut;
        public PlanPoint[] Entrance = new PlanPoint[0];
        public double EntranceHalfWidth;
        public PlanPoint FleetMin, FleetMax;
        public readonly SortedDictionary<string, PlanPoint[]> Lines = new SortedDictionary<string, PlanPoint[]>(StringComparer.Ordinal);
        public PlanPoint IslandCentre;
        public double IslandRadiusX, IslandRadiusY;
        public readonly List<PlanSector> Sectors = new List<PlanSector>();
        public readonly SortedDictionary<string, string> Provenance = new SortedDictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// <b>THE FROZEN SOURCES, ASSEMBLED.</b> The pure half of the gatherer: from what the game holds and the scene and map
    /// files' bytes, the <see cref="TerrainPlanSources"/> the derivation reads, <see cref="TerrainPlanSources.Base"/>
    /// apart (today's analytic ground, sampled at every derivation). Engine-free, so a headless run assembles the same
    /// sources from the same inputs and compares them with the prototype's gathering: the placed things by root, the
    /// wharf's footprint, the points read off the scene by name, and today's paint read from the committed PNGs by the
    /// prototype's rule (<see cref="TerrainPlanToday"/>).
    ///
    /// <para>Strict: a scene name that finds no item, or more than one, and a map that is not the 8-bit map today's
    /// paint was read from, each throw with the name. The sources are frozen once, before PR 5 writes its maps, so a
    /// 16-bit height map here means the sources were frozen already: compare, do not gather.</para>
    /// </summary>
    public static class TerrainPlanGather
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The sources the input holds, today's paint read from its maps; Base is left null.</summary>
        public static TerrainPlanSources Assemble(TerrainPlanGatherInput x)
        {
            if (x == null) throw new ArgumentNullException(nameof(x));
            if (x.Grid.Count <= 0) throw new ArgumentException("[TerrainPlan] the gather has no grid.");
            if (string.IsNullOrEmpty(x.SceneText)) throw new ArgumentException("[TerrainPlan] the gather has no scene text.");
            if (string.IsNullOrEmpty(x.WharfRoot)) throw new ArgumentException("[TerrainPlan] the gather names no wharf root.");
            if (x.Bands == null) throw new ArgumentException("[TerrainPlan] the gather has no shader bands.");

            var items = TerrainPlanSceneScan.Items(x.SceneText);
            var s = new TerrainPlanSources { Grid = x.Grid, StampFleet = x.StampFleet, Bands = x.Bands };
            foreach (var kv in TerrainPlanSceneScan.ByRoot(items)) s.Items[kv.Key] = kv.Value;
            foreach (var b in x.Buildings) s.Buildings.Add(new PlanBuilding(b.Id, Resolve(items, b)));

            var wharf = items.Where(i => i.Root == x.WharfRoot && i.Name != x.WharfRoot && !(i.X == 0 && i.Y == 0)).ToList();
            if (wharf.Count == 0) throw new InvalidDataException("[TerrainPlan] the scene has no parts under the wharf root '" + x.WharfRoot + "'.");
            s.WharfMin = new PlanPoint(wharf.Min(i => i.X), wharf.Min(i => i.Y));
            s.WharfMax = new PlanPoint(wharf.Max(i => i.X), wharf.Max(i => i.Y));

            s.BerthSlip = x.BerthSlip; s.ApproachCut = x.ApproachCut; s.Pocket = x.Pocket; s.Sandbar = x.Sandbar; s.BarGut = x.BarGut;
            s.Entrance = (x.Entrance ?? new PlanPoint[0]).ToArray();
            s.EntranceHalfWidth = x.EntranceHalfWidth;
            foreach (var p in x.DockPoints) s.DockPoints.Add(Resolve(items, p));
            foreach (var p in x.Passages) s.Passages.Add(Resolve(items, p));
            s.FleetMin = x.FleetMin; s.FleetMax = x.FleetMax;
            foreach (var kv in x.Lines) s.Lines[kv.Key] = kv.Value.ToArray();
            s.IslandCentre = x.IslandCentre; s.IslandRadiusX = x.IslandRadiusX; s.IslandRadiusY = x.IslandRadiusY;
            s.Sectors.AddRange(x.Sectors);
            s.Today = Today(x.Grid, x.Bands, x.HeightPng, x.HeightMin, x.HeightMax, x.SplatPngs);
            foreach (var kv in x.Provenance) s.Provenance[kv.Key] = kv.Value;
            return s;
        }

        /// <summary>A place's point: typed, or the one scene item it names.</summary>
        public static PlanPoint Resolve(IList<TerrainPlanSceneScan.Item> items, TerrainPlanPlace p)
        {
            if (!p.FromScene) return p.At;
            var hits = items.Where(i => i.Root == p.Root && i.Name == p.Name).ToList();
            if (hits.Count != 1)
                throw new InvalidDataException("[TerrainPlan] the scene has " + hits.Count + " items named '" + p.Name + "' under '" + p.Root +
                                               "' (the gather needs exactly one" + (p.Id.Length > 0 ? ", for " + p.Id : "") + ").");
            return new PlanPoint(hits[0].X, hits[0].Y);
        }

        /// <summary>Today's zone per cell (row-major, row 0 north) from the committed maps' files.</summary>
        public static byte[] Today(TerrainPlanGrid g, PlanShaderBands bands, byte[] heightPng, float heightMin, float heightMax, IList<byte[]> splatPngs)
        {
            if (heightPng == null) throw new ArgumentException("[TerrainPlan] the gather has no height map.");
            if (splatPngs == null || splatPngs.Count != TerrainPlanZones.SplatSlots / 4)
                throw new ArgumentException("[TerrainPlan] today's paint needs splat A to E (" + TerrainPlanZones.SplatSlots / 4 + " maps).");
            var h = TerrainPlanPng.Read(heightPng);
            Expect(h, g, "today's height map", 0, 1);
            var slots = new byte[TerrainPlanZones.SplatSlots][];
            for (int m = 0; m < splatPngs.Count; m++)
            {
                var img = TerrainPlanPng.Read(splatPngs[m] ?? throw new ArgumentException("[TerrainPlan] splat " + "ABCDE"[m] + " is missing."));
                Expect(img, g, "splat " + "ABCDE"[m], 6, 4);
                for (int ch = 0; ch < 4; ch++) slots[m * 4 + ch] = img.Channel8(ch);          // file order is the plan's (row 0 north)
            }
            return TerrainPlanToday.Zones(g, bands, h.Channel8(0), heightMin, heightMax, slots);
        }

        static void Expect(TerrainPlanPng.Image img, TerrainPlanGrid g, string what, int colourType, int channels)
        {
            if (img.Width != g.W || img.Height != g.H)
                throw new InvalidDataException("[TerrainPlan] " + what + " is " + img.Width + " × " + img.Height + ", not the grid's " + g.W + " × " + g.H + ".");
            if (img.BitDepth != 8 || img.ColourType != colourType || img.Channels != channels)
                throw new InvalidDataException("[TerrainPlan] " + what + " is " + img.BitDepth + "-bit colour type " + img.ColourType +
                                               ", not the 8-bit " + (channels == 1 ? "grayscale" : "RGBA") + " map today's paint was read from" +
                                               (channels == 1 && img.BitDepth == 16 ? " (a 16-bit map: the sources were frozen already; compare, do not gather)" : "") + ".");
        }

        /// <summary>
        /// Every way two sources differ, in the file's order: a number further apart than <paramref name="tolerance"/>
        /// (metres or degrees), a name, a count, or today's paint (by cell count, and the first cell). Provenance and
        /// Base are not compared. Empty when the two agree.
        /// </summary>
        public static List<string> Compare(TerrainPlanSources a, TerrainPlanSources b, double tolerance)
        {
            var o = new List<string>();
            if (a == null || b == null) { o.Add("a side is missing"); return o; }
            if (a.Grid.W != b.Grid.W || a.Grid.H != b.Grid.H || Off(a.Grid.Mpp, b.Grid.Mpp, tolerance) || Off(a.Grid.X0, b.Grid.X0, tolerance) ||
                Off(a.Grid.Y1, b.Grid.Y1, tolerance))
                o.Add("grid: " + GridText(a.Grid) + " vs " + GridText(b.Grid));
            if (a.StampFleet != b.StampFleet) o.Add("stampFleet: " + a.StampFleet + " vs " + b.StampFleet);

            foreach (var root in a.Items.Keys.Union(b.Items.Keys).OrderBy(k => k, StringComparer.Ordinal))
                Points(o, "items." + root, a.ItemsOf(root), b.ItemsOf(root), tolerance);
            if (a.Buildings.Count != b.Buildings.Count) o.Add("buildings: " + a.Buildings.Count + " vs " + b.Buildings.Count);
            for (int k = 0; k < Math.Min(a.Buildings.Count, b.Buildings.Count); k++)
                if (a.Buildings[k].Id != b.Buildings[k].Id || Off(a.Buildings[k].At, b.Buildings[k].At, tolerance))
                    o.Add("buildings[" + k + "]: " + a.Buildings[k].Id + " " + Pt(a.Buildings[k].At) + " vs " + b.Buildings[k].Id + " " + Pt(b.Buildings[k].At));
            Points(o, "wharf", new[] { a.WharfMin, a.WharfMax }, new[] { b.WharfMin, b.WharfMax }, tolerance);
            Cap(o, "berthSlip", a.BerthSlip, b.BerthSlip, tolerance);
            Cap(o, "approachCut", a.ApproachCut, b.ApproachCut, tolerance);
            Cap(o, "pocket", a.Pocket, b.Pocket, tolerance);
            Cap(o, "sandbar", a.Sandbar, b.Sandbar, tolerance);
            Cap(o, "barGut", a.BarGut, b.BarGut, tolerance);
            Points(o, "entrance", a.Entrance, b.Entrance, tolerance);
            if (Off(a.EntranceHalfWidth, b.EntranceHalfWidth, tolerance)) o.Add("entranceHalfWidth: " + N(a.EntranceHalfWidth) + " vs " + N(b.EntranceHalfWidth));
            Points(o, "dockPoints", a.DockPoints, b.DockPoints, tolerance);
            Points(o, "passages", a.Passages, b.Passages, tolerance);
            Points(o, "fleet", new[] { a.FleetMin, a.FleetMax }, new[] { b.FleetMin, b.FleetMax }, tolerance);
            foreach (var line in a.Lines.Keys.Union(b.Lines.Keys).OrderBy(k => k, StringComparer.Ordinal))
                Points(o, "lines." + line, a.Lines.TryGetValue(line, out var la) ? la : new PlanPoint[0],
                       b.Lines.TryGetValue(line, out var lb) ? lb : new PlanPoint[0], tolerance);
            Points(o, "islandCentre", new[] { a.IslandCentre }, new[] { b.IslandCentre }, tolerance);
            if (Off(a.IslandRadiusX, b.IslandRadiusX, tolerance) || Off(a.IslandRadiusY, b.IslandRadiusY, tolerance))
                o.Add("islandRadius: " + N(a.IslandRadiusX) + " × " + N(a.IslandRadiusY) + " vs " + N(b.IslandRadiusX) + " × " + N(b.IslandRadiusY));
            if (a.Sectors.Count != b.Sectors.Count) o.Add("sectors: " + a.Sectors.Count + " vs " + b.Sectors.Count);
            for (int k = 0; k < Math.Min(a.Sectors.Count, b.Sectors.Count); k++)
                if (a.Sectors[k].Class != b.Sectors[k].Class || Off(a.Sectors[k].FromDeg, b.Sectors[k].FromDeg, tolerance))
                    o.Add("sectors[" + k + "]: " + N(a.Sectors[k].FromDeg) + " " + a.Sectors[k].Class + " vs " + N(b.Sectors[k].FromDeg) + " " + b.Sectors[k].Class);
            string ba = BandsText(a.Bands), bb = BandsText(b.Bands);
            if (ba != bb) o.Add("bands: " + ba + " vs " + bb);

            if (a.Today == null || b.Today == null || a.Today.Length != b.Today.Length)
                o.Add("today: " + (a.Today?.Length.ToString(Inv) ?? "none") + " vs " + (b.Today?.Length.ToString(Inv) ?? "none") + " cells");
            else
            {
                int diff = 0, first = -1;
                for (int i = 0; i < a.Today.Length; i++)
                    if (a.Today[i] != b.Today[i]) { diff++; if (first < 0) first = i; }
                if (diff > 0)
                    o.Add("today: " + diff + " cells differ, the first at (" + N(a.Grid.XG(first % a.Grid.W)) + ", " + N(a.Grid.YG(first / a.Grid.W)) + "): " +
                          a.Today[first] + " vs " + b.Today[first]);
            }
            return o;
        }

        static bool Off(double a, double b, double tol) => !(Math.Abs(a - b) <= tol);
        static bool Off(PlanPoint a, PlanPoint b, double tol) => Off(a.X, b.X, tol) || Off(a.Y, b.Y, tol);

        static void Points(List<string> o, string what, IList<PlanPoint> a, IList<PlanPoint> b, double tol)
        {
            if (a.Count != b.Count) { o.Add(what + ": " + a.Count + " vs " + b.Count + " points"); return; }
            int diff = 0, first = -1;
            for (int k = 0; k < a.Count; k++)
                if (Off(a[k], b[k], tol)) { diff++; if (first < 0) first = k; }
            if (diff > 0) o.Add(what + ": " + diff + " of " + a.Count + " points differ, the first [" + first + "] " + Pt(a[first]) + " vs " + Pt(b[first]));
        }

        static void Cap(List<string> o, string what, PlanCapsule a, PlanCapsule b, double tol)
        {
            if (Off(a.A, b.A, tol) || Off(a.B, b.B, tol) || Off(a.HalfWidth, b.HalfWidth, tol))
                o.Add(what + ": " + Pt(a.A) + "–" + Pt(a.B) + " ±" + N(a.HalfWidth) + " vs " + Pt(b.A) + "–" + Pt(b.B) + " ±" + N(b.HalfWidth));
        }

        static string N(double v) => TerrainPlanSourcesJson.Num(v);
        static string Pt(PlanPoint p) => "(" + N(p.X) + ", " + N(p.Y) + ")";
        static string GridText(TerrainPlanGrid g) => g.W + " × " + g.H + " at " + N(g.Mpp) + " from (" + N(g.X0) + ", " + N(g.Y1) + ")";

        static string BandsText(PlanShaderBands d) => d == null ? "none" : string.Join(" ", new[]
        {
            d.PaintFloor, d.RippleFloor, d.SandFloor, d.MarramFloor, d.GrassFloor, d.ShingleFloor, d.BandBlend, d.IslandCentre.X, d.IslandCentre.Y,
            d.IslandAspect, d.Weather.X, d.Weather.Y, d.SectorBlend, d.BarFrom.X, d.BarFrom.Y, d.BarTo.X, d.BarTo.Y, d.BarHalfWidth, d.BarEdge,
        }.Select(N));
    }
}
