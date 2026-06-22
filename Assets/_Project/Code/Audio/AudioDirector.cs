using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Audio
{
    /// <summary>
    /// The adaptive audio director (VS-27/28 scaffold). SELF-INSTALLING via
    /// <see cref="RuntimeInitializeOnLoadMethodAttribute"/> (no GreyboxBuilder wiring): it bootstraps a
    /// persistent GameObject before the first scene's gameplay, builds its mix buses, subscribes to the
    /// EXISTING Core signals, and adapts the soundscape:
    /// <list type="bullet">
    ///   <item>a calm-sea + gull AMBIENT BED (always),</item>
    ///   <item>a hull-slap/row LAYER while aboard,</item>
    ///   <item>a CATCH STING on <see cref="FishCaught"/>,</item>
    ///   <item>"made it home" WARMTH on <see cref="CatchSold"/> / coming ashore,</item>
    ///   <item>the SACRED rising-wind TELL — as the <see cref="IEnvironmentService"/> wind strengthens
    ///   the bed thins and a cue rises, audible BEFORE the sea turns dangerous (Pillar 1).</item>
    /// </list>
    /// Independent ambience/SFX/music volumes; cues duck the beds; nothing allocates per frame. Real
    /// owner SFX slot into the serialized clip refs later — placeholders are generated procedurally
    /// (see <c>Assets/_Project/Audio/AUDIO-MANIFEST.md</c>). All the decisions live in the pure,
    /// EditMode-tested <see cref="AudioDirectorLogic"/>; this class is just the player.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioDirector : MonoBehaviour
    {
        private static AudioDirector _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[AudioDirector]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AudioDirector>();
        }

        // ---- player-set mix (independent buses) ---------------------------------------------
        [Header("Mix (independent bus volumes, 0..1)")]
        [SerializeField, Range(0f, 1f)] private float _masterVolume   = 1f;
        [SerializeField, Range(0f, 1f)] private float _ambienceVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _sfxVolume      = 1f;
        [SerializeField, Range(0f, 1f)] private float _musicVolume    = 0.6f;

        [Header("Tuning")]
        [Tooltip("Wind/environment poll cadence (Hz). Matches the HUD's 4 Hz sampling — not per frame.")]
        [SerializeField] private float _envSampleHz = 4f;
        [Tooltip("Loudness of the at-rest hull-slap/row layer relative to the ambience bus.")]
        [SerializeField, Range(0f, 1f)] private float _hullLayerGain = 0.8f;

        [Header("Clips (placeholders generated if empty; slot owner SFX here later)")]
        [SerializeField] private AudioClip _calmBed;
        [SerializeField] private AudioClip _gulls;
        [SerializeField] private AudioClip _hullRow;
        [SerializeField] private AudioClip _windTell;
        [SerializeField] private AudioClip _catchSting;
        [SerializeField] private AudioClip _homeWarmth;

        // ---- runtime sources ----------------------------------------------------------------
        private AudioSource _bed;     // calm-sea bed (ambience)
        private AudioSource _gull;    // gull layer (ambience)
        private AudioSource _hull;    // hull-slap/row (ambience, aboard only)
        private AudioSource _tell;    // rising-wind tell (ambience, wind-driven)
        private AudioSource _cue;     // one-shot stings/warmth (sfx)

        // ---- state --------------------------------------------------------------------------
        private bool _aboard;
        private bool _tellActive;
        private float _tell01;
        private float _duck;          // 0..1, set by a cue, decays per frame
        private float _envTimer;
        private bool _subscribed;

        // ---- lifecycle ----------------------------------------------------------------------

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            BuildSources();
            ApplyMix();
        }

        private void OnEnable()  => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() { Unsubscribe(); if (_instance == this) _instance = null; }

        private void Subscribe()
        {
            if (_subscribed) return;
            EventBus.Subscribe<FishCaught>(OnFishCaught);
            EventBus.Subscribe<CatchSold>(OnCatchSold);
            EventBus.Subscribe<ControlModeChanged>(OnControlModeChanged);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            EventBus.Unsubscribe<FishCaught>(OnFishCaught);
            EventBus.Unsubscribe<CatchSold>(OnCatchSold);
            EventBus.Unsubscribe<ControlModeChanged>(OnControlModeChanged);
            _subscribed = false;
        }

        // ---- per-frame (allocation-free) ----------------------------------------------------

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // The cue duck recovers smoothly (event-set, frame-decayed).
            if (_duck > 0f)
                _duck = Mathf.MoveTowards(_duck, 0f, AudioDirectorLogic.DuckRecoveryPerSec * dt);

            // Poll the sea's wind at a low cadence and drive the rising-wind tell (P1).
            _envTimer -= dt;
            if (_envTimer <= 0f)
            {
                _envTimer = _envSampleHz > 0f ? 1f / _envSampleHz : 0.25f;
                SampleWind();
            }

            ApplyMix();
        }

        private void SampleWind()
        {
            var env = GameServices.Environment;
            if (env == null) { _tell01 = 0f; _tellActive = false; return; }

            float wind = env.Sample().WindVector.magnitude;
            _tellActive = AudioDirectorLogic.TellActive(wind, _tellActive);
            _tell01 = _tellActive ? AudioDirectorLogic.WindTell01(wind) : 0f;
        }

        private void ApplyMix()
        {
            float amb = _ambienceVolume * _masterVolume;
            float ambDucked = AudioDirectorLogic.DuckedGain(amb, _duck);
            float bedGain = AudioDirectorLogic.CalmBedGain(_tell01);

            if (_bed  != null) _bed.volume  = ambDucked * bedGain;
            if (_gull != null) _gull.volume = ambDucked * bedGain * 0.7f;
            if (_hull != null) _hull.volume = _aboard ? amb * _hullLayerGain : 0f; // being on the water — not ducked
            if (_tell != null) _tell.volume = amb * _tell01;                       // the warning itself rises
            if (_cue  != null) _cue.volume  = _sfxVolume * _masterVolume;          // one-shots at sfx volume
        }

        // ---- events -------------------------------------------------------------------------

        private void OnFishCaught(FishCaught e) => PlayCue(AudioDirectorLogic.CueFor(AudioMoment.FishLanded));
        private void OnCatchSold(CatchSold e)   => PlayCue(AudioDirectorLogic.CueFor(AudioMoment.CatchSold));

        private void OnControlModeChanged(ControlModeChanged e)
        {
            bool wasAboard = _aboard;
            _aboard = AudioDirectorLogic.HullLayerActive(e.Mode);

            if (_hull != null && _aboard && !_hull.isPlaying) _hull.Play();

            // Coming ashore after being out at sea = "made it home".
            if (wasAboard && !_aboard) PlayCue(AudioDirectorLogic.CueFor(AudioMoment.CameAshore));
        }

        private void PlayCue(AudioCue cue)
        {
            if (_cue == null) return;
            AudioClip clip = cue switch
            {
                AudioCue.CatchSting => _catchSting,
                AudioCue.HomeWarmth => _homeWarmth,
                _                   => null,
            };
            if (clip == null) return;
            _cue.PlayOneShot(clip);
            _duck = 1f; // duck the beds under the cue
        }

        // ---- construction -------------------------------------------------------------------

        private void BuildSources()
        {
            EnsurePlaceholderClips();
            _bed  = MakeSource("Bed",      _calmBed,  loop: true,  play: true);
            _gull = MakeSource("Gulls",    _gulls,    loop: true,  play: true);
            _hull = MakeSource("HullRow",  _hullRow,  loop: true,  play: false);
            _tell = MakeSource("WindTell", _windTell, loop: true,  play: true);
            _cue  = MakeSource("Cue",      null,      loop: false, play: false);
        }

        private AudioSource MakeSource(string name, AudioClip clip, bool loop, bool play)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = loop;
            src.playOnAwake = false;
            src.spatialBlend = 0f;  // 2D beds/cues — not positional
            src.volume = 0f;        // ApplyMix sets the real level each frame
            if (play && clip != null) src.Play();
            return src;
        }

        private void EnsurePlaceholderClips()
        {
            // Owner SFX slot into the serialized refs; until then, procedural placeholders so the
            // adaptive mix is audible end-to-end (AUDIO-MANIFEST.md lists the real set).
            if (_calmBed    == null) _calmBed    = ProceduralAudio.CalmSeaBed();
            if (_gulls      == null) _gulls      = ProceduralAudio.GullCalls();
            if (_hullRow    == null) _hullRow    = ProceduralAudio.HullRow();
            if (_windTell   == null) _windTell   = ProceduralAudio.WindTell();
            if (_catchSting == null) _catchSting = ProceduralAudio.CatchSting();
            if (_homeWarmth == null) _homeWarmth = ProceduralAudio.HomeWarmth();
        }
    }
}
