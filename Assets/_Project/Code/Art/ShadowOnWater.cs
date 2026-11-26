using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>One rule for every sun shadow that lies on open water</b> — a floating hull's, a mast's, a pile's.
    ///
    /// <para>The owner, at the Nine Mile Creek wharf at low water (2026-11-25): the masts and piles threw
    /// "long diagonal" hard bars across the basin. A sea surface does not hold a shade like a deck does:
    /// the ripples scatter the light the shadow takes away, so a shadow on water is short, faint and broken
    /// along the wave rows. The hulls already faded with a low sun
    /// (<see cref="HullSunShadowMath.WaterAlphaScale"/>); sprite casters had no water rule at all, and
    /// nothing shortened or broke either kind.</para>
    ///
    /// <para><b>Decided per caster, by where it STANDS</b> (its foot; for a sprite caster the majority of its
    /// foot and its drawn bounds' bottom-centre and centre, so a gangway hanging from the deck counts):
    /// over water at the live tide is the region's
    /// seabed lying below the deterministic water level there (<see cref="ITidalTerrain"/> through Core).
    /// A caster on land or a wharf keeps the shadow it always had, whatever its rake crosses. No terrain
    /// service (a bare test scene) reads as land, so nothing changes there either.</para>
    ///
    /// <para>Pure apart from <see cref="IsOverWater"/>, so the arithmetic is asserted headless. Presentation
    /// only: it saves nothing and steers no sim (rule 5); the level it reads is recomputed from
    /// <c>(worldSeed, gameTime)</c>.</para>
    /// </summary>
    public static class ShadowOnWater
    {
        /// <summary>Per renderer: how much of the shade the ripples break (0 = land).</summary>
        public static readonly int BreakId = Shader.PropertyToID("_HHWaterBreak");

        /// <summary>Per renderer: (art pixels per unit, wave-row pixels, dash pixels, unused), from the profile.</summary>
        public static readonly int GridId = Shader.PropertyToID("_HHWaterGrid");

        /// <summary>Per renderer, for a caster standing on LAND: (cut fraction, water alpha, breakup, on).
        /// The part of its shade that lands on open water (read per pixel off the published seabed and sea
        /// level, the same height map <see cref="IsOverWater(Vector2)"/> asks) is cut beyond the water cap
        /// and faded and broken like a water caster's; the part on land is untouched. A crane on the apron,
        /// a lamp on the quay edge: their rake across the basin is short and broken, on the deck it is not.</summary>
        public static readonly int EdgeId = Shader.PropertyToID("_HHWaterEdge");

        /// <summary>The edge vector for a land caster (see <see cref="EdgeId"/>): off when the profile has no
        /// rule. The cut fraction is in caster heights along the shear: the shade on water ends where it has
        /// run <paramref name="waterCap"/> heights from the foot, so a shadow already that short is not cut.</summary>
        public static Vector4 LandCasterEdge(bool anyRule, float lengthMultiplier, float waterCap, float waterAlphaScale,
                                             float breakup)
        {
            if (!anyRule) return Vector4.zero;
            float cut = lengthMultiplier > Mathf.Max(0f, waterCap) && lengthMultiplier > 1e-6f
                ? Mathf.Max(0f, waterCap) / lengthMultiplier
                : float.MaxValue;
            return new Vector4(cut, Mathf.Clamp01(waterAlphaScale), Mathf.Clamp01(breakup), 1f);
        }

        /// <summary>Over water at the live tide: the region's seabed lies below the water level at
        /// <paramref name="at"/>. No terrain, environment or clock reads as land.</summary>
        public static bool IsOverWater(Vector2 at)
        {
            ITidalTerrain terrain = GameServices.TidalTerrain;
            if (terrain == null || GameServices.Environment == null || GameServices.Clock == null) return false;
            return GameServices.Environment.WaterLevelAt(GameServices.Clock.TotalSeconds) - terrain.ElevationAt(at) > 0f;
        }

        /// <summary>Over water for a caster that may SPAN the edge (a gangway from the deck down to its float,
        /// a pile frame at a float's end): the majority of three plan points, its foot, the bottom-centre and
        /// the centre of its drawn bounds. A gangway's foot is on the deck but most of it hangs over the
        /// basin, so it takes the water rule; a building or a crane standing on the wharf keeps its foot and
        /// its middle on land, so it does not.</summary>
        public static bool IsOverWater(Vector2 foot, Bounds drawn)
            => Majority(IsOverWater(foot), IsOverWater(new Vector2(drawn.center.x, drawn.min.y)),
                        IsOverWater(new Vector2(drawn.center.x, drawn.center.y)));

        /// <summary>Two of three.</summary>
        public static bool Majority(bool a, bool b, bool c) => (a ? 1 : 0) + (b ? 1 : 0) + (c ? 1 : 0) >= 2;

        /// <summary>The length multiplier (x caster height) the shadow rakes by: on water capped at
        /// <paramref name="waterCap"/>, on land untouched.</summary>
        public static float LengthMultiplier(float lengthMultiplier, bool overWater, float waterCap)
            => overWater ? Mathf.Min(lengthMultiplier, Mathf.Max(0f, waterCap)) : lengthMultiplier;

        /// <summary>The share of its darkness a shadow keeps: 1 on land; on water the low-sun fade times
        /// the profile's flat water alpha.</summary>
        public static float AlphaScale(float sunElevation, bool overWater, float lowSunAlpha, float fullSunElevation,
                                       float waterAlpha)
            => overWater
                ? HullSunShadowMath.WaterAlphaScale(sunElevation, true, lowSunAlpha, fullSunElevation) * Mathf.Clamp01(waterAlpha)
                : 1f;

        /// <summary>The breakup published to the shader: the profile's on water, 0 (a solid shade) on land.</summary>
        public static float Breakup(bool overWater, float breakup) => overWater ? Mathf.Clamp01(breakup) : 0f;

        /// <summary>The breakup grid the shader reads, in art pixels.</summary>
        public static Vector4 Grid(SpriteShadowProfile look)
        {
            Vector3 g = look != null ? look.WaterBreakupGrid : new Vector3(32f, 3f, 7f);
            return new Vector4(Mathf.Max(1f, g.x), Mathf.Max(1f, g.y), Mathf.Max(1f, g.z), 0f);
        }

        /// <summary>True when the profile asks for any water rule, so a land-only project pays nothing.</summary>
        public static bool AnyRule(SpriteShadowProfile look)
            => look != null && (look.WaterBreakup > 0f || look.WaterAlpha < 1f || look.WaterMaxLength < 1e5f ||
                                look.HullWaterLowSunAlpha < 1f);
    }
}
