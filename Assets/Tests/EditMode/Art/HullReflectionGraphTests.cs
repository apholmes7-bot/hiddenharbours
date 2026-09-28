using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Records the production pass with Core 17.5's real builders. Never renders, compiles a graph,
    /// creates a GPU RT, or skips on Null Device. See docs/design/hull-reflection-current-frame.md
    /// for the audited API boundary, including why even new RenderGraph() is forbidden here.
    /// </summary>
    public class HullReflectionGraphTests
    {
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        readonly List<Object> _objects = new List<Object>();
        readonly List<RTHandle> _handles = new List<RTHandle>();
        IsoFacetHullFeature.HullPass _pass;
        Material _resolve;

        [SetUp]
        public void SetUp()
        {
            var shader = Shader.Find("Hidden/HiddenHarbours/IsoFacetResolve");
            Assert.That(shader, Is.Not.Null);
            _resolve = new Material(shader);
            _objects.Add(_resolve);
            _pass = new IsoFacetHullFeature.HullPass(AllocateUncreatedTarget) { ResolveMaterial = _resolve };
        }

        [TearDown]
        public void TearDown()
        {
            _pass?.Dispose();
            // Production disposal must release the wrappers, including both cameras after resize.
            foreach (var handle in _handles)
                Assert.That(handle.rt, Is.Null, "Persistent target wrapper leaked by HullPass.Dispose");
            foreach (var obj in _objects) Object.DestroyImmediate(obj);
            _objects.Clear();
            _handles.Clear();
        }

        void AllocateUncreatedTarget(ref RTHandle handle, RenderTextureDescriptor desc, string name)
        {
            if (handle != null && handle.rt.width == desc.width && handle.rt.height == desc.height)
                return;
            handle?.Release();
            var texture = new RenderTexture(desc) { name = name };
            _objects.Add(texture);
            // Core RTHandleSystem.cs:1452-1467 only WRAPS this CPU descriptor object.
            // Default ownership=false: releasing the handle cannot release/create a GPU texture.
            handle = RTHandles.Alloc(texture);
            _handles.Add(handle);
            Assert.That(texture.IsCreated(), Is.False, "Fixture must never create a GPU target");
        }

        Camera Camera(int width, int height, Vector3 position)
        {
            var go = new GameObject("R2 recording-only camera");
            _objects.Add(go);
            var camera = go.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.pixelRect = new Rect(0, 0, width, height);
            camera.transform.position = position;
            return camera;
        }

        RecordedGraph Record(Camera camera, int width, int height, bool hulls = true,
            bool water = true, bool reflections = true, bool guard = true)
        {
            _pass.DrawHulls = hulls;
            _pass.DrawWater = water;
            _pass.DrawReflections = reflections;
            _pass.DrawGuard = guard;
            _pass.DrawFoam = false; // No foam allocation/history or scene services in this fixture.
            var recorded = new RecordedGraph();
            try
            {
                using var frame = new ContextContainer();
                var cameraData = frame.Create<UniversalCameraData>();
                cameraData.camera = camera;
                cameraData.cameraTargetDescriptor = new RenderTextureDescriptor(width, height,
                    RenderTextureFormat.ARGB32, 0) { msaaSamples = 1 };
                frame.Create<UniversalRenderingData>(); // default cullResults; never cull or execute
                _pass.RecordRenderGraph(recorded.Graph, frame);
                recorded.ReadPasses();
                return recorded;
            }
            catch
            {
                recorded.Dispose();
                throw;
            }
        }

        [TestCase(true, true, true, true)]
        [TestCase(true, true, true, false)]
        [TestCase(true, false, true, false)]
        [TestCase(true, true, false, true)]
        [TestCase(false, true, true, false)]
        [TestCase(false, true, false, false)]
        [TestCase(false, false, true, false)]
        [TestCase(true, false, false, false)]
        [TestCase(false, false, false, false)]
        public void HullReflectionGraph_DeclaresCurrentSourceBeforeConsumption(
            bool hulls, bool water, bool reflections, bool guard)
        {
            if (!reflections) AssertIdleBinding();
            var camera = Camera(1920, 1080, new Vector3(5, 11, -10));
            using var graph = Record(camera, 1920, 1080, hulls, water, reflections, guard);
            Assert.That(graph.Find("HH Hull Keyline Resolve") != null, Is.EqualTo(hulls));
            Assert.That(graph.Find("HH Object Reflections") != null, Is.EqualTo(reflections));
            Assert.That(graph.Find("HH Displaced Water") != null, Is.EqualTo(water));
            Assert.That(graph.Find("HH Hull Interior Guard") != null, Is.EqualTo(hulls && water && guard));
            Assert.That(graph.Find("HH Water Depth Before Hulls") != null,
                Is.EqualTo(hulls && water && reflections));
            if (!hulls && !water && !reflections) Assert.That(graph.Passes, Is.Empty);
            graph.AssertEveryReadHasEarlierProducerOrClear();

            var resolve = graph.Find("HH Hull Keyline Resolve");
            var reflect = graph.Find("HH Object Reflections");
            var colour = graph.Find("HH Displaced Water");
            if (hulls)
            {
                var facets = graph.Find("HH Hull Facets");
                Assert.That(facets.Colours.Count, Is.EqualTo(4));
                foreach (int attachment in facets.Colours)
                    graph.AssertReadsLatest(resolve, facets, attachment, "Resolve must read each real facet MRT");
                Assert.That(resolve.Globals.Single().Value, Is.EqualTo(IsoFacetShaderIds.HullScreenTex));
                Assert.That(resolve.Cullable, Is.False);
                if (reflections)
                    graph.AssertReadsLatest(reflect, resolve, resolve.Colours.Single(),
                        "Reflection must explicitly read this recording's current hull resolve");
            }
            if (reflections)
            {
                Assert.That(reflect.Globals.Single().Value, Is.EqualTo(ReflectionShaderIds.ReflectTex));
                Assert.That(reflect.Cullable, Is.False);
                Assert.That(reflect.Tags, Is.EqualTo(new[] { "HHReflect" }));
                Assert.That(reflect.Layers.Single(), Is.EqualTo(ReflectionRegistry.RenderingLayer));
                if (!hulls) Assert.That(reflect.ExplicitReads, Is.Empty);
                if (water)
                    graph.AssertReadsLatest(colour, reflect, reflect.Colours.Single(),
                        "Water colour must explicitly read this recording's reflection");
            }
            if (water)
            {
                Assert.That(colour.Globals.Single().Value, Is.EqualTo(IsoFacetShaderIds.WaterScreenTex));
                Assert.That(colour.Cullable, Is.False);
                Assert.That(colour.Tags.Single(), Is.EqualTo("HHWater"));
                Assert.That(colour.Layers.Single(), Is.EqualTo(DisplacedWaterRegistry.RenderingLayer));
            }
            if (hulls && water && guard)
            {
                var interior = graph.Find("HH Hull Interior Guard");
                graph.AssertReadsLatest(colour, interior, interior.Colours.Single(), "Colour guard read");
                if (reflections)
                    graph.AssertReadsLatest(graph.Find("HH Water Depth Before Hulls"), interior,
                        interior.Colours.Single(), "Depth guard read");
            }
            if (hulls && water && reflections)
            {
                var depth = graph.Find("HH Water Depth Before Hulls");
                var facets = graph.Find("HH Hull Facets");
                Assert.That(depth.Tags.Single(), Is.EqualTo("HHWaterDepth"));
                Assert.That(depth.Layers.Single(), Is.EqualTo(DisplacedWaterRegistry.RenderingLayer));
                Assert.That(depth.Colours, Is.Empty, "Depth pass must not shade a colour attachment");
                Assert.That(depth.ExplicitReads.Any(r => Index(r) == reflect.Colours.Single()), Is.False);
                Assert.That(depth.ExplicitReads.Count, Is.EqualTo(guard ? 1 : 0));
                graph.AssertReadsLatest(facets, depth, depth.Depth, "Hull must depth-test against current water");
                Assert.That(colour.Depth, Is.Not.EqualTo(facets.Depth),
                    "Late water must not use hull-contaminated depth");
                graph.AssertDepth(colour.Depth, "_HHWaterColorZ", 1920, 1080);
                graph.AssertDepth(depth.Depth, "_HHHullZ", 1920, 1080);
                Assert.That(colour.Order, Is.GreaterThan(reflect.Order));
            }
            else if (hulls && water)
                Assert.That(colour.Depth, Is.EqualTo(graph.Find("HH Hull Facets").Depth),
                    "No-reflection path keeps the original shared-depth draw");
            AssertSharedShaderCoverage();
        }

        [TestCase("OneCamera")]
        [TestCase("TwoCameras")]
        [TestCase("Resize")]
        public void HullReflectionSource_CamerasAndResizeAreIndependent(string scenario)
        {
            var a = Camera(1920, 1080, new Vector3(7, 4, -10));
            var b = Camera(1280, 720, new Vector3(-22, 51, -10));
            using var aFirst = Record(a, 1920, 1080);
            var aBacking = AssertSource(aFirst, 1920, 1080);
            if (scenario == "OneCamera")
            {
                using var aNext = Record(a, 1920, 1080);
                Assert.That(AssertSource(aNext, 1920, 1080), Is.SameAs(aBacking));
                AssertSource(aFirst, 1920, 1080); // deferred data was not overwritten
                return;
            }
            using var bFirst = Record(b, 1280, 720);
            var bBacking = AssertSource(bFirst, 1280, 720);
            Assert.That(bBacking, Is.Not.SameAs(aBacking), "Cameras must own distinct resolve targets");
            using (var bNext = Record(b, 1280, 720))
                Assert.That(AssertSource(bNext, 1280, 720), Is.SameAs(bBacking));
            using (var aNext = Record(a, 1920, 1080))
                Assert.That(AssertSource(aNext, 1920, 1080), Is.SameAs(aBacking));
            AssertSource(aFirst, 1920, 1080);
            AssertSource(bFirst, 1280, 720);
            if (scenario == "Resize")
            {
                using (var resized = Record(a, 1280, 720))
                    Assert.That(AssertSource(resized, 1280, 720), Is.Not.SameAs(aBacking));
                using (var unchanged = Record(b, 1280, 720))
                    Assert.That(AssertSource(unchanged, 1280, 720), Is.SameAs(bBacking));
                using var restored = Record(a, 1920, 1080);
                Assert.That(AssertSource(restored, 1920, 1080), Is.Not.SameAs(aBacking));
            }
        }

        static RTHandle AssertSource(RecordedGraph graph, int w, int h)
        {
            var resolve = graph.Find("HH Hull Keyline Resolve");
            var reflect = graph.Find("HH Object Reflections");
            int source = resolve.Colours.Single();
            graph.AssertReadsLatest(reflect, resolve, source, "Camera reflection must read its own current resolve");
            var texture = graph.Texture(source);
            var desc = (TextureDesc)Get(texture, "desc");
            Assert.That(desc.width, Is.EqualTo(w));
            Assert.That(desc.height, Is.EqualTo(h));
            Assert.That(desc.clearBuffer, Is.True, "Empty lists must clear previous silhouettes");
            Assert.That(desc.clearColor, Is.EqualTo(Color.clear));
            Assert.That(Get(texture, "imported"), Is.EqualTo(true));
            foreach (var output in new[] { resolve, reflect, graph.Find("HH Displaced Water") })
            {
                var outputDesc = (TextureDesc)Get(graph.Texture(output.Colours.Single()), "desc");
                Assert.That(outputDesc.width, Is.EqualTo(w), output.Name + " camera width");
                Assert.That(outputDesc.height, Is.EqualTo(h), output.Name + " camera height");
                Assert.That(outputDesc.clearBuffer, Is.True, output.Name + " first/empty frame clear");
                Assert.That(outputDesc.clearColor, Is.EqualTo(Color.clear));
            }
            Assert.That(resolve.Globals.Single().Key, Is.EqualTo(source), "Published hull must be this camera's target");
            var data = Get(resolve.Raw, "data");
            Assert.That(Get(data, "TexSize"), Is.EqualTo(new Vector4(w, h, 0, 0)), "Per-pass camera dimensions");
            graph.AssertDepth(graph.Find("HH Water Depth Before Hulls").Depth, "_HHHullZ", w, h);
            graph.AssertDepth(graph.Find("HH Displaced Water").Depth, "_HHWaterColorZ", w, h);
            return (RTHandle)Get(texture, "graphicsResource");
        }

        static void AssertIdleBinding()
        {
            Assert.That(ReflectionRegistry.Count, Is.Zero);
            Assert.That(IsoFacetHullRegistry.Count + IsoFacetHullRegistry.FigureCount + DisplacedWaterRegistry.Count, Is.Zero);
            Assert.That(FoamInjectionRegistry.ShouldRun, Is.False);
            var field = typeof(ReflectionRegistry).GetField("s_ClearFallback", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            var previousFallback = field.GetValue(null);
            var previousGlobal = Shader.GetGlobalTexture(ReflectionShaderIds.ReflectTex);
            var previousFoamWorld = Shader.GetGlobalVector(FoamShaderIds.BufferWorld);
            // Seed a CPU-only fallback so testing the real idle bind never calls Texture2D.Apply.
            var clear = new Texture2D(1, 1, TextureFormat.RGBAHalf, false, true);
            clear.SetPixel(0, 0, Color.clear);
            IsoFacetHullFeature feature = null;
            try
            {
                field.SetValue(null, clear);
                Shader.SetGlobalTexture(ReflectionShaderIds.ReflectTex, null);
                feature = ScriptableObject.CreateInstance<IsoFacetHullFeature>();
                var data = default(RenderingData);
                feature.AddRenderPasses(null, ref data); // all-idle gate, never reaches camera/renderer
                Assert.That(Shader.GetGlobalTexture(ReflectionShaderIds.ReflectTex), Is.SameAs(clear),
                    "Zero-reflector AddRenderPasses must bind clear before all-idle return");
                Assert.That(clear.GetPixel(0, 0).a, Is.Zero);
            }
            finally
            {
                if (feature != null) Object.DestroyImmediate(feature);
                Shader.SetGlobalTexture(ReflectionShaderIds.ReflectTex, previousGlobal);
                Shader.SetGlobalVector(FoamShaderIds.BufferWorld, previousFoamWorld);
                field.SetValue(null, previousFallback);
                Object.DestroyImmediate(clear);
            }
        }

        static void AssertSharedShaderCoverage()
        {
            string shader = File.ReadAllText(Path.Combine(Application.dataPath,
                "_Project/Art/Shaders/HiddenHarboursWater.shader"));
            Assert.That(shader.Split(new[] { "Varyings vertDisplaced(" }, StringSplitOptions.None).Length, Is.EqualTo(2));
            int body = shader.IndexOf("half4 frag(Varyings IN, const bool depthOnly)", StringComparison.Ordinal);
            int coarse = shader.IndexOf("clip(depth + swashEdgeReach + 1e-4);", body, StringComparison.Ordinal);
            int last = shader.IndexOf("clip(depth + edgeSwash + 1e-4);", coarse, StringComparison.Ordinal);
            int early = shader.IndexOf("if (depthOnly) return 0;", last, StringComparison.Ordinal);
            int reflection = shader.IndexOf("ObjectReflection(worldXY", early, StringComparison.Ordinal);
            Assert.That(body >= 0 && coarse > body && last > coarse && early > last && reflection > early, Is.True,
                "Depth must keep both real coverage clips and exit before the reflection load");
            StringAssert.Contains("ClipHullInterior(IN);\n                return frag(IN, true);", shader.Replace("\r\n", "\n"));
            StringAssert.Contains("ClipHullInterior(IN);\n                return frag(IN, false);", shader.Replace("\r\n", "\n"));
        }

        // Core 17.5 private API adapter. Only introspection/setup lives here; no scheduling rule does.
        static FieldInfo Field(Type type, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var f = t.GetField(name, Members | BindingFlags.DeclaredOnly);
                if (f != null) return f;
            }
            throw new AssertionException("Core 17.5 field changed: " + type.FullName + "." + name);
        }

        static object Get(object target, string name)
        {
            for (var t = target.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(name, Members | BindingFlags.DeclaredOnly);
                if (f != null) return f.GetValue(target);
                var p = t.GetProperty(name, Members | BindingFlags.DeclaredOnly);
                if (p != null) return p.GetValue(target);
            }
            throw new AssertionException("Core 17.5 member changed: " + target.GetType().FullName + "." + name);
        }

        static void Set(object target, string name, object value) => Field(target.GetType(), name).SetValue(target, value);
        static object Call(object target, string name, params object[] args) => target.GetType()
            .GetMethod(name, Members, null, args.Select(a => a.GetType()).ToArray(), null).Invoke(target, args);
        static int Index(object resourceHandle) => (int)Get(resourceHandle, "index");
        static int TextureIndex(object textureHandle) => Index(Get(textureHandle, "handle"));

        sealed class Pass
        {
            internal object Raw;
            internal string Name;
            internal int Order, Depth;
            internal bool Cullable;
            internal List<int> Colours = new List<int>();
            internal List<object> Reads, Writes, ExplicitReads;
            internal Dictionary<int, int> Globals = new Dictionary<int, int>();
            internal List<string> Tags = new List<string>();
            internal List<uint> Layers = new List<uint>();
        }

        sealed class RecordedGraph : IDisposable
        {
            internal readonly RenderGraph Graph;
            internal readonly List<Pass> Passes = new List<Pass>();
            readonly object _resources;
            readonly PropertyInfo _validity;
            readonly object _previousValidity;

            internal RecordedGraph()
            {
                // Do not call new RenderGraph or BeginRecording: their default resources create a
                // shadow RT and execute its clear. Initialize ONLY their managed recording state.
                Graph = (RenderGraph)FormatterServices.GetUninitializedObject(typeof(RenderGraph));
                foreach (string name in new[] { "m_RenderGraphPool", "m_builderInstance", "m_RenderPasses",
                             "m_RendererLists", "m_DebugParameters", "m_DefaultProfilingSamplers", "registeredGlobals" })
                {
                    var field = Field(typeof(RenderGraph), name);
                    field.SetValue(Graph, Activator.CreateInstance(field.FieldType, true));
                }
                var resourceType = Field(typeof(RenderGraph), "m_Resources").FieldType;
                _resources = Activator.CreateInstance(resourceType, Members, null,
                    new[] { Get(Graph, "m_DebugParameters") }, null);
                Set(Graph, "m_Resources", _resources);
                var state = Field(typeof(RenderGraph), "m_RenderGraphState");
                state.SetValue(Graph, Enum.Parse(state.FieldType, "RecordingGraph"));
                // The descriptor/handle epoch is already valid. Do not change ResourceHandle's
                // process-wide epoch; distinct graphs have distinct registries even with equal indices.
                _validity = typeof(RenderGraph).GetProperty("enableValidityChecks", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(_validity, Is.Not.Null);
                _previousValidity = _validity.GetValue(null);
                _validity.SetValue(null, true);
            }

            internal object Texture(int index) => Call(_resources, "GetTextureResource", index);
            internal Pass Find(string name) => Passes.SingleOrDefault(p => p.Name == name);

            internal void ReadPasses()
            {
                foreach (var raw in (IEnumerable)Get(Graph, "m_RenderPasses"))
                {
                    var p = new Pass { Raw = raw, Name = (string)Get(raw, "name"), Order = Passes.Count,
                        Cullable = (bool)Get(raw, "allowPassCulling") };
                    p.Reads = ((IEnumerable)((Array)Get(raw, "resourceReadLists")).GetValue(0)).Cast<object>().ToList();
                    p.Writes = ((IEnumerable)((Array)Get(raw, "resourceWriteLists")).GetValue(0)).Cast<object>().ToList();
                    var implicitReads = ((IEnumerable)Get(raw, "implicitReadsList")).Cast<object>().ToList();
                    p.ExplicitReads = p.Reads.Where(r => !implicitReads.Contains(r)).ToList();
                    p.Depth = TextureIndex(Get(Get(raw, "depthAccess"), "textureHandle"));
                    int max = (int)Get(raw, "colorBufferMaxIndex");
                    var colours = (Array)Get(raw, "colorBufferAccess");
                    for (int i = 0; i <= max; ++i)
                        p.Colours.Add(TextureIndex(Get(colours.GetValue(i), "textureHandle")));
                    foreach (var global in (IEnumerable)Get(raw, "setGlobalsList"))
                        p.Globals.Add(TextureIndex(Get(global, "Item1")), (int)Get(global, "Item2"));
                    var lists = Get(_resources, "m_RendererListResources");
                    // DynamicArray's backing array avoids reflection on its ref-return indexer.
                    var items = (Array)Get(lists, "m_Array");
                    foreach (var list in (IEnumerable)Get(raw, "usedRendererListList"))
                    {
                        var desc = (RendererListParams)Get(items.GetValue((int)Get(list, "handle")), "desc");
                        p.Tags.Add(desc.drawSettings.GetShaderPassName(0).name);
                        p.Layers.Add(desc.filteringSettings.renderingLayerMask);
                    }
                    Passes.Add(p);
                }
            }

            internal void AssertReadsLatest(Pass consumer, Pass producer, int resource, string message)
            {
                Assert.That(consumer.Order, Is.GreaterThan(producer.Order), message + ": producer order");
                var write = producer.Writes.Single(r => Index(r) == resource);
                Assert.That(consumer.ExplicitReads, Does.Contain(write), message + ": declared resource version");
            }

            internal void AssertEveryReadHasEarlierProducerOrClear()
            {
                var written = new HashSet<object>();
                foreach (var pass in Passes)
                {
                    foreach (var read in pass.Reads)
                    {
                        if (written.Contains(read)) continue;
                        var resource = Texture(Index(read));
                        var desc = (TextureDesc)Get(resource, "desc");
                        Assert.That((int)Get(read, "version"), Is.EqualTo(0), "Unproduced version / cycle in " + pass.Name);
                        Assert.That(desc.clearBuffer, Is.True, "Uninitialized texture in " + pass.Name);
                    }
                    foreach (var write in pass.Writes) written.Add(write);
                }
            }

            internal void AssertDepth(int resource, string name, int w, int h)
            {
                var texture = Texture(resource);
                var desc = (TextureDesc)Get(texture, "desc");
                Assert.That(desc.name, Is.EqualTo(name));
                Assert.That(desc.width, Is.EqualTo(w));
                Assert.That(desc.height, Is.EqualTo(h));
                Assert.That(desc.depthBufferBits, Is.EqualTo(DepthBits.Depth32));
                Assert.That(desc.clearBuffer, Is.True);
                Assert.That(desc.msaaSamples, Is.EqualTo(MSAASamples.None));
                Assert.That(Get(texture, "imported"), Is.EqualTo(false));
            }

            public void Dispose()
            {
                // No RenderGraph.Cleanup (that assumes initialized GPU defaults/compiler state).
                try
                {
                    Call(Graph, "ClearCurrentCompiledGraph");
                    Call(_resources, "Cleanup");
                }
                finally { _validity.SetValue(null, _previousValidity); }
            }
        }
    }
}
