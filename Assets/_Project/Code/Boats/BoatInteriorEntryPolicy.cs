namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>MAY A CABIN DOOR OFFER ITSELF AT ALL?</b> — the one place that question is answered, so the
    /// owner's ruling lands in a single predicate rather than in a condition threaded through a
    /// component.
    ///
    /// <para><b>Why this is a class and not three words inside <see cref="BoatCabinDoor.IsAvailable"/>.</b>
    /// The answer is a POLICY that was expected to change once, on evidence — the S0 ruling asked for the
    /// overdraw to be rendered and shown to the owner, and named three outcomes it could have. A policy
    /// that is going to be re-decided should be somewhere a person can find, read, and change without
    /// reading a door.</para>
    ///
    /// <para><b>⭐ THE VERDICT LANDED — OUTCOME 1, THE GATE OPENS</b> (coordinator interim ruling,
    /// 2026-08-22; the owner may overturn it on his own eyeball). The overdraw at
    /// <c>InteriorRockScale</c> 0.45 is ACCEPTED. On the rendered frames the room slips inside its own
    /// house at the crest by 4.0 px (lobster), 5.8 (trawler), 6.8 (tanker), 11.6 (packet) — the
    /// 95th-percentile ink radius from the pivot times the roll the interior does not take, plus the
    /// heave it does not take. That is an OPTIMISATION problem for R1, not a reason to keep every cabin
    /// in the fleet shut. So the exterior half of the swap becomes an optimisation rather than a
    /// precondition, and the rule below is now <c>cabin != null &amp;&amp; cabin.HasInterior</c>:
    /// <b>a hull that was measured may be entered.</b></para>
    ///
    /// <para><b>What that costs, stated rather than discovered.</b> While a hull has no exterior half
    /// wired — which the S0 spike measured to be every hull in the fleet, because a mesh house is
    /// geometry rather than a sheet and there is nothing to switch off until the per-level face tags land
    /// — the interior and the exterior ARE drawn together, posed differently. ADR 0038 ruled proposals 1
    /// and 3 as a set precisely because that co-visibility is the thing the comfort scale is unsafe
    /// under; this ruling accepts the resulting overdraw <b>on measured evidence</b> rather than by
    /// forgetting the coupling. The room draws OVER the house (the hull carries a <c>SortingGroup</c>
    /// that sorts as 2D, and the interior sits at a higher order), so what the player sees is the room,
    /// with a few pixels of house showing past it at the crest.</para>
    ///
    /// <para><b>What still ends the story properly.</b> R1 — a per-level face tag in <c>TexCoord1.x</c>
    /// plus a fleet re-bake — gives the swap its exterior half, at which point
    /// <see cref="BoatInterior.SwapIsCompletable"/> begins answering true hull by hull and the overdraw
    /// goes away with no change here. That is why <see cref="BoatInteriorInstaller"/> keeps its
    /// <c>exterior:</c> argument: landing R1 is one edit, not a redesign.</para>
    ///
    /// <para><b>⚠ <see cref="BoatInterior.SwapIsCompletable"/> is NOT this, and is deliberately left
    /// alone.</b> It is a CAPABILITY — "is the mechanism whole" — and it still answers false on every
    /// mesh hull, honestly, as does <see cref="BoatInterior.ExactlyOneLayerOn"/>. This is a POLICY —
    /// "may she go in". Folding the two together is exactly what having two names prevents: the day the
    /// tags land, the capability changes and the policy does not have to.</para>
    ///
    /// <para><b>The three outcomes this predicate was waiting on</b>, kept verbatim for provenance — the
    /// judgment record is not rewritten after the fact:</para>
    /// <list type="bullet">
    ///   <item><b>Overdraw reads fine at 0.45</b> — the gate OPENS: this returns
    ///   <c>cabin != null &amp;&amp; cabin.HasInterior</c>, and the exterior half becomes an
    ///   optimisation rather than a precondition. ⭐ <b>THIS IS THE ONE THAT WAS TAKEN.</b></item>
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

            // THE POLICY, and the only line expected to change. A hull that was MEASURED may be entered;
            // the exterior half of her swap is an optimisation from here on. See the class remarks for
            // the ruling that opened this, and for what the missing half still costs until R1.
            return cabin.HasInterior;
        }
    }
}
