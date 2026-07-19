using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The shared heading→facing-cell math for ¾-ISO art baked as N evenly-spaced pre-drawn facings.
    /// Pure, static, deterministic — no engine state, no allocation, no instance to construct.
    ///
    /// <para>This lives in Core because more than one lane needs it and no feature module may reach
    /// into another's concrete classes (CLAUDE.md rule 4). The Boats lane drives directional hulls and
    /// their overlay layers (oars, outboard); the CHARACTER rigs are baked to the same 8-way convention
    /// — including the same counter-clockwise-but-labelled-clockwise quirk — and must resolve a cell
    /// from a heading by exactly the same rule. One copy of the rule, one place to fix it.</para>
    /// </summary>
    public static class IsoFacing
    {
        /// <summary>
        /// Pick the facing-array index for a compass heading, snapping to the NEAREST of <paramref name="count"/>
        /// evenly-spaced facings laid out CLOCKWISE from <paramref name="zeroHeadingDeg"/> (the heading that
        /// element 0 is drawn for). Generalised to any count so 8/16-way art drops in unchanged.
        ///
        /// Boundary rule is explicit and off-by-one-free: a heading exactly on a bucket edge rounds to the
        /// NEXT facing clockwise (half-up), so e.g. for count=4 a heading of 45° (dead between N and E) picks
        /// East, 135° picks South, etc. — never an ambiguous tie. The result is always in [0, count).
        /// Pure + static + deterministic (no engine state, no allocation).
        ///
        /// <para><paramref name="facingsAreCounterClockwise"/> mirrors the lookup for art whose cells run the
        /// OTHER way — cell i depicting −step·i rather than +step·i (see
        /// <c>BoatVisualDef.FacingsAreCounterClockwise</c>: the iso rigs bake CCW but label CW).
        /// Default false = the clockwise convention, unchanged. Note this only picks a different CELL; the
        /// heading itself is never altered, which is why <c>DirectionalBoatSprite.SnapHeadingDegrees</c>
        /// ignores the flag.</para>
        /// </summary>
        public static int HeadingToFacingIndex(float headingDeg, int count, float zeroHeadingDeg,
                                               bool facingsAreCounterClockwise = false)
        {
            if (count <= 0) return 0;
            float step = 360f / count;
            // Heading measured from the zero facing, wrapped to [0, 360).
            float rel = (headingDeg - zeroHeadingDeg) % 360f;
            if (rel < 0f) rel += 360f;
            // Half-up rounding (FloorToInt(x + 0.5)) so bucket edges resolve to the next facing CW,
            // deterministically — no banker's-rounding tie at 45/135/225/315 for count=4.
            int idx = Mathf.FloorToInt(rel / step + 0.5f);
            // CCW art: the cell that DEPICTS +rel is the one the rig baked at −rel, i.e. count − idx.
            // (Element 0 is its own mirror — North is North either way — hence the wrap below, not count−1−idx.)
            if (facingsAreCounterClockwise) idx = count - idx;
            idx %= count;             // 360°≡0° wraps the top bucket back to element 0
            if (idx < 0) idx += count;
            return idx;
        }
    }
}
