using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A RAMP OF GROUND UP TO A HEADLAND'S ROOT.</b> One asset per ramp (<c>ground.snake_case</c>, e.g.
    /// <c>ground.stp_ne_neck</c>). Inland of its form's root line it climbs, a smoothstep from
    /// <see cref="From"/> <see cref="Inland"/> metres in, to <see cref="To"/> at the root line; beyond either end of the
    /// root line its flanks fall <see cref="FlankFall"/>.x per metre plus .y per metre squared. It only raises the
    /// ground, and it fades out near its <see cref="Guard"/> (the key scene's TerrainChangeDef).
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Terrain Plan/Ground Ramp", fileName = "GroundRamp")]
    public class GroundRampDef : ScriptableObject
    {
        [Tooltip("Stable id, append-only (ground.snake_case).")]
        public string Id = "ground.example";
        public string DisplayName = "";
        [Tooltip("The form whose root line it climbs to.")]
        public FormDef Form;

        [Header("The ramp")]
        [Tooltip("How far inland of the root line it starts (m).")]
        public float Inland = 16f;
        [Tooltip("Its height where it starts (m).")]
        public float From = 6f;
        [Tooltip("Its height at the root line (m).")]
        public float To = 11f;
        [Tooltip("It reaches this far seaward of the root line (m).")]
        public float SeawardReach = 0.3f;
        [Tooltip("Inland of its start it falls this much per metre (m)...")]
        public float TailFall = 0.9f;
        [Tooltip("...for this many metres.")]
        public float TailLength = 14f;
        [Tooltip("Beyond the root line's ends: m per m, m per m squared.")]
        public Vector2 FlankFall = new Vector2(0.95f, 0.06f);

        [Header("The guard: no change near a frozen piece")]
        public Vector2 Guard;
        [Tooltip("Nothing changes within this of the guard (m)...")]
        public float GuardRadius;
        [Tooltip("...and the ramp fades in over this (m) beyond it.")]
        public float GuardFade = 3f;

        [Header("Paint")]
        [Tooltip("The ramp's ground by height, where it moved it.")]
        public CoastRecipeDef Recipe;

        [Header("Source")]
        public string SourceId = "";
        [TextArea] public string Why = "";
    }
}
