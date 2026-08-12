using UnityEngine;

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

        [Header("Who can be inside")]
        [Tooltip("The on-foot player. Serialized rather than searched for, the same way WorldInteractor " +
                 "takes its player — the builder knows it and this module must not reference Player.")]
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

        /// <summary>The wall thickness and doorway gap this room's colliders must be built with — read
        /// back so the builder cannot construct walls that disagree with the inside test.</summary>
        public float WallThicknessMetres => _wallThicknessMetres;

        public float DoorwayWidthMetres => _doorwayWidthMetres;

        /// <summary>The on-foot player, handed in by whoever built the scene. Mirrors
        /// <c>WorldInteractor.SetPlayer</c> so the World module never reaches into Player.</summary>
        public void SetOccupant(Transform occupant) => _occupant = occupant;

        void OnEnable()
        {
            // Start OUTSIDE and show it, so a room that is never entered is never visible and a scene
            // that was saved mid-visit does not open with the roof off.
            IsInside = false;
            Apply(false);
        }

        void Update()
        {
            if (_occupant == null) return;

            // Hysteresis on the way out only: the inner rectangle is the entry line, and leaving takes
            // a little more than arriving at it, so a player standing in the doorway does not strobe the
            // whole house.
            float inset = IsInside
                ? _wallThicknessMetres - _hysteresisMetres
                : _wallThicknessMetres;

            bool inside = Footprint.Contains(_occupant.position, inset);
            if (inside == IsInside) return;

            IsInside = inside;
            Apply(inside);
        }

        void Apply(bool inside)
        {
            if (_shell != null) _shell.enabled = !inside;
            if (_room != null) _room.enabled = inside;
            if (_props != null && _props.gameObject.activeSelf != inside)
                _props.gameObject.SetActive(inside);
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
