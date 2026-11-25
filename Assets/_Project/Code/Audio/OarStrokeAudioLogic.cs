using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Audio
{
    /// <summary>One stroke sound to start now: which, how loud, at what pitch and pan.</summary>
    public struct OarStrokeVoice
    {
        /// <summary>True for the catch-and-pull, false for the release drip.</summary>
        public bool Catch;
        /// <summary>The voice's gain before the bus (0..1).</summary>
        public float Gain;
        public float Pitch;
        /// <summary>-1 (left) .. +1 (right).</summary>
        public float Pan;
        /// <summary>The boat's stroke count this sound belongs to (1 for its first stroke heard).</summary>
        public int Stroke;
    }

    /// <summary>
    /// <b>Decides which blade-water moments are heard</b>, and how. The oar splash publishes one
    /// <see cref="OarBladeWater"/> per oar per catch and release; this turns them into one catch sound a
    /// stroke and one release drip a stroke per boat, drops rowers out of earshot, and gives each stroke a
    /// small pitch and level variation from a hash of the boat's own stroke count, so the same rowing
    /// always sounds the same (no global randomness).
    ///
    /// <para>Pure and allocation-free: a fixed table of the boats it has heard recently, reused
    /// oldest-first. EditMode-tested; <see cref="OarStrokePlayer"/> voices what it decides.</para>
    /// </summary>
    public sealed class OarStrokeVoicing
    {
        /// <summary>Boats remembered at once. More rowers than this in earshot at once is not a case the
        /// game has; the oldest is forgotten first (its next stroke then counts from 1 again).</summary>
        public const int HullCapacity = 8;

        private readonly int[] _hull = new int[HullCapacity];
        private readonly bool[] _used = new bool[HullCapacity];
        private readonly double[] _lastCatch = new double[HullCapacity];
        private readonly double[] _lastRelease = new double[HullCapacity];
        private readonly double[] _lastHeard = new double[HullCapacity];
        private readonly int[] _strokes = new int[HullCapacity];

        /// <summary>Forget every boat.</summary>
        public void Reset()
        {
            for (int i = 0; i < HullCapacity; i++) _used[i] = false;
        }

        /// <summary>
        /// Hear one blade-water moment at time <paramref name="now"/> (seconds, any steady clock) with the
        /// listener at <paramref name="listener"/>. True with the voice to start when it is heard; false
        /// when it is out of earshot or is the other oar of a stroke already voiced.
        /// </summary>
        public bool TryVoice(in OarBladeWater e, double now, Vector2 listener, OarStrokeAudioTuning t,
                             out OarStrokeVoice voice)
        {
            voice = default;
            if (t == null) return false;
            if (e.Moment != OarStrokePart.Catch && e.Moment != OarStrokePart.Release) return false;

            float distance = Vector2.Distance(e.Position, listener);
            float reach = DistanceGain(distance, t.FullVolumeMetres, t.EarshotMetres);
            if (reach <= 0f) return false;

            int slot = SlotFor(e.HullId, now);
            float gap = Mathf.Max(0f, t.MinGapSeconds);
            bool isCatch = e.IsCatch;
            if (isCatch)
            {
                if (now - _lastCatch[slot] < gap) return false;
                _lastCatch[slot] = now;
                _strokes[slot]++;
            }
            else
            {
                if (now - _lastRelease[slot] < gap) return false;
                _lastRelease[slot] = now;
            }
            _lastHeard[slot] = now;

            int stroke = Mathf.Max(1, _strokes[slot]);
            float baseGain = isCatch ? t.CatchGain : t.ReleaseGain;
            float effort = Mathf.Lerp(Mathf.Clamp01(t.LightStrokeGain), 1f, Mathf.Clamp01(e.Strength01));
            voice = new OarStrokeVoice
            {
                Catch = isCatch,
                Stroke = stroke,
                Pitch = 1f + Signed(t.Seed, stroke, isCatch ? 1 : 3) * Mathf.Max(0f, t.PitchVariation),
                Gain = Mathf.Clamp01(baseGain * effort * reach *
                                     (1f + Signed(t.Seed, stroke, isCatch ? 2 : 4) * Mathf.Max(0f, t.GainVariation))),
                Pan = Pan(e.Position.x - listener.x, t.EarshotMetres, t.PanWidth),
            };
            return true;
        }

        /// <summary>Full volume inside <paramref name="full"/> metres, falling linearly to silence at
        /// <paramref name="earshot"/>; 0 beyond it.</summary>
        public static float DistanceGain(float distance, float full, float earshot)
        {
            if (!(distance >= 0f)) return 0f;
            full = Mathf.Max(0f, full);
            earshot = Mathf.Max(full, earshot);
            if (distance <= full) return 1f;
            if (distance >= earshot) return 0f;
            return 1f - (distance - full) / (earshot - full);
        }

        /// <summary>Pan for a rower <paramref name="dx"/> metres right of the listener.</summary>
        public static float Pan(float dx, float earshot, float width)
        {
            if (!(earshot > 0f)) return 0f;
            return Mathf.Clamp(dx / earshot, -1f, 1f) * Mathf.Clamp01(width);
        }

        /// <summary>A deterministic value in [-1, 1] for stroke <paramref name="stroke"/>, channel
        /// <paramref name="salt"/>, under <paramref name="seed"/>. The same inputs give the same value on
        /// every run and every machine.</summary>
        public static float Signed(int seed, int stroke, int salt)
        {
            uint h = Mix((uint)seed * 0x9E3779B9u ^ Mix((uint)stroke * 0x85EBCA6Bu + (uint)salt * 0xC2B2AE35u));
            return (h & 0xFFFFFF) / (float)0xFFFFFF * 2f - 1f;
        }

        private static uint Mix(uint x)
        {
            x ^= x >> 16; x *= 0x7FEB352Du;
            x ^= x >> 15; x *= 0x846CA68Bu;
            x ^= x >> 16;
            return x;
        }

        private int SlotFor(int hullId, double now)
        {
            int free = -1, oldest = 0;
            for (int i = 0; i < HullCapacity; i++)
            {
                if (_used[i] && _hull[i] == hullId) return i;
                if (!_used[i]) { if (free < 0) free = i; }
                else if (_lastHeard[i] < _lastHeard[oldest] || !_used[oldest]) oldest = i;
            }
            int s = free >= 0 ? free : oldest;
            _used[s] = true;
            _hull[s] = hullId;
            _lastCatch[s] = double.NegativeInfinity;
            _lastRelease[s] = double.NegativeInfinity;
            _lastHeard[s] = now;
            _strokes[s] = 0;
            return s;
        }
    }
}
