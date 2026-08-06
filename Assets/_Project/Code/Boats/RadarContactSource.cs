using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// THE RADAR'S EYES — the one registered <see cref="IRadarContacts"/> producer (ADR 0025 S5): it
    /// gathers everything on the water that throws a return and answers the scope's query.
    ///
    /// <para><b>Why a composer and not "the fleet implements the interface".</b> Two sources feed a
    /// radar and they have different lifetimes. The ambient fleet
    /// (<see cref="AmbientFleetPresenter"/>) installs ONLY when a region has fleets authored — it
    /// returns early with none — so hanging the seam off it would mean that in a region with no NPC
    /// boats the player's OWN trap buoys vanish from the scope too. The player's gear is not the
    /// fleet's business and must not share its gate. This host installs unconditionally and asks each
    /// source in turn.</para>
    ///
    /// <para><b>The player's buoys come off the Core signals, not off Fishing.</b>
    /// <see cref="TrapPlaced"/>/<see cref="TrapRemoved"/> carry positions, so the authoritative gear
    /// service (<c>PlacedTrapService</c>) is never referenced and rule 4 holds — the exact path
    /// <see cref="AmbientFleetPresenter"/> already uses for its polite berth. The two subscriptions are
    /// deliberately separate rather than shared: each host owns the set it needs, and neither depends on
    /// the other existing.</para>
    ///
    /// <para><b>Land is NOT gathered here.</b> The coast is a function of the terrain height map, not a
    /// list somebody keeps, and the instrument samples it directly — see <c>RadarLandEcho</c> for the
    /// waterline ruling and <see cref="IRadarContacts"/> for why publishing thousands of point echoes
    /// per sweep would be the wrong shape.</para>
    ///
    /// <para><b>Reads, never state</b> (rule 5). Everything answered here is live position data another
    /// system already owns and draws. Nothing radar-related is stored, nothing is saved, and the
    /// deterministic half (the fleet) stays a closed-form function of <c>(worldSeed, gameTime)</c> — so
    /// the same seed at the same instant gives the same scope picture.</para>
    ///
    /// <para><b>Self-installing, removable (ADR 0011).</b> A <see cref="RuntimeInitializeOnLoadMethod"/>
    /// spawns one persistent host per play session, so every already-built scene grows the seam with no
    /// builder re-run. Headless-safe: with nothing to gather it answers zero, which is the honest quiet
    /// sea rather than an error.</para>
    ///
    /// <para><b>Perf (rule 7).</b> The read path appends structs into the caller's list and allocates
    /// nothing; the player-buoy set is a dictionary maintained by two rare events, never a per-frame
    /// scan. The scope's own change-detection decides how often this is called at all.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RadarContactSource : MonoBehaviour, IRadarContacts
    {
        private static RadarContactSource _instance;

        // The player's placed gear, keyed by instance id off the Core signals — the same shape
        // AmbientFleetPresenter keeps for its own reasons, owned separately so neither gates the other.
        private readonly Dictionary<string, Vector2> _playerBuoys = new();

        /// <summary>The one live host, or null before it installs. Exposed so a test can drive the host
        /// that is actually registered rather than a second copy — a duplicate destroys itself.</summary>
        public static RadarContactSource Instance => _instance;

        /// <summary>How many player trap buoys this host is tracking. Exposed so a test can prove the
        /// signals landed rather than inferring it from a scope raster.</summary>
        public int PlayerBuoyCount => _playerBuoys.Count;

        /// <summary>What one of the player's trap buoys is worth as a return — the same value the fleet
        /// gives its own (<see cref="AmbientFleetPresenter.BuoyEchoSize"/>). Deliberately identical:
        /// a radar cannot tell whose gear it is looking at, and drawing the player's brighter would be
        /// the instrument lying to make the game easier.</summary>
        public const float PlayerBuoyEchoSize = AmbientFleetPresenter.BuoyEchoSize;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;
            var go = new GameObject("[RadarContactSource]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<RadarContactSource>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            EventBus.Subscribe<TrapPlaced>(OnTrapPlaced);
            EventBus.Subscribe<TrapRemoved>(OnTrapRemoved);
            GameServices.RadarContacts = this;
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<TrapPlaced>(OnTrapPlaced);
            EventBus.Unsubscribe<TrapRemoved>(OnTrapRemoved);
            // Hand the seam back to the null object rather than leaving a destroyed producer behind —
            // the FishSchools discipline. ReferenceEquals, because a comparison against a destroyed
            // UnityEngine.Object through the interface would go through the overloaded ==.
            if (ReferenceEquals(GameServices.RadarContacts, this)) GameServices.RadarContacts = null;
            if (_instance == this) _instance = null;
        }

        private void OnTrapPlaced(TrapPlaced e)
        {
            if (string.IsNullOrEmpty(e.InstanceId)) return;
            _playerBuoys[e.InstanceId] = new Vector2(e.PosX, e.PosY);
        }

        private void OnTrapRemoved(TrapRemoved e)
        {
            if (string.IsNullOrEmpty(e.InstanceId)) return;
            _playerBuoys.Remove(e.InstanceId);
        }

        /// <inheritdoc/>
        public int ContactsAt(Vector2 worldPos, float rangeMetres, double gameSeconds,
                              List<RadarContact> into)
        {
            into?.Clear();                       // the seam's contract: cleared first, then refilled
            if (into == null || rangeMetres <= 0f) return 0;

            int n = 0;

            // The ambient fleet — vessels and their gear. Null when no region authored a fleet, which is
            // a quiet sea and not an error.
            AmbientFleetPresenter fleet = AmbientFleetPresenter.Instance;
            if (fleet != null) n += fleet.RadarContactsInto(worldPos, rangeMetres, into);

            // The player's own gear. A trap you set IS a radar target — finding your string again in
            // poor visibility is the instrument's whole reason to exist on a boat this size.
            float rangeSqr = rangeMetres * rangeMetres;
            foreach (KeyValuePair<string, Vector2> kv in _playerBuoys)
            {
                if ((kv.Value - worldPos).sqrMagnitude > rangeSqr) continue;
                into.Add(RadarContact.Still(kv.Value, RadarEchoKind.Buoy, PlayerBuoyEchoSize));
                n++;
            }

            return n;
        }

        /// <summary>Forget every tracked buoy (region teardown / tests). The gear itself is untouched —
        /// this only drops what the SCOPE is tracking, which is rebuilt from the signals.</summary>
        public void ClearPlayerBuoys() => _playerBuoys.Clear();
    }
}
