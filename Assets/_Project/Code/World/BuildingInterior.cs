using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// Makes one building ENTERABLE — the runtime half of the interiors pilot. Walk through the door
    /// and the shell yields to the room baked for it; walk out and it comes back. <b>No scene load, no
    /// separate screen, no camera cut, no input mode change</b>: the owner's 2026-07-30 ruling is that
    /// interiors are SEAMLESS and true to the footprint, and everything here is in service of that.
    ///
    /// <para><b>How "inside" is decided: geometry, not a trigger volume.</b>
    /// <see cref="InteriorFootprint.Contains"/> is a pure function of the occupant's position, the
    /// room's footprint and its facing, so it is unit-tested headless and behaves identically in
    /// EditMode, in a build, and at any timescale. A trigger would have needed a rigidbody pairing, a
    /// collider that survives the shear at the diagonal facings, and PlayMode to test — three ways to
    /// be wrong about a question that is one dot product wide. The test uses the INNER rectangle (the
    /// footprint less the wall), so you are inside once you are past the wall, which is the same moment
    /// the doorway lets you through.</para>
    ///
    /// <para><b>What changes at the threshold, exactly:</b> the shell renderer switches off and the room
    /// and its furniture switch on. A hard SWAP, not a fade — it costs nothing per frame, it can never
    /// leave a half-transparent house standing, and the crossing happens inside a doorway the player is
    /// already walking through. (The room rig exposes a <c>ghost()</c> ordered-dither stipple if a
    /// later pass wants the walls to fade instead; that is a bake-side change, not a shader.)</para>
    ///
    /// <para><b>The walls block either way and are never switched off.</b> The cutaway that drops the
    /// two camera-facing walls is a courtesy to the camera; the house is solid from outside and from
    /// in, and the doorway is the one gap. That is also what stops a player wandering in through a
    /// wall and finding themselves "inside" a room they never entered.</para>
    ///
    /// <para><b>Sorting.</b> The room sheet is floor and FAR walls only — everything in it is spatially
    /// BEHIND an occupant — so it takes a fixed order below the Y-sort band rather than a
    /// <c>YSortSprite</c> of its own, and no occupant can ever end up behind the floor. The FURNITURE is
    /// separate sprites that Y-sort normally, which is what lets the player walk behind the table and in
    /// front of it. Rooms never move, so their props park themselves the way the rest of the decor
    /// does.</para>
    ///
    /// <para><b>Who it watches is resolved, not just wired.</b> The occupant is the builder's own
    /// reference where there is one and Core's <see cref="GameServices.PlayerTransform"/> where there
    /// is not — see <see cref="ResolveOccupant"/>. A region scene is saved long before the persistent
    /// player exists (and Unity will not serialize a reference across scenes anyway), so a room that
    /// could only be wired at build time was a room that opened in the start scene and nowhere else.</para>
    ///
    /// <para><b>⭐ A SECOND STOREY IS A SECOND LAYER, NOT A SECOND PLACE (ADR 0036).</b> A building may
    /// carry an upper level: another room sprite and another furniture root over the SAME footprint,
    /// with <see cref="Level"/> saying which of the two is drawn. Going up hides the ground floor and
    /// shows the storey above; coming down reverses it. There is no scene load, no camera cut and no
    /// pocket room somewhere off the map — the stairwell stands at the same footprint COORDINATE on both
    /// storeys, so the way back is where you arrived.
    ///
    /// <para><b>⭐ AND THE STOREY ABOVE IS DRAWN A STOREY UP (ADR 0036, amended 2026-08-23).</b> The
    /// original layer swap drew both storeys at the building's own transform, so the bedroom landed
    /// exactly over the kitchen and going upstairs did not look like going anywhere. The upper level now
    /// sits at <see cref="UpperLevelY"/> — the rig's DECLARED storey height, projected once by
    /// <see cref="InteriorLevelLayout.UpperLevelY"/> — which means the two floors are a real distance
    /// apart and the fisher WALKS between them (<see cref="StairTraversal"/>) instead of blinking. The
    /// storey itself still swaps in one frame; only the body takes the half second.</para>
    ///
    /// <para>Three consequences worth stating, because each is a bug that did not have to be written.
    /// <b>Y-sort never has to rank the two storeys</b> against each other, because the one you are not
    /// on is switched OFF rather than sorted behind — the band (ADR 0032) is never asked a question it
    /// has no answer to. <b>The inside test does not change in kind</b>: it is the same rectangle, the
    /// same walls and the same threshold on both storeys, lifted by the height of the one you are
    /// standing on (<see cref="FootprintFor"/>), so there is still exactly one definition of being in
    /// this building. And <b>the level resets on the way out</b>, so a house can never be re-entered
    /// into its bedroom.</para></para>
    ///
    /// <para><b>The one exception to that last rule is waking up (ADR 0037).</b> A player who went to
    /// sleep upstairs is put back at their bed on load, and a house that opened on its ground floor would
    /// stand them in the room below their own bed — the feature failing in the one way that looks like it
    /// working. So a <see cref="PlayerWokeAt"/> whose anchor is inside this footprint arms a ONE-SHOT
    /// storey, spent by the very next inside-transition and by nothing else. Every later walk through the
    /// front door lands on the ground floor exactly as before.</para>
    ///
    /// <para>Visual + collision only: no sim, <b>no save</b> — the wake arrives as a signal, so this class
    /// still has no idea a save file exists (rule 4) — and no allocation per frame (rule 5, rule 7).</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingInterior : MonoBehaviour
    {
        [Header("What swaps")]
        [Tooltip("The exterior shell's renderer. Switched OFF while the occupant is inside.")]
        [SerializeField] private SpriteRenderer _shell;

        [Tooltip("The baked room. Switched ON while the occupant is inside.")]
        [SerializeField] private SpriteRenderer _room;

        [Tooltip("Parent of the furniture sprites. Switched with the room.")]
        [SerializeField] private Transform _props;

        [Header("The storey above (OPTIONAL — a single-storey building leaves all three empty)")]
        [Tooltip("The upper storey's room sprite. Shown INSTEAD of the ground room while Level is 1.")]
        [SerializeField] private SpriteRenderer _upperRoom;

        [Tooltip("Parent of the upper storey's furniture. Switched with the upper room.")]
        [SerializeField] private Transform _upperProps;

        [Tooltip("Parent of the colliders that exist ONLY upstairs — the partition between the two " +
                 "bedrooms, and the plug that closes the front doorway so you cannot walk out of a " +
                 "first-floor bedroom into open air. The building's own walls are NOT in here: they are " +
                 "the same walls on both storeys and stay on throughout.")]
        [SerializeField] private Transform _upperWalls;

        [Tooltip("How far UP THE SCREEN the storey above is drawn, in world units — the rig's declared " +
                 "storey height already projected at the shared ¾ camera (InteriorLevelLayout.UpperLevelY " +
                 "is the one place that projection happens). Written by the builder from the bake's " +
                 "contract; never typed. Zero means the upper storey draws exactly over the ground " +
                 "floor, which is the ADR 0036 defect and is complained about out loud.")]
        [SerializeField, Min(0f)] private float _upperLevelY;

        [Header("Who can be inside")]
        [Tooltip("The on-foot player, AS THIS SCENE'S BUILDER KNEW THEM. Serialized rather than searched " +
                 "for, the same way WorldInteractor takes its player. OPTIONAL: a region scene has no " +
                 "persistent player to name at build time, so leaving this empty is valid — the occupant " +
                 "is then resolved from Core at runtime. See ResolveOccupant.")]
        [SerializeField] private Transform _occupant;

        [Header("The footprint (metres, as the rig reports it)")]
        [Tooltip("Floor width across the room — the rig's Wd. Written by the builder from the bake's contract.")]
        [SerializeField, Min(0f)] private float _widthMetres = 6.6f;

        [Tooltip("Floor length front to back — the rig's Ln.")]
        [SerializeField, Min(0f)] private float _lengthMetres = 8.05f;

        [Tooltip("Which INTERIOR facing this room is showing. The art is baked at a fixed camera, so " +
                 "this is the rotation — nothing here ever rotates a transform.")]
        [SerializeField, Min(0)] private int _facing;

        [Tooltip("Facings the sheet carries. 8 at 45°, the ADR-0006 recipe.")]
        [SerializeField, Min(1)] private int _facings = 8;

        [Tooltip("How far up the screen one metre of NORTHWARD ground travel draws: sin(40°) at the " +
                 "shared bake camera. SpriteLightMath.GroundDepthScale is the definition — this is " +
                 "seeded from it by the builder, and an EditMode test pins the two together, because " +
                 "World cannot reference Art to read it directly.")]
        [SerializeField] private float _groundDepthScale = 0.6427876f;

        [Header("Threshold")]
        [Tooltip("Wall thickness used for the INSIDE test — you are inside once you are past the wall. " +
                 "Match whatever the colliders were built with.")]
        [SerializeField, Min(0f)] private float _wallThicknessMetres = 0.3f;

        [Tooltip("Slack added to the inside test on the way OUT only, so standing in the doorway does " +
                 "not flicker the whole house on and off.")]
        [SerializeField, Min(0f)] private float _hysteresisMetres = 0.15f;

        [Tooltip("The gap left in the front wall. WIDER than the drawn 1.05 m opening on purpose: the " +
                 "gap has to admit a player who has width of their own, and a threshold you have to " +
                 "line up on to the pixel is not cozy. The colliders are built from this same value.")]
        [SerializeField, Min(0f)] private float _doorwayWidthMetres = 1.4f;

        [Tooltip("Which model-frame wall the doorway is in: OFF = the −y wall (interiorIsoRig, the " +
                 "house family), ON = +y (the shop kit, whose room is its shopfront seen from " +
                 "inside). The two kits genuinely differ and both are MEASURED — do not carry one " +
                 "across. Off by default so nothing already standing moves.")]
        [SerializeField] private bool _doorOnPlusY;

        [Tooltip("How far along that wall the doorway sits, in metres from the wall's centre, +x to " +
                 "the right. 0 for a centred door — every house — but MEASURED and non-zero on two of " +
                 "the three shops (post office −1.68 m, restaurant −2.52 m). A centred gap under an " +
                 "off-centre door blocks the doorway and opens a wall.")]
        [SerializeField] private float _doorAcrossMetres;

        /// <summary>Whether the occupant is currently inside. Read-only to everyone else — the only way
        /// in is through the door.</summary>
        public bool IsInside { get; private set; }

        /// <summary>The storey the NEXT inside-transition should land on, armed by a rest wake and spent
        /// immediately (ADR 0037). 0 — the overwhelming majority of the time — means "nothing armed", so
        /// the ordinary rule stands and you come in on the ground floor. Not serialized: it describes one
        /// load, not the building.</summary>
        int _pendingLevel;

        /// <summary>
        /// <b>Which storey the occupant is on</b> — 0 the ground floor, 1 the storey above. Always 0 in a
        /// building with no upper level, and forced back to 0 the moment the occupant steps outside
        /// (<see cref="Update"/>), so you can never re-enter a house and find yourself already upstairs.
        ///
        /// <para>An int rather than a bool because the mechanism is a LADDER, not a toggle: a third
        /// storey, a cellar (−1 would need the floor set widening, not the type changing) or a loft are
        /// all the same swap one rung further, and a bool would have to be replaced rather than
        /// extended. Only 0 and 1 are reachable today: <see cref="TryGoToLevel"/> is the writer for
        /// anyone WALKING between storeys and refuses anything else, and the two paths that place a body
        /// rather than move one (a rest wake, stepping outside) refuse it too.</para>
        /// </summary>
        public int Level { get; private set; }

        /// <summary>Whether this building has a storey above at all. False for every building but one,
        /// and the gate on every level-changing path — a stair wired to a single-storey house is inert
        /// rather than wrong.</summary>
        public bool HasUpperLevel => _upperRoom != null || _upperProps != null;

        /// <summary>The topmost storey that exists here: 1 with an upper level, 0 without.</summary>
        public int TopLevel => HasUpperLevel ? 1 : 0;

        /// <summary>The room's footprint in world units, rebuilt from the serialized fields. Cheap
        /// (a struct of floats) and deliberately not cached: the builder may re-face a room in the
        /// editor and a stale cache would keep the old walls.
        ///
        /// <para><b>This is the GROUND floor's</b>, at every level — it is what "this building's
        /// footprint" means to everyone outside these walls (an NPC routine pathing to the door, a
        /// placement test), and none of them should have to know which storey the player happens to be
        /// standing on. The storey you are on is <see cref="FootprintFor"/>.</para></summary>
        public InteriorFootprint Footprint => FootprintFor(InteriorLevelLayout.GroundLevel);

        /// <summary>
        /// The footprint of ONE STOREY — the same rectangle, lifted by that storey's projected height.
        ///
        /// <para>The lift is what makes the inside test still true upstairs. The footprint is unchanged
        /// in every other respect: same width, same length, same facing, same doorway — ADR 0036's one
        /// footprint, one threshold, one definition of being in this building, now with the storeys drawn
        /// where they actually are.</para>
        /// </summary>
        public InteriorFootprint FootprintFor(int level) =>
            new InteriorFootprint((Vector2)transform.position + LevelOffset(level),
                                  _widthMetres, _lengthMetres,
                                  _facing, _facings, _groundDepthScale,
                                  _doorOnPlusY ? 1f : -1f, _doorAcrossMetres);

        /// <summary>How far up the screen this building's storey above draws, in world units. Zero for
        /// the great majority of buildings, which have no storey above.</summary>
        public float UpperLevelY => _upperLevelY;

        /// <summary>The world-space offset from the ground floor to a given storey of THIS building.
        /// Purely vertical, and one rung per storey — the same shape as
        /// <see cref="InteriorLevelLayout.LevelOffset"/>, which is what a third storey would need.</summary>
        Vector2 LevelOffset(int level) =>
            level <= InteriorLevelLayout.GroundLevel
                ? Vector2.zero
                : new Vector2(0f, level * _upperLevelY);

        /// <summary>The doorway in world units — the threshold, and where a spawn or a "you are here"
        /// marker belongs. The GROUND floor's, always: there is no door on the storey above, which is
        /// the whole reason the upper level carries a plug over the one below it.</summary>
        public Vector2 DoorWorld => Footprint.DoorWorld;

        /// <summary>
        /// Wire this room up from the builder. Everything here is contract data; nothing is guessed.
        /// </summary>
        public void Configure(SpriteRenderer shell, SpriteRenderer room, Transform props,
                              float widthMetres, float lengthMetres, int facing, int facings,
                              float groundDepthScale, float wallThicknessMetres,
                              float doorwayWidthMetres,
                              bool doorOnPlusY = false, float doorAcrossMetres = 0f)
        {
            _shell = shell;
            _room = room;
            _props = props;
            _widthMetres = widthMetres;
            _lengthMetres = lengthMetres;
            _facing = facing;
            _facings = Mathf.Max(1, facings);
            _groundDepthScale = groundDepthScale;
            _wallThicknessMetres = wallThicknessMetres;
            _doorwayWidthMetres = doorwayWidthMetres;
            _doorOnPlusY = doorOnPlusY;
            _doorAcrossMetres = doorAcrossMetres;

            // ⚠️ Apply immediately, and this matters in the EDITOR specifically. OnEnable fires the
            // moment AddComponent runs — BEFORE this call — when every reference is still null, so it
            // has nothing to switch off. Nothing then runs Update outside play mode, and the owner is
            // left looking at a cottage with its furniture stacked on the roof after every build.
            Apply(IsInside);
        }

        /// <summary>
        /// Give this building a storey above. Called by the builder AFTER <see cref="Configure"/>, and
        /// only for a building whose plan declares an upper level — everything else keeps exactly the
        /// behaviour it had, with all three references null and <see cref="HasUpperLevel"/> false.
        ///
        /// <para>Applies immediately for the same reason <see cref="Configure"/> does: nothing runs
        /// Update outside play mode, so an upper storey that waited for a tick would sit switched ON in
        /// the editor, drawn straight over the ground floor, from the moment the builder ran.</para>
        ///
        /// <para><b><paramref name="upperLevelY"/> is how far UP THE SCREEN the storey draws</b> — the
        /// rig's declared storey height already projected (<see cref="InteriorLevelLayout.UpperLevelY"/>).
        /// It is required, not defaulted, because the defect this argument exists to fix was a storey
        /// standing at the ground floor's own Y: a default of 0 would be that bug spelled as an omission.
        /// The room renderer is MOVED to it here rather than by the caller, so the height the component
        /// tests against and the height the sheet is drawn at are one number and cannot drift.</para>
        /// </summary>
        public void ConfigureUpperLevel(SpriteRenderer upperRoom, Transform upperProps,
                                        Transform upperWalls, float upperLevelY)
        {
            _upperRoom = upperRoom;
            _upperProps = upperProps;
            _upperWalls = upperWalls;
            _upperLevelY = float.IsNaN(upperLevelY) ? 0f : Mathf.Max(0f, upperLevelY);
            Level = 0;
            CancelClimb();

            // The sheet goes where the storey is. SET, never offset, so re-running the builder over a
            // house that already has an upstairs cannot stack one lift on top of another.
            if (_upperRoom != null)
            {
                Vector3 local = _upperRoom.transform.localPosition;
                _upperRoom.transform.localPosition = new Vector3(local.x, _upperLevelY, local.z);
            }

            // Loud, and still standing. A contract baked before the rigs declared a storey height reports
            // 0, and the honest response is the pre-fix picture plus a message naming the fix — not a
            // cottage that silently loses its upstairs, and not an exception halfway through a region
            // build. See ADR 0036's amendment.
            if (InteriorLevelLayout.DrawsOverGroundFloor(_upperLevelY))
                Debug.LogError(
                    $"[BuildingInterior] '{name}' has a storey above standing at the GROUND FLOOR's own " +
                    $"Y ({_upperLevelY:0.###} world units). The two storeys will draw exactly over each " +
                    "other and going upstairs will not look like going anywhere (ADR 0036, amended " +
                    "2026-08-23). The height comes from the rig's declared storey height in the " +
                    "interiors contract (storeyHeightMetres) via InteriorLevelLayout.UpperLevelY — a " +
                    "contract baked before that field existed reports 0, so re-bake the interiors kit.",
                    this);

            Apply(IsInside);
        }

        /// <summary>
        /// <b>Change storey.</b> The stairwell's entry point, and the only writer of <see cref="Level"/>.
        /// Returns false — changing nothing — when the move is not on:
        /// <list type="bullet">
        /// <item>the occupant is not inside (you cannot climb a stair from the dooryard),</item>
        /// <item>there is no storey above (a stair in a single-storey house),</item>
        /// <item>the level asked for is out of range, or is the one already occupied.</item>
        /// </list>
        ///
        /// <para><b>The player WALKS.</b> The stairwell is the same footprint coordinate on both storeys
        /// — that part of ADR 0036 is untouched, and it is still why the way back is a press rather than
        /// a search — but the storeys are drawn a real storey apart, so the head of the stairs is that
        /// far up the screen from the foot. The storey swaps NOW — the interact registry, the colliders
        /// and the drawn room all follow it on this frame; the BODY is then carried up over
        /// <c>GameConfig.StairClimbSeconds</c> by
        /// <see cref="StairTraversal"/>, so the camera rides up with the fisher instead of cutting. Set
        /// the tunable to 0 and the arrival is instant, which is exactly the behaviour this had before
        /// the storeys were drawn apart.</para>
        ///
        /// <para><b>The climb is presentation, never state.</b> Everything that can be asked a question —
        /// which storey you are on, which fixtures are in the room, what the walls do — is already true
        /// when this returns. Nothing waits on the walk, so a climb that is interrupted (a spawn, a
        /// region hop, a dev teleport) leaves a consistent house behind it rather than half a
        /// transition.</para>
        /// </summary>
        public bool TryGoToLevel(int level)
        {
            if (!IsInside) return false;
            if (level < 0 || level > TopLevel) return false;
            if (level == Level) return false;

            Vector2 rise = LevelOffset(level) - LevelOffset(Level);
            Level = level;
            Apply(true);
            BeginClimb(rise);
            return true;
        }

        /// <summary>The wall thickness and doorway gap this room's colliders must be built with — read
        /// back so the builder cannot construct walls that disagree with the inside test.</summary>
        public float WallThicknessMetres => _wallThicknessMetres;

        public float DoorwayWidthMetres => _doorwayWidthMetres;

        /// <summary>The on-foot player, handed in by whoever built the scene. Mirrors
        /// <c>WorldInteractor.SetPlayer</c> so the World module never reaches into Player. OPTIONAL —
        /// a room with no occupant wired resolves one from Core instead (<see cref="ResolveOccupant"/>),
        /// which is how every region scene works.</summary>
        public void SetOccupant(Transform occupant) => _occupant = occupant;

        /// <summary>
        /// Who this room is watching, RIGHT NOW — the builder's own reference while it is alive, and
        /// otherwise whoever Core says is walking the world
        /// (<see cref="GameServices.PlayerTransform"/>). Null when there is nobody to watch at all.
        ///
        /// <para><b>Why the serialized reference wins.</b> It is the more specific answer and it is
        /// right wherever it exists: in the START scene it names the real persistent player (the builder
        /// stands the core up in that same scene), and in a region scene played DIRECTLY for review it
        /// names that scene's dev stand-in, who is the only player there is. Preferring it means this
        /// change cannot perturb either path — where the old code worked, it still resolves the same
        /// transform on the same frame.</para>
        ///
        /// <para><b>Why the fallback has to exist.</b> A region scene cannot name the persistent player
        /// at build time — it does not exist yet, and Unity will not serialize a reference across
        /// scenes regardless. So a region's rooms are wired either to nothing or to a dev stand-in that
        /// <c>DevRegionBootstrap</c> DESTROYS the moment you actually travel in. Both cases land here,
        /// and before this fallback existed both meant the same thing: <c>Update</c> returned on its
        /// first line forever and the door never opened in any region you sailed to.</para>
        ///
        /// <para><b>Resolved per tick, never cached.</b> Two <c>UnityEngine.Object</c> null checks and a
        /// static read, no allocation (rule 7) — and staying stateless is what keeps a room correct
        /// across a shell restart, which replaces the persistent player with a different transform. A
        /// cache would hold the dead one.</para>
        ///
        /// <para><b>⚠ Explicit <c>!= null</c>, never <c>??</c>/<c>?.</c>.</b> The reference this method
        /// exists to survive is a DESTROYED one, and a destroyed <c>UnityEngine.Object</c> is fake-null:
        /// the null-propagating operators bypass the overloaded <c>==</c> and sail straight past it. An
        /// <c>_occupant ?? GameServices.PlayerTransform</c> here would compile clean, never take the
        /// fallback, and throw on the next dereference.</para>
        /// </summary>
        Transform ResolveOccupant()
        {
            if (_occupant != null) return _occupant;
            return GameServices.PlayerTransform;   // already laundered to a REAL null by the accessor
        }

        void OnEnable()
        {
            // Start OUTSIDE and show it, so a room that is never entered is never visible and a scene
            // that was saved mid-visit does not open with the roof off.
            IsInside = false;
            Level = 0;
            _pendingLevel = 0;
            CancelClimb();
            Apply(false);

            // The one exception to "you always come in on the ground floor": a player who went to sleep
            // upstairs (ADR 0037). Core publishes the wake; this decides whether it was in THIS house.
            EventBus.Subscribe<PlayerWokeAt>(OnPlayerWokeAt);
        }

        void OnDisable()
        {
            EventBus.Unsubscribe<PlayerWokeAt>(OnPlayerWokeAt);

            // A house torn down mid-climb must not take the player's legs and the move keys with it. The
            // walk is presentation and the storey is already true, so dropping it costs nothing.
            CancelClimb();
        }

        /// <summary>
        /// <b>The player has just been woken at their rest anchor.</b> If that anchor is inside THIS
        /// building and names the storey above, arm the one-shot so the house opens on it.
        ///
        /// <para><b>Why a one-shot rather than opening upstairs now.</b> The wake lands before the
        /// occupant has been seen inside — <see cref="IsInside"/> is still false, and
        /// <see cref="TryGoToLevel"/> rightly refuses to move a storey for someone who is not in the
        /// building. So the level is not set, it is REMEMBERED, and the very next inside-transition
        /// spends it. That keeps one rule about when a storey may change instead of two, and it means
        /// the swap still happens in <see cref="Apply"/>, once, on the frame the house opens.</para>
        ///
        /// <para><b>Every guard here is a way of declining, not a way of failing.</b> A wake somewhere
        /// else in the region, a wake on the ground floor, a wake in a house that no longer has an
        /// upstairs (the plan changed between builds — the save is older than the content): all of them
        /// leave this building exactly as it was, which is a player waking on the ground floor of a
        /// house they are standing in. That is a good outcome, so none of them is loud.</para>
        /// </summary>
        void OnPlayerWokeAt(PlayerWokeAt woke) => WakeAt(woke.Anchor);

        /// <inheritdoc cref="OnPlayerWokeAt"/>
        /// <summary>
        /// The RULE behind <see cref="OnPlayerWokeAt"/>, as a plain call.
        ///
        /// <para>⚠️ <b>PUBLIC ON PURPOSE — DO NOT TIDY THIS TO PRIVATE.</b> An EditMode test cannot reach
        /// the handler through the bus, because EditMode fires no <c>OnEnable</c> on a plain
        /// MonoBehaviour and so this component is never subscribed there. A test that published the
        /// signal anyway would assert against a room that could not have heard it — and the "nothing
        /// happens" cases would pass for exactly the wrong reason. So EditMode drives the rule here and
        /// PlayMode proves the subscription, the split <c>StandablePlatform</c> already uses.</para>
        /// </summary>
        public void WakeAt(in RestAnchor anchor)
        {
            if (anchor.Level <= 0 || !HasUpperLevel) return;
            if (anchor.Level > TopLevel) return;

            // Against the footprint of the storey the anchor names, not the ground floor's. A bed
            // upstairs stands one storey up, so its recorded position is one storey up too, and testing
            // it against the ground floor's rectangle would decide the player fell asleep outdoors.
            if (!FootprintFor(anchor.Level).Contains(anchor.Position, _wallThicknessMetres)) return;

            _pendingLevel = anchor.Level;

            // Already inside — which the title screen can produce, because the world ticks behind it and
            // a spawn can stand in a doorway. No inside-transition is coming to spend the one-shot on, so
            // spend it NOW. It goes through SetLevel and not TryGoToLevel because the sleeper is being
            // PLACED, not walking: the wake restorer has already stood them at the anchor, one storey up,
            // and a climb would carry them a second storey off the one they woke on.
            if (IsInside) { SetLevel(_pendingLevel); _pendingLevel = 0; }
        }

        /// <summary>
        /// Take the armed storey, if there is one, and go there. Clearing FIRST is what makes it a
        /// one-shot: waking upstairs must not mean every later walk through the front door lands in the
        /// bedroom.
        ///
        /// <para>Called ONLY from <see cref="Update"/>'s inside-transition, which calls
        /// <see cref="Apply"/> immediately after — so this deliberately does not redraw, and must not be
        /// called from anywhere that will not. The already-inside case goes through
        /// <see cref="SetLevel"/> instead, for exactly that reason.</para>
        /// </summary>
        void ConsumePendingLevel(Vector2 at, float inset)
        {
            int level = _pendingLevel;
            _pendingLevel = 0;
            if (level <= 0 || level > TopLevel) return;

            // ...and only if they are actually STANDING on that storey. The wake restorer puts the
            // sleeper at the anchor before this arms, so upstairs is where they already are; an anchor
            // that names a storey the body is not on is stale content (the plan changed between builds,
            // or a spawn overrode the placement) and taking it would draw a bedroom around a player
            // standing in the kitchen. Declining leaves them on the ground floor, which is the same good
            // outcome every other guard in WakeAt settles for.
            if (!FootprintFor(level).Contains(at, inset)) return;

            Level = level;
        }

        /// <summary>Put the occupant on a storey WITHOUT walking them there, and redraw. For the one
        /// caller who is placing a body rather than moving one: the rest wake, which has already stood
        /// the sleeper on the storey they woke on (<see cref="WakeAt"/>).</summary>
        void SetLevel(int level)
        {
            if (level < 0 || level > TopLevel || level == Level) return;
            CancelClimb();
            Level = level;
            Apply(IsInside);
        }

        void Update()
        {
            Transform occupant = ResolveOccupant();
            if (occupant == null) { CancelClimb(); return; }

            // The walk between storeys owns the body while it runs — and only while nothing else has
            // taken it. Returns true while it is still carrying them, in which case the threshold test
            // waits: an occupant halfway up a stairwell is in neither storey's rectangle, and asking
            // would answer "outside", switch the shell back on and reset the storey mid-climb.
            if (TickClimb(occupant, Time.deltaTime)) return;

            // Hysteresis on the way out only: the inner rectangle is the entry line, and leaving takes
            // a little more than arriving at it, so a player standing in the doorway does not strobe the
            // whole house.
            float inset = IsInside
                ? _wallThicknessMetres - _hysteresisMetres
                : _wallThicknessMetres;

            // Against the storey they are ON. Upstairs is the same rectangle one storey up, so this is
            // still ADR 0036's single inside test — it just knows how high the floor is.
            bool inside = FootprintFor(Level).Contains(occupant.position, inset);

            // ...and, on the way IN with a rest anchor armed, against the storey that anchor names too.
            // A player who slept upstairs is PUT there by the wake restorer before this ever runs
            // (ADR 0037), so on that one frame the body is a storey above the floor this building is
            // currently showing. Without this they read as still outdoors and would stand in their own
            // bedroom looking at the outside of the house.
            if (!inside && !IsInside && _pendingLevel > 0)
                inside = FootprintFor(_pendingLevel).Contains(occupant.position, inset);

            if (inside == IsInside) return;

            IsInside = inside;

            // Leaving puts you back on the ground floor. You cannot normally walk OUT while upstairs —
            // the doorway is plugged up there — but a spawn, a region travel or a dev teleport can all
            // move an occupant across the threshold without using the door, and a house that remembered
            // "upstairs" would then open on its first-floor bedroom the next time you walked in.
            //
            // Coming IN is where a rest anchor gets its one chance (ADR 0037): a player who slept
            // upstairs walks into their own bedroom, once. Everyone else — and that same player on every
            // later visit — comes in through the front door onto the ground floor, because the one-shot
            // has been spent. An unspent anchor is dropped on the way out for the same reason the level
            // is: whatever it was for, it did not happen.
            if (inside) ConsumePendingLevel(occupant.position, inset);
            else { Level = 0; _pendingLevel = 0; }

            Apply(inside);
        }

        // =====================================================================================
        //  THE CLIMB (ADR 0036, amended) — carrying the body between two storeys that are drawn
        //  a storey apart. State, none of it saved: it describes half a second, not a building.
        // =====================================================================================

        StairTraversal _climb;
        Vector2 _climbWrote;
        IsoCharacterSprite _climbSkin;
        bool _climbClaimedMoveAxis;

        /// <summary>How far the occupant may be from where the climb last put them before the climb
        /// concludes something else owns the body. Generous enough that a physics settle or a float
        /// wobble is not a hand-off, tight enough that any deliberate move — a spawn, a region hop, a dev
        /// teleport, all of which travel metres — is.</summary>
        const float ClimbOwnershipTolerance = 0.05f;

        /// <summary>True while the fisher is on the stairs. The storey is already changed; this is the
        /// walk. For tests, tooling and anything that wants to know why the body is moving itself.</summary>
        public bool IsClimbing => _climb != null;

        /// <summary>The climb in flight, or null. Exposed read-only so a test can watch the path it is
        /// actually playing rather than re-deriving one and hoping they agree.</summary>
        public StairTraversal Climb => _climb;

        /// <summary>
        /// Start carrying the occupant <paramref name="rise"/> — the offset between the storey they were
        /// on and the one they are now on.
        ///
        /// <para>The path starts where they are standing, so the foot of the climb is wherever on the
        /// stairwell they pressed from, and ends at the same point on the storey above. A zero rise, a
        /// zero duration or no occupant to carry all mean the same thing: they are already there.</para>
        /// </summary>
        void BeginClimb(Vector2 rise)
        {
            CancelClimb();

            Transform occupant = ResolveOccupant();
            if (occupant == null || rise.sqrMagnitude <= 0f) return;

            float seconds = GameServices.StairClimbSeconds;
            Vector2 foot = occupant.position;
            Vector2 head = foot + rise;

            if (seconds <= 0f)
            {
                // The instant swap, kept as an owner setting. Still a MOVE — the storeys are a storey
                // apart whether or not the walk between them is drawn.
                WriteOccupant(occupant, head);
                return;
            }

            _climb = new StairTraversal(foot, head, seconds);
            _climbSkin = occupant.GetComponent<IsoCharacterSprite>();   // resolved once, not per frame

            // The move keys would otherwise fight the path — and, worse, would look like another owner
            // taking the body and abandon the climb halfway up. Only claimed if it is going spare, and
            // only dropped by whoever claimed it.
            if (!MoveActionClaim.IsClaimed)
            {
                MoveActionClaim.IsClaimed = true;
                _climbClaimedMoveAxis = true;
            }

            WriteOccupant(occupant, foot);
        }

        /// <summary>
        /// Advance the climb by a frame. Returns TRUE while it still owns the body — the caller then
        /// leaves the threshold test alone for this frame.
        ///
        /// <para><b>It hands the body back the moment anything else moves it.</b> A spawn, a region
        /// travel or a dev teleport are all "somebody else is placing this fisher", and a walk that
        /// carried on regardless would drag them back to a stairwell they are no longer in. The test is
        /// simply whether they are still where the climb last put them.</para>
        ///
        /// <para><b>⚠️ The gait is held HERE, in Update.</b> <c>IsoCharacterSprite</c> measures its speed
        /// from position deltas in <c>LateUpdate</c>; a climb writes ~2.4 world units of RISE in about
        /// half a second, so a measured walker reads ~5 m/s and draws a sprint while covering no ground
        /// at all. The hold has to be in force before that measurement, which means this frame, from
        /// here — a hold applied in <c>LateUpdate</c> is a frame late every frame. See
        /// <see cref="StairTraversal.GaitSpeedMetresPerSecond"/> for why the honest number is zero.</para>
        /// </summary>
        bool TickClimb(Transform occupant, float deltaSeconds)
        {
            if (_climb == null) return false;

            if (((Vector2)occupant.position - _climbWrote).sqrMagnitude >
                ClimbOwnershipTolerance * ClimbOwnershipTolerance)
            {
                CancelClimb();
                return false;
            }

            if (_climbSkin != null) _climbSkin.HoldSpeed(StairTraversal.GaitSpeedMetresPerSecond);

            Vector2 next = _climb.Advance(deltaSeconds);
            WriteOccupant(occupant, next);

            if (!_climb.IsDone) return true;

            CancelClimb();
            return false;   // arrived: this frame's threshold test runs, and answers about the new storey
        }

        /// <summary>
        /// Advance a climb in flight by a STATED delta, exactly as a frame would. Answers whether the
        /// walk is still running.
        ///
        /// <para>⚠️ <b>PUBLIC ON PURPOSE — DO NOT TIDY THIS AWAY.</b> EditMode runs no game loop, and
        /// <c>Time.deltaTime</c> there is whatever the editor last measured — frequently zero. A headless
        /// test that drove the climb by ticking <c>Update</c> would therefore either hang on a walk that
        /// never advances or pass because it advanced in one step, and neither result would be about the
        /// climb. This is the same seam <see cref="WakeAt"/> is: the RULE, callable, with the frame loop
        /// proving the wiring in PlayMode.</para>
        /// </summary>
        public bool AdvanceClimb(float deltaSeconds)
        {
            Transform occupant = ResolveOccupant();
            if (occupant == null) { CancelClimb(); return false; }

            TickClimb(occupant, deltaSeconds);
            return IsClimbing;
        }

        /// <summary>Give the body back — the gait to measurement, the move axis to the player, the walk
        /// to nobody. Idempotent, and safe to call when no climb is running, because every exit from a
        /// climb (arrival, interruption, the house being disabled) comes through here.</summary>
        void CancelClimb()
        {
            if (_climbSkin != null) _climbSkin.ReleaseSpeed();
            _climbSkin = null;

            if (_climbClaimedMoveAxis)
            {
                MoveActionClaim.IsClaimed = false;
                _climbClaimedMoveAxis = false;
            }

            _climb = null;
        }

        /// <summary>Put the occupant somewhere and remember doing it — the memory is what lets
        /// <see cref="TickClimb"/> tell its own writes from somebody else's. Z is left alone: nothing in
        /// this game moves a walker in Z, and taking it to zero would flatten whatever does.</summary>
        void WriteOccupant(Transform occupant, Vector2 position)
        {
            occupant.position = new Vector3(position.x, position.y, occupant.position.z);
            _climbWrote = position;
        }

        /// <summary>
        /// Show exactly one storey, or the shell.
        ///
        /// <para><b>The inactive storey is switched OFF, not sorted behind.</b> That is what keeps a
        /// second level out of the Y-sort argument entirely: there is never a frame in which a bed
        /// upstairs and a table downstairs are both drawn and have to be ranked against each other, so
        /// the band (ADR 0032) is not asked a question it has no answer to. It is also why the swap
        /// stays a swap and not a fade — a half-transparent upstairs would be exactly that frame.</para>
        ///
        /// <para><b>The building's walls are absent from here on purpose.</b> They are the same walls on
        /// both storeys and they are never switched off (see the class remarks). Only
        /// <see cref="_upperWalls"/> — the partition and the doorway plug, which exist upstairs and
        /// nowhere else — rides the level.</para>
        /// </summary>
        void Apply(bool inside)
        {
            bool ground = inside && Level == 0;
            bool upper = inside && Level == 1;

            if (_shell != null) _shell.enabled = !inside;
            if (_room != null) _room.enabled = ground;
            if (_props != null && _props.gameObject.activeSelf != ground)
                _props.gameObject.SetActive(ground);

            if (_upperRoom != null) _upperRoom.enabled = upper;
            if (_upperProps != null && _upperProps.gameObject.activeSelf != upper)
                _upperProps.gameObject.SetActive(upper);
            if (_upperWalls != null && _upperWalls.gameObject.activeSelf != upper)
                _upperWalls.gameObject.SetActive(upper);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Draw the footprint, the walls and the threshold in the Scene view. The reason this exists:
        /// every failure mode of this component is silent — a room whose walls are half a metre out
        /// still draws perfectly — so the geometry is made visible where it is authored.
        /// </summary>
        void OnDrawGizmosSelected()
        {
            InteriorFootprint f = Footprint;

            Gizmos.color = new Color(0.35f, 0.85f, 0.95f, 0.9f);
            Vector2[] corners = f.Corners();
            for (int i = 0; i < corners.Length; i++)
                Gizmos.DrawLine(corners[i], corners[(i + 1) % corners.Length]);

            Gizmos.color = new Color(0.95f, 0.75f, 0.25f, 0.9f);
            foreach (var quad in f.WallQuads(_wallThicknessMetres, _doorwayWidthMetres))
                for (int i = 0; i < quad.Length; i++)
                    Gizmos.DrawLine(quad[i], quad[(i + 1) % quad.Length]);

            Gizmos.color = new Color(0.35f, 0.95f, 0.45f, 1f);
            Gizmos.DrawWireSphere(f.DoorWorld, 0.35f);
        }
#endif
    }
}
