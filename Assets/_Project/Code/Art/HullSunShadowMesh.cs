using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>One owned configuration-time mesh: hull, shut leaf and open leaf, selected by a per-draw marker.</summary>
    public static class HullSunShadowMesh
    {
        public static Mesh Build(Mesh hull, Mesh closedLeaf, Mesh openLeaf)
        {
            if (hull == null) return null;
            int count = hull.vertexCount + (closedLeaf != null ? closedLeaf.vertexCount : 0)
                                         + (openLeaf != null ? openLeaf.vertexCount : 0);
            var vertices = new List<Vector3>(count);
            var levels = new List<Vector4>(count);
            var parts = new List<Vector2>(count);
            var indices = new List<int>();
            Append(hull, 0, vertices, levels, parts, indices);
            Append(closedLeaf, 1, vertices, levels, parts, indices);
            Append(openLeaf, 2, vertices, levels, parts, indices);
            var mesh = new Mesh { name = "HullSunShadowProxy", hideFlags = HideFlags.HideAndDontSave,
                indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, levels);
            mesh.SetUVs(1, parts);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static void Append(Mesh source, int part, List<Vector3> vertices, List<Vector4> levels,
                           List<Vector2> parts, List<int> indices)
        {
            if (source == null) return;
            int start = vertices.Count;
            Vector3[] points = source.vertices;
            var tags = new List<Vector4>();
            source.GetUVs(1, tags);
            for (int i = 0; i < points.Length; i++)
            {
                vertices.Add(points[i]);
                levels.Add(i < tags.Count ? tags[i] : Vector4.zero);
                parts.Add(new Vector2(part, 0));
            }
            for (int submesh = 0; submesh < source.subMeshCount; submesh++)
                foreach (int index in source.GetTriangles(submesh)) indices.Add(start + index);
        }
    }
}
