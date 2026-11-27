using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>Decks over calm water reflect their UNDERSIDE</b> (the owner, 2026-11-26: piers and anything standing
    /// over water show what is under them in the reflection, not their deck top).
    ///
    /// <para>The underside pieces are data (<see cref="UndersideDef"/>, one asset each, stable ids) on ONE sheet
    /// baked from <c>undersideRig.js</c>; the calm mirror lays a piece only where a deck's reflected face draws no
    /// art, so a deck that names none reflects exactly as before (slot 0). Packing them costs no allocation.</para>
    /// </summary>
    public class UndersideReflectionTests
    {
        const string DefFolder = "Assets/_Project/Data/Regions/Undersides";
        const string ConfigAsset = "Assets/_Project/Data/Config/GameConfig.asset";
        const string MirrorShader = "Assets/_Project/Art/Shaders/HiddenHarboursCalmMirror.shader";
        const string MirrorCode = "Assets/_Project/Code/Art/CalmMirror.cs";

        static IRigScriptHost _host;
        static readonly Rect View = new Rect(-50f, -50f, 100f, 100f);

        [OneTimeSetUp]
        public void Boot()
        {
            _host = RigScriptHostFactory.Create();
            UndersideSheetBaker.Install(_host);
        }

        [OneTimeTearDown]
        public void Down()
        {
            _host?.Dispose();
            _host = null;
        }

        static UndersideDef[] Defs() => AssetDatabase.FindAssets("t:UndersideDef", new[] { DefFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<UndersideDef>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(d => d != null).OrderBy(d => d.Id, StringComparer.Ordinal).ToArray();

        static Color32[] CommittedSheet(out int w, out int h)
        {
            byte[] png = File.ReadAllBytes(Path.Combine(RigCatalog.RepoRoot, UndersideSheetBaker.SheetPath));
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                Assert.IsTrue(tex.LoadImage(png, false), "Undersides.png does not decode (an LFS pointer?)");
                w = tex.width; h = tex.height;
                return tex.GetPixels32();
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }

        // ---- the art: the rig's, at the art's own grid, dark ----

        [Test]
        public void TheCommittedSheetIsExactlyWhatTheRigRenders()
        {
            Color32[] rig = UndersideSheetBaker.RenderSheet(_host, out int rw, out int rh);
            Color32[] png = CommittedSheet(out int pw, out int ph);
            Assert.AreEqual(rw, pw, "sheet width");
            Assert.AreEqual(rh, ph, "sheet height");
            int bad = 0, first = -1;
            for (int i = 0; i < rig.Length; i++)
            {
                Color32 a = rig[i], b = png[i];
                if (a.a == 0 && b.a == 0) continue;
                if (a.r != b.r || a.g != b.g || a.b != b.b || a.a != b.a) { bad++; if (first < 0) first = i; }
            }
            Assert.AreEqual(0, bad, $"{bad} pixels differ between undersideRig.js and Undersides.png (first at x {first % rw}, " +
                                    $"y {first / rw}): re-bake with Hidden Harbours/Art/Bake Undersides");
        }

        [Test]
        public void TheRigPaintsAtTheArtGrid_AndTheSheetImportsPointFilteredWithoutMips()
        {
            Assert.AreEqual(FigurePixelGrid.ArtPixelsPerMetre, (int)_host.EvaluateNumber("Underside.PX_PER_M"),
                "one texel per art pixel on screen (no mixels)");
            var importer = AssetImporter.GetAtPath(UndersideSheetBaker.SheetPath) as TextureImporter;
            Assert.IsNotNull(importer, "the sheet is imported as a texture");
            Assert.AreEqual(FilterMode.Point, importer.filterMode, "point filtered");
            Assert.IsFalse(importer.mipmapEnabled, "no mips: the mirror loads exact texels");
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression, "no block compression smears the pixels");
            Assert.AreEqual(TextureImporterNPOTScale.None, importer.npotScale, "never resampled to a power of two");
            Assert.IsTrue(importer.sRGBTexture, "colour art");
        }

        [Test]
        public void EveryCellIsShadowedTimber_DarkAndLowContrast()
        {
            Color32[] png = CommittedSheet(out int w, out _);
            foreach (UndersideDef d in Defs())
            {
                RectInt c = d.Cell;
                float lo = 1f, hi = 0f;
                for (int y = c.yMin; y < c.yMax; y++)
                    for (int x = c.xMin; x < c.xMax; x++)
                    {
                        Color32 p = png[y * w + x];
                        Assert.AreEqual(255, p.a, $"{d.Id}: every cell texel is opaque");
                        float g = ((Color)p).grayscale;
                        lo = Mathf.Min(lo, g); hi = Mathf.Max(hi, g);
                    }
                Assert.Less(hi, 0.3f, $"{d.Id}: an underside is in its own shade");
                Assert.Less(hi - lo, 0.2f, $"{d.Id}: low contrast, so it reads as timber under a deck, not a pattern");
            }
        }

        // ---- the data: one asset per piece, one sheet, the rig's cells ----

        [Test]
        public void EveryRigPieceHasExactlyOneDef_WithTheRigsCellAndFascia_OnTheOneSheet()
        {
            UndersideDef[] defs = Defs();
            Texture2D sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(UndersideSheetBaker.SheetPath);
            Assert.IsNotNull(sheet);
            int n = UndersideSheetBaker.PieceCount(_host);
            Assert.AreEqual(n, defs.Length, "one Def per piece the rig paints");
            for (int i = 0; i < n; i++)
            {
                string id = UndersideSheetBaker.PieceId(_host, i);
                UndersideDef[] match = defs.Where(d => d.Id == id).ToArray();
                Assert.AreEqual(1, match.Length, $"exactly one Def carries {id}");
                UndersideDef def = match[0];
                Assert.AreSame(sheet, def.Atlas, $"{id}: every piece shares the one sheet (one texture bound per frame)");
                Assert.AreEqual(UndersideSheetBaker.UnityCell(_host, i), def.Cell, $"{id}: the rig's cell, in Unity's rows");
                Assert.AreEqual(UndersideSheetBaker.PieceFascia(_host, i), def.FasciaRows, $"{id}: the rig's fascia rows");
                Assert.IsTrue(def.IsUsable, $"{id}: usable");
                StringAssert.IsMatch(@"^underside\.[a-z0-9_]+$", def.Id, "ids are type.snake_case");
            }
        }

        [Test]
        public void Texel_WrapsColumns_LaysTheFasciaOnce_AndRepeatsTheRowsAbove()
        {
            var cell = new RectInt(10, 20, 8, 6);
            Assert.AreEqual(new Vector2Int(10, 20), UndersideMath.Texel(cell, 2, 0, 0));
            Assert.AreEqual(new Vector2Int(17, 20), UndersideMath.Texel(cell, 2, -1, 0), "negative world columns wrap");
            Assert.AreEqual(new Vector2Int(11, 20), UndersideMath.Texel(cell, 2, 9, 0));
            Assert.AreEqual(new Vector2Int(10, 21), UndersideMath.Texel(cell, 2, 0, 1), "the fascia's second row");
            Assert.AreEqual(new Vector2Int(10, 25), UndersideMath.Texel(cell, 2, 0, 5), "the cell's top row");
            Assert.AreEqual(new Vector2Int(10, 22), UndersideMath.Texel(cell, 2, 0, 6), "past the top: the body repeats, never the fascia");
            Assert.AreEqual(new Vector2Int(10, 23), UndersideMath.Texel(cell, 2, 0, 11));
            Assert.AreEqual(new Vector2Int(10, 20), UndersideMath.Texel(cell, 2, 0, -3), "never below the far end");
            Assert.IsFalse(UndersideMath.CellFits(new RectInt(0, 0, 4, 2), 2, 16, 16), "no rows left to repeat");
            Assert.IsFalse(UndersideMath.CellFits(new RectInt(10, 0, 8, 4), 1, 16, 16), "outside the sheet");
            Assert.IsTrue(UndersideMath.CellFits(new RectInt(8, 0, 8, 4), 1, 16, 16));
        }

        // ---- the packing: slot 0 is today's look, nothing allocates ----

        sealed class Deck : IStandableSurface, IStandableOutline, IReflectedUnderside
        {
            public Vector2[] Corners = new Vector2[4];
            public float[] Elevations = new float[4];
            public UndersideDef Under;
            public string Id => "test.deck";
            public UndersideDef Underside => Under;
            public bool TryGetDeckElevation(Vector2 worldPos, out float deckElevation) { deckElevation = 0f; return false; }
            public int GetDrawnOutline(Vector2[] drawn, float[] deckElevation)
            {
                for (int i = 0; i < 4; i++) { drawn[i] = Corners[i]; deckElevation[i] = Elevations[i]; }
                return 4;
            }
        }

        static Deck RectDeck(float x, float elevation, UndersideDef under)
        {
            var d = new Deck { Under = under };
            StandableOutline.Rect(new Rect(x, 0f, 4f, 2f), 0f, elevation, d.Corners, d.Elevations);
            return d;
        }

        static UndersideDef MakeDef(Texture2D atlas, int y)
        {
            var d = ScriptableObject.CreateInstance<UndersideDef>();
            d.Id = "underside.test"; d.Atlas = atlas; d.CellX = 0; d.CellY = y; d.CellWidth = 8; d.CellHeight = 6; d.FasciaRows = 2;
            return d;
        }

        [Test]
        public void ADeckWithNoUnderside_PacksExactlyAsBefore_Slot0()
        {
            var surfaces = new IStandableSurface[] { RectDeck(0f, 2f, null), RectDeck(8f, 3f, null) };
            var a0 = new Vector4[4]; var b0 = new Vector4[4]; var h0 = new Vector4[4];
            int n0 = CalmMirrorMath.PackTops(surfaces, View, 0.5f, new Vector2[16], new float[16], a0, b0, h0);
            var a1 = new Vector4[4]; var b1 = new Vector4[4]; var h1 = new Vector4[4]; var u1 = new Vector4[4];
            Texture2D atlas = null;
            int n1 = CalmMirrorMath.PackTops(surfaces, View, 0.5f, new Vector2[16], new float[16], a1, b1, h1, u1, ref atlas);
            Assert.AreEqual(n0, n1);
            CollectionAssert.AreEqual(a0, a1); CollectionAssert.AreEqual(b0, b1); CollectionAssert.AreEqual(h0, h1);
            for (int i = 0; i < n1; i++)
            {
                Assert.AreEqual(0f, h1[i].z, "slot 0: no underside, the face band and open gaps as before");
                Assert.AreEqual(Vector4.zero, u1[i]);
            }
            Assert.IsNull(atlas, "no sheet bound");
        }

        [Test]
        public void ADeckThatNamesAnUnderside_PacksItsCell_AndOnlyOneSheetIsEverLaid()
        {
            var atlas = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            var other = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            UndersideDef laid = MakeDef(atlas, 4), stray = MakeDef(other, 0);
            try
            {
                var surfaces = new IStandableSurface[] { RectDeck(0f, 2f, laid), RectDeck(8f, 2f, stray) };
                var a = new Vector4[4]; var b = new Vector4[4]; var h = new Vector4[4]; var u = new Vector4[4];
                Texture2D bound = null;
                int n = CalmMirrorMath.PackTops(surfaces, View, 0.5f, new Vector2[16], new float[16], a, b, h, u, ref bound);
                Assert.AreEqual(2, n);
                Assert.AreSame(atlas, bound);
                Assert.AreEqual(1f, h[0].z); Assert.AreEqual(2f, h[0].y, "its fascia rows");
                Assert.AreEqual(new Vector4(0f, 4f, 8f, 6f), u[0], "its cell, in texels");
                Assert.AreEqual(1.5f * IsoGround.HeightScale, h[0].x, 1e-5f, "the face still follows the tide");
                Assert.AreEqual(0f, h[1].z, "a piece on another sheet is not laid (one texture per frame)");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(laid); UnityEngine.Object.DestroyImmediate(stray);
                UnityEngine.Object.DestroyImmediate(atlas); UnityEngine.Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void PackingUndersides_AllocatesNothing()
        {
            var atlas = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            UndersideDef def = MakeDef(atlas, 4);
            try
            {
                var surfaces = new List<IStandableSurface> { RectDeck(0f, 2f, def), RectDeck(8f, 3f, null), RectDeck(16f, 2f, def) };
                var a = new Vector4[CalmMirror.MaxTops]; var b = new Vector4[CalmMirror.MaxTops];
                var h = new Vector4[CalmMirror.MaxTops]; var u = new Vector4[CalmMirror.MaxTops];
                var outline = new Vector2[16]; var elevation = new float[16];
                void Frame()
                {
                    for (int f = 0; f < 60; f++)
                    {
                        Texture2D bound = null;
                        CalmMirrorMath.PackTops(surfaces, View, 0.5f + f * 0.01f, outline, elevation, a, b, h, u, ref bound);
                    }
                }
                Frame();
                Assert.That(() => Frame(), UnityEngine.TestTools.Constraints.Is.Not.AllocatingGCMemory(), "rule 7: nothing per frame");
            }
            finally { UnityEngine.Object.DestroyImmediate(def); UnityEngine.Object.DestroyImmediate(atlas); }
        }

        // ---- the wiring: shader and pass agree, the share is data ----

        [Test]
        public void TheShareIsData_ShippedInTheConfig()
        {
            WaterMirrorSettings s = WaterMirrorSettings.Default;
            Assert.That(s.UndersideShare, Is.InRange(0.01f, 1f), "laid by default");
            Assert.IsTrue(Regex.IsMatch(File.ReadAllText(ConfigAsset), @"^\s*UndersideShare:\s*[0-9.]+\s*$", RegexOptions.Multiline),
                "the shipped GameConfig carries UndersideShare");
        }

        [Test]
        public void ThePassFeedsTheShader_OneSheet_OneCellArray_SizedToTheTops()
        {
            string shader = File.ReadAllText(MirrorShader), code = File.ReadAllText(MirrorCode);
            StringAssert.Contains("float4 _CalmMirrorTopU[HH_MAX_TOPS];", shader);
            StringAssert.Contains("TEXTURE2D(_CalmMirrorUnderTex);", shader);
            StringAssert.Contains("float4 _CalmMirrorUnder;", shader);
            StringAssert.Contains("LOAD_TEXTURE2D(_CalmMirrorUnderTex, ut)", shader, "exact texels, no filtering");
            StringAssert.Contains("_CalmMirrorTopH[capI].z > 0.5", shader, "laid only for a deck that names a piece");
            StringAssert.Contains("Shader.PropertyToID(\"_CalmMirrorTopU\")", code);
            StringAssert.Contains("Shader.PropertyToID(\"_CalmMirrorUnderTex\")", code);
            StringAssert.Contains("Shader.PropertyToID(\"_CalmMirrorUnder\")", code);
            StringAssert.Contains("s_TopA, s_TopB, s_TopH, s_TopU, ref underAtlas", code);
            StringAssert.Contains("new Vector4[MaxTops];", code);
            StringAssert.Contains("Mathf.Clamp01(s.UndersideShare)", code);
        }

        [Test]
        public void AHangerCutByTheWater_IsMirroredAboutTheRealWaterline_NotItsTip()
        {
            string shader = File.ReadAllText(MirrorShader);
            StringAssert.Contains("bool hung = world.y < wl - snap && baseY >= wl - _CalmMirrorTopH[ti].x;", shader);
            StringAssert.Contains("if ((baseY >= wl - snap || hung) && baseY <= hi", shader);
        }

        // ---- the scenes: every deck over water names its underside, or says why not ----

        static readonly Dictionary<string, string> ComponentGuids = new Dictionary<string, string>
        {
            { "44422230a0a985e44ac4eb7c9dd04712", "StandablePlatform" },
            { "3f7c2a91b48d4e5a9c06d1f8b27e4530", "FloatingPlatform" },
            { "8a41c6d0e7b24f13ab9527c4d80f6e19", "GangwayPlatform" },
            { "af684d0b0b9a60ae94ae9b10942c7ba0", "NmcWharfWalkSurface" },
        };

        /// <summary>Decks that reflect no underside, each with its reason (scene/object name).</summary>
        static readonly Dictionary<string, string> Exempt = new Dictionary<string, string>
        {
            { "NineMileCreek/NorthWall", "a solid quay wall: its face is the whole reflection" },
            { "NineMileCreek/WestWall", "a solid quay wall" },
            { "NineMileCreek/crib.walk", "a timber crib, solid to the bed" },
            { "NineMileCreek/conc.walk", "a concrete apron on a solid face" },
            { "NineMileCreek/apron.walk", "the apron behind the face: never over water" },
            { "NineMileCreek/east.walk", "the east wall's walk, a solid face" },
            { "NineMileCreek/rampUpper.walk", "a solid slipway, on the ground" },
            { "NineMileCreek/rampLower.walk", "a solid slipway, under at high water" },
            { "NineMileCreek/yardFront/walk0", "the yard front, a solid face" },
            { "NineMileCreek/slab/walk0", "pending a plate: solid or on piles" },
            { "NineMileCreek/shed/walk0", "pending a plate: solid or on piles" },
            { "StPeters/bridge.stp_gap_footbridge", "pending a plate: whether it stands over tidal water" },
        };

        [Test, Ignore("Lands with the builder pass that names each deck's underside (needs an editor slot).")]
        public void EveryDeckOverWaterInAScene_NamesAnUnderside_OrIsExemptWithAReason()
        {
            var missing = new List<string>();
            foreach (string scene in new[] { "NineMileCreek", "StPeters" })
            {
                string text = File.ReadAllText($"Assets/_Project/Scenes/{scene}.unity");
                string[] docs = Regex.Split("\n" + text, @"\n--- !u!(\d+) &(-?\d+)[^\n]*\n");
                var names = new Dictionary<string, string>();
                var decks = new List<(string go, bool named)>();
                for (int i = 1; i + 2 < docs.Length; i += 3)
                {
                    string cls = docs[i], id = docs[i + 1], body = docs[i + 2];
                    if (cls == "1") names[id] = Regex.Match(body, @"m_Name: (.*)").Groups[1].Value.Trim();
                    else if (cls == "114")
                    {
                        Match g = Regex.Match(body, @"m_Script: \{fileID: \d+, guid: (\w+)");
                        if (!g.Success || !ComponentGuids.ContainsKey(g.Groups[1].Value)) continue;
                        string go = Regex.Match(body, @"m_GameObject: \{fileID: (-?\d+)").Groups[1].Value;
                        bool named = Regex.IsMatch(body, @"\n  _underside: \{fileID: 11400000, guid: [0-9a-f]{32}");
                        decks.Add((go, named));
                    }
                }
                foreach (var (go, named) in decks)
                {
                    string key = scene + "/" + (names.TryGetValue(go, out string n) ? n : go);
                    if (named) Assert.IsFalse(Exempt.ContainsKey(key), $"{key} names an underside but is listed exempt");
                    else if (!Exempt.ContainsKey(key)) missing.Add(key);
                }
            }
            Assert.IsEmpty(missing, "decks over water with no underside and no exemption: " + string.Join(", ", missing));
        }
    }
}
