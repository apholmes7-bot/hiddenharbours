using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Pure, headless-testable twin of the ADR 0027 #8 object-reflection geometry — the <b>mirror
    /// axis</b> the <c>HHReflect</c> pass reflects about, and the <b>world-snapped warp</b> the water
    /// shader looks the result up with (mirrored in <c>Include/ReflectMirror.hlsl</c> and
    /// <c>HiddenHarboursWater.shader</c>'s <c>ObjectReflection()</c> — change one, change BOTH in the
    /// same PR).
    ///
    /// <para><b>The mirror axis is the ground-contact pivot</b> (ADR 0026). A reflection is the
    /// source reflected in the plane it stands on, and the pivot convention we already settled IS the
    /// waterline contact — so the axis needs no new convention, only the pivot's world Y:
    /// <c>y' = 2·pivotY − y</c>. For a mesh hull the same formula runs against the ADR 0023
    /// calibrated iso-depth waterplane, which is where that hull actually meets the sea.</para>
    ///
    /// <para>🔴 <b>The pivot cannot come from <c>unity_ObjectToWorld</c>.</b> Unity submits
    /// SpriteRenderer meshes with their vertices ALREADY IN WORLD SPACE and an IDENTITY
    /// object-to-world matrix, so the object origin reads <c>(0,0)</c> for every sprite in the scene —
    /// measured on this repo 2026-07-29 by the tree lane (a 3 m lamp lit three trees equally). Mirror
    /// about that and every sprite reflects about the world origin instead of its own feet. The pivot
    /// is therefore PUBLISHED per renderer through a MaterialPropertyBlock
    /// (<see cref="ReflectiveObject"/> → <c>_HHReflectOrigin</c>, the <c>_HullOrigin</c> /
    /// <c>_SpriteRootWS</c> pattern). This is not an edge case — it is every sprite.</para>
    ///
    /// <para><b>The warp snaps in WORLD space, never screen/RT space.</b> The reflection lives in a
    /// render target, and a render target is screen-space by default — which is exactly the crawl
    /// trap ADR 0027's Pixelation section names for this item. With <c>CameraFollow</c> panning
    /// continuously behind the boat, a lookup quantized in screen space slides under every feature on
    /// every pan. Snapping the SAMPLE POSITION on the world PPU grid instead
    /// (<see cref="WarpedSampleWorld"/>) means a reflection cell belongs to a place on the water and
    /// stays there while the camera moves — zero crawl by construction, the same law the shader's
    /// world-locked Bayer dither and the ADR 0022 facet pass already hold.
    /// <see cref="ScreenSnappedSampleWorld"/> exists ONLY as the wrong answer the tests measure the
    /// crawl against; nothing ships calling it.</para>
    ///
    /// <para>Visual only: this drives <c>col.rgb</c> and a render target, never <c>depth</c>/
    /// <c>clip()</c>/<c>_WaterLevel</c>/the height read/the sim (P1 integrity, CLAUDE.md rule 5).
    /// Nothing is saved (rule 5 / ADR 0008).</para>
    /// </summary>
    public static class WaterReflectionWarp
    {
        /// <summary>Floor for the pixels-per-unit grid so a degenerate PPU can never divide by zero
        /// (mirrors the shader's <c>max(_PixelsPerUnit, 1.0)</c>).</summary>
        public const float MinPixelsPerUnit = 1f;

        /// <summary>
        /// Reflect a world Y about the ground-contact pivot: <c>y' = 2·pivotY − y</c>. An involution
        /// (mirroring twice returns the original) with the pivot itself as its only fixed point —
        /// which is the property that makes the reflection meet the source exactly at the waterline
        /// contact instead of floating off it.
        /// </summary>
        public static float MirrorY(float worldY, float pivotY)
        {
            return 2f * pivotY - worldY;
        }

        /// <summary>
        /// The full mirrored world position: X is untouched (the mirror plane is horizontal), Y is
        /// reflected about the pivot. Kept as a Vector2 method so the twin and the HLSL read the
        /// same shape rather than agreeing by coincidence.
        /// </summary>
        public static Vector2 Mirror(Vector2 world, Vector2 pivot)
        {
            return new Vector2(world.x, MirrorY(world.y, pivot.y));
        }

        /// <summary>Snap a world coordinate to the PPU grid — the shader's <c>Pixelize</c> helper,
        /// mirrored so the twin quantizes on exactly the same lattice.</summary>
        public static Vector2 Pixelize(Vector2 p, float pixelsPerUnit)
        {
            float ppu = Mathf.Max(pixelsPerUnit, MinPixelsPerUnit);
            return new Vector2(Mathf.Floor(p.x * ppu) / ppu, Mathf.Floor(p.y * ppu) / ppu);
        }

        /// <summary>
        /// THE #8 lookup law — where on the water this fragment reads its reflection from, in WORLD
        /// space: the fragment's own position displaced by the wave slope, then snapped on the world
        /// PPU grid. The displacement is what makes a reflection wobble on the <b>same crests the
        /// hull is riding</b> (the slope comes from <c>WaveFieldSample()</c>, consumed as a black
        /// box — never forked); the snap is what stops it crawling.
        ///
        /// <para><b>It does not depend on the camera at all</b>, which is the whole point and is what
        /// the crawl test measures: pan the camera by any amount, sub-pixel or not, and this returns
        /// the same world cell for the same fragment. Compare
        /// <see cref="ScreenSnappedSampleWorld"/>.</para>
        /// </summary>
        /// <param name="worldXY">The water fragment's world position (m).</param>
        /// <param name="waveSlope">The shared wave field's local slope at that position.</param>
        /// <param name="warpMetres">How far a unit of slope displaces the lookup
        /// (<c>_ReflectWarp</c>, m; 0 = a flat mirror).</param>
        /// <param name="pixelsPerUnit">The world pixel grid (<c>_PixelsPerUnit</c>).</param>
        public static Vector2 WarpedSampleWorld(Vector2 worldXY, Vector2 waveSlope, float warpMetres,
                                                float pixelsPerUnit)
        {
            return Pixelize(worldXY + waveSlope * warpMetres, pixelsPerUnit);
        }

        /// <summary>
        /// ⚠️ <b>THE WRONG ANSWER, kept so the tests can measure how wrong.</b> The same lookup
        /// quantized in CAMERA-RELATIVE (screen/RT) space — snap the offset from the camera, then add
        /// the camera back. It is what you get by reaching for the render target's own pixel grid,
        /// which is the natural thing to do once a reflection lives in an RT.
        ///
        /// <para>The result <b>moves with sub-pixel camera translation</b> while the fragment stands
        /// still: that is the crawl, and <c>WaterReflectionWarpTests</c> reports it as a
        /// camera-offset-dependent sample delta rather than as an adjective. Nothing ships calling
        /// this.</para>
        /// </summary>
        public static Vector2 ScreenSnappedSampleWorld(Vector2 worldXY, Vector2 cameraXY,
                                                       Vector2 waveSlope, float warpMetres,
                                                       float pixelsPerUnit)
        {
            Vector2 warped = worldXY + waveSlope * warpMetres;
            return Pixelize(warped - cameraXY, pixelsPerUnit) + cameraXY;
        }

        /// <summary>
        /// How the water splits one reflection sample into its PRE-grade and POST-grade shares
        /// (§11.6). The <c>HHReflect</c> pass writes PREMULTIPLIED colour, so an ordinary reflection's
        /// rgb can never exceed its coverage; a NIGHT-LIT source writes its colour already divided by
        /// the day/night tint (<c>LightMath.CompensateForDayNightTint</c>, exactly as the moon
        /// glitter does), which at night puts it far ABOVE coverage. The excess over coverage is
        /// therefore precisely the compensated light content, and it needs no flag channel, no second
        /// target and no extra uniform.
        ///
        /// <para>By day the compensation factor is ≈ 1, so a lit source stays under coverage and the
        /// whole sample lands in the pre-grade share — daylight is unchanged, which is the correct
        /// behaviour and not a special case. Without this split a lit wheelhouse at night is crushed
        /// to ~3% by the multiply overlay.</para>
        /// </summary>
        /// <param name="premultipliedRgb">The sampled <c>_HHReflectTex</c> rgb (already × coverage).</param>
        /// <param name="coverage">The sampled alpha.</param>
        /// <param name="preGrade">The share composited before the palette grade (dims with night).</param>
        /// <param name="postGrade">The compensated share added after it (survives the overlay).</param>
        public static void SplitLitShare(Vector3 premultipliedRgb, float coverage,
                                         out Vector3 preGrade, out Vector3 postGrade)
        {
            float cov = Mathf.Clamp01(coverage);
            preGrade = new Vector3(Mathf.Min(premultipliedRgb.x, cov),
                                   Mathf.Min(premultipliedRgb.y, cov),
                                   Mathf.Min(premultipliedRgb.z, cov));
            postGrade = new Vector3(Mathf.Max(premultipliedRgb.x - cov, 0f),
                                    Mathf.Max(premultipliedRgb.y - cov, 0f),
                                    Mathf.Max(premultipliedRgb.z - cov, 0f));
        }
    }
}
