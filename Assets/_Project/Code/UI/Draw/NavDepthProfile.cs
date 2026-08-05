using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The MAX face's <b>DEPTH AHEAD</b> strip, sampled from the survey and cached per line
    /// (navRig.js:446-461).
    ///
    /// <para><b>What the strip profiles is the rig's decision, not a choice.</b> The source samples
    /// <c>depthAt</c> along the OWN-SHIP HEADING for <c>rangeNM * 1.3</c> (js:449-450) and titles the
    /// pane "DEPTH AHEAD" (js:459). It is not the route's next leg and not the measure line — those
    /// have their own readouts in the manager column. The strip answers one question: what is under the
    /// water I am about to sail into.</para>
    ///
    /// <para><b>One survey, two readers.</b> Every sample comes from
    /// <see cref="NavChartSource.DepthAt"/> — the same array the chart shades and the top bar's DPTH
    /// field prints. The strip therefore cannot disagree with the water drawn above it, which is the
    /// invariant that made the survey a shared object in the first place.</para>
    ///
    /// <para><b>Re-sampled per LINE, never per frame</b> (rule 7). The line is
    /// (origin, heading, range, survey) and the key is quantized to what the strip can actually show:
    /// the origin to a whole CHART PIXEL — the own-ship quantum the repaint guard already uses — and
    /// the heading to a whole degree. A boat holding a course therefore re-samples at a bounded rate,
    /// and a repaint driven by something else entirely (a selection moving in the manager, a layer
    /// switching) does not re-sample at all. <see cref="BakeCount"/> is the seam that proves it rather
    /// than the comment claiming it.</para>
    ///
    /// <para><b>No survey ⇒ no profile</b>, the chart's own honesty rule. Samples off the survey come
    /// back <see cref="float.NaN"/> and stay NaN: the strip draws a gap over that span rather than a
    /// confident seabed at zero, and a line with no usable sample at all reports
    /// <see cref="HasProfile"/> false so the pane can say so in words.</para>
    /// </summary>
    public sealed class NavDepthProfile
    {
        /// <summary>How far ahead the strip looks, as a multiple of the chart's range
        /// (navRig.js:449 <c>aheadNM = rangeNM * 1.3</c>).</summary>
        public const float AheadRangeMultiple = 1.3f;

        /// <summary>Most samples taken across the strip (navRig.js:449 <c>min(Pr.w, 120)</c>). The
        /// strip is never wider than a few hundred pixels, and a chart bands its depths anyway.</summary>
        public const int MaxSamples = 120;

        private float[] _depths = System.Array.Empty<float>();
        private int _count;
        private float _maxDepth;
        private bool _hasProfile;

        // The line this cache holds, quantized. int.MinValue is "nothing sampled yet" — a real
        // quantized heading is [0, 359] and a real generation is positive.
        private int _keyGeneration = int.MinValue;
        private long _keyOrigin;
        private int _keyHeading = int.MinValue;
        private float _keyRangeNM;
        private int _keySamples;
        private bool _keyHasBoat;

        /// <summary>How many times the line has actually been re-sampled. The perf guard's evidence:
        /// it must stay far below the repaint count while sailing, and must not move at all for a
        /// repaint that did not move the line.</summary>
        public int BakeCount { get; private set; }

        /// <summary>Samples along the line, nearest first. Datum depth in metres, or
        /// <see cref="float.NaN"/> where the line runs off the survey. Caller-owned — read, never
        /// keep.</summary>
        public float[] Depths => _depths;

        /// <summary>How many entries of <see cref="Depths"/> are live.</summary>
        public int Count => _count;

        /// <summary>The strip's depth axis maximum, in metres — the rig's
        /// <c>max(6, ceil(maxD/2)*2)</c> (js:451), so the seabed never touches the bottom of the pane
        /// and the axis label is always an even number.</summary>
        public float MaxDepth => _maxDepth;

        /// <summary>True iff at least one sample landed on the survey. False is the honest "no
        /// profile" the pane prints in words.</summary>
        public bool HasProfile => _hasProfile;

        /// <summary>
        /// Re-sample the line if it has moved, and report whether a sample actually happened.
        ///
        /// <para><paramref name="samples"/> is the strip's width clamped by the caller; a strip too
        /// narrow to hold two samples has no profile to draw and is refused rather than divided by
        /// zero.</para>
        /// </summary>
        public bool EnsureBaked(NavChartSource chart, Vector2 origin, bool hasBoat, float headingDeg,
                                float rangeNM, int samples)
        {
            samples = Mathf.Clamp(samples, 0, MaxSamples);
            int generation = chart != null ? chart.Generation : 0;
            long quantOrigin = ChartplotterOverlayLayout.QuantizePosition(origin, rangeNM);
            int quantHeading = Mathf.RoundToInt(NavMath.Norm360(headingDeg)) % 360;

            if (_keyGeneration == generation && _keyOrigin == quantOrigin && _keyHeading == quantHeading
                && _keyRangeNM.Equals(rangeNM) && _keySamples == samples && _keyHasBoat == hasBoat)
                return false;

            _keyGeneration = generation;
            _keyOrigin = quantOrigin;
            _keyHeading = quantHeading;
            _keyRangeNM = rangeNM;
            _keySamples = samples;
            _keyHasBoat = hasBoat;

            Sample(chart, origin, hasBoat, headingDeg, rangeNM, samples);
            BakeCount++;
            return true;
        }

        private void Sample(NavChartSource chart, Vector2 origin, bool hasBoat, float headingDeg,
                            float rangeNM, int samples)
        {
            _count = 0;
            _maxDepth = 6f;
            _hasProfile = false;
            if (chart == null || !hasBoat || samples < 2) return;

            if (_depths.Length < samples) _depths = new float[samples];

            Vector2 ahead = NavMath.DirectionFromBearing(headingDeg);
            float lengthMetres = NavMath.NMToMetres((rangeNM > 0f ? rangeNM : 0.05f) * AheadRangeMultiple);

            float deepest = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)(samples - 1);
                Vector2 at = origin + ahead * (t * lengthMetres);
                float d = chart.DepthAt(at);
                if (float.IsNaN(d))
                {
                    _depths[i] = float.NaN;                 // off the survey — a gap, not a zero
                    continue;
                }
                // Ground above datum profiles as zero depth: it IS the seabed breaking surface, which
                // is what the strip is for. The rig's own max(0, depthAt) (js:450).
                d = Mathf.Max(0f, d);
                _depths[i] = d;
                _hasProfile = true;
                if (d > deepest) deepest = d;
            }

            _count = samples;
            // The rig's axis rule (js:451): never shallower than 6 m, always an even number of metres.
            _maxDepth = Mathf.Max(6f, Mathf.Ceil(deepest / 2f) * 2f);
        }
    }
}
