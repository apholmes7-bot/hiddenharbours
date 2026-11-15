#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HiddenHarbours.Art;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>A pad that moves takes its ground with it</b> (NMC H9). Step 06 paints what kit v3 keeps, the gas bar's
    /// forecourt among it, and the planter keeps the meadow and the planting off every pad at BUILD time only. When a
    /// named patch moves a pad on the committed scene, these three plans carry the same rule to it: the paving
    /// repainted from <see cref="NineMileCreekRoads.Pads"/>, the meadow's sites inside the pad struck from the one
    /// <see cref="GrassField"/>, and the planting that stands on it retired. Each is a pure function of the scene and
    /// the pad, and a second plan over its own output changes nothing.
    /// </summary>
    public static partial class NineMileCreekGroundSteps
    {
        /// <summary>Step 06 alone, on the committed scene: both road layers repainted with what kit v3 keeps.</summary>
        public static LayerPatch EssentialRoads(SceneYaml scene) =>
            Roads(scene, new Inputs(NineMileCreekGround.Load(), new EditorBerths()));

        /// <summary>
        /// The meadow steps off <paramref name="pad"/>: every site of the <see cref="GrassField"/> whose tuft stands
        /// inside it is struck, with the margin <see cref="NineMileCreekFields.IsGrassGround"/> gives a pad (none).
        /// Nothing else in the field's payload moves.
        /// </summary>
        public static LayerPatch PadMeadow(SceneYaml scene, Rect pad, string step)
        {
            string rootName = NineMileCreekFieldPlanter.GrassRootName;
            Doc root = scene.RootNamed(rootName);
            var patch = new LayerPatch(step, rootName, root.FileId);
            Doc field = One(scene.ComponentsOf(root).Where(d => d.IsScript("HiddenHarbours.Art.GrassField")), step, rootName + "'s GrassField");
            Vector3 origin = ParseVec3(field.Field("_originWorld").TrimEnd('}') + ", z: 0}");
            var layout = new GrassFieldLayout
            {
                OriginX = origin.x, OriginY = origin.y,
                CellSize = Float(field, "_cellSize"), JitterMetres = Float(field, "_jitterMetres"),
                SpreadMetres = Float(field, "_spreadMetres"),
                CellsX = Int(field, "_cellsX"), CellsY = Int(field, "_cellsY"), Slots = Int(field, "_slots"), Seed = Int(field, "_seed"),
            };
            byte[] plane = GrassFieldCodec.Decode(field.Field("_sitePayload"));
            if (plane.Length != layout.CellsX * layout.CellsY * layout.Slots)
                throw new Refusal($"{step}: {rootName}'s site payload is not its grid's size.");
            float reach = layout.JitterMetres + layout.SpreadMetres + layout.CellSize;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((pad.xMin - reach - layout.OriginX) / layout.CellSize));
            int x1 = Mathf.Min(layout.CellsX - 1, Mathf.CeilToInt((pad.xMax + reach - layout.OriginX) / layout.CellSize));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((pad.yMin - reach - layout.OriginY) / layout.CellSize));
            int y1 = Mathf.Min(layout.CellsY - 1, Mathf.CeilToInt((pad.yMax + reach - layout.OriginY) / layout.CellSize));
            int struck = 0;
            for (int iy = y0; iy <= y1; iy++)
            for (int ix = x0; ix <= x1; ix++)
            for (int t = 0; t < layout.Slots; t++)
            {
                int at = (iy * layout.CellsX + ix) * layout.Slots + t;
                if (GrassFieldScatter.IsEmpty(plane[at])) continue;
                if (!pad.Contains(GrassFieldScatter.SlotPosition(layout, ix, iy, t))) continue;
                plane[at] = 0;
                struck++;
            }
            if (struck > 0)
            {
                patch.Edit(field, Set(field.Text, "_sitePayload", GrassFieldCodec.Encode(plane)), rootName + "/GrassField sites");
                patch.Note(rootName, "the meadow steps off the pad", struck.ToString(CultureInfo.InvariantCulture) + " tufts struck");
            }
            return patch.Seal(scene);
        }

        /// <summary>The planting under <paramref name="rootName"/> that stands inside <paramref name="pad"/> is
        /// retired, each plant with every document under it, as step 12 clears a carriageway.</summary>
        public static LayerPatch PadPlanting(SceneYaml scene, string rootName, Rect pad, string step, string why)
        {
            Doc root = scene.RootNamed(rootName);
            Doc rootTr = scene.TransformOf(root);
            RequireIdentity(rootTr, step);
            var patch = new LayerPatch(step, rootName, root.FileId);
            List<long> all = rootTr.FieldRefs("m_Children");
            var kept = new List<long>(all.Count);
            foreach (long c in all)
            {
                Doc tr = scene.Require(c, "a plant's transform");
                if (tr.Stripped) throw new Refusal($"{step}: a prefab instance under {rootName}; the planter makes none.");
                Vector3 p = Position(tr);
                if (!pad.Contains(new Vector2(p.x, p.y))) { kept.Add(c); continue; }
                Doc go = scene.Require(tr.FieldRef("m_GameObject"), "a plant");
                string label = $"{go.Field("m_Name")} &{go.FileId.ToString(CultureInfo.InvariantCulture)} at {Vec2(p)}";
                foreach (long id in scene.SubtreeOf(go.FileId).OrderBy(id => id))
                {
                    Doc d = scene.Require(id, "a plant's document");
                    patch.Delete(d, $"{label}: {Part(d, go)} ({why})");
                }
            }
            if (kept.Count != all.Count)
            {
                patch.Edit(rootTr, SetList(rootTr.Text, "m_Children", kept), rootName + "/children");
                patch.Note(rootName, "cleared", (all.Count - kept.Count).ToString(CultureInfo.InvariantCulture) + " " + why);
            }
            return patch.Seal(scene);
        }

        static float Float(Doc doc, string key) => float.Parse(doc.Field(key), CultureInfo.InvariantCulture);
        static int Int(Doc doc, string key) => int.Parse(doc.Field(key), CultureInfo.InvariantCulture);
    }
}
#endif
