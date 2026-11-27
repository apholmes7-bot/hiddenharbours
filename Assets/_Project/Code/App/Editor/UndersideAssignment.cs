#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// Names the underside each deck over calm water reflects (the owner, 2026-11-26). A re-runnable text step:
    /// it touches ONE line per named deck component (its <c>_underside</c> reference) and nothing else, so it can
    /// run again after any builder or refresh rewrites a scene. A second run is a no-op. A deck the table names
    /// but the scene lacks is a refusal, never a silent skip.
    /// </summary>
    public static class UndersideAssignment
    {
        public const string StPetersScene = "Assets/_Project/Scenes/StPeters.unity";
        public const string NineMileCreekScene = "Assets/_Project/Scenes/NineMileCreek.unity";
        public const string UndersideFolder = "Assets/_Project/Data/Regions/Undersides/";

        /// <summary>The deck components that carry an underside (script guids from their .meta files).</summary>
        public static readonly IReadOnlyDictionary<string, string> DeckScripts = new Dictionary<string, string>
        {
            ["44422230a0a985e44ac4eb7c9dd04712"] = "StandablePlatform",
            ["3f7c2a91b48d4e5a9c06d1f8b27e4530"] = "FloatingPlatform",
            ["8a41c6d0e7b24f13ab9527c4d80f6e19"] = "GangwayPlatform",
            ["af684d0b0b9a60ae94ae9b10942c7ba0"] = "NmcWharfWalkSurface",
        };

        public readonly struct Entry
        {
            public readonly string Scene, Deck, Asset;
            public Entry(string scene, string deck, string asset) { Scene = scene; Deck = deck; Asset = asset; }
        }

        /// <summary>Which deck (scene, GameObject name) reflects which underside asset.</summary>
        public static IEnumerable<Entry> Table()
        {
            const string stp = "UndersideStPetersPier.asset", pier = "UndersideNmcPier.asset",
                         fl = "UndersideNmcFloat.asset", gw = "UndersideNmcGangway.asset";
            yield return new Entry(StPetersScene, "StPetersWharf", stp);
            foreach (string d in new[] { "pierW/walk0", "pierE/walk0", "w0.walk", "w1.walk", "w2.walk",
                                         "e0.walk", "e1.walk", "e2.walk", "e3.walk" })
                yield return new Entry(NineMileCreekScene, d, pier);
            foreach (string d in new[] { "float.nmc_working", "float.nmc_finger_a", "float.nmc_finger_b", "float.nmc_recreation" })
                yield return new Entry(NineMileCreekScene, d, fl);
            foreach (string d in new[] { "gangway.nmc_west_gangway", "gangway.nmc_fleet_gangway0",
                                         "gangway.nmc_fleet_gangway1", "gangway.nmc_east_gangway" })
                yield return new Entry(NineMileCreekScene, d, gw);
        }

        static readonly Regex DocHead = new Regex(@"^--- !u!(\d+) &(-?\d+)[^\n]*$", RegexOptions.Multiline);

        /// <summary>Pure: patch one scene's text. <paramref name="guidOf"/> maps an asset file name to its guid.</summary>
        public static string Patch(string text, IEnumerable<Entry> entries, Func<string, string> guidOf, out int changed)
        {
            if (text.Contains("\r\n")) throw new InvalidOperationException("underside: the scene is not LF text.");
            var heads = DocHead.Matches(text).Cast<Match>().ToArray();
            var names = new Dictionary<string, string>(StringComparer.Ordinal);
            var decks = new List<(int start, int end, string go)>();
            for (int i = 0; i < heads.Length; i++)
            {
                int start = heads[i].Index, end = i + 1 < heads.Length ? heads[i + 1].Index : text.Length;
                string body = text.Substring(start, end - start);
                string cls = heads[i].Groups[1].Value, id = heads[i].Groups[2].Value;
                if (cls == "1") { Match m = Regex.Match(body, @"\n  m_Name: ([^\n]*)"); if (m.Success) names[id] = m.Groups[1].Value; }
                else if (cls == "114")
                {
                    Match s = Regex.Match(body, @"\n  m_Script: \{fileID: \d+, guid: (\w+)");
                    Match g = Regex.Match(body, @"\n  m_GameObject: \{fileID: (-?\d+)");
                    if (s.Success && g.Success && DeckScripts.ContainsKey(s.Groups[1].Value)) decks.Add((start, end, g.Groups[1].Value));
                }
            }
            var edits = new List<(int start, int end, string body)>();
            changed = 0;
            foreach (Entry e in entries)
            {
                var hits = decks.Where(d => names.TryGetValue(d.go, out string n) && n == e.Deck).ToArray();
                if (hits.Length != 1) throw new InvalidOperationException($"underside: '{e.Deck}' has {hits.Length} deck component(s) in {e.Scene}, needs exactly one.");
                var (start, end, _) = hits[0];
                string body = text.Substring(start, end - start);
                string line = $"  _underside: {{fileID: 11400000, guid: {guidOf(e.Asset)}, type: 2}}";
                string next;
                Match old = Regex.Match(body, @"\n  _underside: [^\n]*");
                if (old.Success) next = body.Substring(0, old.Index + 1) + line + body.Substring(old.Index + old.Length);
                else
                {
                    // The field is the last one each deck class declares, so Unity writes it last in the document.
                    string trimmed = body.TrimEnd('\n');
                    next = trimmed + "\n" + line + body.Substring(trimmed.Length);
                }
                if (next != body) { edits.Add((start, end, next)); changed++; }
            }
            var sb = new StringBuilder(text.Length + edits.Count * 80);
            int at = 0;
            foreach (var ed in edits.OrderBy(x => x.start))
            {
                sb.Append(text, at, ed.start - at).Append(ed.body); at = ed.end;
            }
            sb.Append(text, at, text.Length - at);
            return sb.ToString();
        }

        /// <summary>The serialized line a scene step writes last on deck <paramref name="deck"/>, or none when the
        /// table names no underside for it. Lets a step that writes a deck's whole document keep the reference.</summary>
        public static IEnumerable<string> LinesFor(string scene, string deck)
        {
            foreach (Entry e in Table())
                if (e.Scene == scene && e.Deck == deck)
                {
                    yield return $"  _underside: {{fileID: 11400000, guid: {GuidOf(e.Asset)}, type: 2}}";
                    yield break;
                }
        }

        public static string GuidOf(string assetFile)
        {
            string path = UndersideFolder + assetFile;
            var def = AssetDatabase.LoadAssetAtPath<UndersideDef>(path);
            if (def == null || !def.IsUsable) throw new InvalidOperationException("underside: " + path + " is missing or unusable.");
            return AssetDatabase.AssetPathToGUID(path);
        }

        public static int Run()
        {
            int total = 0;
            foreach (var group in Table().GroupBy(e => e.Scene))
            {
                string text = File.ReadAllText(group.Key);
                string next = Patch(text, group, GuidOf, out int changed);
                if (changed > 0) File.WriteAllText(group.Key, next, new UTF8Encoding(false));
                Debug.Log($"[UndersideAssignment] {group.Key}: {changed} of {group.Count()} deck(s) changed.");
                total += changed;
            }
            AssetDatabase.Refresh();
            return total;
        }

        [MenuItem("Hidden Harbours/World/Name Deck Undersides (calm-water reflections)", priority = 90)]
        public static void RunMenu() => Run();

        public static void RunFromCommandLine()
        {
            int code = 1;
            try
            {
                // Bake the one sheet from its rig first, so the Defs the table names point at today's art.
                HiddenHarbours.Tools.RigBaking.UndersideSheetBaker.BakeMenu();
                Debug.Log("[UndersideAssignment] UNDERSIDE_DONE " + Run());
                code = 0;
            }
            catch (Exception ex) { Debug.LogError("[UndersideAssignment] failed: " + ex); }
            finally { EditorApplication.Exit(code); }
        }
    }
}
#endif
