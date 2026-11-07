using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>Read-only live sea settings for the hull facet term. Published from the same
    /// material/block copied to the displaced sea, never written back. Buffers and ids are cached.</summary>
    public static class HullCausticLighting
    {
        private static readonly string[] Names = {
            "_PixelsPerUnit", "_NoiseScale", "_BandScaleResponse", "_Chop", "_Flow",
            "_CausticScale", "_PaintScale", "_UntileStrength", "_CausticTexStrength",
            "_CausticCurvatureBlend", "_CausticCurvatureStep", "_CausticCurvatureGain",
            "_CausticAmount", "_CausticDayGate"
        };
        private static readonly int[] Ids = BuildIds();
        private static readonly float[] Values = new float[Names.Length];
        private static readonly int ColorId = Shader.PropertyToID("_CausticColor");
        private static readonly int TextureId = Shader.PropertyToID("_CausticTex");
        private static readonly int WaterId = Shader.PropertyToID("_WaterLevel");
        private static readonly int PlaneId = Shader.PropertyToID("_HHHullCausticPlane");
        private static readonly int SettingsId = Shader.PropertyToID("_HHHullCausticSettings");
        private static readonly int PaintedId = Shader.PropertyToID("_HHHullCausticPainted");
        private static readonly int EnvelopeId = Shader.PropertyToID("_HHHullCausticEnvelope");
        private static Object _owner;
        private static Color _color;
        private static Texture _texture;
        private static float _painted, _envelope, _waterLevel;
        internal static bool Enabled => _owner != null && GameServices.Config != null
            && GameServices.Config.HullCausticBandMeters > 0 && GameServices.Config.HullCausticStrength > 0;

        private static int[] BuildIds()
        {
            var ids = new int[Names.Length];
            for (int i = 0; i < ids.Length; i++) ids[i] = Shader.PropertyToID(Names[i]);
            return ids;
        }

        internal static void Publish(Object owner, Material material, MaterialPropertyBlock source, float envelope)
        {
            if (owner == null || material == null) return;
            _owner = owner;
            for (int i = 0; i < Ids.Length; i++)
                Values[i] = source.HasFloat(Ids[i]) ? source.GetFloat(Ids[i]) : material.GetFloat(Ids[i]);
            _color = source.HasVector(ColorId) ? source.GetColor(ColorId) : material.GetColor(ColorId);
            _texture = source.HasTexture(TextureId) ? source.GetTexture(TextureId) : material.GetTexture(TextureId);
            _waterLevel = source.HasFloat(WaterId) ? source.GetFloat(WaterId) : material.GetFloat(WaterId);
            _painted = material.IsKeywordEnabled("_USE_CAUSTICTEX") && _texture != null ? 1 : 0;
            _envelope = envelope;
        }

        internal static void Clear(Object owner)
        {
            if (_owner == owner) { _owner = null; _texture = null; }
        }

        internal static void Prepare(IsoFacetHullRenderer hull, bool receives, Vector3[] vertices,
                                     out Vector4 plane, out Vector4 settings)
        {
            plane = Vector4.zero;
            settings = Vector4.zero;
            GameConfig config = GameServices.Config;
            if (!receives || _owner == null || config == null || hull.PosedMesh == null
                || config.HullCausticBandMeters <= 0 || config.HullCausticStrength <= 0
                || !DisplacedWaterRegistry.TryGetIsoDepthFrame(out WaterIsoDepthFrame frame)) return;

            plane = Plane(frame, hull.DepthShear.x);
            Vector2 root = hull.transform.position;
            bool wet = vertices != null && MinimumHeight(hull.PosedMesh.localToWorldMatrix, vertices, plane) <= 0;
            if (GameServices.TidalTerrain != null
                && GameServices.TidalTerrain.ElevationAt(root) >= _waterLevel) wet = false;
            if (!wet) return;

            settings = new Vector4(config.HullCausticBandMeters,
                config.HullCausticStrength, 1, config.HullCausticMaxUpwardNormal);
        }

        internal static void Apply(MaterialPropertyBlock target, Vector4 plane, Vector4 settings)
        {
            target.SetVector(SettingsId, settings);
            if (settings.z < 0.5f) return;
            for (int i = 0; i < Ids.Length; i++) target.SetFloat(Ids[i], Values[i]);
            target.SetColor(ColorId, _color);
            if (_texture != null) target.SetTexture(TextureId, _texture);
            target.SetFloat(PaintedId, _painted);
            target.SetFloat(EnvelopeId, _envelope);
            target.SetVector(PlaneId, plane);
        }

        internal static Vector4 Plane(in WaterIsoDepthFrame frame)
            => new Vector4(0, frame.CosElev, -frame.SinElev,
                frame.SinElev * frame.BaseZ - frame.CosElev * frame.ReferenceY);

        private static Vector4 Plane(in WaterIsoDepthFrame frame, float depthShear)
        {
            // -sin(elev) times (the sheared hull depth minus the still sea depth at this row).
            // Keep the live authored sea cos/sin and the hull's actual shear, including their rounding.
            float y = (depthShear + frame.CosElev) * frame.SinElev;
            return new Vector4(0, y, -frame.SinElev, frame.SinElev * frame.BaseZ - y * frame.ReferenceY);
        }

        internal static float MinimumHeight(Matrix4x4 matrix, Vector3[] vertices, Vector4 plane)
        {
            Vector3 n = new Vector3(plane.x, plane.y, plane.z);
            Vector3 axis = new Vector3(Vector3.Dot(n, matrix.GetColumn(0)),
                Vector3.Dot(n, matrix.GetColumn(1)), Vector3.Dot(n, matrix.GetColumn(2)));
            float origin = Vector3.Dot(n, matrix.GetColumn(3)) + plane.w;
            float minimum = float.PositiveInfinity;
            for (int i = 0; i < vertices.Length; i++) minimum = Mathf.Min(minimum, Vector3.Dot(axis, vertices[i]));
            return origin + minimum;
        }

        /// <summary>C# twin of the facet's geometric/day/chop weight; optical value is separate.</summary>
        public static float Weight(float height, float width, bool exterior, float upwardNormal,
                                   float maxUpwardNormal, bool wet, float sun, float tintSum,
                                   float dayGate, float chop, float amount, float strength)
        {
            if (!wet || !exterior || height <= 0 || height >= width || width <= 0
                || upwardNormal >= maxUpwardNormal) return 0;
            float fade = 1 - height / width;
            return fade * fade * CausticMath.DayGate(sun, tintSum, dayGate)
                   * (1 - Mathf.Clamp01(chop)) * amount * strength;
        }
    }
}
