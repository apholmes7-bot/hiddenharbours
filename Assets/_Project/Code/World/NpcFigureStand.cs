using System;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>Where a villager's mesh figure stands: on her own feet, on the ground</b> (ADR 0044, amendment
    /// 2026-09-27). The villager's side of the Core seam, as <c>MooredBoat</c> is a skipper's: it says which
    /// sprite she is and who she is, and asks Core's <see cref="CharacterFigurePresentation"/> for a figure.
    /// World never learns what draws it (rule 4).
    ///
    /// <para><b>A publisher, nothing more.</b> <see cref="FigureCharacter"/> is her own
    /// <see cref="IsoCharacterSprite"/>, whose stance, gait and heading her routine already writes, and
    /// <see cref="FigureKey"/> is her <see cref="NpcDef.Id"/>. She stands on no hull, so the hull members
    /// answer null and zero and are never read ashore.</para>
    ///
    /// <para><b>Whether she is seen is not decided here.</b> Her routine's shelter hides her through her
    /// renderer's <c>enabled</c> and makes her untalkable through the <see cref="Interactable"/>'s. The
    /// figure reads the first; this stand reads and writes neither, and its liveness is its own.</para>
    ///
    /// <para><b>An angler, when her block says so</b> (ADR 0053 §11). A block whose
    /// <see cref="RoutineEntry.Doing"/> is an <see cref="NpcAnglingActivityDef"/> makes this stand answer the
    /// Core angler and figure-beat stands: his session is <see cref="AnglerTimeline"/>'s pure function of the
    /// flow clock, a slot begins only when he has been at his station since its start and the routine keeps
    /// him there to its end (<see cref="NpcAnglerWindow"/>), and his catch is asked of
    /// <see cref="GameServices.ShoreCatch"/> once per slot. Nothing is saved; the only memory is a
    /// presentation latch (when he came back from a talk) that a reload simply forgets. The angler drawing is
    /// attached lazily, the first frame a block names angling, so a villager who never fishes gets none.</para>
    ///
    /// <para>Added by <see cref="Interactable"/>'s <c>Awake</c> to a host that names an <see cref="NpcDef"/>
    /// and carries an <see cref="IsoCharacterSprite"/>. It asks for her figure once, in its own
    /// <c>Awake</c>. With no presentation service installed, or no skin on her art def, nothing is attached
    /// and her sprite draws exactly as before.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NpcFigureStand : MonoBehaviour, ICharacterFigureWorkStand, ICharacterFigureBeatStand,
                                         IAnglerStand
    {
        private IsoCharacterSprite _character;
        private string _key;
        private uint _keyHash;
        private VillagerRoutine _routine;
        private bool _routineLooked;

        // ---- the angler (presentation only; never saved) ------------------------------------------------
        private enum AnglerStatus { None, AtRest, InSession }

        private bool _anglerAttached;
        private int _sampledFrame = int.MinValue;
        private double _sampledFlow = double.NaN, _sampledTotal = double.NaN;
        private AnglerStatus _status;
        private AnglerBeat _beat;
        private NpcAnglingActivityDef _angling, _lastAngling;

        // The run of one block: did a talk or a hurry interrupt him after he got there, and if so when he was
        // free again. A slot begins no earlier than that.
        private int _runBlock = -1;
        private RoutinePlan _runPlan;
        private NpcActivityDef _runActivity;
        private bool _runInterrupted;
        private AnglerRunWindow _window;

        // The catch, asked once per slot.
        private bool _catchKnown;
        private long _catchSlot;
        private int _catchSeed;
        private NpcAnglingActivityDef _catchActivity;
        private IShoreCatchFacts _catchFacts;
        private bool _catchHas;
        private ShoreCatch _catch;
        private ShoreEdge _catchEdge;

        /// <inheritdoc/>
        /// <remarks>At work while she stands at a place of today's beach outing whose activity names a work
        /// clip (<see cref="BeachActivityDef.WorkState"/>), with its tool (<see cref="BeachActivityDef.HeldProp"/>).
        /// Read off her routine's pose for the hour every frame; allocation-free.</remarks>
        public bool TryGetFigureWork(out string stateKey, out HeldPropMeshDef heldProp)
        {
            stateKey = null;
            heldProp = null;
            EnsureRoutine();
            BeachLifeDirector beach = BeachLifeDirector.Instance;
            if (_routine == null || beach == null || !beach.TryWorkAt(_routine, out BeachActivityDef activity)
                || string.IsNullOrEmpty(activity.WorkState))
                return false;
            stateKey = activity.WorkState;
            heldProp = activity.HeldProp;
            return true;
        }

        /// <inheritdoc/>
        /// <remarks>His session's beat with his rod while he fishes, and the rod held ("hold") while he rests
        /// at his station between sessions. None otherwise.</remarks>
        public bool TryGetFigureBeat(out string stateKey, out double beatStartSeconds, out float scrub01,
                                     out HeldPropMeshDef heldProp)
        {
            stateKey = null;
            beatStartSeconds = 0d;
            scrub01 = float.NaN;
            heldProp = null;
            AnglerStatus status = Sample();
            if (status == AnglerStatus.None) return false;
            heldProp = _angling.Rod;
            if (status == AnglerStatus.InSession && !string.IsNullOrEmpty(_beat.StateKey))
            {
                stateKey = _beat.StateKey;
                beatStartSeconds = _beat.BeatStartSeconds;
                scrub01 = _beat.Scrub01;
                return true;
            }
            stateKey = FishingBeatMath.StateKeyFor(FishingPose.Hold);
            return true;
        }

        /// <inheritdoc/>
        public bool TryGetAnglerState(out FishingState state, out float secondsInPhase)
        {
            if (Sample() == AnglerStatus.InSession)
            {
                state = _beat.State;
                secondsInPhase = _beat.SecondsInPhase;
                return true;
            }
            state = FishingState.Idle;
            secondsInPhase = 0f;
            return false;
        }

        /// <inheritdoc/>
        public AnglerLook Look => _angling != null ? _angling.Look
                                : _lastAngling != null ? _lastAngling.Look : default;

        /// <inheritdoc/>
        public IsoCharacterSprite FigureCharacter => _character;

        /// <inheritdoc/>
        /// <remarks>Always null: she stands on the ground.</remarks>
        public Transform FigureHull => null;

        /// <inheritdoc/>
        /// <remarks>Zero, and never read ashore.</remarks>
        public Vector3 FigureStandRigMetres => Vector3.zero;

        /// <inheritdoc/>
        /// <remarks>Zero, and never read ashore: her facing is her sprite's heading.</remarks>
        public float FigureDeckBearingDegrees => 0f;

        /// <inheritdoc/>
        public string FigureKey => _key;

        /// <summary>The angling activity this frame's block names, or null. For tests and plates.</summary>
        public NpcAnglingActivityDef Angling { get { Sample(); return _angling; } }

        /// <summary>This frame's session beat (meaningful while <see cref="TryGetAnglerState"/> is true). For
        /// tests and plates.</summary>
        public AnglerBeat Beat { get { Sample(); return _beat; } }

        private void Awake()
        {
            _character = GetComponent<IsoCharacterSprite>();
            Interactable talk = GetComponent<Interactable>();
            NpcDef npc = talk != null ? talk.Npc : null;
            _key = npc != null ? npc.Id : null;
            _keyHash = FigureKeyHash.Of(_key);
            CharacterFigurePresentation.Service?.Attach(gameObject, this);
        }

        /// <summary>The lazy attach: the first frame his block names angling, ask for the angler drawing.
        /// One array read per frame until then, nothing after.</summary>
        private void Update()
        {
            if (_anglerAttached) return;
            EnsureRoutine();
            if (_routine == null || !_routine.IsLiving) return;
            RoutinePlan plan = _routine.Today;
            if (plan == null || !(plan.DoingAt(_routine.Pose.BlockIndex) is NpcAnglingActivityDef)) return;
            IAnglerPresentationService service = AnglerPresentation.Service;
            if (service != null) _anglerAttached = service.Attach(gameObject, this) != null;
        }

        private void OnDisable()
        {
            // Back on: a fresh run, so a villager re-enabled mid-block is judged from the clock again.
            _runBlock = -1;
            _runPlan = null;
            _sampledFrame = int.MinValue;
        }

        private void EnsureRoutine()
        {
            if (_routineLooked) return;
            _routineLooked = true;
            TryGetComponent(out _routine);
        }

        /// <summary>This frame's angler answer, worked out once per frame and clock reading.</summary>
        private AnglerStatus Sample()
        {
            IGameClock clock = GameServices.Clock;
            double flow = clock != null ? clock.FlowSeconds : double.NaN;
            double total = clock != null ? clock.TotalSeconds : double.NaN;
            int frame = Time.frameCount;
            if (frame == _sampledFrame && flow.Equals(_sampledFlow) && total.Equals(_sampledTotal)) return _status;
            _sampledFrame = frame;
            _sampledFlow = flow;
            _sampledTotal = total;
            _status = Evaluate(clock);
            if (_angling != null) _lastAngling = _angling;
            return _status;
        }

        private AnglerStatus Evaluate(IGameClock clock)
        {
            _angling = null;
            _beat = default;
            EnsureRoutine();
            if (clock == null || _routine == null || !_routine.IsLiving) return AnglerStatus.None;
            RoutinePlan plan = _routine.Today;
            if (plan == null) return AnglerStatus.None;

            float secondsPerGameHour = GameServices.SecondsPerDay / RoutineSchedule.HoursPerDay;
            double flow = clock.FlowSeconds;
            var activity = NpcAnglerWindow.At(plan, clock.HourOfDay, secondsPerGameHour, flow, out int block,
                                              out double arrival, out double departure) as NpcAnglingActivityDef;
            if (activity == null || !activity.IsPlayable(out _))
            {
                _runBlock = -1;
                return AnglerStatus.None;
            }

            if (block != _runBlock || !ReferenceEquals(plan, _runPlan) || !ReferenceEquals(activity, _runActivity))
            {
                _runBlock = block;
                _runPlan = plan;
                _runActivity = activity;
                _runInterrupted = false;
                _window.Unlatch();
            }

            RoutinePose pose = _routine.Pose;
            bool arrived = arrival <= flow && pose.BlockIndex == block && !pose.Walking;
            bool free = !_routine.IsAway && _routine.HoldPhase == ConversationHoldPhase.Idle
                        && pose.Shelter == RoutineShelter.Outdoors;
            if (!arrived) return AnglerStatus.None;   // still walking there: no rod yet
            if (!free)
            {
                _runInterrupted = true;
                _window.Unlatch();
                return AnglerStatus.None;
            }
            _angling = activity;

            // Arrival and departure on the flow clock, latched for the run so the hour clock's float steps cannot
            // nudge either (a slot ending within float jitter of departure would otherwise flip mid-session).
            // After a talk or a hurry he begins no slot that started before he was free again.
            _window.Latch(arrival, departure, flow, _runInterrupted);

            int seed = GameServices.Environment != null ? GameServices.Environment.WorldSeed : 0;
            AnglerScript script = activity.Script;
            long slot = AnglerTimeline.SlotIndex(in script, seed, _keyHash, flow);
            if (!AnglerTimeline.SlotMayBegin(in script, seed, _keyHash, slot, _window.ArrivalFlow, _window.DepartureFlow))
                return AnglerStatus.AtRest;

            // The slot's start in routine time: the weather and the catch are read there, so neither changes
            // while the session plays.
            double slotStart = AnglerTimeline.SlotStartSeconds(in script, seed, _keyHash, slot);
            double slotStartTotal = clock.TotalSeconds - (flow - slotStart);
            IEnvironmentService env = GameServices.Environment;
            if (env != null && env.SeaState01At(slotStartTotal) > activity.MaxSeaState01) return AnglerStatus.AtRest;

            // The facts are part of the key: a Fishing module published (or replaced) mid-slot is read at once.
            IShoreCatchFacts facts = GameServices.ShoreCatch;
            if (!_catchKnown || slot != _catchSlot || seed != _catchSeed || !ReferenceEquals(activity, _catchActivity)
                || !ReferenceEquals(facts, _catchFacts))
            {
                _catchKnown = true;
                _catchSlot = slot;
                _catchSeed = seed;
                _catchActivity = activity;
                _catchFacts = facts;
                _catchHas = facts.TryPick(activity.CatchSpotId, seed, _keyHash, slot,
                                                            slotStartTotal, out _catch);
                // The deck edge he fishes over, measured from his stand (the frame the offsets are in).
                _catchEdge = activity.TryGetStand(out ShoreStand stand) ? stand.Edge.RelativeTo(stand.Position) : default;
            }

            float heading = block < plan.StandHeadingDegrees.Length ? plan.StandHeadingDegrees[block] : pose.HeadingDegrees;
            AnglerTimeline.Evaluate(in script, in _catch, _catchHas, seed, _keyHash, heading, flow, in _catchEdge, out _beat);
            return _beat.InSession ? AnglerStatus.InSession : AnglerStatus.AtRest;
        }
    }
}
