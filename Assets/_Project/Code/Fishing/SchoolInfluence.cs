using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// WHAT THE SCHOOLS UNDER YOU ARE WORTH TO THIS CAST (ADR 0025 S3) — the schools at the rod's spot,
    /// reduced to the two things the fishing path acts on: how much quicker the fish bite, and which
    /// species is likely to be the one that does.
    ///
    /// <para><b>This is the honesty invariant's other end.</b> The fish finder draws
    /// <see cref="FishMark"/>s off <see cref="IFishSchools.MarksAt"/>; this is built off
    /// <see cref="IFishSchools.SchoolsAt"/> — the same schools, from the same model, at the same position
    /// and the same instant. <see cref="EffectiveMarks"/> is literally the sum of the mark counts the
    /// glass is drawing, each scaled by the same <see cref="FishSchool.SignalAt"/> the glass dims them
    /// with. There is no separate bite table for the picture to drift away from: if the marks are wrong
    /// the bite is wrong in exactly the same way, which is the only arrangement in which the instrument
    /// is not a liar.</para>
    ///
    /// <para><b>Never a wall.</b> Every term is a soft weight in the same family as bait, tackle and
    /// depth: a school can only speed the bite (never slow it), and a species the school does not hold is
    /// damped, never barred. Fishing off the fish is worse fishing, not no fishing.</para>
    ///
    /// <para><b>Neutral by construction.</b> <see cref="None"/> — and <c>default</c>, which is why the
    /// accessors below are written to survive it — leaves the roll and the wait bit-for-bit what they were
    /// before schools existed. That is what makes an unwired rig, an EditMode fixture and the owner's
    /// <see cref="FishSchoolSettings.BaseAppearanceChance01"/> = 0 off switch all behave identically to
    /// the shipped game.</para>
    /// </summary>
    public readonly struct SchoolInfluence
    {
        /// <summary>
        /// The marks under the rod, weighted by how solidly they are showing: <c>Σ MarkCount × SignalAt ×
        /// depth-match</c>. The same numbers the glass is drawing — see the type doc. 0 with no school.
        /// </summary>
        public readonly float EffectiveMarks;

        /// <summary>How much quicker the fish bite here, ≥ 1 (the wait is DIVIDED by this — see
        /// <see cref="BiteDelayScale"/>). 1 = no school, fish exactly as before.</summary>
        public readonly float BiteRateMultiplier;

        /// <summary>How well the rod is sitting on the best school under it, 0 (nothing / the far rim) ..
        /// 1 (dead centre, at the right depth). Eases the species weighting so clipping the edge of a
        /// school barely re-weights the roll.</summary>
        public readonly float Strength01;

        /// <summary>The species the schools under the rod are holding, by stable id. Null/empty means "no
        /// species stated" — the roll falls back to its normal pool for the place and time
        /// (<see cref="FishSchool.SpeciesIds"/>), it does not refuse to bite.</summary>
        public readonly IReadOnlyList<string> SpeciesIds;

        /// <summary>Catch-roll multiplier for a species the school HOLDS (≥ 1), already eased by
        /// <see cref="Strength01"/>.</summary>
        public readonly float SpeciesBoost;

        /// <summary>Catch-roll multiplier for a species the school does NOT hold (≤ 1, never 0), already
        /// eased by <see cref="Strength01"/>.</summary>
        public readonly float OffSpeciesDamp;

        public SchoolInfluence(float effectiveMarks, float biteRateMultiplier, float strength01,
                               IReadOnlyList<string> speciesIds, float speciesBoost, float offSpeciesDamp)
        {
            EffectiveMarks = effectiveMarks;
            BiteRateMultiplier = biteRateMultiplier;
            Strength01 = strength01;
            SpeciesIds = speciesIds;
            SpeciesBoost = speciesBoost;
            OffSpeciesDamp = offSpeciesDamp;
        }

        /// <summary>No fish under the rod — the neutral influence every legacy/unwired path carries.</summary>
        public static SchoolInfluence None => new SchoolInfluence(0f, 1f, 0f, null, 1f, 1f);

        /// <summary>True when there is a school under the rod worth anything.</summary>
        public bool OnFish => EffectiveMarks > 0f;

        /// <summary>What to multiply the bite WAIT by: the reciprocal of the rate. Clamped so a
        /// <c>default</c> struct (rate 0) reads as 1 rather than dividing by zero.</summary>
        public float BiteDelayScale => 1f / Mathf.Max(1f, BiteRateMultiplier);

        /// <summary>
        /// The catch-roll weight this influence gives one species: the boost if the school holds it, the
        /// damp if it doesn't, and exactly 1 when no species are stated — so a school with nothing
        /// authored raises the bite rate but says nothing about what is down there, and an empty influence
        /// is bit-for-bit the pre-school roll.
        /// </summary>
        public float AffinityFor(string speciesId)
        {
            if (SpeciesIds == null || SpeciesIds.Count == 0 || string.IsNullOrEmpty(speciesId)) return 1f;
            for (int i = 0; i < SpeciesIds.Count; i++)
                if (string.Equals(SpeciesIds[i], speciesId, System.StringComparison.Ordinal))
                    return Mathf.Max(1f, SpeciesBoost);
            return Mathf.Clamp(OffSpeciesDamp, 0.0001f, 1f);
        }

        // ---- the one reduction --------------------------------------------------------------------

        /// <summary>
        /// Read the schools at a spot and reduce them to what the cast is worth — the ONE place the
        /// gameplay side turns <see cref="IFishSchools"/> into numbers.
        ///
        /// <para><b>Depth is folded in through <see cref="FishSchoolMath.DepthMatch01"/></b>, which reuses
        /// the depth drop's existing bands rather than inventing a second "about the right depth" rule.
        /// A cast with no depth game is not judged on depth at all (see that method), so the bobber path
        /// is never made worse by this feature and <see cref="DepthDropMath"/>'s count-the-fall read is
        /// untouched — the finder tells you a spot and roughly how deep; judging the drop is still yours.</para>
        ///
        /// <para><b>Allocation-free</b> given the two scratch lists (rule 7): the caller owns them and
        /// reuses them forever. <paramref name="speciesScratch"/> is COPIED into rather than aliased,
        /// because the model's own species buffers are rewritten by the next query.</para>
        /// </summary>
        /// <param name="schools">The model — <see cref="GameServices.FishSchools"/>, which is never null.</param>
        /// <param name="worldPos">Where the line is in the water.</param>
        /// <param name="gameSeconds">The clock's <c>TotalSeconds</c>.</param>
        /// <param name="heldDepthM">The depth the rig is held at (m), or &lt; 0 for a cast with no depth game.</param>
        /// <param name="depth">The owner's depth-band thresholds (<c>GameConfig.DepthDrop</c>).</param>
        /// <param name="s">The owner's school tuning (<c>GameConfig.FishSchools</c>).</param>
        public static SchoolInfluence At(IFishSchools schools, Vector2 worldPos, double gameSeconds,
                                         float heldDepthM, in DepthDropSettings depth,
                                         in FishSchoolSettings s,
                                         List<FishSchool> schoolScratch, List<string> speciesScratch)
        {
            if (schools == null || schoolScratch == null) return None;

            int n = schools.SchoolsAt(worldPos, gameSeconds, schoolScratch);
            if (n <= 0) return None;

            FishSchoolSettings t = FishSchoolMath.Sanitized(in s);
            speciesScratch?.Clear();

            float effectiveMarks = 0f;
            float strongest = 0f;

            for (int i = 0; i < n; i++)
            {
                FishSchool school = schoolScratch[i];
                float signal = school.SignalAt(worldPos);
                if (signal <= 0f) continue;

                float match = FishSchoolMath.DepthMatch01(heldDepthM, school.DepthMetres, in depth, in t);
                float weight = signal * match;
                if (weight <= 0f) continue;

                effectiveMarks += Mathf.Max(0, school.MarkCount) * weight;
                if (weight > strongest) strongest = weight;

                if (speciesScratch != null && school.SpeciesIds != null)
                    for (int k = 0; k < school.SpeciesIds.Count; k++)
                        Add(speciesScratch, school.SpeciesIds[k]);
            }

            if (effectiveMarks <= 0f) return None;

            float strength01 = Mathf.Clamp01(strongest);
            return new SchoolInfluence(
                effectiveMarks,
                FishSchoolMath.BiteRateMultiplier(effectiveMarks, in t),
                strength01,
                speciesScratch != null && speciesScratch.Count > 0 ? speciesScratch : null,
                Mathf.Lerp(1f, t.SchoolSpeciesBoost, strength01),
                Mathf.Lerp(1f, t.OffSchoolSpeciesDamp01, strength01));
        }

        private static void Add(List<string> ids, string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            for (int i = 0; i < ids.Count; i++)
                if (string.Equals(ids[i], id, System.StringComparison.Ordinal)) return;
            ids.Add(id);
        }
    }
}
