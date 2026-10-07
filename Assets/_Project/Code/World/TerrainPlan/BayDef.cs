using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A BAY'S SAND: A BEACH A GROUND ASK LAID, PAINTED BY HEIGHT.</b> One asset per bay (<c>bay.snake_case</c>, e.g.
    /// <c>bay.stp_main_beach</c>). Inside the bay, where its surface weighs over <see cref="SurfaceWeight"/>, the ground's
    /// height plus a little noise picks a band of <see cref="Recipe"/>; its back, <see cref="BackShort"/> m short of the rim
    /// or past it and over <see cref="BackAbove"/> m, takes <see cref="BackZone"/> (CD's paint rule, <c>tools/stpOnePaint.js</c>
    /// with <c>tools/stpOneBeach.js</c>; terrain PR 5 B, amendment 2 §4.5).
    ///
    /// <para>The bay's extent is its ask's: the mean-tide line (<see cref="Shore"/>) and the rim (<see cref="Rim"/>) the ground
    /// file gives that ask, read in ground metres (a unit north is 1 / sin 40° m). Its surface weighs 1 between them and fades
    /// past the rim, far out to sea and past the lines' ends. Past an end, the bay holds over <see cref="EndFill"/> of the
    /// end's weight where the ask filled (seaward of the shore, and landward where it raised the file's base), and elsewhere
    /// only where that weight passes <see cref="EndCut"/>, the return where the ask's cut stops.</para>
    ///
    /// <para>Its box is the ask's too, and CD's rule stops at it. Where the box cuts the surface while it still weighs in full
    /// (the main beach's west end, at x −8), a straight line would run down the paint; so inside the box the bay fades out over
    /// <see cref="EdgeFade"/>, from a line that wanders by <see cref="EdgeWander"/>, and the paint ends raggedly (terrain
    /// PR 5w).</para>
    ///
    /// <para>The paint reads the imported map (amendment 1 §4.6), so the bands follow the ground the player stands on. A bay
    /// is painted only on a ground file: it is the file's.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Terrain Plan/Bay", fileName = "Bay")]
    public class BayDef : ScriptableObject
    {
        [Tooltip("Stable id, append-only (bay.snake_case).")]
        public string Id = "bay.example";
        public string DisplayName = "";
        [Tooltip("The bands it paints by height, lowest first.")]
        public CoastRecipeDef Recipe;
        [Tooltip("The ground ask that laid it (ground.snake_case); its lines below are that ask's, as the ground file gives them.")]
        public string Ask = "";

        [Header("Its extent")]
        [Tooltip("The mean-tide line, west to east (world x, y).")]
        public Vector2[] Shore = new Vector2[0];
        [Tooltip("The rim, west to east (world x, y).")]
        public Vector2[] Rim = new Vector2[0];
        [Tooltip("Nothing outside this box (world x, y).")]
        public Vector2 BoxMin, BoxMax;
        [Tooltip("Inside the box it fades out over this far to the box's edge (ground m), so where the box cuts it the paint ends raggedly, not on the box's line. 0: the box cuts it.")]
        public float EdgeFade;
        [Tooltip("That fade starts up to x m either side of its line (ground m): fractal value noise (three octaves) of wavelength y (m), keyed on position.")]
        public Vector2 EdgeWander;
        [Tooltip("The shore to the rim is never taken as narrower than this (ground m).")]
        public float MinWidth = 8f;
        [Tooltip("It fades over this far past the ends of its lines (ground m)...")]
        public float EndFade = 6f;
        [Tooltip("...over this far past its rim...")]
        public float RimFade = 2f;
        [Tooltip("...and reaches x m seaward of its shore, fading over the last y m.")]
        public Vector2 SeaReach = new Vector2(30f, 8f);
        [Tooltip("Past an end, where the ask cut: the end's weight must pass x, over a width of y.")]
        public Vector2 EndCut = new Vector2(0.5f, 0.16f);
        [Tooltip("Past an end, where the ask filled: the bay holds in full over this much of the end's weight.")]
        public float EndFill = 0.6f;

        [Header("Its paint")]
        [Tooltip("The paint takes the bay where its surface weighs over this.")]
        public float SurfaceWeight = 0.45f;
        [Tooltip("The back: this far short of the rim (ground m) or past it...")]
        public float BackShort = 0.4f;
        [Tooltip("...and over this height (m)...")]
        public float BackAbove = 5.6f;
        [Tooltip("...takes this zone.")]
        public string BackZone = "grass";
        [Tooltip("The height that picks a band varies by up to x either way (m): value noise of wavelength y (m), keyed on position.")]
        public Vector2 Noise = new Vector2(0.06f, 0.77f);
        [Tooltip("Below this height (m) the bay leaves the paint as it is (CD's seafloor).")]
        public float From = -2.35f;
        [TextArea] public string Why = "";
    }
}
