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
    ///   <item>a propulsion-aware BOAT BED while aboard — the hand-rowed dory gets an oar-stroke/water
    ///   bed, an engine boat gets a looping outboard bed; the two crossfade on a swap, and the engine
    ///   rides the boat's speed over ground,</item>
    ///   <item>a CATCH STING on <see cref="FishCaught"/>,</item>
    ///   <item>"made it home" WARMTH on <see cref="CatchSold"/>, and on coming ashore — but the ashore
    ///   exhale is EARNED: it fires only when the sea had become a worry that trip (the rising-wind tell
    ///   peaked past a small threshold), so a flat-calm hop ends quietly (P5 "warmth is earned"),</item>
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
        [Tooltip("Loudness of the aboard oar-stroke/water bed (the rowed dory) relative to the ambience bus.")]
        [SerializeField, Range(0f, 1f)] private float _oarLayerGain = 0.8f;
        [Tooltip("Loudness of the aboard outboard-engine bed (engine boats) relative to the ambience bus.")]
        [SerializeField, Range(0f, 1f)] private float _engineLayerGain = 0.8f;

        [Header("Clips (placeholders generated if empty; slot owner SFX here later)")]
        [SerializeField] private AudioClip _calmBed;
        [SerializeField] private AudioClip _gulls;
        [SerializeField] private AudioClip _hullRow;          // oar-stroke / water bed (rowed dory)
        [SerializeField] private AudioClip _outboardEngine;   // looping outboard-engine bed (engine boats)
        [SerializeField] private AudioClip _windTell;
        [SerializeField] private AudioClip _catchSting;
        [SerializeField] private AudioClip _homeWarmth;

        // ---- runtime sources ----------------------------------------------------------------
        private AudioSource _bed;     // calm-sea bed (ambience)
        private AudioSource _gull;    // gull layer (ambience)
        private AudioSource _oar;     // oar-stroke/water bed (ambience, aboard a rowed hull)
        private AudioSource _engine;  // outboard-engine bed (ambience, aboard an engine hull)
        private AudioSource _tell;    // rising-wind tell (ambience, wind-driven)
        private AudioSource _cue;     // one-shot stings/warmth (sfx)
        private AudioSource _music;   // music bus (reserved — no stem yet; volume + duck are live)

        // ---- state --------------------------------------------------------------------------
        private ControlMode _mode = ControlMode.OnFoot;
        private string _boatId;       // last active hull id (from ActiveBoatChanged) — picks the boat bed
        private float _oarLevel;      // 0..1 crossfade level for the oar bed
        private float _engineLevel;   // 0..1 crossfade level for the engine bed
        private float _engineThrottle01; // 0..1 from the active boat's speed over ground (4 Hz poll)
        private bool _tellActive;
        private float _tell01;
        private float _peakTellThisTrip; // 0..1 high-water mark of the wind tell since boarding — gates the home-exhale
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
            EventBus.Subscribe<ActiveBoatChanged>(OnActiveBoatChanged);
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            EventBus.Unsubscribe<FishCaught>(OnFishCaught);
            EventBus.Unsubscribe<CatchSold>(OnCatchSold);
            EventBus.Unsubscribe<ControlModeChanged>(OnControlModeChanged);
            EventBus.Unsubscribe<ActiveBoatChanged>(OnActiveBoatChanged);
            _subscribed = false;
        }

        // ---- per-frame (allocation-free) ----------------------------------------------------

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // The cue duck recovers smoothly (event-set, frame-decayed).
            if (_duck > 0f)
                _duck = Mathf.MoveTowards(_duck, 0f, AudioDirectorLogic.DuckRecoveryPerSec * dt);

            // Crossfade the aboard propulsion beds toward the active layer (smooth swap, no pop).
            BoatAudioLayer layer = AudioDirectorLogic.BoatLayerFor(_mode, _boatId);
            float rate = AudioDirectorLogic.BoatLayerCrossfadePerSec * dt;
            _oarLevel    = Mathf.MoveTowards(_oarLevel,    layer == BoatAudioLayer.Oars   ? 1f : 0f, rate);
            _engineLevel = Mathf.MoveTowards(_engineLevel, layer == BoatAudioLayer.Engine ? 1f : 0f, rate);

            // Poll the sea's wind AND the boat's speed at a low cadence (the HUD's 4 Hz) — not per frame.
            _envTimer -= dt;
            if (_envTimer <= 0f)
            {
                _envTimer = _envSampleHz > 0f ? 1f / _envSampleHz : 0.25f;
                SampleWind();
                SampleBoat();
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

            // High-water mark of the tell while ABOARD — it's how worrying the sea got this trip, which
            // decides whether coming ashore earns the home-exhale (reset on boarding).
            if (_mode == ControlMode.Aboard && _tell01 > _peakTellThisTrip)
                _peakTellThisTrip = _tell01;
        }

        // The engine bed is speed-reactive: read the active boat's course-over-ground speed through the
        // Core seam (ADR 0007), so the outboard idles when moored/slow and revs underway. Null/ashore = idle.
        private void SampleBoat()
        {
            var boat = GameServices.ActiveBoat;
            float sog = (boat != null && boat.HasActiveBoat) ? boat.Sample().SpeedOverGround : 0f;
            _engineThrottle01 = AudioDirectorLogic.EngineThrottle01(sog);
        }

        private void ApplyMix()
        {
            float amb = _ambienceVolume * _masterVolume;
            float ambDucked = AudioDirectorLogic.DuckedGain(amb, _duck);
            float bedGain = AudioDirectorLogic.CalmBedGain(_tell01);

            if (_bed  != null) _bed.volume  = ambDucked * bedGain;
            if (_gull != null) _gull.volume = ambDucked * bedGain * 0.7f;

            // Aboard propulsion beds ride the ambience bus and are NOT ducked (you're on the water);
            // they crossfade by level, and the engine swells + lifts pitch with speed over ground.
            if (_oar != null) _oar.volume = amb * _oarLayerGain * _oarLevel;
            if (_engine != null)
            {
                _engine.volume = AudioDirectorLogic.EngineGain(amb * _engineLayerGain, _engineThrottle01) * _engineLevel;
                _engine.pitch  = AudioDirectorLogic.EnginePitch(_engineThrottle01);
            }

            if (_tell  != null) _tell.volume  = amb * _tell01;                       // the warning itself rises
            if (_cue   != null) _cue.volume   = _sfxVolume * _masterVolume;          // one-shots at sfx volume
            if (_music != null) _music.volume = AudioDirectorLogic.DuckedGain(_musicVolume * _masterVolume, _duck); // bus is live; ducks under cues
        }

        // ---- events -------------------------------------------------------------------------

        private void OnFishCaught(FishCaught e) => PlayCue(AudioDirectorLogic.CueFor(AudioMoment.FishLanded));
        private void OnCatchSold(CatchSold e)   => PlayCue(AudioDirectorLogic.CueFor(AudioMoment.CatchSold));

        private void OnControlModeChanged(ControlModeChanged e)
        {
            bool wasAboard = _mode == ControlMode.Aboard;
            _mode = e.Mode;

            if (_mode == ControlMode.Aboard)
            {
                // Boarding starts a fresh trip — the home-exhale is judged on THIS trip's worst sea.
                if (!wasAboard) _peakTellThisTrip = 0f;
            }
            else if (wasAboard)
            {
                // Coming ashore = "made it home" — but only EARNED if the sea had become a worry this
                // trip (a flat-calm hop ends quietly; charter guardrail "warmth is earned, not constant").
                if (AudioDirectorLogic.HomeWarmthOnAshore(_peakTellThisTrip))
                    PlayCue(AudioDirectorLogic.CueFor(AudioMoment.CameAshore));
                _peakTellThisTrip = 0f;
            }
        }

        // The active hull changed (boarded / upgrade swap): remember its id so the boat bed picks
        // oar vs engine. Carries no propulsion type (that lives in Boats); see AudioDirectorLogic.
        private void OnActiveBoatChanged(ActiveBoatChanged e) => _boatId = e.BoatId;

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
            _bed    = MakeSource("Bed",      _calmBed,        loop: true,  play: true);
            _gull   = MakeSource("Gulls",    _gulls,          loop: true,  play: true);
            // Both propulsion beds loop from boot at zero volume and crossfade in — no Play/Stop pops on a swap.
            _oar    = MakeSource("OarRow",   _hullRow,        loop: true,  play: true);
            _engine = MakeSource("Outboard", _outboardEngine, loop: true,  play: true);
            _tell   = MakeSource("WindTell", _windTell,       loop: true,  play: true);
            _cue    = MakeSource("Cue",      null,            loop: false, play: false);
            _music  = MakeSource("Music",    null,            loop: true,  play: false); // bus ready; stem slots in later
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
            if (_calmBed        == null) _calmBed        = ProceduralAudio.CalmSeaBed();
            if (_gulls          == null) _gulls          = ProceduralAudio.GullCalls();
            if (_hullRow        == null) _hullRow        = ProceduralAudio.HullRow();
            if (_outboardEngine == null) _outboardEngine = ProceduralAudio.OutboardEngine();
            if (_windTell       == null) _windTell       = ProceduralAudio.WindTell();
            if (_catchSting     == null) _catchSting     = ProceduralAudio.CatchSting();
            if (_homeWarmth     == null) _homeWarmth     = ProceduralAudio.HomeWarmth();
        }
    }
}
