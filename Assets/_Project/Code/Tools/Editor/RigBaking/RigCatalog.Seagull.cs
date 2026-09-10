using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// The seagull rig kit — the fleet's first CREATURE rig (owner drop of 2026-09-10).
        ///
        /// <para>One kit, one file (see <c>RigCatalog.cs</c> for the assembly contract). The comments
        /// on the entry below are MEASUREMENTS someone paid for — move them with their entry, never
        /// summarise them away.</para>
        ///
        /// <para>A herring gull on the fleet contract: 32 px = 1 m, elev 40°, 45° steps, ringless,
        /// ordered dither, depth-edge darkening, no AA. Self-contained IIFE, no prerequisites, no
        /// <c>isoSolid</c> dependency. Cell 64×64, pivot (32,46) — and the pivot is a CONTACT POINT,
        /// not a waterline: ground when standing / walking / perched, the water surface when
        /// floating, and the point directly below the bird when it is airborne.</para>
        ///
        /// <para><b>⚠️ ALTITUDE IS A SCREEN OFFSET, NEVER A SCALE.</b> The owner's ruling of
        /// 2026-09-09: strict world scale at EVERY altitude — a 1.40 m span is 45 px whether the bird
        /// is on the wharf or 25 m up, because there are no giant gulls crossing the camera. An
        /// airborne frame blits at <c>pivot − altitude·cos(40°)·32·zoom</c> px with the SHADOW LEFT AT
        /// THE PIVOT, so the bird descends onto its own shadow. Any "bigger when closer" knob is a
        /// defect, not a feature.</para>
        ///
        /// <para>The gameplay sidecar (<c>hidden-harbours/creature-gameplay@1</c>, a NEW schema kind —
        /// no DECK, no CLEATS, the sections are behaviours) lands beside the rig and is read by
        /// <see cref="SeagullSidecarReader"/>, which refuses it loudly if the rig's bytes have
        /// moved.</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> SeagullRigs() =>
            new RigRegistration
            {
                // ---- the seagull rig kit (owner drop of 2026-09-10) -----------------------------
                //
                // ⚠️ CLOCKWISE — the MINORITY convention in this repo (19 of 21 directional rigs are
                // counter-clockwise), so it was MEASURED rather than assumed, in the repo's own
                // ClearScript V8 against the committed bytes:
                //
                //   · `project(dir, [0,1,0])` — the rig's own +Y (the bill axis, dir0 = N) — gives a
                //     screen dx of  0 · +22.627 · +32 · +22.627 · 0 · −22.627 · −32 · −22.627
                //     across dirs 0..7. That is the EXACT MIRROR of the shovel rig's table, and the
                //     shovel is the repo's documented counter-clockwise case. A bill that swings
                //     EAST as the index rises is a cell `i` depicting heading +45°·i: clockwise.
                //   · The pair is taken at equal z (the probe vector has z = 0), so the reading is
                //     pure ground-plane bearing. See RigCatalog.SailKit.cs for why a single LIFTED
                //     anchor cannot answer this question at elev 40.
                //
                // ⚠️ THE SHEET'S ROW ORDER IS row r = dir r — measured, not inferred. Baking the
                // drop's own Seagull.png against our bake matched 345 of 384 cells with the direct
                // mapping and only 93 of 384 with a clockwise remap, so the rows are NOT remapped on
                // the way to the sheet. (The 39 that differ are the four `waterZ:0` states; see the
                // water note below and the PR body — every dry cell is byte-exact.)
                //
                // ⚠️ SILENT FALLBACKS, PROBED — each one renders plausible art instead of throwing:
                //   · `render(dir, …)` takes a facing INDEX. Passing the compass STRING the READMEs
                //     talk in ('N', 'NE', …) returns a fully TRANSPARENT cell — 0 opaque px — and
                //     throws nothing. A blank-vs-blank sweep then reads as "0 cells moved", which is
                //     a false green; the bake carries an opaque-pixel control for exactly this.
                //   · An anim name the rig does not know falls back to `stand` in silence. A typo in
                //     a state id therefore bakes 48 columns of a standing bird, all non-blank, all
                //     wrong. The bake asks the rig for `sheetOrder()` rather than naming states.
                //   · `sheetOrder()` yields `{anim, f}` but `render` reads the frame from `frame`.
                //     Passing the record through unmapped renders frame 0 of every column — 48
                //     distinct-looking cells that are all the same pose. The mapping is MANDATORY and
                //     is asserted by the "consecutive frames differ" control (which passes: every
                //     anim's frames differ from their neighbours, in all 8 dirs).
                //
                // ⚠️ THE FOUR WATER STATES CARRY `waterZ: 0` IN THEIR POSE — splash, float, preen and
                // peck. Pixels below waterZ bake the depth-graded tint + alpha exactly as FishIso2;
                // they are never clipped at runtime. A DRY preen or peck (a gull cleaning itself on a
                // wharf) must pass `waterZ: null` explicitly, or it renders half-submerged on dry
                // land. Note the declared TRANSITIONS graph has no dry route into preen/peck at all —
                // float is their only in-edge — which is reported upstream, not invented here.
                //
                // Controls that passed at intake: 384/384 cells non-blank; the `dirOf(heading)` table
                // verified against all 8 sectors; the bake bit-identical across three consecutive
                // runs. 48 columns × 8 dir rows × 64 px = the 3072×512 sheet the drop ships.
                ["seagull"] = new RigEntry($"{RigFolder}/seagullIsoRig.js", "SeagullIso",
                                           AzimuthConvention.Clockwise),
            };
    }
}
