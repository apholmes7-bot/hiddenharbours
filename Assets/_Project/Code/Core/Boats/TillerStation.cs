using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A tiller station</b>: whoever hangs a tiller outboard on a boat with no helm console, and so
    /// knows where its grip is this frame. The sibling of <see cref="IRowingStation"/>, read the same
    /// way (found on or above the hull the figure stands on), so the Player and Art modules never reach
    /// into the Boats module (rule 4). Presentation only: nothing in the simulation reads it (rule 5).
    ///
    /// <para>It answers in the rowing station's own currency, <see cref="RowingTargets"/>, because the
    /// body is the same seated body: hips on a bench, feet on the boards, one hand on a handle. The
    /// steering hand holds the tiller grip (exactly one grip is valid); the other hand rests on his
    /// thigh (<see cref="RowingTargets.RestWeight"/> is 1, so <see cref="RowingPoseSolver"/> puts the
    /// free palm there). His lean is the rest's slump: a relaxed sit at idle and a lean into the way
    /// as the throttle comes up. As the engine swings he looks into the turn, shoulders a little and
    /// head more (<see cref="RowingTargets.TurnStarboardDegrees"/>).</para>
    /// </summary>
    public interface ITillerStation
    {
        /// <summary>The station's targets this frame, or false when it is not wired or no engine hangs
        /// on the transom.</summary>
        bool TryGetTillerTargets(out RowingTargets targets);
    }

    /// <summary>The pure arithmetic of a tiller station. Static, allocation-free, deterministic.</summary>
    public static class TillerStationMath
    {
        /// <summary>
        /// The tiller grip in the hull frame: the grip's rest position on the engine fitting
        /// (<paramref name="gripAtRest"/>, in the fitting's own rig frame) turned by the engine's pose
        /// <paramref name="engineRotation"/> about its <paramref name="pivot"/>, then shifted by the
        /// fitting's <paramref name="fitmentOffset"/> (a borrowed engine's move onto this transom).
        /// Exactly the transform the fitting's mesh is drawn through, so the hand stays on the grip.
        /// </summary>
        public static Vector3 Grip(Vector3 gripAtRest, Vector3 pivot, Quaternion engineRotation, Vector3 fitmentOffset) =>
            pivot + fitmentOffset + engineRotation * (gripAtRest - pivot);

        /// <summary>How far forward he leans this frame, degrees: <paramref name="idleDegrees"/> sitting
        /// easy, less <paramref name="sitUpPerThrottleDegrees"/> times the throttle's size (ahead or
        /// astern): he sits up as she gets under way.</summary>
        public static float Lean(float idleDegrees, float sitUpPerThrottleDegrees, float throttle)
        {
            if (float.IsNaN(throttle)) throttle = 0f;
            return idleDegrees - sitUpPerThrottleDegrees * Mathf.Clamp01(Mathf.Abs(throttle));
        }

        /// <summary>How far he looks toward starboard this frame, degrees (negative = toward port):
        /// <paramref name="fullDegrees"/> times the engine's steer as a share of its authority. The
        /// rigs' <b>+steer</b> swings the prop to starboard and so the bow to starboard: he looks the
        /// way she is turning. A zero or missing authority reads dead ahead.</summary>
        public static float Look(float fullDegrees, float steerDegrees, float maxSteerDegrees)
        {
            if (float.IsNaN(steerDegrees) || !(maxSteerDegrees > 0f)) return 0f;
            return fullDegrees * Mathf.Clamp(steerDegrees / maxSteerDegrees, -1f, 1f);
        }
    }
}
