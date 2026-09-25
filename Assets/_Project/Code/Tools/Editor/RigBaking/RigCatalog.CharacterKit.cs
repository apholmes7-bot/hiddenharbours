using System.Collections.Generic;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// The pass-6 character — eye, head, body. The first registration in this catalog that is not
        /// one file, and the reason <see cref="RigEntry.Prerequisites"/> exists at all.
        ///
        /// <para>One kit, one file (see <c>RigCatalog.cs</c> for the assembly contract). The comments
        /// on the entries below are MEASUREMENTS someone paid for — move them with their entry, never
        /// summarise them away.</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> CharacterKitRigs() =>
            new RigRegistration
            {
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

                // ---- the hand-prop ANCHOR layer, rev 6.4 (drop of 2026-08-14) --------------------
                //
                // The fourth file of the character kit, and the only one that draws nothing: it is a
                // TABLE. For a given carried object it says where on the hand it sits, WHICH hand holds
                // it at which heading, how it is angled to read there, and whether it draws over or
                // under the sprite. That layer used to be written per consumer page, in px, by eye —
                // which is exactly why every held thing hung off ONE fixed hip offset at all eight
                // facings (CarryHands._hipOffsetMeters, flagged in its own tooltip as art-lane work).
                //
                // ⚠️ LOADS AFTER THE BODY, not before: it reads the body's camera basis and anchors().
                // Prerequisites are depth-first and in order, so naming "character" here is what
                // guarantees eye -> head -> body -> hands. Reversed, nothing throws — `C()` resolves to
                // null and every pin() silently returns null.
                //
                // A module, not a rig: it exposes no W/H/pivot triple (its numbers are body-local
                // METRES, which is what makes one table correct at any elev and any density), so
                // Install would throw on the missing pivot and InstallModule is the entry point.
                // Convention is inherited from the body it annotates rather than measured — the table
                // holds no pixels of its own to measure.
                ["characterHands"] = new RigEntry($"{RigFolder}/characterIsoRig6.hands.js",
                                                  "CharacterHands6",
                                                  AzimuthConvention.Clockwise,
                                                  prerequisites: new[] { "character" }),

                // ---- the SKINNED EXPORT layer, rig 7 rev 7.1 (drop of 2026-09-09) ----------------
                //
                // The fifth file, and the second that draws nothing on its own: it re-expresses the
                // body as a SKELETON + a BIND MESH + CLIPS, so one mesh can be posed by bones instead
                // of re-lathed per frame. Its API object is Object.create(CharacterIso6), so W, H,
                // pivot, ANIMS, render() and friends all resolve THROUGH the body by prototype — which
                // is exactly why it must load after it, and why it still installs with InstallModule:
                // its own contribution is the export, not a cell.
                //
                // ⚠️ The base it re-expresses is NAMED: CharacterIso7.base is CharacterIso6.revision.
                // Bump the body without re-running the export and nothing throws — the skinned mesh
                // simply stops agreeing with the sprite, silently, one clip at a time. That agreement
                // is measured on CI by CharacterSkinnedExportTests, not assumed here.
                //
                // Re-run against the drop on 2026-09-09, 10 builds x 56 golden rows x 462 frames:
                // worst vertex error 4.02e-13 m against a 1e-4 m tolerance, 0 failing rows; and
                // 36,960 rendered probes (56 rows x 8 dirs x 10 builds) of renderSkinned() against
                // render() came back byte-identical, 0 pixels.
                ["characterSkin"] = new RigEntry($"{RigFolder}/characterIsoRig7.js",
                                                 "CharacterIso7",
                                                 AzimuthConvention.Clockwise,
                                                 prerequisites: new[] { "character" }),

                // ---- the PASS-05/06 FACE layers (workbench drop of 2026-09-14) -------------------
                //
                // Files six through ten, and the reason the mesh gets a face the sprite does not. The
                // owner's driver was one sentence — "The eyes do not look good" — so pass 06 revises
                // pass 05's flat dark openings and unfocused gaze. These four layers were proved
                // OFFLINE in docs/art/character-workbench/ and are promoted here UNCHANGED, so what
                // the bake runs is the file the checkers scored.
                //
                // ⚠️ None of them draws a cell either. All five install with InstallModule.

                // The tailoring TABLE the finish rig reads, as a module rather than a JSON sidecar:
                // the V8 host has no file system, so a sidecar would have to be injected host-side
                // BEFORE characterFinish ran — and InstallModule has no such hook. characterFinish
                // binds root.CharacterFinishConfig at LOAD time, so a wrong order would NOT throw; it
                // would fail later, inside a bake, on one preset. As a module it is simply a
                // prerequisite, and the missing-global assert catches the order for free.
                // Regenerated from docs/art/character-workbench/character-finish.json, which
                // CharacterFinishConfigTests re-checks value for value. Holds no pixels to measure,
                // so its convention is inherited, not observed.
                ["characterFinishConfig"] = new RigEntry($"{RigFolder}/characterFinishConfig.js",
                                                         "CharacterFinishConfig",
                                                         AzimuthConvention.Clockwise),

                // Pass-05 garment/jaw tailoring. Reads NOTHING but the table above — the body faces,
                // the build and the bind skeleton all arrive as arguments — which is what lets it
                // re-tailor garment volumes without ever seeing a bone it could move.
                ["characterFinish"] = new RigEntry($"{RigFolder}/characterFinish.js",
                                                   "CharacterFinish",
                                                   AzimuthConvention.Clockwise,
                                                   prerequisites: new[] { "characterFinishConfig" }),

                // Pass-06 head: the eyes, brows, mouth and the cheek/jaw planes. It does not replace
                // the head rig, it DRESSES it — poseState() calls root.HeadIso.loopLook() for gaze,
                // lid, blink and speech, so expressions stay the head rig's own state machine and
                // only the surface changes. Hence characterHead, whose file installs HeadIso3,
                // HeadIso2 and HeadIso as the same object (pass 7 is a superset).
                ["characterFaceStudy"] = new RigEntry($"{RigFolder}/characterFaceStudy.js",
                                                      "CharacterHeadStudy",
                                                      AzimuthConvention.Clockwise,
                                                      prerequisites: new[] { "characterHead" }),

                // The Fisher-only body study (the bounded clothing-assembly proof). Self-contained by
                // construction — build, skeletonWorld and bindMesh are all parameters — so it declares
                // no prerequisite: there is no global it could read in the wrong order. Only the
                // 'fisher' preset routes through it; the other nine take their bind faces straight.
                ["characterArtStudy"] = new RigEntry($"{RigFolder}/characterArtStudy.js",
                                                     "CharacterArtStudy",
                                                     AzimuthConvention.Clockwise),

                // ---- the COMPOSITION, and the only layer that changes what the bake exports -------
                //
                // The production form of the workbench cast-engine: finish-tailored body + pass-06
                // head, the head rebound rigidly to the existing head bone at weight 1.
                //
                // ⚠️ It PATCHES NOTHING — installing it is inert, and that is deliberate. Wrapping
                // CharacterIso7.bindMesh reads like the tidiest seam and is wrong: the skinned bake
                // takes its GEOMETRY from rig 6 and only its WEIGHTS from bindMesh, then requires the
                // two to agree corner for corner. Composing one side would not produce a new face, it
                // would produce a bake that throws. The C# caller opts in at a named step instead.
                //
                // Nothing here calls skeleton(), skeletonWorld(), clip() or clips(). Metre scale, physical
                // heights and hand/foot/attachment anchors are gameplay pins; leaving their code paths
                // untouched makes "unchanged" provable by reading the file rather than by trusting a
                // measurement. Measured anyway, all ten presets, on the standalone V8 harness:
                // skeletonWorld identical (worst delta 0 m), local bind skeleton identical, all 350
                // preset x animation clip rows identical, max bone influences still 2, and every
                // preset gains head-tagged faces. CharacterSkinnedPinTests re-measures it on CI.
                //
                // ⚠️ It names all four layers as prerequisites AND asserts them itself, because a
                // missing layer here is the one failure that would otherwise be silent-ish: without
                // the composition installed the bake simply exports the OLD face and passes.
                ["characterFaceComposition"] = new RigEntry(
                    $"{RigFolder}/characterFaceComposition.js",
                    "CharacterFaceComposition",
                    AzimuthConvention.Clockwise,
                    prerequisites: new[] { "characterSkin", "characterFaceStudy",
                                           "characterFinish", "characterArtStudy" }),

                // Added by the character rig intake, PR 1 (2026-09-25): Claude Design's character-rig
                // kit v9.2, landed as delivered under character/rig9/ (rig 7's folder is untouched).
                // The body IS the skinned export (skeleton, bind mesh, clips and its own shading
                // contract), so it names no prerequisite. Its poses (characterIsoRig9.poses.js) and
                // checks (characterIsoRig9.checks.js) patch THIS global and define none of their own,
                // so they cannot be entries: InstallModule asserts the global an entry names.
                // CharacterSkinExtractor.Load9 runs them. Nothing bakes from it until
                // CharacterSkinAssetBaker.LiveRig names it.
                ["characterRig9"] = new RigEntry(
                    $"{RigFolder}/character/rig9/Art/characterIsoRig9.js",
                    "CharacterIso9",
                    AzimuthConvention.Clockwise),
            };
    }
}
