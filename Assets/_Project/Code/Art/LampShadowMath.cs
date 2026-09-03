using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The PURE, deterministic maths of a LAMP-CAST SHADOW (ADR 0016, lights PR B) — the owner's
    /// <i>"the spotlights and headlights need to put shadows"</i>, made arithmetic.
    ///
    /// <para><b>A lamp is not the sun, and that is the whole feature.</b> The sun-shadow
    /// (<see cref="SpriteShadow"/>, ADR 0013 §7) is ONE direction at infinity: every caster in the
    /// world throws the same way, by the same length. A lamp is a POINT at a HEIGHT, so each caster
    /// throws its own way — radially AWAY from the lamp through its own feet — and its own length:
    /// the further from the lamp and the lower the lamp, the longer the rake. The sun's own
    /// elevation-to-length curve (<see cref="DayNightMath.ShadowLength"/>) is kept as the precedent,
    /// with the lamp's elevation AS SEEN FROM THE CASTER'S FEET in place of the sun's.</para>
    ///
    /// <para><b>The shear, and its inverse.</b> The shadow is the caster's own silhouette SHEARED
    /// along the ground: a point <c>h</c> metres above the feet lands <c>dir × L × h</c> away
    /// (<see cref="Shear"/>). The shader draws the shadow by running that map BACKWARDS per pixel
    /// (<see cref="Unshear"/>) and asking the silhouette whether the caster is opaque there — so the
    /// two must be exact inverses, and the HLSL copy of <see cref="Unshear"/> is pinned to this one
    /// by a source guard in the tests.</para>
    ///
    /// <para><b>Fold.</b> A shear pointing DOWN the screen (toward the viewer, in ¾ top-down)
    /// compresses the silhouette — that is the foreshortening a shadow thrown at the camera should
    /// have — but past <c>1 + dir.y × L = 0</c> it turns inside out. <see cref="ClampShearFold"/>
    /// bounds the length so the map stays invertible. The sun never meets this case (its shadow
    /// direction always leans north); a lamp can be anywhere.</para>
    ///
    /// <para>Everything here is a pure function of its arguments — no scene, no GPU, no
    /// time-of-call state (CLAUDE.md rule 5) — so it is unit-tested headless and the runtime
    /// <see cref="LampShadowSystem"/> is a thin shell over it.</para>
    /// </summary>
    public static class LampShadowMath
    {
        /// <summary>
        /// The ground-plane direction a caster's shadow runs from THIS lamp: radially away from the
        /// lamp, through the caster's feet (unit length). A caster standing exactly under the lamp
        /// has no radial direction, so it falls back to <paramref name="fallbackDir"/> — the beam's
        /// own axis for a cone, or straight down the screen for a round lamp.
        /// </summary>
        public static Vector2 ShadowDirection(Vector2 lampWorld, Vector2 footWorld, Vector2 fallbackDir)
        {
            Vector2 d = footWorld - lampWorld;
            if (d.sqrMagnitude > 1e-8f) return d.normalized;
            return fallbackDir.sqrMagnitude > 1e-8f ? fallbackDir.normalized : Vector2.down;
        }

        /// <summary>
        /// The lamp's ELEVATION as seen from the caster's feet, in the same <c>[0, 1]</c> the sun's
        /// <c>_SunElevation</c> uses (1 = straight overhead, 0 = on the horizon): the sine of the
        /// altitude angle, <c>h / sqrt(h² + d²)</c>. A higher lamp or a nearer caster reads higher; a
        /// far caster under a low lamp reads near the horizon and rakes long. The height is floored
        /// at <paramref name="minLampHeightMeters"/> so a lamp declared at ground level (or one that
        /// never declared a height) throws a bounded rake rather than an infinite one.
        /// </summary>
        public static float LampElevation(float lampHeightMeters, float groundDistanceMeters,
                                          float minLampHeightMeters)
        {
            float h = Mathf.Max(lampHeightMeters, Mathf.Max(minLampHeightMeters, 1e-3f));
            float d = Mathf.Max(groundDistanceMeters, 0f);
            return h / Mathf.Sqrt(h * h + d * d);
        }

        /// <summary>
        /// How long the shadow is, as a multiple of the caster's height — the sun's own curve
        /// (<see cref="DayNightMath.ShadowLength"/>: a stub at the zenith, a long clamped rake at the
        /// horizon) driven by the LAMP'S elevation instead of the sun's. Always positive: a lamp
        /// that is on at all throws something, so the elevation is floored just above the horizon
        /// rather than letting the sun's "sun is down ⇒ no shadow" branch fire.
        /// </summary>
        public static float ShadowLengthMultiple(float lampElevation, float lengthAtNoon,
                                                 float lengthAtHorizon, float maxLength)
            => DayNightMath.ShadowLength(Mathf.Clamp(lampElevation, 1e-4f, 1f),
                                         lengthAtNoon, lengthAtHorizon, maxLength);

        /// <summary>
        /// Bound the shear length so the shear stays INVERTIBLE. The map's vertical scale is
        /// <c>1 + dir.y × L</c>; a shadow thrown down the screen (<c>dir.y &lt; 0</c>) compresses the
        /// silhouette, and past zero it folds inside out. The length is capped so that scale never
        /// falls below <paramref name="minDenominator"/>. A shadow thrown up or across the screen is
        /// returned unchanged.
        /// </summary>
        public static float ClampShearFold(Vector2 dir, float lengthMultiple, float minDenominator)
        {
            float len = Mathf.Max(lengthMultiple, 0f);
            float md = Mathf.Clamp(minDenominator, 0.05f, 1f);
            if (dir.y >= 0f) return len;
            float limit = (1f - md) / -dir.y;
            return Mathf.Min(len, limit);
        }

        /// <summary>
        /// Where a point on the CASTER lands in the SHADOW: pushed along <paramref name="dir"/> by
        /// <paramref name="lengthMultiple"/> times its height above the feet. The feet themselves
        /// (height 0) stay put; rows below the feet (a hull's planking under its waterline pivot) rake
        /// the other way, exactly as the sun shader's negative <c>upFrac</c> does.
        /// </summary>
        public static Vector2 Shear(Vector2 casterPoint, Vector2 foot, Vector2 dir, float lengthMultiple)
        {
            float h = casterPoint.y - foot.y;
            return casterPoint + dir * (lengthMultiple * h);
        }

        /// <summary>
        /// The inverse of <see cref="Shear"/>: which point on the CASTER a shadow pixel is the
        /// shadow OF. This is the expression the fragment stage runs (<c>HHUnshear</c> in
        /// <c>HiddenHarboursLampShadow.shader</c>), and the tests pin the two together.
        /// </summary>
        public static Vector2 Unshear(Vector2 shadowPoint, Vector2 foot, Vector2 dir, float lengthMultiple)
        {
            float denom = 1f + dir.y * lengthMultiple;
            float h = (shadowPoint.y - foot.y) / denom;
            return new Vector2(shadowPoint.x - dir.x * lengthMultiple * h, foot.y + h);
        }

        /// <summary>
        /// The axis-aligned box that contains the whole sheared silhouette of a caster whose image
        /// occupies <c>[rectMin, rectMax]</c> — the quad the shadow is rasterised into. The image is
        /// a rectangle and the shear is affine, so the box of its four sheared corners is exact.
        /// </summary>
        public static void ShearedBounds(Vector2 rectMin, Vector2 rectMax, Vector2 foot, Vector2 dir,
                                         float lengthMultiple, out Vector2 min, out Vector2 max)
        {
            Vector2 a = Shear(new Vector2(rectMin.x, rectMin.y), foot, dir, lengthMultiple);
            Vector2 b = Shear(new Vector2(rectMax.x, rectMin.y), foot, dir, lengthMultiple);
            Vector2 c = Shear(new Vector2(rectMin.x, rectMax.y), foot, dir, lengthMultiple);
            Vector2 d = Shear(new Vector2(rectMax.x, rectMax.y), foot, dir, lengthMultiple);
            min = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
            max = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
        }

        /// <summary>
        /// The lamp's own SHAPE at the caster's feet, <c>0..1</c> — the same radial × cone falloff
        /// the additive glow quad draws with (<see cref="LightMath.ShapeIntensity"/>), evaluated at
        /// one point. It is what makes a shadow exactly as strong as the light it blocks: a caster in
        /// the feathered edge of the cone throws a feathered shadow, and one outside the cone or
        /// beyond the range throws none. A round lamp (half-angle 180°) has no angular cut.
        /// </summary>
        public static float LampShapeAtFoot(Vector2 lampWorld, Vector2 beamDir, float range,
                                            float coneHalfAngleDeg, float angularSoftness,
                                            float edgeSoftness, Vector2 foot)
        {
            Vector2 to = foot - lampWorld;
            float dist = to.magnitude;
            float r = Mathf.Max(range, 1e-4f);
            if (dist >= r) return 0f;

            float angleDeg = 0f;
            if (dist > 1e-5f && coneHalfAngleDeg < 180f)
            {
                Vector2 axis = beamDir.sqrMagnitude > 1e-8f ? beamDir.normalized : Vector2.up;
                float cos = Mathf.Clamp(Vector2.Dot(to / dist, axis), -1f, 1f);
                angleDeg = Mathf.Acos(cos) * Mathf.Rad2Deg;
            }
            return LightMath.ShapeIntensity(dist / r, edgeSoftness, angleDeg, coneHalfAngleDeg, angularSoftness);
        }

        /// <summary>
        /// How much of the light at a shadow pixel THIS lamp accounts for, as a share of full
        /// brightness: the lamp's master intensity clamped to <c>[0, 1]</c>. A searchlight dimmed
        /// toward off at a standstill (<see cref="BoatSpotlight"/>'s way-gate) is a weak lamp beside
        /// whatever moon and twilight there is, so its shadow fades with it. Above 1 a lamp is simply
        /// "full": the multiply model removes a FRACTION of the light present, and a brighter lamp
        /// does not block a larger fraction of itself.
        /// </summary>
        public static float IntensityShare(float intensity) => Mathf.Clamp01(intensity);

        /// <summary>
        /// The shadow's ALPHA — the fraction of the light present at a pixel that the shadow
        /// removes: the owner's strength dial × the lamp's shape at the feet × the night-gate × the
        /// lamp's intensity share, clamped. <b>Strength 0 returns exactly 0f</b> — the passthrough
        /// the render fixture proves byte-identical — before any multiply that could leave a
        /// denormal behind.
        /// </summary>
        public static float ShadowAlpha(float strength, float shape, float nightGate, float intensityShare)
        {
            if (strength <= 0f) return 0f;
            return Mathf.Clamp01(Mathf.Clamp01(strength) * Mathf.Clamp01(shape)
                                 * Mathf.Clamp01(nightGate) * Mathf.Clamp01(intensityShare));
        }

        /// <summary>
        /// The world-space rectangle a sprite's whole CELL occupies (not its tight mesh bounds — the
        /// silhouette lookup maps world → texture through this rect, so it must be the rect the
        /// texture cell fills), from the renderer's position, scale, cell rect, pivot and PPU. Flips
        /// mirror the cell about the pivot, exactly as <c>SpriteRenderer.flipX/flipY</c> draw it.
        /// </summary>
        public static void SpriteWorldRect(Vector2 position, Vector2 lossyScale, Rect cellPx, Vector2 pivotPx,
                                           float pixelsPerUnit, bool flipX, bool flipY,
                                           out Vector2 min, out Vector2 max)
        {
            float sx = Mathf.Abs(lossyScale.x), sy = Mathf.Abs(lossyScale.y);
            float ppu = Mathf.Max(pixelsPerUnit, 1e-3f);
            float left = flipX ? pivotPx.x - cellPx.width : -pivotPx.x;
            float bottom = flipY ? pivotPx.y - cellPx.height : -pivotPx.y;
            min = new Vector2(position.x + left / ppu * sx, position.y + bottom / ppu * sy);
            max = new Vector2(min.x + cellPx.width / ppu * sx, min.y + cellPx.height / ppu * sy);
        }

        /// <summary>
        /// The texture-uv rectangle of a sprite's cell as <c>(u0, v0, du, dv)</c>, so that a point at
        /// fraction <c>t</c> across the world rect samples <c>uv = (u0, v0) + t × (du, dv)</c>. A flip
        /// folds in as a NEGATIVE extent from the far edge — one expression in the shader either way.
        /// </summary>
        public static Vector4 SpriteUvRect(Rect cellPx, int textureWidth, int textureHeight, bool flipX, bool flipY)
        {
            float w = Mathf.Max(textureWidth, 1), h = Mathf.Max(textureHeight, 1);
            float u0 = cellPx.x / w, v0 = cellPx.y / h;
            float du = cellPx.width / w, dv = cellPx.height / h;
            if (flipX) { u0 += du; du = -du; }
            if (flipY) { v0 += dv; dv = -dv; }
            return new Vector4(u0, v0, du, dv);
        }

        /// <summary>Snap a world point to the pixel grid (crisp pixel art, no shimmer as a lamp moves).</summary>
        public static Vector2 SnapToPixels(Vector2 world, float pixelsPerUnit)
        {
            float ppu = Mathf.Max(pixelsPerUnit, 1e-3f);
            return new Vector2(Mathf.Round(world.x * ppu) / ppu, Mathf.Round(world.y * ppu) / ppu);
        }
    }
}
