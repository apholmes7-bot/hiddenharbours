using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// THE DECK WORK (trap-fishing arc Build 7) — the owner's post-haul minigame. When a haul surfaces a
    /// ready pot it no longer lands the catch instantly: the POT lands on the DECK with the catch still
    /// inside (exactly what the Build-3 deterministic resolver decided — this changes WHEN/HOW it lands,
    /// never WHAT), and the player works her by hand, walking the deck (<see cref="ControlMode.OnDeck"/>):
    ///
    /// <list type="bullet">
    ///   <item><b>PICK</b> — stand over the pot and HOLD the work key; release to grab the next animal
    ///   out. The teeth (P5, cozy): a grab can get NIPPED — a recoil beat, the animal stays in the pot,
    ///   try again; the cost is only time. The nip risk eases with CARE: the longer the hold before the
    ///   release (the animal visibly lifts out as the hold matures — the diegetic read, no meter), the
    ///   safer the grab. Nip rolls are deterministic per animal + attempt (<see cref="DeckWork"/>).</item>
    ///   <item><b>SORT</b> — each picked animal is sorted on the spot by its deterministic size (and
    ///   berried flag): shorts and berried hens arc back over the side, value zero, a toast says why (the
    ///   honest-fishery read); keepers wait on the deck.</item>
    ///   <item><b>BAND</b> — stand over a waiting keeper and HOLD to band its claws; only a banded keeper
    ///   counts — it lands in the hold through the unchanged <see cref="IHold.TryAdd"/> +
    ///   <see cref="FishCaught"/> path (zero economy change).</item>
    ///   <item><b>RE-BAIT</b> — the emptied pot is baited by hand (HOLD over her), consuming ONE of the
    ///   trap's required bait from <see cref="SaveData.BaitStock"/> — the physical replacement for the
    ///   abstract at-placement consumption. A baited pot is ready to SET (T), as today.</item>
    /// </list>
    ///
    /// <para><b>Diegetic, no HUD (owner canon).</b> The pot and the animals ARE the interface; toasts
    /// (Core <see cref="DevNotice"/>) carry OUTCOMES only, plus one teaching line per new verb, first
    /// time. No timing prompts, no meters. Leaving the deck mid-work is a cozy PAUSE — the pot stays
    /// aboard riding the boat; the work resumes when you step back (the live-haul drop rule's spirit,
    /// gentler: nothing is lost).</para>
    ///
    /// <para><b>Transient state — the ADR 0020 greybox compromise (no schema change).</b> A pot on deck
    /// mid-sort is transient like <see cref="ControlMode"/>: its trap DTO leaves the save the moment it
    /// comes aboard (so a reload can never re-haul it — no dupes), and on a region change or a load the
    /// pot AUTO-RESOLVES cozily: everything still aboard lands as if picked + banded per the
    /// already-derived deterministic sort, one toast, nothing silent (see <see cref="DeckPot.AutoResolve"/>).
    /// A save-schema change (persisting the deck pot) needs lead-architect + an ADR — out of scope.</para>
    ///
    /// <para><b>Seam discipline (rule 4).</b> Fishing-lane component on the boat GameObject (spawned by
    /// its sibling <see cref="TrapHaulController"/> at pot-aboard). Cross-module strictly via Core: the
    /// deck gate reads <see cref="ControlModeChanged"/>, feedback goes out as <see cref="DevNotice"/> /
    /// <see cref="FishCaught"/>, the boat root and the deck-walking player arrive as plain
    /// <see cref="Transform"/>s. Rule 7: the prop/animal renderers are a pooled, reused set built once
    /// per pot at event time; the per-frame path allocates nothing.</para>
    /// </summary>
    public sealed class PotDeckWorkController : MonoBehaviour
    {
        /// <summary>The contextual deck verb the current hold is performing.</summary>
        public enum DeckVerb
        {
            /// <summary>No target in reach (or no pot aboard).</summary>
            None = 0,
            /// <summary>Holding over the pot to pick the next animal (resolves on RELEASE — the care read).</summary>
            Grab = 1,
            /// <summary>Holding over a waiting keeper to band its claws (completes at BandSeconds).</summary>
            Band = 2,
            /// <summary>Holding over the emptied pot to bait her (completes at BaitSeconds).</summary>
            Bait = 3,
        }

        /// <summary>Why T can (or can't) set the pot aboard right now — the sibling
        /// <see cref="DevTrapInput"/> reads this to phrase the cozy refusal.</summary>
        public enum DeckSetState
        {
            /// <summary>No pot aboard — the legacy T flow applies.</summary>
            NoPot = 0,
            /// <summary>Animals still in the pot — pick her out first.</summary>
            StillFull = 1,
            /// <summary>Keepers wait unbanded on the deck.</summary>
            KeepersUnbanded = 2,
            /// <summary>Emptied but not yet baited.</summary>
            Unbaited = 3,
            /// <summary>Baited and squared away — T sets her.</summary>
            Ready = 4,
        }

        [Header("Keys (dev only — replaced by the InputService later, ui-ux)")]
        [Tooltip("HOLD to work the deck: pick from the pot / band a keeper / bait the emptied pot, " +
                 "contextual on the nearest work within reach. The same hold-language as the haul " +
                 "(mouse-hold + gamepad South also hold).")]
        [SerializeField] private Key _workKey = Key.Space;

        // Sorting orders for the greybox props: above the water/rope (rope draws at 50), under any HUD.
        // Draw-order constants, not balance numbers.
        private const int PotSortingOrder = 51;
        private const int AnimalSortingOrder = 52;
        // Pixels-per-unit of the code-built silhouettes and the splash arc's visual height (metres).
        // Greybox-art constants, not balance numbers — the owner's sprites replace the silhouettes.
        private const float SilhouettePixelsPerUnit = 24f;
        private const float SplashArcHeightMeters = 0.35f;

        // ---- wiring (given by TrapHaulController.Configure or tests) ----------------------------
        private PlacedTrapService _service;
        private IHold _hold;
        private Transform _boatRoot;   // the deck anchor the props ride (the haul's rail / physics root)
        private Transform _worker;     // the deck-walking player (reach measures from here); null → boat

        // ---- transient state (session-only; NOTHING saved — see the class doc) ------------------
        private DeckPot _pot;
        private bool _onDeck;
        private bool _wasHolding;
        private bool _requireRelease;   // after a band/bait completion or a nip — a fresh action needs a release
        private float _holdSeconds;
        private float _recoilLeft;
        private DeckVerb _holdVerb = DeckVerb.None;

        // One teaching toast per new verb, first time (owner legibility) — session-only flags.
        private bool _taughtPick, _taughtNip, _taughtBand, _taughtBait;

        // ---- greybox visuals (pooled — built once per pot at event time, reused; rule 7) ---------
        private SpriteRenderer _potRenderer;
        private readonly List<SpriteRenderer> _animalRenderers = new();
        private struct SplashAnim { public int AnimalIndex; public Vector2 From; public Vector2 Dir; public float T; public bool Active; }
        private readonly List<SplashAnim> _splashes = new();
        private static readonly Dictionary<DeckAnimalShape, Sprite> _silhouetteCache = new();
        private static Sprite _fallbackPotSprite;

        /// <summary>True while a hauled pot sits on the deck being worked. The handline (and a fresh
        /// haul) stand down while this is true — the deck belongs to the work.</summary>
        public bool HasPotAboard => _pot != null;

        /// <summary>The live deck pot (null when none aboard). For the sibling set-flow / tests.</summary>
        public DeckPot Pot => _pot;

        /// <summary>Where the T-set flow stands with the pot aboard (see <see cref="DeckSetState"/>).</summary>
        public DeckSetState SetState
        {
            get
            {
                if (_pot == null) return DeckSetState.NoPot;
                if (_pot.InPotCount > 0) return DeckSetState.StillFull;
                if (_pot.OnDeckCount > 0) return DeckSetState.KeepersUnbanded;
                return _pot.Baited ? DeckSetState.Ready : DeckSetState.Unbaited;
            }
        }

        /// <summary>True while the deck-work key is live — ON DECK and not under a modal dialogue.
        /// Public + input-free so the gate itself is EditMode-testable.</summary>
        public bool GearKeysLive => _onDeck && !InteractionGate.IsBlocked;

        /// <summary>The current hold's verb (None when idle) — for tests/tooling.</summary>
        public DeckVerb CurrentVerb => _holdVerb;

        // ---- toast text (OUTCOMES only + one teaching line per verb — the owner's no-HUD rule) ----
        private const string NoticePotAboard = "Pot's aboard — stand over her and HOLD to pick";
        private const string NoticeNipTeach = "Nipped! A fuller hold is a safer grab";
        private const string NoticeNip = "Nipped — she's still in the pot";
        private const string NoticeKeeperTeach = "A keeper — HOLD over it to band the claws";
        private const string NoticeBaitTeach = "Pot's empty — HOLD over her to bait";
        private const string NoticePotBaited = "Pot baited — T sets her";
        private const string NoticeNoBait = "No bait aboard";
        private const string NoticeNoRoom = "No room aboard — sell before banding more";

        private Vector2 BoatPos => _boatRoot != null ? (Vector2)_boatRoot.position : (Vector2)transform.position;
        private Vector2 WorkerPos => _worker != null ? (Vector2)_worker.position : BoatPos;
        private Vector2 PotWorldPos => BoatPos + (_pot != null ? _pot.Def.PotDeckOffset : Vector2.zero);

        private void OnEnable()
        {
            // Deck work is a DECK action — the same Core-mediated gate every gear verb uses. Fresh
            // components start un-decked; every transition republishes the mode.
            _onDeck = false;
            EventBus.Subscribe<ControlModeChanged>(OnControlModeChanged);
            // The transient pot cannot survive a load or a region hop (the ADR 0020 greybox compromise) —
            // both edges square her away cozily instead of losing her silently.
            EventBus.Subscribe<GameLoaded>(OnGameLoaded);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ControlModeChanged>(OnControlModeChanged);
            EventBus.Unsubscribe<GameLoaded>(OnGameLoaded);
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>Public so tests can drive the deck gate through the same path the bus uses. Leaving
        /// the deck mid-work is a cozy PAUSE (the hold resets, the pot stays) — never a penalty.</summary>
        public void OnControlModeChanged(ControlModeChanged e)
        {
            _onDeck = e.Mode == ControlMode.OnDeck;
            if (!_onDeck) CancelHold();
        }

        /// <summary>Seed the deck gate for a controller spawned MID-MODE: the spawner (the haul
        /// controller) heard the last <see cref="ControlModeChanged"/>; a just-added component did not —
        /// without this a pot brought aboard would sit dead until the next mode transition.</summary>
        public void SeedDeckGate(bool onDeck) => _onDeck = onDeck;

        private void OnGameLoaded(GameLoaded _) => AutoResolvePot();

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AutoResolvePot();

        /// <summary>Wire the controller in one call (the sibling haul controller / tests). The boat root
        /// is the deck the props ride; the worker is the deck-walking player reach measures from (null →
        /// the boat root — any unwired rig keeps working, just centre-measured).</summary>
        public void Configure(PlacedTrapService service, IHold hold, Transform boatRoot, Transform worker)
        {
            _service = service;
            _hold = hold;
            _boatRoot = boatRoot;
            _worker = worker;
        }

        // ---- pot aboard --------------------------------------------------------------------------

        /// <summary>
        /// Land the hauled pot ON THE DECK with its already-resolved catch inside (called by the sibling
        /// <see cref="TrapHaulController"/> at surface, BEFORE the trap object is removed). Derives the
        /// deterministic per-animal facts once, shows the greybox props, teaches the pick verb the first
        /// time. Returns false (and lands nothing) if a pot is already aboard or the facts are unusable —
        /// the caller falls back to the legacy instant-land.
        /// </summary>
        public bool BringAboard(PlacedTrap trap, IReadOnlyList<CatchItem> catchItems)
        {
            if (_pot != null || trap == null || trap.Trap == null || trap.Trap.DeckWork == null) return false;
            if (catchItems == null || catchItems.Count == 0) return false;

            _pot = new DeckPot(trap.Trap, trap.Trap.DeckWork, trap.InstanceId,
                               trap.PlacementGameTimeSeconds, trap.WorldSeed, catchItems);
            _holdSeconds = 0f;
            _recoilLeft = 0f;
            _holdVerb = DeckVerb.None;
            _requireRelease = true;   // the haul's hold is likely still down — a fresh verb needs a release
            BuildPotVisuals();

            if (!_taughtPick) { EventBus.Publish(new DevNotice(NoticePotAboard)); _taughtPick = true; }
            Debug.Log($"[DeckWork] Pot aboard with {catchItems.Count} in her — pick, sort, band, bait.");
            return true;
        }

        /// <summary>Drop the deck pot after a successful SET (the sibling dev input placed it back in the
        /// water) — visuals away, state cleared. Idempotent.</summary>
        public void ClearPot()
        {
            _pot = null;
            _holdVerb = DeckVerb.None;
            _holdSeconds = 0f;
            HideVisuals();
        }

        /// <summary>
        /// The cozy auto-resolve (load / region change — the ADR 0020 greybox compromise): everything
        /// still aboard lands as if picked + banded per the already-derived deterministic sort. Keepers go
        /// through the unchanged <see cref="FishCaught"/> land path; ONE summary toast, nothing silent.
        /// Safe to call any time (no-op without a pot). Public so tests and teardown paths can drive it.
        /// </summary>
        public void AutoResolvePot()
        {
            if (_pot == null) return;

            var landed = new List<CatchItem>(_pot.Animals.Count);   // event-time alloc, never per frame
            _pot.AutoResolve(_hold, landed, out int kept, out int returned, out int noRoom);
            for (int i = 0; i < landed.Count; i++)
                EventBus.Publish(new FishCaught(landed[i]));

            string summary = noRoom > 0
                ? $"Squared away the pot — {kept} stowed, {returned} went back, {noRoom} no room"
                : $"Squared away the pot — {kept} stowed, {returned} went back";
            EventBus.Publish(new DevNotice(summary));
            Debug.Log($"[DeckWork] Auto-resolved the deck pot: {kept} kept, {returned} returned, {noRoom} no room.");
            ClearPot();
        }

        // ---- the live work -------------------------------------------------------------------------

        private void Update()
        {
            if (_pot == null) return;
            bool holding = GearKeysLive && ReadHeld();
            TickWork(Time.deltaTime, holding);
        }

        /// <summary>The work hold, dev-keyed (the same three holds the haul reads: key + mouse + pad).</summary>
        private bool ReadHeld()
        {
            var kb = Keyboard.current;
            return (kb != null && kb[_workKey].isPressed)
                   || (Mouse.current != null && Mouse.current.leftButton.isPressed)
                   || (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed);
        }

        /// <summary>
        /// Advance the deck work by <paramref name="dt"/> seconds with the work key
        /// <paramref name="holding"/> (or not). Pure of real input so tests drive it directly:
        /// <list type="bullet">
        ///   <item>A GRAB hold resolves on RELEASE — the hold length is the care read (nip risk eases as
        ///   the hold matures). Releases shorter than the quick-grab threshold do nothing.</item>
        ///   <item>A BAND/BAIT hold completes at its Def seconds, then requires a genuine release before
        ///   the next action (no accidental chained verbs off one long hold).</item>
        ///   <item>A nip locks the hands out for the recoil beat and also requires a release.</item>
        ///   <item>Walking out of reach mid-hold cancels the hold cozily (nothing lost).</item>
        /// </list>
        /// </summary>
        public void TickWork(float dt, bool holding)
        {
            if (_pot == null) return;
            float step = Mathf.Max(0f, dt);

            if (_recoilLeft > 0f)
            {
                _recoilLeft -= step;
                _wasHolding = holding;   // holds during the recoil don't count (and won't resolve)
                _holdSeconds = 0f;
                return;
            }

            if (_requireRelease)
            {
                if (holding) { _wasHolding = true; return; }   // swallow the carried-over hold
                _requireRelease = false;
                _wasHolding = false;
                return;
            }

            if (holding)
            {
                if (!_wasHolding)
                {
                    // A fresh hold locks onto the nearest work within reach NOW (walk-to, diegetic).
                    _holdVerb = ResolveVerb();
                    _holdSeconds = 0f;
                }

                if (_holdVerb != DeckVerb.None)
                {
                    // Walking off the work mid-hold lets go cozily (nothing lost, try again).
                    if (!TargetInReach(_holdVerb)) { CancelHold(); _wasHolding = true; return; }

                    _holdSeconds += step;
                    if (_holdVerb == DeckVerb.Band && _pot.Def != null && _holdSeconds >= _pot.Def.BandSeconds)
                    {
                        ResolveBand();
                        _requireRelease = true;
                    }
                    else if (_holdVerb == DeckVerb.Bait && _pot.Def != null && _holdSeconds >= _pot.Def.BaitSeconds)
                    {
                        ResolveBait();
                        _requireRelease = true;
                    }
                }
                _wasHolding = true;
                return;
            }

            // Released. A grab resolves here — the hold length is the care read.
            if (_wasHolding && _holdVerb == DeckVerb.Grab && _pot.Def != null
                && _holdSeconds >= _pot.Def.QuickGrabSeconds)
            {
                ResolveGrab(_holdSeconds);
            }
            _holdVerb = DeckVerb.None;
            _holdSeconds = 0f;
            _wasHolding = false;
        }

        /// <summary>Reset the live hold without touching the pot (cozy pause — off deck, out of reach).</summary>
        private void CancelHold()
        {
            _holdVerb = DeckVerb.None;
            _holdSeconds = 0f;
        }

        /// <summary>The contextual verb for a hold starting NOW: the nearest actionable work within the
        /// Def's reach — a waiting keeper (band) or the pot (grab while she holds animals; bait once she's
        /// empty and unbanded keepers are stowed). Nearest wins so standing over a keeper bands it even
        /// with the pot alongside.</summary>
        public DeckVerb ResolveVerb()
        {
            if (_pot == null || _pot.Def == null) return DeckVerb.None;
            Vector2 worker = WorkerPos;
            float reach = _pot.Def.WorkReachMeters;
            float reachSqr = reach * reach;

            DeckVerb best = DeckVerb.None;
            float bestSqr = reachSqr;

            // The pot's verb (grab, or bait once she's squared away).
            float potSqr = (PotWorldPos - worker).sqrMagnitude;
            if (potSqr <= bestSqr)
            {
                if (_pot.InPotCount > 0) { best = DeckVerb.Grab; bestSqr = potSqr; }
                else if (_pot.NeedsBait) { best = DeckVerb.Bait; bestSqr = potSqr; }
            }

            // The nearest waiting keeper (band).
            var animals = _pot.Animals;
            for (int i = 0; i < animals.Count; i++)
            {
                if (animals[i].Fate != DeckAnimalFate.OnDeck) continue;
                float sqr = (KeeperWorldPos(animals[i]) - worker).sqrMagnitude;
                if (sqr <= bestSqr) { best = DeckVerb.Band; bestSqr = sqr; }
            }
            return best;
        }

        /// <summary>Is the live hold's target still within working reach?</summary>
        private bool TargetInReach(DeckVerb verb)
        {
            if (_pot == null || _pot.Def == null) return false;
            float reachSqr = _pot.Def.WorkReachMeters * _pot.Def.WorkReachMeters;
            Vector2 worker = WorkerPos;

            if (verb == DeckVerb.Grab || verb == DeckVerb.Bait)
                return (PotWorldPos - worker).sqrMagnitude <= reachSqr;

            // Band: any waiting keeper still in reach keeps the hold honest.
            var animals = _pot.Animals;
            for (int i = 0; i < animals.Count; i++)
                if (animals[i].Fate == DeckAnimalFate.OnDeck
                    && (KeeperWorldPos(animals[i]) - worker).sqrMagnitude <= reachSqr)
                    return true;
            return false;
        }

        /// <summary>Where a waiting keeper sits on the deck: the pot plus the Def's keeper row, spaced by
        /// the animal's stable index (banded animals leave gaps — fine, the row is short).</summary>
        private Vector2 KeeperWorldPos(DeckPot.Animal a)
            => PotWorldPos + _pot.Def.KeeperRowOffset + new Vector2(a.Index * _pot.Def.KeeperSpacingMeters, 0f);

        // ---- verb resolutions -----------------------------------------------------------------------

        private void ResolveGrab(float heldSeconds)
        {
            GrabOutcome outcome = _pot.TryGrabNext(heldSeconds, out DeckPot.Animal animal);
            switch (outcome)
            {
                case GrabOutcome.Nipped:
                    _recoilLeft = _pot.Def.NipRecoilSeconds;
                    _requireRelease = true;
                    if (!_taughtNip) { EventBus.Publish(new DevNotice(NoticeNipTeach)); _taughtNip = true; }
                    else EventBus.Publish(new DevNotice(NoticeNip));
                    break;

                case GrabOutcome.Keeper:
                    ShowKeeper(animal);
                    if (!_taughtBand) { EventBus.Publish(new DevNotice(NoticeKeeperTeach)); _taughtBand = true; }
                    else EventBus.Publish(new DevNotice($"Keeper ({animal.SizeMm:0} mm)"));
                    break;

                case GrabOutcome.ReturnedShort:
                    StartSplash(animal);
                    EventBus.Publish(new DevNotice($"Short ({animal.SizeMm:0} mm) — back over the side"));
                    break;

                case GrabOutcome.ReturnedBerried:
                    StartSplash(animal);
                    EventBus.Publish(new DevNotice("Berried hen — back she goes"));
                    break;
            }
            RefreshPotSprite();
            MaybeTeachBait();
        }

        private void ResolveBand()
        {
            BandOutcome outcome = _pot.TryBandNext(_hold, out DeckPot.Animal banded);
            if (outcome == BandOutcome.Banded)
            {
                HideAnimal(banded);
                EventBus.Publish(new FishCaught(banded.Item));   // the unchanged land path — sellable
                MaybeTeachBait();
            }
            else if (outcome == BandOutcome.NoRoom)
            {
                EventBus.Publish(new DevNotice(NoticeNoRoom));
            }
        }

        private void ResolveBait()
        {
            if (_pot == null || _pot.Trap == null || _service == null) return;
            string baitId = _pot.Trap.RequiredBaitId;
            if (_service.TryConsumeBaitFromStock(baitId))
            {
                _pot.MarkBaited(_service.ResolveBait(baitId));
                RefreshPotSprite();
                EventBus.Publish(new DevNotice(NoticePotBaited));
            }
            else
            {
                EventBus.Publish(new DevNotice(NoticeNoBait));
            }
        }

        private void MaybeTeachBait()
        {
            if (_pot != null && _pot.NeedsBait && !_taughtBait)
            {
                EventBus.Publish(new DevNotice(NoticeBaitTeach));
                _taughtBait = true;
            }
        }

        // ---- greybox visuals (pooled; presentation only — never seeds or writes sim state) -----------

        private void BuildPotVisuals()
        {
            if (_potRenderer == null)
            {
                var go = new GameObject("DeckPot");
                go.transform.SetParent(transform, false);
                _potRenderer = go.AddComponent<SpriteRenderer>();
                _potRenderer.sortingOrder = PotSortingOrder;
            }
            RefreshPotSprite();
            _potRenderer.enabled = true;

            // One pooled renderer per animal (grown to the largest pot seen this session, then reused).
            int need = _pot.Animals.Count;
            while (_animalRenderers.Count < need)
            {
                var go = new GameObject("DeckAnimal_" + _animalRenderers.Count);
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = AnimalSortingOrder;
                sr.enabled = false;
                _animalRenderers.Add(sr);
            }
            for (int i = 0; i < _animalRenderers.Count; i++)
            {
                if (_animalRenderers[i] == null) continue;
                _animalRenderers[i].enabled = false;
                if (i < need)
                {
                    DeckPot.Animal a = _pot.Animals[i];
                    _animalRenderers[i].sprite = a.SpriteOverride != null ? a.SpriteOverride : Silhouette(a.Shape);
                }
            }
            _splashes.Clear();
        }

        /// <summary>The pot reads WET while she still holds animals, DRY once picked empty — the owner's
        /// painted states drop into the Def; greybox falls back to the TrapDef sprite, then a built box.</summary>
        private void RefreshPotSprite()
        {
            if (_potRenderer == null || _pot == null) return;
            bool wet = _pot.InPotCount > 0;
            Sprite s = wet ? _pot.Def.PotSpriteWet : _pot.Def.PotSpriteDry;
            if (s == null) s = _pot.Def.PotSpriteWet;                       // dry unauthored → stay wet-look
            if (s == null && _pot.Trap != null) s = _pot.Trap.TrapSprite;
            if (s == null) s = FallbackPotSprite();
            _potRenderer.sprite = s;
        }

        private void ShowKeeper(DeckPot.Animal a)
        {
            SpriteRenderer sr = RendererFor(a);
            if (sr == null) return;
            sr.color = Color.white;
            sr.enabled = true;
            sr.transform.position = KeeperWorldPos(a);
        }

        private void HideAnimal(DeckPot.Animal a)
        {
            SpriteRenderer sr = RendererFor(a);
            if (sr != null) sr.enabled = false;
        }

        private void StartSplash(DeckPot.Animal a)
        {
            SpriteRenderer sr = RendererFor(a);
            if (sr == null || _pot == null) return;
            sr.color = Color.white;
            sr.enabled = true;

            // Over the nearest side: away from the worker, so the throw reads from the hands. Degenerate
            // (worker on the pot) falls to starboard. Visual only.
            Vector2 from = PotWorldPos;
            Vector2 dir = from - WorkerPos;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            _splashes.Add(new SplashAnim { AnimalIndex = a.Index, From = from, Dir = dir, T = 0f, Active = true });
        }

        private SpriteRenderer RendererFor(DeckPot.Animal a)
            => a != null && a.Index >= 0 && a.Index < _animalRenderers.Count ? _animalRenderers[a.Index] : null;

        private void HideVisuals()
        {
            if (_potRenderer != null) _potRenderer.enabled = false;
            for (int i = 0; i < _animalRenderers.Count; i++)
                if (_animalRenderers[i] != null) _animalRenderers[i].enabled = false;
            _splashes.Clear();
        }

        private void LateUpdate()
        {
            if (_pot == null)
            {
                TickSplashes(Time.deltaTime);   // let a final splash finish even as the pot clears
                return;
            }

            // The props ride the boat but stay screen-upright (the DirectionalBoatSprite convention —
            // position from the physics root, rotation stomped by never rotating at all).
            if (_potRenderer != null)
                _potRenderer.transform.position = PotWorldPos;

            var animals = _pot.Animals;
            for (int i = 0; i < animals.Count; i++)
            {
                DeckPot.Animal a = animals[i];
                SpriteRenderer sr = RendererFor(a);
                if (sr == null) continue;

                if (a.Fate == DeckAnimalFate.OnDeck)
                {
                    sr.enabled = true;
                    sr.transform.position = KeeperWorldPos(a);
                }
                else if (a.Fate == DeckAnimalFate.InPot)
                {
                    // The diegetic care read: the animal being grabbed LIFTS out of the pot as the hold
                    // matures — a full lift is a full, safe grab. No meter, the pot is the interface.
                    bool lifting = _holdVerb == DeckVerb.Grab && _wasHolding && a == _pot.NextInPot();
                    sr.enabled = lifting;
                    if (lifting && _pot.Def != null)
                    {
                        float lift01 = _pot.Def.FullGrabSeconds > 0f
                            ? Mathf.Clamp01(_holdSeconds / _pot.Def.FullGrabSeconds) : 1f;
                        sr.transform.position = PotWorldPos + Vector2.up * (_pot.Def.GrabLiftMeters * lift01);
                    }
                }
            }
            TickSplashes(Time.deltaTime);
        }

        /// <summary>Advance the little over-the-side arcs (visual only). Reverse-iterated removal-free:
        /// finished entries deactivate in place; the list resets when a new pot builds.</summary>
        private void TickSplashes(float dt)
        {
            if (_splashes.Count == 0) return;
            float span = _pot != null && _pot.Def != null ? Mathf.Max(0.05f, _pot.Def.SplashOutSeconds) : 0.7f;
            float dist = _pot != null && _pot.Def != null ? _pot.Def.SplashOutDistanceMeters : 1.6f;

            for (int i = 0; i < _splashes.Count; i++)
            {
                SplashAnim s = _splashes[i];
                if (!s.Active) continue;
                s.T += dt / span;
                SpriteRenderer sr = s.AnimalIndex >= 0 && s.AnimalIndex < _animalRenderers.Count
                    ? _animalRenderers[s.AnimalIndex] : null;
                if (s.T >= 1f)
                {
                    s.Active = false;
                    if (sr != null) sr.enabled = false;
                }
                else if (sr != null)
                {
                    float t = s.T;
                    Vector2 pos = s.From + s.Dir * (dist * t) + Vector2.up * (Mathf.Sin(t * Mathf.PI) * SplashArcHeightMeters);
                    sr.transform.position = pos;
                    Color c = sr.color; c.a = 1f - t * t; sr.color = c;   // fade as she goes under
                }
                _splashes[i] = s;
            }
        }

        // ---- code-built greybox silhouettes (cached; the owner's sprites replace them via the Def) ----

        private static Sprite Silhouette(DeckAnimalShape shape)
        {
            if (_silhouetteCache.TryGetValue(shape, out Sprite cached) && cached != null) return cached;
            Sprite built = shape == DeckAnimalShape.Crab
                ? BuildSprite(CrabMap, new Color(0.62f, 0.32f, 0.22f, 1f))     // brick-brown crab
                : BuildSprite(LobsterMap, new Color(0.16f, 0.25f, 0.32f, 1f)); // dark blue-green lobster
            _silhouetteCache[shape] = built;
            return built;
        }

        private static Sprite FallbackPotSprite()
        {
            if (_fallbackPotSprite != null) return _fallbackPotSprite;
            _fallbackPotSprite = BuildSprite(PotMap, new Color(0.42f, 0.33f, 0.22f, 1f));   // slatted timber
            return _fallbackPotSprite;
        }

        // Pixel maps ('X' = filled), drawn top row first. Greybox art, not balance data.
        private static readonly string[] LobsterMap =
        {
            ".XX..XX.",
            "XXX..XXX",
            "XXX..XXX",
            ".X....X.",
            "..XXXX..",
            "..XXXX..",
            "..XXXX..",
            "..XXXX..",
            "...XX...",
            "...XX...",
            "..XXXX..",
            ".XXXXXX.",
        };

        private static readonly string[] CrabMap =
        {
            "XX..........XX",
            "X.X.X....X.X.X",
            "...XXXXXXXX...",
            ".XXXXXXXXXXXX.",
            ".XXXXXXXXXXXX.",
            "...XXXXXXXX...",
            "X.X.X....X.X.X",
            "XX..........XX",
        };

        private static readonly string[] PotMap =
        {
            "XXXXXXXXXXXXXXXXXXXX",
            "X..X..X..X..X..X...X",
            "X..X..X..X..X..X...X",
            "XXXXXXXXXXXXXXXXXXXX",
            "X..X..X..X..X..X...X",
            "X..X..X..X..X..X...X",
            "XXXXXXXXXXXXXXXXXXXX",
            "X..X..X..X..X..X...X",
            "X..X..X..X..X..X...X",
            "XXXXXXXXXXXXXXXXXXXX",
            "X..X..X..X..X..X...X",
            "XXXXXXXXXXXXXXXXXXXX",
        };

        private static Sprite BuildSprite(string[] map, Color color)
        {
            int h = map.Length;
            int w = map[0].Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var clear = new Color(0f, 0f, 0f, 0f);
            for (int y = 0; y < h; y++)
            {
                string row = map[y];
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, h - 1 - y, x < row.Length && row[x] == 'X' ? color : clear);
            }
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), SilhouettePixelsPerUnit);
        }
    }
}
