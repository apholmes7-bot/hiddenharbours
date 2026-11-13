using System;
using UnityEngine;

namespace HiddenHarbours.World.Golf
{
    public enum GolfLie { Tee, Fairway, Green, Rough, Bunker, Water, OutOfBounds, Holed }

    [CreateAssetMenu(menuName = "Hidden Harbours/Golf/Surface")]
    public sealed class GolfSurfaceDef : ScriptableObject
    {
        public string Id = "golf_surface.example";
        public GolfLie Lie = GolfLie.Rough;
        [Min(0f)] public float RollingFriction = 0.12f;
        [Min(0f)] public float StaticFriction = 0.15f;
        [Range(0f, 1f)] public float Restitution = 0.2f;
        [Range(0f, 1f)] public float LandingSpeedRetention = 0.65f;
        [Range(0f, 1f)] public float LaunchSpeedMultiplier = 1f;

        public GolfSurface Build()
        {
            GolfCheck.Id(Id, "golf_surface.");
            if (!Enum.IsDefined(typeof(GolfLie), Lie) || Lie == GolfLie.OutOfBounds || Lie == GolfLie.Holed)
                throw new ArgumentException("Surface needs a ground lie.");
            GolfCheck.Positive(RollingFriction, nameof(RollingFriction));
            GolfCheck.Positive(StaticFriction, nameof(StaticFriction));
            if (StaticFriction < RollingFriction) throw new ArgumentException("Static friction is below rolling friction.");
            GolfCheck.Unit(Restitution, nameof(Restitution));
            GolfCheck.Unit(LandingSpeedRetention, nameof(LandingSpeedRetention));
            GolfCheck.Unit(LaunchSpeedMultiplier, nameof(LaunchSpeedMultiplier));
            return new GolfSurface(Lie, RollingFriction, StaticFriction, Restitution,
                LandingSpeedRetention, LaunchSpeedMultiplier);
        }
    }

    public readonly struct GolfSurface
    {
        public readonly GolfLie Lie;
        public readonly float RollingFriction, StaticFriction, Restitution, LandingSpeedRetention, LaunchSpeedMultiplier;
        internal GolfSurface(GolfLie lie, float rolling, float stationary, float bounce, float landing, float launch)
        {
            Lie = lie; RollingFriction = rolling; StaticFriction = stationary;
            Restitution = bounce; LandingSpeedRetention = landing; LaunchSpeedMultiplier = launch;
        }
    }
}
