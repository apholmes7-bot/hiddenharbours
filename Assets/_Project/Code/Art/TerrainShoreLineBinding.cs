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
        static readonly int Loaded = Shader.PropertyToID("_ShoreLoaded");
        static readonly int Fields = Shader.PropertyToID("_ShoreFields");
        static readonly int Strips = Shader.PropertyToID("_ShoreStrips");
        static readonly int Steps = Shader.PropertyToID("_ShoreSteps0123");
        static readonly int Step4 = Shader.PropertyToID("_ShoreStep4");
        static readonly int Blend = Shader.PropertyToID("_ShoreBlendMetres");
        static readonly int SourceBlend = Shader.PropertyToID("_ShoreSourceBlendMetres");

        TerrainShoreLineBinding(TerrainShoreLinesDef def, Texture2DArray fields, Texture2DArray strips)
        { _def = def; _fields = fields; _strips = strips; }

        internal static TerrainShoreLineBinding Create(Texture2D height, float min, float max, Vector2 center, Vector2 size)
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
            var grid = new ShoreLineDerivation.Grid(elevations, height.width, height.height, center - size * .5f, size);
            ShoreLineDerivation.Result data = ShoreLineDerivation.Build(grid, def);
            var fields = new Texture2DArray(data.Width, data.Height, ShoreLineDerivation.KindCount,
                TextureFormat.RGBAHalf, false, true)
            {
                name = "TransientShoreAddresses", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = 0,
            };
            try
            {
                for (int kind = 0; kind < ShoreLineDerivation.KindCount; kind++) fields.SetPixels(data.Addresses[kind], kind);
                fields.Apply(false, true); // Keep the GPU data, release its CPU copy.
                return new TerrainShoreLineBinding(def, fields, strips);
            }
            catch { Destroy(fields); throw; }
        }

        internal static void Disable(MaterialPropertyBlock block) => block.SetFloat(Loaded, 0);

        internal void Bind(MaterialPropertyBlock block)
        {
            block.SetTexture(Fields, _fields); block.SetTexture(Strips, _strips);
            block.SetVector(Steps, new Vector4((int)_def.Turf, (int)_def.Scarp, (int)_def.Wrack, (int)_def.Weedline));
            block.SetFloat(Step4, (int)_def.Reefedge);
            block.SetFloat(Blend, _def.BlendMetres); block.SetFloat(SourceBlend, _def.UnderlyingBlendMetres);
            block.SetFloat(Loaded, 1);
        }

        public void Dispose() => Destroy(_fields);
        static void Destroy(UnityEngine.Object obj)
        { if (Application.isPlaying) UnityEngine.Object.Destroy(obj); else UnityEngine.Object.DestroyImmediate(obj); }
    }
}
