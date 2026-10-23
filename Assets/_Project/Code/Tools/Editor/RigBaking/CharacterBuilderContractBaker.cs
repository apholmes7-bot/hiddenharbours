using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>Lifts the character builder's tables from rig 10.3, run in V8</b> (character PR 2b, ADR 0051):
    /// the offer (<see cref="CharacterRigContractDef"/>: fields, options, the ten presets, the NPC
    /// presets the builder refuses), the paint (a rule for every material role), the ramps (69 axis
    /// ramps and the fixed ones, <see cref="CharacterRampsDef"/>), the fit the bodies are rigged on
    /// (<c>fit.character_v10</c>) and the catalog that wires them to the ten builds
    /// (<see cref="CharacterBuilderDef"/>, under Resources).
    ///
    /// <para><b>The paint is OBSERVED, not transcribed.</b> The rig exports its palettes but not the
    /// rules that turn them into material ramps, so the lift paints every preset and every single
    /// colour flip of it (630 builds) and reads off, for each role, which colour fields move it: none
    /// (a fixed ramp), one whose palette ramp it is (an axis), or any other mix (a table, one row per
    /// combination, painted by the rig itself). A role that moves with anything else, or whose tone
    /// moves at all, is a problem and the lift refuses. No colour and no rule is typed here, so the
    /// builder carries no rig literal; <c>CharacterBuilderOracleTests</c> holds the tables to the rig.</para>
    ///
    /// <para>The JS is plain ES5 in single quotes, so it runs the same in the editor's V8 and in a
    /// scratch Node check.</para>
    /// </summary>
    public static class CharacterBuilderContractBaker
    {
        public const string RampsPath = "Assets/_Project/Data/Characters/CharacterRamps10.asset";
        public const string ContractPath = "Assets/_Project/Data/Characters/CharacterContract10.asset";
        public const string FitPath = "Assets/_Project/Data/Characters/CharacterFit10.asset";
        public const string CatalogPath = "Assets/_Project/Resources/" + CharacterBuilderDef.ResourcesPath + ".asset";
        public const string BuildsFolder = "Assets/_Project/Data/Characters/Builds";

        public const string RampsId = "ramps.character_iso10";
        public const string ContractId = "contract.character_iso10";
        public const string FitId = "fit.character_v10";
        public const string SkeletonId = "skeleton.character_v10";
        public const string CatalogId = "builder.character_iso10";
        public const string PlayerDefaultBuildId = "char_build.fisher";

        /// <summary>
        /// Ruling 2 (2026-09-24): youth, adult and elder bodies are the player's, children are the
        /// cast's. Rig 9's kit carried it as <c>rules.notOffered</c>; rig 10's kit carries no rules
        /// block, so the lift states it here and writes it into the contract.
        /// </summary>
        public static readonly (string Field, string Value)[] CastOnly = { ("age", "child") };

        /// <summary>The kit the builder is lifted from: rig 10.3, the kit the cast is baked from.</summary>
        public static CharacterRigKit Kit => CharacterRigKit.Rig10;

        // ---------------------------------------------------------------- menu + CLI

        const string Menu = RigMeshGate.MenuRoot + "/Bake character BUILDER tables (rig 10, ADR 0051)";

        [MenuItem(Menu, priority = 226)]
        public static void BakeMenu()
        {
            try { Debug.Log(Bake()); }
            finally { EditorUtility.ClearProgressBar(); }
        }

        [MenuItem(Menu, validate = true)]
        public static bool BakeMenuValidate() => RigMeshGate.Enabled;

        /// <summary>Headless entry (<c>-executeMethod</c>). Grep the log for <c>[char-builder]</c>;
        /// a fresh worktree's first run imports and quits without calling this.</summary>
        public static void BakeCli()
        {
            try
            {
                Debug.Log(Bake());
                Debug.Log("[char-builder] CLI bake OK.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[char-builder] CLI bake FAILED: {e}");
                EditorApplication.Exit(1);
            }
        }

        // ---------------------------------------------------------------- the lift

        /// <summary>What the rig says, before any asset is touched.</summary>
        public sealed class Lift
        {
            public string Rig, Revision, Sha256;
            public LiftJson Json;
        }

        /// <summary>Read the tables off <paramref name="host"/> (rig 10.3 loaded with
        /// <see cref="CharacterSkinExtractor.Load9(IRigScriptHost, CharacterRigKit)"/>). Refuses, listing
        /// them, if the rig paints anything the tables cannot say, or its fields are not the recipe's.</summary>
        public static Lift Read(IRigScriptHost host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            CharacterSkinExtractor.Load9(host, Kit);
            string json = host.EvaluateString("(" + LiftJs + ")(" + Kit.GlobalName + ")");
            LiftJson j = JsonUtility.FromJson<LiftJson>(json);
            if (j == null) throw new InvalidOperationException("The lift returned nothing JsonUtility can read.");

            var problems = new List<string>(j.problems ?? Array.Empty<string>());
            if (j.revision != Kit.Revision)
                problems.Add($"the rig says revision {j.revision}; the lift is written for {Kit.Revision}");
            string[] names = (j.fields ?? Array.Empty<FieldJson>()).Select(f => f.name).ToArray();
            if (!names.SequenceEqual(CharacterRecipe.Fields))
                problems.Add($"the rig's FIELDS are [{string.Join(", ", names)}]; CharacterRecipe carries " +
                             $"[{string.Join(", ", CharacterRecipe.Fields)}]");
            foreach ((string field, string value) in CastOnly)
            {
                FieldJson f = j.fields?.FirstOrDefault(x => x.name == field);
                if (f == null || Array.IndexOf(f.options ?? Array.Empty<string>(), value) < 0)
                    problems.Add($"the cast-only {field} '{value}' is not one of the rig's options");
            }
            if (problems.Count > 0)
                throw new InvalidOperationException(
                    $"{Kit.Title} cannot be lifted into the builder's tables:\n  " + string.Join("\n  ", problems));

            return new Lift
            {
                Rig = j.rig, Revision = j.revision, Sha256 = CharacterSkinExtractor.SourceSha256V9(Kit), Json = j,
            };
        }

        // ---------------------------------------------------------------- the assets, in memory

        public static void Fill(CharacterRampsDef def, Lift lift)
        {
            def.Id = RampsId;
            def.SourceRig = Kit.ScriptPath;
            def.SourceOptions = $"{lift.Rig} {lift.Revision}'s own palettes, read in V8 (sha256 {lift.Sha256})";
            def.Axes = lift.Json.axes.Select(a => new CharacterRampAxis
            {
                Axis = a.field,
                DefaultKey = a.defaultKey,
                Ramps = a.ramps.Select(r => Ramp($"ramp.{a.field}.{r.key}", r)).ToArray(),
            }).ToArray();
            def.Fixed = lift.Json.fixedRamps.Select(r => Ramp($"ramp.fixed.{r.key}", r)).ToArray();
        }

        public static void Fill(CharacterRigContractDef def, Lift lift)
        {
            def.Id = ContractId;
            def.SourceRig = $"{Kit.ScriptPath} ({lift.Rig} {lift.Revision})";
            def.SourceRigSha256 = lift.Sha256;
            def.Fields = lift.Json.fields.Select(f => new CharacterField
            {
                Name = f.name,
                Kind = f.colour ? CharacterFieldKind.Colour : CharacterFieldKind.Structure,
                Options = f.options ?? Array.Empty<string>(),
                CastOnly = CastOnly.Where(c => c.Field == f.name).Select(c => c.Value).ToArray(),
            }).ToArray();
            def.Presets = lift.Json.presets.Select(p => new CharacterPresetValues
            {
                Key = p.key, Values = p.values ?? Array.Empty<string>(),
            }).ToArray();
            def.NpcPresets = lift.Json.npcs ?? Array.Empty<string>();
            def.Paint = lift.Json.rules.Select(r => new CharacterPaintRule
            {
                Role = r.role,
                Source = (CharacterPaintSource)r.source,
                Field = r.field ?? "",
                FixedKey = r.fixedKey ?? "",
                TableFields = r.tableFields ?? Array.Empty<string>(),
                Rows = (r.rows ?? Array.Empty<RowJson>()).Select(w => new CharacterPaintRow
                {
                    Keys = w.keys ?? Array.Empty<string>(),
                    Colors = (w.colors ?? Array.Empty<string>()).Select((h, i) => Hex(h, $"{r.role} row {i}")).ToArray(),
                }).ToArray(),
                Offset = r.off,
                Gain = (float)r.gain,
                Bias = (float)r.bias,
                ToneLo = r.lo,
                ToneHi = r.hi,
            }).ToArray();
        }

        public static void Fill(CharacterClothingFitDef def, Lift lift)
        {
            def.Spec = new CharacterClothingFitSpec
            {
                SchemaVersion = CharacterClothingSchema.Version,
                Id = FitId,
                SourceRigSha256 = lift.Sha256,
                SkeletonId = SkeletonId,
                BoneIds = lift.Json.bones ?? Array.Empty<string>(),
            };
        }

        /// <summary>The catalog: the lifted tables and, for each of the contract's presets in its
        /// order, its core build. Refuses a missing or duplicate core build.</summary>
        public static void Fill(CharacterBuilderDef def, CharacterRigContractDef contract, CharacterRampsDef ramps,
            CharacterClothingFitDef fit, IReadOnlyList<CharacterBuildDef> builds)
        {
            var chosen = new List<CharacterBuildDef>();
            foreach (CharacterPresetValues p in contract.Presets)
            {
                string coreId = "char_build." + p.Key;
                CharacterBuildDef[] named = builds.Where(b => b != null && b.Id == coreId && b.Preset == p.Key).ToArray();
                if (named.Length != 1)
                    throw new InvalidOperationException(
                        $"Preset '{p.Key}' needs exactly one core build '{coreId}'; found {named.Length}.");
                chosen.Add(named[0]);
            }
            if (chosen.All(b => b.Id != PlayerDefaultBuildId))
                throw new InvalidOperationException($"No build is '{PlayerDefaultBuildId}', the player's default.");

            def.Id = CatalogId;
            def.Contract = contract;
            def.Ramps = ramps;
            def.Fit = fit;
            def.Builds = chosen.ToArray();
            def.PlayerDefaultBuildId = PlayerDefaultBuildId;
        }

        // ---------------------------------------------------------------- the bake

        /// <summary>Lift, write the four assets, then rebuild the ten presets through the builder
        /// and hold each to its own baked def. Returns the report; throws on any refusal.</summary>
        public static string Bake()
        {
            Lift lift;
            using (IRigScriptHost host = RigScriptHostFactory.Create())
            {
                EditorUtility.DisplayProgressBar("Character builder", $"Lifting {Kit.Title}'s tables in V8", 0.2f);
                lift = Read(host);
            }

            EditorUtility.DisplayProgressBar("Character builder", "Writing the tables", 0.7f);
            CharacterRampsDef ramps = LoadOrCreate<CharacterRampsDef>(RampsPath);
            Fill(ramps, lift);
            CharacterRigContractDef contract = LoadOrCreate<CharacterRigContractDef>(ContractPath);
            Fill(contract, lift);
            CharacterClothingFitDef fit = LoadOrCreate<CharacterClothingFitDef>(FitPath);
            Fill(fit, lift);

            var builds = AssetDatabase.FindAssets("t:" + nameof(CharacterBuildDef), new[] { BuildsFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<CharacterBuildDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(b => b != null).ToList();
            CharacterBuilderDef catalog = LoadOrCreate<CharacterBuilderDef>(CatalogPath);
            Fill(catalog, contract, ramps, fit, builds);

            foreach (UnityEngine.Object o in new UnityEngine.Object[] { ramps, contract, fit, catalog })
                EditorUtility.SetDirty(o);
            AssetDatabase.SaveAssets();

            var report = new StringBuilder();
            report.Append($"[char-builder] {lift.Rig} {lift.Revision} (sha {lift.Sha256.Substring(0, 12)}…): ")
                .Append($"{contract.Fields.Length} fields, {contract.Presets.Length} presets, ")
                .Append($"{contract.NpcPresets.Length} NPC presets refused, {contract.Paint.Length} paint rules ")
                .Append($"({Count(contract, CharacterPaintSource.Axis)} axis, {Count(contract, CharacterPaintSource.Fixed)} fixed, ")
                .Append($"{Count(contract, CharacterPaintSource.Table)} table, ")
                .Append($"{contract.Paint.Sum(r => r.Rows.Length)} rows), {ramps.RampCount} axis ramps, ")
                .Append($"{ramps.Fixed.Length} fixed ramps, {fit.Spec.BoneIds.Length} bones.");
            foreach (CharacterBuildDef b in catalog.Builds)
            {
                var recipe = new CharacterRecipe { BuildId = b.Id };
                if (!CharacterBuilder.TryBuild(catalog, recipe, CharacterAudience.Cast, out CharacterLook look, out string why))
                    throw new InvalidOperationException($"The builder refuses the preset build '{b.Id}': {why}");
                for (int m = 0; m < look.Skin.Materials.Length; m++)
                    if (!look.Colours[m].SequenceEqual(look.Skin.Materials[m].Colors ?? Array.Empty<Color32>(), Color32Equality.Instance))
                        throw new InvalidOperationException(
                            $"'{b.Id}' rebuilds '{look.Skin.Materials[m].Name}' in other colours than its baked def. " +
                            "The def and the rig disagree: re-bake the cast, or stop and report.");
                report.Append($"\n  {b.Id}: rebuilt, {look.Skin.Materials.Length} materials equal to '{look.Skin.Id}'.");
            }
            return report.ToString();
        }

        // ---------------------------------------------------------------- helpers

        static int Count(CharacterRigContractDef c, CharacterPaintSource s) => c.Paint.Count(r => r.Source == s);

        static CharacterRamp Ramp(string id, RampJson r) => new CharacterRamp
        {
            Id = id,
            Key = r.key,
            Label = string.IsNullOrEmpty(r.key) ? "" : char.ToUpperInvariant(r.key[0]) + r.key.Substring(1),
            Shades = (r.shades ?? Array.Empty<string>()).Select((h, i) => Hex(h, $"{id}[{i}]")).ToArray(),
        };

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>'#rrggbb' to an opaque colour; refuses anything else, by name.</summary>
        public static Color32 Hex(string hex, string where)
        {
            if (hex == null || hex.Length != 7 || hex[0] != '#' ||
                !int.TryParse(hex.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int v))
                throw new InvalidOperationException($"{where}: '{hex}' is not a #rrggbb colour.");
            return new Color32((byte)(v >> 16), (byte)(v >> 8), (byte)v, 255);
        }

        sealed class Color32Equality : IEqualityComparer<Color32>
        {
            public static readonly Color32Equality Instance = new Color32Equality();
            public bool Equals(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
            public int GetHashCode(Color32 c) => (c.r << 24) | (c.g << 16) | (c.b << 8) | c.a;
        }

        // ---------------------------------------------------------------- the JSON the lift returns

#pragma warning disable 0649 // written by JsonUtility
        [Serializable] public sealed class LiftJson
        {
            public string rig, revision;
            public FieldJson[] fields;
            public PresetJson[] presets;
            public string[] npcs;
            public AxisJson[] axes;
            public RampJson[] fixedRamps;
            public RuleJson[] rules;
            public string[] bones;
            public string[] problems;
            public int records;
        }
        [Serializable] public sealed class FieldJson { public string name; public bool colour; public string[] options; }
        [Serializable] public sealed class PresetJson { public string key; public string[] values; }
        [Serializable] public sealed class AxisJson { public string field, defaultKey; public RampJson[] ramps; }
        [Serializable] public sealed class RampJson { public string key; public string[] shades; }
        [Serializable] public sealed class RuleJson
        {
            public string role, field, fixedKey;
            public int source, off, lo, hi;
            public double gain, bias;
            public string[] tableFields;
            public RowJson[] rows;
        }
        [Serializable] public sealed class RowJson { public string[] keys, colors; }
#pragma warning restore 0649

        /// <summary>The lift, as a JS function of the rig's global. Kept byte-for-byte with the
        /// scratch Node check that proved it (630 builds, 21,420 role colours, the ten defs).</summary>
        public const string LiftJs = @"function (C) {
  var PAL = { skin: 'SKINS', hair: 'HAIRS', eyes: 'EYES', outfit: 'OUTFITS', shirt: 'SHIRTS', hatCol: 'HATCOLS', apronCol: 'APRONS' };
  var problems = [];
  function own(o, k) { return Object.prototype.hasOwnProperty.call(o, k); }
  function str(v) { return v == null ? '' : String(v); }
  function rampOf(v) { return Array.isArray(v) ? v.slice() : [v]; }
  function same(a, b) { return JSON.stringify(a) === JSON.stringify(b); }
  function copy(b) { var o = {}; for (var k in b) if (own(b, k)) o[k] = b[k]; return o; }
  var F = C.FIELDS.slice();
  var colourFields = F.filter(function (f) { return own(PAL, f); });
  function optionsOf(f) { return (C.OPTIONS[f] || []).map(str); }
  Object.keys(PAL).forEach(function (f) { if (F.indexOf(f) < 0) problems.push('the colour field ' + f + ' is not one of the rig FIELDS'); });
  colourFields.forEach(function (f) {
    var keys = Object.keys(C.palettes[PAL[f]] || {});
    if (keys.join(',') !== optionsOf(f).join(','))
      problems.push(f + ': its options (' + optionsOf(f).join(',') + ') are not the keys of ' + PAL[f] + ' (' + keys.join(',') + ')');
  });

  var fields = F.map(function (f) { return { name: f, colour: own(PAL, f), options: optionsOf(f) }; });
  var presetKeys = C.CAST10.slice();
  var presets = presetKeys.map(function (k) {
    var b = C.normBuild(C.BUILDS[k]);
    return { key: k, values: F.map(function (f) { return str(b[f]); }) };
  });
  var dflt = C.normBuild({});
  var axes = colourFields.map(function (f) {
    var pal = C.palettes[PAL[f]];
    return { field: f, defaultKey: str(dflt[f]), ramps: Object.keys(pal).map(function (k) { return { key: k, shades: rampOf(pal[k]) }; }) };
  });

  // The paint: each role as the bake reads it (CharacterSkinExtractor.ReadMaterials9): a fixed
  // colour is a one-entry ramp with the neutral tone, a ramp keeps the contract's own.
  function paintOf(b) {
    var M = C.shadingContract(b).materials, out = {};
    Object.keys(M).forEach(function (r) {
      var m = M[r];
      out[r] = m.fixed ? { cols: [m.color], off: 0, gain: 1, bias: 0, lo: 0, hi: 0 }
        : { cols: m.ramp.slice(), off: m.off == null ? 0 : m.off, gain: m.gain, bias: m.bias, lo: m.lo, hi: m.hi };
    });
    return out;
  }
  var roles = Object.keys(C.shadingContract(dflt).materials);

  // Every preset, and every single colour flip of it.
  var records = [], deps = {};
  roles.forEach(function (r) { deps[r] = {}; });
  presetKeys.forEach(function (p) {
    var base = C.normBuild(C.BUILDS[p]), p0 = paintOf(base);
    records.push({ b: base, paint: p0 });
    colourFields.forEach(function (f) {
      optionsOf(f).forEach(function (o) {
        if (o === str(base[f])) return;
        var b = copy(base); b[f] = o; b = C.normBuild(b);
        var pp = paintOf(b);
        records.push({ b: b, paint: pp });
        roles.forEach(function (r) { if (!same(pp[r].cols, p0[r].cols)) deps[r][f] = true; });
      });
    });
  });

  function toneOf(t) { return [t.off, t.gain, t.bias, t.lo, t.hi]; }
  var rules = [], fixedUse = [];
  roles.forEach(function (r) {
    var t0 = records[0].paint[r];
    records.forEach(function (rec) {
      if (!same(toneOf(rec.paint[r]), toneOf(t0))) problems.push(r + ': its tone changes with the build (' + toneOf(rec.paint[r]).join(',') + ' against ' + toneOf(t0).join(',') + ')');
    });
    var D = colourFields.filter(function (f) { return deps[r][f]; });
    var rule = { role: r, source: 0, field: '', fixedKey: '', tableFields: [], rows: [], off: t0.off, gain: t0.gain, bias: t0.bias, lo: t0.lo, hi: t0.hi };
    if (D.length === 0) {
      records.forEach(function (rec) { if (!same(rec.paint[r].cols, t0.cols)) problems.push(r + ': its colours change between presets, with no colour field'); });
      rule.source = 1;
      fixedUse.push({ role: r, off: t0.off, cols: t0.cols, rule: rule });
    } else if (D.length === 1 && records.every(function (rec) { return same(rec.paint[r].cols, rampOf(C.palettes[PAL[D[0]]][rec.b[D[0]]])); })) {
      rule.source = 0;
      rule.field = D[0];
    } else {
      rule.source = 2;
      rule.tableFields = D;
      var combos = [[]];
      D.forEach(function (f) {
        var next = [];
        combos.forEach(function (c) { optionsOf(f).forEach(function (o) { next.push(c.concat([o])); }); });
        combos = next;
      });
      combos.forEach(function (c) {
        var b = copy(dflt);
        D.forEach(function (f, i) { b[f] = c[i]; });
        rule.rows.push({ keys: c, colors: paintOf(C.normBuild(b))[r].cols });
      });
      records.forEach(function (rec) {
        var key = D.map(function (f) { return str(rec.b[f]); }).join(',');
        var row = rule.rows.filter(function (w) { return w.keys.join(',') === key; })[0];
        if (!row || !same(row.colors, rec.paint[r].cols)) problems.push(r + ': its colours at ' + key + ' depend on more than ' + D.join(' and '));
      });
    }
    rules.push(rule);
  });

  // The fixed ramps: one per distinct colour list, keyed by its plainest user (the smallest
  // offset, then the first in the contract's order), since the rig exports no name for them.
  var fixedRamps = [];
  fixedUse.forEach(function (u) {
    var g = fixedRamps.filter(function (x) { return same(x.shades, u.cols); })[0];
    if (!g) { g = { key: u.role, shades: u.cols.slice(), best: Math.abs(u.off), users: [] }; fixedRamps.push(g); }
    else if (Math.abs(u.off) < g.best) { g.key = u.role; g.best = Math.abs(u.off); }
    g.users.push(u.rule);
  });
  fixedRamps.forEach(function (g) { g.users.forEach(function (rule) { rule.fixedKey = g.key; }); delete g.users; delete g.best; });
  fixedRamps.sort(function (a, b) { return roles.indexOf(a.key) - roles.indexOf(b.key); });

  var bones = null;
  presetKeys.forEach(function (p) {
    var ids = C.exportBuild(p).skeleton.map(function (b) { return b.id; });
    if (bones === null) bones = ids;
    else if (ids.join(',') !== bones.join(',')) problems.push(p + ': its skeleton is not ' + presetKeys[0] + 's');
  });

  return JSON.stringify({ rig: str(C.rig), revision: str(C.revision), fields: fields, presets: presets,
    npcs: C.NPCS.slice(), axes: axes, fixedRamps: fixedRamps, rules: rules, bones: bones, problems: problems,
    records: records.length });
}";
    }
}
