#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    public static partial class StPetersLayerRefresh
    {
        /// <summary>Reads the committed field's actual sites, not the builder's prospective keepouts.</summary>
        public static List<Vector2> CommittedGrassSites(SceneYaml scene)
        {
            var result = new List<Vector2>();
            foreach (var doc in scene.Docs.Where(d => d.ClassId == MonoBehaviourClass &&
                         d.Field("m_EditorClassIdentifier") == "HiddenHarbours.Art::HiddenHarbours.Art.GrassField"))
            {
                Vector3 origin = ParseVec3(doc.Field("_originWorld").Replace("}", ", z: 0}"));
                var layout = new GrassFieldLayout
                {
                    OriginX = origin.x, OriginY = origin.y,
                    CellSize = ParseFloat(doc.Field("_cellSize")),
                    JitterMetres = ParseFloat(doc.Field("_jitterMetres")),
                    SpreadMetres = ParseFloat(doc.Field("_spreadMetres")),
                    CellsX = ParseInt(doc.Field("_cellsX")), CellsY = ParseInt(doc.Field("_cellsY")),
                    Slots = ParseInt(doc.Field("_slots")), Seed = ParseInt(doc.Field("_seed")),
                };
                byte[] sites = GrassFieldCodec.Decode(doc.Field("_sitePayload"));
                if (sites.Length != layout.CellsX * layout.CellsY * layout.Slots)
                    throw new Refusal("The committed grass field has an invalid site count.");
                for (int y = 0; y < layout.CellsY; y++)
                for (int x = 0; x < layout.CellsX; x++)
                for (int slot = 0; slot < layout.Slots; slot++)
                    if (!GrassFieldScatter.IsEmpty(sites[(y * layout.CellsX + x) * layout.Slots + slot]))
                        result.Add(GrassFieldScatter.SlotPosition(layout, x, y, slot));
            }
            if (result.Count == 0) throw new Refusal("The committed scene supplied no grass sites; this is not a passing grass guard.");
            return result;
        }

        public static int GrassInsideBuilding(IReadOnlyList<Vector2> grass, Vector2 centre, Vector2 footprint, int dir)
        {
            int count = 0;
            foreach (Vector2 point in grass)
            {
                Vector2 delta = point - centre;
                Vector2 local = SetPieceDef.ToWorld(new Vector2(delta.x, delta.y / IsoGround.GroundDepthScale), -dir);
                if (Math.Abs(local.x) <= footprint.x * 0.5f && Math.Abs(local.y) <= footprint.y * 0.5f) count++;
            }
            return count;
        }
    }
}
#endif
