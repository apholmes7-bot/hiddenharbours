using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// NMC H4: the road floor bake's <c>RoadFloor.json</c>, as <see cref="RoadFloorStreamer"/> reads it. The fields
    /// are the bake's own names (<c>tools/nmc-roads/bake-chunks.cjs</c>); the ones the streamer has no use for (the
    /// rigs' hashes, the tunables, the seams) are left to the file. Parsed with JsonUtility, whose float conversion
    /// TerrainTexArrayBuilder keeps for the same reason: another parser can differ by an ULP.
    ///
    /// <para>Checked rather than trusted (<see cref="Problems"/>): the shader LOADs by slot and texel, and a chunk
    /// placed off the grid or a ramp row too wide would be read out of bounds.</para>
    /// </summary>
    [Serializable]
    public sealed class RoadFloorManifest
    {
        /// <summary>The format this reader knows.</summary>
        public const int KnownVersion = 1;

        public string kit;
        public int version;
        public string networkSha256;
        public int seed;
        public int size;
        public float metresPerChunk;
        public Point origin;
        public Grid grid;
        public Roles role;
        public Chunk[] chunks;

        /// <summary>The grid's north-west corner in world metres.</summary>
        [Serializable] public sealed class Point { public float x, y; }

        /// <summary>The grid in chunks: w east, h south.</summary>
        [Serializable] public sealed class Grid { public int w, h; }

        /// <summary>The light map's G for each role (<see cref="RoadFloorMaps"/>' Role constants).</summary>
        [Serializable] public sealed class Roles { public string channel; public int floor, face, airFace, none; }

        /// <summary>Each map's sha256 over its raw RGBA, rows top down.</summary>
        [Serializable] public sealed class Hashes { public string normal, light, detail, albedo; }

        /// <summary>One chunk: its place on the grid (cx east, cy south), its palettes flat (palette p band b at
        /// p * 5 + b), its height range, the largest height within the sun march's reach, its largest pond.</summary>
        [Serializable]
        public sealed class Chunk
        {
            public string name;
            public int cx, cy, covered, faces, blank;
            public string[] palettes;
            public float heightMin, heightMax, heightRange, heightMaxNear, pondMax;
            public Hashes sha256;
            public int bytes;
        }

        /// <summary>The manifest a file's text holds (not yet checked: see <see cref="Problems"/>).</summary>
        public static RoadFloorManifest Parse(string json) => JsonUtility.FromJson<RoadFloorManifest>(json);

        /// <summary>A chunk's place in the grid's row-major order (row 0 north).</summary>
        public int GridIndex(int cx, int cy) => cy * grid.w + cx;

        /// <summary>Everything that keeps the streamer from binding this bake; empty when it may.</summary>
        public List<string> Problems()
        {
            var p = new List<string>();
            if (version != KnownVersion) p.Add($"version {version}, not {KnownVersion}.");
            if (size != RoadFloorMaps.ChunkTexels) p.Add($"chunks of {size} px, not {RoadFloorMaps.ChunkTexels}.");
            if (metresPerChunk != RoadFloorMaps.ChunkMetres)
                p.Add($"chunks of {metresPerChunk} m, not {RoadFloorMaps.ChunkMetres} ({RoadFloorMaps.PixelsPerMetre} px a metre).");
            if (origin == null || !Finite(origin.x) || !Finite(origin.y)) p.Add("no origin.");
            if (grid == null || grid.w <= 0 || grid.h <= 0) p.Add("no grid.");
            if (role == null || role.floor != RoadFloorMaps.RoleFloor || role.face != RoadFloorMaps.RoleFace
                || role.airFace != RoadFloorMaps.RoleAirFace || role.none != RoadFloorMaps.RoleNone)
                p.Add($"roles other than floor {RoadFloorMaps.RoleFloor}, face {RoadFloorMaps.RoleFace}, " +
                      $"air face {RoadFloorMaps.RoleAirFace}, none {RoadFloorMaps.RoleNone}.");
            if (chunks == null) { p.Add("no chunks."); return p; }
            if (grid == null || grid.w <= 0 || grid.h <= 0) return p;

            var seen = new HashSet<int>();
            var row = new Color[RoadFloorMaps.RampWidth];
            foreach (Chunk c in chunks)
            {
                if (c == null) { p.Add("an empty chunk."); continue; }
                string at = $"'{c.name}'";
                if (c.name != $"c{c.cx}_{c.cy}") p.Add($"{at} is not named for its place ({c.cx}, {c.cy}).");
                if (c.cx < 0 || c.cy < 0 || c.cx >= grid.w || c.cy >= grid.h)
                {
                    p.Add($"{at} lies off the {grid.w} x {grid.h} grid.");
                    continue;
                }
                if (!seen.Add(GridIndex(c.cx, c.cy))) p.Add($"{at} is baked twice.");
                string ramp = RoadFloorMaps.PackRampRow(c, row, 0);
                if (ramp != null) p.Add(ramp);
                if (!Finite(c.heightMin) || !Finite(c.heightMaxNear) || !(c.heightRange > 0f) || !Finite(c.heightRange))
                    p.Add($"{at} has no height range.");
                else if (c.heightMaxNear < c.heightMin)
                    p.Add($"{at}'s largest near height {c.heightMaxNear} is under its least {c.heightMin}.");
                if (!(c.pondMax > 0f) || !Finite(c.pondMax)) p.Add($"{at} has no pond scale.");
            }
            return p;
        }

        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
