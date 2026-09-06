using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>THE DOORWAY, AS A PLACE YOU WALK THROUGH</b> — the pure geometry of a cabin threshold, so that
    /// "she has stepped through the door" is one measurement asked in one frame rather than two walkers'
    /// private opinions about where a door is.
    ///
    /// <para><b>Why a band and not a line.</b> The owner's 2026-08-28 ruling is that an open door "a
    /// player can walk through freely", and a walker crossing a mathematical LINE is a walker who has to
    /// hit a doorway to the millimetre on a deck that is rolling under her — the least cozy thing a boat
    /// can do (P5). The band is the doorway's own <see cref="BoatInteriorDoor.ClearWidthMeters"/>, which
    /// is the hole the kit actually measured: half of it either side of the threshold point is exactly
    /// "standing in the doorway", and it is DATA rather than a feel knob (rule 6). A door whose sidecar
    /// omits the width has no band at all, which is honest — you cannot walk through an opening nobody
    /// measured — and <see cref="BoatCabinDoor"/> keeps the press-through for her.</para>
    ///
    /// <para><b>⭐ Hull-local, and therefore frame-free.</b> Every quantity here is in the rig's own
    /// metres (+x starboard, +y bow) — the frame <see cref="HullLocalAnchor"/>'s own tooltip names as the
    /// one "every authored deck polygon, fitting pivot and door threshold already speaks". So the test is
    /// the same on the sole and on the deck, at every heading, with no projection, no bake elevation and
    /// no handedness in it. That matters more than it looks: the cabin walk folds the hull's measured
    /// handedness into its projection and the deck walk assumes the counter-clockwise convention, so a
    /// threshold test done in WORLD offsets would have to pick one of them and would mirror the doorway
    /// end for end on the hulls that disagree.</para>
    ///
    /// <para><b>The release radius is the hysteresis</b>, and it is derived rather than typed: one whole
    /// clear width from the threshold is one doorway's own width of daylight between her and the door —
    /// the nearest distance at which she is unambiguously NOT standing in it. Without it a walker resting
    /// on the sill would cross, be re-seated a centimetre away still inside the band, and cross back, and
    /// the cabin would strobe at the frame rate. <see cref="BoatCabinDoor"/> owns the latch itself,
    /// because both sides of one doorway must share one.</para>
    ///
    /// <para>Pure, static, allocation-free and deterministic — <see cref="BoatCabinWalkMath"/>'s
    /// discipline, for its reason: the rule about where a player may walk is worth asserting without a
    /// scene.</para>
    /// </summary>
    public static class BoatCabinThreshold
    {
        /// <summary>Where the doorway is, in the hull's own metres — the threshold point with its sill
        /// HEIGHT dropped, because a band is a place on a floor and the height is what says which floor
        /// (<see cref="BoatInterior.LevelIndexAtHeight"/> asks that question, and it is the only thing
        /// that should).</summary>
        public static Vector2 PointOf(BoatInteriorDoor door)
            => door == null ? Vector2.zero : new Vector2(door.ThresholdPoint.x, door.ThresholdPoint.y);

        /// <summary>
        /// <b>How near the threshold is "in the doorway"</b>, metres — half the clear width the kit
        /// measured. Zero for a door with no measured opening, and every caller reads that as "this
        /// threshold cannot be walked" rather than as a small band.
        /// </summary>
        public static float BandRadiusMetres(BoatInteriorDoor door)
            => door == null ? 0f : Mathf.Max(0f, door.ClearWidthMeters) * 0.5f;

        /// <summary>
        /// <b>How far clear she must get before the doorway will take her again</b>, metres — one whole
        /// clear width, i.e. twice the band. See the class remarks: the ratio is the hysteresis and it is
        /// stated once, here.
        /// </summary>
        public static float ReleaseRadiusMetres(BoatInteriorDoor door)
            => door == null ? 0f : Mathf.Max(0f, door.ClearWidthMeters);

        /// <summary>True when this door has an opening wide enough to walk through at all. False is DATA:
        /// a sidecar that never measured the leaf has not described a doorway, and a zero-width band
        /// would silently make the threshold unwalkable rather than saying so.</summary>
        public static bool HasBand(BoatInteriorDoor door) => BandRadiusMetres(door) > 0f;

        /// <summary>Is <paramref name="hullLocal"/> standing in this doorway?</summary>
        public static bool IsInBand(BoatInteriorDoor door, Vector2 hullLocal)
        {
            float radius = BandRadiusMetres(door);
            if (radius <= 0f) return false;
            return (hullLocal - PointOf(door)).sqrMagnitude <= radius * radius;
        }

        /// <summary>Is she far enough from this doorway that the next approach is a fresh one? False for
        /// a door with no band, which is the safe answer: nothing may arm a threshold that cannot be
        /// crossed.</summary>
        public static bool IsClearOfBand(BoatInteriorDoor door, Vector2 hullLocal)
        {
            float radius = ReleaseRadiusMetres(door);
            if (radius <= 0f) return false;
            return (hullLocal - PointOf(door)).sqrMagnitude > radius * radius;
        }
    }
}
