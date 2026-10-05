#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using HiddenHarbours.Art;                 // CliffWallGeometry.SortY, YSortSprite.OrderFor: how a wall sorts
using HiddenHarbours.Art.Editor;          // CliffCatalog: the aspects and batters a face is baked at
using HiddenHarbours.Core;                // SortingBands: the decor band a wall sorts in
using HiddenHarbours.World;               // CliffWallDef: the walls by real id

namespace HiddenHarbours.App.Editor
{
    public static partial class StPetersLayerRefresh
    {
        // =====================================================================================
        //  CLIFF WALLS: St Peters' walls by their real ids (terrain PR 5w)
        // =====================================================================================

        public const string CliffWallsStep = "CliffWalls";

        /// <summary>The fields a wall's face takes from its aspect and batter: the same text on every wall that
        /// wears that face.</summary>
        static readonly string[] WallFaceFields = { "_bands", "_profile", "_browDecal", "_toeDecal", "_bakeLight" };

        /// <summary>The fields every wall writes alike: the material and the kit's scalars.</summary>
        static readonly string[] WallKitFields =
            { "_material", "_faceMetresS", "_faceMetresT", "_subdivideMetres", "_profileMetres", "_stripMetresT", "_browLineAt" };

        /// <summary>
        /// ⭐ <b>THE WALLS BY THEIR REAL IDS.</b> The <c>CliffWalls</c> root as the walls' Defs have it, written by
        /// text, never by a rebuild (St Peters cannot be rebuilt). The scene's walls are matched to their Defs by the
        /// last three digits of their names:
        /// <list type="bullet">
        /// <item>a live Def whose wall stands is edited in place, its fileIDs kept: its name, its place (its first
        /// brow), its lines, its offset along the run and the run's row basis, its azimuth and batter, and, where its
        /// face changed, the face's fields;</item>
        /// <item>a live Def with no wall yet is its parent's (<see cref="CliffWallDef.SplitFrom"/>) documents copied
        /// under new fileIDs, then edited the same way;</item>
        /// <item>a retired Def's wall is deleted, its four documents each named;</item>
        /// <item>the root lists its walls run by run, in the order of each run's least id.</item>
        /// </list>
        /// A wall's sorting order is written only where its lines moved; a wall standing on the lines it had keeps
        /// the order it has. The face's fields come from a wall in the scene that already wears that face, and the
        /// step refuses if two walls wearing one face disagree on them, or a Def wears a face no wall does. It refuses
        /// a scene wall with no Def, or a held one's, and a re-order of the walls that stay. Re-planned on its own
        /// result, it finds nothing to do.
        /// </summary>
        public static LayerPatch CliffWalls(SceneYaml scene, IReadOnlyList<CliffWallDef> defs)
        {
            string rootName = StPetersCliffWalls.RootName;
            Doc root = scene.RootNamed(rootName);
            Doc rootTr = scene.TransformOf(root);
            RequireIdentity(rootTr, rootName);
            var patch = new LayerPatch(CliffWallsStep, rootName, root.FileId);

            List<StPetersCliffWalls.SceneWall> walls;
            List<StPetersCliffWalls.Chunk> chunks;
            List<CliffWallDef> owners;
            try
            {
                walls = StPetersCliffWalls.WallsUnder(scene);
                chunks = StPetersCliffWalls.ChunksOfDefs(defs, out owners);
            }
            catch (InvalidOperationException e)
            {
                throw new Refusal($"{CliffWallsStep}: {e.Message}");
            }
            var byId = walls.ToDictionary(w => w.RealId, StringComparer.Ordinal);

            var defById = new Dictionary<string, CliffWallDef>(StringComparer.Ordinal);
            foreach (CliffWallDef d in defs)
            {
                if (d == null) continue;
                if (defById.ContainsKey(d.RealId)) throw new Refusal($"{CliffWallsStep}: two Defs are wall {d.RealId}.");
                defById.Add(d.RealId, d);
            }
            foreach (StPetersCliffWalls.SceneWall w in walls)
            {
                if (!defById.TryGetValue(w.RealId, out CliffWallDef d))
                    throw new Refusal($"{CliffWallsStep}: {w.Name} has no Def.");
                if (d.Status == CliffWallStatus.Held)
                    throw new Refusal($"{CliffWallsStep}: {w.Name} stands in the scene, but {d.Id} is held: it stands in no scene.");
            }

            // What each face, and every wall, writes: read off the scene's own walls.
            var faces = new Dictionary<string, Doc>(StringComparer.Ordinal);
            Doc kit = null;
            foreach (StPetersCliffWalls.SceneWall w in walls)
            {
                Doc surface = scene.Get(w.Surface);
                string face = WallFace(w.AspectIndex, w.BatterIndex);
                if (faces.TryGetValue(face, out Doc donor)) RequireSameFields(donor, surface, WallFaceFields, $"the {face} walls");
                else faces.Add(face, surface);
                if (kit == null) kit = surface;
                else RequireSameFields(kit, surface, WallKitFields, "the walls");
            }

            var ids = new IdAllocator(scene);
            var children = new List<long>(chunks.Count);
            var staying = new List<string>(walls.Count);
            for (int k = 0; k < chunks.Count; k++)
            {
                StPetersCliffWalls.Chunk chunk = chunks[k];
                CliffWallDef d = owners[k];
                StPetersCliffWalls.ChunkFields f = StPetersCliffWalls.FieldsOf(in chunk);
                string name = StPetersCliffWalls.WallName(d);
                string face = WallFace(chunk.AspectIndex, chunk.BatterIndex);
                if (!faces.TryGetValue(face, out Doc faceDonor))
                    throw new Refusal($"{CliffWallsStep}: {d.Id} wears {face}, which no wall in the scene wears: no face to copy.");
                int order = YSortSprite.OrderFor(CliffWallGeometry.SortY(chunk.Samples), SortingBands.DecorBase,
                                                 SortingBands.OrdersPerMetre, SortingBands.DecorFloor, SortingBands.DecorCeiling);

                if (byId.TryGetValue(d.RealId, out StPetersCliffWalls.SceneWall w))
                {
                    Doc go = scene.Get(w.GameObject), tr = scene.Get(w.Transform), group = scene.Get(w.SortingGroup),
                        surface = scene.Get(w.Surface);
                    bool moved = !SameLines(w, f);
                    if (!moved && order != w.SortingOrder)
                        patch.Note(name, "keeps its sorting order", $"{w.SortingOrder}; its lines would give {order}");
                    string goAfter = SetField(go.Text, "m_Name", Plain(name));
                    string trAfter = SetField(tr.Text, "m_LocalPosition", WallPlace(f.BrowPlan[0]));
                    string groupAfter = moved ? SetField(group.Text, "m_SortingOrder", Int(order)) : group.Text;
                    string surfaceAfter = WallSurface(surface.Text, f, faceDonor);
                    patch.Edit(go, goAfter, $"{name} GameObject");
                    patch.Edit(tr, trAfter, $"{name} Transform");
                    patch.Edit(group, groupAfter, $"{name} SortingGroup");
                    patch.Edit(surface, surfaceAfter, $"{name} CliffWallSurface");
                    var what = new List<string>();
                    if (goAfter != go.Text) what.Add($"renamed from {w.Name}");
                    if (moved) what.Add($"its lines ({f.BrowPlan.Length} stations, from {w.Brow.Length})");
                    if (trAfter != tr.Text) what.Add("its place");
                    if (groupAfter != group.Text) what.Add($"its sorting order {w.SortingOrder} → {order}");
                    if (!WallFaceFields.All(key => WallFieldBlock(surface.Text, key) == WallFieldBlock(surfaceAfter, key)))
                        what.Add($"its face, to {face}");
                    if (f.AlongOffsetMetres != w.AlongOffsetMetres)
                        what.Add($"along {WallFloat(w.AlongOffsetMetres)} → {WallFloat(f.AlongOffsetMetres)}");
                    if (f.RowsBasisSurfaceMetres != w.RowsBasisSurfaceMetres)
                        what.Add($"rows {WallFloat(w.RowsBasisSurfaceMetres)} → {WallFloat(f.RowsBasisSurfaceMetres)}");
                    if (f.WallAzimuth != w.WallAzimuth)
                        what.Add($"azimuth {WallFloat(w.WallAzimuth)} → {WallFloat(f.WallAzimuth)}");
                    if (f.Batter != w.Batter) what.Add($"batter {WallFloat(w.Batter)} → {WallFloat(f.Batter)}");
                    if (what.Count > 0) patch.Note(name, $"{d.Id} ({d.Status})", string.Join("; ", what));
                    children.Add(w.Transform);
                    staying.Add(d.RealId);
                }
                else
                {
                    string parentId = string.IsNullOrEmpty(d.SplitFrom) ? "" : d.SplitFrom.Substring(Math.Max(0, d.SplitFrom.Length - 3));
                    if (!byId.TryGetValue(parentId, out StPetersCliffWalls.SceneWall parent))
                        throw new Refusal($"{CliffWallsStep}: {d.Id} has no wall, and is cut from none that stands ('{d.SplitFrom}').");
                    var map = new Dictionary<long, long>
                    {
                        [parent.GameObject] = ids.Next($"{CliffWallsStep}/{d.Id}/GameObject"),
                        [parent.Transform] = ids.Next($"{CliffWallsStep}/{d.Id}/Transform"),
                        [parent.SortingGroup] = ids.Next($"{CliffWallsStep}/{d.Id}/SortingGroup"),
                        [parent.Surface] = ids.Next($"{CliffWallsStep}/{d.Id}/Surface"),
                    };
                    patch.Add(SetField(Remap(scene.Get(parent.GameObject).Text, map), "m_Name", Plain(name)), $"{name} GameObject");
                    patch.Add(SetField(Remap(scene.Get(parent.Transform).Text, map), "m_LocalPosition", WallPlace(f.BrowPlan[0])),
                              $"{name} Transform");
                    patch.Add(SetField(Remap(scene.Get(parent.SortingGroup).Text, map), "m_SortingOrder", Int(order)), $"{name} SortingGroup");
                    patch.Add(WallSurface(Remap(scene.Get(parent.Surface).Text, map), f, faceDonor), $"{name} CliffWallSurface");
                    patch.Note(name, $"{d.Id} ({d.Status})",
                               $"added, cut from {parent.Name}: {f.BrowPlan.Length} stations, along {WallFloat(f.AlongOffsetMetres)}, " +
                               $"rows {WallFloat(f.RowsBasisSurfaceMetres)}, sorting order {order}");
                    children.Add(map[parent.Transform]);
                }
            }

            foreach (StPetersCliffWalls.SceneWall w in walls)
            {
                CliffWallDef d = defById[w.RealId];
                if (d.Status != CliffWallStatus.Retired) continue;
                patch.Delete(scene.Get(w.GameObject), $"{w.Name} GameObject (retired: {d.Id})");
                patch.Delete(scene.Get(w.Transform), $"{w.Name} Transform (retired: {d.Id})");
                patch.Delete(scene.Get(w.SortingGroup), $"{w.Name} SortingGroup (retired: {d.Id})");
                patch.Delete(scene.Get(w.Surface), $"{w.Name} CliffWallSurface (retired: {d.Id})");
                patch.Note(w.Name, $"{d.Id} (Retired)", "deleted");
            }

            List<string> before = walls.Select(w => w.RealId).Where(staying.Contains).ToList();
            if (!before.SequenceEqual(staying))
                throw new Refusal($"{CliffWallsStep}: the walls that stay would change their order under '{rootName}': " +
                                  $"{string.Join(" ", before)} became {string.Join(" ", staying)}.");
            patch.Edit(rootTr, SetRefList(rootTr.Text, "m_Children", children), $"{rootName} Transform's children");

            // The new chunks go in after the walls' own last document, not at the file's tail: the key scenes' block,
            // which its step wrote after the walls, stays the last, where that step writes it.
            HashSet<long> mine = scene.SubtreeOf(root.FileId);
            foreach (Doc d in scene.Docs)
                if (mine.Contains(d.FileId)) patch.AddAfter = d.FileId;
            return patch.Seal(scene);
        }

