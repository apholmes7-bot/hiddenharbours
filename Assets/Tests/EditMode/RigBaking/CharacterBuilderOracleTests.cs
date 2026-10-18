using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE BUILDER, HELD TO THE RIG</b> (character PR 2b, ADR 0051). The game builds a person with
    /// no JS: a recipe resolves to one of the ten baked bodies and a colour for each of its materials,
    /// looked up in tables the editor lifted from rig 10.3 (<see cref="CharacterBuilderContractBaker"/>).
    /// The oracle is the rig itself, run here in V8 on its own unmodified source: its
    /// <c>shadingContract</c> for the colours and tones, <c>exportBuild</c>, <c>skeleton</c> and
    /// <c>bindMesh</c> for the body, <c>gameplay</c> for the pins, <c>buildOf</c> and <c>normBuild</c>
    /// for what it accepts.
    ///
    /// <para><b>The tolerance is zero.</b> Colours are compared as bytes, tones as the numbers the bake
    /// stores, structure as the rig's own JSON. Nothing here is rounded or near enough.</para>
    ///
    /// <para>What it holds: the committed tables are the rig's as lifted today; every preset, every
    /// single colour option of it and a seeded set of whole recolours paints what the rig paints; no
    /// colour moves a bone, a vertex or a gameplay number; the ten defs are the rig's paint and rebuild
    /// through the builder; the builder refuses every preset the rig refuses and, by name, the ones the
    /// rig takes that the game does not build; an option the rig quietly swaps for the Fisher's is
    /// refused; the player is offered no child. And, for the ADR, what the rig's own cold build costs
    /// in this editor: logged, never gated.</para>
    ///
    /// <para>The committed tables are read at the baker's paths; until its bake has run they are
    /// missing and these tests say so. The rig is loaded once, in a host of its own.</para>
    /// </summary>
    public class CharacterBuilderOracleTests
    {
        /// <summary>Whole recolours per preset, every colour field drawn from a seeded LCG. The lift
        /// observes single changes; these hold the lookup when several change at once.</summary>
        const int Recolours = 8;

        /// <summary>Cold builds of each preset for candidate A's cost (ADR 0051's numbers).</summary>
        const int CostRounds = 3;

        IRigScriptHost _host;
        readonly List<Object> _made = new List<Object>();

        IRigScriptHost Host
        {
            get
            {
                if (_host == null)
                {
                    _host = RigScriptHostFactory.Create();
                    CharacterSkinExtractor.Load9(_host, CharacterBuilderContractBaker.Kit);
                }
                return _host;
            }
        }

        static string G => CharacterBuilderContractBaker.Kit.GlobalName;

        [OneTimeTearDown]
        public void DisposeHost()
        {
            _host?.Dispose();
            _host = null;
        }

        [TearDown]
        public void DestroyMade()
        {
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        // =======================================================================================
        // 1. the tables
        // =======================================================================================

        /// <summary>The four committed assets are what the lift makes of the rig today, field for
        /// field: a rig edit, a hand edit or a stale bake shows here as the first line that differs.</summary>
        [Test]
        public void TheCommittedTables_AreTheRigsOwn_AsLiftedToday()
        {
            CharacterBuilderDef shipped = Shipped();
            var ramps = AssetDatabase.LoadAssetAtPath<CharacterRampsDef>(CharacterBuilderContractBaker.RampsPath);
            var contract = AssetDatabase.LoadAssetAtPath<CharacterRigContractDef>(CharacterBuilderContractBaker.ContractPath);
            var fit = AssetDatabase.LoadAssetAtPath<CharacterClothingFitDef>(CharacterBuilderContractBaker.FitPath);
            Assert.IsNotNull(ramps, $"{CharacterBuilderContractBaker.RampsPath} is missing: run the builder's bake");
            Assert.IsNotNull(contract, $"{CharacterBuilderContractBaker.ContractPath} is missing: run the builder's bake");
            Assert.IsNotNull(fit, $"{CharacterBuilderContractBaker.FitPath} is missing: run the builder's bake");
            Assert.AreSame(contract, shipped.Contract, "the catalog names another contract than the one at the baker's path");
            Assert.AreSame(ramps, shipped.Ramps, "the catalog names other ramps than the ones at the baker's path");
            Assert.AreSame(fit, shipped.Fit, "the catalog names another fit than the one at the baker's path");

            CharacterBuilderContractBaker.Lift lift = CharacterBuilderContractBaker.Read(Host);

            var freshRamps = Make<CharacterRampsDef>();
            CharacterBuilderContractBaker.Fill(freshRamps, lift);
            var freshContract = Make<CharacterRigContractDef>();
            CharacterBuilderContractBaker.Fill(freshContract, lift);
            var freshFit = Make<CharacterClothingFitDef>();
            CharacterBuilderContractBaker.Fill(freshFit, lift);
            var freshCatalog = Make<CharacterBuilderDef>();
            CharacterBuilderContractBaker.Fill(freshCatalog, contract, ramps, fit, BuildsInFolder());

            AssertSameJson(ramps, freshRamps, CharacterBuilderContractBaker.RampsPath);
            AssertSameJson(contract, freshContract, CharacterBuilderContractBaker.ContractPath);
            AssertSameJson(fit, freshFit, CharacterBuilderContractBaker.FitPath);
            AssertSameJson(shipped, freshCatalog, CharacterBuilderContractBaker.CatalogPath);
            Assert.AreEqual(lift.Sha256, contract.SourceRigSha256, "the contract names another rig source");
            Assert.AreEqual(lift.Sha256, fit.Spec.SourceRigSha256, "the fit names another rig source");

            Debug.Log($"[CharacterBuilderOracleTests] tables: {lift.Rig} {lift.Revision} (sha256 {lift.Sha256}), " +
                      $"{contract.Fields.Length} fields, {contract.Presets.Length} presets, {contract.NpcPresets.Length} " +
                      $"NPC presets, {contract.Paint.Length} paint rules, {ramps.RampCount} axis ramps, " +
                      $"{ramps.Fixed.Length} fixed ramps, {fit.Spec.BoneIds.Length} bones, {lift.Json.records} " +
                      "builds observed: the committed assets are today's lift.");
        }

        // =======================================================================================
        // 2. the paint
        // =======================================================================================

        /// <summary>
        /// Every preset, every single option of every colour field on it (630 builds) and
        /// <see cref="Recolours"/> whole recolours of each: the builder's colours for every material
        /// the preset's def paints are the rig's <c>shadingContract</c> colours, byte for byte. The
        /// options are the rig's own <c>OPTIONS</c>, so an option the tables lack is a refusal here.
        /// </summary>
        [Test]
        public void EveryPresetAndEveryColourOption_IsTheRigsOwnPaint_ByteForByte()
        {
            CharacterBuilderDef shipped = Shipped();
            string colourFields = JsArray(ColourFields(shipped.Contract));
            int builds = 0, singles = 0, wholes = 0, colours = 0;
            var bad = new List<string>();

            foreach (CharacterPresetValues preset in shipped.Contract.Presets)
            {
                CharacterBuildDef build = shipped.BuildForPreset(preset.Key);
                Assert.IsNotNull(build, $"no build for the preset '{preset.Key}'");
                string records = Host.EvaluateString(
                    $"({PaintJs})({G}, {Js(preset.Key)}, {colourFields}, {Recolours})");

                foreach (string record in records.Split('\n'))
                {
                    string[] seg = record.Split('|');
                    string kind = seg[0], changes = seg[1];
                    var recipe = new CharacterRecipe { BuildId = build.Id };
                    foreach (string change in changes.Length == 0 ? Array.Empty<string>() : changes.Split(';'))
                    {
                        int at = change.IndexOf(':');
                        Assert.IsTrue(recipe.Set(change.Substring(0, at), change.Substring(at + 1)),
                            $"the rig flips '{change}', which is not one of the recipe's fields");
                    }
                    string label = $"{preset.Key} {(changes.Length == 0 ? "as baked" : changes)}";
                    if (!CharacterBuilder.TryBuild(shipped, recipe, CharacterAudience.Cast, out CharacterLook look,
                            out string why))
                    {
                        bad.Add($"{label}: the builder refuses it: {why}");
                        continue;
                    }
                    builds++;
                    if (kind == "one") singles++;
                    if (kind == "all") wholes++;

                    var rig = new Dictionary<string, string>(StringComparer.Ordinal);
                    for (int s = 2; s < seg.Length; s++)
                    {
                        int eq = seg[s].IndexOf('=');
                        rig[seg[s].Substring(0, eq)] = seg[s].Substring(eq + 1);
                    }
                    for (int m = 0; m < look.Skin.Materials.Length; m++)
                    {
                        string role = look.Skin.Materials[m].Name;
                        if (!rig.TryGetValue(role, out string want))
                        {
                            bad.Add($"{label}: '{look.Skin.Id}' paints '{role}', which the rig's shadingContract does not hold");
                            continue;
                        }
                        colours++;
                        Color32[] wanted = Colours(want, $"{label} {role}");
                        if (!SameBytes(look.Colours[m], wanted))
                            bad.Add($"{label} {role}: the builder paints {Hexes(look.Colours[m])}, the rig {Hexes(wanted)}");
                    }
                }
            }

            Assert.IsEmpty(bad, $"{bad.Count} differences from the rig (tolerance 0):\n  " +
                                string.Join("\n  ", bad.Take(40)));
            Assert.AreEqual(shipped.Contract.Presets.Length * Recolours, wholes, "a whole recolour went missing");
            Assert.Greater(singles, 0, "the oracle flipped nothing");
            Debug.Log($"[CharacterBuilderOracleTests] paint: {builds} builds ({shipped.Contract.Presets.Length} presets as " +
                      $"baked, {singles} single options, {wholes} whole recolours), {colours} material ramps, " +
                      "every one the rig's own, byte for byte.");
        }

        /// <summary>
        /// Colour never touches structure, which is what lets the ten baked bodies stand for every
        /// recolour: no single option of any colour field on any preset moves the rig's skeleton or
        /// its bind mesh, and the last option of each field moves none of its gameplay pins (the
        /// figure and every clip).
        /// </summary>
        [Test]
        public void AColourOption_MovesNoBone_NoVertex_AndNoGameplayNumber()
        {
            CharacterBuilderDef shipped = Shipped();
            string colourFields = JsArray(ColourFields(shipped.Contract));
            int flips = 0, pins = 0;
            var moved = new List<string>();

            foreach (CharacterPresetValues preset in shipped.Contract.Presets)
            {
                var r = JsonUtility.FromJson<StructJson>(
                    Host.EvaluateString($"({StructJs})({G}, {Js(preset.Key)}, {colourFields})"));
                flips += r.flips;
                pins += r.gameplay;
                moved.AddRange(r.moved.Select(m => $"{preset.Key} {m}"));
                moved.AddRange(r.gameplayMoved.Select(m => $"{preset.Key} {m}: the gameplay pins move"));
            }

            Assert.IsEmpty(moved, $"a colour moves the body (the builder would draw the wrong one):\n  " +
                                  string.Join("\n  ", moved.Take(20)));
            Assert.AreEqual(shipped.Contract.Presets.Length * ColourFields(shipped.Contract).Length, pins,
                "one gameplay comparison per colour field per preset");
            Debug.Log($"[CharacterBuilderOracleTests] structure: {flips} colour options on {shipped.Contract.Presets.Length} " +
                      $"presets, no bone and no bind-mesh face moved; {pins} gameplay comparisons, no pin moved.");
        }

        /// <summary>The ten defs: every material is the rig's own ramp and tone for its preset
        /// (what the bake stores: a fixed colour is a one-entry ramp, offset 0, gain 1, bias 0, tones
        /// 0–0), and the builder rebuilds each from its build id in exactly those colours.</summary>
        [Test]
        public void TheTenDefs_AreTheRigsPaint_AndRebuildThroughTheBuilder()
        {
            CharacterBuilderDef shipped = Shipped();
            int materials = 0;
            var bad = new List<string>();

            foreach (CharacterBuildDef build in shipped.Builds)
            {
                CharacterSkinDef skin = build.Visual.Skin;
                var rig = JsonUtility.FromJson<MatsJson>(Host.EvaluateString($"({MatsJs})({G}, {Js(build.Preset)})"))
                    .mats.ToDictionary(x => x.role, StringComparer.Ordinal);

                Assert.IsTrue(CharacterBuilder.TryBuild(shipped, new CharacterRecipe { BuildId = build.Id },
                    CharacterAudience.Cast, out CharacterLook look, out string why), $"{build.Id}: {why}");
                Assert.AreSame(skin, look.Skin, $"{build.Id} rebuilds on another body");

                for (int m = 0; m < skin.Materials.Length; m++)
                {
                    CharacterSkinDef.Material mat = skin.Materials[m];
                    materials++;
                    if (!rig.TryGetValue(mat.Name, out MatJson want))
                    {
                        bad.Add($"{skin.Id} '{mat.Name}': not a material of the rig's {build.Preset}");
                        continue;
                    }
                    Color32[] wanted = want.cols.Select((h, i) => CharacterBuilderContractBaker.Hex(h, $"{build.Preset} {want.role}[{i}]")).ToArray();
                    if (!SameBytes(mat.Colors, wanted))
                        bad.Add($"{skin.Id} '{mat.Name}': baked {Hexes(mat.Colors)}, the rig paints {Hexes(wanted)}");
                    if (mat.Offset != want.off || mat.Gain != (float)want.gain || mat.Bias != (float)want.bias ||
                        mat.ToneLo != want.lo || mat.ToneHi != want.hi)
                        bad.Add($"{skin.Id} '{mat.Name}': baked off {mat.Offset} gain {mat.Gain} bias {mat.Bias} tones " +
                                $"{mat.ToneLo}–{mat.ToneHi}; the rig says off {want.off} gain {want.gain} bias {want.bias} " +
                                $"tones {want.lo}–{want.hi}");
                    if (!SameBytes(look.Colours[m], mat.Colors))
                        bad.Add($"{build.Id} '{mat.Name}': the builder paints {Hexes(look.Colours[m])}, the def bakes {Hexes(mat.Colors)}");
                }
            }

            Assert.IsEmpty(bad, $"{bad.Count} differences (tolerance 0):\n  " + string.Join("\n  ", bad.Take(40)));
            Assert.AreEqual(shipped.Contract.Presets.Length, shipped.Builds.Length);
            Debug.Log($"[CharacterBuilderOracleTests] the ten defs: {shipped.Builds.Length} builds, {materials} materials, " +
                      "each the rig's own colours and tones, each rebuilt by the builder unchanged.");
        }

        // =======================================================================================
        // 3. what each refuses
        // =======================================================================================

        /// <summary>
        /// The rig's <c>buildOf</c> and the builder's <see cref="CharacterBuilder.RefusePreset"/>, side
        /// by side on the rig's own presets, its NPCs and the edge keys. Every key the rig refuses, the
        /// builder refuses; the builder refuses more, and exactly these: the twenty NPCs (they come
        /// with the wardrobe), a custom build (presets only) and no key at all, which the rig quietly
        /// makes the Fisher.
        /// </summary>
        [Test]
        public void TheRigAndTheBuilder_RefuseTheSamePresets_AndTheBuilderNamesWhatItAlsoRefuses()
        {
            CharacterBuilderDef shipped = Shipped();
            string[] cast = Strings($"{G}.CAST10");
            string[] npcs = Strings($"{G}.NPCS");
            var keys = cast.Concat(npcs).Concat(new[] { "b:{}", "", "ghost", "Fisher", null }).ToList();

            var table = new StringBuilder();
            var rigRefusedBuilderBuilt = new List<string>();
            var builderOnly = new List<string>();
            foreach (string key in keys)
            {
                string rig = Host.EvaluateString(
                    "(function (C, k) { try { return 'builds ' + C.buildOf(k).key; } " +
                    $"catch (e) {{ return 'throws: ' + e.message; }} }})({G}, {Js(key)})");
                string builder = CharacterBuilder.RefusePreset(shipped, key, CharacterAudience.Cast);
                bool rigRefuses = rig.StartsWith("throws", StringComparison.Ordinal);
                table.Append($"\n  {Show(key),-12} rig: {Clip(rig)}\n  {"",-12} builder: {builder ?? "builds"}");
                if (rigRefuses && builder == null) rigRefusedBuilderBuilt.Add(Show(key));
                if (!rigRefuses && builder != null) builderOnly.Add(Show(key));
            }
            Debug.Log("[CharacterBuilderOracleTests] preset refusals, side by side:" + table);

            Assert.IsEmpty(rigRefusedBuilderBuilt, "the builder builds a preset the rig refuses:" + table);
            CollectionAssert.AreEquivalent(npcs.Concat(new[] { "b:{}", null }).Select(Show), builderOnly,
                "the builder refuses exactly the NPCs, a custom build and no key beyond the rig:" + table);
            CollectionAssert.AreEquivalent(shipped.Contract.Presets.Select(p => p.Key), cast,
                "the contract's presets are the rig's CAST10");

            StringAssert.Contains("no preset \"ghost\"", Host.EvaluateString(
                $"(function (C) {{ try {{ C.buildOf('ghost'); return ''; }} catch (e) {{ return e.message; }} }})({G})"));
            StringAssert.Contains("Unknown preset 'ghost'", CharacterBuilder.RefusePreset(shipped, "ghost", CharacterAudience.Cast));
            StringAssert.Contains("NPC preset", CharacterBuilder.RefusePreset(shipped, npcs[0], CharacterAudience.Cast));
            StringAssert.Contains("custom build", CharacterBuilder.RefusePreset(shipped, "b:{}", CharacterAudience.Cast));
            StringAssert.Contains("never falls back to the Fisher", CharacterBuilder.RefusePreset(shipped, null, CharacterAudience.Cast));
        }

        /// <summary>The rig's <c>normBuild</c> swaps an option it does not know for something of its
        /// own (the Fisher's value, a clamp, a default) and builds on. The builder refuses it, naming
        /// the field, the value and the options, on every one of the twenty fields.</summary>
        [Test]
        public void AnOptionTheRigQuietlySwaps_TheBuilderRefuses_OnEveryField()
        {
            CharacterBuilderDef shipped = Shipped();
            CharacterBuildDef fisher = shipped.BuildForPreset("fisher");
            Assert.IsNotNull(fisher, "no build for the fisher");
            string[] swaps = Host.EvaluateString($"({SwapJs})({G}, 'fisher', 'bogus')").Split('\n');

            Assert.AreEqual(CharacterRecipe.Fields.Length, swaps.Length, "one answer per rig field");
            var table = new StringBuilder();
            var bad = new List<string>();
            foreach (string line in swaps)
            {
                string[] p = line.Split('|');
                string field = p[0], rigBuilds = p[1], rigValue = p[2];
                var recipe = new CharacterRecipe { BuildId = fisher.Id };
                Assert.IsTrue(recipe.Set(field, "bogus"), $"the rig's field '{field}' is not the recipe's");
                string builder = CharacterBuilder.Refuse(shipped, recipe, CharacterAudience.Cast);
                table.Append($"\n  {field,-10} rig: {rigBuilds}, as '{rigValue}'   builder: {builder ?? "builds"}");

                if (rigBuilds != "builds" || rigValue == "bogus")
                    bad.Add($"{field}: the rig {rigBuilds} with '{rigValue}' (this test expects it to swap the value and build on)");
                if (builder == null || !builder.Contains($"{field} 'bogus' is not one of the rig's options"))
                    bad.Add($"{field}: the builder answers '{builder ?? "builds"}'");
            }
            Debug.Log("[CharacterBuilderOracleTests] an unknown option, side by side:" + table);
            Assert.IsEmpty(bad, string.Join("\n", bad) + table);
        }

        /// <summary>Children are the cast's (ruling 2): the presets the rig makes children are
        /// exactly the ones the player is refused, the cast is refused none, and a recipe that asks
        /// the player's body for the rig's 'child' is refused as cast-only.</summary>
        [Test]
        public void ThePlayer_IsRefusedExactlyThePresetsTheRigMakesChildren()
        {
            CharacterBuilderDef shipped = Shipped();
            string[] cast = Strings($"{G}.CAST10");
            string[] children = Strings($"{G}.CAST10.filter(function (p) {{ return {G}.normBuild(p).age === 'child'; }})");
            Assert.IsTrue(Host.EvaluateBool($"{G}.OPTIONS.age.indexOf('child') >= 0"), "the rig no longer offers 'child'");
            CollectionAssert.IsNotEmpty(children, "the rig's cast has no child: the cast-only rule guards nothing");

            string[] refusedThePlayer = cast.Where(p => CharacterBuilder.RefusePreset(shipped, p, CharacterAudience.Player) != null).ToArray();
            string[] refusedTheCast = cast.Where(p => CharacterBuilder.RefusePreset(shipped, p, CharacterAudience.Cast) != null).ToArray();

            CollectionAssert.AreEquivalent(children, refusedThePlayer, "the player is refused exactly the rig's children");
            CollectionAssert.IsEmpty(refusedTheCast, "the cast is built from every core preset");
            foreach (string child in children)
                StringAssert.Contains("cast-only", CharacterBuilder.RefusePreset(shipped, child, CharacterAudience.Player));

            var asksForAChild = new CharacterRecipe { BuildId = shipped.PlayerDefaultBuildId, Age = "child" };
            StringAssert.Contains("cast-only", CharacterBuilder.Refuse(shipped, asksForAChild, CharacterAudience.Player));
        }

        // =======================================================================================
        // 4. the road not taken, measured (ADR 0051)
        // =======================================================================================

        /// <summary>
        /// <b>What building a person the rig's way costs, in this editor.</b> ADR 0051 weighs a C# port
        /// of the rig's build (candidate A) against the lookup it chose. No port exists to time, so A's
        /// floor is the rig's own build in this editor's V8, cold: each core preset under a key the rig has
        /// not cached, once for <c>buildOf</c> (body, skeleton and materials) and once for
        /// <c>exportBuild</c> (the same, the shading and every clip), <see cref="CostRounds"/> rounds.
        /// A port does the same work in another language. The times are the ADR's numbers, logged, and
        /// gate nothing: only that every build ran and made a body is asserted.
        /// </summary>
        [Test]
        public void CandidateA_TheRigsOwnColdBuild_IsMilliseconds_InThisEditorsV8()
        {
            string[] cast = Strings($"{G}.CAST10");
            CollectionAssert.IsNotEmpty(cast, "the rig's CAST10 is empty");
            var built = new List<double>();
            var exported = new List<double>();
            var watch = new System.Diagnostics.Stopwatch();
            int nonce = 0;
            for (int round = 0; round < CostRounds; round++)
                foreach (string preset in cast)
                {
                    watch.Restart();
                    double faces = Host.EvaluateNumber($"({CostJs})({G}, {Js(preset)}, {++nonce}, false)");
                    watch.Stop();
                    Assert.Greater(faces, 0, $"{preset}: the rig built no bind faces");
                    built.Add(watch.Elapsed.TotalMilliseconds);

                    watch.Restart();
                    double clips = Host.EvaluateNumber($"({CostJs})({G}, {Js(preset)}, {++nonce}, true)");
                    watch.Stop();
                    Assert.Greater(clips, 0, $"{preset}: the rig exported no clips");
                    exported.Add(watch.Elapsed.TotalMilliseconds);
                }
            Assert.AreEqual(cast.Length * CostRounds, built.Count);
            Assert.AreEqual(cast.Length * CostRounds, exported.Count);

            double[] size = cast.Select(p => Host.EvaluateNumber(
                $"(function (x) {{ return JSON.stringify(x.bindMesh).length + JSON.stringify(x.clips).length; }})({G}.exportBuild({Js(p)}))")).ToArray();
            Debug.Log($"[CharacterBuilderOracleTests] candidate A, the rig's own cold build in this editor's V8 " +
                      $"({cast.Length} presets x {CostRounds}): buildOf {Spread(built)}; exportBuild (+ shading and every " +
                      $"clip) {Spread(exported)}; a figure's bind mesh and clips as JSON: median " +
                      $"{Median(size) / 1024d:F0} KiB, worst {size.Max() / 1024d:F0} KiB.");
        }

        // =======================================================================================
        // the rig's side, as plain ES5 functions of its global
        // =======================================================================================

        /// <summary>One record per build, newline-separated: <c>kind|changes|role=#hex,…|…</c>, where
        /// kind is <c>base</c>, <c>one</c> (one colour field set to one of its other options) or
        /// <c>all</c> (every colour field drawn from a seeded LCG), and changes is <c>field:value;…</c>.
        /// A role's colours are what the bake reads: a fixed material's one colour, else its ramp.</summary>
        const string PaintJs = @"function (C, p, F, n) {
  function own(o, k) { return Object.prototype.hasOwnProperty.call(o, k); }
  function str(v) { return v == null ? '' : String(v); }
  function copy(b) { var o = {}; for (var k in b) if (own(b, k)) o[k] = b[k]; return o; }
  var seed = 20261007;
  for (var i = 0; i < p.length; i++) seed = (Math.imul(seed, 31) + p.charCodeAt(i)) >>> 0;
  function rnd() { seed = (Math.imul(seed, 1103515245) + 12345) >>> 0; return seed / 4294967296; }
  var base = C.normBuild(C.BUILDS[p]), out = [];
  function emit(kind, changes, b) {
    var M = C.shadingContract(b).materials, line = [kind, changes.join(';')];
    Object.keys(M).forEach(function (r) { var m = M[r]; line.push(r + '=' + (m.fixed ? [m.color] : m.ramp).join(',')); });
    out.push(line.join('|'));
  }
  emit('base', [], base);
  F.forEach(function (f) {
    C.OPTIONS[f].forEach(function (o) {
      if (str(o) === str(base[f])) return;
      var b = copy(base); b[f] = o;
      emit('one', [f + ':' + str(o)], C.normBuild(b));
    });
  });
  for (var k = 0; k < n; k++) {
    var b = copy(base), changes = [];
    F.forEach(function (f) { var o = C.OPTIONS[f][Math.floor(rnd() * C.OPTIONS[f].length)]; b[f] = o; changes.push(f + ':' + str(o)); });
    emit('all', changes, C.normBuild(b));
  }
  return out.join('\n');
}";

        /// <summary>Per preset: every single colour option's skeleton and bind mesh against the
        /// preset's own export, and the gameplay pins (figure and clips) of the last option of each
        /// field against the preset's.</summary>
        const string StructJs = @"function (C, p, F) {
  function own(o, k) { return Object.prototype.hasOwnProperty.call(o, k); }
  function str(v) { return v == null ? '' : String(v); }
  function copy(b) { var o = {}; for (var k in b) if (own(b, k)) o[k] = b[k]; return o; }
  function pins(g) { return JSON.stringify({ figure: g.figure, clips: g.clips }); }
  var base = C.normBuild(C.BUILDS[p]), B0 = C.exportBuild(p);
  var sk0 = JSON.stringify(B0.skeleton), bm0 = JSON.stringify(B0.bindMesh), g0 = pins(C.gameplay(p));
  var flips = 0, moved = [], gameplay = 0, gameplayMoved = [];
  F.forEach(function (f) {
    var last = null;
    C.OPTIONS[f].forEach(function (o) {
      if (str(o) === str(base[f])) return;
      var b = copy(base); b[f] = o;
      var key = 'b:' + JSON.stringify(b);
      var sk = JSON.stringify(C.skeleton(key)), bm = JSON.stringify(C.bindMesh(key));
      flips++;
      if (sk !== sk0 || bm !== bm0) moved.push(f + '=' + str(o) + (sk !== sk0 ? ' moves the skeleton' : '') + (bm !== bm0 ? ' moves the bind mesh' : ''));
      last = { key: key, label: f + '=' + str(o) };
    });
    if (last !== null) { gameplay++; if (pins(C.gameplay(last.key)) !== g0) gameplayMoved.push(last.label); }
  });
  return JSON.stringify({ flips: flips, moved: moved, gameplay: gameplay, gameplayMoved: gameplayMoved });
}";

        /// <summary>A preset's materials as the bake reads them (CharacterSkinExtractor.ReadMaterials9).</summary>
        const string MatsJs = @"function (C, p) {
  var M = C.shadingContract(p).materials;
  return JSON.stringify({ mats: Object.keys(M).map(function (r) {
    var m = M[r];
    return m.fixed ? { role: r, cols: [m.color], off: 0, gain: 1, bias: 0, lo: 0, hi: 0 }
      : { role: r, cols: m.ramp.slice(), off: m.off == null ? 0 : m.off, gain: m.gain, bias: m.bias, lo: m.lo, hi: m.hi };
  }) });
}";

        /// <summary>Per rig field: the preset with that field set to <c>v</c>, as the rig's
        /// <c>buildOf</c> takes it (<c>builds</c> or the error) and the value its <c>normBuild</c>
        /// keeps: <c>field|builds|value</c>.</summary>
        const string SwapJs = @"function (C, p, v) {
  function own(o, k) { return Object.prototype.hasOwnProperty.call(o, k); }
  function str(v) { return v == null ? '' : String(v); }
  function copy(b) { var o = {}; for (var k in b) if (own(b, k)) o[k] = b[k]; return o; }
  var base = C.normBuild(C.BUILDS[p]);
  return C.FIELDS.map(function (f) {
    var b = copy(base); b[f] = v;
    var took;
    try { C.buildOf('b:' + JSON.stringify(b)); took = 'builds'; } catch (e) { took = 'throws ' + e.message; }
    return f + '|' + took + '|' + str(C.normBuild(b)[f]);
  }).join('\n');
}";

        /// <summary>The preset under a key the rig has never seen (its own values plus a nonce, which
        /// its <c>normBuild</c> drops), so the rig's build cache cannot answer: <c>buildOf</c>'s bind face
        /// count, or with <c>whole</c> the clip count of a full <c>exportBuild</c>.</summary>
        const string CostJs = @"function (C, p, k, whole) {
  function own(o, x) { return Object.prototype.hasOwnProperty.call(o, x); }
  var b = C.normBuild(C.BUILDS[p]), o = {};
  for (var f in b) if (own(b, f)) o[f] = b[f];
  o.nonce = k;
  var key = 'b:' + JSON.stringify(o);
  return whole ? C.exportBuild(key).clips.length : C.buildOf(key).mesh.faces.length;
}";

