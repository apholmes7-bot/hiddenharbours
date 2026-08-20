using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// The dooryard &amp; landscaping kit: 62 pieces of what a townsperson puts on their own lawn.
        ///
        /// <para>One kit, one file (see <c>RigCatalog.cs</c> for the assembly contract). The comments on
        /// the entry below are MEASUREMENTS someone paid for — move them with the entry, never summarise
        /// them away.</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> YardKitRigs() =>
            new RigRegistration
            {
                // ---- the yard & landscaping kit (owner drop of 2026-08-20) -------------------------
                //
                // 62 pieces over 10 families — fences and gates, hedges, beds, planters, dooryard
                // fixtures, sitting, play, ornaments, paths and signs. Fixed 470×540 buffer, pivot
                // (235,442) MEASURED to be the footprint's ground centre, PX 32. render(key, dir, opts),
                // returning a BARE RGBA array — the IsoPropSheetBaker call shape exactly.
                //
                // ⚠️⚠️ COUNTER-CLOCKWISE, MEASURED — AND THE KIT'S OWN SIDECAR SAYS CLOCKWISE. This
                // declaration is the one line that decides whether every cell of every piece is drawn
                // facing the right way, and the sidecar contradicts it. The rig's gameplay sidecar
                // publishes `contract.facings = [N,NE,E,SE,S,SW,W,NW]` against its own dir index; the
                // MEASURED bearings of the face axis (+Y, by the kit's own axis contract) step the other
                // way round the compass:
                //
                //     dir     0    1    2    3    4    5    6    7
                //     +Y      N    NW   W    SW   S    SE   E    NE     ← measured (this kit)
                //     sidecar N    NE   E    SE   S    SW   W    NW     ← declared, and wrong
                //
                // The two agree only at dir 0 and dir 4, where the sequences cross. Take the sidecar at
                // its word and a consumer asking for "E" gets a panel facing WEST — a plausible picture,
                // no error anywhere. The measurement was controlled the only admissible way: the screen-y
                // sign was FIXED first (model +Z projects to dy = −24.51, so screen y grows downward),
                // because a wrong sign mirrors the table and inverts the conclusion. Full record:
                // docs/art/rigs/yard-landscaping-kit/IMPORT.md §6.
                //
                // ⚠️ Do NOT settle a disagreement here from the rig's `th = dir*Math.PI/4` sign term or
                // from a screen °/step figure — neither is a handedness test (the fuel kit's entry
                // carries the same warning, and its ∓46.75° figure is IDENTICAL for both handednesses).
                // YardRegistrationProbe re-measures at every bake and REFUSES on a disagreement with
                // this declaration.
                //
                // What the declaration buys: RigBaker.DirForCell emits cell k as render((8−k)%8), so the
                // sheet that lands on disk is genuinely CLOCKWISE — cell k faces 45k° from north — which
                // is the convention every other baked kit in this repo already carries. The mirrored
                // sidecar never reaches a placer.
                //
                // ⚠️ It exposes no KEYLINE_DEFAULT gate, so it cannot bake through IsoPropSheetBaker
                // (IsoPackContract.AssertKeylineGated refuses). That gate is the owner's 2026-08-06
                // ruling on the four ISO-PACK rigs; ADR 0031's standing consequence for every other
                // family is that it keeps its ring until its own redo. YardSheetBaker is this kit's own
                // baker for that reason, and it reuses IsoPropSheetBaker's cell rule rather than
                // restating it.
                //
                // No prerequisites: the bed pieces would COMPOSE with globalThis.Flowers/Shrubs if those
                // were loaded, but no piece this repo bakes is a bed and YardKit.BakedCompose passes
                // compose:false explicitly, so the sheets are independent of what else shares the host.
                ["yardIso"] = new RigEntry($"{RigFolder}/yard-landscaping-kit/yardIsoRig.js", "YardIso",
                                           AzimuthConvention.CounterClockwise),
            };
    }
}
