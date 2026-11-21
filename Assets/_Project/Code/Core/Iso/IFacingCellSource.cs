namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Which of the eight sheet facings a placed piece is showing</b>, for something drawn on it (the fish on
    /// a stove) that must turn with the wall the piece backs onto. Cell <c>k</c> is the piece yawed
    /// <c>-45 deg x k</c> on the ground (<see cref="IsoGround"/>). Read through Core so a feature module never
    /// needs the piece's own class.
    /// </summary>
    public interface IFacingCellSource
    {
        /// <summary>The facing shown, 0..7.</summary>
        int FacingCell { get; }
    }
}