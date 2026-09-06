using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>The way into a boat's cabin</b> — her door, as a thing with a STATE and a threshold you walk
    /// across. Stand at it off her deck and press: the leaf runs its baked cue, and at the end of it the
    /// door is standing open. From then on her doorway is simply a place on the floor you walk through,
    /// either way, as often as you like. Press again — from either side — and the cue runs backwards.
    ///
    /// <para><b>⭐ THE RULING THIS IMPLEMENTS (owner, 2026-08-28, verbatim):</b> <i>"a door is closed
    /// until its opened, a player can walk through freely when opened, a player can close/open when
    /// inside."</i> Three sentences, three properties: <see cref="IsOpen"/> starts false and only a press
    /// changes it; <see cref="TryWalkThrough"/> is the free passage and takes no press at all; and the
    /// press works identically on both sides because <see cref="WouldOpen"/> reads the LEAF's state and
    /// never the occupant's.</para>
    ///
    /// <para><b>Press for the LEAF, walk for the PASSAGE — and the split is the whole of the fix.</b>
    /// This component used to resolve its cue by calling <c>TryEnter</c>/<c>TryExit</c>, so every single
    /// crossing of the doorway cost a press and 560 ms of animation. The owner met that in the
    /// 2026-09-04 dawn playtest — <i>"walking in and out of cabin isnt seamless with door open"</i> —
    /// which is exactly right: an open door that still charges you a press is not open. The cue is what
    /// moving the leaf costs, once; walking through a hole in a wall is worth nothing, and now costs
    /// nothing.</para>
    ///
    /// <para><b>This is not a trigger volume, and the distinction is the state.</b> <c>InteriorStair</c>'s
    /// objection to walk-on entry was a threshold that fires on any pass across it — including the one
    /// where you were walking to the rail with a full tub, which is the least cozy thing a boat can do
    /// (P5). That objection is answered by <see cref="IsOpen"/> rather than by a press: a shut door is a
    /// wall, and it takes a deliberate press to stop being one. Once the player has made that choice, the
    /// doorway behaves like a doorway.</para>
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
    /// there is no leaf artwork to step through yet, and the 2026-08-28 charter HELD the 8-frame bake
    /// pending the full-mesh ruling. <see cref="CueFrame"/> and <see cref="IsOpen"/> are published for
    /// whoever draws it; until then the cue is what it honestly is — the time the door takes — and the
    /// state it lands on is a fact about the boat whether or not a pixel says so yet.</para>
    ///
    /// <para><b>⚠ A door with no MEASURED opening keeps the old press-through.</b> The passage is the
    /// doorway's own <see cref="BoatInteriorDoor.ClearWidthMeters"/> (see
    /// <see cref="BoatCabinThreshold"/>), and a sidecar that never measured one has not described a hole
    /// anybody could walk through. Rather than leave such a hull with a door that opens onto nothing, the
    /// press on her resolves the way it always did — straight into the room — and says so once on the
    /// console. No shipped hull is in that state: every measured interior in the fleet states a width
    /// between 0.49 m and 1.40 m.</para>
    ///
    /// <para><b>Where this component stands is placement's business, not this class's.</b> Its
    /// <see cref="WorldPosition"/> is its own transform, exactly as <c>InteriorStair</c>'s is: the def's
    /// <see cref="BoatInteriorDoor.ThresholdPoint"/> is hull-local metres and turning that into a place on
    /// a drawn hull is the placement pass's projection, not a second one done here every frame. The
    /// walk-through is asked in the HULL frame for the same reason from the other end — see
    /// <see cref="BoatCabinThreshold"/>.</para>
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

        [Tooltip("What the popup says while the leaf is shut. Present tense, no key name — there is one " +
                 "interact key and naming it is the plain-text habit the 2026-08-19 ruling retired.")]
        [SerializeField] private string _openLabel = "Open the door";

        [Tooltip("…and while it is standing open.")]
        [SerializeField] private string _closeLabel = "Close the door";

        /// <summary>How long the cue has been running, seconds. Negative when no cue is running, so 0 is
        /// a legal first frame rather than a sentinel.</summary>
        private float _cueElapsed = -1f;

        /// <summary>Which way the cue that is running will resolve — true when the leaf is OPENING.
        /// Latched at the press so a cue cannot change its mind halfway because the interior was moved
        /// under it.</summary>
        private bool _cueOpens;

        /// <summary>⚠ The press-through fallback's only state (see the class remarks): the level a door
        /// with no measured opening will land the player on, resolved from her own sill height at the
        /// press and held so the answer cannot drift mid-cue.</summary>
        private int _cueLevel;

        /// <summary>
        /// <b>The latch that makes one approach one crossing.</b> Cleared by a crossing, and set again
        /// only once the walker is measurably CLEAR of the doorway
        /// (<see cref="BoatCabinThreshold.IsClearOfBand"/>).
        ///
        /// <para><b>⚠ It lives HERE, on the door, and not on either walker — that is the point.</b> The
        /// sole and the deck are two frames either side of ONE hole in ONE wall: a walker who crosses in
        /// lands, by construction, a centimetre from the threshold she just crossed, and a latch owned by
        /// the frame she landed in would arrive armed and put her straight back out. One doorway, one
        /// latch.</para>
        ///
        /// <para>It starts DISARMED, which is the safe seed for the case that actually happens: the
        /// arrival opens with the player already standing in Armand's doorway (a threshold is on the
        /// sole's edge by construction), and an armed latch would walk her out of his cabin on the first
        /// frame of a new game.</para>
        /// </summary>
        private bool _passageArmed;

        /// <summary>True once the no-band fallback has been named on the console — said once per door and
        /// not once per press, because a warning on a press path is a warning in a loop.</summary>
        private bool _saidTheDoorHasNoBand;

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
        /// <b>Is the leaf standing open?</b> The ruling's first sentence — <i>"a door is closed until its
        /// opened"</i> — so this is false on a door nobody has touched, and false again on the next load.
        ///
        /// <para><b>Runtime-transient, deliberately.</b> A reload shuts every door: cheap, deterministic,
        /// and nobody's save grows a row per doorway. If the owner ever wants doors remembered that is
        /// his later call, and it is a save-format change rather than this component's business.</para>
        /// </summary>
        public bool IsOpen { get; private set; }

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
        /// you can stutter is a doorway that ends up in both states at once — and the threshold is shut
        /// for the duration, because a leaf halfway across is not an opening you walk through.</summary>
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
        /// on the way shut when the def says <see cref="BoatInteriorDoor.CueReversedOnExit"/>, which is
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
                bool reverse = !_cueOpens && door.CueReversedOnExit;
                return reverse ? last - forward : forward;
            }
        }

        /// <summary>Which way the next PRESS would move the leaf. Read by <see cref="VerbLabel"/>, so the
        /// popup and the press can never disagree — and it deliberately never asks where the occupant is,
        /// which is what makes the press behave identically from both sides (the ruling's third
        /// sentence).</summary>
        public bool WouldOpen => !IsOpen;

        /// <summary>Which way a WALK through this doorway would go: in when the occupant is out, out when
        /// they are in. The direction test the passage takes, kept apart from <see cref="WouldOpen"/> now
        /// that moving the leaf and crossing the threshold are two different acts.</summary>
        public bool WouldEnter => _interior == null || !_interior.IsInside;

        /// <summary>True when this doorway has a measured opening and can therefore be walked through at
        /// all. False puts the door on the press-through fallback — see the class remarks.</summary>
        public bool ThresholdIsWalkable => BoatCabinThreshold.HasBand(Door);

        /// <summary>Whether the next crossing of the band would be taken. False while she is still
        /// standing in the doorway she last came through. Read by tests; nothing else needs it.</summary>
        public bool PassageIsArmed => _passageArmed;

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
        /// wharf you are boarding, which is the switcher's press and outranks this one anyway.
        ///
        /// <para>⚠ This covers the INSIDE press too, and not by accident: going below is a layer swap and
        /// not a place you travel to (ADR 0038), so a player in a cabin on her own boat is still
        /// <c>ControlMode.OnDeck</c> to the switcher. The one actor that is not is the arrival's
        /// passenger, who reports OnFoot aboard somebody else's hull, and the opening registers its own
        /// offer declaring both contexts for exactly that reason.</para></summary>
        public InteractContext Contexts => InteractContext.OnDeck;

        /// <summary>Facing is not required: you open a door by standing at it. Matches every other
        /// fixture you operate by walking up to (<c>InteriorStair</c>, <c>WetBucketPoint</c>).</summary>
        public bool RequiresFacing => false;

        /// <summary>Which way this press goes, said as the player would say it. Derived from the leaf's
        /// state rather than held as a third field, so the words cannot drift from the action (rule 6).
        /// </summary>
        public string VerbLabel => WouldOpen ? _openLabel : _closeLabel;

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
        /// The cabin's own state deliberately survives this: see <see cref="BoatInterior"/>. So does this
        /// door's, and for the same reason — a leaf that shut itself at every region boundary would shut
        /// behind a player standing in the doorway.</para>
        /// </summary>
        private void OnDisable() => Interactables.Unregister(this);

        private void Update() => Tick(Time.deltaTime);

        // ---- the press and the cue ------------------------------------------------------------

        /// <summary>
        /// Wire this door from the builder. Public so the placement pass never reaches a serialized
        /// field, and so a test can stand one up without a scene.
        /// </summary>
        public void Configure(BoatInterior interior, string id, int additionalDoorIndex,
                              float reachMeters, string openLabel, string closeLabel)
        {
            _interior = interior;
            _id = id;
            _additionalDoorIndex = additionalDoorIndex;
            _reachMeters = Mathf.Max(0f, reachMeters);
            _openLabel = openLabel ?? "";
            _closeLabel = closeLabel ?? "";
            _cueElapsed = -1f;
            IsOpen = false;             // the ruling's first sentence, restated at every wiring
            _passageArmed = false;      // see the field: the arrival starts her IN this doorway
        }

        /// <summary>
        /// <b>Set the leaf's resting state directly, with no cue</b> — how a door is FOUND rather than how
        /// it is worked.
        ///
        /// <para>The one caller today is the arrival: Armand is aboard his own boat in fair weather and
        /// his aft door is standing open, which is the picture the owner's own ruling paints (<i>"they
        /// might leave the door open when they are aboard and the weathers nice"</i>). The NPC habit that
        /// decides this per boat and per forecast is the routine engine's and not this component's; all
        /// this offers is the setter it will use.</para>
        ///
        /// <para>Cancels any running cue — a leaf that has been placed is not a leaf that is moving — and
        /// touches the latch not at all, because the latch is about where the WALKER is standing.</para>
        /// </summary>
        public void SetOpen(bool open)
        {
            IsOpen = open;
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
        /// <para>A door with no cue time resolves on the same call — the leaf lands immediately, which is
        /// the correct behaviour for a threshold nobody has measured the animation of, and keeps the
        /// press honest whether or not the art exists.</para>
        /// </summary>
        public bool TryUse()
        {
            if (!IsAvailable) return false;

            BoatInteriorDoor door = Door;
            _cueOpens = WouldOpen;

            // ⚠ THE FALLBACK, and the only place the old press-through survives. A door with no measured
            // opening has no band to walk across, so a press that merely moved her leaf would leave the
            // room unreachable for good. The sill states which level you walk in ONTO (the sport fishers'
            // is at mezzanine height, so you walk in level); resolved once, at the press, so the answer
            // cannot drift mid-cue.
            if (!ThresholdIsWalkable)
            {
                _cueOpens = WouldEnter;
                _cueLevel = _cueOpens
                    ? Mathf.Max(0, _interior.LevelIndexAtHeight(door.ThresholdPoint.z))
                    : 0;
                if (!_saidTheDoorHasNoBand)
                {
                    _saidTheDoorHasNoBand = true;
                    Debug.LogWarning(
                        $"[BoatCabinDoor] '{_id}' states no ClearWidthMeters, so her threshold cannot be " +
                        "walked and the press carries the player through it as it did before the " +
                        "2026-08-28 door ruling. Measure the leaf in the interior sidecar to get the " +
                        "free passage.", this);
                }
            }

            _cueElapsed = 0f;

            // ⭐ THE LOAD, HERE AND NOWHERE EARLIER. The cabin's sheets are megabytes and they are not
            // referenced from anything a hull pulls in on spawn, so opening her door is the first moment
            // they are wanted — and the leaf's own baked cue is the window that hides the cost. An
            // unenterable cabin never reaches this line, because IsAvailable declined the press.
            if (_cueOpens) _interior.EnsureCells();

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
        /// The leaf has finished moving: it now rests in the state the press asked for.
        ///
        /// <para>On a door with a measured opening that is ALL this does — the passage is
        /// <see cref="TryWalkThrough"/>'s and it takes no press. On one without, this is where the old
        /// press-through still lands; the interior may legitimately refuse it (an unusable level, a def
        /// swapped under a moving door) and the cue ends regardless, because a door that stayed open
        /// because the room behind it declined would be a door that can never be pressed again.</para>
        /// </summary>
        private void Resolve()
        {
            _cueElapsed = -1f;

            if (ThresholdIsWalkable) { IsOpen = _cueOpens; return; }

            if (_interior == null) return;
            if (_cueOpens) _interior.TryEnter(_cueLevel);
            else _interior.TryExit();
        }

        // ---- the passage ----------------------------------------------------------------------

        /// <summary>
        /// ⭐⭐ <b>WALK THROUGH IT.</b> Called once a tick by whoever is walking — the arrival's two
        /// walkers, and the player's own <c>DeckWalkController</c> — with where she is standing in the
        /// HULL's own metres. Crosses the level exactly as the press used to
        /// (<see cref="BoatInterior.TryEnter"/> / <see cref="BoatInterior.TryExit"/>) and returns whether
        /// it did, so no caller has to hold a second copy of "is she inside".
        ///
        /// <para><b>Nothing here moves her.</b> The room is drawn into the same cell at the same pivot as
        /// the hull, so a walker standing in the doorway is already standing in the doorway of the picture
        /// that replaces it: the frame she is placed in changes underneath her and her world point does
        /// not. That is the whole trick of an interior (ADR 0038) and the reason this returns a bool
        /// rather than a position.</para>
        ///
        /// <para><b>The four gates, in the order they are asked.</b> She must have been clear of the
        /// doorway at some point since the last crossing (the latch — one approach, one crossing); the
        /// leaf must be standing open and still; she must be IN the band; and going IN she must be
        /// allowed in at all. <b>Coming OUT is never gated on the entry policy</b> — a cabin that stopped
        /// being enterable while somebody was inside must not thereby become a room she cannot leave.
        /// </para>
        ///
        /// <para><b>⚠ Hull-local metres, not world.</b> See <see cref="BoatCabinThreshold"/>: the sole
        /// walk and the deck walk project through different handedness conventions, and a threshold asked
        /// in world offsets would have to pick one of them and would mirror the doorway end for end on
        /// the hulls that disagree.</para>
        /// </summary>
        public bool TryWalkThrough(Vector2 hullLocalMetres)
        {
            BoatInteriorDoor door = Door;
            if (_interior == null || door == null) return false;

            // Re-arm the moment she is measurably clear of the doorway — asked whatever the leaf is
            // doing, so that the approach she makes AFTER opening a door is a fresh one rather than one
            // the last crossing already spent.
            if (BoatCabinThreshold.IsClearOfBand(door, hullLocalMetres)) _passageArmed = true;

            if (!_passageArmed || !IsOpen || IsCueing) return false;
            if (!BoatCabinThreshold.IsInBand(door, hullLocalMetres)) return false;

            bool goingIn = WouldEnter;
            if (goingIn && !BoatInteriorEntryPolicy.MayOffer(_interior)) return false;

            _passageArmed = false;

            if (!goingIn) return _interior.TryExit();

            _interior.EnsureCells();
            return _interior.TryEnter(Mathf.Max(0, _interior.LevelIndexAtHeight(door.ThresholdPoint.z)));
        }
    }
}
