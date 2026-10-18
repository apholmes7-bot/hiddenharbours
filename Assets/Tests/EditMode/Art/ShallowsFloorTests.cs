using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using static HiddenHarbours.Tests.Art.EditMode.ShallowsFloorShaderTests;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The floor in the shallows (W4-4), the C# side: how a sea finds the ground it is drawn over, what that
    /// ground binds to the sea's floor, when the ground's underwater clip is raised, and that each step's
    /// switch ships OFF as an exact passthrough. The shader side is <see cref="ShallowsFloorShaderTests"/>.
    ///
    /// <para>Headless-safe: source text, <c>Vector2</c> arithmetic and the GameServices fallback, no engine
    /// call. The behaviour on live components (a ground binding a sea's block, the displaced chunks copying
    /// it) is <c>ShallowsActivationTests</c>' moved premises.</para>
    ///
    /// <para><b>On the base</b> (b8710c59) ShallowsFloorMath, the clip flag, WriteGroundFloor and the
    /// switches do not exist, so this file does not compile there. Against base-shaped copies of the sources
    /// the text tests fail at their first assert; the scene test passes there too, because it pins a premise
    /// the base already holds (one height frame per played region), which no scene change may break.</para>
    /// </summary>
    public sealed class ShallowsFloorTests
    {
        private const string SurfacePath = "Assets/_Project/Code/Art/TerrainSplatSurface.cs";
        private const string FeaturePath = "Assets/_Project/Code/Art/IsoFacetHullFeature.cs";
        private const string RegistryPath = "Assets/_Project/Code/Art/IsoFacetHullRegistry.cs";
        private const string ConfigPath = "Assets/_Project/Code/Core/Config/GameConfig.cs";
        private const string ServicesPath = "Assets/_Project/Code/Core/Services/GameServices.cs";
        private const string ConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";
        private const string IncludePath = "Assets/_Project/Art/Shaders/Include/TerrainSplatAlbedo.hlsl";
        private const string TerrainPath = "Assets/_Project/Art/Shaders/HiddenHarboursTerrainSplat.shader";
        private const string TerrainFrag = "half4 frag (Varyings i) : SV_Target";
        private const string WaterSurfacePath = "Assets/_Project/Code/Art/WaterSurface.cs";

        // Script and asset guids, each from its own .meta (checked below, so a moved file fails here first).
        private const string WaterSurfaceGuid = "720419e734bb47b19ff0a4ce8281ed56";   // WaterSurface.cs.meta
        private const string GroundGuid = "09c13e4d6f71605bc28d5e0f1a32b4c6";         // TerrainSplatSurface.cs.meta
        private const string FeatureGuid = "ef8515d09d3738f4986f1c11d9bec5ab";        // IsoFacetHullFeature.cs.meta
        private const string PipelineGuid = "681886c5eb7344803b6206f758bf0b1c";       // Settings/UniversalRP.asset.meta
        private const string RendererGuid = "424799608f7334c24bf367e4bbfa7f9a";       // Settings/Renderer2D.asset.meta

        [Test]
        public void SameFrame_IsOneMapPlacement()
        {
            // The two played regions' frames as their scenes hold them (StPeters.unity and NineMileCreek.unity
            // at b8710c59: centre (0, 0), 760 x 520 m, -4..12 m; centre (0, 0), 760 x 560 m, -6..6 m).
            var stpMin = new Vector2(-380f, -260f);
            var stpSize = new Vector2(760f, 520f);
            Assert.IsTrue(ShallowsFloorMath.SameFrame(stpMin, stpSize, -4f, 12f, stpMin, stpSize, -4f, 12f));
            Assert.IsFalse(ShallowsFloorMath.SameFrame(stpMin, stpSize, -4f, 12f,
                                                       new Vector2(-380f, -280f), new Vector2(760f, 560f), -6f, 6f),
                "St Peters' ground is never Nine Mile Creek's floor");

            // A millimetre (ShallowsFloorMath's doc: under a height-map texel, over float round-off at map
            // scale). 0.9 mm on any ONE bound is the same placement; 2 mm is another.
            Assert.AreEqual(1e-3f, ShallowsFloorMath.SameFrameToleranceMetres);
            for (int bound = 0; bound < 6; bound++)
            {
                Assert.IsTrue(SameAfterShift(stpMin, stpSize, bound, 0.0009f), "0.9 mm on bound " + bound);
                Assert.IsFalse(SameAfterShift(stpMin, stpSize, bound, 0.002f), "2 mm on bound " + bound);
                Assert.IsFalse(SameAfterShift(stpMin, stpSize, bound, -0.002f), "-2 mm on bound " + bound);
            }
        }

        /// <summary>The binding needs no scene change: in each played region the sea feeds its shader the
        /// ground's own height texture over the ground's own rectangle and range, so
        /// <c>TerrainSplatSurface.FindSharing</c> finds that ground. Bars: the scenes at b8710c59.</summary>
        [TestCase("StPeters", "{x: 760, y: 520}", "-4", "12")]
        [TestCase("NineMileCreek", "{x: 760, y: 560}", "-6", "6")]
        public void EachPlayedRegion_SeaAndGround_ShareOneHeightFrame(string region, string size, string low,
                                                                      string high)
        {
            StringAssert.Contains("guid: " + WaterSurfaceGuid, File.ReadAllText("Assets/_Project/Code/Art/WaterSurface.cs.meta"));
            StringAssert.Contains("guid: " + GroundGuid, File.ReadAllText(SurfacePath + ".meta"));
            string yaml = File.ReadAllText("Assets/_Project/Scenes/" + region + ".unity");
            string sea = SceneBlock(yaml, WaterSurfaceGuid);
            string ground = SceneBlock(yaml, GroundGuid);

            Assert.AreEqual("3", Field(sea, "_depthSource"), "the sea reads the painted map (PaintedHeightMap = 3)");
            Assert.AreEqual(Field(ground, "_heightTexture"), Field(sea, "_paintedHeightTex"), "one height texture");
            StringAssert.StartsWith("{fileID: 2800000, guid: ", Field(ground, "_heightTexture"));
            Assert.AreEqual("{x: 0, y: 0}", Field(sea, "_heightWorldCenter"));
            Assert.AreEqual("{x: 0, y: 0}", Field(ground, "_worldCenter"));
            Assert.AreEqual(size, Field(sea, "_heightWorldSize"));
            Assert.AreEqual(size, Field(ground, "_worldSize"));
            Assert.AreEqual(low, Field(sea, "_heightMin"));
            Assert.AreEqual(low, Field(ground, "_heightMin"));
            Assert.AreEqual(high, Field(sea, "_heightMax"));
            Assert.AreEqual(high, Field(ground, "_heightMax"));
        }

        /// <summary>The flag that lets the ground clip is THIS camera's: lowered for every camera before its
        /// passes are added (and on Dispose), raised only inside its own water pass after the coverage is
        /// drawn, and only while that camera draws the displaced sea with the switch on. Every camera passes
        /// through the lowering: each quality level runs the one pipeline, whose one renderer holds the
        /// feature, active.</summary>
        [Test]
        public void TheClipFlag_IsRaisedOnlyByThisCamerasOwnWaterPass()
        {
            string cs = Code(FeaturePath);
            string add = Body(cs, "public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)");
            Assert.AreEqual("Shader.SetGlobalFloat(IsoFacetShaderIds.TerrainUnderwaterClip,0f);",
                Squash(add.Substring(0, add.IndexOf(';') + 1)), "lowered first, before every early return");
            StringAssert.Contains("_pass.ClipTerrain=water&&GameServices.TerrainUnderwaterClip;", Squash(add));

            Assert.AreEqual(1, Count(cs, @"SetGlobalFloat\(\s*IsoFacetShaderIds\.TerrainUnderwaterClip\s*,\s*1f\s*\)"),
                "raised in one place");
            string render = Squash(Body(cs, "builder.SetRenderFunc((WaterPassData data, RasterGraphContext ctx) =>"));
            StringAssert.EndsWith(
                "ctx.cmd.DrawRendererList(data.Renderers);" +
                "if(data.ClipTerrain)ctx.cmd.SetGlobalFloat(IsoFacetShaderIds.TerrainUnderwaterClip,1f);", render,
                "raised by the water pass, after its coverage is drawn");
            StringAssert.Contains(
                "passData.ClipTerrain=ClipTerrain;if(ClipTerrain)builder.AllowGlobalStateModification(true);",
                Squash(cs));

            Assert.AreEqual(2, Count(cs, @"SetGlobalFloat\(\s*IsoFacetShaderIds\.TerrainUnderwaterClip\s*,\s*0f\s*\)"),
                "lowered in two places: the top of AddRenderPasses and Dispose");
            string dispose = Body(cs, "protected override void Dispose(bool disposing)");
            Assert.AreEqual("Shader.SetGlobalFloat(IsoFacetShaderIds.TerrainUnderwaterClip,0f);",
                Squash(dispose.Substring(0, dispose.IndexOf(';') + 1)));

            StringAssert.Contains(
                "publicstaticreadonlyintTerrainUnderwaterClip=Shader.PropertyToID(\"_HHTerrainUnderwaterClip\");",
                Squash(Code(RegistryPath)), "the flag is the terrain's uniform");

            // The water pass runs before the ground draws: before every sprite, on the lowest sorting layer.
            StringAssert.Contains("_pass.renderPassEvent2D=RenderPassEvent2D.BeforeRenderingSprites;", Squash(cs));
            StringAssert.Contains("_pass.renderPassSortingLayerID=LowestSortingLayerId();", Squash(cs));

            // Every camera passes through AddRenderPasses.
            StringAssert.Contains("guid: " + FeatureGuid, File.ReadAllText(FeaturePath + ".meta"));
            StringAssert.Contains("guid: " + PipelineGuid, File.ReadAllText("Assets/Settings/UniversalRP.asset.meta"));
            StringAssert.Contains("guid: " + RendererGuid, File.ReadAllText("Assets/Settings/Renderer2D.asset.meta"));
            string quality = File.ReadAllText("ProjectSettings/QualitySettings.asset");
            int levels = Count(quality, @"customRenderPipeline:");
            Assert.Greater(levels, 0);
            Assert.AreEqual(levels, Count(quality,
                @"customRenderPipeline: \{fileID: 11400000, guid: " + PipelineGuid + @", type: 2\}"),
                "every quality level runs UniversalRP.asset");
            string urp = File.ReadAllText("Assets/Settings/UniversalRP.asset").Replace("\r", "");
            Match list = Regex.Match(urp, @"m_RendererDataList:\n(?<rows>(?:  - [^\n]*\n)+)");
            Assert.IsTrue(list.Success, "UniversalRP.asset's renderer list");
            CollectionAssert.AreEqual(new[] { "  - {fileID: 11400000, guid: " + RendererGuid + ", type: 2}" },
                list.Groups["rows"].Value.TrimEnd('\n').Split('\n'), "one renderer, Renderer2D");
            string feature = SceneBlock(File.ReadAllText("Assets/Settings/Renderer2D.asset"), FeatureGuid);
            StringAssert.Contains("m_Name: IsoFacetHullFeature", feature);
            Assert.AreEqual("1", Field(feature, "m_Active"), "the feature is active");
        }

        /// <summary>The sea's floor (W4-4b) is bound every value the ground's albedo reads, through the
        /// ground's own writer, so the floor is the ground and never a second copy that drifts: the include's
        /// uniforms by name, plus what every includer reads around it (the kit array, the kit's gate and the
        /// pixel grid). Never the height, the sea's own water level or the relight.</summary>
        [Test]
        public void TheGroundFloor_BindsEveryUniformTheIncludeReads()
        {
            string include = Code(IncludePath);
            var reads = new SortedSet<string>(Regex.Matches(include, @"(?<![A-Za-z0-9_])_[A-Z][A-Za-z0-9_]*")
                .Cast<Match>().Select(m => m.Value));
            // 36 by name: the 30 values and 6 splats the include's header lists (this PR's include).
            Assert.AreEqual(36, reads.Count, string.Join(", ", reads));
            reads.UnionWith(new[] { "_DetailArr256", "_DetailLoaded", "_PixelsPerMetre" });

            string cs = Code(SurfacePath);
            Dictionary<string, string> ids = Regex.Matches(cs, @"\b(Id\w+)\s*=\s*Shader\.PropertyToID\(""(_\w+)""\)")
                .Cast<Match>().ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
            string look = Body(cs, "private void WriteLook(MaterialPropertyBlock mpb)");
            string[] looked = Regex.Matches(look, @"\bmpb\.Set\w+\(\s*(Id\w+)")
                .Cast<Match>().Select(m => ids[m.Groups[1].Value]).ToArray();
            string[] floats = PropertyIds(cs, "s_GroundMaterialFloats");
            string[] colors = PropertyIds(cs, "s_GroundMaterialColors");
            var bound = new SortedSet<string>(looked.Concat(floats).Concat(colors));

            CollectionAssert.IsEmpty(reads.Except(bound).ToArray(), "a value the floor would read unbound");
            CollectionAssert.IsEmpty(bound.Where(n => n.StartsWith("_Height") || n == "_WaterLevel" ||
                                                      n.StartsWith("_Relight")).ToArray(),
                "the floor binds no height, no water level and no relight");

            // The one writer: the ground's own push and the sea's floor both go through WriteLook.
            StringAssert.Contains("WriteLook(_mpb);", Squash(Body(cs, "private void PushAll()")));
            string floor = Squash(Body(cs, "public void WriteGroundFloor(MaterialPropertyBlock mpb)"));
            StringAssert.Contains("mpb.SetFloat(IdDetailLoaded,0f);return;", floor);
            StringAssert.Contains("WriteLook(mpb);", floor);
            StringAssert.Contains("mpb.SetFloat(s_GroundMaterialFloats[i],ground.GetFloat(s_GroundMaterialFloats[i]));", floor);
            StringAssert.Contains("mpb.SetColor(s_GroundMaterialColors[i],ground.GetColor(s_GroundMaterialColors[i]));", floor);
            Assert.AreEqual(3, Count(floor, @"mpb\.Set\w+\("), "the no-material gate and the two copies; WriteLook writes the rest");

            // The copied values are the terrain material's own declared properties, of their declared kind,
            // and no push writes them: the material holds what the ground draws with.
            string terrain = Code(TerrainPath);
            int props = terrain.IndexOf("Properties", System.StringComparison.Ordinal);
            int sub = terrain.IndexOf("SubShader", System.StringComparison.Ordinal);
            Assert.IsTrue(props > 0 && sub > props, "the terrain's Properties block");
            string block = terrain.Substring(props, sub - props);
            foreach (string f in floats)
                Assert.IsTrue(Regex.IsMatch(block, Regex.Escape(f) + @"\s*\(\s*""[^""]*""\s*,\s*(Float|Range\s*\([^)]*\))\s*\)"),
                    f + " is a declared Float or Range");
            foreach (string c in colors)
                Assert.IsTrue(Regex.IsMatch(block, Regex.Escape(c) + @"\s*\(\s*""[^""]*""\s*,\s*Color\s*\)"),
                    c + " is a declared Color");
            CollectionAssert.IsEmpty(floats.Concat(colors).Intersect(ids.Values).ToArray(),
                "no push writes a material-carried value");
        }

        /// <summary>Each step's switch ships OFF and, off, is today's pixels bit for bit: OFF in the code's
        /// default and in the shipped asset (a GameConfig.asset lagging the code reads a bool's default,
        /// false, also off), no config wired reads OFF, and the shader's change sits wholly behind it.</summary>
        [TestCase("clip")]
        [TestCase("floor")]
        public void SwitchOff_IsTodaysPixels_BitForBit(string step)
        {
            switch (step)
            {
                case "clip":
                    // W4-4a ships OFF (the handoff's switches: OFF in data until the plates are accepted).
                    Assert.IsFalse(GameConfig.DefaultTerrainUnderwaterClip);
                    Assert.AreEqual("0", AssetKey("TerrainUnderwaterClip"));
                    StringAssert.Contains("publicboolTerrainUnderwaterClip=DefaultTerrainUnderwaterClip;",
                        Squash(Code(ConfigPath)));
                    StringAssert.Contains(
                        "Config!=null?Config.TerrainUnderwaterClip:GameConfig.DefaultTerrainUnderwaterClip;",
                        Squash(Code(ServicesPath)));
                    GameConfig previous = GameServices.Config;
                    try
                    {
                        GameServices.Config = null;
                        Assert.IsFalse(GameServices.TerrainUnderwaterClip, "no config wired reads OFF");
                    }
                    finally
                    {
                        GameServices.Config = previous;
                    }
                    // OFF, the flag is never raised (TheClipFlag_IsRaisedOnlyByThisCamerasOwnWaterPass), and
                    // the clip is one [branch] the frag skips to its first line of ground, the base frag's.
                    string frag = Squash(Body(Code(TerrainPath), TerrainFrag));
                    const string gate = "[branch]if(_HHTerrainUnderwaterClip>0.5)";
                    StringAssert.StartsWith(gate + "{", frag);
                    string gated = Body(frag, gate);
                    string after = frag.Substring(gate.Length + 1 + gated.Length + 1);
                    StringAssert.StartsWith("float2wp=i.worldXY;", after, "nothing between the gate and the ground");
                    break;
                case "floor":
                    // W4-4b ships OFF, pushed by the sea itself from its own config, beside the salience knobs.
                    Assert.IsFalse(GameConfig.DefaultShallowsFloor);
                    Assert.AreEqual("0", AssetKey("ShallowsFloor"));
                    StringAssert.Contains("publicboolShallowsFloor=DefaultShallowsFloor;", Squash(Code(ConfigPath)));
                    // The bend, read only while the floor is on: 0.25, about 1 - 1/1.33 (light entering sea
                    // water from straight above). An asset lagging the code reads the initializer.
                    Assert.AreEqual(0.25f, GameConfig.DefaultShallowsFloorRefraction);
                    Assert.AreEqual("0.25", AssetKey("ShallowsFloorRefraction"));
                    string sea = Squash(Code(WaterSurfacePath));
                    StringAssert.Contains(
                        "privatestaticreadonlyintIdShallowsFloor=Shader.PropertyToID(\"_ShallowsFloor\");", sea);
                    StringAssert.Contains(
                        "boolon=_config!=null?_config.ShallowsFloor:GameConfig.DefaultShallowsFloor;", sea,
                        "no config wired reads the default, OFF");
                    StringAssert.Contains("mpb.SetFloat(IdShallowsFloor,on?1f:0f);", sea);
                    Assert.AreEqual(2, Count(sea, Regex.Escape("PushSalienceKnobs(_mpb);PushShallowsFloor(_mpb);")),
                        "pushed with the salience knobs, in the edit-mode preview and in play");

                    // OFF, the shader's floor is one [branch] around one assignment of the bed, between the
                    // base's bands and its composite: the bed stays the bake's and the column is the base's.
                    string water = Body(Code(WaterPath), WaterFrag);
                    StringAssert.Contains(BedBand + "#ifdefined(HH_SHALLOWS_FLOOR)" + FloorGate + FloorCall + "#endif" +
                                          BedLerp, Squash(water));
                    Assert.AreEqual(1, Count(Squash(water), @"_ShallowsFloor(?![A-Za-z0-9_])"),
                        "the switch is read once, at its gate");
                    // The wave field's sample moved ahead of the composite (TheFloor_BendsByWholeGroundPixels)
                    // is the same call on inputs the frag sets once, before it, and its outputs are written by
                    // the call alone, so the move moves no pixel.
                    foreach (string input in new[] { "worldXY", "waveFetchFactors", "waveFreqScale" })
                        Assert.AreEqual(1, Count(water, @"(?<![A-Za-z0-9_.])" + input + @"(?:\.[xyzw]+)?\s*[-+*/]?=(?!=)"),
                            input + " is set once");
                    foreach (string output in new[] { "waveHeight", "waveSlope", "waveCrest", "wavePrimCos" })
                        Assert.AreEqual(0, Count(water, @"(?<![A-Za-z0-9_.])" + output + @"(?:\.[xyzw]+)?\s*[-+*/]?=(?!=)"),
                            output + " is written by the sample alone");
                    break;
                default:
                    Assert.Fail("no switch named " + step);
                    break;
            }
        }

        // ---- helpers -----------------------------------------------------------------------------------

        private static bool SameAfterShift(Vector2 min, Vector2 size, int bound, float metres)
        {
            Vector2 m = min, s = size;
            float low = -4f, high = 12f;
            switch (bound)
            {
                case 0: m.x += metres; break;
                case 1: m.y += metres; break;
                case 2: s.x += metres; break;
                case 3: s.y += metres; break;
                case 4: low += metres; break;
                default: high += metres; break;
            }
            return ShallowsFloorMath.SameFrame(min, size, -4f, 12f, m, s, low, high);
        }

        /// <summary>The one YAML document whose m_Script names the given script guid.</summary>
        private static string SceneBlock(string yaml, string scriptGuid)
        {
            string[] blocks = Regex.Split(yaml.Replace("\r", ""), @"(?m)^--- !u!")
                .Where(b => b.Contains("m_Script: {fileID: 11500000, guid: " + scriptGuid + ", type: 3}")).ToArray();
            Assert.AreEqual(1, blocks.Length, "one component of script " + scriptGuid);
            return blocks[0];
        }

        private static string Field(string block, string key)
        {
            Match m = Regex.Match(block, @"(?m)^  " + Regex.Escape(key) + @": ([^\n]*)$");
            Assert.IsTrue(m.Success, key);
            return m.Groups[1].Value.Trim();
        }

        private static string AssetKey(string key)
        {
            string yaml = File.ReadAllText(ConfigAssetPath).Replace("\r", "");
            MatchCollection rows = Regex.Matches(yaml, @"(?m)^  " + Regex.Escape(key) + @": ([^\n]*)$");
            Assert.AreEqual(1, rows.Count, key + " is in GameConfig.asset once");
            return rows[0].Groups[1].Value.Trim();
        }

        /// <summary>The property names a static id array is built from.</summary>
        private static string[] PropertyIds(string cs, string array)
        {
            Match m = Regex.Match(cs, Regex.Escape(array) + @"\s*=\s*\{(?<ids>[^}]*)\}");
            Assert.IsTrue(m.Success, array);
            return Regex.Matches(m.Groups["ids"].Value, @"Shader\.PropertyToID\(""(_\w+)""\)")
                .Cast<Match>().Select(x => x.Groups[1].Value).ToArray();
        }
    }
}
