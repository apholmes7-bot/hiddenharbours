using System;
using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HiddenHarbours.Economy
{
    /// <summary>How a press on the mending station came out.</summary>
    public enum DoryMendingStationResult
    {
        /// <summary>No game, no station, or a service missing: nothing happened.</summary>
        NoGame = 0,
        /// <summary>She is not his on the slip in this game (not bought, an old save, or launched).</summary>
        NotAvailable = 1,
        /// <summary>A job started or resumed; holding the key works it.</summary>
        Working = 2,
        /// <summary>The job could not start (<see cref="DoryMendingStation.LastReason"/> says why).</summary>
        Refused = 3,
        /// <summary>She went down the slip and is his boat on the water.</summary>
        Launched = 4,
        /// <summary>She is mended but the launch was refused (a guest's press: the host launches).</summary>
        LaunchRefused = 5,
    }

    /// <summary>
    /// <b>Mending and launching the dory on the slip</b>, as one interact candidate on the slip prop.
    ///
    /// <para><b>Mend: press, then hold.</b> Once she is his and still broken, the verb names the next job
    /// (data: the job's display name). The press starts the job through a
    /// <see cref="DoryMendingSession"/>, which reserves the job's supplies from the paper bag in his hand
    /// (<see cref="ShoppingBagMendingCarry"/>); while he keeps the interact key held and stays in reach,
    /// the held seconds become game minutes of work (<see cref="DoryMendingStationDef"/>), handed to the
    /// session in small ticks. Letting go, walking off or a modal coming up pauses the job; the next press
    /// resumes it. Each finished job writes its own existing saved flag, the last one puts her in the
    /// existing repaired list, and only then are the supplies spent.</para>
    ///
    /// <para><b>Launch: one press.</b> Once she is mended the verb becomes the launch. The press is the
    /// supported journey down the slip the mending sequence asks for
    /// (<see cref="DoryMendingDef.RequiresSupportedLaunch"/>): <see cref="SlipDoryStory.TryLaunch"/> clears
    /// the slip flag once, and his own dory, the boat the persistent rig already carries, is put afloat at
    /// her mooring (<see cref="IWorldWhereabouts.RestoreBoat"/>), where she rides the tide like every hull.
    /// Nothing new is saved.</para>
    ///
    /// <para><b>No single-player static.</b> The acting player's speaker and hands are parameters of
    /// <see cref="Press"/>; <see cref="Interact"/> passes the ones Core publishes. Only the host writes:
    /// a guest's launch changes nothing.</para>
    ///
    /// <para><b>No allocation per frame.</b> The verb is composed when the state changes. The update reads
    /// one key and one distance while a job is being worked, and nothing otherwise.</para>
    /// </summary>
    public sealed class DoryMendingStation : MonoBehaviour, IInteractable
    {
        [Tooltip("The station: its mending sequence, tuning, words and launch berth.")]
        [SerializeField] private DoryMendingStationDef _station;

        [Tooltip("The key held to keep working (the interact key).")]
        [SerializeField] private Key _holdKey = Key.E;

        private DoryMendingSession _session;
        private IShoppingBagHands _sessionHands;
        private Transform _speaker;
        private string _verb = string.Empty;
        private bool _available, _registered, _subscribed, _working;
        private float _held;

        /// <summary>The station data (tests and tools).</summary>
        public DoryMendingStationDef Station => _station;

        /// <summary>Why the last start or work tick was refused, or <see cref="DoryMendReason.None"/>.</summary>
        public DoryMendReason LastReason { get; private set; }

        /// <summary>The last thought he had here (tests).</summary>
        public string LastLine { get; private set; }

        /// <summary>True while a job is being worked (the key is expected to be held).</summary>
        public bool IsWorking => _working;

        /// <summary>Wire by hand (tests); the prefab sets the field in the inspector.</summary>
        public void Configure(DoryMendingStationDef station)
        {
            _station = station;
            DropSession();
            Refresh();
        }

        private void OnEnable()
        {
            if (!_subscribed)
            {
                EventBus.Subscribe<GameLoaded>(OnGameLoaded);
                EventBus.Subscribe<BoatPurchased>(OnBoatPurchased);
                EventBus.Subscribe<SlipDoryLaunched>(OnLaunched);
                _subscribed = true;
            }
            Refresh();
        }

        private void Start() => Refresh();

        private void OnDisable()
        {
            if (_subscribed)
            {
                EventBus.Unsubscribe<GameLoaded>(OnGameLoaded);
                EventBus.Unsubscribe<BoatPurchased>(OnBoatPurchased);
                EventBus.Unsubscribe<SlipDoryLaunched>(OnLaunched);
                _subscribed = false;
            }
            DropSession();
            SetRegistered(false);
        }

        // A load is a new game in the session's eyes: unfinished time is dropped, goods stay his.
        private void OnGameLoaded(GameLoaded e) { DropSession(); Refresh(); }
        private void OnBoatPurchased(BoatPurchased e) => Refresh();
        private void OnLaunched(SlipDoryLaunched e) { DropSession(); Refresh(); }

        private void Update()
        {
            if (!_working) return;
            Keyboard kb = Keyboard.current;
            bool held = kb != null && kb[_holdKey].isPressed;
            Transform player = GameServices.PlayerTransform;
            bool inReach = player != null && _station != null &&
                ((Vector2)player.position - (Vector2)transform.position).sqrMagnitude
                    <= _station.ReachMeters * _station.ReachMeters;
            TickHeld(held && !InteractionGate.IsBlocked, inReach, Time.deltaTime);
        }

        // ---- the state, read when it changes ---------------------------------------------------

        /// <summary>Re-read the save: is she his on the slip, mended or not, and what is the next job?
        /// Composes the verb and lists or delists the candidate.</summary>
        public void Refresh()
        {
            ISaveService save = GameServices.Save;
            DoryMendingDef mending = _station != null ? _station.Mending : null;
            SaveData data = save?.Current;
            _available = mending != null && data != null && SlipDoryStory.IsOnTheSlip(save)
                         && data.OwnedBoats != null && data.OwnedBoats.Contains(mending.BoatId)
                         && SlipDoryStory.HostHere;
            if (_available)
            {
                if (IsMended(data, mending.BoatId)) _verb = _station.LaunchLabel ?? string.Empty;
                else
                {
                    DoryMendingJobDef next = NextJob(mending, save);
                    _verb = next != null ? Fill(_station.MendLabel, "{job}", next.DisplayName) : string.Empty;
                }
            }
            else _verb = string.Empty;
            SetRegistered(_available && isActiveAndEnabled);
        }

        private static bool IsMended(SaveData data, string boatId)
            => data?.RepairedBoats != null && data.RepairedBoats.Contains(boatId);

        /// <summary>The first job, in order, whose saved flag is not yet set; null when all are done.</summary>
        public static DoryMendingJobDef NextJob(DoryMendingDef mending, ISaveService save)
        {
            if (mending?.Jobs == null || save == null) return null;
            DoryMendingJobDef best = null;
            foreach (DoryMendingJobDef job in mending.Jobs)
            {
                if (job == null || save.GetFlag(job.CompletedFlag)) continue;
                if (best == null || job.Order < best.Order) best = job;
            }
            return best;
        }

        private void SetRegistered(bool on)
        {
            if (on == _registered) return;
            if (on) Interactables.Register(this);
            else Interactables.Unregister(this);
            _registered = on;
        }

        // ---- the press ---------------------------------------------------------------------------

        /// <summary>
        /// The press, for the acting player: his thought bubble's anchor and his hands. Mended, it
        /// launches her; otherwise it starts or resumes the next job.
        /// </summary>
        public DoryMendingStationResult Press(Transform speaker, IShoppingBagHands hands)
        {
            _speaker = speaker;
            LastReason = DoryMendReason.None;
            ISaveService save = GameServices.Save;
            if (_station == null || _station.Mending == null || save?.Current == null)
                return DoryMendingStationResult.NoGame;
            Refresh();
            if (!_available) return DoryMendingStationResult.NotAvailable;

            if (IsMended(save.Current, _station.Mending.BoatId)) return Launch(save);

            if (!EnsureSession(save, hands)) return DoryMendingStationResult.NoGame;
            DoryMendingJobDef job = _session.NextJob;
            if (job == null)
            {
                LastReason = DoryMendReason.InvalidProgress;
                return DoryMendingStationResult.Refused;
            }
            if (!_session.TryStart(job.Id, GameServices.CurrentRegionId, out DoryMendReason why))
            {
                LastReason = why;
                if (why == DoryMendReason.SessionChanged) DropSession();
                if (why == DoryMendReason.MissingMaterials)
                    Think(Fill(_station.MissingLine, "{job}", Lower(job.DisplayName)));
                return DoryMendingStationResult.Refused;
            }
            _working = true;
            _held = 0f;
            Think(Fill(_station.WorkLine, "{job}", Lower(job.DisplayName)));
            return DoryMendingStationResult.Working;
        }

        /// <summary>
        /// One frame of the held press: <paramref name="held"/> is whether the key is still down,
        /// <paramref name="inReach"/> whether he is still at her. Public so a test drives it without
        /// input. Gathers held time and hands it to the session every <see cref="DoryMendingStationDef.WorkTickSeconds"/>.
        /// </summary>
        public void TickHeld(bool held, bool inReach, float realSeconds)
        {
            if (!_working || _session == null || _station == null) return;
            if (!held || !inReach)
            {
                _session.Pause();
                _working = false;
                _held = 0f;
                Think(_station.PausedLine);
                return;
            }
            if (!(realSeconds > 0f)) return;
            _held += realSeconds;
            if (_held < _station.WorkTickSeconds) return;
            double minutes = (double)_held * _station.GameMinutesPerHeldSecond;
            _held = 0f;

            string before = _session.Snapshot.JobId;
            if (!_session.WorkFor(minutes, GameServices.CurrentRegionId, out DoryMendReason why))
            {
                _working = false;
                LastReason = why;
                if (why == DoryMendReason.SessionChanged) DropSession();
                if (why == DoryMendReason.MissingMaterials)
                {
                    DoryMendingJobDef job = _session?.NextJob;
                    if (job != null) Think(Fill(_station.MissingLine, "{job}", Lower(job.DisplayName)));
                }
                return;
            }

            DoryMendingSnapshot now = _session.Snapshot;
            if (now.JobId == null && before != null)
            {
                // A job finished: its flag is saved. Say what comes next, or that she is sound.
                _working = false;
                ISaveService save = GameServices.Save;
                if (IsMended(save?.Current, _station.Mending.BoatId)) Think(_station.MendedLine);
                else
                {
                    DoryMendingJobDef next = _session.NextJob;
                    if (next != null) Think(Fill(_station.JobDoneLine, "{next}", Lower(next.DisplayName)));
                }
                Refresh();
            }
            else if (!now.Working) _working = false;   // the session stopped it (the tide, for a wet job)
        }

        private DoryMendingStationResult Launch(ISaveService save)
        {
            SlipDoryLaunchResult result;
            using (save.DeferWrites())
            {
                result = SlipDoryStory.TryLaunch(save, _station.Mending.BoatId, SlipDoryStory.HostHere);
                if (result == SlipDoryLaunchResult.Launched)
                    GameServices.Whereabouts?.RestoreBoat(new Whereabouts(
                        _station.LaunchRegionId, _station.LaunchBerth, _station.LaunchHeadingDegrees));
            }
            if (result != SlipDoryLaunchResult.Launched) return DoryMendingStationResult.LaunchRefused;
            Think(_station.LaunchedLine);
            return DoryMendingStationResult.Launched;
        }

        private bool EnsureSession(ISaveService save, IShoppingBagHands hands)
        {
            if (_session != null && ReferenceEquals(_sessionHands, hands)) return true;
            DropSession();
            if (GameServices.Clock == null || GameServices.Environment == null || GameServices.Config == null)
                return false;
            try
            {
                _session = new DoryMendingSession(_station.Mending, save, GameServices.Clock,
                    GameServices.Environment, new ShoppingBagMendingCarry(save, hands), GameServices.Config);
                _sessionHands = hands;
                return true;
            }
            catch (ArgumentException e)
            {
                Debug.LogException(e, this);
                _session = null;
                return false;
            }
        }

        private void DropSession()
        {
            _session?.Dispose();
            _session = null;
            _sessionHands = null;
            _working = false;
            _held = 0f;
        }

        private void Think(string line)
        {
            LastLine = line;
            if (string.IsNullOrEmpty(line) || _station == null) return;
            EventBus.Publish(new AmbientSpeechRequested(
                AmbientSpeechId.Next(), _speaker, new Vector3(0f, _station.BubbleHeightMetres, 0f),
                null, null, line, _station.VoiceId, AmbientSpeechKind.InnerVoice));
        }

        private static string Fill(string format, string key, string value)
            => string.IsNullOrEmpty(format) ? string.Empty : format.Replace(key, value ?? string.Empty);

        private static string Lower(string text)
            => string.IsNullOrEmpty(text) ? string.Empty : char.ToLowerInvariant(text[0]) + text.Substring(1);

        // ---- IInteractable -------------------------------------------------------------------------

        public string Id => _station != null ? _station.Id : string.Empty;
        public string VerbLabel => _verb;
        public Vector2 WorldPosition => transform.position;
        public float ReachMeters => _station != null ? _station.ReachMeters : 0f;
        public int Priority => InteractPriority.Fixture;
        public InteractContext Contexts => InteractContext.OnFoot;
        public bool RequiresFacing => false;
        public bool IsAvailable => _available;
        public void Interact(in InteractActor actor) => Press(GameServices.PlayerTransform, GameServices.BagHands);
    }
}
