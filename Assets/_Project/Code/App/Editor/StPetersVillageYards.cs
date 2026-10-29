#if UNITY_EDITOR
using System;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>The five V1 yard bindings active in this pass. Other yards keep their main rows.</summary>
    public static class StPetersVillageYards
    {
        public static bool TryKey(string name, out string key)
        {
            key = name switch
            {
                StPetersYards.SchoolYard => "school",
                StPetersYards.GeneralStoreYard => "general_store",
                StPetersYards.SageCottageYard => "sage_cottage",
                StPetersYards.HarbourGableYard => "harbour_gable",
                StPetersYards.WhiteGableYard => "white_gable",
                _ => null,
            };
            return key != null;
        }

        public static T Read<T>(string file) where T : UnityEngine.Object
        {
            var data = AssetDatabase.LoadAssetAtPath<T>(StPetersTerrainPlan.VillageFolder + "/" + file + ".asset");
            if (data == null) throw new InvalidOperationException("Missing village data: " + file);
            return data;
        }

        public static Yard FromDef(string name)
        {
            if (!TryKey(name, out string key)) throw new ArgumentException("No active V1 yard binding: " + name);
            var lot = Read<LotDef>("lot.st_peters." + key);
            var yard = Read<YardDef>("yard.st_peters." + key);
            var plan = Read<VillagePlanDef>("village_plan.st_peters");
            var hull = plan.Footprints.Single(f => f.Id == lot.FootprintId).LocalBounds;
            return FromData(name, lot.Position, hull, yard.Outline, yard.Gate, yard.Fence, yard.Mown);
        }

        public static Yard FromData(string name, Vector2 site, Vector4 hull, Vector2[] outline,
            Vector2 gate, YardFence fence, MownStyle mown)
        {
            float width = hull.z - hull.x;
            float depth = (hull.w - hull.y) / IsoGround.GroundDepthScale;
            float radius = .5f * Mathf.Sqrt(width * width + depth * depth);
            return new Yard(name, site, radius, new GroundPolygon(outline), fence, new[] { gate },
                "the authored V1 outline and entrance", mown);
        }

        public static Vector4 HullFor(string name)
        {
            if (!TryKey(name, out string key)) throw new ArgumentException("No active V1 yard binding: " + name);
            var lot = Read<LotDef>("lot.st_peters." + key);
            var plan = Read<VillagePlanDef>("village_plan.st_peters");
            var b = plan.Footprints.Single(f => f.Id == lot.FootprintId).LocalBounds;
            return new Vector4(b.x + lot.Position.x, b.y + lot.Position.y,
                b.z + lot.Position.x, b.w + lot.Position.y);
        }

        public static float HullMargin(GroundPolygon polygon, Vector4 bounds)
        {
            float margin = float.PositiveInfinity;
            foreach (var p in new[] { new Vector2(bounds.x, bounds.y), new Vector2(bounds.z, bounds.y),
                new Vector2(bounds.z, bounds.w), new Vector2(bounds.x, bounds.w) })
            {
                float distance = float.PositiveInfinity;
                for (int i = 0; i < polygon.Count; i++)
                {
                    polygon.Edge(i, out Vector2 a, out Vector2 b);
                    distance = Mathf.Min(distance, VillageGeometry.PointSegment(p, a, b));
                }
                margin = Mathf.Min(margin, polygon.Contains(p) ? distance : -distance);
            }
            return margin;
        }
    }
}
#endif
