using System;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>One rock's colour in a cliff skin: the base the px kit cuts its six tiers and five bands from
    /// (<c>PxCliffFace.DEF[rock].base</c>), and the spread it cuts them with (<c>DEF[rock].c</c>).</summary>
    [Serializable]
    public struct CliffSkinRock
    {
        [Tooltip("The kit's rock or accessory name: sandstone, till, basalt, or an accessory id (silt, moss, dust, " +
                 "peb, turf, grit).")]
        public string Name;
        [Tooltip("The base colour, #rrggbb. The kit's own procedure cuts the tiers and bands from it.")]
        public string BaseHex;
        [Tooltip("The band spread: 1 is the kit's own contrast for sandstone, 0.85 for till.")]
        public float Contrast;
    }

    /// <summary>
    /// <b>ONE CLIFF SKIN.</b> A palette the px cliff faces can be drawn in. A face's <c>_index</c> map names a
    /// palette ROW (the rock and its tier) and a BAND, never a colour, so a second palette draws the same rock
    /// with the same beds, joints and shadows in another set of colours: exact, banding intact, no rebake of a
    /// face. One asset per skin, with a stable append-only id (<c>cliffskin.snake_case</c>).
    ///
    /// <para><b>Where its colours come from.</b> Not painted by hand into a table: each rock's base and spread
    /// are data here, and <c>CliffSkinPaletteBaker</c> runs them through the px kit's own
    /// <c>paletteLUT()</c> in the rig (V8), so the tiers lean to the kit's cold ambient and warm key the same
    /// way the shipped palette's do. A rock this skin does not name keeps the kit's own colour.</para>
    ///
    /// <para>A skin with no <see cref="Palette"/> is the material's own palette: the current look.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Art/Cliff Face Skin", fileName = "CliffSkin")]
    public class CliffFaceSkinDef : ScriptableObject
    {
        /// <summary>Every skin id starts with this.</summary>
        public const string IdPrefix = "cliffskin.";

        [Header("Identity")]
        [Tooltip("Stable id, append-only (cliffskin.snake_case).")]
        public string Id = "cliffskin.example";

        [Header("Its palette")]
        [Tooltip("The 8 x 32 palette LUT this skin draws in, baked from the rocks below by CliffSkinPaletteBaker. " +
                 "Empty = the CliffFace material's own palette (the current look).")]
        public Texture2D Palette;
        [Tooltip("Where the baker writes the palette (asset path). Empty for the material's own palette.")]
        public string PalettePath = "";

        [Header("Its colours (the px kit's own procedure cuts the tiers and bands from these)")]
        [Tooltip("Rocks whose base this skin moves. A rock not named keeps the kit's colour.")]
        public CliffSkinRock[] Rocks = new CliffSkinRock[0];
        [Tooltip("Accessory palettes (silt, moss, dust, peb, turf, grit) whose base this skin moves.")]
        public CliffSkinRock[] Accessories = new CliffSkinRock[0];

        [Header("Its source")]
        [Tooltip("The boards its colours were read off (paths under the art desk).")]
        public string[] SourceBoards = new string[0];
        [TextArea] public string Why = "";

        /// <summary>True when the skin draws in the material's own palette.</summary>
        public bool IsMaterialPalette => Palette == null && string.IsNullOrEmpty(PalettePath);
    }
}
