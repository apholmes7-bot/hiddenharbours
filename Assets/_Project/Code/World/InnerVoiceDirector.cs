using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>THE PLAYER'S OWN VOICE</b> — the half of the owner's 2026-09-06 ruling that points the ambient
    /// bubble back at her: <i>"these speech bubbles can also be used by the player to narrate their
    /// internal dialogue, such as approaching a broken item and saying 'I could fix this' or other clues
    /// to the player"</i>.
    ///
    /// <para><b>It reads content and publishes a signal. That is all it does.</b> Every line is an
    /// <see cref="InnerVoiceLineDef"/> asset (rule 2) gathered by <see cref="InnerVoiceLibrary"/>; the
    /// only thing this class decides is WHEN. It never draws, never gates, never holds the player, and
    /// never touches the interact key — a thought is not an interaction, and the bubble that carries it
    /// closes itself (<see cref="AmbientSpeechPresenter"/>).</para>
    ///
    /// <para><b>The two triggers, and why these two first.</b>
    /// <see cref="InnerVoiceTrigger.InteractOffered"/> rides <see cref="InteractOfferChanged"/>, which is
    /// already computed every frame for the interact popup and already knows the difference between
    /// walking past a rod and being close enough to pick it up — so "approaching a thing" costs one
    /// string compare and needs no second proximity system. <see cref="InnerVoiceTrigger.Proximity"/>
    /// covers the things there is nothing to press on, which is most of the scenery worth a clue.</para>
    ///
    /// <para><b>The triggers deliberately NOT here.</b> <c>ArrivalCompleted</c> already speaks — through
    /// <see cref="ArrivalLineSpeaker"/> and the modal bubble — and a second listener would put two
    /// bubbles on one moment. <c>FishCaught</c> is a good clue channel and is left to the lane that owns
    /// the catch, with no line authored against it here: an untested branch with no content behind it is
    /// worse than an honest gap.</para>
    ///
    /// <para><b>Self-installing (the <c>DayNightController</c> idiom).</b> One hidden persistent host, so
    /// the clues work in every region with no scene wiring — which matters here, because the region this
    /// PR's first clue lives in cannot be rebuilt (its builder's only entry point wipes the
    /// hand-authored layer).</para>
    ///
    /// <para><b>Rule 4 clean.</b> Reads <see cref="GameServices.PlayerTransform"/>,
    /// <see cref="GameServices.CurrentRegionId"/> and <see cref="GameServices.Save"/>; subscribes
    /// <see cref="InteractOfferChanged"/>; publishes <see cref="AmbientSpeechRequested"/>. No feature
    /// module is named.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InnerVoiceDirector : MonoBehaviour
    {
        /// <summary>How often the proximity lines are measured, in seconds. A thought is not a physics
        /// query: five times a second is imperceptible against a walking speed and costs a handful of
        /// distance compares (rule 7, the slow tick).</summary>
        public const float ProximityTickSeconds = 0.2f;

        /// <summary>Where an inner-voice bubble hangs — the same head height an un-anchored
        /// <see cref="Interactable"/> uses, so her thought sits where anyone else's words would.</summary>
        public const float BubbleHeightMetres = Interactable.DefaultBubbleHeightMetres;

        private static bool _installed;

        private InnerVoiceLibrary _library;
        private readonly List<InnerVoiceLineDef> _offerLines = new List<InnerVoiceLineDef>();
        private readonly List<InnerVoiceLineDef> _proximityLines = new List<InnerVoiceLineDef>();

        /// <summary>Repeatable lines only: when each last fired, on <see cref="Time.unscaledTime"/>.</summary>
        private readonly Dictionary<string, float> _lastSaid = new Dictionary<string, float>(8);

        /// <summary>Proximity lines the player is currently standing inside. A line fires on the way IN
        /// and cannot fire again until she has left the wider re-arm ring — see
        /// <see cref="InnerVoiceLineDef.ReArmFactor"/>.</summary>
        private readonly HashSet<string> _inside = new HashSet<string>();

        private float _proximityClock;
        private bool _subscribed;

        /// <summary>Which region the last proximity tick measured in. A region change invalidates every
        /// "she is standing inside this circle" answer at once: positions are region-local, so a point
        /// she was inside at Nine Mile Creek means nothing off St Peters, and leaving the id in
        /// <see cref="_inside"/> would suppress a repeatable line until she happened to walk far from
        /// the same coordinates in the NEW region.</summary>
        private string _tickedIn;

        /// <summary>How many lines were loaded. Zero is a working state — no library, no clues.</summary>
        public int LineCount => _offerLines.Count + _proximityLines.Count;

        /// <summary>The last line this director actually published, or null. The read surface a test
        /// asserts on without a canvas.</summary>
        public string LastSpokenId { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("InnerVoiceDirector") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<InnerVoiceDirector>();
        }

        private void Awake() => LoadLibrary(Resources.Load<InnerVoiceLibrary>(InnerVoiceLibrary.ResourcesPath));

        /// <summary>
        /// Take a library (the boot path passes the Resources one; a fixture passes its own). Sorts the
        /// lines by trigger once, so the per-frame work is a walk over the list that can actually fire,
        /// and registers every referenced voice so the presenter can resolve the id off the Core signal.
        /// </summary>
        public void LoadLibrary(InnerVoiceLibrary library)
        {
            _library = library;
            _offerLines.Clear();
            _proximityLines.Clear();
            _inside.Clear();
            if (library == null || library.Lines == null) return;

            foreach (InnerVoiceLineDef line in library.Lines)
            {
                if (line == null || string.IsNullOrWhiteSpace(line.Id) ||
                    string.IsNullOrWhiteSpace(line.Line)) continue;

                DialogueVoiceCatalog.Register(line.Voice);

                if (line.Trigger == InnerVoiceTrigger.Proximity) _proximityLines.Add(line);
                else _offerLines.Add(line);
            }
        }

        private void OnEnable()
        {
            if (_subscribed) return;
            EventBus.Subscribe<InteractOfferChanged>(OnOfferChanged);
            _subscribed = true;
        }

        private void OnDisable()
        {
            if (_subscribed) EventBus.Unsubscribe<InteractOfferChanged>(OnOfferChanged);
            _subscribed = false;
        }

        // ---- triggers -------------------------------------------------------------------------

        private void OnOfferChanged(InteractOfferChanged offer)
        {
            if (!offer.Has) return;
            for (int i = 0; i < _offerLines.Count; i++)
                if (_offerLines[i].WantsOffer(offer.Id)) { TrySpeak(_offerLines[i]); return; }
        }

        private void Update()
        {
            if (_proximityLines.Count == 0) return;

            _proximityClock -= Time.unscaledDeltaTime;
            if (_proximityClock > 0f) return;
            _proximityClock = ProximityTickSeconds;

            // ⚠ Explicit != null: a destroyed transform is fake-null and ?. sails past Unity's
            // overloaded ==, so a region reload would leave this measuring against a corpse.
            Transform player = GameServices.PlayerTransform;
            if (player == null) return;

            Vector2 at = player.position;
            string region = GameServices.CurrentRegionId;

            if (!string.Equals(region, _tickedIn, System.StringComparison.Ordinal))
            {
                _inside.Clear();
                _tickedIn = region;
            }

            for (int i = 0; i < _proximityLines.Count; i++)
            {
                InnerVoiceLineDef line = _proximityLines[i];
                bool inside = line.CoversPoint(at, region);

                if (inside)
                {
                    // Fires on the way IN only, so walking around inside the circle is one thought.
                    if (_inside.Add(line.Id)) TrySpeak(line);
                }
                else if (_inside.Contains(line.Id) && line.HasLeftFor(at))
                {
                    _inside.Remove(line.Id);
                }
            }
        }

        // ---- speaking -------------------------------------------------------------------------

        /// <summary>
        /// Publish the thought, unless she has already had it.
        ///
        /// <para>Three refusals, in order of cost: a modal is up (a thought over an open conversation is
        /// noise, and the modal owns the screen); she has heard a once-only line before (the save
        /// remembers, owner Q4); a repeatable line is still cooling down.</para>
        /// </summary>
        private bool TrySpeak(InnerVoiceLineDef line)
        {
            if (line == null || string.IsNullOrWhiteSpace(line.Line)) return false;

            // A conversation owns the screen while it is up. Not a gate WE set — a gate we respect.
            if (InteractionGate.IsBlocked) return false;

            ISaveService save = GameServices.Save;
            string flag = line.SaveFlagKey;
            bool remembered = line.OnceOnly && save != null && !string.IsNullOrEmpty(flag);

            if (line.OnceOnly)
            {
                // With a save service this is the player's history and survives a quit. Without one (a
                // fixture, the shell before a game is loaded) it degrades to session-only memory rather
                // than to a line that repeats forever.
                if (remembered ? save.GetFlag(flag) : _lastSaid.ContainsKey(line.Id)) return false;
            }
            else if (_lastSaid.TryGetValue(line.Id, out float last) &&
                     Time.unscaledTime - last < line.CooldownSeconds)
            {
                return false;
            }

            _lastSaid[line.Id] = Time.unscaledTime;
            if (remembered) save.SetFlag(flag, true);

            // ⚠ The ternary is not redundant: a DESTROYED Transform is fake-null, and handing that to a
            // struct field would store a reference that every plain null check downstream sails past.
            // This launders it into a real null (the GameServices.PlayerTransform lesson).
            Transform player = GameServices.PlayerTransform;
            EventBus.Publish(new AmbientSpeechRequested(
                AmbientSpeechId.Next(),
                player != null ? player : null,
                new Vector3(0f, BubbleHeightMetres, 0f),
                speakerId: null,                 // her own thought has no NpcDef behind it
                speakerName: null,               // and no name chip: see AmbientSpeechPresenter
                line: line.Line,
                voiceId: line.VoiceId,
                kind: AmbientSpeechKind.InnerVoice));

            LastSpokenId = line.Id;
            return true;
        }

        /// <summary>Speak a line by id now, bypassing nothing — the same refusals apply. The seam a
        /// PlayMode fixture and a dev menu use instead of walking the player into a circle.</summary>
        public bool SpeakNow(string lineId)
        {
            for (int i = 0; i < _offerLines.Count; i++)
                if (_offerLines[i].Id == lineId) return TrySpeak(_offerLines[i]);
            for (int i = 0; i < _proximityLines.Count; i++)
                if (_proximityLines[i].Id == lineId) return TrySpeak(_proximityLines[i]);
            return false;
        }

        /// <summary>Forget this session's cooldowns and which circles she is standing in. Does NOT clear
        /// the once-only save flags — those are the player's history, not this object's state.</summary>
        public void ForgetSessionState()
        {
            _lastSaid.Clear();
            _inside.Clear();
            _tickedIn = null;
            LastSpokenId = null;
        }
    }
}
