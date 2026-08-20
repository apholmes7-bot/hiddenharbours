using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>Makes one hull's cabin ENTERABLE</b> — the runtime half of ADR 0038. Step through her door and
    /// the exterior's interior-facing draw yields to the room baked for it; step out and it comes back.
    /// <b>No scene load, no separate screen, no camera cut, no input-mode change</b>: the owner's
    /// 2026-07-30 ruling is that interiors are SEAMLESS and true to the footprint, and a boat cabin is
    /// that same shape of problem with one difference that changes everything — <b>the footprint
    /// moves.</b>
    ///
    /// <para><b>The four ruled behaviours, and where each one lives.</b></para>
    ///
    /// <para><b>1 · MOTION — the room rides her own pose (ADR 0038 proposal 1).</b> The interior sheets
    /// bake into the SAME cell at the SAME pivot as the exterior, so registration holds by construction
    /// when both are posed from one set of numbers rather than by a correction term. What this component
    /// does is read the pose the hull PUBLISHES having drawn — her rock phase
    /// (<see cref="BoatWaveMotion.RockPhaseDegrees"/>) and the displaced ride she reports applying
    /// (<see cref="IBoatHullPresenter.DrawnRideMeters"/>) — and hand it to
    /// <see cref="BoatInteriorPoseMath"/> with the comfort scale. Never recomputed here: the storm heave
    /// filter is a stateful spring, and a second instance of a stateful smoother agrees only with itself.
    /// <b>The scale reaches one draw and nothing else</b> — not the hull's pose, not her handling, not the
    /// save (rule 5).</para>
    ///
    /// <para><b>2 · THE WATER MASK — the hull IS the mask (proposal 2).</b> There is deliberately NO code
    /// here that subtracts from the water surface, and adding some would be the defect the ruling exists
    /// to prevent: a second, screen-space authority would have to agree with the hull's depth shear
    /// (ADR 0033) frame by frame on a rocking hull, and any disagreement shows as sea leaking through a
    /// cabin floor <i>on the wave crests only</i>. Every sole sits inside the hull's own silhouette — the
    /// below-deck levels included, which is why a sole under the waterline is simply a smaller
    /// <see cref="BoatInteriorLevel.SoleZMeters"/> and not a special case. The sea is already behind the
    /// hull's facets at every pixel a sole covers, so there is nothing to disagree with.</para>
    ///
    /// <para><b>3 · OCCLUSION — entering is a LAYER SWAP, off-not-sorted (proposal 3).</b> ADR 0036's
    /// property carried over verbatim: <b>exactly one of {the exterior's interior-facing draw, the
    /// interior} is on at a time</b> (<see cref="ExactlyOneLayerOn"/>), so the Y-sort band (ADR 0032) is
    /// never asked to rank a bunk against a lobster crate, and there is no frame in which the two poses
    /// are both on screen to disagree about registration. That last clause is the whole reason proposals 1
    /// and 3 were ruled as a set, and it is why <see cref="Apply"/> is re-asserted every tick rather than
    /// only on the transition: the invariant is maintained, not merely established.</para>
    ///
    /// <para><b>4 · REGION HOP — the interior shares the hull's lifetime (proposal 4).</b> The boat is
    /// persistent core and her cabin must be too, so this component's state <b>deliberately survives
    /// OnDisable</b>. Root-toggling IS how a region hop works, "disabled" happens constantly and does not
    /// mean "gone", and a cabin that reset itself there would put the player back out on deck every time
    /// they crossed a boundary while below. <see cref="OnEnable"/> therefore RE-ASSERTS the swap it was
    /// already holding rather than clearing it, and <see cref="OnDisable"/> does nothing at all —
    /// neither unregistering a service (the house law: <c>OnDestroy</c>, guarded on still owning it) nor
    /// publishing a release it may not still hold (#601's lesson: an unconditional publish in
    /// <c>OnDisable</c> outlives the phase that made it correct).</para>
    ///
    /// <para><b>Nothing here draws a prompt or reads a key.</b> The way in is
    /// <see cref="BoatCabinDoor"/>, which registers on the interact seam like any other fixture; this
    /// component only knows how to be inside or outside. Cross-module law: UI and World reach it through
    /// Core interfaces and the EventBus, never by naming this type (rule 4).</para>
    ///
    /// <para><b>⚠ WHERE THE INTERIOR RENDERER MAY BE PARENTED.</b> This component writes the interior's
    /// pose ABSOLUTELY, from a cached base local pose — never as a delta on top of what it finds. So its
    /// transform must hang from something SCREEN-ALIGNED that is not itself rocking: the hull's visual
    /// child is counter-rotated against the physics body's yaw, so a plain sibling of the hull under the
    /// physics root would swing with her heading; and a child of the hull's own visual would take her
    /// FULL ride on top of the scaled one and rock twice. Wire it as a screen-aligned, unrocked child of
    /// the boat root.</para>
    ///
    /// <para><b>⚠ KNOWN AND OPEN, and NOT closed by this component: the compositing window at the frame
    /// edge.</b> The interior bakes into the full hull cell at the hull pivot, so the window it composites
    /// through moves and rocks with the boat; where a hull is part-way off screen, that window and the
    /// viewport disagree. ADR 0038 records it open on purpose so whoever built the runtime met it
    /// deliberately. Nothing here touches it — no clipping, no viewport test, no second window — and no
    /// later change may close it silently.</para>
    ///
    /// <para>Visual + state only: no sim, <b>no save</b>, and no allocation per frame (rule 5, rule 7).</para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]   // the overlay-reader band: after BoatWaveMotion (−120) and the hull (−110)
    public sealed class BoatInterior : MonoBehaviour
    {
        [Header("The room, as data (ADR 0003 rule 2 — nothing per-hull in C#)")]
        [Tooltip("This hull's imported interior. Null, or a def with no usable level, means she has no " +
                 "inside to enter — which is DATA, not a fault: most of the fleet has never been " +
                 "measured. Every number below that could be stated by the def is read FROM it.")]
        [SerializeField] private BoatInteriorDef _def;

        [Header("What swaps (ADR 0038 proposal 3 — off, not sorted)")]
        [Tooltip("The exterior's INTERIOR-FACING draw — the cabin as seen from outside, the thing that " +
                 "occludes her contents. Switched OFF while the occupant is inside. A hull with none " +
                 "wired swaps only what she was given; see ExactlyOneLayerOn.")]
        [SerializeField] private Renderer _exterior;

        [Tooltip("The baked interior. Switched ON while the occupant is inside, showing the cell for the " +
                 "level they are on and the facing she is drawn at.")]
        [SerializeField] private SpriteRenderer _interior;

        [Tooltip("Parent of any interior fixtures that Y-sort in their own right. Switched with the " +
                 "interior. OPTIONAL — the sheets carry the room's own fit-out.")]
        [SerializeField] private Transform _fittings;

        [Tooltip("The transform this component POSES. Defaults to the interior renderer's own. See the " +
                 "class remarks on where it may be parented — the pose is written absolutely, so an " +
                 "already-rocking parent rocks the room twice.")]
        [SerializeField] private Transform _interiorPivot;

        [Header("Which cell (level-major, exactly as the sheet contract lays them out)")]
        [Tooltip("The sliced interior cells, element [levelIndex * Facings + facing] — BoatInteriorKit's " +
                 "own CellIndex. May be empty: an unwired hull still swaps her layers, she just has no " +
                 "picture to show yet, and that is honest rather than broken.")]
        [SerializeField] private Sprite[] _cells = System.Array.Empty<Sprite>();

        [Tooltip("Facings per level. 8 across the whole kit.")]
        [SerializeField, Min(1)] private int _facings = 8;

        [Tooltip("Tick for art whose cells run COUNTER-CLOCKWISE. TRUE across this kit — the contract " +
                 "says CounterClockwise for all 24 hulls, and it was measured from the exteriors' own " +
                 "ground-plane bearing after the silhouette probe called 18 of them backwards.")]
        [SerializeField] private bool _cellsAreCounterClockwise = true;

        [Tooltip("The compass heading (degrees, 0 = North, CW) that cell 0 of each level is drawn for.")]
        [SerializeField] private float _zeroHeadingDegrees;

        [Header("The ride — ART FACTS of this hull's baked rock (rule 6)")]
        [Tooltip("The boat this cabin is inside. Her wave motion and hull presenter are read from here; " +
                 "empty falls back to this component's own root.")]
        [SerializeField] private Transform _boatRoot;

        [Tooltip("Peak roll of the hull's baked rock cycle, degrees (the iso dory bakes 5).")]
        [SerializeField] private float _deckRollDegrees = 5f;

        [Tooltip("Peak heave of the same cycle in the sheets' own PIXELS (the iso dory bakes 1.6).")]
        [SerializeField] private float _deckHeavePixels = 1.6f;

        [Tooltip("Screen-vertical travel at the peak of the pitch, metres.")]
        [SerializeField] private float _deckPitchLiftMeters = 0.02f;

        /// <summary>Whether the occupant is inside this hull right now. Read-only to everyone else — the
        /// only ways in and out are <see cref="TryEnter"/> and <see cref="TryExit"/>, and the only caller
        /// that should be using them is her door.</summary>
        public bool IsInside { get; private set; }

        /// <summary>
        /// <b>Which level the occupant is on</b> — an index into <see cref="BoatInteriorDef.Levels"/>, and
        /// always 0 outside.
        ///
        /// <para>An index rather than a bool for the reason <see cref="Core.BoatInteriorLevel"/> is a
        /// LAYER and not a place: a wheelhouse, a forepeak berth under it and a helm deck above it are
        /// the same swap on different rungs, and the five ships carry three levels each. Only
        /// <see cref="TryEnter"/>, <see cref="TryExit"/> and <see cref="TryGoToLevel"/> write it.</para>
        /// </summary>
        public int Level { get; private set; }

        /// <summary>The imported room this hull carries, or null. Read by her door for the threshold and
        /// the cue timings, so those numbers are never transcribed twice.</summary>
        public BoatInteriorDef Def => _def;

        /// <summary>How many levels she declares. 0 for a hull with no def, which is a hull with no
        /// inside — see <see cref="HasInterior"/>.</summary>
        public int LevelCount => _def != null && _def.Levels != null ? _def.Levels.Length : 0;

        /// <summary>True when there is an inside to enter at all. The gate every path here takes: absence
        /// is data (most of the fleet has never been measured), so a hull without one is INERT rather
        /// than broken.</summary>
        public bool HasInterior => _def != null && _def.HasInterior();

        /// <summary>True when the exterior's interior-facing draw is on screen right now.</summary>
        public bool ExteriorDrawn => _exterior != null && _exterior.enabled;

        /// <summary>True when the interior is on screen right now.</summary>
        public bool InteriorDrawn => _interior != null && _interior.enabled;

        /// <summary>
        /// <b>ADR 0036's property, stated as a predicate so a test can hold this component to it.</b>
        /// Exactly one of the two layers is drawn — never both (which would put two differently-posed
        /// pictures of one cabin on screen together) and never neither (which would leave a hole where
        /// the wheelhouse was).
        ///
        /// <para>False when either half is unwired, and that is the honest answer rather than a lie of
        /// omission: a hull with no exterior draw named cannot promise the property, only the half of it
        /// she was given.</para>
        /// </summary>
        public bool ExactlyOneLayerOn =>
            _exterior != null && _interior != null && ExteriorDrawn != InteriorDrawn;

        /// <summary>How many metres one baked pixel of THIS hull's interior is — 1/32 across the fleet and
        /// 1/16 on the tanker. Read from the def every time rather than copied into a field, because two
        /// pixel grids live in this kit and a stale copy is how they get conflated.</summary>
        public float PixelsPerUnit => _def != null && _def.PixelsPerMetre > 0 ? _def.PixelsPerMetre : 32f;

        private BoatWaveMotion _wave;
        private IBoatHullPresenter _hull;
        private bool _boatResolved;

        private Vector3 _baseLocalPosition;
        private bool _baseCached;
        private bool _posed;

        // ---- wiring ---------------------------------------------------------------------------

        /// <summary>
        /// Wire this cabin from the builder. Everything is contract data; nothing is guessed. Applies
        /// immediately for the reason <c>BuildingInterior.Configure</c> does: <c>OnEnable</c> fires the
        /// moment <c>AddComponent</c> runs — before this call, when every reference is still null — and
        /// nothing runs <c>LateUpdate</c> outside play mode, so a cabin that waited for a tick would sit
        /// switched ON in the editor from the moment the builder ran.
        /// </summary>
        public void Configure(BoatInteriorDef def, Renderer exterior, SpriteRenderer interior,
                              Transform fittings, Transform interiorPivot, Transform boatRoot,
                              Sprite[] cells, int facings, bool cellsAreCounterClockwise,
                              float zeroHeadingDegrees,
                              float deckRollDegrees, float deckHeavePixels, float deckPitchLiftMeters)
        {
            _def = def;
            _exterior = exterior;
            _interior = interior;
            _fittings = fittings;
            _interiorPivot = interiorPivot;
            _boatRoot = boatRoot;
            _cells = cells ?? System.Array.Empty<Sprite>();
            _facings = Mathf.Max(1, facings);
            _cellsAreCounterClockwise = cellsAreCounterClockwise;
            _zeroHeadingDegrees = zeroHeadingDegrees;
            _deckRollDegrees = deckRollDegrees;
            _deckHeavePixels = deckHeavePixels;
            _deckPitchLiftMeters = deckPitchLiftMeters;

            _boatResolved = false;
            _baseCached = false;
            Apply();
        }

        // ---- the two transitions --------------------------------------------------------------

        /// <summary>
        /// <b>Go inside</b>, onto <paramref name="level"/>. Returns false — changing nothing — when there
        /// is no inside to enter, the level asked for does not exist or is not usable, or the occupant is
        /// already in.
        ///
        /// <para><b>Nothing moves.</b> The room is drawn into the same cell at the same pivot as the hull
        /// it lives in, so a player standing at the threshold is already standing in the doorway of the
        /// picture that replaces it. That is what lets an interior be a layer swap rather than a place to
        /// travel to — the same trick, for the same reason, as <c>BuildingInterior.TryGoToLevel</c>.</para>
        /// </summary>
        public bool TryEnter(int level)
        {
            if (IsInside || !HasInterior) return false;
            if (!IsUsableLevel(level)) return false;

            IsInside = true;
            Level = level;
            Apply();
            return true;
        }

        /// <summary>
        /// <b>Come out.</b> Returns false when the occupant was not inside to begin with.
        ///
        /// <para><b>The level resets, always</b> — ADR 0036's rule, carried onto boats by ADR 0038 and
        /// restated there in as many words: a boat that remembered "below" would open on her forepeak the
        /// next time you stepped aboard.</para>
        /// </summary>
        public bool TryExit()
        {
            if (!IsInside) return false;

            IsInside = false;
            Level = 0;
            Apply();
            return true;
        }

        /// <summary>
        /// <b>Change level</b> — the companionway's entry point, and the only writer of
        /// <see cref="Level"/> that is not a transition. Returns false, changing nothing, when the
        /// occupant is not inside (you cannot take a companionway from the wharf), when the level asked
        /// for does not exist or is unusable, or when it is the one already occupied.
        /// </summary>
        public bool TryGoToLevel(int level)
        {
            if (!IsInside) return false;
            if (level == Level) return false;
            if (!IsUsableLevel(level)) return false;

            Level = level;
            Apply();
            return true;
        }

        /// <summary>True when <paramref name="level"/> indexes a level of this def with enough vertices
        /// to bound anything. An unusable level is refused rather than entered blind — a sole with two
        /// points is a measurement that did not finish.</summary>
        public bool IsUsableLevel(int level)
        {
            if (_def == null || _def.Levels == null) return false;
            if (level < 0 || level >= _def.Levels.Length) return false;
            BoatInteriorLevel l = _def.Levels[level];
            return l != null && l.IsUsable();
        }

        /// <summary>
        /// <b>Which level a sill at <paramref name="zMeters"/> walks you in onto</b> — the nearest usable
        /// sole by hull-local height, or −1 when she has none.
        ///
        /// <para>The def states it and this reads it (rule 2): a door's
        /// <see cref="BoatInteriorDoor.ThresholdPoint"/> carries z precisely so that the level you arrive
        /// on is DATA — the sport fishers' sill sits at mezzanine height so you walk in level, and the
        /// answer must follow the measurement rather than a constant 0 that happens to be right on the
        /// lobster boats.</para>
        /// </summary>
        public int LevelIndexAtHeight(float zMeters)
        {
            if (_def == null || _def.Levels == null) return -1;

            int best = -1;
            float bestGap = 0f;
            for (int i = 0; i < _def.Levels.Length; i++)
            {
                BoatInteriorLevel l = _def.Levels[i];
                if (l == null || !l.IsUsable()) continue;

                float gap = Mathf.Abs(l.SoleZMeters - zMeters);
                // Strictly-less keeps the FIRST of two equidistant soles, so the answer is a function of
                // the def's own order and not of iteration luck (the determinism rule, one level down).
                if (best < 0 || gap < bestGap) { best = i; bestGap = gap; }
            }
            return best;
        }

        // ---- lifetime -------------------------------------------------------------------------

        /// <summary>
        /// ⚠️ <b>RE-ASSERTS the swap; it does NOT reset it.</b> This is the one place this component
        /// deliberately differs from <c>BuildingInterior</c>, and ADR 0038 proposal 4 is the reason: a
        /// building is somewhere you cannot be while you travel, but a boat is the thing you travel IN,
        /// and her interior shares her lifetime. Root-toggling is how a region hop works, so "disabled"
        /// happens constantly and does not mean "gone" — a cabin that started OUTSIDE here would put the
        /// player back on deck at every region boundary they crossed while below.
        /// </summary>
        private void OnEnable()
        {
            _boatResolved = false;   // a hop may have re-skinned her; the presenter is a POCO the skinner swaps
            Apply();
        }

        /// <summary>
        /// ⚠️ <b>Deliberately empty, and it must stay that way.</b> Two laws meet here. A Core service is
        /// never unregistered in <c>OnDisable</c> — the house law is <c>OnDestroy</c>, guarded on still
        /// owning it — because root-toggling fires this constantly on a hop. And any release or publish
        /// must be guarded by "did I actually hold this?" (#601): an unconditional one here would outlive
        /// the phase that made it correct. This component holds nothing outside itself, so the honest
        /// implementation is nothing at all, said out loud so nobody adds a tidy-up that seals every
        /// cabin in the game the first time a player crosses a region line.
        /// </summary>
        private void OnDisable() { }

        private void LateUpdate()
        {
            // The swap is MAINTAINED, not merely established: re-asserting it every tick is what makes
            // ExactlyOneLayerOn an invariant rather than a hope, and it costs two bool compares and no
            // allocation (rule 7).
            Apply();

            if (!IsInside) { RestorePose(); return; }

            ShowCell();
            ApplyRide();
        }

        // ---- the swap -------------------------------------------------------------------------

        /// <summary>
        /// <b>Show exactly one layer.</b> The inactive one is switched OFF, not sorted behind — ADR 0036's
        /// property, carried onto boats verbatim by ADR 0038 proposal 3. That is what keeps the sorting
        /// band (ADR 0032) out of the argument entirely: there is never a frame in which a bunk and a
        /// lobster crate are both drawn and have to be ranked against each other. It is also what makes
        /// the comfort scale safe, because two differently-posed pictures of one cabin are never on
        /// screen together.
        ///
        /// <para>A hard SWAP and not a fade, for <c>BuildingInterior</c>'s reasons: it costs nothing per
        /// frame, it can never leave a half-transparent wheelhouse standing, and a fade would be exactly
        /// the co-visible frame the paragraph above rules out.</para>
        /// </summary>
        private void Apply()
        {
            bool inside = IsInside;

            if (_exterior != null && _exterior.enabled == inside) _exterior.enabled = !inside;
            if (_interior != null && _interior.enabled != inside) _interior.enabled = inside;
            if (_fittings != null && _fittings.gameObject.activeSelf != inside)
                _fittings.gameObject.SetActive(inside);
        }

        /// <summary>
        /// Put the cell for the level occupied and the facing she is drawn at onto the interior renderer.
        ///
        /// <para><b>The facing is resolved from the DRAWN heading through the shared rule</b>
        /// (<see cref="IsoFacing.HeadingToFacingIndex"/>) rather than from the hull's own facing index,
        /// and the reason is that the two need not be the same number: the interior sheets carry 8 cells
        /// per level whatever the hull's compass carries, and a MESH hull reports
        /// <see cref="IBoatHullPresenter.FacingCount"/> 0 because she is not quantised at all. Asking the
        /// shared rule for "which of MY cells depicts the heading she is drawn at" is the one question
        /// that has an answer on both paths.</para>
        ///
        /// <para>A missing cell leaves the sprite exactly as it was rather than blanking the renderer: an
        /// unwired hull shows nothing, and a partly-wired one must not flicker.</para>
        /// </summary>
        private void ShowCell()
        {
            if (_interior == null || _cells == null || _cells.Length == 0) return;

            ResolveBoat();
            float heading = _hull != null ? _hull.DrawnHeadingDegrees() : 0f;
            int facing = IsoFacing.HeadingToFacingIndex(heading, _facings, _zeroHeadingDegrees,
                                                       _cellsAreCounterClockwise);

            int index = Level * _facings + facing;
            if (index < 0 || index >= _cells.Length) return;

            Sprite cell = _cells[index];
            if (cell != null && _interior.sprite != cell) _interior.sprite = cell;
        }

        // ---- the ride -------------------------------------------------------------------------

        /// <summary>
        /// Pose the interior on the hull's own motion, scaled by the comfort clamp.
        ///
        /// <para><b>Read, never recomputed.</b> The phase is the one the hull PUBLISHES having drawn —
        /// the frame actually on screen for a sprite hull, the continuous pose for a mesh one — and the
        /// ride is the metres she reports having applied, past her storm spring and her waterline settle.
        /// A second copy of either would be a second opinion about one motion, and a stateful smoother
        /// agrees only with itself.</para>
        ///
        /// <para>The fallback is the deck rider's: a hull on the legacy transform rock publishes no phase
        /// because she has no cycle to publish, so her cabin leans by the tilt she is applying to her own
        /// visual.</para>
        /// </summary>
        private void ApplyRide()
        {
            ResolveBoat();

            float scale = GameServices.InteriorRockScale;
            float ride = _hull != null ? _hull.DrawnRideMeters : 0f;

            DeckRidePose pose = _wave != null && _wave.IsRocking
                ? BoatInteriorPoseMath.Ride(_wave.RockPhaseDegrees, ride,
                                            _deckRollDegrees, _deckHeavePixels, _deckPitchLiftMeters,
                                            PixelsPerUnit, scale)
                : BoatInteriorPoseMath.RideTilt(_hull != null ? _hull.VisualTiltDegrees : 0f, ride, scale);

            ApplyPose(pose);
        }

        /// <summary>The transform the pose is written to — the explicit pivot when one is wired, else the
        /// interior renderer's own. Never cached across a re-wire (<see cref="Configure"/> clears the
        /// base), because a different transform has a different base pose.</summary>
        private Transform PoseTarget()
        {
            if (_interiorPivot != null) return _interiorPivot;
            return _interior != null ? _interior.transform : null;
        }

        /// <summary>
        /// Write the pose ABSOLUTELY, from the base local pose captured the first time — never as a delta
        /// on what is already there, which would integrate and walk the room off her hull over a few
        /// hundred frames. The lift is applied in WORLD +Y because screen-up must stay screen-up whatever
        /// the physics body's yaw is doing, exactly as <see cref="BoatWaveMotion"/> applies its own.
        /// </summary>
        private void ApplyPose(in DeckRidePose pose)
        {
            Transform t = PoseTarget();
            if (t == null) return;

            if (!_baseCached)
            {
                _baseLocalPosition = t.localPosition;
                _baseCached = true;
            }

            t.localRotation = Quaternion.Euler(0f, 0f, pose.RollDegrees);
            t.localPosition = _baseLocalPosition;
            if (pose.LiftMeters != 0f) t.position += new Vector3(0f, pose.LiftMeters, 0f);
            _posed = true;
        }

        /// <summary>Put the room back exactly as built. Idempotent, and skipped entirely when nothing was
        /// ever posed — a cabin nobody has entered must be byte-identical to one this component was never
        /// added to.</summary>
        private void RestorePose()
        {
            if (!_posed || !_baseCached) return;
            _posed = false;

            Transform t = PoseTarget();
            if (t == null) return;
            t.localRotation = Quaternion.identity;
            t.localPosition = _baseLocalPosition;
        }

        /// <summary>
        /// Find the boat's wave motion and hull presenter — once per enable, never on the hot path. Plain
        /// <c>!= null</c> throughout: a destroyed boat is fake-null and must degrade to "no ride" rather
        /// than throw, and <c>??</c>/<c>?.</c> sail straight past Unity's overloaded <c>==</c>.
        ///
        /// <para>The presenter is a POCO the skinner swaps out, so it is re-resolved on every enable —
        /// which a region hop is one of.</para>
        /// </summary>
        private void ResolveBoat()
        {
            if (_boatResolved) return;
            _boatResolved = true;

            Transform root = _boatRoot != null ? _boatRoot : transform.root;
            if (root == null) { _wave = null; _hull = null; return; }

            _wave = root.GetComponent<BoatWaveMotion>();
            _hull = BoatHullPresenterHost.Resolve(root.gameObject);
        }
    }
}
