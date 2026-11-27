using UnityEngine;

namespace HiddenHarbours.Environment
{
    /// <summary>What one seal is doing, finer than <see cref="Core.WildlifePose"/>: the host picks frames by it.</summary>
    public enum SealPhase : byte
    {
        /// <summary>Not here (the schedule has it away, or it dived off and has not come back).</summary>
        Away = 0,
        Resting = 1,
        Alert = 2,
        /// <summary>Humping off the rock into the water.</summary>
        SlidingIn = 3,
        /// <summary>A head up in the water.</summary>
        Swimming = 4,
        /// <summary>Going under; the ring shows.</summary>
        Diving = 5,
        /// <summary>Hauling back up out of the water onto its rock.</summary>
        SlidingOut = 6,
    }

    /// <summary>
    /// One seal's short memory: what it is doing and how it got scared. Lives only in the haul-out host
    /// while the region is loaded; never saved. On load every seal starts from the schedule
    /// (<see cref="SealMind.Begin"/>), so a loaded game shows exactly what a pure function of
    /// <c>(worldSeed, gameTime, tide)</c> says (rule 5); only the player's own approach since then adds to it.
    /// </summary>
    public struct SealState
    {
        public SealPhase Phase;

        /// <summary>Seconds in the current phase.</summary>
        public float PhaseSeconds;

        /// <summary>True after an intruder put it in the water: it stays off its rock until calm.</summary>
        public bool Flushed;

        /// <summary>Seconds with no intruder inside the alert distance (the return clock).</summary>
        public float CalmSeconds;

        /// <summary>Seconds of creeping or stillness while watching (the settle clock).</summary>
        public float SettleSeconds;

        /// <summary>The nearest intruder's scaled distance at the last step (for the approach speed); negative when none was near.</summary>
        public float LastDistance;

        /// <summary>How many times it has surfaced since the host started: keys the hashed surface time.</summary>
        public int Surfacings;

        /// <summary>The length of the current surfacing (hashed when it comes up).</summary>
        public float SurfaceSeconds;
    }

    /// <summary>
    /// ⭐ <b>HOW A SEAL REACTS</b> — a pure step function over <see cref="SealState"/>: the schedule's
    /// place, the nearest intruder's distance (already scaled for the wind and for a person on foot) and
    /// the time step in; the next state out. No clock, no engine, no randomness beyond
    /// <see cref="SealHash"/> keyed by the seal and its surfacing count.
    ///
    /// <para><b>The photo puzzle.</b> Inside <see cref="SealTuning.AlertDistanceMetres"/> a resting seal
    /// lifts its head. Closing faster than <see cref="SealTuning.StartleApproachSpeed"/>, or coming inside
    /// <see cref="SealTuning.SlipDistanceMetres"/>, puts it in the water. Creeping (no faster than
    /// <see cref="SealTuning.CreepSpeed"/>) or holding still for <see cref="SealTuning.AlertCalmSeconds"/>
    /// lets a watching seal put its head down again, and a resting seal is not re-alerted by a creep. So
    /// the good picture is a slow approach, better from downwind (the distances shrink) and at low water
    /// (more seals, farther from the channel).</para>
    /// </summary>
    public static class SealMind
    {
        /// <summary>No intruder near: the distance a step reads as "none".</summary>
        public const float NoIntruder = float.PositiveInfinity;

        /// <summary>Salt for a surfacing's length.</summary>
        public const uint SaltSurface = 0x61u;

        /// <summary>The state a seal starts in when its host first looks: straight from the schedule.</summary>
        public static SealState Begin(in SealTuning k, SealPlace place, int seed, uint placeHash, uint spotHash)
        {
            var s = new SealState { LastDistance = -1f, CalmSeconds = 0f };
            s.Phase = place == SealPlace.Hauled ? SealPhase.Resting
                    : place == SealPlace.InWater ? SealPhase.Swimming
                    : SealPhase.Away;
            if (s.Phase == SealPhase.Swimming) s.SurfaceSeconds = SurfaceLength(k, seed, placeHash, spotHash, 0);
            return s;
        }

        /// <summary>The pose a camera reads for a phase.</summary>
        public static Core.WildlifePose PoseOf(SealPhase phase)
        {
            switch (phase)
            {
                case SealPhase.Resting: return Core.WildlifePose.Resting;
                case SealPhase.Alert: return Core.WildlifePose.Alert;
                case SealPhase.SlidingIn:
                case SealPhase.SlidingOut: return Core.WildlifePose.Sliding;
                case SealPhase.Swimming: return Core.WildlifePose.Swimming;
                default: return Core.WildlifePose.Diving;
            }
        }

        /// <summary>True for the phases that draw anything.</summary>
        public static bool IsDrawn(SealPhase phase) => phase != SealPhase.Away;

        /// <summary>
        /// How much bigger (or smaller) an intruder's scare distances are for the wind: x the upwind scale
        /// when the wind blows from it to the seal, x the downwind scale when the seal is upwind of it, faded
        /// towards 1 in a light wind. <paramref name="windDownwind"/> is <c>EnvironmentSample.WindVector</c>
        /// (it points the way the wind blows, length m/s).
        /// </summary>
        public static float WindScale(in SealTuning k, Vector2 intruderToSeal, Vector2 windDownwind)
        {
            float speed = windDownwind.magnitude;
            float dist = intruderToSeal.magnitude;
            if (!(speed > 1e-4f) || !(dist > 1e-4f)) return 1f;
            float strength = Mathf.Clamp01(speed / Mathf.Max(k.FullScentWindSpeed, 1e-4f));
            float along = Vector2.Dot(intruderToSeal / dist, windDownwind / speed); // +1: the wind carries you to it
            return along >= 0f
                ? Mathf.Lerp(1f, k.UpwindDistanceScale, along * strength)
                : Mathf.Lerp(1f, k.DownwindDistanceScale, -along * strength);
        }

