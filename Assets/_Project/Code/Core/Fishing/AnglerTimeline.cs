using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>How a villager's fishing session runs</b>: every beat length, the cast, and the fight's shape, as
    /// authored data (a World activity Def carries one). No defaults (rule 6). A session sits on a fixed slot
    /// of <see cref="SlotSeconds"/>, at least the longest session; rest pads the remainder.
    /// </summary>
    [Serializable]
    public struct AnglerScript
    {
        /// <summary>One session slot (s). Must be at least <see cref="LongestSessionSeconds"/>.</summary>
        public float SlotSeconds;

        /// <summary>The wind-back (scrubbed), the release and flight, the wait (picked per slot between
        /// min and max), the bite tell, and the strike that opens the fight (s).</summary>
        public float CastBackSeconds, CastSeconds, WaitMinSeconds, WaitMaxSeconds, BiteSeconds, StrikeSeconds;

        /// <summary>The fight unseen and at the surface (picked per fish between min and max), the landing
        /// and hold-up, and the empty retrieve when nothing bites (s).</summary>
        public float DeepMinSeconds, DeepMaxSeconds, SurfaceMinSeconds, SurfaceMaxSeconds;
        public float LandSeconds, EmptyRetrieveSeconds;

        /// <summary>How far he casts (m), at what bearing off his station's heading (deg, clockwise), and how
        /// far a slot may turn it either way (deg).</summary>
        public float CastReachM, CastBearingDegrees, CastBearingJitterDegrees;

        /// <summary>The fight's shape: how far she runs across the line (m), the band the line's tension
        /// moves in (0..1), and the rate of her runs (Hz).</summary>
        public float RunSwingM, TensionMin01, TensionMax01, FightWaveHz;

        /// <summary>The longest a session can run, catch or none: the slot must hold it.</summary>
        public float LongestSessionSeconds => CastBackSeconds + CastSeconds + WaitMaxSeconds
            + Mathf.Max(BiteSeconds + DeepMaxSeconds + SurfaceMaxSeconds + LandSeconds, EmptyRetrieveSeconds);

        /// <summary>True when the script can be played: every length non-negative, each min at most its max,
        /// the tension band inside 0..1, and a slot that holds the longest session.</summary>
        public bool IsValid(out string why)
        {
            why = null;
            if (CastBackSeconds < 0f || CastSeconds < 0f || WaitMinSeconds < 0f || BiteSeconds < 0f
                || StrikeSeconds < 0f || DeepMinSeconds < 0f || SurfaceMinSeconds < 0f || LandSeconds < 0f
                || EmptyRetrieveSeconds < 0f || CastReachM < 0f || RunSwingM < 0f || FightWaveHz < 0f)
                why = "a length, reach, swing or rate is negative";
            else if (WaitMaxSeconds < WaitMinSeconds || DeepMaxSeconds < DeepMinSeconds
                     || SurfaceMaxSeconds < SurfaceMinSeconds)
                why = "a max is below its min";
            else if (TensionMin01 < 0f || TensionMax01 > 1f || TensionMax01 < TensionMin01)
                why = "the tension band is not inside 0..1";
            else if (!(SlotSeconds > 0f) || SlotSeconds < LongestSessionSeconds)
                why = $"the slot ({SlotSeconds} s) does not hold the longest session ({LongestSessionSeconds} s)";
            return why == null;
        }
    }

    /// <summary>Where a villager angler is in his session at one instant: the phase, the body's beat, and the
    /// full <see cref="FishingState"/> the shared rod-fight pieces draw.</summary>
    public struct AnglerBeat
    {
        public long Slot;
        /// <summary>The slot's start on the figure's clock.</summary>
        public double SlotStartSeconds;

        /// <summary>The phase (Idle while he rests out the slot), when it began and the seconds into it.</summary>
        public FishingPhase Phase;
        public double PhaseStartSeconds;
        public float SecondsInPhase;

        /// <summary>The body clip's state key (<see cref="FishingBeatMath"/>), null when no beat is played;
        /// the beat's start; and its scrub (NaN = the clip's own rate). The figure beat stand's answer.</summary>
        public string StateKey;
        public double BeatStartSeconds;
        public float Scrub01;

        public FishingState State;

        /// <summary>True while a session is under way (any phase but Idle).</summary>
        public bool InSession => Phase != FishingPhase.Idle;
    }

    /// <summary>
    /// <b>A villager's fishing session as a pure function of the clock</b> (ADR 0053 §11). Equal inputs give
    /// the equal beat: <c>(script, catch, worldSeed, keyHash, heading, now)</c>, where <c>now</c> is the
    /// clock the figure poses on (<see cref="IGameClock.FlowSeconds"/>) and the key hash is
    /// <see cref="FigureKeyHash.Of"/> of the NPC id. Sessions sit on a fixed slot grid offset by his key, so
    /// the slot index is O(1) at any game age: <c>floor((now - keyPhase) / SlotSeconds)</c>. Each slot's
    /// wait and cast bearing are a hash of <c>(worldSeed, keyHash, slot)</c>, and the fight's length and runs
    /// fold in the fish exactly as <see cref="FishFightSeed"/> does. Never <c>Random</c>, never
    /// <c>string.GetHashCode</c>, nothing saved: a hold or a load can shift the grid by the held span, which
    /// is presentation only.
    ///
    /// <para>The timeline never asks for the catch: the stand reads <see cref="IShoreCatchFacts"/> once per
    /// slot and passes it in. Whether a slot may begin at all (he was at the station at its start and the
    /// routine keeps him there to its end) is <see cref="SlotMayBegin"/>, also pure.</para>
    ///
    /// FLAG lead-architect: new Core contract.
    /// </summary>
    public static class AnglerTimeline
    {
        // Hashing constants (MurmurHash3's finalizer and the 32-bit golden-ratio stream step), not tunables.
        private const uint MixA = 0x85EBCA6Bu, MixB = 0xC2B2AE35u, StreamStep = 0x9E3779B9u;
        private const float UnitScale = 1f / 16777216f;   // 24 hash bits to [0, 1)
        // The second wave of her runs is the first's rate times the golden ratio, so the two never fall
        // back into step: a shape constant, not a tunable.
        private const float SecondWaveRatio = 1.6180339887f;
        private const float TwoPi = 2f * Mathf.PI;

        private const uint StreamKeyPhase = 0, StreamWait = 1, StreamBearing = 2;
        private const uint StreamDeep = 3, StreamSurface = 4, StreamWaveA = 5, StreamWaveB = 6, StreamSide = 7;

        /// <summary>His key's offset of the slot grid (s, in [0, SlotSeconds)), so two anglers side by side do
        /// not cast in step.</summary>
        public static double KeyPhaseSeconds(in AnglerScript script, int worldSeed, uint keyHash)
            => U01(Mix((uint)worldSeed ^ keyHash), StreamKeyPhase) * (double)script.SlotSeconds;

        /// <summary>The slot <paramref name="now"/> falls in. O(1).</summary>
        public static long SlotIndex(in AnglerScript script, int worldSeed, uint keyHash, double now)
        {
            if (!(script.SlotSeconds > 0f)) return 0;
            return (long)Math.Floor((now - KeyPhaseSeconds(in script, worldSeed, keyHash)) / script.SlotSeconds);
        }

        /// <summary>When slot <paramref name="slot"/> begins, on the same clock.</summary>
        public static double SlotStartSeconds(in AnglerScript script, int worldSeed, uint keyHash, long slot)
            => KeyPhaseSeconds(in script, worldSeed, keyHash) + slot * (double)script.SlotSeconds;

        /// <summary>
        /// May slot <paramref name="slot"/> be played: it begins no earlier than he arrived at the station
        /// and ends no later than the routine takes him away (both on the same clock). Arriving mid-slot he
        /// stands and watches until the next boundary; he never walks off mid-fight.
        /// </summary>
        public static bool SlotMayBegin(in AnglerScript script, int worldSeed, uint keyHash, long slot,
                                        double arrivalSeconds, double departureSeconds)
        {
            double start = SlotStartSeconds(in script, worldSeed, keyHash, slot);
            return start >= arrivalSeconds && start + script.SlotSeconds <= departureSeconds;
        }

        /// <summary>
        /// Where he is at <paramref name="now"/>. <paramref name="hasCatch"/> false (or a catch with no fish
        /// id) plays cast, wait, empty retrieve, rest. <paramref name="stationHeadingDegrees"/> is the
        /// compass heading he faces at his station (0 = North, clockwise). An invalid script is all rest.
        /// </summary>
        public static void Evaluate(in AnglerScript script, in ShoreCatch shoreCatch, bool hasCatch, int worldSeed,
                                    uint keyHash, float stationHeadingDegrees, double now, out AnglerBeat beat)
        {
            beat = default;
            beat.Scrub01 = float.NaN;
            if (!script.IsValid(out _)) return;

            long slot = SlotIndex(in script, worldSeed, keyHash, now);
            double slotStart = SlotStartSeconds(in script, worldSeed, keyHash, slot);
            beat.Slot = slot;
            beat.SlotStartSeconds = slotStart;

            uint slotHash = SlotHash(worldSeed, keyHash, slot);
            float wait = Mathf.Lerp(script.WaitMinSeconds, script.WaitMaxSeconds, U01(slotHash, StreamWait));
            float jitter = (U01(slotHash, StreamBearing) * 2f - 1f) * script.CastBearingJitterDegrees;
            float bearingRad = (stationHeadingDegrees + script.CastBearingDegrees + jitter) * Mathf.Deg2Rad;
            var dir = new Vector2(Mathf.Sin(bearingRad), Mathf.Cos(bearingRad));   // compass: 0 = +y, clockwise
            Vector2 aim = dir * script.CastReachM;

            bool fish = hasCatch && !string.IsNullOrEmpty(shoreCatch.FishId);
            uint fightHash = fish ? Mix(slotHash ^ (uint)FishFightSeed.For(shoreCatch.FishId, shoreCatch.WeightKg)) : 0u;
            float deep = fish ? Mathf.Lerp(script.DeepMinSeconds, script.DeepMaxSeconds, U01(fightHash, StreamDeep)) : 0f;
            float surface = fish ? Mathf.Lerp(script.SurfaceMinSeconds, script.SurfaceMaxSeconds, U01(fightHash, StreamSurface)) : 0f;

            // The session's phases, end to end from the slot's start.
            double t = now - slotStart;
            double edge = 0d;
            FishingPhase phase = FishingPhase.Idle;
            double phaseStart = 0d;
            float phaseLength = 0f;
            if (Step(ref edge, script.CastBackSeconds, t, FishingPhase.WindBack, ref phase, ref phaseStart, ref phaseLength)
                || Step(ref edge, script.CastSeconds, t, FishingPhase.Cast, ref phase, ref phaseStart, ref phaseLength)
                || Step(ref edge, wait, t, FishingPhase.Waiting, ref phase, ref phaseStart, ref phaseLength))
            { }
            else if (fish)
            {
                if (Step(ref edge, script.BiteSeconds, t, FishingPhase.Bite, ref phase, ref phaseStart, ref phaseLength)
                    || Step(ref edge, deep, t, FishingPhase.FightDeep, ref phase, ref phaseStart, ref phaseLength)
                    || Step(ref edge, surface, t, FishingPhase.FightSurface, ref phase, ref phaseStart, ref phaseLength)
                    || Step(ref edge, script.LandSeconds, t, FishingPhase.Landed, ref phase, ref phaseStart, ref phaseLength))
                { }
            }
            else Step(ref edge, script.EmptyRetrieveSeconds, t, FishingPhase.NoBite, ref phase, ref phaseStart, ref phaseLength);

            beat.Phase = phase;
            if (phase == FishingPhase.Idle) return;

            float inPhase = (float)(t - phaseStart);
            beat.PhaseStartSeconds = slotStart + phaseStart;
            beat.SecondsInPhase = inPhase;
            float progress = phaseLength > 0f ? Mathf.Clamp01(inPhase / phaseLength) : 1f;

            // The body: one mapping (FishingBeatMath), the fight's clock running across deep and surface.
            double fightStart = script.CastBackSeconds + script.CastSeconds + wait + script.BiteSeconds;
            float inFight = (float)(t - fightStart);
            FishingPose pose = FishingBeatMath.PoseFor(phase, inFight, script.StrikeSeconds, stationary: true);
            beat.StateKey = FishingBeatMath.StateKeyFor(pose);
            beat.BeatStartSeconds = pose == FishingPose.Strike || pose == FishingPose.Reel
                ? slotStart + fightStart : beat.PhaseStartSeconds;
            if (pose == FishingPose.CastBack) beat.Scrub01 = progress;

            // The line: the cast aim from the release on, the fish from the bite on.
            string fishId = null;
            float kg = 0f, size = 0f, tension = 0f, landing = 0f, depth = 0f;
            Vector2 offset = Vector2.zero, run = Vector2.zero;
            bool fighting = phase == FishingPhase.FightDeep || phase == FishingPhase.FightSurface;
            if (fish && (phase == FishingPhase.Bite || fighting || phase == FishingPhase.Landed))
            {
                fishId = shoreCatch.FishId;
                kg = shoreCatch.WeightKg;
                size = shoreCatch.Size01;
            }
            if (fighting)
            {
                float span = deep + surface;
                float p = span > 0f ? Mathf.Clamp01(inFight / span) : 1f;
                float k = TwoPi * script.FightWaveHz;
                float phiA = U01(fightHash, StreamWaveA) * TwoPi, phiB = U01(fightHash, StreamWaveB) * TwoPi;
                float side = U01(fightHash, StreamSide) < 0.5f ? -1f : 1f;
                float a = k * inFight + phiA, b = k * SecondWaveRatio * inFight + phiB;
                float swing = 0.5f * (Mathf.Sin(a) + Mathf.Sin(b));                   // -1..1
                float swingRate = 0.5f * k * (Mathf.Cos(a) + SecondWaveRatio * Mathf.Cos(b));
                var perp = new Vector2(-dir.y, dir.x) * (side * script.RunSwingM);
                // She is drawn in from where the hook went in, running across the line as she comes.
                offset = aim * (1f - p) + perp * (swing * (1f - p));
                float pRate = span > 0f ? 1f / span : 0f;
                run = -aim * pRate + perp * (swingRate * (1f - p) - swing * pRate);
                if (run.sqrMagnitude > 0f) run.Normalize();
                tension = Mathf.Lerp(script.TensionMin01, script.TensionMax01, 0.5f + 0.25f * (Mathf.Sin(a) - Mathf.Sin(b)));
                landing = p;
                depth = phase == FishingPhase.FightDeep && deep > 0f ? 1f - Mathf.Clamp01(inFight / deep) : 0f;
            }
            else if (phase == FishingPhase.Landed) landing = 1f;

            float charge = phase == FishingPhase.WindBack ? progress : 0f;
            beat.State = new FishingState(phase, tension, landing, fishId, fishId, default, kg, depth, false, tension,
                                          offset.x, offset.y, charge, aim.x, aim.y, 0f, run.x, run.y, size);
        }

        private static bool Step(ref double edge, float length, double t, FishingPhase next,
                                 ref FishingPhase phase, ref double phaseStart, ref float phaseLength)
        {
            double end = edge + Mathf.Max(0f, length);
            if (t >= edge && t < end)
            {
                phase = next;
                phaseStart = edge;
                phaseLength = length;
                return true;
            }
            edge = end;
            return false;
        }

        private static uint SlotHash(int worldSeed, uint keyHash, long slot)
        {
            unchecked
            {
                uint h = Mix((uint)worldSeed ^ keyHash);
                h = Mix(h ^ (uint)slot);
                return Mix(h ^ (uint)(slot >> 32));
            }
        }

        private static float U01(uint hash, uint stream)
        {
            unchecked { return (Mix(hash ^ (stream * StreamStep)) >> 8) * UnitScale; }
        }

        private static uint Mix(uint h)
        {
            unchecked
            {
                h ^= h >> 16;
                h *= MixA;
                h ^= h >> 13;
                h *= MixB;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
