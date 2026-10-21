namespace HiddenHarbours.World
{
    /// <summary>What kind of boundary a property is enclosed with. Data, not art: the placement pass
    /// resolves each of these to the yard kit's own pieces, and a property changes its character by
    /// changing this one word.</summary>
    public enum YardFence
    {
        /// <summary>No fence. The mow line alone says where the property ends — which is what most of
        /// a rural road actually looks like, and the reason this is a value rather than an absence.
        /// </summary>
        None,

        /// <summary>Painted pickets. The village front, and the one that reads as KEPT.</summary>
        Picket,

        /// <summary>Post and rail. A working property, a bigger frontage, no paint to keep up.</summary>
        PostRail,

        /// <summary>Split rail — the oldest and the cheapest, and what a place that has been let go
        /// still has standing.</summary>
        SplitRail,

        /// <summary>Page wire on posts. Keeps a dog in and a deer out; a working yard, not a front.
        /// </summary>
        Wire,

        /// <summary>Fieldstone. What the ground gave up when it was cleared, stacked at the edge of the
        /// land it came out of.</summary>
        Stone,

        /// <summary>A clipped hedge instead of a fence. Blocks the same ground, reads as care.</summary>
        Hedge,
    }

    /// <summary>
    /// <b>ONE YARD — the ground a property holds, and what encloses it.</b> A row in a region's yard
    /// table, and the single authored fact the lawn, the wild-grass suppression and the fence all read.
    ///
    /// <para><b>⭐ AUTHORED ONCE, DERIVED THREE WAYS</b> (lead-architect's lawn ruling, 2026-08-20). The
    /// polygon is the mow line, the grass gate and the fence line — not three shapes that have to be
    /// kept in agreement, one shape asked three questions. The failure this prevents is the one that is
    /// invisible until you walk it: a lawn painted to one edge, wild grass cleared to another, and a
    /// fence standing on a third.</para>
    ///
    /// <para><b>The geometry is DERIVED from what the yard faces</b>, not authored as an angle. A
    /// dooryard is between the house and the road, so the row states the house it belongs to, how big
    /// the yard is, and what it faces; <see cref="Facing"/> does the arithmetic. Move the road and the
    /// yard turns to follow it — which is exactly what a magic rotation number cannot do (rule 6).</para>
    /// </summary>
    /// <summary>
    /// <b>How well a household keeps its grass</b> — the owner's 2026-08-26 ruling, as data.
    ///
    /// <para>It is a LADDER POSITION on the Lawn terrain material, not a set of separate looks: the
    /// splat encodes a channel's value as both blend weight and ladder position, so a lower style is
    /// simultaneously a rougher cut AND more of the wild meadow showing through. That is why "kept
    /// but rough" needs no second material — see <c>StPetersLawns</c>.</para>
    /// </summary>
    public enum MownStyle
    {
        /// <summary>Grass that gets cut when somebody remembers. Half weight, so the wild band shows
        /// through it — a let-go property, not a bad lawn.</summary>
        Rough = 0,

        /// <summary>An ordinary kept dooryard. The island's default.</summary>
        Kept = 1,

        /// <summary>Kept, and mown in stripes — a household that takes some pride in it. The stripes
        /// themselves are drawn in the shader from this yard's own long axis; this only says that it
        /// HAS them.</summary>
        Striped = 2,
    }

}
