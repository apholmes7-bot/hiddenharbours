using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Builds one <see cref="SetPieceDef"/> per St Peters set piece, at
    /// <c>Assets/_Project/Data/SetPieces/SetPiece_&lt;key&gt;.asset</c>, from what was baked: the today
    /// file's gameplay block (bounds, footprints, facings, cells, anchors, the light rule, the vane), the
    /// bake contract (where each frame sits on its sheet) and the sliced sprites.
    ///
    /// <para><b>Nothing is typed in.</b> Every field is read. A member the kit adds that this builder does
    /// not know is a problem, never a silent drop; the members it knows and leaves off the Def are named in
    /// <see cref="GameplayMembers"/> with the reason.</para>
    ///
    /// <para><b>It builds only what the sheets were baked from.</b> The contract carries the today file's
    /// LF hash; a today file edited after the bake is refused until the pieces are re-baked.</para>
    /// </summary>
    public static class SetPieceDefBuilder
    {
        public const string DefFolder = "Assets/_Project/Data/SetPieces";
        public const string DefPrefix = "SetPiece_";

        public static string DefPath(string key) => $"{DefFolder}/{DefPrefix}{key}.asset";

        /// <summary>
        /// The gameplay block's members. Carried: id, name, scene, key, bounds, layer, walk, mount,
        /// colliders, facings and cells (per frame), anchors, options (as baked), light, animated. Checked,
        /// not carried: schema, generatedBy, defType. Left off: units and notes (prose for people).
        /// </summary>
        static readonly HashSet<string> GameplayMembers = new HashSet<string>(StringComparer.Ordinal)
        {
            "schema", "generatedBy", "key", "id", "name", "defType", "scene", "units", "bounds", "layer",
            "walk", "mount", "colliders", "facings", "cells", "anchors", "options", "light", "animated", "notes",
        };

        /// <summary>
        /// Anchor members that are not model points. <c>px</c> (and a word's <c>atPx</c>/<c>cornersPx</c>, a
        /// mark's <c>px</c>) are the kit's own projection at dir 4: derived, so the Def recomputes them
        /// (<see cref="SetPieceDef.AnchorPixel"/>) and a test holds the two equal.
        /// </summary>
        static readonly HashSet<string> NonPointAnchors = new HashSet<string>(StringComparer.Ordinal)
        {
            "px", "words", "marks", "path", "vaneHeadingDeg",
        };

        /// <summary>A word's members. <c>text</c> and <c>note</c> stay off: the boards bake blank and the
        /// strings table owns what they say.</summary>
        static readonly HashSet<string> WordMembers = new HashSet<string>(StringComparer.Ordinal)
        {
            "id", "text", "body", "at", "corners", "size", "note", "atPx", "cornersPx",
        };

        static readonly HashSet<string> MarkMembers = new HashSet<string>(StringComparer.Ordinal)
        {
            "z", "overDatum", "at", "label", "kind", "px",
        };

        static readonly HashSet<string> ColliderMembers = new HashSet<string>(StringComparer.Ordinal)
        {
            "type", "blocks", "pts",
        };

        static readonly HashSet<string> LightMembers = new HashSet<string>(StringComparer.Ordinal)
        {
            "emitters", "when",
        };

        /// <summary><c>source</c> is prose ("the game wind model …"); the input's name is what wires it.</summary>
        static readonly HashSet<string> AnimatedMembers = new HashSet<string>(StringComparer.Ordinal)
        {
            "part", "input", "source", "steps",
        };

        [MenuItem("Hidden Harbours/Art/Build St Peters Set Piece Defs", priority = 81)]
        public static void BuildMenu()
        {
            var problems = BuildAll();
            if (problems.Count > 0)
                Debug.LogError($"[SetPieceDefBuilder] {problems.Count} problem(s):\n  " + string.Join("\n  ", problems));
            else
                Debug.Log($"[SetPieceDefBuilder] {StPetersSetPieceKit.Pieces.Count} Defs in {DefFolder}.");
        }

        /// <summary>Headless entry (-executeMethod): exit 1 on ANY problem.</summary>
        public static void BuildFromCommandLine()
        {
            int code = 1;
            try
            {
                var problems = BuildAll();
                foreach (string p in problems) Debug.LogError("[SetPieceDefBuilder] " + p);
                code = problems.Count == 0 ? 0 : 1;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SetPieceDefBuilder] headless build failed: {ex}");
            }
            EditorApplication.Exit(code);
        }

        /// <summary>Builds or refreshes every Def in place (a Def keeps its GUID). Returns the problems.</summary>
        public static List<string> BuildAll()
        {
            var problems = new List<string>();

            var contract = SetPieceSheetSlicer.LoadContract(out string error);
            if (contract == null)
            {
                problems.Add(error);
                return problems;
            }

            object today;
            string todaySha;
            try
            {
                string todayText = StPetersSetPieceKit.ReadTodayText();
                today = DeckSidecarJson.Parse(todayText);
                StPetersSetPieceKit.CheckToday(today);
                todaySha = StPetersSetPieceKit.Sha256Hex(Encoding.UTF8.GetBytes(StPetersSetPieceKit.Lf(todayText)));
            }
            catch (Exception ex)
            {
                problems.Add($"{SetPieceSheetSlicer.TodayPath}: {ex.Message}");
                return problems;
            }

            if (contract.todaySha256Lf != todaySha)
                problems.Add($"the sheets were baked from a today file hashing {contract.todaySha256Lf}; " +
                             $"{SetPieceSheetSlicer.TodayPath} is now {todaySha}. Re-bake before building.");
            if (contract.kitVersion != StPetersSetPieceKit.Version)
                problems.Add($"the sheets were baked by kit {contract.kitVersion}; this intake is {StPetersSetPieceKit.Version}.");
            if (!Mathf.Approximately(contract.pixelsPerMetre, ArtImportPipeline.PixelsPerUnit))
                problems.Add($"the kit draws {contract.pixelsPerMetre} px per metre; sprites import at " +
                             $"{ArtImportPipeline.PixelsPerUnit}. A piece would place at the wrong scale.");

            var pieces = DeckSidecarJson.AsArray(DeckSidecarJson.Member(today, "pieces"));
            if (pieces == null || pieces.Count != StPetersSetPieceKit.Pieces.Count)
                problems.Add($"the today file lists {pieces?.Count ?? 0} pieces, not {StPetersSetPieceKit.Pieces.Count}.");
            if (contract.pieces.Count != StPetersSetPieceKit.Pieces.Count)
                problems.Add($"the contract lists {contract.pieces.Count} sheets, not {StPetersSetPieceKit.Pieces.Count}.");
            if (problems.Count > 0) return problems;

            EnsureFolder(DefFolder);

            int built = 0;
            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < pieces.Count; i++)
                {
                    string key = StPetersSetPieceKit.Pieces[i].Key, id = StPetersSetPieceKit.Pieces[i].Value;
                    var sheet = contract.pieces.FirstOrDefault(p => p.key == key);
                    if (sheet == null)
                    {
                        problems.Add($"{key}: no sheet in the contract.");
                        continue;
                    }
                    if (DeckSidecarJson.String(DeckSidecarJson.Member(pieces[i], "key")) != key ||
                        DeckSidecarJson.String(DeckSidecarJson.Member(pieces[i], "id")) != id)
                    {
                        problems.Add($"today piece {i} is not {key} ({id}).");
                        continue;
                    }
                    if (BuildDef(key, id, DeckSidecarJson.Member(pieces[i], "gameplay"), sheet, contract, problems))
                        built++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }

            if (built != pieces.Count)
                problems.Add($"built {built} of {pieces.Count} Defs.");
            return problems;
        }

        static bool BuildDef(string key, string id, object g, SetPieceSheetSlicer.Piece sheet,
                             SetPieceSheetSlicer.Contract contract, List<string> problems)
        {
            int before = problems.Count;
            void Problem(string p) => problems.Add($"{key}: {p}");

            OnlyKnown(g, "the gameplay block", GameplayMembers, Problem);
            if (Str(g, "schema") != StPetersSetPieceKit.SidecarSchema) Problem($"schema is '{Str(g, "schema")}'.");
            if (Str(g, "generatedBy") != $"{StPetersSetPieceKit.GlobalName} {StPetersSetPieceKit.Version}")
                Problem($"generated by '{Str(g, "generatedBy")}'.");
            if (Str(g, "key") != key || Str(g, "id") != id) Problem($"the block is {Str(g, "key")} ({Str(g, "id")}).");
            if (Str(g, "defType") != StPetersSetPieceKit.DefType) Problem($"defType is '{Str(g, "defType")}'.");

            var byName = AssetDatabase.LoadAllAssetsAtPath(SetPieceSheetSlicer.SheetPath(key))
                                      .OfType<Sprite>()
                                      .ToDictionary(s => s.name, StringComparer.Ordinal);
            if (byName.Count != sheet.cells)
            {
                Problem($"{byName.Count} sprites on {SetPieceSheetSlicer.SheetPath(key)}, expected {sheet.cells}. " +
                        "Slice the sheets before building Defs.");
                return false;
            }

            var facings = DeckSidecarJson.AsArray(DeckSidecarJson.Member(g, "facings"));
            var cells = DeckSidecarJson.AsArray(DeckSidecarJson.Member(g, "cells"));
            if (facings == null || facings.Count != SetPieceDef.Facings || cells == null || cells.Count != SetPieceDef.Facings)
            {
                Problem($"the block needs {SetPieceDef.Facings} facings and {SetPieceDef.Facings} cells.");
                return false;
            }
            for (int d = 0; d < SetPieceDef.Facings; d++)
                if (!TryInt(DeckSidecarJson.Member(facings[d], "dir"), out int fd) || fd != d ||
                    !TryInt(DeckSidecarJson.Member(cells[d], "dir"), out int cd) || cd != d)
                    Problem($"facings[{d}] or cells[{d}] is not dir {d}.");

            var frames = new SetPieceFrame[SetPieceDef.Facings];
            for (int k = 0; k < SetPieceDef.Facings; k++)
            {
                int dir = SetPieceDef.RigDirForFrame(k);
                var f = sheet.frames[k];
                if (f.rigDir != dir || f.heading != -1) Problem($"contract frame {k} is rig dir {f.rigDir}, not {dir}.");
                frames[k] = MakeFrame(sheet, f, facings[dir], cells[dir], byName, Problem);
            }

            var bounds = DeckSidecarJson.Member(g, "bounds");
            bool boundsOk = TryVec2(DeckSidecarJson.Member(bounds, "x"), out var bx);
            boundsOk &= TryVec2(DeckSidecarJson.Member(bounds, "y"), out var by);
            boundsOk &= TryVec2(DeckSidecarJson.Member(bounds, "z"), out var bz);
            if (!boundsOk) Problem("bounds need x, y and z as [min, max].");

            var colliders = new List<SetPieceCollider>();
            foreach (object c in DeckSidecarJson.AsArray(DeckSidecarJson.Member(g, "colliders")) ?? new List<object>())
            {
                OnlyKnown(c, "a collider", ColliderMembers, Problem);
                var pts = new List<Vector2>();
                foreach (object p in DeckSidecarJson.AsArray(DeckSidecarJson.Member(c, "pts")) ?? new List<object>())
                    if (TryVec2(p, out var v)) pts.Add(v);
                    else Problem("a collider point is not [x, y].");
                if (pts.Count < 3) Problem($"a collider has {pts.Count} points.");
                colliders.Add(new SetPieceCollider { Type = Str(c, "type") ?? "", Blocks = Str(c, "blocks") ?? "", Points = pts.ToArray() });
            }

            var options = DeckSidecarJson.Member(g, "options");
            object bakedOptions = null;
            try { bakedOptions = DeckSidecarJson.Parse(sheet.optionsJson ?? ""); }
            catch (FormatException) { }
            if (!JsonEquals(bakedOptions, options))
                Problem("the contract's options are not the gameplay block's: the sheets and the block disagree on what was baked.");

            // ---- anchors ------------------------------------------------------------------------------
            var anchorsObj = DeckSidecarJson.AsObject(DeckSidecarJson.Member(g, "anchors"));
            var points = new List<SetPieceAnchor>();
            var words = new List<SetPieceWordsAnchor>();
            var marks = new List<SetPieceTideMark>();
            var path = new List<Vector2>();
            object vaneHeading = null;
            if (anchorsObj == null) Problem("the block has no anchors object.");
            else
                foreach (var kv in anchorsObj)
                {
                    if (!NonPointAnchors.Contains(kv.Key))
                    {
                        if (TryVec3(kv.Value, out var p)) points.Add(new SetPieceAnchor { Name = kv.Key, Position = p });
                        else Problem($"anchor '{kv.Key}' is neither a model point nor a member this builder knows.");
                        continue;
                    }
                    switch (kv.Key)
                    {
                        case "words":
                            foreach (object w in DeckSidecarJson.AsArray(kv.Value) ?? new List<object>())
                                words.Add(MakeWords(w, Problem));
                            break;
                        case "marks":
                            foreach (object m in DeckSidecarJson.AsArray(kv.Value) ?? new List<object>())
                                marks.Add(MakeMark(m, Problem));
                            break;
                        case "path":
                            foreach (object p in DeckSidecarJson.AsArray(kv.Value) ?? new List<object>())
                                if (TryVec2(p, out var v)) path.Add(v);
                                else Problem("a path point is not [x, y].");
                            break;
                        case "vaneHeadingDeg":
                            vaneHeading = kv.Value;
                            break;
                    }
                }
            points.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

            // ---- light ---------------------------------------------------------------------------------
            var lightObj = DeckSidecarJson.Member(g, "light");
            var light = new SetPieceLightRule();
            if (lightObj != null)
            {
                OnlyKnown(lightObj, "the light rule", LightMembers, Problem);
                var emitters = (DeckSidecarJson.AsArray(DeckSidecarJson.Member(lightObj, "emitters")) ?? new List<object>())
                               .Select(DeckSidecarJson.String).ToArray();
                if (emitters.Length == 0 || emitters.Any(string.IsNullOrEmpty)) Problem("the light rule names no emitter.");
                light.Emitters = emitters.Select(e => e ?? "").ToArray();
                light.When = Str(lightObj, "when") ?? "";
            }

            // ---- the animated part ---------------------------------------------------------------------
            var animatedObj = DeckSidecarJson.Member(g, "animated");
            var animated = new SetPieceAnimatedPart();
            if (animatedObj != null)
            {
                OnlyKnown(animatedObj, "the animated part", AnimatedMembers, Problem);
                animated.Part = Str(animatedObj, "part") ?? "";
                animated.Input = Str(animatedObj, "input") ?? "";
                if (!TryInt(DeckSidecarJson.Member(animatedObj, "steps"), out int steps) || steps <= 0)
                    Problem("the animated part has no step count.");
                animated.Steps = steps;
                animated.RigDir = StPetersSetPieceKit.HeadingRigDir;
                if (!DeckSidecarJson.TryDouble(DeckSidecarJson.Member(options, animated.Input), out double at))
                    Problem($"the options carry no '{animated.Input}', the input the part turns with.");
                animated.BakedAtDeg = (float)at;
                if (vaneHeading != null && (!DeckSidecarJson.TryDouble(vaneHeading, out double vh) || vh != at))
                    Problem($"anchors.vaneHeadingDeg is not options.{animated.Input}.");
                if (sheet.headings != steps)
                    Problem($"the sheet bakes {sheet.headings} headings; the part has {steps} steps.");

                var headings = new SetPieceFrame[Math.Max(0, sheet.headings)];
                for (int s = 0; s < headings.Length; s++)
                {
                    var f = sheet.frames[SetPieceDef.Facings + s];
                    if (f.heading != s || f.rigDir != animated.RigDir)
                        Problem($"contract frame {SetPieceDef.Facings + s} is heading {f.heading} at dir {f.rigDir}.");
                    headings[s] = MakeFrame(sheet, f, facings[animated.RigDir], null, byName, Problem);
                }
                animated.Headings = headings;
            }
            else
            {
                if (vaneHeading != null) Problem("anchors.vaneHeadingDeg on a piece with no animated part.");
                if (sheet.headings != 0) Problem($"the sheet bakes {sheet.headings} headings for a piece that does not move.");
            }

            if (problems.Count > before) return false;

            string defPath = DefPath(key);
            var def = AssetDatabase.LoadAssetAtPath<SetPieceDef>(defPath);
            bool created = def == null;
            if (created) def = ScriptableObject.CreateInstance<SetPieceDef>();

            def.Id = id;
            def.DisplayName = Str(g, "name") ?? "";
            def.RigKey = key;
            def.SceneId = Str(g, "scene") ?? "";
            def.BakedOptionsJson = sheet.optionsJson;
            def.Layer = Str(g, "layer") ?? "";
            def.Walk = Str(g, "walk") ?? "";
            def.Mount = Str(g, "mount") ?? "";
            def.BoundsMin = new Vector3(bx.x, by.x, bz.x);
            def.BoundsMax = new Vector3(bx.y, by.y, bz.y);
            def.Frames = frames;
            def.CellSize = new Vector2Int(sheet.cellW, sheet.cellH);
            def.PixelsPerMetre = contract.pixelsPerMetre;
            def.ElevationDeg = contract.elevationDeg;
            def.BakedLit = contract.bakedLit;
            def.Colliders = colliders.ToArray();
            def.Anchors = points.ToArray();
            def.Path = path.ToArray();
            def.TideMarks = marks.ToArray();
            def.Words = words.ToArray();
            def.HasLight = lightObj != null;
            def.Light = light;
            def.IsAnimated = animatedObj != null;
            def.Animated = animated;

            if (created) AssetDatabase.CreateAsset(def, defPath);
            else EditorUtility.SetDirty(def);
            return true;
        }

        static SetPieceFrame MakeFrame(SetPieceSheetSlicer.Piece sheet, SetPieceSheetSlicer.Frame f, object facing,
                                       object cell, IReadOnlyDictionary<string, Sprite> byName, Action<string> problem)
        {
            string name = SetPieceSheetSlicer.SpriteName(sheet.key, f.cell);
            if (!byName.TryGetValue(name, out var sprite))
                problem($"no sprite named {name}.");
            else
            {
                var r = sprite.rect;
                Vector2 wantPivot = new Vector2(f.pivotX, sheet.cellH - f.pivotY);
                if (Mathf.RoundToInt(r.width) != sheet.cellW || Mathf.RoundToInt(r.height) != sheet.cellH)
                    problem($"{name} is {r.width}×{r.height}, not the {sheet.cellW}×{sheet.cellH} cell.");
                if ((sprite.pivot - wantPivot).sqrMagnitude > 1e-4f)
                    problem($"{name}'s pivot is {sprite.pivot}, not the frame's ground point {wantPivot}.");
            }

            // A facing frame's native cell must be the one the gameplay block names; a heading's cell was
            // proven against the kit at its own heading when it was baked.
            if (cell != null)
            {
                var pivot = DeckSidecarJson.AsArray(DeckSidecarJson.Member(cell, "pivot"));
                if (!TryInt(DeckSidecarJson.Member(cell, "w"), out int w) || w != f.nativeW ||
                    !TryInt(DeckSidecarJson.Member(cell, "h"), out int h) || h != f.nativeH ||
                    pivot == null || pivot.Count != 2 ||
                    !TryInt(pivot[0], out int px) || px != f.nativePivotX ||
                    !TryInt(pivot[1], out int py) || py != f.nativePivotY)
                    problem($"frame {f.cell} (dir {f.rigDir}) is not the block's cell for that dir.");
            }

            return new SetPieceFrame
            {
                Sprite = sprite,
                RigDir = f.rigDir,
                ShowFaces = Str(facing, "showFaces") ?? "",
                BearingDeg = DeckSidecarJson.Float(DeckSidecarJson.Member(facing, "bearingDeg")),
                CameraSees = Str(facing, "cameraSees") ?? "",
                NativeSize = new Vector2Int(f.nativeW, f.nativeH),
                NativePivot = new Vector2Int(f.nativePivotX, f.nativePivotY),
                Crop = new Vector2Int(f.cropX, f.cropY),
                Pivot = new Vector2Int(f.pivotX, f.pivotY),
            };
        }

        static SetPieceWordsAnchor MakeWords(object w, Action<string> problem)
        {
            OnlyKnown(w, "a words anchor", WordMembers, problem);
            var words = new SetPieceWordsAnchor
            {
                Id = Str(w, "id") ?? "",
                BodyId = Str(w, "body") ?? "",
                Size = DeckSidecarJson.Float(DeckSidecarJson.Member(w, "size")),
            };
            if (string.IsNullOrEmpty(words.Id)) problem("a words anchor has no string id.");

            object corners = DeckSidecarJson.Member(w, "corners"), at = DeckSidecarJson.Member(w, "at");
            if ((corners == null) == (at == null))
                problem($"{words.Id} needs corners or a point, exactly one.");
            if (corners != null)
            {
                var list = new List<Vector3>();
                foreach (object c in DeckSidecarJson.AsArray(corners) ?? new List<object>())
                    if (TryVec3(c, out var v)) list.Add(v);
                    else problem($"{words.Id}: a corner is not [x, y, z].");
                if (list.Count != 4) problem($"{words.Id} has {list.Count} corners, not 4.");
                words.Corners = list.ToArray();
            }
            if (at != null)
            {
                if (!TryVec3(at, out var p)) problem($"{words.Id}: its point is not [x, y, z].");
                words.HasPoint = true;
                words.Point = p;
            }
            return words;
        }

        static SetPieceTideMark MakeMark(object m, Action<string> problem)
        {
            OnlyKnown(m, "a tide mark", MarkMembers, problem);
            bool ok = DeckSidecarJson.TryDouble(DeckSidecarJson.Member(m, "z"), out double z);
            ok &= DeckSidecarJson.TryDouble(DeckSidecarJson.Member(m, "overDatum"), out double over);
            ok &= TryVec3(DeckSidecarJson.Member(m, "at"), out var at);
            if (!ok) problem("a tide mark needs z, overDatum and at.");
            string kind = Str(m, "kind");
            if (string.IsNullOrEmpty(kind)) problem("a tide mark has no kind.");
            return new SetPieceTideMark
            {
                Z = (float)z, OverDatum = (float)over, At = at,
                Label = Str(m, "label") ?? "", Kind = kind ?? "",
            };
        }

        // ---- reading --------------------------------------------------------------------------------------

        static string Str(object owner, string key) => DeckSidecarJson.String(DeckSidecarJson.Member(owner, key));

        static void OnlyKnown(object obj, string where, ICollection<string> known, Action<string> problem)
        {
            var o = DeckSidecarJson.AsObject(obj);
            if (o == null)
            {
                problem($"{where} is not an object.");
                return;
            }
            foreach (string k in o.Keys)
                if (!known.Contains(k))
                    problem($"{where} carries '{k}', which this builder does not know. The kit grew a field: " +
                            "give the Def a place for it before building, never drop it.");
        }

        static bool TryInt(object v, out int i)
        {
            i = 0;
            if (!DeckSidecarJson.TryDouble(v, out double d) || d != Math.Floor(d) || Math.Abs(d) > int.MaxValue) return false;
            i = (int)d;
            return true;
        }

        static bool TryVec2(object v, out Vector2 p)
        {
            p = default;
            var a = DeckSidecarJson.AsArray(v);
            if (a == null || a.Count != 2 ||
                !DeckSidecarJson.TryDouble(a[0], out double x) || !DeckSidecarJson.TryDouble(a[1], out double y))
                return false;
            p = new Vector2((float)x, (float)y);
            return true;
        }

        static bool TryVec3(object v, out Vector3 p)
        {
            p = default;
            var a = DeckSidecarJson.AsArray(v);
            if (a == null || a.Count != 3 ||
                !DeckSidecarJson.TryDouble(a[0], out double x) || !DeckSidecarJson.TryDouble(a[1], out double y) ||
                !DeckSidecarJson.TryDouble(a[2], out double z))
                return false;
            p = new Vector3((float)x, (float)y, (float)z);
            return true;
        }

        /// <summary>Structural equality of two parsed documents: same members, same order-free keys, equal
        /// numbers and strings, arrays in order.</summary>
        static bool JsonEquals(object a, object b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (a is Dictionary<string, object> oa && b is Dictionary<string, object> ob)
                return oa.Count == ob.Count && oa.All(kv => ob.TryGetValue(kv.Key, out object v) && JsonEquals(kv.Value, v));
            if (a is List<object> la && b is List<object> lb)
                return la.Count == lb.Count && la.Zip(lb, JsonEquals).All(x => x);
            return a.Equals(b);
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            string parent = folder.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
        }
    }
}
