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

            /// <summary>The picture itself (the bake step reads its pixels; never written to the scene).</summary>
            public Sprite Source;
        }

        /// <summary>The yard pieces' kit through the AssetDatabase (a test's assets may not have it).</summary>
        public interface IKeyScenePropAssets
        {
            bool TryPropKit(string kit, out PropPieceKitDef def);
            SpriteRef RefOf(Sprite sprite);

            /// <summary>The sun's shadow a standing yard piece casts (<see cref="SpriteShadow"/>).</summary>
            ScriptRef Shadow { get; }
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
        /// caller sorts it like any cell); a run piece fills its parts and walls, sorted, and returns true. A row's
        /// dressing may swap its pictures, nudge a single piece (<see cref="PropPieceDressing.Offset"/>) and lay more of
        /// the kit with a run (<see cref="PropPieceDressing.Extras"/>: beds in a garden, a walk to its gate), and a
        /// floor run the bake step baked is drawn as its few strips (<see cref="PropPieceKitDef.BakedRuns"/>).
        /// </summary>
        static bool PlaceProp(KeyScenePiece p, IKeySceneAssets assets, KeyScenePlacement at, string what) =>
            PlaceProp(p, assets, at, what, useBakes: true);

        static bool PlaceProp(KeyScenePiece p, IKeySceneAssets assets, KeyScenePlacement at, string what, bool useBakes)
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
            PropPieceExtra[] extras = dressing?.Extras ?? Array.Empty<PropPieceExtra>();

            if (!main.IsRun)
            {
                if (extras.Length > 0) throw new Refusal($"{what}: extras are laid with a run piece, not a single one.");
                at.At += dressing?.Offset ?? Vector2.zero;
                at.Sprite = props.RefOf(main.Sprites[0]);
                at.Cell = main.Id;
                if (main.IsFloor) at.Floor = true;
                at.CastsShadow = main.CastsShadow && !main.IsFloor;
                foreach (PropPieceShape s in main.Colliders ?? Array.Empty<PropPieceShape>())
                {
                    if (s?.Points == null || s.Points.Length < 3) throw new Refusal($"{what}: '{main.Id}' has a collider of under three points.");
                    at.Walls.Add((Vector2[])s.Points.Clone());
                }
                return false;
            }
            if (dressing != null && dressing.Offset != Vector2.zero)
                throw new Refusal($"{what}: an offset moves a single piece; a run lies on its row's line.");

            List<Vector2[]> runs = RunsOf(p, what);
            PropRunLayout.Layout layout = main.IsFloor
                ? PropRunLayout.Walk(runs, main.PlanLengthMetres, e.End != null ? e.End.PlanLengthMetres : 0f,
                                     e.CornerTurnDegrees, e.Bend != null, e.End != null)
                : PropRunLayout.Fence(runs, main.PlanLengthMetres, alternate != null ? every : 0, e.CornerTurnDegrees,
                                      e.GateGapMaxMetres, e.Gate != null ? e.Gate.PlanLengthMetres : 0f, e.GateSwingDegrees,
                                      main.RunColliderThicknessMetres, e.Gate != null);
            at.Parts = new List<KeyScenePart>();
            at.Floor = main.IsFloor;
            int index = 0;
            AddRunParts(p, e, main, alternate, layout, props, at.Parts, ref index, what);
            foreach (Vector2[] wall in layout.Walls)
            {
                var local = new Vector2[wall.Length];
                for (int k = 0; k < wall.Length; k++) local[k] = wall[k] - p.At;
                at.Walls.Add(local);
            }
            foreach (PropPieceExtra x in extras) LayExtra(p, kit, x, props, at, ref index, what);
            if (useBakes) at.Parts = WithBakedFloor(p.Id, at.Parts, kit, props, what);
            at.Cell = $"{at.Parts.Count} pictures";
            return true;
        }

        /// <summary>A layout's parts as pictures, each named for its row and drawn by its role's Def.</summary>
        static void AddRunParts(KeyScenePiece p, PropPieceKitEntry e, PropPieceDef main, PropPieceDef alternate,
                                PropRunLayout.Layout layout, IKeyScenePropAssets props, List<KeyScenePart> parts, ref int index, string what)
        {
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
                    throw new Refusal($"{what}: the kit's '{e.Piece}' has no {part.Role} picture.");
                bool oriented = def.IsFloor && part.Role == PropRunLayout.Role.End;
                int i = PropRunLayout.PictureFor(part.Direction, def.DirectionStepDegrees, def.Sprites.Length, oriented,
                                                 out bool flipX, out bool flipY);
                Sprite sprite = def.Sprites[i] ?? throw new Refusal($"{what}: '{def.Id}' has no picture {i}.");
                parts.Add(PartOf(p.Id, index++, props.RefOf(sprite), sprite, part.At, flipX, flipY, def.IsFloor));
            }
        }

        static KeyScenePart PartOf(string row, int index, SpriteRef sprite, Sprite source, Vector2 at, bool flipX, bool flipY, bool floor) =>
            new KeyScenePart
            {
                Name = $"{row}#{index:00}",
                Sprite = sprite, Source = source,
                At = at,
                FlipX = flipX, FlipY = flipY, Floor = floor,
                SortingOrder = floor ? SortingBands.DecorFloor
                    : YSortSprite.OrderFor(at.y, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                                           SortingBands.DecorFloor, SortingBands.DecorCeiling),
            };

        /// <summary>One of a row's extras laid as its parts: a single piece at its point (its walls joining the row's),
        /// or a floor run along its line.</summary>
        static void LayExtra(KeyScenePiece p, PropPieceKitDef kit, PropPieceExtra x, IKeyScenePropAssets props,
                             KeyScenePlacement at, ref int index, string what)
        {
            string whatX = $"{what} extra '{x?.Piece}'";
            if (x == null || x.Path == null || x.Path.Length == 0) throw new Refusal($"{whatX}: no point to lay it at.");
            PropPieceKitEntry xe = kit.EntryFor(x.Piece) ?? throw new Refusal($"{whatX}: the kit '{kit.Id}' has no such piece.");
            PropPieceDef def = xe.Main;
            if (def == null || def.Sprites == null || def.Sprites.Length == 0 || def.Sprites[0] == null)
                throw new Refusal($"{whatX}: it has no picture.");
            if (!def.IsRun)
            {
                if (x.Path.Length != 1) throw new Refusal($"{whatX}: a single piece takes one point, not {x.Path.Length}.");
                at.Parts.Add(PartOf(p.Id, index++, props.RefOf(def.Sprites[0]), def.Sprites[0], p.At + x.Path[0], false, false, def.IsFloor));
                foreach (PropPieceShape s in def.Colliders ?? Array.Empty<PropPieceShape>())
                {
                    if (s?.Points == null || s.Points.Length < 3) throw new Refusal($"{whatX}: '{def.Id}' has a collider of under three points.");
                    at.Walls.Add(s.Points.Select(q => q + x.Path[0]).ToArray());
                }
                return;
            }
            if (!def.IsFloor) throw new Refusal($"{whatX}: a run laid as an extra is a floor run (a fence is its own row).");
            if (x.Path.Length < 2) throw new Refusal($"{whatX}: a run takes two points or more.");
            var runs = new List<Vector2[]>();
            for (int i = 0; i + 1 < x.Path.Length; i++) runs.Add(new[] { p.At + x.Path[i], p.At + x.Path[i + 1] });
            PropRunLayout.Layout layout = PropRunLayout.Walk(runs, def.PlanLengthMetres, xe.End != null ? xe.End.PlanLengthMetres : 0f,
                                                             xe.CornerTurnDegrees, xe.Bend != null, xe.End != null);
            AddRunParts(p, xe, def, null, layout, props, at.Parts, ref index, what);
        }

        /// <summary>
        /// The floor parts of a row cut into strips in the run's order, each no wider or taller than the kit's
        /// <see cref="PropPieceKitDef.BakeStripMaxMetres"/>: what the bake step bakes one picture of. Raised parts are
        /// never baked (each sorts against a walker by its own ground line).
        /// </summary>
        public static List<List<KeyScenePart>> FloorStrips(IReadOnlyList<KeyScenePart> parts, float maxMetres)
        {
            var strips = new List<List<KeyScenePart>>();
            List<KeyScenePart> open = null;
            Vector2 lo = Vector2.zero, hi = Vector2.zero;
            foreach (KeyScenePart part in parts)
            {
                if (!part.Floor) continue;
                if (open != null)
                {
                    Vector2 nlo = Vector2.Min(lo, part.At), nhi = Vector2.Max(hi, part.At);
                    if (nhi.x - nlo.x <= maxMetres && nhi.y - nlo.y <= maxMetres) { open.Add(part); lo = nlo; hi = nhi; continue; }
                }
                open = new List<KeyScenePart> { part };
                strips.Add(open);
                lo = hi = part.At;
            }
            return strips;
        }

        /// <summary>What a strip was baked from: each part's picture (and its file's import), its place to the pixel
        /// and its flips. Two bakes of the same planks agree; a moved or redrawn plank does not.</summary>
        public static string StripSignature(IReadOnlyList<KeyScenePart> strip)
        {
            var sb = new System.Text.StringBuilder();
            foreach (KeyScenePart part in strip)
            {
                string path = part.Source != null ? AssetDatabase.GetAssetPath(part.Source) : "";
                sb.Append(part.Sprite.Sprite.Guid).Append(':').Append(part.Sprite.Sprite.FileId).Append(':')
                  .Append(path.Length > 0 ? AssetDatabase.GetAssetDependencyHash(path).ToString() : "-").Append(':')
                  .Append(Mathf.RoundToInt(part.At.x * BakePixelsPerMetre)).Append(',')
                  .Append(Mathf.RoundToInt(part.At.y * BakePixelsPerMetre)).Append(':')
                  .Append(part.FlipX ? 1 : 0).Append(part.FlipY ? 1 : 0).Append(';');
            }
            using var sha = System.Security.Cryptography.SHA1.Create();
            byte[] h = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(sb.ToString()));
            return BitConverter.ToString(h).Replace("-", "").Substring(0, 16).ToLowerInvariant();
        }

        /// <summary>The pixels per metre a baked strip is laid on (the project's sprite grid).</summary>
        public const int BakePixelsPerMetre = 32;

        /// <summary>A row's floor parts swapped for its baked strips, when the kit holds a bake of it; refuses a bake
        /// that no longer matches the run (bake again) rather than drawing stale planks.</summary>
        static List<KeyScenePart> WithBakedFloor(string row, List<KeyScenePart> parts, PropPieceKitDef kit, IKeyScenePropAssets props, string what)
        {
            PropPieceBakedRun[] baked = (kit.BakedRuns ?? Array.Empty<PropPieceBakedRun>())
                .Where(b => b != null && b.Row == row).OrderBy(b => b.Index).ToArray();
            if (baked.Length == 0) return parts;
            List<List<KeyScenePart>> strips = FloorStrips(parts, kit.BakeStripMaxMetres);
            if (strips.Count != baked.Length)
                throw new Refusal($"{what}: its floor run makes {strips.Count} strip(s) and the kit holds {baked.Length} baked; bake the floor runs again.");
            var result = parts.Where(x => !x.Floor).ToList();
            for (int i = 0; i < strips.Count; i++)
            {
                PropPieceBakedRun b = baked[i];
                if (b.Index != i || b.Sprite == null || b.Signature != StripSignature(strips[i]))
                    throw new Refusal($"{what}: baked strip {i} no longer matches its planks; bake the floor runs again.");
                result.Add(new KeyScenePart
                {
                    Name = $"{row}~{i:00}", Sprite = props.RefOf(b.Sprite), Source = b.Sprite, At = b.At,
                    Floor = true, SortingOrder = SortingBands.DecorFloor,
                });
            }
            return result;
        }

        /// <summary>The key scene's plank walk, whose ground the planting is taken off.</summary>
        public const string PlankWalkId = "path.stp_ne_plank";

        /// <summary>The plank walk's ground as walked lines: each run segment, reaching half the planks' width and a
        /// tuft's half width past it (<see cref="TroddenMarginMetres"/>, as the footbridge's paths).</summary>
        public static List<Trodden> PlankWalkGround(IEnumerable<KeySceneDef> scenes, float widthMetres, PropPieceKitDef kit = null)
        {
            var all = new List<Trodden>();
            foreach (KeySceneDef ks in scenes)
            foreach (KeyScenePiece p in ks.Pieces ?? Array.Empty<KeyScenePiece>())
            {
                if (p == null) continue;
                if (p.Id == PlankWalkId)
                    foreach (Vector2[] s in RunsOf(p, p.Id))
                        all.Add(new Trodden { A = s[0], B = s[1], Reach = widthMetres * 0.5f + TroddenMarginMetres, Who = p.Id });
                // What a row's dressing lays with it (a walk to a gate, beds in a garden) takes its ground too.
                PropPieceDressing d = kit != null && p.Kit == kit.Kit ? kit.DressingFor(p.Id) : null;
                foreach (PropPieceExtra x in d?.Extras ?? Array.Empty<PropPieceExtra>())
                {
                    PropPieceDef def = kit.EntryFor(x.Piece)?.Main;
                    if (def == null || x.Path == null || x.Path.Length == 0) continue;
                    if (def.IsRun)
                        for (int i = 0; i + 1 < x.Path.Length; i++)
                            all.Add(new Trodden { A = p.At + x.Path[i], B = p.At + x.Path[i + 1], Reach = widthMetres * 0.5f + TroddenMarginMetres, Who = p.Id });
                    else
                        foreach (PropPieceShape s in def.Colliders ?? Array.Empty<PropPieceShape>())
                        {
                            // A bed's footprint as a capsule along its longer side.
                            Vector2 lo = s.Points.Aggregate(Vector2.Min), hi = s.Points.Aggregate(Vector2.Max);
                            Vector2 c = p.At + x.Path[0] + (lo + hi) * 0.5f, half = (hi - lo) * 0.5f;
                            float r = Mathf.Min(half.x, half.y), run = Mathf.Max(half.x, half.y) - r;
                            Vector2 axis = half.y >= half.x ? new Vector2(0f, run) : new Vector2(run, 0f);
                            all.Add(new Trodden { A = c - axis, B = c + axis, Reach = r + TroddenMarginMetres, Who = p.Id });
                        }
                }
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
            new EditorKeySceneAssets().TryPropKit(PropPiecesKit, out PropPieceKitDef kit);
            List<Trodden> ground = PlankWalkGround(LoadStPetersKeyScenes(), PlankWidthMetres, kit);
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

            ScriptRef? _shadow;
            public ScriptRef Shadow => _shadow ??= ScriptOf(typeof(SpriteShadow));
        }
    }
}
#endif
