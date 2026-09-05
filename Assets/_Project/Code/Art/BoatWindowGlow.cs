using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>A lit cabin, seen from outside</b> (owner's ruling, 2026-09-03) — her windows drawn as
    /// windows, and a wash of light leaving each glazed wall. This is what replaced the amber disc
    /// that used to sit over a wheelhouse.
    ///
    /// <para><b>The ruling, verbatim:</b> <i>"The glows should be constrained to their space, if its
    /// interior it should be confined to the cabin with the glow only coming through the windows."</i>
    /// Two things follow from it and this component is both of them: the glass READS LIT (a few bright
    /// rectangles in a dark box, which is what a lit room looks like from outside at any distance),
    /// and the only light that reaches the deck is the light that came out of a window.</para>
    ///
    /// <para><b>Why the panes are one MESH and not one light each.</b> There are 236 panes across the
    /// fleet and up to fourteen on a single hull. A quad each would be 236 draws with the fleet under
    /// way, for 27 boats — and every one of them would be a ROUND glow on a RECTANGULAR window, which
    /// is the blob problem again at a smaller size. One mesh per hull is one draw, and its triangles
    /// are the actual glass: four projected corners of the published rectangle, so a window is the
    /// right shape at every heading and foreshortens correctly as she turns.</para>
    ///
    /// <para><b>⭐ And that foreshortening is also the culling.</b> A wall turning away from the camera
    /// projects to a thinner and thinner sliver, reaches exactly ZERO area edge-on, and then shows the
    /// viewer its inside. So there is no fade to tune and no popping to hide: each pane is dropped at
    /// the instant its own outward direction stops facing the camera, which is the instant its
    /// projected area is nought. The test is the DEPTH of the pane's normal through the hull's own
    /// posed transform — nearer is smaller z, the same axis the facet shader sorts occupants by — so
    /// the mirror in that transform (<c>IsoFacetMath.HullScale</c> is <c>(1,1,-1)</c>) is honoured by
    /// asking the transform rather than by reasoning about it.</para>
    ///
    /// <para><b>Additive, above the night overlay — like every glow here, and this is load-bearing.</b>
    /// The day/night cycle is a whole-frame MULTIPLY at sorting order ~32760, and a hull is drawn well
    /// below it. So NOTHING drawn into the hull's own pass can read as lit at 02:00: the very brightest
    /// pre-multiply colour a pane could take, pure white, comes out as the night tint itself
    /// (luminance ~0.17 at the shipped exposure). Making the glass emissive in the facet pass would
    /// therefore have produced a slightly paler dark-blue rectangle, not a lit window. These quads sit
    /// ABOVE the overlay with the lamps, blended One-One, and gate on the same published
    /// <c>_DayNightTint</c> in-shader — so they are invisible by day with no per-hull coupling to the
    /// clock (rule 5: pure function of the def, the preset and the published tint).</para>
    ///
    /// <para><b>Who decides she is lit.</b> Not this component. <see cref="BoatLamps"/> owns the cabin
    /// state — the vessel's way, the occupancy bus, the boost — and pushes it here with
    /// <see cref="SetLit"/>; this pulls it back on enable, because a component that is added after the
    /// push would otherwise never hear it. One owner, two doors, no second copy of the rule.</para>
    ///
    /// <para><b>Determinism / seams.</b> Visual-only: drives no simulation, saves nothing, reads no
    /// Boats type. The pane table comes off the Art renderer that knows which hull is skinned on this
    /// root, exactly as the lamp table does (rule 4).</para>
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-105)]   // after the hull poses herself (-110), before SceneLight's LateUpdate
    public sealed class BoatWindowGlow : MonoBehaviour
    {
        /// <summary>
        /// How brightly the GLASS itself burns, before the occupancy boost and the night gate.
        ///
        /// <para>Deliberately well under a navigation lamp's: a lit window is something you can tell
        /// is lit, not something you take a bearing off. It is read against the spill's own 0.5 (see
        /// <see cref="BoatLampPresets"/>) — the window is the brighter of the two, because the light
        /// is stronger at the glass than on the deck below it, which is the whole reason the glass is
        /// what you see first.</para>
        ///
        /// <para>Sized against the frame it lands in rather than by eye: the night overlay multiplies
        /// the whole scene to about 0.17 luminance at the shipped 02:00 exposure, so a pane adding
        /// ~0.7 of warm amber ABOVE that overlay is unmistakably the brightest thing on the boat while
        /// still leaving the occupancy boost (x1.5) somewhere to go.</para>
        /// </summary>
        public const float PaneIntensity = 0.72f;

        /// <summary>
        /// The additive shader's throw, in ITS quad space, used to fill a pane rather than to blob it.
        ///
        /// <para>That shader's radial term falls off from the lamp over <c>_Throw</c> quad-space
        /// units, and a pane's own quad spans 2 of them. At 1 the falloff is a halo — the blob. At 4
        /// the pane is at full strength in the middle and ~0.77 at its corners: a filled rectangle
        /// with a soft centre bias, which is a lit window. Reusing the shipped light shader this way
        /// is why this feature adds NO new shader and no new material to the magenta guard's list.</para>
        /// </summary>
        public const float PaneFillThrow = 4f;

        /// <summary>
        /// <b>Is a wall whose outward direction lands HERE one the viewer can see into?</b> Nearer is
        /// SMALLER z — the facet shader's own convention, and the depth this same transform hands its
        /// vertices — so a wall facing the camera has its outward direction going toward smaller z.
        ///
        /// <para><b>Take the direction through the TRANSFORM, never by reasoning about it.</b> The
        /// hull's posed child carries the rig-to-world MIRROR (<c>IsoFacetMath.HullScale</c> is
        /// <c>(1,1,-1)</c>) as well as heading, roll and pitch. A mirror flips the sense of a normal,
        /// and the only oracle that sees it is the transform: <c>TransformVector</c> is exactly the
        /// difference of two <c>TransformPoint</c>s, so this IS the drawn direction and not a model
        /// of it.</para>
        ///
        /// <para>Pure and static so a test can pin the rule against a known heading with no scene —
        /// hand it <c>IsoFacetMath.RigToWorld(dir, elev).MultiplyVector(pane.Outward)</c>.</para>
        /// </summary>
        public static bool FacesCamera(Vector3 outwardInDrawnFrame) => outwardInDrawnFrame.z < 0f;

        static readonly int IdLightColor    = Shader.PropertyToID("_LightColor");
        static readonly int IdIntensity     = Shader.PropertyToID("_Intensity");
        static readonly int IdConeHalfAngle = Shader.PropertyToID("_ConeHalfAngle");
        static readonly int IdEdgeSoftness  = Shader.PropertyToID("_EdgeSoftness");
        static readonly int IdCoreBoost     = Shader.PropertyToID("_CoreBoost");
        static readonly int IdLampPos       = Shader.PropertyToID("_LampPos");
        static readonly int IdThrow         = Shader.PropertyToID("_Throw");

        private IsoFacetHullRenderer _hull;
        private Transform _posed;
        private HullPane[] _panes;

        private Transform _paneNode;
        private MeshRenderer _paneRenderer;
        private SortingGroup _paneSorting;
        private MaterialPropertyBlock _mpb;
        private Mesh _mesh;
        private Vector3[] _verts;

        private SceneLight[] _spills;
        private HullWall[] _spillWalls;

        // Per-wall accumulators, allocated once and cleared per frame — four walls, and the pose loop
        // runs sixty times a second on every lit hull in the region (rule 7: no per-frame allocation).
        private readonly Vector2[] _wallOut = new Vector2[4];
        private readonly Vector3[] _wallAt = new Vector3[4];
        private readonly int[] _wallHits = new int[4];

        private bool _lit;
        private float _boost = 1f;

        /// <summary>The panes this hull is currently drawing — empty until she has been skinned.</summary>
        public HullPane[] Panes => _panes ?? System.Array.Empty<HullPane>();

        /// <summary>The wall spills, one per glazed wall, in <see cref="SpillWalls"/> order. Null
        /// before the first build.</summary>
        public SceneLight[] Spills => _spills;

        /// <summary>Which wall each entry of <see cref="Spills"/> belongs to.</summary>
        public HullWall[] SpillWalls => _spillWalls;

        /// <summary>Is her cabin lit right now, as <see cref="BoatLamps"/> last said? Public so a test
        /// reads the state rather than inferring it back out of which quads happen to be enabled.</summary>
        public bool Lit => _lit;

        /// <summary>The renderer the panes are drawn by — null before the first build. Public so a
        /// fixture can measure what actually draws rather than what was asked for.</summary>
        public MeshRenderer PaneRenderer => _paneRenderer;

        /// <summary>
        /// <b>Her cabin is lit, or it is not</b>, and this much brighter than the room's base while
        /// somebody is actually aboard. Pushed by <see cref="BoatLamps"/>, which owns the regime and
        /// the occupancy bus; idempotent and allocation-free, so it can be called every time that
        /// component re-throws its switches.
        /// </summary>
        public void SetLit(bool lit, float boost)
        {
            _lit = lit;
            _boost = Mathf.Max(0f, boost);
            ApplyEnabled();
        }

        private void OnEnable()
        {
            // Nothing is cached across an enable: a hop or a hull swap may have put a different
            // renderer under us, the same reason BoatLamps clears its own resolution here.
            _hull = null;
            _posed = null;
            _panes = null;
            PullLitFromLamps();
            Resolve();
        }

        private void OnDisable() => ApplyEnabled();

        private void OnDestroy()
        {
            DestroySpills();
            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
                _mesh = null;
            }
            if (_paneNode != null)
            {
                if (Application.isPlaying) Destroy(_paneNode.gameObject);
                else DestroyImmediate(_paneNode.gameObject);
                _paneNode = null;
            }
        }

        /// <summary>Take the cabin state from the component that owns it. The push half is
        /// <see cref="SetLit"/>; this is the late joiner's door — a window glow added AFTER the lamps
        /// have already thrown their switches would otherwise sit dark forever with nothing to say
        /// why.</summary>
        private void PullLitFromLamps()
        {
            var lamps = GetComponent<BoatLamps>();
            if (lamps != null)
            {
                _lit = BoatLamps.ShowsWhen(HullLampKind.CabinGlow, lamps.Way, lamps.CabinOccupied);
                _boost = lamps.CabinGlowScale;
                return;
            }

            // ⚠️ A HULL CAN HAVE WINDOWS AND NO LAMP TABLE, and she must still light up. The two
            // components are installed off two different def fields (Panes and Lamps), so a hull
            // measured for one and not the other is an ordinary state — and if the answer here were
            // "stay dark", her cabin would simply never come on, with no error to find it by. The
            // RULE is still BoatLamps' one static; only the way she is lying is read here, exactly as
            // that component reads it, and a hull that answers nothing is under way.
            Transform root = BoatLamps.BoatRootOf(transform);
            var source = root != null ? root.GetComponent<IVesselWay>() : null;
            VesselWay way = source != null ? source.Way : VesselWay.UnderWay;
            _lit = BoatLamps.ShowsWhen(HullLampKind.CabinGlow, way, cabinOccupied: false);
            _boost = 1f;
        }

        private void LateUpdate()
        {
            if (!Resolve()) return;
            PoseAndDraw();
        }

        // -------------------------------------------------------------------------------------------
        //  building
        // -------------------------------------------------------------------------------------------

        /// <summary>Find this hull's renderer and her pane table, rebuilding when the table changed.
        /// False while there is nothing to pose against — a hull mid-swap, or one with no windows —
        /// both ordinary states, neither an error.</summary>
        private bool Resolve()
        {
            if (_hull == null)
            {
                _hull = GetComponent<IsoFacetHullRenderer>();
                _posed = null;
            }
            if (_hull == null) return false;

            if (_posed == null) _posed = _hull.PosedMesh;
            if (_posed == null) return false;

            HullPane[] wanted = _hull.Panes;
            if (!ReferenceEquals(wanted, _panes)) { _panes = wanted; Build(); }

            return _panes != null && _panes.Length > 0;
        }

        private void Build()
        {
            DestroySpills();

            int n = UsablePaneCount();
            if (n == 0)
            {
                _verts = null;
                if (_paneRenderer != null) _paneRenderer.enabled = false;
                return;
            }

            EnsurePaneNode();
            BuildMesh(n);
            BuildSpills();
            ApplyEnabled();
        }

        private int UsablePaneCount()
        {
            if (_panes == null) return 0;
            int n = 0;
            for (int i = 0; i < _panes.Length; i++) if (_panes[i].IsUsable) n++;
            return n;
        }

        private void EnsurePaneNode()
        {
            if (_paneNode != null) return;

            var go = new GameObject("WindowPanes") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, worldPositionStays: false);
            _paneNode = go.transform;

            _mesh = new Mesh { name = "BoatWindowPanes", hideFlags = HideFlags.DontSave };
            _mesh.MarkDynamic();   // rewritten every frame; tell the driver so before the first upload
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;

            _paneRenderer = go.AddComponent<MeshRenderer>();
            _paneRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _paneRenderer.receiveShadows = false;
            _paneRenderer.lightProbeUsage = LightProbeUsage.Off;
            _paneRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _paneRenderer.sortingOrder = SceneLight.MaxSortingOrder;

            // Sort as 2D, for exactly the reason SceneLight's own quad does: a MeshRenderer does not
            // reliably sort against SpriteRenderers by order alone in the URP 2D renderer, and the
            // sea is one very large sprite.
            _paneSorting = go.AddComponent<SortingGroup>();
            _paneSorting.sortingOrder = SceneLight.MaxSortingOrder;
            _paneSorting.sortAtRoot = true;

            Material mat = SceneLight.SharedLightMaterial();
            if (mat != null) _paneRenderer.sharedMaterial = mat;
            else _paneRenderer.enabled = false;   // no shader yet -> no windows (harmless)

            _mpb = new MaterialPropertyBlock();
        }

        /// <summary>
        /// Lay out the pane mesh once: four vertices and two triangles per usable pane, with each
        /// pane's own [0,1]² UV block so the shader's radial term fills IT rather than the whole hull.
        /// The vertex POSITIONS are rewritten every frame (they are world points); the indices and
        /// UVs never change, which is why they are written here and not there.
        /// </summary>
        private void BuildMesh(int n)
        {
            _verts = new Vector3[n * 4];
            var uvs = new Vector2[n * 4];
            var tris = new int[n * 6];

            for (int i = 0; i < n; i++)
            {
                int v = i * 4, t = i * 6;
                uvs[v + 0] = new Vector2(0f, 0f);
                uvs[v + 1] = new Vector2(1f, 0f);
                uvs[v + 2] = new Vector2(1f, 1f);
                uvs[v + 3] = new Vector2(0f, 1f);
                tris[t + 0] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
                tris[t + 3] = v; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
            }

            _mesh.Clear();
            _mesh.vertices = _verts;      // placeholder; PoseAndDraw writes the real ones this frame
            _mesh.uv = uvs;
            _mesh.triangles = tris;
        }

        /// <summary>
        /// One spill per glazed WALL — not per pane. Three windscreen panes 0.16 m apart do not throw
        /// three wedges of light onto the foredeck, they throw one; and per-pane cones would have been
        /// 218 quads with the fleet under way for a picture nobody could tell apart from this one.
        /// The wash's throw comes off a WINDOW's width, so a lobster boat's long side lights reach
        /// further than a porthole does — from the data rather than from a dial, and without growing
        /// on a wall that simply has more windows in it.
        /// </summary>
        private void BuildSpills()
        {
            var walls = new System.Collections.Generic.List<HullWall>(4);
            for (int i = 0; i < _panes.Length; i++)
            {
                if (!_panes[i].IsUsable) continue;
                if (!walls.Contains(_panes[i].Wall)) walls.Add(_panes[i].Wall);
            }

            _spillWalls = walls.ToArray();
            _spills = new SceneLight[_spillWalls.Length];
            bool legacy = GameServices.BoatLegacyCabinGlow;

            for (int w = 0; w < _spillWalls.Length; w++)
            {
                var go = new GameObject("WindowSpill_" + _spillWalls[w]) { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(transform, worldPositionStays: false);
                var light = go.AddComponent<SceneLight>();
                // In the LEGACY arm the disc is put back by BoatLamps and these draw nothing at all —
                // built but never enabled, so flipping the dial back does not have to rebuild a hull.
                BoatLampPresets.ApplyWallSpill(light, MeanWindowWidth(_spillWalls[w]), _boost);
                if (legacy) light.enabled = false;
                _spills[w] = light;
            }
        }

        /// <summary>
        /// How wide a TYPICAL window in this wall is, metres — the mean, which is what the wash is
        /// scaled off (see <see cref="BoatLampPresets.WallSpillWindowMultiple"/>).
        ///
        /// <para>⚠️ Deliberately NOT the wall's glazed SPAN. Scaling off the span would make the wash
        /// grow with the NUMBER of windows in a wall, so the tanker's five portholes strung along 6.8
        /// metres of accommodation would throw a seven-metre pool — the very thing the ruling
        /// retired — while a single porthole threw almost nothing. Light through a window is a
        /// property of the window.</para>
        /// </summary>
        private float MeanWindowWidth(HullWall wall)
        {
            float sum = 0f; int n = 0;
            for (int i = 0; i < _panes.Length; i++)
            {
                if (!_panes[i].IsUsable || _panes[i].Wall != wall) continue;
                sum += _panes[i].WidthMetres;
                n++;
            }
            return n > 0 ? sum / n : 0f;
        }

        private void DestroySpills()
        {
            if (_spills == null) return;
            for (int i = 0; i < _spills.Length; i++)
            {
                if (_spills[i] == null) continue;
                if (Application.isPlaying) Destroy(_spills[i].gameObject);
                else DestroyImmediate(_spills[i].gameObject);
            }
            _spills = null;
            _spillWalls = null;
        }

        // -------------------------------------------------------------------------------------------
        //  drawing
        // -------------------------------------------------------------------------------------------

        /// <summary>
        /// Push every pane through the frame the hull is DRAWN in, drop the ones now facing away, and
        /// aim each wall's wash along its own outward direction. One pass, no allocation (rule 7).
        /// </summary>
        private void PoseAndDraw()
        {
            if (_verts == null || _mesh == null || _paneNode == null) return;

            bool on = _lit && isActiveAndEnabled && !GameServices.BoatLegacyCabinGlow;

            // ⚠️ A DARK CABIN DOES NO WORK AT ALL, and this early-out is the whole reason a wharf of
            // sleeping boats costs what it did before this feature. At a berth the glow is off unless
            // somebody is aboard, which is nearly every hull nearly all the time; without this we
            // would push every one of their panes through the transform and upload a mesh nobody is
            // going to draw. The vertices left behind are stale, and that is correct: the renderer is
            // off, and the first frame she lights up rewrites all of them before it is turned on.
            if (!on)
            {
                ApplyEnabled();
                return;
            }

            // The node keeps identity rotation at the camera's own depth and the vertices carry world
            // X/Y — the same compositing trick SceneLight plays with its quad, for the same reason
            // (a mesh at the world's depth loses to the sea sprite whatever its sorting order says).
            float z = PinnedDepth();
            _paneNode.SetPositionAndRotation(new Vector3(0f, 0f, z), Quaternion.identity);

            // ⚠️ AND UNDO WHATEVER THE PARENT IS SCALED BY. The vertices below are WORLD points written
            // into a local buffer, which is only the same thing while this node's own world scale is
            // one. SetPositionAndRotation fixes the position and the rotation and says nothing about
            // scale, so a hull hosted under any scaled node would draw her windows at that scale about
            // the world origin — a failure that is invisible on the unscaled fleet we ship today and
            // total on the first boat somebody scales.
            Vector3 parent = transform.lossyScale;
            _paneNode.localScale = new Vector3(
                Mathf.Approximately(parent.x, 0f) ? 1f : 1f / parent.x,
                Mathf.Approximately(parent.y, 0f) ? 1f : 1f / parent.y,
                Mathf.Approximately(parent.z, 0f) ? 1f : 1f / parent.z);

            int v = 0;
            for (int w = 0; w < 4; w++) { _wallOut[w] = Vector2.zero; _wallAt[w] = Vector3.zero; _wallHits[w] = 0; }

            for (int i = 0; i < _panes.Length; i++)
            {
                HullPane p = _panes[i];
                if (!p.IsUsable) continue;

                Vector3 c = _posed.TransformPoint(p.CentreMetres);
                Vector3 nOut = _posed.TransformVector(p.Outward);
                bool faces = on && FacesCamera(nOut);

                if (faces)
                {
                    Vector3 a0 = _posed.TransformPoint(p.Corner(-1f, -1f));
                    Vector3 a1 = _posed.TransformPoint(p.Corner(+1f, -1f));
                    Vector3 a2 = _posed.TransformPoint(p.Corner(+1f, +1f));
                    Vector3 a3 = _posed.TransformPoint(p.Corner(-1f, +1f));
                    _verts[v + 0] = new Vector3(a0.x, a0.y, 0f);
                    _verts[v + 1] = new Vector3(a1.x, a1.y, 0f);
                    _verts[v + 2] = new Vector3(a2.x, a2.y, 0f);
                    _verts[v + 3] = new Vector3(a3.x, a3.y, 0f);

                    int w = (int)p.Wall;
                    _wallOut[w] += new Vector2(nOut.x, nOut.y);
                    _wallAt[w] += c;
                    _wallHits[w]++;
                }
                else
                {
                    // Collapsed to a point: two zero-area triangles, no fragments, and the index
                    // buffer never has to change shape. Cheaper than rebuilding the mesh, and it
                    // keeps a pane's slot stable across the frame she turns through edge-on.
                    //
                    // Collapsed onto its OWN projected centre rather than onto the origin, because
                    // SetVertices recomputes the mesh bounds: a dark pane parked at (0,0) would
                    // stretch this hull's bounds from the world origin out to wherever she is, and a
                    // boat two kilometres offshore would carry a two-kilometre bounding box.
                    var at = new Vector3(c.x, c.y, 0f);
                    _verts[v + 0] = _verts[v + 1] = _verts[v + 2] = _verts[v + 3] = at;
                }
                v += 4;
            }

            // SetVertices, not the `vertices` property: the property's SETTER is fine but its getter
            // allocates, and using the pair invites somebody to read it back in this same loop.
            _mesh.SetVertices(_verts);

            if (_paneRenderer != null)
            {
                _paneRenderer.enabled = on && _paneRenderer.sharedMaterial != null;
                if (_paneRenderer.enabled) PushPaneMaterial();
            }

            PoseSpills(on);
        }

        /// <summary>The additive shader, told to FILL each pane rather than halo it: lamp at the quad
        /// centre, no cone, no core, and a throw far enough that the falloff across one pane is a
        /// gentle bias instead of a blob (see <see cref="PaneFillThrow"/>).</summary>
        private void PushPaneMaterial()
        {
            _paneRenderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(IdLightColor, BoatLampPresets.For(HullLampKind.CabinGlow).Color);
            _mpb.SetFloat(IdIntensity, PaneIntensity * _boost);
            _mpb.SetFloat(IdConeHalfAngle, 180f);      // radial: no angular cut inside a pane
            _mpb.SetFloat(IdEdgeSoftness, 1f);
            _mpb.SetFloat(IdCoreBoost, 0f);
            _mpb.SetVector(IdLampPos, Vector4.zero);   // the lamp is the pane's own centre
            _mpb.SetFloat(IdThrow, PaneFillThrow);
            _paneRenderer.SetPropertyBlock(_mpb);
            _paneRenderer.sortingOrder = SceneLight.MaxSortingOrder;
            if (_paneSorting != null) _paneSorting.sortingOrder = SceneLight.MaxSortingOrder;
        }

        /// <summary>
        /// Put each wall's wash at the middle of its lit glass and point it the way that glass faces.
        /// A wall with no pane facing the camera this frame is switched OFF — the light that leaves it
        /// is going away from the viewer, and drawing it would be a wedge of amber crossing her own
        /// roof.
        /// </summary>
        private void PoseSpills(bool on)
        {
            if (_spills == null || _spillWalls == null) return;

            for (int i = 0; i < _spills.Length; i++)
            {
                SceneLight light = _spills[i];
                if (light == null) continue;

                int w = (int)_spillWalls[i];
                bool showing = on && _wallHits[w] > 0 && _wallOut[w].sqrMagnitude > 1e-8f;
                light.enabled = showing;
                if (!showing) continue;

                Vector3 at = _wallAt[w] / _wallHits[w];
                at.z = transform.position.z;   // the posed z carries depth biases a light has no use for
                Vector2 dir = _wallOut[w].normalized;

                // The cone throws along the light node's own UP, so the node is turned to face the
                // wall's outward direction. This is the one place a lamp on a boat is ORIENTED rather
                // than merely positioned, and it is why the cabin glow could stop being a disc.
                light.transform.SetPositionAndRotation(
                    at, Quaternion.LookRotation(Vector3.forward, new Vector3(dir.x, dir.y, 0f)));

                // The lamp's height for the shadows it throws: the mean height of this wall's glass
                // above the keel, which is as close as her data comes to how high a lit window is.
                light.LampHeightMeters = Mathf.Max(0f, MeanPaneHeight(_spillWalls[i]));
                light.Intensity = BoatLampPresets.For(HullLampKind.CabinGlow).Intensity * _boost;
            }
        }

        private float MeanPaneHeight(HullWall wall)
        {
            float sum = 0f; int n = 0;
            for (int i = 0; i < _panes.Length; i++)
                if (_panes[i].IsUsable && _panes[i].Wall == wall) { sum += _panes[i].CentreMetres.z; n++; }
            return n > 0 ? sum / n : 0f;
        }

        /// <summary>The depth to pin the pane mesh to — the SAME camera and the same offset
        /// <see cref="SceneLight"/> pins its quads to, so the windows and the lamps composite in one
        /// layer instead of two.</summary>
        private float PinnedDepth()
        {
            Camera cam = SceneLight.ActiveCamera();
            if (cam == null) return transform.position.z;
            Transform ct = cam.transform;
            return LightMath.CameraDepthZ(ct.position.z, ct.forward.z, cam.nearClipPlane,
                                          SceneLight.DefaultCameraDepthOffset);
        }

        private void ApplyEnabled()
        {
            bool on = _lit && isActiveAndEnabled && !GameServices.BoatLegacyCabinGlow;
            if (_paneRenderer != null)
                _paneRenderer.enabled = on && _paneRenderer.sharedMaterial != null && _verts != null;
            if (_spills == null) return;
            for (int i = 0; i < _spills.Length; i++)
                if (_spills[i] != null) _spills[i].enabled = on;
        }
    }
}
