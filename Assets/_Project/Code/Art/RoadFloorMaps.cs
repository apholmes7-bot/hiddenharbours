using System.Globalization;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// NMC H4: the road floor's chunk format (road / path kit v4), the one contract between the bake
    /// (<c>tools/nmc-roads/bake-chunks.cjs</c>), <see cref="RoadFloorStreamer"/> and the shader's
    /// <c>Include/RoadFloor4.hlsl</c>. Each constant here is an <c>RF4_*</c> define there, and
    /// RoadFloor4SourceTests holds each pair equal.
    ///
    /// <para>A chunk is 8 m of the region's plan grid, <see cref="ChunkTexels"/> pixels a side at
    /// <see cref="PixelsPerMetre"/>, in three maps the streamer keeps (normal and pond, sky visibility, role,
    /// height and palette, marks and tips; the bake's albedo is a preview it never streams) and one row of the
    /// road ramp: up to <see cref="MaxPalettes"/> palettes of <see cref="Bands"/> bands, then the chunk's
    /// parameters. The owner's ruling Q1 (a) widened that row to 32 palettes for road chunks only; the ground's
    /// relight ramp keeps its sixteen (<see cref="TerrainSplatSurface.RelightRampWidth"/>).</para>
    /// </summary>
    public static class RoadFloorMaps
    {
        /// <summary>A chunk's side in texels (RF4_CHUNK): 8 m at 32 px per metre.</summary>
        public const int ChunkTexels = 256;

        /// <summary>A chunk's side in world metres.</summary>
        public const float ChunkMetres = 8f;

        /// <summary>The bake's pixels per metre, the game's pixels per unit.</summary>
        public const int PixelsPerMetre = 32;

        /// <summary>A palette's bands, as the ground's relight ramp holds them.</summary>
        public const int Bands = 5;

        /// <summary>A chunk's palettes at most (RF4_PALETTES).</summary>
        public const int MaxPalettes = 32;

        /// <summary>The road ramp's width: the palettes at x = palette * 5 + band, then the chunk's parameters
        /// (heightMin, heightRange, pondMax, heightMaxNear) at x = 160 (RF4_RAMP_PARAMS).</summary>
        public const int RampWidth = MaxPalettes * Bands + 1;

        /// <summary>The roles, in the light map's G: a texel no route covers (kept for its neighbours'
        /// relight, never drawn), a face under a deck, a face, a route's floor.</summary>
        public const int RoleNone = 0, RoleAirFace = 85, RoleFace = 170, RoleFloor = 255;

        /// <summary>The maps the streamer keeps per chunk, in the order of their arrays.</summary>
        public static readonly string[] MapSuffixes = { "_normal", "_light", "_detail" };

        /// <summary>One map's bytes: RGBA, 8 bits a channel.</summary>
        public const int MapBytes = ChunkTexels * ChunkTexels * 4;

        /// <summary>The file of a chunk's map, under the bake's folder.</summary>
        public static string MapFile(string chunk, int map) => chunk + MapSuffixes[map] + ".png";

        /// <summary>
        /// One slot's row of the road ramp, at <paramref name="rowStart"/>: palette p's band b at x = p * 5 + b
        /// as sRGB bytes 0..255 (the ground ramp's encoding, TerrainTexArrayBuilder.PackRampRow), the unused
        /// palettes cleared, and the chunk's parameters at x = 160. Null when packed, else what is wrong.
        /// </summary>
        public static string PackRampRow(RoadFloorManifest.Chunk chunk, Color[] ramp, int rowStart)
        {
            if (chunk == null) return "no chunk.";
            string[] palettes = chunk.palettes;
            if (palettes == null || palettes.Length == 0 || palettes.Length % Bands != 0)
                return $"'{chunk.name}': {palettes?.Length ?? 0} palette colours, not whole palettes of {Bands}.";
            if (palettes.Length > MaxPalettes * Bands)
                return $"'{chunk.name}': {palettes.Length / Bands} palettes; a road chunk's row holds {MaxPalettes}.";
            if (ramp == null || rowStart < 0 || rowStart + RampWidth > ramp.Length)
                return $"'{chunk.name}': the ramp has no row of {RampWidth} at {rowStart}.";
            for (int x = 0; x < RampWidth; x++) ramp[rowStart + x] = default;
            for (int i = 0; i < palettes.Length; i++)
            {
                if (!TryHex(palettes[i], out int rgb)) return $"'{chunk.name}': palette colour '{palettes[i]}' is not #rrggbb.";
                ramp[rowStart + i] = new Color((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255, 255);
            }
            ramp[rowStart + MaxPalettes * Bands] = new Color(chunk.heightMin, chunk.heightRange, chunk.pondMax, chunk.heightMaxNear);
            return null;
        }

        /// <summary>A palette colour, <c>#rrggbb</c>.</summary>
        public static bool TryHex(string h, out int rgb)
        {
            rgb = 0;
            return h != null && h.Length == 7 && h[0] == '#'
                && int.TryParse(h.Substring(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out rgb);
        }
    }
}
