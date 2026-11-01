using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>One fixed, weathered pier from the existing pass-two rig. No shared wharf asset is rewritten.</summary>
    public static class StPetersLandingDeckBaker
    {
        public const string Folder = "Assets/_Project/Art/Sprites/StPeters/SetPieces";
        public const string RecipePath = Folder + "/stpLandingDeck.recipe.json";
        public const string SheetPath = Folder + "/stpLandingDeck.png";
        public const string HeightPath = Folder + "/stpLandingDeck_height.png";
        public const string ContractPath = Folder + "/stpLandingDeck.contract.json";
        public const string SpriteName = "stpLandingDeck";
        public const string RigFolder = "docs/art/rigs/wharf-rig-kit-v2/";
        public static readonly string[] Scripts =
        {
            "wharfRig2.geo.js", "wharfRig2.fam.js", "wharfRig2.verbs.js", "wharfRig2.js", "wharfRig2.kit.js",
        };

        [Serializable]
        public sealed class Recipe
        {
            public string schema, source, sourceSha256, family, season, stage;
            public float originX, originY, x0, length, bayLen, width, deckElevation, datumShift;
            public float tideRange, tide, wind, gust, time, cloud;
            public int bays, dir, frame, windDir, importSizeCap;
            public bool fittings, fixtures;
        }

        [Serializable] public sealed class BedPoint { public float x, y, z; }
        [Serializable] public sealed class BedTable { public BedPoint[] points; }
        [Serializable] public sealed class ScriptHash { public string path, sha256; }

        [Serializable]
        public sealed class Contract
        {
            public string schema = "hidden-harbours/stp-landing-deck@1";
            public string recipeSha256, albedoSha256, heightSha256;
            public string tideFinding = "Under-pier tide occlusion is absent; the rig height channel is retained for the later consumer.";
            public int width, height, importSizeCap;
            public float pivotX, pivotY, pixelsPerUnit, originX, originY;
            public float heightMinRigZ, heightRange, datumShift;
            public ScriptHash[] scripts;
            public BedPoint[] bed;
        }

        public sealed class Pixels
        {
            public int Width, Height;
            public float PivotX, PivotY, PixelsPerUnit, HeightMinRigZ, HeightRange;
            public byte[] Albedo, HeightMap;
        }

        static string Abs(string path) => Path.Combine(RigCatalog.RepoRoot, path);
        public static Recipe ReadRecipe() => JsonUtility.FromJson<Recipe>(File.ReadAllText(Abs(RecipePath)));
        public static Contract ReadContract() => JsonUtility.FromJson<Contract>(File.ReadAllText(Abs(ContractPath)));

        public static string Sha(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        static string N(float n) => n.ToString("R", CultureInfo.InvariantCulture);

        // These are the board's context options. Bed samples are measured from the game's terrain;
        // both script hosts receive the same immutable table, so the Node oracle can reproduce the bake.
        public static string DefineScript(Recipe r, string bedFunction) =>
            "WharfRig2Kit.define('stpLandingDeck',{label:'Landing context',tideRange:" + N(r.tideRange) +
            ",bed:" + bedFunction + ",road:()=>false,mods:[{id:'pier',key:'" + r.family + "',at:[" +
            N(r.x0 + r.length / 2 - r.originX) + "," + N(r.originY) + "],rot:0,o:{bays:" + r.bays +
            ",bayLen:" + N(r.bayLen) + ",width:" + N(r.width) + ",deckZ:" + N(r.deckElevation + r.datumShift) +
            ",berths:[],fixtures:[],fittings:{ladders:false,tyres:false,cleats:false,bollards:false,rings:false,foam:false,panels:false},stage:'" +
            r.stage + "'}}]});";

        public static string FrameScript(Recipe r) =>
            "globalThis.__stpDeckFrame=WharfRig2.frame('harbour:stpLandingDeck',{dir:" + r.dir +
            ",tide:" + N(r.tide + r.datumShift) + ",frame:" + r.frame + ",wind:{w:" + N(r.wind) +
            ",gust:" + N(r.gust) + ",dir:" + r.windDir + "},season:'" + r.season + "',stage:'" + r.stage + "'});";

        public static void CheckRecipe(Recipe r)
        {
            if (r == null || r.schema != "hidden-harbours/stp-landing-deck-recipe@1" || r.family != "pier" ||
                r.fittings || r.fixtures || r.importSizeCap > StPetersSetPieceKit.ImportSizeCap ||
                r.importSizeCap < 1 || r.bays < 1 || r.length <= 0 || r.width <= 0 ||
                Math.Abs(r.bays * r.bayLen - r.length) > 0.001f)
                throw new InvalidOperationException("Landing recipe is not a fixed pier without fittings inside the set-piece import cap.");
        }

        public static void Install(IRigScriptHost host)
        {
            RigCatalog.InstallModule(host, RigCatalog.Get("wharfRig2Kit"));
            host.Execute(File.ReadAllText(Abs(StPetersSetPieceKit.KitFolder + "/lib/weatherSky.js")));
        }

        static BedPoint[] MeasureBed(IRigScriptHost host, Recipe recipe, ITidalTerrain terrain)
        {
            var distinct = new List<BedPoint>();
            var seen = new HashSet<(float, float)>();
            // Bed-dependent berth/structure checks may ask new points after the first model.
            // Discovery frames are discarded; only a frame with a complete measured table is baked.
            for (int pass = 0; pass < 8; pass++)
            {
                host.Execute("globalThis.__stpBedQueries=[];globalThis.__stpBed=" +
                    JsonUtility.ToJson(new BedTable { points = distinct.ToArray() }) + ";");
                host.Execute(DefineScript(recipe,
                    "(x,y)=>{__stpBedQueries.push({x:x,y:y});let best=null,d=Infinity;" +
                    "for(const p of __stpBed.points){const q=(x-p.x)**2+(y-p.y)**2;if(q<d){best=p;d=q;}}return best?best.z:0;}"));
                host.Execute(FrameScript(recipe));
                var table = JsonUtility.FromJson<BedTable>("{\"points\":" + host.EvaluateString("JSON.stringify(__stpBedQueries)") + "}");
                bool added = false;
                foreach (BedPoint point in table.points)
                {
                    if (!seen.Add((point.x, point.y))) continue;
                    point.z = terrain.ElevationAt(new Vector2(point.x + recipe.originX, point.y)) + recipe.datumShift;
                    distinct.Add(point); added = true;
                }
                if (!added) return distinct.ToArray();
            }
            throw new InvalidOperationException("Landing terrain queries did not stabilize within eight discovery frames.");
        }

        public static Pixels Render(IRigScriptHost host, Recipe recipe, BedPoint[] bed)
        {
            CheckRecipe(recipe);
            host.Execute("globalThis.__stpBed=" + JsonUtility.ToJson(new BedTable { points = bed }) + ";");
            host.Execute(DefineScript(recipe,
                "(x,y)=>{let best=null,d=Infinity;for(const p of __stpBed.points){const q=(x-p.x)**2+(y-p.y)**2;if(q<d){best=p;d=q;}}" +
                "if(!best||d>1e-8)throw Error('Landing bed query is not in the measured input table: '+x+','+y);return best.z;}"));
            host.Execute(FrameScript(recipe));
            host.Execute("globalThis.__stpDeckSky=WeatherSky.at({time:" + N(recipe.time) + ",cloud:" + N(recipe.cloud) + ",wind:" + N(recipe.wind) + "});");
            return new Pixels
            {
                Width = (int)host.EvaluateNumber("__stpDeckFrame.w"),
                Height = (int)host.EvaluateNumber("__stpDeckFrame.h"),
                PivotX = (float)host.EvaluateNumber("__stpDeckFrame.px"),
                PivotY = (float)host.EvaluateNumber("__stpDeckFrame.py"),
                PixelsPerUnit = (float)host.EvaluateNumber("__stpDeckFrame.ppu"),
                HeightMinRigZ = -3,
                HeightRange = recipe.tideRange + 3.5f,
                Albedo = host.EvaluateBytes("WharfRig2.relight(__stpDeckFrame,__stpDeckSky)"),
                HeightMap = host.EvaluateBytes("WharfRig2.view(__stpDeckFrame,'height')"),
            };
        }

        public static Contract Bake(ITidalTerrain terrain)
        {
            var recipe = ReadRecipe();
            CheckRecipe(recipe);
            using IRigScriptHost host = RigScriptHostFactory.Create();
            Install(host);
            BedPoint[] bed = MeasureBed(host, recipe, terrain);
            Pixels pixels = Render(host, recipe, bed);
            if (pixels.Width <= 0 || pixels.Height <= 0 || pixels.Width > recipe.importSizeCap || pixels.Height > recipe.importSizeCap ||
                pixels.Albedo.Length != pixels.Width * pixels.Height * 4 || pixels.HeightMap.Length != pixels.Albedo.Length)
                throw new InvalidOperationException("Landing pier frame exceeds its import cap or returned an incomplete channel.");
            WritePng(SheetPath, pixels.Albedo, pixels.Width, pixels.Height);
            WritePng(HeightPath, pixels.HeightMap, pixels.Width, pixels.Height);
            var hashes = new List<ScriptHash>();
            foreach (string script in Scripts)
                hashes.Add(new ScriptHash { path = RigFolder + script, sha256 = Sha(File.ReadAllBytes(Abs(RigFolder + script))) });
            string weather = StPetersSetPieceKit.KitFolder + "/lib/weatherSky.js";
            hashes.Add(new ScriptHash { path = weather, sha256 = Sha(File.ReadAllBytes(Abs(weather))) });
            var contract = new Contract
            {
                recipeSha256 = Sha(File.ReadAllBytes(Abs(RecipePath))), width = pixels.Width, height = pixels.Height,
                pivotX = pixels.PivotX, pivotY = pixels.PivotY, pixelsPerUnit = pixels.PixelsPerUnit,
                originX = recipe.originX,
                originY = recipe.originY - recipe.datumShift * (float)host.EvaluateNumber("WharfRig2.CE"),
                heightMinRigZ = pixels.HeightMinRigZ, heightRange = pixels.HeightRange, datumShift = recipe.datumShift,
                importSizeCap = recipe.importSizeCap, bed = bed, scripts = hashes.ToArray(),
                albedoSha256 = Sha(pixels.Albedo), heightSha256 = Sha(pixels.HeightMap),
            };
            File.WriteAllText(Abs(ContractPath), JsonUtility.ToJson(contract, true) + "\n", new UTF8Encoding(false));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Import(contract);
            return contract;
        }

        static void WritePng(string path, byte[] topDown, int width, int height)
        {
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int at = (y * width + x) * 4;
                pixels[(height - 1 - y) * width + x] = new Color32(topDown[at], topDown[at + 1], topDown[at + 2], topDown[at + 3]);
            }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            try
            {
                texture.SetPixels32(pixels); texture.Apply(false, false);
                File.WriteAllBytes(Abs(path), texture.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        public static void Import(Contract contract)
        {
            foreach (string path in new[] { SheetPath, HeightPath })
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("No landing importer: " + path);
                bool height = path == HeightPath;
                importer.textureType = height ? TextureImporterType.Default : TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = contract.pixelsPerUnit;
                importer.spritePivot = new Vector2(contract.pivotX / contract.width, 1 - contract.pivotY / contract.height);
                importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = false; importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.sRGBTexture = !height; importer.alphaIsTransparency = !height;
                importer.isReadable = true; importer.maxTextureSize = contract.importSizeCap;
                var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
                var platform = importer.GetDefaultPlatformTextureSettings();
                platform.format = height ? TextureImporterFormat.R8 : TextureImporterFormat.RGBA32;
                platform.maxTextureSize = contract.importSizeCap; importer.SetPlatformTextureSettings(platform);
                importer.SaveAndReimport();
            }
        }
    }
}
