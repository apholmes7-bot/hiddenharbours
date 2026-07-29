using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The shared wave field <b>as the shader receives it</b> — the packed shader-global payload
    /// <see cref="WaveFieldBridge"/> publishes, in one value.
    ///
    /// <para><b>Why this type exists (ADR 0027 P2 / the ADR 0018 amendment).</b> The seam used to be
    /// six loose <c>Vector4</c>s threaded through every signature that reads the sea. At four trains
    /// that was merely verbose; at <see cref="WaveTrains.MaxTrains"/> = 8 it is eleven, and
    /// <c>DisplacedWaterMath.WatertightZHeaveMeters</c> would have carried sixteen parameters. The
    /// real cost was never the typing: it was that <b>every reader had to know the width</b>, so
    /// widening the field meant editing every signature in the chain and any one of them could be
    /// missed. A miss is not a compile error in the usual case — it is a reader that quietly sees a
    /// NARROWER sea than the shader draws, which is precisely the class of defect ADR 0023's
    /// <c>freqScale</c> incident was (the clamp scanned a sea that was not on screen, and hulls
    /// flooded). One value, one width, one place to change it.</para>
    ///
    /// <para><b>The layout</b> (mirrored by the HLSL uniforms of the same names):
    /// <list type="bullet">
    /// <item><c>_WaveTrain0.._WaveTrain7</c> — xy = unit travel direction, z = wave number
    ///   <c>k = 2π/λ</c> (precomputed so the shader never divides by a wavelength), w = amplitude
    ///   (metres). Slots at or beyond <see cref="Count"/> are zero.</item>
    /// <item><c>_WavePhases</c> / <c>_WavePhases2</c> — the per-train phase (radians), trains 0–3 and
    ///   4–7. Accumulated in C# <c>double</c> and wrapped to [0, 2π) before the float cast, so the
    ///   unbounded game time never reaches float trig on the GPU.</item>
    /// <item><c>_WaveFieldParams</c> — x = live train count (0 = nothing published → the shader's
    ///   legacy noise-swell look holds), y = crest sharpening p, z = total amplitude (the
    ///   crest-factor normalizer), <b>w = the dominant (spectral-peak) slot index</b>.</item>
    /// </list>
    /// ⚠️ <c>.w</c> was <c>reserved</c> and published as 0. It now carries
    /// <see cref="WaveTrains.DominantIndex"/> — which is still 0 under the flat weighting, so the
    /// published bytes are unchanged until the spectrum lands (the P2 PR-A passthrough contract).</para>
    ///
    /// <para>A <c>readonly struct</c> passed by <c>in</c>: allocation-free on the per-hull,
    /// per-tick clamp path (rule 7).</para>
    /// </summary>
    public readonly struct PackedWaveField
    {
        /// <summary>Slot capacity — the seam's width, tied to the Core container so the two can
        /// never disagree.</summary>
        public const int MaxTrains = WaveTrains.MaxTrains;

        private readonly Vector4 _train0;
        private readonly Vector4 _train1;
        private readonly Vector4 _train2;
        private readonly Vector4 _train3;
        private readonly Vector4 _train4;
        private readonly Vector4 _train5;
        private readonly Vector4 _train6;
        private readonly Vector4 _train7;

        /// <summary>Phases for trains 0–3 (the <c>_WavePhases</c> uniform).</summary>
        public readonly Vector4 PhasesLow;

        /// <summary>Phases for trains 4–7 (the <c>_WavePhases2</c> uniform).</summary>
        public readonly Vector4 PhasesHigh;

        /// <summary>(count, crestSharpening, totalAmplitude, dominantIndex) — the
        /// <c>_WaveFieldParams</c> uniform.</summary>
        public readonly Vector4 Params;

        public PackedWaveField(Vector4 train0, Vector4 train1, Vector4 train2, Vector4 train3,
                               Vector4 train4, Vector4 train5, Vector4 train6, Vector4 train7,
                               Vector4 phasesLow, Vector4 phasesHigh, Vector4 fieldParams)
        {
            _train0 = train0;
            _train1 = train1;
            _train2 = train2;
            _train3 = train3;
            _train4 = train4;
            _train5 = train5;
            _train6 = train6;
            _train7 = train7;
            PhasesLow = phasesLow;
            PhasesHigh = phasesHigh;
            Params = fieldParams;
        }

        /// <summary>The packed vector for a slot (xy = direction, z = k, w = amplitude). Out-of-range
        /// reads return zero — a dead slot, which every consumer already treats as silent — because a
        /// throw here would be a crash in a per-pixel-equivalent hot path, and the HLSL twin it
        /// mirrors cannot throw at all.</summary>
        public Vector4 Train(int index) => index switch
        {
            0 => _train0,
            1 => _train1,
            2 => _train2,
            3 => _train3,
            4 => _train4,
            5 => _train5,
            6 => _train6,
            7 => _train7,
            _ => Vector4.zero,
        };

        /// <summary>The published phase (radians, pre-wrapped) for a slot; 0 outside the range.</summary>
        public float Phase(int index) => index switch
        {
            0 => PhasesLow.x,
            1 => PhasesLow.y,
            2 => PhasesLow.z,
            3 => PhasesLow.w,
            4 => PhasesHigh.x,
            5 => PhasesHigh.y,
            6 => PhasesHigh.z,
            7 => PhasesHigh.w,
            _ => 0f,
        };

        /// <summary>Live train count — the shader's <c>(int)(_WaveFieldParams.x + 0.5)</c>, character
        /// for character.</summary>
        public int Count => (int)(Params.x + 0.5f);

        /// <summary>Crest sharpening p, floored at 1 exactly as the shader floors it.</summary>
        public float CrestSharpening => Mathf.Max(Params.y, 1f);

        /// <summary>The amplitude envelope (metres) — the crest-factor normalizer. 0 = dead glass.</summary>
        public float TotalAmplitude => Params.z;

        /// <summary>The spectral-peak slot: whose face sign drives the whitecap lifecycle and whose
        /// phase the rocking consumers read. See <see cref="WaveTrains.DominantIndex"/>.</summary>
        public int DominantIndex => (int)(Params.w + 0.5f);

        /// <summary>The unset field — no trains, all zeros: the "no bridge → legacy look" convention
        /// (the <c>_DayNightTint</c>/<c>_MoonDir</c> discipline).</summary>
        public static readonly PackedWaveField Empty = default;
    }
}
