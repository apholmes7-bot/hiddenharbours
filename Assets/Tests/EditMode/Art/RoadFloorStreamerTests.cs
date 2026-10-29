using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// NMC H4: the road floor streamer, fed a bake made here through its reader seam (no StreamingAssets, no files). The
    /// chunk in view is decoded on a worker, uploaded, and only then published to the index with its ramp row; a chunk
    /// that leaves the view gives its slot up and leaves the index first; a refused chunk stays the ground; no bake, or
    /// a refused one, binds nothing; closing unbinds. The pool's textures are the engine's, so a device without texture
    /// arrays (the CI's null device among them, if it reports none) skips these.
    /// </summary>
    public class RoadFloorStreamerTests
    {
        const int GridW = 4, GridH = 3;
        const float OriginX = 0f, OriginY = 24f;   // the grid's north-west corner: chunk (cx, cy) spans x 8cx.., y 24 - 8cy down
        Func<string, byte[]> _reader;
        readonly List<Object> _made = new List<Object>();
        Dictionary<string, byte[]> _files;
        readonly ManualResetEventSlim _gate = new ManualResetEventSlim(true);   // shut: a chunk's read waits on the worker

        [SetUp]
        public void SetUp()
        {
            _reader = RoadFloorStreamer.ReadBytes;
            _files = new Dictionary<string, byte[]>();
            _gate.Set();
            RoadFloorStreamer.ReadBytes = path =>
            {
                if (path.EndsWith(".png", StringComparison.Ordinal)) _gate.Wait(10000);
                lock (_files)
                    return _files.TryGetValue(Path.GetFileName(path), out byte[] b) ? b : throw new FileNotFoundException(path);
            };
        }

        [TearDown]
        public void TearDown()
        {
            _gate.Set();
            RoadFloorStreamer.ReadBytes = _reader;
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        [Test]
        public void TheChunkInView_IsPublishedOnlyOnceUploaded_WithItsRampRow()
        {
            RequireTextureArrays();
            Bake(("c1_1", 0.5f), ("c2_1", 1.5f));
            var (streamer, surface, cam) = Make(capacity: 2);
            Look(cam, 1, 1);

            int c11 = 1 * GridW + 1, c21 = 1 * GridW + 2;
            _gate.Reset();
            streamer.Tick();   // plans and starts the load, which waits at the gate: nothing is published before the upload
            Assert.AreEqual(RoadFloorSlots.State.Loading, streamer.Slots.StateOf(c11), "the chunk in view did not start loading");
            Assert.AreEqual(0f, streamer.IndexTexel(c11), "the index named a slot before its maps were uploaded");
            _gate.Set();
            TickUntil(streamer, () => streamer.Slots.StateOf(c11) == RoadFloorSlots.State.Resident);

            int slot = streamer.Slots.SlotOf(c11);
            Assert.AreEqual(slot + 1, streamer.IndexTexel(c11), "the resident chunk's index texel is not its slot + 1");
            Assert.AreEqual(0f, streamer.IndexTexel(c21), "a chunk out of view (no ring) was published");
            Color p = streamer.RampTexel(RoadFloorMaps.RampWidth - 1, slot);
            Assert.AreEqual(new Color(0.5f, 2f, 0.25f, 1.75f), p, "the slot's parameters are not the chunk's");
            Assert.AreEqual(new Color(1, 101, 21, 255), streamer.RampTexel(0, slot), "the slot's first palette band is not the chunk's");
            Assert.IsTrue(surface.RoadFloorLoaded, "the pool is open but the ground does not have it");
        }

        [Test]
        public void AChunkLeavingTheView_LeavesTheIndexBeforeItsSlotIsReused()
        {
            RequireTextureArrays();
            Bake(("c1_1", 0.5f), ("c2_1", 1.5f));
            var (streamer, _, cam) = Make(capacity: 1);
            int c11 = 1 * GridW + 1, c21 = 1 * GridW + 2;
            Look(cam, 1, 1);
            TickUntil(streamer, () => streamer.Slots.StateOf(c11) == RoadFloorSlots.State.Resident);

            Look(cam, 2, 1);
            _gate.Reset();
            streamer.Tick();
            Assert.AreEqual(RoadFloorSlots.State.Absent, streamer.Slots.StateOf(c11), "the chunk out of view kept the one slot");
            Assert.AreEqual(0f, streamer.IndexTexel(c11), "the evicted chunk is still in the index while its slot loads another");
            Assert.AreEqual(0f, streamer.IndexTexel(c21), "the new chunk was published before its upload");
            _gate.Set();
            TickUntil(streamer, () => streamer.Slots.StateOf(c21) == RoadFloorSlots.State.Resident);
            Assert.AreEqual(streamer.Slots.SlotOf(c21) + 1, streamer.IndexTexel(c21));
        }

        [Test]
        public void ARefusedChunk_StaysTheGround()
        {
            RequireTextureArrays();
            Bake(("c1_1", 0.5f));
            byte[] light = _files["c1_1_light.png"];
            light[light.Length / 2] ^= 0x10;   // its image data's CRC no longer matches
            var (streamer, _, cam) = Make(capacity: 2);
            Look(cam, 1, 1);
            int c11 = 1 * GridW + 1;
            TickUntil(streamer, () => streamer.Slots.StateOf(c11) == RoadFloorSlots.State.Failed);
            Assert.AreEqual(0f, streamer.IndexTexel(c11), "a refused chunk was published");
            Assert.AreEqual(0, streamer.Held, "a refused chunk kept its slot");
        }

        [Test]
        public void NoBake_OrARefusedOne_BindsNothing_AndClosingUnbinds()
        {
            RequireTextureArrays();
            var (streamer, surface, _) = Make(capacity: 2);
            Assert.IsFalse(streamer.Open, "a streamer with no bake opened");
            Assert.IsFalse(surface.RoadFloorLoaded, "the ground has a road floor with no bake");

            _files["RoadFloor.json"] = Encoding.UTF8.GetBytes(Manifest(("c1_1", 0.5f)).Replace("\"version\": 1", "\"version\": 7"));
            streamer.enabled = false;
            streamer.enabled = true;
            Assert.IsFalse(streamer.Open, "a bake of an unknown version opened");

            Bake(("c1_1", 0.5f));
            streamer.enabled = false;
            streamer.enabled = true;
            Assert.IsTrue(streamer.Open, "premise: the bake opens");
            Assert.IsTrue(surface.RoadFloorLoaded, "premise: the ground has the pool");
            streamer.enabled = false;
            Assert.IsFalse(surface.RoadFloorLoaded, "the ground kept the pool after the streamer closed");
        }

        // ============================ helpers ============================

        (RoadFloorStreamer, TerrainSplatSurface, Camera) Make(int capacity)
        {
            var camGo = new GameObject("RoadFloorTestCamera");
            _made.Add(camGo);
            Camera cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 2f;
            cam.aspect = 1f;
            var go = new GameObject("RoadFloorTestGround");
            _made.Add(go);
            go.SetActive(false);
            var surface = go.AddComponent<TerrainSplatSurface>();
            var streamer = go.AddComponent<RoadFloorStreamer>();
            streamer.Configure(surface, "RoadFloorTest", capacity, 2, 2, 0, cam);
            go.SetActive(true);
            return (streamer, surface, cam);
        }

        /// <summary>The camera over chunk (cx, cy)'s centre, its 4 m view inside the chunk.</summary>
        static void Look(Camera cam, int cx, int cy) =>
            cam.transform.position = new Vector3(OriginX + (cx + 0.5f) * 8f, OriginY - (cy + 0.5f) * 8f, -10f);

        static void TickUntil(RoadFloorStreamer streamer, Func<bool> done)
        {
            var clock = Stopwatch.StartNew();
            while (!done())
            {
                if (clock.ElapsedMilliseconds > 10000) Assert.Fail("the streamer did not get there in 10 s");
                streamer.Tick();
                Thread.Sleep(5);
            }
        }

        void Bake(params (string name, float heightMin)[] chunks)
        {
            _files["RoadFloor.json"] = Encoding.UTF8.GetBytes(Manifest(chunks));
            int seed = 1;
            foreach (var (name, _) in chunks)
                foreach (string suffix in RoadFloorMaps.MapSuffixes)
                    _files[name + suffix + ".png"] = RoadFloorTestPngs.Encode(
                        RoadFloorTestPngs.Pattern(RoadFloorMaps.ChunkTexels, seed++), RoadFloorMaps.ChunkTexels, r => (byte)(r % 5));
        }

        static string Manifest(params (string name, float heightMin)[] chunks)
        {
            string pal = string.Join(", ", Enumerable.Range(0, 5).Select(i => $"\"#{i + 1:x2}{i + 101:x2}{i + 21:x2}\""));
            string rows = string.Join(",\n", chunks.Select(c =>
            {
                string[] at = c.name.Substring(1).Split('_');
                return $"  {{ \"name\": \"{c.name}\", \"cx\": {at[0]}, \"cy\": {at[1]}, \"covered\": 1, \"faces\": 0, \"blank\": 0, " +
                       $"\"palettes\": [{pal}], \"heightMin\": {Num(c.heightMin)}, \"heightMax\": 2.5, \"heightRange\": 2, " +
                       "\"heightMaxNear\": 1.75, \"pondMax\": 0.25 }";
            }));
            return "{ \"kit\": \"test\", \"version\": 1, \"size\": 256, \"metresPerChunk\": 8, " +
                   $"\"origin\": {{ \"x\": {Num(OriginX)}, \"y\": {Num(OriginY)} }}, \"grid\": {{ \"w\": {GridW}, \"h\": {GridH} }}, " +
                   "\"role\": { \"channel\": \"light G\", \"floor\": 255, \"face\": 170, \"airFace\": 85, \"none\": 0 },\n" +
                   "\"chunks\": [\n" + rows + "\n] }";
        }

        static string Num(float v) => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        static void RequireTextureArrays()
        {
            if (!SystemInfo.supports2DArrayTextures)
                Assert.Ignore("this graphics device has no texture arrays, so the road floor's pool cannot be made here.");
        }
    }
}
