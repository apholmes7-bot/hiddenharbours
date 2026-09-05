using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>Everything a mesh hull needs to draw — the payload ADR 0022 phase 2's extraction
    /// produces, in plain runtime types. Phase 4 owns turning this into a baked asset; phase 3
    /// deliberately does not invent that format.</summary>
    public sealed class IsoFacetHullSetup
    {
        /// <summary>The extracted hull mesh (RigMeshBuilder layout: flat per-face normals, UV0 =
        /// (materialId, faceBias b, depthBias db, 0)).</summary>
        public Mesh Mesh;
        /// <summary>Palette ramp per rig material, in rig MATS order.</summary>
        public Color32[][] Ramps;
        /// <summary>Per-material constant ramp-index offset (the rig's <c>off</c>).</summary>
        public int[] RampOffsets;
        /// <summary>The INTERIOR's own palette, in its own index space
        /// (<see cref="HiddenHarbours.Core.HullMeshDef.InteriorRamps"/>). Null or empty on every
        /// hull with no interior geometry — the shipped answer for most of the fleet.</summary>
        public Color32[][] InteriorRamps;
        /// <summary>Per-interior-material ramp-index offset, parallel to
        /// <see cref="InteriorRamps"/>.</summary>
        public int[] InteriorRampOffsets;
        /// <summary>The rig's LN, normalised, in the rig's own right-handed frame. The component
        /// applies the reflection sign for the shader — hand it over untouched.</summary>
        public Vector3 LightN;
        public float Gain, Bias;
        /// <summary>The rig's 4×4 ordered-dither thresholds, already (v+0.5)/16, row-major
        /// <c>[x*4 + y]</c> — indexed <c>BAYER[x&amp;3][y&amp;3]</c> like the rig.</summary>
        public float[] Bayer16;
        public Color32 Keyline;
        /// <summary>Cell pivot in pixels from the cell's TOP-LEFT — the rig's screen origin, and
        /// the origin the hull-frame dither is phased against (ADR 0022's <c>_DitherPhase</c>).</summary>
        public Vector2 PivotPx;
        public int PxPerMetre;
        public int CellW, CellH;
        /// <summary>The rig's bake elevation (degrees above the horizon; 40 for the boat rigs).</summary>
        public float ElevationDeg;
        /// <summary>The watertight clamp line (metres above the KEEL) — the lowest open interior
        /// surface; the calibrated waterline may never climb past it
        /// (<see cref="HiddenHarbours.Core.HullMeshDef.WatertightDeckHeightMeters"/>).
        /// 0 (the default) = clamp off, the pre-fix render byte-identical.</summary>
        public float WatertightDeckHeightMeters;
        /// <summary>The clamp's half-beam reach (rig ground metres) — the far-rail residual's
        /// exact term (<see cref="HiddenHarbours.Core.HullMeshDef.WatertightHalfBeamMeters"/>).</summary>
        public float WatertightHalfBeamMeters;
        /// <summary>The lamps this hull burns at night, in her own rig metres
        /// (<see cref="HiddenHarbours.Core.HullMeshDef.Lamps"/>). Null or empty = no lamps, which is
        /// the shipped answer for every hull whose rig has not been measured: absence is data.</summary>
        public HiddenHarbours.Core.HullLamp[] Lamps;
        /// <summary>The windows of the room her cabin glow lights, in her own rig metres
        /// (<see cref="HiddenHarbours.Core.HullMeshDef.Panes"/>). Null or empty = no lit windows,
        /// which is the shipped answer for every open boat: absence is data.</summary>
        public HiddenHarbours.Core.HullPane[] Panes;
    }

    /// <summary>
    /// Draws one rig-extracted hull mesh through the facet pipeline (ADR 0022 phase 3).
    ///
    /// <para><b>How the image reaches the screen.</b> This component owns two children:</para>
    /// <list type="bullet">
    /// <item><b>"FacetMesh"</b> — a MeshRenderer whose only shader pass is LightMode
    /// <c>HHHullFacet</c>. The 2D renderer's own passes skip it (no Universal2D/SRPDefaultUnlit
    /// pass); it exists so URP culls it and <see cref="IsoFacetHullFeature"/>'s renderer list can
    /// draw it OFF-SCREEN with a private z-buffer. It must not draw in-scene: a mesh writing the
    /// shared depth buffer punches holes in every later sprite that z-tests (the hull's bow sits
    /// at z &lt; 0, closer than every sprite's z = 0 plane).</item>
    /// <item><b>"HullOverlay"</b> — a cell-sized quad with an ordinary Universal2D pass that
    /// re-composes THIS hull's pixels (keyline included) from the feature's resolved screen
    /// texture. The quad is what sorts against SpriteRenderers, whole-object, through the same
    /// SortingGroup workaround mesh renderers already need — so a crew sprite above the boat
    /// covers hull AND keyline, exactly as it covers a baked sprite's inked outline.</item>
    /// </list>
    ///
    /// <para><b>Hull-frame dither with no calibration.</b> The spike phased a screen-space Bayer
    /// lookup per render-target convention (<c>_DitherPhase</c>, probed at runtime). Production
    /// derives the dither index in the FRAGMENT from world position instead:
    /// <c>cell = (worldXY − hullOrigin)·PPU (+pivot, y flipped)</c> — the same number by
    /// construction, immune to render-target y-flip conventions, and locked to the hull's frame
    /// so translation cannot make it crawl (the 13–16% crawl class ADR 0022 measured).</para>
    ///
    /// <para><b>Pose inputs are RIG units</b> (<see cref="HeadingDirUnits"/>: 1 = 45° CCW,
    /// fractional = continuous heading; roll/pitch degrees; heave pixels). Mapping the game's
    /// compass heading (and the per-artwork mirror question) onto rig units is phase 4's seam —
    /// keeping it out of here keeps the golden master a pure statement about rendering.</para>
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class IsoFacetHullRenderer : MonoBehaviour,
        HiddenHarbours.Core.IHullMeshRenderer, HiddenHarbours.Core.IHullCutaway
    {
        [Tooltip("Heading in RIG dir units (1 unit = 45°, CCW, fractional allowed).")]
        [SerializeField] private float _headingDirUnits;
        [SerializeField] private float _rollDegrees;
        [SerializeField] private float _pitchDegrees;
        [SerializeField] private float _heavePixels;
        [SerializeField] private float _ridePixels;

        private IsoFacetHullSetup _setup;
        private Material _facetMaterial;
        private Material _overlayMaterial;
        private Texture2D _rampTex;
        private Texture2D _darkRampTex;
        private Texture2D _rampTexInterior;
        private Texture2D _darkRampTexInterior;
        private Mesh _overlayQuad;
        private Transform _meshChild;
        // The overlay quad's transform, cached like _meshChild: ApplyPose moves it every frame
        // (the compositing window follows the ride) and rule 7 forbids a per-frame lookup.
        private Transform _overlayChild;
        private MeshRenderer _meshRenderer;
        private MeshRenderer _overlayRenderer;
        private SortingGroup _sortingGroup;
        private MaterialPropertyBlock _props;
        private int _hullId;
        // THE DECK-OCCUPANT SPLIT (owner playtest 2026-08-07: sprites showing through closed
        // cabins). A contiguous BLOCK of registry ids — one per occupancy band — held for this
        // hull's whole registered life so it is stable across the frames a figure boards and leaves,
        // plus a fixed array of who is standing where in rig metres. Nobody aboard ⇒ no active slot
        // ⇒ the facet shader's loop never runs and every pixel carries _hullId, exactly as before
        // this existed.
        private int _foreHullId;
        private readonly DeckSlot[] _deckSlots = new DeckSlot[DeckOccupantSlots];
        // Reused every frame — a fresh array per LateUpdate would allocate on every hull in the
        // scene (rule 7). Always sent whole, so a released slot's stale depth cannot survive on the
        // GPU behind a w of 0.
        private readonly Vector4[] _deckSlotVectors = new Vector4[DeckOccupantSlots];
        // Scratch for the rank read — packed with the ACTIVE depths and handed to the Core rule.
        // A field, not a local, so consulting a slot allocates nothing (rule 7).
        private readonly float[] _activeDepths = new float[DeckOccupantSlots];
        private DeckOccupantSlotView _deckOccupants;
        // The slot the single-occupant shim holds, or -1. Claimed on the first ACTIVE write and kept
        // for the hull's life, exactly like any other claimant's.
        private int _legacySlot = -1;
        private bool _poseDirty = true;
        // The watertight clamp's footprint scan radius (half the cell width in world metres) —
        // derived once at Configure, cached (rule 7: ApplyPose runs every frame).
        private float _footprintRadiusMeters;
        // Does this hull's baked mesh actually carry the per-face INTERIOR flag (ADR 0023)? Read
        // once at Configure. It is what makes the rollout safe in either order: a hull baked
        // before the classifier existed keeps the shipped whole-hull clamp, a re-baked one is
        // guarded per pixel instead, and a half-re-baked project is never wrong in a NEW way.
        private bool _hasInteriorFaces;

        // THE CUTAWAY (owner ruling 2026-08-26). Read once at Configure, from the MESH — the same
        // shape as _hasInteriorFaces above and for the same reason: a hull whose rig gained level
        // tags but whose mesh has not been re-baked must keep the shipped whole-exterior picture
        // rather than acquire a cut she has no geometry for. A cheap vertex-attribute question, not
        // a scan: TexCoord1 is present only when the builder wrote it.
        private bool _hasLevelTags;

        // ⚠️ DOES THIS HULL'S MESH CONTAIN A ROOM? Read from the mesh for the same reason
        // _hasInteriorFaces is: a positive test about what the geometry actually holds, not a
        // version stamp that could say yes while the mesh says nothing.
        private bool _hasRoomGeometry;
        private HiddenHarbours.Core.HullMeshDef.Cut _cutaway;

        /// <summary>
        /// True when any face of <paramref name="mesh"/> is flagged as ROOM geometry (TexCoord1.y,
        /// ADR 0038 full mesh) — as distinct from <see cref="MeshCarriesInteriorFaces"/>, which asks
        /// the ADR 0023 water question off UV0.w. Two different meanings of the word "interior" that
        /// happen to live one channel apart, so they are two named methods rather than one.
        /// </summary>
        private static bool MeshCarriesRoomGeometry(Mesh mesh)
        {
            if (mesh == null) return false;
            if (!mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord1)) return false;
            var tags = new List<Vector2>(mesh.vertexCount);
            mesh.GetUVs(LevelUvChannel, tags);
            for (int i = 0; i < tags.Count; i++)
                if (tags[i].y > 0.5f) return true;
            return false;
        }

        /// <summary>UV1 — where the bake writes (level id, is-room). Mirrors
        /// <c>RigMeshBuilder.LevelUvChannel</c>, which is editor-only and cannot be referenced here.</summary>
        private const int LevelUvChannel = 1;

        /// <summary>
        /// True when any face of <paramref name="mesh"/> is flagged interior (UV0.w). Allocates
        /// once per Configure — never per frame (rule 7) — and is deliberately a POSITIVE test:
        /// "this mesh knows about interiors", not "this mesh is new enough", so it cannot be fooled
        /// by a version stamp that says yes while the geometry says nothing.
        /// </summary>
        private static bool MeshCarriesInteriorFaces(Mesh mesh)
        {
            if (mesh == null) return false;
            var uvs = new List<Vector4>(mesh.vertexCount);
            mesh.GetUVs(0, uvs);
            for (int i = 0; i < uvs.Count; i++)
                if (uvs[i].w > 0.5f) return true;
            return false;
        }

        /// <inheritdoc/>
        public bool CarriesLevelTags => _hasLevelTags;

        /// <inheritdoc/>
        public HiddenHarbours.Core.HullMeshDef.Cut CutawayShown => _cutaway;

        /// <summary>
        /// <b>Cut this hull open at <paramref name="levelTag"/></b>, or 0 to close her up — the Core
        /// <see cref="HiddenHarbours.Core.IHullCutaway"/> seam, driven by the Boats lane from
        /// <c>CabinSignals</c> and helm occupancy.
        ///
        /// <para><b>Two writes, and they are deliberately different in kind.</b> The KEYWORD is
        /// per-material state and is toggled here, on this component's OWN instance material
        /// (<c>new Material(...)</c>, <c>HideAndDontSave</c>, built in <c>BuildFacetMaterial</c>) —
        /// never on a <c>sharedMaterial</c> reached off an asset, which would dirty the asset, and
        /// never through <c>Shader.EnableKeyword</c>, which does not reach a <c>_local</c> keyword at
        /// all. The LEVEL is per-draw state and rides the property block out of
        /// <see cref="ApplyPose"/> with <c>_HullId</c> and the deck-occupant slots, so two sister
        /// ships in one creek can hold two different answers.</para>
        ///
        /// <para><b>A hull with no tags stays whole, silently.</b> Most of the fleet has no cutaway
        /// geometry and never will; that is data, not a fault. Refusing loudly here would log once
        /// per boarding, forever, on every boat the kit has not reached.</para>
        ///
        /// <para>Idempotent, and free when nothing changed — which matters because the natural caller
        /// is a transition handler that would rather not remember what it last said.</para>
        /// </summary>
        public void ShowCutaway(HiddenHarbours.Core.HullMeshDef.Cut cut)
        {
            // A hull with no tags cannot be cut, and a cut with no level cannot have a lid: both
            // collapse to None here rather than at four call sites.
            if (!_hasLevelTags || cut.Level <= 0) cut = HiddenHarbours.Core.HullMeshDef.Cut.None;
            else if (cut.Lid < 0) cut = new HiddenHarbours.Core.HullMeshDef.Cut(cut.Level, 0);
            if (cut.Level == _cutaway.Level && cut.Lid == _cutaway.Lid) return;

            _cutaway = cut;
            ApplyCutawayKeyword();
            ApplyPose();          // the level and its lid travel in the property block
        }

        // ⚠️⚠️ A HULL THAT CARRIES A ROOM MUST KEEP THE KEYWORD ON, EVEN CLOSED UP.
        //
        // The original rule here was "on only while a cut is live", which was right while no mesh
        // contained interior geometry: the gate's only job was to CULL the hull's own faces. Full
        // mesh interiors (ADR 0038) changed the premise — the room's faces now live in the hull
        // mesh, and the only thing that hides them is HHLevelDiscards, which exists only inside this
        // keyword's #ifdef. Turning it off on a hull with a room does not restore the shipped
        // picture; it draws her cabin through her own topsides, in the hull's palette, from every
        // angle. MEASURED when this was wrong: 31-42% of her inked pixels differed from her baked
        // sheet, in single connected clusters of 11k-15k px.
        //
        // So the honest cost statement is per hull, not fleet-wide: a hull with no room pays nothing
        // (her program is byte-identical, 1362/1878), and a CONVERTED hull pays the gate's discard
        // always. That is the price of the room being geometry, and it is the same price whichever
        // palette design had been chosen.
        private void ApplyCutawayKeyword()
        {
            if (_facetMaterial == null) return;
            if (_cutaway.Opens || _hasRoomGeometry)
                _facetMaterial.EnableKeyword(IsoFacetShaderIds.LevelGateKeyword);
            else _facetMaterial.DisableKeyword(IsoFacetShaderIds.LevelGateKeyword);
        }

        /// <summary>Heading in rig dir units (1 = 45° CCW). Continuous — that is the point.</summary>
        public float HeadingDirUnits
        {
            get => _headingDirUnits;
            set { if (!Mathf.Approximately(_headingDirUnits, value)) { _headingDirUnits = value; _poseDirty = true; } }
        }

        public float RollDegrees
        {
            get => _rollDegrees;
            set { if (!Mathf.Approximately(_rollDegrees, value)) { _rollDegrees = value; _poseDirty = true; } }
        }

        public float PitchDegrees
        {
            get => _pitchDegrees;
            set { if (!Mathf.Approximately(_pitchDegrees, value)) { _pitchDegrees = value; _poseDirty = true; } }
        }

        /// <summary>Heave in rig PIXELS (the rig's own unit; world metres = px / PPU). The TOTAL
        /// screen lift — the rig's rock plus <see cref="RidePixels"/>.</summary>
        public float HeavePixels
        {
            get => _heavePixels;
            set { if (!Mathf.Approximately(_heavePixels, value)) { _heavePixels = value; _poseDirty = true; } }
        }

        /// <summary>
        /// How much of <see cref="HeavePixels"/> is the displaced sea's WORLD RIDE rather than the
        /// rig's own rock (the Core seam's <c>RidePixels</c>) — the amount the compositing window
        /// must travel with the hull. See <see cref="ApplyPose"/>. 0 ⇒ the pre-split render.
        /// </summary>
        public float RidePixels
        {
            get => _ridePixels;
            set { if (!Mathf.Approximately(_ridePixels, value)) _ridePixels = value; }
        }

        /// <summary>The id in [1,255] this hull writes into the facet buffer's alpha. 0 = not registered.</summary>
        public int HullId => _hullId;

        /// <summary>
        /// <b>This hull's y→z shear, as the facet shader wants it</b> (ADR 0033): x = the shear
        /// <c>g = cos(elev)(1−sin(elev))/sin(elev)</c> from her OWN bake elevation, y = the world y
        /// it is referenced to. All-zero while no displaced sea is live — no sea, no shear, exactly
        /// as there is no depth bias.
        ///
        /// <para>Computed on demand rather than cached from the last <see cref="ApplyPose"/> because
        /// a bolted-on fitting reads it from its own <c>LateUpdate</c>, and the two components'
        /// execution order is not fixed: a cached value would leave a fitting drawn through last
        /// frame's gradient on the frame a sea appears. Costs a static struct read (rule 7).</para>
        /// </summary>
        public Vector4 DepthShear
        {
            get
            {
                if (_setup == null ||
                    !DisplacedWaterRegistry.TryGetIsoDepthFrame(out WaterIsoDepthFrame frame))
                    return Vector4.zero;
                return new Vector4(DisplacedWaterMath.HullDepthShear(_setup.ElevationDeg),
                                   frame.ReferenceY, 0f, 0f);
            }
        }

        /// <summary>
        /// <b>The number of deck occupants one hull can hide at once, and it is MEASURED.</b>
        ///
        /// <para>The stern-deck loop's worst beat — the deck-loop kit's own twelve-beat shift table,
        /// with <c>TrapIso.CAPS</c> for what each surface holds — puts TEN things on a lobster boat's
        /// deck at the same time: two hands (skipper at the hauler, sternman at the bench), four
        /// pieces of working furniture (hauler station, banding bench, chopping board, bait bin), two
        /// trap stacks (deck and washboard cap), the pot in play, and the fish tray. Twelve is that
        /// plus two.</para>
        ///
        /// <para><b>What the number costs</b> is the id budget, not the per-pixel loop (which is one
        /// compare per slot behind a single early-out that is false on every hull with nobody
        /// aboard). Each hull reserves 1 + this many of the 255 ids the facet alpha can carry, so
        /// twelve leaves <b>19</b> simultaneous mesh hulls against a roadmap whose largest scene is a
        /// harbour of a dozen. Raise it and that headroom shrinks; lower it and the surplus occupants
        /// are refused, loudly, and draw un-occluded.</para>
        ///
        /// <para>⚠️ <b>The facet shader holds this same number as a literal</b>
        /// (<c>HH_DECK_OCCUPANT_SLOTS</c>) because HLSL needs a compile-time array bound.
        /// <c>DeckOccupantSlotTests</c> asserts the two agree — change one and that test fails,
        /// which is the only thing standing between a mismatch and a silently truncated deck.</para>
        /// </summary>
        public const int DeckOccupantSlots = 12;

        /// <summary>
        /// The BASE of this hull's contiguous FORE BLOCK — the first of the ids she writes into the
        /// facet alpha for geometry NEARER the camera than somebody standing on her deck. 0 when she
        /// is not registered, or when the id pool could not spare a block.
        ///
        /// <para>Band <i>m</i> (a pixel in front of <i>m</i> occupants) is written as
        /// <c>ForeHullId + m - 1</c>, so the sprite that must be hidden behind her wheelhouse discards
        /// across a RANGE of this block (<c>HiddenHarbours/DeckOccludedSprite</c>). The block is
        /// handed out once at registration and held for life, not per boarding: a figure stepping
        /// aboard must not be able to change what the boat is drawing with, and a per-frame id would
        /// make the split a race between two components' execution order.</para>
        /// </summary>
        public int ForeHullId => _foreHullId;

        /// <summary>The last id of that block — the top of every occupant's discard range, and what
        /// stops one boat's ids cutting holes in another boat's crew. 0 when she holds no block.</summary>
        public int ForeHullIdTop => _foreHullId == 0 ? 0 : _foreHullId + DeckOccupantSlots - 1;

        /// <summary>
        /// <b>Who is standing on this deck</b> — the Core seam (<see cref="IDeckOccupantSlots"/>)
        /// through which a rider, a trap stack or a piece of gear claims its own depth on this hull.
        /// One view object per hull, made on first use and reused (rule 7).
        /// </summary>
        public IDeckOccupantSlots DeckOccupants => _deckOccupants ??= new DeckOccupantSlotView(this);

        /// <summary>How many slots are claimed AND standing — the facet shader's early-out, and the
        /// number a test reads to know the split is armed.</summary>
        public int ActiveDeckOccupants
        {
            get
            {
                int n = 0;
                for (int i = 0; i < DeckOccupantSlots; i++) if (_deckSlots[i].Active) n++;
                return n;
            }
        }

        /// <summary>
        /// The id an occludable sprite should be told to discard against for the SINGLE-OCCUPANT
        /// shim (<see cref="SetDeckOccupant(Vector3, bool)"/>) — 0 whenever nothing is being split.
        /// Pair it with <see cref="ForeHullIdTop"/>; anything carrying more than one occupant should
        /// be going through <see cref="DeckOccupants"/> instead.
        /// </summary>
        public float DeckOccluderId => OccluderIdForSlot(_legacySlot);

        /// <summary>
        /// The child that carries this hull's heading, rock and heave — what an articulated fitting
        /// parents to (ADR 0022 phase 7).
        ///
        /// <para>⚠️ <b>Handed out directly rather than looked up by NAME, and that is a bug fix, not a
        /// tidy-up.</b> Re-configuring this renderer (which is what a hull SWAP does) destroys the old
        /// child and builds a new one with the same name — and in play mode <c>Destroy</c> is deferred
        /// to end of frame, so for the rest of that frame BOTH exist and <c>transform.Find("FacetMesh")</c>
        /// returns the DOOMED one. Fittings attached to it were destroyed with it at end of frame:
        /// the boat you swapped TO lost her oars or her engine, silently, one frame later. Caught by
        /// <c>PilotableFleetPlayTests.Picker_SwappingDoryToSkiff_TradesTheOarsForAnOutboard</c> the
        /// moment the skiffs' engines became fittings and it had two mesh hulls to swap between.</para>
        /// </summary>
        public Transform PosedMesh => _meshChild;

        /// <summary>
        /// The lamps this hull carries, in her own rig metres — the def's table, handed straight
        /// through. Empty until she is set up, and empty forever for a hull whose rig has not been
        /// measured for lamps (absence is data, not a defect). <see cref="BoatLamps"/> reads this
        /// rather than the def, because the renderer is the one thing that knows WHICH hull is
        /// currently skinned onto this root.
        /// </summary>
        public HiddenHarbours.Core.HullLamp[] Lamps =>
            _setup != null && _setup.Lamps != null
                ? _setup.Lamps
                : System.Array.Empty<HiddenHarbours.Core.HullLamp>();

        /// <summary>
        /// The windows of the room her cabin glow lights, in her own rig metres — the def's table,
        /// handed straight through, and empty for every hull with no room to light (absence is data).
        /// <see cref="BoatWindowGlow"/> reads this rather than the def, for the same reason
        /// <see cref="BoatLamps"/> reads <see cref="Lamps"/> here: the renderer is the one thing that
        /// knows WHICH hull is currently skinned onto this root.
        /// </summary>
        public HiddenHarbours.Core.HullPane[] Panes =>
            _setup != null && _setup.Panes != null
                ? _setup.Panes
                : System.Array.Empty<HiddenHarbours.Core.HullPane>();

        public bool IsConfigured => _setup != null;

        /// <summary>The overlay quad's renderer — set sorting layer/order here (it is the only
        /// child that draws in-scene, and the thing that must sort against sprites).</summary>
        public MeshRenderer OverlayRenderer => _overlayRenderer;

        /// <summary>
        /// Where this hull sorts against sprites (the Core <see cref="HiddenHarbours.Core.IHullMeshRenderer"/>
        /// seam). Writes BOTH the SortingGroup (which is what actually competes with SpriteRenderers —
        /// mesh renderers only sort against sprites through the "sort as 2D" group workaround) and the
        /// overlay renderer itself (its in-group order; also what a group-less configuration would use).
        /// </summary>
        public void SetSorting(int sortingLayerId, int sortingOrder)
        {
            if (_sortingGroup != null)
            {
                _sortingGroup.sortingLayerID = sortingLayerId;
                _sortingGroup.sortingOrder = sortingOrder;
            }
            if (_overlayRenderer != null)
            {
                _overlayRenderer.sortingLayerID = sortingLayerId;
                _overlayRenderer.sortingOrder = sortingOrder;
            }
        }

        /// <summary>
        /// Build all GPU-side state from an extracted hull. Call once (idempotent: re-configuring
        /// releases the previous state). Everything created here is owned and destroyed by this
        /// component.
        /// </summary>
        public void Configure(IsoFacetHullSetup setup)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            if (setup.Mesh == null) throw new ArgumentException("Setup has no mesh.", nameof(setup));
            if (setup.Ramps == null || setup.Ramps.Length == 0)
                throw new ArgumentException("Setup has no ramps.", nameof(setup));
            if (setup.Ramps.Length > HiddenHarbours.Core.HullMeshDef.HullRampSlots)
                throw new ArgumentException($"{setup.Ramps.Length} materials; the facet shader's _RampMeta holds 16.");
            // The interior's table is checked SEPARATELY and against its own cap, which is the whole
            // point of it being a second table: a room that outgrows 24 must not be able to fail by
            // eating into the hull's 16, and a hull that outgrows 16 must not be rescuable by
            // spending the room's.
            int interiorCount = setup.InteriorRamps?.Length ?? 0;
            if (interiorCount > HiddenHarbours.Core.HullMeshDef.InteriorRampSlots)
                throw new ArgumentException(
                    $"{interiorCount} interior materials; the facet shader's _RampMetaInterior holds " +
                    $"{HiddenHarbours.Core.HullMeshDef.InteriorRampSlots}. Merge ramps in the " +
                    "extraction, or take a widening upstream with a measured cost — do not spend the " +
                    "hull's slots.");
            if (setup.Bayer16 == null || setup.Bayer16.Length != 16)
                throw new ArgumentException("Bayer16 must be exactly 16 thresholds.", nameof(setup));

            ReleaseOwned();
            _setup = setup;
            _footprintRadiusMeters = DisplacedWaterMath.FootprintRadiusMeters(
                setup.CellW, setup.PxPerMetre);
            _hasInteriorFaces = MeshCarriesInteriorFaces(setup.Mesh);
            _hasLevelTags = setup.Mesh.HasVertexAttribute(
                UnityEngine.Rendering.VertexAttribute.TexCoord1);
            _hasRoomGeometry = MeshCarriesRoomGeometry(setup.Mesh);
            // A re-Configure (a repaint, a hull swap) must not leave a cut standing on geometry that
            // is no longer the same geometry — and must not leave the keyword on a material that has
            // just been rebuilt underneath it.
            _cutaway = HiddenHarbours.Core.HullMeshDef.Cut.None;

            BuildRampTextures(setup);
            BuildFacetMaterial(setup);
            BuildChildren(setup);
            ApplyCutawayKeyword();
            _poseDirty = true;
            ApplyPose();
        }

        /// <summary>
        /// Re-aim the watertight clamp on a hull that is already configured — the two floats, and
        /// nothing else.
        ///
        /// <para><b>Who needs this.</b> An AMPHIBIOUS vehicle (ADR 0035's follow-up), which is the
        /// first thing in the fleet whose relationship with the water changes while she is alive:
        /// ashore she carries the road vehicle's <c>0/0</c> — the documented "clamp off" — and afloat
        /// she carries her own published waterline. A boat's clamp is a fact about her hull and is set
        /// once at <see cref="Configure"/>; hers is a fact about the medium she is in. Re-configuring
        /// to change it would rebuild every ramp texture, the material and both children to move two
        /// numbers.</para>
        ///
        /// <para>Allocation-free and idempotent, so a caller may write it every frame — though the
        /// vehicle driver dirty-checks and only writes it on the swap. Inert before
        /// <see cref="Configure"/>: there is no setup to hold the values, and the next configure
        /// starts from the def's own ashore state, which is the correct place for a freshly installed
        /// machine to start.</para>
        /// </summary>
        public void SetWatertightClamp(float deckHeightMeters, float halfBeamMeters)
        {
            if (_setup == null) return;
            _setup.WatertightDeckHeightMeters = Mathf.Max(0f, deckHeightMeters);
            _setup.WatertightHalfBeamMeters = Mathf.Max(0f, halfBeamMeters);
            _poseDirty = true;   // the clamp is read inside ApplyPose; a silent change would land late
        }

        private void BuildRampTextures(IsoFacetHullSetup setup)
        {
            int maxLen = 0;
            foreach (var ramp in setup.Ramps) maxLen = Mathf.Max(maxLen, ramp.Length);

            // sRGB textures (linear:false): the ramp bytes are sRGB palette values; the GPU decodes
            // on Load and re-encodes into the sRGB targets, so palette bytes survive the trip.
            _rampTex = MakeRampTexture("HHRampTex", maxLen, setup.Ramps.Length);
            _darkRampTex = MakeRampTexture("HHDarkRampTex", maxLen, setup.Ramps.Length);

            Color32[][] dark = IsoFacetMath.BuildDarkenedRamps(setup.Ramps);
            for (int m = 0; m < setup.Ramps.Length; m++)
            {
                var ramp = setup.Ramps[m];
                for (int i = 0; i < maxLen; i++)
                {
                    int k = Mathf.Min(i, ramp.Length - 1);
                    _rampTex.SetPixel(i, m, ramp[k]);
                    _darkRampTex.SetPixel(i, m, dark[m][k]);
                }
            }
            _rampTex.Apply(false, true);
            _darkRampTex.Apply(false, true);

            BuildInteriorRampTextures(setup);
        }

        /// <summary>
        /// The interior's own pair of ramp textures — built only when the hull actually carries an
        /// interior palette, so a hull without one allocates nothing and binds nothing.
        ///
        /// <para>Separate textures rather than extra ROWS of the hull's, because the two tables are
        /// separate index spaces: interior material 0 and hull material 0 are different materials,
        /// and the shader selects the table from the bake's own per-face interior flag. Sharing one
        /// texture would mean offsetting every interior matId by the hull's count at bake time —
        /// which re-couples exactly the budget this design exists to keep apart.</para>
        /// </summary>
        private void BuildInteriorRampTextures(IsoFacetHullSetup setup)
        {
            var table = setup.InteriorRamps;
            if (table == null || table.Length == 0) return;

            int maxLen = 0;
            foreach (var ramp in table) maxLen = Mathf.Max(maxLen, ramp.Length);
            if (maxLen == 0) return;

            _rampTexInterior = MakeRampTexture("HHRampTexInterior", maxLen, table.Length);
            _darkRampTexInterior = MakeRampTexture("HHDarkRampTexInterior", maxLen, table.Length);

            Color32[][] dark = IsoFacetMath.BuildDarkenedRamps(table);
            for (int m = 0; m < table.Length; m++)
            {
                var ramp = table[m];
                for (int i = 0; i < maxLen; i++)
                {
                    int k = Mathf.Min(i, ramp.Length - 1);
                    _rampTexInterior.SetPixel(i, m, ramp[k]);
                    _darkRampTexInterior.SetPixel(i, m, dark[m][k]);
                }
            }
            _rampTexInterior.Apply(false, true);
            _darkRampTexInterior.Apply(false, true);
        }

        private static Texture2D MakeRampTexture(string name, int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false, false)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

        private void BuildFacetMaterial(IsoFacetHullSetup setup)
        {
            var facetShader = Shader.Find("HiddenHarbours/IsoFacet");
            var overlayShader = Shader.Find("HiddenHarbours/IsoFacetOverlay");
            if (facetShader == null || overlayShader == null)
                throw new InvalidOperationException(
                    "IsoFacet shaders not found — HiddenHarbours/IsoFacet and " +
                    "HiddenHarbours/IsoFacetOverlay must both import (see the compile guard test).");

            _facetMaterial = new Material(facetShader) { hideFlags = HideFlags.HideAndDontSave };
            _facetMaterial.SetTexture(IsoFacetShaderIds.RampTex, _rampTex);
            _facetMaterial.SetTexture(IsoFacetShaderIds.DarkRampTex, _darkRampTex);
            _facetMaterial.SetVector(IsoFacetShaderIds.LightN, IsoFacetMath.ShaderLightVector(setup.LightN));
            _facetMaterial.SetFloat(IsoFacetShaderIds.Gain, setup.Gain);
            _facetMaterial.SetFloat(IsoFacetShaderIds.Bias, setup.Bias);
            // The keyline colour crosses the CPU→GPU boundary as a colour PROPERTY, which is raw
            // floats: hand it over pre-linearised so the sRGB render target re-encodes the exact
            // palette byte.
            _facetMaterial.SetColor(IsoFacetShaderIds.KeyColor, ((Color)setup.Keyline).linear);
            _facetMaterial.SetVector(IsoFacetShaderIds.PivotPx, setup.PivotPx);
            _facetMaterial.SetFloat(IsoFacetShaderIds.PixelsPerMetre, setup.PxPerMetre);

            var meta = new Vector4[HiddenHarbours.Core.HullMeshDef.HullRampSlots];
            for (int m = 0; m < setup.Ramps.Length; m++)
                meta[m] = new Vector4(setup.Ramps[m].Length, setup.RampOffsets[m], 0, 0);
            _facetMaterial.SetVectorArray(IsoFacetShaderIds.RampMeta, meta);

            // THE INTERIOR TABLE. Written unconditionally when the hull has one, even though only
            // the HH_LEVEL_GATE variant reads it: the uniform costs nothing in the off variant (the
            // program does not declare it) and writing it at Configure keeps the cut itself a pure
            // two-float per-draw change, with no texture build hiding inside a boarding.
            if (_rampTexInterior != null)
            {
                _facetMaterial.SetTexture(IsoFacetShaderIds.RampTexInterior, _rampTexInterior);
                _facetMaterial.SetTexture(IsoFacetShaderIds.DarkRampTexInterior, _darkRampTexInterior);

                var interiorMeta = new Vector4[HiddenHarbours.Core.HullMeshDef.InteriorRampSlots];
                var table = setup.InteriorRamps;
                for (int m = 0; m < table.Length; m++)
                {
                    int off = setup.InteriorRampOffsets != null && m < setup.InteriorRampOffsets.Length
                              ? setup.InteriorRampOffsets[m] : 0;
                    interiorMeta[m] = new Vector4(table[m].Length, off, 0, 0);
                }
                _facetMaterial.SetVectorArray(IsoFacetShaderIds.RampMetaInterior, interiorMeta);
            }

            // BAYER[x&3][y&3]: row index is X, exactly as the rig holds it.
            var rows = new Vector4[4];
            for (int x = 0; x < 4; x++)
                rows[x] = new Vector4(setup.Bayer16[x * 4 + 0], setup.Bayer16[x * 4 + 1],
                                      setup.Bayer16[x * 4 + 2], setup.Bayer16[x * 4 + 3]);
            _facetMaterial.SetVectorArray(IsoFacetShaderIds.Bayer, rows);

            _overlayMaterial = new Material(overlayShader) { hideFlags = HideFlags.HideAndDontSave };
        }

        private void BuildChildren(IsoFacetHullSetup setup)
        {
            var meshGo = new GameObject("FacetMesh") { hideFlags = HideFlags.DontSave };
            meshGo.transform.SetParent(transform, false);
            meshGo.AddComponent<MeshFilter>().sharedMesh = setup.Mesh;
            _meshRenderer = meshGo.AddComponent<MeshRenderer>();
            _meshRenderer.sharedMaterial = _facetMaterial;
            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _meshRenderer.allowOcclusionWhenDynamic = false;
            _meshChild = meshGo.transform;

            // The overlay quad: the cell rectangle in world metres around the pivot, padded 1 px
            // on every side so the keyline (which floods 1 px OUTSIDE the silhouette) is inside it
            // even when the silhouette touches the cell edge.
            float ppu = setup.PxPerMetre;
            float pad = 1f / ppu;
            float left = -setup.PivotPx.x / ppu - pad;
            float right = (setup.CellW - setup.PivotPx.x) / ppu + pad;
            float top = setup.PivotPx.y / ppu + pad;
            float bottom = -(setup.CellH - setup.PivotPx.y) / ppu - pad;

            _overlayQuad = new Mesh { name = "HHHullOverlayQuad", hideFlags = HideFlags.HideAndDontSave };
            _overlayQuad.SetVertices(new[]
            {
                new Vector3(left, bottom, 0f), new Vector3(right, bottom, 0f),
                new Vector3(right, top, 0f), new Vector3(left, top, 0f),
            });
            _overlayQuad.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);

            var overlayGo = new GameObject("HullOverlay") { hideFlags = HideFlags.DontSave };
            overlayGo.transform.SetParent(transform, false);
            overlayGo.AddComponent<MeshFilter>().sharedMesh = _overlayQuad;
            _overlayRenderer = overlayGo.AddComponent<MeshRenderer>();
            _overlayRenderer.sharedMaterial = _overlayMaterial;
            _overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _overlayRenderer.receiveShadows = false;
            _overlayRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _overlayChild = overlayGo.transform;

            // Mesh renderers do not sort against sprites on their own (they fall back to world z)
            // — the documented workaround is a SortingGroup ("sort as 2D"), same as the water.
            _sortingGroup = GetComponent<SortingGroup>();
            if (_sortingGroup == null)
                _sortingGroup = gameObject.AddComponent<SortingGroup>();
        }

        private void OnEnable()
        {
            if (_hullId == 0)
            {
                _hullId = IsoFacetHullRegistry.Register(this);
                // …and her FORE BLOCK, taken in the same breath so the two live and die together.
                // The block is what costs the id budget (1 + DeckOccupantSlots per hull, so 19
                // simultaneous hulls out of 255) — the registry says so itself when they run out,
                // and hands back 0, which simply means she splits for nobody.
                _foreHullId = IsoFacetHullRegistry.RegisterForeBlock(DeckOccupantSlots);
            }
            _poseDirty = true;
        }

        private void OnDisable()
        {
            if (_hullId != 0)
            {
                IsoFacetHullRegistry.Unregister(this, _hullId);
                _hullId = 0;
            }
            if (_foreHullId != 0)
            {
                IsoFacetHullRegistry.UnregisterForeBlock(_foreHullId, DeckOccupantSlots);
                _foreHullId = 0;
            }
            // A hull going away carries her occupants with her: nothing may keep discarding against
            // ids that are now free for another boat to be issued.
            ClearDeckSlots();
        }

        private void OnDestroy() => ReleaseOwned();

        private void OnValidate() => _poseDirty = true;

        private void LateUpdate() => ApplyPose();

        /// <summary>
        /// Push the current pose to the transform and the per-draw material properties. Runs in
        /// LateUpdate (after gameplay wrote the pose) and allocates nothing — the property block
        /// and children are cached.
        /// </summary>
        public void ApplyPose()
        {
            if (_setup == null || _meshChild == null) return;

            if (_poseDirty)
            {
                _meshChild.localRotation = IsoFacetMath.HullRotation(
                    _headingDirUnits, _setup.ElevationDeg, _rollDegrees, _pitchDegrees);
                _meshChild.localScale = IsoFacetMath.HullScale;
                _poseDirty = false;
            }

            // Position every push, not only when the pose dirties: the calibrated-frame z below
            // tracks the ROOT's world y, which changes as the boat sails without touching the
            // pose fields.
            Vector3 offset = IsoFacetMath.HeaveOffset(_heavePixels, _setup.PxPerMetre);
            // Read ONCE and used twice below — for the compensation term and for the shader — so
            // the constant this hull's root is placed by and the gradient her vertices are drawn
            // through can never come from two different reads of the registry.
            Vector4 depthShear = DepthShear;
            // ADR 0023 phase 3 (the waterline): while a displaced sea is live, translate the
            // whole hull frame into the shared private z-buffer's calibrated iso-depth convention
            // (z = baseZ + (groundY − refY)·cosElev − heaveMetres·sinElev — the water's own
            // vertex-stage depth, applied to this hull's ground anchor and heave), so planking
            // below the lifted surface truthfully loses the z-test to the water drawn before it.
            // ⚠️ It used to be ONE CONSTANT PER HULL, on the belief that a constant was the only
            // thing that could preserve intra-hull depth relations. ADR 0033 showed the constant
            // was also the defect (a constant cancels a gradient at one line) and that a world-space
            // y→z SHEAR preserves those relations just as completely — two fragments sharing a pixel
            // share a world y under the ortho camera, so they take the identical shift. Facet
            // self-occlusion, the deck-occupant bands and the keyline darkening are all still exact;
            // the last two do not even see the shear (the facet shader keeps the rig's own depth for
            // them). No displaced sea ⇒ no frame ⇒ no bias AND no shear, and the render is
            // byte-identical to before phase 3 (the A/B contract).
            if (DisplacedWaterRegistry.TryGetIsoDepthFrame(out WaterIsoDepthFrame isoFrame))
            {
                Vector3 root = transform.position;
                float heaveMeters = _heavePixels / (float)_setup.PxPerMetre;
                // THE WATERTIGHT CLAMP (owner playtest 2026-07-23): the z-bias heave — and only
                // the z bias; the visual ride above stays the honest shared heave — is raised
                // exactly enough that no water sample on the hull's footprint (the same
                // published field the water shader lifts with, exaggeration included) can win
                // the shared z-test against any hull face above the def's deck line. Water
                // still climbs the exterior planking with every wave; it can never climb past
                // the line where it would board the boat. Deck height 0 (unset def) or a
                // silent field (no bridge) leaves this byte-inert.
                // THE MASK SUPERSEDES THE CLAMP. Once the guard pass is keeping the sea out of this
                // hull's interior per PIXEL, shoving her whole frame toward the camera as well
                // would only rebuild the very defect the mask exists to cure — boats riding high
                // with their props out of the water (owner playtest 2026-07-25). Both conditions
                // are required: the feature must have the mask on, AND this hull's mesh must
                // actually carry interior flags, so an un-rebaked hull keeps her clamp rather than
                // silently losing all protection.
                bool clampSuperseded = IsoFacetHullFeature.InteriorMaskEnabled && _hasInteriorFaces;
                float zHeaveMeters = heaveMeters;
                if (!clampSuperseded && _setup.WatertightDeckHeightMeters > 0f)
                {
                    PackedWaveField field = WaveFieldBridge.ReadPublishedField();
                    zHeaveMeters = DisplacedWaterMath.WatertightZHeaveMeters(
                        heaveMeters, _setup.WatertightDeckHeightMeters,
                        _setup.WatertightHalfBeamMeters,
                        new Vector2(root.x, root.y), _footprintRadiusMeters,
                        in field, in isoFrame);
                }
                // ADR 0033 — ONE DEPTH UNIT. The bias above lands this hull's ROOT LINE exactly in
                // the sea, and a constant can do no more than that: her own vertices carry the
                // rig's depth convention, whose ramp along the fore-aft axis runs 1/sin(elev)
                // = 1.556× steeper than the water's, so every line but the root drifted by
                // Δy·cos·(1−sin) — the stern of a north-sailing lobster boat by 1.64 m, which is
                // more false freeboard than a dory has real freeboard (#491). The shear carries the
                // rest of the hull: the shader takes (worldY − ReferenceY)·g off every vertex, and
                // the compensation here adds back what it will take at THIS hull's drawn root, so
                // the calibration above survives untouched and only the gradient changes.
                offset.z = DisplacedWaterMath.HullDepthBias(root.y, zHeaveMeters, in isoFrame)
                           + DisplacedWaterMath.HullShearCompensation(
                                 root.y, heaveMeters, depthShear.x, in isoFrame)
                           - root.z;
            }
            if (_meshChild.localPosition != offset)
                _meshChild.localPosition = offset;

            // THE COMPOSING WINDOW FOLLOWS THE RIDE (owner playtest 2026-07-25: "water shader is
            // leaking onto deck of iso hulls making boats look semi submerged").
            //
            // A mesh hull's only in-scene face is the HullOverlay quad, and its shader is a 1:1
            // screen-space Load() that exists ONLY where that quad rasterises — so a hull pixel
            // outside the quad is never composed, and what shows there is whatever sorts beneath:
            // the WaterOverlay, which covers the whole sea rect under every boat. The quad is the
            // rig CELL rect (+1 px for the keyline), baked once in BuildChildren and parented at
            // the un-heaved root.
            //
            // That was sound while HeavePixels carried only the rig's own rock — 1.0–1.6 px across
            // the fleet, and the cell is authored with margin for exactly that (the rig subtracts
            // its rock from screen y AFTER projecting, so it clips at the cell edge too: matching
            // it is the golden master). ADR 0023 phase 3 step 2 then began pushing the displaced
            // ride through the same channel in metres × PxPerMetre — 20–100× that budget. On a
            // crest the hull's image slid up out of a window that stayed behind, her top band was
            // dropped, and the sea drew through the gap: a hard horizontal cut that reads exactly
            // like a swamped boat. On the dory (cell 156 px, pivot 88 from the top) the headroom is
            // ~21 px ≈ 0.65 m, and the reference sea's crest ride is ~1.4 m.
            //
            // So the window tracks the RIDE and not the rock: the rock keeps clipping at the cell
            // as the rig does, while the boat moving through the world carries her window with her.
            // Y only — never offset.z, which is the private depth buffer's calibrated iso
            // convention (meaningless to an in-scene quad under the ortho camera, and large enough
            // to throw it out of frame). Ride 0 ⇒ localPosition 0 ⇒ byte-identical to before.
            if (_overlayChild != null)
            {
                Vector3 window = IsoFacetMath.HeaveOffset(_ridePixels, _setup.PxPerMetre);
                if (_overlayChild.localPosition != window)
                    _overlayChild.localPosition = window;
            }

            _props ??= new MaterialPropertyBlock();
            // The hull ORIGIN the dither grid is phased against is this root — NOT the heaved
            // child: the rig subtracts heave from screen y AFTER projecting, so its dither stays
            // indexed by the final screen pixel, which the world-derived cell coordinate
            // reproduces only when the origin excludes the heave offset.
            Vector3 p = transform.position;
            _props.SetVector(IsoFacetShaderIds.HullOrigin, new Vector4(p.x, p.y, 0f, 0f));
            _props.SetVector(IsoFacetShaderIds.HullShear, depthShear);
            _props.SetFloat(IsoFacetShaderIds.HullId, _hullId / 255f);
            _props.SetFloat(IsoFacetShaderIds.HullIdFore, _foreHullId / 255f);
            _props.SetFloat(IsoFacetShaderIds.HullIdForeSpan, DeckOccupantSlots);
            // THE CUTAWAY, per draw. Written unconditionally — including the 0 that means "whole
            // exterior" — so a block re-used across hulls can never carry one boat's open house onto
            // her sister. Inert while HH_LEVEL_GATE is off: nothing reads it.
            _props.SetFloat(IsoFacetShaderIds.LevelShown, _cutaway.Level);
            _props.SetFloat(IsoFacetShaderIds.LevelLid, _cutaway.Lid);
            PublishDeckSlots();
            _meshRenderer.SetPropertyBlock(_props);
            _overlayRenderer.SetPropertyBlock(_props);
        }

        /// <summary>
        /// <b>Somebody is standing on this deck HERE</b> — the rig point their feet are at
        /// (+X starboard, +Y bow, +Z up from the keel; the deck data's own frame), or
        /// <paramref name="active"/> false for nobody.
        ///
        /// <para>What it buys: hull geometry nearer the camera than that point is written with this
        /// hull's FORE id, and a sprite told <see cref="DeckOccluderId"/> discards there — so the
        /// wheelhouse covers the figure inside it, per pixel, at any heading, with no sorting-order
        /// hack and no authored cabin footprint. What it costs when nobody is aboard: one bool.</para>
        ///
        /// <para>Cheap and idempotent, safe to write every frame (rule 7): it only stores, and the
        /// depth is computed in <see cref="ApplyPose"/> where the posed transform is already to hand.</para>
        /// </summary>
        public void SetDeckOccupant(Vector3 rigLocalMeters, bool active)
        {
            if (_legacySlot < 0)
            {
                // Saying "nobody is aboard" must not burn a slot — that is the state of nearly every
                // hull, nearly always, and a claim held for it would starve the real occupants.
                if (!active) return;
                _legacySlot = ClaimSlot(s_LegacyOccupant);
                if (_legacySlot < 0) return;    // refused, and ClaimSlot has already said so loudly
            }
            SetSlot(_legacySlot, s_LegacyOccupant, rigLocalMeters, active);
        }

        // ---- the occupant slots -------------------------------------------------------------------

        /// <summary>
        /// WHERE EACH DECK OCCUPANT STANDS, in the depth the facet fragment carries — re-projected
        /// here, in <see cref="ApplyPose"/>, where the posed transform is already to hand and is the
        /// one the vertices will be drawn through this frame.
        ///
        /// <para>The whole array goes every time, deliberately: a shorter write would leave a
        /// released slot's depth live on the GPU behind a stale w, and the one thing worse than a
        /// figure showing through a cabin is a hole cut for somebody who left.</para>
        ///
        /// <para><c>_DeckOccupantCount</c> is the shader's early-out. 0 — every hull with nobody
        /// aboard, which is nearly all of them — and the slot loop never runs at all, leaving the
        /// facet alpha byte-identical to before any of this existed.</para>
        /// </summary>
        private void PublishDeckSlots()
        {
            int active = 0;
            bool live = _foreHullId != 0 && _meshChild != null;
            for (int i = 0; i < DeckOccupantSlots; i++)
            {
                if (live && _deckSlots[i].Active)
                {
                    float depth = _meshChild.TransformPoint(_deckSlots[i].RigMeters).z;
                    _deckSlots[i].ViewDepth = depth;
                    _deckSlotVectors[i] = new Vector4(depth, 0f, 0f, 1f);
                    active++;
                }
                else
                {
                    _deckSlotVectors[i] = Vector4.zero;
                }
            }
            _props.SetVectorArray(IsoFacetShaderIds.DeckOccupant, _deckSlotVectors);
            _props.SetFloat(IsoFacetShaderIds.DeckOccupantCount, active);
        }

        /// <summary>One slot: who holds it, where they stand, and the depth that was published for
        /// them. <see cref="Owner"/> null = free.</summary>
        private struct DeckSlot
        {
            public object Owner;
            public Vector3 RigMeters;
            public bool Active;
            /// <summary>The published view depth, refreshed on every <see cref="SetSlot"/> and again
            /// in <see cref="ApplyPose"/>, so the rank the CPU hands out and the bands the GPU counts
            /// are computed from the same numbers.</summary>
            public float ViewDepth;
        }

        /// <summary>The owner token the single-occupant shim claims with. Static because slots are
        /// per hull — one sentinel cannot collide with itself across two boats.</summary>
        private static readonly object s_LegacyOccupant = new object();

        /// <summary>
        /// Take a slot for <paramref name="owner"/>, or -1 when all are held (logged LOUDLY — a
        /// deck occupant that quietly failed to be occluded is exactly the defect this whole seam
        /// exists to fix, and it would look like a shader bug).
        ///
        /// <para><b>Idempotent.</b> An owner that already holds a slot gets the same index back
        /// rather than a second one, so a consumer that lost its index (a domain reload, a re-wire)
        /// recovers instead of leaking slots one boarding at a time.</para>
        /// </summary>
        private int ClaimSlot(object owner)
        {
            if (owner == null) return -1;
            for (int i = 0; i < DeckOccupantSlots; i++)
                if (ReferenceEquals(_deckSlots[i].Owner, owner)) return i;

            for (int i = 0; i < DeckOccupantSlots; i++)
            {
                if (_deckSlots[i].Owner != null) continue;
                _deckSlots[i] = new DeckSlot { Owner = owner };
                return i;
            }

            Debug.LogWarning(
                $"[IsoFacetHullRenderer] '{name}' has all {DeckOccupantSlots} deck-occupant slots " +
                $"claimed; '{owner}' is REFUSED and will draw over this hull's cabins instead of " +
                "behind them. The earlier claims keep their slots (first claim wins). Either release " +
                "a slot or raise IsoFacetHullRenderer.DeckOccupantSlots — and the shader's " +
                "HH_DECK_OCCUPANT_SLOTS with it — at the cost of simultaneous hulls.", this);
            return -1;
        }

        /// <summary>Give a slot back. Ignored unless <paramref name="owner"/> is the token it was
        /// claimed with, so a stale holder cannot evict a live one.</summary>
        private void ReleaseSlot(int slot, object owner)
        {
            if (slot < 0 || slot >= DeckOccupantSlots) return;
            if (!ReferenceEquals(_deckSlots[slot].Owner, owner)) return;
            _deckSlots[slot] = default;
            if (slot == _legacySlot && ReferenceEquals(owner, s_LegacyOccupant)) _legacySlot = -1;
        }

        /// <summary>Publish where this occupant stands. Cheap and idempotent, safe every LateUpdate:
        /// it stores and projects one point (rule 7).</summary>
        private void SetSlot(int slot, object owner, Vector3 rigLocalMeters, bool active)
        {
            if (slot < 0 || slot >= DeckOccupantSlots) return;
            if (!ReferenceEquals(_deckSlots[slot].Owner, owner)) return;
            _deckSlots[slot].RigMeters = rigLocalMeters;
            _deckSlots[slot].Active = active;
            _deckSlots[slot].ViewDepth = ViewDepthOf(rigLocalMeters);
        }

        /// <summary>
        /// The LOW id this slot's sprite must discard from, /255 — 0 when there is nothing to hide
        /// behind (unclaimed, not standing, no fore block, or the hull is not live).
        ///
        /// <para><b>It is a RANK, and that is why publishing has to come first.</b> An occupant is
        /// hidden by every band from their own depth inward, so their low id is
        /// <c>base + rank - 1</c>, where rank counts the occupants standing AT OR DEEPER THAN them —
        /// a number that depends on where everybody else is. Consulting before publishing gets last
        /// frame's ordering, which a consumer that writes both every frame corrects on the next one;
        /// it can only differ at all in the frame two occupants actually cross in depth.</para>
        /// </summary>
        private float OccluderIdForSlot(int slot)
        {
            if (_foreHullId == 0 || slot < 0 || slot >= DeckOccupantSlots) return 0f;
            if (!_deckSlots[slot].Active) return 0f;

            int n = 0;
            for (int i = 0; i < DeckOccupantSlots; i++)
                if (_deckSlots[i].Active) _activeDepths[n++] = _deckSlots[i].ViewDepth;

            // The rank rule lives in Core beside the band rule the SHADER counts, because the two
            // are one theorem and a private copy here is how they would drift apart. It counts this
            // slot itself, so it is never 0.
            int rank = DeckOccupantPartition.RankOf(_deckSlots[slot].ViewDepth, _activeDepths, n);
            return (_foreHullId + rank - 1) / 255f;
        }

        /// <summary>
        /// A rig point in the depth the facet fragment carries. Through the POSED child, not the
        /// root: it holds the rig→world map (rotation × the mirror scale) AND the heave/waterline
        /// offset, which is exactly what the vertices got — so the two numbers are commensurate by
        /// construction rather than by a duplicated copy of the iso projection. (Duplicating that
        /// projection is what the wake plume and the deck clamp both had to be fixed for.)
        /// </summary>
        private float ViewDepthOf(in Vector3 rigLocalMeters)
            => _meshChild != null ? _meshChild.TransformPoint(rigLocalMeters).z : 0f;

        /// <summary>Empty every slot — a hull going away, or being re-configured, carries her
        /// occupants with her. Nothing may keep discarding against ids that are about to be handed
        /// to another boat.</summary>
        private void ClearDeckSlots()
        {
            for (int i = 0; i < DeckOccupantSlots; i++) _deckSlots[i] = default;
            _legacySlot = -1;
        }

        /// <summary>
        /// The hull's <see cref="IDeckOccupantSlots"/> face. A small view rather than the component
        /// implementing the interface itself, so the renderer's own API keeps saying what it means
        /// (<c>Claim</c>/<c>Set</c> on a renderer would read as something about drawing).
        /// </summary>
        private sealed class DeckOccupantSlotView : IDeckOccupantSlots
        {
            private readonly IsoFacetHullRenderer _hull;
            internal DeckOccupantSlotView(IsoFacetHullRenderer hull) => _hull = hull;

            public int Capacity => DeckOccupantSlots;
            public int ActiveCount => _hull.ActiveDeckOccupants;
            public int Claim(object owner) => _hull.ClaimSlot(owner);
            public void Release(int slot, object owner) => _hull.ReleaseSlot(slot, owner);
            public void Set(int slot, object owner, Vector3 rigLocalMeters, bool active)
                => _hull.SetSlot(slot, owner, rigLocalMeters, active);
            public float OccluderId(int slot) => _hull.OccluderIdForSlot(slot);
            public float OccluderIdTop => _hull._foreHullId == 0 ? 0f : _hull.ForeHullIdTop / 255f;
        }

        private void ReleaseOwned()
        {
            static void Kill(UnityEngine.Object o)
            {
                if (o == null) return;
                if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
            }

            if (_meshChild != null) Kill(_meshChild.gameObject);
            if (_overlayRenderer != null) Kill(_overlayRenderer.gameObject);
            Kill(_facetMaterial);
            Kill(_overlayMaterial);
            Kill(_rampTex);
            Kill(_darkRampTex);
            Kill(_rampTexInterior);
            Kill(_darkRampTexInterior);
            Kill(_overlayQuad);
            _meshChild = null;
            _overlayChild = null;
            _meshRenderer = null;
            _overlayRenderer = null;
            _facetMaterial = null;
            _overlayMaterial = null;
            _rampTex = null;
            _darkRampTex = null;
            _overlayQuad = null;
            _setup = null;
        }
    }
}
