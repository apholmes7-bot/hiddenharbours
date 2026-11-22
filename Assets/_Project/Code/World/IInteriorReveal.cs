namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>What happens at a building's threshold</b> (ADR 0060 section 3): the one seam between
    /// <see cref="BuildingInterior"/>, which decides whether the occupant is inside and on which storey,
    /// and whatever shows that.
    ///
    /// <para>Two implementations. The sprite swap (shell off, room and furniture on, one storey at a time)
    /// is built into <see cref="BuildingInterior"/> and is the picture every existing house keeps,
    /// unchanged. The mesh reveal is <see cref="DwellingCutaway"/>: outside, no cut; inside on storey
    /// <i>n</i>, the cut for that storey's level. Either way <see cref="BuildingInterior"/> stays the one
    /// definition of inside (ADR 0036); a reveal never decides it.</para>
    ///
    /// <para>Called on real transitions only (entering, leaving, changing storey, and the re-assert on
    /// configure and enable), never per frame, so an implementation may do a component lookup but must not
    /// poll.</para>
    /// </summary>
    public interface IInteriorReveal
    {
        /// <summary>Show the building as seen with the occupant <paramref name="inside"/> or not, on
        /// <paramref name="storey"/> (0 the ground floor; always 0 when outside).</summary>
        void Reveal(bool inside, int storey);
    }
}
