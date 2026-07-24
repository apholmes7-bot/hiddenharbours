using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>Where an outboard points, for the MESH path (ADR 0022 phase 7)</b> — a transcription of the
    /// two motor rigs' shared <c>mxform(opts)</c>, and nothing more.
    ///
    /// <para><b>Why a transcription at all.</b> The sprite path never needed one: it picked a baked
    /// column and the rig's arithmetic stayed inside the bake. A mesh engine is posed at runtime,
    /// where the rig does not exist. Every line below is adjudicated in PIXELS against the rigs' own
    /// <c>renderMotor</c> by <c>OutboardPropMeshAcceptanceTests</c> — transcribe, then prove.</para>
    ///
    /// <para><b>The derivation, because it is the whole file.</b> Both rigs write the same thing
    /// (<c>puntIsoRig.js:248</c>, <c>skiffMotorRig.js:49</c>):</para>
    /// <code>
    ///   y1 = y·ct + (z−ZT)·st;   z1 = ZT − y·st + (z−ZT)·ct     // tilt
    ///   x2 = x·cs − y1·ss;       y2 = x·ss + y1·cs               // steer
    ///   return [mx + x2, YA + y2, z1];
    /// </code>
    /// <para>At steer 0 / tilt 0 that collapses to <c>[mx+x, YA+y, z]</c> — which is the placement the
    /// canonical bake already carries at <c>mx = 0</c>. Writing the baked vertex as <c>v</c> and
    /// <c>P = (0, YA, ZT)</c>, the general pose is</para>
    /// <code>
    ///   out = P + (mx,0,0) + R·(v − P),   R = Rz(steer) · Rx(−tilt)
    /// </code>
    /// <para>so the runtime transform is <b>exactly rigid</b>: one rotation about the clamp swivel,
    /// plus the lateral mount. That is the entire <see cref="Core.IHullPropRenderer"/> contract, which
    /// is why an outboard and an oar — which articulate nothing alike — ride one seam.</para>
    ///
    /// <para>⚠️ <b>P is the swivel, not the mount.</b> The punt exports <c>MOUNT.y = −L/2</c> while her
    /// swivel sits at <c>YA = −L/2 − 0.06</c>, six centimetres further aft. It is read off the rig into
    /// <c>HullPropMeshDef.PivotLocalMeters</c> rather than written here, and the renderer rotates about
    /// it — rotating about the hull ORIGIN instead swings the engine clean through the boat, which
    /// this project has already shipped once on the sprite path.</para>
    ///
    /// <para><b>Pure, static, allocation-free</b> — the same discipline as <see cref="DoryOarMeshPose"/>
    /// and <see cref="OutboardMotorMath"/>.</para>
    /// </summary>
    public static class OutboardMotorMeshPose
    {
        /// <summary>
        /// The rotation carrying the engine from its BAKED pose (dead ahead, untilted) to
        /// <paramref name="steerDegrees"/> / <paramref name="tiltDegrees"/>, about the fitting's clamp
        /// swivel. Hand it to <c>IHullPropRenderer.LocalRotation</c>; the swivel itself is the def's
        /// <c>PivotLocalMeters</c>, so the renderer needs no translation beyond the lateral mount.
        ///
        /// <para>Sign conventions are the rigs' own: <b>+steer</b> swings the prop to starboard (the
        /// punt's tiller to port), <b>+tilt</b> swings the leg aft and up out of the water. Both are
        /// taken in DEGREES, as the rigs state them.</para>
        /// </summary>
        public static Quaternion Rotation(float steerDegrees, float tiltDegrees)
        {
            float sa = steerDegrees * Mathf.Deg2Rad, ta = tiltDegrees * Mathf.Deg2Rad;
            float cs = Mathf.Cos(sa), ss = Mathf.Sin(sa);
            float ct = Mathf.Cos(ta), st = Mathf.Sin(ta);

            // R = Rz(steer)·Rx(−tilt), written out by column so the composition ORDER is visible.
            // Composing the other way round (tilt applied after steer) tilts about the hull's beam
            // instead of the engine's own clamp bar, and only shows up on a tilted, hard-over engine.
            var m = Matrix4x4.identity;
            m[0, 0] = cs; m[0, 1] = -ct * ss; m[0, 2] = -st * ss;
            m[1, 0] = ss; m[1, 1] = ct * cs; m[1, 2] = st * cs;
            m[2, 0] = 0f; m[2, 1] = -st; m[2, 2] = ct;
            return RigRotationMath.FromMatrix(m);
        }

        /// <summary>
        /// The rigs' own tilt clamp (<c>max(0, min(tiltMax, tilt))</c>): an outboard trims UP out of
        /// the water and never below its running position, so negative tilt is not a pose the art has.
        /// A def with no tilt authority (<c>MaxTiltDegrees</c> 0) pins to 0.
        /// </summary>
        public static float ClampTilt(float tiltDegrees, float maxTiltDegrees)
        {
            if (float.IsNaN(tiltDegrees)) return 0f;
            return Mathf.Clamp(tiltDegrees, 0f, Mathf.Max(0f, maxTiltDegrees));
        }

        /// <summary>
        /// The rigs' steer clamp for a caller working in degrees. Unlike tilt, the rigs clamp steer
        /// only on their <c>steer</c> (−1..1) input and take <c>steerDeg</c> unbounded, so this is the
        /// guard that keeps a bad column count or a NaN helm from swinging the engine past hard-over.
        /// </summary>
        public static float ClampSteer(float steerDegrees, float maxSteerDegrees)
        {
            if (float.IsNaN(steerDegrees)) return 0f;
            float max = Mathf.Max(0f, maxSteerDegrees);
            return Mathf.Clamp(steerDegrees, -max, max);
        }
    }
}
