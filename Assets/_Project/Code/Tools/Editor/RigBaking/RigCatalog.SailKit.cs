using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// The sail rig kit — the FIRST SAILS in the fleet (owner drop of 2026-09-06).
        ///
        /// <para>One kit, one file (see <c>RigCatalog.cs</c> for the assembly contract). The comments
        /// on the entries below are MEASUREMENTS someone paid for — move them with their entry, never
        /// summarise them away.</para>
        ///
        /// <para>Two hulls, each a self-contained IIFE with no prerequisites and no <c>isoSolid</c>
        /// dependency, on the fleet contract: 32 px = 1 m, elev 40°, 45° steps, ringless, binary
        /// alpha, origin amidships / canoe-body bottom / centreline. Each ships TWO sidecars — the
        /// fleet's <c>boat-gameplay-geometry@1</c> and a new <c>boat-sailing@1</c> — both landed in
        /// <c>docs/art/rigs/gameplay/</c> and both pinned to the rig by
        /// <see cref="HiddenHarbours.Tools.Sailing.SailingSidecarReader"/>'s hash check.</para>
        ///
        /// <para><b>⚠️ THE SAILS ARE POSE, NOT PIXELS.</b> <c>render(dir, {awa, aws, main, jib, hoist,
        /// furl, cover, …})</c> derives the whole picture from one law, <c>sailPose()</c>, whose
        /// source is quoted in <c>docs/art/spikes/sail-rig-kit/</c>. That is what makes a mesh bake of
        /// <c>build()</c> a bake of ONE pose, and it is the seam this kit's PR proposes rather than
        /// decides.</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> SailKitRigs() =>
            new RigRegistration
            {
                // ---- the sail rig kit (owner drop of 2026-09-06) --------------------------------
                //
                // ⚠️ COUNTER-CLOCKWISE — the fleet's convention, MEASURED here rather than inherited
                // from the neighbours, and measured two independent ways in the repo's own
                // ClearScript V8 (Assets/_Project/Plugins/Editor/JsEngine):
                //
                //   · The bearing of the rig's OWN +X (starboard) axis, taken from the projected
                //     `winchPort`/`winchStbd` pair — anchors that share a y and a z, so their screen
                //     offset is pure +X and the anchor's height cannot corrupt the reading. Depth
                //     un-squashed by sin(elev 40°) = 0.6428 before the atan2, because a raw screen
                //     angle is not a world bearing at this elevation. The heading steps
                //     −45.0000° per dir, seven times, EXACTLY: 0 · 315 · 270 · 225 · 180 · 135 ·
                //     90 · 45.
                //   · The bow. `bowRoller` projects 147.84 px WEST of the pivot at cell 2 — the cell
                //     the rig's own `order` array labels 'E'. A cell labelled east depicting west is
                //     the counter-clockwise signature (RigAzimuthProbe step 4).
                //
                // ⚠️ AN EARLIER READING OF THE SAME QUESTION WAS INCOHERENT, and it is recorded
                // because the failure is instructive: taking the bearing from `bowRoller` alone gave
                // steps of −29.8 / −32.4 / −42.1 / −75.7 …, summing to 330° over seven steps instead
                // of 315. The bow roller sits 2.044 m ABOVE the origin, and at elev 40 that height
                // moves the projected point up the screen by a term that has nothing to do with
                // heading — so un-squashing the whole offset by sin(elev) answers a different
                // question. Only a pair at equal z isolates the ground plane. Do not re-derive a
                // bearing from a single lifted anchor.
                //
                // ⚠️ SILENT FALLBACKS, all four PROBED and pinned by BUFFER IDENTITY (an FNV-1a over
                // the whole RGBA return, not a pixel count — a count cannot tell two paints apart):
                //   · render('N', …) — the compass STRING the READMEs talk in — returns a fully
                //     TRANSPARENT cell: 0 opaque px where the integer call paints 28,570, and throws
                //     nothing. dir is an INTEGER 0..7. Same trap as the camper (RigCatalog.CamperKit);
                //     any baker must assert an opaque-pixel floor per cell rather than trust the call
                //     site. ⚠️ RE-PROBED on the pass-3 bytes (drop 2026-09-13): still 0, on both
                //     hulls. The DENOMINATOR moved with the art — it read 28,827 before pass 3, at
                //     the same facing and the same pose — which is exactly why the floor is written
                //     "> 0" and never as a pinned count.
                //   · render(8, …) is byte-identical to render(0, …) and render(-1, …) to
                //     render(7, …) — dir wraps cleanly, so an off-by-one is invisible, not loud.
                //   · An unknown `scheme` silently renders the DEFAULT (`gelcoat-white`) — byte
                //     identical, no warning. The control that proves the fingerprint can see paint at
                //     all: `atlantic-navy` really does differ.
                //   · `awa` is the one input with NO trap: sailPose wraps it into ±180 at its first
                //     line, and sailPose(a) is identical to sailPose(wrap180(a)) over 155 samples
                //     from −540° to +540°.
                //
                // 9.4 m LOA · 2.98 m beam · masthead 13.0 m · cell 400×552 · pivot (200,432) ·
                // DWL 0.55 m above the origin (NOT the pivot row) · fin keel 1.90 m, rudder 1.50 m,
                // both baked only with `underbody:true`.
                //
                // Her body, re-measured on the pass-3 bytes: 2,035 faces · 8,278 vertices · 14
                // distinct materials (1,852 · 7,514 · 14 before it). Her CELL did not move — 400×552,
                // pivot (200,432), the same on both sides of the drop.
                //
                // ⚠️ PASS 3 WIDENED HER EXPORT SURFACE, which the drop's own notes do not mention:
                // she now hands out `FIT` (the cabin's bulkhead/settee/table fit constants, which her
                // gameplay sidecar's obstruction footprints are written from) and `bounds(dir,opts)`.
                // She exported neither on main. Nothing was removed.
                ["sloop30"] = new RigEntry($"{RigFolder}/sail-rig-kit/sloop-30/sloopIsoRig.js",
                                           "SloopIso", AzimuthConvention.CounterClockwise),

                // The same measurement, re-run on her own pixels rather than assumed from her
                // sister: `helmPort`/`helmStbd` (equal y, equal z) gives the identical −45.0000°
                // step seven times, and `bowRoller` projects 448.00 px WEST at cell 2. Every silent
                // fallback above reproduces on her too, at her own scale (0 opaque px for the
                // compass string where the integer call paints 200,898 — 202,675 before pass 3, at
                // the same facing and the same pose).
                //
                // ⚠️ HER CELL MOVED IN PASS 3 (drop 2026-09-13): 1072×1504, pivot (536,1150) →
                // 1072×1568, pivot (536,1182). Canvas and pivot each grew 32 px, so anything that
                // places her BY the pivot moves with them. Nothing committed had to be rebaked for
                // it: she sits on HullMeshFleet.BakeBlocked rather than in Hulls, and there is no
                // sloop sprite sheet anywhere in the tree — the cell is what a FUTURE bake would
                // emit, not a picture that exists.
                //
                // At 1,680,896 px she is the THIRD largest cell in the fleet, behind the coastal
                // packet's 2112×1760 and the tanker's 1920×1600 — both MeshOnly, neither ever
                // sheeted. (The line here used to say "the largest cell in the game … 6.4× the Cape
                // Islander's area"; neither half reproduces today — against her committed 456×420
                // the multiple is 8.8× — so both are restated from the rigs as they stand.) The size
                // is moot on the mesh path and FATAL on a sheet path: a 32-facing sheet of her would
                // exceed the texture size cap and slice wrong, which is why no sheet is baked.
                //
                // ⚠️ THE NEW CELL IS HEADROOM, NOT A CLIP — measured, not claimed. Painted envelope
                // in the repo's own V8 over 8 pose families (sailing · stored under her cover · the
                // cabin cutaway · heeled 18° · underbody · grinding · everything deployed · close-
                // hauled at 24°) × all 8 facings — 64 rasters a side, none blank:
                //
                //                            main (3,088 faces)     pass 3 (4,500 faces)
                //   painted envelope         932×1367               980×1381
                //   around the pivot l/r/u/d 466/465/1073/293       490/489/1073/307
                //   bounds() extent          931×1369               981×1382
                //
                // She widened 48 px and dropped 14; she did not rise at all. Main's 1072×1504 cell
                // held 536/535/1150/353 around its pivot, so it would still have contained pass-3's
                // picture on all four edges. ⚠️ An EARLIER reading recorded 1008×1395 "over the
                // sailing states"; that frame is not this one and does not reproduce under it, so
                // both are kept — a measurement is only comparable to one shot through the same
                // frame.
                //
                // 27.0 m LOA · 6.70 m beam (5.4 m transom) · masthead 37.0 m · DWL 1.35 m above the
                // origin · fin keel 4.60 m — deeper than NMC's berth trench, which is where she may
                // NOT lie. She carries what the 30 does not: a self-tacking staysail on an inner
                // forestay (`sfurl`), a fold-down swim platform (`platform`, and the swim_platform
                // DECK polygon is conditional on it), a SLIDING saloon door, and — on main — a
                // `bounds(dir,opts)` the 30 did not export at all.
                //
                // ⚠️ `bounds` IS NO LONGER HERS ALONE: pass 3 gives the 30 one too, so a loop over
                // both hulls may assume it from this drop forward and not before. What still parts
                // them, read off the two export surfaces on the pass-3 bytes: hers alone are
                // `staysail` · `platform` · `stair` · `well` · `aft` · `LOW` · `PLAT` · `SAL` ·
                // `STAY` · `WHEEL_HUBS`; the 30's alone are `CAB_SOLE` · `FIT` · `HATCH` ·
                // `WHEEL_HUB` (singular — she has one wheel hub, the 88 has two). Do not write one
                // loop over both hulls that assumes either list.
                //
                // Her body, re-measured on the pass-3 bytes: 4,500 faces · 18,327 vertices · 14
                // distinct materials (3,088 · 12,530 · 14 before it).
                ["sloop88"] = new RigEntry($"{RigFolder}/sail-rig-kit/sloop-88/sloop88IsoRig.js",
                                           "Sloop88Iso", AzimuthConvention.CounterClockwise),
            };
    }
}
