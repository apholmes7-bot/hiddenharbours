using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The local player's body, as saved</b> (body slice 1, save schema v21). Flat and
    /// JsonUtility-friendly. Written only through <see cref="IBodyService"/>.
    ///
    /// <para><b>Unset is a real answer.</b> A negative <see cref="Frame"/>, <see cref="HeightScale"/> or
    /// <see cref="MassKg"/> means "the default": the config's <see cref="BodySettings.DefaultFrame"/>, a
    /// height scale of 1, and the lean mass of that body. That is what every save older than v21 loads as,
    /// and it is the reference body, so an existing player keeps exactly today's hunger rate and walk.</para>
    ///
    /// <para><b>Mass and stomach are (value, instant) pairs</b>, like a need: the value now is worked out
    /// from the clock (<see cref="BodyMath"/>), never ticked and stored, so a reload is exact (rule 5).</para>
    /// </summary>
    [Serializable]
    public sealed class PlayerBodyDto
    {
        /// <summary>The marker for "the default" on the three shape/mass numbers.</summary>
        public const float Unset = -1f;

        /// <summary>Bone structure, 0 small .. 1 large, or <see cref="Unset"/>. Fixed once
        /// <see cref="ShapeChosen"/>.</summary>
        public float Frame = Unset;

        /// <summary>Height as a scale on <see cref="BodySettings.ReferenceHeightMetres"/>, or
        /// <see cref="Unset"/> (= 1).</summary>
        public float HeightScale = Unset;

        /// <summary>True once the creator chose the shape. A chosen shape is never re-chosen.</summary>
        public bool ShapeChosen;

        /// <summary>Body mass in kg at <see cref="MassSinceGameSeconds"/>, or negative for the lean mass.</summary>
        public double MassKg = Unset;

        /// <summary>The game clock the mass was true at, or negative (not yet anchored).</summary>
        public double MassSinceGameSeconds = Unset;

        /// <summary>Stomach fullness in meals at <see cref="StomachSinceGameSeconds"/>.</summary>
        public float StomachMeals;

        /// <summary>The game clock the stomach fullness was true at, or negative (empty, unanchored).</summary>
        public double StomachSinceGameSeconds = Unset;

        // ---- body slice 2: fitness (same save v21) --------------------------------------------------

        /// <summary>Grip and arms (the rod).</summary>
        public FitnessStateDto Grip = FitnessStateDto.Untrained;

        /// <summary>Back and pull (hauling).</summary>
        public FitnessStateDto Back = FitnessStateDto.Untrained;

        /// <summary>Legs (carrying, lifting).</summary>
        public FitnessStateDto Legs = FitnessStateDto.Untrained;

        /// <summary>Heart and lungs (moving).</summary>
        public FitnessStateDto Cardio = FitnessStateDto.Untrained;

        public PlayerBodyDto Clone() => (PlayerBodyDto)MemberwiseClone();

        /// <summary>The saved state of <paramref name="kind"/>.</summary>
        public FitnessStateDto Get(FitnessKind kind) => kind switch
        {
            FitnessKind.Grip => Grip,
            FitnessKind.Back => Back,
            FitnessKind.Legs => Legs,
            _ => Cardio,
        };

        /// <summary>Replace the saved state of <paramref name="kind"/>.</summary>
        public void Set(FitnessKind kind, in FitnessStateDto state)
        {
            switch (kind)
            {
                case FitnessKind.Grip: Grip = state; break;
                case FitnessKind.Back: Back = state; break;
                case FitnessKind.Legs: Legs = state; break;
                default: Cardio = state; break;
            }
        }
    }

    /// <summary>
    /// <b>One mess left in the world</b> (body slice 1): where the player was sick. It stays, saved, until
    /// someone cleans it up (<see cref="IMessRegistry.CleanUp"/>). The id is stable and minted from a
    /// saved serial (<c>mess.1</c>, <c>mess.2</c>...), never from a roll. The position is in the named
    /// region's own scene frame, the convention every saved position follows (two floats, like the rest
    /// anchor, so the file holds no engine type).
    /// </summary>
    [Serializable]
    public struct MessRecordDto
    {
        public string Id;
        public string RegionId;
        public float PosX;
        public float PosY;
        public double GameSeconds;

        public MessRecordDto(string id, string regionId, Vector2 position, double gameSeconds)
        {
            Id = id;
            RegionId = regionId;
            PosX = position.x;
            PosY = position.y;
            GameSeconds = gameSeconds;
        }

        public Vector2 Position => new Vector2(PosX, PosY);
    }
}
