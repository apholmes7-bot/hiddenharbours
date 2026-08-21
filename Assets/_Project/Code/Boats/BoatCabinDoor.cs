using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>The way into a boat's cabin</b> — her door, as a candidate for the one interact verb. Stand at
    /// the threshold off her deck and press: the leaf runs its baked cue, and at the end of it the
    /// exterior gives way to the room. Press again from inside and the same cue runs backwards.
    ///
    /// <para><b>Press, not walk-on</b> — <c>InteriorStair</c>'s reasoning, and it applies harder here. A
    /// trigger volume on a threshold would fire on any pass across it, including the one where you were
    /// walking to the rail with a full tub; a cabin that opened because you clipped its doorway on a
    /// rolling deck is the least cozy thing a boat can do (P5). And a press is what a door CUE is for:
    /// there is a moment the leaf starts moving, and it is the one the player chose.</para>
    ///
    /// <para><b>It surfaces through the seam that already exists, and adds no second prompt path.</b>
    /// Registering as an <see cref="IInteractable"/> is the whole of it: <see cref="InteractVerb"/>
    /// resolves the candidate, publishes <see cref="InteractOfferChanged"/> on the
    /// <see cref="InteractOfferSource.Fixture"/> slot from this component's own
    /// <see cref="VerbLabel"/>, and the popup draws it. The modal gate is checked there too
    /// (<see cref="InteractionGate.IsBlocked"/> guards both the dispatch and the offer), so re-checking
    /// it here would be a second, lagging opinion about a decision already made — the precise failure
    /// <see cref="InteractCandidateChanged"/>'s remarks forbid.</para>
    ///
    /// <para><b>The cue is DECLARED, not invented.</b> <see cref="BoatInteriorDoor.CueFrames"/> and
    /// <see cref="BoatInteriorDoor.CueMillisecondsPerFrame"/> come off the sidecar the kit measured (8
    /// frames at ~70 ms across the whole fleet), and <see cref="BoatInteriorDoor.CueReversedOnExit"/>
    /// says the close is the open played backwards. Nothing here re-derives any of it — that is the
    /// hand-transcribed-constant failure the sidecars exist to close. A door whose def states no cue
    /// simply opens at once, which is the honest picture of a threshold nobody has measured.</para>
    ///
    /// <para><b>⚠ The shipped sheets bake at <c>doorOpen: 0</c> only</b> (the interiors contract), so
    /// there is no leaf artwork to step through yet. <see cref="CueFrame"/> is published for whoever
    /// draws it; until then the cue is what it honestly is — the time the door takes — and the swap lands
    /// at the end of it rather than under the player's hand.</para>
    ///
    /// <para><b>Where this component stands is placement's business, not this class's.</b> Its
    /// <see cref="WorldPosition"/> is its own transform, exactly as <c>InteriorStair</c>'s is: the def's
    /// <see cref="BoatInteriorDoor.ThresholdPoint"/> is hull-local metres and turning that into a place on
    /// a drawn hull is the placement pass's projection, not a second one done here every frame.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BoatCabinDoor : MonoBehaviour, IInteractable
    {
        [Tooltip("The cabin this door opens. Required — a door with no interior is INERT rather than " +
                 "broken, which is the right answer for a hull nobody has measured yet.")]
        [SerializeField] private BoatInterior _interior;

        [Tooltip("Stable id, unique among live interactables (CLAUDE.md §5). Follow the fixture " +
                 "convention: fixture.boat.<hull>.cabin_door.")]
        [SerializeField] private string _id = "fixture.boat.cabin_door";

        [Tooltip("Which of the def's doors this is: -1 = the main THRESHOLD, 0+ = an index into " +
                 "AdditionalDoors (the 90's skylounge slider onto her aft deck). Every hull with one " +
                 "way in leaves this at -1.")]
        [SerializeField] private int _additionalDoorIndex = -1;

        [Tooltip("How close (m) you must stand. Tighter than a shore fixture's on purpose: a doorway is " +
                 "a specific place on a small deck, and a generous radius would have it outranking the " +
                 "hauler beside it.")]
        [SerializeField, Min(0f)] private float _reachMeters = 1.2f;

        [Tooltip("What the popup says on the way in. Present tense, no key name — there is one interact " +
                 "key and naming it is the plain-text habit the 2026-08-19 ruling retired.")]
        [SerializeField] private string _enterLabel = "Go below";

        [Tooltip("…and on the way out.")]
        [SerializeField] private string _exitLabel = "Come out";

        /// <summary>How long the cue has been running, seconds. Negative when no cue is running, so 0 is
        /// a legal first frame rather than a sentinel.</summary>
        private float _cueElapsed = -1f;

        /// <summary>Which way the cue that is running will resolve. Latched at the press so a cue cannot
        /// change its mind halfway because the interior was moved under it.</summary>
        private bool _cueEnters;

        /// <summary>The level the running cue will land on — resolved from the door's own sill height at
        /// the press, and held so the answer cannot drift mid-cue.</summary>
        private int _cueLevel;

        // ---- what this door IS ----------------------------------------------------------------

        /// <summary>The door this component speaks for: the def's main threshold, or one of its
        /// additional doors. Null when there is no def, no door, or the index names nothing — every path
        /// here treats that as "there is no door", which is a hull with no measured way in.</summary>
        public BoatInteriorDoor Door
        {
            get
            {
                BoatInteriorDef def = _interior != null ? _interior.Def : null;
                if (def == null) return null;
                if (_additionalDoorIndex < 0) return def.Door;
                if (def.AdditionalDoors == null || _additionalDoorIndex >= def.AdditionalDoors.Length)
                    return null;
                return def.AdditionalDoors[_additionalDoorIndex];
            }
        }

        /// <summary>The cabin this door opens. Read by tests and by whoever places her.</summary>
        public BoatInterior Interior => _interior;

        /// <summary>
        /// <b>How long the leaf takes</b>, seconds — the sidecar's own frame count times its own
        /// milliseconds per frame, and 0 for a door whose def states no cue. Never a constant here: the
        /// kit bakes 8 frames at ~70 ms today and a re-measure must reach the game without a second edit.
        /// </summary>
        public float CueSeconds
        {
            get
            {
                BoatInteriorDoor door = Door;
                if (door == null || door.CueFrames <= 0 || door.CueMillisecondsPerFrame <= 0f) return 0f;
                return door.CueFrames * door.CueMillisecondsPerFrame * 0.001f;
            }
        }

        /// <summary>True while the leaf is moving. The door refuses a second press during it — a doorway
        /// you can stutter is a doorway that ends up in both states at once.</summary>
        public bool IsCueing => _cueElapsed >= 0f;

        /// <summary>How far through the cue the leaf is, 0→1. 0 when nothing is running.</summary>
        public float CueProgress01
        {
            get
            {
                if (!IsCueing) return 0f;
                float seconds = CueSeconds;
                return seconds <= 0f ? 1f : Mathf.Clamp01(_cueElapsed / seconds);
            }
        }

        /// <summary>
        /// <b>Which baked cue frame the leaf is on</b>, or −1 when nothing is running — published for
        /// whoever draws the door, so the timing lives in one place. Counts DOWN through the same frames
        /// on the way out when the def says <see cref="BoatInteriorDoor.CueReversedOnExit"/>, which is
        /// every door in this kit; a def that says otherwise plays it forward both ways.
        /// </summary>
        public int CueFrame
        {
            get
            {
                BoatInteriorDoor door = Door;
                if (!IsCueing || door == null || door.CueFrames <= 0) return -1;

                int last = door.CueFrames - 1;
                int forward = Mathf.Clamp(Mathf.FloorToInt(CueProgress01 * door.CueFrames), 0, last);
                bool reverse = !_cueEnters && door.CueReversedOnExit;
                return reverse ? last - forward : forward;
            }
        }

        /// <summary>Which way the next press would go: in when the occupant is out, out when they are
        /// in. Read by <see cref="VerbLabel"/>, so the popup and the press can never disagree.</summary>
        public bool WouldEnter => _interior == null || !_interior.IsInside;

        // ---- the interact seam ----------------------------------------------------------------

        /// <inheritdoc/>
        public string Id => _id;

        /// <inheritdoc/>
        public Vector2 WorldPosition => transform.position;

        /// <inheritdoc/>
        public float ReachMeters => _reachMeters;

        /// <inheritdoc/>
        public int Priority => InteractPriority.Fixture;

        /// <summary>From her deck. A cabin door is worked by someone standing on the boat — off the
        /// wharf you are boarding, which is the switcher's press and outranks this one anyway.</summary>
        public InteractContext Contexts => InteractContext.OnDeck;

        /// <summary>Facing is not required: you open a door by standing at it. Matches every other
        /// fixture you operate by walking up to (<c>InteriorStair</c>, <c>WetBucketPoint</c>).</summary>
        public bool RequiresFacing => false;

        /// <summary>Which way this press goes, said as the player would say it. Derived from the state
        /// rather than held as a third field, so the words cannot drift from the action (rule 6).</summary>
        public string VerbLabel => WouldEnter ? _enterLabel : _exitLabel;

        /// <summary>
        /// Available when there is an inside to reach, the swap that reveals it can actually complete,
        /// and no leaf is already moving.
        ///
        /// <para>All three are genuine "not a thing to act on right now" rather than refusals with a
        /// message: a hull with no measured interior has no door to press, a hull whose swap cannot
        /// complete has no cabin to show yet, and a door mid-cue has already been pressed. None of them
        /// is loud, and a hull that gains a def or an exterior half later gains her door with no other
        /// change.</para>
        ///
        /// <para><b>⭐ The first two are ONE named policy — <see cref="BoatInteriorEntryPolicy.MayOffer"/>
        /// — and deliberately not spelled out here.</b> Whether a cabin may be entered before the
        /// exterior half of its swap exists is a question the owner is settling on rendered evidence,
        /// with three named outcomes; a policy that is going to be re-decided belongs somewhere a person
        /// can find and change without reading a door. What that predicate protects is the ADR's own
        /// invariant: the interior takes only <c>InteriorRockScale</c> of the hull's rock, which is safe
        /// ONLY because the two are never drawn together. <b>Today it means the fleet wires up and stays
        /// silent, and each cabin lights the moment its own exterior half arrives</b> — no second
        /// switch, no migration, and no window in which a half-built cabin is enterable.</para>
        /// </summary>
        public bool IsAvailable =>
            BoatInteriorEntryPolicy.MayOffer(_interior) && Door != null && !IsCueing;

        /// <inheritdoc/>
        public void Interact(in InteractActor actor) => TryUse();

        private void OnEnable() => Interactables.Register(this);

        /// <summary>
        /// Relinquish the scene-scoped candidacy — the register-on-enable/relinquish-on-disable contract
        /// <see cref="Interactables"/> states and every other registrant keeps.
        ///
        /// <para>⚠️ Not a counter-example to the OnDisable law: that law is about CORE SERVICES, which
        /// must be unregistered in <c>OnDestroy</c> guarded on still owning them, because root-toggling
        /// is how a region hop works and a service dropped there dies mid-crossing. A candidate is the
        /// opposite case by design — a door on a boat in a region you are not in SHOULD offer nothing,
        /// and <see cref="Interactables.Unregister"/> is itself guarded (it removes only what is there).
        /// The cabin's own state deliberately survives this: see <see cref="BoatInterior"/>.</para>
        /// </summary>
        private void OnDisable() => Interactables.Unregister(this);

        private void Update() => Tick(Time.deltaTime);

        // ---- the press and the cue ------------------------------------------------------------

        /// <summary>
        /// Wire this door from the builder. Public so the placement pass never reaches a serialized
        /// field, and so a test can stand one up without a scene.
        /// </summary>
        public void Configure(BoatInterior interior, string id, int additionalDoorIndex,
                              float reachMeters, string enterLabel, string exitLabel)
        {
            _interior = interior;
            _id = id;
            _additionalDoorIndex = additionalDoorIndex;
            _reachMeters = Mathf.Max(0f, reachMeters);
            _enterLabel = enterLabel ?? "";
            _exitLabel = exitLabel ?? "";
            _cueElapsed = -1f;
        }

        /// <summary>
        /// <b>Work the door.</b> Starts the cue and returns whether it started — false when there is no
        /// interior, no door, or a leaf already moving.
        ///
        /// <para>Public so a test drives it directly. It has to be: a PlayMode test cannot deliver a
        /// virtual keypress in this project, so the component API IS the way in, and driving the press
        /// here exercises exactly the path <see cref="Interact"/> takes.</para>
        ///
        /// <para>A door with no cue time resolves on the same call — the swap lands immediately, which
        /// is the correct behaviour for an unmeasured threshold and keeps "press to enter" true whether
        /// or not the art exists.</para>
        /// </summary>
        public bool TryUse()
        {
            if (!IsAvailable) return false;

            BoatInteriorDoor door = Door;
            _cueEnters = WouldEnter;
            // The sill states which level you walk in ONTO (the sport fishers' is at mezzanine height, so
            // you walk in level). Resolved once, at the press, so the answer cannot drift mid-cue.
            _cueLevel = _cueEnters ? Mathf.Max(0, _interior.LevelIndexAtHeight(door.ThresholdPoint.z)) : 0;
            _cueElapsed = 0f;

            // ⭐ THE LOAD, HERE AND NOWHERE EARLIER. The cabin's sheets are megabytes and they are not
            // referenced from anything a hull pulls in on spawn, so this press is the first moment they
            // are wanted — and the leaf's own baked cue is the window that hides the cost. An
            // unenterable cabin never reaches this line, because IsAvailable declined the press.
            if (_cueEnters) _interior.EnsureCells();

            if (CueSeconds <= 0f) Resolve();
            return true;
        }

        /// <summary>
        /// Advance a running cue by <paramref name="deltaSeconds"/>, resolving it when the leaf finishes.
        ///
        /// <para>⚠️ <b>Public and time-driven on purpose.</b> A PlayMode test that stepped frames would be
        /// asserting against the machine it happens to run on — frames are not time — so the cue is
        /// advanced by seconds and a test hands it the seconds it means. <see cref="Update"/> is a
        /// one-line caller for the same reason <c>BoatWaveMotion.Tick</c> is.</para>
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (!IsCueing) return;
            if (deltaSeconds > 0f) _cueElapsed += deltaSeconds;
            if (_cueElapsed >= CueSeconds) Resolve();
        }

        /// <summary>
        /// The leaf has finished: make the swap the press asked for, and stop cueing whatever the answer
        /// was.
        ///
        /// <para>The interior may legitimately refuse (an unusable level, a def swapped under a moving
        /// door). The cue still ends — a door that stayed open because the room behind it declined would
        /// be a door that can never be pressed again.</para>
        /// </summary>
        private void Resolve()
        {
            _cueElapsed = -1f;
            if (_interior == null) return;

            if (_cueEnters) _interior.TryEnter(_cueLevel);
            else _interior.TryExit();
        }
    }
}
