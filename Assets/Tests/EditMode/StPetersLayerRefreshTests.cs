using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>EACH REFRESH IS THE BUILDER'S OWN ANSWER</b> (terrain pass 9, PR 4b). Subjects:
    /// <see cref="StPetersBuilder.ScatterClamHoles"/> (each hole made by the builder's own
    /// <c>MakeClamHole</c>), <see cref="StPetersShorePainter"/> (its own <c>PlaceRocks</c>) and
    /// <see cref="StPetersNavMarks"/> (its own <c>Place</c>).
    ///
    /// <para>Over the whole island, on today's terrain: each step of <see cref="StPetersLayerRefresh"/>
    /// is applied to the committed scene's TEXT in memory, and the root it leaves is compared, object by
    /// object and in order, with what the builder's own function lays on the same terrain in a scratch
    /// hierarchy. Then again from a STALE root — the committed one with an object missing, one nudged
    /// and one the builder never laid — so the step's adding and deleting answer to the builder too,
    /// not only its edits. The scene file is read, never written and never opened.</para>
    ///
    /// <para>The YAML is read here with the test's own reader (<see cref="LayerRefreshSceneText"/>), not
    /// the step's, so a fault in the step's model of the file cannot vouch for itself.</para>
    /// </summary>
    public class StPetersLayerRefreshTests
    {
        internal const string ScenePath = "Assets/_Project/Scenes/StPeters.unity";

        GameObject _terrainGo;
        TidalTerrain _terrain;
        string _sceneText;
        readonly List<GameObject> _made = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _terrainGo = new GameObject("TidalTerrain_LayerRefreshTest");
            _terrain = _terrainGo.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(_terrain);   // the same zones the scene is built with
            _sceneText = File.ReadAllText(ScenePath);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _made)
                if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
            if (_terrainGo != null) Object.DestroyImmediate(_terrainGo);
        }

        GameObject Make(string name)
        {
            var go = new GameObject(name);
            _made.Add(go);
            return go;
        }

        // =============================================================================================
        //  ClamHoles — ScatterClamHoles, each hole made by the builder's own MakeClamHole
        // =============================================================================================

        [Test]
        public void ClamHoles_EqualScatterClamHolesThroughMakeClamHole_OverTheWholeIsland()
        {
            List<Vector2> holes = StPetersBuilder.ScatterClamHoles(_terrain);
            Assert.That(holes.Count, Is.GreaterThan(0), "the scatter laid no holes, so the comparison would be empty");

            MethodInfo makeHole = typeof(StPetersBuilder).GetMethod("MakeClamHole", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(makeHole, "StPetersBuilder.MakeClamHole is the builder's own hole, and this test calls it");
            Transform parent = Make("ClamHoles_Builder").transform;
            foreach (Vector2 p in holes)
                makeHole.Invoke(null, new object[] { parent, p, null, "fish.soft_shell_clam", null, null, null, null });

            var built = new List<LayerRefreshChecks.BuiltHole>();
            foreach (Transform hole in parent)
            {
                var dig = hole.GetComponent<ClamDig>();
                Assert.NotNull(dig, $"the builder's hole at {hole.localPosition} carries no ClamDig");
                built.Add(new LayerRefreshChecks.BuiltHole
                {
                    Name = hole.name,
                    Position = hole.localPosition,
                    Order = hole.GetComponent<SpriteRenderer>().sortingOrder,
                    Id = new SerializedObject(dig).FindProperty("_id").stringValue,
                });
            }

            LayerRefreshChecks.ClamHolesRefresh(_sceneText, _terrain, built);
        }

        // =============================================================================================
        //  Shoreline — the shore painter's own PlaceRocks, into a scratch grid
        // =============================================================================================

        [Test]
        public void Shoreline_EqualsStPetersShorePainterPlaceRocks_OverTheWholeIsland()
        {
            Transform parent = Make("Shoreline_Builder").transform;
            GameObject grid = Make("ShoreContact_Grid");
            grid.AddComponent<Grid>();
            var contactGo = new GameObject(StPetersShorePainter.ContactLayerName);
            contactGo.transform.SetParent(grid.transform, false);
            var contactMap = contactGo.AddComponent<Tilemap>();

            // Paint's own cache, in the style Paint resolves a null style to.
            Type cacheType = typeof(StPetersShorePainter).GetNestedType("TileCache", BindingFlags.NonPublic);
            Assert.NotNull(cacheType, "StPetersShorePainter.TileCache is the painter's own tile source");
            object cache = Activator.CreateInstance(cacheType, new object[] { ShoreIso2TileLibrary.DefaultStyle });
            MethodInfo placeRocks = typeof(StPetersShorePainter).GetMethod("PlaceRocks", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(placeRocks, "StPetersShorePainter.PlaceRocks is the painter's own placement, and this test calls it");
            int placed = (int)placeRocks.Invoke(null, new object[] { parent, _terrain, contactMap, cache });
            Assert.That(placed, Is.GreaterThan(0), "the painter placed no rocks, so the comparison would be empty");

            Transform rocks = parent.Find(StPetersShorePainter.RocksRootName);
            Assert.NotNull(rocks, "the painter's rocks root");
            var built = new List<LayerRefreshChecks.BuiltRock>();
            foreach (Transform rock in rocks)
            {
                var sr = rock.GetComponent<SpriteRenderer>();
                Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sr.sprite, out string guid, out long fileId),
                              $"{rock.name}'s sprite is not an asset");
                built.Add(new LayerRefreshChecks.BuiltRock
                {
                    Name = rock.name, Position = rock.localPosition, SpriteGuid = guid, SpriteFileId = fileId,
                    Size = sr.size, Order = sr.sortingOrder,
                });
            }

            var cells = new List<Vector3Int>();
            foreach (Vector3Int c in contactMap.cellBounds.allPositionsWithin)
                if (contactMap.HasTile(c)) cells.Add(c);
            Assert.That(cells.Count, Is.GreaterThan(0), "the painter laid no contact tiles");
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(contactMap.GetTile(cells[0]), out string tileGuid, out long _),
                          "the contact tile is not an asset");
            var contacts = new LayerRefreshChecks.BuiltContacts
            {
                Cells = cells, TileGuid = tileGuid, Origin = contactMap.origin, Size = contactMap.size,
            };

            LayerRefreshChecks.ShorelineRefresh(_sceneText, _terrain, new StPetersLayerRefresh.EditorShoreRockSprites(), built, contacts);
        }

        // =============================================================================================
        //  StPetersNavMarks — the marks' records, against the builder's own Place
        // =============================================================================================

        [Test]
        public void NavMarks_EqualStPetersNavMarksPlace_OverTheWholeIsland()
        {
            var before = new HashSet<GameObject>(SceneManager.GetActiveScene().GetRootGameObjects());
            int placed = StPetersNavMarks.Place(_terrain);
            GameObject root = SceneManager.GetActiveScene().GetRootGameObjects()
                .Single(g => !before.Contains(g) && g.name == StPetersNavMarks.RootName);
            _made.Add(root);
            Assert.That(placed, Is.GreaterThan(0), "the builder placed no marks, so the comparison would be empty");

            var built = new List<LayerRefreshChecks.BuiltMark>();
            foreach (Transform mark in root.transform)
            {
                var so = new SerializedObject(mark.GetComponent<NavBuoyVisual>());
                Object def = so.FindProperty("_def").objectReferenceValue;
                Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(def, out string defGuid, out long defId),
                              $"{mark.name}'s def is not an asset");
                built.Add(new LayerRefreshChecks.BuiltMark
                {
                    Name = mark.name, Position = mark.localPosition, DefGuid = defGuid, DefFileId = defId,
                    SizeId = so.FindProperty("_sizeId").stringValue,
                    Facing = so.FindProperty("_facing").intValue,
                    MarkId = so.FindProperty("_markId").stringValue,
                    Phase = so.FindProperty("_phaseFraction").floatValue,
                });
            }

            LayerRefreshChecks.NavMarksRefresh(_sceneText, _terrain, new StPetersLayerRefresh.EditorNavMarkAssets(), LoadNavPrefab(), built);
        }

        /// <summary>The nav buoy prefab as Unity LOADS it: what an instance without an override reads.</summary>
        internal static LayerRefreshChecks.PrefabFacts LoadNavPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StPetersNavMarks.PrefabPath);
            Assert.NotNull(prefab, StPetersNavMarks.PrefabPath);
            var visual = prefab.GetComponent<NavBuoyVisual>();
            Assert.NotNull(visual, "the prefab's NavBuoyVisual");
            var so = new SerializedObject(visual);
            var facts = new LayerRefreshChecks.PrefabFacts
            {
                Guid = AssetDatabase.AssetPathToGUID(StPetersNavMarks.PrefabPath),
                Name = prefab.name,
                Position = prefab.transform.localPosition,
                SizeId = so.FindProperty("_sizeId").stringValue,
                Facing = so.FindProperty("_facing").intValue,
                MarkId = so.FindProperty("_markId").stringValue,
                Phase = so.FindProperty("_phaseFraction").floatValue,
            };
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab, out string _, out facts.GameObject));
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(prefab.transform, out string _, out facts.Transform));
            Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(visual, out string _, out facts.Visual));
            Object def = so.FindProperty("_def").objectReferenceValue;
            if (def != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(def, out string dg, out long di))
            {
                facts.DefGuid = dg;
                facts.DefFileId = di;
            }
            return facts;
        }
    }

    /// <summary>
    /// The comparisons, as plain functions of the patched text and the builder's objects written down
    /// as data — so the same checks can be run against a deliberately broken step outside Unity.
    /// </summary>
    internal static class LayerRefreshChecks
    {
        internal sealed class BuiltHole
        {
            public string Name, Id;
            public Vector3 Position;
            public int Order;
        }

        internal sealed class BuiltRock
        {
            public string Name, SpriteGuid;
            public long SpriteFileId;
            public Vector3 Position;
            public Vector2 Size;
            public int Order;
        }

        internal sealed class BuiltContacts
        {
            public List<Vector3Int> Cells;
            public string TileGuid;
            public Vector3Int Origin, Size;
        }

        internal sealed class BuiltMark
        {
            public string Name, DefGuid, SizeId, MarkId;
            public long DefFileId;
            public Vector3 Position;
            public int Facing;
            public float Phase;
        }

        internal sealed class PrefabFacts
        {
            public string Guid, Name, SizeId, MarkId, DefGuid;
            public long GameObject, Transform, Visual, DefFileId;
            public Vector3 Position;
            public int Facing;
            public float Phase;
        }

        const string FromCommitted = "the committed root", FromStale = "a stale root";

        static void SamePosition(Vector3 expected, Vector3 actual, string who)
        {
            // Exact: the step writes the builder's own floats as text that reads back to the same float.
            Assert.That(actual.x, Is.EqualTo(expected.x), who + " x");
            Assert.That(actual.y, Is.EqualTo(expected.y), who + " y");
            Assert.That(actual.z, Is.EqualTo(expected.z), who + " z");
        }

        // ---- the refresh, from the committed root and from a stale one ------------------------------------
        //
        // The stale root is the step's own patch from an input changed on purpose (the patch tests' input:
        // one object dropped, one nudged, one the builder never laid), so the refresh after it has to add,
        // move back and delete — and what it leaves is still compared with the builder's objects.

        internal static void ClamHolesRefresh(string sceneText, ITidalTerrain terrain, IList<BuiltHole> built)
        {
            var scene = StPetersLayerRefresh.SceneYaml.Parse(sceneText);
            ClamHolesAre(StPetersLayerRefresh.ClamHoles(scene, terrain).ApplyTo(sceneText), built, FromCommitted);

            List<Vector2> off = LayerRefreshPatchChecks.ChangedOnPurpose(LayerRefreshPatchChecks.CommittedHoles(sceneText));
            string stale = StPetersLayerRefresh.ClamHoles(scene, off).ApplyTo(sceneText);
            ClamHolesAre(StPetersLayerRefresh.ClamHoles(StPetersLayerRefresh.SceneYaml.Parse(stale), terrain).ApplyTo(stale), built, FromStale);
        }

        internal static void ShorelineRefresh(string sceneText, ITidalTerrain terrain, StPetersLayerRefresh.IShoreRockSprites sprites,
                                              IList<BuiltRock> built, BuiltContacts contacts)
        {
            var scene = StPetersLayerRefresh.SceneYaml.Parse(sceneText);
            ShorelineIs(StPetersLayerRefresh.Shoreline(scene, terrain, sprites, Array.Empty<ShoreRockDef>()).ApplyTo(sceneText),
                        built, contacts, FromCommitted);

            List<StPetersLayerRefresh.PlacedRock> off = LayerRefreshPatchChecks.ChangedOnPurpose(LayerRefreshPatchChecks.CommittedRocks(sceneText));
            string stale = StPetersLayerRefresh.Shoreline(scene, off, sprites, Array.Empty<ShoreRockDef>()).ApplyTo(sceneText);
            StaleContactsFollowTheirRocks(stale, off, built, contacts);
            ShorelineIs(StPetersLayerRefresh.Shoreline(StPetersLayerRefresh.SceneYaml.Parse(stale), terrain, sprites,
                                                       Array.Empty<ShoreRockDef>()).ApplyTo(stale),
                        built, contacts, FromStale);
        }

        /// <summary>
        /// The stale root's contact layer follows its own rocks, so the refresh after it has contact cells
        /// to put back (a step that never touched the layer would leave today's cells in both roots, and
        /// pass). The rule is the painter's, one tile at (⌊x⌋, ⌊y⌋ + 1) under each rock, restated here and
        /// held first to the painter's own cells for today.
        /// </summary>
        static void StaleContactsFollowTheirRocks(string stale, IList<StPetersLayerRefresh.PlacedRock> off,
                                                  IList<BuiltRock> built, BuiltContacts contacts)
        {
            CollectionAssert.AreEquivalent(contacts.Cells, ContactCells(built.Select(b => (Vector2)b.Position)),
                                           "the painter's contact rule, as restated here, does not give the painter's own cells");
            List<Vector3Int> want = ContactCells(off.Select(r => r.Position));
            Assert.IsFalse(new HashSet<Vector3Int>(want).SetEquals(contacts.Cells),
                           "the stale rocks sit on today's contact cells, so the refresh would have no cell to put back");
            var s = new LayerRefreshSceneText(stale);
            long map = s.ComponentOfClass(s.ChildNamed(s.RootGameObject("Shoreline"), "ShoreContact"), 1839735485);
            CollectionAssert.AreEquivalent(want, s.TileCells(map), $"ShoreContact, in {FromStale}: the cells under its own rocks");
        }

        static List<Vector3Int> ContactCells(IEnumerable<Vector2> rocks) =>
            rocks.Select(p => new Vector3Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y) + 1, 0)).Distinct().ToList();

        /// <summary>The stale root here keeps all of today's marks: a new mark is copied from the root's
        /// first and must wear its dressing, and no other mark of today's plan does, so one the refresh
        /// had to add back would be refused (by design). It has one moved and one stray.</summary>
        internal static void NavMarksRefresh(string sceneText, ITidalTerrain terrain, StPetersLayerRefresh.INavMarkAssets assets,
                                             PrefabFacts prefab, IList<BuiltMark> built)
        {
            var scene = StPetersLayerRefresh.SceneYaml.Parse(sceneText);
            NavMarksAre(StPetersLayerRefresh.NavMarks(scene, terrain, assets).ApplyTo(sceneText), prefab, built, FromCommitted);

            NavMarkPlanResult off = LayerRefreshPatchChecks.ChangedOnPurpose(StPetersNavMarks.Plan(terrain), sceneText, drop: false);
            string stale = StPetersLayerRefresh.NavMarks(scene, off, assets).ApplyTo(sceneText);
            NavMarksAre(StPetersLayerRefresh.NavMarks(StPetersLayerRefresh.SceneYaml.Parse(stale), terrain, assets).ApplyTo(stale),
                        prefab, built, FromStale);
        }

        // ---- the comparisons ------------------------------------------------------------------------------

        internal static void ClamHolesAre(string sceneText, IList<BuiltHole> built, string from)
        {
            var s = new LayerRefreshSceneText(sceneText);
            List<long> kids = s.ChildGameObjects(s.RootGameObject("ClamHoles"));
            Assert.AreEqual(built.Count, kids.Count, $"ClamHoles, from {from}: the number of holes");
            for (int i = 0; i < kids.Count; i++)
            {
                BuiltHole b = built[i];
                long go = kids[i];
                string who = $"ClamHoles child {i}, from {from} (the builder's hole at {b.Position.x}, {b.Position.y})";
                Assert.AreEqual(b.Name, s.Field(go, "m_Name"), who + ": name");
                SamePosition(b.Position, s.Vec3(s.TransformOf(go), "m_LocalPosition"), who);
                Assert.AreEqual(b.Order.ToString(CultureInfo.InvariantCulture),
                                s.Field(s.ComponentOfClass(go, 212), "m_SortingOrder"), who + ": sorting order");
                Assert.AreEqual(b.Id, s.Field(s.Script(go, "ClamDig"), "_id"), who + ": dig id");
            }
        }

        internal static void ShorelineIs(string sceneText, IList<BuiltRock> built, BuiltContacts contacts, string from)
        {
            var s = new LayerRefreshSceneText(sceneText);
            long shoreline = s.RootGameObject("Shoreline");
            List<long> kids = s.ChildGameObjects(s.ChildNamed(shoreline, "ShoreRocks"));
            Assert.AreEqual(built.Count, kids.Count, $"Shoreline, from {from}: the number of rocks");
            for (int i = 0; i < kids.Count; i++)
            {
                BuiltRock b = built[i];
                long go = kids[i];
                string who = $"ShoreRocks child {i}, from {from} (the painter's {b.Name} at {b.Position.x}, {b.Position.y})";
                Assert.AreEqual(b.Name, s.Field(go, "m_Name"), who + ": name");
                SamePosition(b.Position, s.Vec3(s.TransformOf(go), "m_LocalPosition"), who);
                long sr = s.ComponentOfClass(go, 212);
                Assert.AreEqual(b.SpriteGuid, s.Guid(sr, "m_Sprite"), who + ": sprite guid");
                Assert.AreEqual(b.SpriteFileId, s.Ref(sr, "m_Sprite"), who + ": sprite fileID");
                Vector2 size = s.Vec2(sr, "m_Size");
                Assert.That(size.x, Is.EqualTo(b.Size.x), who + ": size x");
                Assert.That(size.y, Is.EqualTo(b.Size.y), who + ": size y");
                Assert.AreEqual(b.Order.ToString(CultureInfo.InvariantCulture), s.Field(sr, "m_SortingOrder"), who + ": sorting order");
            }

            long map = s.ComponentOfClass(s.ChildNamed(shoreline, "ShoreContact"), 1839735485);
            List<Vector3Int> cells = s.TileCells(map);
            string contact = $"ShoreContact, from {from}";
            CollectionAssert.AreEquivalent(contacts.Cells, cells, contact + ": the contact cells");
            Assert.AreEqual(contacts.Cells.Count, cells.Count, contact + ": one tile per cell");
            StringAssert.Contains("guid: " + contacts.TileGuid + ",", s.Text(map), contact + ": the painter's contact tile");
            Assert.AreEqual(contacts.Origin, s.Vec3Int(map, "m_Origin"), contact + ": origin");
            Assert.AreEqual(contacts.Size, s.Vec3Int(map, "m_Size"), contact + ": size");
        }

        internal static void NavMarksAre(string sceneText, PrefabFacts prefab, IList<BuiltMark> built, string from)
        {
            var s = new LayerRefreshSceneText(sceneText);
            List<long> instances = s.ChildInstances(s.RootGameObject("StPetersNavMarks"));
            Assert.AreEqual(built.Count, instances.Count, $"StPetersNavMarks, from {from}: the number of marks");
            for (int i = 0; i < instances.Count; i++)
            {
                BuiltMark b = built[i];
                long pi = instances[i];
                string who = $"StPetersNavMarks child {i}, from {from} (the builder's {b.Name})";
                Assert.AreEqual(prefab.Guid, s.Guid(pi, "m_SourcePrefab"), who + ": source prefab");

                string Loaded(long target, string path, string prefabValue) => s.ModValue(pi, target, path) ?? prefabValue;
                float LoadedFloat(long target, string path, float prefabValue)
                {
                    string v = s.ModValue(pi, target, path);
                    return v == null ? prefabValue : float.Parse(v, NumberStyles.Float, CultureInfo.InvariantCulture);
                }

                Assert.AreEqual(b.Name, Loaded(prefab.GameObject, "m_Name", prefab.Name), who + ": name");
                SamePosition(b.Position, new Vector3(LoadedFloat(prefab.Transform, "m_LocalPosition.x", prefab.Position.x),
                                                     LoadedFloat(prefab.Transform, "m_LocalPosition.y", prefab.Position.y),
                                                     LoadedFloat(prefab.Transform, "m_LocalPosition.z", prefab.Position.z)), who);
                string defRef = s.ModObjectReference(pi, prefab.Visual, "_def");
                Assert.AreEqual(b.DefGuid, defRef != null ? LayerRefreshSceneText.GuidIn(defRef) : prefab.DefGuid, who + ": def guid");
                Assert.AreEqual(b.DefFileId, defRef != null ? LayerRefreshSceneText.FileIdIn(defRef) : prefab.DefFileId, who + ": def fileID");
                Assert.AreEqual(b.SizeId, Loaded(prefab.Visual, "_sizeId", prefab.SizeId), who + ": size rung");
                Assert.AreEqual(b.Facing.ToString(CultureInfo.InvariantCulture),
                                Loaded(prefab.Visual, "_facing", prefab.Facing.ToString(CultureInfo.InvariantCulture)), who + ": facing");
                Assert.AreEqual(b.MarkId, Loaded(prefab.Visual, "_markId", prefab.MarkId), who + ": chart id");
                Assert.That(LoadedFloat(prefab.Visual, "_phaseFraction", prefab.Phase), Is.EqualTo(b.Phase), who + ": light phase");
            }
        }
    }

    /// <summary>
    /// The test's own reader of a Unity scene's text: documents by fileID, a document's own fields, and
    /// the hierarchy. Deliberately not <c>StPetersLayerRefresh.SceneYaml</c>.
    /// </summary>
    internal sealed class LayerRefreshSceneText
    {
        static readonly Regex HeaderRx = new Regex(@"^--- !u!(\d+) &(-?\d+)( stripped)?$", RegexOptions.CultureInvariant);
        static readonly Regex FileIdRx = new Regex(@"fileID: (-?\d+)", RegexOptions.CultureInvariant);
        static readonly Regex GuidRx = new Regex(@"guid: ([0-9a-f]{32})", RegexOptions.CultureInvariant);
        static readonly Regex XyzRx = new Regex(@"^\{x: ([^,]+), y: ([^,}]+)(?:, z: ([^}]+))?\}$", RegexOptions.CultureInvariant);

        public readonly List<long> Order = new List<long>();
        readonly Dictionary<long, string[]> _lines = new Dictionary<long, string[]>();
        readonly Dictionary<long, int> _class = new Dictionary<long, int>();
        readonly HashSet<long> _stripped = new HashSet<long>();

        public LayerRefreshSceneText(string text)
        {
            List<string> cur = null;
            long id = 0;
            foreach (string line in text.Split('\n'))
            {
                Match m = HeaderRx.Match(line);
                if (m.Success)
                {
                    if (cur != null) Close(id, cur);
                    id = long.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                    Assert.IsFalse(_class.ContainsKey(id), $"two documents are &{id}");
                    _class[id] = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                    if (m.Groups[3].Success) _stripped.Add(id);
                    Order.Add(id);
                    cur = new List<string> { line };
                }
                else cur?.Add(line);
            }
            if (cur != null) Close(id, cur);
        }

        void Close(long id, List<string> lines)
        {
            while (lines.Count > 1 && lines[lines.Count - 1].Length == 0) lines.RemoveAt(lines.Count - 1);
            _lines[id] = lines.ToArray();
        }

        public bool Has(long id) => _lines.ContainsKey(id);
        public int ClassOf(long id) => _class[id];
        public bool IsStripped(long id) => _stripped.Contains(id);
        public string Text(long id) => string.Join("\n", _lines[id]);

        public string Field(long id, string key)
        {
            Assert.IsTrue(_lines.ContainsKey(id), $"no document &{id}");
            string head = "  " + key + ":";
            string[] lines = _lines[id];
            for (int i = 1; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith(head, StringComparison.Ordinal)) continue;
                if (lines[i].Length == head.Length) return "";
                if (lines[i][head.Length] == ' ') return lines[i].Substring(head.Length + 1);
            }
            return null;
        }

        public static long FileIdIn(string value)
        {
            Match m = value == null ? Match.Empty : FileIdRx.Match(value);
            return m.Success ? long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        }

        public static string GuidIn(string value)
        {
            Match m = value == null ? Match.Empty : GuidRx.Match(value);
            return m.Success ? m.Groups[1].Value : null;
        }

        public long Ref(long id, string key) => FileIdIn(Field(id, key));
        public string Guid(long id, string key) => GuidIn(Field(id, key));

        public List<long> Refs(long id, string key)
        {
            var ids = new List<long>();
            string[] lines = _lines[id];
            int at = Array.IndexOf(lines, "  " + key + ":");
            if (at < 0) return ids;
            for (int j = at + 1; j < lines.Length && lines[j].StartsWith("  - ", StringComparison.Ordinal); j++)
                ids.Add(FileIdIn(lines[j]));
            return ids;
        }

        static float Num(string s) => float.Parse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);

        public Vector3 Vec3(long id, string key)
        {
            Match m = XyzRx.Match(Field(id, key) ?? "");
            Assert.IsTrue(m.Success && m.Groups[3].Success, $"&{id} {key} is not an x/y/z vector");
            return new Vector3(Num(m.Groups[1].Value), Num(m.Groups[2].Value), Num(m.Groups[3].Value));
        }

        public Vector2 Vec2(long id, string key)
        {
            Match m = XyzRx.Match(Field(id, key) ?? "");
            Assert.IsTrue(m.Success && !m.Groups[3].Success, $"&{id} {key} is not an x/y vector");
            return new Vector2(Num(m.Groups[1].Value), Num(m.Groups[2].Value));
        }

        public Vector3Int Vec3Int(long id, string key)
        {
            Vector3 v = Vec3(id, key);
            return new Vector3Int((int)v.x, (int)v.y, (int)v.z);
        }

        public long RootGameObject(string name)
        {
            var hits = Order.Where(i => _class[i] == 1 && !_stripped.Contains(i) && Field(i, "m_Name") == name &&
                                        Ref(TransformOf(i), "m_Father") == 0).ToList();
            Assert.AreEqual(1, hits.Count, $"roots named {name}");
            return hits[0];
        }

        public long TransformOf(long go)
        {
            foreach (long c in Refs(go, "m_Component"))
                if (_class[c] == 4 || _class[c] == 224) return c;
            Assert.Fail($"&{go} has no transform");
            return 0;
        }

        public List<long> ChildGameObjects(long go)
        {
            var kids = new List<long>();
            foreach (long t in Refs(TransformOf(go), "m_Children"))
            {
                Assert.IsFalse(_stripped.Contains(t), $"&{t} under &{go} is a prefab instance");
                kids.Add(Ref(t, "m_GameObject"));
            }
            return kids;
        }

        /// <summary>The prefab instance behind each of a GameObject's children, in child order.</summary>
        public List<long> ChildInstances(long go)
        {
            var pis = new List<long>();
            foreach (long t in Refs(TransformOf(go), "m_Children"))
            {
                Assert.IsTrue(_stripped.Contains(t), $"&{t} under &{go} is not a prefab instance");
                pis.Add(Ref(t, "m_PrefabInstance"));
            }
            return pis;
        }

        public long ChildNamed(long go, string name)
        {
            List<long> hits = ChildGameObjects(go).Where(k => Field(k, "m_Name") == name).ToList();
            Assert.AreEqual(1, hits.Count, $"children of &{go} named {name}");
            return hits[0];
        }

        public long ComponentOfClass(long go, int classId)
        {
            List<long> hits = Refs(go, "m_Component").Where(c => _class[c] == classId).ToList();
            Assert.AreEqual(1, hits.Count, $"&{go}'s components of class {classId}");
            return hits[0];
        }

        public long Script(long go, string className)
        {
            bool Named(string identifier) =>
                identifier != null && (identifier.EndsWith("." + className, StringComparison.Ordinal) ||
                                       identifier.EndsWith("::" + className, StringComparison.Ordinal));
            List<long> hits = Refs(go, "m_Component")
                .Where(c => _class[c] == 114 && Named(Field(c, "m_EditorClassIdentifier")))
                .ToList();
            Assert.AreEqual(1, hits.Count, $"&{go}'s {className} components");
            return hits[0];
        }

        public List<Vector3Int> TileCells(long tilemap)
        {
            var cells = new List<Vector3Int>();
            foreach (string line in _lines[tilemap])
            {
                if (!line.StartsWith("  - first: ", StringComparison.Ordinal)) continue;
                Match m = XyzRx.Match(line.Substring("  - first: ".Length));
                Assert.IsTrue(m.Success, line);
                cells.Add(new Vector3Int((int)Num(m.Groups[1].Value), (int)Num(m.Groups[2].Value), (int)Num(m.Groups[3].Value)));
            }
            return cells;
        }

        /// <summary>One prefab instance's modifications, in file order: (target, path, value, objectReference).</summary>
        public List<(long Target, string Path, string Value, string ObjectReference)> Mods(long pi)
        {
            var mods = new List<(long, string, string, string)>();
            string[] lines = _lines[pi];
            for (int i = 0; i + 3 < lines.Length; i++)
            {
                if (!lines[i].StartsWith("    - target: ", StringComparison.Ordinal)) continue;
                Assert.IsTrue(lines[i + 1].StartsWith("      propertyPath: ", StringComparison.Ordinal), lines[i + 1]);
                Assert.IsTrue(lines[i + 2].StartsWith("      value:", StringComparison.Ordinal), lines[i + 2]);
                Assert.IsTrue(lines[i + 3].StartsWith("      objectReference: ", StringComparison.Ordinal), lines[i + 3]);
                string value = lines[i + 2].Length > "      value:".Length ? lines[i + 2].Substring("      value: ".Length) : "";
                mods.Add((FileIdIn(lines[i]), lines[i + 1].Substring("      propertyPath: ".Length), value,
                          lines[i + 3].Substring("      objectReference: ".Length)));
                i += 3;
            }
            return mods;
        }

        public string ModValue(long pi, long target, string path)
        {
            var hits = Mods(pi).Where(m => m.Target == target && m.Path == path).ToList();
            return hits.Count == 0 ? null : hits[hits.Count - 1].Value;
        }

        public string ModObjectReference(long pi, long target, string path)
        {
            var hits = Mods(pi).Where(m => m.Target == target && m.Path == path).ToList();
            return hits.Count == 0 ? null : hits[hits.Count - 1].ObjectReference;
        }

        /// <summary>
        /// Every document in a root's hierarchy, walked here and not by the step: the GameObjects and
        /// their components, every prefab instance hung under it with its stripped documents, and any
        /// component or GameObject added to those instances.
        /// </summary>
        public HashSet<long> Subtree(long rootGo)
        {
            // Who points at what, indexed once: components by their GameObject, transforms by their
            // father, and stripped documents by their prefab instance.
            var byGameObject = new Dictionary<long, List<long>>();
            var byFather = new Dictionary<long, List<long>>();
            var byInstance = new Dictionary<long, List<long>>();
            void Index(Dictionary<long, List<long>> map, long key, long id)
            {
                if (key == 0) return;
                if (!map.TryGetValue(key, out List<long> list)) map[key] = list = new List<long>();
                list.Add(id);
            }
            List<long> Of(Dictionary<long, List<long>> map, long key) =>
                map.TryGetValue(key, out List<long> list) ? list : new List<long>();
            foreach (long d in Order)
            {
                if (_stripped.Contains(d)) { Index(byInstance, Ref(d, "m_PrefabInstance"), d); continue; }
                if (_class[d] == 1 || _class[d] == 1001) continue;
                Index(byGameObject, Ref(d, "m_GameObject"), d);
                if (_class[d] == 4 || _class[d] == 224) Index(byFather, Ref(d, "m_Father"), d);
            }

            var set = new HashSet<long>();
            var transforms = new Stack<long>();
            void TakeGameObject(long go)
            {
                if (!set.Add(go)) return;
                foreach (long c in Refs(go, "m_Component")) set.Add(c);
                foreach (long c in Of(byGameObject, go)) set.Add(c);
                transforms.Push(TransformOf(go));
            }

            TakeGameObject(rootGo);
            while (transforms.Count > 0)
            {
                long t = transforms.Pop();
                var children = _stripped.Contains(t) ? new List<long>() : Refs(t, "m_Children");
                foreach (long c in Of(byFather, t))
                    if (!children.Contains(c)) children.Add(c);
                foreach (long child in children)
                {
                    if (!Has(child)) continue;   // a dangling child is the seal's to refuse, not the walk's
                    if (!_stripped.Contains(child))
                    {
                        TakeGameObject(Ref(child, "m_GameObject"));
                        continue;
                    }
                    long pi = Ref(child, "m_PrefabInstance");
                    if (!set.Add(pi)) continue;
                    foreach (long d in Of(byInstance, pi))
                    {
                        set.Add(d);
                        if (_class[d] == 1)
                            foreach (long c in Of(byGameObject, d)) set.Add(c);
                        if (_class[d] == 4 || _class[d] == 224) transforms.Push(d);
                    }
                }
            }
            return set;
        }

        /// <summary>The name of the object a document belongs to: a GameObject's own, a component's
        /// GameObject's, or a prefab instance's (its m_Name override).</summary>
        public string ObjectName(long id)
        {
            if (_class[id] == 1001)
            {
                (long Target, string Path, string Value, string ObjectReference) name =
                    Mods(id).LastOrDefault(m => m.Path == "m_Name");
                return name.Value;
            }
            if (_stripped.Contains(id)) return ObjectName(Ref(id, "m_PrefabInstance"));
            if (_class[id] == 1) return Field(id, "m_Name");
            long go = Ref(id, "m_GameObject");
            return go != 0 && Has(go) ? ObjectName(go) : null;
        }
    }
}
