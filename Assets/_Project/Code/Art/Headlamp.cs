using System.Collections.Generic;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>The walker's headlamp</b> — the directed half of world-lighting PR 3: a narrow cone worn on her
    /// brow, switched, aimed wherever she is looking.
    ///
    /// <para><b>Why a headlamp and not a torch in her hand.</b> The owner asked for <i>"a spotlight"</i>.
    /// A hand torch would cost a hand — and the TOOL CONTINUITY LAW (owner playtest, 2026-08-23) says what
    /// she is holding does not silently change underneath her, so a torch would have to be taken out and
    /// put away, and would fight the rod and the catch for the same slot. A headlamp costs no slot. Its small housing and lens are drawn at the live mesh head point.</para>
    ///
    /// <para><b>It lights the world, not just a patch of ground</b> — which is the whole reason it is a
    /// <see cref="SceneLight"/> and not a sprite:
    /// <list type="bullet">
    ///   <item>the GROUND, through <see cref="LampPoolSystem"/> off <see cref="SceneLight.ReachMetres"/>
    ///   (#736's pool — the thing the bloom used to stand in for);</item>
    ///   <item>the WATER, through <see cref="IWaterLightEmitter"/> — from a wharf edge her beam plays on
    ///   the sea by the same relief model the searchlight uses (#691), taking one of the bridge's four
    ///   nearest-lamp slots;</item>
    ///   <item>the TREES, shrubs and shore plants, through the lit-decor path — which reads one global
    ///   lamp, so who writes it is arbitrated: see <see cref="WalkerLights.OwnsDecorLight"/>;</item>
    ///   <item>and it throws CAST SHADOWS off whatever stands in it (<see cref="LampShadowSystem"/>) —
    ///   except her own, which the carrier rule declines.</item>
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
        [Tooltip("Is the beam switched ON? A transient — never saved, and off is the state she starts in.")]
        [SerializeField] private bool _on;

        [Tooltip("How hard the beam reads on WATER relative to land, so the owner can balance the two " +
                 "independently — the same dial BoatSpotlight's searchlight carries, and for the same reason.")]
        [Range(0f, 2f)] [SerializeField] private float _waterStrength = 0.8f;

        private SceneLight _light;
        private WaterLightState _waterLight;
        private bool _live = true;
        private bool _hasHeadPoint;
        private Vector2 _headOffset;
        private float _projectedLift;
        private Transform _carrier, _facet;
        private IsoCharacterFigureRenderer _mountedFigure;
        private SpriteRenderer _fitting;
        private Camera _headCamera;
        private readonly List<IsoCharacterFigureRenderer> _figures = new(2);
        private readonly List<MeshFilter> _filters = new(3);
        private static Sprite _fittingSprite;
        private static Headlamp _decorOwner;

        /// <summary>Is the beam lit?</summary>
        public bool IsOn => _on;

        /// <summary>The lamp this drives.</summary>
        public SceneLight Light => _light != null ? _light : _light = GetComponent<SceneLight>();

        /// <summary>Set the height it is worn at. The beam's SHAPE is not a parameter: it comes from
        /// <see cref="LightPresets.ConeHalfDegrees"/>, so the angle is written in exactly one place and
        /// the preset library's own guard can assert it. Called once by the carrier.</summary>
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
            l.BloomLiftMetres = browHeightMetres;      // the lit fitting rides on her brow (#733)
            l.LampHeightMeters = browHeightMetres;     // and its cast shadows are thrown from there
            l.CastsShadows = true;
            ApplyBeamState();
        }

        /// <summary>Throw the switch.</summary>
        public void Toggle() => SetOn(!_on);

        /// <summary>Set the beam on or off.</summary>
        public void SetOn(bool on)
        {
            _on = on;
            ApplyBeamState();
        }

        /// <summary>
        /// Is this lamp hers to burn at all right now? Off her feet the whole component stands down —
        /// the renderer, the water slot and the decor singleton alike — so nothing of hers is lit while
        /// she is at a wheel.
        ///
        /// <para>⚠️ It does NOT clear <see cref="_on"/>. Stepping aboard and back off again should give her
        /// the beam she had, not a lamp she has to switch on twice; the state is hers, the LIVENESS is the
        /// mode's.</para>
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
        /// <b>⭐ SHE WRITES THE ONE DECOR LAMP — the other half of "one publisher".</b>
        ///
        /// <para><see cref="WalkerLights.OwnsDecorLight"/> makes every <see cref="BoatSpotlight"/> in the
        /// scene STAND OFF the <c>_BoatLight*</c> singleton while her beam is lit. That half on its own is
        /// not a handover, it is a HOLE: with the boats standing off and nothing put in their place, the
        /// five globals simply KEEP whatever a boat wrote last — a beam frozen at a moored dory's bow, or
        /// zeros in a region with no boat in it — and the trees in front of her are lit by a ghost, or not
        /// at all. Standing the boats off is only correct because this line takes their place.
        /// </para>
        ///
        /// <para><b>Off the WATER state, deliberately.</b> The numbers are the ones
        /// <see cref="TryGetWaterLight"/> has already composed, published through
        /// <see cref="DecorLampGlobals"/>'s <c>WaterLightState</c> overload — so her beam cannot light a
        /// spruce unlike it lights the sea she is pointing it over, and it carries the same water-strength
        /// scaling a boat's searchlight carries into this same path (<c>BoatSpotlight</c> publishes its
        /// <c>waterIntensity</c> here, not its land intensity). One packing, one meaning, both lamps.</para>
        ///
        /// <para><b>⚠️ LateUpdate, not Update, and that is the ordering guarantee.</b> Every
        /// <see cref="BoatSpotlight"/> publishes in <c>Update</c>. Writing here makes her the LAST word in
        /// the frame whatever order the boats ticked in — so on the frame her beam is switched ON she
        /// cannot be overwritten by a boat that happened to run after her, and on the frame it is switched
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
                if (f.isActiveAndEnabled && f.IsConfigured && f.IsAshore && f.Visible && f.HeadSnapPoint.w > .5f)
                { liveFigure = f; break; }
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
            if (drawn)
            {
                Vector4 h = liveFigure.HeadSnapPoint;
                Vector3 head = SnapDrawnHead(_facet.TransformPoint(new Vector3(h.x, h.y, h.z)));
                _headOffset = (Vector2)head - (Vector2)_carrier.position;
                _projectedLift = IsoGround.HeightScale * Mathf.Max(.01f, h.z);
                Light.LampHeightMeters = Mathf.Max(.01f, h.z);
                _hasHeadPoint = true;
            }
            // ConstantLiftUntilFirstHeadPoint; after that, every sprite pose holds this root offset.
            Vector2 point = (Vector2)_carrier.position + _headOffset;
            transform.position = new Vector3(point.x, point.y - _projectedLift, _carrier.position.z);
            Light.OriginOffset = Vector2.zero;
            Light.BloomLiftMetres = _projectedLift;
            Light.RefreshMount(); // SceneLight's earlier LateUpdate must not leave a one-frame bloom lag.
            if (drawn && _hasHeadPoint && _fitting == null) BuildFitting();
            if (_fitting != null)
            {
                _fitting.enabled = drawn && _live;
                _fitting.transform.position = new Vector3(point.x, point.y, _carrier.position.z);
                _fitting.transform.rotation = Quaternion.identity;
                _fitting.color = _on ? Color.white : new Color(.55f, .55f, .55f, 1f);
                if (drawn && liveFigure.AshoreOverlay != null)
                {
                    _fitting.sortingLayerID = liveFigure.AshoreOverlay.sortingLayerID;
                    _fitting.sortingOrder = Mathf.Min(SceneLight.MaxSortingOrder - 10,
                                                     liveFigure.AshoreOverlay.sortingOrder + 1);
                }
            }
        }

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
            if (_fittingSprite == null)
            {
                var texture = new Texture2D(5, 3, TextureFormat.RGBA32, false)
                { name = "HeadlampHousing", filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
                var pixels = new Color32[15];
                for (int y = 0; y < 3; y++)
                for (int x = 0; x < 5; x++)
                    pixels[y * 5 + x] = y == 1 && x > 0 && x < 4
                        ? new Color32(210, 220, 230, 255) : new Color32(38, 42, 46, 255);
                texture.SetPixels32(pixels); texture.Apply();
                // Centre the housing; its measured raster offset stays within one art pixel of the head origin.
                var pivot = new Vector2(.5f, .5f);
                _fittingSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), pivot, 32f);
                _fittingSprite.hideFlags = HideFlags.HideAndDontSave;
            }
            var go = new GameObject("HeadlampFitting") { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
            go.transform.SetParent(transform, false);
            _fitting = go.AddComponent<SpriteRenderer>();
            _fitting.sprite = _fittingSprite;
            _fitting.sharedMaterial = Resources.Load<Material>("PlayerSubmerge");
        }

        /// <summary>
        /// Publish this beam as the lit-decor path's one lamp, IF she is the one who owns it this frame.
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
        }

        /// <summary>
        /// The water's view of this beam: the same numbers the land quad burns at, so the sea and the
        /// ground can never disagree about where she is pointing or how hard the lamp is going.
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
