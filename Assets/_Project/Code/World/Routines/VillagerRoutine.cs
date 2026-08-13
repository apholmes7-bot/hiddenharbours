using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>THE VILLAGER, LIVING</b> — the presentation half of the routine engine (P3, A Living Working
    /// Coast). Drop it on a placed islander and they keep their day: out at dawn, at their post through the
    /// morning, down to the store for their dinner, on the green at dusk, in through their own door at
    /// night.
    ///
    /// <para><b>Where they are is READ, never accumulated (CLAUDE.md rule 5).</b> Every frame this asks
    /// <see cref="RoutinePlan.SampleAt"/> for the pose at the clock's hour and writes it onto the
    /// transform. There is no state machine, no "walking toward" target, no integration and nothing saved —
    /// so a villager cannot drift, cannot get stuck, cannot be resynchronised wrongly by a pause or a
    /// timescale change, and appears mid-stride on the right leg when a region loads at 14:20. A paused
    /// clock stops the whole village, because a paused clock IS the same hour.</para>
    ///
    /// <para><b>Zero coupling to how they are drawn (rule 4).</b> It writes a position and nothing else.
    /// <see cref="IsoCharacterSprite"/> — the SAME component the player uses — reads motion off its own
    /// transform and picks the direction row and the gait itself, so a villager walks with no reference
    /// between the two beyond the one <c>GetComponent</c> here, and a villager with no baked body simply
    /// slides their fallback sprite around. The only thing stated rather than measured is the FACING while
    /// standing still, because a station has a stance ("Basil watches the water") and motion has nothing
    /// to say about it.</para>
    ///
    /// <para><b>What is throttled and what is not.</b> Sampling the plan is an index scan over a handful of
    /// floats plus a polyline walk — cheaper than the bookkeeping it would take to skip it — and it must
    /// run every frame or the walk would visibly step. What IS throttled is the INTERIOR read: whether the
    /// player is inside the building this villager is inside is a component read, it only matters at the
    /// coarse grain of "is she visible", and it is the one thing here that touches another object
    /// (rule 7).</para>
    ///
    /// <para><b>Null-safe throughout.</b> No routine, no station table, no lane network, no clock, or a
    /// routine whose content does not resolve: this component reports once and goes inert, leaving the
    /// villager standing exactly where the builder placed them. An island whose routines are half-authored
    /// looks like the island did before routines existed — which is the only acceptable failure mode for a
    /// layer that moves people around.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VillagerRoutine : MonoBehaviour
    {
        /// <summary>How often the interior/visibility read happens, in real seconds. Presentation plumbing,
        /// not owner feel — a quarter second is the rate the ambient fleet plans at, and a villager
        /// appearing a fifth of a second after you step through her door is not perceptible.</summary>
        public const float ShelterCheckSeconds = 0.25f;

        [Header("Content")]
        [Tooltip("Whose day this is. Null leaves this component completely inert.")]
        [SerializeField] private RoutineDef _routine;

        [Tooltip("The region's station table — where the named places are. Handed in by the builder, the " +
                 "same way WorldInteractor takes its player, so the World module never searches the scene.")]
        [SerializeField] private RoutineStations _stations;

        [Tooltip("The region's lane network — the paths they keep to.")]
        [SerializeField] private RoutineLanes _lanes;

        [Header("Presentation")]
        [Tooltip("The renderer switched off while this villager is inside a building the player is not in. " +
                 "Left empty it is taken from this object, which is where a placed islander's is.")]
        [SerializeField] private SpriteRenderer _renderer;

        private RoutinePlan _plan;
        private BuildingInterior[] _blockInteriors = System.Array.Empty<BuildingInterior>();
        private IsoCharacterSprite _iso;
        private Interactable _talkable;
        private int _plannedSeed;
        private bool _planned;
        private bool _reported;
        private float _nextShelterCheck;
        private bool _headingHeld;

        /// <summary>The routine this villager keeps. For tests and tooling.</summary>
        public RoutineDef Routine => _routine;

        /// <summary>The resolved plan, or null while inert. For tests and tooling.</summary>
        public RoutinePlan Plan => _plan;

        /// <summary>The pose last written onto the transform. For tests and tooling.</summary>
        public RoutinePose Pose { get; private set; }

        /// <summary>True while this villager is actually keeping a routine (as opposed to standing where
        /// the builder left them because something did not resolve).</summary>
        public bool IsLiving => _plan != null;

        /// <summary>Wire this villager up in one call (the region builder).</summary>
        public void Configure(RoutineDef routine, RoutineStations stations, RoutineLanes lanes,
                              SpriteRenderer renderer)
        {
            _routine = routine;
            _stations = stations;
            _lanes = lanes;
            _renderer = renderer;
            _planned = false;
            _reported = false;
        }

        void Awake()
        {
            if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
        }

        void OnEnable()
        {
            // Re-plan on every enable rather than caching across one: a region that unloads and comes back
            // may come back with a different world seed (a new game), and a stale plan would keep
            // yesterday's jitter. The build is one allocation and happens once per activation.
            _planned = false;
            _reported = false;
            _nextShelterCheck = 0f;
            _headingHeld = false;
        }

        void OnDisable()
        {
            // Hand the facing back, so a villager who is disabled mid-stand does not come back pinned to a
            // stance nobody asked for (IsoCharacterSprite keeps the held direction, which is what we want).
            if (_headingHeld && _iso != null) _iso.ReleaseHeading();
            _headingHeld = false;

            // Never leave them hidden. A villager switched off inside a house would come back invisible in
            // a scene the player is standing outside of, and the only symptom would be a missing person —
            // and an un-talkable one, which is worse because it reads as broken dialogue.
            if (_renderer != null) _renderer.enabled = true;
            if (_talkable != null) _talkable.enabled = true;
        }

        void Update()
        {
            IGameClock clock = GameServices.Clock;
            if (clock == null) return;

            if (!_planned) TryPlan();
            if (_plan == null) return;

            float secondsPerGameHour = GameServices.SecondsPerDay / RoutineSchedule.HoursPerDay;
            RoutinePose pose = _plan.SampleAt(clock.HourOfDay, secondsPerGameHour);
            Pose = pose;

            // The position. Z is left alone — the Y-sort owns the draw order and nothing here is 3D.
            Vector3 p = transform.position;
            transform.position = new Vector3(pose.Position.x, pose.Position.y, p.z);

            ApplyFacing(pose);

            if (Time.unscaledTime >= _nextShelterCheck)
            {
                _nextShelterCheck = Time.unscaledTime + ShelterCheckSeconds;
                ApplyShelter(pose);

                // A new game can replace the environment service without this component ever being
                // disabled, and the seed is what her departures are jittered from — so a stale plan would
                // keep yesterday's world's few minutes, permanently and invisibly. One int compare on the
                // throttled tick is the whole cost of not having to think about it again.
                IEnvironmentService env = GameServices.Environment;
                if (env != null && env.WorldSeed != _plannedSeed) _planned = false;
            }
        }

        /// <summary>
        /// Build the plan, once the services it needs are up. The world seed only reaches the plan as this
        /// person's departure jitter, but it has to be the REAL one: planning against a seed of zero
        /// because <c>GameServices.Environment</c> had not registered yet would give the whole village the
        /// wrong few minutes, permanently and invisibly. So the build waits for the seed and is redone if it
        /// ever changes.
        /// </summary>
        void TryPlan()
        {
            IEnvironmentService env = GameServices.Environment;
            if (env == null) return;   // not an error — the composition root has not finished

            _planned = true;
            _plannedSeed = env.WorldSeed;
            _plan = RoutinePlanner.Build(_routine, _stations, _lanes, _plannedSeed,
                                         out _blockInteriors, out string problem);

            // The skin and the talk point are looked up HERE rather than in Awake: a builder (or a test) may
            // add either after this component, and Awake would have cached the null forever. This is the
            // become-live moment and it happens once, so it is also the only place a GetComponent belongs
            // (rule 7).
            if (_iso == null) _iso = GetComponent<IsoCharacterSprite>();
            if (_talkable == null) _talkable = GetComponent<Interactable>();

            if (_plan == null && !_reported)
            {
                _reported = true;
                Debug.LogWarning(
                    $"[VillagerRoutine] {name} is standing still: {problem}. They keep the spot the " +
                    "builder placed them on, which is what an islander without a routine already does — " +
                    "fix the content rather than the placement.", this);
            }
        }

        /// <summary>
        /// While walking, the facing follows the walk — which <see cref="IsoCharacterSprite"/> would do by
        /// itself from motion, except on the FIRST frame of a leg (a mid-leg spawn on region load has no
        /// previous position to difference, and would face North for one frame). While standing, the
        /// station's own stance is stated, because motion has nothing to say about which way somebody
        /// looking out at the water is turned.
        /// </summary>
        void ApplyFacing(in RoutinePose pose)
        {
            if (_iso == null) return;
            _iso.HoldHeading(pose.HeadingDegrees);
            _headingHeld = true;
        }

        /// <summary>
        /// The reveal: an occupant of an interior is drawn only while the PLAYER shares that interior.
        /// <see cref="BuildingInterior.IsInside"/> already means exactly that (it tracks the occupant the
        /// builder handed it), so this asks rather than re-deciding — one question, one owner.
        ///
        /// <para><b>⭐ And she stops being TALKABLE too, not just invisible.</b> A home station is about a
        /// metre past a door the player can walk right up to, and <c>WorldInteractor</c> resolves by plain
        /// proximity — so hiding only the sprite would leave an "E: Talk" prompt on an invisible neighbour
        /// through her own wall, and she would answer. The <see cref="Interactable"/> goes with the
        /// renderer, and the interactor skips one that is switched off.</para>
        /// </summary>
        void ApplyShelter(in RoutinePose pose)
        {
            BuildingInterior interior = InteriorFor(pose);
            bool visible = interior == null || interior.IsInside;

            if (_renderer != null && _renderer.enabled != visible) _renderer.enabled = visible;
            if (_talkable != null && _talkable.enabled != visible) _talkable.enabled = visible;
        }

        /// <summary>Which building's threshold this pose is behind, or null out in the world.</summary>
        BuildingInterior InteriorFor(in RoutinePose pose)
        {
            int n = _blockInteriors.Length;
            if (n == 0 || pose.BlockIndex < 0) return null;

            switch (pose.Shelter)
            {
                case RoutineShelter.InsideDestination:
                    return _blockInteriors[pose.BlockIndex % n];
                case RoutineShelter.InsideOrigin:
                    return _blockInteriors[(pose.BlockIndex - 1 + n) % n];
                default:
                    return null;
            }
        }

#if UNITY_EDITOR
        /// <summary>Draw this villager's whole day in the Scene view — every leg, so the owner can see
        /// where the lanes actually send them without playing a full day to find out.</summary>
        void OnDrawGizmosSelected()
        {
            if (_plan == null) return;
            for (int i = 0; i < _plan.BlockCount; i++)
            {
                Gizmos.color = i % 2 == 0
                    ? new Color(0.40f, 0.85f, 0.95f, 0.9f)
                    : new Color(0.95f, 0.55f, 0.85f, 0.9f);
                int start = _plan.RouteStart[i], count = _plan.RouteCount[i];
                for (int k = start; k + 1 < start + count; k++)
                    Gizmos.DrawLine(_plan.Waypoints[k], _plan.Waypoints[k + 1]);
            }
        }
#endif
    }
}
