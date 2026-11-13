using System;

namespace HiddenHarbours.Core
{
    /// <summary>The size word a landed fish earns, from where her weight sits in her species' range.</summary>
    public enum FishSizeBand
    {
        Small = 0,
        Fair = 1,
        Good = 2,
        Heavy = 3,
        Trophy = 4,
    }

    /// <summary>
    /// HOW BIG SHE FEELS (owner's ask 2026-11-13: "rods should bend and react to fish, size … i want
    /// player to get excited woah its something big!!"). The PURE maths that turns the hooked fish's
    /// weight — as a 0..1 place in her species' own range (<see cref="Size01"/>) — and her movement into
    /// what the rod, the line, the camera and the strain voice do. Every number comes from
    /// <see cref="FishFeelSettings"/> (<c>GameConfig.FishFeel</c>, rule 6).
    ///
    /// <para><b>Feedback, never balance.</b> Nothing here is read by the fight's rules: tension, landing
    /// progress, the snap and the lean scoring are untouched. These functions only shape what the
    /// presenters DRAW and PLAY from the state the fight already publishes.</para>
    ///
    /// <para><b>Pure and deterministic</b> (rule 5): closed-form functions of their arguments — no RNG,
    /// no clock of their own, no allocation — so a presenter may call them every frame and an EditMode
    /// test can pin every curve. NaN-safe: a NaN input reads as 0.</para>
    /// </summary>
    public static class FishFeelMath
    {
        // ---- size ------------------------------------------------------------------------------

        /// <summary>Where <paramref name="weightKg"/> sits in the species' range, 0 (her smallest) .. 1
        /// (her heaviest). A degenerate or unknown range (max ≤ min) reads as 0 — "no opinion", never a
        /// false big one.</summary>
        public static float Size01(float weightKg, float minKg, float maxKg)
        {
            float w = Safe(weightKg), lo = Safe(minKg), hi = Safe(maxKg);
            if (hi - lo <= 1e-6f) return 0f;
            return Clamp01((w - lo) / (hi - lo));
        }

        /// <summary>Is she in the top band of her species — the "woah, something big" fish?</summary>
        public static bool IsBigOne(float size01, in FishFeelSettings s)
            => Clamp01(Safe(size01)) >= s.BigOneThreshold01;

        /// <summary>The landing line's size band. Heavy starts exactly where the hook-set's big-one
        /// moment does (<see cref="FishFeelSettings.BigOneThreshold01"/>), so the fish that made the
        /// player say "woah" is always named a heavy one (or better) when she comes aboard.</summary>
        public static FishSizeBand BandFor(float size01, in FishFeelSettings s)
        {
            float z = Clamp01(Safe(size01));
            if (z >= s.SizeTrophyFrom01 && z >= s.BigOneThreshold01) return FishSizeBand.Trophy;
            if (z >= s.BigOneThreshold01) return FishSizeBand.Heavy;
            if (z >= s.SizeGoodFrom01) return FishSizeBand.Good;
            if (z >= s.SizeSmallBelow01) return FishSizeBand.Fair;
            return FishSizeBand.Small;
        }

        /// <summary>The size word, as an interned literal (no composed string).</summary>
        public static string SizeWord(FishSizeBand band)
        {
            switch (band)
            {
                case FishSizeBand.Small: return "a small one";
                case FishSizeBand.Fair: return "a fair one";
                case FishSizeBand.Good: return "a good one";
                case FishSizeBand.Heavy: return "a heavy one";
                default: return "a real monster";
            }
        }

        // ---- the rod: bend ---------------------------------------------------------------------

        /// <summary>
        /// How loaded the rod DRAWS, 0..1, from the fight's own load read (the worse of rod-bend and
        /// tension) and her size. A small fish only gets <see cref="FishFeelSettings.BendSmallScale01"/>
        /// of the load onto the rod (she nods the tip); a heavy one puts all of it on AND holds the rod
        /// down at a floor (<see cref="FishFeelSettings.BendHeavyFloor01"/>) even between her runs. While
        /// the slack tell is open the floor relaxes to <see cref="FishFeelSettings.BendSlackKeep01"/> of
        /// itself, so "she gave — reel" still reads on the rod.
        /// </summary>
        public static float RodLoad01(float baseLoad01, float size01, bool slack, in FishFeelSettings s)
        {
            float load = Clamp01(Safe(baseLoad01)), z = Clamp01(Safe(size01));
            float scaled = load * Lerp(s.BendSmallScale01, 1f, z);
            float floor = s.BendHeavyFloor01 * z * (slack ? s.BendSlackKeep01 : 1f);
            return Clamp01(Math.Max(scaled, floor));
        }

