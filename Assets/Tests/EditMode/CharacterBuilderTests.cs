using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE CHARACTER BUILDER'S OWN RULES</b> (character PR 2b, ADR 0051, candidate C), on a small
    /// world of its own: two presets, four fields, one material per kind of paint.
    ///
    /// <list type="bullet">
    /// <item>An empty recipe is the player's default build in its baked colours.</item>
    /// <item>A colour value paints only the materials its rule names, from the tables: an axis ramp, a
    /// fixed ramp, a table row. The build's own overrides sit under the recipe's.</item>
    /// <item>Presets only: a structural value other than the preset's own is refused, naming it.</item>
    /// <item>Every refusal names what it refused: an unknown option (listing the options), a cast-only
    /// value or preset for the player, an NPC, custom, unknown or missing preset, an unknown build, a
    /// newer recipe, another fit, a def the contract does not describe or shades otherwise, a ramp of
    /// another length, too many materials, a missing table.</item>
    /// </list>
    ///
    /// <para>That the SHIPPED tables answer as the rig does, colour for colour on every preset and every
    /// option, and refuse what the rig refuses, is <c>CharacterBuilderOracleTests</c>'s, against the rig
    /// in V8.</para>
    /// </summary>
    public class CharacterBuilderTests
    {
        private const string Fisher = "fisher", Boy = "boy", Joe = "joe";
        private const string FisherBuild = "char_build.fisher", BoyBuild = "char_build.boy";
        private const string Fit = "fit.character_v10";

        private static readonly Color32[] Fair = { C(10), C(11), C(12), C(13) };
        private static readonly Color32[] Tan = { C(20), C(21), C(22), C(23) };
        private static readonly Color32[] Navy = { C(30), C(31), C(32) };
        private static readonly Color32[] Red = { C(40), C(41), C(42) };
        private static readonly Color32[] Leather = { C(50) };
        private static readonly Color32[] LipFair = { C(60) };
        private static readonly Color32[] LipTan = { C(61) };

        private readonly List<Object> _made = new List<Object>();
        private CharacterBuilderDef _builder;
        private CharacterRigContractDef _contract;
        private CharacterRampsDef _ramps;
        private CharacterSkinDef _fisherSkin, _boySkin;
        private CharacterBuildDef _fisherBuild, _boyBuild;

        [SetUp]
        public void SetUp()
        {
            _contract = Make<CharacterRigContractDef>();
            _contract.Id = "contract.test";
            _contract.Fields = new[]
            {
                new CharacterField { Name = "shape", Kind = CharacterFieldKind.Structure, Options = new[] { "0", "0.5", "1" } },
                new CharacterField { Name = "age", Kind = CharacterFieldKind.Structure, Options = new[] { "child", "adult" },
                                     CastOnly = new[] { "child" } },
                new CharacterField { Name = "skin", Kind = CharacterFieldKind.Colour, Options = new[] { "fair", "tan" } },
                new CharacterField { Name = "outfit", Kind = CharacterFieldKind.Colour, Options = new[] { "navy", "red" } },
            };
            _contract.Presets = new[]
            {
                new CharacterPresetValues { Key = Fisher, Values = new[] { "0.5", "adult", "fair", "navy" } },
                new CharacterPresetValues { Key = Boy, Values = new[] { "0", "child", "tan", "red" } },
            };
            _contract.NpcPresets = new[] { Joe };
            _contract.Paint = new[]
            {
                new CharacterPaintRule { Role = "skin", Source = CharacterPaintSource.Axis, Field = "skin",
                                         Gain = 2.4f, Bias = 2.7f, ToneLo = 2, ToneHi = 5 },
                new CharacterPaintRule { Role = "coat", Source = CharacterPaintSource.Axis, Field = "outfit",
                                         Gain = 2.4f, Bias = 1.8f, ToneLo = 1, ToneHi = 4 },
                new CharacterPaintRule { Role = "boot", Source = CharacterPaintSource.Fixed, FixedKey = "boot", Gain = 1f },
                new CharacterPaintRule
                {
                    Role = "lip", Source = CharacterPaintSource.Table, TableFields = new[] { "skin" }, Gain = 1f,
                    Rows = new[]
                    {
                        new CharacterPaintRow { Keys = new[] { "fair" }, Colors = LipFair },
                        new CharacterPaintRow { Keys = new[] { "tan" }, Colors = LipTan },
                    },
                },
            };

            _ramps = Make<CharacterRampsDef>();
            _ramps.Id = "ramps.test";
            _ramps.Axes = new[]
            {
                Axis("skin", "fair", ("fair", Fair), ("tan", Tan)),
                Axis("outfit", "navy", ("navy", Navy), ("red", Red)),
            };
            _ramps.Fixed = new[] { new CharacterRamp { Id = "ramp.fixed.boot", Key = "boot", Shades = Leather } };

            _fisherSkin = Skin("skin.fisher", Fair, Navy, LipFair);
            _boySkin = Skin("skin.boy", Tan, Red, LipTan);
            _fisherBuild = Build(FisherBuild, Fisher, _fisherSkin);
            _boyBuild = Build(BoyBuild, Boy, _boySkin);

            var fit = Make<CharacterClothingFitDef>();
            fit.Spec = new CharacterClothingFitSpec { Id = Fit };

            _builder = Make<CharacterBuilderDef>();
            _builder.Id = "builder.test";
            _builder.Contract = _contract;
            _builder.Ramps = _ramps;
            _builder.Fit = fit;
            _builder.Builds = new[] { _fisherBuild, _boyBuild };
            _builder.PlayerDefaultBuildId = FisherBuild;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        // ---- what it builds ----------------------------------------------------------------------

        [Test]
        public void AnEmptyRecipe_IsThePlayersDefaultBuild_InItsBakedColours()
        {
            CharacterLook look = Built(new CharacterRecipe(), CharacterAudience.Player);

            Assert.AreSame(_fisherBuild, look.Build);
            Assert.AreSame(_fisherSkin, look.Skin, "structure is baked: the preset's own def, untouched");
            CollectionAssert.AreEqual(new[] { "0.5", "adult", "fair", "navy" }, look.Values);
            for (int m = 0; m < _fisherSkin.Materials.Length; m++)
                CollectionAssert.AreEqual(_fisherSkin.Materials[m].Colors, look.Colours[m],
                    $"material '{_fisherSkin.Materials[m].Name}' is not its baked colours");
        }

        [Test]
        public void AColourValue_PaintsOnlyTheMaterialsItsRulesName_FromTheTables()
        {
            CharacterLook look = Built(Recipe(("skin", "tan")), CharacterAudience.Player);

            CollectionAssert.AreEqual(Tan, look.Colours[0], "the skin's axis ramp");
            CollectionAssert.AreEqual(Navy, look.Colours[1], "the coat moved with the skin");
            CollectionAssert.AreEqual(Leather, look.Colours[2], "a fixed ramp moved");
            CollectionAssert.AreEqual(LipTan, look.Colours[3], "the lip's table row, keyed by the skin");
            Assert.AreSame(_fisherSkin, look.Skin, "a colour moved the body");
        }

        [Test]
        public void TheBuildsOwnColours_SitUnderTheRecipes()
        {
            _fisherBuild.Outfit = "red";

            CollectionAssert.AreEqual(Red, Built(new CharacterRecipe { BuildId = FisherBuild }, CharacterAudience.Player).Colours[1],
                "the build's own outfit was not applied");
            CollectionAssert.AreEqual(Navy, Built(Recipe(("outfit", "navy")), CharacterAudience.Player).Colours[1],
                "the recipe's outfit did not win over the build's");
        }

        [Test]
        public void RestatingThePresetsOwnStructure_IsAllowed()
        {
            CharacterLook look = Built(Recipe(("shape", "0.5"), ("age", "adult")), CharacterAudience.Player);
            Assert.AreSame(_fisherSkin, look.Skin);
        }

        [Test]
        public void TheCast_IsBuiltFromAnyPreset_ChildrenIncluded()
        {
            CharacterLook look = Built(new CharacterRecipe { BuildId = BoyBuild }, CharacterAudience.Cast);

            Assert.AreSame(_boySkin, look.Skin);
            CollectionAssert.AreEqual(Tan, look.Colours[0]);
            CollectionAssert.AreEqual(LipTan, look.Colours[3]);
            Assert.IsNull(CharacterBuilder.RefusePreset(_builder, Boy, CharacterAudience.Cast));
        }

        // ---- presets only ------------------------------------------------------------------------

        [Test]
        public void AStructuralValue_OtherThanThePresetsOwn_IsRefused_NamingIt()
        {
            string refusal = Refused(Recipe(("shape", "1")), CharacterAudience.Player);

            StringAssert.Contains("shape '1'", refusal);
            StringAssert.Contains("'0.5'", refusal, "the refusal does not say what the preset's own is");
            StringAssert.Contains("presets only", refusal);
        }

        // ---- refusals, each naming what it refused -----------------------------------------------

        [Test]
        public void AnUnknownOption_IsRefused_ListingTheOptions()
        {
            string refusal = Refused(Recipe(("skin", "green")), CharacterAudience.Player);

            StringAssert.Contains("skin 'green'", refusal);
            StringAssert.Contains("fair, tan", refusal);
        }

        [Test]
        public void ACastOnlyValue_IsNeverTheirs_ForThePlayer()
        {
            string refusal = Refused(Recipe(("age", "child")), CharacterAudience.Player);

            StringAssert.Contains("age 'child'", refusal);
            StringAssert.Contains("cast-only", refusal);
        }

        [Test]
        public void ACastOnlyPreset_IsTheCastsAlone()
        {
            string preset = CharacterBuilder.RefusePreset(_builder, Boy, CharacterAudience.Player);
            StringAssert.Contains("'boy' is cast-only (age child)", preset);

            StringAssert.Contains("cast-only", Refused(new CharacterRecipe { BuildId = BoyBuild }, CharacterAudience.Player));
        }

        [Test]
        public void AnNpcPreset_IsRefused_ByName_UntilTheWardrobe()
        {
            string preset = CharacterBuilder.RefusePreset(_builder, Joe, CharacterAudience.Cast);
            StringAssert.Contains("'joe' is an NPC preset", preset);

            StringAssert.Contains("NPC", Refused(new CharacterRecipe { BuildId = "char_build.joe" }, CharacterAudience.Cast));
        }

        [Test]
        public void ACustomBuild_IsRefused()
        {
            StringAssert.Contains("custom build",
                CharacterBuilder.RefusePreset(_builder, "b:{\"shape\":1}", CharacterAudience.Cast));
        }

        [Test]
        public void AnUnknownPreset_IsRefused_ListingThePresets()
        {
            string refusal = CharacterBuilder.RefusePreset(_builder, "ghost", CharacterAudience.Cast);

            StringAssert.Contains("'ghost'", refusal);
            StringAssert.Contains("fisher, boy", refusal);
        }

        [Test]
        public void NoPreset_IsRefused_AndNeverBecomesTheFisher()
        {
            StringAssert.Contains("never falls back", CharacterBuilder.RefusePreset(_builder, "", CharacterAudience.Cast));
            StringAssert.Contains("never falls back", CharacterBuilder.RefusePreset(_builder, null, CharacterAudience.Cast));
        }

        [Test]
        public void APresetWithNoBuild_IsRefused()
        {
            _builder.Builds = new[] { _fisherBuild };

            StringAssert.Contains("'boy' has no build", CharacterBuilder.RefusePreset(_builder, Boy, CharacterAudience.Cast));
        }

        [Test]
        public void AnUnknownBuild_IsRefused_ListingTheBuilds()
        {
            string refusal = Refused(new CharacterRecipe { BuildId = "char_build.ghost" }, CharacterAudience.Cast);

            StringAssert.Contains("'char_build.ghost'", refusal);
            StringAssert.Contains($"{FisherBuild}, {BoyBuild}", refusal);
        }

        [Test]
        public void ARecipeFromANewerGame_IsRefused()
        {
            var recipe = Recipe(("skin", "tan"));
            recipe.SchemaVersion = CharacterRecipe.Version + 1;

            StringAssert.Contains("newer game", Refused(recipe, CharacterAudience.Player));
        }

        [Test]
        public void ARecipeCutForAnotherFit_IsRefused_AndItsOwnFitIsNot()
        {
            var other = Recipe(("skin", "tan"));
            other.FitId = "fit.character_v9";
            StringAssert.Contains("'fit.character_v9'", Refused(other, CharacterAudience.Player));

            var own = Recipe(("skin", "tan"));
            own.FitId = Fit;
            Assert.IsNull(CharacterBuilder.Refuse(_builder, own, CharacterAudience.Player));
        }

        [Test]
        public void AContractFieldTheRecipeDoesNotCarry_IsRefused()
        {
            var fields = new List<CharacterField>(_contract.Fields)
            {
                new CharacterField { Name = "tail", Kind = CharacterFieldKind.Structure, Options = new[] { "none" } },
            };
            _contract.Fields = fields.ToArray();
            foreach (CharacterPresetValues p in _contract.Presets)
                p.Values = new List<string>(p.Values) { "none" }.ToArray();

            StringAssert.Contains("'tail'", Refused(new CharacterRecipe(), CharacterAudience.Player));
        }

        [Test]
        public void ADefTheContractDoesNotDescribe_IsRefused()
        {
            CharacterSkinDef.Material[] mats = _fisherSkin.Materials;
            mats[2].Name = "tail";

            StringAssert.Contains("'tail'", Refused(new CharacterRecipe(), CharacterAudience.Player));
        }

        [Test]
        public void ADefShadedOtherwiseThanTheContract_IsRefused_NamingBoth()
        {
            CharacterSkinDef.Material[] mats = _fisherSkin.Materials;
            mats[0].Offset = -1;

            string refusal = Refused(new CharacterRecipe(), CharacterAudience.Player);
            StringAssert.Contains("off -1", refusal);
            StringAssert.Contains("off 0", refusal);
        }

        [Test]
        public void ARampOfAnotherLength_IsRefused()
        {
            _ramps.Axes[0].Ramps[1].Shades = new[] { C(20), C(21), C(22) };

            StringAssert.Contains("takes 3 colours", Refused(Recipe(("skin", "tan")), CharacterAudience.Player));
        }

        [Test]
        public void AColourWithNoRamp_IsRefused()
        {
            _ramps.Axes[1].Ramps = new[] { _ramps.Axes[1].Ramps[0] };

            StringAssert.Contains("outfit 'red' has no ramp", Refused(Recipe(("outfit", "red")), CharacterAudience.Player));
        }

        [Test]
        public void MoreMaterialsThanAFigureCarries_IsRefused()
        {
            var mats = new CharacterSkinDef.Material[CharacterSkinDef.MaxMaterials(_fisherSkin.ToneRule) + 1];
            for (int m = 0; m < mats.Length; m++) mats[m] = _fisherSkin.Materials[2];
            _fisherSkin.Materials = mats;

            StringAssert.Contains("more than the", Refused(new CharacterRecipe(), CharacterAudience.Player));
        }

        [Test]
        public void MissingTables_AreRefused_NotThrownOn()
        {
            StringAssert.Contains("No character builder", CharacterBuilder.Refuse(null, new CharacterRecipe(), CharacterAudience.Player));

            _builder.Ramps = null;
            StringAssert.Contains("names no ramps", Refused(new CharacterRecipe(), CharacterAudience.Player));

            _builder.Contract = null;
            StringAssert.Contains("names no rig contract", Refused(new CharacterRecipe(), CharacterAudience.Player));
            StringAssert.Contains("names no rig contract", CharacterBuilder.RefusePreset(_builder, Fisher, CharacterAudience.Cast));
        }

        [Test]
        public void ANullRecipe_IsRefused_NotThrownOn()
        {
            StringAssert.Contains("No recipe", Refused(null, CharacterAudience.Player));
        }

        // ---- helpers -----------------------------------------------------------------------------

        private CharacterLook Built(CharacterRecipe recipe, CharacterAudience audience)
        {
            Assert.IsTrue(CharacterBuilder.TryBuild(_builder, recipe, audience, out CharacterLook look, out string refusal),
                $"refused: {refusal}");
            Assert.IsNotNull(look);
            return look;
        }

        private string Refused(CharacterRecipe recipe, CharacterAudience audience)
        {
            Assert.IsFalse(CharacterBuilder.TryBuild(_builder, recipe, audience, out CharacterLook look, out string refusal),
                $"built {recipe} when it should have been refused");
            Assert.IsNull(look);
            Assert.IsNotEmpty(refusal);
            return refusal;
        }

        private static CharacterRecipe Recipe(params (string field, string value)[] set)
        {
            var r = new CharacterRecipe();
            foreach ((string field, string value) in set) Assert.IsTrue(r.Set(field, value), $"harness: no field {field}");
            return r;
        }

        private static Color32 C(byte v) => new Color32(v, (byte)(v + 1), (byte)(v + 2), 255);

        private static CharacterRampAxis Axis(string axis, string dflt, params (string key, Color32[] shades)[] ramps)
        {
            var a = new CharacterRampAxis { Axis = axis, DefaultKey = dflt, Ramps = new CharacterRamp[ramps.Length] };
            for (int i = 0; i < ramps.Length; i++)
                a.Ramps[i] = new CharacterRamp { Id = $"ramp.{axis}.{ramps[i].key}", Key = ramps[i].key, Shades = ramps[i].shades };
            return a;
        }

        private CharacterSkinDef Skin(string id, Color32[] skin, Color32[] coat, Color32[] lip)
        {
            var def = Make<CharacterSkinDef>();
            def.Id = id;
            def.ToneRule = ToneRule.V9;
            def.Materials = new[]
            {
                Mat("skin", skin, 2.4f, 2.7f, 2, 5),
                Mat("coat", coat, 2.4f, 1.8f, 1, 4),
                Mat("boot", Leather, 1f, 0f, 0, 0),
                Mat("lip", lip, 1f, 0f, 0, 0),
            };
            return def;
        }

        private static CharacterSkinDef.Material Mat(string name, Color32[] colors, float gain, float bias, int lo, int hi) =>
            new CharacterSkinDef.Material
            {
                Name = name, Colors = (Color32[])colors.Clone(), Offset = 0, Gain = gain, Bias = bias, FixedIndex = -1,
                ToneLo = lo, ToneHi = hi,
            };

        private CharacterBuildDef Build(string id, string preset, CharacterSkinDef skin)
        {
            var visual = Make<CharacterVisualDef>();
            visual.Skin = skin;
            var build = Make<CharacterBuildDef>();
            build.Id = id;
            build.Preset = preset;
            build.Visual = visual;
            return build;
        }

        private T Make<T>() where T : ScriptableObject
        {
            var o = ScriptableObject.CreateInstance<T>();
            _made.Add(o);
            return o;
        }
    }
}
