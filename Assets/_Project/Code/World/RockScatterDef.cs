using System;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>ONE SCATTER OF SHORE ROCKS.</b> One asset per scatter (<c>scatter.snake_case</c>, e.g.
    /// <c>scatter.stp_stack_aprons</c>): the rule part 2 scattered it by (round a form's feet, along walls in a band
    /// off their toes, or over sections in a height band) and every rock it laid, with their count (terrain PR 5w, §4.7).
    /// A rock is off a beach's sand, out of the opened span, off every path and inside its band.
    ///
    /// <para>Plain data, no behaviour: the rocks are drawn from the Rock Px kit by these records when a later pass lays
    /// them. Nothing reads it yet.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Rock Scatter", fileName = "RockScatter")]
    public class RockScatterDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (scatter.snake_case).")]
        public string Id = "scatter.example";
        public string DisplayName = "";

        [Header("The rule")]
        [Tooltip("around (a form's feet), walls (off their toes) or sections (a height band).")]
        public string Rule = "around";
        [Tooltip("The forms it rings (FormDef ids), the sections it covers (CoastSectionDef ids), or its walls' first and last " +
                 "(wall ids, clockwise).")]
        public string[] Over = new string[0];
        [Tooltip("Round a foot: from x to y metres past its radius. Off a toe: from x to y metres seaward.")]
        public Vector2 Band;
        [Tooltip("Over sections: between these heights (m).")]
        public Vector2 Elevation;
        [Tooltip("One rock every x to y metres.")]
        public Vector2 Every;
        [Tooltip("The kit's forms it draws, by weight.")]
        public RockFormWeight[] Forms = new RockFormWeight[0];
        [Tooltip("The kit's stone.")]
        public string Stone = "sandstone";
        [Tooltip("The tide row: dry, wet or awash.")]
        public string State = "wet";

        [Header("What it laid")]
        [Tooltip("How many rocks it laid: Rocks' length.")]
        public int Count;
        public ScatterRock[] Rocks = new ScatterRock[0];

        [TextArea] public string Why = "";
    }

    /// <summary>A form a scatter draws, and its weight.</summary>
    [Serializable]
    public struct RockFormWeight
    {
        public string Form;
        public int Weight;
    }

    /// <summary>One rock a scatter laid: its place, the ground's height there and its form.</summary>
    [Serializable]
    public struct ScatterRock
    {
        [Tooltip("Its index in the scatter's draw.")]
        public int N;
        public Vector2 At;
        [Tooltip("The ground file's height under it (m).")]
        public float Elevation;
        public string Form;
    }
}
