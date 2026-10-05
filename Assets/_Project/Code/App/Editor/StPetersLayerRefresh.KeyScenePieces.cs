#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using HiddenHarbours.Art;                 // SetPieceDef (the kit's pieces), YSortSprite.OrderFor, the two runtime parts
using HiddenHarbours.Core;                // SortingBands — the decor band a piece sorts in
using HiddenHarbours.Tools.RigBaking;     // IsoPackSprites (the iso kits' cells), SetPieceDefBuilder (where a Def lives)
using HiddenHarbours.World;               // KeySceneDef — the placements

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE KEY SCENES' PIECES, PLACED WITHOUT A BUILD()</b> — the refresh's fourth step, a sibling of the
    /// three in <see cref="PlanAll"/> that uses their <see cref="LayerPatch"/> and its <see cref="LayerPatch.Seal"/>.
    ///
    /// <para><b>What it writes.</b> Every piece the St Peters <see cref="KeySceneDef"/>s place today, under
    /// its own root (<see cref="KeyScenesRootName"/>), one group per key scene, as whole YAML documents. Each
    /// piece gets a sprite renderer showing its cell for CD's facing; the sort its Def asks for (a
    /// <c>raised</c> piece is y-sorted by a <see cref="YSortSprite"/>, a <c>floor</c> piece sits at
    /// <see cref="SortingBands.DecorFloor"/>, under every figure); its walk-blocking polygons; and no light.
    /// The iso kits' props follow <c>NineMileCreekDressing</c>: the kit's cell, a sorter, no collider and no
    /// shadow. Two pieces carry a runtime part: the tide board a <see cref="TidalFaceWaterline"/> that lets
    /// the sea cover it to the mark it stands at, the harbour vane a <see cref="SetPieceWindVane"/>.</para>
    ///
    /// <para><b>A pure function of (scene text, assets)</b> (<see cref="KeyScenePieces"/>): no scene is
    /// opened, and every fileID is drawn from the piece's key, so a second plan finds the first one's
    /// documents where it left them and writes nothing. The root is new the first time, so the patch's one
    /// document outside it is the scene's SceneRoots list, which the root joins by one named edit
    /// (<see cref="LayerPatch.Seal"/> holds it to exactly that). Heights are a record in the data, held to the
    /// game's height source by the z table test; nothing here draws with them.</para>
    /// </summary>
    public static partial class StPetersLayerRefresh
    {
        // =====================================================================================
        //  names
        // =====================================================================================

        public const string KeyScenesRootName = "StPetersKeyScenes";
        public const string KeyScenesStep = "KeyScenePieces";

        /// <summary>Where the key scenes' data lives: one <see cref="KeySceneDef"/> per scene.</summary>
        public const string KeySceneFolder = "Assets/_Project/Data/KeyScenes";
        public const string StPetersRegionId = "region.st_peters";

        /// <summary>The three kits a key scene places from: the set pieces (one Def each), and the two iso
        /// packs Nine Mile Creek dresses with.</summary>
        public const string SetPiecesKit = "stPetersSetPieces";
        public const string DecorKit = NineMileCreekDressing.DecorFamily;
        public const string FindsKit = NineMileCreekDressing.FindsFamily;

        /// <summary>The words a <see cref="SetPieceDef"/>'s sidecar uses, as the kit writes them.</summary>
        public const string RaisedLayer = "raised", FloorLayer = "floor";
        public const string GroundMount = "ground";
        public const string PolygonShape = "polygon", BlocksWalk = "walk";
        public const string WindFromInput = "windFromDeg";

        /// <summary>The finds' variant row a key scene shows. CD names none; the first is the kit's own.</summary>
        public const int FindVariant = 0;

        /// <summary>How far, on screen, a tide board's marks may disagree about the foot they were baked against or
        /// the face they are painted on: half a pixel, the most a sea's edge may miss a mark and still read it true.
        /// (The kit paints its spring-high mark a centimetre up the post from the others' foot: a quarter of a
        /// pixel.)</summary>
        public const float TideMarkAgreementPixels = 0.5f;

        const int PolygonCollider2DClass = 60;

        // =====================================================================================
        //  the assets, as the step reads them
        // =====================================================================================

        /// <summary>A MonoBehaviour's script as the scene records it: the reference, and Unity 6's
        /// <c>Assembly::Namespace.Class</c> identifier.</summary>
        public struct ScriptRef
        {
            public ObjRef Script;
            public string ClassIdentifier;

            public ScriptRef(ObjRef script, string classIdentifier) { Script = script; ClassIdentifier = classIdentifier; }
        }

        /// <summary>A <see cref="SetPieceDef"/> as the step reads it: plain values, its frames as the scene
        /// records a sprite.</summary>
        public sealed class KeySceneSetPiece
        {
            public string Id = "";
            public string Layer = RaisedLayer;
            public string Mount = GroundMount;
            public ObjRef Def;
            public SpriteRef[] Frames = Array.Empty<SpriteRef>();
            public SetPieceCollider[] Colliders = Array.Empty<SetPieceCollider>();
            public SetPieceTideMark[] TideMarks = Array.Empty<SetPieceTideMark>();
            public float ElevationDeg;
            public float PixelsPerMetre;
            public bool HasLight;
            public bool IsAnimated;
            public string AnimatedInput = "";
            public int AnimatedRigDir;
        }

        /// <summary>The art the key scenes step writes. The editor reads it through the AssetDatabase
        /// (<see cref="EditorKeySceneAssets"/>); a test hands in its own.</summary>
        public interface IKeySceneAssets
        {
            /// <summary>The iso kit's cell that turns a piece to CD's dir, by the kit's own registered
            /// convention.</summary>
            int IsoFacing(string kit, int dir);

            bool TryIsoCell(string kit, string piece, int facing, out SpriteRef sprite);
            bool TryFind(string find, string state, int lie, int variant, out SpriteRef sprite);
            bool TrySetPiece(string rigKey, out KeySceneSetPiece piece);

            /// <summary>The material a new SpriteRenderer gets.</summary>
            ObjRef SpriteMaterial { get; }

            /// <summary>The material <see cref="TidalFaceWaterline"/> puts its face on.</summary>
            ObjRef FaceMaterial { get; }

            ScriptRef Sorter { get; }
            ScriptRef Waterline { get; }
            ScriptRef Vane { get; }
        }

        // =====================================================================================
        //  where each piece goes, and how it draws
        // =====================================================================================

        /// <summary>One placed piece, resolved: everything its documents record.</summary>
        public sealed class KeyScenePlacement
        {
            public string Scene;
            public string Id;
            public string Kit;
            public string Piece;
            public Vector2 At;
            public string Cell;
            public SpriteRef Sprite;
            public ObjRef Material;
            public bool Floor;
            public float SortYOffset;
            public int SortingOrder;
            public List<Vector2[]> Walls = new List<Vector2[]>();
            public bool RidesTheTide;
            public float LipWorldY;
            public float LipElevation;
            public bool TurnsWithWind;
            public ObjRef Def;
            public string LeftOut = "";
        }

        /// <summary>The compass heading (N = 0, clockwise) a kit's dir faces: the kits' dirs turn
        /// counterclockwise from north (0 N, 1 NW, 2 W … 4 S … 7 NE).</summary>
        public static float KitDirHeading(int dir) => ((8 - ((dir % 8) + 8) % 8) % 8) * 45f;

        /// <summary>How far a y-sorted piece's sort line sits from its ground point: CD's own line where CD
        /// gives one, else the ground point itself.</summary>
        public static float SortYOffsetFor(KeyScenePiece p) => p.HasSortY ? p.SortY - p.At.y : 0f;

        /// <summary>
        /// A tide board's waterline: the world row its marks' face meets the height its foot was baked at, and
        /// that height. Each mark knows both (<c>Z</c> is its height, <c>At.z</c> how far up the post it is
        /// painted, <c>At.y</c> the face it is painted on), and all must agree to within half a pixel on screen,
        /// or no one waterline reads them true; the lip is what the marks agree on (their median). The face's plan
        /// offset turns with the facing and rises on screen by <c>sin(elevation)</c>; a height rises by
        /// <c>cos(elevation)</c>.
        /// </summary>
        public static bool TryTideLip(KeySceneSetPiece sp, int rigDir, Vector2 at, out float lipWorldY, out float lipElevation)
        {
            lipWorldY = lipElevation = 0f;
            if (sp.TideMarks == null || sp.TideMarks.Length == 0) return false;
            if (!(sp.PixelsPerMetre > 0f))
                throw new Refusal($"{KeyScenesStep}: '{sp.Id}' has tide marks but no scale, so no pixel to read them to.");
            double e = sp.ElevationDeg * Math.PI / 180.0;
            var feet = new float[sp.TideMarks.Length];
            var rises = new float[sp.TideMarks.Length];
            for (int i = 0; i < sp.TideMarks.Length; i++)
            {
                SetPieceTideMark m = sp.TideMarks[i] ?? throw new Refusal($"{KeyScenesStep}: '{sp.Id}' has an empty tide mark.");
                feet[i] = m.Z - m.At.z;
                rises[i] = (float)(SetPieceDef.ToWorld(new Vector2(m.At.x, m.At.y), rigDir).y * Math.Sin(e));
            }
            float foot = Median(feet), rise = Median(rises);
            for (int i = 0; i < feet.Length; i++)
            {
                double footPixels = Math.Abs(feet[i] - foot) * Math.Cos(e) * sp.PixelsPerMetre;
                double risePixels = Math.Abs(rises[i] - rise) * sp.PixelsPerMetre;
                if (footPixels > TideMarkAgreementPixels || risePixels > TideMarkAgreementPixels)
                    throw new Refusal($"{KeyScenesStep}: '{sp.Id}''s tide marks disagree about their foot or face by more than " +
                                      $"half a pixel (mark {i}: foot {F(feet[i])}, rise {F(rises[i])}; the marks' median: {F(foot)}, {F(rise)}).");
            }
            lipWorldY = at.y + rise;
            lipElevation = foot;
            return true;
        }

        /// <summary>The middle value, or the mean of the middle two: what most of the values agree on.</summary>
        static float Median(float[] values)
        {
            var sorted = (float[])values.Clone();
            Array.Sort(sorted);
            int n = sorted.Length;
            return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2f;
        }

        /// <summary>A set piece's walk-blocking polygons, turned to the facing a frame shows (the Def's own
        /// <see cref="SetPieceDef.ColliderAt"/>), in metres from its ground point.</summary>
        public static List<Vector2[]> WallsOf(SetPieceCollider[] colliders, int frame, string what)
        {
            var walls = new List<Vector2[]>();
            int dir = SetPieceDef.RigDirForFrame(frame);
            foreach (SetPieceCollider c in colliders ?? Array.Empty<SetPieceCollider>())
            {
                if (c == null) throw new Refusal($"{what}: an empty collider.");
                if (c.Type != PolygonShape) throw new Refusal($"{what}: a '{c.Type}' collider; the step writes polygons only.");
                if (c.Blocks != BlocksWalk) throw new Refusal($"{what}: a collider that blocks '{c.Blocks}'; the step writes walk-blocking ones only.");
                if (c.Points == null || c.Points.Length < 3) throw new Refusal($"{what}: a collider of {c.Points?.Length ?? 0} points.");
                walls.Add(c.Points.Select(pt => SetPieceDef.ToWorld(pt, dir)).ToArray());
            }
            return walls;
        }

        /// <summary>Every piece St Peters' key scenes place today, resolved, in scene then CD order.
        /// Refuses what the step cannot place truly rather than placing it wrong.</summary>
        public static List<KeyScenePlacement> PlaceKeyScenes(IReadOnlyList<KeySceneDef> keyScenes, IKeySceneAssets assets)
        {
            if (keyScenes == null || keyScenes.Count == 0) throw new Refusal($"{KeyScenesStep}: no key scenes to place.");
            var placed = new List<KeyScenePlacement>();
            var sceneIds = new HashSet<string>(StringComparer.Ordinal);
            var pieceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeySceneDef ks in keyScenes)
            {
                if (ks == null) throw new Refusal($"{KeyScenesStep}: an empty key scene.");
                if (!sceneIds.Add(Plain(ks.Id))) throw new Refusal($"{KeyScenesStep}: two key scenes are '{ks.Id}'.");
                foreach (KeyScenePiece p in ks.Pieces ?? Array.Empty<KeyScenePiece>())
                {
                    if (p == null) throw new Refusal($"{KeyScenesStep}: '{ks.Id}' holds an empty piece.");
                    if (p.Variants == null || !p.Variants.Contains(KeySceneDef.Today)) continue;
                    if (!pieceIds.Add(Plain(p.Id))) throw new Refusal($"{KeyScenesStep}: '{p.Id}' is placed twice.");
                    placed.Add(Place(ks.Id, p, assets));
                }
            }
            return placed;
        }

        static KeyScenePlacement Place(string scene, KeyScenePiece p, IKeySceneAssets assets)
        {
            string what = $"{KeyScenesStep}: '{p.Id}' ({p.Kit} {p.Piece})";
            var at = new KeyScenePlacement
            {
                Scene = scene, Id = p.Id, Kit = p.Kit, Piece = p.Piece, At = p.At, Material = assets.SpriteMaterial,
            };
            switch (p.Kit)
            {
                case SetPiecesKit:
                {
                    if (!assets.TrySetPiece(p.Piece, out KeySceneSetPiece sp) || sp == null)
                        throw new Refusal($"{what}: the kit has no SetPieceDef '{p.Piece}'.");
                    if (sp.Id != p.Id) throw new Refusal($"{what}: its SetPieceDef is '{sp.Id}'; a set piece is placed under its Def's id.");
                    if (sp.Mount != GroundMount)
                        throw new Refusal($"{what}: mounted on '{sp.Mount}'. A mounted piece stands on its mount's picture (ADR 0042), which this step does not place.");
                    if (sp.Frames == null || sp.Frames.Length != SetPieceDef.Facings)
                        throw new Refusal($"{what}: {sp.Frames?.Length ?? 0} frames; a set piece bakes {SetPieceDef.Facings}.");
                    int frame = SetPieceDef.FrameForRigDir(p.Dir);
                    at.Sprite = sp.Frames[frame];
                    at.Cell = $"frame {frame} (dir {SetPieceDef.RigDirForFrame(frame)})";
                    if (sp.Layer == FloorLayer) at.Floor = true;
                    else if (sp.Layer != RaisedLayer) throw new Refusal($"{what}: layer '{sp.Layer}'; the step knows '{RaisedLayer}' and '{FloorLayer}'.");
                    at.Walls = WallsOf(sp.Colliders, frame, what);
                    if (TryTideLip(sp, SetPieceDef.RigDirForFrame(frame), p.At, out float lipY, out float lipZ))
                    {
                        at.RidesTheTide = true;
                        at.LipWorldY = lipY;
                        at.LipElevation = lipZ;
                    }
                    if (sp.IsAnimated)
                    {
                        if (sp.AnimatedInput != WindFromInput)
                            throw new Refusal($"{what}: its moving part turns with '{sp.AnimatedInput}'; the step knows '{WindFromInput}' only.");
                        if (SetPieceDef.RigDirForFrame(frame) != sp.AnimatedRigDir)
                            throw new Refusal($"{what}: placed at dir {p.Dir}, but its headings were rendered at dir {sp.AnimatedRigDir}; the vane would jump when the wind first turns it.");
                        if (string.IsNullOrEmpty(sp.Def.Guid)) throw new Refusal($"{what}: its SetPieceDef is not an asset.");
                        at.TurnsWithWind = true;
                        at.Def = sp.Def;
                    }
                    if (sp.HasLight) at.LeftOut = "its light (the Def carries one; the step places none)";
                    break;
                }
                case DecorKit:
                {
                    int facing = assets.IsoFacing(p.Kit, p.Dir);
                    if (!assets.TryIsoCell(p.Kit, p.Piece, facing, out at.Sprite))
                        throw new Refusal($"{what}: the kit's '{p.Piece}' sheet has no cell {facing}.");
                    at.Cell = $"cell {facing}";
                    break;
                }
                case FindsKit:
                {
                    if (string.IsNullOrEmpty(p.State)) throw new Refusal($"{what}: a find needs its state (wet, dry, bleached).");
                    if (!assets.TryFind(p.Piece, p.State, p.Dir, FindVariant, out at.Sprite))
                        throw new Refusal($"{what}: the kit has no '{p.Piece}_{p.State}' cell for lie {p.Dir}, variant {FindVariant}.");
                    at.Cell = $"{p.State}, lie {p.Dir}";
                    break;
                }
                default:
                    throw new Refusal($"{what}: the step places from {SetPiecesKit}, {DecorKit} and {FindsKit} only.");
            }
            if (at.Sprite.Sprite.FileId == 0 || string.IsNullOrEmpty(at.Sprite.Sprite.Guid))
                throw new Refusal($"{what}: its cell is not a sprite asset.");
            if (at.RidesTheTide) at.Material = assets.FaceMaterial;

            if (at.Floor) at.SortingOrder = SortingBands.DecorFloor;
            else
            {
                at.SortYOffset = SortYOffsetFor(p);
                at.SortingOrder = YSortSprite.OrderFor(p.At.y + at.SortYOffset, SortingBands.DecorBase,
                                                       SortingBands.OrdersPerMetre, SortingBands.DecorFloor, SortingBands.DecorCeiling);
            }
            return at;
        }

        // =====================================================================================
        //  the step
        // =====================================================================================

        /// <summary>
        /// The key scenes' pieces as one patch to the <see cref="KeyScenesRootName"/> root: the root, one
        /// group per key scene, and every piece the scenes place today, as whole documents. What already
        /// stands as written is left alone; what changed is edited, what is gone deleted, each by name; a
        /// new root joins the scene's SceneRoots list. Sealed before it is returned.
        /// </summary>
        public static LayerPatch KeyScenePieces(SceneYaml scene, IReadOnlyList<KeySceneDef> keyScenes, IKeySceneAssets assets)
        {
            List<KeyScenePlacement> pieces = PlaceKeyScenes(keyScenes, assets);
            ScriptRef sorter = assets.Sorter, waterline = assets.Waterline, vane = assets.Vane;

            Doc root = KeyScenesRootOf(scene);
            HashSet<long> existing = root == null ? new HashSet<long>() : scene.SubtreeOf(root.FileId);
            var ids = new IdAllocator(scene, existing);
            long rootGo = ids.Next(KeyOf("GameObject")), rootTr = ids.Next(KeyOf("Transform"));
            if (root != null && root.FileId != rootGo)
                throw new Refusal($"{KeyScenesStep}: the scene's '{KeyScenesRootName}' (&{root.FileId}) is not the one this step writes " +
                                  $"(&{rootGo}); it was made by hand. Move its work elsewhere, or delete it, before the step runs.");

            var want = new List<Wanted>();
            var groupTrs = new List<long>();
            foreach (KeySceneDef ks in keyScenes)
            {
                long gGo = ids.Next(KeyOf(ks.Id, "GameObject")), gTr = ids.Next(KeyOf(ks.Id, "Transform"));
                var pieceTrs = new List<long>();
                var pieceDocs = new List<Wanted>();
                foreach (KeyScenePlacement p in pieces.Where(x => x.Scene == ks.Id))
                {
                    long go = ids.Next(KeyOf(ks.Id, p.Id, "GameObject")), tr = ids.Next(KeyOf(ks.Id, p.Id, "Transform"));
                    long sr = ids.Next(KeyOf(ks.Id, p.Id, "SpriteRenderer"));
                    long sort = p.Floor ? 0 : ids.Next(KeyOf(ks.Id, p.Id, "YSortSprite"));
                    long walls = p.Walls.Count == 0 ? 0 : ids.Next(KeyOf(ks.Id, p.Id, "PolygonCollider2D"));
                    long face = p.RidesTheTide ? ids.Next(KeyOf(ks.Id, p.Id, "TidalFaceWaterline")) : 0;
                    long turn = p.TurnsWithWind ? ids.Next(KeyOf(ks.Id, p.Id, "SetPieceWindVane")) : 0;
                    var comps = new List<long> { tr, sr };
                    foreach (long c in new[] { sort, walls, face, turn }) if (c != 0) comps.Add(c);

                    pieceDocs.Add(Want(GameObjectYaml(go, p.Id, comps), p.Id, "GameObject"));
                    pieceDocs.Add(Want(TransformYaml(tr, go, p.At, new List<long>(), gTr), p.Id, "Transform"));
                    pieceDocs.Add(Want(SpriteRendererYaml(sr, go, p.Material, p.Sprite, p.SortingOrder), p.Id, "SpriteRenderer"));
                    if (sort != 0)
                        pieceDocs.Add(Want(MonoBehaviourYaml(sort, go, sorter, new[]
                        {
                            "  _dynamic: 0",
                            "  _baseOrder: " + F(SortingBands.DecorBase),
                            "  _orderPerUnit: " + F(SortingBands.OrdersPerMetre),
                            "  _minOrder: " + Int(SortingBands.DecorFloor),
                            "  _maxOrder: " + Int(SortingBands.DecorCeiling),
                            "  _pivotYOffset: " + F(p.SortYOffset),
                        }), p.Id, "YSortSprite"));
                    if (walls != 0) pieceDocs.Add(Want(PolygonColliderYaml(walls, go, p.Walls), p.Id, "PolygonCollider2D"));
                    if (face != 0)
                        pieceDocs.Add(Want(MonoBehaviourYaml(face, go, waterline, new[]
                        {
                            "  _lipWorldY: " + F(p.LipWorldY),
                            "  _lipElevation: " + F(p.LipElevation),
                            "  _ridesTheTide: 1",
                            "  _configured: 1",
                        }), p.Id, "TidalFaceWaterline"));
                    if (turn != 0)
                        pieceDocs.Add(Want(MonoBehaviourYaml(turn, go, vane, new[]
                        {
                            "  _def: " + p.Def.Yaml,
                            "  _refreshHz: " + F(SetPieceWindVane.DefaultRefreshHz),
                            "  _calmMetresPerSecond: " + F(SetPieceWindVane.DefaultCalmMetresPerSecond),
                        }), p.Id, "SetPieceWindVane"));
                    pieceTrs.Add(tr);
                }
                want.Add(Want(GameObjectYaml(gGo, ks.Id, new List<long> { gTr }), ks.Id, "GameObject"));
                want.Add(Want(TransformYaml(gTr, gGo, Vector2.zero, pieceTrs, rootTr), ks.Id, "Transform"));
                want.AddRange(pieceDocs);
                groupTrs.Add(gTr);
            }
            want.Insert(0, Want(GameObjectYaml(rootGo, KeyScenesRootName, new List<long> { rootTr }), KeyScenesRootName, "GameObject"));
            want.Insert(1, Want(TransformYaml(rootTr, rootGo, Vector2.zero, groupTrs, 0), KeyScenesRootName, "Transform"));

            var patch = new LayerPatch(KeyScenesStep, KeyScenesRootName, rootGo);
            var wanted = new HashSet<long>(want.Select(w => w.FileId));
            var removed = new List<string>();
            foreach (Doc d in scene.Docs)
            {
                if (!existing.Contains(d.FileId) || wanted.Contains(d.FileId)) continue;
                string name = NameOfExisting(scene, d);
                patch.Delete(d, name);
                if (d.ClassId == GameObjectClass) removed.Add(d.Field("m_Name") ?? "?");
            }

            var changed = new HashSet<string>(StringComparer.Ordinal);
            var added = new HashSet<string>(StringComparer.Ordinal);
            foreach (Wanted w in want)
            {
                Doc now = scene.Get(w.FileId);
                if (now == null)
                {
                    patch.Add(w.Text, w.Name);
                    added.Add(w.Owner);
                    continue;
                }
                if (!existing.Contains(w.FileId))
                    throw new Refusal($"{KeyScenesStep}: &{w.FileId} ({w.Name}) is taken outside the root.");
                if (now.ClassId != w.ClassId)
                    throw new Refusal($"{KeyScenesStep}: &{w.FileId} holds a {now.Lines[1].TrimEnd(':')} where the step writes {w.Name}; it was changed by hand.");
                if (now.Text == w.Text) continue;
                patch.Edit(now, w.Text, w.Name);
                changed.Add(w.Owner);
            }

            if (root == null)
            {
                Doc list = SceneRootsOf(scene);
                patch.Edit(list, WithRootListed(list.Text, rootTr), $"the scene's SceneRoots list ('{KeyScenesRootName}' joins it)");
                patch.Note(KeyScenesRootName, "a new root", "joins the scene's SceneRoots list");
            }
            foreach (KeyScenePlacement p in pieces)
            {
                string how = $"{p.Kit} {p.Piece}, {p.Cell}, at {At(p.At)}, order {Int(p.SortingOrder)}" +
                             (p.Floor ? " (floor)" : "") + (p.Walls.Count > 0 ? $", {p.Walls.Count} wall(s)" : "") +
                             (p.RidesTheTide ? $", the sea cuts it from its foot at {F(p.LipElevation)} m (lip y {F(p.LipWorldY)})" : "") +
                             (p.TurnsWithWind ? ", turns with the wind" : "");
                if (added.Contains(p.Id)) patch.Note(p.Id, "placed", how);
                else if (changed.Contains(p.Id)) patch.Note(p.Id, "re-placed", how);
                if (p.LeftOut.Length > 0 && (added.Contains(p.Id) || changed.Contains(p.Id))) patch.Note(p.Id, "left out", p.LeftOut);
            }
            foreach (string name in removed) patch.Note(name, "removed", "no key scene places it today");
            return patch.Seal(scene);
        }

        /// <summary>The scene's key scenes root, or null when it has none yet (refuses two, or one below the top).</summary>
        static Doc KeyScenesRootOf(SceneYaml scene) =>
            scene.Docs.Any(d => d.ClassId == GameObjectClass && !d.Stripped && d.Field("m_Name") == KeyScenesRootName)
                ? scene.RootNamed(KeyScenesRootName)
                : null;

        static string KeyOf(params string[] parts) => KeyScenesRootName + "/" + string.Join("/", parts);

        /// <summary>A document the step wants: its text, its name in the patch, and the object it belongs to.</summary>
        sealed class Wanted
        {
            public readonly long FileId;
            public readonly int ClassId;
            public readonly string Text, Name, Owner;

            public Wanted(string text, string owner, string kind)
            {
                var d = new Doc(text);
                FileId = d.FileId; ClassId = d.ClassId; Text = text; Owner = owner; Name = $"'{owner}' ({kind})";
            }
        }

        static Wanted Want(string text, string owner, string kind) => new Wanted(text, owner, kind);

        /// <summary>A document the step deletes, by its object's name and where that object stood, which is
        /// enough to pick it out under the root.</summary>
        static string NameOfExisting(SceneYaml scene, Doc d)
        {
            Doc go = d.ClassId == GameObjectClass ? d : scene.Get(d.FieldRef("m_GameObject"));
            string kind = d.ClassId == GameObjectClass ? "GameObject"
                : d.ClassId == MonoBehaviourClass ? (d.Field("m_EditorClassIdentifier") ?? "MonoBehaviour").Split('.').Last()
                : d.Lines.Length > 1 ? d.Lines[1].TrimEnd(':') : "document";
            string where = "";
            if (go != null)
            {
                Vector3 p = ParseVec3(scene.TransformOf(go).Field("m_LocalPosition"));
                where = $" @({F(p.x)}, {F(p.y)})";
            }
            return $"'{go?.Field("m_Name") ?? "?"}' ({kind}){where}";
        }

        // =====================================================================================
        //  the documents, as Unity writes them
        // =====================================================================================

        static string Header(int cls, long id, string type) => $"--- !u!{Int(cls)} &{Id(id)}\n{type}:";
        static string Id(long id) => id.ToString(System.Globalization.CultureInfo.InvariantCulture);

        const string Unlinked = "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n" +
                                "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}";

        static string GameObjectYaml(long id, string name, IList<long> components)
        {
            var lines = new List<string> { Header(GameObjectClass, id, "GameObject"), Unlinked, "  serializedVersion: 6", "  m_Component:" };
            foreach (long c in components) lines.Add($"  - component: {{fileID: {Id(c)}}}");
            lines.Add("  m_Layer: 0");
            lines.Add("  m_Name: " + Plain(name));
            lines.Add("  m_TagString: Untagged");
            lines.Add("  m_Icon: {fileID: 0}");
            lines.Add("  m_NavMeshLayer: 0");
            lines.Add("  m_StaticEditorFlags: 0");
            lines.Add("  m_IsActive: 1");
            return string.Join("\n", lines);
        }

        static string TransformYaml(long id, long go, Vector2 at, IList<long> children, long father)
        {
            var lines = new List<string>
            {
                Header(TransformClass, id, "Transform"), Unlinked,
                $"  m_GameObject: {{fileID: {Id(go)}}}",
                "  serializedVersion: 2",
                "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}",
                "  m_LocalPosition: " + Vec3Yaml(at.x, at.y, 0f),
                "  m_LocalScale: {x: 1, y: 1, z: 1}",
                "  m_ConstrainProportionsScale: 0",
                children.Count == 0 ? "  m_Children: []" : "  m_Children:",
            };
            foreach (long c in children) lines.Add($"  - {{fileID: {Id(c)}}}");
            lines.Add($"  m_Father: {{fileID: {Id(father)}}}");
            lines.Add("  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}");
            return string.Join("\n", lines);
        }

        static string SpriteRendererYaml(long id, long go, ObjRef material, SpriteRef sprite, int order) => string.Join("\n",
            $"--- !u!{Int(SpriteRendererClass)} &{Id(id)}", "SpriteRenderer:", "  serializedVersion: 3", Unlinked,
            $"  m_GameObject: {{fileID: {Id(go)}}}",
            "  m_Enabled: 1", "  m_CastShadows: 0", "  m_ReceiveShadows: 0", "  m_DynamicOccludee: 1", "  m_StaticShadowCaster: 0",
            "  m_MotionVectors: 1", "  m_LightProbeUsage: 1", "  m_ReflectionProbeUsage: 1", "  m_RayTracingMode: 0",
            "  m_RayTraceProcedural: 0", "  m_RayTracingAccelStructBuildFlagsOverride: 0", "  m_RayTracingAccelStructBuildFlags: 1",
            "  m_SmallMeshCulling: 1", "  m_ForceMeshLod: -1", "  m_MeshLodSelectionBias: 0", "  m_RenderingLayerMask: 1",
            "  m_RendererPriority: 0", "  m_Materials:", "  - " + material.Yaml, "  m_StaticBatchInfo:", "    firstSubMesh: 0",
            "    subMeshCount: 0", "  m_StaticBatchRoot: {fileID: 0}", "  m_ProbeAnchor: {fileID: 0}",
            "  m_LightProbeVolumeOverride: {fileID: 0}", "  m_ScaleInLightmap: 1", "  m_ReceiveGI: 1", "  m_PreserveUVs: 0",
            "  m_IgnoreNormalsForChartDetection: 0", "  m_ImportantGI: 0", "  m_StitchLightmapSeams: 1",
            "  m_SelectedEditorRenderState: 0", "  m_MinimumChartSize: 4", "  m_AutoUVMaxDistance: 0.5", "  m_AutoUVMaxAngle: 89",
            "  m_LightmapParameters: {fileID: 0}", "  m_GlobalIlluminationMeshLod: 0", "  m_SortingLayerID: 0", "  m_SortingLayer: 0",
            "  m_SortingOrder: " + Int(order), "  m_MaskInteraction: 0", "  m_Sprite: " + sprite.Sprite.Yaml,
            "  m_Color: {r: 1, g: 1, b: 1, a: 1}", "  m_FlipX: 0", "  m_FlipY: 0", "  m_DrawMode: 0", "  m_Size: " + Vec2Yaml(sprite.Size),
            "  m_AdaptiveModeThreshold: 0.5", "  m_SpriteTileMode: 0", "  m_WasSpriteAssigned: 1", "  m_SpriteSortPoint: 0",
            "  m_BlendShapeWeights: []");

        static string MonoBehaviourYaml(long id, long go, ScriptRef script, IEnumerable<string> fields)
        {
            if (string.IsNullOrEmpty(script.Script.Guid) || string.IsNullOrEmpty(script.ClassIdentifier))
                throw new Refusal($"{KeyScenesStep}: a component with no script.");
            var lines = new List<string>
            {
                Header(MonoBehaviourClass, id, "MonoBehaviour"), Unlinked,
                $"  m_GameObject: {{fileID: {Id(go)}}}",
                "  m_Enabled: 1",
                "  m_EditorHideFlags: 0",
                "  m_Script: " + script.Script.Yaml,
                "  m_Name: ",
                "  m_EditorClassIdentifier: " + Plain(script.ClassIdentifier),
            };
            lines.AddRange(fields);
            return string.Join("\n", lines);
        }

        static string PolygonColliderYaml(long id, long go, IList<Vector2[]> walls)
        {
            var lines = new List<string>
            {
                Header(PolygonCollider2DClass, id, "PolygonCollider2D"), Unlinked,
                $"  m_GameObject: {{fileID: {Id(go)}}}",
                "  m_Enabled: 1", "  serializedVersion: 3", "  m_Density: 1", "  m_Material: {fileID: 0}",
                "  m_IncludeLayers:", "    serializedVersion: 2", "    m_Bits: 0",
                "  m_ExcludeLayers:", "    serializedVersion: 2", "    m_Bits: 0",
                "  m_LayerOverridePriority: 0",
                "  m_ForceSendLayers:", "    serializedVersion: 2", "    m_Bits: 4294967295",
                "  m_ForceReceiveLayers:", "    serializedVersion: 2", "    m_Bits: 4294967295",
                "  m_ContactCaptureLayers:", "    serializedVersion: 2", "    m_Bits: 4294967295",
                "  m_CallbackLayers:", "    serializedVersion: 2", "    m_Bits: 4294967295",
                "  m_IsTrigger: 0", "  m_UsedByEffector: 0", "  m_CompositeOperation: 0", "  m_CompositeOrder: 0",
                "  m_Offset: {x: 0, y: 0}",
                "  m_SpriteTilingProperty:", "    border: {x: 0, y: 0, z: 0, w: 0}", "    pivot: {x: 0, y: 0}",
                "    oldSize: {x: 0, y: 0}", "    newSize: {x: 0, y: 0}", "    adaptiveTilingThreshold: 0", "    drawMode: 0",
                "    adaptiveTiling: 0",
                "  m_AutoTiling: 0",
                "  m_Points:",
                "    m_Paths:",
            };
            foreach (Vector2[] wall in walls)
                for (int i = 0; i < wall.Length; i++)
                    lines.Add((i == 0 ? "    - - " : "      - ") + Vec2Yaml(wall[i]));
            lines.Add("  m_UseDelaunayMesh: 1");
            return string.Join("\n", lines);
        }

        // =====================================================================================
        //  the assets, as the editor holds them
        // =====================================================================================

        /// <summary>A Def as the step reads it.</summary>
        public static KeySceneSetPiece ViewOf(SetPieceDef def, ObjRef defRef, Func<Sprite, SpriteRef> refOf)
        {
            var frames = new SpriteRef[def.Frames?.Length ?? 0];
            for (int i = 0; i < frames.Length; i++)
            {
                Sprite s = def.Frames[i]?.Sprite;
                if (s == null) throw new Refusal($"{KeyScenesStep}: '{def.Id}' frame {i} has no sprite.");
                frames[i] = refOf(s);
            }
            return new KeySceneSetPiece
            {
                Id = def.Id, Layer = def.Layer, Mount = def.Mount, Def = defRef, Frames = frames,
                Colliders = def.Colliders ?? Array.Empty<SetPieceCollider>(),
                TideMarks = def.TideMarks ?? Array.Empty<SetPieceTideMark>(),
                ElevationDeg = def.ElevationDeg, PixelsPerMetre = def.PixelsPerMetre, HasLight = def.HasLight, IsAnimated = def.IsAnimated,
                AnimatedInput = def.Animated?.Input ?? "", AnimatedRigDir = def.Animated?.RigDir ?? 0,
            };
        }

        /// <summary>The kits' art through the AssetDatabase: the iso packs as Nine Mile Creek's dressing reads
        /// them, the set pieces by their Defs, the scripts by their MonoScripts.</summary>
        public sealed class EditorKeySceneAssets : IKeySceneAssets
        {
            ObjRef? _spriteMaterial, _faceMaterial;
            ScriptRef? _sorter, _waterline, _vane;

            public int IsoFacing(string kit, int dir) => IsoPackSprites.FacingForHeading(kit, KitDirHeading(dir));

            public bool TryIsoCell(string kit, string piece, int facing, out SpriteRef sprite) =>
                Found(IsoPackSprites.Facing(kit, piece, facing), out sprite);

            public bool TryFind(string find, string state, int lie, int variant, out SpriteRef sprite) =>
                Found(IsoPackSprites.Find(find, state, lie, variant), out sprite);

            public bool TrySetPiece(string rigKey, out KeySceneSetPiece piece)
            {
                var def = AssetDatabase.LoadAssetAtPath<SetPieceDef>(SetPieceDefBuilder.DefPath(rigKey));
                piece = def == null ? null : ViewOf(def, AssetRef(def, 2), EditorShoreRockSprites.RefOf);
                return piece != null;
            }

            public ObjRef SpriteMaterial => _spriteMaterial ??= NewRendererMaterial();

            public ObjRef FaceMaterial => _faceMaterial ??= AssetRef(
                Resources.Load<Material>(TidalFaceWaterline.MaterialResourceName) ??
                throw new Refusal($"no Resources/{TidalFaceWaterline.MaterialResourceName}.mat."), 2);

            public ScriptRef Sorter => _sorter ??= ScriptOf(typeof(YSortSprite));
            public ScriptRef Waterline => _waterline ??= ScriptOf(typeof(TidalFaceWaterline));
            public ScriptRef Vane => _vane ??= ScriptOf(typeof(SetPieceWindVane));

            static bool Found(Sprite s, out SpriteRef sprite)
            {
                sprite = s == null ? default : EditorShoreRockSprites.RefOf(s);
                return s != null;
            }

            static ObjRef AssetRef(UnityEngine.Object o, int type) =>
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(o, out string guid, out long id)
                    ? new ObjRef(id, guid, type)
                    : throw new Refusal($"'{o.name}' is not an asset.");

            /// <summary>What a SpriteRenderer added in the editor draws with, asked of one.</summary>
            static ObjRef NewRendererMaterial()
            {
                var probe = new GameObject("KeyScenePieces_Probe") { hideFlags = HideFlags.HideAndDontSave };
                try
                {
                    Material m = probe.AddComponent<SpriteRenderer>().sharedMaterial;
                    if (m == null) throw new Refusal("a new SpriteRenderer has no material.");
                    return AssetRef(m, 2);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(probe);
                }
            }

            static ScriptRef ScriptOf(Type t)
            {
                foreach (string g in AssetDatabase.FindAssets("t:MonoScript " + t.Name))
                {
                    var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(g));
                    if (ms != null && ms.GetClass() == t)
                        return new ScriptRef(AssetRef(ms, 3), t.Assembly.GetName().Name + "::" + t.FullName);
                }
                throw new Refusal($"no MonoScript for {t.FullName}.");
            }
        }

        // =====================================================================================
        //  the menu: plan against the scene FILE, write the patch, apply only when asked
        // =====================================================================================

        /// <summary>St Peters' key scenes, by id.</summary>
        public static List<KeySceneDef> LoadStPetersKeyScenes() =>
            AssetDatabase.FindAssets("t:" + nameof(KeySceneDef), new[] { KeySceneFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<KeySceneDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(d => d != null && d.RegionId == StPetersRegionId)
                .OrderBy(d => d.Id, StringComparer.Ordinal)
                .ToList();

        [MenuItem("Hidden Harbours/World/St Peters Layer Refresh/Write the Key Scenes Patch (dry run)")]
        static void WriteKeyScenesMenu() => KeyScenesFromMenu(apply: false);

        [MenuItem("Hidden Harbours/World/St Peters Layer Refresh/Apply the Key Scenes Patch to StPeters.unity")]
        static void ApplyKeyScenesMenu()
        {
            if (EditorUtility.DisplayDialog("St Peters key scenes",
                    $"Write the key scenes' pieces under a '{KeyScenesRootName}' root in the StPeters.unity FILE?\n\n" +
                    "The scene must be closed. The patch is written to " + PatchFolder + " first.", "Apply", "Cancel"))
                KeyScenesFromMenu(apply: true);
        }

        /// <summary>The dry run for <c>-executeMethod</c>: a refusal fails the run instead of logging.</summary>
        public static void WriteKeyScenesPatchBatch() => RunKeyScenes(apply: false);

        /// <summary>The apply for <c>-executeMethod</c>, the menu's own path.</summary>
        public static void ApplyKeyScenesPatchBatch() => RunKeyScenes(apply: true);

        static void KeyScenesFromMenu(bool apply)
        {
            try
            {
                RunKeyScenes(apply);
            }
            catch (Refusal r)
            {
                Debug.LogError(r.Message);
            }
        }

        /// <summary>Plan the key scenes' patch against the scene file, write it to <see cref="PatchFolder"/>,
        /// and apply it only when asked, with the scene closed.</summary>
        public static LayerPatch RunKeyScenes(bool apply)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (apply && string.Equals(SceneManager.GetSceneAt(i).path, ScenePath, StringComparison.Ordinal))
                    throw new Refusal("StPeters is open in the editor. Close it first: an open scene would overwrite the file on its next save.");

            string text = File.ReadAllText(ScenePath);
            LayerPatch patch = KeyScenePieces(SceneYaml.Parse(text), LoadStPetersKeyScenes(), new EditorKeySceneAssets());

            Directory.CreateDirectory(PatchFolder);
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(Path.Combine(PatchFolder, patch.Step + ".patch.yaml"), patch.ToYaml(), utf8);
            if (apply)
            {
                string result = patch.ApplyTo(text);
                if (result != text) File.WriteAllText(ScenePath, result, utf8);
                Debug.Log($"[StPetersLayerRefresh] applied to {ScenePath} (patch in {PatchFolder}):\n{patch.Summary()}");
            }
            else Debug.Log($"[StPetersLayerRefresh] dry run, nothing written to the scene (patch in {PatchFolder}):\n{patch.Summary()}");
            return patch;
        }
    }
}
#endif
