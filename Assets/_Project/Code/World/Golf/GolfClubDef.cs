using System;
using UnityEngine;

namespace HiddenHarbours.World.Golf
{
    [CreateAssetMenu(menuName = "Hidden Harbours/Golf/Club")]
    public sealed class GolfClubDef : ScriptableObject
    {
        public string Id = "golf_club.example";
        [Min(0f)] public float FullPowerSpeedMetresPerSecond = 45f;
        [Range(0f, 89f)] public float LoftDegrees = 35f;
        [Min(0f)] public float PowerExponent = 1f;
        [Range(0f, 45f)] public float AimSpreadDegrees = 0f;

        public GolfClub Build()
        {
            GolfCheck.Id(Id, "golf_club.");
            GolfCheck.Positive(FullPowerSpeedMetresPerSecond, nameof(FullPowerSpeedMetresPerSecond));
            GolfCheck.Positive(PowerExponent, nameof(PowerExponent));
            GolfCheck.Finite(LoftDegrees, nameof(LoftDegrees));
            GolfCheck.Finite(AimSpreadDegrees, nameof(AimSpreadDegrees));
            if (LoftDegrees < 0 || LoftDegrees >= 90 || AimSpreadDegrees < 0 || AimSpreadDegrees > 45)
                throw new ArgumentException("Club angles are outside their supported range.");
            return new GolfClub(Id, FullPowerSpeedMetresPerSecond, LoftDegrees, PowerExponent, AimSpreadDegrees);
        }
    }

    public readonly struct GolfClub
    {
        public readonly string Id;
        public readonly float Speed, LoftDegrees, PowerExponent, AimSpreadDegrees;
        internal GolfClub(string id, float speed, float loft, float exponent, float spread)
        { Id = id; Speed = speed; LoftDegrees = loft; PowerExponent = exponent; AimSpreadDegrees = spread; }
    }
}
