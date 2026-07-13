using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;   // BaitDef (Fishing → Economy is allowed)

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// PLACEHOLDER dev input for the trap loop (trap-fishing arc Build 4) — <b>NOT</b> the real place/haul UX.
    /// It scaffolds the two ends of the manual loop so the owner can play it end-to-end in the greybox:
    ///
    /// <list type="bullet">
    ///   <item><b>T — set a baited trap</b> at the drop point via the REAL Build-4 depth gate
    ///   (<see cref="PlacedTrapService.TryPlaceGated"/>): it drops only where the water is deep enough
    ///   (<see cref="TrapPlacement"/>) and only if the required bait is in stock, consuming one. Refusals log
    ///   a cozy reason (too shoal / no bait). This REPLACES the Build-3 unconditional dev drop.</item>
    ///   <item><b>G — dev-grant supply</b> (greybox only): tops up a small stock of the dev trap's bait so
    ///   the loop is playable NOW. Real trap/bait acquisition is a later ECONOMY offer (a Shipwright/gear
    ///   sale) — flagged, not built here.</item>
    /// </list>
    ///
    /// <para>The HAUL is no longer here — it's the rhythm minigame on <see cref="TrapHaulController"/> (lay
    /// alongside a buoy, H to start, pull to the swell). This dev input only PLACES + grants. Mirrors the
    /// <see cref="DevFishingInput"/>/<c>ClamDigger</c> dev scaffolds: dev-keyed via the New Input System
    /// (legacy Input throws at runtime — memory), stood down under a modal dialogue
    /// (<see cref="InteractionGate"/>). Replaced by the InputService + the real place UX later (ui-ux).</para>
    ///
    /// <para><b>Build 5:</b> the keys live only while the player stands <b>ON DECK</b>
    /// (<see cref="ControlMode.OnDeck"/>) — working the gear is a deck action; at the helm you're steering
    /// and on foot you're ashore. Every outcome raises a Core <see cref="DevNotice"/> toast so the owner
    /// sees refusals/grants/sets on screen instead of the Console.</para>
    /// </summary>
    public sealed class DevTrapInput : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The service that owns placed traps (place/save). Required.")]
        [SerializeField] private PlacedTrapService _service;
        [Tooltip("Where a trap drops (the active boat / player). Defaults to this object's transform.")]
        [SerializeField] private Transform _dropPoint;

        [Header("What to drop (greybox — a fixed baited trap for the dev loop)")]
        [Tooltip("The trap kind the dev drop places.")]
        [SerializeField] private TrapDef _trapDef;
        [Tooltip("The bait the dev drop loads (its FavorsSpeciesIds soft-weight the catch). Null = unbaited.")]
        [SerializeField] private BaitDef _bait;
        [Tooltip("The region id placed traps are tagged with (scene-per-region).")]
        [SerializeField] private string _regionId = "region.st_peters";

        [Header("Dev supply grant (greybox — real acquisition is a later ECONOMY offer)")]
        [Tooltip("How many bait the G key grants per press, so the loop is playable before the economy sells " +
                 "bait. Tunable; greybox only.")]
        [Min(1)][SerializeField] private int _devBaitGrant = 5;

        [Header("Keys (dev only)")]
        [SerializeField] private Key _dropKey = Key.T;
        [SerializeField] private Key _grantKey = Key.G;

        // Working the gear is a DECK action (owner's Build-5 split): keys live only while standing ON
        // DECK — never at the helm (you're steering) and never on foot (ControlModeChanged, via Core).
        private bool _onDeck;

        // Build 7: the deck-work sibling (lazy + cached, the DevFishingInput convention). While a hauled
        // pot sits on the deck, T sets THAT pot (pre-baited by the deck's re-bait) instead of conjuring a
        // fresh abstract one — the deck absorbs the abstract at-placement flow whenever a pot is aboard.
        private PotDeckWorkController _deckWork;

        private PotDeckWorkController DeckWork
            => _deckWork != null ? _deckWork : (_deckWork = GetComponent<PotDeckWorkController>());

        /// <summary>True while the dev gear keys are live — ON DECK and not under a modal dialogue.
        /// Public + input-free so the gate itself is EditMode-testable.</summary>
        public bool GearKeysLive => _onDeck && !InteractionGate.IsBlocked;

        private void Awake()
        {
            if (_dropPoint == null) _dropPoint = transform;
        }

        private void OnEnable()
        {
            // Fresh components start un-decked; every transition (and the region-arrival re-assert)
            // republishes the mode, which keeps this true across scene hops.
            _onDeck = false;
            EventBus.Subscribe<ControlModeChanged>(OnControlModeChanged);
        }

        private void OnDisable() => EventBus.Unsubscribe<ControlModeChanged>(OnControlModeChanged);

        /// <summary>Public so tests can drive the deck gate through the same path the bus uses.</summary>
        public void OnControlModeChanged(ControlModeChanged e) => _onDeck = e.Mode == ControlMode.OnDeck;

        private void Update()
        {
            if (!GearKeysLive) return;               // gear is worked from the DECK; keys are dead elsewhere
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb[_dropKey].wasPressedThisFrame) DropTrap();
            if (kb[_grantKey].wasPressedThisFrame) GrantDevSupply();
        }

        // On-screen greybox feedback (DevNotice → DevToast) so the owner reads the loop without the
        // Console. Event-time strings only (a keypress), never per frame.
        private const string NoticePotSet = "Pot set";
        private const string NoticeTooShallow = "Too shallow here";
        private const string NoticeNoBait = "No bait aboard";
        private const string NoticeNotWired = "No trap gear wired (dev)";
        // Build 7 deck-pot refusals — the pot aboard must be worked to READY before T sets her.
        private const string NoticeDeckStillFull = "She's still full — pick her out first";
        private const string NoticeDeckUnbanded = "Band the keepers first";
        private const string NoticeDeckUnbaited = "Bait her first";

        /// <summary>Set a baited trap at the drop point through the real Build-4 depth+bait gate. Public so a
        /// test/tool can drive it without input. Returns the placement result. Every outcome raises a Core
        /// <see cref="DevNotice"/> so the owner sees the refusal/success on screen, not in the Console.</summary>
        public PlacedTrapService.PlaceResult DropTrap()
        {
            if (_service == null || _trapDef == null)
            {
                Debug.LogWarning("[DevTrapInput] No service/trap wired.");
                EventBus.Publish(new DevNotice(NoticeNotWired));
                return PlacedTrapService.PlaceResult.NoTrap;
            }
            Vector2 pos = _dropPoint != null ? (Vector2)_dropPoint.position : Vector2.zero;

            // Build 7: a hauled pot on the deck ABSORBS the T flow — set HER (pre-baited by the deck's
            // re-bait; no second bait charge), or say cozily why she isn't ready. The abstract fresh-pot
            // drop below only applies while no pot is aboard (it still makes sense for pots not yet
            // in hand — trap acquisition stays a later economy offer).
            var deck = DeckWork;
            if (deck != null && deck.HasPotAboard) return TrySetDeckPot(deck, pos);

            var result = _service.TryPlaceGated(_trapDef, _bait, pos, _regionId, out _);
            EventBus.Publish(new DevNotice(result switch
            {
                PlacedTrapService.PlaceResult.Placed => NoticePotSet,
                PlacedTrapService.PlaceResult.TooShallow => NoticeTooShallow,
                PlacedTrapService.PlaceResult.NoBait => NoticeNoBait,
                _ => NoticeNotWired,
            }));
            return result;
        }

        /// <summary>Set the worked deck pot back in the water (Build 7): only a READY pot (picked empty,
        /// keepers banded, re-baited) goes — refusals name the missing step. The set runs the same depth
        /// gate as ever but consumes NO bait (the deck's re-bait already did). Public-path helper of
        /// <see cref="DropTrap"/>; toasts every outcome.</summary>
        private PlacedTrapService.PlaceResult TrySetDeckPot(PotDeckWorkController deck, Vector2 pos)
        {
            switch (deck.SetState)
            {
                case PotDeckWorkController.DeckSetState.StillFull:
                    EventBus.Publish(new DevNotice(NoticeDeckStillFull));
                    return PlacedTrapService.PlaceResult.PotNotReady;
                case PotDeckWorkController.DeckSetState.KeepersUnbanded:
                    EventBus.Publish(new DevNotice(NoticeDeckUnbanded));
                    return PlacedTrapService.PlaceResult.PotNotReady;
                case PotDeckWorkController.DeckSetState.Unbaited:
                    EventBus.Publish(new DevNotice(NoticeDeckUnbaited));
                    return PlacedTrapService.PlaceResult.PotNotReady;
            }

            DeckPot pot = deck.Pot;
            var result = _service.TryPlacePreBaited(pot.Trap, pot.LoadedBait, pos, _regionId, out _);
            if (result == PlacedTrapService.PlaceResult.Placed)
            {
                deck.ClearPot();   // she's back in the water — the deck is clear for the next haul
                EventBus.Publish(new DevNotice(NoticePotSet));
            }
            else
            {
                EventBus.Publish(new DevNotice(result == PlacedTrapService.PlaceResult.TooShallow
                    ? NoticeTooShallow : NoticeNotWired));
            }
            return result;
        }

        /// <summary>Dev-grant a few bait into the save so the loop is playable now (greybox only). Real bait
        /// acquisition is a later economy offer. Public so a test/tool can drive it without input.</summary>
        public void GrantDevSupply()
        {
            var save = GameServices.Save?.Current;
            if (save == null || _bait == null) { Debug.Log("[DevTrapInput] No save / bait to grant."); return; }

            save.BaitStock ??= new System.Collections.Generic.List<BaitStock>();
            for (int i = 0; i < save.BaitStock.Count; i++)
            {
                if (save.BaitStock[i].BaitId == _bait.Id)
                {
                    save.BaitStock[i] = new BaitStock(_bait.Id, save.BaitStock[i].Count + _devBaitGrant);
                    EventBus.Publish(new DevNotice($"+{_devBaitGrant} {_bait.DisplayName} bait ({save.BaitStock[i].Count} aboard)"));
                    return;
                }
            }
            save.BaitStock.Add(new BaitStock(_bait.Id, _devBaitGrant));
            EventBus.Publish(new DevNotice($"+{_devBaitGrant} {_bait.DisplayName} bait"));
        }

        /// <summary>Wire the dev input in one call (tests / editor).</summary>
        public void Configure(PlacedTrapService service, Transform dropPoint, TrapDef trapDef, BaitDef bait,
                              string regionId)
        {
            _service = service;
            _dropPoint = dropPoint;
            _trapDef = trapDef;
            _bait = bait;
            _regionId = regionId;
        }
    }
}
