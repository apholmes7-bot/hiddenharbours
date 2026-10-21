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
    /// <para>Part 2's six (PR 5w: the Harbour Stack, the Whelps, the Old Man, the Sisters and the gaps' skerries) are in
    /// the ground file already. Their Defs record them; the derivation only paints inside their <see cref="Feet"/>.</para>
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

        [Header("Part 2's stacks and skerries (PR 5w): Defs only, on the ground file")]
        [Tooltip("A group's members: (x, y, radius (m), top (m)). A skerry's top is its plinth's.")]
        public Vector4[] Members = new Vector4[0];
        [Tooltip("The plinth's radius (m) for a stack, and a group's plinth radius before its members' share of it.")]
        public float PlinthRadius;
        [Tooltip("Past a foot's radius, the plinth's skirt falls this many metres per metre.")]
        public float PlinthSkirtSlope;
        [Tooltip("Each foot the form stands on: (x, y, radius (m)). The south paints its recipe inside each, on the ground.")]
        public Vector3[] Feet = new Vector3[0];

        [Header("Paint")]
        [Tooltip("The top's and the plinth's ground by height, where they moved it (the key scene's ground); part 2's feet.")]
        public CoastRecipeDef Recipe;

        [Header("Source")]
        [Tooltip("The key scene's TerrainChangeDef the form came from.")]
        public string SourceId = "";
        [TextArea] public string Why = "";
    }
}
