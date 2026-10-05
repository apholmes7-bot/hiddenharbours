using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A ROCK FORM: A HEADLAND, A STACK, SKERRIES.</b> One asset per form (<c>form.snake_case</c>, e.g.
    /// <c>form.stp_lantern_stack</c>). A form's mass is not terrain: the floor is drawn flat and lit, so a face or
    /// a stack body is a baked v6 tier, y-sorted with the walls, and laid by PR 5b. The terrain carries only
    /// the ground the form stands on (terrain pass 9, part 2 §4.4):
    /// <list type="bullet">
    /// <item>a headland's top, flat at <see cref="Top"/> inside its <see cref="Brow"/>, seaward of its root line;</item>
    /// <item>a stack's plinth: flat at <see cref="PlinthTop"/>, its skirt falling <see cref="PlinthFall"/> between
    /// <see cref="PlinthSkirt"/>.x and .y of its radius. The plinth only raises the ground.</item>
    /// </list>
    /// The face, the ledge tier and the skirt ledge are recorded here for PR 5b and shape no ground in PR 5.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Terrain Plan/Form", fileName = "Form")]
    public class FormDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (form.snake_case).")]
        public string Id = "form.example";
        public string DisplayName = "";
        [Tooltip("headland, stack, stacks or skerries.")]
        public string Kind = "stack";
        public string Stone = "sandstone";
        [Tooltip("The face's batter (degrees from the horizontal). PR 5b's.")]
        public float BatterDeg = 86f;

        [Header("A headland: its brow and root")]
        [Tooltip("The brow, counter-clockwise; its last edge (last point to first) is the root line.")]
        public Vector2[] Brow = new Vector2[0];
        [Tooltip("The top stands seaward of this far inside the root line (m).")]
        public float TopInset = 0.4f;
        [Tooltip("The top (m).")]
        public float Top = 11f;

        [Header("A headland's ledge tier (PR 5b)")]
        public float LedgeHeight;
        [Tooltip("How far out from the brow (m).")]
        public float LedgeOut;
        [Tooltip("The tier runs east of this x (m).")]
        public float LedgeEastOf;

        [Header("A stack: its body (PR 5b) and its plinth (PR 5)")]
        public Vector2 Centre;
        [Tooltip("The body's radii (m).")]
        public Vector2 Radii;
        public float RotationDeg;
        [Tooltip("The plinth's radii (m), turned with the body.")]
        public Vector2 PlinthRadii;
        [Tooltip("The plinth's top (m).")]
        public float PlinthTop;
        [Tooltip("The skirt falls this far (m)...")]
        public float PlinthFall;
        [Tooltip("...between these fractions of the plinth's radius.")]
        public Vector2 PlinthSkirt = new Vector2(0.85f, 1.9f);
        [Tooltip("The skirt ledge round the body (PR 5b): radii (m), top (m).")]
        public Vector2 SkirtLedgeRadii;
        public float SkirtLedgeTop;

        [Header("Paint")]
        [Tooltip("The top's and the plinth's ground by height, where they moved it (the key scene's ground).")]
        public CoastRecipeDef Recipe;

        [Header("Source")]
        [Tooltip("The key scene's TerrainChangeDef the form came from.")]
        public string SourceId = "";
        [TextArea] public string Why = "";
    }
}
