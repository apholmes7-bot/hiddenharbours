using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The ground Nine Mile Creek's scene stands on</b> (NMC H2): G3, the painted third issue. A test that holds the
    /// SCENE to a claim (a berth floats, a quay stays dry, a road is dry) probes it here. A test of the analytic plan's
    /// own authoring keeps <see cref="NineMileCreekBuilder.ConfigureNineMileCreekTerrain"/>: since NMC H2 that is the
    /// premise the later waves still author on, not the scene's ground.
    ///
    /// <para><b>And the scene that stands on it.</b> <see cref="Scene"/> is NineMileCreek.unity as N1 writes it, read
    /// as text: the objects a live test holds to G3 are the ones that ship, not the ones the builder would lay.</para>
    /// </summary>
    public static class NineMileCreekLiveGround
    {
        static NineMileCreekGround.PlanGround _plan;
        static SceneYaml _scene;

        /// <summary>G3 through PR 1's seam, decoded once per domain. Pure, so a probe needs no scene and no asset
        /// import; the runtime component below reads the same ground within one R16 code.</summary>
        public static ITidalTerrain Plan => _plan ??= NineMileCreekGround.Load();

        /// <summary>The scene's own terrain on <paramref name="host"/>: a <see cref="PaintedTidalTerrain"/> on the G3
        /// map, as the builder and 04-ground-g3 lay it.</summary>
        public static PaintedTidalTerrain AddTo(GameObject host)
        {
            var terrain = host.AddComponent<PaintedTidalTerrain>();
            terrain.Map = NineMileCreekBuilder.LoadGround();
            return terrain;
        }

        /// <summary><see cref="AddTo"/>, registered as the region's tidal terrain the way its OnEnable registers it in
        /// play (an edit-mode fixture gets no OnEnable).</summary>
        public static PaintedTidalTerrain Register(GameObject host)
        {
            var terrain = AddTo(host);
            GameServices.TidalTerrain = terrain;
            return terrain;
        }

        /// <summary>Wire <paramref name="sea"/> to draw the G3 map <paramref name="terrain"/> decodes, as the builder
        /// wires the scene's Sea: the visible depth is the sailed depth.</summary>
        public static void DrawUnder(GameObject sea, PaintedTidalTerrain terrain)
        {
            var map = terrain.Map;
            sea.GetComponent<WaterSurface>().ConfigurePaintedHeightMap(map.HeightTexture, map.WorldCenter, map.WorldSize,
                                                                       map.MinElevation, map.MaxElevation);
        }

        /// <summary>The shore gradient the builder still measures off the analytic premise for the scene's sea.</summary>
        public static float PremiseShoreGradient()
        {
            var go = new GameObject("NMC analytic premise (test)") { hideFlags = HideFlags.HideAndDontSave };
            go.SetActive(false);
            try
            {
                var premise = go.AddComponent<MainlandTidalTerrain>();
                NineMileCreekBuilder.ConfigureNineMileCreekTerrain(premise);
                return premise.MaxShoreGradient();
            }
            finally { Object.DestroyImmediate(go); }
        }

        // ---- the scene, as text ---------------------------------------------------------------------------------

        /// <summary>Unity's YAML class ids for the documents the live tests read.</summary>
        public const int TransformClass = 4, RectTransformClass = 224, PolygonCollider2DClass = 60, EdgeCollider2DClass = 68,
                         SpriteRendererClass = 212, PrefabInstanceClass = 1001, TilemapClass = 1839735485;

        /// <summary><b>The activated scene</b>: NineMileCreek.unity as N1 writes it, its documents parsed once per
        /// domain. Text only, so no import and no engine: it holds what ships.</summary>
        public static SceneYaml Scene => _scene ??= SceneYaml.Parse(File.ReadAllText(NineMileCreekLayerRefresh.ScenePath));

        /// <summary>The one GameObject at <paramref name="path"/>: a root, then children by name ("Root/Child").</summary>
        public static Doc At(string path)
        {
            string[] names = path.Split('/');
            Doc go = Scene.RootNamed(names[0]);
            for (int i = 1; i < names.Length; i++) go = Scene.ChildNamed(Scene.TransformOf(go), names[i]);
            return go;
        }

        /// <summary>The GameObjects directly under <paramref name="go"/>, in child order (prefab instances skipped).</summary>
        public static List<Doc> Children(Doc go) =>
            Scene.TransformOf(go).FieldRefs("m_Children").Select(id => Scene.Require(id, "a child transform"))
                 .Where(tr => !tr.Stripped).Select(tr => Scene.Require(tr.FieldRef("m_GameObject"), "a child GameObject"))
                 .ToList();

        /// <summary>The one <paramref name="script"/> (Namespace.Class) on <paramref name="go"/>.</summary>
        public static Doc Script(Doc go, string script) => Scene.ComponentsOf(go).Single(d => d.IsScript(script));

        /// <summary>The components of Unity class <paramref name="classId"/> on <paramref name="go"/>, in order.</summary>
        public static List<Doc> Components(Doc go, int classId) =>
            Scene.ComponentsOf(go).Where(d => d.ClassId == classId).ToList();

        public static string Name(Doc go) => go.Field("m_Name");
        public static bool Active(Doc go) => go.Field("m_IsActive") == "1";
        public static bool Enabled(Doc component) => component.Field("m_Enabled") == "1";
        public static float Number(Doc doc, string key) => ParseFloat(doc.Field(key));

        static readonly Regex Vec2Rx = new Regex(@"^\{x: ([^,]+), y: ([^,}]+)\}$", RegexOptions.CultureInvariant);
        static readonly Regex QuatRx = new Regex(@"^\{x: ([^,]+), y: ([^,]+), z: ([^,]+), w: ([^,}]+)\}$",
                                                 RegexOptions.CultureInvariant);

        /// <summary>A serialized <c>{x: …, y: …}</c>.</summary>
        public static Vector2 Vector(string yaml)
        {
            Match m = Vec2Rx.Match(yaml.Trim());
            if (!m.Success) throw new FormatException($"not a 2D vector: '{yaml}'.");
            return new Vector2(ParseFloat(m.Groups[1].Value), ParseFloat(m.Groups[2].Value));
        }

        public static Vector2 Vector(Doc doc, string key) => Vector(doc.Field(key));

        /// <summary>A serialized Rect block (<c>serializedVersion</c>, x, y, width, height).</summary>
        public static Rect RectOf(Doc doc, string key)
        {
            string[] lines = doc.Lines;
            int at = Array.IndexOf(lines, "  " + key + ":");
            if (at < 0 || at + 5 >= lines.Length) throw new FormatException($"&{doc.FileId} has no {key}.");
            float Value(int i) => ParseFloat(lines[at + i].Substring(lines[at + i].IndexOf(':') + 1));
            return new Rect(Value(2), Value(3), Value(4), Value(5));
        }

        /// <summary>Where <paramref name="go"/> stands: its local position composed up its parents, every one of which
        /// must stand unrotated and unscaled, as the region's roots do.</summary>
        public static Vector2 WorldPosition(Doc go)
        {
            Doc tr = Scene.TransformOf(go);
            Vector3 p = ParseVec3(tr.Field("m_LocalPosition"));
            for (long father = tr.FieldRef("m_Father"); father != 0;)
            {
                Doc f = Scene.Require(father, "a parent transform");
                if (f.Field("m_LocalRotation") != "{x: 0, y: 0, z: 0, w: 1}" || f.Field("m_LocalScale") != "{x: 1, y: 1, z: 1}")
                    throw new InvalidOperationException($"&{f.FileId} turns or scales its children; compose it first.");
                p += ParseVec3(f.Field("m_LocalPosition"));
                father = f.FieldRef("m_Father");
            }
            return p;
        }

        /// <summary>The local rotation of <paramref name="go"/>'s transform.</summary>
        public static Quaternion Rotation(Doc go) => Turn(Scene.TransformOf(go));

        static Quaternion Turn(Doc transform)
        {
            Match m = QuatRx.Match(transform.Field("m_LocalRotation"));
            if (!m.Success) throw new FormatException($"&{transform.FileId} has no readable rotation.");
            return new Quaternion(ParseFloat(m.Groups[1].Value), ParseFloat(m.Groups[2].Value),
                                  ParseFloat(m.Groups[3].Value), ParseFloat(m.Groups[4].Value));
        }

        /// <summary>A point in <paramref name="go"/>'s own space, carried out to the world through every transform
        /// above it (scale, then turn, then place), as a collider's outline is.</summary>
        public static Vector2 ToWorld(Doc go, Vector2 local) => Up(Scene.TransformOf(go), local);

        /// <summary>A point in the space of the scene's <paramref name="transform"/>, carried out to the world.</summary>
        static Vector3 Up(Doc transform, Vector3 p)
        {
            for (Doc tr = transform; tr != null;)
            {
                p = Turn(tr) * Vector3.Scale(p, ParseVec3(tr.Field("m_LocalScale"))) + ParseVec3(tr.Field("m_LocalPosition"));
                long father = tr.FieldRef("m_Father");
                tr = father == 0 ? null : Scene.Require(father, "a parent transform");
            }
            return p;
        }

        /// <summary>Whether <paramref name="go"/> is active in the hierarchy: it and every GameObject above it.</summary>
        public static bool Live(Doc go)
        {
            for (Doc tr = Scene.TransformOf(go); ;)
            {
                if (!Active(Scene.Require(tr.FieldRef("m_GameObject"), "a GameObject"))) return false;
                long father = tr.FieldRef("m_Father");
                if (father == 0) return true;
                tr = Scene.Require(father, "a parent transform");
            }
        }

        /// <summary>The GUID in a serialized object reference (<c>{fileID: …, guid: …, type: …}</c>).</summary>
        public static string RefGuid(string yaml)
        {
            Match m = Regex.Match(yaml ?? "", @"guid: ([0-9a-f]{32})");
            if (!m.Success) throw new FormatException($"not an asset reference: '{yaml}'.");
            return m.Groups[1].Value;
        }

        /// <summary>A PolygonCollider2D's paths (<c>m_Points.m_Paths</c>), in its GameObject's space.</summary>
        public static List<Vector2[]> Paths(Doc polygon)
        {
            var paths = new List<Vector2[]>();
            List<Vector2> cur = null;
            string[] lines = polygon.Lines;
            int at = Array.IndexOf(lines, "    m_Paths:");
            for (int i = at + 1; at >= 0 && i < lines.Length; i++)
            {
                string l = lines[i];
                if (l.StartsWith("    - - ", StringComparison.Ordinal))
                {
                    if (cur != null) paths.Add(cur.ToArray());
                    cur = new List<Vector2> { Vector(l.Substring(8)) };
                }
                else if (l.StartsWith("      - ", StringComparison.Ordinal) && cur != null) cur.Add(Vector(l.Substring(8)));
                else break;
            }
            if (cur != null) paths.Add(cur.ToArray());
            return paths;
        }

        /// <summary>An EdgeCollider2D's points (<c>m_Points</c>), in its GameObject's space.</summary>
        public static Vector2[] EdgePoints(Doc edge)
        {
            var points = new List<Vector2>();
            string[] lines = edge.Lines;
            int at = Array.IndexOf(lines, "  m_Points:");
            for (int i = at + 1; at >= 0 && i < lines.Length && lines[i].StartsWith("  - ", StringComparison.Ordinal); i++)
                points.Add(Vector(lines[i].Substring(4)));
            return points.ToArray();
        }

        /// <summary>The cells a Tilemap document holds (<c>m_Tiles</c>).</summary>
        public static HashSet<Vector2Int> Cells(Doc tilemap)
        {
            var cells = new HashSet<Vector2Int>();
            foreach (string l in tilemap.Lines)
            {
                if (!l.StartsWith("  - first: {x: ", StringComparison.Ordinal)) continue;
                Vector3 c = ParseVec3(l.Substring("  - first: ".Length));
                cells.Add(new Vector2Int((int)c.x, (int)c.y));
            }
            return cells;
        }

        /// <summary>The tile asset GUID a Tilemap document paints at each of its cells (<c>m_Tiles</c>, through
        /// <c>m_TileAssetArray</c>).</summary>
        public static Dictionary<Vector2Int, string> Tiles(Doc tilemap)
        {
            var index = new Dictionary<Vector2Int, int>();
            var assets = new List<string>();
            Vector2Int? cell = null;
            bool inAssets = false;
            foreach (string l in tilemap.Lines)
            {
                if (l.StartsWith("  - first: {x: ", StringComparison.Ordinal))
                {
                    Vector3 c = ParseVec3(l.Substring("  - first: ".Length));
                    cell = new Vector2Int((int)c.x, (int)c.y);
                }
                else if (cell != null && l.StartsWith("      m_TileIndex: ", StringComparison.Ordinal))
                {
                    index[cell.Value] = ParseInt(l.Substring("      m_TileIndex: ".Length));
                    cell = null;
                }
                else if (l == "  m_TileAssetArray:") inAssets = true;
                else if (inAssets && l.StartsWith("    m_Data: ", StringComparison.Ordinal))
                    assets.Add(l.Contains("guid: ") ? RefGuid(l) : null);
                else if (inAssets && !l.StartsWith("  - ", StringComparison.Ordinal) && !l.StartsWith("    ", StringComparison.Ordinal))
                    inAssets = false;
            }
            return index.ToDictionary(kv => kv.Key, kv => assets[kv.Value]);
        }

        /// <summary>A prefab instance's overrides as (target fileID, property path) → value or reference.</summary>
        public static Dictionary<(long, string), string> Overrides(Doc instance)
        {
            var map = new Dictionary<(long, string), string>();
            string[] lines = instance.Lines;
            for (int i = 0; i + 3 < lines.Length; i++)
            {
                if (!lines[i].StartsWith("    - target: ", StringComparison.Ordinal)) continue;
                long target = ParseRef(lines[i]);
                string path = lines[i + 1].Substring(lines[i + 1].IndexOf(':') + 2);
                string value = lines[i + 2].Length > "      value:".Length ? lines[i + 2].Substring("      value: ".Length) : "";
                string reference = lines[i + 3].Substring(lines[i + 3].IndexOf(':') + 2);
                map[(target, path)] = reference != "{fileID: 0}" ? reference : value;
            }
            return map;
        }

        /// <summary>The GUID a Unity <c>.meta</c> file gives the asset at <paramref name="assetPath"/>.</summary>
        public static string GuidOf(string assetPath)
        {
            Match m = Regex.Match(File.ReadAllText(assetPath + ".meta"), @"(?m)^guid: ([0-9a-f]{32})\r?$");
            if (!m.Success) throw new FileNotFoundException("no guid in " + assetPath + ".meta");
            return m.Groups[1].Value;
        }

        /// <summary>The highest G3 sample over a rectangle at <paramref name="step"/> metres (both edges included).</summary>
        public static float Highest(Rect r, float step)
        {
            float worst = float.MinValue;
            int nx = Mathf.CeilToInt(r.width / step), ny = Mathf.CeilToInt(r.height / step);
            for (int i = 0; i <= nx; i++)
                for (int j = 0; j <= ny; j++)
                    worst = Mathf.Max(worst, Plan.ElevationAt(new Vector2(Mathf.Min(r.xMin + i * step, r.xMax),
                                                                          Mathf.Min(r.yMin + j * step, r.yMax))));
            return worst;
        }

        // ---- the solids, as world outlines ----------------------------------------------------------------------

        static readonly int[] ColliderClasses = { 58, 60, 61, 66, 68, 70 };

        /// <summary>Every live, enabled, non-trigger collider the scene loads, as world segments: polygons by their
        /// closed paths, edges by their points, boxes by their four corners, circles as a point with a radius. That is
        /// the scene's own colliders and the ones its prefab instances bring (the Route 91 station's, the wharf
        /// pumps'), each placed through its instance's overrides. A collider this cannot draw or place throws rather
        /// than being skipped, so the test that asked fails.</summary>
        public static List<(Vector2 a, Vector2 b, float radius, string who)> SolidOutlines(
            out List<(Vector2[] path, string who)> polygons)
        {
            var outlines = new List<(Vector2, Vector2, float, string)>();
            polygons = new List<(Vector2[], string)>();
            foreach (Doc d in Scene.Docs)
            {
                if (d.ClassId == PrefabInstanceClass && !d.Stripped)
                {
                    InstanceSolids(d, outlines, polygons);
                    continue;
                }
                if (d.Stripped || !ColliderClasses.Contains(d.ClassId)) continue;
                Doc go = Scene.Require(d.FieldRef("m_GameObject"), "a GameObject");
                if (go.Stripped)
                    throw new NotSupportedException($"&{d.FileId} is a collider added to a prefab instance, which a live test cannot place.");
                if (!Enabled(d) || !Live(go) || d.Field("m_IsTrigger") == "1") continue;
                Draw(d, $"{Name(go)} &{d.FileId}", k => Number(d, k), k => Vector(d, k), v => ToWorld(go, v), outlines, polygons);
            }
            return outlines;
        }

        /// <summary>One collider drawn into the world: <paramref name="number"/> and <paramref name="vector"/> read its
        /// fields as they load, <paramref name="toWorld"/> carries a point in its GameObject's space out.</summary>
        static void Draw(Doc d, string who, Func<string, float> number, Func<string, Vector2> vector, Func<Vector2, Vector2> toWorld,
                         List<(Vector2, Vector2, float, string)> outlines, List<(Vector2[], string)> polygons)
        {
            Vector2 offset = vector("m_Offset");
            switch (d.ClassId)
            {
                case PolygonCollider2DClass:
                    foreach (Vector2[] path in Paths(d))
                    {
                        Vector2[] w = path.Select(v => toWorld(v + offset)).ToArray();
                        polygons.Add((w, who));
                        for (int i = 0; i < w.Length; i++) outlines.Add((w[i], w[(i + 1) % w.Length], 0f, who));
                    }
                    break;
                case EdgeCollider2DClass:
                {
                    Vector2[] w = EdgePoints(d).Select(v => toWorld(v + offset)).ToArray();
                    float r = number("m_EdgeRadius");
                    for (int i = 0; i + 1 < w.Length; i++) outlines.Add((w[i], w[i + 1], r, who));
                    break;
                }
                case 61:
                {
                    Vector2 h = vector("m_Size") * 0.5f;
                    Vector2[] w = new[] { new Vector2(-h.x, -h.y), new Vector2(h.x, -h.y), new Vector2(h.x, h.y), new Vector2(-h.x, h.y) }
                                  .Select(v => toWorld(v + offset)).ToArray();
                    polygons.Add((w, who));
                    float r = number("m_EdgeRadius");
                    for (int i = 0; i < 4; i++) outlines.Add((w[i], w[(i + 1) % 4], r, who));
                    break;
                }
                case 58:
                {
                    // A circle grows with the larger of its two scales, as Unity sizes it.
                    Vector2 c = toWorld(offset);
                    float scale = Mathf.Max(Vector2.Distance(toWorld(offset + Vector2.right), c),
                                            Vector2.Distance(toWorld(offset + Vector2.up), c));
                    outlines.Add((c, c, number("m_Radius") * scale, who));
                    break;
                }
                default:
                    throw new NotSupportedException($"{who} is a collider of class {d.ClassId}, which a live test cannot draw.");
            }
        }

        /// <summary>The colliders a prefab instance brings, as the scene loads them: the prefab's own documents, with
        /// the instance's overrides on each target, its removed components and GameObjects left out, and its root
        /// hung from the instance's parent. Nothing is drawn under a parent that is not live.</summary>
        static void InstanceSolids(Doc instance, List<(Vector2, Vector2, float, string)> outlines, List<(Vector2[], string)> polygons)
        {
            Dictionary<(long, string), string> overrides = Overrides(instance);
            string Over(long target, string path) => overrides.TryGetValue((target, path), out string v) ? v : null;
            long parent = ParseRef(InstanceLine(instance, "m_TransformParent"));
            Doc parentTr = parent == 0 ? null : Scene.Require(parent, "a prefab instance's parent transform");
            if (parentTr != null && !Live(Scene.Require(parentTr.FieldRef("m_GameObject"), "a GameObject"))) return;

            SceneYaml prefab = Prefab(RefGuid(instance.Field("m_SourcePrefab")));
            if (prefab.Docs.Any(x => x.ClassId == PrefabInstanceClass))
                throw new NotSupportedException($"&{instance.FileId}'s prefab nests another prefab, which a live test cannot place.");
            var removed = new HashSet<long>(Listed(instance, "m_RemovedComponents").Concat(Listed(instance, "m_RemovedGameObjects")));
            Doc root = prefab.Docs.Single(x => (x.ClassId == TransformClass || x.ClassId == RectTransformClass) && !x.Stripped &&
                                               x.FieldRef("m_Father") == 0);
            long rootGo = root.FieldRef("m_GameObject");
            string name = Over(rootGo, "m_Name") ?? prefab.Require(rootGo, "a prefab's root").Field("m_Name");

            foreach (Doc c in prefab.Docs)
            {
                if (c.Stripped || !ColliderClasses.Contains(c.ClassId) || removed.Contains(c.FileId)) continue;
                Doc go = prefab.Require(c.FieldRef("m_GameObject"), "a prefab GameObject");
                bool live = true;
                for (Doc tr = prefab.TransformOf(go); tr != null && live;)
                {
                    long g = tr.FieldRef("m_GameObject");
                    live = !removed.Contains(g) && (Over(g, "m_IsActive") ?? prefab.Require(g, "a prefab GameObject").Field("m_IsActive")) == "1";
                    long father = tr.FieldRef("m_Father");
                    tr = father == 0 ? null : prefab.Require(father, "a prefab transform");
                }
                if (!live || (Over(c.FileId, "m_Enabled") ?? c.Field("m_Enabled")) != "1" ||
                    (Over(c.FileId, "m_IsTrigger") ?? c.Field("m_IsTrigger")) == "1") continue;
                string who = $"{name}/{go.Field("m_Name")} &{instance.FileId}:{c.FileId}";
                if (overrides.Keys.Any(k => k.Item1 == c.FileId && k.Item2.StartsWith("m_Points", StringComparison.Ordinal)))
                    throw new NotSupportedException($"{who}'s instance overrides its outline, which a live test cannot draw.");
                float NumberOf(string key) => Over(c.FileId, key) is string s ? ParseFloat(s) : Number(c, key);
                Vector2 VectorOf(string key)
                {
                    Vector2 v = Vector(c, key);
                    return new Vector2(Over(c.FileId, key + ".x") is string x ? ParseFloat(x) : v.x,
                                       Over(c.FileId, key + ".y") is string y ? ParseFloat(y) : v.y);
                }
                Vector2 ToWorldThrough(Vector2 local)
                {
                    Vector3 p = local;
                    for (Doc tr = prefab.TransformOf(go); tr != null;)
                    {
                        long t = tr.FileId;
                        Vector3 Field3(string key)
                        {
                            Vector3 v = ParseVec3(tr.Field(key));
                            return new Vector3(Over(t, key + ".x") is string x ? ParseFloat(x) : v.x,
                                               Over(t, key + ".y") is string y ? ParseFloat(y) : v.y,
                                               Over(t, key + ".z") is string z ? ParseFloat(z) : v.z);
                        }
                        Quaternion q = Turn(tr);
                        q = new Quaternion(Over(t, "m_LocalRotation.x") is string qx ? ParseFloat(qx) : q.x,
                                           Over(t, "m_LocalRotation.y") is string qy ? ParseFloat(qy) : q.y,
                                           Over(t, "m_LocalRotation.z") is string qz ? ParseFloat(qz) : q.z,
                                           Over(t, "m_LocalRotation.w") is string qw ? ParseFloat(qw) : q.w);
                        p = q * Vector3.Scale(p, Field3("m_LocalScale")) + Field3("m_LocalPosition");
                        long father = tr.FieldRef("m_Father");
                        tr = father == 0 ? null : prefab.Require(father, "a prefab transform");
                    }
                    return parentTr == null ? (Vector2)p : (Vector2)Up(parentTr, p);
                }
                Draw(c, who, NumberOf, VectorOf, ToWorldThrough, outlines, polygons);
            }
        }

        /// <summary>A line of a prefab instance's <c>m_Modification</c> block (<c>    key: value</c>).</summary>
        static string InstanceLine(Doc instance, string key)
        {
            string head = "    " + key + ":";
            string line = instance.Lines.SingleOrDefault(l => l.StartsWith(head, StringComparison.Ordinal))
                          ?? throw new FormatException($"&{instance.FileId} has no {key}.");
            return line.Substring(head.Length).Trim();
        }

        /// <summary>The fileIDs a prefab instance lists under <paramref name="key"/> (its removed components or
        /// GameObjects).</summary>
        static IEnumerable<long> Listed(Doc instance, string key)
        {
            string[] lines = instance.Lines;
            int at = Array.IndexOf(lines, "    " + key + ":");
            if (at < 0) yield break;
            for (int i = at + 1; i < lines.Length && lines[i].StartsWith("    - ", StringComparison.Ordinal); i++)
                yield return ParseRef(lines[i].Substring("    - ".Length));
        }

        static Dictionary<string, string> _prefabPaths;
        static readonly Dictionary<string, SceneYaml> Prefabs = new Dictionary<string, SceneYaml>();

        /// <summary>The prefab whose <c>.meta</c> gives it <paramref name="guid"/>, parsed once per domain.</summary>
        static SceneYaml Prefab(string guid)
        {
            if (Prefabs.TryGetValue(guid, out SceneYaml prefab)) return prefab;
            _prefabPaths ??= Directory.EnumerateFiles("Assets", "*.prefab.meta", SearchOption.AllDirectories)
                .Select(meta => (meta, m: Regex.Match(File.ReadAllText(meta), @"(?m)^guid: ([0-9a-f]{32})\r?$")))
                .Where(x => x.m.Success)
                .ToDictionary(x => x.m.Groups[1].Value, x => x.meta.Substring(0, x.meta.Length - ".meta".Length));
            if (!_prefabPaths.TryGetValue(guid, out string path)) throw new FileNotFoundException("no prefab has the guid " + guid);
            return Prefabs[guid] = SceneYaml.Parse(File.ReadAllText(path).Replace("\r\n", "\n"));
        }

        /// <summary>The solid <paramref name="p"/> stands in, or null: inside or on a closed outline, or within a
        /// rounded edge's or a circle's radius.</summary>
        public static string SolidAt(Vector2 p, List<(Vector2 a, Vector2 b, float radius, string who)> outlines,
                                     List<(Vector2[] path, string who)> polygons)
        {
            foreach (var polygon in polygons)
                if (Inside(polygon.path, p)) return polygon.who;
            foreach (var o in outlines)
                if (o.radius > 0f && DistanceToSegment(p, o.a, o.b) <= o.radius) return o.who;
            return null;
        }

        /// <summary>Inside or on a closed path (its edges count, to the thousandth of a millimetre).</summary>
        public static bool Inside(Vector2[] path, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = path.Length - 1; i < path.Length; j = i++)
            {
                if (DistanceToSegment(p, path[j], path[i]) <= 1e-5f) return true;
                if ((path[i].y > p.y) != (path[j].y > p.y) &&
                    p.x < (path[j].x - path[i].x) * (p.y - path[i].y) / (path[j].y - path[i].y) + path[i].x)
                    inside = !inside;
            }
            return inside;
        }

        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude <= 0f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>A number as a message shows it (three places, invariant).</summary>
        public static string Shown(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        // ---- the hulls, as text ---------------------------------------------------------------------------------

        /// <summary>The asset under Data/Boats whose <c>.meta</c> gives it <paramref name="guid"/>.</summary>
        public static string BoatData(string guid)
        {
            foreach (string meta in Directory.EnumerateFiles("Assets/_Project/Data/Boats", "*.meta", SearchOption.AllDirectories))
                if (File.ReadAllText(meta).Contains("guid: " + guid)) return meta.Substring(0, meta.Length - ".meta".Length);
            throw new FileNotFoundException("no boat data has guid " + guid);
        }

        /// <summary>The GUID an asset's top-level <paramref name="field"/> references.</summary>
        public static string GuidField(string text, string field)
        {
            Match m = Regex.Match(text, @"(?m)^  " + field + @": \{fileID: [-\d]+, guid: (\w+)");
            if (!m.Success) throw new FormatException("no reference " + field);
            return m.Groups[1].Value;
        }

        /// <summary>An asset's top-level number <paramref name="field"/>.</summary>
        public static float AssetNumber(string text, string field)
        {
            Match m = Regex.Match(text, @"(?m)^  " + field + @": (.*?)\r?$");
            if (!m.Success) throw new FormatException("no number " + field);
            return ParseFloat(m.Groups[1].Value);
        }

        /// <summary>A hull's length, draught and half beam, read from its asset as PR 1 reads the Cape Islander's:
        /// the half beam is its hull mesh's watertight one, or the presence fraction of its length if unauthored.</summary>
        public static (float Length, float Draught, float HalfBeam) Hull(string hullPath)
        {
            string hull = File.ReadAllText(hullPath);
            string visual = File.ReadAllText(BoatData(GuidField(hull, "Visual")));
            float length = AssetNumber(hull, "LengthMeters"),
                  authored = AssetNumber(File.ReadAllText(BoatData(GuidField(visual, "HullMesh"))), "WatertightHalfBeamMeters");
            return (length, AssetNumber(hull, "DraughtMeters"),
                    authored > 0f ? authored : length * HiddenHarbours.Boats.HullPresence.BeamFraction / 2f);
        }
    }
}
