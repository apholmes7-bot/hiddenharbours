using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.Tests.Art.EditMode.ShallowsFloorShaderTests;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>A flat test bed for the plant field: one ground elevation everywhere, a water level the test
    /// moves, and no scatter (every plant in these tests is an authored site).</summary>
    public sealed class UnderWaterWeedGround : MonoBehaviour, IPlantFieldGround
    {
        public float Elevation = -2f;
        public float WaterLevel;
        public int PlantSeason => 1;
        public PlantGroundSample Sample(float x, float y) => new PlantGroundSample(0, 0, Elevation, true);
        public float WaterLevelAt(float x, float y) => WaterLevel;
    }

    /// <summary>
    /// The plant field's weed under the water: a tidal species' chunk that sorts under the sea plane joins
    /// the sea's under-water list (UnderWaterRegistry), so the sea draws it over its floor through the one
    /// water column (depth darkening and blueing, caustics, grade), the law the floor and a placed shore plant
    /// already take. The plant side adds only its albedo, a per-species hue and a soft surge sway, all from
    /// the Defs. These tests pin the join rule, the two C# twins of the shader's helpers, the chunk's
    /// tunables, the Def defaults and bounds, and that the shader pass carries no copy of the column law.
    ///
    /// <para>No GPU needed: membership and the property block are plain engine state, the shader is read as
    /// source.</para>
    /// </summary>
    public sealed class PlantFieldUnderWaterTests
    {
        const string FieldPath = "Assets/_Project/Data/Plants5/StPetersPlantField.asset";
        const string KelpPath = "Assets/_Project/Data/Plants5/Plant_SugarKelp.asset";
        const string PlantShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursPlant.shader";
        const string ConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";
        const string WaterSurfacePath = "Assets/_Project/Code/Art/WaterSurface.cs";
        const int Submerged = -8;

        static readonly int SwayId = Shader.PropertyToID("_UnderWaterSway");
        static readonly int PeriodId = Shader.PropertyToID("_UnderWaterSwayPeriod");
        static readonly int WavelengthId = Shader.PropertyToID("_UnderWaterSwayWavelength");
        static readonly int TintAmountId = Shader.PropertyToID("_UnderWaterTintAmount");
        static readonly int ShadeId = Shader.PropertyToID("_UnderWaterShade");

        readonly List<Object> _owned = new List<Object>();
        readonly List<PlantField> _fields = new List<PlantField>();
        GameConfig _priorConfig;
        GameConfig _config;
        int _baseline;

        [SetUp]
        public void SetUp()
        {
            _priorConfig = GameServices.Config;
            _config = Own(ScriptableObject.CreateInstance<GameConfig>());
            _config.ShallowsSubmergedProps = true;
            GameServices.Config = _config;
            _baseline = UnderWaterRegistry.Count;
        }

        [TearDown]
        public void TearDown()
        {
            // Edit mode never calls OnDisable on these hosts, so give the list back by hand first.
            foreach (var field in _fields) if (field != null) field.Rebuild(false);
            _fields.Clear();
            GameServices.Config = _priorConfig;
            for (int i = _owned.Count - 1; i >= 0; i--) if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
        }

        T Own<T>(T item) where T : Object { _owned.Add(item); return item; }

        // ---- the join rule ----------------------------------------------------------------------------

        [Test]
        public void JoinsUnderWater_OnlyTidalWeedUnderTheSeaPlane_WithBothSwitchesOn()
        {
            int lying = SortingBands.UnderHulls;
            Assert.IsTrue(PlantField.JoinsUnderWater(Submerged, Submerged, true, true, true), "submerged weed joins");
            Assert.IsTrue(PlantField.JoinsUnderWater(lying, Submerged, true, true, true), "lying weed (also under the Sea) joins");
            Assert.IsFalse(PlantField.JoinsUnderWater(Submerged, Submerged, false, true, true), "a non-tidal species never joins");
            Assert.IsFalse(PlantField.JoinsUnderWater(lying, Submerged, false, true, true), "dry ground cover at the lying order never joins");
            Assert.IsFalse(PlantField.JoinsUnderWater(Submerged, Submerged, true, false, true), "the field's switch off");
            Assert.IsFalse(PlantField.JoinsUnderWater(Submerged, Submerged, true, true, false), "the config switch off");
            Assert.IsFalse(PlantField.JoinsUnderWater(SortingBands.DecorBase, Submerged, true, true, true), "standing in the air");
        }

        [Test]
        public void SubmergedTidalWeed_JoinsTheList_AndLeavesWhenTheTideFalls()
        {
            var (field, ground) = Field(tidal: true, habitat: "", waterLevel: 0f);   // 2 m over a 1 m plant
            Assert.Greater(field.ChunkCount, 0);
            Assert.AreEqual(field.ChunkCount, field.UnderWaterChunkCount, "every submerged chunk is in the list");
            Assert.AreEqual(_baseline + field.UnderWaterChunkCount, UnderWaterRegistry.Count);
            foreach (var mr in Renderers(field))
            {
                Assert.AreEqual(Submerged, mr.sortingOrder);
                Assert.AreNotEqual(0u, mr.renderingLayerMask & UnderWaterRegistry.RenderingLayer, "it carries the bit");
                Assert.AreNotEqual(0u, mr.renderingLayerMask & 1u, "and keeps its own layer");
            }

            ground.WaterLevel = -5f;                // dry, standing in the air
            field.RefreshAppearance();
            Assert.AreEqual(0, field.UnderWaterChunkCount, "out of the water, out of the list");
            Assert.AreEqual(_baseline, UnderWaterRegistry.Count, "the list is given back");
            foreach (var mr in Renderers(field))
                Assert.AreEqual(0u, mr.renderingLayerMask & UnderWaterRegistry.RenderingLayer, "the bit is given back");
        }

        [Test]
        public void LyingTidalWeed_StaysInTheList_SoTheRisingTideShowsIt()
        {
            var (field, _) = Field(tidal: true, habitat: "fringe", waterLevel: -5f);
            Assert.Greater(field.ChunkCount, 0);
            foreach (var mr in Renderers(field)) Assert.AreEqual(SortingBands.UnderHulls, mr.sortingOrder);
            Assert.AreEqual(field.ChunkCount, field.UnderWaterChunkCount);
        }

        [Test]
        public void NothingJoins_WhenEitherSwitchIsOff_OrTheSpeciesIsNotTidal()
        {
            var (off, _) = Field(tidal: true, habitat: "", waterLevel: 0f, fieldOn: false);
            Assert.AreEqual(0, off.UnderWaterChunkCount, "the field's switch off");
            var (dry, _) = Field(tidal: false, habitat: "", waterLevel: 0f);
            Assert.AreEqual(0, dry.UnderWaterChunkCount, "a species with no tide ladder");
            Assert.AreEqual(_baseline, UnderWaterRegistry.Count);

            _config.ShallowsSubmergedProps = false;
            var (unlit, _) = Field(tidal: true, habitat: "", waterLevel: 0f);
            Assert.AreEqual(0, unlit.UnderWaterChunkCount, "the config switch off");
        }

        [Test]
        public void FlippingTheConfigSwitch_ReappliesWithoutARebuild()
        {
            var (field, _) = Field(tidal: true, habitat: "", waterLevel: 0f);
            int chunks = field.UnderWaterChunkCount;
            Assert.Greater(chunks, 0);
            _config.ShallowsSubmergedProps = false;
            field.RefreshAppearance();
            Assert.AreEqual(0, field.UnderWaterChunkCount);
            Assert.AreEqual(_baseline, UnderWaterRegistry.Count);
            _config.ShallowsSubmergedProps = true;
            field.RefreshAppearance();
            Assert.AreEqual(chunks, field.UnderWaterChunkCount);
        }

        [Test]
        public void ClearingTheField_GivesTheListBack()
        {
            var (field, _) = Field(tidal: true, habitat: "", waterLevel: 0f);
            Assert.Greater(UnderWaterRegistry.Count, _baseline);
            field.Rebuild(false);
            Assert.AreEqual(0, field.ChunkCount);
            Assert.AreEqual(0, field.UnderWaterChunkCount);
            Assert.AreEqual(_baseline, UnderWaterRegistry.Count);
        }

        [Test]
        public void EachChunk_CarriesTheDefsTunables()
        {
            var (field, _) = Field(tidal: true, habitat: "", waterLevel: 0f, sway: 0.2f, period: 0f, wavelength: 4f, tintAmount: 0.6f, shade: 0.4f);
            var block = new MaterialPropertyBlock();
            foreach (var mr in Renderers(field))
            {
                mr.GetPropertyBlock(block);
                Assert.AreEqual(0.2f, block.GetFloat(SwayId), 1e-6f);
                Assert.AreEqual(PlantFieldDefinition.MinUnderWaterSwaySpan, block.GetFloat(PeriodId), 1e-6f, "a zero period is floored");
                Assert.AreEqual(4f, block.GetFloat(WavelengthId), 1e-6f);
                Assert.AreEqual(0.6f, block.GetFloat(TintAmountId), 1e-6f);
                Assert.AreEqual(0.4f, block.GetFloat(ShadeId), 1e-6f, "the species' shade");
            }
        }

        // ---- the C# twins of the shader's helpers -----------------------------------------------------

        [Test]
        public void UnderWaterSurge_RocksAlongTheWind_ByTheSwellsPeriodAndWavelength()
        {
            var wind = new Vector2(0f, 3f);
            var root = new Vector2(2.5f, -7f);
            const float sway = 0.12f, period = 7f, wavelength = 9f;

            Assert.AreEqual(Vector2.zero, PlantField.UnderWaterSurge(root, 1.3f, wind, 0f, period, wavelength), "0 holds it still");
            for (float t = 0f; t < 2f * period; t += 0.37f)
            {
                Vector2 s = PlantField.UnderWaterSurge(root, t, wind, sway, period, wavelength);
                Assert.LessOrEqual(s.magnitude, sway + 1e-5f, "never past the sway");
                Assert.AreEqual(0f, s.x, 1e-6f, "along the wind only");
                AssertNear(s, PlantField.UnderWaterSurge(root, t + period, wind, sway, period, wavelength), "one period later");
                AssertNear(s, PlantField.UnderWaterSurge(root + new Vector2(0f, wavelength), t + period, wind, sway, period, wavelength) , "one wavelength on, one period on");
                AssertNear(-s, PlantField.UnderWaterSurge(root + new Vector2(0f, wavelength * 0.5f), t, wind, sway, period, wavelength), "half a wavelength on, the other way");
                AssertNear(s, PlantField.UnderWaterSurge(root + new Vector2(5f, 0f), t, wind, sway, period, wavelength), "across the wind, in step");
            }
            // A quarter period in, at the origin, it is at its full reach.
            Assert.AreEqual(sway, PlantField.UnderWaterSurge(Vector2.zero, period * 0.25f, wind, sway, period, wavelength).y, 1e-5f);

            // Calm: east. Degenerate spans are floored, never a NaN.
            Vector2 calm = PlantField.UnderWaterSurge(Vector2.zero, period * 0.25f, Vector2.zero, sway, period, wavelength);
            Assert.AreEqual(sway, calm.x, 1e-5f); Assert.AreEqual(0f, calm.y, 1e-6f);
            Vector2 degenerate = PlantField.UnderWaterSurge(root, 0.4f, wind, sway, 0f, 0f);
            Assert.IsFalse(float.IsNaN(degenerate.x) || float.IsNaN(degenerate.y));
        }

        [Test]
        public void UnderWaterTint_KeepsEachPixelsBrightness_AndOnlyMovesItsHue()
        {
            var tint = new Color(0.40f, 0.38f, 0.16f, 1f);
            var baked = new Color(0.20f, 0.30f, 0.45f, 0.7f);   // a cold, wet-ramp blue
            Assert.AreEqual(baked, PlantField.UnderWaterTint(baked, tint, 0f), "0: the baked sheet as it is");

            Color full = PlantField.UnderWaterTint(baked, tint, 1f);
            Assert.AreEqual(Luma(baked), Luma(full), 1e-4f, "the pixel's own brightness");
            Assert.AreEqual(tint.r / tint.g, full.r / full.g, 1e-4f, "the species' hue");
            Assert.AreEqual(tint.b / tint.g, full.b / full.g, 1e-4f);
            Assert.AreEqual(baked.a, full.a, "alpha untouched");
            Assert.AreEqual(full, PlantField.UnderWaterTint(baked, tint, 3f), "the amount clamps at 1");
            Assert.AreEqual(baked, PlantField.UnderWaterTint(baked, tint, -1f), "and at 0");

            Color half = PlantField.UnderWaterTint(baked, tint, 0.5f);
            Assert.AreEqual((baked.g + full.g) * 0.5f, half.g, 1e-5f, "a straight blend between");
            Color black = PlantField.UnderWaterTint(new Color(0f, 0f, 0f, 1f), tint, 1f);
            Assert.AreEqual(0f, black.r + black.g + black.b, 1e-6f, "black stays black");
            Color hot = PlantField.UnderWaterTint(Color.white, new Color(1f, 0.1f, 0.1f, 1f), 1f);
            Assert.LessOrEqual(Mathf.Max(hot.r, Mathf.Max(hot.g, hot.b)), 1f, "never past 1");
        }

        [Test]
        public void UnderWaterShade_DarkensTheTintedFrond_TowardASilhouette()
        {
            var tint = new Color(0.36f, 0.30f, 0.12f, 1f);
            var baked = new Color(0.20f, 0.30f, 0.45f, 0.7f);
            Color tinted = PlantField.UnderWaterTint(baked, tint, 1f);
            Assert.AreEqual(tinted, PlantField.UnderWaterTint(baked, tint, 1f, 1f), "1: as bright as the sheet");
            Color dark = PlantField.UnderWaterTint(baked, tint, 1f, 0.4f);
            Assert.AreEqual(tinted.r * 0.4f, dark.r, 1e-6f, "every channel by the shade");
            Assert.AreEqual(tinted.g * 0.4f, dark.g, 1e-6f);
            Assert.AreEqual(tinted.b * 0.4f, dark.b, 1e-6f);
            Assert.AreEqual(tinted.r / tinted.g, dark.r / dark.g, 1e-4f, "the hue kept");
            Assert.AreEqual(baked.a, dark.a, "alpha untouched: the frond's shape is the sheet's");
            Assert.AreEqual(0f, PlantField.UnderWaterTint(baked, tint, 1f, 0f).g, 1e-6f, "0: a black silhouette");
            Assert.AreEqual(tinted, PlantField.UnderWaterTint(baked, tint, 1f, 2f), "clamped at 1");
            Assert.AreEqual(0f, PlantField.UnderWaterTint(baked, tint, 1f, -1f).g, 1e-6f, "and at 0");
            Assert.AreEqual(baked.g * 0.5f, PlantField.UnderWaterTint(baked, tint, 0f, 0.5f).g, 1e-6f,
                "a shade with no hue darkens the baked sheet");
        }

        // ---- the Defs ---------------------------------------------------------------------------------

        [Test]
        public void TheDefs_DefaultSafe_AndShipInBounds()
        {
            var fresh = Own(ScriptableObject.CreateInstance<PlantFieldDefinition>());
            Assert.IsTrue(fresh.UnderWater);
            Assert.AreEqual(PlantFieldDefinition.DefaultUnderWaterSwayM, fresh.UnderWaterSwayM);
            Assert.AreEqual(PlantFieldDefinition.DefaultUnderWaterSwayPeriodS, fresh.UnderWaterSwayPeriodS);
            Assert.AreEqual(PlantFieldDefinition.DefaultUnderWaterSwayWavelengthM, fresh.UnderWaterSwayWavelengthM);
            Assert.AreEqual(0f, Own(ScriptableObject.CreateInstance<PlantSpeciesDef>()).UnderWaterTintAmount,
                "a species with no say draws its baked sheet under the water");
            Assert.AreEqual(1f, Own(ScriptableObject.CreateInstance<PlantSpeciesDef>()).UnderWaterShade,
                "and as bright as the sheet");

            var real = AssetDatabase.LoadAssetAtPath<PlantFieldDefinition>(FieldPath);
            Assert.IsNotNull(real);
            Assert.IsTrue(real.UnderWater, "St Peters shows its weed under the water");
            Assert.That(real.UnderWaterSwayM, Is.InRange(0f, 0.5f), "a soft rock, not a thrash");
            Assert.GreaterOrEqual(real.UnderWaterSwayPeriodS, PlantFieldDefinition.MinUnderWaterSwaySpan);
            Assert.GreaterOrEqual(real.UnderWaterSwayWavelengthM, PlantFieldDefinition.MinUnderWaterSwaySpan);

            foreach (var species in real.Species)
            {
                Assert.IsNotNull(species);
                Assert.That(species.UnderWaterTintAmount, Is.InRange(0f, 1f), species.Id);
                Assert.That(species.UnderWaterShade, Is.InRange(0f, 1f), species.Id);
                for (int c = 0; c < 3; c++) Assert.That(species.UnderWaterTint[c], Is.InRange(0f, 1f), species.Id);
            }

            var kelp = AssetDatabase.LoadAssetAtPath<PlantSpeciesDef>(KelpPath);
            Assert.IsNotNull(kelp);
            Assert.AreEqual("shoreplant.sugar_kelp", kelp.Id);
            CollectionAssert.Contains(real.Species, kelp, "the kelp grows in the St Peters field");
            Assert.Greater(kelp.TideLadder.Length, 0, "the kelp is tidal, so it can join");
            Assert.Greater(kelp.UnderWaterTintAmount, 0f, "its cold baked wet ramp is warmed before the sea blues it");
            Assert.Greater(kelp.UnderWaterTint.g, kelp.UnderWaterTint.b, "a brown-olive frond, not a blue one");
            Assert.Greater(kelp.UnderWaterTint.r, kelp.UnderWaterTint.b, "brown, not teal");
            Assert.Less(kelp.UnderWaterShade, 1f, "a dark frond: the bed reads as a dark mass over the lighter floor");
            Assert.Greater(kelp.UnderWaterShade, 0f, "a frond, not a hole");

            // The other tidal weed stays subtler than the kelp: never darker.
            foreach (var species in real.Species)
                if (species != null && species != kelp && species.TideLadder != null && species.TideLadder.Length > 0)
                    Assert.GreaterOrEqual(species.UnderWaterShade, kelp.UnderWaterShade, species.Id + " no darker than the kelp");
        }

        [Test]
        public void TheWeedsClarity_ShipsFromGameConfig_AndTheSeaPushesIt()
        {
            Assert.AreEqual(0f, GameConfig.DefaultUnderWaterWeedClarity, "the code's default is the floor's own law");
            Assert.AreEqual(0f, Own(ScriptableObject.CreateInstance<GameConfig>()).UnderWaterWeedClarity);
            Assert.AreEqual(0.9f, GameConfig.MaxUnderWaterWeedClarity);
            Assert.AreEqual(0.9f, GameConfig.ClampUnderWaterWeedClarity(4f));
            Assert.AreEqual(0f, GameConfig.ClampUnderWaterWeedClarity(-1f));

            var shipped = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAssetPath);
            Assert.IsNotNull(shipped);
            Assert.Greater(shipped.UnderWaterWeedClarity, 0f, "the shipped look reads the weed through less water");
            Assert.LessOrEqual(shipped.UnderWaterWeedClarity, GameConfig.MaxUnderWaterWeedClarity);
            Assert.AreEqual(1, Regex.Matches(File.ReadAllText(ConfigAssetPath), @"(?m)^  UnderWaterWeedClarity: ").Count,
                "written in the asset once, so the owner can tune it");

            // The sea pushes it with the floor's switch, from its own config (the code's default with none).
            string sea = Code(WaterSurfacePath);
            StringAssert.Contains(
                "privatestaticreadonlyintIdUnderWaterClarity=Shader.PropertyToID(\"_HHUnderWaterClarity\");", Squash(sea));
            StringAssert.Contains(
                "floatclarity=_config!=null?_config.UnderWaterWeedClarity:GameConfig.DefaultUnderWaterWeedClarity;" +
                "mpb.SetFloat(IdUnderWaterClarity,GameConfig.ClampUnderWaterWeedClarity(clarity));",
                Squash(Body(sea, "private void PushShallowsFloor(MaterialPropertyBlock mpb)")));
        }

        // ---- the shader pass --------------------------------------------------------------------------

        [Test]
        public void ThePlantShadersUnderWaterPass_IsAlbedoOnly_AndTwinsTheCSharp()
        {
            string shader = Code(PlantShaderPath);
            Assert.AreEqual(1, Count(shader, @"""LightMode""\s*=\s*""HHUnderWater"""), "one under-water pass");
            string pass = UnderWaterPass(shader);
            string squashed = Squash(pass);
            StringAssert.Contains("BlendOneOneMinusSrcAlpha", squashed, "premultiplied");
            StringAssert.Contains("ZWriteOff", squashed);
            StringAssert.Contains("ZTestAlways", squashed);
            StringAssert.EndsWith("returnhalf4(albedo.rgb*albedo.a,albedo.a);",
                Squash(Body(pass, "half4 UnderWaterFrag(Output i) : SV_Target")), "premultiplied albedo");

            // The column law (depth, caustics, grade) is the sea's alone; no light, no walker push, no wind bend.
            foreach (string law in new[]
                     {
                         "Absorption", "[Cc]austic", "PaletteGrade", "_DayNight", "_HHUnderWater", "SpriteLight",
                         "_SunDir", "_NormalTex", "_LightTex", "_GrassTrail", "_GrassWalkers", "PlantWindOffset",
                     })
                Assert.AreEqual(0, Count(pass, law), "the weed's pass carries no law of its own: " + law);

            // Both passes share one UnityPerMaterial, row for row (SRP batcher).
            string[] buffers = Regex.Matches(shader, @"CBUFFER_START\(UnityPerMaterial\)(.*?)CBUFFER_END", RegexOptions.Singleline)
                .Cast<Match>().Select(m => Squash(m.Groups[1].Value)).ToArray();
            Assert.AreEqual(2, buffers.Length);
            Assert.AreEqual(buffers[0], buffers[1], "one UnityPerMaterial for both passes");

            // The twins' constants: the span floor and the Rec. 601 weights.
            Assert.AreEqual(0.1f, PlantFieldDefinition.MinUnderWaterSwaySpan);
            StringAssert.Contains("max(period,0.1)", squashed);
            StringAssert.Contains("max(wavelength,0.1)", squashed);
            StringAssert.Contains("float3(0.299,0.587,0.114)", squashed);

            // The bare-material defaults are the Def's.
            string props = File.ReadAllText(PlantShaderPath);
            Assert.AreEqual(PlantFieldDefinition.DefaultUnderWaterSwayM, Default(props, "_UnderWaterSway"), 1e-6f);
            Assert.AreEqual(PlantFieldDefinition.DefaultUnderWaterSwayPeriodS, Default(props, "_UnderWaterSwayPeriod"), 1e-6f);
            Assert.AreEqual(PlantFieldDefinition.DefaultUnderWaterSwayWavelengthM, Default(props, "_UnderWaterSwayWavelength"), 1e-6f);
            Assert.AreEqual(0f, Default(props, "_UnderWaterTintAmount"), 1e-6f);
            Assert.AreEqual(1f, Default(props, "_UnderWaterShade"), 1e-6f);
            StringAssert.Contains("returnlerp(albedo,saturate(hue),saturate(amount))*saturate(shade);", squashed,
                "the shade twins PlantField.UnderWaterTint");
            StringAssert.Contains(
                "PlantUnderWaterTint(albedo.rgb,_UnderWaterTint.rgb,_UnderWaterTintAmount,_UnderWaterShade);", squashed);
        }

        // ---- fixture ----------------------------------------------------------------------------------

        (PlantField field, UnderWaterWeedGround ground) Field(bool tidal, string habitat, float waterLevel,
            bool fieldOn = true, float sway = PlantFieldDefinition.DefaultUnderWaterSwayM,
            float period = PlantFieldDefinition.DefaultUnderWaterSwayPeriodS,
            float wavelength = PlantFieldDefinition.DefaultUnderWaterSwayWavelengthM, float tintAmount = 0.8f, float shade = 1f)
        {
            var real = AssetDatabase.LoadAssetAtPath<PlantFieldDefinition>(FieldPath);
            Assert.That(real, Is.Not.Null); Assert.That(real.Material, Is.Not.Null);

            var texture = Own(new Texture2D(64, 128));
            var species = Own(ScriptableObject.CreateInstance<PlantSpeciesDef>());
            species.Code = 1; species.Family = "shore"; species.Habitat = habitat; species.StandingHeightM = 1f;
            species.TideLadder = tidal ? new[] { 0f, 1f } : new float[0];
            species.Pages = new[] { new PlantSheetPage { Albedo = texture, Normal = texture, Light = texture, Detail = texture } };
            species.UnderWaterTintAmount = tintAmount; species.UnderWaterShade = shade;
            var cells = new List<PlantCell>();
            for (int tide = 0; tide <= 1; tide++) for (int stage = 0; stage <= 2; stage++) for (int variant = 0; variant < 4; variant++)
                cells.Add(new PlantCell { Stage = stage, Season = 1, Variant = variant, Tide = tide,
                    Rect = new Rect(0, 0, 32, 64), Pivot = new Vector2(16, 0), HeightM = 1f });
            species.Cells = cells.ToArray();

            var definition = Own(ScriptableObject.CreateInstance<PlantFieldDefinition>());
            definition.Width = definition.Height = 16; definition.Seed = 9109; definition.CellSize = 0.5f;
            definition.GroundMetresPerY = 1; definition.ChunkWidth = 8; definition.SlowTickSeconds = 0.5f;
            definition.HeightProjection = 0.76604444f; definition.WindMargin = 0.75f; definition.SunShadowStrength = 0;
            definition.SubmergedOrder = Submerged;
            definition.Recipes = new BiomeRecipeDef[0]; definition.Species = new[] { species };
            definition.Material = Own(new Material(real.Material));
            definition.UnderWater = fieldOn; definition.UnderWaterSwayM = sway;
            definition.UnderWaterSwayPeriodS = period; definition.UnderWaterSwayWavelengthM = wavelength;
            // Far from anything a hull fixture could leave behind (PlantHullSorting.BehindAHull).
            var authored = new List<PlantFieldSite>();
            for (int i = 0; i < 6; i++)
                authored.Add(new PlantFieldSite { Id = "under-water-fixture-" + i, At = new Vector2(5001.5f + 3f * i, 5002f), Species = 1, Stage = 1, Variant = i % 4 });
            definition.Authored = authored.ToArray();

            var host = Own(new GameObject("PlantFieldUnderWaterFixture"));
            var ground = host.AddComponent<UnderWaterWeedGround>(); ground.WaterLevel = waterLevel;
            var field = host.AddComponent<PlantField>(); field.Definition = definition; field.GroundSource = ground;
            _fields.Add(field);
            field.Rebuild(true);
            return (field, ground);
        }

        static IEnumerable<MeshRenderer> Renderers(PlantField field) =>
            field.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.gameObject.name == "PlantChunk");

        static float Luma(Color c) => c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;

        static void AssertNear(Vector2 expected, Vector2 actual, string what)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-4f, what);
            Assert.AreEqual(expected.y, actual.y, 1e-4f, what);
        }

        static float Default(string shader, string property)
        {
            Match m = Regex.Match(shader, Regex.Escape(property) + @"\s*\(""[^""]*"",\s*[^)]*\)\)?\s*=\s*([-0-9.]+)");
            Assert.IsTrue(m.Success, property + " has a default");
            return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}
