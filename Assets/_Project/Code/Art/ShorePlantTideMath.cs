using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// The <b>tide rule for a placed shore plant</b> — which of the five baked tide states a plant is
    /// wearing right now, and whether it is under water. Pure, stateless, allocation-free: same
    /// inputs, same answer, forever.
    ///
    /// <para><b>⭐ THE TIDE IS AN AXIS, NOT A VARIANT — and this is where that becomes true at
    /// runtime.</b> The rig resolves ONE number, <c>overM</c> = water height over the plant's OWN
    /// ground, and drives four things off it together so they cannot disagree: algae LAY down when
    /// drained, WET fades gloss→bleach as the water falls away, everything below the waterline takes
    /// the cold WATER ramp, and SWAY switches breeze↔surge (and stops dead when a drained plant is
    /// limp). All four are already baked into the five states; this class only picks which one.</para>
    ///
    /// <para><b>⚠ THE PLANT'S GROUND IS THE PAINTED SEABED, NOT ITS NOMINAL ZONE.</b> The kit's zone
    /// table is a staircase of NOMINAL base heights (subtidal fringe 0.15 m … upland 4.65 m) used to
    /// bake the states. A plant actually placed at St Peters stands on whatever
    /// <c>ITidalTerrain.ElevationAt</c> says, which is not that number — so the live
    /// <c>overM = WaterLevelAt(t) − ElevationAt(pos)</c> is mapped onto the ladder the states were
    /// baked at (<c>step.waterM − zone.baseM</c>). Feed the nominal zone base in instead and every
    /// plant would swap state on the tide clock rather than on its own depth, which is exactly the
    /// thing one height map with three consumers exists to prevent.</para>
    ///
    /// <para><b>Nothing here is saved (rule 5).</b> Water level is recomputed from
    /// <c>(worldSeed, gameTime)</c> and elevation is authored geometry; the plant's visual state is a
    /// pure function of the two and never enters a save.</para>
    /// </summary>
    public static class ShorePlantTideMath
    {
        /// <summary>The five baked tide steps, low → high — the sheet's own columns.</summary>
        public const int TideStates = 5;

        /// <summary>
        /// Which baked column a plant wears, given live water height over its own ground and the
        /// ladder of <c>overM</c> values the columns were baked at.
        ///
        /// <para>Nearest rung, not a threshold sweep: the runtime axis is CONTINUOUS and the sheet
        /// samples it five times, so the honest answer to "which of these five is this" is the
        /// closest one. Ties resolve to the DRIER state — a plant that is exactly borderline reads
        /// better standing than drowned, and it makes the choice total (never two answers for one
        /// input).</para>
        /// </summary>
        /// <param name="overM">Live water height over the plant's own ground, in metres. Negative =
        /// the water has fallen below the plant's feet.</param>
        /// <param name="ladder">The baked columns' own <c>overM</c>, low → high. Must be
        /// <see cref="TideStates"/> long; a shorter array is clamped rather than thrown on, because a
        /// half-built Def must not take the scene down.</param>
        public static int StateFor(float overM, float[] ladder)
        {
            if (ladder == null || ladder.Length == 0) return 0;

            int best = 0;
            float bestDist = Mathf.Abs(overM - ladder[0]);
            for (int i = 1; i < ladder.Length; i++)
            {
                float d = Mathf.Abs(overM - ladder[i]);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        /// <summary>
        /// Water height over a plant's own ground: the ONE number the whole tide response turns on.
        /// <paramref name="waterLevel"/> and <paramref name="groundElevation"/> are both metres above
        /// chart datum — the same frame <see cref="Core.ITidalTerrain"/>, the tide model and
        /// <c>TidalExposure</c> all use, so this is the project's one depth rule and not a second one.
        /// </summary>
        public static float OverGround(float waterLevel, float groundElevation) =>
            waterLevel - groundElevation;

        /// <summary>
        /// Is the plant's <b>whole standing height</b> under water? This is the SORTING question, not
        /// the state question: a fully submerged plant is drawn beneath the water surface so the
        /// sea's own shallow transparency reveals it, while a plant sticking out through the surface
        /// must draw in the decor band or its emergent half would be painted over by the water.
        ///
        /// <para><b>⚠ Height, not depth alone.</b> A 2.75 m sugar kelp under 1 m of water is NOT
        /// submerged — it is lying across the surface. Testing <c>overM &gt; 0</c> would sink every
        /// tall plant the moment its holdfast got wet, which is the bug this signature exists to make
        /// impossible to write.</para>
        /// </summary>
        /// <param name="overM">Water height over the plant's own ground (see <see cref="OverGround"/>).</param>
        /// <param name="standingHeightM">The plant's true standing height in metres — the kit's own
        /// <c>heightM</c>, which is world height and not a sprite dimension.</param>
        public static bool IsSubmerged(float overM, float standingHeightM) =>
            overM >= Mathf.Max(0f, standingHeightM);

        /// <summary>
        /// Where the plant draws. Two bands, and the choice is the whole submerged-rendering trick:
        ///
        /// <list type="bullet">
        /// <item><b>Under water</b> → <paramref name="submergedOrder"/>, which sits BELOW the Sea
        /// plane (−5) and ABOVE the painted seabed (−21…−18). The water shader already fades toward
        /// transparent in the shallows (the shore-fade path), so a bed drawn in that gap is simply
        /// VISIBLE THROUGH the water where it is shallow and lost as it deepens — no new
        /// transparency system, no second waterline.</item>
        /// <item><b>Emergent</b> → the ordinary <see cref="YSortSprite"/> decor band, sorted against
        /// the player like grass, because that is what it now is.</item>
        /// </list>
        ///
        /// <para>Returns the sorting order to write; <paramref name="ySorted"/> reports whether
        /// <see cref="YSortSprite"/> should own the order instead (it does, above water).</para>
        /// </summary>
        public static int SortingOrderFor(bool submerged, int submergedOrder, int emergentOrder,
                                          out bool ySorted)
        {
            ySorted = !submerged;
            return submerged ? submergedOrder : emergentOrder;
        }
    }
}
