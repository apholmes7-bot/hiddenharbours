using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// <b>The lobster-boat variant generator</b> — one rig file, EIGHTEEN hulls, from the fleet
        /// rig pack (2026-08-12).
        ///
        /// <para>3 sizes × 2 styles × 3 regions: <c>inshore</c>/<c>standard</c>/<c>offshore</c>,
        /// <c>open</c>/<c>hardtop</c>, <c>northumberland</c>/<c>fundy</c>/<c>newfoundland</c>. She
        /// is the hero hull's cousin, not her replacement — <c>lobsterBoatIsoRig.js</c> stays
        /// exactly where she is.</para>
        ///
        /// <para><b>All three axes are GEOMETRY; the paint axis is not.</b> Measured in the repo's
        /// own V8 host by hashing every cell at heading SE (lit) and NW (shaded):</para>
        /// <list type="bullet">
        /// <item><description><b>18 of 18 distinct</b> — no redundant axis, nothing collapses to a
        /// scheme. A region is not a colour: each carries its own 9-station hull table <c>T</c>, its
        /// own stem <c>rake</c>, washboard width and <c>house{}</c> block, and its own masthead
        /// signature (roof rail / mast-and-boom / dry stack).</description></item>
        /// <item><description><b>Style is geometry too, though the hull is not.</b> LOA, beam, depth
        /// and freeboard are identical across an <c>open</c>/<c>hardtop</c> pair, but their sidecars
        /// differ in <c>hull</c>, <c>DECK</c> and — for the roof-rail and stack regions — in
        /// <c>ANCHORS</c>, because the roof extension moves the masthead. Only the mast-and-boom
        /// (Fundy) region keeps its anchors, its boom being carried by the hull.</description></item>
        /// <item><description><b>The 12 paints move no vertex.</b> alphaDiff 0 for all twelve against
        /// the default; the eleven non-default schemes each recolour exactly 22 499 px of
        /// <c>standard_hardtop_fundy</c> at SE — the same pixel set, eleven times. So one mesh serves
        /// every scheme, as on every painted hull since #497.</description></item>
        /// </list>
        ///
        /// <para><b>Her paint table IS the hero's, verified rather than assumed.</b> All 12 ids and
        /// all 12 resolved ramps are byte-identical to <c>lobsterBoatIsoRig.js</c>'s, so the committed
        /// <c>paint.lobster_*</c> scheme Defs cover her too and no second table is needed. That is the
        /// one claim the pack README makes about her that measurement confirms outright.</para>
        ///
        /// <para><b>Handedness was MEASURED, not inherited</b> — boats have shipped CCW historically
        /// but the fuel kit measured CW, so the convention is never assumed. Method: the un-squashed
        /// ground-plane bearing of the +X world axis, taken from her <c>navMounts</c> port→star pair
        /// (a pure ±X separation at equal y and z), depth divided by <c>sin(40°) = 0.642788</c>
        /// BEFORE any angle, against <c>lobsterBoatIsoRig.js</c> as the registered reference IN THE
        /// SAME HOST. Both give exactly <b>+45.000° per 45° step at all eight headings</b>: CCW,
        /// matching the reference.</para>
        ///
        /// <para>The oracle is sabotage-proved in both directions. A wrong divisor visibly breaks the
        /// grid rather than passing quietly — 1.000 gives steps of 32.7/57.3 (max deviation 12.27°),
        /// sin 30° gives 7.12°, sin 50° gives 5.00°, and only 0.643 returns 0.00° — which also
        /// independently confirms the 40° elevation. And an X-mirrored rig returns a mean step of
        /// <b>−45.000</b> against the true <b>+45.000</b>: identical magnitude, opposite sign. That is
        /// exactly why the screen-space step is inadmissible for handedness and only the un-squashed
        /// bearing will do — the answer is in the SIGN, and the screen-space form does not have one.
        /// </para>
        ///
        /// <para><b>No prerequisites.</b> She generates her own paint in her own closure (OKLCH) and
        /// loads no shared library, so load order cannot go wrong here. One kit, one file — see
        /// <c>RigCatalog.cs</c> for the assembly contract.</para>
        ///
        /// <para>⚠️ <b>Not yet a mesh.</b> Her faces come from a private <c>facesFor(V)</c> driven by
        /// the exported <c>resolve(opts)</c>, not from a static <c>F</c>, so her face list is a
        /// function of the variant — which neither <see cref="HullMeshFleet"/> nor
        /// <c>RigMeshSymbols.Reconstructions</c> can express today. She is listed in
        /// <c>HullMeshFleet.NotHulls</c> with that reason. Her sidecars and deck defs are committed;
        /// the 18 <c>HullMeshDef</c>s are the follow-up.</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> LobsterVariantRigs() =>
            new RigRegistration
            {
                ["lobsterBoatVariants"] =
                    new RigEntry($"{RigFolder}/lobsterBoatVariantsIsoRig.js", "LobsterBoatVariantsIso",
                                 AzimuthConvention.CounterClockwise),
            };
    }
}