        /// <summary>The most the rod may lean under load, in degrees: the presenter's own base lean,
        /// grown toward <see cref="FishFeelSettings.BendHeavyLeanScale"/> times that for her heaviest.</summary>
        public static float MaxLeanDegrees(float baseLeanDegrees, float size01, in FishFeelSettings s)
            => Math.Max(0f, Safe(baseLeanDegrees)) * Lerp(1f, s.BendHeavyLeanScale, Clamp01(Safe(size01)));

        // ---- the rod: her movement ---------------------------------------------------------------

        /// <summary>
        /// HEAD-SHAKES, as sharp tip pulses in degrees (≥ 0, added toward her). A pulse train whose
        /// rate falls and whose depth grows with size — a small fish rattles the tip quickly and
        /// lightly, a heavy one thumps it slowly and hard — gated by how hard she is working
        /// (<paramref name="effort01"/>; pass 0 while she is slack). <paramref name="seed01"/> (0..1)
        /// offsets the train so two fights do not shake in step. Closed-form in
        /// <paramref name="fightSeconds"/>: the same clock in, the same pulse out.
        /// </summary>
        public static float TipPulseDegrees(float fightSeconds, float size01, float effort01,
                                            float seed01, in FishFeelSettings s)
        {
            float z = Clamp01(Safe(size01)), effort = Clamp01(Safe(effort01));
            if (effort <= 0f) return 0f;
            float hz = Lerp(s.ShakeHzSmall, s.ShakeHzHeavy, z);
            float amp = Lerp(s.ShakeDegreesSmall, s.ShakeDegreesHeavy, z);
            if (hz <= 0f || amp <= 0f) return 0f;
            double phase = Math.Max(0f, Safe(fightSeconds)) * (double)hz + Safe(seed01);
            double wave = Math.Sin(2.0 * Math.PI * phase);
            if (wave <= 0.0) return 0f;                      // half the cycle is rest: a pulse, not a wobble
            return amp * effort * (float)Math.Pow(wave, Math.Max(1f, s.ShakeSharpness));
        }

        /// <summary>How far (world metres) her run drags the point the rod leans toward, along her
        /// published run direction — small for a small fish, the full
        /// <see cref="FishFeelSettings.RunPullMetres"/> for her heaviest.</summary>
        public static float RunPullMetres(float size01, in FishFeelSettings s)
            => Math.Max(0f, s.RunPullMetres) * Lerp(s.RunPullSmall01, 1f, Clamp01(Safe(size01)));

        /// <summary>Did she TURN between two published run directions? True when both are real
        /// directions and the cosine between them is under <see cref="FishFeelSettings.TurnCosine"/>.
        /// A run starting from rest (or stopping) is not a turn.</summary>
        public static bool IsTurn(float prevX, float prevY, float nextX, float nextY, in FishFeelSettings s)
        {
            float px = Safe(prevX), py = Safe(prevY), nx = Safe(nextX), ny = Safe(nextY);
            double pl = Math.Sqrt(px * px + py * py), nl = Math.Sqrt(nx * nx + ny * ny);
            if (pl <= 1e-4 || nl <= 1e-4) return false;
            return (px * nx + py * ny) / (pl * nl) < s.TurnCosine;
        }

        /// <summary>The tip kick a turn throws, in degrees, decaying linearly to nothing across
        /// <see cref="FishFeelSettings.TurnKickSeconds"/>; sized like the run pull.</summary>
        public static float TurnKickDegrees(float secondsSinceTurn, float size01, in FishFeelSettings s)
            => s.TurnKickDegrees * Lerp(s.RunPullSmall01, 1f, Clamp01(Safe(size01)))
               * Decay01(secondsSinceTurn, s.TurnKickSeconds);

        /// <summary>
        /// THE HOOK-SET SLAM, in degrees: the rod is yanked over the instant she is on and springs back
        /// across <see cref="FishFeelSettings.HookSlamSeconds"/>. Grows with size, and a big one
        /// (<see cref="IsBigOne"/>) slams <see cref="FishFeelSettings.BigOneSlamScale"/> times harder —
        /// the first frame of the fight already says how big she is.
        /// </summary>
        public static float HookSlamDegrees(float secondsSinceHook, float size01, in FishFeelSettings s)
        {
            float z = Clamp01(Safe(size01));
            float peak = s.HookSlamDegrees * Lerp(s.HookSlamSmall01, 1f, z);
            if (IsBigOne(z, in s)) peak *= s.BigOneSlamScale;
            return Math.Max(0f, peak) * Decay01(secondsSinceHook, s.HookSlamSeconds);
        }

        // ---- the line ----------------------------------------------------------------------------

        /// <summary>The line's taut read with her weight on it: never slacker than
        /// <see cref="FishFeelSettings.LineHeavyTautFloor01"/> × size. The slack tell (a base taut of 0
        /// with <paramref name="slack"/>) passes through untouched — the sag IS the instruction.</summary>
        public static float LineTaut01(float baseTaut01, float size01, bool slack, in FishFeelSettings s)
        {
            float taut = Clamp01(Safe(baseTaut01));
            if (slack) return taut;
            return Clamp01(Math.Max(taut, s.LineHeavyTautFloor01 * Clamp01(Safe(size01))));
        }

