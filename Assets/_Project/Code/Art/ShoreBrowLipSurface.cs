using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Draws a region's turf lip on its banks that have no cliff wall, from the runs
    /// <see cref="ShoreBrowLipMath.Runs"/> found at load. One generated child of the ground, one mesh, built once;
    /// nothing here runs per frame. It is the same strip a wall's lip draws (<see cref="CliffTurfLipDef"/>) on the
    /// cliff face's own material, flagged a decal, so both lips take the same light.
    /// </summary>
    internal static class ShoreBrowLipSurface
    {
        public const string ChildName = "ShoreBrowTurfLip";

        static readonly int IdUnlit = Shader.PropertyToID("_Unlit");
        static readonly int IdPxDecal = Shader.PropertyToID("_PxDecal");
        static readonly int IdBaseTexelScale = Shader.PropertyToID("_BaseTexelScale");
        static readonly int IdVariantOn = Shader.PropertyToID("_VariantOn");
        static readonly int IdVariantFaceOn = Shader.PropertyToID("_VariantFaceOn");
        static readonly int IdFaultB = Shader.PropertyToID("_FaultB");

        /// <summary>The lip's child under <paramref name="parent"/>, or null where the region hangs none.</summary>
        internal static GameObject Create(Transform parent, CliffFaceMixDef mix, IReadOnlyList<ShoreBrowLipMath.Column[]> runs)
        {
            if (parent == null || mix == null || runs == null || runs.Count == 0) return null;
            // A bank may hang its own strip (its rock riser under the soil, owner 11 27); else the wall's.
            CliffTurfLipDef lip = mix.ShoreBrowTurfLip != null ? mix.ShoreBrowTurfLip : mix.TurfLip;
            if (lip == null || !lip.IsDrawable || mix.ShoreBrowMaterial == null) return null;

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            Vector3 origin = parent.position;
            ShoreBrowLipMath.Mesh(runs, lip.StripTexels, lip.MeshLineRow, origin, verts, uvs, tris);
            if (verts.Count == 0) return null;

            var mesh = new Mesh { name = "HH" + ChildName, hideFlags = HideFlags.HideAndDontSave };
            if (verts.Count > ushort.MaxValue) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            // Every channel the cliff face shader reads is written, zero where it has nothing to say: no real
            // elevation (so no waterline), no mix weight, no band to cut. A channel left out does not read as zero.
            var none2 = new Vector2[verts.Count];
            var white = new Color[verts.Count];
            for (int i = 0; i < white.Length; i++) white[i] = Color.white;
            mesh.SetColors(white);
            mesh.SetUVs(1, none2);
            mesh.SetUVs(2, none2);
            mesh.SetUVs(3, none2);
            mesh.SetUVs(4, new Vector3[verts.Count]);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            var go = new GameObject(ChildName) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            Vector3 s = parent.lossyScale;
            go.transform.localScale = new Vector3(s.x != 0f ? 1f / s.x : 1f, s.y != 0f ? 1f / s.y : 1f, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mix.ShoreBrowMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.allowOcclusionWhenDynamic = false;
            // It lies on the ground: over the ground, under the sea where the tide reaches it, under a wharf's
            // deck and under everything that stands on the land.
            var group = go.AddComponent<SortingGroup>();
            group.sortingOrder = SortingBands.UnderHulls;
            renderer.sortingOrder = SortingBands.UnderHulls;

            var block = new MaterialPropertyBlock();
            block.SetTexture(IdUnlit, lip.Strip);
            block.SetFloat(IdPxDecal, 1f);
            block.SetFloat(IdBaseTexelScale, 1f);
            block.SetFloat(IdVariantOn, 0f);
            block.SetFloat(IdVariantFaceOn, 0f);
            block.SetVector(IdFaultB, Vector4.zero);
            renderer.SetPropertyBlock(block);
            return go;
        }

        internal static void Destroy(GameObject lip)
        {
            if (lip == null) return;
            var filter = lip.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            if (Application.isPlaying) { Object.Destroy(lip); if (mesh != null) Object.Destroy(mesh); }
            else { Object.DestroyImmediate(lip); if (mesh != null) Object.DestroyImmediate(mesh); }
        }
    }
}
