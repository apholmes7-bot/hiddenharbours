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
    /// <summary>
    /// <b>The footbridges.</b> Each <see cref="FootbridgeDef"/> in the Whelps' folder is stood in the scene under
    /// <see cref="FootbridgesRootName"/>: the deck as ground that can be stood on, the planks drawn on the floor
    /// under everything that walks, and the seaward rail standing up, sorted by its foot and not walked through.
    ///
    /// <para>After it, <see cref="ApplyTroddenGround"/> takes the planting off the ground that is walked here: the
    /// paths by the cottage and the bridge's planks.</para>
    /// </summary>
    public static partial class StPetersLayerRefresh
    {
        public const string FootbridgesStep = "Footbridges";
        public const string FootbridgesRootName = "StPetersFootbridges";
        public const string FootbridgeArt = "Assets/_Project/Art/Sprites/StPeters/Footbridge";
        public const string FootbridgeContractPath = FootbridgeArt + "/Footbridge.json";
        /// <summary>The deck's picture is a floor: the sun casts nothing from a name that says so.</summary>
        public const string FootbridgeDeckName = "DeckFloor";
        public const string FootbridgeRailName = "Rail";

        /// <summary>What the recipe drew (tools/rig-recipes/gap-footbridge.mjs).</summary>
        [Serializable]
        public sealed class FootbridgeContract
        {
            public int pixelsPerMetre;
            public float spanMetres, widthMetres, railInsetMetres, railHeightMetres;
            public string deck, rail;

            public static FootbridgeContract Parse(string json)
            {
                var c = JsonUtility.FromJson<FootbridgeContract>(json);
                if (c == null || !(c.spanMetres > 0f) || !(c.widthMetres > 0f) || string.IsNullOrEmpty(c.deck) || string.IsNullOrEmpty(c.rail))
                    throw new Refusal($"{FootbridgesStep}: {FootbridgeContractPath} does not say what was drawn.");
                return c;
            }
        }

        /// <summary>A bridge as the step stands it.</summary>
        public sealed class Footbridge
        {
            public string Id;
            public Vector2 At;
            public Rect Footprint;
            public float DeckMetres, RailInsetMetres, RailThicknessMetres;
            public bool Rail;
            public SpriteRef DeckPicture, RailPicture;
        }

        public interface IFootbridgeAssets
        {
            ObjRef SpriteMaterial { get; }
            ScriptRef ScriptFor(Type type);
        }

        public sealed class EditorFootbridgeAssets : IFootbridgeAssets
        {
            readonly EditorKeySceneAssets _shared = new EditorKeySceneAssets();
            readonly EditorTidalRockAssets _scripts = new EditorTidalRockAssets();
            public ObjRef SpriteMaterial => _shared.SpriteMaterial;
            public ScriptRef ScriptFor(Type type) => _scripts.ScriptFor(type);
        }

        /// <summary>The rail's line on the ground: a thin strip along the deck's south edge, local to the bridge.</summary>
        public static Vector2[] FootbridgeRailLine(Footbridge b)
        {
            float half = b.Footprint.width * 0.5f - b.RailInsetMetres, y = -b.Footprint.height * 0.5f, t = b.RailThicknessMetres;
            return new[] { new Vector2(-half, y - t), new Vector2(half, y - t), new Vector2(half, y), new Vector2(-half, y) };
        }

        /// <summary>The patch that stands the bridges under <see cref="FootbridgesRootName"/>.</summary>
        public static LayerPatch Footbridges(SceneYaml scene, IReadOnlyList<Footbridge> bridges, IFootbridgeAssets assets)
        {
            if (bridges.Select(b => b.Id).Distinct(StringComparer.Ordinal).Count() != bridges.Count)
                throw new Refusal($"{FootbridgesStep}: bridges have duplicate ids.");
            const string rootName = FootbridgesRootName;
            var root = scene.Docs.Any(d => d.ClassId == GameObjectClass && d.Field("m_Name") == rootName)
                ? scene.RootNamed(rootName) : null;
            var owned = root == null ? new HashSet<long>() : scene.SubtreeOf(root.FileId);
            var ids = new IdAllocator(scene, owned);
            long Next(string name, string kind) => ids.Next(rootName + "/" + name + "/" + kind);
            long rootGo = Next("", "GameObject"), rootTr = Next("", "Transform");
            if (root != null && root.FileId != rootGo) throw new Refusal($"{FootbridgesStep}: the root has unexpected ids.");

            var wanted = new List<Wanted>();
            var rootChildren = new List<long>();
            var none = new List<long>();
            foreach (Footbridge b in bridges.OrderBy(x => x.Id, StringComparer.Ordinal))
            {
                string name = b.Id, deck = name + "/" + FootbridgeDeckName, rail = name + "/" + FootbridgeRailName;
                long go = Next(name, "GameObject"), tr = Next(name, "Transform"), platform = Next(name, "StandablePlatform");
                long dg = Next(deck, "GameObject"), dt = Next(deck, "Transform"), dsr = Next(deck, "SpriteRenderer");
                var children = new List<long> { dt };
                long rg = 0, rt = 0, rsr = 0, rsort = 0, rwall = 0;
                if (b.Rail)
                {
                    rg = Next(rail, "GameObject"); rt = Next(rail, "Transform"); rsr = Next(rail, "SpriteRenderer");
                    rsort = Next(rail, "YSortSprite"); rwall = Next(rail, "PolygonCollider2D");
                    children.Add(rt);
                }

                wanted.Add(Want(GameObjectYaml(go, name, new[] { tr, platform }), name, "GameObject"));
                wanted.Add(Want(TransformYaml(tr, go, b.At, children, rootTr), name, "Transform"));
                wanted.Add(Want(MonoBehaviourYaml(platform, go, assets.ScriptFor(typeof(StandablePlatform)), new[]
                {
                    "  _id: " + Plain(b.Id), "  _footprint:", "    serializedVersion: 2", "    x: " + F(b.Footprint.x),
                    "    y: " + F(b.Footprint.y), "    width: " + F(b.Footprint.width), "    height: " + F(b.Footprint.height),
                    "  _deckElevation: " + F(b.DeckMetres), "  _drawnLiftedByDeck: 0",
                }), name, "StandablePlatform"));

                // The planks lie on the floor of the decor band: under the walker and under every plant.
                wanted.Add(Want(GameObjectYaml(dg, FootbridgeDeckName, new[] { dt, dsr }), deck, "GameObject"));
                wanted.Add(Want(TransformYaml(dt, dg, Vector2.zero, none, tr), deck, "Transform"));
                wanted.Add(Want(SpriteRendererYaml(dsr, dg, assets.SpriteMaterial, b.DeckPicture, SortingBands.DecorFloor), deck, "SpriteRenderer"));

                if (b.Rail)
                {
                    // The rail stands on the deck's south edge and takes the order that edge earns.
                    float foot = -b.Footprint.height * 0.5f;
                    int order = YSortSprite.OrderFor(b.At.y + foot, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                        SortingBands.DecorFloor + 1, SortingBands.DecorCeiling - 1);
                    wanted.Add(Want(GameObjectYaml(rg, FootbridgeRailName, new[] { rt, rsr, rsort, rwall }), rail, "GameObject"));
                    wanted.Add(Want(TransformYaml(rt, rg, new Vector2(0f, foot), none, tr), rail, "Transform"));
                    wanted.Add(Want(SpriteRendererYaml(rsr, rg, assets.SpriteMaterial, b.RailPicture, order), rail, "SpriteRenderer"));
                    wanted.Add(Want(MonoBehaviourYaml(rsort, rg, assets.ScriptFor(typeof(YSortSprite)), new[]
                    {
                        "  _dynamic: 0", "  _baseOrder: " + F(SortingBands.DecorBase),
                        "  _orderPerUnit: " + F(SortingBands.OrdersPerMetre), "  _minOrder: " + Int(SortingBands.DecorFloor + 1),
                        "  _maxOrder: " + Int(SortingBands.DecorCeiling - 1), "  _pivotYOffset: 0",
                    }), rail, "YSortSprite"));
                    Vector2[] line = FootbridgeRailLine(b);
                    for (int i = 0; i < line.Length; i++) line[i] -= new Vector2(0f, foot);
                    wanted.Add(Want(PolygonColliderYaml(rwall, rg, new List<Vector2[]> { line }), rail, "PolygonCollider2D"));
                }
                rootChildren.Add(tr);
            }

            wanted.Insert(0, Want(GameObjectYaml(rootGo, rootName, new[] { rootTr }), rootName, "GameObject"));
            wanted.Insert(1, Want(TransformYaml(rootTr, rootGo, Vector2.zero, rootChildren, 0), rootName, "Transform"));
            var patch = new LayerPatch(FootbridgesStep, rootName, rootGo);
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

        public static FootbridgeContract LoadFootbridgeContract() =>
            FootbridgeContract.Parse(File.ReadAllText(FootbridgeContractPath));

        /// <summary>The bridge Defs, each checked against what was drawn for it, in id order.</summary>
        public static List<Footbridge> LoadFootbridges(FootbridgeContract contract)
        {
            Sprite Picture(string stem) => AssetDatabase.LoadAssetAtPath<Sprite>(FootbridgeArt + "/" + stem + ".png")
                ?? throw new Refusal($"{FootbridgesStep}: {stem}.png is not an imported picture.");
            var bridges = new List<Footbridge>();
            foreach (FootbridgeDef def in LoadFootbridgeDefs())
            {
                if (!Mathf.Approximately(def.SpanMetres, contract.spanMetres) || !Mathf.Approximately(def.WidthMetres, contract.widthMetres))
                    throw new Refusal($"{FootbridgesStep}: {def.Id} is {F(def.SpanMetres)} by {F(def.WidthMetres)} m; the planks were drawn " +
                                      $"{F(contract.spanMetres)} by {F(contract.widthMetres)} m. Draw them again (tools/rig-recipes/gap-footbridge.mjs).");
                bridges.Add(new Footbridge
                {
                    Id = def.Id, At = def.At, Footprint = def.Footprint, DeckMetres = def.DeckMetres, Rail = def.SeawardRail,
                    RailInsetMetres = contract.railInsetMetres, RailThicknessMetres = def.RailThicknessMetres,
                    DeckPicture = EditorShoreRockSprites.RefOf(Picture(contract.deck)),
                    RailPicture = EditorShoreRockSprites.RefOf(Picture(contract.rail)),
                });
            }
            return bridges;
        }

        public static List<FootbridgeDef> LoadFootbridgeDefs()
        {
            var defs = new List<FootbridgeDef>();
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(FootbridgeDef), new[] { WhelpsFolder }))
            {
                var def = AssetDatabase.LoadAssetAtPath<FootbridgeDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null) defs.Add(def);
            }
            defs.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return defs;
        }

        [MenuItem("Hidden Harbours/World/St Peters Layer Refresh/Write the Footbridges Patch (dry run)")]
        static void WriteFootbridgesMenu() { try { RunFootbridges(false); } catch (Refusal r) { Debug.LogError(r.Message); } }

        /// <summary>The apply for <c>-executeMethod</c>: the bridges, then the walked ground kept clear of planting.</summary>
        public static void ApplyFootbridgesAndTroddenGroundBatch()
        {
            RunFootbridges(apply: true);
            ApplyTroddenGround();
        }

        public static LayerPatch RunFootbridges(bool apply)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (apply && string.Equals(SceneManager.GetSceneAt(i).path, ScenePath, StringComparison.Ordinal))
                    throw new Refusal("StPeters is open in the editor. Close it first: an open scene would overwrite the file on its next save.");

            string text = File.ReadAllText(ScenePath);
            LayerPatch patch = Footbridges(SceneYaml.Parse(text), LoadFootbridges(LoadFootbridgeContract()), new EditorFootbridgeAssets());
            Directory.CreateDirectory(PatchFolder);
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(Path.Combine(PatchFolder, patch.Step + ".patch.yaml"), patch.ToYaml(), utf8);
            string after = patch.ApplyTo(text);
            if (apply && after != text) File.WriteAllText(ScenePath, after, utf8);
            Debug.Log($"[StPetersLayerRefresh] {(apply ? "applied to " + ScenePath : "dry run, nothing written to the scene")} " +
                      $"(patch in {PatchFolder}):\n{patch.Summary()}");
            return patch;
        }

        // ---- the walked ground ------------------------------------------------------------------------------------

        /// <summary>The plan's paths whose ground by the cottage is kept clear of planting.</summary>
        public static readonly string[] TroddenPathAssets =
        {
            "Assets/_Project/Data/Terrain/StPetersPlan/Path_CliffWalk.asset",
            "Assets/_Project/Data/Terrain/StPetersPlan/Path_LookoutSpur.asset",
            "Assets/_Project/Data/Terrain/StPetersPlan/Path_EastGapPath.asset",
            "Assets/_Project/Data/Terrain/StPetersPlan/Path_WhelpsCottageDoor.asset",
        };

        /// <summary>The ground this rule looks at, world metres: the cottage above the Whelps, its lookout and the
        /// brook's crossing. The cliff walk runs the island's whole east side; only this stretch was re-laid.</summary>
        public static readonly Rect TroddenGroundWindow = Rect.MinMaxRect(150f, -46f, 176f, -24f);

        /// <summary>How far past a path's edge a plant's root is still on it, metres (a tuft's half width).</summary>
        public const float TroddenMarginMetres = 0.3f;

        /// <summary>A stretch of walked ground: a line and how far from it is walked.</summary>
        public struct Trodden
        {
            public Vector2 A, B;
            public float Reach;
            public string Who;

            public bool Holds(Vector2 p, float pad = 0f)
            {
                Vector2 ab = B - A;
                float t = ab.sqrMagnitude < 1e-12f ? 0f : Mathf.Clamp01(Vector2.Dot(p - A, ab) / ab.sqrMagnitude);
                return Vector2.Distance(p, A + ab * t) <= Reach + pad;
            }
        }

        /// <summary>The walked ground inside <see cref="TroddenGroundWindow"/>: the named paths' segments and each
        /// bridge's deck (its centre line, reaching half its width).</summary>
        public static List<Trodden> TroddenGround(IEnumerable<PathDef> paths, IEnumerable<FootbridgeDef> bridges)
        {
            var all = new List<Trodden>();
            foreach (PathDef path in paths)
                for (int i = 0; i + 1 < path.Points.Length; i++)
                {
                    Vector2 a = path.Points[i], b = path.Points[i + 1];
                    if (!TroddenGroundWindow.Contains(a) && !TroddenGroundWindow.Contains(b)) continue;
                    all.Add(new Trodden { A = a, B = b, Reach = path.Width * 0.5f + TroddenMarginMetres, Who = path.Id });
                }
            foreach (FootbridgeDef bridge in bridges)
            {
                Vector2 half = new Vector2(bridge.SpanMetres * 0.5f, 0f);
                all.Add(new Trodden { A = bridge.At - half, B = bridge.At + half, Reach = bridge.WidthMetres * 0.5f + TroddenMarginMetres, Who = bridge.Id });
            }
            return all;
        }

        /// <summary>
        /// Take the planting off the walked ground: the field's ground grid is closed under it, so nothing is seeded
        /// there, and a plant set down by hand on it is taken off the list. Idempotent. Batch:
        /// <c>-executeMethod HiddenHarbours.App.Editor.StPetersLayerRefresh.ApplyTroddenGround</c>.
        /// </summary>
        public static void ApplyTroddenGround()
        {
            var paths = TroddenPathAssets.Select(p => AssetDatabase.LoadAssetAtPath<PathDef>(p)
                ?? throw new Refusal($"{FootbridgesStep}: {p} is not a path.")).ToList();
            List<Trodden> ground = TroddenGround(paths, LoadFootbridgeDefs());
            var data = AssetDatabase.LoadAssetAtPath<PlantFieldGroundData>(StPetersBuildingClearance.GroundPath);
            var definition = AssetDatabase.LoadAssetAtPath<PlantFieldDefinition>(StPetersBuildingClearance.DefinitionPath);
            float cell = definition.CellSize;
            byte[] map = data.Decode();
            int changed = 0, walked = 0;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((TroddenGroundWindow.xMin - definition.Origin.x) / cell));
            int x1 = Mathf.Min(data.Width - 1, Mathf.FloorToInt((TroddenGroundWindow.xMax - definition.Origin.x) / cell));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((TroddenGroundWindow.yMin - definition.Origin.y) / cell));
            int y1 = Mathf.Min(data.Height - 1, Mathf.FloorToInt((TroddenGroundWindow.yMax - definition.Origin.y) / cell));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var centre = new Vector2(definition.Origin.x + (x + 0.5f) * cell, definition.Origin.y + (y + 0.5f) * cell);
                int at = y * data.Width + x;
                if (!ground.Any(g => g.Holds(centre))) continue;
                walked++;
                if (map[at] == 255) continue;
                map[at] = 255; changed++;
            }
            if (changed > 0)
            {
                data.Payload = StPetersBuildingClearance.Encode(map);
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
            }
            var uprooted = definition.Authored.Where(a => TroddenGroundWindow.Contains(a.At) && ground.Any(g => g.Holds(a.At))).ToArray();
            if (uprooted.Length > 0)
            {
                var gone = new HashSet<string>(uprooted.Select(a => a.Id));
                definition.Authored = definition.Authored.Where(a => !gone.Contains(a.Id)).ToArray();
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssetIfDirty(definition);
            }
            var log = new StringBuilder();
            foreach (var who in ground.Select(g => g.Who).Distinct())
                log.AppendLine($"{who}: {uprooted.Count(a => ground.Any(g => g.Who == who && g.Holds(a.At)))} authored plant(s) removed");
            log.AppendLine($"total {walked} cell(s) of {cell} m walked, {changed} newly closed, {uprooted.Length} authored plant(s) removed, {definition.Authored.Length} kept");
            Debug.Log("[StPetersTroddenGround]\n" + log);
        }
    }
}
#endif
