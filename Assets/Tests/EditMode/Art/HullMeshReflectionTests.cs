using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine.Experimental.Rendering;
using UnityEngine.TestTools;
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
    /// creates a GPU RT, or skips on Null Device. See docs/design/hull-reflection-mesh.md
    /// for the audited API boundary, including why even new RenderGraph() is forbidden here.
    /// </summary>
    public class HullMeshReflectionTests
    {
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        readonly List<Object> _objects = new List<Object>();
        readonly List<RTHandle> _handles = new List<RTHandle>();
        IsoFacetHullFeature.HullPass _pass;
        Material _resolve;
        object _recordingPool;
        FieldInfo _epoch;
        object _previousEpoch;
        int _recordingIndex;
        GameConfig _previousConfig;
        bool _previousMask;
        ITidalTerrain _previousTerrain;
        Vector4 _previousWaveParams;
        sealed class FlatTerrain : ITidalTerrain
        {
            readonly float _height;
            internal FlatTerrain(float height) { _height = height; }
            public float ElevationAt(Vector2 position) => _height;
        }
        readonly List<IsoFacetHullRenderer> _hulls = new List<IsoFacetHullRenderer>();
        readonly List<IsoCharacterFigureRenderer> _figures = new List<IsoCharacterFigureRenderer>();
        readonly List<HullMeshReflection.Snapshot> _snapshots = new List<HullMeshReflection.Snapshot>();
        DisplacedWaterSurface _waterOwner;

        [SetUp]
        public void SetUp()
        {
            _previousTerrain = GameServices.TidalTerrain;
            GameServices.TidalTerrain = new FlatTerrain(0);
            _previousWaveParams = Shader.GetGlobalVector("_WaveFieldParams");
            Shader.SetGlobalVector("_WaveFieldParams", Vector4.zero);
            _previousConfig = GameServices.Config;
            _previousMask = IsoFacetHullFeature.InteriorMaskEnabled;
            typeof(IsoFacetHullFeature).GetProperty("InteriorMaskEnabled").SetValue(null, false);
            var config = ScriptableObject.CreateInstance<GameConfig>();
            _objects.Add(config);
            GameServices.Config = config;
            // Core pools pass objects across graph instances. Isolate each case from idle objects
            // left by other fixtures, without constructing any graph default/GPU resources.
            _recordingPool = Activator.CreateInstance(typeof(RenderGraphObjectPool), true);
            Call(_recordingPool, "Cleanup");
            _epoch = typeof(TextureHandle).Assembly.GetType("UnityEngine.Rendering.RenderGraphModule.ResourceHandle")
                .GetField("s_CurrentValidBit", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(_epoch, Is.Not.Null);
            _previousEpoch = _epoch.GetValue(null);
            _recordingIndex = 0;
            var shader = Shader.Find("Hidden/HiddenHarbours/IsoFacetResolve");
            Assert.That(shader, Is.Not.Null);
            _resolve = new Material(shader);
            _objects.Add(_resolve);
            _pass = new IsoFacetHullFeature.HullPass(AllocateUncreatedTarget) { ResolveMaterial = _resolve };
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                _pass?.Dispose();
                foreach (var snapshot in _snapshots) snapshot.Dispose();
                foreach (var figure in _figures) HullMeshReflection.Unregister(figure);
                foreach (var hull in _hulls) HullMeshReflection.Unregister(hull);
                if (_waterOwner != null) DisplacedWaterRegistry.Unregister(_waterOwner);
                GameServices.Config = _previousConfig;
                GameServices.TidalTerrain = _previousTerrain;
                Shader.SetGlobalVector("_WaveFieldParams", _previousWaveParams);
                typeof(IsoFacetHullFeature).GetProperty("InteriorMaskEnabled").SetValue(null, _previousMask);
                // Production disposal must release the wrappers, including both cameras after resize.
                foreach (var handle in _handles)
                    Assert.That(handle.rt, Is.Null, "Persistent target wrapper leaked by HullPass.Dispose");
            }
            finally
            {
                foreach (var obj in _objects) Object.DestroyImmediate(obj);
                _objects.Clear();
                _handles.Clear();
                _snapshots.Clear(); _hulls.Clear(); _figures.Clear();
                _waterOwner = null;
                LogAssert.ignoreFailingMessages = false;
                // Match the managed pool cleanup in RenderGraph.CleanupResourcesAndGraph. Discard
                // our cached pass handles BEFORE restoring the epoch of any outside recorder.
                if (_recordingPool != null) Call(_recordingPool, "Cleanup");
                if (_previousEpoch != null) _epoch.SetValue(null, _previousEpoch);
            }
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
            var go = new GameObject("R3 recording-only camera");
            _objects.Add(go);
            var camera = go.AddComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.pixelRect = new Rect(0, 0, width, height);
            camera.aspect = width / (float)height;
            camera.transform.position = position;
            return camera;
        }

        RecordedGraph Record(Camera camera, int width, int height, bool hulls = true,
            bool water = true, bool reflections = true, bool guard = true, bool mesh = true)
        {
            _pass.MeshReflections = mesh;
            _pass.DrawHulls = hulls;
            _pass.DrawWater = water;
            _pass.DrawReflections = reflections;
            _pass.DrawGuard = guard;
            _pass.DrawFoam = false; // No foam allocation/history or scene services in this fixture.
            var recorded = new RecordedGraph(_recordingIndex++);
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


        HullMeshDef Def(string name) => AssetDatabase.LoadAssetAtPath<HullMeshDef>(
            "Assets/_Project/Data/Boats/HullMeshes/" + name + "IsoHullMesh.asset");

        IsoFacetHullRenderer Hull(string name = "Dory")
        {
            HullMeshDef def = Def(name);
            Assert.That(def, Is.Not.Null);
            var go = new GameObject(name + " reflection fixture");
            _objects.Add(go);
            var hull = go.AddComponent<IsoFacetHullRenderer>();
            hull.Configure(new IsoFacetHullSetup {
                Mesh = def.Mesh, DoorLeafClosed = def.DoorLeafClosed, DoorLeafOpen = def.DoorLeafOpen,
                Ramps = def.Ramps.Select(r => r.Colors).ToArray(), RampOffsets = def.Ramps.Select(r => r.Offset).ToArray(),
                InteriorRamps = (def.InteriorRamps ?? Array.Empty<HullMeshDef.Ramp>()).Select(r => r.Colors).ToArray(),
                InteriorRampOffsets = (def.InteriorRamps ?? Array.Empty<HullMeshDef.Ramp>()).Select(r => r.Offset).ToArray(),
                LightN = def.LightN, Gain = def.Gain, Bias = def.Bias, Bayer16 = def.Bayer16,
                Keyline = def.Keyline, PivotPx = def.PivotPx, PxPerMetre = def.PxPerMetre,
                CellW = def.CellW, CellH = def.CellH, ElevationDeg = def.ElevationDeg });
            hull.OverlayRenderer.gameObject.AddComponent<ReflectiveObject>();
            HullMeshReflection.Register(hull); // ExecuteAlways is not required of a figure or fixture.
            _hulls.Add(hull);
            return hull;
        }

        void WaterFrame()
        {
            if (_waterOwner == null)
            {
                var go = new GameObject("R3 calibration publisher"); _objects.Add(go);
                go.AddComponent<SpriteRenderer>(); // DisplacedWaterSurface requires a concrete Renderer.
                _waterOwner = go.AddComponent<DisplacedWaterSurface>();
                Assert.That(_waterOwner, Is.Not.Null, "Calibration publisher must exist before publishing its frame");
            }
            float e = 40 * Mathf.Deg2Rad;
            DisplacedWaterRegistry.PublishIsoDepthFrame(_waterOwner,
                new WaterIsoDepthFrame(0, Mathf.Cos(e), Mathf.Sin(e), 0));
        }

        HullMeshReflection.Snapshot Snapshot(Camera camera = null, int width = 1920, int height = 1080,
            int capacity = 1024, int hulls = 64, long triangles = 1000000)
        {
            camera = camera != null ? camera : Camera(width, height, new Vector3(0, 0, -50));
            camera.orthographicSize = 12;
            var snapshot = new HullMeshReflection.Snapshot(capacity);
            _snapshots.Add(snapshot);
            HullMeshReflection.Prepare(snapshot, camera, width, height, hulls, triangles);
            return snapshot;
        }

        static void Near(Vector3 actual, Vector3 expected) =>
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.00005f));

        [TestCase(0, false)] [TestCase(1, false)] [TestCase(2, false)]
        [TestCase(3, false)] [TestCase(5, false)] [TestCase(7, false)]
        [TestCase(0, true)] [TestCase(1, true)] [TestCase(2, true)]
        [TestCase(3, true)] [TestCase(5, true)] [TestCase(7, true)]
        public void HullMeshMirror_ComposesWithRigToWorld(int heading, bool tilted)
        {
            const float elevation = 40, waterline = 0.53f;
            Vector3 origin = new Vector3(9, -17, 3);
            Matrix4x4 projection = IsoFacetMath.RigToWorld(0, elevation);
            Matrix4x4 posed = IsoFacetMath.RigToWorld(heading, elevation, tilted ? 13 : 0, tilted ? -7 : 0);
            Matrix4x4 mirror = IsoFacetMath.HullReflectionMatrix(origin, elevation, waterline);
            // Use Unity rotations as the independent posed-rig oracle; production uses its own basis arithmetic.
            Quaternion rotation = Quaternion.AngleAxis(heading * 45, Vector3.forward)
                * Quaternion.AngleAxis(tilted ? -7 : 0, Vector3.right)
                * Quaternion.AngleAxis(tilted ? 13 : 0, Vector3.up);
            foreach (Vector3 p in new[] { new Vector3(1, -3, 2), new Vector3(-2, 4, 0), Vector3.zero })
            {
                Vector3 q = rotation * p; q.z = 2 * waterline - q.z;
                Near(mirror.MultiplyPoint3x4(origin + posed.MultiplyPoint3x4(p)), origin + projection.MultiplyPoint3x4(q));
            }
        }

        [TestCase(20, -1)] [TestCase(20, 2)] [TestCase(40, -1)]
        [TestCase(40, 2)] [TestCase(60, -1)] [TestCase(60, 2)]
        public void HullMeshMirror_FixesEachContactAndPreservesX(float elevation, float w)
        {
            Vector3 origin = new Vector3(12, -7, 4);
            Matrix4x4 s = IsoFacetMath.RigToWorld(0, elevation);
            Matrix4x4 mirror = IsoFacetMath.HullReflectionMatrix(origin, elevation, w);
            Vector4 plane = IsoFacetMath.HullReflectionPlane(origin, elevation, w);
            foreach (float z in new[] { w, w - 2, w + 3 })
            {
                Vector3 p = origin + s.MultiplyPoint3x4(new Vector3(-3, 5, z));
                Vector3 r = mirror.MultiplyPoint3x4(p);
                Assert.That(r.x, Is.EqualTo(p.x).Within(0.00001));
                Assert.That(Vector4.Dot(plane, new Vector4(r.x, r.y, r.z, 1)), Is.EqualTo(w - z).Within(0.00001));
                if (z == w) Near(r, p);
                Near(mirror.MultiplyPoint3x4(r), p);
            }
        }

        [TestCase("Calm")] [TestCase("Heave")] [TestCase("Clamp")] [TestCase("NoFrame")]
        public void HullMeshMirror_UsesProductionSettleAndClamp(string scenario)
        {
            var hull = Hull();
            Set(hull, "_hasInteriorFaces", false); // Exercise the production clamp rather than the guard supersession.
            if (scenario != "NoFrame") WaterFrame();
            hull.HeavePixels = scenario == "Calm" ? 0 : -32;
            if (scenario == "Clamp") hull.SetWatertightClamp(0.06f, 0.85f);
            hull.ApplyPose();
            if (scenario == "NoFrame") { Assert.That(HullMeshReflection.Waterline(hull), Is.Zero); return; }
            DisplacedWaterRegistry.TryGetIsoDepthFrame(out WaterIsoDepthFrame frame);
            var field = WaveFieldBridge.ReadPublishedField();
            float expectedDepthHeave = scenario == "Clamp" ? DisplacedWaterMath.WatertightZHeaveMeters(
                -1, 0.06f, 0.85f, Vector2.zero, (float)Get(hull, "_footprintRadiusMeters"), in field, in frame) : hull.ReflectionHeave;
            Assert.That(hull.ReflectionDepthHeave, Is.EqualTo(expectedDepthHeave));
            // At the returned height the production surface-vs-hull depth comparison must meet.
            float w = HullMeshReflection.Waterline(hull);
            float s = frame.SinElev, c = frame.CosElev;
            Assert.That(w / s + expectedDepthHeave * s + hull.ReflectionHeave * c, Is.EqualTo(0).Within(0.00001));
        }

        [TestCase(false, 0)] [TestCase(true, 0)] [TestCase(false, 1)] [TestCase(true, 1)]
        public void HullMeshReflection_UsesCurrentDoorAndFigurePose(bool open, int frame)
        {
            var hull = Hull("CapeIslander");
            hull.ShowDoorLeaf(open); hull.HeadingDirUnits = 1; hull.RollDegrees = 9; hull.ApplyPose();
            var go = new GameObject("R3 attached figure"); _objects.Add(go);
            go.transform.SetParent(hull.PosedMesh, false);
            var figure = go.AddComponent<IsoCharacterFigureRenderer>();
            var def = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>("Assets/_Project/Data/Characters/Skin/fisher.asset");
            figure.Configure(def);
            // Select an authored animated clip, not synthetic skinning in the test.
            var clip = def.Clips.First(c => c.FrameCount > 1);
            Assert.That(figure.SetPose(clip.StateKey, frame), Is.True);
            HullMeshReflection.Register(figure); _figures.Add(figure);
            var first = Snapshot();
            var leaf = first.Packets.Take(first.Count).Single(p => p.Source == hull.ReflectionLeaf);
            Assert.That(leaf.Mesh, Is.SameAs(open ? Def("CapeIslander").DoorLeafOpen : Def("CapeIslander").DoorLeafClosed));
            var person = first.Packets.Take(first.Count).Single(p => p.Source == figure.ReflectionRenderer);
            Assert.That(person.Mesh.vertices, Is.EqualTo(figure.ReflectionMesh.vertices));
            Assert.That(person.ObjectToWorld, Is.EqualTo(figure.ReflectionRenderer.localToWorldMatrix));
            Vector3[] saved = person.Mesh.vertices;
            figure.SetPose(clip.StateKey, 1 - frame);
            Snapshot();
            Assert.That(person.Mesh.vertices, Is.EqualTo(saved), "A later camera must not re-skin a pending packet");
        }

        [TestCase("Dory", 800, 1, 0.11f, 0.308f)]
        [TestCase("CapeIslander", 681, 4, 0.53f, 0.72f)]
        [TestCase("LobsterBoat", 784, 5, 0.4f, 0.5f)]
        public void HullMeshReflection_RealHullFirstHitsHideDeckAndRooms(string name, int deck, int wall, float w, float deckZ)
        {
            var hull = Hull(name); WaterFrame();
            float e = Def(name).ElevationDeg * Mathf.Deg2Rad;
            hull.HeavePixels = -32 * w / (Mathf.Sin(e) * (Mathf.Cos(e) + Mathf.Sin(e)));
            foreach (bool open in new[] { false, true })
            {
                hull.ShowDoorLeaf(open);
                var snapshot = Snapshot();
                var packet = snapshot.Packets.Take(snapshot.Count).Single(p => p.Source == hull.ReflectionHull);
                int[] indices = packet.Mesh.triangles;
                Vector3[] vertices = packet.Mesh.vertices;
                // Semantic witnesses: doryIsoRig.js thwarts u=.60; Cape cockpit DECK=.72;
                // lobster cockpit DECK=.50. These pinned triangles must retain their source identity.
                for (int i = 0; i < 3; i++) Assert.That(vertices[indices[deck * 3 + i]].z, Is.EqualTo(deckZ).Within(0.000001));
                Vector3 deckCenter = Center(vertices, indices, deck);
                Vector3 expected = name == "Dory" ? new Vector3(0.15791369f, 0.41666667f, 0.308f)
                    : name == "CapeIslander" ? new Vector3(-0.39520001f, -6.08426682f, 0.72f)
                    : new Vector3(0.51862637f, -5.84f, 0.5f);
                Near(deckCenter, expected);
                var full = FirstHit(snapshot, packet, deckCenter, false);
                var sabotage = FirstHit(snapshot, packet, deckCenter, true);
                Assert.That(full.Height, Is.LessThan(0), "Nearest complete-shell hit must be a below-plane blocker");
                Assert.That(sabotage.Packet, Is.SameAs(packet));
                Assert.That(sabotage.Triangle, Is.EqualTo(deck), "Dropping blockers must expose the independently identified deck");
                var exterior = FirstHit(snapshot, packet, Center(vertices, indices, wall), false);
                Assert.That(exterior.Height, Is.GreaterThan(0), "Positive exterior colour coverage; an empty image cannot pass");
                var miss = FirstHit(snapshot, packet, new Vector3(100, 0, 0), false);
                Assert.That(miss.Packet, Is.Null);
            }
        }

        static Vector3 Center(Vector3[] vertices, int[] indices, int triangle) =>
            (vertices[indices[triangle * 3]] + vertices[indices[triangle * 3 + 1]] + vertices[indices[triangle * 3 + 2]]) / 3;

        struct Hit { internal HullMeshReflection.Packet Packet; internal int Triangle; internal float Height; }
        static Hit FirstHit(HullMeshReflection.Snapshot snapshot, HullMeshReflection.Packet target, Vector3 localPoint, bool dropBlockers)
        {
            Vector3 point = (target.Mirror * target.ObjectToWorld).MultiplyPoint3x4(localPoint);
            Vector3 origin = point - Vector3.forward * 100;
            double nearest = double.PositiveInfinity;
            Hit result = default;
            foreach (var packet in snapshot.Packets.Take(snapshot.Count))
            {
                Vector3[] vertices = packet.Mesh.vertices;
                int[] indices = packet.Mesh.triangles;
                var tags = new List<Vector2>(); packet.Mesh.GetUVs(1, tags);
                Matrix4x4 matrix = packet.Mirror * packet.ObjectToWorld;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    // The real hull UV channel is the independent room label, never UV0.w.
                    if (tags.Count > 0 && tags[indices[i]].y > 0.5f) continue;
                    Vector3 a = matrix.MultiplyPoint3x4(vertices[indices[i]]);
                    Vector3 b = matrix.MultiplyPoint3x4(vertices[indices[i + 1]]);
                    Vector3 c = matrix.MultiplyPoint3x4(vertices[indices[i + 2]]);
                    // Orthographic two-sided barycentric intersection in double precision.
                    double ax = a.x - origin.x, ay = a.y - origin.y;
                    double bx = b.x - origin.x, by = b.y - origin.y;
                    double cx = c.x - origin.x, cy = c.y - origin.y;
                    double determinant = (by - cy) * (ax - cx) + (cx - bx) * (ay - cy);
                    if (Math.Abs(determinant) < 1e-12) continue;
                    double u = ((by - cy) * -cx + (cx - bx) * -cy) / determinant;
                    double v = ((cy - ay) * -cx + (ax - cx) * -cy) / determinant;
                    double t = u * a.z + v * b.z + (1 - u - v) * c.z - origin.z;
                    if (u < -1e-6 || v < -1e-6 || u + v > 1.000001 || t < 0 || t >= nearest) continue;
                    Vector3 reflected = origin + Vector3.forward * (float)t;
                    Vector3 original = packet.Mirror.MultiplyPoint3x4(reflected); // mirror is an involution
                    float height = Vector4.Dot(packet.Plane, new Vector4(original.x, original.y, original.z, 1));
                    if (dropBlockers && height < 0) continue;
                    nearest = t; result = new Hit { Packet = packet, Triangle = i / 3, Height = height };
                }
            }
            return result;
        }

        [TestCase("Top")] [TestCase("Side")] [TestCase("Invisible")] [TestCase("Margin")]
        public void HullMeshReflection_BoundsIncludeOffscreenSource(string scenario)
        {
            var hull = Hull();
            var camera = Camera(320, 180, new Vector3(0, 0, -10));
            camera.orthographicSize = 2;
            if (scenario == "Top") hull.transform.position = new Vector3(0, 3.4f, 0);
            if (scenario == "Side") hull.transform.position = new Vector3(3.6f, 0, 0);
            if (scenario == "Invisible") hull.transform.position = new Vector3(100, 100, 0);
            var snapshot = new HullMeshReflection.Snapshot(32); _snapshots.Add(snapshot);
            HullMeshReflection.Prepare(snapshot, camera, 320, 180, 64, 1000000);
            Assert.That(snapshot.Count > 0, Is.EqualTo(scenario != "Invisible"));
            if (scenario == "Margin")
            {
                var pixel = new Bounds(new Vector3(0, 2 + 1f / 90, 0), Vector3.zero);
                Assert.That(HullMeshReflection.OverlapsTarget(pixel, camera, 320, 180), Is.True);
            }
            if (snapshot.Count > 0)
                foreach (Vector3 vertex in hull.ReflectionMesh.vertices)
                {
                    var packet = snapshot.Packets[0];
                    var point = (packet.Mirror * packet.ObjectToWorld).MultiplyPoint3x4(vertex);
                    Assert.That(Vector3.Distance(packet.Bounds.ClosestPoint(point), point), Is.LessThan(0.00001));
                }
        }

        [TestCase("Reverse")] [TestCase("Ties")] [TestCase("Overflow")]
        public void HullMeshReflection_SelectionIsOrderIndependent(string scenario)
        {
            var a = Hull(); var b = Hull("CapeIslander");
            b.transform.position = scenario == "Ties" ? Vector3.zero : Vector3.right;
            var first = Snapshot();
            HullMeshReflection.Unregister(a); HullMeshReflection.Unregister(b);
            HullMeshReflection.Register(b); HullMeshReflection.Register(a);
            var second = Snapshot();
            Assert.That(second.Packets.Take(second.Count).Select(p => p.Source),
                Is.EqualTo(first.Packets.Take(first.Count).Select(p => p.Source)));
            if (scenario == "Overflow")
                foreach (var limits in new[] { (1, 1024, 1000000L), (64, 1, 1000000L), (64, 1024, 1L) })
                {
                    var overflow = Snapshot(hulls: limits.Item1, capacity: limits.Item2, triangles: limits.Item3);
                    Assert.That(overflow.Overflow, Is.True);
                    Assert.That(overflow.Count, Is.Zero);
                }
        }

        [TestCase(true, true, true, true)] [TestCase(true, true, false, true)]
        [TestCase(true, false, true, false)] [TestCase(false, true, true, true)]
        [TestCase(false, true, false, false)] [TestCase(false, false, true, true)]
        [TestCase(true, false, false, false)] [TestCase(true, true, true, false)]
        [TestCase(false, false, false, false)]
        public void HullMeshReflectionGraph_DeclaresCurrentSourceBeforeConsumption(bool hulls, bool water, bool trees, bool mesh)
        {
            Hull();
            var camera = Camera(1920, 1080, new Vector3(0, 0, -50));
            using var graph = Record(camera, 1920, 1080, hulls, water, trees, true, mesh);
            var geometry = graph.Find("HH Hull Mesh Reflections");
            var resolve = graph.Find("HH Hull Mesh Reflection Resolve");
            var colour = graph.Find("HH Displaced Water");
            Assert.That(geometry != null, Is.EqualTo(mesh));
            Assert.That(graph.Find("HH Hull Facets") != null, Is.EqualTo(hulls));
            Assert.That(graph.Find("HH Object Reflections") != null, Is.EqualTo(trees));
            Assert.That(colour != null, Is.EqualTo(water));
            Assert.That(graph.Find("HH Water Depth Before Hulls"), Is.Null);
            graph.AssertEveryReadHasEarlierProducerOrClear();
            if (mesh)
            {
                Assert.That(geometry.Colours.Count, Is.EqualTo(4));
                foreach (int attachment in geometry.Colours)
                    graph.AssertReadsLatest(resolve, geometry, attachment, "Resolve reads actual current geometry, including marker");
                graph.AssertDepth(geometry.Depth, "_HHMeshReflectZ", 1920, 1080);
                Assert.That(resolve.Globals.Single().Value, Is.EqualTo(ReflectionShaderIds.ReflectTex));
                Assert.That(resolve.Cullable, Is.False);
                if (trees) Assert.That(resolve.Order, Is.GreaterThan(graph.Find("HH Object Reflections").Order));
                if (water) graph.AssertReadsLatest(colour, resolve, resolve.Colours.Single(), "Current mesh reflection reaches water");
                if (hulls) Assert.That(resolve.Order, Is.LessThan(graph.Find("HH Hull Keyline Resolve").Order));
            }
            else if (water && trees)
                graph.AssertReadsLatest(colour, graph.Find("HH Object Reflections"),
                    graph.Find("HH Object Reflections").Colours.Single(), "Legacy reflection edge");
            if (hulls && water)
                Assert.That(colour.Depth, Is.EqualTo(graph.Find("HH Hull Facets").Depth), "Original shared-depth path remains");
        }

        [TestCase("OneCamera")] [TestCase("TwoCameras")] [TestCase("Resize")]
        public void HullMeshReflectionSource_CamerasAndResizeAreIndependent(string scenario)
        {
            var hull = Hull();
            var a = Camera(1920, 1080, new Vector3(0, 0, -50));
            var b = scenario == "TwoCameras" ? Camera(1280, 720, new Vector3(1, 2, -20)) : a;
            using var first = Record(a, 1920, 1080);
            var dataA = Get(first.Find("HH Hull Mesh Reflection Resolve").Raw, "data");
            var snapshotA = (HullMeshReflection.Snapshot)Get(dataA, "MeshSnapshot");
            RTHandle outputA = snapshotA.Output;
            Matrix4x4 saved = snapshotA.Packets[0].ObjectToWorld;
            hull.HeavePixels = 12; hull.HeadingDirUnits = 1;
            int w = scenario == "OneCamera" ? 1920 : 1280, h = scenario == "OneCamera" ? 1080 : 720;
            using var next = Record(b, w, h);
            var dataB = Get(next.Find("HH Hull Mesh Reflection Resolve").Raw, "data");
            Assert.That(Get(dataB, "MeshSnapshot"), Is.Not.SameAs(snapshotA));
            Assert.That(Get(dataA, "TexSize"), Is.EqualTo(new Vector4(1920, 1080, 0, 0)));
            Assert.That(Get(dataB, "TexSize"), Is.EqualTo(new Vector4(w, h, 0, 0)));
            Assert.That(snapshotA.Packets[0].ObjectToWorld, Is.EqualTo(saved));
            Assert.That(outputA.rt, Is.Not.Null, "Resize cannot release a pending camera's target");
            Assert.That(outputA.rt.width, Is.EqualTo(1920));
            var snapshotB = (HullMeshReflection.Snapshot)Get(dataB, "MeshSnapshot");
            Assert.That(Get(dataB, "Material"), Is.Not.SameAs(Get(dataA, "Material")));
            if (scenario != "OneCamera") Assert.That(snapshotB.Output, Is.Not.SameAs(outputA));
            var desc = (TextureDesc)Get(next.Texture(next.Find("HH Hull Mesh Reflections").Colours[0]), "desc");
            Assert.That(desc.width, Is.EqualTo(w)); Assert.That(desc.height, Is.EqualTo(h));
        }

        [TestCase("Empty")] [TestCase("Disabled")] [TestCase("OutOfGate")]
        [TestCase("OutOfView")] [TestCase("Overflow")]
        public void HullMeshReflection_IdleAndDisableBindClear(string scenario)
        {
            var camera = Camera(320, 180, new Vector3(0, 0, -10));
            if (scenario != "Empty")
            {
                var hull = Hull();
                using (Record(camera, 320, 180, reflections: false)) { }
                if (scenario == "Disabled") hull.ReflectionSource.enabled = false;
                if (scenario == "OutOfGate") GameServices.TidalTerrain = new FlatTerrain(1000);
                if (scenario == "OutOfView") hull.transform.position = new Vector3(1000, 1000, 0);
                if (scenario == "Overflow")
                {
                    GameServices.Config.HullReflectionMaxTriangles = 1;
                    LogAssert.Expect(LogType.Warning, "[HullMeshReflection] Submission budget exceeded; this camera draws no mesh reflections.");
                }
            }
            Shader.SetGlobalTexture(ReflectionShaderIds.ReflectTex, Texture2D.whiteTexture);
            using var graph = Record(camera, 320, 180, hulls: false, water: false, reflections: false);
            Assert.That(graph.Passes, Is.Empty);
            Assert.That(Shader.GetGlobalTexture(ReflectionShaderIds.ReflectTex).name, Is.EqualTo("HHReflectTexFallback"));
            Assert.That(GameServices.HullMeshReflections, Is.False, "Ship default stays off");
        }

        [TestCase(1)] [TestCase(2)]
        public void HullMeshReflection_TargetBudgetMatchesQuality(int divisor)
        {
            Hull(); GameServices.Config.HullReflectionResolutionDivisor = divisor;
            using var graph = Record(Camera(1920, 1080, new Vector3(0, 0, -50)), 1920, 1080);
            var geometry = graph.Find("HH Hull Mesh Reflections");
            int w = 1920 / divisor, h = 1080 / divisor;
            for (int i = 0; i < 4; i++)
            {
                var desc = (TextureDesc)Get(graph.Texture(geometry.Colours[i]), "desc");
                Assert.That(desc.width, Is.EqualTo(w)); Assert.That(desc.height, Is.EqualTo(h));
                Assert.That(desc.format, Is.EqualTo(i == 3 ? GraphicsFormat.R32_SFloat : GraphicsFormat.R8G8B8A8_SRGB));
                Assert.That(desc.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(Get(graph.Texture(geometry.Colours[i]), "imported"), Is.EqualTo(false));
            }
            graph.AssertDepth(geometry.Depth, "_HHMeshReflectZ", w, h);
            var output = (TextureDesc)Get(graph.Texture(graph.Find("HH Hull Mesh Reflection Resolve").Colours.Single()), "desc");
            Assert.That(output.width, Is.EqualTo(1920)); Assert.That(output.height, Is.EqualTo(1080));
            Assert.That(20L * w * h, Is.EqualTo(divisor == 1 ? 41472000L : 10368000L));
        }

        [TestCase(false, false)] [TestCase(true, false)] [TestCase(true, true)]
        public void HullMeshReflection_PreservesStyleAndLitMetadata(bool keyline, bool lit)
        {
            var hull = Hull(); Set(hull.ReflectionSource, "_nightLitSource", lit);
            _pass.KeylineFlood = keyline;
            using var graph = Record(Camera(320, 180, new Vector3(0, 0, -50)), 320, 180);
            var data = Get(graph.Find("HH Hull Mesh Reflection Resolve").Raw, "data");
            Assert.That(Get(data, "KeylineFlood"), Is.EqualTo(keyline));
            Assert.That(Get(data, "FigureInk"), Is.EqualTo(IsoFacetFigureInk.Value));
            var snapshot = (HullMeshReflection.Snapshot)Get(data, "MeshSnapshot");
            var packet = snapshot.Packets[0];
            int id = Mathf.RoundToInt(packet.Properties.GetFloat(IsoFacetShaderIds.HullId) * 255);
            Assert.That(snapshot.Lit[id], Is.EqualTo(lit ? 1 : 0));
            Assert.That(packet.Material.GetTexture(IsoFacetShaderIds.RampTex),
                Is.SameAs(hull.ReflectionHull.sharedMaterial.GetTexture(IsoFacetShaderIds.RampTex)));
            Assert.That(Get(data, "Material"), Is.Not.SameAs(_resolve), "Reflection resolve state cannot mutate the normal material");
            var normal = new MaterialPropertyBlock(); hull.ReflectionHull.GetPropertyBlock(normal);
            Assert.That(normal.HasMatrix(HullMeshReflection.MirrorId), Is.False);
            Assert.That(normal.GetFloat(IsoFacetShaderIds.HullId), Is.EqualTo(hull.HullId / 255f));
            HullMeshReflection.SetMode(false);
            Assert.That(hull.OverlayRenderer.sharedMaterial.GetShaderPassEnabled("HHReflect"), Is.True);
        }

        [Test]
        public void HullMeshReflection_ShaderCompiles()
        {
            LogAssert.ignoreFailingMessages = true;
            foreach (string name in new[] { "HiddenHarbours/IsoFacet", "Hidden/HiddenHarbours/IsoFacetResolve" })
            {
                var shader = Shader.Find(name); Assert.That(shader, Is.Not.Null);
                var material = new Material(shader); _objects.Add(material);
                foreach (string keyword in new[] { "", "HH_LEVEL_GATE", "HH_FIGURE" })
                {
                    material.DisableKeyword("HH_LEVEL_GATE"); material.DisableKeyword("HH_FIGURE");
                    if (keyword.Length != 0) material.EnableKeyword(keyword);
                    for (int pass = 0; pass < material.passCount; pass++) ShaderUtil.CompilePass(material, pass, true);
                }
                Assert.That(ShaderUtil.GetShaderMessages(shader).Where(m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error), Is.Empty);
            }
        }

        [TestCase("Level")] [TestCase("Heel")] [TestCase("Heave")]
        [TestCase("Half")]
        public void HullMeshReflection_PacketsRetainCutOccluders(string scenario)
        {
            var hull = Hull("CapeIslander"); WaterFrame();
            hull.HeavePixels = scenario == "Heave" ? -24 : -16;
            hull.RollDegrees = scenario == "Heel" ? 15 : 0;
            var snapshot = Snapshot(width: scenario == "Half" ? 960 : 1920, height: scenario == "Half" ? 540 : 1080);
            var packet = snapshot.Packets.Take(snapshot.Count).Single(p => p.Source == hull.ReflectionHull);
            Assert.That(packet.Mesh, Is.SameAs(Def("CapeIslander").Mesh));
            Assert.That(packet.Triangles * 3, Is.EqualTo(packet.Mesh.GetIndexCount(0)));
            Assert.That(packet.Pass, Is.EqualTo(packet.Material.FindPass("HHHullMeshReflection")));
            Assert.That(packet.Properties.GetVector(HullMeshReflection.PlaneId), Is.EqualTo(packet.Plane));
            bool below = false, above = false;
            foreach (Vector3 vertex in packet.Mesh.vertices)
            {
                Vector3 world = packet.ObjectToWorld.MultiplyPoint3x4(vertex);
                float s = Vector4.Dot(packet.Plane, new Vector4(world.x, world.y, world.z, 1));
                below |= s < 0; above |= s > 0;
                Vector3 reflected = packet.Mirror.MultiplyPoint3x4(world);
                Assert.That(Vector3.Distance(packet.Bounds.ClosestPoint(reflected), reflected), Is.LessThan(0.00001));
            }
            Assert.That(below && above, Is.True, "Both colour and blocker geometry must remain submitted");
        }

        [TestCase("Hull")] [TestCase("Trees")] [TestCase("Ink")] [TestCase("Half")]
        public void HullMeshReflection_CutOutputAndResolveAreWired(string scenario)
        {
            Hull(); _pass.KeylineFlood = scenario == "Ink";
            GameServices.Config.HullReflectionResolutionDivisor = scenario == "Half" ? 2 : 1;
            using var graph = Record(Camera(320, 180, new Vector3(0, 0, -50)), 320, 180, reflections: scenario == "Trees");
            var geometry = graph.Find("HH Hull Mesh Reflections");
            var resolve = graph.Find("HH Hull Mesh Reflection Resolve");
            graph.AssertReadsLatest(resolve, geometry, geometry.Colours[2], "Actual marker target edge");
            string facet = File.ReadAllText("Assets/_Project/Art/Shaders/HiddenHarboursIsoFacet.shader");
            string shader = File.ReadAllText("Assets/_Project/Art/Shaders/HiddenHarboursIsoFacetResolve.shader");
            string include = File.ReadAllText("Assets/_Project/Art/Shaders/Include/HullReflection.hlsl");
            int branch = facet.IndexOf("if (reflection && i.reflectionHeight < 0)", StringComparison.Ordinal);
            int end = facet.IndexOf("return blocker;", branch, StringComparison.Ordinal);
            string block = facet.Substring(branch, end - branch);
            foreach (string statement in new[] { "blocker.facet = 0;", "blocker.dark = 0;",
                         "blocker.key = float4(0, 0, 0, HH_MESH_BLOCKER);", "blocker.depth = i.reflectionDepth;" })
                StringAssert.Contains(statement, block);
            StringAssert.DoesNotContain("discard", block);
            StringAssert.Contains("return HHFacetVertex(v, true)", facet);
            StringAssert.Contains("return HHFacetFragment(i, true)", facet);
            string pass = facet.Substring(facet.IndexOf("Name \"HHHullMeshReflection\"", StringComparison.Ordinal));
            foreach (string contract in new[] { "#pragma vertex vertReflection", "#pragma fragment fragReflection", "Cull Off", "ZWrite On", "ZTest LEqual" })
                StringAssert.Contains(contract, pass);
            StringAssert.Contains("reflection ? i.lvl.y > 0.5 : HHLevelDiscards(i.lvl)", facet);
            int marker = shader.IndexOf("if (HHMeshBlockerAt(p)) return 0;", StringComparison.Ordinal);
            int colour = shader.IndexOf("float4 c = frag(input);", StringComparison.Ordinal);
            Assert.That(marker, Is.GreaterThan(0)); Assert.That(colour, Is.GreaterThan(marker));
            StringAssert.Contains("input.positionCS.xy = p;", shader);
            StringAssert.Contains("HHMeshIsBlocker(_HHKeyTex.Load(int3(p, 0)).a)", include);
            StringAssert.Contains("Blend One OneMinusSrcAlpha", shader);
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

            internal RecordedGraph(int executionIndex)
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
                // BeginRecording calls this CPU-only step before initializing its GPU defaults.
                // Clear() intentionally retains pooled attachment slots: a new epoch makes their
                // old handles invalid. Distinct registries alone do NOT invalidate those handles.
                Call(_resources, "BeginRenderGraph", executionIndex);
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
                        var backing = Get(resource, "graphicsResource") as RTHandle;
                        if (backing != null && ReferenceEquals(Get(backing, "m_ExternalTexture"), ReflectionRegistry.ClearFallback))
                        {
                            Assert.That((int)Get(read, "version"), Is.Zero);
                            continue; // Constant initialized external fallback, never a render target/history read.
                        }
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