        static string WallFace(int aspectIndex, int bakedBatterIndex) =>
            $"{CliffCatalog.Aspects[aspectIndex]} {CliffCatalog.Batters[StPetersCliffWalls.BakedBatterToCatalogIndex(bakedBatterIndex)]}";

        /// <summary>The wall stands on the lines it had, to the bit.</summary>
        static bool SameLines(StPetersCliffWalls.SceneWall w, StPetersCliffWalls.ChunkFields f)
        {
            int n = f.BrowPlan.Length;
            if (w.Brow.Length != n) return false;
            for (int i = 0; i < n; i++)
                if (!w.Brow[i].Equals(f.BrowPlan[i]) || !w.Toe[i].Equals(f.ToePlan[i]) ||
                    w.DropMetres[i] != f.DropMetres[i] || w.ToeElevations[i] != f.ToeElevations[i])
                    return false;
            return true;
        }

        /// <summary>A surface's text with the chunk's lines and scalars, and the face's fields as the donor has them.</summary>
        static string WallSurface(string text, StPetersCliffWalls.ChunkFields f, Doc faceDonor)
        {
            text = WithWallFieldBlock(text, "_browPlan", WallList("_browPlan", f.BrowPlan.Select(WallVec2)));
            text = WithWallFieldBlock(text, "_toePlan", WallList("_toePlan", f.ToePlan.Select(WallVec2)));
            text = WithWallFieldBlock(text, "_dropMetres", WallList("_dropMetres", f.DropMetres.Select(WallFloat)));
            text = WithWallFieldBlock(text, "_toeElevations", WallList("_toeElevations", f.ToeElevations.Select(WallFloat)));
            text = SetField(text, "_alongOffsetMetres", WallFloat(f.AlongOffsetMetres));
            text = SetField(text, "_rowsBasisSurfaceMetres", WallFloat(f.RowsBasisSurfaceMetres));
            foreach (string key in WallFaceFields) text = WithWallFieldBlock(text, key, WallFieldBlock(faceDonor.Text, key));
            text = SetField(text, "_wallAzimuth", WallFloat(f.WallAzimuth));
            text = SetField(text, "_batter", WallFloat(f.Batter));
            return text;
        }

