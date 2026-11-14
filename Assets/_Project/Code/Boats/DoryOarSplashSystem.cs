using System;
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
            LifetimeJitter        = 0.2f,
            SizeJitter            = 0.25f,
            FoamPoolSize          = 32,
            DropletPoolSize       = 24,
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
        }

        public const int Port = 0;
        public const int Starboard = 1;

        private readonly WakeParticleSystem _foam;
        private readonly WakeParticleSystem _droplets;
        private readonly bool[] _wasImmersed = new bool[2];
        private readonly bool[] _primed = new bool[2];
        private readonly float[] _foamCarry = new float[2];
        private readonly int[] _catches = new int[2];
        private uint _salt;

        public DoryOarSplashSystem(int foamPoolSize, int dropletPoolSize)
        {
            _foam = new WakeParticleSystem(Mathf.Max(1, foamPoolSize));
            _droplets = new WakeParticleSystem(Mathf.Max(1, dropletPoolSize));
        }

        /// <summary>The foam puffs (fixed pool; drawn by the emitter).</summary>
        public WakeParticleSystem Foam => _foam;
        /// <summary>The thrown droplets (fixed pool; drawn by the emitter).</summary>
        public WakeParticleSystem Droplets => _droplets;

        /// <summary>Catches fired so far by one oar (<see cref="Port"/> / <see cref="Starboard"/>).</summary>
        public int CatchCount(int oar) => _catches[Mathf.Clamp(oar, 0, 1)];

        /// <summary>Forget what the blades were doing (a re-skin, a hull swap). Live motes are left to fade.</summary>
        public void ResetTracking()
        {
            for (int i = 0; i < 2; i++) { _primed[i] = false; _wasImmersed[i] = false; _foamCarry[i] = 0f; }
        }

        /// <summary>
        /// One frame: fire catches, lay pull foam, then age every mote under the tide's
        /// <paramref name="current"/>. Returns how many catches fired this frame (0, 1 or 2).
        /// </summary>
        public int Step(in OarFrame port, in OarFrame starboard, float boatSpeed, Vector2 current,
                        float dt, in OarSplashConfig cfg)
        {
            int fired = 0;
            if (StepOar(Port, in port, boatSpeed, dt, in cfg)) fired++;
            if (StepOar(Starboard, in starboard, boatSpeed, dt, in cfg)) fired++;

            if (dt > 0f)
            {
                _foam.Step(current, cfg.FoamVelocityRetain, dt);
                _droplets.Step(current, cfg.DropletVelocityRetain, dt);
            }
            return fired;
        }

        private bool StepOar(int oar, in OarFrame f, float boatSpeed, float dt, in OarSplashConfig cfg)
        {
            bool immersed = DoryOarSplashMath.IsImmersed(f.BladeHeightMeters);
            // The blade's state is tracked on EVERY frame, working or not, so an oar that was
            // already lying in the water when the pull began does not report a second entry.
            bool entered = _primed[oar] && immersed && !_wasImmersed[oar];
            _primed[oar] = true;
            _wasImmersed[oar] = immersed;

            if (!cfg.Enabled || !f.Stroking || !immersed)
            {
                _foamCarry[oar] = 0f;   // no burst of saved-up foam on the next entry
                return false;
            }

            float strength = DoryOarSplashMath.Strength01(f.Effort01, boatSpeed, in cfg);
            float sweep = f.StrokeDirection < 0f ? -1f : 1f;
            Vector2 push = f.Aft * (sweep * cfg.FoamPushSpeed * strength);

            if (entered)
            {
                _catches[oar]++;
                ThrowDroplets(in f, DoryOarSplashMath.CatchDrops(strength, in cfg), strength, in cfg);
                LayFoam(in f, cfg.CatchFoamPuffs, push, strength, in cfg);
            }

            int puffs = DoryOarSplashMath.FoamPuffs(cfg.FoamPuffsPerSecond * strength, dt, ref _foamCarry[oar]);
            LayFoam(in f, puffs, push, strength, in cfg);
            return entered;
        }

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
