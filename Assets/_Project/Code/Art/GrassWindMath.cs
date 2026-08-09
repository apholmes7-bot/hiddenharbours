using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>The grass shader's wind term, on the CPU.</b> A line-for-line twin of the vertex-stage wind block
    /// in <c>HiddenHarboursGrass.shader</c>, so the one claim that matters about the batched meadow — <i>a
    /// merged tuft bends exactly as the sprite did</i> — can be stated as an assertion instead of a
    /// screenshot.
    ///
    /// <para><b>⚠ WHY A TWIN AT ALL, GIVEN NOTHING RENDERS THROUGH IT.</b> The batching change moved grass
    /// from a <see cref="SpriteRenderer"/> per tuft to merged chunk meshes. The shader was not touched — not
    /// a line — because a merged tuft feeds it the same two inputs a sprite did: the vertex's WORLD position
    /// and the sprite's own <c>uv</c>. That is a claim about INPUTS, and <c>GrassFieldWindParityTests</c>
    /// checks it directly against the mesh. This twin is for the other half: proving the shader's response
    /// to those inputs is actually load-bearing, by sabotaging it. Same idiom as
    /// <c>GrassWindBridge.WindToShaderVector</c> and <c>SpriteLightMath</c> — shader arithmetic hoisted
    /// somewhere a headless test can reach it.</para>
    ///
    /// <para><b>⚠ THE RENDERING-GUARD LAW: ZERO A STRENGTH, NOT A SPEED.</b> A guard that "proves" the wind
    /// works by zeroing <c>_SwaySpeed</c> proves nothing — kill the speed and the steady lean and the
    /// spatial gust pattern both survive, so the grass still bends and a dead shader would pass. The kill
    /// switch is the AMPLITUDE: <c>swayMag = _IdleSway + windStrength × _SwayAmount</c> multiplies the whole
    /// offset, so zeroing those two strengths must take the bend to exactly zero for every input, and
    /// restoring either must bring it back. That asymmetry is the test.</para>
    ///
    /// <para><b>Drift.</b> A twin that nobody renders through can rot. <c>GrassFieldWindParityTests</c>
    /// re-reads the shader SOURCE and fails if the expressions modelled here are no longer in it, so the
    /// twin cannot quietly describe a shader that has moved on. Pure and headless — no scene, no GPU.</para>
    /// </summary>
    public static class GrassWindMath
    {
        /// <summary>The material rows the wind term reads. Names match the shader's properties exactly, so
        /// a rename shows up as a compile error here and as a source-guard failure in the tests.</summary>
        public struct Params
        {
            public float IdleSway;      // _IdleSway   — wind-INDEPENDENT baseline (m)
            public float SwayAmount;    // _SwayAmount — extra tip sway at full wind (m)
            public float WindLean;      // _WindLean   — how much a steady wind holds the tips over (0..1)
            public float SwaySpeed;     // _SwaySpeed  — gust temporal speed
            public float GustScale;     // _GustScale  — gust travel scale (per m)
            public float GustStrength;  // _GustStrength
            public float PhaseGrid;     // _PhaseGrid  — tuft decorrelation cell (m)
        }

        /// <summary>
        /// The wind offset (metres, world XY) the shader adds to ONE VERTEX before the tip weighting.
        ///
        /// <para><paramref name="worldPos"/> is the vertex's own world position — deliberately, because
        /// that is what the shader uses. Both the decorrelation phase and the travelling gust are sampled
        /// per vertex, so a tuft shears slightly as it bends rather than swinging rigidly; a merged mesh
        /// reproduces that only because it reproduces the same per-vertex world positions.</para>
        /// </summary>
        public static Vector2 WindOffset(Vector2 worldPos, Vector2 windWorld, float time, in Params p)
        {
            float windStr = windWorld.magnitude;
            Vector2 wdir = windStr > 1e-4f ? windWorld / windStr : new Vector2(1f, 0f);

            float grid = Mathf.Max(p.PhaseGrid, 0.01f);
            float cellX = Mathf.Floor(worldPos.x / grid);
            float cellY = Mathf.Floor(worldPos.y / grid);
            float phase = cellX * 12.9898f + cellY * 78.233f;

            float travel = Vector2.Dot(worldPos, wdir) * p.GustScale;
            float gust = Mathf.Sin(time * p.SwaySpeed - travel + phase) * 0.6f
                       + Mathf.Sin(time * p.SwaySpeed * 1.7f - travel * 1.3f + phase) * 0.4f;

            // ⭐ THE KILL SWITCH. Every term above is multiplied by this amplitude, so zeroing both
            // strengths zeroes the bend for ALL inputs — which is exactly what the sabotage-proof asserts,
            // and exactly what zeroing a SPEED would fail to do.
            float swayMag = p.IdleSway + windStr * p.SwayAmount;
            float lean = windStr * p.WindLean;

            return wdir * ((lean + gust * p.GustStrength) * swayMag);
        }

        /// <summary>
        /// The tip weighting: 0 at the root (<c>uv.y</c> = 0), 1 at the tip, SQUARED so the base stays
        /// planted and the displacement concentrates toward the tip.
        ///
        /// <para><b>⚠ This is why a batched tuft must carry the sprite's own tessellated mesh.</b> On a
        /// four-vertex FullRect quad the squaring is evaluated at uv.y 0 and 1 only and interpolates
        /// linearly between them — and 0² = 0, 1² = 1, so it does nothing at all. That is the defect
        /// <c>WindBendTessellationTests</c> pins for the trees; the grass escapes it only because the tufts
        /// import Tight and <see cref="Sprite.vertices"/> is a tessellated silhouette.</para>
        /// </summary>
        public static float BendWeight(float spriteV)
        {
            float w = Mathf.Clamp01(spriteV);
            return w * w;
        }

        /// <summary>Snap a world offset to the PPU grid, so the bend moves in WHOLE pixels — the same
        /// discipline the water shader applies to its sample coordinates. Pixel-art faithful: a sub-pixel
        /// bend shimmers.</summary>
        public static Vector2 PixelSnap(Vector2 offset, float pixelsPerUnit)
        {
            float ppu = Mathf.Max(pixelsPerUnit, 1f);
            return new Vector2(Mathf.Floor(offset.x * ppu) / ppu, Mathf.Floor(offset.y * ppu) / ppu);
        }
    }
}
