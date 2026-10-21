using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using NUnit.Framework;
using static HiddenHarbours.Tests.Art.EditMode.ShallowsFloorShaderTests;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The props under the water (W4-4c), the plants' and the rocks' side: which renderers the sea draws
    /// through its own column, and which it never does. A shore plant wholly under the live water, with the
    /// switch on, joins the sea's under-water list; a shore rock never does, because its sprite carries its
    /// own waterline. The shader side of the one law is
    /// <see cref="ShallowsFloorShaderTests.TheFloorAndThePropsShareOneColumnLaw"/>, and the switch's exact
    /// passthrough is <see cref="ShallowsFloorTests"/>' "props" case.
    ///
    /// <para>Headless-safe: source and scene text, and mask arithmetic on a constant, no engine call.</para>
    ///
    /// <para><b>On the base</b> (b8710c59) and at W4-4b (5dab2083) there is no UnderWaterRegistry and no
    /// UnderWaterMask, so this file does not compile there. Against copies of those sources each test fails
    /// at its first assert.</para>
    /// </summary>
    public sealed class ShallowsPropsTests
    {
        private const string ViewPath = "Assets/_Project/Code/Art/ShorePlantTideView.cs";
        private const string MathPath = "Assets/_Project/Code/Art/ShorePlantTideMath.cs";
        private const string RegistryPath = "Assets/_Project/Code/Art/UnderWaterRegistry.cs";
        private const string FeaturePath = "Assets/_Project/Code/Art/IsoFacetHullFeature.cs";
        private const string WaterSurfacePath = "Assets/_Project/Code/Art/WaterSurface.cs";
        private const string PlantMaterialPath = "Assets/_Project/Art/Materials/LitShorePlant.mat";
        private const string CodeDir = "Assets/_Project/Code";
        private const string ScenesDir = "Assets/_Project/Scenes/";

        private const string ViewGuid = "06e7444939cab3b76d2723aa8c09e04f";            // ShorePlantTideView.cs.meta
        private const string PlantMaterialGuid = "7a38660189f548f6a35c5497da3c1867";   // LitShorePlant.mat.meta
        private const string LitSpriteGuid = "4fe9773925dd472fa23c3530e0a90746";       // HiddenHarboursLitSprite.shader.meta
        // Every shore rock's material in the scenes at b8710c59: URP's Sprite-Lit-Default, a package asset
        // (its meta is the package's, not this repo's). Its shader is URP's own, with no HHUnderWater pass.
        private const string SpriteLitDefaultGuid = "a97c105638bdf8b4a8650670310a4cd3";

        // 1u << 27, UnderWaterRegistry.RenderingLayer, beside SilhouetteRegistry's 28, ReflectionRegistry's
        // 29 and DisplacedWaterRegistry's 30.
        private const uint UnderWaterBit = 134217728u;

        private static readonly string[] PlayedRegions = { "StPeters", "NineMileCreek" };

        /// <summary>
        /// A shore plant wholly under the live water is seen through it. It carries the under-water bit
        /// exactly while its own submerged test holds (the test that already sinks its sort order under the
        /// sea, read against the level the sea itself eases to) and the switch is on; the bit puts it in the
        /// feature's under-water list; the list draws its lit sprite's HHUnderWater pass where the sprite
        /// stands, with the main pass's tint, albedo and clip, into the target the sea lays over its floor.
        /// Every placed plant draws with the material that carries that pass, and no scene stores the bit.
        ///
        /// <para>Bars, each beside its assert: this PR's ShorePlantTideView.cs (OnEnable :97, OnDisable
        /// :140-:149, Refresh :166-:192, PropsOn :196, the level :232, Apply :236-:269), WaterSurface.cs :995,
        /// IsoFacetHullFeature.cs (the props block :799-:835, the water pass's read :1181-:1182),
        /// HiddenHarboursLitSprite.shader (the main pass :169-:192, the HHUnderWater pass :236-:300),
        /// LitShorePlant.mat :11, the metas named beside each guid, and the two played scenes at b8710c59
        /// (35 plants at StPeters and 1377 at NineMileCreek, each one SpriteRenderer on LitShorePlant.mat,
        /// mask 1).</para>
        /// </summary>
        [Test]
        public void ASubmergedPlant_IsSeenThroughTheWater()
        {
            string viewCode = Code(ViewPath);
            string view = Squash(viewCode);
            // The switch, play mode only: an edit-mode write of the bit would land in the scene file.
            StringAssert.Contains(
                "privatestaticboolPropsOn=>Application.isPlaying&&GameServices.ShallowsSubmergedProps;", view);
            // Membership is the plant's own submerged test, read against the level the sea eases to.
            StringAssert.Contains("waterLevel=env.WaterLevelAt(now);", view, "the plant's level");
            StringAssert.Contains("_targetWaterLevel=env.WaterLevelAt(now);", Squash(Code(WaterSurfacePath)),
                "the sea's level, by the same call");
            StringAssert.Contains("boolsubmerged=ShorePlantTideMath.IsSubmerged(overM,DrawnStandingHeightM);", view);
            StringAssert.Contains(
                "if(_appliedOnce&&state==_state&&submerged==_submerged&&propsOn==_propsOn)return;" +
                "Apply(state,submerged,propsOn);", view, "a flip of the switch re-applies");

            // The bit follows that one test, written only when it changes, and the list follows the bit.
            string apply = Squash(Body(viewCode, "private void Apply(int state, bool submerged, bool propsOn)"));
            StringAssert.Contains("_sr.sortingOrder=ShorePlantTideMath.SortingOrderFor(submerged,", apply,
                "the order and the bit read one test");
            StringAssert.Contains(
                "uintmask=ShorePlantTideMath.UnderWaterMask(_sr.renderingLayerMask,submerged,propsOn);" +
                "if(mask!=_sr.renderingLayerMask)_sr.renderingLayerMask=mask;" +
                "boolunderWater=(mask&UnderWaterRegistry.RenderingLayer)!=0;" +
                "if(underWater!=_underWater){if(underWater)UnderWaterRegistry.Register(_sr);" +
                "elseUnderWaterRegistry.Unregister(_sr);_underWater=underWater;}", apply);
            StringAssert.EndsWith("_state=state;_submerged=submerged;_propsOn=propsOn;_appliedOnce=true;", apply);
            // Disabled, it leaves the list and gives the bit back; enabled again, it applies afresh.
            Assert.AreEqual(
                "ShorePlantTideDriver.Unregister(this);if(!_underWater)return;UnderWaterRegistry.Unregister(_sr);" +
                "if(_sr!=null)_sr.renderingLayerMask=" +
                "ShorePlantTideMath.UnderWaterMask(_sr.renderingLayerMask,false,false);_underWater=false;",
                Squash(Body(viewCode, "private void OnDisable()")));
            StringAssert.Contains("_state=-1;BindLightSheets();Refresh();",
                Squash(Body(viewCode, "private void OnEnable()")), "re-enabled, it applies afresh");

            // The list: the HHUnderWater pass of every renderer carrying the bit, sorted as the main pass
            // sorts them, into one target the water pass declares it reads, after it is drawn.
            string featureCode = Code(FeaturePath);
            string feature = Squash(featureCode);
            StringAssert.Contains(
                "privatestaticreadonlyShaderTagIds_UnderWaterTag=newShaderTagId(\"HHUnderWater\");", feature);
            Assert.AreEqual(1, Count(featureCode, @"if \(DrawUnderWater\)"), "one props block");
            string props = Squash(Body(featureCode, "if (DrawUnderWater)"));
            foreach (string line in new[]
                     {
                         "RTHandleunderTarget=GetUnderWaterTarget(cameraData.camera.GetEntityId(),w,h);",
                         "underTex=renderGraph.ImportTexture(underTarget,newImportResourceParams{" +
                         "clearOnFirstUse=true,clearColor=Color.clear,discardOnLastUse=false});",
                         "varsorting=newSortingSettings(cameraData.camera){criteria=SortingCriteria.CommonTransparent};",
                         "vardrawing=newDrawingSettings(s_UnderWaterTag,sorting){perObjectData=PerObjectData.None};",
                         "varfiltering=newFilteringSettings(RenderQueueRange.all,-1,UnderWaterRegistry.RenderingLayer);",
                         "builder.SetRenderAttachment(underTex,0);",
                         "builder.SetGlobalTextureAfterPass(underTex,UnderWaterShaderIds.UnderWaterTex);",
                     })
                StringAssert.Contains(line, props);
            const string read = "if(underTex.IsValid())builder.UseTexture(underTex,AccessFlags.Read);";
            Assert.AreEqual(1, Count(feature, Regex.Escape(read)), "the water pass reads the props once");
            Assert.Greater(feature.IndexOf(read, System.StringComparison.Ordinal),
                feature.IndexOf("builder.SetGlobalTextureAfterPass(underTex,", System.StringComparison.Ordinal),
                "after they are drawn");
            StringAssert.Contains("publicconstuintRenderingLayer=1u<<27;", Squash(Code(RegistryPath)));

            // The pass: the main pass's placement, tint, albedo and clip, premultiplied, nothing lit (its
            // frag is TheFloorAndThePropsShareOneColumnLaw's).
            string lit = Code(LitSpritePath);
            StringAssert.Contains(
                "float3wp=TransformObjectToWorld(IN.positionOS);OUT.positionCS=TransformWorldToHClip(wp);" +
                "OUT.color=IN.color*_Color;OUT.uv=IN.uv;", Squash(Body(lit, "Varyings vert(Attributes IN)")),
                "the main pass's placement");
            StringAssert.StartsWith(
                "UNITY_SETUP_INSTANCE_ID(IN);half4tex=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,IN.uv);" +
                "half4col=tex*IN.color;clip(col.a-_AlphaClip);",
                Squash(Body(lit, "half4 frag(Varyings IN) : SV_Target")), "the main pass's albedo and clip");
            string pass = UnderWaterPass(lit);
            string passSquashed = Squash(pass);
            StringAssert.Contains("BlendOneOneMinusSrcAlpha", passSquashed, "premultiplied");
            StringAssert.Contains("ZWriteOff", passSquashed);
            StringAssert.Contains("ZTestAlways", passSquashed);
            Assert.AreEqual(
                "UnderWaterVaryingsOUT;UNITY_SETUP_INSTANCE_ID(IN);UNITY_TRANSFER_INSTANCE_ID(IN,OUT);" +
                "OUT.positionCS=TransformWorldToHClip(TransformObjectToWorld(IN.positionOS));" +
                "OUT.color=IN.color*_Color;OUT.uv=IN.uv;returnOUT;",
                Squash(Body(pass, "UnderWaterVaryings underWaterVert(UnderWaterAttributes IN)")), "where it stands");

            // Every placed plant draws with the material whose shader carries that pass, and no scene
            // stores the bit.
            StringAssert.Contains("guid: " + ViewGuid, File.ReadAllText(ViewPath + ".meta"));
            StringAssert.Contains("guid: " + PlantMaterialGuid, File.ReadAllText(PlantMaterialPath + ".meta"));
            StringAssert.Contains("guid: " + LitSpriteGuid, File.ReadAllText(LitSpritePath + ".meta"));
            StringAssert.Contains("m_Shader: {fileID: 4800000, guid: " + LitSpriteGuid + ", type: 3}",
                File.ReadAllText(PlantMaterialPath));
            foreach (string region in PlayedRegions)
            {
                SceneIndex scene = SceneIndex.Read(ScenesDir + region + ".unity");
                string[] plants = scene.WithScript(ViewGuid);
                Assert.Greater(plants.Length, 0, region + ": placed plants");
                foreach (string plant in plants)
                    AssertOneSprite(scene, plant, PlantMaterialGuid, region + " plant " + plant);
            }

            // The mask arithmetic. Bars: 1u << 27 = 134217728; 1 | 1u << 27 = 134217729; a reflector's
            // 536870913 (1 | 1u << 29) | 1u << 27 = 671088641.
            Assert.AreEqual(UnderWaterBit, UnderWaterRegistry.RenderingLayer);
            Assert.AreEqual(134217729u, ShorePlantTideMath.UnderWaterMask(1u, true, true), "under, on: it joins");
            Assert.AreEqual(134217729u, ShorePlantTideMath.UnderWaterMask(134217729u, true, true), "and stays");
            Assert.AreEqual(671088641u, ShorePlantTideMath.UnderWaterMask(536870913u, true, true),
                "beside the other bits");
            Assert.AreEqual(1u, ShorePlantTideMath.UnderWaterMask(134217729u, false, true), "emerged: it leaves");
            Assert.AreEqual(1u, ShorePlantTideMath.UnderWaterMask(134217729u, true, false), "switched off: it leaves");
            Assert.AreEqual(536870913u, ShorePlantTideMath.UnderWaterMask(671088641u, false, false),
                "and the other bits stay");
            Assert.AreEqual(1u, ShorePlantTideMath.UnderWaterMask(1u, false, false), "never there, never added");
            // The one submerged test it reads (ShorePlantTideMath.IsSubmerged, unchanged): wholly under.
            Assert.IsTrue(ShorePlantTideMath.IsSubmerged(0.5f, 0.5f), "a top at the surface is under");
            Assert.IsFalse(ShorePlantTideMath.IsSubmerged(0.49f, 0.5f), "a tip above it is not");
        }

        /// <summary>
        /// An awash rock keeps its ONE waterline: its sprite is clipped at the sea plane its bake assumed
        /// (RockRigBakeTests.AnAwashRockIsCLIPPED_NotFlooded_SoNoWaterIsEverBaked), and no rock is also drawn
        /// under the water, which would be a second line for the sea's to disagree with. Only the plant view
        /// puts a renderer in the sea's under-water list, only the lit sprite carries the pass that list
        /// draws, and every placed rock draws with Sprite-Lit-Default, with no plant view and no under-water
        /// bit. Drawing a rock's below-water pixels through the column (its pixel rows against the sea's own
        /// level over the DRAWN ground at its foot, wearing its wet row) is a material change in both played
        /// scenes, raised before it is built; that change moves this guard by name.
        ///
        /// <para>Bars: this PR's sources (one registrar, one mask writer, the bit spelt once, one pass) and
        /// the scenes at b8710c59 (33 rocks under Shoreline/ShoreRocks at StPeters and 18 at NineMileCreek,
        /// each one SpriteRenderer on Sprite-Lit-Default, mask 1).</para>
        /// </summary>
        [Test]
        public void AnAwashRock_DrawsNoSecondWaterline()
        {
            // One pass: only the lit sprite can be drawn under the water.
            string[] passes = Files("Assets", "*.shader").Concat(Files("Assets", "*.hlsl"))
                .Where(p => Regex.IsMatch(Code(p), @"""LightMode""\s*=\s*""HHUnderWater""")).ToArray();
            CollectionAssert.AreEqual(new[] { LitSpritePath }, passes, "the one shader with an HHUnderWater pass");

            // One way in: only the plant view registers, only it and its math write the bit, and the bit
            // is spelt once.
            KeyValuePair<string, string>[] code = Files(CodeDir, "*.cs")
                .Select(p => new KeyValuePair<string, string>(p, Code(p))).ToArray();
            CollectionAssert.AreEquivalent(new[] { ViewPath }, Using(code, @"UnderWaterRegistry\.Register\("),
                "one registrar");
            CollectionAssert.AreEquivalent(new[] { MathPath, ViewPath }, Using(code, @"\bUnderWaterMask\("),
                "one mask, written by the plant view");
            CollectionAssert.AreEquivalent(new[] { MathPath, ViewPath, FeaturePath },
                Using(code, @"UnderWaterRegistry\.RenderingLayer"), "the bit's readers");
            CollectionAssert.AreEquivalent(new[] { RegistryPath }, Using(code, @"<<\s*27\b"), "the bit, spelt once");
            CollectionAssert.IsEmpty(Using(code, @"\b134217728"), "and never as a number");

            // The rocks: every one under ShoreRocks draws with Sprite-Lit-Default, no plant view, no bit.
            foreach (string region in PlayedRegions)
            {
                SceneIndex scene = SceneIndex.Read(ScenesDir + region + ".unity");
                string[] rocks = scene.ChildrenOf(scene.OneNamed("ShoreRocks"));
                Assert.Greater(rocks.Length, 0, region + ": the shore rocks");
                foreach (string rock in rocks)
                {
                    CollectionAssert.DoesNotContain(scene.ScriptsOn(rock), ViewGuid,
                        region + " rock " + rock + ": no plant view");
                    AssertOneSprite(scene, rock, SpriteLitDefaultGuid, region + " rock " + rock);
                }
            }
        }

        private static void AssertOneSprite(SceneIndex scene, string gameObject, string materialGuid, string what)
        {
            List<SceneIndex.Sprite> sprites = scene.SpritesOn(gameObject);
            Assert.AreEqual(1, sprites.Count, what + ": one SpriteRenderer");
            Assert.AreEqual(materialGuid, sprites[0].Material, what + ": its material");
            Assert.AreEqual(0u, sprites[0].Mask & UnderWaterBit, what + ": the scene stores no under-water bit");
        }

        private static IEnumerable<string> Files(string dir, string pattern) =>
            Directory.GetFiles(dir, pattern, SearchOption.AllDirectories).Select(p => p.Replace('\\', '/'));

        private static string[] Using(IEnumerable<KeyValuePair<string, string>> code, string pattern) =>
            code.Where(c => Regex.IsMatch(c.Value, pattern)).Select(c => c.Key).ToArray();

        /// <summary>The few fields these guards read from a scene, streamed a line at a time: a played
        /// scene is 5 to 16 MB, and nothing here needs it whole.</summary>
        private sealed class SceneIndex
        {
            public struct Sprite
            {
                public string Material;
                public uint Mask;
            }

            private readonly Dictionary<string, string> _names = new Dictionary<string, string>();
            private readonly Dictionary<string, string> _gameObjectOf = new Dictionary<string, string>();
            private readonly Dictionary<string, string> _transformOf = new Dictionary<string, string>();
            private readonly Dictionary<string, List<string>> _children = new Dictionary<string, List<string>>();
            private readonly Dictionary<string, List<string>> _scripts = new Dictionary<string, List<string>>();
            private readonly Dictionary<string, List<Sprite>> _sprites = new Dictionary<string, List<Sprite>>();

            // The document being read, and the list inside it being read (m_Children or m_Materials), if any.
            private int _class;
            private string _id, _gameObject, _name, _script, _material, _list;
            private List<string> _childTransforms = new List<string>();
            private int _items;
            private uint _mask;

            public static SceneIndex Read(string path)
            {
                var index = new SceneIndex();
                foreach (string raw in File.ReadLines(path))
                    index.Line(raw.TrimEnd('\r'));
                index.Close();
                return index;
            }

            public string OneNamed(string name)
            {
                string[] found = _names.Where(n => n.Value == name).Select(n => n.Key).ToArray();
                Assert.AreEqual(1, found.Length, "one GameObject named " + name);
                return found[0];
            }

            public string[] ChildrenOf(string gameObject)
            {
                Assert.IsTrue(_transformOf.TryGetValue(gameObject, out string transform), "its Transform");
                return _children[transform].Select(t =>
                {
                    Assert.IsTrue(_gameObjectOf.TryGetValue(t, out string child), "child Transform " + t + "'s GameObject");
                    return child;
                }).ToArray();
            }

            public string[] WithScript(string guid) =>
                _scripts.Where(s => s.Value.Contains(guid)).Select(s => s.Key).ToArray();

            public List<string> ScriptsOn(string gameObject) =>
                _scripts.TryGetValue(gameObject, out List<string> s) ? s : new List<string>();

            public List<Sprite> SpritesOn(string gameObject) =>
                _sprites.TryGetValue(gameObject, out List<Sprite> s) ? s : new List<Sprite>();

            private void Line(string line)
            {
                if (line.StartsWith("--- !u!", System.StringComparison.Ordinal))
                {
                    Close();
                    Match head = Regex.Match(line, @"^--- !u!(\d+) &(-?\d+)");
                    _class = head.Success ? int.Parse(head.Groups[1].Value) : 0;
                    _id = head.Success ? head.Groups[2].Value : null;
                    _gameObject = _name = _script = _material = _list = null;
                    _childTransforms = new List<string>();
                    _mask = 0;
                    return;
                }
                if (_list != null)
                {
                    if (line.StartsWith("  - ", System.StringComparison.Ordinal))
                    {
                        if (_list == "m_Children") _childTransforms.Add(FileId(line));
                        else if (_items == 0)
                        {
                            Match guid = Regex.Match(line, @"guid: ([0-9a-f]{32})");
                            if (guid.Success) _material = guid.Groups[1].Value;
                        }
                        _items++;
                        return;
                    }
                    _list = null;
                }
                if (line.StartsWith("  m_GameObject: ", System.StringComparison.Ordinal)) _gameObject = FileId(line);
                else if ((_class == 4 || _class == 224) && line == "  m_Children:") Open("m_Children");
                else if (_class == 1 && line.StartsWith("  m_Name: ", System.StringComparison.Ordinal))
                    _name = line.Substring("  m_Name: ".Length);
                else if (_class == 114 && line.StartsWith("  m_Script: ", System.StringComparison.Ordinal))
                {
                    Match guid = Regex.Match(line, @"guid: ([0-9a-f]{32})");
                    if (guid.Success) _script = guid.Groups[1].Value;
                }
                else if (_class == 212 && line.StartsWith("  m_RenderingLayerMask: ", System.StringComparison.Ordinal))
                    _mask = uint.Parse(line.Substring("  m_RenderingLayerMask: ".Length));
                else if (_class == 212 && line == "  m_Materials:")
                    Open("m_Materials");
            }

            private void Open(string list)
            {
                _list = list;
                _items = 0;
            }

            private void Close()
            {
                if (_id == null) return;
                switch (_class)
                {
                    case 1:
                        if (_name != null) _names[_id] = _name;
                        break;
                    case 4:
                    case 224:
                        if (_gameObject == null) break;
                        _gameObjectOf[_id] = _gameObject;
                        _transformOf[_gameObject] = _id;
                        _children[_id] = _childTransforms;
                        break;
                    case 114:
                        if (_gameObject != null && _script != null) Add(_scripts, _gameObject, _script);
                        break;
                    case 212:
                        if (_gameObject != null)
                            Add(_sprites, _gameObject, new Sprite { Material = _material, Mask = _mask });
                        break;
                }
            }

            private static string FileId(string line)
            {
                Match m = Regex.Match(line, @"\{fileID: (-?\d+)");
                return m.Success ? m.Groups[1].Value : null;
            }

            private static void Add<T>(Dictionary<string, List<T>> map, string key, T value)
            {
                if (!map.TryGetValue(key, out List<T> list)) map[key] = list = new List<T>();
                list.Add(value);
            }
        }
    }
}
