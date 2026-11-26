#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Tools.RigBaking;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>Nothing grows on the slipway.</b> The shore scatter was laid before the wharf's west slipway, so two of its
    /// plants stand on the ramp's concrete; since the ramp is cut at the waterline they float on the sea where it runs
    /// under. The slipway's footprint is the water side's own record (<see cref="SlipwayFootprints"/>); the shore scatter
    /// refuses it (<see cref="NineMileCreekShore.IsPlantableShore"/>) and this step lifts the plants already standing on it.
    /// </summary>
    public static partial class NineMileCreekWharfLandSide
    {
        /// <summary>The shore scatter's root, and its plants' parent under it.</summary>
        public const string ShorelineRootName = "Shoreline", ShorePlantsName = "ShorePlants";

        /// <summary>How far round a slipway's edge a plant is still on it, metres: a shrub's canopy reaches about this far
        /// from its root, and a plant whose leaves lie on the concrete is a plant on the ramp.</summary>
        public const float SlipwayPlantClearanceMetres = 0.5f;

        static Rect[] _slipways;

        /// <summary>Each slipway part's walk in plan, from the water side's record (<c>module.key</c> slipway).</summary>
        public static IReadOnlyList<Rect> SlipwayFootprints()
        {
            if (_slipways != null) return _slipways;
            var rects = new List<Rect>();
            foreach (object part in DeckSidecarJson.AsArray(DeckSidecarJson.Member(NineMileCreekWharfWaterSide.ReadToday(), "parts")))
            {
                object module = DeckSidecarJson.Member(part, "module");
                if (module == null || DeckSidecarJson.String(DeckSidecarJson.Member(module, "key")) != NineMileCreekWharf.SlipwayModule) continue;
                Vector2[] walk = DeckSidecarJson.AsArray(DeckSidecarJson.Member(part, "walkPolygon")).Select(q => DeckSidecarJson.AsArray(q))
                    .Select(q => new Vector2(DeckSidecarJson.Float(q[0]), DeckSidecarJson.Float(q[1]))).ToArray();
                rects.Add(Rect.MinMaxRect(walk.Min(v => v.x), walk.Min(v => v.y), walk.Max(v => v.x), walk.Max(v => v.y)));
            }
            return _slipways = rects.ToArray();
        }

        /// <summary>Is <paramref name="p"/> on a slipway, within <see cref="SlipwayPlantClearanceMetres"/> of its edge?</summary>
        public static bool OnASlipway(Vector2 p)
        {
            foreach (Rect r in SlipwayFootprints())
                if (p.x >= r.xMin - SlipwayPlantClearanceMetres && p.x <= r.xMax + SlipwayPlantClearanceMetres &&
                    p.y >= r.yMin - SlipwayPlantClearanceMetres && p.y <= r.yMax + SlipwayPlantClearanceMetres) return true;
            return false;
        }

        /// <summary>The shore plants standing on a slipway, lifted: their objects deleted and their parent's child list
        /// trimmed. Applying it again finds none.</summary>
        public static LayerPatch LiftSlipwayPlants(SceneYaml scene)
        {
            Doc root = scene.RootNamed(ShorelineRootName);
            var patch = new LayerPatch(LampsStep, ShorelineRootName, root.FileId);
            HashSet<long> mine = scene.SubtreeOf(root.FileId);
            Doc plantsGo = scene.Docs.SingleOrDefault(d => mine.Contains(d.FileId) && d.ClassId == 1 && !d.Stripped && d.Field("m_Name") == ShorePlantsName)
                ?? throw new Refusal(LampsStep + ": no " + ShorelineRootName + "/" + ShorePlantsName + ".");
            Doc plantsTr = scene.TransformOf(plantsGo);
            Vector2 origin = PlantWorldOf(scene, plantsTr);
            var lifted = new List<long>();
            var deleted = new HashSet<long>();
            foreach (long childTr in plantsTr.FieldRefs("m_Children"))
            {
                Doc tr = scene.Require(childTr, "a shore plant");
                if (!OnASlipway(origin + PlantLocalXY(tr))) continue;
                lifted.Add(childTr);
                foreach (long id in scene.SubtreeOf(tr.FieldRef("m_GameObject"))) deleted.Add(id);
            }
            foreach (long id in deleted.OrderBy(id => id))
            {
                Doc doc = scene.Require(id, ShorePlantsName);
                string objectName = doc.ClassId == 1 ? doc.Field("m_Name") : scene.Require(doc.FieldRef("m_GameObject"), ShorePlantsName).Field("m_Name");
                patch.Delete(doc, ShorePlantsName + "/" + objectName + "/&" + id + " class " + doc.ClassId + " (on the slipway)");
            }
            if (lifted.Count > 0)
            {
                long[] keep = plantsTr.FieldRefs("m_Children").Where(id => !lifted.Contains(id)).ToArray();
                string value = keep.Length == 0 ? "  m_Children: []" : "  m_Children:\n" + string.Join("\n", keep.Select(id => "  - {fileID: " + id + "}"));
                patch.Edit(plantsTr, Regex.Replace(plantsTr.Text, @"(?m)^  m_Children:(?: \[\])?(?:\n  - [^\n]*)*", _ => value),
                    ShorePlantsName + "/children after the slipway's plants are lifted");
            }
            return patch.Seal(scene);
        }

        static Vector2 PlantLocalXY(Doc tr)
        {
            Match m = Regex.Match(tr.Field("m_LocalPosition") ?? "", @"x: ([-\d.eE+]+), y: ([-\d.eE+]+)");
            return m.Success ? new Vector2(float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)) : Vector2.zero;
        }

        static Vector2 PlantWorldOf(SceneYaml scene, Doc tr)
        {
            Vector2 at = Vector2.zero;
            for (Doc t = tr; t != null; t = t.FieldRef("m_Father") == 0 ? null : scene.Require(t.FieldRef("m_Father"), "a parent"))
                at += PlantLocalXY(t);
            return at;
        }
    }
}
#endif
