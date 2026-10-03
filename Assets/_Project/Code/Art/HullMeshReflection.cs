using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>Explicit hull-owned geometry, never the camera's normal-view culling list.
    /// Packet storage belongs to a recording camera lease until its resolve executes.</summary>
    internal static class HullMeshReflection
    {
        internal static readonly int MirrorId = Shader.PropertyToID("_HHMeshReflectMatrix");
        internal static readonly int PlaneId = Shader.PropertyToID("_HHMeshReflectPlane");
        internal static readonly int SizeId = Shader.PropertyToID("_HHMeshReflectSize");
        internal static readonly int DepthRangeId = Shader.PropertyToID("_HHMeshReflectDepthRange");
        internal static readonly int LitId = Shader.PropertyToID("_HHMeshReflectLit");
        static readonly List<IsoFacetHullRenderer> s_Hulls = new List<IsoFacetHullRenderer>();
        static readonly List<IsoCharacterFigureRenderer> s_Figures = new List<IsoCharacterFigureRenderer>();
        internal static int Count => s_Hulls.Count;
        internal static void Register(IsoFacetHullRenderer hull) { if (!s_Hulls.Contains(hull)) s_Hulls.Add(hull); }
        internal static void Unregister(IsoFacetHullRenderer hull) => s_Hulls.Remove(hull);
        internal static void Register(IsoCharacterFigureRenderer figure) { if (!s_Figures.Contains(figure)) s_Figures.Add(figure); }
        internal static void Unregister(IsoCharacterFigureRenderer figure) => s_Figures.Remove(figure);

        internal static void SetMode(bool enabled)
        {
            for (int i = 0; i < s_Hulls.Count; i++) s_Hulls[i].SetLegacyReflection(!enabled);
        }

        internal sealed class Packet : IDisposable
        {
            internal Mesh Mesh, SourceMesh;
            Mesh _figureCopy;
            readonly List<Vector3> _vertices = new List<Vector3>();
            readonly List<Vector3> _normals = new List<Vector3>();
            internal Renderer Source;
            IsoFacetHullRenderer _sortHull;
            internal string SortKey;
            internal Material Material;
            internal Matrix4x4 ObjectToWorld, Mirror;
            internal Vector4 Plane;
            internal Bounds Bounds;
            internal int Pass, Submesh;
            internal long Triangles;
            internal readonly MaterialPropertyBlock Properties = new MaterialPropertyBlock();
            internal void Set(Renderer source, Mesh mesh, int submesh, Matrix4x4 mirror, Vector4 plane)
            {
                IsoFacetHullRenderer owner = source.GetComponentInParent<IsoFacetHullRenderer>();
                if (Source != source || _sortHull != owner)
                {
                    _sortHull = owner;
                    SortKey = string.Empty;
                    for (Transform t = source.transform; t != null; t = t.parent)
                        SortKey += "/" + t.name + "[" + t.GetSiblingIndex() + "]";
                }
                bool newMesh = SourceMesh != mesh;
                Source = source;
                SourceMesh = mesh;
                Mesh = mesh;
                if (source.GetComponentInParent<IsoCharacterFigureRenderer>() != null)
                {
                    if (_figureCopy == null || newMesh)
                    {
                        CoreUtils.Destroy(_figureCopy);
                        _figureCopy = UnityEngine.Object.Instantiate(mesh);
                        _figureCopy.hideFlags = HideFlags.HideAndDontSave;
                        _figureCopy.MarkDynamic();
                    }
                    mesh.GetVertices(_vertices);
                    mesh.GetNormals(_normals);
                    _figureCopy.SetVertices(_vertices);
                    _figureCopy.SetNormals(_normals);
                    _figureCopy.bounds = mesh.bounds;
                    Mesh = _figureCopy;
                }
                Submesh = submesh;
                Triangles = mesh.GetIndexCount(submesh) / 3;
                ObjectToWorld = source.localToWorldMatrix;
                Mirror = mirror;
                Plane = plane;
                Bounds = TransformBounds(mesh.bounds, mirror * ObjectToWorld);
                // Snapshot material state as well as MPBs: a second camera cannot alter an
                // earlier camera's figure face uniforms, ramps or ink state before execution.
                if (Material == null || Material.shader != source.sharedMaterial.shader)
                {
                    CoreUtils.Destroy(Material);
                    Material = new Material(source.sharedMaterial) { hideFlags = HideFlags.HideAndDontSave };
                }
                else Material.CopyPropertiesFromMaterial(source.sharedMaterial);
                Pass = Material.FindPass("HHHullMeshReflection");
                source.GetPropertyBlock(Properties);
                Properties.SetMatrix(MirrorId, mirror);
                Properties.SetVector(PlaneId, plane);
                Properties.SetFloat(Shader.PropertyToID("_DeckOccupantCount"), 0);
            }
            public void Dispose()
            {
                CoreUtils.Destroy(Material);
                CoreUtils.Destroy(_figureCopy);
            }
        }

        internal sealed class Snapshot : IDisposable
        {
            internal readonly Packet[] Packets;
            internal RTHandle Output;
            internal Material ResolveMaterial;
            internal readonly float[] Lit = new float[256];
            internal readonly List<MeshFilter> Fittings = new List<MeshFilter>();
            internal int Count, HullCount, Width, Height;
            internal long Triangles;
            internal bool InUse, Overflow;
            internal Vector4 DepthRange;
            internal Snapshot(int capacity) { Packets = new Packet[capacity]; }
            internal void Clear()
            {
                Count = HullCount = 0;
                Triangles = 0;
                Overflow = false;
                Array.Clear(Lit, 0, Lit.Length);
            }
            internal bool Add(Renderer renderer, Mesh mesh, Matrix4x4 mirror, Vector4 plane, Camera camera)
            {
                if (renderer == null || mesh == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy
                    || (camera.cullingMask & (1 << renderer.gameObject.layer)) == 0 || renderer.sharedMaterial == null) return true;
                Bounds bounds = TransformBounds(mesh.bounds, mirror * renderer.localToWorldMatrix);
                if (!OverlapsTarget(bounds, camera, Width, Height)) return true;
                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    if (Count == Packets.Length) return false;
                    // Preserve cold slots too: an offscreen packet must not repeatedly evict
                    // another source and allocate a hierarchy key on every subsequent frame.
                    int cached = -1, empty = -1;
                    for (int old = Count; old < Packets.Length; old++)
                    {
                        if (Packets[old] == null) { empty = old; break; }
                        if (Packets[old].Source == renderer && Packets[old].Submesh == s) { cached = old; break; }
                    }
                    if (cached < 0 && empty >= 0)
                    {
                        cached = empty;
                        Packets[cached] = new Packet();
                    }
                    if (cached >= 0)
                    {
                        Packet swap = Packets[Count]; Packets[Count] = Packets[cached]; Packets[cached] = swap;
                    }
                    Packet p = Packets[Count];
                    p.Set(renderer, mesh, s, mirror, plane);
                    if (p.Pass < 0) continue;
                    Count++;
                    Triangles += p.Triangles;
                }
                return true;
            }
            public void Dispose()
            {
                for (int i = 0; i < Packets.Length; i++) Packets[i]?.Dispose();
                CoreUtils.Destroy(ResolveMaterial);
                ResolveMaterial = null;
                InUse = false;
            }
        }

        // Two outstanding recordings per camera, bounded independently of live hull count.
        // Normally one is enough; a repeated camera record before execution takes the other.
        internal sealed class CameraState : IDisposable
        {
            readonly Snapshot[] _leases = new Snapshot[2];
            readonly RTHandle[] _retired = new RTHandle[2];
            internal bool OverflowWarned;
            bool IsPending(RTHandle target)
            {
                foreach (Snapshot lease in _leases)
                    if (lease != null && lease.InUse && lease.Output == target) return true;
                return false;
            }
            internal void ReleaseRetired()
            {
                for (int i = 0; i < _retired.Length; i++)
                    if (_retired[i] != null && !IsPending(_retired[i]))
                    {
                        _retired[i].Release();
                        _retired[i] = null;
                    }
            }
            internal bool RetainPending(RTHandle target)
            {
                if (target == null || !IsPending(target)) return false;
                for (int i = 0; i < _retired.Length; i++)
                    if (_retired[i] == target) return true;
                for (int i = 0; i < _retired.Length; i++)
                    if (_retired[i] == null) { _retired[i] = target; return true; }
                // At most two leases can refer to distinct pending targets.
                throw new InvalidOperationException("Reflection retirement exceeds the recording lease bound.");
            }
            internal Snapshot Acquire(int capacity)
            {
                ReleaseRetired();
                for (int i = 0; i < _leases.Length; i++)
                {
                    if (_leases[i] != null && _leases[i].InUse) continue;
                    if (_leases[i] == null || _leases[i].Packets.Length != capacity)
                    {
                        _leases[i]?.Dispose();
                        _leases[i] = new Snapshot(capacity);
                    }
                    _leases[i].Clear();
                    _leases[i].Output = null;
                    _leases[i].InUse = true;
                    return _leases[i];
                }
                return null;
            }
            public void Dispose()
            {
                foreach (Snapshot s in _leases) s?.Dispose();
                foreach (RTHandle target in _retired) target?.Release();
            }
        }

        internal static float Waterline(IsoFacetHullRenderer hull)
        {
            if (!DisplacedWaterRegistry.TryGetIsoDepthFrame(out WaterIsoDepthFrame frame)) return 0;
            Vector2 anchor = hull.transform.position;
            float lift = 0;
            if (DisplacedSea.TryGet(out DisplacedSeaState sea))
            {
                PackedWaveField field = WaveFieldBridge.ReadPublishedField();
                float raw = WaveFieldBridge.ShaderTwinSample(anchor, in field, sea.FreqScale,
                    GameServices.FetchEnvelopeAt(anchor)).Height;
                var environment = GameServices.Environment;
                float water = environment != null ? environment.WaterLevelAt(GameServices.Clock != null
                    ? GameServices.Clock.TotalSeconds : 0) : 0;
                float ground = GameServices.TidalTerrain != null ? GameServices.TidalTerrain.ElevationAt(anchor) : 0;
                lift = DisplacedWaterMath.VertexLift(raw, water - ground, sea.ShoreFadeBandMeters, sea.Exaggeration);
            }
            return IsoFacetMath.HullReflectionWaterline(hull.ReflectionElevation, lift,
                hull.ReflectionHeave, hull.ReflectionDepthHeave);
        }

        internal static void Prepare(Snapshot output, Camera camera, int width, int height,
                                     int maxHulls, long maxTriangles)
        {
            output.Clear();
            output.Width = width; output.Height = height;
            if (!camera.orthographic) return;
            for (int f = 0; f < s_Figures.Count; f++) s_Figures[f].PrepareReflection();
            for (int i = 0; i < s_Hulls.Count; i++)
            {
                IsoFacetHullRenderer hull = s_Hulls[i];
                hull.ApplyPose();
                ReflectiveObject source = hull.ReflectionSource;
                if (source == null || !source.isActiveAndEnabled || !source.WithinDistanceGate
                    || hull.ReflectionMesh == null) continue;
                float w = Waterline(hull);
                Vector3 origin = hull.PosedMesh.position;
                Matrix4x4 mirror = IsoFacetMath.HullReflectionMatrix(origin, hull.ReflectionElevation, w);
                Vector4 plane = IsoFacetMath.HullReflectionPlane(origin, hull.ReflectionElevation, w);
                int before = output.Count;
                bool fits = output.Add(hull.ReflectionHull, hull.ReflectionMesh, mirror, plane, camera)
                    && output.Add(hull.ReflectionLeaf, hull.ReflectionLeafMesh, mirror, plane, camera);
                // Mesh fittings are explicitly owned by this hull and use the facet shader.
                // The supplied List overload allocates only when authored topology grows.
                hull.GetComponentsInChildren(false, output.Fittings);
                for (int f = 0; fits && f < output.Fittings.Count; f++)
                {
                    MeshFilter filter = output.Fittings[f];
                    var fitting = filter.GetComponentInParent<IsoFacetPropRenderer>();
                    if (fitting == null || !fitting.isActiveAndEnabled ||
                        filter.GetComponentInParent<IsoFacetHullRenderer>() != hull) continue;
                    fits = output.Add(filter.GetComponent<MeshRenderer>(), filter.sharedMesh, mirror, plane, camera);
                }
                for (int f = 0; fits && f < s_Figures.Count; f++)
                    if (s_Figures[f].Hull == hull && !s_Figures[f].IsAshore)
                        fits = output.Add(s_Figures[f].ReflectionRenderer, s_Figures[f].ReflectionMesh,
                            mirror, plane, camera);
                if (output.Count > before) output.HullCount++;
                if (!fits || output.HullCount > maxHulls || output.Triangles > maxTriangles)
                {
                    output.Clear(); output.Overflow = true; return;
                }
                // Reflection ownership is only occupancy + lit class, never a main-view HullId.
                // This also avoids the normal view's fore-band id budget limiting this list.
                int id = source.NightLitSource ? 2 : 1;
                output.Lit[id] = source.NightLitSource ? 1 : 0;
                for (int packet = before; packet < output.Count; packet++)
                    output.Packets[packet].Properties.SetFloat(IsoFacetShaderIds.HullId, id / 255f);
            }
            if (output.Count == 0) return;
            // Original-view cullResults never participates; only full reflected bounds do.
            Array.Sort(output.Packets, 0, output.Count, PacketOrder.Instance);
            float low = float.PositiveInfinity, high = float.NegativeInfinity;
            for (int i = 0; i < output.Count; i++)
            {
                Bounds b = TransformBounds(output.Packets[i].Bounds, camera.worldToCameraMatrix);
                low = Mathf.Min(low, -b.max.z); high = Mathf.Max(high, -b.min.z);
            }
            // Reflection-only depth mapping includes the entire reflected shell, irrespective
            // of the normal camera's near/far planes. Ordering is common to every packet.
            float span = Mathf.Max(high - low, Mathf.Epsilon);
            output.DepthRange = new Vector4(low, 1 / span, 0, 0);
            for (int i = 0; i < output.Count; i++)
            {
                output.Packets[i].Properties.SetVector(SizeId, new Vector4(width, height, 0, 0));
                output.Packets[i].Properties.SetVector(DepthRangeId, output.DepthRange);
            }
        }

        internal static bool OverlapsTarget(Bounds bounds, Camera camera, int width, int height)
        {
            // Water bounds-checks AFTER warp: no off-target texel can ever be fetched.
            // Thus the complete target is a conservative bound for every possible warp,
            // regardless of the live material. Add one source texel for keyline/figure ink.
            // Ignore z here; reflection builds its own complete depth span, including blockers.
            Vector2 lo = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 hi = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 p = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                Vector3 v = camera.WorldToViewportPoint(p);
                lo = Vector2.Min(lo, v); hi = Vector2.Max(hi, v);
            }
            return hi.x >= -1f / width && lo.x <= 1 + 1f / width
                && hi.y >= -1f / height && lo.y <= 1 + 1f / height;
        }

        internal static Bounds TransformBounds(Bounds source, Matrix4x4 matrix)
        {
            Vector3 e = source.extents;
            Vector3 x = matrix.MultiplyVector(new Vector3(e.x, 0, 0));
            Vector3 y = matrix.MultiplyVector(new Vector3(0, e.y, 0));
            Vector3 z = matrix.MultiplyVector(new Vector3(0, 0, e.z));
            return new Bounds(matrix.MultiplyPoint3x4(source.center), 2 * new Vector3(
                Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z)));
        }

        sealed class PacketOrder : IComparer<Packet>
        {
            internal static readonly PacketOrder Instance = new PacketOrder();
            public int Compare(Packet a, Packet b)
            {
                int c = string.CompareOrdinal(a.SortKey, b.SortKey);
                if (c != 0) return c;
                for (int i = 0; i < 16; i++) { c = a.ObjectToWorld[i].CompareTo(b.ObjectToWorld[i]); if (c != 0) return c; }
                for (int i = 0; i < 4; i++) { c = a.Plane[i].CompareTo(b.Plane[i]); if (c != 0) return c; }
                return a.Submesh.CompareTo(b.Submesh);
            }
        }
    }
}
