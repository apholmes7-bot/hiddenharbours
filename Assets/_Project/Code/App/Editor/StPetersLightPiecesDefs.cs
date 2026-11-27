#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// The light station's yard pieces as data: one <see cref="PropPieceDef"/> per picture set the recipe drew
    /// (<c>tools/rig-recipes/stp-light-pieces.mjs</c>, its contract <see cref="ContractPath"/>), and the
    /// <see cref="PropPieceKitDef"/> St Peters' key scenes place them through. Re-running it re-reads the contract
    /// and rewrites the same assets in place (same paths, same ids); the kit's dressing is set here once and is the
    /// owner's to tune after.
    /// </summary>
    public static class StPetersLightPiecesDefs
    {
        public const string ArtFolder = "Assets/_Project/Art/Sprites/StPeters/LightPieces";
        public const string ContractPath = ArtFolder + "/LightPieces.json";
        public const string DataFolder = "Assets/_Project/Data/Regions/StPetersLightPieces";
        public const string KitPath = DataFolder + "/PropPieceKit_stp_light_pieces.asset";
        public const string KitId = "proppiecekit.stp_light_pieces";

        /// <summary>The recipe's piece name, its Def's id and what a player calls it.</summary>
        public static readonly (string Name, string Id, string DisplayName)[] Pieces =
        {
            ("PicketPanel_fresh", "proppiece.picket_panel", "Picket fence"),
            ("PicketPanel_weathered", "proppiece.picket_panel_weathered", "Weathered picket fence"),
            ("PicketPanel_broken", "proppiece.picket_panel_broken", "Broken picket fence"),
            ("PicketGate", "proppiece.picket_gate", "Picket gate"),
            ("PicketCorner", "proppiece.picket_corner", "Picket corner post"),
            ("PicketEnd", "proppiece.picket_end", "Picket end post"),
            ("Plank", "proppiece.plank", "Plank walk"),
            ("PlankBend", "proppiece.plank_bend", "Plank walk bend"),
            ("PlankEnd", "proppiece.plank_end", "Plank walk end"),
            ("FogBell", "proppiece.fog_bell", "Fog bell"),
            ("Clothesline", "proppiece.clothesline", "Clothesline"),
            ("GardenBed", "proppiece.garden_bed", "Garden bed"),
        };

        [Serializable] sealed class Contract { public string schema; public Piece[] pieces; }
        [Serializable] sealed class Half { public float x, y; }
        [Serializable]
        sealed class Piece
        {
            public string name, kind, layer, sprite;
            public string[] sprites;
            public float directionStepDegrees, planLengthMetres, heightMetres;
            public Half footprintHalfMetres, postHalfMetres;
            public float[] postsAtMetres;
        }

        public static string DefPath(string name) => $"{DataFolder}/PropPiece_{name}.asset";

        [MenuItem("Hidden Harbours/World/St Peters Light Pieces/Build the Defs from the Contract")]
        static void BuildMenu() => Build();

        /// <summary>For <c>-executeMethod</c>.</summary>
        public static void BuildBatch() => Build();

        public static void Build()
        {
            var contract = JsonUtility.FromJson<Contract>(File.ReadAllText(ContractPath));
            if (contract?.schema != "stp-light-pieces@1" || contract.pieces == null)
                throw new InvalidOperationException(ContractPath + " is not an stp-light-pieces@1 contract.");
            if (!AssetDatabase.IsValidFolder(DataFolder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(DataFolder)!.Replace('\\', '/'), Path.GetFileName(DataFolder));

            var defs = new Dictionary<string, PropPieceDef>(StringComparer.Ordinal);
            foreach ((string name, string id, string display) in Pieces)
            {
                Piece p = contract.pieces.SingleOrDefault(x => x.name == name)
                          ?? throw new InvalidOperationException($"{ContractPath} has no piece '{name}'.");
                string path = DefPath(name);
                var def = AssetDatabase.LoadAssetAtPath<PropPieceDef>(path);
                bool created = def == null;
                if (created) def = ScriptableObject.CreateInstance<PropPieceDef>();
                def.Id = id;
                def.DisplayName = display;
                def.Layer = p.layer == PropPieceDef.FloorLayer ? PropPieceDef.FloorLayer : PropPieceDef.RaisedLayer;
                string[] files = p.kind == "run" ? p.sprites : new[] { p.sprite };
                def.Sprites = files.Select(f => LoadSprite(ArtFolder + "/" + f)).ToArray();
                def.DirectionStepDegrees = p.kind == "run" ? p.directionStepDegrees : 0f;
                def.PlanLengthMetres = p.planLengthMetres;
                def.HeightMetres = p.heightMetres;
                // A fence bay blocks a walker along its run; a gate stands open; planks are walked on.
                def.RunColliderThicknessMetres = name.StartsWith("PicketPanel", StringComparison.Ordinal)
                    ? PropertyBoundary.DefaultThicknessMetres : 0f;
                def.Colliders = CollidersOf(p);
                // The bell stands tall in its frame and casts the sun's shadow like the yard's other standing props.
                def.CastsShadow = name == "FogBell";
                if (created) AssetDatabase.CreateAsset(def, path);
                else EditorUtility.SetDirty(def);
                defs[name] = def;
            }

            var kit = AssetDatabase.LoadAssetAtPath<PropPieceKitDef>(KitPath);
            bool newKit = kit == null;
            if (newKit)
            {
                kit = ScriptableObject.CreateInstance<PropPieceKitDef>();
                kit.Id = KitId;
                kit.Kit = StPetersLayerRefresh.PropPiecesKit;
                kit.Pieces = new[]
                {
                    new PropPieceKitEntry { Piece = "fogBell", Main = defs["FogBell"] },
                    new PropPieceKitEntry { Piece = "clothesline", Main = defs["Clothesline"] },
                    new PropPieceKitEntry
                    {
                        Piece = "picket", Main = defs["PicketPanel_fresh"], Corner = defs["PicketCorner"], End = defs["PicketEnd"],
                        Gate = defs["PicketGate"], CornerTurnDegrees = 20f, GateGapMaxMetres = 1.6f, GateSwingDegrees = 70f,
                    },
                    new PropPieceKitEntry
                    {
                        Piece = "plank", Main = defs["Plank"], End = defs["PlankEnd"], Bend = defs["PlankBend"], CornerTurnDegrees = 12f,
                    },
                };
                // The brow fence has stood in the wind off the cliff: weathered, with a bay down every so often.
                kit.Dressings = new[]
                {
                    new PropPieceDressing
                    {
                        PieceId = "structure.stp_ne_brow_fence", Main = defs["PicketPanel_weathered"],
                        Alternate = defs["PicketPanel_broken"], AlternateEvery = 7,
                    },
                };
                AssetDatabase.CreateAsset(kit, KitPath);
            }
            if (AddYardDressing(kit, defs)) EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();
            Debug.Log($"[StPetersLightPiecesDefs] {defs.Count} piece Defs {(newKit ? "and the kit " : "")}written to {DataFolder}.");
        }

        public const string GardenFenceId = "structure.stp_ne_garden_fence";
        public const string ClotheslineId = "prop.stp_ne_clothesline";

        /// <summary>
        /// The yard's second-pass dressing, added once to a kit that lacks it and the owner's to tune after: the bed
        /// key; the keeper's garden with two dug beds inside its pickets and a plank walk from the yard's walk through
        /// its gate (the package fences an empty lawn; its two "beds" south of the fence are woodpiles); and the
        /// clothesline set back from the north brow into the yard. The garden's points are world metres from the fence
        /// row's point (187.2, 58); the clothesline moves from the package's (185, 59.6) to (185.6, 55.6). The package's
        /// next issue should carry these; until it does they live here, in the kit, not in the imported rows (a
        /// re-import retires an id the package does not hold).
        /// </summary>
        static bool AddYardDressing(PropPieceKitDef kit, IReadOnlyDictionary<string, PropPieceDef> defs)
        {
            bool changed = false;
            if (kit.EntryFor("gardenBed") == null)
            {
                kit.Pieces = kit.Pieces.Append(new PropPieceKitEntry { Piece = "gardenBed", Main = defs["GardenBed"] }).ToArray();
                changed = true;
            }
            if (kit.DressingFor(GardenFenceId) == null)
            {
                kit.Dressings = kit.Dressings.Append(new PropPieceDressing
                {
                    PieceId = GardenFenceId,
                    Extras = new[]
                    {
                        new PropPieceExtra { Piece = "gardenBed", Path = new[] { new Vector2(1.05f, 1.8f) } },
                        new PropPieceExtra { Piece = "gardenBed", Path = new[] { new Vector2(3.65f, 1.8f) } },
                        new PropPieceExtra { Piece = "plank", Path = new[] { new Vector2(2.55f, -6.9f), new Vector2(2.55f, 3.0f) } },
                    },
                }).ToArray();
                changed = true;
            }
            if (kit.DressingFor(ClotheslineId) == null)
            {
                kit.Dressings = kit.Dressings.Append(new PropPieceDressing { PieceId = ClotheslineId, Offset = new Vector2(0.6f, -4f) }).ToArray();
                changed = true;
            }
            return changed;
        }

        // =====================================================================================
        //  the floor runs, baked
        // =====================================================================================

        public const string BakeFolder = ArtFolder + "/Runs";

        [MenuItem("Hidden Harbours/World/St Peters Light Pieces/Bake the Floor Runs")]
        static void BakeMenu() => BakeFloorRuns();

        /// <summary>For <c>-executeMethod</c>.</summary>
        public static void BakeFloorRunsBatch() => BakeFloorRuns();

        /// <summary>
        /// Every floor run of the yard kit that St Peters' key scenes place, drawn into a few long pictures
        /// (<see cref="StPetersLayerRefresh.FloorStrips"/>): each plank's own pixels, laid on the 32 px/m grid at its
        /// place to the nearest pixel, never scaled, turned or filtered. The pictures are written beside the pieces'
        /// art and imported with the art lock's settings; the kit records each with the hash of the planks it holds.
        /// Re-run after any change to a walk's line or its pictures (the placer refuses a stale bake).
        /// </summary>
        public static void BakeFloorRuns()
        {
            var kit = AssetDatabase.LoadAssetAtPath<PropPieceKitDef>(KitPath) ?? throw new InvalidOperationException(KitPath + " is missing.");
            kit.BakedRuns = Array.Empty<PropPieceBakedRun>();               // place unbaked, then bake what that lays
            List<StPetersLayerRefresh.KeyScenePlacement> placed =
                StPetersLayerRefresh.PlaceKeyScenes(StPetersLayerRefresh.LoadStPetersKeyScenes(), new StPetersLayerRefresh.EditorKeySceneAssets());
            if (!AssetDatabase.IsValidFolder(BakeFolder)) AssetDatabase.CreateFolder(ArtFolder, "Runs");
            var baked = new List<PropPieceBakedRun>();
            var written = new HashSet<string>(StringComparer.Ordinal);
            var cache = new Dictionary<string, Color32[]>();
            var sizes = new Dictionary<string, Vector2Int>();
            foreach (StPetersLayerRefresh.KeyScenePlacement p in placed.Where(x => x.Kit == StPetersLayerRefresh.PropPiecesKit && x.Parts != null))
            {
                List<List<StPetersLayerRefresh.KeyScenePart>> strips = StPetersLayerRefresh.FloorStrips(p.Parts, kit.BakeStripMaxMetres);
                for (int i = 0; i < strips.Count; i++)
                {
                    string path = $"{BakeFolder}/{p.Id.Replace('.', '_')}_{i:00}.png";
                    Vector2 centre = Composite(strips[i], path, cache, sizes);
                    written.Add(path);
                    baked.Add(new PropPieceBakedRun { Row = p.Id, Index = i, At = centre, Signature = StPetersLayerRefresh.StripSignature(strips[i]) });
                }
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (PropPieceBakedRun b in baked) b.Sprite = LoadSprite($"{BakeFolder}/{b.Row.Replace('.', '_')}_{b.Index:00}.png");
            foreach (string stale in Directory.GetFiles(BakeFolder, "*.png").Select(f => f.Replace('\\', '/')).Where(f => !written.Contains(f)))
                AssetDatabase.DeleteAsset(stale);
            kit.BakedRuns = baked.ToArray();
            EditorUtility.SetDirty(kit);
            AssetDatabase.SaveAssets();
            Debug.Log($"[StPetersLightPiecesDefs] baked {baked.Count} floor strip(s) from " +
                      $"{placed.Where(x => x.Parts != null).Sum(x => x.Parts.Count(q => q.Floor))} plank picture(s) into {BakeFolder}.");
        }

        /// <summary>Lays a strip's planks into one picture, the later over the earlier, and returns where its centre
        /// stands (world metres). The picture is padded to even sides so its centre pivot falls on a pixel corner.</summary>
        static Vector2 Composite(List<StPetersLayerRefresh.KeyScenePart> strip, string path,
                                 Dictionary<string, Color32[]> cache, Dictionary<string, Vector2Int> sizes)
        {
            const int ppm = StPetersLayerRefresh.BakePixelsPerMetre;
            var placed = new List<(Color32[] px, Vector2Int size, int left, int top, bool fx, bool fy)>();
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (StPetersLayerRefresh.KeyScenePart part in strip)
            {
                string src = AssetDatabase.GetAssetPath(part.Source);
                if (string.IsNullOrEmpty(src)) throw new InvalidOperationException($"{part.Name} has no picture file.");
                if (!cache.TryGetValue(src, out Color32[] px))
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!tex.LoadImage(File.ReadAllBytes(src))) throw new InvalidOperationException(src + " does not read as a picture.");
                    cache[src] = px = tex.GetPixels32();
                    sizes[src] = new Vector2Int(tex.width, tex.height);
                    UnityEngine.Object.DestroyImmediate(tex);
                }
                Vector2Int size = sizes[src];
                // The picture is centred on its ground point; its top-left pixel on the world grid (y up):
                int left = Mathf.RoundToInt(part.At.x * ppm - size.x / 2f);
                int top = Mathf.RoundToInt(part.At.y * ppm + size.y / 2f);
                placed.Add((px, size, left, top, part.FlipX, part.FlipY));
                minX = Math.Min(minX, left); maxX = Math.Max(maxX, left + size.x);
                minY = Math.Min(minY, top - size.y); maxY = Math.Max(maxY, top);
            }
            if ((maxX - minX) % 2 != 0) maxX++;
            if ((maxY - minY) % 2 != 0) maxY++;
            int w = maxX - minX, h = maxY - minY;
            var outPx = new Color32[w * h];
            foreach (var (px, size, left, top, fx, fy) in placed)
                for (int y = 0; y < size.y; y++)            // texture rows run bottom-up
                for (int x = 0; x < size.x; x++)
                {
                    Color32 c = px[(fy ? size.y - 1 - y : y) * size.x + (fx ? size.x - 1 - x : x)];
                    if (c.a == 0) continue;
                    int ox = left - minX + x, oy = (top - size.y) - minY + y;
                    Color32 under = outPx[oy * w + ox];
                    outPx[oy * w + ox] = c.a == 255 || under.a == 0 ? c : Color32.Lerp(under, new Color32(c.r, c.g, c.b, 255), c.a / 255f);
                }
            var outTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            outTex.SetPixels32(outPx);
            File.WriteAllBytes(path, outTex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(outTex);
            return new Vector2((minX + maxX) / 2f / ppm, (minY + maxY) / 2f / ppm);
        }

        /// <summary>
        /// The picture as ONE sprite pivoted on its centre. The recipe pads every picture so its ground point is the
        /// exact centre; the editor's default for a new texture is Multiple with the sprite trimmed to the opaque
        /// pixels (and a broken bay split into islands), which moves that pivot. So the import is set here: Single,
        /// centred, and the art lock's other settings (<see cref="HiddenHarbours.Art.Editor.ArtImportPipeline"/>).
        /// </summary>
        internal static Sprite LoadSprite(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter
                      ?? throw new InvalidOperationException($"{path} is not an imported picture (import it first).");
            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            if (imp.textureType != TextureImporterType.Sprite || imp.spriteImportMode != SpriteImportMode.Single ||
                s.spriteAlignment != (int)SpriteAlignment.Center || !Mathf.Approximately(imp.spritePixelsPerUnit, HiddenHarbours.Art.Editor.ArtImportPipeline.PixelsPerUnit))
            {
                HiddenHarbours.Art.Editor.ArtImportPipeline.ApplyLockedSettings(imp, path);
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.ReadTextureSettings(s);
                s.spriteAlignment = (int)SpriteAlignment.Center;
                imp.SetTextureSettings(s);
                imp.SaveAndReimport();
            }
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            if (sprites.Length != 1) throw new InvalidOperationException($"{path} imports as {sprites.Length} sprites, not one.");
            return sprites[0];
        }

        static PropPieceShape[] CollidersOf(Piece p)
        {
            if (p.postsAtMetres != null && p.postsAtMetres.Length > 0 && p.postHalfMetres != null)
                return p.postsAtMetres.Select(x => Quad(new Vector2(x, 0f), p.postHalfMetres)).ToArray();
            if (p.footprintHalfMetres != null && p.footprintHalfMetres.x > 0f)
                return new[] { Quad(Vector2.zero, p.footprintHalfMetres) };
            return Array.Empty<PropPieceShape>();
        }

        static PropPieceShape Quad(Vector2 c, Half h) => new PropPieceShape
        {
            Points = new[]
            {
                new Vector2(c.x - h.x, c.y - h.y), new Vector2(c.x + h.x, c.y - h.y),
                new Vector2(c.x + h.x, c.y + h.y), new Vector2(c.x - h.x, c.y + h.y),
            },
        };
    }
}
#endif
