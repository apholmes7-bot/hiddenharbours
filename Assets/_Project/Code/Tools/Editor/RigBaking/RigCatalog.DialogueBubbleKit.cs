using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tools.RigBaking
{
    public static partial class RigCatalog
    {
        /// <summary>
        /// The dialogue bubble kit — the talking UI, drawn as objects in the scene.
        ///
        /// <para>One kit, one file (see <c>RigCatalog.cs</c> for the assembly contract). The comments
        /// on the entries below are MEASUREMENTS someone paid for — move them with their entry, never
        /// summarise them away.</para>
        /// </summary>
        [RigContribution]
        static IEnumerable<KeyValuePair<string, RigEntry>> DialogueBubbleKitRigs() =>
            new RigRegistration
            {
                // NOT DIRECTIONAL — no camera, no turntable, no facings at all. Every piece is a
                // flat screen-space stamp on the 32 px/m assets grid, so the convention below is the
                // placeholder every non-directional entry carries (shellfish, catchKit) and nothing
                // probes it.
                //
                // ⚠️ IT DRAWS, so it needs a canvas — and unlike catchKit's, which only wants
                // somewhere to leave pixels it composed itself, this one genuinely rasterises
                // through fillStyle/fillRect. BubbleKitBaker.CanvasShimJs installs a real
                // rasteriser BEFORE this loads (ADR 0021 §5: what the rig needs and does not
                // provide is the HOST's job, never a patch to the art director's file).
                //
                // No prerequisites: the bubble rig declares "No dependencies" on the tin and it
                // holds — it installs and renders every piece in a bare host carrying nothing but
                // the shim.
                ["dialogueBubble"] = new RigEntry(
                    $"{RigFolder}/dialogue-bubble-kit/Art/dialogueBubbleRig.js", "BubbleKit",
                    AzimuthConvention.Clockwise),

                // The character-side talk cues + the mouth-channel probe.
                //
                // ⚠️ THE PREREQUISITE IS THE SHIPPING CHARACTER RIG, NOT THE KIT'S COPY. The kit
                // ships characterIsoRig6.js / headIsoRig3.js / eyeIsoRig.js under its own Art/
                // folder for its harness, and all three are BYTE-IDENTICAL to the ones already at
                // docs/art/rigs/ (verified — DialogueBubbleKitRigSourcesTests keeps it that way).
                // Pointing at the kit's duplicates would fork the character rig the day either copy
                // moves, and the probe would then be adjudicating a rig the game does not run. So
                // this depends on "character", which drags in characterHead and characterEye
                // through its own declared prerequisites.
                //
                // TalkCue itself draws (emote markers, fillStyle/fillRect), so it wants the same
                // shim the bubble rig does.
                ["talkCue"] = new RigEntry(
                    $"{RigFolder}/dialogue-bubble-kit/Art/talkCueRig.js", "TalkCue",
                    AzimuthConvention.Clockwise,
                    prerequisites: new[] { "character" }),
            };
    }
}
