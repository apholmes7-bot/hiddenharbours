using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE SCENE, READ AS TEXT.</b> Every placed thing in a scene file and the root it hangs under, read from the
    /// Force Text YAML by lines: the scene is never opened. Terrain pass 9's <c>scene_parse.py</c>, ported line for line,
    /// so the plan's sources read the same items the prototype read (the gatherer's scan is checked against the
    /// prototype's parse of the same file).
    ///
    /// <para>An item is every GameObject that is not a prefab's stripped stand-in, at its transform's world position
    /// (the local positions summed up the father chain, translation only), then every prefab instance, at its parent's
    /// world position plus the instance's own position modification. Its root is the top ancestor's name; a prefab
    /// instance at the top is its own root. Positions are rounded to 2 decimals, as the prototype's JSON holds them.
    /// Pure: no engine call, so a headless run can check it.</para>
    /// </summary>
    public static class TerrainPlanSceneScan
    {
        /// <summary>One placed thing.</summary>
        public sealed class Item
        {
            public string Name, Root, Kind;
            public double X, Y;
        }

        static readonly Regex Header = new Regex(@"^--- !u!(\d+) &(-?\d+)( stripped)?", RegexOptions.CultureInvariant);
        static readonly Regex FileId = new Regex(@"m_GameObject: \{fileID: (-?\d+)", RegexOptions.CultureInvariant);
        static readonly Regex Father = new Regex(@"m_Father: \{fileID: (-?\d+)", RegexOptions.CultureInvariant);
        static readonly Regex Prefab = new Regex(@"m_PrefabInstance: \{fileID: (-?\d+)", RegexOptions.CultureInvariant);
        static readonly Regex LocalPosition = new Regex(
            @"m_LocalPosition: \{x: ([-\d.eE+]+), y: ([-\d.eE+]+), z: ([-\d.eE+]+)(?:, w: ([-\d.eE+]+))?\}", RegexOptions.CultureInvariant);
        static readonly Regex TransformParent = new Regex(@"m_TransformParent: \{fileID: (-?\d+)", RegexOptions.CultureInvariant);
        static readonly Regex SourcePrefab = new Regex(@"m_SourcePrefab: \{fileID: -?\d+, guid: ([0-9a-f]+)", RegexOptions.CultureInvariant);
        static readonly Regex Mod = new Regex(
            @"- target: \{fileID: (-?\d+), guid: [0-9a-f]+, type: 3\}\s*\n\s*propertyPath: (\S+)\s*\n\s*value: ?(.*)", RegexOptions.CultureInvariant);

        sealed class Go { public string Name = ""; public bool Stripped; }
        sealed class Tr { public long Go, Father, Pi; public double[] Pos = new double[3]; public bool Stripped; }
        sealed class Pi
        {
            public long Parent;
            public string Src = "";
            // target → (property → value), each in first-seen order, as the prototype's defaultdict(dict) holds them
            public readonly List<KeyValuePair<long, List<KeyValuePair<string, string>>>> Mods =
                new List<KeyValuePair<long, List<KeyValuePair<string, string>>>>();
        }
        sealed class PiInfo { public string Name; public double[] Pos; public long Parent; public string Src; }

        /// <summary>Every item in the scene text, GameObjects in document order, then prefab instances.</summary>
        public static List<Item> Items(string sceneText)
        {
            if (sceneText == null) throw new ArgumentNullException(nameof(sceneText));
            var gos = new List<KeyValuePair<long, Go>>();
            var goIndex = new Dictionary<long, Go>();
            var trs = new Dictionary<long, Tr>();
            var trOrder = new List<long>();
            var pis = new Dictionary<long, Pi>();
            var piOrder = new List<long>();

            int cls = -1; long fid = 0; bool stripped = false;
            var lines = new List<string>();
            void Flush()
            {
                if (cls < 0) return;
                string body = string.Join("\n", lines);
                if (cls == 1)
                {
                    var g = new Go { Stripped = stripped };
                    foreach (var l in lines)
                    {
                        string t = l.Trim();
                        if (t.StartsWith("m_Name:", StringComparison.Ordinal)) { g.Name = l.Substring(l.IndexOf("m_Name:", StringComparison.Ordinal) + 7).Trim(); break; }
                    }
                    gos.Add(new KeyValuePair<long, Go>(fid, g));
                    goIndex[fid] = g;
                }
                else if (cls == 4 || cls == 224)
                {
                    var t = new Tr { Stripped = stripped };
                    var m = FileId.Match(body); if (m.Success) t.Go = long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                    m = Father.Match(body); if (m.Success) t.Father = long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                    m = LocalPosition.Match(body);
                    if (m.Success) for (int i = 0; i < 3; i++) t.Pos[i] = Num(m.Groups[i + 1].Value);
                    m = Prefab.Match(body); if (m.Success) t.Pi = long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                    if (!trs.ContainsKey(fid)) trOrder.Add(fid);
                    trs[fid] = t;
                }
                else if (cls == 1001)
                {
                    var p = new Pi();
                    var m = TransformParent.Match(body); if (m.Success) p.Parent = long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                    m = SourcePrefab.Match(body); if (m.Success) p.Src = m.Groups[1].Value;
                    foreach (Match mm in Mod.Matches(body))
                    {
                        long target = long.Parse(mm.Groups[1].Value, CultureInfo.InvariantCulture);
                        string path = mm.Groups[2].Value, value = mm.Groups[3].Value.Trim();
                        int k = p.Mods.FindIndex(e => e.Key == target);
                        if (k < 0) { p.Mods.Add(new KeyValuePair<long, List<KeyValuePair<string, string>>>(target, new List<KeyValuePair<string, string>>())); k = p.Mods.Count - 1; }
                        var props = p.Mods[k].Value;
                        int j = props.FindIndex(e => e.Key == path);
                        if (j < 0) props.Add(new KeyValuePair<string, string>(path, value));
                        else props[j] = new KeyValuePair<string, string>(path, value);
                    }
                    if (!pis.ContainsKey(fid)) piOrder.Add(fid);
                    pis[fid] = p;
                }
            }

            int at = 0, n = sceneText.Length;
            while (at < n)
            {
                int end = sceneText.IndexOf('\n', at);
                if (end < 0) end = n;
                string line = sceneText.Substring(at, end - at).TrimEnd('\r');
                at = end + 1;
                var h = line.StartsWith("--- !u!", StringComparison.Ordinal) ? Header.Match(line) : Match.Empty;
                if (h.Success)
                {
                    Flush();
                    cls = int.Parse(h.Groups[1].Value, CultureInfo.InvariantCulture);
                    fid = long.Parse(h.Groups[2].Value, CultureInfo.InvariantCulture);
                    stripped = h.Groups[3].Success;
                    lines.Clear();
                }
                else if (cls >= 0) lines.Add(line);
            }
            Flush();

            PiInfo Info(long pid)
            {
                if (!pis.TryGetValue(pid, out var p)) return null;
                string name = null;
                var pos = new double?[3];
                foreach (var target in p.Mods)
                    foreach (var prop in target.Value)
                    {
                        if (prop.Key == "m_Name") name = prop.Value;
                        for (int i = 0; i < 3; i++)
                            if (prop.Key == "m_LocalPosition." + "xyz"[i] && TryNum(prop.Value, out double v)) pos[i] = v;
                    }
                return new PiInfo { Name = name, Pos = new[] { pos[0] ?? 0.0, pos[1] ?? 0.0, pos[2] ?? 0.0 }, Parent = p.Parent, Src = p.Src };
            }

            double[] World(long tid, int depth)
            {
                if (depth > 64 || tid == 0 || !trs.TryGetValue(tid, out var t)) return new double[3];
                if (t.Stripped)
                {
                    var info = Info(t.Pi);
                    if (info == null) return new double[3];
                    var pw = World(info.Parent, depth + 1);
                    return new[] { pw[0] + info.Pos[0], pw[1] + info.Pos[1], pw[2] + info.Pos[2] };
                }
                var fw = World(t.Father, depth + 1);
                return new[] { fw[0] + t.Pos[0], fw[1] + t.Pos[1], fw[2] + t.Pos[2] };
            }

            long RootOf(long tid, int depth)
            {
                if (depth > 64 || !trs.TryGetValue(tid, out var t)) return tid;
                if (t.Stripped)
                {
                    var info = Info(t.Pi);
                    return info == null || info.Parent == 0 ? tid : RootOf(info.Parent, depth + 1);
                }
                return t.Father == 0 ? tid : RootOf(t.Father, depth + 1);
            }

            string RootName(long rt)
            {
                if (!trs.TryGetValue(rt, out var t)) return rt.ToString(CultureInfo.InvariantCulture);
                if (t.Stripped)
                {
                    var info = Info(t.Pi);
                    return (info != null && !string.IsNullOrEmpty(info.Name) ? info.Name : "prefab") + " [prefab]";
                }
                return goIndex.TryGetValue(t.Go, out var g) ? g.Name : rt.ToString(CultureInfo.InvariantCulture);
            }

            var goTr = new Dictionary<long, long>();
            foreach (long tid in trOrder)
                if (!trs[tid].Stripped) goTr[trs[tid].Go] = tid;

            var items = new List<Item>();
            foreach (var kv in gos)
            {
                if (kv.Value.Stripped || !goTr.TryGetValue(kv.Key, out long tid)) continue;
                var w = World(tid, 0);
                items.Add(new Item { Name = kv.Value.Name, Kind = "go", Root = RootName(RootOf(tid, 0)),
                                     X = TerrainPlanMath.PyRound(w[0], 2), Y = TerrainPlanMath.PyRound(w[1], 2) });
            }
            foreach (long pid in piOrder)
            {
                var info = Info(pid);
                var pw = World(info.Parent, 0);
                string name = !string.IsNullOrEmpty(info.Name) ? info.Name : "prefab:" + (info.Src.Length > 8 ? info.Src.Substring(0, 8) : info.Src);
                string root = info.Parent == 0 ? name : RootName(RootOf(info.Parent, 0));
                items.Add(new Item { Name = name, Kind = "prefab", Root = root,
                                     X = TerrainPlanMath.PyRound(pw[0] + info.Pos[0], 2), Y = TerrainPlanMath.PyRound(pw[1] + info.Pos[1], 2) });
            }
            return items;
        }

        /// <summary>The items by root name, in item order, never an item at the world origin (a root's own transform).</summary>
        public static Dictionary<string, List<PlanPoint>> ByRoot(IEnumerable<Item> items)
        {
            var o = new Dictionary<string, List<PlanPoint>>(StringComparer.Ordinal);
            foreach (var i in items)
            {
                if (i.X == 0 && i.Y == 0) continue;
                if (!o.TryGetValue(i.Root, out var l)) o[i.Root] = l = new List<PlanPoint>();
                l.Add(new PlanPoint(i.X, i.Y));
            }
            return o;
        }

        static double Num(string s) => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        static bool TryNum(string s, out double v) => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
    }
}
