namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>Is this hull being ROWED?</b> Asked of the boat root by the water's wave layer, which draws a
    /// rowed hull's waves as softly broken arcs (the owner's approved dory look of 11-21) and every other
    /// hull's, motored or sailed, as the clean V of her arms (the owner, 11-21: "make motored boats a v
    /// wake").
    ///
    /// <para><b>Why a seam in Core.</b> The answer belongs to the Boats lane (her oars, and whether her
    /// outboard is running) and the question is the Art lane's (the foam injector that lays her waves),
    /// and neither may reference the other (rule 4). Nothing is registered: a hull whose root answers
    /// nothing is not rowed, so she lays the V.</para>
    ///
    /// <para>A pure read of state the implementor already holds, presentation only: nothing in the
    /// simulation reads it and nothing is saved (rule 5).</para>
    /// </summary>
    public interface IRowedHull
    {
        /// <summary>True while she is driven by oars: oars fitted and wired, and no engine running.</summary>
        bool IsRowed { get; }
    }
}
