#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;                 // YSortSprite.OrderFor
using HiddenHarbours.Core;                // SortingBands
using HiddenHarbours.World;               // KeySceneDef, PropPieceKitDef, PropRunLayout

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// The key scenes' yard pieces (<see cref="PropPiecesKit"/>): the fog bell, the clothesline, the picket fences
    /// and the plank walk at the north-east light. A single piece is placed like any other cell (one picture, the
    /// sort its layer asks for, its Def's walk-blocking shapes). A run piece (a fence, a walk) is laid by
    /// <see cref="PropRunLayout"/> along the row's <c>runs</c> option: the piece's object holds the walk-blocking
    /// band, and each bay, post, gate or plank is a child picture of it.
    /// </summary>
    public static partial class StPetersLayerRefresh
    {
        /// <summary>The yard pieces' kit, as St Peters' key scenes name it.</summary>
        public const string PropPiecesKit = "stpLightPieces";

        /// <summary>The option a run piece's line is in: segments of two [x, y, z] points.</summary>
        public const string RunsOption = "runs";

        /// <summary>One picture of a run piece, as its child object records it.</summary>
        public sealed class KeyScenePart
        {
            public string Name;
            public SpriteRef Sprite;
            public Vector2 At;
            public bool FlipX, FlipY, Floor;
            public int SortingOrder;
        }

        /// <summary>The yard pieces' kit through the AssetDatabase (a test's assets may not have it).</summary>
        public interface IKeyScenePropAssets
        {
            bool TryPropKit(string kit, out PropPieceKitDef def);
            SpriteRef RefOf(Sprite sprite);
        }

        static readonly Regex RunPointRx = new Regex(@"\[\s*(-?[0-9.]+)\s*,\s*(-?[0-9.]+)\s*,\s*(-?[0-9.]+)\s*\]", RegexOptions.CultureInvariant);

        /// <summary>A row's <see cref="RunsOption"/> as segments (world metres; the heights are the data's record).</summary>
        public static List<Vector2[]> RunsOf(KeyScenePiece p, string what)
        {
            string json = null;
            foreach (KeySceneOption o in p.Options ?? Array.Empty<KeySceneOption>())
                if (o != null && o.Key == RunsOption) json = o.Value;
            if (string.IsNullOrEmpty(json)) throw new Refusal($"{what}: a run piece with no '{RunsOption}'.");
            MatchCollection m = RunPointRx.Matches(json);
            if (m.Count == 0 || m.Count % 2 != 0) throw new Refusal($"{what}: its '{RunsOption}' is not segments of two points.");
            var segments = new List<Vector2[]>();
            for (int i = 0; i < m.Count; i += 2)
                segments.Add(new[] { PointOf(m[i]), PointOf(m[i + 1]) });
            return segments;
        }

        static Vector2 PointOf(Match m) => new Vector2(
            float.Parse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture),
            float.Parse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture));

        /// <summary>
        /// Resolves a yard piece. A single piece fills <paramref name="at"/>'s picture and walls and returns false (the
        /// caller sorts it like any cell); a run piece fills its parts and walls, sorted, and returns true.
        /// </summary>
        static bool PlaceProp(KeyScenePiece p, IKeySceneAssets assets, KeyScenePlacement at, string what)
        {
            if (!(assets is IKeyScenePropAssets props) || !props.TryPropKit(p.Kit, out PropPieceKitDef kit) || kit == null)
                throw new Refusal($"{what}: no PropPieceKitDef for the kit '{p.Kit}'.");
            PropPieceKitEntry e = kit.EntryFor(p.Piece) ?? throw new Refusal($"{what}: the kit '{kit.Id}' has no piece '{p.Piece}'.");
            PropPieceDef main = e.Main, alternate = e.Alternate;
            int every = e.AlternateEvery;
            PropPieceDressing dressing = kit.DressingFor(p.Id);
            if (dressing != null)
            {
                main = dressing.Main != null ? dressing.Main : main;
                alternate = dressing.Alternate;
                every = dressing.AlternateEvery;
            }
            if (main == null || main.Sprites == null || main.Sprites.Length == 0 || main.Sprites[0] == null)
                throw new Refusal($"{what}: the kit's '{p.Piece}' has no picture.");
            at.Def = DefRef(main);

            if (!main.IsRun)
            {
                at.Sprite = props.RefOf(main.Sprites[0]);
                at.Cell = main.Id;
                if (main.IsFloor) at.Floor = true;
                foreach (PropPieceShape s in main.Colliders ?? Array.Empty<PropPieceShape>())
                {
                    if (s?.Points == null || s.Points.Length < 3) throw new Refusal($"{what}: '{main.Id}' has a collider of under three points.");
                    at.Walls.Add((Vector2[])s.Points.Clone());
                }
                return false;
            }

            List<Vector2[]> runs = RunsOf(p, what);
            PropRunLayout.Layout layout = main.IsFloor
                ? PropRunLayout.Walk(runs, main.PlanLengthMetres, e.End != null ? e.End.PlanLengthMetres : 0f,
                                     e.CornerTurnDegrees, e.Bend != null, e.End != null)
                : PropRunLayout.Fence(runs, main.PlanLengthMetres, alternate != null ? every : 0, e.CornerTurnDegrees,
                                      e.GateGapMaxMetres, e.Gate != null ? e.Gate.PlanLengthMetres : 0f, e.GateSwingDegrees,
                                      main.RunColliderThicknessMetres, e.Gate != null);
            at.Parts = new List<KeyScenePart>();
            at.Floor = main.IsFloor;
            at.Cell = $"{layout.Parts.Count} pictures";
            int index = 0;
            foreach (PropRunLayout.Part part in layout.Parts)
            {
                PropPieceDef def = part.Role switch
                {
                    PropRunLayout.Role.Bay => main,
                    PropRunLayout.Role.Alternate => alternate,
                    PropRunLayout.Role.Corner => e.Corner,
                    PropRunLayout.Role.End => e.End,
                    PropRunLayout.Role.Gate => e.Gate,
                    PropRunLayout.Role.Bend => e.Bend,
                    _ => null,
                };
                if (def == null || def.Sprites == null || def.Sprites.Length == 0)
                    throw new Refusal($"{what}: the kit's '{p.Piece}' has no {part.Role} picture.");
                bool oriented = def.IsFloor && part.Role == PropRunLayout.Role.End;
                int i = PropRunLayout.PictureFor(part.Direction, def.DirectionStepDegrees, def.Sprites.Length, oriented,
                                                 out bool flipX, out bool flipY);
                Sprite sprite = def.Sprites[i] ?? throw new Refusal($"{what}: '{def.Id}' has no picture {i}.");
                at.Parts.Add(new KeyScenePart
                {
                    Name = $"{p.Id}#{index++:00}",
                    Sprite = props.RefOf(sprite),
                    At = part.At,
                    FlipX = flipX, FlipY = flipY, Floor = def.IsFloor,
                    SortingOrder = def.IsFloor ? SortingBands.DecorFloor
                        : YSortSprite.OrderFor(part.At.y, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                                               SortingBands.DecorFloor, SortingBands.DecorCeiling),
                });
            }
            foreach (Vector2[] wall in layout.Walls)
            {
                var local = new Vector2[wall.Length];
                for (int k = 0; k < wall.Length; k++) local[k] = wall[k] - p.At;
                at.Walls.Add(local);
            }
            return true;
        }

        /// <summary>The key scene's plank walk, whose ground the planting is taken off.</summary>
        public const string PlankWalkId = "path.stp_ne_plank";

        /// <summary>The plank walk's ground as walked lines: each run segment, reaching half the planks' width and a
        /// tuft's half width past it (<see cref="TroddenMarginMetres"/>, as the footbridge's paths).</summary>
        public static List<Trodden> PlankWalkGround(IEnumerable<KeySceneDef> scenes, float widthMetres)
        {
            var all = new List<Trodden>();
            foreach (KeySceneDef ks in scenes)
            foreach (KeyScenePiece p in ks.Pieces ?? Array.Empty<KeyScenePiece>())
            {
                if (p == null || p.Id != PlankWalkId) continue;
                foreach (Vector2[] s in RunsOf(p, p.Id))
                    all.Add(new Trodden { A = s[0], B = s[1], Reach = widthMetres * 0.5f + TroddenMarginMetres, Who = p.Id });
            }
            return all;
        }

        /// <summary>
        /// Take the planting off the plank walk: the field's ground grid is closed under it and a plant set down by
        /// hand on it is taken off the list. Idempotent. Batch:
        /// <c>-executeMethod HiddenHarbours.App.Editor.StPetersLayerRefresh.ApplyPlankWalkGround</c>.
        /// </summary>
        public static void ApplyPlankWalkGround()
        {
            List<Trodden> ground = PlankWalkGround(LoadStPetersKeyScenes(), PlankWidthMetres);
            if (ground.Count == 0) throw new Refusal($"no {PlankWalkId} run in St Peters' key scenes.");
            var data = AssetDatabase.LoadAssetAtPath<PlantFieldGroundData>(StPetersBuildingClearance.GroundPath);
            var definition = AssetDatabase.LoadAssetAtPath<PlantFieldDefinition>(StPetersBuildingClearance.DefinitionPath);
            float cell = definition.CellSize;
            byte[] map = data.Decode();
            float pad = cell * 0.70710678f;
            int changed = 0, walked = 0;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            foreach (Trodden g in ground)
            {
                minX = Mathf.Min(minX, Mathf.Min(g.A.x, g.B.x) - g.Reach); maxX = Mathf.Max(maxX, Mathf.Max(g.A.x, g.B.x) + g.Reach);
                minY = Mathf.Min(minY, Mathf.Min(g.A.y, g.B.y) - g.Reach); maxY = Mathf.Max(maxY, Mathf.Max(g.A.y, g.B.y) + g.Reach);
            }
            int x0 = Mathf.Max(0, Mathf.FloorToInt((minX - definition.Origin.x) / cell));
            int x1 = Mathf.Min(data.Width - 1, Mathf.FloorToInt((maxX - definition.Origin.x) / cell));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((minY - definition.Origin.y) / cell));
            int y1 = Mathf.Min(data.Height - 1, Mathf.FloorToInt((maxY - definition.Origin.y) / cell));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var centre = new Vector2(definition.Origin.x + (x + 0.5f) * cell, definition.Origin.y + (y + 0.5f) * cell);
                if (!ground.Any(g => g.Holds(centre, pad))) continue;
                walked++;
                int at = y * data.Width + x;
                if (map[at] == 255) continue;
                map[at] = 255; changed++;
            }
            if (changed > 0)
            {
                data.Payload = StPetersBuildingClearance.Encode(map);
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
            }
            var uprooted = definition.Authored.Where(a => ground.Any(g => g.Holds(a.At))).ToArray();
            if (uprooted.Length > 0)
            {
                var gone = new HashSet<string>(uprooted.Select(a => a.Id));
                definition.Authored = definition.Authored.Where(a => !gone.Contains(a.Id)).ToArray();
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssetIfDirty(definition);
            }
            Debug.Log($"[StPetersPlankWalkGround] {walked} cell(s) of {cell} m walked, {changed} newly closed, " +
                      $"{uprooted.Length} authored plant(s) removed ({string.Join(", ", uprooted.Select(a => a.Id))}), {definition.Authored.Length} kept");
        }

        /// <summary>The planks' width across the walk (m): the recipe's PLANK.width.</summary>
        public const float PlankWidthMetres = 0.7f;

        static ObjRef DefRef(UnityEngine.Object o) =>
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string guid, out long id) ? new ObjRef(id, guid, 2) : default;

        public sealed partial class EditorKeySceneAssets : IKeyScenePropAssets
        {
            public bool TryPropKit(string kit, out PropPieceKitDef def)
            {
                def = null;
                foreach (string g in AssetDatabase.FindAssets("t:" + nameof(PropPieceKitDef)))
                {
                    var k = AssetDatabase.LoadAssetAtPath<PropPieceKitDef>(AssetDatabase.GUIDToAssetPath(g));
                    if (k == null || k.Kit != kit) continue;
                    if (def != null) throw new Refusal($"two PropPieceKitDefs are the kit '{kit}'.");
                    def = k;
                }
                return def != null;
            }

            public SpriteRef RefOf(Sprite sprite) => EditorShoreRockSprites.RefOf(sprite);
        }
    }
}
#endif
