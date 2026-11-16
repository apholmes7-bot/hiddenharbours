using System;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>What kind of thing this is, as far as putting it away goes</b> — a field on the ITEM's Def
    /// (<c>ToolDef.Stowage</c>), never switched on an id in code (rule 2).
    ///
    /// <para><b>General is the default and means "the containers that exist already".</b> A catch, a
    /// shovel, a pail: everything that was stowed before this enum existed is General, so the holds,
    /// coolers, pails and lockers keep answering exactly as they did. Only a class with its own storage
    /// rule gets a value here, and each value needs a matching <see cref="StowageAccepts"/> flag.</para>
    ///
    /// <para>Values are serialized as ints in Def assets: <b>append only, never renumber.</b></para>
    /// </summary>
    public enum StowageClass
    {
        /// <summary>Anything without a storage rule of its own. Goes where it always went.</summary>
        General = 0,

        /// <summary>A fishing rod (owner ask 2026-11-15): only a rod rack or a locker flagged for rods
        /// takes one. Never a fish hold, a cooler, a pantry or a crate.</summary>
        Rod = 1,
    }

    /// <summary>
    /// <b>What a container takes</b> — a field on the CONTAINER's Def (<c>StowageDef.Accepts</c>). Flags,
    /// so a gear locker that takes rods AND general kit is one value (<c>General | Rods</c>) rather than a
    /// second kind of container.
    ///
    /// <para>Every container that existed before this enum (the holds, the pail, the coolers) takes
    /// <see cref="General"/> only, and is asked as <see cref="StowageRule.ExistingContainer"/>; none of
    /// their assets had to change. A rod rack takes <see cref="Rods"/> only.</para>
    /// </summary>
    [Flags]
    public enum StowageAccepts
    {
        /// <summary>Takes nothing. A container authored with no flags refuses everything, which is the
        /// safe failure: nothing can vanish into a thing nobody configured.</summary>
        None = 0,

        /// <summary>Takes <see cref="StowageClass.General"/> items.</summary>
        General = 1 << 0,

        /// <summary>Takes <see cref="StowageClass.Rod"/> items.</summary>
        Rods = 1 << 1,
    }
}
