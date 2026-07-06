using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;   // BaitDef (Fishing → Economy is allowed)

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// PLACEHOLDER dev input to prove the trap loop end-to-end (trap-fishing arc Build 3) — <b>NOT</b> the
    /// real placement flow. Two keys: <b>T drops</b> a baited trap at the drop point, and <b>Y checks/hauls</b>
    /// the nearest trap (resolves its deterministic catch and lands it into the active boat's hold, like
    /// <c>ClamDigger</c>). It exists ONLY so the owner can feel the loop: <i>drop → wait → ready → reload =
    /// identical catch</i>. It is not depth-gated placement and not the haul minigame (both Build 4).
    ///
    /// <para>Mirrors the <see cref="DevFishingInput"/>/<c>ClamDigger</c> dev scaffolds: a serialized drop
    /// point + hold provider, dev-keyed via the New Input System (legacy Input throws at runtime — memory),
    /// stood down under a modal dialogue (<see cref="InteractionGate"/>). Replaced by the InputService +
    /// the real place/haul UX later (ui-ux). Touches no sim state directly — it drives the
    /// <see cref="PlacedTrapService"/>, which owns determinism + save.</para>
    /// </summary>
    public sealed class DevTrapInput : MonoBehaviour
    {
        [Header("Wiring")]
        [Tooltip("The service that owns placed traps (place/haul/save). Required.")]
        [SerializeField] private PlacedTrapService _service;
        [Tooltip("Where a trap drops (the active boat / player). Defaults to this object's transform.")]
        [SerializeField] private Transform _dropPoint;
        [Tooltip("A GameObject carrying an IHold (the boat's ShipHold) a hauled catch lands into.")]
        [SerializeField] private GameObject _holdProvider;

        [Header("What to drop (greybox — a fixed baited trap for the dev loop)")]
        [Tooltip("The trap kind the dev drop places.")]
        [SerializeField] private TrapDef _trapDef;
        [Tooltip("The bait the dev drop loads (its FavorsSpeciesIds soft-weight the catch). Null = unbaited.")]
        [SerializeField] private BaitDef _bait;
        [Tooltip("The region id placed traps are tagged with (scene-per-region).")]
        [SerializeField] private string _regionId = "region.coddle_cove";

        [Header("Keys (dev only)")]
        [SerializeField] private Key _dropKey = Key.T;
        [SerializeField] private Key _haulKey = Key.Y;

        private IHold _hold;

        private void Awake()
        {
            if (_dropPoint == null) _dropPoint = transform;
            if (_holdProvider != null) _hold = _holdProvider.GetComponent<IHold>();
        }

        private void Update()
        {
            if (InteractionGate.IsBlocked) return;   // a modal dialogue owns the keys while up
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb[_dropKey].wasPressedThisFrame) DropTrap();
            if (kb[_haulKey].wasPressedThisFrame) HaulNearest();
        }

        /// <summary>Drop a baited trap at the drop point. Public so a test/tool can drive it without input.</summary>
        public PlacedTrap DropTrap()
        {
            if (_service == null || _trapDef == null)
            {
                Debug.LogWarning("[DevTrapInput] No service/trap wired.");
                return null;
            }
            Vector2 pos = _dropPoint != null ? (Vector2)_dropPoint.position : Vector2.zero;
            return _service.PlaceTrap(_trapDef, _bait, pos, _regionId);
        }

        /// <summary>Check/haul the nearest placed trap into the hold — logs the state + soak %, and on a ready
        /// trap resolves + lands the deterministic catch. Public so a test/tool can drive it without input.</summary>
        public bool HaulNearest()
        {
            if (_service == null) return false;
            EnsureHold();

            PlacedTrap best = Nearest(_dropPoint != null ? (Vector2)_dropPoint.position : Vector2.zero);
            if (best == null) { Debug.Log("[DevTrapInput] No traps down to check."); return false; }

            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0.0;
            Debug.Log($"[DevTrapInput] Nearest trap: {best.StateAt(now)} — soak {best.Progress01(now) * 100f:0}%.");

            CatchContext ctx = BuildContext();
            return _service.HaulTrap(best, _hold, in ctx);
        }

        private PlacedTrap Nearest(Vector2 from)
        {
            PlacedTrap best = null;
            float bestSqr = float.MaxValue;
            var live = _service.Live;
            for (int i = 0; i < live.Count; i++)
            {
                PlacedTrap t = live[i];
                if (t == null) continue;
                float sqr = ((Vector2)t.transform.position - from).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = t; }
            }
            return best;
        }

        private CatchContext BuildContext()
        {
            IGameClock clock = GameServices.Clock;
            IEnvironmentService env = GameServices.Environment;
            float tide = env != null ? env.Sample().TideHeight : 0f;
            float hour = clock != null ? clock.HourOfDay : 12f;
            Season season = clock != null ? clock.Season : Season.HighSummer;
            return new CatchContext(_regionId, tide, hour, season, Gear.Trap);
        }

        private void EnsureHold()
        {
            if (_hold == null && _holdProvider != null) _hold = _holdProvider.GetComponent<IHold>();
        }

        /// <summary>Wire the dev input in one call (tests / editor).</summary>
        public void Configure(PlacedTrapService service, Transform dropPoint, IHold hold,
                              TrapDef trapDef, BaitDef bait, string regionId)
        {
            _service = service;
            _dropPoint = dropPoint;
            _hold = hold;
            _trapDef = trapDef;
            _bait = bait;
            _regionId = regionId;
        }
    }
}
