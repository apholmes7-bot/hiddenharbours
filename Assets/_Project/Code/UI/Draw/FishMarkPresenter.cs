using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// ONE FISH ICON on the sonar glass — the finder's own presentation of part of a
    /// <see cref="FishMark"/>. Horizontal placement and size are the UI's business; the DEPTH stays in
    /// METRES all the way to the paint call, because the normalised position it eventually becomes means
    /// something different the moment the RANGE pushers move.
    /// </summary>
    public readonly struct SonarMark
    {
        /// <summary>Horizontal position in the sonar box, 0…1. Pure presentation — the seam states a
        /// school's depth, not which pixel column a fish is in.</summary>
        public readonly float X01;

        /// <summary>Depth below the surface in METRES — the seam's own number, carried unchanged.</summary>
        public readonly float DepthMetres;

        /// <summary>The rig's <c>size</c> multiplier for this icon (<c>fishRig.js:159</c>, README ≈0.5–2.5).</summary>
        public readonly float Size;

        public SonarMark(float x01, float depthMetres, float size)
        {
            X01 = x01; DepthMetres = depthMetres; Size = size;
        }
    }

    /// <summary>
    /// Turns the Core seam's <see cref="FishMark"/> returns into the icons the glass draws (ADR 0025 S3b).
    /// Pure, engine-free and deterministic, so it is EditMode-testable without a canvas or a fish model.
    ///
    /// <para><b>This is presentation ONLY.</b> It invents no fish: one <see cref="FishMark"/> per school
    /// arrives with a depth, a count and a signal strength, and this spreads that count across the scan as
    /// icons. <see cref="FishMark.Count"/> is the owner's honesty invariant in one number — the same
    /// figure that raises the bite rate — so the number of icons IS the density, never a decoration
    /// chosen here. What this class may decide is only where on the scan they sit and how big they look.</para>
    ///
    /// <para><b>Deterministic, never random</b> (rule 5's spirit, and rule 7's requirement). Placement
    /// comes from the rig's own hash noise seeded on the school's <em>own</em> numbers — its depth and
    /// count — plus the icon index. So the same school looks the same on every repaint (a scan that
    /// reshuffled 8× a second would be unreadable), a school does not jump when another one appears or
    /// disappears beside it in the list, and nothing here touches a global RNG.</para>
    ///
    /// <para><b>Allocation-free on the steady path</b> (rule 7): the caller owns the output list, which is
    /// cleared and refilled.</para>
    /// </summary>
    public static class FishMarkPresenter
    {
        /// <summary>
        /// Expand every school's <see cref="FishMark"/> into up to
        /// <see cref="FishFinderSettings.MaxMarksPerSchool"/> icons in <paramref name="into"/> (cleared
        /// first). Returns how many were written. A null/empty input — the empty sea, which is the shipped
        /// default until a fish model registers — writes nothing and is not an error.
        /// </summary>
        public static int Build(IReadOnlyList<FishMark> marks, in FishFinderSettings s, List<SonarMark> into)
        {
            into?.Clear();
            if (marks == null || into == null) return 0;

            int cap = s.MaxMarksPerSchool > 0 ? s.MaxMarksPerSchool : 1;
            float margin = Clamp(s.MarkXMargin01, 0f, 0.45f);
            float span = 1f - margin * 2f;

            for (int i = 0; i < marks.Count; i++)
            {
                FishMark m = marks[i];
                int count = m.Count;
                if (count <= 0) continue;
                if (count > cap) count = cap;

                float strength = Clamp(m.Strength, 0f, 1f);
                float baseSize = s.MarkSizeMin + (s.MarkSizeMax - s.MarkSizeMin) * strength;

                for (int k = 0; k < count; k++)
                {
                    double seed = Seed(in m, k);
                    float x01 = margin + span * (float)FishRigGeometry.HashRnd(seed);

                    // ±jitter around the school's stated depth — cosmetic only; the depth that FISHES is
                    // the school's own, which is what the seam hands the gameplay lane.
                    float wobble = (float)(FishRigGeometry.HashRnd(seed + 91.3) * 2.0 - 1.0);
                    float depth = m.DepthMetres + wobble * s.MarkDepthJitterMetres;
                    if (depth < 0f) depth = 0f;

                    float sizeWobble = (float)(FishRigGeometry.HashRnd(seed + 17.7) * 2.0 - 1.0);
                    float size = baseSize * (1f + sizeWobble * Clamp(s.MarkSizeJitter01, 0f, 1f));
                    if (size < 0.05f) size = 0.05f;

                    into.Add(new SonarMark(x01, depth, size));
                }
            }
            return into.Count;
        }

        /// <summary>The hash seed for one icon. Built from the SCHOOL's own numbers, deliberately not from
        /// its index in the list: a school must not jump across the glass because a different school
        /// appeared before it.</summary>
        private static double Seed(in FishMark m, int iconIndex)
            => m.DepthMetres * 13.1 + m.Count * 5.7 + iconIndex * 2.9;

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
