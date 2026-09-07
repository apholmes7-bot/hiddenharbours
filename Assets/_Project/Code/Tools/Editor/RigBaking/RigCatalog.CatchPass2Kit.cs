using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// Catch pass 2 (drop of 2026-09-05): seven fish, the two crustaceans lofted as solids, five
        /// shellfish at true size, the clam hod, and the glue that fills containers with them.
        ///
        /// <para>One kit, one file (see <c>RigCatalog.cs</c> for the assembly contract). The comments
        /// on the entries below are MEASUREMENTS someone paid for — move them with their entry, never
        /// summarise them away.</para>
        ///
        /// <para><b>Pass 1 is NOT retired by this registration.</b> <c>fish</c>, <c>crustacean</c>,
        /// <c>shellfish</c> and <c>catchKit</c> stay registered and still load; pass 2 is a parallel
        /// set on the same cells and pivots. What it is NOT is pixel-identical — see the
        /// <c>fish2</c> entry.</para>
        ///
        /// <para><b>The shared turntable is the deck-loop kit's one copy.</b> This kit shipped its own
        /// <c>isoSolid.js</c>, verified byte-identical (LF sha256 f7fc9db510b0…) to
        /// <c>deck-loop-kit/Art/isoSolid.js</c>, so it was not landed twice and every entry here that
        /// lathes declares <c>deckIsoSolid</c> as its prerequisite instead.</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> CatchPass2KitRigs() =>
            new RigRegistration
            {
                // ---- catch pass 2 (drop of 2026-09-05) ----------------------------------------

                // Seven species (cod haddock pollock mackerel + bass flounder herring), cell 64x64,
                // pivot (32,38) — the pass-1 cell and pivot exactly. sheetOrder() reports 35 columns
                // (25 water frames + 10 rest frames); AORDER is swim 6f / dart 3f / thrash 4f /
                // shadow 2f / roll 4f / jump 6f, RESTS deck 4 / gill 2 / tail 2 / cradle 2. All read
                // from the rig, none from its README.
                //
                // ⚠️ PASS 2 IS A REDRAW, NOT A PIXEL-IDENTICAL SUPERSET. The kit README says
                // "nothing moves a pixel"; measured in the V8 harness, 33,547 pixels differ across
                // the four pass-1 species at scale 1 — every one of 280 cells. It is not the keyline
                // (pass 1 ignores {outline:true} entirely: plain vs outline = 0 px) and not the
                // strict world scale (SPECIES.len is unchanged). The bodies are re-lofted: cod
                // 132 to 96 opaque px, haddock 100 to 63, pollock 112 to 71, mackerel 66 to 38. swim
                // also grew 4f to 6f and dart 2f to 3f. Replacing the pass-1 sheets is therefore a
                // visible art change, not a no-op.
                //
                // ⚠️ SILENT FALLBACK: hold() resolves SPECIES[key] || SPECIES.mackerel, so an
                // unknown species is quietly weighed as a mackerel. The baker asserts the species
                // against ORDER before rendering — the pass-1 baker already does; keep it.
                //
                // Stands alone: reaches no other global (grepped from the source, not assumed).
                ["fish2"] = new RigEntry($"{RigFolder}/catch-pass-2-kit/Art/fishIsoRig2.js", "FishIso2",
                                         AzimuthConvention.Clockwise),

                // Lobster + rock crab, lofted as real solids against the shared turntable. Cell
                // 64x64, pivot (32,40) = ground centre; the HELD pose re-pivots to hpivot (32,12),
                // the grip on the back.
                //
                // ⚠️ THE SIGNATURE IS render(KIND, opts) — the kind is the FIRST ARGUMENT and the
                // camera is opts.dir. Passing a dir first costs nothing loudly: KINDS.indexOf(kind)
                // fails, the rig falls back to 'lobster', and every cell renders a lobster at dir 0.
                // Measured: render('haddock', ...) is 0 px different from render('lobster', ...), and
                // an unknown pose is 0 px different from walk. Assert both before baking.
                //
                // Pose remaps, measured by rendering all 8 dirs x every frame, not read from prose:
                // lobster sidle to walk and burrow to walk (identical at all 32 cells); crab flip to
                // walk (identical at all 32 cells). Everything else is distinct.
                //
                // ang (radians, turns the animal) and dir (the camera) COMPOSE EXACTLY: ang=45deg at
                // dir 0 is byte-identical to ang=0 at dir 1.
                ["crustacean2"] = new RigEntry($"{RigFolder}/catch-pass-2-kit/Art/crustaceanRig2.js",
                                               "Crustacean2", AzimuthConvention.Clockwise,
                                               prerequisites: new[] { "deckIsoSolid" }),

                // NOT DIRECTIONAL, exactly like its pass-1 sibling: 8x8 item lays (ipivot 4,6 =
                // ground) and 8x8 handfuls (hpivot 4,4 = the grip), no camera. Exposes IW/IH/ipivot
                // instead of W/H/pivot, so it must be loaded with InstallModule, never Install.
                //
                // holes()/spurt() are the clam flat's gameplay TELL and they are CONTRACTED, not
                // decorative: holes(3,400,100,100) reproduces the sidecar's periodMs bit-for-bit
                // ([2616.2039735354483, 7793.919028900564]), dur is a flat 420 ms, and rise =
                // sin(pi*u) matches the sidecar's spurtSampleAtDur420 on all 400 holes.
                // ⚠️ The window opens when (t + phase) % period is at most dur, so a hole's first
                // spurt is at t = period - (phase % period) — NOT at t = phase.
                ["shellfish2"] = new RigEntry($"{RigFolder}/catch-pass-2-kit/Art/shellfishRig2.js",
                                              "Shellfish2", AzimuthConvention.Clockwise),

                // The wire roller basket, 40x40, pivot (20,30) = ground centre. Hollow like the tote:
                // render(dir,{layer:'back'}) then the heap then {layer:'front'}; opening(dir) is the
                // rim quad and depthPx() is 4.
                //
                // ⚠️ THE HEAP IS NOT THIS RIG’S. render() takes no fill at all — the middle layer is
                // CatchKit2.heap('clam', opening(dir), depthPx(), band), which is why BakeClamHod
                // installs the whole catchKit2 chain before this entry even though clamHod itself
                // needs only the turntable. Measured: loading that chain first moves ZERO pixels of
                // Hod2_back/Hod2_front (CatchPass2KitTests pins it).
                //
                // cpivot(dir) is the same point at all 8 facings, and that is CORRECT, not a stub:
                // it projects (0, 0, GZ) — dead centre in x and y — so rotating the camera about the
                // vertical axis cannot move it. It is the bail's grip directly above the centre.
                ["clamHod"] = new RigEntry($"{RigFolder}/catch-pass-2-kit/Art/clamHodRig.js", "ClamHod",
                                           AzimuthConvention.Clockwise,
                                           prerequisites: new[] { "deckIsoSolid" }),

                // THE GLUE, not a renderer — item()/fillItems()/heap()/hold() over the other three
                // catch rigs. 14 kinds (7 fish + 2 crustaceans + 5 shellfish) plus 'mixed'; the five
                // shellfish are isHeap and fill as a heap clipped to the container opening.
                //
                // ⚠️ Its prerequisites are read from the rig's own source, not guessed: it reaches
                // root.FishIso2, root.Crustacean2 and root.Shellfish2. fillItems() in particular
                // reads FishIso2.SPECIES[kind].range for every item's scale, so a missing fish rig
                // does not throw — it silently falls back to [0.85, 1.15] for every species.
                //
                // ⚠️ Like pass 1 it calls document.createElement('canvas') inside item(), so the
                // storage baker must execute CatchStorageBaker.CanvasShimJs BEFORE this rig loads
                // (ADR 0021 section 5: what the engine needs and the rig does not provide is the
                // HOST's job, never a patch to the art director's file). InstallModule only — it has
                // no cell geometry of its own.
                //
                // ⚠️ fillItems() is documented MONOTONIC and IS NOT, for the four DENSE kinds.
                // Measured over 8 seeds x the 5 FRAC steps: 0 violations / 312 prefix checks for
                // every DENSE==1 kind (cod haddock pollock bass lobster mixed), but herring 624/952
                // and mackerel, flounder and crab 152/480 each. The gate is
                // jit = dense>1 && i >= n/dense — n grows with the fill, so the jitter threshold
                // moves across items already placed, and the two extra rng draws it then consumes
                // shift variant and scale for every later item. Upstream ask; do not paper over it.
                ["catchKit2"] = new RigEntry($"{RigFolder}/catch-pass-2-kit/Art/catchKit2.js", "CatchKit2",
                                             AzimuthConvention.Clockwise,
                                             prerequisites: new[] { "fish2", "crustacean2", "shellfish2" }),
            };
    }
}
