using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// NMC H4: the road floor bake's manifest, checked before the streamer binds it. A manifest the bake writes has no
    /// problems; each fault that would have the shader read out of bounds or the wrong chunk is named; the ramp row
    /// holds each palette's band where the shader reads it and the chunk's parameters at x = 160. The checks run on
    /// manifests built here (no engine); one case reads the committed bake when there is one.
    /// </summary>
    public class RoadFloorManifestTests
    {
        const string CommittedBake = "Assets/StreamingAssets/RoadFloor/NineMileCreek/RoadFloor.json";
        const string Network = "Assets/_Project/Data/Terrain/NineMileCreekRoads/NineMileCreekRoadNetwork.json";

        static RoadFloorManifest.Chunk NewChunk(int cx, int cy, int palettes = 2) => new RoadFloorManifest.Chunk
        {
            name = $"c{cx}_{cy}", cx = cx, cy = cy, covered = 10, faces = 0, blank = 0,
            palettes = Enumerable.Range(0, palettes * 5).Select(i => $"#{(i + 1) & 255:x2}{(i + 101) & 255:x2}{(i + 21) & 255:x2}").ToArray(),
            heightMin = -1.5f, heightMax = 2f, heightRange = 3.5f, heightMaxNear = 2.25f, pondMax = 0.4f,
        };

        static RoadFloorManifest NewManifest(params RoadFloorManifest.Chunk[] chunks) => new RoadFloorManifest
        {
            kit = "test", version = 1, size = 256, metresPerChunk = 8f,
            origin = new RoadFloorManifest.Point { x = -380f, y = 280f },
            grid = new RoadFloorManifest.Grid { w = 95, h = 70 },
            role = new RoadFloorManifest.Roles { channel = "light G", floor = 255, face = 170, airFace = 85, none = 0 },
            chunks = chunks,
        };

        [Test]
        public void AManifestAsTheBakeWritesIt_HasNoProblems()
        {
            CollectionAssert.IsEmpty(NewManifest(NewChunk(0, 0), NewChunk(94, 69, 32), NewChunk(40, 20)).Problems());
        }

        [Test]
        public void EachFaultTheShaderWouldMisread_IsNamed()
        {
            void Refused(Action<RoadFloorManifest> fault, string expect)
            {
                RoadFloorManifest m = NewManifest(NewChunk(3, 4), NewChunk(5, 6));
                fault(m);
                List<string> p = m.Problems();
                Assert.IsTrue(p.Any(x => x.Contains(expect)), $"no problem names '{expect}'; got: {string.Join(" | ", p)}");
            }
            Refused(m => m.version = 2, "version");
            Refused(m => m.size = 128, "px");
            Refused(m => m.metresPerChunk = 4f, " m,");
            Refused(m => m.grid = null, "no grid");
            Refused(m => m.origin = null, "no origin");
            Refused(m => m.role.face = 171, "roles");
            Refused(m => m.chunks = null, "no chunks");
            Refused(m => { m.chunks[1].cx = 95; m.chunks[1].name = "c95_6"; }, "off the");
            Refused(m => { m.chunks[1].cx = 3; m.chunks[1].cy = 4; m.chunks[1].name = "c3_4"; }, "twice");
            Refused(m => m.chunks[0].name = "c4_3", "not named for its place");
            Refused(m => m.chunks[0].palettes = NewChunk(0, 0, 33).palettes, "33 palettes");
            Refused(m => m.chunks[0].palettes = m.chunks[0].palettes.Take(7).ToArray(), "not whole palettes");
            Refused(m => m.chunks[0].palettes[3] = "#12345g", "#rrggbb");
            Refused(m => m.chunks[0].heightRange = 0f, "no height range");
            Refused(m => m.chunks[0].heightMaxNear = -2f, "under its least");
            Refused(m => m.chunks[0].pondMax = 0f, "no pond scale");
        }

        [Test]
        public void TheRampRow_HoldsEachBandAtItsColumn_AndTheChunksParametersAt160()
        {
            RoadFloorManifest.Chunk c = NewChunk(1, 1, 3);
            int w = RoadFloorMaps.RampWidth;
            var ramp = new Color[w * 2];
            for (int x = 0; x < w; x++) ramp[w + x] = Color.magenta;   // stale: must be overwritten

            Assert.IsNull(RoadFloorMaps.PackRampRow(c, ramp, w));
            Assert.AreEqual(new Color(1, 101, 21, 255), ramp[w + 0], "palette 0 band 0 (x = 0)");
            Assert.AreEqual(new Color(14, 114, 34, 255), ramp[w + 13], "palette 2 band 3 (x = 2 * 5 + 3)");
            Assert.AreEqual(default(Color), ramp[w + 15], "palette 3 does not exist: x = 15 must be clear");
            Assert.AreEqual(default(Color), ramp[w + 159], "palette 31 does not exist: x = 159 must be clear");
            Assert.AreEqual(new Color(-1.5f, 3.5f, 0.4f, 2.25f), ramp[w + 160],
                "x = 160 is not (heightMin, heightRange, pondMax, heightMaxNear)");
            Assert.AreEqual(default(Color), ramp[0], "the pack wrote outside its own row");
            Assert.IsNotNull(RoadFloorMaps.PackRampRow(c, new Color[w], 1), "a row past the ramp's end was packed");
        }

        [Test]
        public void TheCommittedBake_ParsesWithoutProblems_AndIsTheNetworksBake()
        {
            if (!File.Exists(CommittedBake))
                Assert.Ignore("no road floor bake is committed yet (NMC H4 Phase B commits it).");
            RoadFloorManifest m = RoadFloorManifest.Parse(File.ReadAllText(CommittedBake));
            CollectionAssert.IsEmpty(m.Problems(), "the committed bake would be refused by the streamer");
            string sha;
            using (var h = System.Security.Cryptography.SHA256.Create())
                sha = string.Concat(h.ComputeHash(File.ReadAllBytes(Network)).Select(b => b.ToString("x2")));
            Assert.AreEqual(sha, m.networkSha256,
                "the committed bake is not the committed network's: re-run tools/nmc-roads/bake-chunks.cjs");
        }
    }
}
