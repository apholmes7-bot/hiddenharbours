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
    /// <see cref="FootbridgesRootName"/>, turned along its own axis (square to the brook): the deck as ground that
    /// can be stood on and that guides a walker along it (<see cref="FootbridgeDeck"/>), the planks drawn on the
    /// floor under everything that walks, and a low rail along each long side, not walked through: the landward one
    /// sorts behind a walker anywhere on the planks (from its highest point), the seaward one in front (from its
    /// lowest).
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
        public const string FootbridgeNearRailName = "RailNear", FootbridgeFarRailName = "RailFar";

        /// <summary>What the recipe drew (tools/rig-recipes/gap-footbridge.mjs).</summary>
        [Serializable]
        public sealed class FootbridgeContract
        {
            public int pixelsPerMetre;
            public float axisDegrees, spanMetres, widthMetres, railInsetMetres, railHeightMetres;
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
            public float AxisDegrees, SpanMetres, WidthMetres, StandingReachMetres, GuideConeDegrees, GuideApproachMetres;
            public float DeckMetres, RailInsetMetres, RailThicknessMetres, FarRailSortsBehindMetres;
            public bool NearRail, FarRail;
            public SpriteRef DeckPicture, RailPicture;

            public Vector2 Axis => FootbridgeDef.AxisOf(AxisDegrees);
            public Vector2 Across => new Vector2(-Axis.y, Axis.x);
        }

        /// <summary>Where a rail stands, local to the bridge: the middle of its long edge of the planks.</summary>
        public static Vector2 FootbridgeRailAt(Footbridge b, bool near) => b.Across * ((near ? -0.5f : 0.5f) * b.WidthMetres);

        /// <summary>How far above (+) or below (-) its own place a rail sorts, metres. The near rail sorts from its
        /// lowest point, so it is in front of a walker anywhere on the planks; the far rail from its highest point
        /// and the walker's body height behind that, so it is behind him anywhere on them.</summary>
        public static float FootbridgeRailSortOffset(Footbridge b, bool near)
        {
            float rise = (b.SpanMetres * 0.5f - b.RailInsetMetres) * Mathf.Abs(b.Axis.y);
            return near ? -rise : rise + b.FarRailSortsBehindMetres;
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

        /// <summary>A rail's line on the ground, local to the bridge: a thin strip along the axis just outside the
        /// deck's seaward edge (the near rail) or its landward edge (the far one), so the whole width of the planks
        /// is walked.</summary>
        public static Vector2[] FootbridgeRailLine(Footbridge b, bool near)
        {
            float half = b.SpanMetres * 0.5f - b.RailInsetMetres, t = b.RailThicknessMetres;
            float v0 = near ? -b.WidthMetres * 0.5f - t : b.WidthMetres * 0.5f;
            Vector2 a = b.Axis, n = b.Across;
            Vector2 P(float u, float v) => a * u + n * v;
            return new[] { P(-half, v0), P(half, v0), P(half, v0 + t), P(-half, v0 + t) };
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
                string name = b.Id, deck = name + "/" + FootbridgeDeckName;
                long go = Next(name, "GameObject"), tr = Next(name, "Transform"), platform = Next(name, "FootbridgeDeck");
                long dg = Next(deck, "GameObject"), dt = Next(deck, "Transform"), dsr = Next(deck, "SpriteRenderer");
                var children = new List<long> { dt };
                var rails = new List<(string Name, bool Near, long Go, long Tr, long Sr, long Sort, long Wall)>();
                foreach (bool near in new[] { false, true })
                {
                    if (near ? !b.NearRail : !b.FarRail) continue;
                    string rail = name + "/" + (near ? FootbridgeNearRailName : FootbridgeFarRailName);
                    rails.Add((rail, near, Next(rail, "GameObject"), Next(rail, "Transform"), Next(rail, "SpriteRenderer"),
                        Next(rail, "YSortSprite"), Next(rail, "PolygonCollider2D")));
                    children.Add(rails[rails.Count - 1].Tr);
                }

                wanted.Add(Want(GameObjectYaml(go, name, new[] { tr, platform }), name, "GameObject"));
                wanted.Add(Want(TransformYaml(tr, go, b.At, children, rootTr), name, "Transform"));
                wanted.Add(Want(MonoBehaviourYaml(platform, go, assets.ScriptFor(typeof(FootbridgeDeck)), new[]
                {
                    "  _id: " + Plain(b.Id), "  _at: {x: " + F(b.At.x) + ", y: " + F(b.At.y) + "}",
                    "  _axisDegrees: " + F(b.AxisDegrees), "  _spanMetres: " + F(b.SpanMetres),
                    "  _widthMetres: " + F(b.WidthMetres), "  _standingReachMetres: " + F(b.StandingReachMetres),
                    "  _deckElevation: " + F(b.DeckMetres), "  _guideConeDegrees: " + F(b.GuideConeDegrees),
                    "  _guideApproachMetres: " + F(b.GuideApproachMetres),
                }), name, "FootbridgeDeck"));

                // The planks lie on the floor of the decor band: under the walker and under every plant.
                wanted.Add(Want(GameObjectYaml(dg, FootbridgeDeckName, new[] { dt, dsr }), deck, "GameObject"));
                wanted.Add(Want(TransformYaml(dt, dg, Vector2.zero, none, tr), deck, "Transform"));
                wanted.Add(Want(SpriteRendererYaml(dsr, dg, assets.SpriteMaterial, b.DeckPicture, SortingBands.DecorFloor), deck, "SpriteRenderer"));

                foreach (var (rail, near, rg, rt, rsr, rsort, rwall) in rails)
                {
                    // A rail stands on its edge of the deck and takes the order that edge earns: the far one is drawn
                    // before a walker anywhere on the planks, the near one after. The edge runs up the screen with the
                    // axis, so each sorts from the end that keeps that true (FootbridgeRailSortOffset).
                    Vector2 foot = FootbridgeRailAt(b, near);
                    string railName = near ? FootbridgeNearRailName : FootbridgeFarRailName;
                    float behind = FootbridgeRailSortOffset(b, near);
                    int order = YSortSprite.OrderFor(b.At.y + foot.y + behind, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                        SortingBands.DecorFloor + 1, SortingBands.DecorCeiling - 1);
                    wanted.Add(Want(GameObjectYaml(rg, railName, new[] { rt, rsr, rsort, rwall }), rail, "GameObject"));
                    wanted.Add(Want(TransformYaml(rt, rg, foot, none, tr), rail, "Transform"));
                    wanted.Add(Want(SpriteRendererYaml(rsr, rg, assets.SpriteMaterial, b.RailPicture, order), rail, "SpriteRenderer"));
                    wanted.Add(Want(MonoBehaviourYaml(rsort, rg, assets.ScriptFor(typeof(YSortSprite)), new[]
                    {
                        "  _dynamic: 0", "  _baseOrder: " + F(SortingBands.DecorBase),
                        "  _orderPerUnit: " + F(SortingBands.OrdersPerMetre), "  _minOrder: " + Int(SortingBands.DecorFloor + 1),
                        "  _maxOrder: " + Int(SortingBands.DecorCeiling - 1), "  _pivotYOffset: " + F(behind),
                    }), rail, "YSortSprite"));
                    Vector2[] line = FootbridgeRailLine(b, near);
                    for (int i = 0; i < line.Length; i++) line[i] -= foot;
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

        /// <summary>How far a Def's axis may stand from the one the planks were drawn at, degrees (the contract
        /// rounds to five places).</summary>
        public const float FootbridgeAxisToleranceDegrees = 0.001f;

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
                if (Mathf.Abs(def.AxisDegrees - contract.axisDegrees) > FootbridgeAxisToleranceDegrees)
                    throw new Refusal($"{FootbridgesStep}: {def.Id} runs at {F(def.AxisDegrees)} degrees; the planks were drawn at " +
                                      $"{F(contract.axisDegrees)}. Draw them again (tools/rig-recipes/gap-footbridge.mjs).");
                bridges.Add(new Footbridge
                {
                    Id = def.Id, At = def.At, AxisDegrees = def.AxisDegrees, SpanMetres = def.SpanMetres, WidthMetres = def.WidthMetres,
                    StandingReachMetres = def.StandingReachMetres, GuideConeDegrees = def.GuideConeDegrees, GuideApproachMetres = def.GuideApproachMetres,
                    DeckMetres = def.DeckMetres, NearRail = def.SeawardRail, FarRail = def.LandwardRail,
                    FarRailSortsBehindMetres = def.LandwardRailSortsBehindMetres,
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
                Vector2 half = bridge.Axis * (bridge.SpanMetres * 0.5f);
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
