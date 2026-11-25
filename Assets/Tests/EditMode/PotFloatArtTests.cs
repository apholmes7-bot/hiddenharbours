using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The pot floats: the sheet is its rig's, the colours are data, the radii agree.</b>
    ///
    /// <para>The floats are lathed by <c>potFloatRig.js</c> on the nav kit's turntable and committed as
    /// one sheet (<c>PotFloats.png</c>). Owners' colours are a ramp swap computed in C#
    /// (<see cref="PotFloatPaint"/>) and matched exactly by the shader, so three things must hold to the
    /// byte: the committed PNG is what the rig renders, the C# ramp is the rig's ramp, and no colour
    /// outside the two paint coats sits close enough to a paint colour to be recoloured with it. The
    /// curved waterline's radius lives in data in four places; each must equal the rig's own figure.</para>
    /// </summary>
    public class PotFloatArtTests
    {
        const string SpriteSheet = PotFloatSheetBaker.SheetPath;
        const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        const string LobsterPath = "Assets/_Project/Data/Traps/LobsterTrap.asset";
        const string CrabPath = "Assets/_Project/Data/Traps/CrabTrap.asset";
        const string FleetPath = "Assets/_Project/Data/Boats/StPetersAmbientFleet.asset";

        /// <summary>BuoyWaveVisual's match tolerance (squared, linear RGB).</summary>
        const float MatchEpsSq = 3.1e-06f;

        static IRigScriptHost _host;

        [OneTimeSetUp]
        public void Boot()
        {
            _host = RigScriptHostFactory.Create();
            PotFloatSheetBaker.Install(_host);
        }

        [OneTimeTearDown]
        public void Down()
        {
            _host?.Dispose();
            _host = null;
        }

        static Color32[] CommittedSheet(out int w, out int h)
        {
            byte[] png = File.ReadAllBytes(Path.Combine(RigCatalog.RepoRoot, SpriteSheet));
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                Assert.IsTrue(tex.LoadImage(png, false), "PotFloats.png does not decode (an LFS pointer?)");
                w = tex.width; h = tex.height;
                return tex.GetPixels32();
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }

        static double Meta(string shape) =>
            _host.EvaluateNumber($"PotFloat.META['{shape}'].waterRadius");

        static Color32 Hex(string hex)
        {
            hex = hex.TrimStart('#');
            return new Color32(Convert.ToByte(hex.Substring(0, 2), 16), Convert.ToByte(hex.Substring(2, 2), 16),
                               Convert.ToByte(hex.Substring(4, 2), 16), 0xff);
        }

        static Color32[] RigRamp(string expr)
        {
            string joined = _host.EvaluateString($"({expr}).join(',')");
            return joined.Split(',').Select(Hex).ToArray();
        }

        static string ToHex(Color32 c) => $"#{c.r:x2}{c.g:x2}{c.b:x2}";

        [Test]
        public void TheCommittedSheetIsExactlyWhatTheRigRenders()
        {
            Color32[] rig = PotFloatSheetBaker.RenderSheet(_host, out int rw, out int rh);
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
            Assert.AreEqual(0, bad,
                $"{bad} pixels differ between potFloatRig.js and PotFloats.png (first at x {first % rw}, y {first / rw}): re-bake with Hidden Harbours/Art/Bake Pot Floats");
        }

        [Test]
        public void TheSheetHasOneCellPerShapeWithBinaryAlpha()
        {
            Assert.AreEqual(string.Join(",", PotFloatSheetBaker.Shapes), _host.EvaluateString("PotFloat.SHAPES.join(',')"));
            Color32[] png = CommittedSheet(out int w, out int h);
            int cell = (int)_host.EvaluateNumber("PotFloat.W");
            Assert.AreEqual(cell * PotFloatSheetBaker.Shapes.Length, w);
            Assert.IsTrue(png.All(p => p.a == 0 || p.a == 255), "pixel art: every pixel is fully on or off");
            for (int s = 0; s < PotFloatSheetBaker.Shapes.Length; s++)
            {
                int ink = 0;
                for (int y = 0; y < h; y++)
                    for (int x = s * cell; x < (s + 1) * cell; x++)
                        if (png[y * w + x].a != 0) ink++;
                Assert.Greater(ink, 30, $"{PotFloatSheetBaker.Shapes[s]} cell is nearly empty");
            }

            // the importer cuts the same three cells, pivot on the still waterline
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(SpriteSheet).OfType<Sprite>().ToArray();
            double cy = _host.EvaluateNumber("PotFloat.cy"), H = _host.EvaluateNumber("PotFloat.H");
            foreach (string shape in PotFloatSheetBaker.Shapes)
            {
                Sprite sp = sprites.FirstOrDefault(x => x.name == "PotFloats_" + shape);
                Assert.IsNotNull(sp, $"PotFloats_{shape} sprite");
                Assert.AreEqual(cell, sp.rect.width, 0.01f);
                Assert.AreEqual(32f, sp.pixelsPerUnit, 0.01f, "PPU 32 = true scale");
                Assert.AreEqual(H - cy, sp.pivot.y, 0.51, $"{shape}: pivot sits on the rig's waterline row");
            }
        }

        [Test]
        public void TheCSharpPaintRampIsTheRigsToTheByte()
        {
            string[] coats = { "#e0b13a", "#9c4a3c", "#2a3b52", "#f23d1e", "#ffffff", "#000000", "#3fae5c", "#f2cc26", "#7a1f8f" };
            var cs = new Color32[PotFloatPaint.RampSteps];
            foreach (string coat in coats)
            {
                PotFloatPaint.PaintRamp(Hex(coat), cs);
                Color32[] js = RigRamp($"PotFloat.paintRamp('{coat}')");
                Assert.AreEqual(PotFloatPaint.RampSteps, js.Length);
                for (int i = 0; i < js.Length; i++)
                    Assert.AreEqual(ToHex(js[i]), ToHex(cs[i]), $"{coat} step {i}");
            }
            Assert.AreEqual(ToHex(PotFloatPaint.BakedBody), _host.EvaluateString("PotFloat.BAKED.body"));
            Assert.AreEqual(ToHex(PotFloatPaint.BakedBand), _host.EvaluateString("PotFloat.BAKED.band"));
        }

        [Test]
        public void EverySheetColourIsARampEntryAndNoFixedColourSitsNearAPaintColour()
        {
            var paint = new List<Color32>();
            var cs = new Color32[PotFloatPaint.RampSteps];
            PotFloatPaint.PaintRamp(PotFloatPaint.BakedBody, cs); paint.AddRange(cs);
            PotFloatPaint.PaintRamp(PotFloatPaint.BakedBand, cs); paint.AddRange(cs);

            var known = new HashSet<string>(paint.Select(ToHex));
            foreach (string k in new[] { "STICK", "ROPE", "GROW", "DARK", "SHADE" })
                foreach (Color32 c in RigRamp($"PotFloat.FIXED.{k}")) known.Add(ToHex(c));

            Color32[] png = CommittedSheet(out _, out _);
            var used = new HashSet<string>(png.Where(p => p.a != 0).Select(ToHex));
            var stray = used.Where(u => !known.Contains(u)).ToArray();
            CollectionAssert.IsEmpty(stray, "off-palette pixels in PotFloats.png");

            var paintHex = new HashSet<string>(paint.Select(ToHex));
            foreach (string u in used.Where(u => !paintHex.Contains(u)))
            {
                Vector4 lu = PotFloatPaint.Linear(Hex(u));
                foreach (Color32 p in paint)
                {
                    Vector4 lp = PotFloatPaint.Linear(p);
                    float d = (lu.x - lp.x) * (lu.x - lp.x) + (lu.y - lp.y) * (lu.y - lp.y) + (lu.z - lp.z) * (lu.z - lp.z);
                    Assert.Greater(d, MatchEpsSq * 4f, $"{u} is close enough to paint {ToHex(p)} to be recoloured with it");
                }
            }
            Assert.IsTrue(paint.Any(p => used.Contains(ToHex(p))), "the sheet carries the paint coats");
        }

        [Test]
        public void SwapsAreBuiltPerChangedCoatAndUnusedSlotsNeverMatch()
        {
            var from = new Vector4[12];
            var to = new Vector4[12];
            Assert.AreEqual(0, PotFloatPaint.BuildSwaps(PotFloatPaint.BakedBody, PotFloatPaint.BakedBand, from, to), "baked colours: nothing to swap");
            Assert.AreEqual(5, PotFloatPaint.BuildSwaps(Color.red, PotFloatPaint.BakedBand, from, to), "body only");
            Assert.AreEqual(10, PotFloatPaint.BuildSwaps(Color.red, Color.blue, from, to), "both coats");
            for (int i = 10; i < from.Length; i++) Assert.AreEqual(PotFloatPaint.NoColour, from[i]);

            var ramp = new Color32[PotFloatPaint.RampSteps];
            PotFloatPaint.PaintRamp(PotFloatPaint.BakedBody, ramp);
            Assert.AreEqual(PotFloatPaint.Linear(ramp[2]), from[2], "slot 2 = the body's base step, linear");
        }

        [Test]
        public void TheWaterlineRadiusInDataIsTheRigsOwn()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            var lobster = AssetDatabase.LoadAssetAtPath<TrapDef>(LobsterPath);
            var crab = AssetDatabase.LoadAssetAtPath<TrapDef>(CrabPath);
            var fleet = AssetDatabase.LoadAssetAtPath<AmbientFleetDef>(FleetPath);
            Assert.IsNotNull(cfg); Assert.IsNotNull(lobster); Assert.IsNotNull(crab); Assert.IsNotNull(fleet);

            Assert.AreEqual(Meta("bullet"), cfg.LobsterFloatRadiusMeters, 1e-4, "GameConfig: the player's float");
            Assert.AreEqual(Meta("bullet"), lobster.BuoyFloatRadiusMeters, 1e-4, "lobster pot float");
            Assert.AreEqual(Meta("ball"), crab.BuoyFloatRadiusMeters, 1e-4, "crab pot float");
            Assert.AreEqual(Meta("spar"), fleet.BuoyFloatRadiusMeters, 1e-4, "the fleet's spar markers");

            Assert.AreEqual("PotFloats_bullet", lobster.BuoySprite != null ? lobster.BuoySprite.name : null);
            Assert.AreEqual("PotFloats_ball", crab.BuoySprite != null ? crab.BuoySprite.name : null);
            Assert.AreEqual("PotFloats_spar", fleet.BuoySprite != null ? fleet.BuoySprite.name : null);
        }

        static double MetaOf(string shape, string key) =>
            _host.EvaluateNumber($"PotFloat.META['{shape}'].{key}");

        [Test]
        public void TheRideBoundsInDataAreTheRigsOwn()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            var lobster = AssetDatabase.LoadAssetAtPath<TrapDef>(LobsterPath);
            var crab = AssetDatabase.LoadAssetAtPath<TrapDef>(CrabPath);
            var fleet = AssetDatabase.LoadAssetAtPath<AmbientFleetDef>(FleetPath);

            Assert.AreEqual(MetaOf("bullet", "draft"), cfg.LobsterFloatDraftMeters, 1e-4, "GameConfig draft");
            Assert.AreEqual(MetaOf("bullet", "freeboard"), cfg.LobsterFloatFreeboardMeters, 1e-4, "GameConfig freeboard");
            Assert.AreEqual(MetaOf("bullet", "draft"), lobster.BuoyDraftMeters, 1e-4, "lobster draft");
            Assert.AreEqual(MetaOf("bullet", "freeboard"), lobster.BuoyFreeboardMeters, 1e-4, "lobster freeboard");
            Assert.AreEqual(MetaOf("ball", "draft"), crab.BuoyDraftMeters, 1e-4, "crab draft");
            Assert.AreEqual(MetaOf("ball", "freeboard"), crab.BuoyFreeboardMeters, 1e-4, "crab freeboard");
            Assert.AreEqual(MetaOf("spar", "draft"), fleet.BuoyDraftMeters, 1e-4, "fleet draft");
            Assert.AreEqual(MetaOf("spar", "freeboard"), fleet.BuoyFreeboardMeters, 1e-4, "fleet freeboard");
            Assert.AreEqual(2f, cfg.PotFloatDrawScale, 1e-6, "the owner's pick (2026-11-25): pot floats are drawn at 2x");
        }

        [Test]
        public void NoFishersFloatIsMistakenForThePlayersGold()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Vector4 gold = PotFloatPaint.Linear((Color32)cfg.PlayerPotFloatColour);
            string[] guids = AssetDatabase.FindAssets("t:NpcPotDayDef");
            Assert.IsNotEmpty(guids, "no pot-day defs found");
            foreach (string g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                var so = new SerializedObject(AssetDatabase.LoadMainAssetAtPath(path));
                SerializedProperty p = so.FindProperty("BuoyColour");
                if (p == null) continue;
                Vector4 c = PotFloatPaint.Linear((Color32)p.colorValue);
                float d = Mathf.Sqrt((c.x - gold.x) * (c.x - gold.x) + (c.y - gold.y) * (c.y - gold.y) + (c.z - gold.z) * (c.z - gold.z));
                Assert.Greater(d, 0.25f, $"{path}: its float colour reads as the player's gold");
            }
        }
    }
}
