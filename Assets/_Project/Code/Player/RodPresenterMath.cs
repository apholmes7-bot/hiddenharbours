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

        /// <summary>Which rod sheet a fisher pose pairs with — index into the presenter's rod-state
        /// array, which the builder fills in this exact order: hold(0), bite(1), strike(2), reel(3),
        /// land(4), castBack(5), castRelease(6). <see cref="FishingPose.None"/> maps to HOLD: the line
        /// can be out while the body walks (the hold pose yielded), and the rod stays in hand.</summary>
        public const int RodStateCount = 7;

        public static int RodSheetFor(FishingPose pose)
        {
            switch (pose)
            {
                case FishingPose.Bite: return 1;
                case FishingPose.Strike: return 2;
                case FishingPose.Reel: return 3;
                case FishingPose.Land: return 4;
                case FishingPose.CastBack: return 5;
                case FishingPose.CastRelease: return 6;
                default: return 0;   // None / Hold → the hold sheet
            }
        }

        /// <summary>Compass heading (deg, 0 = North=+Y, CW toward +X — the IsoFacing convention) of a
        /// world-space delta. (0,0)-safe (returns 0).</summary>
        public static float HeadingDegrees(float dx, float dy)
            => (dx == 0f && dy == 0f) ? 0f : Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;

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
