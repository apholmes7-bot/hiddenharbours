using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// Pure, headless-testable laws for the foam a hull makes where she PUSHES the water, besides the trail
    /// she sheds (<see cref="FoamInjector"/>). The owner, 2026-11-02: "i would like the whole hull body to
    /// be responsive to making wake/foam, right now it just comes from stern from moving forward and looks
    /// off when going in reverse".
    ///
    /// <para>Three laws, all read off the hull's velocity THROUGH THE WATER in her own frame (surge along
    /// her heading, sway to starboard, yaw rate):</para>
    /// <list type="number">
    /// <item><b>The trail is shed from the end that trails.</b> Ahead that is the transom; astern it is the
    /// bow. A dead band (<see cref="HullFoamSettings.TrailFlipSpeed"/>) keeps the choice still at rest.</item>
    /// <item><b>The leading end throws foam across her.</b> The bow ahead, the transom astern, by speed
    /// along her length.</item>
    /// <item><b>The side she pushes through the water foams.</b> Each end's sideways speed is her sway plus
    /// what the turn adds at that end, so a slide foams one whole side and a pivot foams opposite
    /// quarters.</item>
    /// </list>
    ///
    /// <para>Presentation only (rule 5): it reads the sim and feeds nothing back.</para>
    /// </summary>
    public static class HullContactFoam
    {
        /// <summary>A stamp in the hull's own plan frame: x to starboard, y toward the bow, metres from
        /// her centre. <see cref="Rate01"/> is 0..1 of a fully churning hull, before the injector's own
        /// deposit rate and strength.</summary>
        public readonly struct Stamp
        {
            public readonly Vector2 From, To;
            public readonly float Radius, Rate01;
            public Stamp(Vector2 from, Vector2 to, float radius, float rate01)
            { From = from; To = to; Radius = radius; Rate01 = rate01; }
            public bool IsActive => Rate01 > 1e-5f && Radius > 0f;
        }

        static float Or(float value, float fallback) => value > 0f ? value : fallback;

        /// <summary>
        /// Whether the trail is shed from the bow this frame. <paramref name="wasFromBow"/> is last frame's
        /// answer: inside the dead band it is kept.
        /// </summary>
        public static bool TrailFromBow(float surge, bool wasFromBow, in HullFoamSettings s)
        {
            float flip = Or(s.TrailFlipSpeed, HullFoamSettings.Default.TrailFlipSpeed);
            if (surge < -flip) return true;
            if (surge > flip) return false;
            return wasFromBow;
        }

        /// <summary>
        /// The leading end's foam: a bar across her at the end she is driving into the water.
        /// </summary>
        public static Stamp Lead(float surge, float halfLength, float halfBeam, in HullFoamSettings s)
        {
            if (s.LeadWeight <= 0f || halfLength <= 0f || halfBeam <= 0f || surge == 0f) return default;
            HullFoamSettings d = HullFoamSettings.Default;
            float rate = Mathf.Clamp01(FoamBuffer.Shape01(Mathf.Abs(surge), Or(s.LeadSpeedKnee, d.LeadSpeedKnee), 1f)
                                       * s.LeadWeight);
            float y = surge > 0f ? halfLength : -halfLength;
            float span = halfBeam * Or(s.LeadSpanHalfBeams, d.LeadSpanHalfBeams);
            return new Stamp(new Vector2(-span, y), new Vector2(span, y),
                             halfBeam * Or(s.LeadRadiusHalfBeams, d.LeadRadiusHalfBeams), rate);
        }

        /// <summary>
        /// Sideways speed through the water at a point <paramref name="along"/> metres toward the bow from
        /// her centre: positive to starboard. <paramref name="yawRate"/> is counter-clockwise, rad/s, so a
        /// turn to port carries the bow to port and the stern to starboard.
        /// </summary>
        public static float LateralSpeedAt(float sway, float yawRate, float along) => sway - yawRate * along;

        /// <summary>
        /// The pushed side's foam: a line from her stern quarter to her bow quarter, each end on the side
        /// that end is moving toward. Its rate follows the faster end.
        /// </summary>
        public static Stamp Side(float sway, float yawRate, float halfLength, float halfBeam,
                                 in HullFoamSettings s)
        {
            if (s.SideWeight <= 0f || halfLength <= 0f || halfBeam <= 0f) return default;
            HullFoamSettings d = HullFoamSettings.Default;
            float reach = halfLength * Or(s.SideReach, d.SideReach);
            float bow = LateralSpeedAt(sway, yawRate, reach);
            float stern = LateralSpeedAt(sway, yawRate, -reach);
            float fastest = Mathf.Max(Mathf.Abs(bow), Mathf.Abs(stern));
            if (fastest <= 0f) return default;
            float rate = Mathf.Clamp01(FoamBuffer.Shape01(fastest, Or(s.SideSpeedKnee, d.SideSpeedKnee), 1f)
                                       * s.SideWeight);
            // An end that is not moving sideways takes the other end's side, so a slide is one straight line.
            float bowSide = bow != 0f ? Mathf.Sign(bow) : Mathf.Sign(stern);
            float sternSide = stern != 0f ? Mathf.Sign(stern) : Mathf.Sign(bow);
            return new Stamp(new Vector2(sternSide * halfBeam, -reach), new Vector2(bowSide * halfBeam, reach),
                             halfBeam * Or(s.SideRadiusHalfBeams, d.SideRadiusHalfBeams), rate);
        }

        /// <summary>
        /// A hull-frame point as the water draws it: rotated to her heading, then foreshortened in Y the
        /// way her art is (<see cref="WakeRootMath.ForeshortenY"/>), from her drawn centre.
        /// </summary>
        public static Vector2 ToDrawn(Vector2 drawnCentre, Vector2 heading, Vector2 local,
                                      float bakeElevationDegrees)
        {
            Vector2 h = heading.sqrMagnitude > 1e-8f ? heading.normalized : Vector2.up;
            var right = new Vector2(h.y, -h.x);
            Vector2 off = right * local.x + h * local.y;
            return drawnCentre + new Vector2(off.x, off.y * WakeRootMath.ForeshortenY(bakeElevationDegrees));
        }
    }
}
