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
            AssetDatabase.SaveAssets();
            Debug.Log($"[StPetersLightPiecesDefs] {defs.Count} piece Defs {(newKit ? "and the kit " : "")}written to {DataFolder}.");
        }

        /// <summary>
        /// The picture as ONE sprite pivoted on its centre. The recipe pads every picture so its ground point is the
        /// exact centre; the editor's default for a new texture is Multiple with the sprite trimmed to the opaque
        /// pixels (and a broken bay split into islands), which moves that pivot. So the import is set here: Single,
        /// centred, and the art lock's other settings (<see cref="HiddenHarbours.Art.Editor.ArtImportPipeline"/>).
        /// </summary>
        static Sprite LoadSprite(string path)
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
