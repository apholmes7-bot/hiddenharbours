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
        RenderTexture _walls;
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
            params Texture2D[] splats)
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
            var grid = new ShoreLineDerivation.Grid(elevations, height.width, height.height, worldMin, size,
                p => reader.At(new Vector2((p.x-worldMin.x)/size.x, (p.y-worldMin.y)/size.y)));
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
                return new TerrainShoreLineBinding(def, fields, strips, segments, worldMin, size, data.KindStarts, indices);
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
        // Start runs after all walls have built on enable. This is one load-only raster, not frame work.
        // The full wall mesh, including its brow/toe decals, is deliberately conservative: a new
        // ground strip cannot show through any part of the existing wall's footprint.
        internal void CaptureWalls(UnityEngine.SceneManagement.Scene scene)
        {
            if (_walls != null) return;
            var walls = UnityEngine.Object.FindObjectsByType<CliffWallSurface>(FindObjectsSortMode.None);
            var meshes = new System.Collections.Generic.List<MeshRenderer>();
            foreach (var wall in walls) if (wall.gameObject.scene == scene)
                meshes.AddRange(wall.GetComponentsInChildren<MeshRenderer>());
            if (meshes.Count == 0) return;
            Shader shader = Resources.Load<Shader>("TerrainShoreLines/ShoreWallMask");
            if (shader == null) throw new InvalidOperationException("Shore wall protection shader missing.");
            var material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _walls = new RenderTexture(Mathf.CeilToInt(_size.x / _def.BlendMetres),
                Mathf.CeilToInt(_size.y / _def.BlendMetres), 0, RenderTextureFormat.R8, RenderTextureReadWrite.Linear)
                { name = "TransientShoreWallFootprint", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var command = new UnityEngine.Rendering.CommandBuffer { name = "Shore wall footprint at load" };
            var rectangle=new Mesh{hideFlags=HideFlags.HideAndDontSave};
            rectangle.vertices=new[]{new Vector3(-.5f,-.5f),new Vector3(.5f,-.5f),new Vector3(.5f,.5f),new Vector3(-.5f,.5f)};
            rectangle.triangles=new[]{0,1,2,0,2,3};
            try
            {
                _walls.Create(); command.SetRenderTarget(_walls);
                command.SetViewport(new Rect(0, 0, _walls.width, _walls.height));
                command.ClearRenderTarget(false, true, Color.black);
                Vector2 center = _min + _size * .5f;
                var view = Matrix4x4.Scale(new Vector3(1, 1, -1))
                    * Matrix4x4.Translate(new Vector3(-center.x, -center.y, 100));
                var projection = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-_size.x*.5f, _size.x*.5f,
                    -_size.y*.5f, _size.y*.5f, .01f, 1000), true);
                // This is world-Y-addressed data, read with Load rather than a camera blit.
                // A top-origin backend needs the explicit row inversion for those addresses.
                if (SystemInfo.graphicsUVStartsAtTop) projection.m11 = -projection.m11;
                command.SetViewProjectionMatrices(view, projection);
                foreach (var renderer in meshes)
                {
                    if (!renderer.enabled)continue;
                    Bounds bounds=renderer.bounds;float pad=_def.BlendMetres;
                    command.DrawMesh(rectangle,Matrix4x4.TRS(bounds.center,Quaternion.identity,
                        new Vector3(bounds.size.x+2*pad,bounds.size.y+2*pad,1)),material);
                }
                command.SetViewProjectionMatrices(Shader.GetGlobalMatrix("unity_MatrixV"), Shader.GetGlobalMatrix("unity_MatrixP"));
                Graphics.ExecuteCommandBuffer(command);
            }
            finally { command.Release(); Destroy(material); Destroy(rectangle); }
        }
        public void Dispose()
        {
            Destroy(_fields); Destroy(_segments); Destroy(_indices);
            if (_walls != null) { _walls.Release(); Destroy(_walls); }
        }
        static void Destroy(UnityEngine.Object obj)
        { if (Application.isPlaying) UnityEngine.Object.Destroy(obj); else UnityEngine.Object.DestroyImmediate(obj); }
    }
}
