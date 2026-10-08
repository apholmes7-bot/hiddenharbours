#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    public static partial class StPetersLayerRefresh
    {
        public interface IKeySceneWaveAssets
        {
            bool TryBuilding(KeyScenePiece piece, out SpriteRef sprite, out Vector2 footprint);
            bool TryMount(KeyScenePiece piece, out Vector2 screenPoint, out float sortY);
            ScriptRef Perch { get; }
        }

        public static float KeySceneGroundAt(Vector2 point)
        {
            var ground = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>("Assets/_Project/Data/Terrain/StPetersSeabed.asset");
            if (ground == null || ground.Field == null) throw new Refusal("The committed St Peters ground is unavailable.");
            return ground.Field.ElevationAt(point);
        }

        static KeyScenePlacement PlaceWavePiece(string scene, KeyScenePiece piece, IKeySceneAssets assets)
        {
            if (!(assets is IKeySceneWaveAssets wave)) throw new Refusal($"{piece.Id}: no building or perch asset reader.");
            var result = new KeyScenePlacement
            {
                Scene = scene, Id = piece.Id, Kit = piece.Kit, Piece = piece.Piece, At = piece.At,
                GroundPoint = piece.At, Material = assets.SpriteMaterial, SortYOffset = SortYOffsetFor(piece),
            };
            if (piece.Kind == KeySceneDef.KindGull)
            {
                if (!wave.TryMount(piece, out Vector2 screen, out float sortY))
                    throw new Refusal($"{piece.Id}: its perch host is absent or its anchor is unknown.");
                result.At = screen;
                result.SortYOffset = sortY - screen.y;
                result.IsPerch = true;
                result.Cell = "perch (no sprite)";
                return result;
            }
            if (!wave.TryBuilding(piece, out result.Sprite, out Vector2 footprint) ||
                result.Sprite.Sprite.FileId == 0 || string.IsNullOrEmpty(result.Sprite.Sprite.Guid))
                throw new Refusal($"{piece.Id}: its building sheet is not baked and sliced.");
            var corners = new[]
            {
                new Vector2(-footprint.x, -footprint.y) * 0.5f,
                new Vector2( footprint.x, -footprint.y) * 0.5f,
                new Vector2( footprint.x,  footprint.y) * 0.5f,
                new Vector2(-footprint.x,  footprint.y) * 0.5f,
            };
            for (int i = 0; i < corners.Length; i++)
            {
                Vector2 rotated = SetPieceDef.ToWorld(corners[i], piece.Dir);
                corners[i] = new Vector2(rotated.x, rotated.y * IsoGround.GroundDepthScale);
            }
            result.Walls.Add(corners);
            result.Cell = "building dir " + piece.Dir;
            result.SortingOrder = YSortSprite.OrderFor(piece.At.y + result.SortYOffset,
                SortingBands.DecorBase, SortingBands.OrdersPerMetre, SortingBands.DecorFloor, SortingBands.DecorCeiling);
            return result;
        }

        public sealed partial class EditorKeySceneAssets
        {
            ScriptRef? _perch;
            SceneYaml _hostScene;
            public ScriptRef Perch => _perch ??= ScriptOf(typeof(GullPerch));

            public bool TryBuilding(KeyScenePiece piece, out SpriteRef sprite, out Vector2 footprint)
            {
                sprite = default; footprint = default;
                var build = VillageBuildingKit.KeySceneSet.FirstOrDefault(b => b.RigKey == piece.Kit && b.Preset == piece.Piece);
                if (build.Key == null) return false;
                var placement = VillageBuildingCatalog.Find(build.Key);
                if (!placement.IsValid) return false;
                footprint = placement.FootprintMetres;
                return Found(VillageBuildingCatalog.LoadFacing(placement, SetPieceDef.FrameForRigDir(piece.Dir)), out sprite);
            }

            public bool TryMount(KeyScenePiece piece, out Vector2 screenPoint, out float sortY)
            {
                screenPoint = default; sortY = 0;
                _hostScene ??= SceneYaml.Parse(File.ReadAllText(ScenePath));
                float baseZ;
                if (piece.MountHost == "structure.stp_cannery" &&
                    (piece.MountAnchor == "ridge" || piece.MountAnchor == "boilerHouseRidge" || piece.MountAnchor == "door"))
                {
                    if (!_hostScene.Docs.Any(d => d.ClassId == GameObjectClass && d.Field("m_Name") == StPetersCannery.RootName)) return false;
                    baseZ = KeySceneGroundAt(StPetersCannery.Site);
                    sortY = StPetersCannery.Site.y;
                }
                else if (piece.MountHost == "context.wharf" && piece.MountAnchor == "northPilehead")
                {
                    if (!_hostScene.Docs.Any(d => d.ClassId == GameObjectClass && d.Field("m_Name") == StPetersWharf.RootName)) return false;
                    // The pilehead belongs to the committed wharf; only its relative rise is drawn.
                    var root = new Vector2(StPetersWharf.RootCellX + 0.5f, 0f);
                    baseZ = KeySceneGroundAt(root);
                    sortY = piece.At.y;
                }
                else return false;
                screenPoint = piece.At + Vector2.up * ((piece.Z - baseZ) * IsoGround.HeightScale);
                return true;
            }
        }
    }
}
#endif
