using System.Globalization;
using System.Text;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>A clifftop's turf lip, as one generated strip.</b> The meadow runs untouched to the brow; this strip is
    /// what hangs over it: a ragged fringe of blades over the edge, the sod mat rolled over it, clumps of long
    /// grass falling down the face, the dark undercut the mat throws, the cut soil of the overburden with its
    /// roots and stones, and now and then a block of turf that has slid and clings lower.
    ///
    /// <para>Drawn at the GROUND's texel, <see cref="CliffWaterlineMath.TexelsPerMetre"/> a metre, one texel per
    /// pixel of the strip, and never stretched: <see cref="CliffWallSurface"/> lays it rigid on the jagged brow
    /// (owner 11-26: no mixels). The strip is baked by the px kit's rig host from
    /// <see cref="PainterScriptPath"/> with <see cref="OptionsJson"/> (<c>CliffTurfLipBaker</c>), committed, and
    /// held equal to a fresh bake by <c>CliffTurfLipBakeTests</c>. Every size below is in texels.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "CliffTurfLip_", menuName = "Hidden Harbours/Art/Cliff Turf Lip")]
    public class CliffTurfLipDef : ScriptableObject
    {
        /// <summary>The id prefix: <c>clifflip.</c></summary>
        public const string IdPrefix = "clifflip.";

        [Header("Identity")]
        public string Id = "clifflip.example";
        [Tooltip("The painter script, repo-relative. Loaded after the px kit's pixel language; installs globalThis.PxCliffTurfLip.")]
        public string PainterScriptPath = "docs/art/rigs/px-cliff-styles/pxCliffTurfLip.js";
        [Tooltip("Where the baker writes the strip (asset path of the PNG).")]
        public string OutputPath = "Assets/_Project/Art/Terrain/CliffStyles/TurfLip/TurfLip.png";
        [Tooltip("The baked strip. Filled by the bake.")]
        public Texture2D Strip;

        [Header("The strip")]
        [Tooltip("The strip's size. Its length is how far a run goes before the lip repeats.")]
        public Vector2Int StripTexels = new Vector2Int(2048, 96);
        [Tooltip("The row of the brow line: the rows above it carry only the fringe standing over the edge.")]
        [Min(1)] public int LineRow = 10;
        public int Seed = 9127;

        [Header("The edge")]
        [Tooltip("Height of a fringe blade over the line, min and max.")]
        public Vector2Int Fringe = new Vector2Int(1, 6);
        [Range(0f, 1f)] public float FringeShare = 0.55f;
        [Tooltip("Thickness of the sod mat rolled over the edge, min and max.")]
        public Vector2Int Sod = new Vector2Int(4, 8);
        [Tooltip("Share of the edge where the mat is torn away and the soil shows.")]
        [Range(0f, 0.6f)] public float TornShare = 0.1f;
        [Tooltip("Depth of the undercut shade under the mat, min and max.")]
        public Vector2Int Shade = new Vector2Int(5, 11);

        [Header("The hanging grass")]
        [Tooltip("Spacing between hanging clumps, min and max.")]
        public Vector2Int ClumpGap = new Vector2Int(8, 26);
        [Tooltip("Half width of a clump, min and max.")]
        public Vector2Int ClumpHalf = new Vector2Int(4, 11);
        [Tooltip("Length of a clump's longest blades, min and max.")]
        public Vector2Int Hang = new Vector2Int(12, 34);
        [Tooltip("Length of the short blades between clumps, min and max.")]
        public Vector2Int Short = new Vector2Int(3, 8);

        [Header("The soil apron")]
        [Tooltip("How far below the line the cut soil runs before it breaks up over the rock, min and max. 0 0 draws no apron.")]
        public Vector2Int Apron = new Vector2Int(30, 54);
        [Tooltip("Share of soil texels that are stones.")]
        [Range(0f, 0.2f)] public float StoneShare = 0.012f;
        [Tooltip("Share of columns that dangle a root, and a root's length, min and max.")]
        [Range(0f, 0.5f)] public float RootShare = 0.06f;
        public Vector2Int Root = new Vector2Int(5, 16);

        [Header("Slumps and tufts")]
        [Tooltip("Spacing between slumped turf blocks, min and max (0 0 = none).")]
        public Vector2Int SlumpGap = new Vector2Int(150, 330);
        [Tooltip("How far below the line a slumped block or a tuft clings, min and max.")]
        public Vector2Int SlumpDrop = new Vector2Int(16, 46);
        [Tooltip("Mean spacing of small clinging tufts (0 = none).")]
        [Min(0)] public int TuftGap = 46;

        [Header("Colours (hex, darkest first)")]
        public string[] Grass = { "#253832", "#334333", "#445338", "#565f40", "#707959" };
        public string[] Soil = { "#2a1c16", "#3d2a20", "#573b30", "#6c4c38", "#8b6a50" };
        public string Straw = "#9a8d58";
        public string Stone = "#8a8478";
        public string ShadeColour = "#160d0a";
        [Tooltip("Alpha of the undercut, near the mat and far from it, 0 to 255.")]
        public Vector2Int ShadeAlpha = new Vector2Int(190, 110);

        [Header("The rock riser (a bank's own lip, owner 11 27: banks with no wall read tall)")]
        [Tooltip("Height of the upright rock face painted under the soil, min and max. 0 0 = none (a wall's lip: the wall is the face).")]
        public Vector2Int RockRiser = Vector2Int.zero;
        [Tooltip("Spacing of the riser's upright joints, min and max.")]
        public Vector2Int RockJointGap = new Vector2Int(7, 15);
        [Tooltip("Rows of shade the riser throws on the ledge at its foot.")]
        [Min(0)] public int RockToeShade = 2;
        [Tooltip("The riser's rock, darkest first: joint and toe line, foot, middle, top, a joint's lit edge (the cliff palette's sandstone).")]
        public string[] Rock = { "#2d2828", "#59382d", "#7c513a", "#955a3d", "#ad7856" };

        [Header("The terrace (a ledge wall's lip, owner 11 27: stepped cliffs keep their flat grass ledge)")]
        [Tooltip("Rows of flat turf painted under the riser (or under the soil where there is none), min and max: the " +
                 "grass step between two tiers, lit along its outer edge. 0 0 = none.")]
        public Vector2Int TerraceRows = Vector2Int.zero;
        [Tooltip("Rows of shade the terrace's outer edge throws on the face below it.")]
        [Min(0)] public int TerraceShade = 3;

        [Header("On the wall")]
        [Tooltip("Where the rough edges step the brow line on a wall that faces across the screen, the step's riser is " +
                 "seen side on and the lip laid over it is a wedge of turf in the rock. A step seen this many metres " +
                 "across dissolves the lip out there altogether; smaller steps keep part of it. 0 = never (the lip " +
                 "follows every step). A wall that faces down the screen has no riser to see and is untouched.")]
        [Min(0f)] public float RiserFadeMetres = 0f;

        [TextArea] public string Why = "";

        /// <summary>Metres of shore one strip covers before it repeats.</summary>
        public float StripMetresS => StripTexels.x / CliffWaterlineMath.TexelsPerMetre;
        /// <summary>Metres of the strip above the brow line (on the meadow's side).</summary>
        public float InlandMetres => Mathf.Clamp(LineRow, 0, StripTexels.y) / CliffWaterlineMath.TexelsPerMetre;
        /// <summary>Metres of the strip below the brow line (down the face).</summary>
        public float FaceMetres => Mathf.Max(0, StripTexels.y - LineRow) / CliffWaterlineMath.TexelsPerMetre;
        /// <summary>Whether a wall can draw this lip.</summary>
        public bool IsDrawable => Strip != null && StripTexels.x > 0 && StripTexels.y > LineRow;

        /// <summary>The painter's options, as the JSON object the baker hands it.</summary>
        public string OptionsJson()
        {
            var sb = new StringBuilder("{");
            void I(string k, int v) => sb.Append('"').Append(k).Append("\":").Append(v).Append(',');
            void F(string k, float v) => sb.Append('"').Append(k).Append("\":").Append(v.ToString("0.####", CultureInfo.InvariantCulture)).Append(',');
            void R(string k, Vector2Int v) => sb.Append('"').Append(k).Append("\":[").Append(v.x).Append(',').Append(Mathf.Max(v.x, v.y)).Append("],");
            void S(string k, string v) => sb.Append('"').Append(k).Append("\":\"").Append(v).Append("\",");
            void A(string k, string[] v)
            {
                sb.Append('"').Append(k).Append("\":[");
                for (int i = 0; i < v.Length; i++) sb.Append(i > 0 ? "," : "").Append('"').Append(v[i]).Append('"');
                sb.Append("],");
            }
            I("w", StripTexels.x); I("h", StripTexels.y); I("line", LineRow); I("seed", Seed);
            R("fringe", Fringe); F("fringeShare", FringeShare); R("sod", Sod); F("tornShare", TornShare); R("shade", Shade);
            R("clumpGap", ClumpGap); R("clumpHalf", ClumpHalf); R("hang", Hang); R("short", Short);
            R("apron", Apron); F("stoneShare", StoneShare); F("rootShare", RootShare); R("root", Root);
            R("slumpGap", SlumpGap); R("slumpDrop", SlumpDrop); I("tuftGap", TuftGap);
            A("grass", Grass); A("soil", Soil); S("straw", Straw); S("stone", Stone); S("shadeColour", ShadeColour);
            sb.Append("\"shadeAlpha\":[").Append(ShadeAlpha.x).Append(',').Append(ShadeAlpha.y).Append("],");
            R("riser", RockRiser); R("jointGap", RockJointGap); I("toeShade", RockToeShade); A("rock", Rock);
            R("terrace", TerraceRows); I("terraceShade", TerraceShade);
            sb.Length--;
            return sb.Append('}').ToString();
        }
    }
}