        static void RequireSameFields(Doc a, Doc b, string[] keys, string what)
        {
            foreach (string key in keys)
                if (WallFieldBlock(a.Text, key) != WallFieldBlock(b.Text, key))
                    throw new Refusal($"{CliffWallsStep}: {what} disagree on {key} (&{a.FileId}, &{b.FileId}).");
        }

        static string WallPlace(Vector2 brow0) => $"{{x: {WallFloat(brow0.x)}, y: {WallFloat(brow0.y)}, z: 0}}";

        static string WallVec2(Vector2 v) => $"{{x: {WallFloat(v.x)}, y: {WallFloat(v.y)}}}";

        static string WallList(string key, IEnumerable<string> values)
        {
            var lines = values.Select(v => "  - " + v).ToList();
            return lines.Count == 0 ? $"  {key}: []" : $"  {key}:\n" + string.Join("\n", lines);
        }

        /// <summary>
        /// A float as Unity writes it into a scene: the shortest digits that read back to the same float, and never an
        /// exponent (Unity writes 2.9802322e-8 as <c>0.000000029802322</c>, which <see cref="F"/>'s "G" format does not).
        /// </summary>
        static string WallFloat(float v)
        {
            string s = ShortestDigits(v);
            int e = s.IndexOf('E');
            if (e < 0) return s;
            bool negative = s[0] == '-';
            string mantissa = s.Substring(negative ? 1 : 0, e - (negative ? 1 : 0));
            int exponent = int.Parse(s.Substring(e + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
            int dot = mantissa.IndexOf('.');
            string digits = dot < 0 ? mantissa : mantissa.Remove(dot, 1);
            int point = (dot < 0 ? mantissa.Length : dot) + exponent;
            string plain = point <= 0 ? "0." + new string('0', -point) + digits
                         : point >= digits.Length ? digits + new string('0', point - digits.Length)
                         : digits.Substring(0, point) + "." + digits.Substring(point);
            return (negative ? "-" : "") + plain;
        }

        /// <summary>
        /// The fewest significant digits that read back to the float, rounded once from its exact value. The digits are
        /// the double's: the editor's Mono rounds a float to 9 digits before it rounds to fewer, so its "G8" of
        /// 189.2016449 is 189.20165 where Unity writes 189.20164, though both read back to the same float. A kept wall
        /// would then change its text and not its value.
        /// </summary>
        static string ShortestDigits(float v)
        {
            if (v == 0f || float.IsNaN(v) || float.IsInfinity(v)) return F(v);
            for (int digits = 1; digits <= 9; digits++)
            {
                string s = ((double)v).ToString("G" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                if (ParseFloat(s) == v) return s;
            }
            return F(v);
        }

        /// <summary>A field's lines in a document: <c>  key: value</c>, or <c>  key:</c> and its entries (the lines under
        /// it that start "  - " or go deeper).</summary>
        static string WallFieldBlock(string docText, string key) =>
            string.Join("\n", WallFieldLines(docText.Split('\n'), key, out int at, out int end), at, end - at);

        static string WithWallFieldBlock(string docText, string key, string block)
        {
            var lines = new List<string>(docText.Split('\n'));
            WallFieldLines(lines.ToArray(), key, out int at, out int end);
            lines.RemoveRange(at, end - at);
            lines.InsertRange(at, block.Split('\n'));
            return string.Join("\n", lines);
        }

        static string[] WallFieldLines(string[] lines, string key, out int at, out int end)
        {
            string prefix = "  " + key + ":";
            at = -1;
            for (int i = 1; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith(prefix, StringComparison.Ordinal)) continue;
                if (lines[i].Length != prefix.Length && lines[i][prefix.Length] != ' ') continue;
                if (at >= 0) throw new Refusal($"'{key}' appears twice in {lines[0]}.");
                at = i;
            }
            if (at < 0) throw new Refusal($"no '{key}' in {lines[0]}.");
            end = at + 1;
            while (end < lines.Length && (lines[end].StartsWith("  - ", StringComparison.Ordinal) ||
                                          lines[end].StartsWith("    ", StringComparison.Ordinal)))
                end++;
            return lines;
        }

        // =====================================================================================
        //  the menu: plan against the scene FILE, write the patch, apply only when asked
        // =====================================================================================

        [MenuItem("Hidden Harbours/World/St Peters Layer Refresh/Write the Cliff Walls Patch (dry run)")]
        static void WriteCliffWallsMenu() => CliffWallsFromMenu(apply: false);

        [MenuItem("Hidden Harbours/World/St Peters Layer Refresh/Apply the Cliff Walls Patch to StPeters.unity")]
        static void ApplyCliffWallsMenu()
        {
            if (EditorUtility.DisplayDialog("St Peters cliff walls",
                    $"Put the walls under the '{StPetersCliffWalls.RootName}' root as their Defs have them, by real id, in the " +
                    "StPeters.unity FILE?\n\nThe scene must be closed. The patch is written to " + PatchFolder + " first.",
                    "Apply", "Cancel"))
                CliffWallsFromMenu(apply: true);
        }

        /// <summary>The dry run for <c>-executeMethod</c>: a refusal fails the run instead of logging.</summary>
        public static void WriteCliffWallsPatchBatch() => RunCliffWalls(apply: false);

        /// <summary>The apply for <c>-executeMethod</c>, the menu's own path.</summary>
        public static void ApplyCliffWallsPatchBatch() => RunCliffWalls(apply: true);

        static void CliffWallsFromMenu(bool apply)
        {
            try
            {
                RunCliffWalls(apply);
            }
            catch (Refusal r)
            {
                Debug.LogError(r.Message);
            }
        }

        /// <summary>Plan the walls' patch against the scene file, write it to <see cref="PatchFolder"/>, and apply it
        /// only when asked, with the scene closed. Applied, the frozen sources then drop today's walls that no longer
        /// stand (<see cref="StPetersTerrainPlan.KeepStandingWalls"/>), so the plan holds no paint where they stood.</summary>
        public static LayerPatch RunCliffWalls(bool apply)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (apply && string.Equals(SceneManager.GetSceneAt(i).path, ScenePath, StringComparison.Ordinal))
                    throw new Refusal("StPeters is open in the editor. Close it first: an open scene would overwrite the file on its next save.");

            string text = File.ReadAllText(ScenePath);
            LayerPatch patch = CliffWalls(SceneYaml.Parse(text), StPetersCliffWalls.LoadDefs());

            Directory.CreateDirectory(PatchFolder);
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(Path.Combine(PatchFolder, patch.Step + ".patch.yaml"), patch.ToYaml(), utf8);
            string after = patch.ApplyTo(text);
            if (apply && after != text) File.WriteAllText(ScenePath, after, utf8);
            int fallen = StPetersTerrainPlan.KeepStandingWalls(after, write: apply);
            string sources = $"\nThe frozen sources ({StPetersTerrainPlan.SourcesPath}): {fallen} of today's wall points no wall stands on " +
                             (apply ? "now, dropped." : "after the patch, to drop.");
            if (apply) Debug.Log($"[StPetersLayerRefresh] applied to {ScenePath} (patch in {PatchFolder}):\n{patch.Summary()}{sources}");
            else Debug.Log($"[StPetersLayerRefresh] dry run, nothing written to the scene (patch in {PatchFolder}):\n{patch.Summary()}{sources}");
            return patch;
        }
    }
}
#endif