        /// <summary>The surfacing length for a seal's n-th surfacing: hashed, between the species' bounds.</summary>
        public static float SurfaceLength(in SealTuning k, int seed, uint placeHash, uint spotHash, int surfacing)
        {
            double u = SealHash.Unit(seed, placeHash, spotHash, surfacing, SaltSurface);
            float lo = Mathf.Min(k.SurfaceSecondsMin, k.SurfaceSecondsMax);
            float hi = Mathf.Max(k.SurfaceSecondsMin, k.SurfaceSecondsMax);
            return lo + (float)u * (hi - lo);
        }

        /// <summary>
        /// One step. <paramref name="intruderDistance"/> is the nearest intruder's distance divided by its
        /// scale (so it compares straight against the species' distances), or <see cref="NoIntruder"/>.
        /// </summary>
        public static SealState Step(in SealTuning k, SealState s, SealPlace place, float intruderDistance, float dt,
                                     int seed, uint placeHash, uint spotHash)
        {
            if (!(dt > 0f)) return s;

            bool near = intruderDistance < k.AlertDistanceMetres;
            float approach = 0f;
            if (near && s.LastDistance >= 0f && !float.IsInfinity(s.LastDistance))
                approach = (s.LastDistance - intruderDistance) / dt;
            bool startled = near && (intruderDistance < k.SlipDistanceMetres || approach > k.StartleApproachSpeed);
            s.LastDistance = near ? intruderDistance : -1f;

            s.CalmSeconds = near ? 0f : s.CalmSeconds + dt;
            if (s.Flushed && s.CalmSeconds >= k.CalmReturnSeconds) s.Flushed = false;
            s.PhaseSeconds += dt;

            switch (s.Phase)
            {
                case SealPhase.Away:
                    if (place != SealPlace.Away) Enter(ref s, SealPhase.Swimming, k, seed, placeHash, spotHash);
                    break;

                case SealPhase.Resting:
                case SealPhase.Alert:
                    if (place != SealPlace.Hauled) { Enter(ref s, SealPhase.SlidingIn, k, seed, placeHash, spotHash); break; }
                    if (startled)
                    {
                        s.Flushed = true;
                        Enter(ref s, SealPhase.SlidingIn, k, seed, placeHash, spotHash);
                        break;
                    }
                    if (s.Phase == SealPhase.Resting)
                    {
                        // A creep inside the ring does not lift its head; anything quicker does.
                        if (near && approach > k.CreepSpeed) { Enter(ref s, SealPhase.Alert, k, seed, placeHash, spotHash); s.SettleSeconds = 0f; }
                    }
                    else
                    {
                        s.SettleSeconds = !near || approach <= k.CreepSpeed ? s.SettleSeconds + dt : 0f;
                        if (s.SettleSeconds >= k.AlertCalmSeconds) Enter(ref s, SealPhase.Resting, k, seed, placeHash, spotHash);
                    }
                    break;

                case SealPhase.SlidingIn:
                    if (s.PhaseSeconds >= k.SlideSeconds)
                        Enter(ref s, place == SealPlace.Away ? SealPhase.Diving : SealPhase.Swimming, k, seed, placeHash, spotHash);
                    break;

                case SealPhase.SlidingOut:
                    if (startled || place != SealPlace.Hauled)
                    {
                        if (startled) s.Flushed = true;
                        Enter(ref s, SealPhase.SlidingIn, k, seed, placeHash, spotHash);
                    }
                    else if (s.PhaseSeconds >= k.SlideSeconds) Enter(ref s, SealPhase.Resting, k, seed, placeHash, spotHash);
                    break;

                case SealPhase.Swimming:
                    if (place == SealPlace.Hauled && !s.Flushed && !near)
                        Enter(ref s, SealPhase.SlidingOut, k, seed, placeHash, spotHash);
                    else if (s.PhaseSeconds >= s.SurfaceSeconds || startled && intruderDistance < k.SlipDistanceMetres)
                        Enter(ref s, SealPhase.Diving, k, seed, placeHash, spotHash);
                    break;

                case SealPhase.Diving:
                    if (s.PhaseSeconds >= k.DiveSeconds)
                        Enter(ref s, place == SealPlace.Away ? SealPhase.Away : SealPhase.Swimming, k, seed, placeHash, spotHash);
                    break;
            }
            return s;
        }

        static void Enter(ref SealState s, SealPhase phase, in SealTuning k, int seed, uint placeHash, uint spotHash)
        {
            s.Phase = phase;
            s.PhaseSeconds = 0f;
            if (phase == SealPhase.Swimming)
            {
                s.Surfacings++;
                s.SurfaceSeconds = SurfaceLength(k, seed, placeHash, spotHash, s.Surfacings);
            }
            if (phase == SealPhase.Alert || phase == SealPhase.Resting) s.SettleSeconds = 0f;
        }
    }
}
