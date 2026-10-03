using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>THE PLAN'S FROZEN SOURCES, AS TEXT.</b> Everything <see cref="TerrainPlanDerivation"/> reads besides the plan's
    /// Defs and today's analytic ground: the grid, the placed things by scene root, the builder's shapes and lines, the
    /// coast plan's frame, the band rule today's paint was read by, and today's paint (run-length, row-major, row 0 north).
    ///
    /// <para>Why frozen: the derivation reads today's splat and today's clam holes, and the same PR writes the new splat
    /// and re-scatters the holes over the new ground. Read live, a second derivation would read its own output. So the
    /// editor gathers the sources once, before anything is written, and commits them beside the Defs; the derivation,
    /// the window and the determinism test read that file. A fresh derivation is a derivation from this file.</para>
    ///
    /// <para>Pure and engine-free, so a headless run can round-trip it. The text is canonical: the same sources write
    /// the same bytes, and <see cref="Sha256"/> hashes that text.</para>
    /// </summary>
    public static class TerrainPlanSourcesJson
    {
        public const string Format = "hidden-harbours.terrain-plan-sources/1";

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---- write ---------------------------------------------------------------------------------------------------

        /// <summary>The sources as canonical text. <see cref="TerrainPlanSources.Base"/> is not written: it is today's
        /// analytic ground, recomputed from the builder's constants at every derivation.</summary>
        public static string Write(TerrainPlanSources s, string planId)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (s.Today == null || s.Today.Length != s.Grid.Count) throw new ArgumentException("[TerrainPlan] the sources' Today does not fill the grid.");
            var b = new StringBuilder(1 << 16);
            b.Append("{\n");
            Key(b, "format"); Str(b, Format); b.Append(",\n");
            Key(b, "plan"); Str(b, planId ?? ""); b.Append(",\n");
            Key(b, "grid"); b.Append("{\"w\": ").Append(s.Grid.W.ToString(Inv)).Append(", \"h\": ").Append(s.Grid.H.ToString(Inv))
                             .Append(", \"mpp\": ").Append(Num(s.Grid.Mpp)).Append(", \"x0\": ").Append(Num(s.Grid.X0))
                             .Append(", \"y1\": ").Append(Num(s.Grid.Y1)).Append("},\n");
            Key(b, "stampFleet"); b.Append(s.StampFleet ? "true" : "false").Append(",\n");
            Key(b, "items"); b.Append("{\n");
            var roots = s.Items.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            for (int k = 0; k < roots.Count; k++)
            {
                b.Append("  "); Str(b, roots[k]); b.Append(": "); Points(b, s.Items[roots[k]]);
                b.Append(k + 1 < roots.Count ? ",\n" : "\n");
            }
            b.Append("},\n");
            Key(b, "buildings"); b.Append("[");
            for (int k = 0; k < s.Buildings.Count; k++)
            {
                if (k > 0) b.Append(", ");
                b.Append('['); Str(b, s.Buildings[k].Id); b.Append(", ").Append(Num(s.Buildings[k].At.X)).Append(", ").Append(Num(s.Buildings[k].At.Y)).Append(']');
            }
            b.Append("],\n");
            Key(b, "wharf"); Nums(b, s.WharfMin.X, s.WharfMin.Y, s.WharfMax.X, s.WharfMax.Y); b.Append(",\n");
            Key(b, "berthSlip"); Cap(b, s.BerthSlip); b.Append(",\n");
            Key(b, "approachCut"); Cap(b, s.ApproachCut); b.Append(",\n");
            Key(b, "pocket"); Cap(b, s.Pocket); b.Append(",\n");
            Key(b, "sandbar"); Cap(b, s.Sandbar); b.Append(",\n");
            Key(b, "barGut"); Cap(b, s.BarGut); b.Append(",\n");
            Key(b, "entrance"); Points(b, s.Entrance); b.Append(",\n");
            Key(b, "entranceHalfWidth"); b.Append(Num(s.EntranceHalfWidth)).Append(",\n");
            Key(b, "dockPoints"); Points(b, s.DockPoints); b.Append(",\n");
            Key(b, "passages"); Points(b, s.Passages); b.Append(",\n");
            Key(b, "fleet"); Nums(b, s.FleetMin.X, s.FleetMin.Y, s.FleetMax.X, s.FleetMax.Y); b.Append(",\n");
            Key(b, "lines"); b.Append("{\n");
            var lines = s.Lines.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            for (int k = 0; k < lines.Count; k++)
            {
                b.Append("  "); Str(b, lines[k]); b.Append(": "); Points(b, s.Lines[lines[k]]);
                b.Append(k + 1 < lines.Count ? ",\n" : "\n");
            }
            b.Append("},\n");
            Key(b, "islandCentre"); Nums(b, s.IslandCentre.X, s.IslandCentre.Y); b.Append(",\n");
            Key(b, "islandRadiusX"); b.Append(Num(s.IslandRadiusX)).Append(",\n");
            Key(b, "islandRadiusY"); b.Append(Num(s.IslandRadiusY)).Append(",\n");
            Key(b, "sectors"); b.Append("[");
            for (int k = 0; k < s.Sectors.Count; k++)
            {
                if (k > 0) b.Append(", ");
                b.Append('[').Append(Num(s.Sectors[k].FromDeg)).Append(", "); Str(b, s.Sectors[k].Class); b.Append(']');
            }
            b.Append("],\n");
            var bd = s.Bands ?? throw new ArgumentException("[TerrainPlan] the sources have no shader bands.");
            Key(b, "bands"); b.Append("{\"floors\": ");
            Nums(b, bd.PaintFloor, bd.RippleFloor, bd.SandFloor, bd.MarramFloor, bd.GrassFloor, bd.ShingleFloor);
            b.Append(", \"blend\": ").Append(Num(bd.BandBlend)).Append(", \"islandCentre\": "); Nums(b, bd.IslandCentre.X, bd.IslandCentre.Y);
            b.Append(", \"islandAspect\": ").Append(Num(bd.IslandAspect)).Append(", \"weather\": "); Nums(b, bd.Weather.X, bd.Weather.Y);
            b.Append(", \"sectorBlend\": ").Append(Num(bd.SectorBlend)).Append(", \"bar\": ");
            Nums(b, bd.BarFrom.X, bd.BarFrom.Y, bd.BarTo.X, bd.BarTo.Y, bd.BarHalfWidth, bd.BarEdge);
            b.Append("},\n");
            Key(b, "today"); b.Append("{\"order\": \"row-major, row 0 north\", \"unpainted\": ").Append(TerrainPlanZones.Unpainted.ToString(Inv))
                              .Append(", \"runs\": [");
            int n = s.Today.Length, runs = 0;
            for (int i = 0; i < n;)
            {
                int j = i + 1;
                while (j < n && s.Today[j] == s.Today[i]) j++;
                if (runs > 0) b.Append(runs % 16 == 0 ? ",\n  " : ", ");
                b.Append('[').Append(s.Today[i].ToString(Inv)).Append(", ").Append((j - i).ToString(Inv)).Append(']');
                runs++;
                i = j;
            }
            b.Append("]},\n");
            Key(b, "provenance"); b.Append("{\n");
            var prov = s.Provenance.Keys.ToList();                                  // a SortedDictionary: ordinal order
            for (int k = 0; k < prov.Count; k++)
            {
                b.Append("  "); Str(b, prov[k]); b.Append(": "); Str(b, s.Provenance[prov[k]]);
                b.Append(k + 1 < prov.Count ? ",\n" : "\n");
            }
            b.Append("}\n}\n");
            return b.ToString();
        }

        /// <summary>The sha256 of the canonical text (lower-case hex).</summary>
        public static string Sha256(string canonical)
        {
            using (var h = SHA256.Create())
                return string.Concat(h.ComputeHash(Encoding.UTF8.GetBytes(canonical)).Select(x => x.ToString("x2", Inv)));
        }

        /// <summary>A number as the file holds it: up to six decimals when that is exact, else round-trip digits.</summary>
        public static string Num(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) throw new ArgumentException("[TerrainPlan] a source number is not finite.");
            double r6 = Math.Round(v, 6, MidpointRounding.ToEven);
            string s = r6 == v ? v.ToString("0.######", Inv) : v.ToString("G17", Inv);
            return s == "-0" ? "0" : s;
        }

        static void Key(StringBuilder b, string k) { Str(b, k); b.Append(": "); }

        internal static void Str(StringBuilder b, string s)
        {
            b.Append('"');
            foreach (char ch in s)
            {
                switch (ch)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\t': b.Append("\\t"); break;
                    default:
                        if (ch < 0x20) b.Append("\\u").Append(((int)ch).ToString("x4", Inv));
                        else b.Append(ch);
                        break;
                }
            }
            b.Append('"');
        }

        static void Nums(StringBuilder b, params double[] v)
        {
            b.Append('[');
            for (int k = 0; k < v.Length; k++) { if (k > 0) b.Append(", "); b.Append(Num(v[k])); }
            b.Append(']');
        }

        static void Cap(StringBuilder b, PlanCapsule c) => Nums(b, c.A.X, c.A.Y, c.B.X, c.B.Y, c.HalfWidth);

        static void Points(StringBuilder b, IList<PlanPoint> pts)
        {
            b.Append('[');
            for (int k = 0; k < pts.Count; k++)
            {
                if (k > 0) b.Append(k % 12 == 0 ? ",\n    " : ", ");
                b.Append('[').Append(Num(pts[k].X)).Append(", ").Append(Num(pts[k].Y)).Append(']');
            }
            b.Append(']');
        }

        // ---- read ----------------------------------------------------------------------------------------------------

        /// <summary>
        /// Read the file's text into sources, <see cref="TerrainPlanSources.Base"/> left null for the caller to fill.
        /// Throws <see cref="FormatException"/> naming the first thing that does not read.
        /// </summary>
        public static TerrainPlanSources Read(string text, out string planId)
        {
            var root = new Parser(text).Document() as Dictionary<string, object>;
            if (root == null) throw new FormatException("[TerrainPlan] the sources file is not a JSON object.");
            if (S(root, "format") != Format) throw new FormatException("[TerrainPlan] the sources file's format is not " + Format + ".");
            planId = S(root, "plan");
            var g = O(root, "grid");
            var s = new TerrainPlanSources
            {
                Grid = new TerrainPlanGrid((int)D(g, "w"), (int)D(g, "h"), D(g, "mpp"), D(g, "x0"), D(g, "y1")),
                StampFleet = B(root, "stampFleet"),
            };
            foreach (var kv in O(root, "items")) s.Items[kv.Key] = Pts(kv.Value, "items." + kv.Key).ToList();
            foreach (var o in A(root, "buildings"))
            {
                var t = o as List<object>;
                if (t == null || t.Count != 3 || !(t[0] is string)) throw new FormatException("[TerrainPlan] a building is not [id, x, y].");
                s.Buildings.Add(new PlanBuilding((string)t[0], new PlanPoint(Dn(t[1]), Dn(t[2]))));
            }
            var w = Ds(root, "wharf", 4);
            s.WharfMin = new PlanPoint(w[0], w[1]); s.WharfMax = new PlanPoint(w[2], w[3]);
            s.BerthSlip = Cp(root, "berthSlip"); s.ApproachCut = Cp(root, "approachCut"); s.Pocket = Cp(root, "pocket");
            s.Sandbar = Cp(root, "sandbar"); s.BarGut = Cp(root, "barGut");
            s.Entrance = Pts(Get(root, "entrance"), "entrance");
            s.EntranceHalfWidth = D(root, "entranceHalfWidth");
            s.DockPoints.AddRange(Pts(Get(root, "dockPoints"), "dockPoints"));
            s.Passages.AddRange(Pts(Get(root, "passages"), "passages"));
            var f = Ds(root, "fleet", 4);
            s.FleetMin = new PlanPoint(f[0], f[1]); s.FleetMax = new PlanPoint(f[2], f[3]);
            foreach (var kv in O(root, "lines")) s.Lines[kv.Key] = Pts(kv.Value, "lines." + kv.Key);
            var ic = Ds(root, "islandCentre", 2);
            s.IslandCentre = new PlanPoint(ic[0], ic[1]);
            s.IslandRadiusX = D(root, "islandRadiusX");
            s.IslandRadiusY = D(root, "islandRadiusY");
            foreach (var o in A(root, "sectors"))
            {
                var t = o as List<object>;
                if (t == null || t.Count != 2 || !(t[1] is string)) throw new FormatException("[TerrainPlan] a sector is not [fromDeg, class].");
                s.Sectors.Add(new PlanSector(Dn(t[0]), (string)t[1]));
            }
            var bo = O(root, "bands");
            var fl = Ds(bo, "floors", 6);
            var bc = Ds(bo, "islandCentre", 2);
            var bw = Ds(bo, "weather", 2);
            var bb = Ds(bo, "bar", 6);
            s.Bands = new PlanShaderBands
            {
                PaintFloor = fl[0], RippleFloor = fl[1], SandFloor = fl[2], MarramFloor = fl[3], GrassFloor = fl[4], ShingleFloor = fl[5],
                BandBlend = D(bo, "blend"), IslandCentre = new PlanPoint(bc[0], bc[1]), IslandAspect = D(bo, "islandAspect"),
                Weather = new PlanPoint(bw[0], bw[1]), SectorBlend = D(bo, "sectorBlend"),
                BarFrom = new PlanPoint(bb[0], bb[1]), BarTo = new PlanPoint(bb[2], bb[3]), BarHalfWidth = bb[4], BarEdge = bb[5],
            };
            var today = O(root, "today");
            var run = A(today, "runs");
            s.Today = new byte[s.Grid.Count];
            int i = 0;
            foreach (var o in run)
            {
                var t = o as List<object>;
                if (t == null || t.Count != 2) throw new FormatException("[TerrainPlan] a run of today's paint is not [zone, count].");
                double z = Dn(t[0]), c = Dn(t[1]);
                if (z < 0 || z > 255 || z != Math.Floor(z) || c < 1 || c != Math.Floor(c) || i + c > s.Today.Length)
                    throw new FormatException("[TerrainPlan] a run of today's paint is out of range at cell " + i + ".");
                for (int k = 0; k < (int)c; k++) s.Today[i++] = (byte)z;
            }
            if (i != s.Today.Length) throw new FormatException("[TerrainPlan] today's paint covers " + i + " of " + s.Today.Length + " cells.");
            foreach (var kv in O(root, "provenance"))
                s.Provenance[kv.Key] = kv.Value as string ?? throw new FormatException("[TerrainPlan] provenance." + kv.Key + " is not a string.");
            return s;
        }

        /// <summary>
        /// Any JSON text by the same strict reader: objects as <c>Dictionary&lt;string, object&gt;</c>, arrays as
        /// <c>List&lt;object&gt;</c>, strings, doubles, true, false and null. A second key, text after the document or an
        /// unknown escape is refused. The ground file's intake reads with it.
        /// </summary>
        public static object ParseDocument(string text) => new Parser(text).Document();

        static object Get(Dictionary<string, object> o, string k) =>
            o.TryGetValue(k, out var v) ? v : throw new FormatException("[TerrainPlan] the sources file has no \"" + k + "\".");

        static Dictionary<string, object> O(Dictionary<string, object> o, string k) =>
            Get(o, k) as Dictionary<string, object> ?? throw new FormatException("[TerrainPlan] \"" + k + "\" is not an object.");

        static List<object> A(Dictionary<string, object> o, string k) =>
            Get(o, k) as List<object> ?? throw new FormatException("[TerrainPlan] \"" + k + "\" is not an array.");

        static string S(Dictionary<string, object> o, string k) =>
            Get(o, k) as string ?? throw new FormatException("[TerrainPlan] \"" + k + "\" is not a string.");

        static bool B(Dictionary<string, object> o, string k) =>
            Get(o, k) is bool v ? v : throw new FormatException("[TerrainPlan] \"" + k + "\" is not true or false.");

        static double D(Dictionary<string, object> o, string k) => Dn(Get(o, k));

        static double Dn(object v) => v is double d ? d : throw new FormatException("[TerrainPlan] a source value is not a number.");

        static double[] Ds(Dictionary<string, object> o, string k, int n)
        {
            var a = A(o, k);
            if (a.Count != n) throw new FormatException("[TerrainPlan] \"" + k + "\" needs " + n + " numbers.");
            return a.Select(Dn).ToArray();
        }

        static PlanCapsule Cp(Dictionary<string, object> o, string k)
        {
            var v = Ds(o, k, 5);
            return new PlanCapsule(new PlanPoint(v[0], v[1]), new PlanPoint(v[2], v[3]), v[4]);
        }

        static PlanPoint[] Pts(object v, string where)
        {
            var a = v as List<object> ?? throw new FormatException("[TerrainPlan] \"" + where + "\" is not an array of points.");
            var pts = new PlanPoint[a.Count];
            for (int k = 0; k < a.Count; k++)
            {
                var t = a[k] as List<object>;
                if (t == null || t.Count != 2) throw new FormatException("[TerrainPlan] \"" + where + "\"[" + k + "] is not [x, y].");
                pts[k] = new PlanPoint(Dn(t[0]), Dn(t[1]));
            }
            return pts;
        }

        /// <summary>A strict reader for the JSON this class writes: objects, arrays, strings, numbers, true, false, null.</summary>
        sealed class Parser
        {
            readonly string _t;
            int _i;

            public Parser(string text) { _t = text ?? throw new FormatException("[TerrainPlan] the sources file is empty."); }

            public object Document()
            {
                var v = Value();
                Ws();
                if (_i != _t.Length) Fail("text after the document");
                return v;
            }

            object Value()
            {
                Ws();
                if (_i >= _t.Length) Fail("the end of the text");
                char ch = _t[_i];
                if (ch == '{') return Obj();
                if (ch == '[') return Arr();
                if (ch == '"') return Str();
                if (Lit("true")) return true;
                if (Lit("false")) return false;
                if (Lit("null")) return null;
                return Number();
            }

            Dictionary<string, object> Obj()
            {
                var o = new Dictionary<string, object>(StringComparer.Ordinal);
                _i++;
                Ws();
                if (Peek('}')) { _i++; return o; }
                while (true)
                {
                    Ws();
                    if (!Peek('"')) Fail("a key");
                    string k = Str();
                    Ws();
                    if (!Peek(':')) Fail("':'");
                    _i++;
                    if (o.ContainsKey(k)) Fail("a second \"" + k + "\"");
                    o[k] = Value();
                    Ws();
                    if (Peek(',')) { _i++; continue; }
                    if (Peek('}')) { _i++; return o; }
                    Fail("',' or '}'");
                }
            }

            List<object> Arr()
            {
                var a = new List<object>();
                _i++;
                Ws();
                if (Peek(']')) { _i++; return a; }
                while (true)
                {
                    a.Add(Value());
                    Ws();
                    if (Peek(',')) { _i++; continue; }
                    if (Peek(']')) { _i++; return a; }
                    Fail("',' or ']'");
                }
            }

            string Str()
            {
                var b = new StringBuilder();
                _i++;
                while (true)
                {
                    if (_i >= _t.Length) Fail("the string's end");
                    char ch = _t[_i++];
                    if (ch == '"') return b.ToString();
                    if (ch != '\\') { b.Append(ch); continue; }
                    if (_i >= _t.Length) Fail("an escape");
                    char e = _t[_i++];
                    switch (e)
                    {
                        case '"': b.Append('"'); break;
                        case '\\': b.Append('\\'); break;
                        case '/': b.Append('/'); break;
                        case 'b': b.Append('\b'); break;
                        case 'f': b.Append('\f'); break;
                        case 'n': b.Append('\n'); break;
                        case 'r': b.Append('\r'); break;
                        case 't': b.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _t.Length) Fail("four hex digits");
                            b.Append((char)int.Parse(_t.Substring(_i, 4), NumberStyles.HexNumber, Inv));
                            _i += 4;
                            break;
                        default: Fail("a known escape"); break;
                    }
                }
            }

            double Number()
            {
                int s = _i;
                while (_i < _t.Length && "+-0123456789.eE".IndexOf(_t[_i]) >= 0) _i++;
                if (s == _i) Fail("a value");
                if (!double.TryParse(_t.Substring(s, _i - s), NumberStyles.Float, Inv, out double v)) { _i = s; Fail("a number"); }
                return v;
            }

            bool Lit(string w)
            {
                if (string.CompareOrdinal(_t, _i, w, 0, w.Length) != 0) return false;
                _i += w.Length;
                return true;
            }

            bool Peek(char ch) => _i < _t.Length && _t[_i] == ch;

            void Ws()
            {
                while (_i < _t.Length && (_t[_i] == ' ' || _t[_i] == '\n' || _t[_i] == '\r' || _t[_i] == '\t')) _i++;
            }

            void Fail(string wanted)
            {
                int line = 1;
                for (int k = 0; k < Math.Min(_i, _t.Length); k++) if (_t[k] == '\n') line++;
                throw new FormatException("[TerrainPlan] the sources file: expected " + wanted + " at line " + line + ".");
            }
        }
    }
}
