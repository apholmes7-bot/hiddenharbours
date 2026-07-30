using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The versioned save system (VS-08). A self-installing, persistent service: it bootstraps itself
    /// before the first scene loads (no scene wiring needed), restores the save on launch, keeps the
    /// in-memory <see cref="SaveData"/> current, and autosaves on app suspend/quit
    /// (tech-architecture.md §2, §6).
    ///
    /// <para><b>What it captures, and how — staying in lane.</b> Scalars it can pull on demand through
    /// existing Core seams at save time: money (<see cref="IWallet"/>), gameTime + dayIndex
    /// (<see cref="IGameClock"/>), worldSeed (<see cref="IEnvironmentService"/>). The owned fleet + active
    /// hull have no getter, so it learns them by listening to the existing <see cref="BoatPurchased"/> /
    /// <see cref="ActiveBoatChanged"/> signals (reading the active-boat seam — no new GameSignals). The
    /// onboarding flags are consolidated here off PlayerPrefs (VS-21 → VS-08); the world reads/writes them
    /// through <see cref="GameServices.Save"/>.</para>
    ///
    /// <para>Restoring the captured fleet/clock/wallet back into the live gameplay objects is the owning
    /// lanes' follow-up (e.g. OwnedFleet's <c>TODO(VS-08)</c>); they read <see cref="Current"/>. This
    /// service's job is the durable, versioned, migratable substrate.</para>
    /// </summary>
    public sealed class SaveService : MonoBehaviour, ISaveService
    {
        private static SaveService _instance;

        private string _path;

        public SaveData Current { get; private set; }

        public bool LoadedExistingSave { get; private set; }

        // ---- self-installing bootstrap ---------------------------------------------------------

        /// <summary>Create the persistent service before the first scene's objects awake, so flags are
        /// readable and the save is loaded by the time gameplay starts.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[SaveService]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SaveService>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            _path = SaveStore.DefaultPath;
            var loaded = SaveStore.Read(_path);                           // null when no save on disk yet
            LoadedExistingSave = loaded != null;
            Current = loaded ?? SaveMigration.NewGame();                  // load on launch (or new game)

            GameServices.Save = this;

            EventBus.Subscribe<BoatPurchased>(OnBoatPurchased);
            EventBus.Subscribe<ActiveBoatChanged>(OnActiveBoatChanged);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<BoatPurchased>(OnBoatPurchased);
            EventBus.Unsubscribe<ActiveBoatChanged>(OnActiveBoatChanged);

            if (ReferenceEquals(GameServices.Save, this)) GameServices.Save = null;
            if (_instance == this) _instance = null;
        }

        // ---- autosave on suspend / exit --------------------------------------------------------

        private void OnApplicationPause(bool paused)
        {
            if (paused) Save();   // backgrounded / device sleep — the mobile-safe autosave point
        }

        private void OnApplicationQuit() => Save();

        // ---- ISaveService ----------------------------------------------------------------------

        /// <summary>
        /// May a snapshot-and-write happen right now? NO while the shell is at the title (M1 §7.8):
        /// until the world is entered, the live services still hold boot defaults — clock at the authored
        /// start hour, an empty wallet — while <see cref="Current"/> holds the player's real, loaded game.
        /// Snapshotting that and writing it would quietly overwrite a real save with an empty one, and
        /// quitting from the title is a perfectly normal thing to do.
        ///
        /// <para>New Game is deliberately NOT gated by this: <see cref="BeginNewGame"/> writes its fresh
        /// blob straight to the store, because starting over is the one write that must land while the
        /// player is still standing at the title.</para>
        /// </summary>
        public static bool WritesAllowed => !ShellFlow.AtTitle;

        public void Save()
        {
            if (!WritesAllowed) return;

            if (Current == null) Current = SaveMigration.NewGame();
            SnapshotLiveState();
            SaveStore.Write(Current, _path);
        }

        /// <summary>
        /// Start over on the one slot (M1 §7.8). The in-memory blob is replaced by a fresh one and the
        /// file is overwritten NOW — a new game that only existed in memory would be undone by the next
        /// autosave-on-quit, or worse, resurrected whole on the next launch.
        ///
        /// <para>Deliberately no <see cref="SnapshotLiveState"/> on the way out: this is not a save of
        /// the session being abandoned, it is the erasure of it. The live services are then brought to
        /// the fresh blob's state by the shell's restore, not the other way round.</para>
        /// </summary>
        public void BeginNewGame()
        {
            Current = SaveMigration.NewGame();
            LoadedExistingSave = false;
            SaveStore.Write(Current, _path);
        }

        public bool GetFlag(string key)
        {
            if (string.IsNullOrEmpty(key) || Current?.OnboardingFlags == null) return false;
            for (int i = 0; i < Current.OnboardingFlags.Count; i++)
                if (Current.OnboardingFlags[i].Key == key) return Current.OnboardingFlags[i].Value;
            return false;
        }

        public void SetFlag(string key, bool value)
        {
            if (string.IsNullOrEmpty(key) || Current == null) return;
            Current.OnboardingFlags ??= new System.Collections.Generic.List<SaveFlag>();

            for (int i = 0; i < Current.OnboardingFlags.Count; i++)
            {
                if (Current.OnboardingFlags[i].Key == key)
                {
                    if (Current.OnboardingFlags[i].Value == value) return; // unchanged — skip the write
                    Current.OnboardingFlags[i] = new SaveFlag(key, value);
                    Save();
                    return;
                }
            }
            Current.OnboardingFlags.Add(new SaveFlag(key, value));
            Save();
        }

        // ---- capture from existing seams -------------------------------------------------------

        /// <summary>Pull the on-demand scalars from the live services into <see cref="Current"/>. Owned
        /// boats / active hull are maintained incrementally from signals, not snapshotted here.</summary>
        private void SnapshotLiveState()
        {
            var clock = GameServices.Clock;
            if (clock != null)
            {
                Current.GameTimeSeconds = clock.TotalSeconds;
                Current.DayIndex = clock.DayIndex;
            }

            if (GameServices.Environment != null) Current.WorldSeed = GameServices.Environment.WorldSeed;
            if (GameServices.Wallet != null) Current.Money = GameServices.Wallet.Money;
        }

        private void OnBoatPurchased(BoatPurchased e) => RecordActiveBoat(e.BoatId);

        private void OnActiveBoatChanged(ActiveBoatChanged e) => RecordActiveBoat(e.BoatId);

        /// <summary>Mark <paramref name="hullId"/> owned + active and persist. Idempotent on the list.</summary>
        private void RecordActiveBoat(string hullId)
        {
            if (string.IsNullOrEmpty(hullId) || Current == null) return;
            Current.OwnedBoats ??= new System.Collections.Generic.List<string>();
            if (!Current.OwnedBoats.Contains(hullId)) Current.OwnedBoats.Add(hullId);
            Current.ActiveHullId = hullId;
            Save();
        }
    }
}
