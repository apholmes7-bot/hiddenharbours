using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Taking and leaving a helm, as a short move instead of a one-frame snap.</b> The switcher still
    /// re-seats the player in one frame (the gameplay root, the controls and the camera are unchanged);
    /// this is the DRAWN figure catching up, over <see cref="HelmTransitionSettings"/>' times.
    ///
    /// <para><b>Two weights, both eased (zero speed at each end, so neither end pops):</b></para>
    /// <list type="bullet">
    /// <item><b>Root</b>: the figure's place and facing on the deck, from where he was DRAWN when the hop
    /// came to where the station puts him. Taking, it runs first (he steps to the station and turns to
    /// face it); leaving, it runs last (he stands, then steps off).</item>
    /// <item><b>Body</b>: his pose, from the pose he was drawn in to the live pose of where he is going
    /// (seated at the tiller with his hand on the grip, hands on the wheel, or standing balanced on
    /// deck). Each pose is taken relative to its own root, so the root carries the body.</item>
    /// </list>
    ///
    /// <para><b>Interruptible.</b> A new hop mid-move starts from the pose and place drawn on that frame,
    /// so a press, a change of mind and a press again never jump. Pure state and arithmetic: no
    /// allocation, no randomness, nothing read from the sim (rule 5).</para>
    /// </summary>
    public sealed class HelmTransition
    {
        /// <summary>True while a move is drawing.</summary>
        public bool Active { get; private set; }

        /// <summary>True for a take (deck to helm), false for a leave.</summary>
        public bool Taking { get; private set; }

        /// <summary>The move's length, seconds.</summary>
        public float DurationSeconds { get; private set; }

        /// <summary>When it started, on the caller's clock.</summary>
        public double StartSeconds { get; private set; }

        /// <summary>Where the figure was drawn when the move began, in the hull's rig metres.</summary>
        public Vector3 FromRootLocal { get; private set; }

        /// <summary>The yaw the figure was drawn with when the move began, degrees.</summary>
        public float FromRootYawDegrees { get; private set; }

        /// <summary>The settings' split, held for the move.</summary>
        public float RootShare { get; private set; }
        public float BodyStart { get; private set; }

        /// <summary>The weights last sampled (1, 1 when idle).</summary>
        public float RootWeight { get; private set; } = 1f;
        public float BodyWeight { get; private set; } = 1f;

        /// <summary>Which hop a control change is, for the figure: a take, a leave or neither.</summary>
        public static int HopBetween(ControlMode from, ControlMode to)
        {
            if (from == ControlMode.OnDeck && to == ControlMode.Aboard) return +1;
            if (from == ControlMode.Aboard && to == ControlMode.OnDeck) return -1;
            return 0;
        }

        /// <summary>Start a move. False (and idle) when it should snap instead: no time, or too far.</summary>
        public bool Begin(bool taking, double nowSeconds, in HelmTransitionSettings settings, bool seated,
                          Vector3 fromRootLocal, float fromRootYawDegrees, Vector3 toRootLocal)
        {
            float seconds = settings.SecondsFor(taking, seated);
            Vector3 travel = toRootLocal - fromRootLocal;
            travel.z = 0f;   // a seat is higher than the sole: only the step across the deck counts
            if (seconds <= 0f || travel.magnitude > settings.MaxTravelMeters)
            {
                Cancel();
                return false;
            }
            Active = true;
            Taking = taking;
            DurationSeconds = seconds;
            StartSeconds = nowSeconds;
            FromRootLocal = fromRootLocal;
            FromRootYawDegrees = fromRootYawDegrees;
            RootShare = Mathf.Clamp(settings.RootShare, 0.05f, 1f);
            BodyStart = Mathf.Clamp(settings.BodyStart, 0f, 0.95f);
            RootWeight = 0f;
            BodyWeight = 0f;
            return true;
        }

        /// <summary>Stop drawing a move: the figure is drawn at its live pose from now on.</summary>
        public void Cancel()
        {
            Active = false;
            RootWeight = 1f;
            BodyWeight = 1f;
        }

        /// <summary>Sample the move at <paramref name="nowSeconds"/>. True while it is still drawing;
        /// at its end (or a clock run backwards past its start, treated as its start) it goes idle and
        /// answers false with both weights 1.</summary>
        public bool Sample(double nowSeconds)
        {
            if (!Active) return false;
            float t = DurationSeconds > 0f
                ? (float)((nowSeconds - StartSeconds) / DurationSeconds)
                : 1f;
            if (t >= 1f)
            {
                Cancel();
                return false;
            }
            Phase(Taking, Mathf.Max(0f, t), RootShare, BodyStart, out float root, out float body);
            RootWeight = root;
            BodyWeight = body;
            return true;
        }

        /// <summary>The figure's root this sample: from where he was drawn toward <paramref name="toLocal"/>
        /// and <paramref name="toYawDegrees"/> by the root weight (the shorter way round).</summary>
        public void Root(Vector3 toLocal, float toYawDegrees, out Vector3 local, out float yawDegrees)
        {
            local = Vector3.LerpUnclamped(FromRootLocal, toLocal, RootWeight);
            yawDegrees = FromRootYawDegrees + Mathf.DeltaAngle(FromRootYawDegrees, toYawDegrees) * RootWeight;
        }

        /// <summary>
        /// The two weights at normalised time <paramref name="t"/> (0..1). Taking: the root runs over
        /// [0, rootShare] and the body over [bodyStart, 1]. Leaving: the body over [0, 1 - bodyStart] and
        /// the root over [1 - rootShare, 1]. Each eased by <see cref="Ease"/>. PURE.
        /// </summary>
        public static void Phase(bool taking, float t, float rootShare, float bodyStart,
                                 out float root, out float body)
        {
            t = Mathf.Clamp01(t);
            rootShare = Mathf.Clamp(rootShare, 0.05f, 1f);
            bodyStart = Mathf.Clamp(bodyStart, 0f, 0.95f);
            if (taking)
            {
                root = Ease(t / rootShare);
                body = Ease((t - bodyStart) / (1f - bodyStart));
            }
            else
            {
                body = Ease(t / (1f - bodyStart));
                root = Ease((t - (1f - rootShare)) / rootShare);
            }
        }

        /// <summary>Smoothstep on a clamped input: 0 and 1 at the ends with zero slope at both, so a move
        /// leaves and arrives at rest.</summary>
        public static float Ease(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }
    }
}
