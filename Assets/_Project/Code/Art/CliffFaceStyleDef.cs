using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>One face of a style: the kit face it stands in for, and its own three channels.</summary>
    [Serializable]
    public struct CliffStyleFace
    {
        [Tooltip("The kit's px face (its _index, the colour slot a wall band names) this face replaces.")]
        public Texture2D Base;
        [Tooltip("This style's _index: palette rows and bands, the same rows the base face packs.")]
        public Texture2D Index;
        [Tooltip("This style's tangent-space normal (A cavity AO).")]
        public Texture2D Normal;
        [Tooltip("This style's mask (R key with cast shadow, G sky, B height, A coverage).")]
        public Texture2D Mask;

        public bool HasChannels => Base != null && Index != null && Normal != null && Mask != null;
    }

    /// <summary>
    /// <b>ONE CLIFF FACE STYLE.</b> A second way of drawing a rock's px faces: the same rock, aspect and batter,
    /// the same form field, tiers, cast shadow and palette rows as the kit's face, but a different texel pattern
    /// (the key-scene boards' regular courses, crisp bed lines, pale interbeds and block-by-block facets, for
    /// <c>cliffstyle.board_coursed</c>). One asset per style, stable append-only id (<c>cliffstyle.snake_case</c>).
    ///
    /// <para><b>Baked, not painted.</b> <c>CliffFaceStyleBaker</c> runs the px kit's own <c>face()</c> and
    /// <c>channels()</c> in the rig host with the rock's painter swapped for the style's
    /// (<see cref="PainterScriptPath"/>), passing <see cref="OptionsJson"/> as its tunables, and writes the faces
    /// to <see cref="OutputFolder"/> (committed, LFS, stable GUIDs). <see cref="Faces"/> is filled by that bake.</para>
    ///
    /// <para>Presentation only: a face texture. No collider, brow, blocker or walk rule reads it.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Art/Cliff Face Style", fileName = "CliffStyle")]
    public class CliffFaceStyleDef : ScriptableObject
    {
        /// <summary>Every style id starts with this.</summary>
        public const string IdPrefix = "cliffstyle.";

        [Header("Identity")]
        [Tooltip("Stable id, append-only (cliffstyle.snake_case).")]
        public string Id = "cliffstyle.example";

        [Header("What it draws")]
        [Tooltip("The kit rock whose faces this style redraws (its palette rows are kept).")]
        public string Rock = "sandstone";
        [Tooltip("The add-on painter script, repo-relative. Loaded after the px rig; installs globalThis.PxCliffCoursed.")]
        public string PainterScriptPath = "docs/art/rigs/px-cliff-styles/pxCliffCoursed.js";
        [Tooltip("Where the baker writes this style's faces (asset folder).")]
        public string OutputFolder = "Assets/_Project/Art/Terrain/CliffStyles/Coursed";
        [Tooltip("The file-name prefix of this style's faces, in place of the rock's (SandstoneCoursed_S_S76_index).")]
        public string FilePrefix = "SandstoneCoursed";

        [Header("The courses (texels; the kit draws 16 a metre)")]
        [Tooltip("A course's height on a vertical wall, min and max. A batter stretches it by 1/sin.")]
        public Vector2Int CourseTexels = new Vector2Int(8, 13);
        [Tooltip("Chance a course boundary carries a thin pale interbed (the silt row).")]
        [Range(0f, 1f)] public float PaleShare = 0.22f;
        [Tooltip("A pale interbed's height, min and max.")]
        public Vector2Int PaleTexels = new Vector2Int(2, 4);
        [Tooltip("Vertical joint spacing along a course, min and max: a block is this long.")]
        public Vector2Int JointSpacingTexels = new Vector2Int(30, 60);
        [Tooltip("Chance a vertical joint is cut through its course; the rest are closed and the slab runs on.")]
        [Range(0f, 1f)] public float JointCut = 0.6f;
        [Tooltip("Chance per segment that a bed line steps one texel up or down.")]
        [Range(0f, 1f)] public float BedStep = 0.4f;
        [Tooltip("A bed-line segment's length. The tile holds a whole number of them, so it still wraps.")]
        [Min(4)] public int BedSegmentTexels = 32;

        [Header("The facets")]
        [Tooltip("Chance a block stands proud, and the same chance it sits back.")]
        [Range(0f, 0.5f)] public float ProudShare = 0.28f;
        [Tooltip("A proud block's detail height: how hard the live light facets it.")]
        [Min(0f)] public float ProudRelief = 1.6f;
        [Tooltip("Chance a block's top row is a lit lip (a proud block always has one).")]
        [Range(0f, 1f)] public float LipShare = 0.6f;
        [Tooltip("Chance of a one-texel tone speck inside a block.")]
        [Range(0f, 0.5f)] public float SpeckShare = 0.03f;
        [Tooltip("Chance a block sits back (in shade, never black: its fill keeps the course tone, only its top row is shadow).")]
        [Range(0f, 0.5f)] public float SunkShare = 0.05f;

        [Header("The texture inside a block (pass 3: no large flat fills)")]
        [Tooltip("Share of a block's area a broad stain darkens one band (0 = none).")]
        [Range(0f, 1f)] public float StainShare = 0.3f;
        [Tooltip("Chance per texel column of a short vertical hairline joint inside a course (1 to 3 texels long).")]
        [Range(0f, 0.3f)] public float TickShare = 0.05f;
        [Tooltip("Chance a row inside a course carries a darker flaggy parting (thin lamination).")]
        [Range(0f, 1f)] public float PartingShare = 0.35f;
        [Tooltip("Mean spacing of small chips and sockets on the face, in texels (0 = none).")]
        [Min(0)] public int ChipPitchTexels = 9;

        [Header("The baked faces (filled by CliffFaceStyleBaker)")]
        public List<CliffStyleFace> Faces = new List<CliffStyleFace>();

        [TextArea] public string Why = "";

        /// <summary>The painter's tunables as the JS object the add-on reads (invariant culture).</summary>
        public string OptionsJson()
        {
            var sb = new StringBuilder("{");
            void Pair(string k, Vector2Int v) => sb.Append($"\"{k}\":[{v.x},{v.y}],");
            void Num(string k, float v) => sb.Append($"\"{k}\":{v.ToString("R", CultureInfo.InvariantCulture)},");
            Pair("course", CourseTexels);
            Num("pale", PaleShare);
            Pair("paleTh", PaleTexels);
            Pair("jsp", JointSpacingTexels);
            Num("jgate", JointCut);
            Num("step", BedStep);
            Num("seg", BedSegmentTexels);
            Num("proud", ProudShare);
            Num("relief", ProudRelief);
            Num("lip", LipShare);
            Num("speck", SpeckShare);
            Num("sunk", SunkShare);
            Num("stain", StainShare);
            Num("tick", TickShare);
            Num("part", PartingShare);
            Num("chip", ChipPitchTexels);
            sb.Length--;
            return sb.Append('}').ToString();
        }

        // ---- the lookup a wall band uses --------------------------------------------------------------------

        [NonSerialized] Dictionary<Texture2D, CliffStyleFace> _byBase;

        /// <summary>This style's face for a kit face (the band's colour-slot texture), when it has one.</summary>
        public bool TryGetFace(Texture2D baseFace, out CliffStyleFace face)
        {
            face = default;
            if (baseFace == null) return false;
            if (_byBase == null)
            {
                _byBase = new Dictionary<Texture2D, CliffStyleFace>();
                foreach (CliffStyleFace f in Faces)
                    if (f.HasChannels && !_byBase.ContainsKey(f.Base)) _byBase.Add(f.Base, f);
            }
            return _byBase.TryGetValue(baseFace, out face);
        }

        private void OnValidate() => _byBase = null;
    }
}
