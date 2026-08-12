using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// A rig the baker knows how to load: where the art director's file lives and what global it
    /// installs. Deliberately thin — cell size, pivot, facing count and rock frames are read FROM
    /// THE RIG at bake time (ADR 0021 §4: "cell geometry, pivot and the crop rect come from the rig
    /// instead of a README"), so there is no hand-maintained table to drift.
    /// </summary>
    public readonly struct RigEntry
    {
        /// <summary>Path relative to the repo root, e.g. "docs/art/rigs/puntIsoRig.js".</summary>
        public readonly string ScriptPath;

        /// <summary>The global the IIFE installs, e.g. "PuntIso".</summary>
        public readonly string GlobalName;

        /// <summary>
        /// What <c>docs/art/rigs/README.md</c> DECLARES this rig's convention to be.
        ///
        /// ⚠️ This is an EXPECTATION TO CROSS-CHECK, not an input to the bake. The baker uses the
        /// value <see cref="RigAzimuthProbe"/> measures from rendered pixels. If the two disagree
        /// the bake FAILS LOUDLY rather than silently picking one — because a silent pick is
        /// exactly how this mislabel shipped defects in five separate kits. If you are here because
        /// a rig now fails that cross-check, the fix is to correct the README, having first
        /// confirmed the measurement by eye.
        /// </summary>
        public readonly AzimuthConvention DeclaredConvention;

        /// <summary>
        /// Catalog keys this rig DELEGATES TO — installed into the same host, transitively and in
        /// order, before this rig's own source runs. Empty for every rig that stands alone, which
        /// until the pass-6 character kit was all of them.
        ///
        /// <para>⚠️ <b>A missing prerequisite does not throw — it renders the wrong art.</b> The
        /// pass-6 body asks <c>root.HeadIso</c> for the hat table and falls back to its own local one
        /// when the head rig is absent; the face never stamps. That is the rigs' shared failure mode
        /// (resolve as <c>opts[k] ?? fallback</c>, never complain), which is why the dependency is
        /// declared HERE and not left to each caller to remember. Same principle as the canvas shim
        /// <c>CatchStorageBaker</c> installs: whatever a rig needs and does not provide is the HOST's
        /// job, never a patch to the art director's file (ADR 0021 §5).</para>
        /// </summary>
        public readonly IReadOnlyList<string> Prerequisites;

        public RigEntry(string scriptPath, string globalName, AzimuthConvention declared,
                        string[] prerequisites = null)
        {
            ScriptPath = scriptPath;
            GlobalName = globalName;
            DeclaredConvention = declared;
            Prerequisites = prerequisites ?? Array.Empty<string>();
        }
    }

    public static class RigCatalog
    {
        const string RigFolder = "docs/art/rigs";

        /// <summary>
        /// Only the rigs Phase 1 actually bakes. The other 37 files in docs/art/rigs/ are imported
        /// source, and importing source is not a licence to wire content (CLAUDE.md rule 8) — most
        /// of the un-baked hulls are M2/M3 fleet.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, RigEntry> Entries =
            new Dictionary<string, RigEntry>(StringComparer.Ordinal)
            {
                // The golden-master probe. Both its TUBS are at x:0 — see RigAzimuthProbe.
                ["punt"] = new RigEntry($"{RigFolder}/puntIsoRig.js", "PuntIso",
                                        AzimuthConvention.CounterClockwise),

                // The reason Phase 1 exists: Tier 3, ~12.0 m LOA, and no baked art anywhere.
                ["lobsterBoat"] = new RigEntry($"{RigFolder}/lobsterBoatIsoRig.js", "LobsterBoatIso",
                                               AzimuthConvention.CounterClockwise),

                // ---- the character, pass 6 (drop of 2026-08-02, PR #397) — THREE FILES -----------
                //
                // The first rig in the catalog that is not one file. The body delegates skull / hair /
                // beard / hats to the head rig, which delegates the eye socket to the eye rig, so all
                // three must be in the host and IN THAT ORDER. Declared as prerequisites rather than
                // remembered by each caller: load them wrong and nothing throws — the body silently
                // uses its local HATS_LOCAL table and never stamps a face.
                //
                // The eye and head rigs expose no standard W/H/pivot triple, so they install with
                // InstallModule (the shellfish/catchKit path) and only the body reports geometry.
                ["characterEye"] = new RigEntry($"{RigFolder}/eyeIsoRig.js", "EyeIso",
                                                AzimuthConvention.Clockwise),

                ["characterHead"] = new RigEntry($"{RigFolder}/headIsoRig3.js", "HeadIso3",
                                                 AzimuthConvention.Clockwise,
                                                 prerequisites: new[] { "characterEye" }),

                // Still the non-boat host it always was: no ROCK block (characters RIDE a deck's rock
                // via opts.roll/pitch/heave rather than owning one), so Install reports rockFrames 0
                // and the turntable path does not apply. Baked by CharacterRigBaker (8 direction rows
                // × ANIMS-declared frames), never by the boat turntable.
                //
                // ⚠️ CLOCKWISE here is a PRIOR AGAIN, not the inherited pass-1 measurement. Pass 1 was
                // pixel-verified clockwise; pass 6 is a different renderer with a new head rig in the
                // projection path, and this lane has been CCW-mislabelled twice. CharacterRigAzimuthProbe
                // measures it from rendered pixels at bake time and the bake refuses on a mismatch.
                ["character"] = new RigEntry($"{RigFolder}/characterIsoRig6.js", "CharacterIso6",
                                             AzimuthConvention.Clockwise,
                                             prerequisites: new[] { "characterHead" }),

                // ---- the fishing kit (drop of 2026-07-22, PR #258) — Rod Fishing v2 wave 3 ------

                // Parametric fish loft: one skeleton, a SPECIES data table. CLAIMED clockwise
                // (th = −dir·45°, same term as the character) but the kit is UNVERIFIED — the
                // README's correction note is explicit that the sign term is not proof, so
                // FishingRigAzimuthProbe measures the head side from pixels before any bake.
                // Declares no DIRS global (Install reports 0); the 8 headings are the ADR-0006
                // recipe, supplied by FishingKitBaker.
                ["fish"] = new RigEntry($"{RigFolder}/fishIsoRig.js", "FishIso",
                                        AzimuthConvention.Clockwise),

                // One of the two rigs the art director fixed CLOCKWISE at source (README's
                // pixel-verified group). Still measured at bake time like everything else —
                // FishingRigAzimuthProbe reads which side the blank extends at the E/W rows.
                ["rod"] = new RigEntry($"{RigFolder}/rodIsoRig.js", "RodIso",
                                       AzimuthConvention.Clockwise),

                // NOT DIRECTIONAL: a 16×22 state sprite (float/nibble/strike/fly) with no azimuth
                // term at all — render(state, frame), no dir argument. The declared convention
                // below is a placeholder that nothing reads and nothing probes; the bobber bake
                // path never consults it and never calls DirForCell.
                ["bobber"] = new RigEntry($"{RigFolder}/bobberRig.js", "RodBobber",
                                          AzimuthConvention.Clockwise),

                // ---- the storage wave (catch-handling pass) — container fills ------------------

                // CONTINUOUS heading (ang in radians), not an 8-way turntable — fills scatter it.
                // The declared convention is a placeholder nothing probes; the storage bake renders
                // the exact per-variant angles CatchKit.item composes.
                ["crustacean"] = new RigEntry($"{RigFolder}/crustaceanRig.js", "Crustacean",
                                              AzimuthConvention.Clockwise),

                // NOT DIRECTIONAL: 14×12 item lays + 22×16 handfuls, no camera. Exposes IW/IH/
                // ipivot instead of W/H/pivot, so it must be loaded with InstallModule, never
                // Install. Placeholder convention, nothing probes it.
                ["shellfish"] = new RigEntry($"{RigFolder}/shellfishRig.js", "Shellfish",
                                             AzimuthConvention.Clockwise),

                // THE GLUE, not a renderer: item()/fillItems()/tintSpoil over the other catch
                // rigs. No cell geometry of its own (InstallModule only) and — uniquely — it calls
                // document.createElement('canvas') inside item(), so the storage baker installs a
                // host-side canvas shim FIRST (ADR 0021 §5: anything the engine needs that the rig
                // doesn't provide belongs in OUR host code, never in his file).
                ["catchKit"] = new RigEntry($"{RigFolder}/catchKit.js", "CatchKit",
                                            AzimuthConvention.Clockwise),

                // The insulated deck tote. README claims CLOCKWISE (th = −dir·45°, the character/
                // fish term, and the fish MEASURED clockwise in the #265 bake) — still measured
                // from pixels by StorageRigAzimuthProbe before any bake, per the correction note.
                ["fishTote"] = new RigEntry($"{RigFolder}/fishToteRig.js", "FishTote",
                                            AzimuthConvention.Clockwise),

                // Pails + fish tray. README's CCW-inferred group (th = +dir·45°, the boat term) —
                // measured from pixels (tray-footprint chirality) before any bake. Exposes
                // pivotCarry/pivotRest instead of pivot → InstallModule; the storage baker reads
                // the REST pivot itself (rest mode is the only mode it bakes).
                ["bucket"] = new RigEntry($"{RigFolder}/bucketRig.js", "BucketIso",
                                          AzimuthConvention.CounterClockwise),

                // ---- the buildings (Nine Mile Creek + the St Peters village) --------------------

                // The clapboard houses. Same turntable, same elev 40°, same 32 px = 1 m as the fleet,
                // so a cottage and a Cape Islander stand in one space. In the README's INFERRED
                // counter-clockwise group and never measured — BuildingRigAzimuthProbe measures it at
                // bake time from the door anchor (a building has no bow taper for RigAzimuthProbe to
                // read) and the bake REFUSES on a mismatch, same as every sibling.
                //
                // ⚠️ Baked by BuildingRigBaker, never the boat turntable: the cell is 992×1060, so
                // eight facings in a row would be 7936 px — past the 4096 cap. The building baker
                // tight-crops to the drawn pixels first, which is what makes the bake possible at all.
                ["house"] = new RigEntry($"{RigFolder}/houseIsoRig.js", "HouseIso",
                                         AzimuthConvention.CounterClockwise),

                // The net-shed / storage-barn / fish-plant family — the wharf's working buildings.
                // Same story as the house, one size worse: the 1200×1160 cell is sized to hold the
                // `cannery`, so a net shed occupies a fraction of it and eight uncropped facings would
                // be 9600 px wide (the kit's own reference sheet, which is why that PNG stayed in
                // docs/ and was never imported).
                ["wharfBuilding"] = new RigEntry($"{RigFolder}/wharfBuildingRig.js", "WharfBuilding",
                                                 AzimuthConvention.CounterClockwise),

                // ---- the insides of those buildings (ADR 0021 §4; the interior pilot) ------------

                // THE INTERIOR IS THE BUILDING SEEN FROM INSIDE. Same turntable, same elev 40°, same
                // 32 px = 1 m, and — verified by measurement, not by the header — the SAME Wd/Ln/wallH
                // formulas as houseIsoRig, so a "cottage" interior registers under a "cottage"
                // exterior. Open-dollhouse cutaway: the camera-facing walls are dropped IN THE BAKE,
                // so visibility is art, not a runtime mask.
                //
                // ⚠️ Declared CCW like its exterior — but do NOT reach for BuildingRigAzimuthProbe to
                // check it. That probe reads which SIDE the door lands on at a quarter turn, and it
                // assumes the door is on the +Y gable because both exterior rigs put it there. THIS
                // RIG PUTS ITS DOOR ON −Y (anchors → pj(0,−Ln/2,fZ); the hearth takes +Y). Fed to that
                // probe the interior measures CLOCKWISE — a wrong answer with a confident report, and
                // the bake would then apply the opposite correction and mislabel every cell.
                // InteriorRigAzimuthProbe measures the door's gable FIRST and is what this entry is
                // cross-checked against.
                //
                // Cell 1180×900 and eight facings: past the 2048 the village kit imports at, so the
                // interior kit LIFTS its import cap and asserts native resolution instead
                // (InteriorKit.ImportSizeCap).
                ["interior"] = new RigEntry($"{RigFolder}/interiorIsoRig.js", "InteriorIso",
                                            AzimuthConvention.CounterClockwise),

                // The furniture that stands on that floor. Prop origin is the floor-centre of the
                // prop's footprint — the same pivot convention as the room — so a prop drops onto a
                // room floor point with no offset maths. Exposes project() like the room does, which is
                // how InteriorRigAzimuthProbe proves the two share one camera and one turntable rather
                // than declaring it. Its render() takes (name, dir, opts), not (dir, opts).
                ["interiorProp"] = new RigEntry($"{RigFolder}/interiorPropRig.js", "PropIso",
                                                AzimuthConvention.CounterClockwise),

                // ---- the ISO RIG PACK (owner drop of 2026-08-06) — FOUR INDEPENDENT RIGS ---------
                //
                // wharf structure · wharf dressing · village services · shoreline finds. Unlike the
                // pass-6 character kit and the shop kit, these declare NO prerequisites, and that is
                // MEASURED rather than taken from the pack README's "no order dependency" claim: 83
                // probe keys — including the full RGBA buffer of 24 representative renders across all
                // four rigs — are byte-identical whether each rig is loaded alone, in the README's
                // order, in reverse, or shuffled. The shop kit's README made the same claim and was
                // wrong three ways (#437), so the claim was re-measured here rather than inherited.
                //
                // ⚠️ ALL THREE DIRECTIONAL RIGS ARE COUNTER-CLOCKWISE BY MEASUREMENT, not by label.
                // Each turns its +X axis −46.75° of screen rotation per dir step — the same figure,
                // to the digit, as houseIsoRig / wharfBuildingRig / interiorIsoRig, all of which are
                // registered CCW here after their own probes measured them. Their project() calls
                // also agree to 0.000000000 px relative to each rig's own origin, so the three share
                // ONE turntable rather than three that happen to look alike. The bake must still
                // probe from rendered pixels like every sibling; this entry is the prior it is
                // cross-checked against.

                // Structure: quay/pier/crib/float/gangway/slipway/riprap, 7 families and 17 presets.
                // NO standard W/H/pivot triple — the cell sizes itself from the projected bbox and
                // the rig reports px,py PER BAKE (both fractional), so this must load with
                // InstallModule; Install would throw on the missing pivot.
                //
                // ⚠️ It does NOT replace the near-plan wharf tile kit. Its own README is explicit
                // ("the baked kit is untouched — WharfAtlas.png / WharfOverlays.png stay where they
                // are; this rig sits beside them"), so WharfKitCatalog, StPetersWharf and
                // NineMileCreekWharf are untouched by this import and their migration is a separate
                // decision, not a side effect of registering a rig.
                ["wharfIso"] = new RigEntry($"{RigFolder}/iso-rig-pack/wharf-kit-iso/wharfIsoRig.js",
                                            "WharfIso", AzimuthConvention.CounterClockwise),

                // Dressing that sits ON the deck: 61 pieces in 7 categories. FIXED 420×520 sheet
                // with the pivot at (210,420), so unlike its structural sibling this one does expose
                // the W/H/pivot triple and loads with Install.
                ["wharfDecor"] = new RigEntry($"{RigFolder}/iso-rig-pack/wharf-decor-iso/wharfDecorRig.js",
                                              "WharfDecor", AzimuthConvention.CounterClockwise),

                // Village services — power, light, water, sewer, fuel, telecom. 42 pieces, FIXED
                // 440×620 sheet, pivot (220,520); the sheet is sized for a 10.10 m radio mast, so
                // most pieces sit in a lot of transparent space and the bake crops to ink.
                // Spans are NEVER baked: ties() hands out wire/lamp/drop points in metres and the
                // catenary between two poles is drawn at runtime, so there is no sprite per gap.
                ["utilityIso"] = new RigEntry($"{RigFolder}/iso-rig-pack/utility-iso/utilityIsoRig.js",
                                              "UtilityIso", AzimuthConvention.CounterClockwise),

                // NOT DIRECTIONAL, and not in the same way anything else here is not directional.
                // The finds lie FLAT on the sand, so there is no facing and no project() at all —
                // only a lie angle, 8 canonical steps of the object's long axis in the ground plane,
                // rotated in the GENERATOR before projection (never a pixel rotate). The convention
                // below is a placeholder nothing reads and nothing probes, exactly as for the bobber.
                //
                // ⚠️ TWO SHAPE TRAPS. Its DIRS is a string ARRAY (["N","NE",…]), not the number the
                // other rigs expose, so Install's `typeof DIRS === 'number'` reports 0 rather than 8;
                // and it declares no pivot, which Install would throw on. Load it with InstallModule
                // and read cellOf(key) for the per-find cell and pivot.
                //
                // ⚠️ Its ground foreshorten is Q = 0.72, NOT the 0.6428 (= sin 40°) every other rig
                // in this pack projects with. Carrying that constant across the two families is the
                // un-squash mistake this repo keeps making; the value is per family and measured.
                ["shoreFinds"] = new RigEntry($"{RigFolder}/iso-rig-pack/shoreline-finds-iso/shoreFindsRig.js",
                                              "ShoreFinds", AzimuthConvention.CounterClockwise),

                // ---- the deck-loop kit (drop of 2026-08-09) — the stern-deck working furniture ----
                //
                // ⚠️ CLOCKWISE, AND THAT IS MEASURED, NOT A PRIOR. This kit rides its OWN turntable
                // (isoSolid.js) and turns the OPPOSITE WAY to every boat in this catalog. Measured
                // against utility-iso (registered CounterClockwise) in one harness: utility's +X
                // ground-plane bearing steps +45°/dir, this kit's steps −45°/dir. Its drop README
                // declared CW and — unusually — is correct. Applying the fleet's CCW correction here
                // mirrors all eight cells of every piece. See docs/art/rigs/deck-loop-kit/IMPORT.md,
                // and re-measure with `node docs/art/rigs/deck-loop-kit/_verify.js`.
                //
                // ⚠️ The −46.75°/step SCREEN figure does not distinguish the two families — this kit
                // shares it with the CCW pack. Only the un-squashed ground-plane bearing is a
                // handedness test.
                //
                // isoSolid is the shared turntable, not a piece of art: no W/H/pivot triple, so it
                // loads with InstallModule and is declared as everything else's prerequisite. A
                // missing turntable does not throw — every rig here lathes against it.
                ["deckIsoSolid"] = new RigEntry($"{RigFolder}/deck-loop-kit/Art/isoSolid.js", "IsoSolid",
                                                AzimuthConvention.Clockwise),

                // Five pieces of working furniture. station() is the crew contract — where to stand,
                // which dir to face, and the WORLD-METRE workZ a clip must be handed; the hauler is
                // the one station whose operator faces outboard (turn 4).
                ["deckGear"] = new RigEntry($"{RigFolder}/deck-loop-kit/Art/deckGearRig.js", "DeckGear",
                                            AzimuthConvention.Clockwise,
                                            prerequisites: new[] { "deckIsoSolid" }),

                // Four builds. CAPS carries the per-hull stack limits (lobsterBoat 5 deck / 2
                // washboard, capeIslander 3 / 2, wharf 6 / 0) — those are rig DATA, not game
                // constants, and the deck loop reads them from here.
                ["trap"] = new RigEntry($"{RigFolder}/deck-loop-kit/Art/trapIsoRig.js", "TrapIso",
                                        AzimuthConvention.Clockwise,
                                        prerequisites: new[] { "deckIsoSolid" }),

                // ⚠️ TWO GLOBALS IN ONE FILE, and the drop README names only the second. TrapFauna
                // is the bakeable half: render(kind, opts) takes NO dir, because what a pot comes up
                // holding is not directional. TrapCatch is a canvas FAÇADE — it calls
                // document.createElement and cannot run in the bare host at all; bake through
                // TrapFauna.
                //
                // ⚠️ catchKit + crustacean are PREREQUISITES, and they are the reason this entry is
                // three keys long instead of one. The file's two halves split the kit's kinds: this
                // rig owns six (urchin whelk starfish sculpin kelp baitbag) and DELEGATES the rest —
                // lobster, crab, jonah, short — to CatchKit / Crustacean. TrapCatch returns NULL for a
                // delegated kind when those are absent, so a host without them produces empty mixes in
                // silence; TrapFauna.render, since #480, THROWS for the same kinds rather than drawing
                // a byte-identical urchin in their place. Declaring the pair here means neither
                // failure can reach a bake, in the same way deckIsoSolid means the turntable cannot go
                // missing. Both load clean in a bare host — catchKit only reaches
                // document.createElement inside item(), which no bake path calls (the storage baker
                // installs its canvas shim for exactly that call).
                ["trapFauna"] = new RigEntry($"{RigFolder}/deck-loop-kit/Art/trapFaunaRig.js", "TrapFauna",
                                             AzimuthConvention.Clockwise,
                                             prerequisites: new[] { "deckIsoSolid", "catchKit", "crustacean" }),

                // The banding/grading tray. Distinct from the top-level fishTrayRig.js — this is the
                // kit's own FishTray2, with nest/stack steps in metres.
                ["fishTray2"] = new RigEntry($"{RigFolder}/deck-loop-kit/Art/trayIsoRig.js", "FishTray2",
                                             AzimuthConvention.Clockwise,
                                             prerequisites: new[] { "deckIsoSolid" }),

                // Eight fleet colour schemes over ONE spar-buoy geometry: every scheme measures the
                // same 10×32 cell at the same pivot, and each turns barely at all (98.8–99.0% pixel
                // agreement with dir 0). A packer may collapse those rows; a baker must not assume it.
                ["buoyIso"] = new RigEntry($"{RigFolder}/deck-loop-kit/Art/buoyIsoRig.js", "BuoyIso",
                                           AzimuthConvention.Clockwise,
                                           prerequisites: new[] { "deckIsoSolid" }),

                // ---- the NAVIGATION BUOY kit (drop of 2026-08-11) — the aids to navigation --------
                //
                // ⚠️ THE SECOND BUOY FAMILY. `buoyIso` above is the LOBSTER SPAR FLOAT — 1.2 m of foam
                // in a fisher's colours on a 10×32 cell, baked to DeckLoopSheets/Buoys for the trap
                // loop. THIS is the channel furniture: 14 IALA Region B marks (4 cardinals, 4 laterals,
                // isolated danger, safe water, special, regulatory, mooring, spar) × 5 hull diameters
                // × 3 wear states, up to 6.6 m tall and made of steel. Different global, different
                // sheet folder (Art/Sprites/NavBuoys/Iso), different consumer. Nothing about this entry
                // touches the trap-loop buoys.
                //
                // ⚠️ IT RIDES deckIsoSolid, AND THAT IS NOT A SUBSTITUTION OF CONVENIENCE. The drop
                // shipped its own isoSolid.js; it is CHARACTER-IDENTICAL to the deck-loop kit's
                // already-registered copy (same sha256 once CRLF is normalised — the repo's copy is
                // CRLF, the drop's LF, which is the whole 216-byte difference). Committing a second
                // copy of a registered global's source is the drift docs/art/rigs/README.md's no-edit
                // rule exists to prevent — whichever loaded last would silently win for BOTH kits — so
                // the kit's copy is gitignored and this entry declares the existing turntable instead.
                //
                // The substitution is proven at PIXEL level, not by file hash: loaded against
                // deckIsoSolid, this rig reproduces all four of the drop's own reference sheets
                // BYTE-FOR-BYTE (PortCan|s18 working · CardinalW|s20 fresh+lit · StbdLit|s24
                // working+lit · Spar|s12 working). See docs/art/rigs/nav-buoy-kit/IMPORT.md.
                //
                // ⚠️ CLOCKWISE, MEASURED: the +X ground-plane bearing steps −45°/dir with the depth
                // un-squashed by /sin 40° — same turntable as the deck-loop kit, OPPOSITE to every
                // boat here. The SCREEN mean is −46.7525°, numerically identical to the figure the
                // iso-rig-pack records for its COUNTER-clockwise rigs, and is therefore not a
                // handedness test. NavBuoyRegistrationProbe re-measures from the live rig at bake time
                // and the bake refuses on a mismatch, same as every sibling.
                //
                // ⚠️ Loads with InstallModule, never Install: no W/H/pivot/DIRS/defaultElev globals at
                // all (measured — all five are `undefined`). Cell geometry is per type+size, from
                // cell(type,size) → {W,H,cx,cy}, and FACINGS come from the contract, never a DIRS field.
                //
                // ⚠️ A missing turntable here THROWS ("Cannot read properties of undefined (reading
                // 'tube')") rather than rendering wrong art — measured, and a happier failure mode than
                // the pass-6 character body's silent fallback. The prerequisite is still declared: a
                // loud failure at bake time is not a reason to leave the dependency to each caller.
                ["navBuoy"] = new RigEntry($"{RigFolder}/nav-buoy-kit/navBuoyRig.js", "NavBuoy",
                                           AzimuthConvention.Clockwise,
                                           prerequisites: new[] { "deckIsoSolid" }),

                // The boatyard: 20 parts in metres and 5 named SITES that assemble them around a
                // hull, sized by the boat each yard serves. Tight cells like wharfIso — it reports
                // px,py per bake and declares no pivot global, so it loads with InstallModule.
                //
                // ⚠️ CounterClockwise is MEASURED, not inferred from its th = +dir*PI/4 sign (which
                // the rig-lane README rules inadmissible). ShipyardIso.project() returns figures
                // IDENTICAL to WharfIso.project() at all 8 dirs to 4 dp, and wharfIso is measured
                // CCW — so the two ride one turntable. A probe that read the positive sign as
                // clockwise would have registered this family mirrored and flipped all 25 keys at
                // once. See docs/art/rigs/shipyard-iso-kit/VERIFICATION.md §2.
                ["shipyardIso"] = new RigEntry($"{RigFolder}/shipyard-iso-kit/shipyardIsoRig.js",
                                               "ShipyardIso", AzimuthConvention.CounterClockwise),

                // ---- the fuel storage & dispensing kit (owner drop of 2026-08-11) ----------------
                //
                // Eight vessels over 21 sizes and 4 grades, plus a continuous fill solver:
                // jug · jerry · nozzle carried, drum · tote · skid · bulk · pump standing.
                //
                // ⚠️ CLOCKWISE, MEASURED — and it shares the deck-loop kit's turntable rather than
                // merely resembling it. The drop ships its own isoSolid.js which is BYTE-IDENTICAL
                // (LF-normalised sha256 f7fc9db5…) to the registered deckIsoSolid, so that copy is
                // gitignored — a second copy of a registered global's source is the drift the no-edit
                // rule exists to prevent — and this entry declares the registered one as prerequisite.
                // Identity is proved in PIXELS, not by hash: rendered against the REGISTERED
                // turntable the rig reproduces all TWELVE of the drop's reference sheets
                // byte-for-byte. See docs/art/rigs/fuel-storage-kit/IMPORT.md.
                //
                // Handedness measured the only admissible way — the un-squashed ground-plane bearing
                // of +X, against utilityIso (registered CounterClockwise) in ONE host: this kit steps
                // −45.000°/dir, the reference +45.000°/dir. ⚠️ Their SCREEN means are ∓46.7525°, the
                // same magnitude, so that figure does NOT distinguish the two families and must not
                // be used. FuelRegistrationProbe re-measures both at every bake and refuses on a
                // disagreement with this declaration.
                //
                // ⚠️ NO W/H/pivot triple — the cell is per (type, size, mode) and comes from
                // cell(type, size, opts), so this loads with InstallModule; Install would throw on
                // the missing pivot. It DOES expose DIRS as a number (8) and defaultElev (40).
                //
                // ⚠️ Two silent-divergence traps live in this rig, both recorded rather than patched
                // (his file runs unmodified, ADR 0021 §5) and both defended in FuelSheetBaker:
                // an OMITTED wear renders a phantom fourth state that collides in the geometry cache
                // with 'working' (see FuelStorageKit.BakedWear), and resolveSize falls through to a
                // default for any size it does not know. Unlike most of this lane, a missing
                // turntable THROWS here rather than rendering wrong.
                ["fuel"] = new RigEntry($"{RigFolder}/fuel-storage-kit/fuelRig.js", "FuelIso",
                                        AzimuthConvention.Clockwise,
                                        prerequisites: new[] { "deckIsoSolid" }),

                // ---- THE COMMERCIAL BLOCK (the 2026-08-06 shop kit) ------------------------------
                // Nine trades — general store, fish market, chandlery, bakery, restaurant, tavern,
                // post office, takeout stand, gift shop — across three rigs that share ONE camera, one
                // palette and one footprint formula. Measured rather than read off the header: all
                // three project identically to 0.0000 px at every facing, and all three turn the same
                // way as houseIsoRig and the fleet.
                //
                // ⭐ ALL THREE PUT THE DOOR ON +Y — which is why this family does NOT inherit the
                // interiorIsoRig trap two entries up. THAT rig's door is on −Y, so a room stands a
                // half-turn from its shell and the offset must be measured and carried. The shop kit's
                // room is the shopfront seen from inside (its own README: "the +Y wall is the street
                // elevation from the inside"), so shell and room register at the SAME facing.
                // ShopRegistrationProbe measures that at bake time and writes the working into the
                // contract. It is not declared here, and it must not be assumed to be 4 by analogy.
                //
                // ⚠️ THE LOAD ORDER IS LOAD-BEARING AND EVERY ONE OF ITS FAILURES IS SILENT. Measured:
                //   · Shopfront ALONE resolves restaurant 8.90 × 11.25 m; with ShopInterior loaded,
                //     9.00 × 11.50. The 0.5 m cell SNAP lives in the interior rig and the other two
                //     read it back — so a shell baked without it is a shell its own interior cannot fit.
                //   · ShopInterior.dims({room:'kitchen'}) without ShopBuilding returns the WHOLE SHELL
                //     (9.00 × 11.50, planned=false) instead of the kitchen (6.09 × 5.09): the same call,
                //     3.5× the area, no error. The rig reports which happened as dims().planned, and
                //     ShopRigBaker asserts it rather than trusting the load set.
                //   · shopBuildingRig's README says it "throws without ShopInterior". It does NOT — it
                //     installs cleanly and fails later, at render. Do not lean on that guard.
                // Declaring the dependency here is what stops every caller having to remember it.
                ["shopInterior"] = new RigEntry($"{RigFolder}/shop-building-kit/shopInteriorRig.js",
                                                "ShopInterior", AzimuthConvention.CounterClockwise),

                ["shopfront"] = new RigEntry($"{RigFolder}/shop-building-kit/shopfrontRig.js",
                                             "Shopfront", AzimuthConvention.CounterClockwise,
                                             new[] { "shopInterior" }),

                // Depth-first in this order installs shopInterior, then shopfront, then this — the whole
                // kit in one host, the only configuration in which every number above is right. NOT a
                // cycle: shopInterior needs nothing loaded to install, and its plan-adoption use of
                // ShopBuilding resolves at CALL time (measured), so the graph stays acyclic.
                ["shopBuilding"] = new RigEntry($"{RigFolder}/shop-building-kit/shopBuildingRig.js",
                                                "ShopBuilding", AzimuthConvention.CounterClockwise,
                                                new[] { "shopInterior", "shopfront" }),
            };

        public static string RepoRoot =>
            Directory.GetParent(Application.dataPath)!.FullName;

        public static RigEntry Get(string key) =>
            Entries.TryGetValue(key, out var e)
                ? e
                : throw new ArgumentException(
                    $"No rig '{key}' in the catalog. Known: {string.Join(", ", Entries.Keys)}.");

        public static string ReadSource(in RigEntry entry)
        {
            string full = Path.Combine(RepoRoot, entry.ScriptPath);
            if (!File.Exists(full))
                throw new FileNotFoundException(
                    $"Rig source missing at {full}. The rigs are committed under docs/art/rigs/ — " +
                    "if this fired, the branch predates that import.", full);

            // Read and hand over UNMODIFIED. No preamble, no shim, no patched globals: ADR 0021 §5
            // makes "his file is what runs" the whole point. Where a rig genuinely needs an
            // environment global the file doesn't provide (catchKit's document.createElement),
            // the HOST installs the shim as separate host-side code BEFORE this source runs
            // (CatchStorageBaker.CanvasShimJs) — the file itself is never touched.
            return File.ReadAllText(full);
        }

        /// <summary>
        /// Loads a rig that declares NO standard cell geometry (no <c>W/H/pivot</c> globals) —
        /// the kits and item rigs (shellfishRig's IW/ipivot, catchKit's functions, bucketRig's
        /// dual pivots). Executes the source and asserts the global installed, nothing more; the
        /// caller reads whatever shape the rig actually exposes. <see cref="Install"/> would throw
        /// on the missing <c>pivot</c>, and papering that over with defaults would silently bake
        /// a wrong pivot — hence a separate, geometry-free entry point.
        /// </summary>
        public static void InstallModule(IRigScriptHost host, in RigEntry entry)
        {
            InstallPrerequisites(host, entry);
            host.Execute(ReadSource(entry));
            string g = entry.GlobalName;
            if (!host.EvaluateBool($"typeof {g} === 'object' && {g} !== null"))
                throw new InvalidOperationException(
                    $"Rig '{entry.ScriptPath}' ran but did not install globalThis.{g}. " +
                    "Either the global name in the catalog is wrong or the rig changed shape.");
        }

        /// <summary>
        /// Runs a rig's declared <see cref="RigEntry.Prerequisites"/> — depth first, in order, each
        /// through <see cref="InstallModule"/> so its own prerequisites come first and its global is
        /// asserted. Already-present globals are skipped, so installing the body twice into one host
        /// (probe then bake) does not re-run the head.
        ///
        /// <para>A prerequisite is loaded with InstallModule and never Install: the head and eye rigs
        /// expose no <c>W/H/pivot</c> triple, and Install would throw on the missing pivot. Papering
        /// that over with defaults is exactly the silent-wrong-geometry failure the split entry points
        /// exist to prevent.</para>
        /// </summary>
        static void InstallPrerequisites(IRigScriptHost host, in RigEntry entry)
        {
            var prereqs = entry.Prerequisites;
            if (prereqs == null || prereqs.Count == 0) return;

            foreach (string key in prereqs)
            {
                var dep = Get(key);
                // Idempotent: a host that already carries the global has already run the file.
                if (host.EvaluateBool($"typeof {dep.GlobalName} === 'object' && " +
                                      $"{dep.GlobalName} !== null")) continue;
                InstallModule(host, dep);
            }
        }

        /// <summary>Loads a rig into a fresh host and returns its self-reported geometry.</summary>
        public static RigGeometry Install(IRigScriptHost host, in RigEntry entry)
        {
            InstallPrerequisites(host, entry);
            host.Execute(ReadSource(entry));
            string g = entry.GlobalName;

            // Assert the global really installed before trusting anything downstream.
            if (!host.EvaluateBool($"typeof {g} === 'object' && {g} !== null"))
                throw new InvalidOperationException(
                    $"Rig '{entry.ScriptPath}' ran but did not install globalThis.{g}. " +
                    "Either the global name in the catalog is wrong or the rig changed shape.");

            // ROCK is a HULL contract: boats own their rock cycle and export its frame count.
            // Character rigs have no ROCK at all — they ride a deck's rock through
            // opts.roll/pitch/heave instead — so its absence is a legitimate rig shape, not an
            // error. Report 0 rather than throwing; the boat turntable never runs on such a rig
            // (RigBakeMenu's recipes name boat keys only) and CharacterRigBaker never reads it.
            bool hasRock = host.EvaluateBool($"typeof {g}.ROCK === 'object' && {g}.ROCK !== null");

            // DIRS is likewise a rig-shape fact, not a universal: the fishing kit's rigs
            // (FishIso, RodIso, RodBobber) declare no DIRS global. 0 = "the rig does not say" —
            // a directional baker then supplies its recipe's facing count (8 per ADR-0006) and a
            // non-directional one (the bobber) never asks. Same for defaultElev: the bobber is a
            // hand-plotted sprite with no camera at all.
            bool hasDirs = host.EvaluateBool($"typeof {g}.DIRS === 'number'");
            bool hasElev = host.EvaluateBool($"typeof {g}.defaultElev === 'number'");

            return new RigGeometry(
                width:      (int)host.EvaluateNumber($"{g}.W"),
                height:     (int)host.EvaluateNumber($"{g}.H"),
                pivotX:     host.EvaluateNumber($"{g}.pivot.x"),
                pivotY:     host.EvaluateNumber($"{g}.pivot.y"),
                nativeDirs: hasDirs ? (int)host.EvaluateNumber($"{g}.DIRS") : 0,
                rockFrames: hasRock ? (int)host.EvaluateNumber($"{g}.ROCK.frames") : 0,
                defaultElevation: hasElev ? host.EvaluateNumber($"{g}.defaultElev") : 0);
        }
    }

    /// <summary>Geometry read from the rig itself, never from a README.</summary>
    public readonly struct RigGeometry
    {
        public readonly int Width, Height, NativeDirs, RockFrames;
        /// <summary>Pivot in cell pixels, measured from the TOP-LEFT (the rigs' screen origin).
        /// Unity sprite pivots are normalised from the BOTTOM-LEFT, so converting is
        /// <c>(pivotX / W, (H - pivotY) / H)</c> — that is where PuntIso's 0.44047618 comes from
        /// (168 − 94) / 168, and getting it upside-down is an easy and silent mistake.</summary>
        public readonly double PivotX, PivotY;
        public readonly double DefaultElevation;

        public RigGeometry(int width, int height, double pivotX, double pivotY,
                           int nativeDirs, int rockFrames, double defaultElevation)
        {
            Width = width; Height = height; PivotX = pivotX; PivotY = pivotY;
            NativeDirs = nativeDirs; RockFrames = rockFrames; DefaultElevation = defaultElevation;
        }

        /// <summary>
        /// The Unity sprite pivot: normalised, BOTTOM-origin.
        ///
        /// <para>⚠️ <b>The y term is <c>(H − pivotY)/H</c>, NOT <c>(H − 1 − pivotY)/H</c>, and that
        /// is correct — it has been challenged and MEASURED.</b> See <b>ADR 0026</b> and
        /// <see cref="RigPivotConventionProbe"/>. In short: a rig's <c>pivot</c> is a CONTINUOUS
        /// coordinate whose origin is the cell's top-left corner, not a pixel index. The rigs
        /// project with <c>sy = cy − (…)·S</c> into a space the rasterizer samples at pixel
        /// CENTRES (<c>y + 0.5</c>), and every rig in the repo sets <c>cx = W/2</c> exactly — an
        /// integer only the continuous reading can produce, since a column index would need the
        /// half-integer <c>(W − 1)/2</c>. So <c>pivotY</c> lands on the pivot row's TOP edge and
        /// this formula is exact.</para>
        ///
        /// <para><b>The tree bake deliberately differs</b> (<c>TreeKitCatalog.NormalizedPivot</c>
        /// uses the rig's own <c>pad/cellH</c>, one row lower). That is not a contradiction — a
        /// tree's pivot is a chosen ROW, a hull's is a projected POINT. Do not unify them; ADR 0026
        /// has the argument and a test guards both directions.</para>
        /// </summary>
        public Vector2 UnityNormalisedPivot =>
            new Vector2((float)(PivotX / Width), (float)((Height - PivotY) / Height));

        public override string ToString() =>
            $"{Width}×{Height} px, pivot ({PivotX},{PivotY}) top-left, " +
            $"{NativeDirs} native dirs, {RockFrames} rock frames, elev {DefaultElevation}°";
    }
}
