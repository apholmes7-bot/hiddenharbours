using UnityEngine;

namespace HiddenHarbours.Audio
{
    /// <summary>
    /// The twenty-four manifest slots as DATA (rule 2) — one serialized <see cref="AudioClip"/> per slot in
    /// <c>Assets/_Project/Audio/AUDIO-MANIFEST.md</c>, eleven for <see cref="AudioDirector"/> and thirteen
    /// for <see cref="FishingAudio"/>.
    ///
    /// <para><b>Why this exists.</b> Both players already generate a procedural placeholder for every
    /// slot they find empty. Slotting a real recording used to mean touching the player, which makes a
    /// new sound a CODE change. It is content, so it is an asset: the single instance lives at
    /// <c>Assets/_Project/Audio/Resources/AudioClipSet.asset</c>, each player loads it before it builds
    /// its sources, and a clip that is present replaces that slot's placeholder. Adding, swapping or
    /// removing a sound after this is an inspector edit and a licence row — no C#.</para>
    ///
    /// <para><b>A null slot is not a bug.</b> It is the honest state for a sound we have no
    /// correctly-licensed source for yet; the procedural placeholder keeps playing and the slot stays on
    /// the owner's shopping list in <c>Assets/_Project/Audio/LICENSES.md</c>. Every field is null by
    /// default for exactly that reason.</para>
    ///
    /// <para><b>Every clip here is redistributed.</b> The repository is public, so a file in one of these
    /// slots must carry a licence that permits it — CC0, or CC-BY with the attribution recorded. The
    /// ledger in <c>LICENSES.md</c> has one row per file and is part of the definition of done.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "AudioClipSet", menuName = "Hidden Harbours/Audio/Clip Set")]
    public sealed class AudioClipSetDef : ScriptableObject
    {
        [Tooltip("Stable, append-only id (rule 2). There is exactly one of these assets.")]
        public string Id = "audio.clip_set";

        // ---- AudioDirector: ambience and cues -----------------------------------------------

        [Header("Director — ambience beds")]
        [Tooltip("The always-on calm-sea bed. Even, eventless, no gulls and no breaking waves in it.")]
        public AudioClip CalmBed;

        [Tooltip("Sparse gull calls over true silence — the coast's voice, layered onto the bed.")]
        public AudioClip Gulls;

        [Tooltip("Oar-stroke / water bed, aboard the hand-rowed dory. Its gain is ridden by the crossfade.")]
        public AudioClip HullRow;

        [Tooltip("Looping outboard-engine bed. PITCHED at runtime by speed over ground, so the source " +
                 "must hold one steady rev with no acceleration baked into it.")]
        public AudioClip OutboardEngine;

        [Tooltip("The SACRED rising-wind tell (P1/P5). Faded up as the wind strengthens, ahead of danger, " +
                 "so the source must be an even wind with no gusts and no events of its own.")]
        public AudioClip WindTell;

        [Header("Director — one-shot cues")]
        [Tooltip("The catch sting. Musical: it must sit in the same tonal world as the eventual score.")]
        public AudioClip CatchSting;

        [Tooltip("The earned made-it-home warmth. Musical, and it only fires when the trip was a worry.")]
        public AudioClip HomeWarmth;

        [Header("Director — the three moments (juice charter §4.5; no placeholder, a null slot is SILENT)")]
        [Tooltip("The landing frame: JuiceMomentCue(Landing), the frame the fish leaves the water, with the hit-stop and the splash.")]
        public AudioClip LandingHit;

        [Tooltip("The sale's reward beat on CatchSold, with the coins flying in the notebook. While null the director plays HomeWarmth in its place.")]
        public AudioClip SaleChime;

        [Tooltip("The shovel's strike: JuiceMomentCue(DigStrike), with the sand chunks.")]
        public AudioClip DigStrike;

        [Tooltip("The line touching down: JuiceMomentCue(CastEntry), with the rings.")]
        public AudioClip CastEntry;

        // ---- FishingAudio: the rod fight ----------------------------------------------------

        [Header("Fishing — loops (gain- and pitch-ridden by the fight)")]
        [Tooltip("Rod creak under the wind-back draw.")]
        public AudioClip RodCreakLoop;

        [Tooltip("Line pay-out ticks while the rig sinks. A steady tick rate — no ritardando.")]
        public AudioClip PayoutTickLoop;

        [Tooltip("The fight's tension voice: a low strain groan whose gain follows the strain.")]
        public AudioClip StrainGroanLoop;

        [Tooltip("Reel clicks while gaining line. Steady, like the pay-out ticks.")]
        public AudioClip ReelClickLoop;

        [Tooltip("Surface thrash and churn once she is up.")]
        public AudioClip SurfaceThrashLoop;

        [Header("Fishing — one-shots")]
        [Tooltip("The cast whoosh.")]
        public AudioClip CastWhoosh;

        [Tooltip("The rig hitting the water.")]
        public AudioClip SplashDown;

        [Tooltip("The bobber settling — a small, close plop.")]
        public AudioClip BobberPlop;

        [Tooltip("A knock through the rod: the bite tell.")]
        public AudioClip RodKnock;

        [Tooltip("The weight finding the bottom.")]
        public AudioClip BottomSettle;

        [Tooltip("Line going slack — she is off, or she turned.")]
        public AudioClip SlackRelease;

        [Tooltip("The line parting. The trip's worst sound; it must read as a break, not a thump.")]
        public AudioClip SnapSting;

        [Tooltip("Landed. Short, and not musical — the sting carries the music.")]
        public AudioClip LandedFlourish;
    }
}
