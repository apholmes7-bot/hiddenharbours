using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A GROUND FILE: A REGION'S HEIGHTS AS A PACKAGE GIVES THEM.</b> One asset per file (<c>ground.snake_case</c>,
    /// e.g. <c>ground.stp_island</c>): the base height texture, its identity, and the asks laid on it, in order.
    /// <see cref="GroundFileImport"/> reads it and writes the region's R16 height map: "E(x, y) is the last ask whose
    /// patch covers (x, y), bilinear between its samples; elsewhere the base", then the game's own asks.
    ///
    /// <para>The package's JSON stays out of the repo: its base and its asks' samples are here as PNGs, with the
    /// hashes the import checks. A region's <see cref="RegionTerrainPlanDef.Ground"/> names its file.</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/Terrain Plan/Ground File", fileName = "GroundFile")]
    public class GroundFileDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (ground.snake_case): the file's own id.")]
        public string Id = "ground.example";
        public string DisplayName = "";
        [Tooltip("The file's schema.")]
        public string Schema = "hidden-harbours/island-ground@2";
        [Tooltip("The package the file came in (its folder name).")]
        public string Package = "";
        [Tooltip("The file's sha256 (the JSON).")]
        public string FileSha256 = "";

        [Header("The base")]
        [Tooltip("The base height texture: 16-bit grey, north up (row 0 at the rect's north edge).")]
        public Texture2D Base;
        [Tooltip("The file's name for the base.")]
        public string BaseTexture = "";
        [Tooltip("sha256 of the base's samples, u16 big-endian, rows top-down (the file's pixelsSha256): the import stops on a mismatch.")]
        public string BasePixelsSha256 = "";
        [Tooltip("The file's fileSha256 for the base. A copy may differ (an added chunk) while its pixels match.")]
        public string BaseFileSha256 = "";
        [Tooltip("The base's range (m): height = x + (y - x) × code / 65535.")]
        public Vector2 BaseRange = new Vector2(-4f, 7f);
        [Tooltip("The base's rect (world units): its south-west corner...")]
        public Vector2 RectMin = new Vector2(-380f, -260f);
        [Tooltip("...and its north-east corner. Texel (col, row) from the top-left is centred at x = min.x + (col + 0.5) × w, y = max.y − (row + 0.5) × h.")]
        public Vector2 RectMax = new Vector2(380f, 260f);

        [Header("The asks, in order")]
        [Tooltip("The package's asks in the file's order, then the game's own. The last covering patch wins.")]
        public GroundAskDef[] Asks = new GroundAskDef[0];

        [Header("The walls it keeps")]
        [Tooltip("The walls the file keeps, by real id (the last three digits of the scene's CliffWall_..._NNN), the toe-lifted ones " +
                 "among them: at each one's toe stations its channel holds water at a spring low wherever the base does (amendment 1 " +
                 "§4.5). The re-lined, banked and opened walls are PR 5w's, and their channels are guarded there.")]
        public string[] KeptWalls = new string[0];
        [Tooltip("Where the kept walls come from: the table, its sha256, and how it counts them.")]
        public string KeptWallsFrom = "";
    }
}
