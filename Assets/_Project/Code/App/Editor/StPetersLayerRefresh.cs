#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using HiddenHarbours.Art;                 // YSortSprite.OrderFor — the order a rock's sorter gives it
using HiddenHarbours.Art.Editor;          // RockPxCatalog (the swaps' kit), ShorelineIsoCatalog (the painter's rock keys)
using HiddenHarbours.Boats;               // NavBuoyDef, NavBuoyVisual — the nav marks' records
using HiddenHarbours.Core;                // ITidalTerrain, NavLightPhasePlan
using HiddenHarbours.World;               // ShoreRockDef, TidalTerrain, the nav plan

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>REFRESH ST PETERS' BUILD()-ONLY LAYERS WITHOUT A BUILD()</b> (terrain pass 9, PR 4b).
    /// The St Peters scene cannot be rebuilt — Build() wipes it, and it carries hand work — so the
    /// layers only Build() ever wrote are refreshed one root at a time, as text:
    /// <list type="number">
    /// <item><b>Shoreline</b>: the shore painter's own rock placement over the whole island, then
    /// the <see cref="ShoreRockDef"/> swaps (part 2 §4.4) — each named rock keeps its transform and
    /// takes its Rock Px sprite.</item>
    /// <item><b>ClamHoles</b>: <see cref="StPetersBuilder.ScatterClamHoles"/> over the whole island.</item>
    /// <item><b>StPetersNavMarks</b>: the marks' records from <see cref="StPetersNavMarks.Plan"/> —
    /// name, position, chart id and light phase, as the placer writes them. The dressing (def, size
    /// rung, facing) is checked against the plan and never changed: see <c>RequireDressing</c>.</item>
    /// </list>
    ///
    /// <para><b>One patch per step, to its own root.</b> A step is a pure function of (scene text,
    /// terrain, assets). It returns one <see cref="LayerPatch"/> that deletes, edits or adds whole
    /// YAML documents inside its root's hierarchy and nowhere else, names every object it deletes,
    /// and changes nothing more when applied twice. <see cref="LayerPatch.Seal"/> refuses a patch
    /// that breaks any of that, before anything is written.</para>
    ///
    /// <para><b>No scene is opened.</b> Nothing here opens, loads or saves a scene through Unity:
    /// the scene is a file the menu reads and, only when asked to apply, writes.</para>
    ///
    /// <para><b>Same answer as the builder.</b> The placements are the builder's own functions
    /// (<see cref="StPetersShoreMap.ScatterRocks"/>, <see cref="StPetersShoreMap.ScatterFieldRocks"/>,
    /// <see cref="StPetersBuilder.ScatterClamHoles"/>, <see cref="StPetersNavMarks.Plan"/>). The few
    /// lines of per-object dressing restated here (a rock's name, a hole's id and order, a mark's
    /// phase share) are held to the builder's own code by <c>StPetersLayerRefreshTests</c>.</para>
    /// </summary>
    public static partial class StPetersLayerRefresh
    {
        // =====================================================================================
        //  names and tolerances
        // =====================================================================================

        public const string ScenePath = "Assets/_Project/Scenes/StPeters.unity";

        /// <summary>Where the menu writes the three patches: git-ignored, and it outlives the editor.</summary>
        public const string PatchFolder = "artifacts/st-peters-layer-refresh";

        /// <summary>The builder's own literals for what it lays (<c>StPetersBuilder</c>'s clam root,
        /// <c>StPetersShorePainter.PlaceRocks</c>' two name prefixes, <c>NavMarkPlacer</c>'s mark names).</summary>
        public const string ClamHolesRootName = "ClamHoles";
        public const string ClamHoleName = "ClamHole";
        public const string RockNamePrefix = "Rock";
        public const string FieldRockNamePrefix = "FieldRock";
        public const string NavMarkNamePrefix = "NavMark_";

        /// <summary><see cref="ShoreRockDef"/> ids are <c>rock.snake_case</c>; St Peters' are <c>rock.stp_*</c>.</summary>
        public const string RockIdPrefix = "rock.";
        public const string StPetersRockIdPrefix = "rock.stp_";

        /// <summary>MakeClamHole's order clamp (a hole-vs-hole tiebreak), restated.</summary>
        public const int ClamOrderFloor = -4, ClamOrderCeiling = 4;

        /// <summary><c>NavBuoyVisual.Configure</c>'s facing clamp: the kit's eight cells.</summary>
        public const int NavFacingMax = 7;

        /// <summary>How close a recomputed object must sit to a committed one to BE that object, and be
        /// edited in place rather than deleted and re-added. A millimetre: far below any spacing the
        /// scatters allow, far above float noise.</summary>
        public const float SameObjectMetres = 0.001f;

        /// <summary>How far a <see cref="ShoreRockDef.At"/> may sit from its painted rock. Part 2 writes
        /// At to 0.01 m, and that rounding alone can put it 0.0071 m off.</summary>
        public const float SwapMatchMetres = 0.01f;

        const int GameObjectClass = 1, TransformClass = 4, RectTransformClass = 224, MonoBehaviourClass = 114,
                  SpriteRendererClass = 212, PrefabInstanceClass = 1001, TilemapClass = 1839735485,
                  SceneRootsClass = 1660057539;

        /// <summary>A step that cannot give the builder's answer safely stops here, with the reason.</summary>
        public sealed class Refusal : Exception
        {
            public Refusal(string message) : base("[StPetersLayerRefresh] " + message) { }
        }

        // =====================================================================================
        //  the scene as YAML documents
        // =====================================================================================

        static readonly Regex LocalRefRx = new Regex(@"\{fileID: (-?\d+)\}", RegexOptions.CultureInvariant);
        static readonly Regex FileIdRx = new Regex(@"fileID: (-?\d+)", RegexOptions.CultureInvariant);
        static readonly Regex GuidRx = new Regex(@"guid: ([0-9a-f]{32})", RegexOptions.CultureInvariant);
        static readonly Regex TypeRx = new Regex(@"type: (\d+)", RegexOptions.CultureInvariant);
        static readonly Regex Vec3Rx = new Regex(@"^\{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}$", RegexOptions.CultureInvariant);

        /// <summary>One Unity YAML document, exactly as the file holds it.</summary>
        public sealed class Doc
        {
            static readonly Regex HeaderRx =
                new Regex(@"^--- !u!(\d+) &(-?\d+)( stripped)?$", RegexOptions.CultureInvariant);

            public readonly int ClassId;
            public readonly long FileId;
            public readonly bool Stripped;

            /// <summary>Every line, header first, LF-joined, no trailing LF.</summary>
            public readonly string Text;

            string[] _lines;

            public Doc(string text)
            {
                if (string.IsNullOrEmpty(text)) throw new Refusal("an empty document.");
                if (text.IndexOf('\r') >= 0) throw new Refusal("a document with a CR in it; the scene is LF.");
                int nl = text.IndexOf('\n');
                string head = nl < 0 ? text : text.Substring(0, nl);
                if (!TryHeader(head, out ClassId, out FileId, out Stripped))
                    throw new Refusal($"not a Unity YAML document header: '{head}'.");
                Text = text;
            }

            public static bool TryHeader(string line, out int classId, out long fileId, out bool stripped)
            {
                classId = 0; fileId = 0; stripped = false;
                Match m = HeaderRx.Match(line);
                if (!m.Success) return false;
                classId = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                fileId = long.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
                stripped = m.Groups[3].Success;
                return true;
            }

            public string[] Lines => _lines ?? (_lines = Text.Split('\n'));

            /// <summary>The value of one of the document's own fields (two-space indent), or null.</summary>
            public string Field(string key)
            {
                string prefix = "  " + key + ":";
                foreach (string line in Lines)
                {
                    if (!line.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    if (line.Length == prefix.Length) return "";
                    if (line[prefix.Length] == ' ') return line.Substring(prefix.Length + 1);
                }
                return null;
            }

            public long FieldRef(string key) => ParseRef(Field(key));

            /// <summary>The local fileIDs a sequence field lists (<c>m_Children</c>, <c>m_Component</c>).</summary>
            public List<long> FieldRefs(string key)
            {
                var ids = new List<long>();
                string head = "  " + key + ":";
                string[] lines = Lines;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (lines[i] != head) continue;       // "  m_Children: []" is the empty list
                    for (int j = i + 1; j < lines.Length && lines[j].StartsWith("  - ", StringComparison.Ordinal); j++)
                        ids.Add(ParseRef(lines[j]));
                    break;
                }
                return ids;
            }

            /// <summary>True when this MonoBehaviour is the named class (Unity 6 writes the class into
            /// <c>m_EditorClassIdentifier</c> as <c>Assembly::Namespace.Class</c>).</summary>
            public bool IsScript(string fullClassName)
            {
                string id = Field("m_EditorClassIdentifier");
                return ClassId == MonoBehaviourClass && id != null &&
                       (id == fullClassName || id.EndsWith("::" + fullClassName, StringComparison.Ordinal));
            }
        }

        /// <summary>A scene file as its documents, in file order. Parse → Render is byte-exact.</summary>
        public sealed class SceneYaml
        {
            public readonly string Preamble;
            readonly List<Doc> _docs;
            readonly Dictionary<long, Doc> _byId = new Dictionary<long, Doc>();

            public SceneYaml(string preamble, List<Doc> docs)
            {
                Preamble = preamble ?? "";
                _docs = docs;
                foreach (Doc d in docs)
                    if (!_byId.ContainsKey(d.FileId)) _byId.Add(d.FileId, d);
                    else throw new Refusal($"two documents share the fileID &{d.FileId}.");
            }

            public static SceneYaml Parse(string text)
            {
                if (string.IsNullOrEmpty(text)) throw new Refusal("an empty scene.");
                if (text.IndexOf('\r') >= 0) throw new Refusal("the scene has CR line ends; Unity writes LF.");
                if (text[text.Length - 1] != '\n') throw new Refusal("the scene does not end in a newline.");
                var preamble = new List<string>();
                var docs = new List<Doc>();
                var cur = new List<string>();
                foreach (string line in text.Substring(0, text.Length - 1).Split('\n'))
                {
                    if (Doc.TryHeader(line, out _, out _, out _))
                    {
                        if (cur.Count > 0) docs.Add(new Doc(string.Join("\n", cur)));
                        cur.Clear();
                        cur.Add(line);
                    }
                    else if (cur.Count == 0) preamble.Add(line);
                    else cur.Add(line);
                }
                if (cur.Count > 0) docs.Add(new Doc(string.Join("\n", cur)));
                return new SceneYaml(string.Join("\n", preamble), docs);
            }

            public string Render()
            {
                var sb = new StringBuilder();
                if (Preamble.Length > 0) sb.Append(Preamble).Append('\n');
                foreach (Doc d in _docs) sb.Append(d.Text).Append('\n');
                return sb.ToString();
            }

            public IReadOnlyList<Doc> Docs => _docs;
            public bool Contains(long id) => _byId.ContainsKey(id);
            public Doc Get(long id) => _byId.TryGetValue(id, out Doc d) ? d : null;

            public Doc Require(long id, string what) =>
                Get(id) ?? throw new Refusal($"{what}: the scene holds no document &{id}.");

            public List<Doc> ComponentsOf(Doc go)
            {
                var comps = new List<Doc>();
                foreach (long id in go.FieldRefs("m_Component"))
                    comps.Add(Require(id, $"a component of '{go.Field("m_Name")}'"));
                return comps;
            }

            public Doc TransformOf(Doc go)
            {
                foreach (Doc c in ComponentsOf(go))
                    if (c.ClassId == TransformClass || c.ClassId == RectTransformClass) return c;
                throw new Refusal($"'{go.Field("m_Name")}' (&{go.FileId}) has no Transform.");
            }

            /// <summary>The one scene root with this name.</summary>
            public Doc RootNamed(string name)
            {
                var hits = new List<Doc>();
                foreach (Doc d in _docs)
                {
                    if (d.ClassId != GameObjectClass || d.Stripped || d.Field("m_Name") != name) continue;
                    if (TransformOf(d).FieldRef("m_Father") == 0) hits.Add(d);
                }
                if (hits.Count != 1) throw new Refusal($"the scene holds {hits.Count} roots named '{name}'; the step needs exactly one.");
                return hits[0];
            }

            /// <summary>The one child GameObject with this name under a transform.</summary>
            public Doc ChildNamed(Doc parentTransform, string name)
            {
                var hits = new List<Doc>();
                foreach (long id in parentTransform.FieldRefs("m_Children"))
                {
                    Doc tr = Require(id, "a child transform");
                    if (tr.Stripped) continue;
                    Doc go = Require(tr.FieldRef("m_GameObject"), "a child GameObject");
                    if (go.Field("m_Name") == name) hits.Add(go);
                }
                if (hits.Count != 1) throw new Refusal($"&{parentTransform.FileId} holds {hits.Count} children named '{name}'; the step needs exactly one.");
                return hits[0];
            }

            /// <summary>
            /// Every document in a root's hierarchy: its GameObjects and their components, every
            /// prefab instance parented under it with its stripped documents, the components added to
            /// those instances, and the GameObjects added under them.
            /// </summary>
            public HashSet<long> SubtreeOf(long rootGameObject)
            {
                var byInstance = new Dictionary<long, List<long>>();
                var byGameObject = new Dictionary<long, List<long>>();
                var byFather = new Dictionary<long, List<long>>();
                foreach (Doc d in _docs)
                {
                    if (d.Stripped) Add(byInstance, d.FieldRef("m_PrefabInstance"), d.FileId);
                    else if (d.ClassId != GameObjectClass && d.ClassId != PrefabInstanceClass)
                    {
                        long go = d.FieldRef("m_GameObject");
                        if (go != 0) Add(byGameObject, go, d.FileId);
                        if (d.ClassId == TransformClass || d.ClassId == RectTransformClass)
                        {
                            long father = d.FieldRef("m_Father");
                            if (father != 0) Add(byFather, father, d.FileId);
                        }
                    }
                }

                var set = new HashSet<long>();
                var queue = new Queue<long>();
                void AddGameObject(Doc go)
                {
                    if (!set.Add(go.FileId)) return;
                    foreach (long c in go.FieldRefs("m_Component")) set.Add(c);
                    if (byGameObject.TryGetValue(go.FileId, out List<long> attached))
                        foreach (long c in attached) set.Add(c);
                    queue.Enqueue(TransformOf(go).FileId);
                }

                AddGameObject(Require(rootGameObject, "the step's root"));
                while (queue.Count > 0)
                {
                    long trId = queue.Dequeue();
                    Doc tr = Require(trId, "a transform in the hierarchy");
                    var children = new List<long>(tr.Stripped ? new List<long>() : tr.FieldRefs("m_Children"));
                    if (byFather.TryGetValue(trId, out List<long> fathered))
                        foreach (long f in fathered) if (!children.Contains(f)) children.Add(f);
                    foreach (long childId in children)
                    {
                        Doc child = Require(childId, "a child transform");
                        if (!child.Stripped)
                        {
                            AddGameObject(Require(child.FieldRef("m_GameObject"), "a child GameObject"));
                            continue;
                        }
                        long instance = child.FieldRef("m_PrefabInstance");
                        if (!set.Add(instance)) continue;
                        if (!byInstance.TryGetValue(instance, out List<long> parts)) continue;
                        foreach (long part in parts)
                        {
                            set.Add(part);
                            Doc p = Require(part, "a stripped document");
                            if (p.ClassId == GameObjectClass && byGameObject.TryGetValue(part, out List<long> added))
                                foreach (long c in added) set.Add(c);
                            if (p.ClassId == TransformClass || p.ClassId == RectTransformClass) queue.Enqueue(part);
                        }
                    }
                }
                return set;
            }

            static void Add(Dictionary<long, List<long>> map, long key, long value)
            {
                if (!map.TryGetValue(key, out List<long> list)) map[key] = list = new List<long>();
                list.Add(value);
            }
        }

        // =====================================================================================
        //  the patch
        // =====================================================================================

        public enum OpKind { Delete, Edit, Add }

        /// <summary>One whole-document operation. <see cref="Before"/> is the document as the step
        /// found it; <see cref="After"/> as it leaves it.</summary>
        public sealed class Op
        {
            public readonly OpKind Kind;
            public readonly long FileId;
            public readonly string Name;
            public readonly string Before;
            public readonly string After;

            public Op(OpKind kind, long fileId, string name, string before, string after)
            {
                Kind = kind; FileId = fileId; Name = name; Before = before; After = after;
            }
        }

        /// <summary>One difference between the root as it stands and the builder's answer, in words.</summary>
        public sealed class Change
        {
            public readonly string Object;
            public readonly string What;
            public readonly string Detail;

            public Change(string obj, string what, string detail)
            {
                Object = obj; What = what; Detail = detail ?? "";
            }

            public override string ToString() => Detail.Length > 0 ? $"{Object}: {What} ({Detail})" : $"{Object}: {What}";
        }

        /// <summary>
        /// ONE step's patch to ONE root. Idempotent: an operation whose result already stands is
        /// skipped, and one that finds neither its before nor its after refuses.
        /// </summary>
        public sealed class LayerPatch
        {
            public readonly string Step;
            public readonly string Root;
            public readonly long RootGameObject;

            readonly List<Op> _ops = new List<Op>();
            readonly List<Change> _changes = new List<Change>();
            bool _sealed;

            public LayerPatch(string step, string root, long rootGameObject)
            {
                Step = step; Root = root; RootGameObject = rootGameObject;
            }

            public IReadOnlyList<Op> Ops => _ops;
            public IReadOnlyList<Change> Changes => _changes;
            public bool IsEmpty => _ops.Count == 0;
            public IEnumerable<Op> Deletions => _ops.Where(o => o.Kind == OpKind.Delete);

            internal void Delete(Doc d, string name) => Push(new Op(OpKind.Delete, d.FileId, name, d.Text, null));

            internal void Edit(Doc d, string after, string name)
            {
                if (after != d.Text) Push(new Op(OpKind.Edit, d.FileId, name, d.Text, after));
            }

            internal void Add(string text, string name) => Push(new Op(OpKind.Add, new Doc(text).FileId, name, null, text));

            internal void Note(string obj, string what, string detail = null) => _changes.Add(new Change(obj, what, detail));

            void Push(Op op)
            {
                if (_sealed) throw new InvalidOperationException("the patch is sealed.");
                _ops.Add(op);
            }

            /// <summary>The scene text with this patch applied. Applying it again changes nothing.</summary>
            public string ApplyTo(string sceneText) => Apply(SceneYaml.Parse(sceneText)).Render();

            public SceneYaml Apply(SceneYaml scene)
            {
                var removed = new HashSet<long>();
                var replaced = new Dictionary<long, Doc>();
                var added = new List<Doc>();
                foreach (Op op in _ops)
                {
                    Doc now = scene.Get(op.FileId);
                    switch (op.Kind)
                    {
                        case OpKind.Delete:
                            if (now == null) break;
                            if (now.Text != op.Before) throw Conflict(op);
                            removed.Add(op.FileId);
                            break;
                        case OpKind.Edit:
                            if (now == null) throw new Refusal($"{Step}: {op.Name} (&{op.FileId}) is not in the scene.");
                            if (now.Text == op.After) break;
                            if (now.Text != op.Before) throw Conflict(op);
                            replaced[op.FileId] = new Doc(op.After);
                            break;
                        default:
                            if (now != null)
                            {
                                if (now.Text != op.After) throw Conflict(op);
                                break;
                            }
                            added.Add(new Doc(op.After));
                            break;
                    }
                }

                // New documents go in just before SceneRoots, as the hand-written tail already does.
                int rootsAt = -1;
                for (int i = 0; i < scene.Docs.Count; i++)
                    if (scene.Docs[i].ClassId == SceneRootsClass) rootsAt = i;
                var docs = new List<Doc>(scene.Docs.Count + added.Count);
                for (int i = 0; i < scene.Docs.Count; i++)
                {
                    if (i == rootsAt) docs.AddRange(added);
                    Doc d = scene.Docs[i];
                    if (removed.Contains(d.FileId)) continue;
                    docs.Add(replaced.TryGetValue(d.FileId, out Doc r) ? r : d);
                }
                if (rootsAt < 0) docs.AddRange(added);
                return new SceneYaml(scene.Preamble, docs);
            }

            Refusal Conflict(Op op) =>
                new Refusal($"{Step}: {op.Name} (&{op.FileId}) holds neither what the patch found nor what it " +
                            "writes. The scene changed after the patch was planned: re-plan it.");

            /// <summary>
            /// The gate, run once when the step finishes and before anything is written: every
            /// deletion named; one operation per document; everything it deletes or edits already in
            /// its root, everything it adds hanging under its root; nothing left pointing at a deleted
            /// document; everything it writes pointing only at documents that exist.
            /// </summary>
            public LayerPatch Seal(SceneYaml before)
            {
                var seen = new HashSet<long>();
                foreach (Op op in _ops)
                {
                    if (!seen.Add(op.FileId)) throw new Refusal($"{Step}: two operations on &{op.FileId}.");
                    if (string.IsNullOrWhiteSpace(op.Name)) throw new Refusal($"{Step}: an unnamed {op.Kind} of &{op.FileId}.");
                    if (op.After != null && (op.After.Contains("\n\n") || op.After.EndsWith("\n", StringComparison.Ordinal)))
                        throw new Refusal($"{Step}: {op.Name} would write a blank line.");
                }

                // A root the scene does not hold yet is one this patch ADDS: none of it is in the scene before,
                // and the one document outside it the patch may touch is the scene's SceneRoots list, which a
                // new root must join (RootsEditOf).
                bool newRoot = !before.Contains(RootGameObject);
                HashSet<long> mine = newRoot ? new HashSet<long>() : before.SubtreeOf(RootGameObject);
                long rootsEdit = newRoot ? RootsEditOf(before) : 0;
                foreach (Op op in _ops)
                    if (op.Kind != OpKind.Add && op.FileId != rootsEdit && !mine.Contains(op.FileId))
                        throw new Refusal($"{Step}: {op.Name} (&{op.FileId}) is outside the '{Root}' root.");

                SceneYaml after = Apply(before);
                HashSet<long> mineAfter = after.SubtreeOf(RootGameObject);
                foreach (Op op in _ops)
                    if (op.Kind == OpKind.Add && !mineAfter.Contains(op.FileId))
                        throw new Refusal($"{Step}: the added {op.Name} (&{op.FileId}) does not hang under '{Root}'.");

                var deleted = new HashSet<long>(Deletions.Select(o => o.FileId));
                if (deleted.Count > 0)
                    foreach (Doc d in after.Docs)
                        foreach (Match m in LocalRefRx.Matches(d.Text))
                            if (deleted.Contains(long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)))
                                throw new Refusal($"{Step}: &{d.FileId} still points at the deleted &{m.Groups[1].Value}.");

                foreach (Op op in _ops)
                {
                    if (op.After == null) continue;
                    foreach (Match m in LocalRefRx.Matches(op.After))
                    {
                        long id = long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                        if (id != 0 && !after.Contains(id))
                            throw new Refusal($"{Step}: {op.Name} points at &{id}, which the scene does not hold.");
                    }
                }
                _sealed = true;
                return this;
            }

            /// <summary>
            /// A NEW root's one edit outside it: the scene's SceneRoots list, which must list the root's
            /// transform once more, at its end, and change nothing else. The patch must add the root's
            /// GameObject under the root's name, and its transform at the top of the hierarchy. Returns the
            /// SceneRoots document's fileID.
            /// </summary>
            long RootsEditOf(SceneYaml before)
            {
                Op go = _ops.FirstOrDefault(o => o.Kind == OpKind.Add && o.FileId == RootGameObject)
                        ?? throw new Refusal($"{Step}: the scene holds no '{Root}' (&{RootGameObject}), and the patch does not add it.");
                var goDoc = new Doc(go.After);
                if (goDoc.ClassId != GameObjectClass || goDoc.Field("m_Name") != Root)
                    throw new Refusal($"{Step}: &{RootGameObject} is not a GameObject named '{Root}'.");
                List<long> comps = goDoc.FieldRefs("m_Component");
                Op tr = _ops.FirstOrDefault(o => o.Kind == OpKind.Add && comps.Contains(o.FileId) &&
                                                 new Doc(o.After).ClassId == TransformClass)
                        ?? throw new Refusal($"{Step}: the new root '{Root}' adds no transform.");
                if (new Doc(tr.After).FieldRef("m_Father") != 0)
                    throw new Refusal($"{Step}: the new root '{Root}' is not at the top of the hierarchy.");

                Doc list = SceneRootsOf(before);
                List<Op> edits = _ops.Where(o => o.FileId == list.FileId).ToList();
                if (edits.Count != 1 || edits[0].Kind != OpKind.Edit)
                    throw new Refusal($"{Step}: the new root '{Root}' must join the scene's SceneRoots list by one named edit.");
                if (edits[0].Before != list.Text || edits[0].After != WithRootListed(list.Text, tr.FileId))
                    throw new Refusal($"{Step}: {edits[0].Name} does more than list '{Root}' among the scene's roots.");
                return list.FileId;
            }

            public string Summary()
            {
                int del = _ops.Count(o => o.Kind == OpKind.Delete), ed = _ops.Count(o => o.Kind == OpKind.Edit),
                    add = _ops.Count(o => o.Kind == OpKind.Add);
                var sb = new StringBuilder();
                sb.Append($"{Step} ('{Root}'): {del} documents deleted, {ed} edited, {add} added; {_changes.Count} differences");
                foreach (Change c in _changes) sb.Append("\n  - ").Append(c);
                return sb.ToString();
            }

            /// <summary>The patch as one YAML file: what it deletes (named), edits and adds, whole documents.</summary>
            public string ToYaml()
            {
                var sb = new StringBuilder();
                sb.Append("# StPetersLayerRefresh: one step's patch to one root. Whole Unity YAML documents.\n");
                sb.Append("step: ").Append(Q(Step)).Append('\n');
                sb.Append("root: ").Append(Q(Root)).Append('\n');
                sb.Append("rootGameObject: ").Append(RootGameObject.ToString(CultureInfo.InvariantCulture)).Append('\n');
                sb.Append("differences:").Append(_changes.Count == 0 ? " []\n" : "\n");
                foreach (Change c in _changes) sb.Append("- ").Append(Q(c.ToString())).Append('\n');
                foreach (OpKind kind in new[] { OpKind.Delete, OpKind.Edit, OpKind.Add })
                {
                    var ops = _ops.Where(o => o.Kind == kind).ToList();
                    string key = kind == OpKind.Delete ? "deletions" : kind == OpKind.Edit ? "edits" : "additions";
                    sb.Append(key).Append(':').Append(ops.Count == 0 ? " []\n" : "\n");
                    foreach (Op op in ops)
                    {
                        sb.Append("- fileID: ").Append(op.FileId.ToString(CultureInfo.InvariantCulture)).Append('\n');
                        sb.Append("  name: ").Append(Q(op.Name)).Append('\n');
                        if (op.Before != null) Block(sb, "before", op.Before);
                        if (op.After != null) Block(sb, "after", op.After);
                    }
                }
                return sb.ToString();
            }

            static void Block(StringBuilder sb, string key, string text)
            {
                sb.Append("  ").Append(key).Append(": |-\n");
                foreach (string line in text.Split('\n')) sb.Append("    ").Append(line).Append('\n');
            }

            static string Q(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        /// <summary>New fileIDs: deterministic from what the document is, and never one the scene holds.</summary>
        public sealed class IdAllocator
        {
            const long MinId = 100000000L, MaxId = 2147483647L;
            readonly HashSet<long> _taken;

            public IdAllocator(SceneYaml scene) { _taken = new HashSet<long>(scene.Docs.Select(d => d.FileId)); }

            /// <summary>An allocator that may hand back the ids in <paramref name="reusable"/>: a step that writes
            /// its own root whole draws the same ids on every plan, so a second plan finds its documents where
            /// the first left them.</summary>
            public IdAllocator(SceneYaml scene, ICollection<long> reusable)
            {
                _taken = new HashSet<long>(scene.Docs.Select(d => d.FileId).Where(id => !reusable.Contains(id)));
            }

            public long Next(string key)
            {
                for (int salt = 0; ; salt++)
                {
                    ulong h = 14695981039346656037UL;
                    foreach (char ch in salt == 0 ? key : key + "#" + salt.ToString(CultureInfo.InvariantCulture))
                    {
                        h ^= ch;
                        h *= 1099511628211UL;
                    }
                    long id = MinId + (long)(h % (ulong)(MaxId - MinId));
                    if (_taken.Add(id)) return id;
                }
            }
        }

        // =====================================================================================
        //  values as the YAML writes them
        // =====================================================================================

        public struct ObjRef
        {
            public long FileId;
            public string Guid;
            public int Type;

            public ObjRef(long fileId, string guid, int type) { FileId = fileId; Guid = guid; Type = type; }

            public string Yaml => string.IsNullOrEmpty(Guid)
                ? $"{{fileID: {FileId.ToString(CultureInfo.InvariantCulture)}}}"
                : $"{{fileID: {FileId.ToString(CultureInfo.InvariantCulture)}, guid: {Guid}, type: {Type.ToString(CultureInfo.InvariantCulture)}}}";

            public bool SameTarget(ObjRef o) =>
                FileId == o.FileId && string.Equals(Guid ?? "", o.Guid ?? "", StringComparison.Ordinal);

            public static ObjRef Parse(string yaml)
            {
                if (yaml == null) return default;
                Match g = GuidRx.Match(yaml), t = TypeRx.Match(yaml);
                return new ObjRef(ParseRef(yaml), g.Success ? g.Groups[1].Value : null,
                                  t.Success ? int.Parse(t.Groups[1].Value, CultureInfo.InvariantCulture) : 0);
            }
        }

        /// <summary>A sprite as a renderer records it: the reference, and the size in metres
        /// (rect ÷ pixels per unit) that a sprite assignment writes into <c>m_Size</c>.</summary>
        public struct SpriteRef
        {
            public ObjRef Sprite;
            public Vector2 Size;

            public SpriteRef(ObjRef sprite, Vector2 size) { Sprite = sprite; Size = size; }
        }

        public static long ParseRef(string value)
        {
            if (value == null) return 0;
            Match m = FileIdRx.Match(value);
            return m.Success ? long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        }

        public static float ParseFloat(string s) =>
            float.Parse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);

        public static int ParseInt(string s) =>
            int.Parse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture);

        public static Vector3 ParseVec3(string value)
        {
            Match m = value == null ? Match.Empty : Vec3Rx.Match(value);
            if (!m.Success) throw new Refusal($"not a vector: '{value}'.");
            return new Vector3(ParseFloat(m.Groups[1].Value), ParseFloat(m.Groups[2].Value), ParseFloat(m.Groups[3].Value));
        }

        /// <summary>A float as the scene writes it: the shortest text that reads back to the same float.</summary>
        public static string F(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) throw new Refusal($"a non-finite number ({v}).");
            if (v == 0f) return BitConverter.GetBytes(v)[3] >= 0x80 ? "-0" : "0";
            for (int digits = 1; digits <= 9; digits++)
            {
                string s = v.ToString("G" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                if (ParseFloat(s) == v) return s;
            }
            return v.ToString("R", CultureInfo.InvariantCulture);
        }

        static string Int(int v) => v.ToString(CultureInfo.InvariantCulture);
        static string Vec3Yaml(float x, float y, float z) => $"{{x: {F(x)}, y: {F(y)}, z: {F(z)}}}";
        static string Vec2Yaml(Vector2 v) => $"{{x: {F(v.x)}, y: {F(v.y)}}}";
        static string At(Vector2 p) => $"({F(p.x)}, {F(p.y)})";

        /// <summary>The same place to the bit. Unity's <c>Vector3 ==</c> forgives 1e-5 m, and a step that kept
        /// a place that near the builder's would not be writing the builder's answer.</summary>
        static bool SamePlace(Vector3 a, Vector3 b) => a.x == b.x && a.y == b.y && a.z == b.z;

        /// <summary>A string as a plain YAML scalar, refused if it would need quoting.</summary>
        static string Plain(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Trim() != s || s.Contains(": ") || s.Contains(" #") ||
                "!&*|>'\"%@`#,[]{}".IndexOf(s[0]) >= 0 || ((s[0] == '-' || s[0] == '?' || s[0] == ':') && s.Length > 1 && s[1] == ' '))
                throw new Refusal($"'{s}' would need quoting in the scene; the step writes plain values only.");
            return s;
        }

        /// <summary>Replace the one line holding a document's own field.</summary>
        static string SetField(string docText, string key, string value)
        {
            string[] lines = docText.Split('\n');
            string prefix = "  " + key + ":";
            int hit = -1;
            for (int i = 1; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (lines[i].Length != prefix.Length && lines[i][prefix.Length] != ' ') continue;
                if (hit >= 0) throw new Refusal($"'{key}' appears twice in {lines[0]}.");
                hit = i;
            }
            if (hit < 0) throw new Refusal($"no '{key}' in {lines[0]}.");
            lines[hit] = prefix + " " + value;
            return string.Join("\n", lines);
        }

        /// <summary>Replace a sequence field's entries (<c>m_Children</c>) with these local fileIDs.</summary>
        static string SetRefList(string docText, string key, IList<long> ids)
        {
            var lines = new List<string>(docText.Split('\n'));
            string head = "  " + key + ":";
            int at = lines.FindIndex(l => l == head || l == head + " []");
            if (at < 0) throw new Refusal($"no '{key}' in {lines[0]}.");
            int end = at + 1;
            while (end < lines.Count && lines[end].StartsWith("  - ", StringComparison.Ordinal)) end++;
            lines.RemoveRange(at, end - at);
            var block = new List<string> { ids.Count == 0 ? head + " []" : head };
            foreach (long id in ids) block.Add($"  - {{fileID: {id.ToString(CultureInfo.InvariantCulture)}}}");
            lines.InsertRange(at, block);
            return string.Join("\n", lines);
        }

        /// <summary>A document copied under new fileIDs: its header, and every local reference to a
        /// document in <paramref name="map"/>.</summary>
        static string Remap(string text, IReadOnlyDictionary<long, long> map)
        {
            string[] lines = text.Split('\n');
            if (!Doc.TryHeader(lines[0], out int cls, out long id, out bool stripped) || !map.TryGetValue(id, out long nid))
                throw new Refusal($"cannot copy {lines[0]}.");
            lines[0] = $"--- !u!{Int(cls)} &{nid.ToString(CultureInfo.InvariantCulture)}" + (stripped ? " stripped" : "");
            for (int i = 1; i < lines.Length; i++)
                lines[i] = LocalRefRx.Replace(lines[i], m =>
                    map.TryGetValue(long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), out long n)
                        ? $"{{fileID: {n.ToString(CultureInfo.InvariantCulture)}}}"
                        : m.Value);
            return string.Join("\n", lines);
        }

        /// <summary>The scene's one SceneRoots document: the list of its top-level transforms.</summary>
        static Doc SceneRootsOf(SceneYaml scene)
        {
            List<Doc> hits = scene.Docs.Where(d => d.ClassId == SceneRootsClass).ToList();
            if (hits.Count != 1) throw new Refusal($"the scene holds {hits.Count} SceneRoots documents; a new root needs exactly one.");
            return hits[0];
        }

        /// <summary>A SceneRoots document with one more root listed at the end of <c>m_Roots</c>. Refuses a
        /// root that is listed already.</summary>
        static string WithRootListed(string rootsDoc, long rootTransform)
        {
            var lines = new List<string>(rootsDoc.Split('\n'));
            int at = lines.FindIndex(l => l == "  m_Roots:" || l == "  m_Roots: []");
            if (at < 0) throw new Refusal("the SceneRoots document has no m_Roots.");
            string entry = $"  - {{fileID: {rootTransform.ToString(CultureInfo.InvariantCulture)}}}";
            int end = at + 1;
            for (; end < lines.Count && lines[end].StartsWith("  - ", StringComparison.Ordinal); end++)
                if (lines[end] == entry) throw new Refusal($"&{rootTransform} is listed among the scene's roots already.");
            lines[at] = "  m_Roots:";
            lines.Insert(end, entry);
            return string.Join("\n", lines);
        }

        /// <summary>The builder puts these roots at the origin, so a child's local position is its world
        /// position. Refuse a root that has been moved, rather than write positions off by its offset.</summary>
        static void RequireIdentity(Doc tr, string what)
        {
            if (tr.Field("m_LocalPosition") != "{x: 0, y: 0, z: 0}" ||
                tr.Field("m_LocalRotation") != "{x: 0, y: 0, z: 0, w: 1}" ||
                tr.Field("m_LocalScale") != "{x: 1, y: 1, z: 1}")
                throw new Refusal($"'{what}' is not at the origin, unrotated and unscaled; its children's positions would be off.");
        }

        static List<long> NewChildren<T>(IReadOnlyList<T> desired, Func<int, long> idOf)
        {
            var ids = new List<long>(desired.Count);
            for (int i = 0; i < desired.Count; i++) ids.Add(idOf(i));
            return ids;
        }

        // =====================================================================================
        //  1. SHORELINE — the painter's rock placement, then the ShoreRockDef swaps
        // =====================================================================================

        /// <summary>One rock of the painter's placement: its object name, where it stands, its sheet key.</summary>
        public struct PlacedRock
        {
            public string Name;
            public Vector2 Position;
            public string SpriteKey;
        }

        /// <summary>Where a rock's sprite comes from: the painter's ShoreIso sheet, or a swap's Rock Px cell.</summary>
        public interface IShoreRockSprites
        {
            bool TryShoreIso(string spriteKey, out SpriteRef sprite);
            bool TryRockPx(ShoreRockDef def, out SpriteRef sprite);
        }

        /// <summary>The shore painter's own placement (<c>PlaceRocks</c>): the reef's scatter, then the
        /// field rocks, named as it names them.</summary>
        public static List<PlacedRock> PainterPlacement(ITidalTerrain terrain)
        {
            var rocks = new List<PlacedRock>();
            foreach (StPetersShoreMap.RockSite s in StPetersShoreMap.ScatterRocks(terrain))
                rocks.Add(new PlacedRock { Name = $"{RockNamePrefix}_{s.Sprite}", Position = s.Position, SpriteKey = s.Sprite });
            foreach (StPetersShoreMap.RockSite s in StPetersShoreMap.ScatterFieldRocks(terrain))
                rocks.Add(new PlacedRock { Name = $"{FieldRockNamePrefix}_{s.Sprite}", Position = s.Position, SpriteKey = s.Sprite });
            return rocks;
        }

        /// <summary>The rocks as the scene holds them, as a placement (the sheet key is the name's tail).</summary>
        public static List<PlacedRock> RocksInScene(SceneYaml scene)
        {
            Doc rocksTr = ShoreRocksTransform(scene, out _, out _);
            return ReadRocks(scene, rocksTr).Select(r => new PlacedRock
            {
                Name = r.Name,
                Position = r.Position,
                SpriteKey = r.Name.Substring(r.Name.LastIndexOf('_') + 1),
            }).ToList();
        }

        /// <summary>The Rock Px cell a Def names: <c>&lt;stem&gt;_c&lt;col&gt;_r&lt;row&gt;</c>, the
        /// kit slicer's own naming — col = variant (+4 mirrored), row = the tide state.</summary>
        public static string RockPxSpriteName(ShoreRockDef def)
        {
            int row = Array.IndexOf(RockPxCatalog.Tides, def.State);
            if (row < 0) throw new Refusal($"{def.Id}: state '{def.State}' is not one of the kit's tides ({string.Join(", ", RockPxCatalog.Tides)}).");
            if (def.Variant < 0 || def.Variant >= RockPxCatalog.VariantCols)
                throw new Refusal($"{def.Id}: variant {def.Variant} is outside 0..{RockPxCatalog.VariantCols - 1}.");
            int col = def.Variant + (def.Mirrored ? RockPxCatalog.VariantCols : 0);
            return $"{KitStem(def)}_c{Int(col)}_r{Int(row)}";
        }

        public static string RockPxSheetPath(ShoreRockDef def)
        {
            try { return RockPxCatalog.SheetPath(def.Form, def.Stone, def.Dress, RockPxCatalog.Channel.Albedo); }
            catch (ArgumentException e) { throw new Refusal($"{def.Id}: {e.Message}"); }
        }

        static string KitStem(ShoreRockDef def)
        {
            try { return RockPxCatalog.StemFor(def.Form, def.Stone, def.Dress); }
            catch (ArgumentException e) { throw new Refusal($"{def.Id}: {e.Message}"); }
        }

        static readonly Regex RockIdRx = new Regex(@"^rock\.[a-z0-9]+(_[a-z0-9]+)*$", RegexOptions.CultureInvariant);

        public static LayerPatch Shoreline(SceneYaml scene, ITidalTerrain terrain, IShoreRockSprites sprites,
                                           IReadOnlyList<ShoreRockDef> swaps, IdAllocator ids = null) =>
            Shoreline(scene, PainterPlacement(terrain), sprites, swaps, ids);

        public static LayerPatch Shoreline(SceneYaml scene, IReadOnlyList<PlacedRock> placement, IShoreRockSprites sprites,
                                           IReadOnlyList<ShoreRockDef> swaps, IdAllocator ids = null)
        {
            ids = ids ?? new IdAllocator(scene);
            Doc rocksTr = ShoreRocksTransform(scene, out Doc rootGo, out Doc contactMap);
            var patch = new LayerPatch("Shoreline", StPetersShorePainter.RootName, rootGo.FileId);

            // The painter's placement, less any rock the sheet has no sprite for: the painter skips those too.
            var desired = new List<DesiredRock>();
            foreach (PlacedRock p in placement)
                if (sprites.TryShoreIso(p.SpriteKey, out SpriteRef s))
                    desired.Add(new DesiredRock { Name = p.Name, Position = p.Position, Sprite = s });

            Swap(desired, swaps, sprites, patch);

            List<SceneRock> existing = ReadRocks(scene, rocksTr);
            SceneRock template = existing.Count > 0 ? existing[0] : null;
            var used = new HashSet<SceneRock>();
            var trIds = new long[desired.Count];

            for (int i = 0; i < desired.Count; i++)
            {
                DesiredRock d = desired[i];
                SceneRock hit = null;
                float best = float.MaxValue;
                foreach (SceneRock e in existing)
                {
                    if (used.Contains(e) || e.Name != d.Name) continue;
                    float dist = Vector2.Distance(e.Position, d.Position);
                    if (dist <= SameObjectMetres && dist < best) { hit = e; best = dist; }
                }

                string obj = $"{d.Name} @{At(d.Position)}";
                if (hit == null)
                {
                    if (template == null) throw new Refusal("ShoreRocks holds no rock to copy, so a new one cannot be written.");
                    trIds[i] = AddRock(patch, template, d, ids, obj);
                    patch.Note(obj, "added", d.SwapId != null ? $"as {d.SwapId}" : null);
                    continue;
                }

                used.Add(hit);
                trIds[i] = hit.Tr.FileId;
                var want = new Vector3(d.Position.x, d.Position.y, 0f);
                if (!SamePlace(hit.Position, want))
                {
                    patch.Edit(hit.Tr, SetField(hit.Tr.Text, "m_LocalPosition", Vec3Yaml(want.x, want.y, want.z)),
                               $"Shoreline/ShoreRocks/{obj}: Transform");
                    patch.Note(obj, "moved", $"from ({F(hit.Position.x)}, {F(hit.Position.y)}, {F(hit.Position.z)})");
                }

                string sr = hit.Sr.Text;
                ObjRef had = ObjRef.Parse(hit.Sr.Field("m_Sprite"));
                if (!had.SameTarget(d.Sprite.Sprite) || hit.Sr.Field("m_Size") != Vec2Yaml(d.Sprite.Size))
                {
                    sr = SetField(sr, "m_Sprite", d.Sprite.Sprite.Yaml);
                    sr = SetField(sr, "m_Size", Vec2Yaml(d.Sprite.Size));
                    if (d.SwapId != null) patch.Note(obj, "swapped", $"{d.SwapId}: {had.Yaml} to {d.Sprite.Sprite.Yaml}");
                    else patch.Note(obj, "re-sprited", $"{had.Yaml} to {d.Sprite.Sprite.Yaml}");
                }
                int order = SortingOrderFor(hit.Sorter, d.Position.y);
                string hadOrder = hit.Sr.Field("m_SortingOrder");
                if (hadOrder != Int(order))
                {
                    sr = SetField(sr, "m_SortingOrder", Int(order));
                    patch.Note(obj, "re-sorted", $"order {hadOrder} to {Int(order)}");
                }
                patch.Edit(hit.Sr, sr, $"Shoreline/ShoreRocks/{obj}: SpriteRenderer");
            }

            foreach (SceneRock e in existing)
            {
                if (used.Contains(e)) continue;
                string obj = $"{e.Name} @({F(e.Position.x)}, {F(e.Position.y)})";
                patch.Delete(e.Go, $"Shoreline/ShoreRocks/{obj}: GameObject");
                patch.Delete(e.Tr, $"Shoreline/ShoreRocks/{obj}: Transform");
                patch.Delete(e.Sr, $"Shoreline/ShoreRocks/{obj}: SpriteRenderer");
                patch.Delete(e.Sorter, $"Shoreline/ShoreRocks/{obj}: YSortSprite");
                patch.Note(obj, "removed");
            }

            List<long> children = NewChildren(desired, i => trIds[i]);
            if (!children.SequenceEqual(rocksTr.FieldRefs("m_Children")))
                patch.Edit(rocksTr, SetRefList(rocksTr.Text, "m_Children", children),
                           $"Shoreline/ShoreRocks: Transform m_Children ({existing.Count} to {children.Count} rocks)");

            RefreshContacts(patch, contactMap, desired.Select(d => d.Position));
            return patch.Seal(scene);
        }

        sealed class DesiredRock
        {
            public string Name;
            public Vector2 Position;
            public SpriteRef Sprite;
            public string SwapId;
        }

        sealed class SceneRock
        {
            public Doc Go, Tr, Sr, Sorter;
            public string Name;
            public Vector3 Position;
        }

        static Doc ShoreRocksTransform(SceneYaml scene, out Doc rootGo, out Doc contactMap)
        {
            rootGo = scene.RootNamed(StPetersShorePainter.RootName);
            Doc rootTr = scene.TransformOf(rootGo);
            RequireIdentity(rootTr, StPetersShorePainter.RootName);
            Doc rocksTr = scene.TransformOf(scene.ChildNamed(rootTr, StPetersShorePainter.RocksRootName));
            RequireIdentity(rocksTr, StPetersShorePainter.RocksRootName);
            List<Doc> maps = scene.ComponentsOf(scene.ChildNamed(rootTr, StPetersShorePainter.ContactLayerName))
                                  .Where(c => c.ClassId == TilemapClass).ToList();
            if (maps.Count != 1) throw new Refusal($"'{StPetersShorePainter.ContactLayerName}' carries {maps.Count} Tilemaps; the painter lays one.");
            contactMap = maps[0];
            return rocksTr;
        }

        static List<SceneRock> ReadRocks(SceneYaml scene, Doc rocksTr)
        {
            var rocks = new List<SceneRock>();
            foreach (long id in rocksTr.FieldRefs("m_Children"))
            {
                Doc tr = scene.Require(id, "a ShoreRocks child");
                if (tr.Stripped || tr.ClassId != TransformClass)
                    throw new Refusal($"ShoreRocks' child &{id} is not a plain Transform: the painter lays only plain rocks.");
                Doc go = scene.Require(tr.FieldRef("m_GameObject"), "a rock's GameObject");
                List<Doc> comps = scene.ComponentsOf(go);
                List<Doc> srs = comps.Where(c => c.ClassId == SpriteRendererClass).ToList();
                List<Doc> sorters = comps.Where(c => c.IsScript("HiddenHarbours.Art.YSortSprite")).ToList();
                if (comps.Count != 3 || srs.Count != 1 || sorters.Count != 1 || tr.FieldRefs("m_Children").Count != 0)
                    throw new Refusal($"'{go.Field("m_Name")}' (&{go.FileId}) under ShoreRocks is not the painter's shape " +
                                      "(Transform, SpriteRenderer, YSortSprite, no children). The step will not touch hand work.");
                rocks.Add(new SceneRock
                {
                    Go = go, Tr = tr, Sr = srs[0], Sorter = sorters[0],
                    Name = go.Field("m_Name"), Position = ParseVec3(tr.Field("m_LocalPosition")),
                });
            }
            return rocks;
        }

        /// <summary>The order the rock's YSortSprite gives it at this height — its own serialized band.</summary>
        static int SortingOrderFor(Doc sorter, float y)
        {
            string Need(string key) => sorter.Field(key) ?? throw new Refusal($"YSortSprite &{sorter.FileId} has no '{key}'.");
            return YSortSprite.OrderFor(y + ParseFloat(Need("_pivotYOffset")), ParseFloat(Need("_baseOrder")),
                                        ParseFloat(Need("_orderPerUnit")), ParseInt(Need("_minOrder")), ParseInt(Need("_maxOrder")));
        }

        static long AddRock(LayerPatch patch, SceneRock t, DesiredRock d, IdAllocator ids, string obj)
        {
            string key = $"Shoreline/{d.Name}/{F(d.Position.x)},{F(d.Position.y)}";
            var map = new Dictionary<long, long>
            {
                [t.Go.FileId] = ids.Next(key + "/GameObject"),
                [t.Tr.FileId] = ids.Next(key + "/Transform"),
                [t.Sr.FileId] = ids.Next(key + "/SpriteRenderer"),
                [t.Sorter.FileId] = ids.Next(key + "/YSortSprite"),
            };
            string sr = Remap(t.Sr.Text, map);
            sr = SetField(sr, "m_Sprite", d.Sprite.Sprite.Yaml);
            sr = SetField(sr, "m_Size", Vec2Yaml(d.Sprite.Size));
            sr = SetField(sr, "m_SortingOrder", Int(SortingOrderFor(t.Sorter, d.Position.y)));
            patch.Add(SetField(Remap(t.Go.Text, map), "m_Name", Plain(d.Name)), $"Shoreline/ShoreRocks/{obj}: GameObject");
            patch.Add(SetField(Remap(t.Tr.Text, map), "m_LocalPosition", Vec3Yaml(d.Position.x, d.Position.y, 0f)),
                      $"Shoreline/ShoreRocks/{obj}: Transform");
            patch.Add(sr, $"Shoreline/ShoreRocks/{obj}: SpriteRenderer");
            patch.Add(Remap(t.Sorter.Text, map), $"Shoreline/ShoreRocks/{obj}: YSortSprite");
            return map[t.Tr.FileId];
        }

        /// <summary>
        /// Part 2 §4.4's swaps. Each Def names exactly one painted rock (its name, within
        /// <see cref="SwapMatchMetres"/> of At) and gives it its Rock Px cell. Only the sprite changes:
        /// the rock's place, and every other rock, are the placement's.
        /// </summary>
        static void Swap(List<DesiredRock> desired, IReadOnlyList<ShoreRockDef> swaps, IShoreRockSprites sprites, LayerPatch patch)
        {
            if (swaps == null || swaps.Count == 0) return;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var claimed = new Dictionary<DesiredRock, string>();
            foreach (ShoreRockDef def in swaps.OrderBy(s => s == null ? "" : s.Id, StringComparer.Ordinal))
            {
                if (def == null) throw new Refusal("a missing ShoreRockDef in the swap list.");
                if (def.Id == null || !RockIdRx.IsMatch(def.Id)) throw new Refusal($"'{def.Id}' is not a rock.snake_case id.");
                if (!ids.Add(def.Id)) throw new Refusal($"two ShoreRockDefs share the id '{def.Id}'.");
                List<DesiredRock> hits = desired.Where(r => r.Name == def.Today &&
                                                            Vector2.Distance(r.Position, def.At) <= SwapMatchMetres).ToList();
                if (hits.Count != 1)
                    throw new Refusal($"{def.Id} names {def.Today} at {At(def.At)}, and {hits.Count} painted rocks match it " +
                                      $"(within {F(SwapMatchMetres)} m); a swap needs exactly one.");
                if (claimed.TryGetValue(hits[0], out string other)) throw new Refusal($"{def.Id} and {other} name the same rock.");
                claimed[hits[0]] = def.Id;
                if (!sprites.TryRockPx(def, out SpriteRef px))
                    throw new Refusal($"{def.Id}: no sprite '{RockPxSpriteName(def)}' in {RockPxSheetPath(def)}.");
                hits[0].Sprite = px;
                hits[0].SwapId = def.Id;
            }
        }

        /// <summary>
        /// The contact shade under each rock, as <c>PlaceRock</c> sets it: one tile at
        /// (⌊x⌋, ⌊y⌋ + 1). The layer holds one tile kind, so only its cells, its four ref counts and
        /// its bounds move; every entry copies an existing one.
        /// </summary>
        static void RefreshContacts(LayerPatch patch, Doc map, IEnumerable<Vector2> rockPositions)
        {
            List<Vector3Int> want = rockPositions
                .Select(p => new Vector3Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y) + 1, 0))
                .Distinct().OrderBy(c => c.z).ThenBy(c => c.y).ThenBy(c => c.x).ToList();

            var lines = new List<string>(map.Lines);
            int head = lines.IndexOf("  m_Tiles:");
            if (head < 0) throw new Refusal($"{StPetersShorePainter.ContactLayerName} holds no tiles to copy.");
            int end = head + 1;
            var had = new List<Vector3Int>();
            List<string> entry = null;
            while (end < lines.Count && lines[end].StartsWith("  - first: ", StringComparison.Ordinal))
            {
                Vector3 c = ParseVec3(lines[end].Substring("  - first: ".Length));
                had.Add(new Vector3Int((int)c.x, (int)c.y, (int)c.z));
                int next = end + 1;
                while (next < lines.Count && lines[next].StartsWith("    ", StringComparison.Ordinal)) next++;
                List<string> body = lines.GetRange(end + 1, next - end - 1);
                if (entry == null) entry = body;
                else if (!entry.SequenceEqual(body))
                    throw new Refusal($"{StPetersShorePainter.ContactLayerName} mixes tile entries; the painter lays one kind.");
                end = next;
            }
            if (had.SequenceEqual(want)) return;
            if (entry == null || want.Count == 0)
                throw new Refusal($"{StPetersShorePainter.ContactLayerName} would go from {had.Count} to {want.Count} tiles; the step only moves cells.");

            var tiles = new List<string>();
            foreach (Vector3Int c in want)
            {
                tiles.Add($"  - first: {{x: {Int(c.x)}, y: {Int(c.y)}, z: {Int(c.z)}}}");
                tiles.AddRange(entry);
            }
            lines.RemoveRange(head + 1, end - head - 1);
            lines.InsertRange(head + 1, tiles);

            int refCounts = 0;
            for (int i = 0; i < lines.Count; i++)
                if (lines[i].StartsWith("    m_RefCount: ", StringComparison.Ordinal)) { lines[i] = "    m_RefCount: " + Int(want.Count); refCounts++; }
            if (refCounts != 4)
                throw new Refusal($"{StPetersShorePainter.ContactLayerName} has {refCounts} tile arrays; the painter's layer has four, one entry each.");

            string text = string.Join("\n", lines);
            int minX = want.Min(c => c.x), minY = want.Min(c => c.y), maxX = want.Max(c => c.x), maxY = want.Max(c => c.y);
            text = SetField(text, "m_Origin", $"{{x: {Int(minX)}, y: {Int(minY)}, z: 0}}");
            text = SetField(text, "m_Size", $"{{x: {Int(maxX - minX + 1)}, y: {Int(maxY - minY + 1)}, z: 1}}");

            var gone = had.Except(want).ToList();
            var fresh = want.Except(had).ToList();
            patch.Edit(map, text, $"Shoreline/{StPetersShorePainter.ContactLayerName}: Tilemap ({had.Count} to {want.Count} contact tiles)");
            string Cells(List<Vector3Int> cs) => string.Join(" ", cs.Select(c => $"({Int(c.x)}, {Int(c.y)})"));
            if (gone.Count > 0) patch.Note(StPetersShorePainter.ContactLayerName, "tiles removed", Cells(gone));
            if (fresh.Count > 0) patch.Note(StPetersShorePainter.ContactLayerName, "tiles added", Cells(fresh));
            if (gone.Count == 0 && fresh.Count == 0) patch.Note(StPetersShorePainter.ContactLayerName, "tiles re-ordered");
        }

        // =====================================================================================
        //  2. CLAMHOLES — ScatterClamHoles over the whole island
        // =====================================================================================

        /// <summary>MakeClamHole's id for a hole: its position to the hundredth, formatted as the builder
        /// formats it (the editor's current culture).</summary>
        public static string ClamHoleId(Vector2 p) => $"fixture.clam_hole.{p.x:0.00}_{p.y:0.00}";

        /// <summary>MakeClamHole's order for a hole: a hole-vs-hole tiebreak on its height.</summary>
        public static int ClamHoleSortingOrder(Vector2 p) => Mathf.Clamp(-Mathf.RoundToInt(p.y), ClamOrderFloor, ClamOrderCeiling);

        public static LayerPatch ClamHoles(SceneYaml scene, ITidalTerrain terrain, IdAllocator ids = null) =>
            ClamHoles(scene, StPetersBuilder.ScatterClamHoles(terrain), ids);

        public static LayerPatch ClamHoles(SceneYaml scene, IReadOnlyList<Vector2> holes, IdAllocator ids = null)
        {
            ids = ids ?? new IdAllocator(scene);
            Doc rootGo = scene.RootNamed(ClamHolesRootName);
            Doc rootTr = scene.TransformOf(rootGo);
            RequireIdentity(rootTr, ClamHolesRootName);
            var patch = new LayerPatch("ClamHoles", ClamHolesRootName, rootGo.FileId);

            List<SceneHole> existing = ReadHoles(scene, rootTr);
            SceneHole template = existing.Count > 0 ? existing[0] : null;
            var used = new HashSet<SceneHole>();
            var trIds = new long[holes.Count];

            for (int i = 0; i < holes.Count; i++)
            {
                Vector2 p = holes[i];
                SceneHole hit = null;
                float best = float.MaxValue;
                foreach (SceneHole e in existing)
                {
                    if (used.Contains(e)) continue;
                    float dist = Vector2.Distance(e.Position, p);
                    if (dist <= SameObjectMetres && dist < best) { hit = e; best = dist; }
                }

                string obj = $"{ClamHoleName} @{At(p)}";
                string id = Plain(ClamHoleId(p));
                string order = Int(ClamHoleSortingOrder(p));
                if (hit == null)
                {
                    if (template == null) throw new Refusal("ClamHoles holds no hole to copy, so a new one cannot be written.");
                    trIds[i] = AddHole(patch, template, p, id, order, ids, obj);
                    patch.Note(obj, "added", id);
                    continue;
                }

                used.Add(hit);
                trIds[i] = hit.Tr.FileId;
                var want = new Vector3(p.x, p.y, 0f);
                if (!SamePlace(hit.Position, want))
                {
                    patch.Edit(hit.Tr, SetField(hit.Tr.Text, "m_LocalPosition", Vec3Yaml(want.x, want.y, want.z)),
                               $"ClamHoles/{obj}: Transform");
                    patch.Note(obj, "moved", $"from ({F(hit.Position.x)}, {F(hit.Position.y)}, {F(hit.Position.z)})");
                }
                string hadOrder = hit.Sr.Field("m_SortingOrder");
                if (hadOrder != order)
                {
                    patch.Edit(hit.Sr, SetField(hit.Sr.Text, "m_SortingOrder", order), $"ClamHoles/{obj}: SpriteRenderer");
                    patch.Note(obj, "re-sorted", $"order {hadOrder} to {order}");
                }
                string hadId = hit.Dig.Field("_id");
                if (hadId != id)
                {
                    patch.Edit(hit.Dig, SetField(hit.Dig.Text, "_id", id), $"ClamHoles/{obj}: ClamDig");
                    patch.Note(obj, "re-id'd", $"{hadId} to {id}");
                }
            }

            foreach (SceneHole e in existing)
            {
                if (used.Contains(e)) continue;
                string obj = $"{ClamHoleName} @({F(e.Position.x)}, {F(e.Position.y)})";
                foreach (Doc d in e.All)
                    patch.Delete(d, $"ClamHoles/{obj}: {Kind(d)}");
                patch.Note(obj, "removed", e.Dig.Field("_id"));
            }

            List<long> children = NewChildren(holes, i => trIds[i]);
            if (!children.SequenceEqual(rootTr.FieldRefs("m_Children")))
                patch.Edit(rootTr, SetRefList(rootTr.Text, "m_Children", children),
                           $"ClamHoles: Transform m_Children ({existing.Count} to {children.Count} holes)");
            return patch.Seal(scene);
        }

        sealed class SceneHole
        {
            public Doc Go, Tr, Sr, Spot, Dig, Visual;
            public Vector3 Position;
            public IEnumerable<Doc> All => new[] { Go, Tr, Sr, Spot, Dig, Visual };
        }

        static List<SceneHole> ReadHoles(SceneYaml scene, Doc rootTr)
        {
            var holes = new List<SceneHole>();
            foreach (long id in rootTr.FieldRefs("m_Children"))
            {
                Doc tr = scene.Require(id, "a ClamHoles child");
                if (tr.Stripped || tr.ClassId != TransformClass)
                    throw new Refusal($"ClamHoles' child &{id} is not a plain Transform: the builder lays only plain holes.");
                Doc go = scene.Require(tr.FieldRef("m_GameObject"), "a hole's GameObject");
                List<Doc> comps = scene.ComponentsOf(go);
                Doc One(Func<Doc, bool> match) { List<Doc> m = comps.Where(match).ToList(); return m.Count == 1 ? m[0] : null; }
                var hole = new SceneHole
                {
                    Go = go, Tr = tr,
                    Sr = One(c => c.ClassId == SpriteRendererClass),
                    Spot = One(c => c.IsScript("HiddenHarbours.World.ClamSpot")),
                    Dig = One(c => c.IsScript("HiddenHarbours.Fishing.ClamDig")),
                    Visual = One(c => c.IsScript("HiddenHarbours.Fishing.ClamHoleVisual")),
                    Position = ParseVec3(tr.Field("m_LocalPosition")),
                };
                if (go.Field("m_Name") != ClamHoleName || comps.Count != 5 || hole.Sr == null || hole.Spot == null ||
                    hole.Dig == null || hole.Visual == null || tr.FieldRefs("m_Children").Count != 0)
                    throw new Refusal($"'{go.Field("m_Name")}' (&{go.FileId}) under ClamHoles is not the builder's hole " +
                                      "(Transform, SpriteRenderer, ClamSpot, ClamDig, ClamHoleVisual, no children). The step will not touch hand work.");
                holes.Add(hole);
            }
            return holes;
        }

        static string Kind(Doc d)
        {
            switch (d.ClassId)
            {
                case GameObjectClass: return "GameObject";
                case TransformClass: return "Transform";
                case SpriteRendererClass: return "SpriteRenderer";
                case PrefabInstanceClass: return "PrefabInstance";
            }
            string id = d.Field("m_EditorClassIdentifier");
            if (!string.IsNullOrEmpty(id)) return id.Substring(id.LastIndexOf('.') + 1);
            return $"class {Int(d.ClassId)}";
        }

        static long AddHole(LayerPatch patch, SceneHole t, Vector2 p, string id, string order, IdAllocator ids, string obj)
        {
            string key = $"ClamHoles/{F(p.x)},{F(p.y)}";
            var map = new Dictionary<long, long>();
            foreach (Doc d in t.All) map[d.FileId] = ids.Next(key + "/" + Kind(d));
            foreach (Doc d in t.All)
            {
                string text = Remap(d.Text, map);
                if (d == t.Tr) text = SetField(text, "m_LocalPosition", Vec3Yaml(p.x, p.y, 0f));
                if (d == t.Sr) text = SetField(text, "m_SortingOrder", order);
                if (d == t.Dig) text = SetField(text, "_id", id);
                patch.Add(text, $"ClamHoles/{obj}: {Kind(d)}");
            }
            return map[t.Tr.FileId];
        }

        // =====================================================================================
        //  3. STPETERSNAVMARKS — the marks' records, from StPetersNavMarks.Plan
        // =====================================================================================

        /// <summary>The nav buoy prefab as an instance's modifications see it: its guid, its root's
        /// fileIDs, and what the prefab itself holds for each record (the LOADED value, so a field the
        /// file lacks reads as its C# default).</summary>
        public sealed class NavPrefab
        {
            public string Guid;
            public long GameObject, Transform, Visual;
            public string Name, SizeId, MarkId;
            public Vector3 Position;
            public int Facing;
            public float Phase;
            public ObjRef Def;
        }

        public interface INavMarkAssets
        {
            NavPrefab Prefab { get; }
            bool TryDef(string markType, out ObjRef def, out string lightText);
        }

        public static LayerPatch NavMarks(SceneYaml scene, ITidalTerrain terrain, INavMarkAssets assets, IdAllocator ids = null) =>
            NavMarks(scene, StPetersNavMarks.Plan(terrain), assets, ids);

        public static LayerPatch NavMarks(SceneYaml scene, NavMarkPlanResult plan, INavMarkAssets assets, IdAllocator ids = null)
        {
            ids = ids ?? new IdAllocator(scene);
            NavPrefab prefab = assets.Prefab ?? throw new Refusal("no nav buoy prefab.");
            Doc rootGo = scene.RootNamed(StPetersNavMarks.RootName);
            Doc rootTr = scene.TransformOf(rootGo);
            RequireIdentity(rootTr, StPetersNavMarks.RootName);
            var patch = new LayerPatch("StPetersNavMarks", StPetersNavMarks.RootName, rootGo.FileId);

            // The placer's phase share, over the whole plan and before any mark is placed.
            Dictionary<string, float> phases = NavLightPhasePlan.Spread(
                plan.Marks.Select(m => (m.Id, assets.TryDef(m.MarkType, out _, out string light) ? light : (string)null)).ToList());

            List<SceneMark> existing = ReadMarks(scene, rootTr, prefab);
            SceneMark template = existing.Count > 0 ? existing[0] : null;
            var byName = new Dictionary<string, SceneMark>(StringComparer.Ordinal);
            foreach (SceneMark e in existing)
                if (!byName.ContainsKey(e.Name)) byName.Add(e.Name, e);
                else throw new Refusal($"two marks under {StPetersNavMarks.RootName} are named '{e.Name}'.");

            var used = new HashSet<SceneMark>();
            var planned = new HashSet<string>(StringComparer.Ordinal);
            var trIds = new List<long>();
            foreach (PlannedNavMark mark in plan.Marks)
            {
                if (!assets.TryDef(mark.MarkType, out ObjRef def, out _)) continue;   // the placer skips a type with no def
                string name = NavMarkNamePrefix + mark.Id;
                if (!planned.Add(name)) throw new Refusal($"the plan names {name} twice.");
                List<NavRecord> records = RecordsFor(prefab, mark, def, phases.TryGetValue(mark.Id, out float ph) ? ph : -1f);
                var changed = new List<string>();

                if (byName.TryGetValue(name, out SceneMark hit) && used.Add(hit))
                {
                    List<string> had = hit.Pi.Lines.ToList();
                    RequireDressing(name, had, records, "the scene's mark");
                    if (hit.Attached.Count > 0 && records.Any(r => r.Target == prefab.Transform && !Holds(had, r)))
                        throw new Refusal($"{name} would move, and it carries components added after the prefab (a mooring, whose " +
                                          "anchor would go stale). The step does not re-moor: re-place it with the placer.");
                    trIds.Add(hit.Tr.FileId);
                    patch.Edit(hit.Pi, Rerecord(hit.Pi.Text, records, prefab, changed), $"StPetersNavMarks/{name}: PrefabInstance");
                    foreach (string c in changed) patch.Note(name, "re-recorded", c);
                    continue;
                }

                if (template == null) throw new Refusal($"{StPetersNavMarks.RootName} holds no mark to copy, so a new one cannot be written.");
                foreach (string list in new[] { "m_RemovedComponents", "m_RemovedGameObjects", "m_AddedGameObjects", "m_AddedComponents" })
                    if (InstanceField(template.Pi, list) != "[]" || template.Attached.Count > 0)
                        throw new Refusal($"the mark a new one would copy, {template.Name}, is not a plain instance ({list}); a copy would carry its changes too.");
                RequireDressing(name, template.Pi.Lines.ToList(), records, $"the mark it would be copied from, {template.Name},");
                string key = $"StPetersNavMarks/{mark.Id}";
                var map = new Dictionary<long, long>
                {
                    [template.Pi.FileId] = ids.Next(key + "/PrefabInstance"),
                    [template.Tr.FileId] = ids.Next(key + "/Transform"),
                };
                patch.Add(Rerecord(Remap(template.Pi.Text, map), records, prefab, changed), $"StPetersNavMarks/{name}: PrefabInstance");
                patch.Add(Remap(template.Tr.Text, map), $"StPetersNavMarks/{name}: Transform (stripped)");
                trIds.Add(map[template.Tr.FileId]);
                patch.Note(name, "added", $"at {At(mark.At)}, copied from {template.Name}, which wears the same dressing");
            }

            foreach (SceneMark e in existing)
            {
                if (used.Contains(e)) continue;
                patch.Delete(e.Pi, $"StPetersNavMarks/{e.Name}: PrefabInstance");
                patch.Delete(e.Tr, $"StPetersNavMarks/{e.Name}: Transform (stripped)");
                foreach (Doc d in e.Attached) patch.Delete(d, $"StPetersNavMarks/{e.Name}: {Kind(d)} (&{d.FileId})");
                patch.Note(e.Name, "removed");
            }

            if (!trIds.SequenceEqual(rootTr.FieldRefs("m_Children")))
                patch.Edit(rootTr, SetRefList(rootTr.Text, "m_Children", trIds),
                           $"StPetersNavMarks: Transform m_Children ({existing.Count} to {trIds.Count} marks)");
            return patch.Seal(scene);
        }

        sealed class SceneMark
        {
            public Doc Pi, Tr;
            public List<Doc> Attached;
            public string Name;
        }

        sealed class NavRecord
        {
            public long Target;
            public string Path;
            public char Kind;          // 's' string, 'f' float, 'i' int, 'r' reference
            public string Value, Default;
            public ObjRef Ref, DefaultRef;
            public bool Dressing;      // def, size rung, facing: what NavBuoyVisual.Apply dresses her from
        }

        /// <summary>What the placer writes for one mark: the name, the planned position, and
        /// <c>NavBuoyVisual.Configure(def, size, facing, id, phase)</c>.</summary>
        static List<NavRecord> RecordsFor(NavPrefab p, PlannedNavMark m, ObjRef def, float phase)
        {
            NavRecord S(long t, string path, string v, string d) => new NavRecord { Target = t, Path = path, Kind = 's', Value = Plain(v), Default = d ?? "" };
            NavRecord Fl(long t, string path, float v, float d) => new NavRecord { Target = t, Path = path, Kind = 'f', Value = F(v), Default = F(d) };
            NavRecord size = S(p.Visual, "_sizeId", m.SizeId, p.SizeId);
            size.Dressing = true;
            return new List<NavRecord>
            {
                S(p.GameObject, "m_Name", NavMarkNamePrefix + m.Id, p.Name),
                Fl(p.Transform, "m_LocalPosition.x", m.At.x, p.Position.x),
                Fl(p.Transform, "m_LocalPosition.y", m.At.y, p.Position.y),
                Fl(p.Transform, "m_LocalPosition.z", 0f, p.Position.z),
                new NavRecord { Target = p.Visual, Path = "_def", Kind = 'r', Ref = def, DefaultRef = p.Def, Dressing = true },
                size,
                new NavRecord
                {
                    Target = p.Visual, Path = "_facing", Kind = 'i', Dressing = true,
                    Value = Int(Mathf.Clamp(m.Facing, 0, NavFacingMax)), Default = Int(p.Facing),
                },
                S(p.Visual, "_markId", m.Id, p.MarkId),
                Fl(p.Visual, "_phaseFraction", phase, p.Phase),
            };
        }

        /// <summary>True when the instance already LOADS with the record's value: its override, or
        /// the prefab's own value where it has none.</summary>
        static bool Holds(List<string> piLines, NavRecord r)
        {
            Mod m = ReadMods(piLines, out _).LastOrDefault(x => x.Target == r.Target && x.Path == r.Path);
            return m != null ? Same(r, m.Value, m.Ref) : Same(r, r.Default, r.DefaultRef);
        }

        static string Loaded(List<string> piLines, NavRecord r)
        {
            Mod m = ReadMods(piLines, out _).LastOrDefault(x => x.Target == r.Target && x.Path == r.Path);
            if (r.Kind == 'r') return m != null ? m.Ref.Yaml : r.DefaultRef.Yaml;
            return m != null ? m.Value : r.Default;
        }

        /// <summary>
        /// A mark's DRESSING — def, size rung, facing — is what <c>NavBuoyVisual.Apply</c> derives her
        /// sprite, float line and mooring from, and Apply runs only in the editor. A text patch cannot
        /// run it, so the step never changes a dressing: a mark it keeps, or copies, must already wear
        /// the plan's. A plan that re-dresses a mark is refused, for the placer to do.
        /// </summary>
        static void RequireDressing(string who, List<string> piLines, List<NavRecord> records, string wearer)
        {
            foreach (NavRecord r in records)
                if (r.Dressing && !Holds(piLines, r))
                    throw new Refusal($"{who}: the plan dresses it with {r.Path} {(r.Kind == 'r' ? r.Ref.Yaml : r.Value)}, and " +
                                      $"{wearer} wears {Loaded(piLines, r)}. Its sprite, float line and mooring follow the dressing " +
                                      "through NavBuoyVisual.Apply, which a text patch cannot run: re-dress it with the placer.");
        }

        static List<SceneMark> ReadMarks(SceneYaml scene, Doc rootTr, NavPrefab prefab)
        {
            var marks = new List<SceneMark>();
            foreach (long id in rootTr.FieldRefs("m_Children"))
            {
                Doc tr = scene.Require(id, "a nav mark's transform");
                if (!tr.Stripped)
                    throw new Refusal($"&{id} under {StPetersNavMarks.RootName} is not a prefab instance: the placer lays only NavBuoy instances.");
                Doc pi = scene.Require(tr.FieldRef("m_PrefabInstance"), "a nav mark's prefab instance");
                if (pi.ClassId != PrefabInstanceClass || ObjRef.Parse(pi.Field("m_SourcePrefab")).Guid != prefab.Guid ||
                    ParseRef(InstanceField(pi, "m_TransformParent")) != rootTr.FileId ||
                    ParseRef(tr.Field("m_CorrespondingSourceObject")) != prefab.Transform)
                    throw new Refusal($"&{pi.FileId} under {StPetersNavMarks.RootName} is not a NavBuoy instance hung from the root.");
                if (InstanceField(pi, "m_AddedGameObjects") != "[]")
                    throw new Refusal($"&{pi.FileId} under {StPetersNavMarks.RootName} carries added GameObjects; the step will not touch hand work.");

                var attached = new List<Doc>();
                foreach (Doc d in scene.Docs)
                    if (d.Stripped && d.FileId != tr.FileId && d.FieldRef("m_PrefabInstance") == pi.FileId) attached.Add(d);
                var strippedGos = new HashSet<long>(attached.Where(d => d.ClassId == GameObjectClass).Select(d => d.FileId));
                if (strippedGos.Count > 0)
                    foreach (Doc d in scene.Docs)
                        if (!d.Stripped && strippedGos.Contains(d.FieldRef("m_GameObject"))) attached.Add(d);

                List<Mod> mods = ReadMods(pi.Lines.ToList(), out _);
                Mod nameMod = mods.LastOrDefault(m => m.Target == prefab.GameObject && m.Path == "m_Name");
                marks.Add(new SceneMark { Pi = pi, Tr = tr, Attached = attached, Name = nameMod != null ? nameMod.Value : prefab.Name });
            }
            return marks;
        }

        /// <summary>A prefab instance's own field under <c>m_Modification</c> (four-space indent).</summary>
        static string InstanceField(Doc pi, string key)
        {
            string prefix = "    " + key + ": ";
            foreach (string line in pi.Lines)
                if (line.StartsWith(prefix, StringComparison.Ordinal)) return line.Substring(prefix.Length);
            return pi.Lines.Contains("    " + key + ":") ? "" : null;
        }

        sealed class Mod
        {
            public long Target;
            public string Path, Value;
            public ObjRef Ref;
            public int Line;           // the "- target:" line; value is Line + 2, objectReference Line + 3
        }

        const string ModsHead = "    m_Modifications:";

        static List<Mod> ReadMods(List<string> lines, out int end)
        {
            var mods = new List<Mod>();
            int head = lines.IndexOf(ModsHead);
            if (head < 0)
            {
                if (lines.IndexOf(ModsHead + " []") < 0) throw new Refusal("a prefab instance without m_Modifications.");
                end = -1;
                return mods;
            }
            int i = head + 1;
            while (i < lines.Count && lines[i].StartsWith("    - target: ", StringComparison.Ordinal))
            {
                if (i + 3 >= lines.Count || !lines[i + 1].StartsWith("      propertyPath: ", StringComparison.Ordinal) ||
                    !lines[i + 2].StartsWith("      value:", StringComparison.Ordinal) ||
                    !lines[i + 3].StartsWith("      objectReference: ", StringComparison.Ordinal))
                    throw new Refusal($"a modification that is not four lines at line {i + 1} of a prefab instance.");
                string value = lines[i + 2].Length > "      value:".Length ? lines[i + 2].Substring("      value: ".Length) : "";
                mods.Add(new Mod
                {
                    Target = ParseRef(lines[i]),
                    Path = lines[i + 1].Substring("      propertyPath: ".Length),
                    Value = value,
                    Ref = ObjRef.Parse(lines[i + 3]),
                    Line = i,
                });
                i += 4;
            }
            end = i;
            return mods;
        }

        /// <summary>
        /// Bring one instance's records to the plan's. A record the instance already overrides is
        /// rewritten in place; one it does not is added only when the prefab's own value differs,
        /// next to its target's other overrides in path order — so the value the instance LOADS with is
        /// the placer's either way.
        /// </summary>
        static string Rerecord(string piText, List<NavRecord> records, NavPrefab prefab, List<string> changed)
        {
            var lines = piText.Split('\n').ToList();
            foreach (NavRecord r in records)
            {
                List<Mod> mods = ReadMods(lines, out int end);
                List<Mod> same = mods.Where(m => m.Target == r.Target && m.Path == r.Path).ToList();
                if (same.Count > 1) throw new Refusal($"a prefab instance overrides '{r.Path}' twice.");
                if (same.Count == 1)
                {
                    Mod m = same[0];
                    if (Same(r, m.Value, m.Ref)) continue;
                    if (r.Kind == 'r')
                    {
                        lines[m.Line + 2] = "      value: ";
                        lines[m.Line + 3] = "      objectReference: " + r.Ref.Yaml;
                    }
                    else lines[m.Line + 2] = "      value: " + r.Value;
                    changed.Add($"{r.Path}: {(r.Kind == 'r' ? m.Ref.Yaml : m.Value)} to {(r.Kind == 'r' ? r.Ref.Yaml : r.Value)}");
                    continue;
                }
                if (Same(r, r.Default, r.DefaultRef)) continue;

                if (end < 0)
                {
                    int empty = lines.IndexOf(ModsHead + " []");
                    lines[empty] = ModsHead;
                    end = empty + 1;
                }
                int at = end;
                Mod after = mods.Where(m => m.Target == r.Target && string.CompareOrdinal(m.Path, r.Path) < 0).LastOrDefault();
                Mod before = mods.FirstOrDefault(m => m.Target == r.Target);
                Mod nextTarget = mods.FirstOrDefault(m => m.Target > r.Target);
                if (after != null) at = after.Line + 4;
                else if (before != null) at = before.Line;
                else if (nextTarget != null) at = nextTarget.Line;
                lines.InsertRange(at, new[]
                {
                    $"    - target: {{fileID: {r.Target.ToString(CultureInfo.InvariantCulture)}, guid: {prefab.Guid}, type: 3}}",
                    "      propertyPath: " + r.Path,
                    "      value: " + (r.Kind == 'r' ? "" : r.Value),
                    "      objectReference: " + (r.Kind == 'r' ? r.Ref.Yaml : "{fileID: 0}"),
                });
                changed.Add($"{r.Path}: prefab's {(r.Kind == 'r' ? r.DefaultRef.Yaml : r.Default)} to {(r.Kind == 'r' ? r.Ref.Yaml : r.Value)}");
            }
            return string.Join("\n", lines);
        }

        static bool Same(NavRecord r, string value, ObjRef reference)
        {
            switch (r.Kind)
            {
                case 'r': return r.Ref.SameTarget(reference);
                case 'f':
                    return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) &&
                           v == ParseFloat(r.Value);
                case 'i':
                    return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) &&
                           n == ParseInt(r.Value);
                default: return string.Equals(value ?? "", r.Value, StringComparison.Ordinal);
            }
        }

        // =====================================================================================
        //  the assets, as the editor holds them
        // =====================================================================================

        /// <summary>The painter's ShoreIso rock sprites (as <c>LoadRockSprites</c> picks them) and the
        /// Rock Px cells, through the AssetDatabase.</summary>
        public sealed class EditorShoreRockSprites : IShoreRockSprites
        {
            Dictionary<string, SpriteRef> _iso;

            public bool TryShoreIso(string spriteKey, out SpriteRef sprite)
            {
                if (_iso == null)
                {
                    _iso = new Dictionary<string, SpriteRef>(StringComparer.Ordinal);
                    foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(StPetersShorePainter.RockSheetPath))
                    {
                        if (!(obj is Sprite s)) continue;
                        foreach (string item in ShorelineIsoCatalog.RockSprites)
                            if (s.name.EndsWith("_" + item, StringComparison.Ordinal)) _iso[item] = RefOf(s);
                    }
                }
                return _iso.TryGetValue(spriteKey ?? "", out sprite);
            }

            public bool TryRockPx(ShoreRockDef def, out SpriteRef sprite)
            {
                string name = RockPxSpriteName(def);
                foreach (UnityEngine.Object obj in AssetDatabase.LoadAllAssetsAtPath(RockPxSheetPath(def)))
                    if (obj is Sprite s && s.name == name) { sprite = RefOf(s); return true; }
                sprite = default;
                return false;
            }

            public static SpriteRef RefOf(Sprite s)
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out string guid, out long localId))
                    throw new Refusal($"the sprite '{s.name}' is not an asset.");
                return new SpriteRef(new ObjRef(localId, guid, 3), s.rect.size / s.pixelsPerUnit);
            }
        }

        /// <summary>The NavBuoy prefab and the mark defs, as the placer loads them.</summary>
        public sealed class EditorNavMarkAssets : INavMarkAssets
        {
            readonly Dictionary<string, NavBuoyDef> _defs = NavMarkPlacer.LoadDefsByMarkType(StPetersNavMarks.DefFolder);
            NavPrefab _prefab;

            public NavPrefab Prefab => _prefab ?? (_prefab = LoadPrefab());

            public bool TryDef(string markType, out ObjRef def, out string lightText)
            {
                def = default;
                lightText = null;
                if (markType == null || !_defs.TryGetValue(markType, out NavBuoyDef d) || d == null) return false;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(d, out string guid, out long id)) return false;
                def = new ObjRef(id, guid, 2);
                lightText = d.LightText;
                return true;
            }

            static NavPrefab LoadPrefab()
            {
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(StPetersNavMarks.PrefabPath);
                if (root == null) throw new Refusal($"no prefab at {StPetersNavMarks.PrefabPath}.");
                var visual = root.GetComponent<NavBuoyVisual>();
                if (visual == null) throw new Refusal($"{StPetersNavMarks.PrefabPath} carries no NavBuoyVisual.");
                var so = new SerializedObject(visual);
                UnityEngine.Object def = so.FindProperty("_def").objectReferenceValue;
                ObjRef defRef = default;
                if (def != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(def, out string dg, out long di))
                    defRef = new ObjRef(di, dg, 2);
                return new NavPrefab
                {
                    Guid = AssetDatabase.AssetPathToGUID(StPetersNavMarks.PrefabPath),
                    GameObject = LocalId(root),
                    Transform = LocalId(root.transform),
                    Visual = LocalId(visual),
                    Name = root.name,
                    Position = root.transform.localPosition,
                    Def = defRef,
                    SizeId = so.FindProperty("_sizeId").stringValue,
                    Facing = so.FindProperty("_facing").intValue,
                    MarkId = so.FindProperty("_markId").stringValue,
                    Phase = so.FindProperty("_phaseFraction").floatValue,
                };
            }

            static long LocalId(UnityEngine.Object o) =>
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string _, out long id)
                    ? id
                    : throw new Refusal($"'{o.name}' is not part of an asset.");
        }

        // =====================================================================================
        //  the menu: plan against the scene FILE, write the patches, apply only when asked
        // =====================================================================================

        /// <summary>St Peters' ShoreRockDefs, by id.</summary>
        public static List<ShoreRockDef> LoadStPetersShoreRockDefs() =>
            AssetDatabase.FindAssets("t:" + nameof(ShoreRockDef))
                .Select(g => AssetDatabase.LoadAssetAtPath<ShoreRockDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null && d.Id != null && d.Id.StartsWith(StPetersRockIdPrefix, StringComparison.Ordinal))
                .OrderBy(d => d.Id, StringComparer.Ordinal)
                .ToList();

        /// <summary>All three steps against one scene, their new fileIDs drawn from one pool.</summary>
        public static List<LayerPatch> PlanAll(SceneYaml scene, ITidalTerrain terrain, IShoreRockSprites sprites,
                                               IReadOnlyList<ShoreRockDef> swaps, INavMarkAssets nav)
        {
            var ids = new IdAllocator(scene);
            return new List<LayerPatch>
            {
                Shoreline(scene, terrain, sprites, swaps, ids),
                ClamHoles(scene, terrain, ids),
                NavMarks(scene, terrain, nav, ids),
            };
        }

        [MenuItem("Hidden Harbours/World/St Peters Layer Refresh/Write the Three Patches (dry run)")]
        static void WritePatchesMenu() => RunFromMenu(apply: false);

        [MenuItem("Hidden Harbours/World/St Peters Layer Refresh/Apply the Three Patches to StPeters.unity")]
        static void ApplyPatchesMenu()
        {
            if (EditorUtility.DisplayDialog("St Peters layer refresh",
                    "Rewrite the Shoreline, ClamHoles and StPetersNavMarks roots in the StPeters.unity FILE?\n\n" +
                    "The scene must be closed. The patches are written to " + PatchFolder + " first.", "Apply", "Cancel"))
                RunFromMenu(apply: true);
        }

        static void RunFromMenu(bool apply)
        {
            try
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (apply && string.Equals(SceneManager.GetSceneAt(i).path, ScenePath, StringComparison.Ordinal))
                        throw new Refusal("StPeters is open in the editor. Close it first: an open scene would overwrite the file on its next save.");

                string text = File.ReadAllText(ScenePath);
                SceneYaml scene = SceneYaml.Parse(text);
                var terrainGo = new GameObject("StPetersLayerRefresh_Terrain") { hideFlags = HideFlags.HideAndDontSave };
                List<LayerPatch> patches;
                try
                {
                    var terrain = terrainGo.AddComponent<TidalTerrain>();
                    StPetersBuilder.ConfigureTidalTerrain(terrain);
                    patches = PlanAll(scene, terrain, new EditorShoreRockSprites(), LoadStPetersShoreRockDefs(), new EditorNavMarkAssets());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(terrainGo);
                }

                Directory.CreateDirectory(PatchFolder);
                var utf8 = new UTF8Encoding(false);
                foreach (LayerPatch p in patches)
                    File.WriteAllText(Path.Combine(PatchFolder, p.Step + ".patch.yaml"), p.ToYaml(), utf8);
                string summary = string.Join("\n", patches.Select(p => p.Summary()));

                if (apply)
                {
                    string result = text;
                    foreach (LayerPatch p in patches) result = p.ApplyTo(result);
                    if (result != text) File.WriteAllText(ScenePath, result, utf8);
                    Debug.Log($"[StPetersLayerRefresh] applied to {ScenePath} (patches in {PatchFolder}):\n{summary}");
                }
                else Debug.Log($"[StPetersLayerRefresh] dry run, nothing written to the scene (patches in {PatchFolder}):\n{summary}");
            }
            catch (Refusal r)
            {
                Debug.LogError(r.Message);
            }
        }
    }
}
#endif
