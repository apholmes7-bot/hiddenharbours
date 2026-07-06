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

        private bool _aboard;   // setting a pot is a BOAT action — keys only live while aboard (ControlModeChanged)

        private void Awake()
        {
            if (_dropPoint == null) _dropPoint = transform;
        }

        private void OnEnable()
        {
            _aboard = GameServices.ActiveBoat != null && GameServices.ActiveBoat.HasActiveBoat;
            EventBus.Subscribe<ControlModeChanged>(OnControlModeChanged);
        }

        private void OnDisable() => EventBus.Unsubscribe<ControlModeChanged>(OnControlModeChanged);

        private void OnControlModeChanged(ControlModeChanged e) => _aboard = e.Mode == ControlMode.Aboard;

        private void Update()
        {
            if (!_aboard) return;                    // on foot → setting a pot is a boat action, keys are dead
            if (InteractionGate.IsBlocked) return;   // a modal dialogue owns the keys while up
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb[_dropKey].wasPressedThisFrame) DropTrap();
            if (kb[_grantKey].wasPressedThisFrame) GrantDevSupply();
        }

        /// <summary>Set a baited trap at the drop point through the real Build-4 depth+bait gate. Public so a
        /// test/tool can drive it without input. Returns the placement result.</summary>
        public PlacedTrapService.PlaceResult DropTrap()
        {
            if (_service == null || _trapDef == null)
            {
                Debug.LogWarning("[DevTrapInput] No service/trap wired.");
                return PlacedTrapService.PlaceResult.NoTrap;
            }
            Vector2 pos = _dropPoint != null ? (Vector2)_dropPoint.position : Vector2.zero;
            return _service.TryPlaceGated(_trapDef, _bait, pos, _regionId, out _);
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
                    Debug.Log($"[DevTrapInput] Granted {_devBaitGrant} {_bait.DisplayName} (now {save.BaitStock[i].Count}).");
                    return;
                }
            }
            save.BaitStock.Add(new BaitStock(_bait.Id, _devBaitGrant));
            Debug.Log($"[DevTrapInput] Granted {_devBaitGrant} {_bait.DisplayName} (dev supply).");
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
