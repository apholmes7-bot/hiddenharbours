using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>What the hauling fisher's body is DOING this beat — which part of the owner's PlayerHaul
    /// sheet to show. Derived purely from consecutive haul snapshots (see <see cref="PlayerHaulAnimMath"/>).</summary>
    public enum HaulPose
    {
        /// <summary>No live haul — the normal standing/walking sprite owns the renderer.</summary>
        None = 0,
        /// <summary>Line is coming IN (holding on the lift / the calm wind-in) — play the hand-over-hand cycle.</summary>
        Pull = 1,
        /// <summary>Line is SLIPPING back (holding through the drop — the rope is fighting) — the strain frame.</summary>
        Strain = 2,
        /// <summary>The line is holding still (the pawl has it; the player eased off) — the ease frame.</summary>
        Ease = 3,
    }

    /// <summary>
    /// The PURE maths behind the deck hauler's animation — haul snapshot → pose → frame. Split out (the
    /// <c>PlayerSubmergeMath</c> / <c>TrapHaulMath</c> pattern) so the whole state→frame mapping is
    /// EditMode-testable headless. No engine state, no <c>Time</c>, no RNG.
    /// </summary>
    public static class PlayerHaulAnimMath
    {
        /// <summary>
        /// The pose for a haul snapshot, read off the LINE'S MOTION between consecutive snapshots — the same
        /// diegetic read the rope gives the player: line gaining → the hand-over-hand <see cref="HaulPose.Pull"/>;
        /// line slipping back → <see cref="HaulPose.Strain"/> (leaning into a loaded rope); line holding
        /// still → <see cref="HaulPose.Ease"/> (the pawl has it). Any non-hauling phase (idle / surfaced /
        /// empty) → <see cref="HaulPose.None"/>: give the renderer back to the walk sprite.
        /// </summary>
        /// <param name="phase">The snapshot's haul phase.</param>
        /// <param name="lineDelta">line01 change since the previous live snapshot (0 on the first).</param>
        /// <param name="epsilon">Dead-band below which the line reads as holding still.</param>
        public static HaulPose PoseFor(TrapHaulPhase phase, float lineDelta, float epsilon)
        {
            if (phase != TrapHaulPhase.Hauling) return HaulPose.None;
            float eps = Mathf.Max(0f, epsilon);
            if (lineDelta > eps) return HaulPose.Pull;
            if (lineDelta < -eps) return HaulPose.Strain;
            return HaulPose.Ease;
        }

        /// <summary>
        /// Which cycle frame (0..<paramref name="cycleFrameCount"/>−1) the hand-over-hand pull shows at a
        /// haul progress of <paramref name="line01"/>. The cycle is keyed DIRECTLY to the line hauled —
        /// <paramref name="cycleFramesPerLine"/> frames pass over a full 0→1 haul — so the hands literally
        /// move with the rope: a brisk take on a big lift runs the cycle fast, the calm wind-in strolls it,
        /// and when the line stops the hands stop (the pose logic swaps to strain/ease there). Deterministic
        /// from the snapshot alone — no timers to drift. Negative-safe; count ≤ 0 returns 0.
        /// </summary>
        public static int CycleFrame(float line01, float cycleFramesPerLine, int cycleFrameCount)
        {
            if (cycleFrameCount <= 0) return 0;
            float phase = Mathf.Max(0f, line01) * Mathf.Max(0f, cycleFramesPerLine);
            return Mathf.FloorToInt(phase) % cycleFrameCount;
        }

        /// <summary>
        /// Whether the haul sprite mirrors (flipX) so its ROPE SIDE points at the worked buoy.
        /// <paramref name="buoyDx"/> is buoy.x − player.x; <paramref name="artRopeSideIsLeft"/> says which
        /// side the SHEET draws the rope on (the owner's art holds it to the sprite's left). A buoy dead on
        /// the player's x keeps the un-flipped art. Pure + static.
        /// </summary>
        public static bool FlipXFor(float buoyDx, bool artRopeSideIsLeft)
            => artRopeSideIsLeft ? buoyDx > 0f : buoyDx < 0f;

        /// <summary>
        /// The quantity the owner actually tuned when they set "frames per line": how many full HEAVE
        /// CYCLES pass over one whole haul. <c>24 frames / a 6-frame cycle = 4 cycles per haul.</c>
        ///
        /// <para><b>This exists because the tuned number does not survive a re-baked sheet.</b> The rig's
        /// <c>haul</c> clip is EIGHT frames where the legacy sheet's cycle was six, and feeding an
        /// 8-frame clip the same 24 frames-per-line would give three cycles a haul instead of four — the
        /// same rope hauling a visibly slower fisher. What the owner tuned is the RATE OF WORK, not a
        /// frame index, so the rate is what carries across and the frame count comes from the art.</para>
        /// </summary>
        public static float CyclesPerHaul(float cycleFramesPerLine, int cycleFrameCount)
            => cycleFrameCount <= 0 ? 0f : Mathf.Max(0f, cycleFramesPerLine) / cycleFrameCount;

        /// <summary>
        /// How far along a <paramref name="clipFrameCount"/>-frame heave the hands stand at a haul
        /// progress of <paramref name="line01"/>, in clip frames — the <see cref="CycleFrame"/> mapping
        /// expressed for a clip that owns its own frame count. Keyed straight to the line, so the hands
        /// still move with the rope: the cycle rate breathes with the take rate and stops when it stops.
        /// Negative-safe; a degenerate clip or rate answers 0 (frame 0).
        /// </summary>
        public static float HeavePosition(float line01, float cyclesPerHaul, int clipFrameCount)
            => Mathf.Max(0f, line01) * Mathf.Max(0f, cyclesPerHaul) * Mathf.Max(0, clipFrameCount);
    }

    /// <summary>
    /// Plays the owner's PLAYER HAUL sheet on the deck-walking fisher while a trap haul is live — the
    /// body language of haul-with-the-swell (#184): the hand-over-hand cycle while line comes in, the
    /// STRAIN frame while the rope fights back on a drop, the EASE frame while the pawl holds, and the
    /// sprite mirrored (flipX) so the rope side faces the worked buoy. When the haul ends (surfaced /
    /// empty / let go) the walk sprite is restored exactly as it was.
    ///
    /// <para><b>Cross-module via Core only (rule 4).</b> It never references Fishing: everything it needs
    /// arrives in the <see cref="TrapHaulStateChanged"/> snapshots (phase, line01, strain, the buoy's
    /// position) — published on quantized line/strain steps and hold/release edges, never per frame. The
    /// pose is derived from the line's motion between snapshots (<see cref="PlayerHaulAnimMath.PoseFor"/>),
    /// and the pull cycle is keyed straight to line01 (<see cref="PlayerHaulAnimMath.CycleFrame"/>) so the
    /// cycle rate breathes with the take rate for free — no Update loop, no timers, no per-frame work at
    /// all (rule 7): the component only runs when a snapshot lands.</para>
    ///
    /// <para><b>The art it draws with is the rig's <c>haul</c> clip when that is baked</b> (pass 6.2),
    /// and the owner's flat <c>PlayerHaul.png</c> when it is not. The clip is the better picture by a
    /// distance — eight DRAWN facings instead of one mirrored pose, so the fisher heaves toward the pot
    /// she is actually working rather than merely toward its side of her — and it is the sheet the kit
    /// says replaces the legacy one. The swap changes nothing about WHEN each pose shows: the snapshot →
    /// pose read (<see cref="PlayerHaulAnimMath.PoseFor"/>) is untouched, and the cycle stays keyed to
    /// <c>line01</c> rather than to a clock, which is why it plays through
    /// <see cref="CharacterClipPlayer.PlayDriven"/> and not <c>Play</c>.</para>
    ///
    /// <para><b>Strain and ease are HOLDS inside the heave, because the rig has no separate poses for
    /// them.</b> The legacy sheet drew three things — a 6-frame cycle, a strain frame and an ease frame;
    /// the rig draws one 8-frame two-handed heave and nothing else. But the heave passes through both
    /// extremes on its way round, and the rig says exactly where: <c>haulCurve</c> publishes a rope
    /// TENSION per frame, baked into the anchors sidecar by <c>haulGrip()</c>, and over the eight frames
    /// it reads 0, 0.03, 0.06, 0.43, 0.78, <b>0.90</b>, 0.49, 0.10 — identically on all eight facings.
    /// Frame 5 is the rope at its most loaded and frame 0 is it slack, so STRAIN holds 5 and EASE holds
    /// 0. Both are measured off the rig, not eyeballed, and both are serialized tunables (rule 6) so a
    /// re-authored heave can be re-pinned without code.</para>
    ///
    /// <para><b>Legacy frames.</b> Wired by the builder from the sliced <c>PlayerHaul.png</c> in slice
    /// order — frames 0..5 the hand-over-hand cycle, 6 STRAIN (leaning back, rope loaded), 7 EASE
    /// (letting the pawl hold) — the owner's sheet spec. The indices are serialized tunables so the owner
    /// can remap without code if the sheet evolves. Neither art → the component is inert (null-safe
    /// greybox rule).</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class PlayerHaulAnimator : MonoBehaviour
    {
        // Line-motion dead-band for the pose read: matches the take epsilon the haul itself uses to call a
        // tick "gained" (a read threshold, not a balance number).
        private const float LineDeltaEpsilon = 1e-5f;

        [Header("The owner's haul sheet (slice order)")]
        [Tooltip("PlayerHaul frames in slice order: 0..5 = the hand-over-hand pull cycle, 6 = STRAIN " +
                 "(leaning back, rope loaded), 7 = EASE (relaxed, the pawl holds). Wired by the builder " +
                 "from the sliced sheet; empty = the animator is inert.")]
        [SerializeField] private Sprite[] _frames;

        [Header("Frame mapping (owner-remappable, rule 6)")]
        [Tooltip("How many leading frames form the hand-over-hand pull cycle.")]
        [SerializeField, Min(1)] private int _cycleFrameCount = 6;
        [Tooltip("Frame index shown while the rope FIGHTS (line slipping back on a held drop).")]
        [SerializeField, Min(0)] private int _strainFrame = 6;
        [Tooltip("Frame index shown while the pawl holds the line (player eased off).")]
        [SerializeField, Min(0)] private int _easeFrame = 7;
        [Tooltip("Cycle frames that pass over a FULL haul (line 0→1) — the hands move with the rope, so " +
                 "the cycle rate breathes with the take rate. 24 = four full 6-frame cycles per haul.")]
        [SerializeField, Min(0f)] private float _cycleFramesPerLine = 24f;
        [Tooltip("Which side the SHEET draws the rope on (the owner's art pulls at the sprite's LEFT). " +
                 "The sprite mirrors so this side faces the worked buoy. LEGACY SHEET ONLY — the rig's " +
                 "clip draws all eight facings, so it is aimed rather than mirrored.")]
        [SerializeField] private bool _artRopeSideIsLeft = true;

        [Header("The rig's haul CLIP (used whenever it is baked)")]
        [Tooltip("Frame of the rig's heave held while the rope FIGHTS. MEASURED, not chosen: the rig's " +
                 "haulCurve peaks its rope tension at 0.90 on frame 5 of 8 (sidecar clipPins). Re-measure " +
                 "if the heave is ever re-authored.")]
        [SerializeField, Min(0)] private int _clipStrainFrame = 5;
        [Tooltip("Frame of the rig's heave held while the pawl holds the line. MEASURED: tension is 0 on " +
                 "frame 0 of 8 — the slack reach at the top of the stroke.")]
        [SerializeField, Min(0)] private int _clipEaseFrame = 0;

        // A buoy this close to the fisher in plan gives no honest direction to face, so the last heading
        // is kept instead of snapping her North. Well under a hull's beam — a worked pot is never here.
        private const float MinBuoyOffset = 0.05f;

        private SpriteRenderer _renderer;
        // The on-foot 8-direction iso skin, when one is installed. While a haul is live the haul sheet owns
        // the renderer, so the iso driver is explicitly SUSPENDED for the duration and Released on hand-back
        // — an announced hand-off rather than two components overwriting each other's sprite every frame.
        // Cached once (this component has no Update loop, but GetComponent per snapshot is still waste).
        private HiddenHarbours.Core.IsoCharacterSprite _isoSkin;
        private bool _isoSkinResolved;
        // The shared clip seam. It does its OWN Suspend/Release of the iso skin, so on the clip path
        // this component never touches the renderer at all — exactly one claimant per path, never two.
        private HiddenHarbours.Core.CharacterClipPlayer _clipPlayer;
        private bool _clipPlayerResolved;
        private bool _clipActive;         // the rig's clip owns the renderer, started by US
        private float _clipHeading;       // the direction last aimed at the buoy (the no-offset fallback)
        private bool _active;             // the LEGACY sheet owns the renderer right now
        private Sprite _restoreSprite;    // the walk sprite to hand back when the haul ends
        private bool _restoreFlipX;
        private float _lastLine01;
        private bool _hasLastLine;        // the previous snapshot was a live-haul one (delta is meaningful)

        /// <summary>The pose the live haul is in — None when there is no haul and the walk sprite owns
        /// the renderer. It reads the HAUL, not the renderer, so it stays truthful through the one beat
        /// where a boarding vault has the body and the heave is standing aside. For tests/tooling.</summary>
        public HaulPose Pose { get; private set; } = HaulPose.None;

        private void OnEnable() => EventBus.Subscribe<TrapHaulStateChanged>(OnHaulStateChanged);

        private void OnDisable()
        {
            EventBus.Unsubscribe<TrapHaulStateChanged>(OnHaulStateChanged);
            EndHaul();   // disabling mid-haul hands the renderer back (never a stuck haul frame)
        }

        /// <summary>Public so tests can drive the mapping through the same path the bus uses (the
        /// TrapDeckGating convention — no play-mode lifecycle needed).</summary>
        public void OnHaulStateChanged(TrapHaulStateChanged e)
        {
            TrapHaulState s = e.State;
            float delta = _hasLastLine ? s.Line01 - _lastLine01 : 0f;
            _lastLine01 = s.Line01;
            _hasLastLine = s.Phase == TrapHaulPhase.Hauling;

            HaulPose pose = PlayerHaulAnimMath.PoseFor(s.Phase, delta, LineDeltaEpsilon);
            if (pose == HaulPose.None) { EndHaul(); return; }

            // The rig's clip first when it is baked; the owner's flat sheet below when it is not. The
            // fallback is a STRICT no-op — every line past here is the code that shipped before.
            if (DrawWithClip(s, pose)) { Pose = pose; return; }

            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
            if (_renderer == null || _frames == null || _frames.Length == 0) return;   // inert without art

            if (!_active)
            {
                // Take the renderer over: remember the walk sprite so the haul ends exactly where it began,
                // and tell the iso skin (if any) to stand down so it stops writing frames underneath us.
                _restoreSprite = _renderer.sprite;
                _restoreFlipX = _renderer.flipX;
                _active = true;
                ResolveIsoSkin();
                if (_isoSkin != null) _isoSkin.Suspend();
            }

            // Face the buoy: the rope side of the sheet points at the pot being worked.
            _renderer.flipX = PlayerHaulAnimMath.FlipXFor(s.BuoyX - transform.position.x, _artRopeSideIsLeft);

            int idx = pose switch
            {
                HaulPose.Pull => PlayerHaulAnimMath.CycleFrame(s.Line01, _cycleFramesPerLine, _cycleFrameCount),
                HaulPose.Strain => _strainFrame,
                _ => _easeFrame,
            };
            if (idx >= 0 && idx < _frames.Length && _frames[idx] != null)
                _renderer.sprite = _frames[idx];
            Pose = pose;
        }

        // ---- the rig's haul clip (pass 6.2) ---------------------------------------------------------

        /// <summary>
        /// Draw this beat with the rig's <c>haul</c> clip, and say whether it did. FALSE means the clip
        /// is not available and the caller should fall through to the owner's flat sheet unchanged.
        ///
        /// <para><b>What the clip is asked to do is the LINE, not a clock</b> — hence
        /// <see cref="CharacterClipPlayer.PlayDriven"/>: the heave is seeked to a position derived from
        /// <c>line01</c>, so the hands still move with the rope and this component still does no
        /// per-frame work at all (rule 7). Started ONCE and seeked thereafter; re-playing every snapshot
        /// would freeze it on frame 0.</para>
        /// </summary>
        private bool DrawWithClip(in TrapHaulState s, HaulPose pose)
        {
            var player = ResolveClipPlayer();
            if (player == null) { EndClip(); return false; }

            // Another driver has the seam. A boarding vault is a whole-body move over a rail and it wins
            // outright: stand down and touch nothing. The snapshot after it lands takes the heave back
            // on its own, because the re-take below tests what is actually running rather than a flag.
            if (player.IsPlaying && player.Clip != HiddenHarbours.Core.CharacterClip.Haul)
            {
                _clipActive = false;
                return true;
            }

            if (!player.CanPlay(HiddenHarbours.Core.CharacterClip.Haul)) { EndClip(); return false; }

            var def = player.ResolvedVisual();
            if (def == null) { EndClip(); return false; }
            int clipFrames = def.ClipFrameCount(HiddenHarbours.Core.CharacterClip.Haul);
            if (clipFrames <= 0) { EndClip(); return false; }

            // AIMED, NOT MIRRORED. Every one of the clip's eight facings is drawn, so the fisher turns to
            // the pot she is working instead of the flat sheet's one pose flipped toward its side of her.
            float heading = HeadingToBuoy(s);
            _clipHeading = heading;

            // The cycle rate carries across as CYCLES PER HAUL — the thing the owner tuned — and the
            // frame count comes from the art. Handing the 8-frame clip the 6-frame sheet's 24
            // frames-per-line would quietly drop the fisher from four heaves a pot to three.
            float position = pose switch
            {
                HaulPose.Pull => PlayerHaulAnimMath.HeavePosition(
                    s.Line01,
                    PlayerHaulAnimMath.CyclesPerHaul(_cycleFramesPerLine, _cycleFrameCount),
                    clipFrames),
                HaulPose.Strain => Mathf.Clamp(_clipStrainFrame, 0, clipFrames - 1),
                _ => Mathf.Clamp(_clipEaseFrame, 0, clipFrames - 1),
            };

            // Re-take whenever what is running is not OUR driven haul — which also covers the case where
            // a vault's Stop() took our claim down with it while we were standing aside.
            bool ours = player.IsDriven && player.Clip == HiddenHarbours.Core.CharacterClip.Haul;
            if (!ours)
            {
                if (!player.PlayDriven(HiddenHarbours.Core.CharacterClip.Haul, heading, position))
                {
                    EndClip();
                    return false;      // the seam refused it — the flat sheet is still a picture
                }
                _clipActive = true;
                return true;
            }

            player.SetHeading(heading);
            player.SeekFrame(position);
            return true;
        }

        /// <summary>The compass direction from the fisher to the pot she is working. A buoy sitting all
        /// but on top of her gives no honest direction, so the last one aimed is kept rather than
        /// snapping her round to North.
        ///
        /// <para>GroundHeadingFor, not HeadingFor: both positions are WORLD-space, and world XY is the
        /// squashed ground plane while the clip's rows depict ground bearings (see
        /// <c>Core.IsoGround</c>).</para></summary>
        private float HeadingToBuoy(in TrapHaulState s)
        {
            Vector3 p = transform.position;
            if (!_clipActive)
            {
                // Seed the fallback from wherever she is already looking, so the very first snapshot of a
                // haul on a buoy underfoot keeps her facing rather than jumping her.
                ResolveIsoSkin();
                if (_isoSkin != null) _clipHeading = _isoSkin.HeadingDegrees;
            }
            return HiddenHarbours.Core.IsoCharacterMath.GroundHeadingFor(
                new Vector2(s.BuoyX - p.x, s.BuoyY - p.y), MinBuoyOffset, _clipHeading);
        }

        /// <summary>Take the clip off the renderer (idempotent). It restores the walk sprite itself.</summary>
        private void EndClip()
        {
            if (!_clipActive) return;
            _clipActive = false;
            // ONLY ever stop the clip THIS component started. The boarding move shares this one seam, so
            // a haul ending mid-vault must not yank the fisher's board clip off the renderer and drop her
            // back to walk frames in mid-air.
            if (_clipPlayer != null && _clipPlayer.Clip == HiddenHarbours.Core.CharacterClip.Haul)
                _clipPlayer.Stop();
        }

        /// <summary>Find the clip seam once, lazily — this component may be added before it is.</summary>
        private HiddenHarbours.Core.CharacterClipPlayer ResolveClipPlayer()
        {
            if (_clipPlayerResolved) return _clipPlayer;
            _clipPlayerResolved = true;
            _clipPlayer = GetComponent<HiddenHarbours.Core.CharacterClipPlayer>();
            return _clipPlayer;
        }

        /// <summary>Hand the renderer back to the walk sprite (idempotent — safe on disable / repeat ends).</summary>
        private void EndHaul()
        {
            Pose = HaulPose.None;
            _hasLastLine = false;
            EndClip();               // no-op on the legacy path, and the clip restores the sprite itself
            if (!_active) return;
            _active = false;
            // Hand the renderer back to the iso skin FIRST (it re-asserts its own cell next frame), then
            // restore the sprite we found, so a character with no iso skin still ends exactly where it began.
            if (_isoSkin != null) _isoSkin.Release();
            if (_renderer == null) return;
            _renderer.sprite = _restoreSprite;
            _renderer.flipX = _restoreFlipX;
            _restoreSprite = null;
        }

        /// <summary>Find the iso skin once, lazily — this component may be added before it is.</summary>
        private void ResolveIsoSkin()
        {
            if (_isoSkinResolved) return;
            _isoSkinResolved = true;
            _isoSkin = GetComponent<HiddenHarbours.Core.IsoCharacterSprite>();
        }

        /// <summary>Wire the animator in one call (tests / editor builder): the sliced haul frames, and
        /// optionally a remapped cycle/strain/ease layout for the flat sheet plus the CLIP's two hold
        /// frames (negatives leave the serialized defaults).</summary>
        public void Configure(Sprite[] frames, int cycleFrameCount = -1, int strainFrame = -1, int easeFrame = -1,
                              float cycleFramesPerLine = -1f, int clipStrainFrame = -1, int clipEaseFrame = -1)
        {
            _frames = frames;
            if (cycleFrameCount > 0) _cycleFrameCount = cycleFrameCount;
            if (strainFrame >= 0) _strainFrame = strainFrame;
            if (easeFrame >= 0) _easeFrame = easeFrame;
            if (cycleFramesPerLine >= 0f) _cycleFramesPerLine = cycleFramesPerLine;
            if (clipStrainFrame >= 0) _clipStrainFrame = clipStrainFrame;
            if (clipEaseFrame >= 0) _clipEaseFrame = clipEaseFrame;
        }
    }
}
