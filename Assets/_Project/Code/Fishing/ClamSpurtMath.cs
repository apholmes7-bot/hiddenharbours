using System;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// The clam flat's spurt — <b>the art director's own contract, not a placeholder cadence</b>.
    ///
    /// <para>A buried clam gives itself away by squirting through its keyhole, and that squirt is the
    /// gameplay TELL for the dig: it is the only thing that says "here, and now". Catch pass 2's
    /// <c>shellfishRig2.js</c> publishes the timing as data — <c>holes()</c> hands every keyhole a
    /// phase and a period, and <c>spurt(hole, t)</c> turns a time into a jet height — and this is
    /// that law, transcribed, so the game squirts on the same clock the art was drawn to.</para>
    ///
    /// <para><b>What it replaces.</b> The greybox tell flipped a boolean on a stateful 10–20 second
    /// timer for 1.5 seconds, driven by a <c>System.Random</c>. Three things were wrong with it: the
    /// numbers were invented rather than the rig's (the rig says <b>2.6–7.8 s apart, 420 ms long</b>,
    /// so the old tell was roughly three times too slow and four times too long); it was a bare
    /// on/off, so the art could only ever be a flip-book rather than a jet that rises and falls; and
    /// it was STATEFUL, which makes it a thing that drifts rather than a thing you can ask about at
    /// a given moment.</para>
    ///
    /// <para><b>Why a pure function of time (rule 5).</b> Every value here is computed from
    /// <c>(worldSeed, hole position, t)</c> and nothing is accumulated, so two observers asking at
    /// the same moment get the same answer, a reload reproduces the same flat, and nothing needs
    /// saving. Same discipline as the tide: recompute, never store.</para>
    ///
    /// <para><b>The numbers are the rig's, and they are pinned.</b> <c>dur</c> is a flat 420 ms and
    /// <c>period</c> is <c>2600 + rng()·5200</c> ms; the sidecar's own 400-hole sample reports the
    /// realised span as [2616.204, 7793.919] ms, which is what <c>ClamSpurtMathTests</c> holds these
    /// constants to. The rise is <c>sin(π·u)</c> over the window — 0 at the lip, 1 at the crest,
    /// 0 again as it falls back.</para>
    ///
    /// <para>⚠️ <b>The window's phase convention is not the obvious one.</b> The rig opens the window
    /// when <c>(t + phase) % period ≤ dur</c>, so a hole's first spurt is at
    /// <c>t = period − (phase % period)</c>, NOT at <c>t = phase</c>. Reading <c>phase</c> as "when
    /// it starts" gets a hole that never appears to spurt at the time you expect.</para>
    /// </summary>
    public static class ClamSpurtMath
    {
        /// <summary>How long one spurt lasts, ms. <c>shellfishRig2.holes()</c> writes a flat
        /// <c>dur: 420</c> on every hole — it is not rolled.</summary>
        public const float SpurtMilliseconds = 420f;

        /// <summary>Shortest gap between a hole's spurts, ms — the <c>2600</c> in the rig's
        /// <c>period: 2600 + rng()*5200</c>.</summary>
        public const float MinPeriodMilliseconds = 2600f;

        /// <summary>Longest gap, ms (exclusive) — <c>2600 + 5200</c>.</summary>
        public const float MaxPeriodMilliseconds = 7800f;

        /// <summary>Span the rig rolls a hole's phase over, ms — <c>phase: rng()*9000</c>.</summary>
        public const float PhaseSpanMilliseconds = 9000f;

        /// <summary>The jet is 1 px wide and <c>rise × 4</c> px tall — the sidecar's own instruction
        /// to the page, and the only reason the rise is worth publishing at all.</summary>
        public const int JetPixels = 4;

        /// <summary>A droplet detaches 1 px above the jet once <c>u</c> passes this — sidecar.</summary>
        public const float DropletAfterU = 0.45f;

        /// <summary>
        /// The phase and period of the hole at <paramref name="worldX"/>, <paramref name="worldY"/>.
        ///
        /// <para>The hole's POSITION is its identity — the flat is rebuilt deterministically every
        /// load and never saved, so there is no id to key on and none is needed. Positions are
        /// quantised to the centimetre first, so a hole placed by a float that differs in its last
        /// bits between two builds still lands on one timing.</para>
        ///
        /// <para>Each hole draws its phase and period INDEPENDENTLY, which is what the rig does
        /// (<c>rng()</c> per hole, per field) and what a real flat looks like — clams do not take
        /// turns. Deliberately NOT an even share-out of the range: an evenly spread flat reads as a
        /// sprinkler system.</para>
        /// </summary>
        public static void TimingFor(int worldSeed, float worldX, float worldY,
                                     out float phaseMs, out float periodMs)
        {
            int hx = (int)Math.Round(worldX * 100.0);
            int hy = (int)Math.Round(worldY * 100.0);

            uint h = Mix((uint)worldSeed, (uint)hx, (uint)hy);
            phaseMs = Unit(h) * PhaseSpanMilliseconds;

            uint h2 = Mix(h, 0x9E3779B9u, (uint)hy);
            periodMs = MinPeriodMilliseconds
                     + Unit(h2) * (MaxPeriodMilliseconds - MinPeriodMilliseconds);
        }

        /// <summary>
        /// Is this hole spurting at <paramref name="timeMilliseconds"/>, and how high?
        ///
        /// <para><paramref name="rise"/> is 0..1 — <c>sin(π·u)</c> across the window — and
        /// <paramref name="u"/> is 0..1 progress through it. Both are meaningless when this returns
        /// false, and are set to zero so a caller that ignores the return value draws nothing rather
        /// than a stuck jet.</para>
        /// </summary>
        public static bool TrySpurt(double timeMilliseconds, float phaseMs, float periodMs,
                                    out float rise, out float u)
        {
            rise = 0f; u = 0f;
            if (!(periodMs > 0f)) return false;              // also rejects NaN

            double l = (timeMilliseconds + phaseMs) % periodMs;
            if (l < 0) l += periodMs;                        // a negative clock must not go dark
            if (l > SpurtMilliseconds) return false;

            u = (float)(l / SpurtMilliseconds);
            rise = (float)Math.Sin(Math.PI * u);
            return true;
        }

        /// <summary>Jet height in pixels for a rise — the sidecar's <c>rise × 4 px</c>.</summary>
        public static float JetHeightPixels(float rise) => rise * JetPixels;

        /// <summary>Whether the detached droplet shows at this point in the window.</summary>
        public static bool ShowsDroplet(float u) => u > DropletAfterU;

        /// <summary>
        /// The share of its life a hole spends spurting — <c>dur / period</c>. Handy for a test that
        /// wants to know a flat is alive without watching one hole: over the rig's own period span
        /// this is 5.4%–16.2%, and the sidecar's 400-hole sample averages about 9%.
        /// </summary>
        public static float DutyCycle(float periodMs) =>
            periodMs > 0f ? SpurtMilliseconds / periodMs : 0f;

        // ---- the hash -----------------------------------------------------------------------

        /// <summary>
        /// A 32-bit avalanche mix of three words. Not a PRNG stream: each hole is asked once, so what
        /// matters is that one bit of change anywhere redistributes the whole output, which is what
        /// keeps two holes a centimetre apart from spurting in unison.
        /// </summary>
        static uint Mix(uint a, uint b, uint c)
        {
            unchecked
            {
                uint h = a * 0x9E3779B1u;
                h ^= b + 0x85EBCA6Bu + (h << 6) + (h >> 2);
                h ^= c + 0xC2B2AE35u + (h << 6) + (h >> 2);
                h ^= h >> 16; h *= 0x7FEB352Du;
                h ^= h >> 15; h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>The top 24 bits as 0..1 — the low bits of a mixed hash carry the least
        /// avalanche, and 24 bits is far more resolution than a millisecond needs.</summary>
        static float Unit(uint h) => (h >> 8) * (1.0f / 16777216.0f);
    }
}
