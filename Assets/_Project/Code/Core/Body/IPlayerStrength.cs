using System;
using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The player's strength and cardio, read-only</b> (body slice 2). The seam Fishing, Boats and the
    /// player controller may read later; nothing reads it yet beyond the walk. Every value is a multiplier on
    /// today's behaviour and is EXACTLY 1 for the default, untrained body (pinned by tests), so wiring a
    /// reader in changes nothing until the player trains or the creator picks a different body.
    /// </summary>
    public interface IPlayerStrength
    {
        /// <summary>Grip and arms against today's rod: 1 untrained default body, more trained or bigger.</summary>
        float RodStrength { get; }

        /// <summary>Back and pull against today's haul.</summary>
        float PullStrength { get; }

        /// <summary>Legs against today's lifts and footwork.</summary>
        float LegStrength { get; }

        /// <summary>What the body carries comfortably at a walk, kg (legs and back).</summary>
        float CarryCapacityKg { get; }

        /// <summary>Cardio level 0..1 (0 untrained).</summary>
        float CardioLevel { get; }

        /// <summary>Level 0..1 of <paramref name="kind"/> now.</summary>
        float FitnessLevel(FitnessKind kind);
    }

    /// <summary>Today's player: every strength exactly 1, untrained, the reference capacity.</summary>
    public sealed class NeutralStrength : IPlayerStrength
    {
        public static readonly NeutralStrength Instance = new NeutralStrength();
        public float RodStrength => 1f;
        public float PullStrength => 1f;
        public float LegStrength => 1f;
        public float CarryCapacityKg => GameServices.StrengthSettings.ReferenceCarryCapacityKg;
        public float CardioLevel => 0f;
        public float FitnessLevel(FitnessKind kind) => 0f;
    }

    /// <summary>
    /// <b>What the player is carrying, by weight</b> (body slice 2). The hands module (or a pack, later)
    /// installs one on <see cref="GameServices.CarriedLoad"/>; the default carries nothing, so the walk is
    /// today's. Kept a seam because most hand loads have no weight yet (only <c>CatchItem.WeightKg</c>).
    /// </summary>
    public interface ICarriedLoad
    {
        /// <summary>Weight carried now, kg (0 or less: nothing).</summary>
        float CarriedKg { get; }
    }

    /// <summary>The default load: nothing.</summary>
    public sealed class NoCarriedLoad : ICarriedLoad
    {
        public static readonly NoCarriedLoad Instance = new NoCarriedLoad();
        public float CarriedKg => 0f;
    }

    /// <summary>
    /// An activity was reported to the body (body slice 2): which, how much, the kcal it cost and the hunger
    /// it added. For audio (breathing), the look (sweat, posture) and the debug view; never a number on screen.
    /// </summary>
    public readonly struct ActivityReported
    {
        public readonly string ActivityId;
        public readonly float Units;
        public readonly double Kcal;
        public readonly float HungerAdded;

        public ActivityReported(string activityId, float units, double kcal, float hungerAdded)
        {
            ActivityId = activityId;
            Units = units;
            Kcal = kcal;
            HungerAdded = hungerAdded;
        }
    }

    /// <summary>
    /// <b>Every <see cref="ActivityDef"/> and <see cref="GymEquipmentDef"/></b>, loaded once from
    /// <c>Resources/Activities</c> and <c>Resources/GymEquipment</c> (the <see cref="TraitService"/> pattern).
    /// </summary>
    public static class ActivityCatalog
    {
        public const string ActivitiesFolder = "Activities";
        public const string EquipmentFolder = "GymEquipment";

        private static ActivityDef[] _activities;
        private static GymEquipmentDef[] _equipment;

        public static IReadOnlyList<ActivityDef> Activities => _activities ??= Load<ActivityDef>(ActivitiesFolder);

        public static IReadOnlyList<GymEquipmentDef> Equipment => _equipment ??= Load<GymEquipmentDef>(EquipmentFolder);

        /// <summary>The activity with <paramref name="id"/>, or null.</summary>
        public static ActivityDef Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            IReadOnlyList<ActivityDef> all = Activities;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && string.Equals(all[i].Id, id, StringComparison.Ordinal)) return all[i];
            return null;
        }

        /// <summary>The first activity (by id) bound to <paramref name="signal"/>, or null.</summary>
        public static ActivityDef ForSignal(ActivitySignal signal)
        {
            if (signal == ActivitySignal.None) return null;
            IReadOnlyList<ActivityDef> all = Activities;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].Signal == signal) return all[i];
            return null;
        }

        /// <summary>Forget the loaded catalog (tests).</summary>
        public static void Reset()
        {
            _activities = null;
            _equipment = null;
        }

        private static T[] Load<T>(string folder) where T : UnityEngine.Object
        {
            T[] found = Resources.LoadAll<T>(folder) ?? Array.Empty<T>();
            Array.Sort(found, (a, b) => string.CompareOrdinal(IdOf(a), IdOf(b)));   // stable order, not load order
            return found;
        }

        private static string IdOf(UnityEngine.Object o) => o switch
        {
            ActivityDef a => a.Id ?? "",
            GymEquipmentDef g => g.Id ?? "",
            _ => "",
        };
    }
}
