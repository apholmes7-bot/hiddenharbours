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
                //     TRANSPARENT cell: 0 opaque px of 28,827, and throws nothing. dir is an INTEGER
                //     0..7. Same trap as the camper (RigCatalog.CamperKit); any baker must assert an
                //     opaque-pixel floor per cell rather than trust the call site.
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
                ["sloop30"] = new RigEntry($"{RigFolder}/sail-rig-kit/sloop-30/sloopIsoRig.js",
                                           "SloopIso", AzimuthConvention.CounterClockwise),

                // The same measurement, re-run on her own pixels rather than assumed from her
                // sister: `helmPort`/`helmStbd` (equal y, equal z) gives the identical −45.0000°
                // step seven times, and `bowRoller` projects 448.00 px WEST at cell 2. Every silent
                // fallback above reproduces on her too, at her own scale (0 of 202,675 opaque px for
                // the compass string).
                //
                // ⚠️ SHE IS THE LARGEST CELL IN THE GAME: 1072×1504, pivot (536,1150) — 6.4× the
                // Cape Islander's area. That is moot on the mesh path and FATAL on a sheet path
                // (a 32-facing sheet of her would exceed the texture size cap and slice wrong), which
                // is why HullMeshFleet registers her MeshOnly and no sheet is baked. Her painted
                // envelope over the sailing states measures 1008×1395 — inside the cell on all four
                // edges, measured, not claimed.
                //
                // 27.0 m LOA · 6.70 m beam (5.4 m transom) · masthead 37.0 m · DWL 1.35 m above the
                // origin · fin keel 4.60 m — deeper than NMC's berth trench, which is where she may
                // NOT lie. She carries what the 30 does not: a self-tacking staysail on an inner
                // forestay (`sfurl`), a fold-down swim platform (`platform`, and the swim_platform
                // DECK polygon is conditional on it), a SLIDING saloon door, and `bounds(dir,opts)`
                // — a surface the 30 does not export at all. Do not write one loop over both hulls
                // that assumes either has it.
                ["sloop88"] = new RigEntry($"{RigFolder}/sail-rig-kit/sloop-88/sloop88IsoRig.js",
                                           "Sloop88Iso", AzimuthConvention.CounterClockwise),
            };
    }
}
