using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE SHIPPED CHARACTER BUILDER</b> (character PR 2b, ADR 0051): the catalog under Resources,
    /// its rig contract, ramps and fit, and the ten builds, as the game loads them. No JS runs here:
    /// that the tables are rig 10.3's own is <c>CharacterBuilderOracleTests</c>' (RigBaking), which
    /// runs the rig in V8. These hold that the tables are whole and agree with each other and with
    /// the ten baked bodies, that the player is offered no child, that the vest takes its bottom's
    /// colour, what the builder's ramps changed from the wardrobe's, and what a build costs.
    /// </summary>
    public class CharacterBuilderContentTests
    {
        const string CatalogPath = "Assets/_Project/Resources/" + CharacterBuilderDef.ResourcesPath + ".asset";

        /// <summary>The wardrobe's ramps (rig 6's options), which the sprite layer recolours with.</summary>
        const string WardrobeRampsPath = "Assets/_Project/Data/Characters/CharacterRamps.asset";

        /// <summary>The rig's colour fields, in its FIELDS order (colour option (b): a key per role).</summary>
        static readonly string[] ColourFields = { "skin", "hair", "eyes", "outfit", "shirt", "hatCol", "apronCol" };

        static CharacterBuilderDef Shipped()
        {
            CharacterBuilderDef builder = CharacterBuilderDef.LoadShipped();
            Assert.IsNotNull(builder, $"Resources/{CharacterBuilderDef.ResourcesPath} is missing: run the builder's " +
                                      "bake (CharacterBuilderContractBaker) and commit what it writes");
            Assert.IsNotNull(builder.Contract, $"{builder.Id} names no contract");
            Assert.IsNotNull(builder.Ramps, $"{builder.Id} names no ramps");
            Assert.IsNotNull(builder.Fit, $"{builder.Id} names no fit");
            return builder;
        }

        // ---- the tables ---------------------------------------------------------------------------

        [Test]
        public void TheShippedBuilder_LoadsFromResources_WithItsOwnTables_OneEntityPerFile()
        {
            CharacterBuilderDef b = Shipped();

            Assert.AreEqual("builder.character_iso10", b.Id);
            Assert.AreEqual(CatalogPath, AssetDatabase.GetAssetPath(b));
            Assert.AreEqual("contract.character_iso10", b.Contract.Id);
            Assert.AreEqual("ramps.character_iso10", b.Ramps.Id);
            Assert.IsNotNull(b.Fit.Spec, "the fit carries no spec");
            Assert.AreEqual("fit.character_v10", b.FitId);
            Assert.AreEqual("skeleton.character_v10", b.Fit.Spec.SkeletonId);
            StringAssert.IsMatch("^[0-9a-f]{64}$", b.Contract.SourceRigSha256, "the contract names no rig source hash");
            Assert.AreEqual(b.Contract.SourceRigSha256, b.Fit.Spec.SourceRigSha256,
                "the contract and the fit were lifted from different rigs");
            StringAssert.Contains("characterIsoRig10.js", b.Contract.SourceRig);
            StringAssert.Contains("characterIsoRig10.js", b.Ramps.SourceRig);

            string[] paths = new Object[] { b, b.Contract, b.Ramps, b.Fit }.Select(o => AssetDatabase.GetAssetPath(o)).ToArray();
            CollectionAssert.AllItemsAreUnique(paths, "one entity per file (rule 2)");
            foreach (string path in paths) StringAssert.EndsWith(".asset", path, "a table that is not its own asset");
        }

        [Test]
        public void TheContract_OffersTheRecipesTwentyFields_SevenOfThemColour_AndTenPresets()
        {
            CharacterRigContractDef c = Shipped().Contract;

            CollectionAssert.AreEqual(CharacterRecipe.Fields, c.Fields.Select(f => f.Name).ToArray(),
                "the contract's fields are the recipe's, in the rig's order");
            CollectionAssert.AreEqual(ColourFields,
                c.Fields.Where(f => f.Kind == CharacterFieldKind.Colour).Select(f => f.Name).ToArray(),
                "colour option (b): a key per role, on the rig's seven colour fields");
            foreach (CharacterField f in c.Fields)
            {
                CollectionAssert.IsNotEmpty(f.Options, $"{f.Name} offers nothing");
                CollectionAssert.AllItemsAreUnique(f.Options, $"{f.Name} offers an option twice");
            }
            CollectionAssert.AreEqual(new[] { "age child" },
                c.Fields.SelectMany(f => (f.CastOnly ?? Array.Empty<string>()).Select(v => $"{f.Name} {v}")).ToArray(),
                "ruling 2: children are the cast's, and nothing else is held back");

            Assert.AreEqual(10, c.Presets.Length, "presets only: the ten core presets (owner)");
            Assert.AreEqual("fisher", c.Presets[0].Key);
            foreach (CharacterPresetValues p in c.Presets)
            {
                Assert.AreEqual(c.Fields.Length, p.Values.Length, $"{p.Key} carries the wrong number of values");
                for (int i = 0; i < c.Fields.Length; i++)
                    Assert.IsTrue(c.Fields[i].Allows(p.Values[i]), $"{p.Key}'s {c.Fields[i].Name} '{p.Values[i]}' is not an option");
            }
            CollectionAssert.IsNotEmpty(c.NpcPresets, "the NPC presets the builder refuses by name are missing");
            CollectionAssert.IsEmpty(c.NpcPresets.Intersect(c.Presets.Select(p => p.Key)), "an NPC preset is also a core one");
        }

        [Test]
        public void TheRamps_AreOnePerColourOption_PlusTheFixedOnes_EachIdItsOwn()
        {
            CharacterBuilderDef b = Shipped();
            var ids = new List<string>();
            int axisRamps = 0;
            foreach (string f in ColourFields)
            {
                CharacterField field = b.Contract.Field(f);
                CharacterRampAxis axis = b.Ramps.Axis(f);
                Assert.IsNotNull(axis, $"no ramps for {f}");
                CollectionAssert.AreEqual(field.Options, axis.Ramps.Select(r => r.Key).ToArray(),
                    $"{f}: one ramp per option, in the rig's order");
                Assert.IsTrue(field.Allows(axis.DefaultKey), $"{f}'s default '{axis.DefaultKey}' is not an option");
                foreach (CharacterRamp r in axis.Ramps)
                {
                    Assert.AreEqual($"ramp.{f}.{r.Key}", r.Id);
                    Assert.IsTrue(r.IsUsable, $"{r.Id} has no shades");
                    ids.Add(r.Id);
                }
                axisRamps += axis.Ramps.Length;
            }
            foreach (CharacterRamp r in b.Ramps.Fixed)
            {
                Assert.AreEqual($"ramp.fixed.{r.Key}", r.Id);
                Assert.IsTrue(r.IsUsable, $"{r.Id} has no shades");
                ids.Add(r.Id);
            }

            Assert.AreEqual(ColourFields.Length, b.Ramps.Axes.Length, "an axis that is no colour field");
            Assert.AreEqual(b.Ramps.RampCount, axisRamps);
            Assert.AreEqual(69, axisRamps, "rig 10.3 offers 69 colour options");
            Assert.AreEqual(6, b.Ramps.Fixed.Length, "rig 10.3 paints six fixed ramps (belt, boot, brass, ink, ink2, white)");
            CollectionAssert.AllItemsAreUnique(ids, "ids are stable and unique (rule 2)");
        }

        [Test]
        public void EveryPaintRule_NamesAColourField_AFixedRamp_OrAWholeTable()
        {
            CharacterBuilderDef b = Shipped();
            CharacterRigContractDef c = b.Contract;
            CollectionAssert.AllItemsAreUnique(c.Paint.Select(r => r.Role), "a role is painted by two rules");
            foreach (CharacterPaintRule r in c.Paint)
            {
                switch (r.Source)
                {
                    case CharacterPaintSource.Axis:
                        Assert.AreEqual(CharacterFieldKind.Colour, c.Field(r.Field)?.Kind, $"{r.Role} ramps through '{r.Field}'");
                        break;
                    case CharacterPaintSource.Fixed:
                        Assert.IsNotNull(b.Ramps.FixedRamp(r.FixedKey), $"{r.Role} names the fixed ramp '{r.FixedKey}'");
                        break;
                    case CharacterPaintSource.Table:
                    {
                        CollectionAssert.IsNotEmpty(r.TableFields, $"{r.Role} is a table of nothing");
                        int combos = 1;
                        foreach (string f in r.TableFields)
                        {
                            Assert.AreEqual(CharacterFieldKind.Colour, c.Field(f)?.Kind, $"{r.Role} is tabled by '{f}'");
                            combos *= c.Field(f).Options.Length;
                        }
                        Assert.AreEqual(combos, r.Rows.Length, $"{r.Role}: one row per combination of {string.Join(" and ", r.TableFields)}");
                        CollectionAssert.AllItemsAreUnique(r.Rows.Select(w => string.Join(",", w.Keys)), $"{r.Role} has a row twice");
                        foreach (CharacterPaintRow w in r.Rows)
                        {
                            CollectionAssert.IsNotEmpty(w.Colors, $"{r.Role} row {string.Join(",", w.Keys)} has no colours");
                            for (int k = 0; k < w.Keys.Length; k++)
                                Assert.IsTrue(c.Field(r.TableFields[k]).Allows(w.Keys[k]), $"{r.Role}: '{w.Keys[k]}' is not an option");
                        }
                        break;
                    }
                    default:
                        Assert.Fail($"{r.Role}: unknown source {r.Source}");
                        break;
                }
            }
        }

        // ---- the builds ---------------------------------------------------------------------------

        [Test]
        public void TheCatalog_HasOneBuildPerPreset_TheFisherTheDefault_AndNoNpcBuild()
        {
            CharacterBuilderDef b = Shipped();
            CharacterRigContractDef c = b.Contract;

            Assert.AreEqual(c.Presets.Length, b.Builds.Length, "one build per preset");
            for (int i = 0; i < c.Presets.Length; i++)
            {
                CharacterBuildDef build = b.Builds[i];
                Assert.IsNotNull(build, $"build {i} is missing");
                Assert.AreEqual(c.Presets[i].Key, build.Preset, "the builds follow the contract's order");
                Assert.AreEqual(CharacterBuilder.BuildIdPrefix + build.Preset, build.Id);
                Assert.IsNotNull(build.Visual?.Skin, $"{build.Id} has no baked body");
            }
            CollectionAssert.AllItemsAreUnique(b.Builds.Select(x => AssetDatabase.GetAssetPath(x)), "one entity per file (rule 2)");
            Assert.AreEqual("char_build.fisher", b.PlayerDefaultBuildId);
            Assert.IsNotNull(b.Build(b.PlayerDefaultBuildId), "the player's default build is not in the catalog");
            foreach (string npc in c.NpcPresets)
                Assert.IsNull(b.BuildForPreset(npc), $"'{npc}' has a build: the NPCs come with the wardrobe, after PR 3");
        }

        /// <summary>The ten defs rebuilt through the builder: each preset's own build resolves to its
        /// own baked body in exactly its baked colours, rigged on the fit's skeleton, within the
        /// material limit.</summary>
        [Test]
        public void EveryBuild_RebuildsForTheCast_InItsBakedColours_OnTheFitsSkeleton()
        {
            CharacterBuilderDef b = Shipped();
            int materials = 0;
            foreach (CharacterBuildDef build in b.Builds)
            {
                CharacterSkinDef skin = build.Visual.Skin;
                Assert.IsTrue(CharacterBuilder.TryBuild(b, new CharacterRecipe { BuildId = build.Id }, CharacterAudience.Cast,
                    out CharacterLook look, out string why), $"{build.Id}: {why}");
                Assert.AreSame(skin, look.Skin, $"{build.Id} resolves to another body");
                CollectionAssert.AreEqual(b.Contract.Preset(build.Preset).Values, look.Values, $"{build.Id} resolves other values");
                for (int m = 0; m < skin.Materials.Length; m++)
                    Assert.IsTrue(Same(look.Colours[m], skin.Materials[m].Colors),
                        $"{build.Id} rebuilds '{skin.Materials[m].Name}' as {Hexes(look.Colours[m])}; '{skin.Id}' bakes {Hexes(skin.Materials[m].Colors)}");
                CollectionAssert.AreEqual(b.Fit.Spec.BoneIds, skin.Bones.Select(x => x.Id).ToArray(),
                    $"'{skin.Id}' is not rigged on {b.FitId}'s skeleton");
                Assert.LessOrEqual(skin.Materials.Length, CharacterSkinDef.MaxMaterials(skin.ToneRule), $"'{skin.Id}' paints too many materials");
                materials += skin.Materials.Length;
            }
            Debug.Log($"[CharacterBuilderContentTests] the ten defs rebuilt: {b.Builds.Length} builds, {materials} materials, " +
                      $"each in its baked colours, on {b.Fit.Spec.BoneIds.Length} bones.");
        }

        [Test]
        public void ThePlayer_IsOfferedNoChild_AndTheCastIsBuiltFromEveryPreset()
        {
            CharacterBuilderDef b = Shipped();
            CharacterRigContractDef c = b.Contract;
            int age = c.FieldIndex("age");
            string[] children = c.Presets.Where(p => p.Values[age] == "child").Select(p => p.Key).ToArray();
            CollectionAssert.IsNotEmpty(children, "no preset is a child: the cast-only rule guards nothing");

            foreach (CharacterPresetValues p in c.Presets)
            {
                Assert.IsNull(CharacterBuilder.RefusePreset(b, p.Key, CharacterAudience.Cast), $"the cast is refused '{p.Key}'");
                string player = CharacterBuilder.RefusePreset(b, p.Key, CharacterAudience.Player);
                if (children.Contains(p.Key))
                {
                    StringAssert.Contains("cast-only", player);
                    StringAssert.Contains("cast-only", CharacterBuilder.Refuse(b,
                        new CharacterRecipe { BuildId = b.BuildForPreset(p.Key).Id }, CharacterAudience.Player));
                }
                else Assert.IsNull(player, $"the player is refused '{p.Key}'");
            }
            StringAssert.Contains("cast-only", CharacterBuilder.Refuse(b, new CharacterRecipe { Age = "child" }, CharacterAudience.Player));
            Assert.IsNull(CharacterBuilder.Refuse(b, new CharacterRecipe(), CharacterAudience.Player),
                "an empty recipe is the player's default build, as baked");
        }

        /// <summary>The vest has no colour of its own: it and its bottom paint the outfit's roles, so
        /// the recipe's one outfit key recolours both, and nothing else.</summary>
        [Test]
        public void TheVest_AndItsBottom_AreOneOutfitColour()
        {
            CharacterBuilderDef b = Shipped();
            CharacterRigContractDef c = b.Contract;
            foreach (string role in new[] { "over", "overD", "overL" })
            {
                CharacterPaintRule r = c.Rule(role);
                Assert.IsNotNull(r, $"no rule paints '{role}'");
                Assert.AreEqual(CharacterPaintSource.Axis, r.Source, $"'{role}' is not a ramp of one field");
                Assert.AreEqual("outfit", r.Field, $"'{role}' ramps through '{r.Field}'");
            }

            int garment = c.FieldIndex("garment"), outfit = c.FieldIndex("outfit");
            CharacterPresetValues vested = c.Presets.FirstOrDefault(p => p.Values[garment] == "vest");
            Assert.IsNotNull(vested, "no preset wears the vest");
            string other = c.Fields[outfit].Options.First(o => o != vested.Values[outfit]);
            string id = b.BuildForPreset(vested.Key).Id;
            Assert.IsTrue(CharacterBuilder.TryBuild(b, new CharacterRecipe { BuildId = id }, CharacterAudience.Cast,
                out CharacterLook own, out string why), why);
            Assert.IsTrue(CharacterBuilder.TryBuild(b, new CharacterRecipe { BuildId = id, Outfit = other }, CharacterAudience.Cast,
                out CharacterLook recoloured, out why), why);

            int moved = 0;
            for (int m = 0; m < own.Skin.Materials.Length; m++)
            {
                CharacterPaintRule rule = c.Rule(own.Skin.Materials[m].Name);
                bool outfitRole = rule.Source == CharacterPaintSource.Axis && rule.Field == "outfit";
                bool changed = !Same(own.Colours[m], recoloured.Colours[m]);
                Assert.AreEqual(outfitRole, changed, $"{vested.Key} in outfit '{other}': '{rule.Role}' " +
                                                     (changed ? "changes, though it is not the outfit's" : "keeps its colour"));
                if (outfitRole)
                {
                    Assert.IsTrue(Same(b.Ramps.Ramp("outfit", other).Shades, recoloured.Colours[m]));
                    moved++;
                }
            }
            Assert.Greater(moved, 0, $"{vested.Key}'s vest and bottom paint no outfit role");
        }

        /// <summary>
        /// The builder's ramps against the wardrobe's (ramps.character_iso, rig 6's options), which
        /// the sprite layer still recolours with and which this PR leaves alone: every wardrobe ramp
        /// is here in the same shades but one, hair.white, whose lightest shade rig 10.3 moved from
        /// #e8eae3 to #dfe1db, and rig 10.3 adds 24 keys. If the wardrobe is re-baked from rig 10,
        /// hair.white leaves this list.
        /// </summary>
        [Test]
        public void TheBuildersRamps_KeepEveryWardrobeRamp_ButHairWhite_AndAddTwentyFourKeys()
        {
            CharacterBuilderDef b = Shipped();
            var wardrobe = AssetDatabase.LoadAssetAtPath<CharacterRampsDef>(WardrobeRampsPath);
            Assert.IsNotNull(wardrobe, $"{WardrobeRampsPath} is missing");
            Assert.AreEqual("ramps.character_iso", wardrobe.Id);
            Assert.AreNotSame(wardrobe, b.Ramps, "the builder's ramps are its own asset");

            var missing = new List<string>();
            var changed = new List<string>();
            var detail = new List<string>();
            foreach (CharacterRampAxis axis in wardrobe.Axes)
                foreach (CharacterRamp old in axis.Ramps)
                {
                    CharacterRamp mine = b.Ramps.Ramp(axis.Axis, old.Key);
                    if (mine == null) missing.Add($"{axis.Axis}.{old.Key}");
                    else if (!Same(old.Shades, mine.Shades))
                    {
                        changed.Add($"{axis.Axis}.{old.Key}");
                        detail.Add($"{axis.Axis}.{old.Key} {Hexes(old.Shades)} → {Hexes(mine.Shades)}");
                    }
                }
            var added = b.Ramps.Axes.SelectMany(a => a.Ramps.Where(r => wardrobe.Ramp(a.Axis, r.Key) == null)
                .Select(r => $"{a.Axis}.{r.Key}")).ToList();

            CollectionAssert.IsEmpty(missing, "the builder lost a wardrobe colour");
            CollectionAssert.AreEqual(new[] { "hair.white" }, changed, "the ramps that differ from the wardrobe's:\n  " +
                                                                      string.Join("\n  ", detail));
            Assert.AreEqual("#e8eae3", Hex(wardrobe.Ramp("hair", "white").Shades.Last()));
            Assert.AreEqual("#dfe1db", Hex(b.Ramps.Ramp("hair", "white").Shades.Last()));
            Assert.AreEqual(24, added.Count, "the keys rig 10.3 adds: " + string.Join(" ", added));
            Debug.Log($"[CharacterBuilderContentTests] ramps: {b.Ramps.RampCount} axis ramps against the wardrobe's " +
                      $"{wardrobe.RampCount}; changed {string.Join("; ", detail)}; added {added.Count}: {string.Join(" ", added)}.");
        }

        // ---- the cost (ADR 0051) ------------------------------------------------------------------

        /// <summary>
        /// A build is a lookup: every preset and every single colour option of it (630 recipes), five
        /// rounds after a warm-up, timed one by one, with the bytes it allocates. The look holds the
        /// tables' own ramps, so a build allocates only its value list, its ramp list and the look. The
        /// numbers are logged for ADR 0051; the bounds are loose enough for any machine.
        /// </summary>
        [Test]
        public void ABuild_IsALookup_FarInsideAFrame_AndAllocatesOnlyTheLook()
        {
            CharacterBuilderDef b = Shipped();
            CharacterRigContractDef c = b.Contract;
            var recipes = new List<CharacterRecipe>();
            foreach (CharacterPresetValues p in c.Presets)
            {
                string id = b.BuildForPreset(p.Key).Id;
                recipes.Add(new CharacterRecipe { BuildId = id });
                foreach (string f in ColourFields)
                    foreach (string o in c.Field(f).Options)
                    {
                        if (o == p.Values[c.FieldIndex(f)]) continue;
                        var r = new CharacterRecipe { BuildId = id };
                        r.Set(f, o);
                        recipes.Add(r);
                    }
            }
            foreach (CharacterRecipe r in recipes)
                Assert.IsTrue(CharacterBuilder.TryBuild(b, r, CharacterAudience.Cast, out _, out string why), why);

            const int Rounds = 5;
            var ticks = new long[recipes.Count * Rounds];
            int n = 0, gen0 = GC.CollectionCount(0);
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int round = 0; round < Rounds; round++)
                foreach (CharacterRecipe r in recipes)
                {
                    long t0 = Stopwatch.GetTimestamp();
                    CharacterBuilder.TryBuild(b, r, CharacterAudience.Cast, out _, out _);
                    ticks[n++] = Stopwatch.GetTimestamp() - t0;
                }
            allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
            gen0 = GC.CollectionCount(0) - gen0;

            Array.Sort(ticks);
            double Us(long t) => t * 1e6 / Stopwatch.Frequency;
            double median = Us(ticks[n / 2]), p95 = Us(ticks[(int)(n * 0.95)]), worst = Us(ticks[n - 1]);
            double perBuild = allocated / (double)n;
            int maxMaterials = b.Builds.Max(x => x.Visual.Skin.Materials.Length);
            int maxShades = b.Builds.Max(x => x.Visual.Skin.Materials.Max(m => m.Colors?.Length ?? 0));
            long colourBytes = 4L * (b.Ramps.Axes.Sum(a => a.Ramps.Sum(r => r.Shades.Length)) +
                                     b.Ramps.Fixed.Sum(r => r.Shades.Length) +
                                     c.Paint.Sum(r => r.Rows.Sum(w => w.Colors.Length)));
            long fileBytes = new Object[] { b, c, b.Ramps, b.Fit }
                .Sum(o => new FileInfo(AssetDatabase.GetAssetPath(o)).Length);

            Debug.Log($"[CharacterBuilderContentTests] ADR 0051 cost: {n} builds ({recipes.Count} recipes × {Rounds}): " +
                      $"median {median:F1} µs, p95 {p95:F1} µs, worst {worst:F1} µs; {perBuild:F0} B allocated per build, " +
                      $"{gen0} gen-0 collections; a recolour redraws ≤ {maxMaterials} ramps of ≤ {maxShades} shades " +
                      $"(3 RGBA32 textures, ≤ {3 * 4 * maxMaterials * maxShades} B, replacing the figure's own); the tables " +
                      $"hold {colourBytes} B of colour, {fileBytes} B on disk, loaded once.");

            // Only the median is bounded: the worst of thousands catches whatever the editor's
            // collector or another thread did meanwhile, and is reported, not asserted.
            Assert.Less(median, 1000.0, "a build's median is over a millisecond: it is no longer a lookup");
            if (CountsAllocations())
                Assert.Less(perBuild, 4096.0, "a build allocates more than its look");
            else
                Debug.Log("[CharacterBuilderContentTests] GC.GetAllocatedBytesForCurrentThread counts nothing on this " +
                          "runtime: the allocation bound is not checked.");
        }

        // ---- helpers ------------------------------------------------------------------------------

        static bool CountsAllocations()
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            var probe = new byte[4096];
            GC.KeepAlive(probe);
            return GC.GetAllocatedBytesForCurrentThread() - before > 0;
        }

        static bool Same(Color32[] a, Color32[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b || a[i].a != b[i].a) return false;
            return true;
        }

        static string Hex(Color32 c) => $"#{c.r:x2}{c.g:x2}{c.b:x2}";

        static string Hexes(Color32[] c) => c == null ? "nothing" : "[" + string.Join(",", c.Select(Hex)) + "]";
    }
}
