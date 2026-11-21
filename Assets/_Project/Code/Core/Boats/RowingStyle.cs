using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A <see cref="RowingStyleDef"/>, baked for the stroke clock</b>: the time knots of its five
    /// shares, and the mean of its blade shape so the surge keeps the average push. Built once when a
    /// style is chosen (<see cref="From"/>), then read every tick with no allocation.
    ///
    /// <para><b>Two jobs, one phase.</b> The stroke clock advances a RAW stroke phase at an even rate
    /// (the tempo). <see cref="WarpStrokePhase"/> maps it to the stroke phase the oars are drawn at, so a
    /// style can spend longer on the pull or hold the oars still in a rest. <see cref="ThrustMultiplier"/>
    /// reads the SAME warped phase to decide how hard the oar is driving the hull at that moment, so the
    /// boat surges exactly while the blades are drawn in the water.</para>
    ///
    /// <para><see cref="Unstyled"/> (no def) is the old behaviour bit for bit: identity timing, an even
    /// push. Pure, deterministic, allocation-free.</para>
    /// </summary>
    public readonly struct RowingStyle
    {
        // ---- the stroke's own parts (structural: OarStrokeMath's four parts, centred on the catch and
        // the release). Not tunables; the style's shares are measured against them. ----

        /// <summary>End of the catch: half a part after phase 0.</summary>
        public const float CatchEnd = OarStrokeMath.PartSpan * 0.5f;
        /// <summary>End of the pull: half a part before the release.</summary>
        public const float PullEnd = OarStrokeMath.ReleasePhase - OarStrokeMath.PartSpan * 0.5f;
        /// <summary>End of the release: half a part after it. The recovery runs from here to 1.</summary>
        public const float ReleaseEnd = OarStrokeMath.ReleasePhase + OarStrokeMath.PartSpan * 0.5f;

        /// <summary>The share of phase each part spans: the shares that reproduce the plain stroke.</summary>
        public const float CatchSpan = CatchEnd;
        public const float PullSpan = PullEnd - CatchEnd;
        public const float ReleaseSpan = ReleaseEnd - PullEnd;
        public const float RecoverySpan = 1f - ReleaseEnd;

        /// <summary>Default rest point: the middle of the recovery (oars square, blades highest).</summary>
        public const float DefaultRestAt = (ReleaseEnd + 1f) * 0.5f;

        /// <summary>The slowest tempo a def may ask for (a guard against a stopped clock, not a feel knob).</summary>
        public const float MinTempoScale = 0.05f;

        /// <summary>Samples taken over one stroke to find the blade shape's mean (integration resolution).</summary>
        public const int MeanSamples = 1024;

        /// <summary>Two timings within this are the same timing (the steady style's shares, written as
        /// decimals in an asset, against the parts above).</summary>
        public const float TimingEpsilon = 1e-5f;

        /// <summary>Stroke rate as a share of the boat's own stroke tempo.</summary>
        public readonly float TempoScale;
        /// <summary>Scales the rowing station's lean-back.</summary>
        public readonly float LaybackScale;
        /// <summary>0 = an even push, 1 = all of it on the pull.</summary>
        public readonly float SurgeDepth;
        /// <summary>The average push over a stroke, as a share of the hull's oar power.</summary>
        public readonly float AverageThrustScale;
        /// <summary>Where the oars wait (stroke phase).</summary>
        public readonly float RestAt;
        /// <summary>How softly the oars settle into the rest and leave it, 0..1.</summary>
        public readonly float RestEase;
        /// <summary>The share of a stroke's time spent in the rest.</summary>
        public readonly float RestShare;
        /// <summary>True when the timing is the plain stroke's: the warp then returns its input unchanged.</summary>
        public readonly bool IdentityTiming;
        /// <summary>Mean of <see cref="BladeShape"/> over one stroke of RAW time under this timing.</summary>
        public readonly float MeanBladeShape;

        // Raw-time knots: end of catch, pull, release, pre-rest recovery, rest. The post-rest recovery
        // runs from _t5 to 1.
        private readonly float _t1, _t2, _t3, _t4, _t5;

        /// <summary>No style: the stroke as it always was, and an even push (no surge).</summary>
        public static readonly RowingStyle Unstyled = Build(1f, CatchSpan, PullSpan, ReleaseSpan, RecoverySpan,
                                                            0f, DefaultRestAt, 0f, 1f, 0f, 1f);

        private RowingStyle(float tempo, float layback, float surge, float average, float restAt, float restEase,
                            float restShare, bool identity, float t1, float t2, float t3, float t4, float t5,
                            float meanShape)
        {
            TempoScale = tempo;
            LaybackScale = layback;
            SurgeDepth = surge;
            AverageThrustScale = average;
            RestAt = restAt;
            RestEase = restEase;
            RestShare = restShare;
            IdentityTiming = identity;
            _t1 = t1; _t2 = t2; _t3 = t3; _t4 = t4; _t5 = t5;
            MeanBladeShape = meanShape;
        }

        /// <summary>The style a def describes, or <see cref="Unstyled"/> for none.</summary>
        public static RowingStyle From(RowingStyleDef def) => def == null
            ? Unstyled
            : Build(def.TempoScale, def.CatchShare, def.PullShare, def.ReleaseShare, def.RecoveryShare,
                    def.RestShare, def.RestAtStrokePhase, def.RestEase, def.LaybackScale, def.SurgeDepth,
                    def.AverageThrustScale);

        /// <summary>Bake a style from its numbers. Shares are normalised by their sum; an all-zero set of
        /// shares falls back to the plain stroke's.</summary>
        public static RowingStyle Build(float tempoScale, float catchShare, float pullShare, float releaseShare,
                                        float recoveryShare, float restShare, float restAt, float restEase,
                                        float laybackScale, float surgeDepth, float averageThrustScale)
        {
            float c = Mathf.Max(0f, catchShare), p = Mathf.Max(0f, pullShare), r = Mathf.Max(0f, releaseShare);
            float rec = Mathf.Max(0f, recoveryShare), rest = Mathf.Max(0f, restShare);
            float sum = c + p + r + rec + rest;
            if (sum <= 0f) { c = CatchSpan; p = PullSpan; r = ReleaseSpan; rec = RecoverySpan; rest = 0f; sum = 1f; }
            c /= sum; p /= sum; r /= sum; rec /= sum; rest /= sum;

            float at = Mathf.Clamp(restAt, ReleaseEnd, 1f);
            float pre = rec * (at - ReleaseEnd) / RecoverySpan;
            float t1 = c, t2 = t1 + p, t3 = t2 + r, t4 = t3 + pre, t5 = t4 + rest;

            bool identity = rest <= TimingEpsilon
                            && Mathf.Abs(c - CatchSpan) <= TimingEpsilon
                            && Mathf.Abs(p - PullSpan) <= TimingEpsilon
                            && Mathf.Abs(r - ReleaseSpan) <= TimingEpsilon;

            var timing = new RowingStyle(Mathf.Max(MinTempoScale, tempoScale), Mathf.Max(0f, laybackScale),
                                         Mathf.Clamp01(surgeDepth), Mathf.Max(0f, averageThrustScale), at,
                                         Mathf.Clamp01(restEase), rest, identity, t1, t2, t3, t4, t5, 1f);
            float mean = 0f;
            for (int i = 0; i < MeanSamples; i++)
                mean += BladeShape(timing.WarpStrokePhase((i + 0.5f) / MeanSamples));
            mean /= MeanSamples;

            return new RowingStyle(timing.TempoScale, timing.LaybackScale, timing.SurgeDepth,
                                   timing.AverageThrustScale, at, timing.RestEase, rest, identity,
                                   t1, t2, t3, t4, t5, mean);
        }

        /// <summary>
        /// The stroke phase the oars are drawn at when the clock's even (raw) stroke phase is
        /// <paramref name="rawStrokePhase"/>. Continuous and monotone over the stroke; wraps at 1 with no
        /// jump; holds at <see cref="RestAt"/> through the rest. The plain stroke returns its input.
        /// </summary>
        public float WarpStrokePhase(float rawStrokePhase)
        {
            float s = OarStrokeMath.Wrap01(rawStrokePhase);
            if (IdentityTiming) return s;
            if (s < _t1) return Remap(s, 0f, _t1, 0f, CatchEnd);
            if (s < _t2) return Remap(s, _t1, _t2, CatchEnd, PullEnd);
            if (s < _t3) return Remap(s, _t2, _t3, PullEnd, ReleaseEnd);
            if (s < _t4)
            {
                float u = (s - _t3) / (_t4 - _t3);
                float easeOut = 1f - (1f - u) * (1f - u);
                return ReleaseEnd + (u + (easeOut - u) * RestEase) * (RestAt - ReleaseEnd);
            }
            if (s < _t5) return RestAt;
            {
                float u = (s - _t5) / (1f - _t5);
                float easeIn = u * u;
                return OarStrokeMath.Wrap01(RestAt + (u + (easeIn - u) * RestEase) * (1f - RestAt));
            }
        }

        /// <summary>True when <paramref name="rawStrokePhase"/> falls in the rest.</summary>
        public bool IsResting(float rawStrokePhase)
        {
            float s = OarStrokeMath.Wrap01(rawStrokePhase);
            return !IdentityTiming && s >= _t4 && s < _t5;
        }

        /// <summary>
        /// How far into the rest the rower is at <paramref name="rawStrokePhase"/>: 1 through the rest,
        /// easing up over the last <see cref="RestEase"/> share of the recovery before it and down over the
        /// same share after it (smoothstep), 0 elsewhere and always 0 for a style with no rest. What the
        /// rower's body relaxes by, and the blades settle by, so a rest never snaps in or out.
        /// </summary>
        public float RestWeight(float rawStrokePhase)
        {
            if (IdentityTiming || RestShare <= TimingEpsilon) return 0f;
            float s = OarStrokeMath.Wrap01(rawStrokePhase);
            if (s >= _t4 && s < _t5) return 1f;
            float pre = (_t4 - _t3) * RestEase, post = (1f - _t5) * RestEase;
            if (pre > 0f && s >= _t4 - pre && s < _t4) return SmoothStep01((s - (_t4 - pre)) / pre);
            if (post > 0f && s >= _t5 && s < _t5 + post) return 1f - SmoothStep01((s - _t5) / post);
            return 0f;
        }

        /// <summary>
        /// The breath taken in the rest at <paramref name="rawStrokePhase"/>, -1..1: one whole breath per
        /// rest (a sine over the rest's own span, 0 at both ends so it joins the stroke without a step),
        /// 0 outside it. Deterministic: read off the stroke clock, no clock of its own.
        /// </summary>
        public float RestBreath(float rawStrokePhase)
        {
            if (IdentityTiming || RestShare <= TimingEpsilon) return 0f;
            float s = OarStrokeMath.Wrap01(rawStrokePhase);
            if (s < _t4 || s >= _t5) return 0f;
            return Mathf.Sin(2f * Mathf.PI * (s - _t4) / (_t5 - _t4));
        }

        private static float SmoothStep01(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// How hard an oar drives the hull at <paramref name="rawStrokePhase"/>, as a multiple of the
        /// hull's oar power: <c>average × ((1 − depth) + depth × shape / mean shape)</c>, where the shape is
        /// a half sine over the drive (the blade in the water, phase 0 to 0.5) and 0 through the recovery
        /// and the rest. Its mean over one stroke of raw time is exactly <see cref="AverageThrustScale"/>,
        /// whatever the depth; it is continuous through the wrap.
        /// </summary>
        public float ThrustMultiplier(float rawStrokePhase)
        {
            if (SurgeDepth <= 0f || MeanBladeShape <= 0f) return AverageThrustScale;
            float shape = BladeShape(WarpStrokePhase(rawStrokePhase));
            return AverageThrustScale * ((1f - SurgeDepth) + SurgeDepth * shape / MeanBladeShape);
        }

        /// <summary>The blade's drive at drawn stroke phase <paramref name="strokePhase"/>: a half sine
        /// over the drive (0 at the catch and the release, 1 mid-pull), 0 with the blade out.</summary>
        public static float BladeShape(float strokePhase)
        {
            float p = OarStrokeMath.Wrap01(strokePhase);
            return p < OarStrokeMath.ReleasePhase ? Mathf.Sin(Mathf.PI * p / OarStrokeMath.ReleasePhase) : 0f;
        }

        /// <summary>The style a character the game rows for uses: its own, else the game's NPC default.</summary>
        public static RowingStyleDef ChooseForNpc(RowingStyleDef characterStyle, GameConfig config)
            => characterStyle != null ? characterStyle : (config != null ? config.NpcRowingStyle : null);

        /// <summary>The style the player rows: the game's player setting (null = unstyled).</summary>
        public static RowingStyleDef ChooseForPlayer(GameConfig config)
            => config != null ? config.PlayerRowingStyle : null;

        private static float Remap(float s, float a, float b, float pa, float pb)
            => pa + (s - a) / (b - a) * (pb - pa);
    }
}
