#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;   // MiniJson — the shared editor JSON reader
using HiddenHarbours.Core;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>Generates <see cref="CharacterRampsDef"/> from the rig kit's own <c>options.json</c></b> —
    /// the colour half of ADR 0029, and the reason a shirt can change colour without a re-bake.
    ///
    /// <para>Transcription is the enemy here. Sixty-odd ramps of five or six hex values each is
    /// exactly the kind of table that is copied once, drifts on the next drop, and then ships a
    /// character whose in-game shirt is a shade the rig never draws. So the ramps are LIFTED: this
    /// reads the contract the art director shipped beside the rig and writes the asset from it. When
    /// a drop revises a palette, re-run — do not hand-edit the colours.</para>
    ///
    /// <para><b>The generated asset is committed</b>; the owner does not run this after every pull.
    /// It refreshes in place, keeping the asset's guid, so nothing pointing at the def breaks.</para>
    ///
    /// <para>Only the SEVEN axes a build can set are lifted. <c>options.json</c>'s colour block also
    /// carries <c>boot</c> and <c>keyline</c>, which the rig derives rather than accepts — importing
    /// them would advertise two choices the creator cannot actually make.</para>
    /// </summary>
    public static class CharacterRampsBuilder
    {
        const string MenuPath = "Hidden Harbours/Art/Import (after a new drop)/Build Character Colour Ramps";
        const string OptionsPath = "docs/art/rigs/character/options.json";
        const string VisualsFolder = "Assets/_Project/Data/Characters";
        const string AssetName = "CharacterRamps";
        const string DefId = "ramps.character_iso";

        /// <summary>
        /// The build keys a character's COLOUR is made of, in the kit's own order. Stated here (and
        /// only here) because <c>options.json</c>'s colour block is a superset: <c>boot</c> and
        /// <c>keyline</c> are derived by the rig, not chosen, and <c>note</c> is prose.
        /// </summary>
        public static readonly string[] ColourAxes =
        {
            "skin", "hair", "outfit", "shirt", "hatCol", "apronCol", "eyes",
        };

        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        [MenuItem(MenuPath, priority = 232)]
        public static void Build()
        {
            var def = BuildAsset(out string report);
            Debug.Log(report);
            if (def == null) return;

            Debug.Log($"[CharacterRampsBuilder] {def.RampCount} ramps across {def.Axes.Length} axes " +
                      $"in {VisualsFolder}/{AssetName}.asset. Commit it; re-run only when a drop " +
                      "revises a palette.");
        }

        // The headless entry that chains ramps → visual defs → builds + mapping lives on
        // CharacterBuildsBuilder, at the END of the dependency order rather than the start of it.

        /// <summary>
        /// Reads the contract and refreshes the asset. Returns null (with the reason in
        /// <paramref name="report"/>) rather than writing a partial palette: half a colour space is
        /// worse than none, because the missing half resolves to null at the point of use instead of
        /// at the point of import.
        /// </summary>
        public static CharacterRampsDef BuildAsset(out string report)
        {
            string abs = Path.Combine(RepoRoot, OptionsPath);
            if (!File.Exists(abs))
            {
                report = $"[CharacterRampsBuilder] No contract at {OptionsPath} — the rig kit's " +
                         "options.json is what this builds from. Land the kit first.";
                return null;
            }

            object root = MiniJson.Parse(File.ReadAllText(abs));
            var colour = MiniJson.Dict(root, "colour");
            var defaults = MiniJson.Dict(root, "default_build");
            if (colour == null)
            {
                report = $"[CharacterRampsBuilder] {OptionsPath} carries no 'colour' block — the " +
                         "contract changed shape. Read it before changing this builder.";
                return null;
            }

            var axes = new List<CharacterRampAxis>(ColourAxes.Length);
            var missing = new List<string>();

            foreach (string axisKey in ColourAxes)
            {
                if (!(colour.TryGetValue(axisKey, out object node)) ||
                    !(node is Dictionary<string, object> values) || values.Count == 0)
                {
                    missing.Add(axisKey);
                    continue;
                }

                var ramps = new List<CharacterRamp>(values.Count);
                foreach (var kv in values)          // the contract's own order is the kit's order
                {
                    Color32[] shades = ParseRamp(kv.Value, axisKey, kv.Key);
                    if (shades.Length == 0) { missing.Add($"{axisKey}.{kv.Key}"); continue; }

                    ramps.Add(new CharacterRamp
                    {
                        Id = $"ramp.{axisKey}.{kv.Key}",
                        Key = kv.Key,
                        Label = Titlecase(kv.Key),
                        Shades = shades,
                    });
                }

                axes.Add(new CharacterRampAxis
                {
                    Axis = axisKey,
                    DefaultKey = defaults != null && defaults.TryGetValue(axisKey, out object d)
                                 ? d as string : null,
                    Ramps = ramps.ToArray(),
                });
            }

            if (missing.Count > 0)
            {
                report = "[CharacterRampsBuilder] The contract is missing " +
                         $"{string.Join(", ", missing)} — refusing to write a partial colour space. " +
                         "A half-imported palette resolves to null at the point of USE, which is a " +
                         "long way from here.";
                return null;
            }

            EnsureFolder(VisualsFolder);
            string path = $"{VisualsFolder}/{AssetName}.asset";
            var def = AssetDatabase.LoadAssetAtPath<CharacterRampsDef>(path);
            bool created = def == null;
            if (created) def = ScriptableObject.CreateInstance<CharacterRampsDef>();

            def.Id = DefId;
            def.SourceRig = MiniJsonString(root, "rig");
            def.SourceOptions = OptionsPath;
            def.Axes = axes.ToArray();

            if (created) AssetDatabase.CreateAsset(def, path);
            else EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();

            report = $"[CharacterRampsBuilder] {(created ? "Created" : "Refreshed")} {path} from " +
                     $"{OptionsPath}: " +
                     string.Join(" · ", axes.Select(a => $"{a.Axis} {a.Ramps.Length}"));
            return def;
        }

        /// <summary>
        /// A <c>['#rrggbb', …]</c> array, dark → light, in the contract's order — <b>or a single
        /// <c>'#rrggbb'</c> string</b>, which is a one-shade ramp.
        ///
        /// <para>The single-colour form is not a shortcut in the contract, it is the art: an IRIS is
        /// about two pixels across, so <c>eyes</c> ships one hex per key while every cloth and skin
        /// axis ships five or six. Both are ramps as far as a swap is concerned — one just has a
        /// single step.</para>
        /// </summary>
        static Color32[] ParseRamp(object node, string axis, string key)
        {
            if (node is string single)
                return TryHex(single, out Color32 only)
                    ? new[] { only }
                    : Array.Empty<Color32>();

            if (!(node is List<object> list)) return Array.Empty<Color32>();

            var shades = new List<Color32>(list.Count);
            foreach (object o in list)
            {
                if (!(o is string hex) || !TryHex(hex, out Color32 c))
                {
                    Debug.LogWarning($"[CharacterRampsBuilder] {axis}.{key}: '{o}' is not a " +
                                     "#rrggbb colour — dropping the whole ramp rather than a shade " +
                                     "of it, since a ramp with a hole in it dithers wrong.");
                    return Array.Empty<Color32>();
                }
                shades.Add(c);
            }
            return shades.ToArray();
        }

        static bool TryHex(string hex, out Color32 c)
        {
            c = default;
            if (string.IsNullOrEmpty(hex) || hex[0] != '#' || hex.Length < 7) return false;
            try
            {
                c = new Color32(
                    byte.Parse(hex.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(hex.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    byte.Parse(hex.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    255);
                return true;
            }
            catch (FormatException) { return false; }
        }

        static string MiniJsonString(object node, string key) =>
            node is Dictionary<string, object> d && d.TryGetValue(key, out object v) ? v as string : null;

        /// <summary>Display capitalisation for a rig key. Never matched on — <see cref="CharacterRamp.Key"/> is.</summary>
        static string Titlecase(string key) =>
            string.IsNullOrEmpty(key) ? key : char.ToUpperInvariant(key[0]) + key.Substring(1);

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }
    }
}
#endif
