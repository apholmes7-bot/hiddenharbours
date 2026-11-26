using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>The walker's headlamp</b> — the directed half of world-lighting PR 3: a narrow cone worn on his
    /// forehead, switched, aimed wherever he is looking.
    ///
    /// <para><b>Why a headlamp and not a torch in his hand.</b> The owner asked for <i>"a spotlight"</i>.
    /// A hand torch would cost a hand — and the TOOL CONTINUITY LAW (owner playtest, 2026-08-23) says what
    /// he is holding does not silently change underneath him, so a torch would have to be taken out and
    /// put away, and would fight the rod and the catch for the same slot. A headlamp costs no slot.</para>
    ///
    /// <para><b>Worn from the pocket (owner, 2026-11-16).</b> It starts in his pocket and nothing is drawn
    /// on his head. Putting it on (<see cref="PutOn"/>) draws a small lamp on a head strap at his
    /// forehead, and it stays drawn while he wears it, lit or not (<see cref="FittingShows"/>); taking it
    /// off (<see cref="TakeOff"/>) puts it back in the pocket and switches it off. Lighting it implies
    /// wearing it. The worn state is a transient like the switch: nothing is saved.</para>
    ///
    /// <para><b>The light leaves the lens.</b> The lens is a point on his head, in the head bone's own
    /// frame (<see cref="GameConfig.WalkerHeadlampLensForwardMetres"/>, <see cref="GameConfig.WalkerHeadlampLensUpMetres"/>
    /// from the rig's head mid point), so it turns and bobs with the head. The glow sits exactly on that
    /// point, the cone starts on the ground directly beneath it, and the drawn lens pixel is pinned to it in
    /// every facing. From behind the strap is drawn and the lens is hidden by his head.</para>
    ///
    /// <para><b>It lights the world, not just a patch of ground</b> — which is the whole reason it is a
    /// <see cref="SceneLight"/> and not a sprite:
    /// <list type="bullet">
    ///   <item>the GROUND, through <see cref="LampPoolSystem"/> off <see cref="SceneLight.ReachMetres"/>
    ///   (#736's pool — the thing the bloom used to stand in for);</item>
    ///   <item>the WATER, through <see cref="IWaterLightEmitter"/> — from a wharf edge his beam plays on
    ///   the sea by the same relief model the searchlight uses (#691), taking one of the bridge's four
    ///   nearest-lamp slots;</item>
    ///   <item>the TREES, shrubs and shore plants, through the lit-decor path — which reads one global
    ///   lamp, so who writes it is arbitrated: see <see cref="WalkerLights.OwnsDecorLight"/>;</item>
    ///   <item>and it throws CAST SHADOWS off whatever stands in it (<see cref="LampShadowSystem"/>) —
    ///   except his own, which the carrier rule declines.</item>
    /// </list></para>
    ///
    /// <para><b>Determinism (rule 5):</b> the beam's state is a transient the player toggles; nothing is
    /// saved, nothing is random, and the lamp is steady (a battery lamp does not flicker).</para>
    /// </summary>
    [DefaultExecutionOrder(200)] // after the figure has drawn its pose at 100
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SceneLight))]
    public sealed class Headlamp : MonoBehaviour, IWaterLightEmitter
    {
        [Tooltip("Is the beam switched ON? A transient — never saved, and off is the state he starts in.")]
        [SerializeField] private bool _on;

        [Tooltip("Is the lamp on his head (true) or in his pocket (false)? A transient — never saved; " +
                 "it starts in the pocket.")]
        [SerializeField] private bool _worn;

        [Tooltip("How hard the beam reads on WATER relative to land, so the owner can balance the two " +
                 "independently — the same dial BoatSpotlight's searchlight carries, and for the same reason.")]
        [Range(0f, 2f)] [SerializeField] private float _waterStrength = 0.8f;

        private SceneLight _light, _lens;
        private WaterLightState _waterLight;
        private bool _live = true;
        private bool _hasHeadPoint;
        private Vector2 _headOffset;
        private float _projectedLift;
        private Transform _carrier, _facet;
        private IsoCharacterFigureRenderer _mountedFigure;
        private SpriteRenderer _fitting;
        private Camera _headCamera;
        private View _view = View.Front;
        private Vector2 _bandOffset;
        private readonly List<IsoCharacterFigureRenderer> _figures = new(2);
        private readonly List<MeshFilter> _filters = new(3);
        private static Sprite[] _unlitSprites, _litSprites;
        private static Headlamp _decorOwner;

        /// <summary>Is the beam lit?</summary>
        public bool IsOn => _on;

        /// <summary>Is the lamp on his head (as opposed to in his pocket)?</summary>
        public bool IsWorn => _worn;

        /// <summary>Which way the drawn lamp is seen this frame (from the head's facing on screen).</summary>
        public View DrawnView => _view;

        /// <summary>The lamp this drives.</summary>
        public SceneLight Light => _light != null ? _light : _light = GetComponent<SceneLight>();

        /// <summary>The lens's own glow: a small round glass lamp at the lens point (the beam's quad is a
        /// cone, which would cut a round lens in half), with no pool of its own. Built by
        /// <see cref="Configure"/>; null before.</summary>
        public SceneLight Lens => _lens;

        /// <summary>Set the height it is worn at before a mesh head exists. The beam's SHAPE is not a
        /// parameter: it comes from <see cref="LightPresets.ConeHalfDegrees"/>, so the angle is written in
        /// exactly one place and the preset library's own guard can assert it. Called once by the carrier.</summary>
        public void Configure(float browHeightMetres)
        {
            SceneLight l = Light;
            _hasHeadPoint = false;
            _headOffset = Vector2.up * browHeightMetres;
            _projectedLift = browHeightMetres;
            LightPresets.Apply(l, LightPresets.Kind.Headlamp);
            l.Shape = SceneLight.LightShape.Cone;
            l.ConeHalfAngle = LightPresets.ConeHalfDegrees(LightPresets.Kind.Headlamp);
            l.AngularSoftness = 0.35f;                 // a feathered edge; a hard-edged cone reads as a cutout
            l.ReachMetres = LightPresets.ReachMetres(LightPresets.Kind.Headlamp);
            l.BloomLiftMetres = browHeightMetres;      // the lit lens rides on his forehead (#733)
            l.LampHeightMeters = browHeightMetres;     // and its cast shadows are thrown from there
            l.CastsShadows = true;
            // The beam draws no glow of its own: its quad is the cone. The lens is a separate round glass
            // lamp riding the same point (ADR 0016, "lamps light what they can see").
            l.DrawsBloom = false;
            BuildLens(browHeightMetres);
            ApplyBeamState();
        }

        private void BuildLens(float browHeightMetres)
        {
            if (_lens == null)
            {
                var go = new GameObject("HeadlampLens");
                go.transform.SetParent(transform, worldPositionStays: false);
                _lens = go.AddComponent<SceneLight>();
            }
            LightPresets.Apply(_lens, LightPresets.Kind.Headlamp);   // the lens look, glass, from GlassLook
            _lens.Shape = SceneLight.LightShape.Radial;
            _lens.ReachMetres = 0f;                                  // the beam lights the ground, not the lens
            _lens.CastsShadows = false;
            _lens.Range = GlassLook.Current.HeadlampLensRadiusMetres;   // its own size, not the preset's bloom
            _lens.BloomLiftMetres = browHeightMetres;
            _lens.LampHeightMeters = browHeightMetres;
        }

        /// <summary>Take it out of the pocket and put it on, lit. Already worn, it just lights.</summary>
        public void PutOn()
        {
            _worn = true;
            _on = true;
            ApplyBeamState();
        }

        /// <summary>Take it off into the pocket. It goes out as it comes off.</summary>
        public void TakeOff()
        {
            _worn = false;
            _on = false;
            ApplyBeamState();
        }

        /// <summary>Wear it or pocket it without touching the switch, except that a pocketed lamp is
        /// never lit.</summary>
        public void SetWorn(bool worn)
        {
            _worn = worn;
            if (!worn) _on = false;
            ApplyBeamState();
        }

        /// <summary>Throw the switch. A lamp in the pocket is put on first (<see cref="PutOn"/>).</summary>
        public void Toggle()
        {
            if (!_worn) PutOn();
            else SetOn(!_on);
        }

        /// <summary>Set the beam on or off. Lighting it puts it on his head: a lit lamp is a worn lamp.</summary>
        public void SetOn(bool on)
        {
            _on = on;
            if (on) _worn = true;
            ApplyBeamState();
        }

        /// <summary>
        /// Is this lamp his to burn at all right now? Off his feet the whole component stands down —
        /// the renderer, the drawn lamp, the water slot and the decor singleton alike — so nothing of his is
        /// lit or drawn while he is at a wheel.
        ///
        /// <para>⚠️ It does NOT clear <see cref="_on"/> or <see cref="_worn"/>. Stepping aboard and back
        /// off again should give him the lamp he had, not a lamp he has to put on and switch on again; the
        /// state is his, the LIVENESS is the mode's.</para>
        /// </summary>
        public void SetLive(bool live)
        {
            _live = live;
            ApplyBeamState();
        }

        private void Awake() => _light = GetComponent<SceneLight>();

        // ⚠️ The bridge's own contract is register-on-enable / unregister-on-disable (WaterLightBridge's
        // doc says so), NOT Start/OnDestroy: a lamp that is switched off by disabling the component must
        // leave the four nearest-lamp slots, or it holds one against a light that is actually burning.
        private void OnEnable()
        {
            ApplyBeamState();
            WaterLightBridge.Register(this);
        }

        private void OnDisable()
        {
            WaterLightBridge.Unregister(this);
            if (_fitting != null) _fitting.enabled = false;
            ClearDecorIfStillOurs();
        }

        /// <summary>
        /// <b>⭐ HE WRITES THE ONE DECOR LAMP — the other half of "one publisher".</b>
        ///
        /// <para><see cref="WalkerLights.OwnsDecorLight"/> makes every <see cref="BoatSpotlight"/> in the
        /// scene STAND OFF the <c>_BoatLight*</c> singleton while his beam is lit. That half on its own is
        /// not a handover, it is a HOLE: with the boats standing off and nothing put in their place, the
        /// five globals simply KEEP whatever a boat wrote last — a beam frozen at a moored dory's bow, or
        /// zeros in a region with no boat in it — and the trees in front of him are lit by a ghost, or not
        /// at all. Standing the boats off is only correct because this line takes their place.
        /// </para>
        ///
        /// <para><b>Off the WATER state, deliberately.</b> The numbers are the ones
        /// <see cref="TryGetWaterLight"/> has already composed, published through
        /// <see cref="DecorLampGlobals"/>'s <c>WaterLightState</c> overload — so his beam cannot light a
        /// spruce unlike it lights the sea he is pointing it over, and it carries the same water-strength
        /// scaling a boat's searchlight carries into this same path (<c>BoatSpotlight</c> publishes its
        /// <c>waterIntensity</c> here, not its land intensity). One packing, one meaning, both lamps.</para>
        ///
        /// <para><b>⚠️ LateUpdate, not Update, and that is the ordering guarantee.</b> Every
        /// <see cref="BoatSpotlight"/> publishes in <c>Update</c>. Writing here makes him the LAST word in
        /// the frame whatever order the boats ticked in — so on the frame his beam is switched ON he
        /// cannot be overwritten by a boat that happened to run after him, and on the frame it is switched
        /// OFF the boats resume with no blank frame in between. The stand-off guard and this ordering are
        /// belt and braces on purpose: either one alone leaves a single-frame flicker in the woods, which
        /// is exactly the class of defect nothing would ever report.</para>
        /// </summary>
        private void LateUpdate()
        {
            SynchronizeMount();
            if (!PublishDecorLampIfOwned()) ClearDecorIfStillOurs();
        }

        internal static void DecorPublishedByBoat() => _decorOwner = null;

        private void ClearDecorIfStillOurs()
        {
            if (_decorOwner != this) return;
            WaterLightState dark = default;
            DecorLampGlobals.Publish(in dark);
            _decorOwner = null;
        }

        /// <summary>The lens, metres in the head bone's frame from the rig's head mid point.</summary>
        public static Vector3 LensOffsetHeadLocal()
        {
            GameConfig c = GameServices.Config;
            float forward = c != null ? c.WalkerHeadlampLensForwardMetres : GameConfig.DefaultWalkerHeadlampLensForwardMetres;
            float up = c != null ? c.WalkerHeadlampLensUpMetres : GameConfig.DefaultWalkerHeadlampLensUpMetres;
            return new Vector3(0f, forward, up);
        }

        /// <summary>How far behind the lens the strap crosses the back of his head, metres.</summary>
        public static float StrapBackMetres()
        {
            GameConfig c = GameServices.Config;
            return c != null ? c.WalkerHeadlampStrapBackMetres : GameConfig.DefaultWalkerHeadlampStrapBackMetres;
        }

        /// <summary>True while the beam is aimed by his drawn head (set each LateUpdate from the posed figure).
        /// While it holds, nothing else turns the lamp: <see cref="WalkerLights"/> aims by the interact probe
        /// only when no head is drawn, so the beam never flips between the two within a frame.</summary>
        public bool AimsByDrawnHead { get; private set; }

        // A visible mesh is the source of truth. Hidden figures retain stale HeadSnapPoint data.
        private void SynchronizeMount()
        {
            if (_carrier == null) _carrier = transform.parent;
            if (_carrier == null || Light == null) return;
            _carrier.GetComponentsInChildren(true, _figures);
            IsoCharacterFigureRenderer liveFigure = null;
            for (int i = 0; i < _figures.Count; i++)
            {
                var f = _figures[i];
                // Ashore, or riding a hull: in an open boat the lamp is his too (WalkerLights.HeadlampIsHis),
                // and his figure is drawn by the hull there, so it holds no ashore id but still a drawn head.
                if (f.isActiveAndEnabled && f.IsConfigured && (f.IsAshore || f.Hull != null) && f.Visible &&
                    f.HeadSnapPoint.w > .5f)
                { liveFigure = f; break; }
            }
            // Aboard the hull draws him: his figure hangs under its posed mesh, not under him, and names him
            // as its wearer. Without this the beam stood fixed aboard while his drawn head turned.
            if (liveFigure == null)
            {
                IReadOnlyList<IsoCharacterFigureRenderer> worn = IsoCharacterFigureRenderer.WornFigures;
                for (int i = 0; i < worn.Count; i++)
                {
                    var f = worn[i];
                    if (f == null || f.Wearer == null || !f.Wearer.IsChildOf(_carrier) && !_carrier.IsChildOf(f.Wearer))
                        continue;
                    if (f.isActiveAndEnabled && f.IsConfigured && f.Hull != null && f.Visible && f.HeadSnapPoint.w > .5f)
                    { liveFigure = f; break; }
                }
            }
            if (liveFigure != null && liveFigure != _mountedFigure)
            {
                _mountedFigure = liveFigure;
                _facet = null;
                liveFigure.GetComponentsInChildren(true, _filters);
                for (int i = 0; i < _filters.Count; i++)
                    if (_filters[i].sharedMesh != null && _filters[i].sharedMesh.name == "HHCharacterSkinPosed")
                    { _facet = _filters[i].transform; break; }
            }
            bool drawn = liveFigure != null && _facet != null;
            if (!drawn) AimsByDrawnHead = false;
            if (drawn)
            {
                Vector4 h = liveFigure.HeadSnapPoint;
                Vector3 headRig = new Vector3(h.x, h.y, h.z);
                Vector3 offset = LensOffsetHeadLocal();
                Vector3 mid = liveFigure.HeadMidLocal;
                Vector3 lensRig = liveFigure.HeadLocalToFigure(mid + offset);
                Vector3 bandRig = liveFigure.HeadLocalToFigure(mid + offset - new Vector3(0f, StrapBackMetres(), 0f));
                // The head is drawn snapped to the art grid about its mid point; the lens rides the same
                // snap, a whole number of art pixels from it, so it never swims against the face.
                Vector3 headWorld = _facet.TransformPoint(headRig);
                Vector3 head = SnapDrawnHead(headWorld);
                Vector2 lens = (Vector2)head + OnArtGrid((Vector2)(_facet.TransformPoint(lensRig) - headWorld));
                _bandOffset = OnArtGrid((Vector2)(_facet.TransformPoint(bandRig) - headWorld)) + (Vector2)head - lens;
                _headOffset = lens - (Vector2)_carrier.position;
                float height = Mathf.Max(.01f, lensRig.z);
                _projectedLift = IsoGround.HeightScale * height;
                Light.LampHeightMeters = height;
                Vector3 headForward = liveFigure.HeadDirectionToFigure(Vector3.up);
                _view = ViewOf(headForward, _facet);
                // THE BEAM LEAVES THE WAY HIS DRAWN HEAD FACES (the owner, 2026-11-25: "headlamp should always
                // follow players sprite"). WalkerLights aims it by the interact probe's facing, which is not the
                // drawn heading: the figure turns through the diagonals and eases round, the probe does not, so
                // the cone pointed one way while his face pointed another. Here, after the figure has drawn
                // (order 200 > 100), the drawn head is the one source for the lens, the view and the beam.
                AimsByDrawnHead = DrawnGroundDirection(headForward, _facet, out Vector2 aim);
                if (AimsByDrawnHead) transform.up = new Vector3(aim.x, aim.y, 0f);
                _hasHeadPoint = true;
            }
            // ConstantLiftUntilFirstHeadPoint; after that, every sprite pose holds this root offset.
            Vector2 point = (Vector2)_carrier.position + _headOffset;
            transform.position = new Vector3(point.x, point.y - _projectedLift, _carrier.position.z);
            Light.OriginOffset = Vector2.zero;
            Light.BloomLiftMetres = _projectedLift;
            // THE LIGHT STARTS AT THE LAMP ON HIS HEAD (the owner, 2026-11-25: "the light appears to originate
            // from the players feet and not from their headlamp"). The foot above is the ground under the
            // lens and the height is the lens's own, so the geometry was already the head's; what read as
            // "from his feet" was the pool, brightest straight under the lamp like a post's. A lamp on a
            // brow tilted down lights the ground where its beam lands, ahead of him: nothing nearer than the
            // beam's lower edge, full where its axis lands. The shadows read the same throw.
            LightMath.HeadlampThrow(Light.LampHeightMeters, GlassLook.Current.HeadlampTiltDownDegrees,
                                    Light.ConeHalfAngle, out float throwStart, out float throwFull);
            Light.SetThrow(throwStart, throwFull);
            Light.RefreshMount(); // SceneLight's earlier LateUpdate must not leave a one-frame bloom lag.
            if (drawn && _hasHeadPoint && _fitting == null) BuildFitting();
            // What his figure is drawn at: his own overlay ashore; aboard, the hull that draws him.
            Renderer carrierOverlay = !drawn ? null
                : liveFigure.AshoreOverlay != null ? liveFigure.AshoreOverlay
                : liveFigure.Hull != null ? liveFigure.Hull.OverlayRenderer : null;
            if (_fitting != null)
            {
                _fitting.enabled = FittingShows(drawn, _live, _worn);
                _fitting.sprite = SpriteFor(_view, _on);
                // Views that show the lens pin it to the light's origin; from behind, the strap is centred
                // on the band round the back of his head.
                Vector2 at = LensShows(_view) ? point : point + _bandOffset;
                _fitting.transform.position = new Vector3(at.x, at.y, _carrier.position.z);
                _fitting.transform.rotation = Quaternion.identity;
                if (carrierOverlay != null)
                {
                    _fitting.sortingLayerID = carrierOverlay.sortingLayerID;
                    _fitting.sortingOrder = Mathf.Min(SceneLight.MaxSortingOrder - 10,
                                                     carrierOverlay.sortingOrder + 1);
                }
            }
            // THE LENS AMONG THE WORLD (ADR 0016, "lamps light what they can see"). The lens is its own small
            // round glass lamp (the beam's quad is a cone and would cut a round lens in half), sorted one
            // rung over the drawn fitting on his head, not at the ceiling over everything: a wall, a shed or a
            // stack of traps nearer the camera covers his lamp as it covers him. From behind his head the lens
            // is not seen, so it draws no glow there; the beam still lights what is in front of him.
            bool overFitting = _fitting != null && carrierOverlay != null;
            if (Light.DrawsBloom) Light.DrawsBloom = false;
            if (_lens != null)
            {
                _lens.OriginOffset = Vector2.zero;
                _lens.BloomLiftMetres = _projectedLift;
                _lens.LampHeightMeters = Light.LampHeightMeters;
                _lens.SetCarrierOrder(overFitting ? _fitting.sortingOrder : (int?)null);
                bool lensSeen = !_hasHeadPoint || LensShows(_view);
                if (_lens.DrawsBloom != lensSeen) _lens.DrawsBloom = lensSeen;
                _lens.RefreshMount();
            }
        }

        /// <summary>
        /// <b>Is the lamp drawn on his head?</b> While he wears it, lit or not, he is on his feet and a
        /// mesh head is drawn to wear it. In the pocket nothing is drawn: the 2026-11-16 face fix (#1259)
        /// stands, now as "pocketed means no lamp on the head".
        /// </summary>
        public static bool FittingShows(bool headDrawn, bool live, bool worn) => headDrawn && live && worn;

        // =============================================================================================
        //  THE DRAWN LAMP — a small lamp on a head strap, per view
        // =============================================================================================

        /// <summary>How the lamp is seen. Right/Left is the way he faces on screen.</summary>
        public enum View
        {
            Front, FrontQuarterRight, SideRight, BackQuarterRight, Back,
            FrontQuarterLeft, SideLeft, BackQuarterLeft,
        }

        /// <summary>Does this view show the lens? From behind his head hides it.</summary>
        public static bool LensShows(View view) =>
            view != View.Back && view != View.BackQuarterRight && view != View.BackQuarterLeft;

        /// <summary>
        /// The view from his head's facing: the angle between where the head points on the ground and the
        /// direction toward the camera, read through the facet's own projection so no screen ratio is
        /// assumed. 0° faces the viewer, 180° faces away; the eight views split it every 45°.
        /// </summary>
        public static View ViewOf(Vector3 headForwardFigure, Transform facet)
        {
            Vector3 ex = facet.TransformVector(Vector3.right), ey = facet.TransformVector(Vector3.up);
            float det = ex.x * ey.y - ey.x * ex.y;
            if (Mathf.Abs(det) < 1e-9f) return View.Front;
            // Figure-ground directions whose screen images are straight down (toward the viewer) and right.
            Vector2 toward = new Vector2(ey.x, -ex.x) / det, right = new Vector2(ey.y, -ex.y) / det;
            Vector2 g = new Vector2(headForwardFigure.x, headForwardFigure.y);
            if (g.sqrMagnitude < 1e-9f || toward.sqrMagnitude < 1e-12f || right.sqrMagnitude < 1e-12f) return View.Front;
            float a = Mathf.Atan2(Vector2.Dot(g, right.normalized), Vector2.Dot(g, toward.normalized)) * Mathf.Rad2Deg;
            return ViewOfAngle(a);
        }

        /// <summary>
        /// The world ground direction his head points, from the head's facing in the figure's frame read
        /// through the facet's own projection (the same frame <see cref="ViewOf"/> reads): a ground step drawn
        /// on screen IS the world ground step, because the camera is orthographic down +Z and the ground is
        /// drawn at its own world (x, y). False (no aim) for a degenerate facet or a head pointing straight up.
        /// </summary>
        public static bool DrawnGroundDirection(Vector3 headForwardFigure, Transform facet, out Vector2 aim)
        {
            aim = Vector2.zero;
            if (facet == null) return false;
            Vector3 ex = facet.TransformVector(Vector3.right), ey = facet.TransformVector(Vector3.up);
            Vector2 d = new Vector2(ex.x, ex.y) * headForwardFigure.x + new Vector2(ey.x, ey.y) * headForwardFigure.y;
            if (d.sqrMagnitude < 1e-12f) return false;
            aim = d.normalized;
            return true;
        }

        /// <summary>The view for a facing angle in degrees (0 toward the viewer, positive to screen right).</summary>
        public static View ViewOfAngle(float degrees)
        {
            float a = Mathf.Abs(Mathf.DeltaAngle(0f, degrees));
            bool right = Mathf.DeltaAngle(0f, degrees) > 0f;
            if (a <= 22.5f) return View.Front;
            if (a <= 67.5f) return right ? View.FrontQuarterRight : View.FrontQuarterLeft;
            if (a <= 112.5f) return right ? View.SideRight : View.SideLeft;
            if (a <= 157.5f) return right ? View.BackQuarterRight : View.BackQuarterLeft;
            return View.Back;
        }

        // Pixel maps, top row first, drawn for the RIGHT-facing views and mirrored for the left.
        // S strap · B buckle · h housing top · H housing · L lens. The anchor is the lens pixel where the
        // view shows one, else the middle of the band; the sprite's pivot sits on its centre.
        private static readonly string[][] Maps =
        {
            new[] { "..hhh..", "SSHLHSS" },   // Front: lamp on the forehead, strap both ways
            new[] { "...hh.", "SSSHLS" },     // FrontQuarterRight
            new[] { "....hh.", "SSSSSHL" },   // SideRight: lamp at the brow in profile, strap round the head
            new[] { ".....h", "SSSSSH" },     // BackQuarterRight: strap and the lamp's edge past his head
            new[] { "SSSBSSS" },              // Back: strap only
        };
        private static readonly Vector2Int[] Anchors =
        {
            new(3, 1), new(4, 1), new(6, 1), new(3, 1), new(3, 0),
        };

        public static readonly Color32 StrapColour = new(84, 74, 64, 255);
        public static readonly Color32 BuckleColour = new(140, 132, 118, 255);
        public static readonly Color32 HousingColour = new(170, 74, 58, 255);
        public static readonly Color32 HousingTopColour = new(198, 104, 82, 255);
        public static readonly Color32 LensUnlitColour = new(150, 170, 186, 255);
        public static readonly Color32 LensLitColour = new(255, 246, 220, 255);

        /// <summary>The sprite drawn for a view, lit or unlit (the lens is the only pixel that changes).</summary>
        public static Sprite SpriteFor(View view, bool lit)
        {
            if (_litSprites == null) BuildSprites();
            return (lit ? _litSprites : _unlitSprites)[(int)view];
        }

        private static void BuildSprites()
        {
            int views = System.Enum.GetValues(typeof(View)).Length;
            _unlitSprites = new Sprite[views];
            _litSprites = new Sprite[views];
            for (int v = 0; v < views; v++)
            {
                bool left = v > (int)View.Back;
                int map = left ? v - (int)View.Back : v;   // FrontQuarterLeft→1, SideLeft→2, BackQuarterLeft→3
                _unlitSprites[v] = BuildSprite(Maps[map], Anchors[map], left, false, ((View)v).ToString());
                _litSprites[v] = BuildSprite(Maps[map], Anchors[map], left, true, ((View)v).ToString());
            }
        }

        private static Sprite BuildSprite(string[] rows, Vector2Int anchor, bool mirror, bool lit, string name)
        {
            int h = rows.Length, w = rows[0].Length;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "Headlamp" + name + (lit ? "Lit" : ""), filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color32[w * h];
            for (int r = 0; r < h; r++)
            for (int x = 0; x < w; x++)
            {
                char c = rows[r][mirror ? w - 1 - x : x];
                pixels[(h - 1 - r) * w + x] = c switch
                {
                    'S' => StrapColour,
                    'B' => BuckleColour,
                    'h' => HousingTopColour,
                    'H' => HousingColour,
                    'L' => lit ? LensLitColour : LensUnlitColour,
                    _ => new Color32(0, 0, 0, 0),
                };
            }
            texture.SetPixels32(pixels); texture.Apply();
            int ax = mirror ? w - 1 - anchor.x : anchor.x;
            var pivot = new Vector2((ax + .5f) / w, (h - 1 - anchor.y + .5f) / h);
            var sprite = Sprite.Create(texture, new Rect(0, 0, w, h), pivot, ArtPixelsPerMetre);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        /// <summary>Art pixels per world unit — the figure grid the snap rounds to.</summary>
        public const float ArtPixelsPerMetre = 32f;

        private static Vector2 OnArtGrid(Vector2 d) =>
            new(Mathf.Round(d.x * ArtPixelsPerMetre) / ArtPixelsPerMetre, Mathf.Round(d.y * ArtPixelsPerMetre) / ArtPixelsPerMetre);

        private Vector3 SnapDrawnHead(Vector3 world)
        {
            if (_headCamera == null) _headCamera = SceneLight.ActiveCamera();
            Camera camera = _headCamera;
            if (camera == null) return world;
            Vector3 pixel = camera.WorldToScreenPoint(world);
            double perPixel = camera.orthographicSize * 2d / camera.pixelHeight;
            double left = camera.transform.position.x - camera.orthographicSize * camera.aspect;
            double bottom = camera.transform.position.y - camera.orthographicSize;
            if (GameServices.Config != null && GameServices.Config.FigurePixelTarget &&
                FigurePixelGrid.TryPlan(camera.pixelWidth, camera.pixelHeight, left, bottom, perPixel, out var plan))
            {
                pixel.x = (float)(plan.OffsetX + (System.Math.Floor(world.x * 32d) - plan.OriginTexelX + .5d) * plan.Magnification);
                pixel.y = (float)(plan.OffsetY + (System.Math.Floor(world.y * 32d) - plan.OriginTexelY + .5d) * plan.Magnification);
            }
            else
            {
                pixel.x = Mathf.Floor(pixel.x) + .5f;
                pixel.y = Mathf.Floor(pixel.y) + .5f;
            }
            return camera.ScreenToWorldPoint(pixel);
        }

        private void BuildFitting()
        {
            var go = new GameObject("HeadlampFitting") { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
            go.transform.SetParent(transform, false);
            _fitting = go.AddComponent<SpriteRenderer>();
            _fitting.sprite = SpriteFor(_view, _on);
            _fitting.sharedMaterial = Resources.Load<Material>("PlayerSubmerge");
            _fitting.enabled = false;
        }

        /// <summary>
        /// Publish this beam as the lit-decor path's one lamp, IF he is the one who owns it this frame.
        /// Returns whether it published.
        ///
        /// <para>Split out of <c>LateUpdate</c> for one reason: EditMode runs no Unity messages, so a
        /// fixture that could not call this could only assert the globals by reaching through reflection
        /// at a private frame callback — and the defect this closes (the boats standing off with nothing
        /// put in their place) is invisible to every other kind of assertion. The rule is worth a seam.</para>
        /// </summary>
        public bool PublishDecorLampIfOwned()
        {
            if (!WalkerLights.OwnsDecorLight) return false;
            if (!TryGetWaterLight(out WaterLightState state)) return false;
            DecorLampGlobals.Publish(in state);
            _decorOwner = this;
            return true;
        }

        private void ApplyBeamState()
        {
            if (Light != null) Light.enabled = _live && _on;
            if (_lens != null) _lens.enabled = _live && _on;
            if (_fitting != null)
            {
                if (!(_live && _worn)) _fitting.enabled = false;
                else _fitting.sprite = SpriteFor(_view, _on);
            }
        }

        /// <summary>
        /// The water's view of this beam: the same numbers the land quad burns at, so the sea and the
        /// ground can never disagree about where he is pointing or how hard the lamp is going.
        /// </summary>
        public bool TryGetWaterLight(out WaterLightState state)
        {
            SceneLight l = Light;
            if (l == null || !_on || !l.enabled || !l.isActiveAndEnabled)
            {
                state = default;
                return false;
            }

            float half = Mathf.Clamp(l.ConeHalfAngle, 0f, 89.5f);
            float inner = half * (1f - Mathf.Clamp01(l.AngularSoftness));
            Vector2 dir = transform.up;

            _waterLight = new WaterLightState
            {
                LampWorld = l.WorldOrigin,
                LampHeightMeters = l.LampHeightMeters,
                BeamDir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.up,
                Color = l.Color,
                Intensity = Mathf.Max(0f, l.Intensity) * Mathf.Max(0f, _waterStrength),
                Range = Mathf.Max(0.01f, l.ReachMetres),
                CosHalfAngle = LightMath.CosFromHalfAngleDeg(half),
                CosInnerAngle = LightMath.CosFromHalfAngleDeg(inner),
                EdgeSoftness = Mathf.Clamp01(l.EdgeSoftness),
                GateThreshold = l.GateThreshold,
                GateSoftness = l.GateSoftness,
                GateFallback = l.GateFallback,
            };
            state = _waterLight;
            return true;
        }

    }
}