        /// <summary>The line's shudder 0..1 with her weight humming on it: the strain shudder the
        /// presenter already draws, plus a hum that grows with size and with how hard she is working
        /// (it idles at <see cref="FishFeelSettings.LineHumIdle01"/> of itself between runs). No hum
        /// while the slack tell is open.</summary>
        public static float LineShudder01(float baseShudder01, float size01, float effort01, bool slack,
                                          in FishFeelSettings s)
        {
            float shudder = Clamp01(Safe(baseShudder01));
            if (slack) return shudder;
            float work = Lerp(s.LineHumIdle01, 1f, Clamp01(Safe(effort01)));
            return Clamp01(shudder + s.LineHumHeavy01 * Clamp01(Safe(size01)) * work);
        }

        /// <summary>The line's whitening 0..1 with her weight on it: a heavy fish lifts the resting
        /// line toward white by <see cref="FishFeelSettings.LineWhitenHeavy01"/> of the headroom left,
        /// so the tension read still climbs to full white at the same tension it always did.</summary>
        public static float LineWhiten01(float baseWhiten01, float size01, in FishFeelSettings s)
        {
            float whiten = Clamp01(Safe(baseWhiten01));
            return Clamp01(whiten + (1f - whiten) * s.LineWhitenHeavy01 * Clamp01(Safe(size01)));
        }

        // ---- the camera and the strain voice -------------------------------------------------------

        /// <summary>The multiplier on the hook-set camera kick: <see cref="FishFeelSettings.HookKickSmallScale"/>
        /// for her smallest up to <see cref="FishFeelSettings.HookKickHeavyScale"/> for her heaviest,
        /// times <see cref="FishFeelSettings.BigOneKickScale"/> for a big one.</summary>
        public static float HookKickScale(float size01, in FishFeelSettings s)
        {
            float z = Clamp01(Safe(size01));
            float scale = Lerp(s.HookKickSmallScale, s.HookKickHeavyScale, z);
            if (IsBigOne(z, in s)) scale *= s.BigOneKickScale;
            return Math.Max(0f, scale);
        }

        /// <summary>The strain voice's gain floor while a fish of this size is on and not slack — the
        /// heavy fish's line hums even before the tension climbs.</summary>
        public static float StrainHumGain(float size01, bool slack, in FishFeelSettings s)
            => slack ? 0f : Clamp01(s.StrainHumHeavyGain * Clamp01(Safe(size01)));

        /// <summary>The multiplier on the strain voice's pitch: a heavier fish groans deeper, by up to
        /// <see cref="FishFeelSettings.StrainPitchHeavyDrop01"/>.</summary>
        public static float StrainPitchScale(float size01, in FishFeelSettings s)
            => Math.Max(0.1f, 1f - s.StrainPitchHeavyDrop01 * Clamp01(Safe(size01)));

        /// <summary>The hook-set SCREAM for a big one: an extra strain gain that opens at
        /// <see cref="FishFeelSettings.HookScreamGain"/> and dies away across
        /// <see cref="FishFeelSettings.HookScreamSeconds"/>. 0 for any fish under the big-one line.</summary>
        public static float HookScreamGain(float secondsSinceHook, float size01, in FishFeelSettings s)
            => IsBigOne(size01, in s)
                ? Clamp01(s.HookScreamGain) * Decay01(secondsSinceHook, s.HookScreamSeconds)
                : 0f;

        /// <summary>The strain voice's pitch multiplier during that scream: opens at
        /// <see cref="FishFeelSettings.HookScreamPitchScale"/> and falls back to 1 with the gain. 1 for
        /// any fish under the big-one line.</summary>
        public static float HookScreamPitchScale(float secondsSinceHook, float size01, in FishFeelSettings s)
            => IsBigOne(size01, in s)
                ? 1f + Math.Max(0f, s.HookScreamPitchScale - 1f) * Decay01(secondsSinceHook, s.HookScreamSeconds)
                : 1f;

        // ---- helpers -------------------------------------------------------------------------------

        /// <summary>1 at t = 0 falling linearly to 0 at <paramref name="seconds"/>; 0 before 0, after
        /// the window, and for a non-positive window.</summary>
        private static float Decay01(float t, float seconds)
        {
            float time = Safe(t);
            if (seconds <= 0f || time < 0f || time >= seconds) return 0f;
            return 1f - time / seconds;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
        private static float Clamp01(float x) => x < 0f ? 0f : (x > 1f ? 1f : x);
        private static float Safe(float x) => float.IsNaN(x) ? 0f : x;
    }
}
