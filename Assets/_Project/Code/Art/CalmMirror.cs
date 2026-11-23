using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The calm mirror's arithmetic, kept pure so EditMode tests can hold it without a camera.
    /// </summary>
    public static class CalmMirrorMath
    {
        /// <summary>1 on calm water, 0 once the sea reaches <see cref="WaterMirrorSettings.SeaFadeEnd"/>.</summary>
        public static float SeaFade(float seaState01, in WaterMirrorSettings s)
            => 1f - Smooth(s.SeaFadeStart, s.SeaFadeEnd, seaState01);

        /// <summary>0 on a flat sea, 1 at <see cref="WaterMirrorSettings.SeaFadeEnd"/>: drives the ripple and the breaks.</summary>
        public static float Roughness(float seaState01, in WaterMirrorSettings s)
            => Smooth(0f, s.SeaFadeEnd, seaState01);

        /// <summary>The strength the mirror is laid at: the owner's dial, faded out as the sea gets up.</summary>
        public static float EffectiveStrength(float dial, float seaState01, in WaterMirrorSettings s)
            => Mathf.Clamp01(dial) * SeaFade(seaState01, s);

        /// <summary>
        /// The share of the water's own object reflection the mirror takes over (0 = the water keeps all of
        /// it, today's look). While the mirror runs it owns the calm part, so the meshes never reflect twice;
        /// as the sea gets up the water has its reflection back. Dial 0 hands nothing over.
        /// </summary>
        public static float Handover(float dial, float seaState01, in WaterMirrorSettings s)
            => dial > 0f ? SeaFade(seaState01, s) : 0f;

        internal static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / Mathf.Max(b - a, 1e-4f));
            return t * t * (3f - 2f * t);
        }
    }

    /// <summary>
    /// The calm mirror (the owner, 2026-11-23: calm water shows a TRUE reflection). Driven from
    /// <see cref="IsoFacetHullFeature.AddRenderPasses"/> once per camera per frame.
    ///
    /// <para>Dial 0 (<see cref="GameConfig.WaterMirrorStrength"/>, the shipped default): the two globals it
    /// owns stay 0, which is also their unset value, the camera is never touched and no pass is recorded,
    /// so every pixel is today's.</para>
    ///
    /// <para>Dial above 0, on a game camera: the camera clears its alpha to 0.5 (the "ground" class), the
    /// hull and figure overlay tags mesh pixels (<c>_HHCalmMirrorMeshTag</c>), the water hands its object
    /// reflection share over (<c>_HHCalmMirrorHandover</c>), figures ashore join the mesh mirror pass
    /// (<see cref="HullMeshReflection"/>), and one full-screen pass lays the mirror over the water.</para>
    /// </summary>
    internal static class CalmMirror
    {
        internal static readonly int HandoverId = Shader.PropertyToID("_HHCalmMirrorHandover");
        internal static readonly int MeshTagId = Shader.PropertyToID("_HHCalmMirrorMeshTag");
        static readonly int ParamsId = Shader.PropertyToID("_CalmMirrorParams");
        static readonly int Params2Id = Shader.PropertyToID("_CalmMirrorParams2");
        static readonly int RectId = Shader.PropertyToID("_CalmMirrorRect");
        static readonly int TexelId = Shader.PropertyToID("_CalmMirrorTexel");
        static readonly int ReachId = Shader.PropertyToID("_CalmMirrorReach");
        internal const string ShaderName = "Hidden/HiddenHarbours/CalmMirror";

        /// <summary>The camera alpha that marks a pixel nobody drew over as ground.</summary>
        internal const float GroundAlpha = 0.5f;

        /// <summary>True while the camera being set up runs the mirror. Read by the mesh mirror pass to
        /// take figures ashore in.</summary>
        internal static bool Active { get; private set; }

        /// <summary>Looks only: 1 shows the pixel classes, 2 the true layer's coverage. Never saved.</summary>
        internal static float DebugView;
        /// <summary>Looks only: a sea state to use instead of the weather's (below 0 = the weather's).</summary>
        internal static float SeaStateOverride = -1f;

        static Camera s_Camera;
        static float s_CameraAlpha;
        static Material s_Material;
        static Pass s_Pass;

        /// <summary>Per camera, before the feature's own early returns.</summary>
        internal static void AddTo(ScriptableRenderer renderer, Camera camera)
        {
            float dial = GameServices.WaterMirrorStrength;
            bool run = dial > 0f && camera != null && camera.cameraType == CameraType.Game && camera.orthographic
                       && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
            WaterMirrorSettings s = GameServices.WaterMirror;
            float sea = SeaState();
            Publish(run ? CalmMirrorMath.Handover(dial, sea, s) : 0f, run ? 1f : 0f);
            Active = run;
            if (!run)
            {
                if (s_Camera == camera) RestoreCamera();
                return;
            }
            if (s_Camera != camera)
            {
                RestoreCamera();
                s_Camera = camera;
                s_CameraAlpha = camera.backgroundColor.a;
            }
            Color bg = camera.backgroundColor;
            if (bg.a != GroundAlpha) { bg.a = GroundAlpha; camera.backgroundColor = bg; }
            if (s_Material == null)
            {
                Shader shader = Shader.Find(ShaderName);
                if (shader == null || !shader.isSupported) return;
                s_Material = CoreUtils.CreateEngineMaterial(shader);
            }
            s_Pass ??= new Pass { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
            s_Pass.Material = s_Material;
            s_Pass.Strength = CalmMirrorMath.EffectiveStrength(dial, sea, s);
            s_Pass.Roughness = CalmMirrorMath.Roughness(sea, s);
            s_Pass.Settings = s;
            renderer.EnqueuePass(s_Pass);
        }

        /// <summary>The feature is going away: lower everything, give the camera its alpha back.</summary>
        internal static void Release()
        {
            Publish(0f, 0f);
            Active = false;
            RestoreCamera();
            CoreUtils.Destroy(s_Material);
            s_Material = null;
        }

        static float SeaState()
        {
            if (SeaStateOverride >= 0f) return Mathf.Clamp01(SeaStateOverride);
            return GameServices.Environment != null ? GameServices.Environment.Sample().SeaState01 : 0f;
        }

        // Globals are written only when they differ from what is bound, so dial 0 never writes one
        // (both read 0 unset, the value that leaves the water and the overlay as they were).
        static void Publish(float handover, float meshTag)
        {
            if (Shader.GetGlobalFloat(HandoverId) != handover) Shader.SetGlobalFloat(HandoverId, handover);
            if (Shader.GetGlobalFloat(MeshTagId) != meshTag) Shader.SetGlobalFloat(MeshTagId, meshTag);
        }

        static void RestoreCamera()
        {
            if (s_Camera != null)
            {
                Color bg = s_Camera.backgroundColor;
                bg.a = s_CameraAlpha;
                s_Camera.backgroundColor = bg;
            }
            s_Camera = null;
        }

        sealed class PassData
        {
            public TextureHandle Source;
            public Material Material;
        }

        sealed class Pass : ScriptableRenderPass
        {
            public Material Material;
            public float Strength, Roughness;
            public WaterMirrorSettings Settings;
            static readonly Vector4 NoScaleBias = new Vector4(1f, 1f, 0f, 0f);

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                TextureHandle target = resources.activeColorTexture;
                if (!target.IsValid() || Material == null) return;
                TextureDesc desc = graph.GetTextureDesc(target);
                // The classes ride the alpha channel; a target without one has nothing to read.
                if (!GraphicsFormatUtility.HasAlphaChannel(desc.format) || desc.width <= 0 || desc.height <= 0) return;
                Camera camera = cameraData.camera;

                float h = camera.orthographicSize * 2f, w = h * camera.aspect;
                Vector3 c = camera.transform.position;
                float pxPerMetre = desc.height / h;
                float ppm = FigurePixelGrid.ArtPixelsPerMetre;
                float pxPerArt = pxPerMetre / ppm;
                WaterMirrorSettings s = Settings;
                float t = GameServices.Clock != null ? (float)(GameServices.Clock.FlowSeconds % 3600.0) : Time.time;
                Material.SetVector(ParamsId, new Vector4(Strength,
                    Mathf.Lerp(s.RippleCalmMetres, s.RippleRoughMetres, Roughness),
                    Mathf.Lerp(s.BreakCalm, s.BreakRough, Roughness), t));
                // The flip is gone past ReachEndMetres, so the search never walks further than that.
                Material.SetVector(Params2Id, new Vector4(Mathf.Min(s.MaxMetres, Mathf.Max(s.ReachEndMetres, 0f)) * pxPerMetre,
                    Mathf.Max(1f, Mathf.Round(s.StepPixels * pxPerArt)), s.Darken, DebugView));
                Material.SetVector(RectId, new Vector4(c.x - w * 0.5f, c.y - h * 0.5f, w, h));
                Material.SetVector(TexelId, new Vector4(1f / desc.width, 1f / desc.height, ppm, Roughness));
                Material.SetVector(ReachId, new Vector4(s.ReachStartMetres * pxPerMetre, s.ReachEndMetres * pxPerMetre,
                    s.MinStandPixels, s.RippleBandPixels));

                desc.name = "_HHCalmMirrorCopy";
                desc.clearBuffer = false;
                desc.msaaSamples = MSAASamples.None;
                desc.depthBufferBits = DepthBits.None;
                TextureHandle copy = graph.CreateTexture(desc);
                graph.AddBlitPass(target, copy, Vector2.one, Vector2.zero, passName: "HH Calm Mirror Copy");

                using var builder = graph.AddRasterRenderPass<PassData>("HH Calm Mirror", out var data);
                data.Source = copy;
                data.Material = Material;
                builder.UseTexture(copy, AccessFlags.Read);
                builder.SetRenderAttachment(target, 0, AccessFlags.Write);
                // The true layer reads the mesh mirror pass's target through its global.
                builder.UseAllGlobalTextures(true);
                builder.SetRenderFunc(static (PassData d, RasterGraphContext ctx) =>
                    Blitter.BlitTexture(ctx.cmd, d.Source, NoScaleBias, d.Material, 0));
            }
        }
    }
}
