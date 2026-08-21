namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>MAY A CABIN DOOR OFFER ITSELF AT ALL?</b> — the one place that question is answered, so the
    /// owner's ruling lands in a single predicate rather than in a condition threaded through a
    /// component.
    ///
    /// <para><b>Why this is a class and not three words inside <see cref="BoatCabinDoor.IsAvailable"/>.</b>
    /// The answer is a POLICY that is expected to change once, on evidence — the S0 ruling asked for the
    /// overdraw to be rendered and shown to the owner, and named three outcomes it could have. A policy
    /// that is going to be re-decided should be somewhere a person can find, read, and change without
    /// reading a door.</para>
    ///
    /// <para><b>The policy today: a door may offer only when the swap it opens can COMPLETE</b> — both
    /// halves of ADR 0038's layer swap wired (<see cref="BoatInterior.SwapIsCompletable"/>). After the
    /// mesh-hull spike that is no hull in the fleet: the exterior half needs a per-level face tag that
    /// does not exist yet. So every cabin is built, every door is registered, and every one of them
    /// declines — deliberately.</para>
    ///
    /// <para><b>What is being protected.</b> The interior takes only <c>InteriorRockScale</c> of the
    /// hull's rock. That is safe ONLY because the two are never drawn together (ADR 0038 ruled
    /// proposals 1 and 3 as a set). Opening onto a half-wired swap puts the interior and the exterior on
    /// screen at once, posed differently — the exact co-visibility the ADR forbids.</para>
    ///
    /// <para><b>⭐ THE THREE OUTCOMES THIS PREDICATE IS WAITING ON</b>, from the S0 ruling, so whoever
    /// applies the owner's verdict does not have to reconstruct it:</para>
    /// <list type="bullet">
    ///   <item><b>Overdraw reads fine at 0.45</b> — the gate OPENS: this returns
    ///   <c>cabin != null &amp;&amp; cabin.HasInterior</c>, and the exterior half becomes an
    ///   optimisation rather than a precondition.</item>
    ///   <item><b>Fine only at 1.0</b> — the gate opens, and mesh hulls PIN the interior rock scale to
    ///   1.0 until the per-level tags land, flagged as a deliberate departure from the ruled 0.45.</item>
    ///   <item><b>Neither</b> — this stands exactly as written, and the cabins wait for R1.</item>
    /// </list>
    ///
    /// <para>Pure and static: no state, no lifecycle, no allocation. It is a rule, and a rule should be
    /// readable in one screen.</para>
    /// </summary>
    public static class BoatInteriorEntryPolicy
    {
        /// <summary>
        /// Whether <paramref name="cabin"/>'s door may advertise itself and accept a press.
        ///
        /// <para>Null-safe: a door with no cabin offers nothing, which is what a hull nobody has
        /// measured should do.</para>
        /// </summary>
        public static bool MayOffer(BoatInterior cabin)
        {
            if (cabin == null) return false;
            if (!cabin.HasInterior) return false;

            // THE POLICY, and the only line expected to change. See the class remarks for the three
            // outcomes the owner's overdraw verdict picks between.
            return cabin.SwapIsCompletable;
        }
    }
}
