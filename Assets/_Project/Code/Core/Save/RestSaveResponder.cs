namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The thing that actually writes when the player turns in.</b> Subscribes to
    /// <see cref="RestSaveRequested"/>, asks <see cref="GameServices.Save"/> for a write, and reports
    /// the outcome as <see cref="GameSaved"/> so a surface can say "Turned in for the night." — the
    /// manual-save entry point the game has never had.
    ///
    /// <para><b>Why this is a static installer and not another MonoBehaviour.</b> It owns no state and
    /// no frame: it is one subscription and one call. Making it static means an EditMode test installs
    /// it, puts a fake <see cref="ISaveService"/> in <see cref="GameServices.Save"/>, publishes the
    /// request and reads the outcome — with no scene, no bootstrap, and no real save file anywhere near
    /// the disk. <c>SaveService</c> owns the lifetime (<see cref="Install"/> in its Awake,
    /// <see cref="Uninstall"/> in its OnDestroy), which is the same shape as its existing
    /// <see cref="BoatPurchased"/> / <see cref="ActiveBoatChanged"/> subscriptions.</para>
    ///
    /// <para><b>Why the bed does not simply call <c>GameServices.Save.Save()</c>.</b> It could — that is
    /// a Core interface and rule 4 would be satisfied. But the request and the write are genuinely
    /// different concerns with different futures: the beat around the write (a fade, later the
    /// <c>Fisher_sleep</c> animation, later still a clock move to morning) belongs to whoever is
    /// presenting the game, not to a bed and not to the save system. Splitting them here means both
    /// halves grow without touching each other.</para>
    ///
    /// <para><b>It also records WHERE you slept</b> (ADR 0037). The request carries the spot the player
    /// is standing on and the storey they are on; this adds the region they are in and stamps all three
    /// into the blob through <see cref="RestLocker"/> immediately before the write, so a load can wake
    /// them there instead of at the authored spawn. A rest in a context with no region reported records
    /// nothing and still saves the day — see <see cref="AnchorFor"/>.</para>
    ///
    /// <para><b>Refusal is a normal outcome, not a fault.</b> A save asked for at the title is refused by
    /// the service's own write gate, and a context with no save service at all (EditMode, a bare region
    /// scene opened for review) has nothing to write to. Both answer with an <c>Ok:false</c>
    /// <see cref="GameSaved"/> carrying player-facing prose, and neither logs an error — the player
    /// asked a reasonable question and gets a reasonable answer.</para>
    /// </summary>
    public static class RestSaveResponder
    {
        /// <summary>What the player is told when the save landed. Public so the test asserts on the
        /// contract rather than on a re-typed string literal.</summary>
        public const string SavedText = "You turn in. The day is kept.";

        /// <summary>What the player is told when there is no save service to write to.</summary>
        public const string NoServiceText = "You lie down a while, but nothing here keeps the day.";

        /// <summary>Whether the responder is currently listening. Idempotence is the point: see
        /// <see cref="Install"/>.</summary>
        public static bool Installed { get; private set; }

        /// <summary>Begin answering rest requests. Double-install is a no-op — two subscriptions would
        /// write the save twice and toast twice for one press, and a service that survives
        /// quit-to-title can plausibly be re-awoken.</summary>
        public static void Install()
        {
            if (Installed) return;
            EventBus.Subscribe<RestSaveRequested>(OnRestRequested);
            Installed = true;
        }

        /// <summary>Stop answering. Safe when not installed (a teardown must never have to check).</summary>
        public static void Uninstall()
        {
            if (!Installed) return;
            EventBus.Unsubscribe<RestSaveRequested>(OnRestRequested);
            Installed = false;
        }

        static void OnRestRequested(RestSaveRequested request) => Fulfil(request);

        /// <summary>
        /// Do the write and report it. Public so a test can drive the whole contract as a pure call,
        /// without the bus in the way.
        ///
        /// <para>⚠ Explicit <c>== null</c> on the service rather than <c>?.</c>: <see cref="GameServices"/>
        /// hands back a reference that may be a destroyed <c>UnityEngine.Object</c>, and the
        /// null-propagating operators sail straight past Unity's overloaded <c>==</c> (the same trap
        /// <c>BuildingInterior.ResolveOccupant</c> documents).</para>
        /// </summary>
        public static GameSaved Fulfil(RestSaveRequested request)
        {
            ISaveService save = GameServices.Save;

            GameSaved outcome = save == null
                ? new GameSaved(false, NoServiceText)
                : Write(save, request);

            EventBus.Publish(outcome);
            return outcome;
        }

        /// <summary>
        /// Build the anchor this request should be remembered by: the request's own wake spot and storey,
        /// stamped with the region the player is CURRENTLY in.
        ///
        /// <para><b>Why the region comes from Core and the rest comes from the bed.</b> A bed genuinely
        /// does not know its region — a region id lives on the scene's <c>RegionAnchor</c> and is
        /// published to <see cref="GameServices.CurrentRegionId"/> precisely so that content which
        /// outlives one scene can ask (rule 4). The bed, equally, is the only thing that knows which
        /// STOREY it is on. Each fact is taken from whoever actually holds it, and neither is inferred
        /// from the other.</para>
        ///
        /// <para><b>No region reported is <see cref="RestAnchor.None"/>, not a guess.</b> That is EditMode,
        /// a bare region scene opened for review, a test rig — contexts where there is no honest answer,
        /// and where writing one would put a real save's bed in a region that does not exist. Pure and
        /// public so the whole rule is assertable without a scene.</para>
        /// </summary>
        public static RestAnchor AnchorFor(in RestSaveRequested request, string currentRegionId) =>
            string.IsNullOrEmpty(currentRegionId)
                ? RestAnchor.None
                : new RestAnchor(currentRegionId, request.WakePosition, request.WakeLevel);

        /// <summary>
        /// Ask the service to write, and read back whether it did.
        ///
        /// <para><b>How we know it landed without a return value.</b> <see cref="ISaveService.Save"/> is
        /// void and its write gate is private to the concrete service, so the outcome is read from
        /// <see cref="ISaveService.LoadedExistingSave"/> — which the service sets true on, and only on, a
        /// write that reached the disk. A refused save (at the title) leaves it as it was, so a first
        /// save that was refused reports false and a player who is told "the day is kept" was actually
        /// kept. The one honest limitation: a save refused AFTER an earlier successful one still reports
        /// true, because the flag is latched. That is a Core-contract change (a bool return on
        /// <c>Save</c>) and this PR is not the place for it — the failure it hides is "the player pressed
        /// rest while at the title", which they cannot do from inside a bedroom.</para>
        /// </summary>
        static GameSaved Write(ISaveService save, in RestSaveRequested request)
        {
            // Where you slept goes in BEFORE the write, because the write is what makes it durable —
            // stamping after it would keep the anchor only until the next save happened to run, and the
            // next save is an autosave on quit, which is the exact moment the player expects this to
            // have worked.
            RestLocker.Stamp(save.Current, AnchorFor(request, GameServices.CurrentRegionId));

            save.Save();
            return save.LoadedExistingSave
                ? new GameSaved(true, SavedText)
                : new GameSaved(false, NoServiceText);
        }
    }
}
