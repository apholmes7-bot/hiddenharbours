using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>One deck's underside, as the calm water shows it</b> (the owner, 2026-11-26: piers and anything
    /// standing over water reflect their underside). A cell of the one underside sheet
    /// (<c>docs/art/rigs/underside-kit/undersideRig.js</c>, baked to <see cref="Atlas"/>) that the calm mirror
    /// lays where a deck's reflected face draws no art: the gaps between a pier's piles, a float drawn as a top
    /// only. The cell's bottom <see cref="FasciaRows"/> rows are the deck's edge beam, laid once at the far end
    /// of the reflection; the rows above repeat back under the deck. The band is only ever as tall as the
    /// deck's freeboard, so it follows the tide by itself.
    ///
    /// <para>Presentation only: never read by the simulation, never saved. One piece per asset, with a stable,
    /// append-only <see cref="Id"/> (<c>underside.&lt;piece&gt;</c>). Every piece names the SAME sheet, so the
    /// mirror binds one texture whatever is in view (a test holds them to it).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/World/UndersideDef", fileName = "Underside")]
    public sealed class UndersideDef : ScriptableObject
    {
        [Tooltip("Stable, append-only id (underside.<piece>).")]
        public string Id = "underside.";

        [Tooltip("The one underside sheet every piece shares (Art/Textures/Water/Undersides.png): point " +
                 "filtered, no mips, 32 texels per metre.")]
        public Texture2D Atlas;

        [Header("Cell (texels, Unity's bottom-up rows: y 0 = the sheet's bottom)")]
        [Min(0)] public int CellX;
        [Min(0)] public int CellY;
        [Min(1)] public int CellWidth = 1;
        [Min(1)] public int CellHeight = 1;

        /// <summary>This piece's cell in the sheet.</summary>
        public RectInt Cell => new RectInt(CellX, CellY, CellWidth, CellHeight);

        [Tooltip("The bottom rows of the cell that are the deck's edge beam: laid once, at the far end of the " +
                 "reflection. The rows above them repeat.")]
        [Min(0)] public int FasciaRows;

        /// <summary>True when the piece can be laid: a sheet, a cell inside it, and rows left to repeat
        /// above the fascia.</summary>
        public bool IsUsable => Atlas != null && UndersideMath.CellFits(Cell, FasciaRows, Atlas.width, Atlas.height);
    }

    /// <summary>
    /// The underside lookup the calm mirror's shader makes, kept pure so EditMode tests can hold it
    /// (HiddenHarboursCalmMirror.shader, <c>UnderTexel</c>, must agree).
    /// </summary>
    public static class UndersideMath
    {
        /// <summary>A cell sits inside a <paramref name="width"/> x <paramref name="height"/> sheet and has at
        /// least one row above its fascia to repeat.</summary>
        public static bool CellFits(RectInt cell, int fasciaRows, int width, int height)
            => cell.width > 0 && cell.height > fasciaRows && fasciaRows >= 0 && cell.xMin >= 0 && cell.yMin >= 0
               && cell.xMax <= width && cell.yMax <= height;

        /// <summary>
        /// The texel laid at world art column <paramref name="artX"/> (floor(world x x art px per metre)) and
        /// <paramref name="rowFromFar"/> art rows up from the far end of the reflected face: columns wrap across
        /// the cell, the fascia rows are laid once and the rows above repeat with a period of
        /// (height - fascia).
        /// </summary>
        public static Vector2Int Texel(RectInt cell, int fasciaRows, int artX, int rowFromFar)
        {
            int w = Mathf.Max(1, cell.width);
            int x = ((artX % w) + w) % w;
            int r = Mathf.Max(0, rowFromFar);
            if (r >= fasciaRows)
            {
                int period = Mathf.Max(1, cell.height - fasciaRows);
                r = fasciaRows + (r - fasciaRows) % period;
            }
            return new Vector2Int(cell.xMin + x, cell.yMin + r);
        }
    }

    /// <summary>
    /// <b>A standable deck that names what the calm water shows of its underside.</b> An optional companion
    /// of <see cref="IStandableOutline"/>: a deck without it (or answering null) reflects exactly as before,
    /// its face mirrored and the gaps in it left as open water.
    /// </summary>
    public interface IReflectedUnderside
    {
        /// <summary>The underside piece, or null for none.</summary>
        UndersideDef Underside { get; }
    }
}
