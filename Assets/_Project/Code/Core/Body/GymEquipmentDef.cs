using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One piece of gym equipment, as data</b> (body slice 2, owner ask 2026-11-15: "add workout equipment
    /// and a gym to the NMC region"). One asset each under <c>Data/Resources/GymEquipment</c>, a stable id
    /// (<c>gym_equipment.snake_case</c>). It names the exercises it offers (<see cref="ActivityDef"/> ids);
    /// the world slice places it (plan: <c>docs/world/nmc-gym.md</c>) and reports a set through
    /// <see cref="IBodyService.ReportActivity"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "GymEquipment", menuName = "Hidden Harbours/Body/Gym Equipment")]
    public sealed class GymEquipmentDef : ScriptableObject
    {
        [Tooltip("Stable id, gym_equipment.snake_case. Append-only: never rename.")]
        public string Id;

        public string DisplayName;

        [Tooltip("Exercise ids (ActivityDef) this machine offers.")]
        public string[] ExerciseIds = Array.Empty<string>();

        [Tooltip("Floor space it takes, in tiles (width x depth), for the gym layout.")]
        public Vector2Int FootprintTiles = Vector2Int.one;
    }
}
