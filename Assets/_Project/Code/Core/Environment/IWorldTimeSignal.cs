namespace HiddenHarbours.Core
{
    /// <summary>
    /// A scalar the world can be asked for at any game time, past ones included: the shape
    /// <see cref="WaveMemory.AverageSignal"/> averages on <see cref="WaveMemory"/>'s grid (ADR 0027 #7,
    /// the rain a water type's silt remembers). Implementations are pure in (worldSeed, gameTime) and
    /// keep no history.
    /// </summary>
    public interface IWorldTimeSignal
    {
        float ValueAt(double gameTimeSeconds);
    }
}