#pragma warning disable 0649 // written by JsonUtility
        [Serializable] sealed class StructJson { public int flips, gameplay; public string[] moved, gameplayMoved; }
        [Serializable] sealed class MatJson { public string role; public string[] cols; public int off, lo, hi; public double gain, bias; }
        [Serializable] sealed class MatsJson { public MatJson[] mats; }
        [Serializable] sealed class StringsJson { public string[] a; }
#pragma warning restore 0649

        // =======================================================================================
        // helpers
        // =======================================================================================

        static CharacterBuilderDef Shipped()
        {
            var shipped = AssetDatabase.LoadAssetAtPath<CharacterBuilderDef>(CharacterBuilderContractBaker.CatalogPath);
            Assert.IsNotNull(shipped, $"{CharacterBuilderContractBaker.CatalogPath} is missing: run the builder's bake " +
                                      "(Hidden Harbours ▸ … ▸ Bake character BUILDER tables) and commit what it writes");
            Assert.IsNotNull(shipped.Contract, $"{shipped.Id} names no contract");
            Assert.IsNotNull(shipped.Ramps, $"{shipped.Id} names no ramps");
            return shipped;
        }

        static List<CharacterBuildDef> BuildsInFolder() =>
            AssetDatabase.FindAssets("t:" + nameof(CharacterBuildDef), new[] { CharacterBuilderContractBaker.BuildsFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<CharacterBuildDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(b => b != null).ToList();

        static string[] ColourFields(CharacterRigContractDef contract) =>
            contract.Fields.Where(f => f.Kind == CharacterFieldKind.Colour).Select(f => f.Name).ToArray();

        string[] Strings(string expression) =>
            JsonUtility.FromJson<StringsJson>(Host.EvaluateString($"JSON.stringify({{ a: {expression} }})")).a;

        static double Median(IReadOnlyList<double> xs)
        {
            double[] s = xs.OrderBy(x => x).ToArray();
            return s.Length % 2 == 1 ? s[s.Length / 2] : (s[s.Length / 2 - 1] + s[s.Length / 2]) / 2d;
        }

        static string Spread(IReadOnlyList<double> ms)
        {
            double[] s = ms.OrderBy(x => x).ToArray();
            double p95 = s[Math.Min(s.Length - 1, (int)Math.Ceiling(0.95 * s.Length) - 1)];
            return $"median {Median(s):F3} ms, p95 {p95:F3} ms, worst {s[s.Length - 1]:F3} ms";
        }

        T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            _made.Add(o);
            return o;
        }

        /// <summary>The two as the serializer writes them, field for field; on a difference, the
        /// first line that differs with a little of what is around it.</summary>
        static void AssertSameJson(Object committed, Object fresh, string path)
        {
            string a = JsonUtility.ToJson(committed, true), b = JsonUtility.ToJson(fresh, true);
            if (a == b) return;
            string[] la = a.Split('\n'), lb = b.Split('\n');
            int i = 0;
            while (i < la.Length && i < lb.Length && la[i] == lb[i]) i++;
            int from = Math.Max(0, i - 3);
            string Around(string[] l) => string.Join("\n    ", l.Skip(from).Take(i - from + 3));
            Assert.Fail($"{path} is not what the lift makes of the rig today: line {i + 1} differs.\n" +
                        $"  committed:\n    {Around(la)}\n  lifted today:\n    {Around(lb)}\n" +
                        "Re-run the builder's bake and commit it, or find what edited the asset.");
        }

        static Color32[] Colours(string hexes, string where) =>
            hexes.Split(',').Select((h, i) => CharacterBuilderContractBaker.Hex(h, $"{where}[{i}]")).ToArray();

        static bool SameBytes(Color32[] a, Color32[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b || a[i].a != b[i].a) return false;
            return true;
        }

        static string Hexes(Color32[] c) =>
            c == null ? "nothing" : "[" + string.Join(",", c.Select(x => $"#{x.r:x2}{x.g:x2}{x.b:x2}")) + "]";

        static string Js(string s) =>
            s == null ? "null" : "'" + s.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n") + "'";

        static string JsArray(IEnumerable<string> items) => "[" + string.Join(", ", items.Select(Js)) + "]";

        static string Show(string key) => key == null ? "null" : $"'{key}'";

        static string Clip(string s) => s.Length <= 90 ? s : s.Substring(0, 90) + "…";
    }
}
