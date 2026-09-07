using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// 🔴 <b>THE ONE PLACE A WAKE SPRINGS FROM (register row 29).</b> The owner, 2026-09-06, in play:
    /// <i>"the foam seemed off-centred with three different sections leaving the boat."</i>
    ///
    /// <para>Three families of foam leave a hull — the advected buffer's sheet (<c>FoamInjector</c>), the
    /// sprite deposits' banded lobes and the crest lines' hatched dashes (<c>BoatWakeEmitter</c>) — and
    /// before this class each computed its own stern point, from its own length, through its own copy of
    /// the projection. They disagreed in two ways at once:</para>
    ///
    /// <list type="bullet">
    /// <item><b>Two length sources.</b> The buffer took the RIG-lofted <c>HullMeshDef.WakeSternOffsetMeters</c>
    /// (6.40 m on the cape, re-derived from the rig in water PR 11b); the sprite families took
    /// <c>BoatHullDef.LengthMeters</c>/2 (6.45) plus their own nudges. That is the same 12.9-vs-12.8
    /// disagreement 11b found and fixed on ONE side only.</item>
    /// <item><b>Two projections.</b> <c>FoamBuffer.SternWorld</c> clamped the elevation to [1, 90]; the
    /// sprite path's <c>ForeshortenY</c> answered 1 for anything at or below 0. They agree at the 40° the
    /// iso kits bake at and disagree at the degenerate ends — which is exactly the kind of drift a second
    /// copy exists to produce.</item>
    /// </list>
    ///
    /// <para><b>So there is one function, and it lives in Core</b> because the two callers are in different
    /// modules (<c>HiddenHarbours.Art</c>'s foam buffer and <c>HiddenHarbours.Boats</c>' emitter) and
    /// rule 4 says cross-module agreement goes through Core. One quantity, one computation.</para>
    ///
    /// <para>Pure and static: no clock, no RNG, nothing saved (rule 5). The wake is drawn, not simulated.</para>
    /// </summary>
    public static class WakeRootMath
    {
        /// <summary>The elevation that means "a plan view": no foreshortening at all.</summary>
        public const float PlanViewElevationDegrees = 90f;

        /// <summary>
        /// How much of a north–south distance ON THE WATER the ¾ camera actually draws, given the
        /// elevation the hull's art was baked at. X is untouched; Y is squashed by this.
        ///
        /// <para>A plan view (90°, or a hand-drawn compass that is not a bake at all) returns exactly 1, and
        /// so does a degenerate or NaN elevation — an anchor with no art fact behind it must land where it
        /// always did, never collapsed onto the hull's own origin.</para>
        /// </summary>
        public static float ForeshortenY(float bakeElevationDegrees)
        {
            if (float.IsNaN(bakeElevationDegrees)) return 1f;
            if (bakeElevationDegrees <= 0f || bakeElevationDegrees >= PlanViewElevationDegrees) return 1f;
            return Mathf.Sin(bakeElevationDegrees * Mathf.Deg2Rad);
        }

        /// <summary>
        /// Walk <paramref name="alongHeading"/> metres from the hull's origin along her bow (+ toward the
        /// bow, − astern) and project the result the way her artwork is drawn.
        ///
        /// <para>The foreshortening is not decoration. The stern is a distance ON THE WATER, and a ¾ camera
        /// draws that distance in full to the east and only <c>sin(elevation)</c> of it to the north — so an
        /// unprojected anchor sits off the transom by an amount that OPENS AND CLOSES through every turn.
        /// On the cape that is 4.15–6.40 m of screen. The plume anchor paid for this lesson once already
        /// (<i>"not even connected to it and way off to the stern"</i>).</para>
        ///
        /// <para>A degenerate bow falls back to +Y so the anchor is never NaN.</para>
        /// </summary>
        public static Vector2 ProjectAlongHeading(Vector2 origin, Vector2 bow, float alongHeading,
                                                  float bakeElevationDegrees)
        {
            Vector2 dir = bow.sqrMagnitude > 1e-8f ? bow.normalized : Vector2.up;
            Vector2 off = dir * alongHeading;
            return origin + new Vector2(off.x, off.y * ForeshortenY(bakeElevationDegrees));
        }

        /// <summary>
        /// 🔴 <b>THE ROOT.</b> Where this hull sheds her wake: <paramref name="sternOffsetMeters"/> astern of
        /// her origin along her bow, projected the way her art is drawn. Every foam family reads this.
        ///
        /// <para>An offset of 0 or less returns the origin unchanged — the shipped behaviour for a hull whose
        /// stern has never been measured. Callers that have a hull LENGTH to fall back on should resolve it
        /// through <see cref="SternOffsetMeters"/> first rather than passing 0 here.</para>
        /// </summary>
        public static Vector2 SternWorld(Vector2 origin, Vector2 bow, float sternOffsetMeters,
                                         float bakeElevationDegrees)
        {
            if (sternOffsetMeters <= 0f) return origin;
            return ProjectAlongHeading(origin, bow, -sternOffsetMeters, bakeElevationDegrees);
        }

        /// <summary>
        /// 🔴 <b>THE ONE LENGTH, resolved.</b> How far astern this hull's transom is: the RIG-lofted
        /// <c>HullMeshDef.WakeSternOffsetMeters</c> when she has one, and half her
        /// <c>BoatHullDef.LengthMeters</c> when she does not.
        ///
        /// <para><b>Why the fallback exists and is not a hedge.</b> The rig-lofted offset is authored on the
        /// 34 mesh hull defs — the whole real fleet. A hull drawn by a sprite compass (the ambient fleet)
        /// has no rig to loft from and reports 0, and for her half the length is exactly the rule the sprite
        /// wake has always used. So a mesh hull gains the one measured number and a sprite hull is
        /// byte-unchanged, which is what makes this a fix rather than a re-tune of everything afloat.</para>
        ///
        /// <para>⚠️ The two numbers are NOT interchangeable where both exist: the cape's rig lofts 12.8 m
        /// where her <c>BoatHullDef</c> says 12.9, so LOA/2 is 6.45 against the rig's 6.40. That 0.05 m — and
        /// the nudges layered on top of it — is what put her three foam families 0.20 m apart.</para>
        /// </summary>
        /// <param name="riggedSternOffsetMeters">The presenter's own offset; 0 when she has no rig.</param>
        /// <param name="hullLengthMeters">Her <c>BoatHullDef.LengthMeters</c>, the fallback's source.</param>
        public static float SternOffsetMeters(float riggedSternOffsetMeters, float hullLengthMeters)
        {
            if (riggedSternOffsetMeters > 0f) return riggedSternOffsetMeters;
            return Mathf.Max(0f, hullLengthMeters) * 0.5f;
        }

        /// <summary>
        /// 🔴 <b>THE ONE WIDTH.</b> How wide this hull's wake is at her transom, in metres either side of the
        /// track: her watertight half-beam when she has one, else a fraction of her length.
        ///
        /// <para>Before row 29 the buffer's sheet took <c>WatertightHalfBeamMeters</c> (2.4 m on the cape)
        /// while the sprite deposits took <c>length × ShoulderHalfWidthFraction</c> (12.9 × 0.14 = 1.81 m) —
        /// so the two halves of one wake disagreed about how wide the boat was by a quarter. A wake is as
        /// wide as the hull that made it, and the hull's beam is the number that says so.</para>
        ///
        /// <para>The length fraction survives as the fallback for a hull with no measured beam, which is the
        /// sprite fleet again — unchanged for them, correct for everyone else.</para>
        /// </summary>
        /// <param name="watertightHalfBeamMeters">The presenter's own half-beam; 0 when she has no rig.</param>
        /// <param name="hullLengthMeters">Her length, the fallback's source.</param>
        /// <param name="lengthFraction">The legacy <c>ShoulderHalfWidthFraction</c>.</param>
        public static float WakeHalfWidthMeters(float watertightHalfBeamMeters, float hullLengthMeters,
                                                float lengthFraction)
        {
            if (watertightHalfBeamMeters > 0f) return watertightHalfBeamMeters;
            return Mathf.Max(0f, hullLengthMeters) * Mathf.Max(0f, lengthFraction);
        }
    }
}
