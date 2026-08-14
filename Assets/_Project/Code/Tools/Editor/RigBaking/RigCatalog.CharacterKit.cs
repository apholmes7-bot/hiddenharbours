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
            };
    }
}
