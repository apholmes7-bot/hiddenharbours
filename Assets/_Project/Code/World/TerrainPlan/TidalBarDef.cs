using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A BAR THE TIDE BARES: A SAND RIDGE OFF A HEADLAND.</b> One asset per bar (<c>bar.snake_case</c>, e.g.
    /// <c>bar.stp_ne_bar</c>). Along its <see cref="Crest"/> (s, from the root) the crest falls
    /// <see cref="CrestTop"/> − <see cref="CrestFall"/>·k^<see cref="CrestExponent"/> + <see cref="CrestWave"/>.x·k·sin(.y·s),
    /// k = s / length, and past the tip <see cref="TipSlope"/> per metre. Across it (t, + to the lee) the section
    /// falls <see cref="FlankFall"/>·(t / half width)², the seaward side <see cref="SeawardSteepen"/> times as steep.
    /// It only raises the ground (the key scene's TerrainChangeDef: its crest and section rules, its stations).
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Terrain Plan/Tidal Bar", fileName = "TidalBar")]
    public class TidalBarDef : ScriptableObject
    {
        [Tooltip("Stable id, append-only (bar.snake_case).")]
        public string Id = "bar.example";
        public string DisplayName = "";

        [Header("The crest")]
        [Tooltip("The crest line, from the root (m).")]
        public Vector2[] Crest = new Vector2[0];
        [Tooltip("The crest's height at the root (m).")]
        public float CrestTop = 0.75f;
        public float CrestFall = 2.8f;
        public float CrestExponent = 1.15f;
        [Tooltip("The crest's swell: amplitude (m, times k), frequency (radians per m).")]
        public Vector2 CrestWave = new Vector2(0.12f, 0.9f);
        [Tooltip("Past the tip the crest falls this much per metre (m).")]
        public float TipSlope = 0.32f;

        [Header("The section")]
        [Tooltip("The half width at the root and at the tip (m).")]
        public Vector2 HalfWidth = new Vector2(8.5f, 5.5f);
        public float FlankFall = 1.7f;
        [Tooltip("The seaward flank is this many times as steep.")]
        public float SeawardSteepen = 1.3f;

        [Header("Where it is laid")]
        [Tooltip("The bar is evaluated inside this box (m): min...")]
        public Vector2 WindowMin;
        [Tooltip("...max.")]
        public Vector2 WindowMax;
        [Tooltip("It starts this far before its root (m, s).")]
        public float Before = -6f;
        [Tooltip("It reaches this far either side of the crest (m, t).")]
        public float Reach = 22f;

        [Header("Paint")]
        [Tooltip("The bar's ground by height.")]
        public CoastRecipeDef Recipe;

        [Header("Source")]
        public string SourceId = "";
        [TextArea] public string Why = "";
    }
}
