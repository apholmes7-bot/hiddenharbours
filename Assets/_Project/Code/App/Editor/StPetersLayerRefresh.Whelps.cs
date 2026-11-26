#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenHarbours.App.Editor
{
    // The Whelps: the skerry rocks off the South Arm, each a row of the lookout's key scene, drawn from the shaded
    // rock kit and covered by the tide. One root in StPeters.unity, written as plain documents.
    //
    // A rock is two pictures on one spot. The under one is the kit's wetter state and the sea cuts it where the
    // water stands; the upper one is the state its row asks for and is cut a band higher, so the strip between is
    // the rock just left by the water. Both are cut by the height each pixel stands above the rock's own lower
    // edge (tools/rig-recipes/whelp-rock-heights.mjs), scaled to the height the kit's sidecar gives the rock, so
    // the waterline follows the rock and not a ruler. The level is the one the sea publishes for everything that
    // meets it, so nothing here reads the tide a second time and nothing is saved.
    public static partial class StPetersLayerRefresh
    {
        public const string WhelpsStep = "WhelpSkerries";
        public const string WhelpsRootName = "StPetersWhelps";
        public const string WhelpsFolder = "Assets/_Project/Data/Regions/StPetersWhelps";
        public const string WhelpsHeights = "Assets/_Project/Art/Sprites/StPeters/Whelps";
        public const string WhelpsHeightsContractPath = WhelpsHeights + "/RockHeights.json";
        public const string TidalRockUpperName = "Above";
        const string RockKind = "rock";

        /// <summary>The height sheets' contract (tools/rig-recipes/whelp-rock-heights.mjs writes it).</summary>
        [Serializable]
        public sealed class RockHeightsContract
        {
            public float rangeMetres;
            public Sheet[] sheets;

            [Serializable] public sealed class Sheet { public string stem; public string height; public float[] tallestDrawnMetres; }

            public static RockHeightsContract Parse(string json)
            {
                var c = JsonUtility.FromJson<RockHeightsContract>(json);
                if (c == null || !(c.rangeMetres > 0f) || c.sheets == null || c.sheets.Length == 0)
                    throw new Refusal($"{WhelpsStep}: the height contract names no range or no sheets.");
                return c;
            }
        }

        /// <summary>One rock to stand in the water.</summary>
        public sealed class TidalRock
        {
            public string Id;
            public Vector2 At;
            /// <summary>The height of the ground it sits on, metres above the datum.</summary>
            public float SeatMetres;
            /// <summary>The rock's own height, from the kit's sidecar.</summary>
            public float TopMetres;
            /// <summary>The tallest rise the height sheet holds for this rock's cell, as drawn.</summary>
            public float TallestDrawnMetres;
            public SpriteRef Band, Upper;
            public ObjRef Height;
        }

        public sealed class TidalRockField
        {
            public TidalRockFieldDef Def;
            public List<TidalRock> Rocks = new List<TidalRock>();
        }

        public interface ITidalRockAssets
        {
            ObjRef FaceMaterial { get; }
            ScriptRef ScriptFor(Type type);
        }

        public sealed class EditorTidalRockAssets : ITidalRockAssets
        {
            readonly EditorKeySceneAssets _shared = new EditorKeySceneAssets();
            readonly EditorVillageLightAssets _scripts = new EditorVillageLightAssets();
            public ObjRef FaceMaterial => _shared.FaceMaterial;
            public ScriptRef ScriptFor(Type type) => _scripts.ScriptFor(type);
        }

        /// <summary>
        /// How a rock's height sheet is read back: (seat, metres a full-white pixel means, lift). A pixel is
        /// under water when <c>seat + red * range - lift</c> is below the sea, so a lift of the wet band cuts the
        /// upper picture that much above the water. The sheet's rise is as drawn; it is scaled so the tallest
        /// pixel stands at the height the kit gives the rock.
        /// </summary>
        public static Vector3 TidalRockDecode(float seatMetres, float sheetRangeMetres, float topMetres,
            float tallestDrawnMetres, float liftMetres)
        {
            if (!(tallestDrawnMetres > 0f) || !(topMetres > 0f) || !(sheetRangeMetres > 0f))
                throw new Refusal($"{WhelpsStep}: a rock with no height cannot stand in the tide.");
            return new Vector3(seatMetres, sheetRangeMetres * topMetres / tallestDrawnMetres, liftMetres);
        }

        static string[] TidalFaceFields(ObjRef height, Vector3 decode) => new[]
        {
            "  _lipWorldY: 0", "  _lipElevation: 0", "  _ridesTheTide: 1", "  _configured: 1",
            "  _heightTexture: " + height.Yaml, "  _heightDecode: " + Vec3Yaml(decode.x, decode.y, decode.z),
        };

        /// <summary>The patch that stands the fields' rocks under <see cref="WhelpsRootName"/>.</summary>
        public static LayerPatch Whelps(SceneYaml scene, IReadOnlyList<TidalRockField> fields,
            RockHeightsContract contract, IReadOnlyList<TidalForm> forms, ITidalRockAssets assets)
        {
            var all = fields.SelectMany(f => f.Rocks).ToList();
            if (all.Select(r => r.Id).Concat(forms.Select(f => f.Id)).Distinct(StringComparer.Ordinal).Count() != all.Count + forms.Count)
                throw new Refusal($"{WhelpsStep}: rocks or forms have duplicate ids.");
            const string rootName = WhelpsRootName;
            var root = scene.Docs.Any(d => d.ClassId == GameObjectClass && d.Field("m_Name") == rootName)
                ? scene.RootNamed(rootName) : null;
            var owned = root == null ? new HashSet<long>() : scene.SubtreeOf(root.FileId);
            var ids = new IdAllocator(scene, owned);
            long Next(string name, string kind) => ids.Next(rootName + "/" + name + "/" + kind);
            long rootGo = Next("", "GameObject"), rootTr = Next("", "Transform");
            if (root != null && root.FileId != rootGo) throw new Refusal($"{WhelpsStep}: the root has unexpected ids.");

            var wanted = new List<Wanted>();
            var rootChildren = new List<long>();
            var none = new List<long>();
            // The forms first: the stack and the ledges the rocks sit on.
            foreach (TidalForm form in forms.OrderBy(f => f.Id, StringComparer.Ordinal))
                rootChildren.Add(WantTidalForm(form, Next, wanted, rootTr, assets));
            foreach (var field in fields.OrderBy(f => f.Def.Id, StringComparer.Ordinal))
            foreach (var rock in field.Rocks.OrderBy(r => r.Id, StringComparer.Ordinal))
            {
                string name = rock.Id, upper = name + "/" + TidalRockUpperName;
                long go = Next(name, "GameObject"), tr = Next(name, "Transform"), sr = Next(name, "SpriteRenderer");
                long sort = Next(name, "YSortSprite"), face = Next(name, "TidalFaceWaterline");
                long ug = Next(upper, "GameObject"), ut = Next(upper, "Transform"), usr = Next(upper, "SpriteRenderer");
                long uface = Next(upper, "TidalFaceWaterline");
                int order = YSortSprite.OrderFor(rock.At.y, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                    SortingBands.DecorFloor, SortingBands.DecorCeiling - 1);
                Vector3 atTheWater = TidalRockDecode(rock.SeatMetres, contract.rangeMetres, rock.TopMetres, rock.TallestDrawnMetres, 0f);
                Vector3 aboveTheBand = TidalRockDecode(rock.SeatMetres, contract.rangeMetres, rock.TopMetres, rock.TallestDrawnMetres,
                    field.Def.WetBandMetres);

                wanted.Add(Want(GameObjectYaml(go, name, new[] { tr, sr, sort, face }), name, "GameObject"));
                wanted.Add(Want(TransformYaml(tr, go, rock.At, new[] { ut }, rootTr), name, "Transform"));
                wanted.Add(Want(SpriteRendererYaml(sr, go, assets.FaceMaterial, rock.Band, order), name, "SpriteRenderer"));
                wanted.Add(Want(MonoBehaviourYaml(sort, go, assets.ScriptFor(typeof(YSortSprite)), new[]
                {
                    "  _dynamic: 0", "  _baseOrder: " + F(SortingBands.DecorBase),
                    "  _orderPerUnit: " + F(SortingBands.OrdersPerMetre), "  _minOrder: " + Int(SortingBands.DecorFloor),
                    "  _maxOrder: " + Int(SortingBands.DecorCeiling - 1), "  _pivotYOffset: 0",
                }), name, "YSortSprite"));
                wanted.Add(Want(MonoBehaviourYaml(face, go, assets.ScriptFor(typeof(TidalFaceWaterline)),
                    TidalFaceFields(rock.Height, atTheWater)), name, "TidalFaceWaterline"));

                wanted.Add(Want(GameObjectYaml(ug, TidalRockUpperName, new[] { ut, usr, uface }), upper, "GameObject"));
                wanted.Add(Want(TransformYaml(ut, ug, Vector2.zero, none, tr), upper, "Transform"));
                wanted.Add(Want(SpriteRendererYaml(usr, ug, assets.FaceMaterial, rock.Upper, order + 1), upper, "SpriteRenderer"));
                wanted.Add(Want(MonoBehaviourYaml(uface, ug, assets.ScriptFor(typeof(TidalFaceWaterline)),
                    TidalFaceFields(rock.Height, aboveTheBand)), upper, "TidalFaceWaterline"));
                rootChildren.Add(tr);
            }

            wanted.Insert(0, Want(GameObjectYaml(rootGo, rootName, new[] { rootTr }), rootName, "GameObject"));
            wanted.Insert(1, Want(TransformYaml(rootTr, rootGo, Vector2.zero, rootChildren, 0), rootName, "Transform"));
            var patch = new LayerPatch(WhelpsStep, rootName, rootGo);
            var desired = new HashSet<long>(wanted.Select(w => w.FileId));
            foreach (var doc in scene.Docs.Where(d => owned.Contains(d.FileId) && !desired.Contains(d.FileId)))
                patch.Delete(doc, NameOfExisting(scene, doc));
            foreach (var want in wanted)
            {
                var doc = scene.Get(want.FileId);
                if (doc == null) patch.Add(want.Text, want.Name);
                else patch.Edit(doc, want.Text, want.Name);
            }
            if (root == null)
            {
                var list = SceneRootsOf(scene);
                patch.Edit(list, WithRootListed(list.Text, rootTr), rootName + " joins SceneRoots");
            }
            return patch.Seal(scene);
        }

        static string OptionOf(KeyScenePiece p, string key) =>
            (p.Options ?? Array.Empty<KeySceneOption>()).FirstOrDefault(o => o.Key == key)?.Value
            ?? throw new Refusal($"{WhelpsStep}: {p.Id} has no '{key}'.");

        /// <summary>The rock fields as their Defs, their key scenes' rows, the kit and the height sheets have them.</summary>
        public static List<TidalRockField> LoadTidalRockFields(RockHeightsContract contract)
        {
            var scenes = new List<KeySceneDef>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(KeySceneDef), new[] { KeySceneFolder }))
            {
                var k = AssetDatabase.LoadAssetAtPath<KeySceneDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (k != null) scenes.Add(k);
            }
            var fields = new List<TidalRockField>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(TidalRockFieldDef), new[] { WhelpsFolder }))
            {
                var def = AssetDatabase.LoadAssetAtPath<TidalRockFieldDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null) continue;
                if (string.IsNullOrEmpty(def.PiecePrefix)) throw new Refusal($"{WhelpsStep}: {def.Id} names no rows.");
                KeySceneDef scene = scenes.SingleOrDefault(k => k.Id == def.KeySceneId)
                    ?? throw new Refusal($"{WhelpsStep}: {def.Id} names no key scene ({def.KeySceneId}).");
                int bandRow = Array.IndexOf(RockPxCatalog.Tides, def.WetBandState);
                if (bandRow < 0) throw new Refusal($"{WhelpsStep}: {def.Id}: '{def.WetBandState}' is not one of the kit's tides.");
                var field = new TidalRockField { Def = def };
                foreach (KeyScenePiece p in scene.Pieces.Where(p => p.Kind == RockKind &&
                             p.Id.StartsWith(def.PiecePrefix, StringComparison.Ordinal)))
                {
                    string stone = OptionOf(p, "stone");
                    if (!int.TryParse(OptionOf(p, "variant"), out int variant) || variant < 0 || variant >= RockPxCatalog.VariantCols)
                        throw new Refusal($"{WhelpsStep}: {p.Id} has no variant the kit holds.");
                    if (!int.TryParse(OptionOf(p, "dress"), out int dressIndex) || dressIndex < 0 || dressIndex >= RockPxCatalog.Dress.Length)
                        throw new Refusal($"{WhelpsStep}: {p.Id} has no dress the kit holds.");
                    int upperRow = Array.IndexOf(RockPxCatalog.Tides, OptionOf(p, "tide"));
                    if (upperRow < 0) throw new Refusal($"{WhelpsStep}: {p.Id}: its tide is not one of the kit's.");
                    string stem, sheet;
                    try
                    {
                        stem = RockPxCatalog.StemFor(p.Piece, stone, RockPxCatalog.Dress[dressIndex]);
                        sheet = RockPxCatalog.SheetPath(p.Piece, stone, RockPxCatalog.Dress[dressIndex], RockPxCatalog.Channel.Albedo);
                    }
                    catch (ArgumentException e) { throw new Refusal($"{WhelpsStep}: {p.Id}: {e.Message}"); }
                    RockPxCatalog.VariantEntry entry = RockPxCatalog.LoadSidecar(p.Piece)?.For(stone)?.FirstOrDefault(v => v.Variant == variant)
                        ?? throw new Refusal($"{WhelpsStep}: {p.Id}: the kit's sidecar has no {p.Piece} {stone} variant {variant}.");
                    RockHeightsContract.Sheet heights = contract.sheets.FirstOrDefault(s => s.stem == stem)
                        ?? throw new Refusal($"{WhelpsStep}: {p.Id}: no height sheet for {stem}.");
                    string heightGuid = AssetDatabase.AssetPathToGUID(WhelpsHeights + "/" + heights.height);
                    if (string.IsNullOrEmpty(heightGuid)) throw new Refusal($"{WhelpsStep}: {heights.height} is not an asset.");
                    var cells = AssetDatabase.LoadAllAssetsAtPath(sheet).OfType<Sprite>().ToDictionary(s => s.name, s => s, StringComparer.Ordinal);
                    Sprite Cell(int row) => cells.TryGetValue($"{stem}_c{Int(variant)}_r{Int(row)}", out Sprite s) ? s
                        : throw new Refusal($"{WhelpsStep}: {p.Id}: {sheet} has no cell for variant {variant}, row {row}.");
                    field.Rocks.Add(new TidalRock
                    {
                        Id = p.Id, At = p.At, SeatMetres = p.Z, TopMetres = entry.TopM,
                        TallestDrawnMetres = heights.tallestDrawnMetres[variant],
                        Band = EditorShoreRockSprites.RefOf(Cell(bandRow)), Upper = EditorShoreRockSprites.RefOf(Cell(upperRow)),
                        Height = new ObjRef(2800000, heightGuid, 3),
                    });
                }
                if (field.Rocks.Count == 0) throw new Refusal($"{WhelpsStep}: {def.Id} finds no rock row starting '{def.PiecePrefix}'.");
                fields.Add(field);
            }
            fields.Sort((a, b) => string.CompareOrdinal(a.Def.Id, b.Def.Id));
            return fields;
        }

        public static RockHeightsContract LoadRockHeightsContract() =>
            RockHeightsContract.Parse(File.ReadAllText(WhelpsHeightsContractPath));

        [MenuItem("Hidden Harbours/World/St Peters Layer Refresh/Write the Whelps Patch (dry run)")]
        static void WriteWhelpsMenu() { try { RunWhelps(false); } catch (Refusal r) { Debug.LogError(r.Message); } }

        /// <summary>The dry run for <c>-executeMethod</c>.</summary>
        public static void WriteWhelpsPatchBatch() => RunWhelps(apply: false);

        /// <summary>The apply for <c>-executeMethod</c>.</summary>
        public static void ApplyWhelpsPatchBatch() => RunWhelps(apply: true);

        /// <summary>One run for <c>-executeMethod</c>: the keepers' step (the cottage's door lamp is its), the rocks,
        /// then the ground kept clear of planting under every building, the cottage among them.</summary>
        public static void ApplyWhelpsAndCottageBatch()
        {
            RunKeepers(apply: true);
            RunWhelps(apply: true);
            StPetersBuildingClearance.ApplyToGround();
        }

        public static LayerPatch RunWhelps(bool apply)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (apply && string.Equals(SceneManager.GetSceneAt(i).path, ScenePath, StringComparison.Ordinal))
                    throw new Refusal("StPeters is open in the editor. Close it first: an open scene would overwrite the file on its next save.");

            string text = File.ReadAllText(ScenePath);
            RockHeightsContract contract = LoadRockHeightsContract();
            LayerPatch patch = Whelps(SceneYaml.Parse(text), LoadTidalRockFields(contract), contract,
                LoadTidalForms(LoadTidalFormsContract()), new EditorTidalRockAssets());
            Directory.CreateDirectory(PatchFolder);
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(Path.Combine(PatchFolder, patch.Step + ".patch.yaml"), patch.ToYaml(), utf8);
            string after = patch.ApplyTo(text);
            if (apply && after != text) File.WriteAllText(ScenePath, after, utf8);
            Debug.Log($"[StPetersLayerRefresh] {(apply ? "applied to " + ScenePath : "dry run, nothing written to the scene")} " +
                      $"(patch in {PatchFolder}):\n{patch.Summary()}");
            return patch;
        }
    }
}
#endif
