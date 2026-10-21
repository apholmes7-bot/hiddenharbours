using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A REGION'S TIDAL FLATS.</b> One asset per flats (<c>flats.snake_case</c>, e.g. <c>flats.stp_south_flats</c>): the
    /// sand and mud a spring low bares off a shore, seaward of its 0 m line and clear of its walls' toes (terrain pass 9,
    /// part 2 §4.3). The ground is the ground file's (PR 5 B), so the derivation lays only the flats' paint, from where they
    /// are and how wide, their drains (the runnels), their ribs and their mussels. Their pools stand on the ground file as
    /// hollows (PR 5 B's still water); their guts are <see cref="TidalCreekDef"/>s.
    ///
    /// <para>Each section names its stretch's character (<see cref="CoastSectionDef.FlatsCharacter"/>): sand, mud, ribs or
    /// boulder.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Terrain Plan/Flats", fileName = "Flats")]
    public class FlatsDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (flats.snake_case).")]
        public string Id = "flats.example";
        public string DisplayName = "";

        [Header("Where")]
        [Tooltip("The window they are laid in: (x0, y0, x1, y1) (m).")]
        public Vector4 Window;
        [Tooltip("Their width off the 0 m line by bearing: (bearing (degrees), width (m)), bearing rising.")]
        public Vector2[] Width = new Vector2[0];
        [Tooltip("Their seaward edge lobes by this much (m) over this wavelength (m).")]
        public Vector2 Lobes;
        [Tooltip("They fade out over this many metres past their width.")]
        public float Front = 12f;
        [Tooltip("None within x (m) of a wall's toe, feathered out to y (m).")]
        public Vector2 Moat = new Vector2(3.5f, 8f);
        [Tooltip("Off a toe reef, none within x (m) of its toe, feathered out to y (m).")]
        public Vector2 ReefMoat = new Vector2(10f, 16f);

        [Header("Their ground (the ground file holds it; the masks read it)")]
        [Tooltip("The height at the 0 m line's foot (m)...")]
        public float Inner = -0.45f;
        [Tooltip("...and at their width (m).")]
        public float Outer = -1.85f;
        [Tooltip("The outer height wanders by this (m) over this wavelength (m).")]
        public Vector2 OuterVar;
        [Tooltip("Swell, ridges and ripple: amplitude (m), wavelength (m).")]
        public Vector2 Swell;
        public Vector2 Ridges;
        public Vector2 Ripple;

        [Header("Their drains (the runnels)")]
        [Tooltip("A runnel every x to y metres along the line.")]
        public Vector2 RunnelEvery;
        [Tooltip("Its width (m) and depth (m), each between x and y.")]
        public Vector2 RunnelWidth;
        public Vector2 RunnelDepth;
        [Tooltip("It meanders by x (m) over y (m).")]
        public Vector2 RunnelMeander;
        [Tooltip("It starts between these fractions of the width.")]
        public Vector2 RunnelStart;
        [Tooltip("It has x to y branches...")]
        public Vector2Int RunnelBranches;
        [Tooltip("...each leaving between these fractions of the width...")]
        public Vector2 BranchAt;
        [Tooltip("...this long (fractions of the width)...")]
        public Vector2 BranchLength;
        [Tooltip("...at this angle off the runnel (degrees)...")]
        public Vector2 BranchAngle;
        [Tooltip("...this share of its width and depth.")]
        public float BranchWidth = 0.55f;
        public float BranchDepth = 0.6f;
        [Tooltip("The trickle a runnel holds on the ground file (m). PR 5 B's still water; recorded here.")]
        public float Trickle = 0.04f;

        [Header("Their pools (the ground file's hollows; recorded here)")]
        public float PoolsPer100m = 6f;
        public Vector2 PoolRadius;
        public Vector2 PoolDepth;
        [Tooltip("Between these fractions of the width.")]
        public Vector2 PoolBand;

        [Header("Their ribs")]
        [Tooltip("Rib spacing (m), its half width (a fraction of the spacing) and its lift (m).")]
        public float RibSpacing = 9f;
        public float RibWidth = 0.13f;
        public float RibLift = 0.28f;
        [Tooltip("A rib breaks where its noise is under this...")]
        public float RibBreakUp = 0.45f;
        [Tooltip("...and wanders by this many spacings.")]
        public float RibWander = 0.55f;

        [Header("Their mussels")]
        [Tooltip("Between these heights (m)...")]
        public Vector2 MusselBand;
        [Tooltip("...in patches this wide (m) where their noise passes the cover.")]
        public float MusselLambda = 6f;
        public float MusselCover = 0.62f;

        [Header("Paint")]
        public CoastRecipeDef SandRecipe;
        public CoastRecipeDef MudRecipe;
        [Tooltip("A rib's ground.")]
        public CoastRecipeDef RibRecipe;

        [TextArea] public string Why = "";
    }
}
