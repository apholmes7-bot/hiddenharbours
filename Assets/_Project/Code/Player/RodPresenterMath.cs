using System;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>Which diegetic elements the rod-fight presenter draws this beat — a pure mapping from
    /// the published phase so the whole show/hide choreography is EditMode-testable headless.</summary>
    [Flags]
    public enum RodElements
    {
        None        = 0,
        Rod         = 1 << 0,   // the rod overlay pinned to the angler's hands
        Line        = 1 << 1,   // the catenary from the rod tip to the far end
        Bobber      = 1 << 2,   // fly/float/nibble at the cast-path far end
        FishShadow  = 1 << 3,   // the dark shape circling the entry point (FightDeep)
        FishSurface = 1 << 4,   // the visible dart/thrash fish (FightSurface)
        HeldFish    = 1 << 5,   // the landed fish in the fisher's hands
        SinkRipples = 1 << 6,   // the count-the-fall rings at the entry point (Sinking)
    }

    /// <summary>Which surface-breaking beat a published phase edge is (<see cref="RodPresenterMath.SplashFor"/>);
    /// each owner decides where its splash lands.</summary>
    public enum RodSplashEdge
    {
        None = 0,
        Touchdown,     // Cast → Waiting: the float lands at the cast aim
        PulledUnder,   // Bite → a hook-set phase, on the cast path: the float goes under at the old aim
        Breaks,        // FightDeep → FightSurface: she breaks the surface at her offset
        Result,        // → Landed or Snapped: at the line's last far end
    }

    /// <summary>
    /// The PURE maths of the rod-fight presenter (<see cref="RodFightPresenter"/>): phase → drawn
    /// elements, pose → rod sheet, the shadow's circling choreography, the cast arc, and the taut-line
    /// read the line samples with. Split out (the <see cref="PlayerFishingAnimMath"/> pattern) so the
    /// state→visual mapping is EditMode-testable without a scene. The line/bobber/ripple SHAPES live in
    /// the Art lane's <c>RodLineMath</c> — this class only decides WHAT shows and WHERE it anchors.
    /// </summary>
    public static class RodPresenterMath
    {
        /// <summary>
        /// The splash a published edge throws, or <see cref="RodSplashEdge.None"/>. Only a phase CHANGE
        /// splashes, and the first matching rule wins, in this order: touchdown, pulled under, she
        /// breaks, the result. Pure, so a villager's fight splashes on exactly the player's beats.
        /// </summary>
        public static RodSplashEdge SplashFor(in FishingState prev, in FishingState next)
        {
            if (next.Phase == prev.Phase) return RodSplashEdge.None;
            bool prevCastPath = prev.CastAimX * prev.CastAimX + prev.CastAimY * prev.CastAimY > 1e-4f;
            if (prev.Phase == FishingPhase.Cast && next.Phase == FishingPhase.Waiting)
                return RodSplashEdge.Touchdown;
            if (prev.Phase == FishingPhase.Bite && IsHookSet(next.Phase) && prevCastPath)
                return RodSplashEdge.PulledUnder;
            if (prev.Phase == FishingPhase.FightDeep && next.Phase == FishingPhase.FightSurface)
                return RodSplashEdge.Breaks;
            if (next.Phase == FishingPhase.Landed || next.Phase == FishingPhase.Snapped)
                return RodSplashEdge.Result;
            return RodSplashEdge.None;
        }

        /// <summary>Is this a hook-set phase (the fish is on: a fight phase a bite can open into).</summary>
        public static bool IsHookSet(FishingPhase p)
            => p == FishingPhase.Fighting || p == FishingPhase.FightDeep || p == FishingPhase.FightSurface;

        /// <summary>
        /// The elements drawn for a published phase. <paramref name="castPath"/> = the interaction has a
        /// cast-path far end (a non-neutral <c>CastAim</c> — the bobber path); a weighted/depth drop and
        /// the v2 fight publish it neutral. The mapping is deliberately conservative: results (Snapped /
        /// NoBite) and the hand-gather (Tending) draw NOTHING — the walk skin and the toast own those
        /// beats, and a rod nobody is holding must never float.
        /// </summary>
        public static RodElements ElementsFor(FishingPhase phase, bool castPath)
        {
            switch (phase)
            {
                case FishingPhase.WindBack:
                    return RodElements.Rod;
                case FishingPhase.Cast:
                    return RodElements.Rod | RodElements.Line | RodElements.Bobber;
                case FishingPhase.Waiting:
                case FishingPhase.Bite:
                case FishingPhase.BiteNibble:   // the tease shows on the same rig the bite does (§10.2)
                    return castPath
                        ? RodElements.Rod | RodElements.Line | RodElements.Bobber
                        : RodElements.Rod | RodElements.Line;
                case FishingPhase.Sinking:
                    return RodElements.Rod | RodElements.Line | RodElements.SinkRipples;
                case FishingPhase.Fighting:   // the legacy single-phase fight — she fights at the bobber
                    return castPath
                        ? RodElements.Rod | RodElements.Line | RodElements.Bobber
                        : RodElements.Rod | RodElements.Line;
                case FishingPhase.FightDeep:
                    return RodElements.Rod | RodElements.Line | RodElements.FishShadow;
                case FishingPhase.FightSurface:
                    return RodElements.Rod | RodElements.Line | RodElements.FishSurface;
                case FishingPhase.Landed:
                    return RodElements.Rod | RodElements.HeldFish;
                default:
                    return RodElements.None;   // Idle, Snapped, NoBite, Tending
            }
        }

        /// <summary>
        /// <b>The rod's states, in the one order everything uses.</b> These are the RIG's own state
        /// names (<c>RodIso</c>'s tool anims, then its <c>REST</c> states), and this array is the only
        /// place the order is written down: <see cref="RodSheetFor"/> resolves through it by NAME, and
        /// the importer fills the presenter's array from it. It used to be spelled out in four places
        /// — the rig, the baker's <c>RodToolAnims</c>, the importer's state order, and a run of bare
        /// ordinals in <c>RodSheetFor</c> — with nothing checking that the four agreed. That is the
        /// shape of the bug this fixes: a swap keyed on a number rather than on the rod it is asking
        /// for will happily hand you a different rod and say nothing.
        ///
        /// <para>The last three are the rig's rests — the rod set down and the two stows. They draw no
        /// gameplay yet, but they are the SAME rod as the held states and they are wired here so that
        /// <see cref="SameRod"/> covers them: the one-rod rule is only worth anything if the states
        /// nobody is looking at are held to it too.</para>
        /// </summary>
        public static readonly string[] RodStates =
        {
            "hold", "bite", "strike", "reel", "land", "castBack", "castRelease",
            "ground", "stowV", "stowH",
        };

        public static int RodStateCount => RodStates.Length;

        /// <summary>How many <see cref="FishingPose"/> values there are — the width of a pose-indexed
        /// lookup. Stated rather than reflected: this is read at component construction.</summary>
        public const int PoseCount = (int)FishingPose.CastRelease + 1;

        /// <summary>The index of a rig state name in <see cref="RodStates"/>, or −1. Ordinal.</summary>
        public static int IndexOfState(string state)
        {
            if (string.IsNullOrEmpty(state)) return -1;
            for (int i = 0; i < RodStates.Length; i++)
                if (string.Equals(RodStates[i], state, StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>Which RIG STATE a fisher pose is holding the rod in. <see cref="FishingPose.None"/>
        /// maps to hold: the line can be out while the body walks (the hold pose yielded), and the rod
        /// stays in hand.</summary>
        public static string RodStateFor(FishingPose pose)
        {
            switch (pose)
            {
                case FishingPose.Bite: return "bite";
                case FishingPose.Strike: return "strike";
                case FishingPose.Reel: return "reel";
                case FishingPose.Land: return "land";
                case FishingPose.CastBack: return "castBack";
                case FishingPose.CastRelease: return "castRelease";
                default: return "hold";   // None / Hold → the hold sheet
            }
        }

        /// <summary>Which rod sheet a fisher pose pairs with — the index of <see cref="RodStateFor"/>'s
        /// rig state. Falls back to hold (0) rather than throwing, so a state the rig stops declaring
        /// leaves the rod in the fisher's hand instead of leaving her empty-handed.</summary>
        public static int RodSheetFor(FishingPose pose)
        {
            int i = IndexOfState(RodStateFor(pose));
            return i >= 0 ? i : 0;
        }

        /// <summary>
        /// <b>Is this the same rod?</b> The owner's law for the whole fishing kit is that the rod never
        /// teleports, resizes or re-points across a transition, and a presenter that swaps sheets is
        /// exactly where that law gets broken — silently, because two sheets of a rod look like a rod
        /// either way. So before one state's sheet stands in for another's, the two must agree about
        /// what the rod IS: the same cell, the same pivot inside it, the same tier, the same blank
        /// length. Anything else is a different rod wearing this one's name.
        ///
        /// <para>Blank length is compared in millimetres of slack — the anchors carry it as a rounded
        /// decimal, and an exact float compare would fail on the rounding rather than on the rod.</para>
        /// </summary>
        public static bool SameRod(RodStateVisual a, RodStateVisual b, out string why)
        {
            why = null;
            if (a == null || b == null) return true;   // an unwired state substitutes nothing
            if (a.CellW != b.CellW || a.CellH != b.CellH)
                why = $"cell {a.CellW}x{a.CellH} vs {b.CellW}x{b.CellH}";
            else if (Mathf.Abs(a.PivotX - b.PivotX) > 0.01f || Mathf.Abs(a.PivotY - b.PivotY) > 0.01f)
                why = $"pivot ({a.PivotX},{a.PivotY}) vs ({b.PivotX},{b.PivotY})";
            else if (!string.Equals(a.Tier, b.Tier, StringComparison.Ordinal))
                why = $"tier '{a.Tier}' vs '{b.Tier}'";
            else if (Mathf.Abs(a.BlankLenM - b.BlankLenM) > 0.001f)
                why = $"blank {a.BlankLenM} m vs {b.BlankLenM} m";
            return why == null;
        }

        /// <summary>Compass heading (deg, 0 = North=+Y, CW toward +X — the IsoFacing convention) of a
        /// world-space delta. (0,0)-safe (returns 0).</summary>
        public static float HeadingDegrees(float dx, float dy)
            => (dx == 0f && dy == 0f) ? 0f : Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;

        /// <summary>A sideways part of her travel smaller than this share of it counts as none: she is
        /// coming straight down the line and keeps the side she last showed. A float tolerance, not a feel.</summary>
        private const float FlankLateralTolerance01 = 1e-3f;

        /// <summary>
        /// <b>SHE SHOWS HER FLANK ON THE LINE</b> (coordinator's ask 2026-11-26: the fish read as a thin
        /// sliver at the line end). The heading her surfaced row is picked from. A fish reeled straight in
        /// travels along the line, which picks the end-on rows; within <paramref name="flankWithinDegrees"/>
        /// of the line (toward the fisher or away) the row is taken ACROSS the line instead, on the side her
        /// travel drifts toward, so she turns side-on with the line. Further off the line her own travel
        /// picks the row, as before.
        ///
        /// <para><paramref name="fromFisher"/> is her offset from the fisher (the line's axis).
        /// <paramref name="side"/> is +1 for the axis turned a quarter clockwise, −1 for the other; it is
        /// handed in as the side she last showed (0 = none yet) and handed back as the side shown. With no
        /// sideways drift she keeps that side; with none held she shows the side whose heading points
        /// east (+x), or north when the line runs east-west. 0 or less turns the rule off. Pure.</para>
        /// </summary>
        public static Vector2 FlankRowHeading(Vector2 travel, Vector2 fromFisher, float flankWithinDegrees,
                                              ref int side)
        {
            float t = travel.magnitude;
            float a = fromFisher.magnitude;
            if (flankWithinDegrees <= 0f || t < 1e-6f || a < 1e-6f) return travel;

            Vector2 axis = fromFisher / a;
            Vector2 across = new Vector2(axis.y, -axis.x);                      // the axis, a quarter clockwise
            float lateral = Vector2.Dot(travel, across) / t;
            if (Mathf.Abs(lateral) > FlankLateralTolerance01) side = lateral > 0f ? 1 : -1;

            float along = Mathf.Clamp(Mathf.Abs(Vector2.Dot(travel, axis)) / t, 0f, 1f);
            if (Mathf.Acos(along) * Mathf.Rad2Deg > flankWithinDegrees) return travel;

            if (side == 0)
                side = Mathf.Abs(across.x) > FlankLateralTolerance01
                    ? (across.x > 0f ? 1 : -1)
                    : (across.y >= 0f ? 1 : -1);
            return across * (side * t);
        }

        /// <summary>The flying bobber's vertical lift over the cast: a simple lob peaking mid-flight
        /// (<c>4t(1−t)</c>), zero at release and touchdown. Pure.</summary>
        public static float ArcLift(float progress01, float arcHeightM)
        {
            float t = Mathf.Clamp01(progress01);
            return Mathf.Max(0f, arcHeightM) * 4f * t * (1f - t);
        }

        /// <summary>The deep fish's shadow offset from the line's entry point: a flattened circle (the
        /// ¾ iso view squashes y). Pure.</summary>
        public static Vector2 ShadowOffset(float thetaRad, float radiusM, float ySquash01)
            => new Vector2(Mathf.Cos(thetaRad) * radiusM,
                           Mathf.Sin(thetaRad) * radiusM * Mathf.Clamp01(ySquash01));

        /// <summary>The heading (deg, 0=N, CW) the circling shadow SWIMS — the tangent of
        /// <see cref="ShadowOffset"/>'s ellipse at <paramref name="thetaRad"/>. Pure.</summary>
        public static float ShadowHeadingDegrees(float thetaRad, float ySquash01)
            => HeadingDegrees(-Mathf.Sin(thetaRad), Mathf.Cos(thetaRad) * Mathf.Clamp01(ySquash01));

        /// <summary>How loaded the rod is in a fight, 0..1: the larger of its bend and the line's tension.
        /// One rule for the player's lean and line hum and a villager's line, so neither copies it.</summary>
        public static float FightLoad01(float rodBend01, float tension01) =>
            Mathf.Clamp01(Mathf.Max(rodBend01, tension01));

        /// <summary>True when the surfaced fish reads as a DART (drawn with the dart sheet, facing her
        /// travel); below the threshold she station-holds and THRASHES. Pure.</summary>
        public static bool IsDarting(float fishSpeedMps, float dartSpeedThresholdMps)
            => fishSpeedMps > Mathf.Max(0f, dartSpeedThresholdMps);

        /// <summary>
        /// The line's taut read 0..1 for a published state (feeds <c>RodLineMath.SampleLine</c>): slack
        /// when the slack window is open (the PULL-now / hit-bottom tell — the sag IS the tell), a
        /// gentle rest sag while waiting, running-taut while the rig sinks or the line flies, and in a
        /// fight the max of the rod-bend and tension reads over a floor (a fight is never drawn slack
        /// unless the tell says so). Pure, clamped.
        /// </summary>
        public static float TautFor(FishingPhase phase, bool slackWindowOpen, float rodBend01,
                                    float tension01, float restTaut, float sinkTaut, float fightTautFloor)
        {
            if (slackWindowOpen) return 0f;
            switch (phase)
            {
                case FishingPhase.Cast:
                    return 0.85f;
                case FishingPhase.Sinking:
                    return Mathf.Clamp01(sinkTaut);
                case FishingPhase.Fighting:
                case FishingPhase.FightDeep:
                case FishingPhase.FightSurface:
                    return Mathf.Clamp01(Mathf.Max(fightTautFloor, Mathf.Max(rodBend01, tension01)));
                default:
                    return Mathf.Clamp01(restTaut);   // Waiting / Bite — the resting line
            }
        }

        /// <summary>The current frame of a fixed-rate flipbook (floor(clock/spf) % frames). Pure,
        /// degenerate-safe.</summary>
        public static int FlipFrame(float clockSeconds, float secondsPerFrame, int frameCount)
        {
            if (frameCount <= 0) return 0;
            float spf = Mathf.Max(1e-3f, secondsPerFrame);
            return Mathf.FloorToInt(Mathf.Max(0f, clockSeconds) / spf) % frameCount;
        }

        /// <summary>The play-once frame of a flipbook; returns <paramref name="frameCount"/> (one past
        /// the end) once finished so the caller can hide. Pure, degenerate-safe.</summary>
        public static int OnceFlipFrame(float clockSeconds, float secondsPerFrame, int frameCount)
        {
            if (frameCount <= 0) return 0;
            float spf = Mathf.Max(1e-3f, secondsPerFrame);
            return Mathf.Min(Mathf.FloorToInt(Mathf.Max(0f, clockSeconds) / spf), frameCount);
        }

        /// <summary>
        /// Which churn period of a thrashing fish <paramref name="surfaceSeconds"/> falls in: 0 for the
        /// first period after she comes up, then 1, 2, ... A splash is thrown each time it steps up, so
        /// the first comes one whole period after she surfaces. 0 always (no churn) for a period of 0 or
        /// less, a NaN clock or a negative one. Pure: the same seconds give the same beat (rule 5).
        /// </summary>
        public static long ChurnBeat(double surfaceSeconds, float periodSeconds)
        {
            if (!(periodSeconds > 0f) || !(surfaceSeconds > 0d)) return 0;
            return (long)System.Math.Floor(surfaceSeconds / periodSeconds);
        }

        /// <summary>
        /// Is <paramref name="point"/> inside the fisher's drawn figure: within <paramref name="halfWidthM"/>
        /// either side of his feet at <paramref name="feet"/> and from his feet up to
        /// <paramref name="heightM"/> above them (the three-quarter view draws height as screen-up). False for
        /// a zero or negative size (off) or a NaN point.
        /// </summary>
        public static bool InFisherFigure(Vector2 point, Vector2 feet, float halfWidthM, float heightM)
        {
            if (!(halfWidthM > 0f) || !(heightM > 0f)) return false;
            float dx = point.x - feet.x, dy = point.y - feet.y;
            return Mathf.Abs(dx) <= halfWidthM && dy >= 0f && dy <= heightM;
        }

        /// <summary>
        /// Where the line leaves the rod: the tip of whichever rod is DRAWN this frame. The mesh rod
        /// (ADR 0053) when it draws, else the sprite rod's tip pin. One rod is drawn at a time, so the line
        /// can never hang off the one that is hidden.
        /// </summary>
        public static Vector2 LineAnchor(bool meshRodDrawn, Vector2 meshTip, Vector2 spriteTip) =>
            meshRodDrawn ? meshTip : spriteTip;

        /// <summary>Safe [dir·framesPerDir + frame] index into a d/f sheet, or −1 when out of range /
        /// unwired — the caller hides the element instead of throwing (null-safe greybox rule).</summary>
        public static int SheetIndex(int row, int frame, int framesPerDir, int totalLength)
        {
            if (framesPerDir <= 0 || row < 0 || frame < 0 || frame >= framesPerDir) return -1;
            int idx = row * framesPerDir + frame;
            return idx >= 0 && idx < totalLength ? idx : -1;
        }
    }
}
