using System;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// The owner's dials for the OAR SPLASH (owner ruling 2026-11-13: "oars that MUST splash and
    /// foam"). Serialized on the hull's <see cref="BoatVisualDef"/> beside the oar fittings it
    /// dresses, so the dory's splash is tuned on the dory's own asset and a second rowed hull can
    /// carry different numbers (rules 2 + 6). <see cref="Default"/> is the shipped feel.
    /// </summary>
    [Serializable]
    public struct OarSplashConfig
    {
        [Tooltip("Master switch. Off = the oars row dry, exactly as before this effect existed.")]
        public bool Enabled;

        [Header("The blade (rig facts — read off the oar rig, not feel)")]
        [Tooltip("Oarlock to blade TIP along the loom (m). The dory rig's outboard length is 1.45. " +
                 "With the oarlock's height above the waterline this decides WHEN the blade is in " +
                 "the water, so the splash lands on the frame the drawn blade touches down.")]
        [Min(0.1f)] public float BladeReachMeters;
        [Tooltip("Oarlock to blade CENTRE along the loom (m): where the splash and the foam are " +
                 "laid. The rig's blade is 0.5 m long, so its middle sits 1.2 m out.")]
        [Min(0.1f)] public float SplashReachMeters;

        [Header("How hard a stroke splashes")]
        [Tooltip("Strength of the gentlest working stroke (0..1). A light pull still makes a small splash.")]
        [Range(0f, 1f)] public float MinStrength;
        [Tooltip("How much of the strength comes from BOAT SPEED rather than the pull itself (0..1). " +
                 "A blade dropped into water already rushing past throws more than one at rest.")]
        [Range(0f, 1f)] public float SpeedShare;
        [Tooltip("Boat speed (m/s) at which the speed share is fully in.")]
        [Min(0.01f)] public float SpeedForFullSplash;

        [Header("The catch — droplets thrown as the blade enters")]
        [Tooltip("Droplets thrown by the gentlest catch.")]
        [Min(0)] public int CatchDropsMin;
        [Tooltip("Droplets thrown by a full-strength catch.")]
        [Min(0)] public int CatchDropsMax;
        [Tooltip("Launch speed of a droplet at full strength (m/s).")]
        [Min(0f)] public float DropletSpeed;
        [Tooltip("Seconds a droplet lives.")]
        [Min(0.05f)] public float DropletLifetime;
        [Tooltip("Droplet size at birth (m).")]
        [Min(0.01f)] public float DropletSize;
        [Tooltip("Per-second retention of a droplet's launch speed (0..1). Low = it stops short.")]
        [Range(0f, 1f)] public float DropletVelocityRetain;
        [Tooltip("Foam puffs laid at the catch itself, under the droplets.")]
        [Min(0)] public int CatchFoamPuffs;

        [Header("The pull — foam left where the blade works")]
        [Tooltip("Foam puffs per second laid at the blade while it is in the water, at full strength.")]
        [Min(0f)] public float FoamPuffsPerSecond;
        [Tooltip("Seconds a foam puff lives before it has faded out.")]
        [Min(0.05f)] public float FoamLifetime;
        [Tooltip("Foam puff size at birth (m).")]
        [Min(0.01f)] public float FoamSize;
        [Tooltip("Foam opacity at birth (0..1).")]
        [Range(0f, 1f)] public float FoamStartAlpha;
        [Tooltip("Fade shaping: 1 = linear, above 1 = stays bright then drops late.")]
        [Min(0.01f)] public float FoamFadePower;
        [Tooltip("How much a puff grows over its life (1 = none, 2 = doubles).")]
        [Min(1f)] public float FoamSpreadFactor;
        [Tooltip("How far a puff is scattered about the blade (m), so the puddle is a patch and not a dot.")]
        [Min(0f)] public float FoamScatterMeters;
        [Tooltip("Speed (m/s) the blade shoves its foam the way it is sweeping, at full strength.")]
        [Min(0f)] public float FoamPushSpeed;
        [Tooltip("Per-second retention of that shove (0..1). After it dies only the tide's drift is left.")]
        [Range(0f, 1f)] public float FoamVelocityRetain;

        [Header("The release — the blade leaving the water")]
        [Tooltip("Droplets flicked off by the gentlest release.")]
        [Min(0)] public int ReleaseDropsMin;
        [Tooltip("Droplets flicked off by a full-strength release.")]
        [Min(0)] public int ReleaseDropsMax;
        [Tooltip("Launch speed of a release droplet at full strength (m/s). Thrown the way the blade " +
                 "was sweeping, so they leave aft on a forward pull.")]
        [Min(0f)] public float ReleaseDropletSpeed;

        [Header("The puddle — the swirl a blade leaves behind")]
        [Tooltip("Foam puffs laid in a ring where the blade came out: the little whirlpool a rower " +
                 "leaves on the water. 0 = no puddle.")]
        [Min(0)] public int SwirlPuffs;
        [Tooltip("Radius of that ring of foam (m), on the water.")]
        [Min(0f)] public float SwirlRadiusMeters;
        [Tooltip("How fast the puddle turns at full strength (m/s at its rim). It slows as the foam's " +
                 "own push dies (Foam Velocity Retain).")]
        [Min(0f)] public float SwirlSpinSpeed;
        [Tooltip("How far the puddle opens out as it turns, as a share of its spin (0..1).")]
        [Range(0f, 1f)] public float SwirlOpenShare;
        [Tooltip("A puddle puff's life, as a multiple of Foam Lifetime. Above 1 it outlasts the pull foam.")]
        [Min(0.1f)] public float SwirlLifetimeScale;
        [Tooltip("A puddle puff's birth size, as a multiple of Foam Size.")]
        [Min(0.1f)] public float SwirlSizeScale;

        [Header("Ripples — rings spreading from where the water is broken")]
        [Tooltip("Rings sent out where a blade goes in (each one fainter and later than the one before).")]
        [Min(0)] public int RipplesAtCatch;
        [Tooltip("Rings sent out where a blade comes out.")]
        [Min(0)] public int RipplesAtRelease;
        [Tooltip("Seconds between one ring of a set and the next.")]
        [Min(0f)] public float RippleStaggerSeconds;
        [Tooltip("Opacity of each later ring of a set, as a share of the one before (0..1).")]
        [Range(0f, 1f)] public float RippleEchoFade;
        [Tooltip("Ring radius when it appears (m).")]
        [Min(0f)] public float RippleStartRadius;
        [Tooltip("Ring radius when it has faded out (m), at full strength.")]
        [Min(0.01f)] public float RippleEndRadius;
        [Tooltip("Seconds a ring lives as it spreads and fades.")]
        [Min(0.05f)] public float RippleLifetime;
        [Tooltip("A ring from the gentlest stroke spreads to this share of Ripple End Radius (0..1); " +
                 "a full-strength one to all of it.")]
        [Range(0.1f, 1f)] public float RippleGentleSizeShare;
        [Tooltip("Ring opacity at birth (0..1), at full strength.")]
        [Range(0f, 1f)] public float RippleStartAlpha;
        [Tooltip("Fade shaping: 1 = linear, above 1 = stays bright then drops late.")]
        [Min(0.01f)] public float RippleFadePower;
        [Tooltip("Spread shaping: 1 = steady, above 1 = runs out fast and slows, as a real ripple does.")]
        [Min(0.01f)] public float RippleSpreadEase;
        [Tooltip("Where the ring's bright band sits, as a share of the ring picture's radius (0..1).")]
        [Range(0.1f, 1f)] public float RippleBandCentre;
        [Tooltip("Half-width of the bright band, as a share of the ring picture's radius. Smaller = a finer line.")]
        [Range(0.02f, 0.5f)] public float RippleBandHalfWidth;
        [Tooltip("Ring tint. Alpha is driven by the fade.")]
        public Color RippleColor;

        [Header("Drips — water running off the blade on the recovery")]
        [Tooltip("Drips per second off a blade just out of the water, at full strength. 0 = no drips.")]
        [Min(0f)] public float DripsPerSecond;
        [Tooltip("Seconds for the dripping to fall to about a third: the blade drains as it swings forward.")]
        [Min(0.01f)] public float DripDecaySeconds;
        [Tooltip("Drip size (m) as it falls.")]
        [Min(0.005f)] public float DripSize;
        [Tooltip("How far along the blade a drip may leave from (m), either side of the tip's mark.")]
        [Min(0f)] public float DripScatterMeters;
        [Tooltip("The ring a landing drip makes, as a share of a blade ripple's size.")]
        [Range(0.05f, 1f)] public float DripRingScale;
        [Tooltip("The same ring's life, as a share of a blade ripple's life.")]
        [Range(0.05f, 1f)] public float DripRingLifetimeScale;
        [Tooltip("The same ring's opacity, as a share of a blade ripple's.")]
        [Range(0f, 1f)] public float DripRingAlphaScale;

        [Header("Variation")]
        [Tooltip("Plus or minus variation in a mote's lifetime (0..1 fraction).")]
        [Range(0f, 1f)] public float LifetimeJitter;
        [Tooltip("Plus or minus variation in a mote's birth size (0..1 fraction).")]
        [Range(0f, 1f)] public float SizeJitter;

        [Header("Pools and drawing")]
        [Tooltip("Foam puffs alive at once for this boat, both oars together. Fixed; the oldest is recycled.")]
        [Min(1)] public int FoamPoolSize;
        [Tooltip("Droplets alive at once for this boat, both oars together. Fixed; the oldest is recycled.")]
        [Min(1)] public int DropletPoolSize;
        [Tooltip("Ripple rings alive at once for this boat (blade rings and drip rings). Fixed; the oldest is recycled.")]
        [Min(1)] public int RipplePoolSize;
        [Tooltip("Falling drips alive at once for this boat. Fixed; the oldest is recycled.")]
        [Min(1)] public int DripPoolSize;
        [Tooltip("Foam tint. Alpha is driven by the fade.")]
        public Color FoamColor;
        [Tooltip("Droplet tint. Alpha is driven by the fade.")]
        public Color DropletColor;
        [Tooltip("Order in the sorting layer: the wake's own band, above the sea and below the hull.")]
        public int SortingOrder;

        public static OarSplashConfig Default => new OarSplashConfig
        {
            Enabled               = true,
            BladeReachMeters      = 1.45f,
            SplashReachMeters     = 1.2f,
            MinStrength           = 0.35f,
            SpeedShare            = 0.35f,
            SpeedForFullSplash    = 2.0f,
            CatchDropsMin         = 3,
            CatchDropsMax         = 8,
            DropletSpeed          = 1.1f,
            DropletLifetime       = 0.4f,
            DropletSize           = 0.07f,
            DropletVelocityRetain = 0.05f,
            CatchFoamPuffs        = 2,
            FoamPuffsPerSecond    = 9f,
            FoamLifetime          = 1.6f,
            FoamSize              = 0.32f,
            FoamStartAlpha        = 0.75f,
            FoamFadePower         = 1.4f,
            FoamSpreadFactor      = 1.7f,
            FoamScatterMeters     = 0.12f,
            FoamPushSpeed         = 0.25f,
            FoamVelocityRetain    = 0.2f,
            ReleaseDropsMin       = 1,
            ReleaseDropsMax       = 4,
            ReleaseDropletSpeed   = 0.6f,
            SwirlPuffs            = 6,
            SwirlRadiusMeters     = 0.13f,
            SwirlSpinSpeed        = 0.3f,
            SwirlOpenShare        = 0.35f,
            SwirlLifetimeScale    = 1.4f,
            SwirlSizeScale        = 0.55f,
            RipplesAtCatch        = 2,
            RipplesAtRelease      = 1,
            RippleStaggerSeconds  = 0.16f,
            RippleEchoFade        = 0.55f,
            RippleStartRadius     = 0.1f,
            RippleEndRadius       = 0.8f,
            RippleLifetime        = 1.4f,
            RippleGentleSizeShare = 0.6f,
            RippleStartAlpha      = 0.5f,
            RippleFadePower       = 0.9f,
            RippleSpreadEase      = 2f,
            RippleBandCentre      = 0.82f,
            RippleBandHalfWidth   = 0.12f,
            RippleColor           = new Color(0.9f, 0.95f, 1f, 1f),
            DripsPerSecond        = 14f,
            DripDecaySeconds      = 0.3f,
            DripSize              = 0.035f,
            DripScatterMeters     = 0.12f,
            DripRingScale         = 0.3f,
            DripRingLifetimeScale = 0.5f,
            DripRingAlphaScale    = 0.6f,
            LifetimeJitter        = 0.2f,
            SizeJitter            = 0.25f,
            FoamPoolSize          = 48,
            DropletPoolSize       = 24,
            RipplePoolSize        = 24,
            DripPoolSize          = 16,
            FoamColor             = new Color(0.92f, 0.96f, 1f, 1f),
            DropletColor          = new Color(0.95f, 0.98f, 1f, 1f),
            SortingOrder          = -1,
        };

        /// <summary>The life curves and jitter the shared <see cref="WakeParticleSystem"/> reads for a foam puff.</summary>
        public WakeConfig FoamWakeConfig()
        {
            WakeConfig c = default;
            c.Lifetime = FoamLifetime;
            c.FoamSize = FoamSize;
            c.StartAlpha = FoamStartAlpha;
            c.FadePower = FoamFadePower;
            c.SpreadFactor = FoamSpreadFactor;
            c.LifetimeJitter = LifetimeJitter;
            c.SizeJitter = SizeJitter;
            c.VelocityDecay = FoamVelocityRetain;
            return c;
        }

        /// <summary>The same, for a thrown droplet: full opacity at birth, no spread.</summary>
        public WakeConfig DropletWakeConfig()
        {
            WakeConfig c = default;
            c.Lifetime = DropletLifetime;
            c.FoamSize = DropletSize;
            c.StartAlpha = 1f;
            c.FadePower = 1f;
            c.SpreadFactor = 1f;
            c.LifetimeJitter = LifetimeJitter;
            c.SizeJitter = SizeJitter;
            c.VelocityDecay = DropletVelocityRetain;
            return c;
        }
    }

    /// <summary>
    /// <b>When an oar blade is in the water, and how hard it splashes</b> — the pure half of the oar
    /// splash. Everything is derived from the pose the oar is ACTUALLY drawn in
    /// (<see cref="DoryOarMeshPose"/>), so the splash cannot drift out of step with the stroke: there
    /// is no second clock and no "frame 2 is the catch" constant to keep in sync with the rig.
    /// </summary>
    public static class DoryOarSplashMath
    {
        /// <summary>
        /// Height of the blade above the still waterline (m): positive in the air, zero or below
        /// in the water. The oar is a straight line from its lock, dipped <paramref name="dipDegrees"/>
        /// below level, so the blade sits <c>reach·sin(dip)</c> under the lock.
        /// </summary>
        public static float BladeHeightMeters(float lockHeightAboveWaterMeters, float reachMeters, float dipDegrees)
            => lockHeightAboveWaterMeters - Mathf.Max(0f, reachMeters) * Mathf.Sin(dipDegrees * Mathf.Deg2Rad);

        /// <summary>True when the blade is at or under the surface.</summary>
        public static bool IsImmersed(float bladeHeightMeters) => bladeHeightMeters <= 0f;

        /// <summary>
        /// The blade's point on the water, in hull-rig metres (x abeam, +starboard; y forward; z up):
        /// out along the oar from its lock, dropped onto the waterline.
        /// </summary>
        public static Vector3 BladeOnWaterLocal(Vector3 lockLocalMeters, int side, in DoryOarMeshPose.Pose pose,
                                                float reachMeters, float waterlineMeters)
        {
            Vector3 p = lockLocalMeters
                        + DoryOarMeshPose.Direction(side, pose.SweepDegrees, pose.DipDegrees) * Mathf.Max(0f, reachMeters);
            p.z = waterlineMeters;
            return p;
        }

        /// <summary>
        /// How hard this stroke works the water, 0..1: the pull's own effort, plus a share for the
        /// boat's speed through the water. Never below <see cref="OarSplashConfig.MinStrength"/>
        /// for a working oar.
        /// </summary>
        public static float Strength01(float effort01, float boatSpeed, in OarSplashConfig cfg)
        {
            float speed01 = Mathf.Clamp01(Mathf.Abs(boatSpeed) / Mathf.Max(0.01f, cfg.SpeedForFullSplash));
            float share = Mathf.Clamp01(cfg.SpeedShare);
            float drive = Mathf.Clamp01(Mathf.Abs(effort01)) * (1f - share) + speed01 * share;
            float min = Mathf.Clamp01(cfg.MinStrength);
            return Mathf.Clamp01(min + (1f - min) * drive);
        }

        /// <summary>Droplets a catch of this strength throws.</summary>
        public static int CatchDrops(float strength01, in OarSplashConfig cfg)
        {
            int min = Mathf.Max(0, cfg.CatchDropsMin);
            int max = Mathf.Max(min, cfg.CatchDropsMax);
            return Mathf.RoundToInt(Mathf.Lerp(min, max, Mathf.Clamp01(strength01)));
        }

        /// <summary>Droplets a release of this strength flicks off.</summary>
        public static int ReleaseDrops(float strength01, in OarSplashConfig cfg)
        {
            int min = Mathf.Max(0, cfg.ReleaseDropsMin);
            int max = Mathf.Max(min, cfg.ReleaseDropsMax);
            return Mathf.RoundToInt(Mathf.Lerp(min, max, Mathf.Clamp01(strength01)));
        }

        /// <summary>Standard gravity (m/s²): how fast a drip falls from the blade to the water. A
        /// physical constant, not a feel dial.</summary>
        public const float GravityMetersPerSecondSquared = 9.81f;

        /// <summary>Seconds a drip takes to fall <paramref name="heightMeters"/> from rest; 0 at or under the water.</summary>
        public static float DripFallSeconds(float heightMeters)
            => heightMeters > 0f ? Mathf.Sqrt(2f * heightMeters / GravityMetersPerSecondSquared) : 0f;

        /// <summary>
        /// Drips per second off a blade <paramref name="secondsSinceRelease"/> out of the water: the
        /// configured rate at the release, scaled by the stroke's strength, falling away as
        /// <c>exp(−t / DripDecaySeconds)</c> as the blade drains. Zero before any release.
        /// </summary>
        public static float DripRate(float strength01, float secondsSinceRelease, in OarSplashConfig cfg)
        {
            if (cfg.DripsPerSecond <= 0f || float.IsInfinity(secondsSinceRelease) || secondsSinceRelease < 0f)
                return 0f;
            float decay = Mathf.Max(0.01f, cfg.DripDecaySeconds);
            return cfg.DripsPerSecond * Mathf.Clamp01(strength01) * Mathf.Exp(-secondsSinceRelease / decay);
        }

        /// <summary>
        /// Where puff <paramref name="k"/> of <paramref name="count"/> sits on the puddle's ring,
        /// relative to the release point: evenly round the circle, turned by <paramref name="turn01"/>
        /// of a step, and squashed by <paramref name="groundSquash"/> because the ring lies on the
        /// water and the camera looks down at it (screen y of a ground metre is <c>sin(elevation)</c>).
        /// </summary>
        public static Vector2 SwirlOffset(int k, int count, float turn01, float radius, float groundSquash)
        {
            float a = SwirlAngle(k, count, turn01);
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a) * groundSquash) * Mathf.Max(0f, radius);
        }

        /// <summary>
        /// The same puff's push: round the ring at <paramref name="spin"/> (signed: the two oars turn
        /// their puddles opposite ways, as a mirrored stroke does) plus a share opening outward. On
        /// the water, so squashed the same way as <see cref="SwirlOffset"/>.
        /// </summary>
        public static Vector2 SwirlVelocity(int k, int count, float turn01, float spin, float openShare,
                                            float groundSquash)
        {
            float a = SwirlAngle(k, count, turn01);
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            float open = Mathf.Abs(spin) * Mathf.Clamp01(openShare);
            var round = new Vector2(-s, c) * spin;
            var outward = new Vector2(c, s) * open;
            Vector2 v = round + outward;
            v.y *= groundSquash;
            return v;
        }

        private static float SwirlAngle(int k, int count, float turn01)
        {
            int n = Mathf.Max(1, count);
            return 2f * Mathf.PI * ((k + turn01) / n);
        }

        /// <summary>
        /// Whole foam puffs to lay this step at <paramref name="puffsPerSecond"/>, carrying the
        /// fraction so a slow rate still lays a puff every few frames instead of never.
        /// </summary>
        public static int FoamPuffs(float puffsPerSecond, float dt, ref float carry)
        {
            if (puffsPerSecond <= 0f || dt <= 0f) { carry = 0f; return 0; }
            carry += puffsPerSecond * dt;
            int whole = Mathf.FloorToInt(carry);
            if (whole > 0) carry -= whole;
            return whole;
        }
    }

    /// <summary>
    /// <b>The oar splash, as plain logic</b>: watches both blades, throws droplets on the frame a
    /// working blade enters the water (the catch), lays foam at the blade for as long as it stays
    /// in, and ages both. No scene, no renderer — <see cref="DoryOarSplashEmitter"/> draws what
    /// this holds.
    ///
    /// <para><b>The whole stroke on the water</b> (owner, 2026-11-17: the foam must come from the
    /// oars — a swirl, white water and spreading ripples where each blade goes in and comes out, and
    /// drips on the recovery). At the CATCH: droplets, foam and a set of rings. Through the PULL:
    /// foam at the blade. At the RELEASE (the blade leaving the water mid-stroke): a flick of
    /// droplets the way it was sweeping, a puddle of foam laid in a ring that turns and opens out,
    /// and a ring. On the RECOVERY: drips fall from the blade tip as drawn, thinning as the blade
    /// drains, each with a small ring that shows when the drip lands.</para>
    ///
    /// <para><b>Reuse.</b> The motes are two <see cref="WakeParticleSystem"/> pools — the wake's own
    /// fixed, recycled pool with its fade, spread and drift-with-the-current laws — so oar foam
    /// ages and drifts exactly as wake foam does, and nothing here allocates after construction
    /// (rule 7).</para>
    ///
    /// <para><b>Determinism (rule 5).</b> Presentation only: it reads the drawn pose and writes
    /// nothing back. Scatter comes from the wake's integer hash on a private counter, never
    /// <c>UnityEngine.Random</c>.</para>
    /// </summary>
    public sealed class DoryOarSplashSystem
    {
        /// <summary>One oar, this frame — everything the splash needs to know about it.</summary>
        public struct OarFrame
        {
            /// <summary>True only while this oar is being rowed: not shipped, not trailing, not
            /// stowed under power, not aground. Only a stroking oar splashes.</summary>
            public bool Stroking;
            /// <summary><see cref="DoryOarSplashMath.BladeHeightMeters"/> for the pose it is drawn in.</summary>
            public float BladeHeightMeters;
            /// <summary>Where the blade meets the water, world metres.</summary>
            public Vector2 BladeWorld;
            /// <summary>Unit world direction away from the hull on this oar's side.</summary>
            public Vector2 Outboard;
            /// <summary>Unit world direction toward the stern.</summary>
            public Vector2 Aft;
            /// <summary>|oar state|, 0..1 — how hard it is being pulled.</summary>
            public float Effort01;
            /// <summary>+1 pulling ahead (the blade sweeps aft), −1 backing water (it sweeps forward).</summary>
            public float StrokeDirection;
            /// <summary>The blade TIP dropped onto the waterline, world metres: where its drips land.</summary>
            public Vector2 TipOnWaterWorld;
            /// <summary>Screen metres per ground metre across the view, <c>sin(elevation)</c>: how much a
            /// ring lying on the water is squashed. 0 or less is read as 1 (seen from straight above).</summary>
            public float GroundSquash;
            /// <summary>Screen metres per metre of HEIGHT, <c>cos(elevation)</c>: how far up the screen a
            /// blade in the air is drawn above its mark on the water.</summary>
            public float HeightToScreen;
        }

        public const int Port = 0;
        public const int Starboard = 1;

        private readonly WakeParticleSystem _foam;
        private readonly WakeParticleSystem _droplets;
        private readonly OarWaterMarkPool _ripples;
        private readonly OarWaterMarkPool _drips;
        private readonly bool[] _wasImmersed = new bool[2];
        private readonly bool[] _primed = new bool[2];
        private readonly float[] _foamCarry = new float[2];
        private readonly float[] _dripCarry = new float[2];
        private readonly float[] _sinceRelease = { float.PositiveInfinity, float.PositiveInfinity };
        private readonly float[] _releaseStrength = new float[2];
        private readonly int[] _catches = new int[2];
        private readonly int[] _releases = new int[2];
        private uint _salt;

        /// <summary>
        /// One oar's deposit into the sea's ONE foam sheet this frame (the owner, 2026-11-19: "wake foam
        /// should be the same as any foam"): the capsule of water the blade worked, in DRAWN world metres,
        /// its half-width, the coverage to add at its centre (already dt-scaled) and the freshness gate.
        /// The emitter hands it to <see cref="HiddenHarbours.Core.FoamSplashes"/>; the buffer ages it.
        /// </summary>
        public struct SheetDeposit
        {
            public Vector2 From, To;
            public float Radius, Amount, Vigour;
        }

        private readonly SheetDeposit[] _sheet = new SheetDeposit[2];
        private readonly bool[] _hasSheet = new bool[2];
        private readonly Vector2[] _lastWater = new Vector2[2];
        private readonly bool[] _hadWater = new bool[2];
        private FoamSheetSettings _sheetCfg;
        private bool _joined;

        /// <summary>
        /// A ring of real water waves to send out this frame (joined to the sheet only): where a blade went
        /// in or came out, and the crest height. The emitter hands it to the hull-wave field, so the sea
        /// itself draws the ring (lit faces and troughs), never a picture.
        /// </summary>
        public struct WaveRing
        {
            public Vector2 At;
            public float HeightMetres;
            public bool Catch;
        }

        /// <summary>At most a catch and a release per oar per frame.</summary>
        public const int MaxWaveRingsPerFrame = 4;
        private readonly WaveRing[] _waveRings = new WaveRing[MaxWaveRingsPerFrame];
        private int _waveRingCount;

        /// <summary>True while the oars lay their foam into the sheet: no drawn puffs, puddles or rings.</summary>
        public bool JoinsSheet => _joined;

        /// <summary>How many water rings this frame's step asked for (0..<see cref="MaxWaveRingsPerFrame"/>).</summary>
        public int WaveRingCount => _waveRingCount;

        /// <summary>One of this frame's water rings (0..<see cref="WaveRingCount"/>-1).</summary>
        public WaveRing GetWaveRing(int i) => _waveRings[Mathf.Clamp(i, 0, MaxWaveRingsPerFrame - 1)];

        private void AskRing(Vector2 at, float heightMetres, bool isCatch)
        {
            if (!(heightMetres > 0f) || _waveRingCount >= MaxWaveRingsPerFrame) return;
            _waveRings[_waveRingCount++] = new WaveRing { At = at, HeightMetres = heightMetres, Catch = isCatch };
        }

        /// <summary>This frame's sheet deposit for one oar, if it laid one.</summary>
        public bool TryGetSheetDeposit(int oar, out SheetDeposit deposit)
        {
            int i = Mathf.Clamp(oar, 0, 1);
            deposit = _sheet[i];
            return _hasSheet[i];
        }

        public DoryOarSplashSystem(int foamPoolSize, int dropletPoolSize)
            : this(foamPoolSize, dropletPoolSize, OarSplashConfig.Default.RipplePoolSize,
                   OarSplashConfig.Default.DripPoolSize) { }

        public DoryOarSplashSystem(int foamPoolSize, int dropletPoolSize, int ripplePoolSize, int dripPoolSize)
        {
            _foam = new WakeParticleSystem(Mathf.Max(1, foamPoolSize));
            _droplets = new WakeParticleSystem(Mathf.Max(1, dropletPoolSize));
            _ripples = new OarWaterMarkPool(Mathf.Max(1, ripplePoolSize));
            _drips = new OarWaterMarkPool(Mathf.Max(1, dripPoolSize));
        }

        /// <summary>The foam puffs (fixed pool; drawn by the emitter).</summary>
        public WakeParticleSystem Foam => _foam;
        /// <summary>The thrown droplets (fixed pool; drawn by the emitter).</summary>
        public WakeParticleSystem Droplets => _droplets;
        /// <summary>The spreading rings, blade and drip alike (fixed pool; drawn by the emitter).</summary>
        public OarWaterMarkPool Ripples => _ripples;
        /// <summary>The drips falling off the blades on the recovery (fixed pool; drawn by the emitter).</summary>
        public OarWaterMarkPool Drips => _drips;

        /// <summary>Catches fired so far by one oar (<see cref="Port"/> / <see cref="Starboard"/>).</summary>
        public int CatchCount(int oar) => _catches[Mathf.Clamp(oar, 0, 1)];

        /// <summary>Releases (the blade leaving the water mid-stroke) so far by one oar.</summary>
        public int ReleaseCount(int oar) => _releases[Mathf.Clamp(oar, 0, 1)];

        /// <summary>Forget what the blades were doing (a re-skin, a hull swap). Live motes are left to fade.</summary>
        public void ResetTracking()
        {
            for (int i = 0; i < 2; i++)
            {
                _primed[i] = false; _wasImmersed[i] = false; _foamCarry[i] = 0f;
                _dripCarry[i] = 0f; _sinceRelease[i] = float.PositiveInfinity;
                _hadWater[i] = false; _hasSheet[i] = false;
            }
        }

        /// <summary>
        /// One frame: fire catches, lay pull foam, then age every mote under the tide's
        /// <paramref name="current"/>. Returns how many catches fired this frame (0, 1 or 2).
        /// </summary>
        public int Step(in OarFrame port, in OarFrame starboard, float boatSpeed, Vector2 current,
                        float dt, in OarSplashConfig cfg)
            => Step(in port, in starboard, boatSpeed, current, dt, in cfg, default);

        /// <summary>
        /// The same frame, with the one foam sheet's dials: when <paramref name="sheet"/> joins the oars
        /// to the sheet (<see cref="FoamSheetSettings.OarJoinsSheet"/>), each working blade lays a
        /// <see cref="SheetDeposit"/> where it goes in, pulls and comes out, and no foam puffs, puddle
        /// puffs or rings are laid. Droplets and drips (water in the air, not foam) are kept either way.
        /// </summary>
        public int Step(in OarFrame port, in OarFrame starboard, float boatSpeed, Vector2 current,
                        float dt, in OarSplashConfig cfg, in FoamSheetSettings sheet)
        {
            _sheetCfg = sheet;
            _joined = sheet.OarJoinsSheet;
            _hasSheet[Port] = false;
            _hasSheet[Starboard] = false;
            _waveRingCount = 0;
            int fired = 0;
            if (StepOar(Port, in port, boatSpeed, dt, in cfg)) fired++;
            if (StepOar(Starboard, in starboard, boatSpeed, dt, in cfg)) fired++;

            if (dt > 0f)
            {
                _foam.Step(current, cfg.FoamVelocityRetain, dt);
                _droplets.Step(current, cfg.DropletVelocityRetain, dt);
                _ripples.Step(current, dt);
                _drips.Step(current, dt);
            }
            return fired;
        }

        private bool StepOar(int oar, in OarFrame f, float boatSpeed, float dt, in OarSplashConfig cfg)
        {
            bool immersed = DoryOarSplashMath.IsImmersed(f.BladeHeightMeters);
            // The blade's state is tracked on EVERY frame, working or not, so an oar that was
            // already lying in the water when the pull began does not report a second entry (nor
            // a release when it is lifted out to be shipped).
            bool primed = _primed[oar];
            bool wasIn = _wasImmersed[oar];
            bool entered = primed && immersed && !wasIn;
            bool exited = primed && !immersed && wasIn;
            _primed[oar] = true;
            _wasImmersed[oar] = immersed;

            // Where this blade last worked the water, for the sheet's capsule. Read before it is updated.
            bool hadWater = _hadWater[oar];
            Vector2 lastWater = _lastWater[oar];
            _hadWater[oar] = immersed && cfg.Enabled && f.Stroking;
            if (_hadWater[oar]) _lastWater[oar] = f.BladeWorld;

            if (!cfg.Enabled || !f.Stroking)
            {
                _foamCarry[oar] = 0f;   // no burst of saved-up foam on the next entry
                _dripCarry[oar] = 0f;
                _sinceRelease[oar] = float.PositiveInfinity;   // a shipped blade does not drip on
                return false;
            }

            float strength = DoryOarSplashMath.Strength01(f.Effort01, boatSpeed, in cfg);
            float sweep = f.StrokeDirection < 0f ? -1f : 1f;
            Vector2 push = f.Aft * (sweep * cfg.FoamPushSpeed * strength);

            if (!immersed)
            {
                _foamCarry[oar] = 0f;
                if (exited)
                {
                    _releases[oar]++;
                    // The puddle the blade leaves, laid where it last worked the water.
                    if (_joined && hadWater)
                    {
                        LaySheet(oar, lastWater, lastWater, _sheetCfg.OarRelease * strength, strength, _sheetCfg.OarCatchRadius);
                        AskRing(lastWater, _sheetCfg.OarRingHeightMetres * Mathf.Clamp01(_sheetCfg.OarReleaseRingShare) * strength,
                                isCatch: false);
                    }
                    Release(in f, sweep, strength, in cfg);
                    _sinceRelease[oar] = 0f;
                    _releaseStrength[oar] = strength;
                }
                else if (!float.IsInfinity(_sinceRelease[oar]))
                {
                    _sinceRelease[oar] += dt;
                }
                Drip(oar, in f, dt, in cfg);
                return false;
            }

            _dripCarry[oar] = 0f;
            _sinceRelease[oar] = float.PositiveInfinity;

            if (entered)
            {
                _catches[oar]++;
                ThrowDroplets(in f, DoryOarSplashMath.CatchDrops(strength, in cfg), strength, in cfg);
                if (!_joined)
                {
                    LayFoam(in f, cfg.CatchFoamPuffs, push, strength, in cfg);
                    SendRipples(f.BladeWorld, cfg.RipplesAtCatch, 1f, 1f, 1f, 0f, strength, in cfg);
                }
                else
                {
                    AskRing(f.BladeWorld, _sheetCfg.OarRingHeightMetres * strength, isCatch: true);
                }
            }

            if (_joined)
            {
                // The blade's sweep through the water since the last frame, plus the catch's own white on
                // the frame it went in: one capsule per oar per frame, continuous at any frame rate.
                Vector2 from = hadWater && !entered ? lastWater : f.BladeWorld;
                float amount = Mathf.Max(0f, _sheetCfg.OarPerSecond) * strength * Mathf.Max(0f, dt);
                if (entered) amount += Mathf.Max(0f, _sheetCfg.OarCatch) * strength;
                LaySheet(oar, from, f.BladeWorld, amount, strength, entered ? _sheetCfg.OarCatchRadius : _sheetCfg.OarRadius);
                return entered;
            }

            int puffs = DoryOarSplashMath.FoamPuffs(cfg.FoamPuffsPerSecond * strength, dt, ref _foamCarry[oar]);
            LayFoam(in f, puffs, push, strength, in cfg);
            return entered;
        }

        private void LaySheet(int oar, Vector2 from, Vector2 to, float amount, float strength, float radius)
        {
            if (!(amount > 0f) || !(radius > 0f)) return;
            _sheet[oar] = new SheetDeposit
            {
                From = from,
                To = to,
                Radius = radius,
                Amount = Mathf.Clamp01(amount),
                Vigour = Mathf.Clamp01(strength),
            };
            _hasSheet[oar] = true;
        }

        /// <summary>
        /// The blade comes out: a few droplets flicked the way it was sweeping, the puddle of turning
        /// foam it leaves, and a ring spreading from the hole.
        /// </summary>
        private void Release(in OarFrame f, float sweep, float strength, in OarSplashConfig cfg)
        {
            int drops = DoryOarSplashMath.ReleaseDrops(strength, in cfg);
            if (drops > 0)
            {
                WakeConfig wc = cfg.DropletWakeConfig();
                Vector2 along = f.Aft * sweep;
                for (int k = 0; k < drops; k++)
                {
                    float fan = (Hash() - 0.5f) * 2f;
                    float speed = cfg.ReleaseDropletSpeed * strength * (0.5f + 0.5f * Hash());
                    Vector2 dir = along + f.Outboard * (0.5f * fan);
                    if (dir.sqrMagnitude > 1e-8f) dir.Normalize();
                    _droplets.EmitAt(f.BladeWorld, dir * speed, in wc, 1f, 1f, strength, orientDeg: 0f);
                }
            }

            // Joined to the sheet, the puddle is the sheet's own foam (laid by the caller) and the water
            // draws no ring picture.
            if (_joined) return;

            int n = cfg.SwirlPuffs;
            if (n > 0)
            {
                WakeConfig wc = cfg.FoamWakeConfig();
                float squash = Squash(in f);
                // The two oars are mirror images, so their puddles turn opposite ways: the sign of
                // outboard × aft differs port and starboard on any heading.
                float hand = f.Outboard.x * f.Aft.y - f.Outboard.y * f.Aft.x;
                float spin = (hand < 0f ? -1f : 1f) * sweep * cfg.SwirlSpinSpeed * strength;
                float turn = Hash();
                for (int k = 0; k < n; k++)
                {
                    Vector2 at = f.BladeWorld + DoryOarSplashMath.SwirlOffset(k, n, turn, cfg.SwirlRadiusMeters, squash);
                    Vector2 vel = DoryOarSplashMath.SwirlVelocity(k, n, turn, spin, cfg.SwirlOpenShare, squash);
                    _foam.EmitAt(at, vel, in wc, cfg.SwirlLifetimeScale, cfg.SwirlSizeScale, strength, orientDeg: 0f);
                }
            }

            SendRipples(f.BladeWorld, cfg.RipplesAtRelease, 1f, 1f, 1f, 0f, strength, in cfg);
        }

        /// <summary>
        /// Drips off a blade on its way forward through the air: each falls from the tip as drawn to
        /// its mark on the water, and the ring it makes there shows when it lands.
        /// </summary>
        private void Drip(int oar, in OarFrame f, float dt, in OarSplashConfig cfg)
        {
            float rate = DoryOarSplashMath.DripRate(_releaseStrength[oar], _sinceRelease[oar], in cfg);
            int count = DoryOarSplashMath.FoamPuffs(rate, dt, ref _dripCarry[oar]);
            if (count <= 0) return;

            float height = Mathf.Max(0f, f.BladeHeightMeters);
            float fall = DoryOarSplashMath.DripFallSeconds(height);
            if (fall <= 0f) return;
            float lift = height * Mathf.Max(0f, f.HeightToScreen);
            float strength = _releaseStrength[oar];
            Vector2 along = f.Outboard;   // the blade lies out from the hull: drips leave along it
            for (int k = 0; k < count; k++)
            {
                Vector2 landing = f.TipOnWaterWorld + along * ((Hash() - 0.5f) * 2f * cfg.DripScatterMeters);
                _drips.Add(landing + new Vector2(0f, lift), new Vector2(0f, -lift / fall), 0f, fall,
                           cfg.DripSize * 0.5f, cfg.DripSize * 0.5f, strength, airborne: true);
                if (!_joined) SendRipples(landing, 1, cfg.DripRingScale, cfg.DripRingLifetimeScale, cfg.DripRingAlphaScale,
                            fall, strength, in cfg);
            }
        }

        /// <summary>A set of <paramref name="count"/> rings from one point, each later and fainter.</summary>
        private void SendRipples(Vector2 at, int count, float sizeScale, float lifeScale, float alphaScale,
                                 float delay, float strength, in OarSplashConfig cfg)
        {
            if (count <= 0) return;
            float alpha = cfg.RippleStartAlpha * alphaScale * strength;
            float end = cfg.RippleEndRadius * sizeScale * Mathf.Lerp(Mathf.Clamp01(cfg.RippleGentleSizeShare), 1f, strength);
            float life = cfg.RippleLifetime * lifeScale;
            for (int k = 0; k < count; k++)
            {
                float jitter = 1f + (Hash() - 0.5f) * 2f * cfg.LifetimeJitter;
                _ripples.Add(at, Vector2.zero, delay + k * cfg.RippleStaggerSeconds, life * jitter,
                             cfg.RippleStartRadius * sizeScale, end, alpha, airborne: false);
                alpha *= cfg.RippleEchoFade;
            }
        }

        private static float Squash(in OarFrame f) => f.GroundSquash > 0f ? Mathf.Min(1f, f.GroundSquash) : 1f;

        private void ThrowDroplets(in OarFrame f, int count, float strength, in OarSplashConfig cfg)
        {
            if (count <= 0) return;
            WakeConfig wc = cfg.DropletWakeConfig();
            Vector2 side = f.Outboard;
            Vector2 along = f.Aft;
            for (int k = 0; k < count; k++)
            {
                // A fan thrown away from the hull: mostly outboard, spread fore and aft.
                float fan = (Hash() - 0.5f) * 2f;
                float speed = cfg.DropletSpeed * strength * (0.5f + 0.5f * Hash());
                Vector2 dir = side + along * fan;
                if (dir.sqrMagnitude > 1e-8f) dir.Normalize();
                _droplets.EmitAt(f.BladeWorld, dir * speed, in wc, 1f, 1f, strength, orientDeg: 0f);
            }
        }

        private void LayFoam(in OarFrame f, int count, Vector2 push, float strength, in OarSplashConfig cfg)
        {
            if (count <= 0) return;
            WakeConfig wc = cfg.FoamWakeConfig();
            for (int k = 0; k < count; k++)
            {
                var scatter = new Vector2(Hash() - 0.5f, Hash() - 0.5f) * (2f * cfg.FoamScatterMeters);
                _foam.EmitAt(f.BladeWorld + scatter, push, in wc, 1f, 1f, strength, orientDeg: 0f);
            }
        }

        private float Hash() => WakeParticleSystem.Hash01(++_salt);
    }
}
