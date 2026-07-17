using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// POSES A LEVEL-BAKED OVERLAY ONTO A ROCK-BAKED ISO HULL — by re-running the art rig's own camera, not by
    /// guessing a fudge factor.
    ///
    /// <para><b>The problem this exists to solve.</b> Every iso kit bakes the hull's rock INTO its frames
    /// (<c>rockMotion(i)</c> feeds the camera basis, and the whole boat is re-rendered leaning). The overlays that
    /// hang off that hull — the outboard, above all — are baked LEVEL, so they must be posed onto the wave by
    /// hand. The pose is NOT a single number: what the camera does to a rigidly-mounted part depends on
    /// <b>WHERE it is mounted</b> and on <b>WHICH WAY the boat is pointing</b>. An outboard hangs ~3.5 m aft of
    /// the origin; a bow-up pitch there reads as the stern (and the engine) going DOWN, and reads differently at
    /// N than at E. A scalar cannot say that.</para>
    ///
    /// <para><b>How it works.</b> <see cref="Project"/> is a faithful port of the rigs' <c>projVert</c>
    /// (<c>docs/art/skiff-fleet-rigs/consoleIsoRig.js</c>, <c>docs/art/punt-iso-rig/puntIsoRig.js</c> — they are
    /// character-identical), minus the <c>×S</c> that turns metres into pixels: because our sheets import at the
    /// same PPU the rigs bake at (32), <b>the rig's screen offset in metres IS our screen offset in world
    /// units</b> and S cancels. <see cref="Pose"/> then asks the rig two questions:
    /// <list type="number">
    ///   <item>where does the mount point land on screen when the boat is rocked to this frame? (the target);</item>
    ///   <item>which way does the boat's own vertical (its mast/stanchion axis) lean on screen? (the roll).</item>
    /// </list>
    /// and returns the transform that puts the level cell there.</para>
    ///
    /// <para><b>Why the offset is not simply "the mount moved".</b> The overlay renderers pin the hull cell's
    /// PIVOT (the boat origin, amidships) — that is what makes them register pixel-perfect at localPosition zero.
    /// So a <c>localRotation</c> rotates the sprite about the ORIGIN, not about the mount, and on an engine
    /// hanging 3.5 m aft that swing is the LARGEST term in the whole pose (≈4.3 px at the console skiff's peak
    /// roll — bigger than the mount's true travel). <see cref="Pose"/> accounts for it exactly:
    /// <c>offset = P_rocked(mount) − R(φ)·P_level(sheetMount)</c>, which is the algebra for "rotate the cell about
    /// its mount by φ, then put the mount where the rig puts it", re-expressed for a pivot-at-origin transform.
    /// At the level pose with no lateral shift it is exactly zero; with a lateral shift it reduces to
    /// <see cref="OutboardMotorMath.MountOffset"/> — two independent derivations that agree, which is what
    /// <c>MountedRockPoseTests</c> pins.</para>
    ///
    /// <para><b>The dory's oars deliberately do NOT use this.</b> <see cref="DoryOarMath.RockPose"/> is a tuned
    /// approximation with a hand-set pitch offset, and the owner has a standing verdict that the rowing feels
    /// good (#35). Its oarlocks sit near amidships, where the lever arm this class turns on is small and the
    /// approximation is cheap and close. Re-deriving them is a change to a shipped FEEL, not a bug fix, and it is
    /// not taken here.</para>
    ///
    /// <para><b>Rules.</b> Pure, static, allocation-free, deterministic — the same discipline as
    /// <see cref="OutboardMotorMath"/> / <see cref="WakeGrading"/>. Visual-only: nothing here feeds the sim
    /// (rule 5). It holds no amplitudes and no mount points: <b>every one of them is an art fact carried as data
    /// on <see cref="BoatVisualDef"/></b> and passed in (rule 6) — a kit's numbers must never be a const in a
    /// class two kits share.</para>
    /// </summary>
    public static class MountedRockPoseMath
    {
        /// <summary>The rock-free/level sentinel: <see cref="DirectionalBoatSprite.RockFrame"/> is −1 when the hull
        /// draws its static facing (calm sea, or no rock grid).</summary>
        public const int LevelRockFrame = DoryOarMath.LevelRockFrame;

        /// <summary>The screen-space transform that rides a LEVEL-baked overlay cell on the hull's currently-drawn
        /// rock frame. Applied to a renderer whose sprite pivot is the boat origin: rotate by
        /// <see cref="RollDegrees"/>, then translate by <see cref="Offset"/>.</summary>
        public readonly struct MountRockPose
        {
            /// <summary>Additive z-rotation in DEGREES (+ = CCW), about the sprite's pivot (the boat origin) —
            /// the screen lean of the boat's own vertical at this heading and rock frame.</summary>
            public readonly float RollDegrees;

            /// <summary>Screen offset in METRES (Unity axes: +X right, +Y up), added to the renderer's local
            /// position. Carries BOTH the rock travel and the engine's own lateral clamp shift.</summary>
            public readonly Vector2 Offset;

            public MountRockPose(float rollDegrees, Vector2 offset)
            {
                RollDegrees = rollDegrees;
                Offset = offset;
            }

            /// <summary>The identity pose: the cell drawn exactly as baked.</summary>
            public static MountRockPose Level => new MountRockPose(0f, Vector2.zero);
        }

        /// <summary>
        /// Project a boat-local point (METRES; +X starboard, +Y bow, +Z up — the rigs' own axes) through the rig
        /// camera, returning its screen offset from the cell pivot in METRES (Unity axes: +X right, +Y up).
        ///
        /// <para>A faithful port of the rigs' <c>projVert</c>/<c>camBasis</c> pair:
        /// <code>
        ///   x1 =  x·cr + z·sr;   z1 = −x·sr + z·cr        // roll  (about the fore-aft axis)
        ///   y2 =  y·cq − z1·sq;  z2 =  y·sq + z1·cq        // pitch (about the athwartships axis)
        ///   xr =  x1·ct − y2·st; yr =  x1·st + y2·ct       // heading turntable
        ///   sx =  cx + xr·S;     sy = cy − (yr·se + zr·ce)·S − heave
        /// </code>
        /// The image's y-DOWN flips to Unity's y-UP (killing the minus), and the <c>×S</c> is dropped because our
        /// sheets import at exactly the rigs' 32 px/m — so this returns <c>(xr, yr·sin(e) + zr·cos(e))</c>.</para>
        ///
        /// <para><paramref name="headingRadians"/> is the rig's turntable angle <c>θ = cell·2π/count</c>
        /// (CELL space — see <see cref="BoatVisualDef.FacingsAreCounterClockwise"/> for why a cell's label is not
        /// its heading). Non-finite inputs return <see cref="Vector2.zero"/> rather than propagating NaN.</para>
        /// </summary>
        public static Vector2 Project(Vector3 local, float headingRadians, float rollRadians, float pitchRadians,
                                      float elevationRadians)
        {
            if (!IsFinite(local.x) || !IsFinite(local.y) || !IsFinite(local.z) ||
                !IsFinite(headingRadians) || !IsFinite(rollRadians) || !IsFinite(pitchRadians) ||
                !IsFinite(elevationRadians))
                return Vector2.zero;

            Vector3 r = RigRotate(local, headingRadians, rollRadians, pitchRadians);
            return new Vector2(r.x, r.y * Mathf.Sin(elevationRadians) + r.z * Mathf.Cos(elevationRadians));
        }

        /// <summary>
        /// The pose for ONE overlay engine/part, given where its kit's sheet was baked and where this instance
        /// actually hangs.
        ///
        /// <list type="bullet">
        ///   <item><paramref name="sheetMountLocal"/> — the mount the CELL was baked at, in boat-local metres
        ///   (the rigs' <c>MOUNT</c>, read at <c>motorMount</c>'s <c>y − 0.03</c>). Console/sport skiff
        ///   <c>(0, −3.53, 0.72)</c>; punt <c>(0, −2.63, 0.56)</c>.</item>
        ///   <item><paramref name="lateralMetres"/> — how far off the centreline THIS instance is clamped (0 for a
        ///   single engine; ±the twin spacing for the sport skiff's pair).</item>
        ///   <item><paramref name="rollAmpDegrees"/> / <paramref name="pitchAmpDegrees"/> /
        ///   <paramref name="heavePixels"/> — the kit's baked <c>ROCK.rollA/pitchA/heaveA</c>. <b>Pitch is in
        ///   DEGREES</b>, as the rig states it — it is a rotation, and how much screen travel it causes is what
        ///   this function works out from the mount.</item>
        /// </list>
        ///
        /// <paramref name="rockFrame"/> &lt; 0 (<see cref="LevelRockFrame"/>) or a non-positive
        /// <paramref name="strength"/> zeroes the rock but STILL returns the lateral clamp offset — a calm sea
        /// must not collapse a twin's engines onto each other. <paramref name="strength"/> scales all three
        /// amplitudes together (0 = the overlay sits level on a rocking hull; 1 = the art's own read).
        /// </summary>
        public static MountRockPose Pose(int rockFrame, int rockFrameCount, int headingRow, int headingCount,
                                         Vector3 sheetMountLocal, float lateralMetres, float elevationDegrees,
                                         float rollAmpDegrees, float pitchAmpDegrees, float heavePixels,
                                         float pixelsPerUnit, float strength)
        {
            int count = Mathf.Max(1, headingCount);
            int row = headingRow % count;
            if (row < 0) row += count;
            float theta = row * (2f * Mathf.PI / count);
            float elev = elevationDegrees * Mathf.Deg2Rad;

            // A level/calm hull still needs the clamp shift — only the WAVE terms drop out.
            bool rocking = rockFrame >= 0 && strength > 0f && rockFrameCount > 0;
            float roll = 0f, pitch = 0f, heaveMeters = 0f;
            if (rocking)
            {
                float k = strength;
                float a = DoryOarMath.RockPhaseDegrees(rockFrame, rockFrameCount) * Mathf.Deg2Rad;
                // The rigs' rockMotion(i), verbatim: roll = rollA·sin(a), pitch = pitchA·sin(a + π/2) ≡
                // pitchA·cos(a), heave = heaveA·sin(a) — in DEGREES, DEGREES and PIXELS respectively.
                roll = rollAmpDegrees * Mathf.Sin(a) * k * Mathf.Deg2Rad;
                pitch = pitchAmpDegrees * Mathf.Cos(a) * k * Mathf.Deg2Rad;
                heaveMeters = (heavePixels / Mathf.Max(1e-3f, pixelsPerUnit)) * Mathf.Sin(a) * k;
            }

            if (!IsFinite(lateralMetres)) lateralMetres = 0f;

            // (1) Where the rig puts THIS engine's mount on the rocked hull — the target.
            var mount = new Vector3(sheetMountLocal.x + lateralMetres, sheetMountLocal.y, sheetMountLocal.z);
            Vector2 target = Project(mount, theta, roll, pitch, elev) + new Vector2(0f, heaveMeters);

            // (2) The screen lean of the boat's own vertical — the rotation to give the cell. Derived, not
            // tuned: it is 0 wherever the roll axis points at the camera, which is why a scalar "rollA·sin(a)"
            // at every heading was wrong in both magnitude AND heading-dependence.
            float rollDegrees = ScreenRollDegrees(theta, roll, pitch, elev);

            // (3) Re-express "rotate about the mount, then place the mount" for a pivot-at-ORIGIN transform.
            // P_level(sheetMount) is where the mount sits in the cell as baked; rotating the cell about the
            // origin drags it, and this subtracts exactly that drag back out.
            Vector2 baked = Project(sheetMountLocal, theta, 0f, 0f, elev);
            float phi = rollDegrees * Mathf.Deg2Rad;
            float cp = Mathf.Cos(phi), sp = Mathf.Sin(phi);
            var rotatedBaked = new Vector2(baked.x * cp - baked.y * sp, baked.x * sp + baked.y * cp);

            return new MountRockPose(rollDegrees, target - rotatedBaked);
        }

        /// <summary>
        /// The screen lean (DEGREES, + = CCW) of the boat's own vertical axis at this heading, roll and pitch —
        /// the rotation a level-baked overlay standing ON the boat must take.
        ///
        /// <para>Projects the boat-local UP vector <c>(0,0,1)</c> through the same camera and measures its screen
        /// angle off vertical. This is what makes the roll HEADING-DEPENDENT and correctly signed: at N/S the
        /// fore-aft (roll) axis lies across the screen, so a roll tips the mast sideways and reads at nearly
        /// <c>rollA/cos(elev)</c>; at E/W that axis points at the camera, the roll all but vanishes from the
        /// screen, and it is the PITCH that turns the mast instead.</para>
        /// </summary>
        public static float ScreenRollDegrees(float headingRadians, float rollRadians, float pitchRadians,
                                              float elevationRadians)
        {
            Vector2 up = Project(Vector3.forward, headingRadians, rollRadians, pitchRadians, elevationRadians);
            if (up.sqrMagnitude < 1e-12f) return 0f;
            // The angle of (x,y) measured off screen-+Y, CCW positive.
            return Mathf.Atan2(-up.x, up.y) * Mathf.Rad2Deg;
        }

        /// <summary>The rigs' rotate-then-turntable chain, returning <c>(xr, yr, zr)</c> — the camera-space point
        /// the projection reads. Split out so <see cref="Project"/> and its tests share ONE copy of the art's
        /// arithmetic.</summary>
        private static Vector3 RigRotate(Vector3 p, float theta, float roll, float pitch)
        {
            float ct = Mathf.Cos(theta), st = Mathf.Sin(theta);
            float cr = Mathf.Cos(roll), sr = Mathf.Sin(roll);
            float cq = Mathf.Cos(pitch), sq = Mathf.Sin(pitch);

            float x1 = p.x * cr + p.z * sr;
            float z1 = -p.x * sr + p.z * cr;
            float y2 = p.y * cq - z1 * sq;
            float z2 = p.y * sq + z1 * cq;

            return new Vector3(x1 * ct - y2 * st, x1 * st + y2 * ct, z2);
        }

        private static bool IsFinite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
