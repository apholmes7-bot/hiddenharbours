using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>A bed you can turn in at</b> — and, when it is the player's OWN, the game's first manual save.
    ///
    /// <para><b>The ownership rule, and why it is a field rather than a deed lookup.</b> The player
    /// sleeps at Ginny's by her invitation: this is LODGING, not property. Nothing here consults
    /// <c>HomeDeeds</c>, and deliberately so — the camper is the first thing the player owns, and a bed
    /// that quietly required a deed would either break the opening or lie about what the opening is.
    /// <see cref="IsPlayerBed"/> is authored per bed by the builder, which is the honest model: the
    /// upstairs has two beds and exactly one of them is yours because your host said so.</para>
    ///
    /// <para><b>Someone else's bed is a refusal, never a save.</b> Pressing Ginny's answers with a
    /// line and does nothing — the cozy refusal (P5), the same shape as an empty freezer saying it is
    /// empty. It stays <see cref="IsAvailable"/> while it does it: a bed you are told is not yours is a
    /// better answer than a bed the prompt pretends is not there.</para>
    ///
    /// <para><b>What "save" means here.</b> The bed publishes <see cref="RestSaveRequested"/> and stops.
    /// It never touches the save system, never writes a file, and does not know whether one was written
    /// — <see cref="RestSaveResponder"/> answers and reports. That is what lets the beat around the
    /// write (a fade now, the <c>Fisher_sleep</c> animation when the owner's sheet lands, a clock move
    /// to morning after that) arrive without this component changing at all.</para>
    ///
    /// <para><b>It also says WHERE you will wake up</b> (ADR 0037). The request carries the position the
    /// actor is standing on and the storey this bed is on, and <see cref="RestSaveResponder"/> stamps the
    /// region onto them. The position is the PLAYER'S, not the bed's, deliberately: they are standing on
    /// floor they walked to and are inside this bed's own reach, so "at the bedside" needs no offset to
    /// be tuned and no furniture to be guessed at. The storey is read live off
    /// <see cref="BuildingInterior.Level"/>, so nothing here has to agree with the builder about which
    /// floor it was placed on.</para>
    ///
    /// <para><b>⭐ It also says WHICH WAY ROUND IT IS</b> — <see cref="Pillow"/>, the end the pillow is
    /// on, DECLARED by whoever placed this bed and never worked out from the sprite. The sleeping pose
    /// reads it and lays the fisher head-to-pillow; before it existed the pose played one hard-coded
    /// heading, which is right for a bed at facing 0 and wrong for the same bed on a building the
    /// builder happened to turn — and being wrong LOOKS like art, not like a bug. Nothing about the save
    /// turns on it: see <see cref="SleepBeatRequested.Pillow"/>.</para>
    ///
    /// <para><b>⚠ The deck-work guard, asserted rather than assumed.</b> A save must never land in the
    /// middle of a deck-work cycle (#193). Being indoors is supposed to make that structurally
    /// impossible, and <see cref="Contexts"/> is what makes it so — <see cref="InteractContext.OnFoot"/>
    /// alone means the resolver will not even offer this candidate to an actor on a deck. But
    /// <see cref="Interact"/> re-checks the actor it is handed anyway: "structurally impossible" is a
    /// claim about a seam two files away, and the cost of proving it here is one comparison.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteriorBed : MonoBehaviour, IInteractable
    {
        [Tooltip("Stable id — unique among live interactables. Ginny's and the player's need different " +
                 "ones (…bed_player / …bed_ginny).")]
        [SerializeField] private string _id = "fixture.interior.bed";

        [Tooltip("Is this the PLAYER'S bed? Only a player's bed offers to keep the day; anyone else's " +
                 "gives the polite refusal below. Authored per bed — lodging is not ownership, so no " +
                 "deed is consulted.")]
        [SerializeField] private bool _isPlayerBed;

        [Tooltip("Whose bed this is, for the refusal line. Ignored on the player's own.")]
        [SerializeField] private string _ownerName = "";

        [Tooltip("Where the player is turning in, for the notice — e.g. your bed at Ginny's.")]
        [SerializeField] private string _placeName = "";

        [Tooltip("How close (m) you must stand. A bed is a big piece of furniture you sit down on, so " +
                 "this is a step-up distance rather than a precise one.")]
        [SerializeField, Min(0f)] private float _reachMeters = 1.4f;

        [Tooltip("The building this bed stands in — read for which STOREY it is on, so a rest upstairs " +
                 "is recorded as a rest upstairs (ADR 0037), and for the room's own FACING, which is the " +
                 "frame the pillow side below is declared in. OPTIONAL: a bed with no interior reports " +
                 "the ground floor and an unturned room, which is right for every bed that is not in a " +
                 "house with a second storey and is a safe answer for one that is.")]
        [SerializeField] private BuildingInterior _interior;

        [Tooltip("WHICH END THE PILLOW IS ON, in the ROOM's own model frame (+x across to the right, " +
                 "+y toward the back/hearth wall) — the frame the placement's own coordinates are in. " +
                 "DECLARED by the builder from the furnishing table, never read off the sprite. " +
                 "Undeclared keeps whatever heading the sleeping pose already had, and fails content " +
                 "validation for a bed.")]
        [SerializeField] private PillowSide _pillowSide = PillowSide.Undeclared;

        [Tooltip("How far the pillow's centre is from the middle of this bed, in ground metres. " +
                 "DERIVED by the builder from the bake's own prop depth and the rig's pillow inset " +
                 "(BedPillow.PillowReachMetres) — never a number typed in here, so a re-bake at another " +
                 "size moves the pillow with the bed.")]
        [SerializeField, Min(0f)] private float _pillowReachMetres;

        /// <summary>Whether this bed is the player's own — the one thing that decides save vs refusal.</summary>
        public bool IsPlayerBed => _isPlayerBed;

        /// <summary>The refusal for someone else's bed. Public so the test asserts the contract rather
        /// than a re-typed literal.</summary>
        public string RefusalText =>
            string.IsNullOrEmpty(_ownerName)
                ? "Not your bed."
                : $"That is {_ownerName}'s bed. Yours is the other one.";

        // ---- the interact seam ---------------------------------------------------------------

        /// <inheritdoc/>
        public string Id => _id;

        /// <inheritdoc/>
        public Vector2 WorldPosition => transform.position;

        /// <inheritdoc/>
        public float ReachMeters => _reachMeters;

        /// <inheritdoc/>
        public int Priority => InteractPriority.Fixture;

        /// <summary>On foot only — and this is load-bearing, not decorative: it is the first half of the
        /// deck-work guard (see the class remarks).</summary>
        public InteractContext Contexts => InteractContext.OnFoot;

        /// <summary>Facing not required — you turn in by walking up to the bed.</summary>
        public bool RequiresFacing => false;

        /// <summary>Always available, including someone else's bed: pressing it answers with the
        /// refusal, which is the cozy behaviour and is what <see cref="IInteractable.IsAvailable"/>
        /// explicitly asks for.</summary>
        public bool IsAvailable => true;

        /// <summary>
        /// The same word for both beds, deliberately. Someone else's bed still OFFERS a sleep — pressing it
        /// is how you find out whose it is, and that refusal is the cozy beat (P5). A popup that read
        /// "Whose bed?" would answer the question before the player had asked it, and turn a small warm
        /// moment into a label.
        /// </summary>
        public string VerbLabel => "Sleep";

        /// <summary>
        /// Turn in — or be told whose bed this is.
        ///
        /// <para>The actor's own context is re-checked before anything happens. See the class remarks:
        /// the resolver should never route a deck-bound press here, and this is where that stops being
        /// an assumption.</para>
        /// </summary>
        public void Interact(in InteractActor actor)
        {
            if (actor.Context != InteractContext.OnFoot)
            {
                // Unreachable through the resolver, and left as a quiet refusal rather than an assert:
                // a future caller driving this directly should be told no, not crash the player's game.
                EventBus.Publish(new DevNotice("Not from here."));
                return;
            }

            // The actor's own position is the wake spot — see Rest.
            Rest(actor.Position);
        }

        private void OnEnable() => Interactables.Register(this);
        private void OnDisable() => Interactables.Unregister(this);

        /// <summary>Wire this bed from the builder. Public so the placement pass never reaches a
        /// serialized field, and so a test stands one up with no scene.</summary>
        public void Configure(string id, bool isPlayerBed, string ownerName, string placeName,
                              float reachMeters, BuildingInterior interior = null,
                              PillowSide pillowSide = PillowSide.Undeclared,
                              float pillowReachMetres = 0f)
        {
            _id = id;
            _isPlayerBed = isPlayerBed;
            _ownerName = ownerName ?? "";
            _placeName = placeName ?? "";
            _reachMeters = Mathf.Max(0f, reachMeters);
            _interior = interior;
            _pillowSide = pillowSide;
            _pillowReachMetres = Mathf.Max(0f, pillowReachMetres);
        }

        /// <summary>
        /// Which end of this bed the pillow is on, and the frame that makes it a direction — everything a
        /// sleeping pose needs to lie a person down head-first.
        ///
        /// <para><b>The frame is read LIVE off the room, exactly as <see cref="Level"/> is.</b> The yaw and
        /// the ground squash both come from <see cref="BuildingInterior.Footprint"/>, which is the same
        /// struct the builder placed this bed with — so the direction the pillow points and the direction
        /// the room is drawn cannot drift apart, and a rebuild at a different facing needs no second
        /// number updated anywhere. A bed with no interior answers for an unturned room at the shared bake
        /// camera's own squash, which is what a free-standing bed in a test rig actually is.</para>
        ///
        /// <para>⚠ Unity fake-null: an explicit <c>== null</c> comparison, never <c>?.</c>, which would
        /// sail straight past a destroyed interior and throw.</para>
        /// </summary>
        public PillowAnchor Pillow
        {
            get
            {
                if (_interior == null)
                    return new PillowAnchor(_pillowSide, 0f, _pillowReachMetres, 0f);

                InteriorFootprint room = _interior.Footprint;
                return new PillowAnchor(_pillowSide, room.YawDegrees, _pillowReachMetres,
                                        room.DepthScale);
            }
        }

        /// <summary>Where the sleeper's head lands on this bed, world units — on the pillow for a declared
        /// bed, and in the middle of the mattress for one that never said. The quantity the content test
        /// asserts is on the pillow side.</summary>
        public Vector2 HeadWorldPosition => Pillow.HeadWorld(WorldPosition);

        /// <summary>
        /// Which storey this bed is on, right now — 0 for a bed in a single-storey building, outdoors, or
        /// in no building at all.
        ///
        /// <para><b>Read live rather than authored, and that is the point.</b> The builder places the
        /// player's bed in the upper storey's furniture list; it does not also write down "1" somewhere
        /// this component could disagree with. Asking the building means the two cannot drift, and a bed
        /// moved downstairs in a later plan is correct with no code change (rule 6 — there is no number
        /// here to get wrong).</para>
        ///
        /// <para>⚠ Unity fake-null: an explicit <c>!= null</c>, never <c>?.</c>, which would sail straight
        /// past a destroyed interior and throw.</para>
        /// </summary>
        public int Level => _interior != null ? _interior.Level : 0;

        /// <summary>
        /// Ask to turn in. Returns true if a rest was REQUESTED (the player's own bed) — not whether a
        /// save landed, which is the responder's answer and arrives as <see cref="GameSaved"/>. Public
        /// so tests drive it without input.
        /// </summary>
        public bool Rest(Vector2 wakePosition)
        {
            if (!_isPlayerBed)
            {
                EventBus.Publish(new DevNotice(RefusalText));
                return false;
            }

            // The SAVE half, unchanged from #574/#580 — same signal, same fields, same order.
            EventBus.Publish(new RestSaveRequested(_placeName, wakePosition, Level));

            // The PRESENTATION half, additive: where the MATTRESS is, which the save signal deliberately
            // does not carry (it carries the wake spot, which is the player's feet — the opposite
            // point), and WHICH WAY ROUND the mattress is, which nothing outside this bed can know.
            // Published second so the day is already kept before any pose is shown: a beat that is
            // interrupted, or a scene with no sleep art at all, still rests exactly as it did before.
            EventBus.Publish(new SleepBeatRequested(WorldPosition, Level, _placeName, Pillow));
            return true;
        }

        /// <summary>
        /// Turn in without naming a wake spot — the shape this method had before there was one, kept for
        /// callers that have no actor in hand (a dev menu, a test asserting the refusal).
        ///
        /// <para><b>It falls back to the BED, not to the origin</b>, which is the least wrong thing
        /// available: a caller who cannot say where the player is standing has still told us which bed,
        /// and the middle of the mattress is at worst a stride from the bedside. The real path never
        /// takes it — <see cref="Interact"/> always has the actor.</para>
        /// </summary>
        public bool Rest() => Rest(WorldPosition);
    }
}
