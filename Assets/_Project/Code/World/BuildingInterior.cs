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
    /// shows the storey above; coming down reverses it. There is no scene load, no teleport, no pocket
    /// room somewhere off the map, and — the part that makes it cheap — <b>nothing moves</b>: the
    /// stairwell stands at the same footprint position on both storeys, so the occupant is already
    /// where the other storey's stair is.
    ///
    /// <para>Three consequences worth stating, because each is a bug that did not have to be written.
    /// <b>Y-sort never has to rank the two storeys</b> against each other, because the one you are not
    /// on is switched OFF rather than sorted behind — the band (ADR 0032) is never asked a question it
    /// has no answer to. <b>The inside test does not change at all</b>: it is the same footprint, so
    /// <see cref="Footprint"/>, the walls and the threshold are shared and there is exactly one
    /// definition of being in this building. And <b>the level resets on the way out</b>, so a house can
    /// never be re-entered into its bedroom.</para></para>
    ///
    /// <para>Visual + collision only: no sim, no save, no allocation per frame (rule 5, rule 7).</para>
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

        /// <summary>
        /// <b>Which storey the occupant is on</b> — 0 the ground floor, 1 the storey above. Always 0 in a
        /// building with no upper level, and forced back to 0 the moment the occupant steps outside
        /// (<see cref="Update"/>), so you can never re-enter a house and find yourself already upstairs.
        ///
        /// <para>An int rather than a bool because the mechanism is a LADDER, not a toggle: a third
        /// storey, a cellar (−1 would need the floor set widening, not the type changing) or a loft are
        /// all the same swap one rung further, and a bool would have to be replaced rather than
        /// extended. Only 0 and 1 are reachable today — <see cref="TryGoToLevel"/> is the only writer
        /// and it refuses anything else.</para>
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
        /// editor and a stale cache would keep the old walls.</summary>
        public InteriorFootprint Footprint =>
            new InteriorFootprint(transform.position, _widthMetres, _lengthMetres,
                                  _facing, _facings, _groundDepthScale,
                                  _doorOnPlusY ? 1f : -1f, _doorAcrossMetres);

        /// <summary>The doorway in world units — the threshold, and where a spawn or a "you are here"
        /// marker belongs.</summary>
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
        /// </summary>
        public void ConfigureUpperLevel(SpriteRenderer upperRoom, Transform upperProps, Transform upperWalls)
        {
            _upperRoom = upperRoom;
            _upperProps = upperProps;
            _upperWalls = upperWalls;
            Level = 0;
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
        /// <para><b>Nothing moves.</b> The occupant is not teleported and the camera is not cut: the
        /// stairwell is at the SAME footprint position on both storeys, so a player standing at the foot
        /// of the stairs is already standing at the head of them. That is the whole reason a second
        /// storey can be a layer swap rather than a room to travel to — and it is why the builder places
        /// the two stair fixtures on one shared model coordinate rather than two.</para>
        /// </summary>
        public bool TryGoToLevel(int level)
        {
            if (!IsInside) return false;
            if (level < 0 || level > TopLevel) return false;
            if (level == Level) return false;

            Level = level;
            Apply(true);
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
            Apply(false);
        }

        void Update()
        {
            Transform occupant = ResolveOccupant();
            if (occupant == null) return;

            // Hysteresis on the way out only: the inner rectangle is the entry line, and leaving takes
            // a little more than arriving at it, so a player standing in the doorway does not strobe the
            // whole house.
            float inset = IsInside
                ? _wallThicknessMetres - _hysteresisMetres
                : _wallThicknessMetres;

            bool inside = Footprint.Contains(occupant.position, inset);
            if (inside == IsInside) return;

            IsInside = inside;

            // Leaving puts you back on the ground floor. You cannot normally walk OUT while upstairs —
            // the doorway is plugged up there — but a spawn, a region travel or a dev teleport can all
            // move an occupant across the threshold without using the door, and a house that remembered
            // "upstairs" would then open on its first-floor bedroom the next time you walked in.
            if (!inside) Level = 0;

            Apply(inside);
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
