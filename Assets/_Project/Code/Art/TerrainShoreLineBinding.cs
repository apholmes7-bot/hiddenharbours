using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>One load's transient addressing array. Created on enable, bound only to the ground;
    /// WriteGroundFloor deliberately does not publish it to the water's separate floor drawing.</summary>
    internal sealed class TerrainShoreLineBinding : IDisposable
    {
        readonly TerrainShoreLinesDef _def;
        readonly Texture2DArray _fields, _strips;
        readonly Texture2D _segments;
        readonly Texture2D _indices;
        readonly Vector2 _min, _size;
        readonly int[] _kindStarts;
        Texture2D _walls;
        Texture2D _wallDomain;
        static readonly int Loaded = Shader.PropertyToID("_ShoreLoaded");
        static readonly int Fields = Shader.PropertyToID("_ShoreFields");
        static readonly int Grid = Shader.PropertyToID("_ShoreGrid");
        static readonly int Strips = Shader.PropertyToID("_ShoreStrips");
        static readonly int Segments = Shader.PropertyToID("_ShoreSegments");
        static readonly int Steps = Shader.PropertyToID("_ShoreSteps0123");
        static readonly int Step4 = Shader.PropertyToID("_ShoreStep4");
        static readonly int Blend = Shader.PropertyToID("_ShoreBlendMetres");
        static readonly int SourceBlend = Shader.PropertyToID("_ShoreSourceBlendMetres");

        TerrainShoreLineBinding(TerrainShoreLinesDef def, Texture2DArray fields, Texture2DArray strips, Texture2D segments,
            Vector2 min, Vector2 size, int[] kindStarts, Texture2D indices)
        { _def = def; _fields = fields; _strips = strips; _segments = segments; _min = min; _size = size; _kindStarts = kindStarts; _indices = indices; }

        internal static TerrainShoreLineBinding Create(Texture2D height, float min, float max, Vector2 center, Vector2 size,
            UnityEngine.SceneManagement.Scene scene, params Texture2D[] splats)
        {
            TerrainShoreLinesDef def = TerrainShoreLinesDef.Find(height);
            if (def == null) return null; // Explicit region opt-in: no NMC change.
            var strips = Resources.Load<Texture2DArray>("TerrainShoreLines/TerrainEdge256x128");
            if (strips == null || strips.width != 256 || strips.height != 128 || strips.depth != 15)
                throw new InvalidOperationException("Shore line array missing or invalid.");
            if (!height.isReadable) throw new InvalidOperationException("Shore height texture must be readable.");
            var elevations = new float[height.width * height.height];
            if (height.format == TextureFormat.R16)
            {
                var raw = height.GetPixelData<ushort>(0);
                for (int i = 0; i < elevations.Length; i++) elevations[i] = Mathf.Lerp(min, max, raw[i] / 65535f);
            }
            else
            {
                Color[] pixels = height.GetPixels();
                for (int i = 0; i < elevations.Length; i++) elevations[i] = Mathf.Lerp(min, max, pixels[i].r);
            }
            var reader = new ShoreLineDerivation.SplatReader(splats);
            Vector2 worldMin = center - size * .5f;
            var footprint = CaptureWalls(scene, worldMin, size, def);
            var grid = new ShoreLineDerivation.Grid(elevations, height.width, height.height, worldMin, size,
                p => reader.At(new Vector2((p.x-worldMin.x)/size.x, (p.y-worldMin.y)/size.y)), footprint);
            ShoreLineDerivation.Result data = ShoreLineDerivation.Build(grid, def);
            if (data.Segments.Length > SystemInfo.maxTextureSize) throw new InvalidOperationException("Shore segment table exceeds texture limit.");
            var segments = new Texture2D(data.Segments.Length, 2, TextureFormat.RGBAFloat, false, true)
                { name = "TransientShoreSegments", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
            int indexHeight=data.IndexTexels.Length/1024;
            if(indexHeight>SystemInfo.maxTextureSize)throw new InvalidOperationException("Shore candidate table exceeds texture limit.");
            var indices=new Texture2D(1024,indexHeight,TextureFormat.RFloat,false,true)
                {name="TransientShoreCandidates",hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Point};
            var fields = new Texture2DArray(data.Width, data.Height, ShoreLineDerivation.KindCount,
                TextureFormat.RGBAHalf, false, true)
            {
                name = "TransientShoreAddresses", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, anisoLevel = 0,
            };
            try
            {
                for (int kind = 0; kind < ShoreLineDerivation.KindCount; kind++) fields.SetPixels(data.Addresses[kind], kind);
                fields.Apply(false, true); // Keep the GPU data, release its CPU copy.
                segments.SetPixels(data.SegmentTexels); segments.Apply(false, true);
                indices.SetPixelData(data.IndexTexels,0);indices.Apply(false,true);
                var binding = new TerrainShoreLineBinding(def, fields, strips, segments, worldMin, size, data.KindStarts, indices);
                binding._walls = new Texture2D(footprint.Width, footprint.Height, TextureFormat.R8, false, true)
                    { name = "TransientShoreWallFootprint", hideFlags = HideFlags.HideAndDontSave,
                      filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                binding._walls.SetPixelData(footprint.Pixels, 0); binding._walls.Apply(false, true);
                // seat ruling s15, 11-07: a wall protects its shore-normal strand, including
                // the exposed foreshore. Elsewhere the procedural foot exclusion stays in force.
                // Reuse the S12 coast classification; no brow, contour or face footprint moves.
                var domain = new byte[data.Width * data.Height];
                for (int y = 0; y < data.Height; y++)
                for (int x = 0; x < data.Width; x++)
                {
                    int i = y * data.Width + x;
                    bool addressed = false;
                    for (int kind = 0; kind < ShoreLineDerivation.KindCount; kind++)
                        addressed |= data.Addresses[kind][i].a > 0;
                    if (!addressed) continue;
                    Vector2 p = worldMin + Vector2.Scale(new Vector2((x + .5f) / data.Width,
                        (y + .5f) / data.Height), size);
                    if (grid.CliffCoast(p, def)) domain[i] = 255;
                }
                binding._wallDomain = new Texture2D(data.Width, data.Height, TextureFormat.R8, false, true)
                    { name = "TransientShoreWallDomain", hideFlags = HideFlags.HideAndDontSave,
                      filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                binding._wallDomain.SetPixelData(domain, 0); binding._wallDomain.Apply(false, true);
                return binding;
            }
            catch { Destroy(fields); Destroy(segments); Destroy(indices); throw; }
        }

        internal static void Disable(MaterialPropertyBlock block) => block.SetFloat(Loaded, 0);

        internal void Bind(MaterialPropertyBlock block)
        {
            block.SetTexture(Fields, _fields); block.SetTexture(Strips, _strips);
            block.SetTexture(Segments, _segments);
            block.SetTexture("_ShoreIndexList",_indices);
            block.SetVector("_ShoreRange01", new Vector4(_kindStarts[0], _kindStarts[1], _kindStarts[1], _kindStarts[2]));
            block.SetVector("_ShoreRange23", new Vector4(_kindStarts[2], _kindStarts[3], _kindStarts[3], _kindStarts[4]));
            block.SetVector("_ShoreRange4", new Vector4(_kindStarts[4], _kindStarts[5], 0, 0));
            block.SetFloat("_ShoreWallsLoaded", _walls != null ? 1 : 0);
            if (_walls != null)
            {
                block.SetTexture("_ShoreWallMask", _walls);
                block.SetTexture("_ShoreWallDomain", _wallDomain);
                block.SetVector("_ShoreWallGrid", new Vector4(_walls.width, _walls.height, 1f / _walls.width, 1f / _walls.height));
            }
            block.SetVector(Grid, new Vector4(_fields.width, _fields.height, 1f / _fields.width, 1f / _fields.height));
            block.SetVector(Steps, new Vector4((int)_def.Turf, (int)_def.Scarp, (int)_def.Wrack, (int)_def.Weedline));
            block.SetFloat(Step4, (int)_def.Reefedge);
            block.SetFloat(Blend, _def.BlendMetres); block.SetFloat(SourceBlend, _def.UnderlyingBlendMetres);
            BindPattern(block, _def);
            block.SetFloat(Loaded, 1);
        }

        public static void BindPattern(MaterialPropertyBlock block, TerrainShoreLinesDef def)
        {
            block.SetInt("_ShoreSeed", def.PatternSeed);
            float pitch = (def.WrackGapMetres.x + def.WrackGapMetres.y + def.WrackLengthMetres.x + def.WrackLengthMetres.y) * .5f;
            float jitter = Mathf.Max(0, (def.WrackGapMetres.y - def.WrackGapMetres.x - def.WrackLengthMetres.y + def.WrackLengthMetres.x) * .25f);
            block.SetVector("_ShoreWrack", new Vector4(def.WrackLengthMetres.x, def.WrackLengthMetres.y, pitch, jitter));
            block.SetVector("_ShorePattern", new Vector4(def.WrackAcrossMetres, def.LipPitchMetres, 0, 0));
            block.SetVector("_ShoreLip", new Vector4(def.LipDepthMetres.x, def.LipDepthMetres.y, def.LipBreakMetres.x, def.LipBreakMetres.y));
            block.SetColor("_ShoreLipShadow", def.LipShadow);
        }
        // seat ruling s12, 11-07: raster the real wall triangles, never broad renderer bounds.
        // The one-address-texel margin is baked once; both CPU fields and shader read this mask.
        internal static ShoreLineDerivation.WallFootprint CaptureWalls(UnityEngine.SceneManagement.Scene scene,
            Vector2 min, Vector2 size, TerrainShoreLinesDef def)
        {
            // seat ruling s17: a metre of margin erases a .5-1m lip. Protect real faces plus
            // one mask texel; the brow decal's plan-ground portion is not a wall face.
            var footprint = new ShoreLineDerivation.WallFootprint(min, size, def.BlendMetres, def.BlendMetres);
            foreach (var wall in UnityEngine.Object.FindObjectsByType<CliffWallSurface>(FindObjectsSortMode.None))
            {
                if (wall.gameObject.scene != scene) continue;
                foreach (var filter in wall.GetComponentsInChildren<MeshFilter>())
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    if (renderer == null || !renderer.enabled || filter.sharedMesh == null) continue;
                    var vertices = FaceVertices(filter, wall);
                    for (int i = 0; i < vertices.Length; i += 3)
                        footprint.AddTriangle(vertices[i], vertices[i + 1], vertices[i + 2]);
                }
            }
            return footprint;
        }
        internal static Vector3[] FaceVertices(MeshFilter filter, CliffWallSurface wall)
        {
            var mesh = filter.sharedMesh; var vertices = mesh.vertices;
            var world = filter.transform.localToWorldMatrix;
            for (int i = 0; i < vertices.Length; i++) vertices[i] = world.MultiplyPoint3x4(vertices[i]);
            if (filter.name == CliffWallSurface.BrowChildName)
            {
                // Read the builder's existing value; do not duplicate a kit tuning constant.
                float line = (float)typeof(CliffWallSurface).GetField("_browLineAt",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(wall);
                return ShoreLineDerivation.FaceVertices(vertices, mesh.uv, mesh.triangles, line);
            }
            var result = new Vector3[mesh.triangles.Length]; var triangles = mesh.triangles;
            for (int i = 0; i < result.Length; i++) result[i] = vertices[triangles[i]];
            return result;
        }
        public void Dispose()
        {
            Destroy(_fields); Destroy(_segments); Destroy(_indices);
            Destroy(_walls);
            Destroy(_wallDomain);
        }
        static void Destroy(UnityEngine.Object obj)
        { if (Application.isPlaying) UnityEngine.Object.Destroy(obj); else UnityEngine.Object.DestroyImmediate(obj); }
    }
}
