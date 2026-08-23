using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>WHERE A STOREY ABOVE ACTUALLY DRAWS</b> — the arithmetic between "the rig says this house's
    /// floors are 3.1 m apart" and "the upper level's renderers go HERE".
    ///
    /// <para>This exists because ADR 0036 shipped without it. A storey was a LAYER on one footprint, and
    /// the layer went up at the same transform as the one below it: swap the level and the bedroom drew
    /// exactly over the kitchen, pixel for pixel. Nothing threw, nothing sorted wrong, and the only
    /// symptom was that going upstairs did not look like going anywhere. The layer model was right; what
    /// was missing was that a storey has a HEIGHT.</para>
    ///
    /// <para><b>⚠️ A HEIGHT IS NOT A DEPTH, AND NEITHER IS A WORLD-Y.</b> The shared ¾ camera sits 40°
    /// above the horizon, so one metre of northward GROUND travel draws <c>sin 40° ≈ 0.643</c> world
    /// units up the screen while one metre of HEIGHT draws <c>cos 40° ≈ 0.766</c>
    /// (<see cref="IsoGround.HeightScale"/>). Run a storey height through the ground scale and the upper
    /// floor sits 19% low; use the raw metres and it sits 31% high. Both look like "the art is a bit
    /// off", which is exactly the class of bug this file is here to make impossible to write by
    /// accident. <see cref="UpperLevelY"/> is the ONE place the projection happens.</para>
    ///
    /// <para><b>The storey height itself is never invented here.</b> It is DECLARED by the rig
    /// (<c>interiorIsoRig.anchors().storeyZ</c> — this storey's ceiling plus the joists it carries),
    /// baked into the interiors contract as <c>storeyHeightMetres</c>, and handed in. The precedent is
    /// the facing offset: the room that stands under exterior facing <c>f</c> is interior facing
    /// <c>f + 4</c>, and that 4 is MEASURED at bake time and written into the contract rather than typed
    /// into a builder. A storey height typed into C# is the same mistake one storey up — it goes quietly
    /// wrong the first time a rig moves a ceiling.</para>
    ///
    /// <para>Pure, static, allocation-free: no scene, no assets, no <c>MonoBehaviour</c>, so every number
    /// in it is unit-tested headless. It has to be — every failure mode here draws perfectly.</para>
    /// </summary>
    public static class InteriorLevelLayout
    {
        /// <summary>The storey you come in on. Always drawn at the building's own transform.</summary>
        public const int GroundLevel = 0;

        /// <summary>The first storey above — the only other one reachable today
        /// (<c>BuildingInterior.TryGoToLevel</c> refuses anything else), though nothing here assumes it
        /// is the last: <see cref="LevelOffset"/> takes any rung.</summary>
        public const int UpperLevel = 1;

        /// <summary>
        /// The smallest rise that counts as a storey, in world units. One pixel at the project's PPU 32
        /// (ADR 0006): below this the two floors land on the same texel row and the upper level draws
        /// over the one below — which is not "a small storey", it is the defect ADR 0036 was amended for.
        /// </summary>
        public const float MinimumRiseWorldUnits = 1f / 32f;

        /// <summary>
        /// <b>How far up the screen one storey of <paramref name="storeyHeightMetres"/> draws</b>, in
        /// world units — the rig's declared height projected at the shared bake camera.
        ///
        /// <para>Negative and NaN heights answer 0 rather than throwing: a contract baked before the
        /// field existed reports 0, and the correct response to "this bake does not say how tall a storey
        /// is" is the old picture plus a loud complaint from whoever placed it, not a builder that dies
        /// halfway through a region.</para>
        /// </summary>
        public static float UpperLevelY(float storeyHeightMetres)
        {
            if (float.IsNaN(storeyHeightMetres) || storeyHeightMetres <= 0f) return 0f;
            return storeyHeightMetres * IsoGround.HeightScale;
        }

        /// <summary>
        /// The world-space offset from the ground floor to <paramref name="level"/>, for a building whose
        /// storeys are <paramref name="storeyHeightMetres"/> apart. Purely vertical: a storey above sits
        /// over the same footprint, so only the height changes — which is what keeps ADR 0036's one
        /// footprint, one threshold, one inside test true however many rungs there are.
        /// </summary>
        public static Vector2 LevelOffset(int level, float storeyHeightMetres) =>
            level <= GroundLevel
                ? Vector2.zero
                : new Vector2(0f, level * UpperLevelY(storeyHeightMetres));

        /// <summary>
        /// Would a level placed at this rise draw over the storey below? True for anything under
        /// <see cref="MinimumRiseWorldUnits"/> — including the 0 a contract with no declared storey
        /// height produces.
        ///
        /// <para>The guard exists because the failure is SILENT: an upper storey at the ground floor's
        /// own Y renders perfectly, sorts legally, collides correctly and simply does not look like
        /// upstairs. Whoever stands a storey calls this and says so out loud.</para>
        /// </summary>
        public static bool DrawsOverGroundFloor(float upperLevelY) =>
            !(upperLevelY >= MinimumRiseWorldUnits);   // NaN-safe: NaN fails the comparison and is caught

        /// <summary>
        /// <b>The ADR 0032 sorting order for a world Y</b>, using the decor band's own numbers.
        ///
        /// <para>Deliberately identical arithmetic to <c>Art.YSortSprite.OrderFor</c> at its defaults —
        /// World cannot reference Art (rule 4), so the mapping is restated here against the SAME
        /// <see cref="SortingBands"/> constants both read, and an EditMode test pins the two together.
        /// What must not happen is a second set of literals: that is precisely how the band rotted the
        /// first time (ADR 0032's context).</para>
        /// </summary>
        public static int SortingOrderFor(float worldY) =>
            Mathf.Clamp(
                Mathf.RoundToInt(SortingBands.DecorBase - worldY * SortingBands.OrdersPerMetre),
                SortingBands.DecorFloor, SortingBands.DecorCeiling);

        /// <summary>
        /// <b>The sorting order an upper storey's ROOM SHEET takes</b> — the band order of its FAR
        /// (northmost) edge.
        ///
        /// <para><b>Why the upper sheet joins the band at all, when the ground floor's does not.</b> The
        /// ground room sits at the building's own transform, buried inside a footprint nothing else is
        /// drawn on, so a fixed <see cref="SortingBands.InteriorFloor"/> (1, under the whole band) is
        /// safe and always has been. The upper room does not: it is lifted up-screen by
        /// <see cref="UpperLevelY"/>, over ground that has grass, trees and a dooryard on it — all of
        /// them Y-sorted four-digit orders. At order 1 the meadow behind the house would draw straight
        /// through a first-floor bedroom.</para>
        ///
        /// <para><b>Why the FAR edge and not the centre.</b> A room sheet is floor and far walls, so
        /// everything standing on it is spatially in front of all of it. The far edge is the sheet's
        /// LOWEST order (order falls as Y rises), so every occupant, prop and fixture anywhere on that
        /// floor outranks it — the same guarantee the fixed order gives downstairs, kept while joining
        /// the band. Taking the centre instead would hide the player behind their own bedroom floor the
        /// moment they walked past the middle of it.</para>
        /// </summary>
        public static int UpperRoomSortingOrder(in InteriorFootprint upperFootprint)
        {
            Vector2[] corners = upperFootprint.Corners();
            float far = corners[0].y;
            for (int i = 1; i < corners.Length; i++)
                if (corners[i].y > far) far = corners[i].y;
            return SortingOrderFor(far);
        }
    }
}
