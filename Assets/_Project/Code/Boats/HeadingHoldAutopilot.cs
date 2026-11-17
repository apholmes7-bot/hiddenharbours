using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>Why the autopilot last handed the helm back (or refused to take it).</summary>
    public enum AutopilotDisengage
    {
        None = 0,
        /// <summary>The player flicked it off.</summary>
        Toggled = 1,
        /// <summary>A steer key, the stick or the wheel took the helm back.</summary>
        SteerInput = 2,
        /// <summary>Nobody is at the helm any more (left it, stopped, region change).</summary>
        LeftHelm = 3,
        /// <summary>Her keel is on the bottom.</summary>
        Aground = 4,
        /// <summary>She hit something harder than a fender touch.</summary>
        Collision = 5,
        /// <summary>Too little way ahead for the rudder to bite (or going astern).</summary>
        NoWay = 6,
        /// <summary>This hull's Def carries no autopilot.</summary>
        NotFitted = 7,
        /// <summary>The hull under the controller changed.</summary>
        HullChanged = 8,
    }

    /// <summary>What the autopilot reads each fixed step: every input is a plain value, so the rules
    /// and the controller are provable without a scene.</summary>
    public readonly struct AutopilotSensors
    {
        public readonly bool Fitted;
        public readonly bool Manned;
        public readonly bool Aground;
        public readonly bool Collided;
        /// <summary>The body's rotation in degrees, counter-clockwise positive.</summary>
        public readonly float HeadingDegrees;
        /// <summary>The body's angular velocity in degrees per second, counter-clockwise positive.</summary>
        public readonly float YawRateDegreesPerSecond;
        /// <summary>Forward speed through the water (m/s); negative going astern.</summary>
        public readonly float WayMetresPerSecond;

        public AutopilotSensors(bool fitted, bool manned, bool aground, bool collided,
                                float headingDegrees, float yawRateDegreesPerSecond, float wayMetresPerSecond)
        {
            Fitted = fitted;
            Manned = manned;
            Aground = aground;
            Collided = collided;
            HeadingDegrees = headingDegrees;
            YawRateDegreesPerSecond = yawRateDegreesPerSecond;
            WayMetresPerSecond = wayMetresPerSecond;
        }
    }

    /// <summary>
    /// <b>The autopilot's heading hold</b>: the rules that let it take and keep the helm, and the
    /// controller that steers while it has it. A POCO owned by <see cref="BoatController"/> and stepped
    /// from its fixed physics step, before the drive reads the helm. It writes nothing but a helm value
    /// in −1..1, the same value a steer key writes, so the hull's own way-scaled rudder does all the
    /// turning (no new physics).
    ///
    /// <para><b>Deterministic.</b> The output is a pure function of the state, the sensors, the step
    /// and the settings: no clock, no randomness, no frame-rate term. The state is transient and never
    /// saved, so a loaded game starts disengaged.</para>
    ///
    /// <para><b>The controller.</b> A PID on the heading error (the heading minus the held heading,
    /// wrapped to ±180°): proportional, plus a damping term on the yaw rate, plus a clamped integral
    /// for a steady offset. The command is clamped to <see cref="HelmAutopilotSettings.MaxHelm"/> and
    /// slewed at <see cref="HelmAutopilotSettings.HelmSlewPerSecond"/>, starting from the helm she had
    /// when it engaged, so taking over never jerks the wheel.</para>
    /// </summary>
    public sealed class HeadingHoldAutopilot
    {
        private float _integral;
        private float _helm;

        /// <summary>Is the autopilot holding the helm?</summary>
        public bool Engaged { get; private set; }

        /// <summary>The heading being held, degrees counter-clockwise (meaningful while engaged).</summary>
        public float TargetHeadingDegrees { get; private set; }

        /// <summary>The helm the autopilot last commanded (−1..1).</summary>
        public float Helm => _helm;

        /// <summary>Why it last let go or refused; <see cref="AutopilotDisengage.None"/> while engaged or
        /// before it was ever used.</summary>
        public AutopilotDisengage LastDisengage { get; private set; }

        /// <summary>
        /// Take the helm and hold the heading she is on. Refused (and the reason kept in
        /// <see cref="LastDisengage"/>) under the same rules that would hand it straight back: no
        /// autopilot on this hull, nobody at the helm, aground, or too little way. A collision flag is
        /// not a reason to refuse: a knock before the press is history.
        /// </summary>
        public bool TryEngage(in AutopilotSensors sensors, float currentHelm, in HelmAutopilotSettings cfg)
        {
            AutopilotSensors noKnock = new AutopilotSensors(sensors.Fitted, sensors.Manned, sensors.Aground,
                false, sensors.HeadingDegrees, sensors.YawRateDegreesPerSecond, sensors.WayMetresPerSecond);
            AutopilotDisengage refusal = HandBackReason(in noKnock, in cfg);
            if (refusal != AutopilotDisengage.None)
            {
                Engaged = false;
                LastDisengage = refusal;
                return false;
            }

            Engaged = true;
            LastDisengage = AutopilotDisengage.None;
            TargetHeadingDegrees = sensors.HeadingDegrees;
            _integral = 0f;
            _helm = Mathf.Clamp(currentHelm, -1f, 1f);
            return true;
        }

        /// <summary>Hand the helm back. A no-op (the first reason stands) while already disengaged.</summary>
        public void Disengage(AutopilotDisengage reason)
        {
            if (!Engaged) return;
            Engaged = false;
            LastDisengage = reason;
            _integral = 0f;
        }

        /// <summary>
        /// The rules that end a hold, in priority order. Pure + static: the truth table is a test.
        /// <see cref="AutopilotDisengage.None"/> means the autopilot may keep (or take) the helm.
        /// </summary>
        public static AutopilotDisengage HandBackReason(in AutopilotSensors s, in HelmAutopilotSettings cfg)
        {
            if (!s.Fitted) return AutopilotDisengage.NotFitted;
            if (!s.Manned) return AutopilotDisengage.LeftHelm;
            if (s.Aground) return AutopilotDisengage.Aground;
            if (s.Collided) return AutopilotDisengage.Collision;
            if (s.WayMetresPerSecond < cfg.MinWayMetresPerSecond) return AutopilotDisengage.NoWay;
            return AutopilotDisengage.None;
        }

        /// <summary>Does a steer input this hard take the helm back? Pure + static.</summary>
        public static bool IsSteerOverride(float steerInput, in HelmAutopilotSettings cfg)
            => steerInput != 0f && Mathf.Abs(steerInput) >= cfg.SteerOverrideThreshold;

        /// <summary>Does a knock this hard end the hold? Pure + static.</summary>
        public static bool IsHazardousImpact(float relativeSpeedMetresPerSecond, in HelmAutopilotSettings cfg)
            => relativeSpeedMetresPerSecond >= cfg.CollisionMinImpactMetresPerSecond;

        /// <summary>
        /// One fixed step. Returns true with the helm to write while the autopilot holds; returns false
        /// (and writes nothing) when it is off or the rules ended the hold this step, in which case the
        /// helm is the player's again.
        /// </summary>
        public bool Tick(in AutopilotSensors sensors, float dt, in HelmAutopilotSettings cfg, out float helm)
        {
            helm = 0f;
            if (!Engaged) return false;

            AutopilotDisengage reason = HandBackReason(in sensors, in cfg);
            if (reason != AutopilotDisengage.None)
            {
                Disengage(reason);
                return false;
            }

            _helm = Step(TargetHeadingDegrees, sensors.HeadingDegrees, sensors.YawRateDegreesPerSecond,
                         dt, in cfg, ref _integral, _helm);
            helm = _helm;
            return true;
        }

        /// <summary>
        /// The controller law, one step. Pure + static apart from the integral and the previous helm it
        /// is handed, so the same inputs give the same bits on every machine.
        /// </summary>
        public static float Step(float targetDegrees, float headingDegrees, float yawRateDegreesPerSecond,
                                 float dt, in HelmAutopilotSettings cfg, ref float integral, float previousHelm)
        {
            float error = Mathf.DeltaAngle(targetDegrees, headingDegrees);   // heading − target, ±180
            float iLimit = Mathf.Max(0f, cfg.IntegralLimitHelm);
            integral = Mathf.Clamp(integral + cfg.IntegralGainPerDegreeSecond * error * dt, -iLimit, iLimit);

            float raw = cfg.HeadingGainPerDegree * error
                      + cfg.YawRateGainPerDegreePerSecond * yawRateDegreesPerSecond
                      + integral;
            float max = Mathf.Clamp01(cfg.MaxHelm);
            float wanted = Mathf.Clamp(raw, -max, max);

            return cfg.HelmSlewPerSecond > 0f
                ? Mathf.MoveTowards(previousHelm, wanted, cfg.HelmSlewPerSecond * dt)
                : wanted;
        }
    }
}
