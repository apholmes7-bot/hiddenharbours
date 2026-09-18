namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A hull renderer that can draw her cabin door OPEN or SHUT</b>: the 2026-08-28 door ruling
    /// ("the press moves the LEAF"), as a seam.
    ///
    /// <para><b>Why this exists.</b> A mesh hull used to carry her door leaf baked into her one mesh at
    /// <c>doorOpen 0</c>, so a press flipped <c>BoatCabinDoor.IsOpen</c> and the picture never moved.
    /// The owner's playtest said exactly that ("i dont see the doors open"). The bake now keeps the leaf
    /// OUT of <see cref="HullMeshDef.Mesh"/> and stores it twice, as
    /// <see cref="HullMeshDef.DoorLeafClosed"/> and <see cref="HullMeshDef.DoorLeafOpen"/>, and the
    /// renderer draws whichever one this seam last asked for.</para>
    ///
    /// <para><b>A THIRD interface, not a widening of <see cref="IHullMeshRenderer"/> or
    /// <see cref="IHullCutaway"/></b>, for the reason <see cref="IHullCutaway"/> gives: test doubles
    /// implement both, and a new member on either stops every one of them compiling in files nobody
    /// touched. Ask for the capability with <c>GetComponentInChildren&lt;IHullDoorLeaf&gt;</c>; a
    /// renderer without it is simply absent.</para>
    ///
    /// <para><b>Who drives it.</b> The Boats lane's <c>BoatCabinDoor</c>, from its own
    /// <c>IsOpen</c>: Boats owns the door and may not name the Art type that implements this.</para>
    /// </summary>
    public interface IHullDoorLeaf
    {
        /// <summary>
        /// True when the configured hull carries her leaf in both poses. False on a hull with no door,
        /// and on a door hull whose mesh predates the split (her leaf is still inside
        /// <see cref="HullMeshDef.Mesh"/>, drawn shut, which is the shipped picture).
        /// </summary>
        bool CarriesDoorLeaf { get; }

        /// <summary>The pose last asked for. False (shut) until somebody says otherwise.</summary>
        bool DoorLeafShownOpen { get; }

        /// <summary>
        /// Draw the leaf open or shut. Idempotent and allocation-free, so a caller may push its state
        /// every frame. On a hull with no leaf it only records the answer, and a hull configured later
        /// draws that answer.
        /// </summary>
        void ShowDoorLeaf(bool open);
    }
}
