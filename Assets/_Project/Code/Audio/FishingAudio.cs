using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Audio
{
    /// <summary>
    /// The diegetic ROD-FIGHT sound layer for Rod Fishing v2 (design rod-fishing-v2-brainstorm.md
    /// §2–3, §7 — "the rod is the instrument, no HUD"). SELF-INSTALLING like <see cref="AudioDirector"/>
    /// (no builder wiring) and consuming ONLY the Core <see cref="FishingStateChanged"/> snapshot
    /// (rule 4: never the Fishing module). It voices:
    /// <list type="bullet">
    ///   <item>the wind-back rod CREAK, the cast WHOOSH + line whistle, and the SPLASH-DOWN,</item>
    ///   <item>the sinking reel PAY-OUT tick that slows as the rig nears bottom, and the soft
    ///   "line settles" note when the slack bottom tell opens (§2.3's you-felt-bottom read),</item>
    ///   <item>the two bite tells — the bobber PLOP (cast path) vs the deep rod-tip KNOCK (depth
    ///   path, <c>Depth01 &gt; 0</c>) — §2.1's audio/feel tell,</item>
    ///   <item>the fight: the continuous line-STRAIN groan riding Tension01 (the "ease off!"
    ///   voice), REEL clicks while Landing01 is rising, the SLACK-window release cue (the "PULL
    ///   now" moment), the surface THRASH churn panned on the fish (FightSurface), the cozy SNAP
    ///   sting and the LANDED flourish + wet slap.</item>
    /// </list>
    /// The legacy <see cref="FishingPhase.Fighting"/> is serviced by the same strain/reel layers
    /// (grouped via <see cref="FishingState.IsFightPhase"/> — no special casing). All decisions live
    /// in the pure, EditMode-tested <see cref="FishingAudioLogic"/>; this class is just the player.
    /// Loop sources are built ONCE and play at zero volume (crossfaded in — no Play/Stop pops, no
    /// per-frame allocation, rule 7). Placeholder clips are procedural until the owner's real SFX
    /// slot into the serialized refs (see <c>Assets/_Project/Audio/AUDIO-MANIFEST.md</c>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FishingAudio : MonoBehaviour
    {
        private static FishingAudio _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[FishingAudio]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<FishingAudio>();
        }

        // ---- owner-tunable mix --------------------------------------------------------------
        [Header("Mix (owner-tunable, 0..1; layers multiply into the master)")]
        [Tooltip("Master volume of the whole rod-fight sound layer.")]
        [SerializeField, Range(0f, 1f)] private float _fishingVolume = 1f;
        [Tooltip("The wind-back rod creak (deepens as the rod loads).")]
        [SerializeField, Range(0f, 1f)] private float _creakLevel = 0.7f;
        [Tooltip("The sinking reel pay-out tick (its SPEED slows as the rig nears bottom).")]
        [SerializeField, Range(0f, 1f)] private float _payoutLevel = 0.6f;
        [Tooltip("The line-strain groan — the continuous 'ease off!' voice riding tension.")]
        [SerializeField, Range(0f, 1f)] private float _strainLevel = 0.9f;
        [Tooltip("Reel clicks while you are actually gaining line (Landing01 rising).")]
        [SerializeField, Range(0f, 1f)] private float _reelLevel = 0.8f;
        [Tooltip("The surface thrash/splash churn once she's up (FightSurface).")]
        [SerializeField, Range(0f, 1f)] private float _thrashLevel = 0.9f;
        [Tooltip("One-shot cues: whoosh, splash, bite tells, bottom settle, slack release, snap, landed.")]
        [SerializeField, Range(0f, 1f)] private float _cueLevel = 1f;

        [Header("Clips (placeholders generated if empty; slot owner SFX here later)")]
        [SerializeField] private AudioClip _rodCreakLoop;      // WindBack draw
        [SerializeField] private AudioClip _payoutTickLoop;    // Sinking pay-out
        [SerializeField] private AudioClip _strainGroanLoop;   // fight tension voice
        [SerializeField] private AudioClip _reelClickLoop;     // gaining line
        [SerializeField] private AudioClip _surfaceThrashLoop; // FightSurface churn
        [SerializeField] private AudioClip _castWhoosh;
        [SerializeField] private AudioClip _splashDown;
        [SerializeField] private AudioClip _bobberPlop;
        [SerializeField] private AudioClip _rodKnock;
        [SerializeField] private AudioClip _bottomSettle;
        [SerializeField] private AudioClip _slackRelease;
        [SerializeField] private AudioClip _snapSting;
        [SerializeField] private AudioClip _landedFlourish;

        // ---- runtime sources (built once, pooled for the app's life) ------------------------
        private AudioSource _creak;
        private AudioSource _payout;
        private AudioSource _strain;
        private AudioSource _reel;
        private AudioSource _thrash;
        private AudioSource _cue;    // one-shots (PlayOneShot — overlapping cues mix on one source)

        // ---- state --------------------------------------------------------------------------
        private FishingState _prev = FishingState.Idle;
        private float _lastEventTime;
        private FishingAudioMix _target;   // set by each snapshot (pure logic)
        private FishingAudioMix _current;  // smoothed toward the target every frame
        private bool _subscribed;

        // ---- lifecycle ----------------------------------------------------------------------

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            BuildSources();
        }

        private void OnEnable()  => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() { Unsubscribe(); if (_instance == this) _instance = null; }

        private void Subscribe()
        {
            if (_subscribed) return;
            EventBus.Subscribe<FishingStateChanged>(OnFishingState);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            EventBus.Unsubscribe<FishingStateChanged>(OnFishingState);
            _subscribed = false;
        }

        // ---- the event drives the target; Update smooths and applies (no allocation) --------

        private void OnFishingState(FishingStateChanged e)
        {
            FishingState next = e.State;
            float now = Time.unscaledTime;
            float dt = now - _lastEventTime;

            PlayCue(FishingAudioLogic.PhaseCue(_prev.Phase, in next));
            PlayCue(FishingAudioLogic.SlackCue(in _prev, in next));

            float landingPerSec = FishingAudioLogic.LandingPerSec(_prev.Landing01, next.Landing01, dt);
            float dart = FishingAudioLogic.DartSpeed01(
                next.FishOffsetX - _prev.FishOffsetX, next.FishOffsetY - _prev.FishOffsetY, dt);
            _target = FishingAudioLogic.MixFor(in next, landingPerSec, dart);

            _prev = next;
            _lastEventTime = now;
        }

        private void Update()
        {
            float step = FishingAudioLogic.MixSmoothingPerSec * Time.unscaledDeltaTime;

            // Chase the target so every layer is CONTINUOUS (the groan glides, never steps) and a
            // fight that ends (or an event stream that stops) fades cleanly to silence.
            _current.CreakGain   = Mathf.MoveTowards(_current.CreakGain,   _target.CreakGain,   step);
            _current.PayoutGain  = Mathf.MoveTowards(_current.PayoutGain,  _target.PayoutGain,  step);
            _current.PayoutPitch = Mathf.MoveTowards(_current.PayoutPitch, _target.PayoutPitch, step);
            _current.StrainGain  = Mathf.MoveTowards(_current.StrainGain,  _target.StrainGain,  step);
            _current.StrainPitch = Mathf.MoveTowards(_current.StrainPitch, _target.StrainPitch, step);
            _current.ReelGain    = Mathf.MoveTowards(_current.ReelGain,    _target.ReelGain,    step);
            _current.ThrashGain  = Mathf.MoveTowards(_current.ThrashGain,  _target.ThrashGain,  step);
            _current.ThrashPan   = Mathf.MoveTowards(_current.ThrashPan,   _target.ThrashPan,   step);

            float master = _fishingVolume;
            if (_creak  != null) _creak.volume  = master * _creakLevel  * _current.CreakGain;
            if (_payout != null)
            {
                _payout.volume = master * _payoutLevel * _current.PayoutGain;
                _payout.pitch  = _current.PayoutPitch;
            }
            if (_strain != null)
            {
                _strain.volume = master * _strainLevel * _current.StrainGain;
                _strain.pitch  = _current.StrainPitch;
            }
            if (_reel != null) _reel.volume = master * _reelLevel * _current.ReelGain;
            if (_thrash != null)
            {
                _thrash.volume    = master * _thrashLevel * _current.ThrashGain;
                _thrash.panStereo = _current.ThrashPan;
            }
            if (_cue != null) _cue.volume = master * _cueLevel;
        }

        private void PlayCue(FishingCue cue)
        {
            if (_cue == null || cue == FishingCue.None) return;
            AudioClip clip = cue switch
            {
                FishingCue.CastWhoosh     => _castWhoosh,
                FishingCue.SplashDown     => _splashDown,
                FishingCue.BobberPlop     => _bobberPlop,
                FishingCue.RodKnock       => _rodKnock,
                FishingCue.BottomSettle   => _bottomSettle,
                FishingCue.SlackRelease   => _slackRelease,
                FishingCue.SnapSting      => _snapSting,
                FishingCue.LandedFlourish => _landedFlourish,
                _                         => null,
            };
            if (clip != null) _cue.PlayOneShot(clip);
        }

        // ---- construction -------------------------------------------------------------------

        private void BuildSources()
        {
            EnsurePlaceholderClips();
            // Loops idle at zero volume from boot and are gain-faded in — no Play/Stop pops.
            _creak  = MakeSource("RodCreak",      _rodCreakLoop,      loop: true);
            _payout = MakeSource("PayoutTick",    _payoutTickLoop,    loop: true);
            _strain = MakeSource("StrainGroan",   _strainGroanLoop,   loop: true);
            _reel   = MakeSource("ReelClicks",    _reelClickLoop,     loop: true);
            _thrash = MakeSource("SurfaceThrash", _surfaceThrashLoop, loop: true);
            _cue    = MakeSource("FishingCue",    null,               loop: false);
            _current.PayoutPitch = 1f;
            _current.StrainPitch = 1f;
            _target = FishingAudioLogic.MixFor(FishingState.Idle, 0f, 0f);
        }

        private AudioSource MakeSource(string name, AudioClip clip, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = loop;
            src.playOnAwake = false;
            src.spatialBlend = 0f;  // 2D — the fight is the player's own rod; the thrash pans, not attenuates
            src.volume = 0f;        // Update sets the real level each frame
            if (loop && clip != null) src.Play();
            return src;
        }

        private void EnsurePlaceholderClips()
        {
            // Owner SFX slot into the serialized refs; until then, procedural placeholders keep the
            // whole fight audible end-to-end (AUDIO-MANIFEST.md lists the real set).
            if (_rodCreakLoop      == null) _rodCreakLoop      = ProceduralAudio.RodCreak();
            if (_payoutTickLoop    == null) _payoutTickLoop    = ProceduralAudio.PayoutTick();
            if (_strainGroanLoop   == null) _strainGroanLoop   = ProceduralAudio.StrainGroan();
            if (_reelClickLoop     == null) _reelClickLoop     = ProceduralAudio.ReelClicks();
            if (_surfaceThrashLoop == null) _surfaceThrashLoop = ProceduralAudio.SurfaceThrash();
            if (_castWhoosh        == null) _castWhoosh        = ProceduralAudio.CastWhoosh();
            if (_splashDown        == null) _splashDown        = ProceduralAudio.SplashDown();
            if (_bobberPlop        == null) _bobberPlop        = ProceduralAudio.BobberPlop();
            if (_rodKnock          == null) _rodKnock          = ProceduralAudio.RodKnock();
            if (_bottomSettle      == null) _bottomSettle      = ProceduralAudio.BottomSettle();
            if (_slackRelease      == null) _slackRelease      = ProceduralAudio.SlackRelease();
            if (_snapSting         == null) _snapSting         = ProceduralAudio.SnapSting();
            if (_landedFlourish    == null) _landedFlourish    = ProceduralAudio.LandedFlourish();
        }
    }
}
