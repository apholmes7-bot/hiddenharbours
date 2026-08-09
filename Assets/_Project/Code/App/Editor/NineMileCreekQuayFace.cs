#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;                // ITidalTerrain

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE QUAY FACE AT NINE MILE CREEK — THE MEASUREMENT, AND WHY PHASE B DOES NOT DRAW IT YET.</b>
    ///
    /// <para>A-1 (#462) authored both wharf walls as terrain fills, registered them as standable floor at
    /// the measured deck height, and deliberately DREW NO QUAY — leaving the drawing to Phase B and the
    /// ISO wharf pack, with one flag attached: <i>"the quay face is 4.6 m tall and the kit bakes a 0.75 m
    /// face, so Phase B must tile the face vertically."</i></para>
    ///
    /// <para><b>⭐ THE FLAG IS RIGHT THAT THERE IS A GAP AND WRONG ABOUT WHAT IT IS.</b> 0.75 m is the
    /// OLD near-plan tile kit's 24 px face. The ISO pack is a different thing: a parametric rig baked
    /// through the shared ¾ camera, and every structural preset in it carries a <b>2.80 m</b> face, not
    /// 0.75 m. The gap is 2.4 m, not 3.85 m — and, more usefully, it is <b>ONE PARAMETER</b> rather than
    /// a modelling limit:</para>
    ///
    /// <list type="number">
    /// <item><description>The rig quotes deck height as clearance above the HIGHEST water:
    /// <c>deckZ = tideRange + clearance</c>. The pack baked at the rig's own defaults —
    /// <see cref="BakedRigTideRange"/> m of range, <see cref="BakedRigClearance"/> m of clearance — so
    /// every non-float preset came out at <see cref="BakedDeckZMetres"/> m.</description></item>
    /// <item><description>Nine Mile Creek's tide is <see cref="RequiredTideRangeMetres"/> m from datum
    /// to highest water, and its authored deck stands <see cref="RequiredClearanceMetres"/> m above
    /// spring high — which <c>NineMileCreekMainland</c> states in words ("0.8 m of freeboard at spring
    /// high water"). Put those through the rig's OWN formula and the deck this region needs is
    /// <see cref="RequiredDeckZMetres"/> m — the same arithmetic, different coast.</description></item>
    /// <item><description>So the pack is not short of geometry. It is baked for a 1.8 m tide and this is
    /// a 4.4 m one, and every structural height in it is short by exactly that difference
    /// (<see cref="ShortfallMetres"/> m).</description></item>
    /// </list>
    ///
    /// <para><b>⛔ AND VERTICAL TILING DOES NOT RESCUE IT — but NOT because the arithmetic fails, which
    /// is the easy version of this finding and the wrong one.</b> Stacking needs a course with a flat,
    /// FURNITURE-FREE top: anything carrying bollards, ladders or hung fenders puts them halfway up the
    /// finished wall. Of the pack's 17 presets only the four riprap ones bake no fittings, and three of
    /// those are mounds with sloped tops (two revetments and the breakwater) — leaving <c>sheetCell</c>
    /// as the single stackable course in the pack. <see cref="StackableCourses"/> is that list, and the
    /// two facts about it that matter are recorded as data rather than as prose:</para>
    /// <list type="bullet">
    /// <item><description><b>Two <c>sheetCell</c> courses come to 5.20 m — this wharf's required deck
    /// height, exactly.</b> <see cref="AStackReachesTheHeight"/> says so, on purpose, so nobody can
    /// later restate this stop as "the pieces do not add up".</description></item>
    /// <item><description><b>What rules it out is the MATERIAL and the missing deck.</b> Both courses
    /// would be steel sheet pile — the one material this wharf is ruled not to be built of (#462 read
    /// the owner's photographs as log crib and reserved sheet pile for "the commercial quay money and
    /// machinery would build") — and a capped cell has no working top: no coping, nothing to land a
    /// catch on, nothing to stand a bollard beside.</description></item>
    /// <item><description><b>In the ruled material there is no course at all.</b> Crib bakes a plank or
    /// concrete deck with furniture on it, so nothing seats on it; and even ignoring that, crib's 2.80 m
    /// stacks to 2.80 (2.40 m short) or 5.60 (0.40 m over). 0.40 m over is a deck drawn 10 px above the
    /// <c>StandablePlatform</c> the sim stands you on — the same class of quiet lie as a wharf that
    /// floods without anything failing.</description></item>
    /// </list>
    ///
    /// <para><b>THE ASK (art-director / art-pipeline lane — this file reports it rather than doing
    /// it).</b> Re-bake the wharf ISO pack, or a Nine Mile Creek variant of it, at
    /// <c>{ tideRange: 4.4, clearance: 0.8 }</c> — equivalently <c>{ deckZ: 5.2 }</c>. <b>No new
    /// geometry is needed and no new preset is needed:</b> the rig already renders it (its deckZ clamp
    /// is <c>max(familyMax, tideRange + 3)</c>, which opens to 7.4 m at this tide), it draws the whole
    /// 4.6 m face in ONE course, and — the part a stack could never have fixed — it re-pins the growth
    /// bands to the real frame. The baked sheets put barnacle at 0.72–1.44 m and rockweed at
    /// 0.11–0.72 m above datum, sized for a 1.8 m tide; on this coast they belong at 1.76–3.52 m and
    /// 0.26–1.76 m. A stacked wall would have worn two sets of them, at the wrong heights, twice.</para>
    ///
    /// <para><b>What this class is FOR, now that it draws nothing.</b> It is the measurement, so the
    /// re-bake is ordered off derived numbers rather than off a paragraph — and so that the day the
    /// sheets land, the placement reads its deck height from here instead of re-deriving it. Pure:
    /// no scene, no assets, no rig. <c>NineMileCreekDressingTests</c> holds every claim above.</para>
    /// </summary>
    public static class NineMileCreekQuayFace
    {
        // =============================================================================================
        //  1. WHAT THIS WHARF NEEDS — derived from the authored geography, never typed in
        // =============================================================================================
        // The rig's frame and the game's frame are BOTH "metres above chart datum" but they do not
        // share a zero: the rig puts z = 0 at the LOWEST water ("z = 0 chart datum — lowest water"),
        // while the game's datum is mean water with the tide swinging either side of it. One conversion,
        // in one place, is what stops every number below from being out by an amplitude.

        /// <summary>A game elevation (m above the game's datum) in the RIG's frame — metres above the
        /// lowest water. This is the only place the two frames are reconciled.</summary>
        public static float ToRigZ(float gameElevation) =>
            gameElevation - NineMileCreekMainland.SpringLowWater;

        /// <summary>The rig's <c>tideRange</c> for this region: datum (lowest water) to highest water.
        /// 4.4 m — twice the amplitude, and comfortably inside the rig's own 0.3–14 m band.</summary>
        public static float RequiredTideRangeMetres => ToRigZ(NineMileCreekMainland.SpringHighWater);

        /// <summary>The rig's <c>clearance</c> for this region: how far the deck stands above the
        /// HIGHEST water. 0.8 m — the freeboard <c>NineMileCreekMainland</c> authored.</summary>
        public static float RequiredClearanceMetres =>
            NineMileCreekMainland.WharfDeckElevation - NineMileCreekMainland.SpringHighWater;

        /// <summary>The rig's <c>deckZ</c> for this region — the deck's height above lowest water.
        /// 5.2 m, and it agrees with <see cref="RequiredTideRangeMetres"/> +
        /// <see cref="RequiredClearanceMetres"/> because that is the rig's own auto rule.</summary>
        public static float RequiredDeckZMetres => ToRigZ(NineMileCreekMainland.WharfDeckElevation);

        /// <summary>
        /// The STRUCTURAL face: deck down to the ground the wall actually stands on, 4.6 m. Shorter than
        /// <see cref="RequiredDeckZMetres"/> because the harbour shoal is filled to −1.6 m and the last
        /// 0.6 m of the rig's frame is under the seabed, where nothing is drawn.
        /// </summary>
        public static float StructuralFaceMetres =>
            NineMileCreekMainland.WharfDeckElevation - NineMileCreekMainland.BasinBedElevation;

        /// <summary>The same face MEASURED off the authored terrain rather than read off the constants —
        /// the #462 discipline, so a terrain edit that lowered the wall shows up here instead of leaving
        /// this class quietly describing a wharf the region no longer has.</summary>
        public static float StructuralFaceFrom(ITidalTerrain terrain) =>
            terrain == null
                ? StructuralFaceMetres
                : NineMileCreekWharf.DeckElevationFrom(terrain) - NineMileCreekMainland.BasinBedElevation;

        // =============================================================================================
        //  2. WHAT THE PACK ACTUALLY BAKED
        // =============================================================================================
        // These are the rig's DEFAULTS, quoted with their source, not measurements of the PNGs — the
        // sheets were baked "at rig-default dimensions and tide" (wharfIsoRig.contract.json's own
        // `generated` line), so the defaults ARE what the pixels carry. Stating the rule rather than the
        // answer means a re-bake at a different tide is a one-line edit here, not a re-measurement.

        /// <summary>The rig's default <c>tideRange</c> (<c>DEFAULTS.tideRange</c>) — the tide the
        /// committed sheets were baked for. 1.8 m: the Gulf figure in the rig's own README table.</summary>
        public const float BakedRigTideRange = 1.8f;

        /// <summary>The rig's default deck clearance above highest water — the <c>1.0</c> in
        /// <c>auto = tideRange + (opts.clearance ?? 1.0)</c>.</summary>
        public const float BakedRigClearance = 1.0f;

        /// <summary>What every non-float preset in the committed pack therefore stands at: 2.80 m.
        /// <b>This is the number A-1's flag should have said instead of 0.75 m</b> — that figure is the
        /// retired near-plan tile kit's 24 px face and has nothing to do with this pack.</summary>
        public static float BakedDeckZMetres => BakedRigTideRange + BakedRigClearance;

        /// <summary>How far short the tallest baked course falls: 2.40 m, which is exactly the
        /// difference between the two tides. That equality is the whole finding.</summary>
        public static float ShortfallMetres => RequiredDeckZMetres - BakedDeckZMetres;

        /// <summary>
        /// One piece of the pack that could serve as a lower COURSE — a flat, furniture-free top another
        /// piece can seat on — and the two things about it that decide whether it may be used here.
        /// </summary>
        public readonly struct Course
        {
            /// <summary>The preset key, so a reader can go and look at it.</summary>
            public readonly string Key;
            /// <summary>Its baked deck height above the rig's datum, in metres.</summary>
            public readonly float DeckZMetres;
            /// <summary>Whether it is the material THIS wharf is built of. #462 read the owner's
            /// photographs as timber crib — "log boxes filled with stone, what a small community wharf
            /// actually builds" — and ruled sheet pile out as "the commercial quay money and machinery
            /// would build".</summary>
            public readonly bool IsRuledMaterial;
            /// <summary>Whether its top is a WORKING deck — coping, curb, something to land a catch on
            /// and stand a bollard beside — as opposed to a capped mass you merely cannot fall into.</summary>
            public readonly bool HasWorkingDeck;

            public Course(string key, float deckZMetres, bool isRuledMaterial, bool hasWorkingDeck)
            {
                Key = key; DeckZMetres = deckZMetres;
                IsRuledMaterial = isRuledMaterial; HasWorkingDeck = hasWorkingDeck;
            }

            /// <summary>Whether this course may be used to build THIS wharf's face.</summary>
            public bool UsableHere => IsRuledMaterial && HasWorkingDeck;
        }

        /// <summary>
        /// The pack's stackable courses. <b>There is exactly one</b>, and that is the finding.
        ///
        /// <para>Of the 17 presets, only the four <c>riprap</c> ones bake no fittings
        /// (<c>graniteEdge</c>, <c>redEdge</c>, <c>breakwater</c>, <c>sheetCell</c>); of those, the two
        /// revetments and the breakwater are MOUNDS whose tops are slopes, so nothing seats on them.
        /// Everything else carries bollards, rings, ladders or hung tyres, and a course with furniture on
        /// it puts that furniture halfway up the finished wall.</para>
        /// </summary>
        public static IReadOnlyList<Course> StackableCourses() => new[]
        {
            // A rock-filled steel sheet-pile cell with a cast cap: the only flat furniture-free top in
            // the pack, and the one preset that overrides the auto deck height (2.60 m, not 2.80).
            new Course("sheetCell", 2.6f, isRuledMaterial: false, hasWorkingDeck: false),
        };

        /// <summary>The stackable course's preset key, so the report names the piece rather than a
        /// number.</summary>
        public const string StackableCourseKey = "sheetCell";

        /// <summary>
        /// ⚠️ <b>THE ARITHMETIC DOES CLOSE, AND SAYING OTHERWISE WOULD BE THE EASY LIE.</b> Two
        /// <c>sheetCell</c> courses come to 5.20 m — this wharf's required deck height, to the
        /// centimetre. What rules the stack out is not the height; it is that both courses would be
        /// steel sheet pile, the one material this wharf is ruled not to be built of, and that neither
        /// has a working deck to land a catch on. That is a judgement about the PLACE, so it is recorded
        /// as data on <see cref="Course"/> and applied in <see cref="BakedPackCanDrawTheFace"/> rather
        /// than smuggled into a search that then reports "impossible".
        ///
        /// <para>The closest total a stack of <paramref name="courseMetres"/> can reach, and how many it
        /// takes. Exhaustive rather than clever — a five-course ceiling is already 13 m, past any wharf
        /// this world has — and overshoot is reported as a signed error rather than silently preferred,
        /// because a deck ABOVE the standable platform is its own defect.</para>
        /// </summary>
        public static void BestStackOf(float courseMetres, float targetMetres,
                                       out float bestTotal, out int courses)
        {
            bestTotal = 0f;
            courses = 0;
            if (courseMetres <= 0f) return;

            float bestError = Mathf.Abs(targetMetres);
            for (int n = 1; n <= MaxCoursesConsidered; n++)
            {
                float total = courseMetres * n;
                float error = Mathf.Abs(targetMetres - total);
                if (error >= bestError) continue;
                bestError = error;
                bestTotal = total;
                courses = n;
            }
        }

        /// <summary>The best any stackable course can do against a target, ignoring whether it may be
        /// used here — the pure arithmetic half of the finding.</summary>
        public static void BestStackTo(float targetMetres, out float bestTotal, out int courses)
        {
            bestTotal = 0f;
            courses = 0;
            float bestError = Mathf.Abs(targetMetres);

            foreach (Course course in StackableCourses())
            {
                BestStackOf(course.DeckZMetres, targetMetres, out float total, out int n);
                if (n == 0) continue;
                float error = Mathf.Abs(targetMetres - total);
                if (error >= bestError) continue;
                bestError = error;
                bestTotal = total;
                courses = n;
            }
        }

        /// <summary>How tall a stack this search will entertain. Five courses of the shortest piece is
        /// already 13 m — well past any wharf this world has — so nothing useful lies beyond it, and a
        /// bound keeps the search a thing you can check in your head.</summary>
        public const int MaxCoursesConsidered = 5;

        /// <summary>How close a stacked deck must land to the measured one to be worth drawing, in
        /// metres. Half the 0.10 m the terrain's own deck test allows, because a deck is drawn at
        /// <c>cos 40° × 32</c> ≈ 24.5 px per metre and anything past this is a visible step between
        /// where you stand and where the planks are.</summary>
        public const float DeckHeightToleranceMetres = 0.05f;

        /// <summary>
        /// Whether the committed pack can build this wharf's face — reaching the deck height with a
        /// course that may actually be used here. False, and <see cref="StackReport"/> says why in the
        /// same words the PR does.
        /// </summary>
        public static bool BakedPackCanDrawTheFace()
        {
            foreach (Course course in StackableCourses())
            {
                if (!course.UsableHere) continue;
                BestStackOf(course.DeckZMetres, RequiredDeckZMetres, out float total, out _);
                if (Mathf.Abs(RequiredDeckZMetres - total) <= DeckHeightToleranceMetres) return true;
            }
            return false;
        }

        /// <summary>Whether a stack could reach the height at all, setting aside whether the material is
        /// this wharf's. True — two <c>sheetCell</c> courses land on it exactly — and it is stated as its
        /// own fact so the stop cannot quietly become "the arithmetic does not work".</summary>
        public static bool AStackReachesTheHeight()
        {
            BestStackTo(RequiredDeckZMetres, out float total, out _);
            return Mathf.Abs(RequiredDeckZMetres - total) <= DeckHeightToleranceMetres;
        }

        // =============================================================================================
        //  3. THE ORDER — what art-director needs, as numbers
        // =============================================================================================

        /// <summary>
        /// The bake this region needs, as the rig's own option names. Handed to the PR and to whoever
        /// runs <i>Hidden Harbours ▸ Art ▸ Bake Iso Pack</i>, so the re-bake is ordered off derived
        /// numbers and not off a remembered paragraph.
        /// </summary>
        public static string RequiredBakeOptions() =>
            $"{{ tideRange: {RequiredTideRangeMetres:0.##}, clearance: {RequiredClearanceMetres:0.##} }}" +
            $"  // deckZ resolves to {RequiredDeckZMetres:0.##} m";

        /// <summary>The finding, in one paragraph, from the numbers above — logged by the builder so the
        /// owner sees it in the console after a scene rebuild rather than only in a merged PR.</summary>
        public static string StackReport()
        {
            BestStackTo(RequiredDeckZMetres, out float total, out int courses);
            return
                $"[NineMileCreekQuayFace] THE DRAWN QUAY IS STILL NOT DRAWN, and this is the reason. " +
                $"This wharf needs a {StructuralFaceMetres:0.0} m face with its deck " +
                $"{RequiredDeckZMetres:0.0} m above lowest water. The committed ISO pack baked at the " +
                $"rig's default {BakedRigTideRange:0.0} m tide, so every structural preset stands at " +
                $"{BakedDeckZMetres:0.0} m — short by {ShortfallMetres:0.0} m, which is exactly the " +
                $"difference between that tide and this one ({RequiredTideRangeMetres:0.0} m). Vertical " +
                $"tiling does not rescue it, and NOT for an arithmetic reason: '{StackableCourseKey}' is " +
                $"the pack's only flat furniture-free course and {courses} of them reach {total:0.00} m " +
                $"against {RequiredDeckZMetres:0.00} m — the height lands exactly. What rules the stack " +
                $"out is that both courses would be steel sheet pile, the one material this wharf is " +
                $"ruled not to be built of, with no working deck to land a catch on. " +
                $"THE FIX IS A RE-BAKE, NOT A NEW PIECE — the rig already " +
                $"draws this wall in one course at {RequiredBakeOptions()} — and it also re-pins the " +
                "growth bands, which no stack could have fixed. Art-director's lane; reported, not " +
                "worked around. The wharf's decor, services and shore finds are placed regardless: " +
                "none of them depend on the face.";
        }
    }
}
#endif
