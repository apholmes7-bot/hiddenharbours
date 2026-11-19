using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>How the player's figure uses a fixture while he is at it.</summary>
    public enum FixturePose
    {
        /// <summary>He stands where he is (a basin, the plain shower stance).</summary>
        None = 0,
        /// <summary>He climbs in over the rim and sits low in the water (a bath).</summary>
        Bathe = 1,
        /// <summary>He sits on the seat (a water closet).</summary>
        Sit = 2,
        /// <summary>He steps onto the spot under the rose and stands facing out (a shower).</summary>
        Stand = 3,
    }

    /// <summary>
    /// <b>A fixture asks the player's figure to take, or leave, a pose at it</b> (owner ask 2026-11-17: "a
    /// player getting inside animation"). Published by the fixture's art (World) once per edge, read by the
    /// player's presenter (Player), so neither module knows the other's classes. Presentation only: the wash
    /// or the seat itself is the cleanliness service's and the seat point's, and nothing here is saved.
    ///
    /// <para>Every point is in WORLD units. <see cref="SeatWorld"/> is the ground point under his hips;
    /// <see cref="SeatHeightMetres"/> is how high above that floor he sits (a bath's floor, a seat's cushion),
    /// which the presenter turns into a screen lift against the height the seated pose was baked at.
    /// <see cref="EntryWorld"/> is where he stands to climb in and is put back to when he leaves.</para>
    /// </summary>
    public readonly struct FixturePoseRequested
    {
        public readonly bool On;
        public readonly FixturePose Pose;
        public readonly Vector2 SeatWorld;
        public readonly Vector2 EntryWorld;
        public readonly float SeatHeightMetres;
        public readonly float HeadingDegrees;
        public readonly string FixtureId;

        public FixturePoseRequested(bool on, FixturePose pose, Vector2 seatWorld, Vector2 entryWorld,
                                    float seatHeightMetres, float headingDegrees, string fixtureId)
        {
            On = on;
            Pose = pose;
            SeatWorld = seatWorld;
            EntryWorld = entryWorld;
            SeatHeightMetres = seatHeightMetres;
            HeadingDegrees = headingDegrees;
            FixtureId = fixtureId;
        }
    }
}
