#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Art;                 // SpriteLightMath — the shared bake camera's two scales
using HiddenHarbours.Core;                // ITidalTerrain

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>THE QUAY FACE AT NINE MILE CREEK — THE MEASUREMENT, AND NOW THE PLACEMENT IT WAS KEPT FOR.</b>
    ///
    /// <para>A-1 (#462) authored both wharf walls as terrain fills, registered them as standable floor at
    /// the measured deck height, and deliberately DREW NO QUAY. #471 measured why the ISO pack could not
    /// draw it — <b>2.40 m of missing face</b> — and ordered the fix as two numbers on one bake call
    /// rather than as new geometry. #477 re-parameterised the rig at those numbers and #478 re-baked all
    /// 17 sheets. <b>This class now describes the sheets that are actually committed, and the quay is
    /// drawn</b> (<see cref="NineMileCreekDressing.FacePieces"/>).</para>
    ///
    /// <para><b>⭐ THE TRIPWIRE, AND WHY IT IS THE CENTRE OF THIS FILE.</b> #471 wrote
    /// <see cref="TideShortfallMetres"/> with the note <i>"if it ever goes to zero the pack has been
    /// re-baked at this tide"</i>. It is zero. So are <see cref="ClearanceShortfallMetres"/> and
    /// <see cref="ShortfallMetres"/>, and that is the whole gate: the numbers below are not a story about
    /// the bake, they are <b>recomputed from the region's own geography and from the rig's committed
    /// defaults</b>, and they agree. If a future re-bake moves the face, all three come off zero and
    /// <c>NineMileCreekDressingTests</c> fails loudly here rather than leaving a quay drawn at the wrong
    /// height — which is the one defect this whole apparatus exists to make impossible, because a wall
    /// two metres short renders perfectly and merely looks like a slightly wrong wharf.</para>
    ///
    /// <para><b>WHAT THE RE-BAKE ACTUALLY BOUGHT</b>, in the order it matters:</para>
    /// <list type="number">
    /// <item><description><b>One course, not a stack.</b> Every non-float preset now stands at
    /// <see cref="BakedDeckZMetres"/> m, which is this wharf's <see cref="RequiredDeckZMetres"/> m to the
    /// centimetre, so <see cref="CoursesNeeded"/> is 1 and the face covers deck lip to seabed in a single
    /// piece.</description></item>
    /// <item><description><b>The fittings land AT the deck.</b> That is the half a vertical stack could
    /// never have had: a lower course carrying bollards, rings or a ladder puts that furniture halfway up
    /// the finished wall. With one course the pack's own fittings sit on the deck, where a rope goes
    /// round them. <see cref="FittingsLandAtTheDeck"/>.</description></item>
    /// <item><description><b>The growth bands are re-pinned to the real frame.</b> The old sheets put
    /// barnacle at 0.72–1.44 m and rockweed at 0.11–0.72 m, sized for a 1.8 m tide; this coast wanted
    /// 1.76–3.52 and 0.26–1.76. The rig pins growth to the tidal FRAME as fractions of the range, so the
    /// re-bake moved them for free — <see cref="BarnacleBand"/> / <see cref="RockweedBand"/> compute the
    /// asked-for figures from <see cref="BakedRigTideRange"/> alone. <b>No stack could have fixed this</b>:
    /// a stacked wall would have worn two sets of bands, at the wrong heights, twice.</description></item>
    /// </list>
    ///
    /// <para><b>What this class is FOR.</b> Three things, and it does no drawing itself — the drawing is
    /// <see cref="NineMileCreekDressing"/>'s, which reads its deck height from here instead of
    /// re-deriving it (#471 said it would, and this is that promise kept):</para>
    /// <list type="bullet">
    /// <item><description>the MEASUREMENT — what this wharf needs, derived from its own authored
    /// geography and never typed in (§1);</description></item>
    /// <item><description>the BAKE — what the committed sheets carry, quoted from the rig's defaults with
    /// their source, so a re-bake is a one-line edit here rather than a re-measurement (§2);</description></item>
    /// <item><description>the PLACEMENT ARITHMETIC — where a piece's pivot goes so that its drawn deck
    /// lip lands on the wall's real lip (§4). Pure <see cref="Vector2"/> trigonometry, so every rule is
    /// asserted headless.</description></item>
    /// </list>
    /// <para>Pure: no scene, no assets, no rig. <c>NineMileCreekDressingTests</c> holds every claim above.</para>
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
        //  2. WHAT THE PACK ACTUALLY BAKED — re-derived at #478, not remembered from #471
        // =============================================================================================
        // ⚠️ THESE ARE THE RIG'S DEFAULTS, quoted with their source, not measurements of the PNGs — the
        // sheets are baked "at rig-default dimensions, tide and clearance" (wharfIsoRig.contract.json's
        // own `generated` line, which now reads "tideRange 4.4, clearance 0.8"), so the defaults ARE what
        // the pixels carry. Stating the RULE rather than the answer is what made the re-bake a one-line
        // edit here instead of a re-measurement — which is exactly what this section just went through.
        //
        // ⚠️ AND THEY ARE NOT WHAT #471's DRAFT OF THIS FILE SAID. They were 1.8 / 1.0. Any number
        // carried over from that draft, from its PR, or from the gameplay sidecar (which #477 did not
        // regenerate) describes sheets that are no longer committed.

        /// <summary>The rig's default <c>tideRange</c> (<c>DEFAULTS.tideRange</c>) — the tide the
        /// committed sheets were baked for. <b>4.4 m: Hillsborough Bay's, which is this region's</b>, set
        /// by #477 off #471's order. It was 1.8 (the Gulf figure) when #471 measured the gap.</summary>
        public const float BakedRigTideRange = 4.4f;

        /// <summary>The rig's default deck clearance above highest water (<c>DEFAULTS.clearance</c>, a
        /// first-class default since #477 rather than a literal buried in the deck auto). <b>0.8 m</b> —
        /// this wharf's authored freeboard. It was 1.0.</summary>
        public const float BakedRigClearance = 0.8f;

        /// <summary>The rig's default <c>mudZ</c> — where a structure's feet stop, 1.4 m BELOW chart
        /// datum. Unchanged by the re-bake, and what makes the drawn face reach past the seabed rather
        /// than stop short of it.</summary>
        public const float BakedRigMudZ = -1.4f;

        /// <summary>What every non-float preset in the committed pack therefore stands at: <b>5.20 m</b>.
        /// It was 2.80, and the 2.40 m between those two numbers is the whole of #471's finding.</summary>
        public static float BakedDeckZMetres => BakedRigTideRange + BakedRigClearance;

        /// <summary>
        /// How far short the tallest baked course falls: <b>zero</b>.
        ///
        /// <para>It was 2.40 m, and it decomposed — <b>+2.60 m of tide</b> less <b>0.20 m of freeboard</b>
        /// — because deck heights are quoted as <c>tideRange + clearance</c> and this wharf authors less
        /// freeboard than the rig's old default. Both halves are now zero, which is a stronger statement
        /// than the total being zero: the bake did not merely land on the right height by luck, it landed
        /// on the right TIDE and the right FREEBOARD, which is what re-pins the growth bands.</para>
        /// </summary>
        public static float ShortfallMetres => RequiredDeckZMetres - BakedDeckZMetres;

        /// <summary>⭐ <b>THE TRIPWIRE.</b> The part of the shortfall that is TIDE. #471 wrote it with the
        /// note <i>"if it ever goes to zero the pack has been re-baked at this tide"</i> — and it is zero.
        /// A future re-bake at any other coast's tide moves this off zero and fails the dressing tests
        /// before it can ship a wall at the wrong height.</summary>
        public static float TideShortfallMetres => RequiredTideRangeMetres - BakedRigTideRange;

        /// <summary>The part that is FREEBOARD — also zero now. This wharf sits closer to its own high
        /// water than a generic coast assumes (a working wharf you step DOWN onto), and #477 made that
        /// the rig's default rather than leaving it as a correction applied afterwards.</summary>
        public static float ClearanceShortfallMetres => RequiredClearanceMetres - BakedRigClearance;

        // --- THE FLOAT: the one family whose deck is NOT tideRange + clearance -------------------------
        // ⚠️ Quoted from the rig with their source, exactly like the three above, and pinned to the JS
        // by NineMileCreekFloatTests so a re-bake that re-shapes the float cannot leave the SIM standing
        // on planks the PIXELS no longer draw. The rig states the rule itself:
        //     "FLOATS RIDE IT: deck z = tide + freeboard"          (wharfIsoRig.js header)
        //     resolve():  s.floatDeckZ = s.tide + s.freeboard;
        // A float is therefore the one family that ignores BakedDeckZMetres entirely. It is NOT immune to
        // the re-bake, though: the rig's baked water level is itself a fraction of the range
        // (s.tide = s.tideRange * 0.55, which the contract records as tide.baked = 2.42), so #478 moved
        // the float cells too — it moved where their WET MASK sits, not what their deck rule is. The game
        // drives the deck from the live tide, so only the two numbers below have to agree with the art.

        /// <summary>The rig's default float <c>freeboard</c> (<c>DEFAULTS.freeboard</c>) — <b>0.40 m</b>,
        /// how high a float's deck rides above her own waterline. The rig clamps it to 0.25–0.80.</summary>
        public const float BakedRigFloatFreeboard = 0.40f;

        // The timber float's structural depth, read straight off the float family's own build(). Four
        // literals, in the order the rig applies them, so the arithmetic below IS the rig's chain rather
        // than a remembered total:
        //     const top = s.floatDeckZ, t = 0.05, frameD = 0.26, fz1 = top - t, fz0 = fz1 - frameD;
        //     ...flotation: const z1 = fz0 + 0.02, bh = 0.42, z0 = z1 - bh;

        /// <summary>The deck planking's thickness (<c>t</c>) — 0.05 m below the walking surface.</summary>
        public const float BakedRigFloatDeckPlankMetres = 0.05f;
        /// <summary>The perimeter frame's depth (<c>frameD</c>) — 0.26 m more.</summary>
        public const float BakedRigFloatFrameDepthMetres = 0.26f;
        /// <summary>How far the billets are slung UP into the frame (<c>z1 = fz0 + 0.02</c>).</summary>
        public const float BakedRigFloatBilletRiseMetres = 0.02f;
        /// <summary>And the flotation billets' own depth (<c>bh</c>) — 0.42 m of poly-shelled foam.</summary>
        public const float BakedRigFloatBilletDepthMetres = 0.42f;

        /// <summary>The timber float's whole structural depth, deck to the bottom of her billets:
        /// <b>0.71 m</b>. What she grounds on is the billets, so this is where her underside is.</summary>
        public static float BakedRigFloatHullDepthMetres =>
            BakedRigFloatDeckPlankMetres + BakedRigFloatFrameDepthMetres
            - BakedRigFloatBilletRiseMetres + BakedRigFloatBilletDepthMetres;

        /// <summary>
        /// ⭐ <b>THE FLOAT'S DRAUGHT — 0.31 m</b>, and the only number in this pass that decides when she
        /// takes the ground: her hull depth less the freeboard the same drawing gives her. Derived, so a
        /// re-bake that makes the billets deeper grounds her earlier instead of leaving the sim floating
        /// a float the art has already put on the mud.
        /// </summary>
        public static float BakedRigFloatDraughtMetres =>
            BakedRigFloatHullDepthMetres - BakedRigFloatFreeboard;

        /// <summary>
        /// The gangway run the rig would solve for THIS coast if it were left to itself:
        /// <c>clamp((tideRange + freeboard + 0.9) × 2.4, 3, 14)</c> = <b>13.68 m</b> (the float family's
        /// own <c>gRun</c> auto, for a brow it hangs itself). Quoted so the region can say by how much its
        /// authored brow differs from what the art would have chosen — see
        /// <c>NineMileCreekWharf.GangwayRunMetres</c>, which is shorter, and says so.
        /// </summary>
        public static float RigAutoGangwayRunMetres =>
            Mathf.Clamp((BakedRigTideRange + BakedRigFloatFreeboard + 0.9f) * 2.4f, 3f, 14f);

        /// <summary>The rig's default gangway walking width (<c>DEFAULTS.gangWidth</c>) — <b>0.95 m</b>,
        /// inside the gangway family's own 0.80–1.40 band. The narrowest walking surface this region
        /// has.</summary>
        public const float BakedRigGangwayWidthMetres = 0.95f;

        /// <summary>The rig's default float WIDTH (<c>FAMILIES.float.dims.width</c> = [1.8, 3.6, 2.4]) —
        /// <b>2.4 m</b> across, which is what a chained run of float modules measures.</summary>
        public const float BakedRigFloatWidthMetres = 2.4f;

        // --- the growth bands: the half no stack could ever have fixed --------------------------------
        // The rig pins growth to the tidal FRAME as fractions of the range (frame(): barnTop = R×0.80,
        // barnBot = R×0.40, weedTop = R×0.40, weedBot = R×0.06), never to the water level. So the ONE
        // parameter that fixed the height fixed these too, and they are recomputed here rather than
        // copied: #471 asked for barnacle at 1.76–3.52 m and rockweed at 0.26–1.76 m, and that is what
        // 4.4 m of range produces.

        /// <summary>Fractions of <see cref="BakedRigTideRange"/> the rig bands growth at
        /// (<c>frame()</c>): barnacle 0.40–0.80 of the range, rockweed 0.06–0.40.</summary>
        public const float BarnacleBandLowFraction = 0.40f, BarnacleBandHighFraction = 0.80f;
        /// <inheritdoc cref="BarnacleBandLowFraction"/>
        public const float RockweedBandLowFraction = 0.06f, RockweedBandHighFraction = 0.40f;

        /// <summary>Where the barnacle band lies on the committed sheets, in metres above chart datum —
        /// 1.76 to 3.52 m. The band a wall on THIS coast wears.</summary>
        public static Vector2 BarnacleBand => new Vector2(
            BakedRigTideRange * BarnacleBandLowFraction, BakedRigTideRange * BarnacleBandHighFraction);

        /// <summary>Where the rockweed band lies — 0.26 to 1.76 m above chart datum.</summary>
        public static Vector2 RockweedBand => new Vector2(
            BakedRigTideRange * RockweedBandLowFraction, BakedRigTideRange * RockweedBandHighFraction);

        // =============================================================================================
        //  3. THE COURSE THAT DRAWS IT
        // =============================================================================================

        /// <summary>
        /// One piece of the pack that could serve as a COURSE, and the two things about it that decide
        /// whether it may be used here.
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
        /// <b>THE PIECE THIS WHARF IS DRAWN WITH.</b> <c>logCrib</c> — the pack's drift-pinned log crib
        /// with a plank deck, which is #462's ruling in the pack's own vocabulary: <i>"log boxes filled
        /// with stone, what a small community wharf actually builds"</i>. Sheet pile stays reserved for
        /// "the commercial quay money and machinery would build", so <c>torbayQuay</c> and
        /// <c>sheetedPier</c> are ruled out here however well they would render.
        /// </summary>
        public const string FaceCourseKey = "logCrib";

        /// <summary>…and the rig FAMILY that preset resolves to. Held equal to
        /// <c>NineMileCreekWharf.BreakwaterArmour</c> by test, so the region's material ruling and the
        /// piece drawn for it cannot drift apart.</summary>
        public const string FaceCourseFamily = "crib";

        /// <summary>
        /// How much wall ONE piece covers along the run: 9.6 m — the crib family's default
        /// <c>bays</c> × <c>bayLen</c> (3 × 3.2), which <c>logCrib</c> does not override. Quoted from the
        /// rig with its source, the same discipline as the tide above. <b>Independent of the re-bake</b>:
        /// tide and clearance move the piece's HEIGHT, never its plan.
        /// </summary>
        public const int FaceCourseBays = 3;
        /// <inheritdoc cref="FaceCourseBays"/>
        public const float FaceCourseBayLengthMetres = 3.2f;
        /// <inheritdoc cref="FaceCourseBays"/>
        public static float FaceCourseRunMetres => FaceCourseBays * FaceCourseBayLengthMetres;

        /// <summary>How deep one piece is ACROSS the run — the crib family's default <c>width</c>, 5 m.
        /// It is the reason a piece's pivot is not simply its lip: half of it lies between the two
        /// (§4).</summary>
        public const float FaceCourseWidthMetres = 5f;

        /// <summary>The course the face is built of, as data — so "may this be used here?" is answered by
        /// the same rule that once answered "no".</summary>
        public static Course FaceCourse() =>
            new Course(FaceCourseKey, BakedDeckZMetres, isRuledMaterial: true, hasWorkingDeck: true);

        /// <summary>
        /// The pack's stackable courses — flat, furniture-free tops another piece could seat on.
        /// <b>There is still exactly one</b>, and it is still not this wharf's material. It is kept
        /// because it is the record of what the alternative would have been: of the 17 presets only the
        /// four <c>riprap</c> ones bake no fittings, and of those the two revetments and the breakwater
        /// are MOUNDS whose tops are slopes.
        ///
        /// <para>⚠️ <c>sheetCell</c> is one of the THREE presets the re-bake did not move (it pins its
        /// own <c>deckZ</c>), so this number is still 2.60 and is not stale.</para>
        /// </summary>
        public static IReadOnlyList<Course> StackableCourses() => new[]
        {
            new Course("sheetCell", 2.6f, isRuledMaterial: false, hasWorkingDeck: false),
        };

        /// <summary>The stackable course's preset key, so the report names the piece rather than a
        /// number.</summary>
        public const string StackableCourseKey = "sheetCell";

        /// <summary>
        /// The closest total a stack of <paramref name="courseMetres"/> can reach, and how many it takes.
        /// Exhaustive rather than clever — a five-course ceiling is already 13 m, past any wharf this
        /// world has — and overshoot is reported as a signed error rather than silently preferred,
        /// because a deck ABOVE the standable platform is its own defect.
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
        /// used here — the pure arithmetic half of #471's finding, kept because it is what stopped that
        /// stop from being restated as "the pieces do not add up".</summary>
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

        /// <summary>How close a drawn deck must land to the measured one to be worth drawing, in metres.
        /// Half the 0.10 m the terrain's own deck test allows, because a deck is drawn at
        /// <c>cos 40° × 32</c> ≈ 24.5 px per metre and anything past this is a visible step between where
        /// you stand and where the planks are.</summary>
        public const float DeckHeightToleranceMetres = 0.05f;

        /// <summary>
        /// How many courses of <see cref="FaceCourse"/> it takes to reach this wharf's deck: <b>1</b>.
        /// The number the whole re-bake was ordered to produce.
        /// </summary>
        public static int CoursesNeeded
        {
            get
            {
                BestStackOf(FaceCourse().DeckZMetres, RequiredDeckZMetres, out _, out int courses);
                return courses;
            }
        }

        /// <summary>⭐ The face is drawn in ONE course — no vertical tiling, and therefore no seam and no
        /// second set of growth bands.</summary>
        public static bool DrawnInOneCourse => CoursesNeeded == 1;

        /// <summary>
        /// Whether the committed pack can build this wharf's face — reaching the deck height with a
        /// course that may actually be used here. <b>True since #478</b>; it was false for the whole of
        /// #471, and <see cref="FaceReport"/> says so in the same words the PR does.
        /// </summary>
        public static bool BakedPackCanDrawTheFace()
        {
            Course course = FaceCourse();
            if (!course.UsableHere) return false;
            BestStackOf(course.DeckZMetres, RequiredDeckZMetres, out float total, out _);
            return Mathf.Abs(RequiredDeckZMetres - total) <= DeckHeightToleranceMetres;
        }

        /// <summary>
        /// ⭐ <b>THE FITTINGS LAND AT THE DECK.</b> The half of the re-bake a stack could never have
        /// delivered: the pack bakes a course's bollards, rings, ladder and hung tyres at ITS deck, so a
        /// piece used as a lower course puts that furniture halfway up the finished wall. One course, at
        /// the height the wharf actually stands, puts them where a rope goes round them.
        /// </summary>
        public static bool FittingsLandAtTheDeck =>
            DrawnInOneCourse && Mathf.Abs(ShortfallMetres) <= DeckHeightToleranceMetres;

        /// <summary>Whether a stack could have reached the height at all, setting aside whether the
        /// material is this wharf's. True — two <c>sheetCell</c> courses land on it exactly — and it is
        /// kept as its own fact so #471's stop can never be misremembered as "the arithmetic did not
        /// work".</summary>
        public static bool AStackReachesTheHeight()
        {
            BestStackTo(RequiredDeckZMetres, out float total, out _);
            return Mathf.Abs(RequiredDeckZMetres - total) <= DeckHeightToleranceMetres;
        }

        // =============================================================================================
        //  4. WHERE A PIECE GOES — the pivot, and the one line that has to be right
        // =============================================================================================
        //
        // ⚠️ THE WHARF PACK'S PIVOT IS NOT A GROUND-CONTACT PIVOT, and that is the trap this section
        // exists to absorb. Every other pack this region places (wharfDecor, utilityIso, shoreFinds)
        // pivots where the piece TOUCHES THE GROUND, so a prop's plan position is its transform position
        // and nothing is offset. The wharf pack's contract says something different:
        //
        //     "origin: ground-centre of the footprint at CHART DATUM (z = 0 = lowest water)"
        //
        // …so its pivot is 5.2 m of drawn height BELOW the deck and half a piece-width BEHIND the lip.
        // Placing a face piece at the wall's lip the way a crate is placed on the deck would draw the
        // whole quay several metres up-screen of the wall it belongs to — and it would look fine, which
        // is the problem.
        //
        // So exactly ONE line is anchored: the DRAWN DECK LIP, onto the wall's real lip. Everything else
        // in the sprite falls where the rig's own projection puts it. This is the same rule the retired
        // tile kit's breakwater used ("TOP-CENTRE pivot: the block hangs BELOW its crest point"),
        // generalised to a pivot that is neither top nor centre.

        /// <summary>
        /// The offset from a face piece's PIVOT to its drawn deck lip, in world units — add it to the
        /// pivot to get the lip, subtract it from the lip to get the pivot
        /// (<see cref="PivotForLip"/>).
        ///
        /// <para><b>Two terms, projected by DIFFERENT scales</b>, which is the whole of the arithmetic
        /// and the one thing that is easy to get wrong (<c>CliffWallGeometry</c>'s first rule, in a
        /// different kit):</para>
        /// <list type="bullet">
        /// <item><description><b>Height</b> — the deck stands <see cref="BakedDeckZMetres"/> m above the
        /// pivot's chart datum, and a metre of HEIGHT draws
        /// <see cref="SpriteLightMath.HeightScale"/> ≈ 0.766 up-screen. Always up, whichever way the
        /// piece faces.</description></item>
        /// <item><description><b>Plan</b> — the lip is half a piece-width toward the water from the
        /// footprint centre, and a metre of GROUND travel draws foreshortened by
        /// <see cref="SpriteLightMath.GroundDepthScale"/> ≈ 0.643 in Y and not at all in X. So the term
        /// bites on a face that looks at the camera (the north wall, whose water is SOUTH) and vanishes
        /// from the Y of one that looks across it (the apron, whose water is EAST) — where it becomes an
        /// X offset instead.</description></item>
        /// </list>
        /// </summary>
        /// <param name="seaward">Unit plan direction from the deck out over the water — the way the
        /// piece's working face looks.</param>
        public static Vector2 LipRiseFromPivot(Vector2 seaward)
        {
            Vector2 s = seaward.sqrMagnitude < 1e-12f ? Vector2.down : seaward.normalized;
            float half = FaceCourseWidthMetres * 0.5f;
            return new Vector2(
                half * s.x,
                BakedDeckZMetres * SpriteLightMath.HeightScale + half * s.y * SpriteLightMath.GroundDepthScale);
        }

        /// <summary>Where a piece's pivot goes so that its drawn deck lip lands on
        /// <paramref name="lip"/>. The only placement rule this file publishes, and the reason #471 kept
        /// the class alive after it stopped drawing anything.</summary>
        public static Vector2 PivotForLip(Vector2 lip, Vector2 seaward) => lip - LipRiseFromPivot(seaward);

        /// <summary>
        /// ⭐ How far UP-SCREEN the piece's own picture reaches above its pivot — the top corner of the
        /// FOOTPRINT at deck height, which is the far side of a piece that looks at the camera and the
        /// far END of one that looks across it.
        ///
        /// <para><b>This is why the lip is not a sorting line.</b> A course is a face plus its own deck
        /// top: 5 m of crib drawn foreshortened, so on the mooring face the picture stands
        /// <c>FaceCourseWidthMetres × GroundDepthScale</c> = 3.21 units above the lip, and on the apron
        /// (whose length runs up-screen instead) <c>FaceCourseRunMetres/2 × GroundDepthScale</c> = 3.09.
        /// Anything Y-sorted standing in that band sorted BEHIND the piece and was drawn over by it —
        /// the owner's "you can disappear under them", 2026-09-04.</para>
        ///
        /// <para>⚠️ It is <b>not</b> a bound on the ink, and must never be used as one. Measured against
        /// the committed sheet the two disagree in both directions: facing 4 draws to 5.5625 against
        /// this 5.590 (the deck's corner is chamfered in), facing 6 draws to 7.1875 against this 7.069
        /// (the crib's header logs stand proud of the footprint by 0.10 m plus their own radius). A
        /// shipped piece is not a rectangle. Sorting is settled by leaving the decor band altogether —
        /// <c>NineMileCreekDressing.FaceSortingOrder</c> — and this number is kept because it is the
        /// measurement that says why.</para>
        /// </summary>
        /// <param name="seaward">Unit plan direction from the deck out over the water.</param>
        public static float DrawnTopRiseFromPivot(Vector2 seaward)
        {
            Vector2 s = seaward.sqrMagnitude < 1e-12f ? Vector2.down : seaward.normalized;
            Vector2 along = new Vector2(-s.y, s.x);              // the run, across the working face
            float planRise = Mathf.Abs(s.y) * FaceCourseWidthMetres * 0.5f +
                             Mathf.Abs(along.y) * FaceCourseRunMetres * 0.5f;
            return BakedDeckZMetres * SpriteLightMath.HeightScale +
                   planRise * SpriteLightMath.GroundDepthScale;
        }

        /// <summary>
        /// How far the drawn face reaches below its own lip, in world units: <b>≈ 5.06 units for a 6.6 m
        /// structure</b> — <c>(deckZ − mudZ)</c> of HEIGHT, at 0.766 a metre.
        ///
        /// <para>Pure height, with no plan term: on a face that looks at the camera the lip and the toe
        /// stand on the SAME plan line (the water side of the footprint), so the only thing between them
        /// is the wall. This is what the quay covers on screen, and therefore what a boat lying at the
        /// berth line 2 m off the wall would be drawn behind if the piece sorted by its pivot instead of
        /// by its lip — see <c>NineMileCreekDressing.PlaceFace</c>.</para>
        /// </summary>
        public static float DrawnFaceDropMetres =>
            (BakedDeckZMetres - BakedRigMudZ) * SpriteLightMath.HeightScale;

        /// <summary>
        /// How many pieces it takes to cover <paramref name="runMetres"/> of wall, and at what pitch.
        ///
        /// <para><b>The run is spread to COVER, never to leave a gap.</b> The count is the CEILING of the
        /// division and the pitch is the run over the count, so pieces butt or overlap slightly and the
        /// wall is continuous end to end. No wall here is a whole number of pieces long, so a FLOOR would
        /// have left the far end of every one of them undrawn — up to a piece-length of open bay showing
        /// through the quay. An overlap of under a metre between two log cribs is invisible; a hole is
        /// not. The pack's own snap sockets (±<c>L/2</c> at deck height) are what make butting legal in
        /// the first place, and an overlap is only ever butting with the seam pushed inside a piece.</para>
        /// </summary>
        public static void CoverRun(float runMetres, out int count, out float pitchMetres)
        {
            count = Mathf.Max(1, Mathf.CeilToInt(runMetres / FaceCourseRunMetres));
            pitchMetres = runMetres / count;
        }

        // =============================================================================================
        //  5. THE REPORT
        // =============================================================================================

        /// <summary>
        /// The bake this region needs, as the rig's own option names — now also the bake it HAS. Kept so
        /// the numbers stay quotable in a PR and so a future re-bake at a different coast has the order
        /// to hand rather than a remembered paragraph.
        /// </summary>
        public static string RequiredBakeOptions() =>
            $"{{ tideRange: {RequiredTideRangeMetres:0.##}, clearance: {RequiredClearanceMetres:0.##} }}" +
            $"  // deckZ resolves to {RequiredDeckZMetres:0.##} m";

        /// <summary>The finding, in one paragraph, from the numbers above — logged by the builder so the
        /// owner sees the state of the quay in the console after a scene rebuild rather than only in a
        /// merged PR.</summary>
        public static string FaceReport() =>
            $"[NineMileCreekQuayFace] THE QUAY FACE IS DRAWN, in ONE course of '{FaceCourseKey}'. " +
            $"This wharf needs a {StructuralFaceMetres:0.0} m face with its deck " +
            $"{RequiredDeckZMetres:0.0} m above lowest water; the committed ISO pack bakes every " +
            $"structural preset at {BakedDeckZMetres:0.0} m, so the shortfall #471 measured at 2.40 m is " +
            $"now {ShortfallMetres:0.0#} m — {TideShortfallMetres:0.0#} m of tide and " +
            $"{ClearanceShortfallMetres:0.0#} m of freeboard, both closed by the re-bake at " +
            $"tideRange {BakedRigTideRange:0.##} / clearance {BakedRigClearance:0.##}. " +
            $"{CoursesNeeded} course, so the pack's bollards, rings and ladder " +
            $"land AT the deck instead of halfway up the wall, and the growth bands are re-pinned to " +
            $"this coast's frame: barnacle {BarnacleBand.x:0.00}–{BarnacleBand.y:0.00} m, rockweed " +
            $"{RockweedBand.x:0.00}–{RockweedBand.y:0.00} m above datum (they were 0.72–1.44 and " +
            $"0.11–0.72, and no vertical stack could ever have fixed that). For the record, a stack was " +
            $"never the answer even though the arithmetic closed: {StackableCourseKey} is the pack's " +
            "only flat furniture-free course, two of them reach 5.20 m exactly, and both would have been " +
            "steel sheet pile — the one material this wharf is ruled not to be built of.";
    }
}
#endif
