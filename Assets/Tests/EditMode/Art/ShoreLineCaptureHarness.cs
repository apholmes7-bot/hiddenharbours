using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>Capture-only clock and readback. Changes no quality, material or scene asset.</summary>
    public static class ShoreLineCaptureHarness
    {
        public const int SettleFrames = 4;
        public const int MedianFrames = 3;

        public static Vector3[] FacePositions(Renderer renderer)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            var wall = renderer.GetComponentInParent<CliffWallSurface>(true);
            if (filter == null || filter.sharedMesh == null || wall == null) return Array.Empty<Vector3>();
            var binding = typeof(TerrainSplatSurface).Assembly.GetType("HiddenHarbours.Art.TerrainShoreLineBinding");
            return (Vector3[])binding.GetMethod("FaceVertices", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { filter, wall });
        }
        public static ShoreLineDerivation.WallFootprint SnapshotFaces(IEnumerable<Renderer> renderers,
            IReadOnlyDictionary<Renderer, bool> originalEnabled, Vector2 min, Vector2 size, float texel)
        {
            // Diagnostic passes disable walls while drawing ground. Their original state,
            // not the temporary current state, defines the independent distance reference.
            var result = new ShoreLineDerivation.WallFootprint(min, size, texel, texel);
            foreach (var renderer in renderers)
            {
                if (!originalEnabled.TryGetValue(renderer, out bool enabled) || !enabled) continue;
                var positions = FacePositions(renderer);
                for (int i = 0; i < positions.Length; i += 3) result.AddTriangle(positions[i], positions[i + 1], positions[i + 2]);
            }
            return result;
        }

        public sealed class PoseClock : IDisposable
        {
            readonly Camera _camera;
            readonly IGameClock _prior;
            readonly ClockPass _pass;
            readonly string[] _names = { "_Time", "_SinTime", "_CosTime", "unity_DeltaTime", "_TimeParameters", "_LastTimeParameters" };
            readonly Vector4[] _values;
            public readonly float Hour;
            public int AppliedFrames => _pass.Applied;
            public PoseClock(Camera camera, float hour)
            {
                _camera = camera; Hour = hour; _prior = GameServices.Clock;
                _values = _names.Select(Shader.GetGlobalVector).ToArray();
                GameServices.Clock = new FixedClock(hour, _prior);
                _pass = new ClockPass(hour * 3600);
                RenderPipelineManager.beginCameraRendering += Begin;
            }
            void Begin(ScriptableRenderContext context, Camera camera)
            {
                if (camera == _camera) camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(_pass);
            }
            public void Dispose()
            {
                RenderPipelineManager.beginCameraRendering -= Begin;
                GameServices.Clock = _prior;
                for (int i = 0; i < _names.Length; i++) Shader.SetGlobalVector(_names[i], _values[i]);
            }
            sealed class ClockPass : ScriptableRenderPass2D
            {
                sealed class Data { public float Seconds; }
                readonly float _seconds;
                public int Applied;
                public ClockPass(float seconds)
                {
                    _seconds = seconds;
                    renderPassEvent2D = RenderPassEvent2D.BeforeRenderingSprites;
                    renderPassSortingLayerID = SortingLayer.layers.OrderBy(l => l.value).First().id;
                }
                public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
                {
                    using var builder = graph.AddUnsafePass<Data>("Shore capture frozen clock", out var data);
                    data.Seconds = _seconds;
                    builder.AllowPassCulling(false); builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((Data d, UnsafeGraphContext context) =>
                    {
                        float t = d.Seconds;
                        context.cmd.SetGlobalVector("_Time", new Vector4(t / 20, t, t * 2, t * 3));
                        context.cmd.SetGlobalVector("_SinTime", new Vector4(Mathf.Sin(t / 8), Mathf.Sin(t / 4), Mathf.Sin(t / 2), Mathf.Sin(t)));
                        context.cmd.SetGlobalVector("_CosTime", new Vector4(Mathf.Cos(t / 8), Mathf.Cos(t / 4), Mathf.Cos(t / 2), Mathf.Cos(t)));
                        context.cmd.SetGlobalVector("unity_DeltaTime", new Vector4(0, 0, 0, 0));
                        var parameters = new Vector4(t, Mathf.Sin(t), Mathf.Cos(t), 0);
                        context.cmd.SetGlobalVector("_TimeParameters", parameters);
                        context.cmd.SetGlobalVector("_LastTimeParameters", parameters);
                        Applied++;
                    });
                }
            }
        }

        sealed class FixedClock : IGameClock
        {
            readonly IGameClock _prior;
            public FixedClock(float hour, IGameClock prior) { HourOfDay = hour; _prior = prior; }
            public double TotalSeconds => (_prior?.DayIndex ?? 0) * 86400.0 + HourOfDay * 3600;
            public double FlowSeconds => TotalSeconds;
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => _prior?.Season ?? default;
            public int Year => _prior?.Year ?? 1;
            public int DayIndex => _prior?.DayIndex ?? 0;
            public int DayOfSeason => _prior?.DayOfSeason ?? 1;
            public Weekday Weekday => _prior?.Weekday ?? default;
            public bool IsMarketDay => _prior?.IsMarketDay ?? false;
            public float HourOfDay { get; }
            public float DayFraction => HourOfDay / 24;
            public bool IsPaused { get; set; } = true;
            public float TimeScale { get; set; }
        }

        public static Color32[] ReadMedian(Camera camera, RenderTexture target)
        {
            var priorTarget = camera.targetTexture; var priorActive = RenderTexture.active;
            var reader = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                for (int i = 0; i < SettleFrames; i++) camera.Render();
                var frames = new Color32[MedianFrames][];
                for (int i = 0; i < MedianFrames; i++)
                {
                    camera.Render(); RenderTexture.active = target;
                    reader.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); reader.Apply();
                    frames[i] = reader.GetPixels32();
                }
                return Median(frames[0], frames[1], frames[2]);
            }
            finally
            {
                camera.targetTexture = priorTarget; RenderTexture.active = priorActive;
                UnityEngine.Object.DestroyImmediate(reader);
            }
        }
        static byte Middle(byte a, byte b, byte c) => (byte)(a + b + c - Math.Min(a, Math.Min(b, c)) - Math.Max(a, Math.Max(b, c)));
        public static Color32[] Median(Color32[] a, Color32[] b, Color32[] c)
        {
            if (a.Length != b.Length || a.Length != c.Length) throw new ArgumentException("Frame dimensions differ.");
            var result = new Color32[a.Length];
            for (int i = 0; i < result.Length; i++) result[i] = new Color32(Middle(a[i].r, b[i].r, c[i].r),
                Middle(a[i].g, b[i].g, c[i].g), Middle(a[i].b, b[i].b, c[i].b), Middle(a[i].a, b[i].a, c[i].a));
            return result;
        }
        [Serializable] public sealed class NoisePixel
        {
            public int x, y, dr, dg, db;
        }
        public static NoisePixel[] Noise(Color32[] a, Color32[] b, int width)
        {
            if (a.Length != b.Length || a.Length % width != 0) throw new ArgumentException("Frame dimensions differ.");
            int height = a.Length / width;
            var result = new List<NoisePixel>();
            for (int i = 0; i < a.Length; i++)
                if (a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b)
                    result.Add(new NoisePixel { x = i % width, y = height - 1 - i / width,
                        dr = Math.Abs(a[i].r - b[i].r), dg = Math.Abs(a[i].g - b[i].g), db = Math.Abs(a[i].b - b[i].b) });
            return result.ToArray();
        }
        public static bool[] NoiseMask(NoisePixel[] pixels, int width, int height)
        {
            var mask = new bool[width * height];
            foreach (var p in pixels)
            {
                if (p.x < 0 || p.x >= width || p.y < 0 || p.y >= height) throw new ArgumentOutOfRangeException(nameof(pixels));
                mask[(height - 1 - p.y) * width + p.x] = true;
            }
            return mask;
        }
    }
}
