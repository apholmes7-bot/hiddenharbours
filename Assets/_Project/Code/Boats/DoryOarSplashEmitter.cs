using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>Draws the oar splash</b> (owner ruling 2026-11-13: the dory's oars "MUST splash and
    /// foam"): droplets on the frame a rowing blade enters the water, and a short patch of foam
    /// left where the blade works, fading behind the boat. Since 2026-11-17 also the rest of the
    /// stroke: the flick and the turning puddle where the blade comes out, rings spreading from the
    /// catch and the release (drawn squashed by <c>sin(elevation)</c>, because they lie on the water),
    /// and drips falling from the blade tip on the recovery, each with a ring where it lands.
    ///
    /// <para><b>The timing is the oar's own.</b> Each frame this reads the pose
    /// <see cref="DoryOarMeshLayer"/> actually drew — the same sweep and dip the mesh was rotated
    /// to — and asks <see cref="DoryOarSplashMath"/> whether that blade is in the water. There is
    /// no second stroke clock, so the splash cannot land on a different frame from the blade.</para>
    ///
    /// <para><b>Where.</b> The blade's point on the waterline is projected through the same rig
    /// camera the hull's own anchors use (<see cref="MountedRockPoseMath.Project"/>), from the
    /// drawn hull's position, so the splash sits under the drawn blade on every heading.</para>
    ///
    /// <para><b>When not.</b> A shipped or trailing oar, a dory running on her outboard (the layer
    /// stows the oars), an empty helm and a boat aground all report "not rowing", and a blade that
    /// is not rowing never splashes.</para>
    ///
    /// <para><b>Reuse and cost (rule 7).</b> The motes are the wake's <see cref="WakeParticleSystem"/>
    /// pools drawn with the wake host's own foam rafts (<see cref="BoatWakeEmitter.SharedFoamRafts"/>)
    /// in the wake's sorting band. Two fixed pools of sprite renderers built once at wiring; a
    /// frame with nothing alive and nobody rowing costs two bool reads. Installed and removed by
    /// <see cref="BoatHullSkinner"/> with the oar fittings themselves.</para>
    ///
    /// <para>Visual only: reads the drawn pose and the Core environment sample, writes nothing to
    /// the sim, saves nothing (rule 5).</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class DoryOarSplashEmitter : MonoBehaviour
    {
        [Tooltip("The dials. Written by the skinner from the hull's BoatVisualDef (OarSplash) at " +
                 "every wiring — tune them on that asset, not here.")]
        [SerializeField] private OarSplashConfig _config = OarSplashConfig.Default;

        private DoryOarMeshLayer _oars;
        private MeshHullDriver _driver;
        private BoatController _boat;
        private Vector3 _portLock, _starLock;

        private DoryOarSplashSystem _system;
        private Transform _moteRoot;
        private SpriteRenderer[] _foamRenderers, _dropletRenderers;
        private bool _spritesBound;

        /// <summary>The logic this component draws — for tests and tooling.</summary>
        public DoryOarSplashSystem Logic => _system;

        /// <summary>True once the skinner has handed over the oars, the hull driver and the locks.</summary>
        public bool IsWired => _oars != null && _driver != null && _system != null;

        /// <summary>
        /// Wire the emitter — the skinner's path, called on every (re-)skin. The oarlocks are the
        /// fittings' own pivots in hull-rig metres (<c>HullPropMeshDef.PivotLocalMeters</c>).
        /// </summary>
        public void Configure(DoryOarMeshLayer oars, MeshHullDriver driver, BoatController boat,
                              Vector3 portLockLocalMeters, Vector3 starLockLocalMeters,
                              in OarSplashConfig config)
        {
            _oars = oars;
            _driver = driver;
            _boat = boat;
            _portLock = portLockLocalMeters;
            _starLock = starLockLocalMeters;
            _config = config;

            int foam = Mathf.Max(1, config.FoamPoolSize);
            int drops = Mathf.Max(1, config.DropletPoolSize);
            int rings = Mathf.Max(1, config.RipplePoolSize);
            int drips = Mathf.Max(1, config.DripPoolSize);
            if (_system == null || _system.Foam.Capacity != foam || _system.Droplets.Capacity != drops ||
                _system.Ripples.Capacity != rings || _system.Drips.Capacity != drips)
            {
                DestroyMotes();
                _system = new DoryOarSplashSystem(foam, drops, rings, drips);
            }
            else if (_ringRenderers != null && !RingPictureMatches(in config))
            {
                // A retuned band changes the ring picture: rebuild it on the next draw.
                DestroyMotes();
            }
            _system.ResetTracking();
        }

        private void OnDisable() => HideAll();

        private void OnDestroy() => DestroyMotes();

        private void LateUpdate()
        {
            if (!IsWired) return;

            bool live = _config.Enabled && _oars.isActiveAndEnabled && _oars.IsWired;
            bool afloat = _boat == null || !_boat.IsAground;
            bool portStroking = live && afloat && _oars.PortStroking;
            bool starStroking = live && afloat && _oars.StarboardStroking;

            // Nothing rowing and nothing left fading: the whole component is two bool reads.
            if (!portStroking && !starStroking && !_anyAlive)
            {
                // Keep the blade tracking honest across the idle stretch without doing the projection.
                if (live) _system.ResetTracking();
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float elevation = _driver.ElevationDegrees * Mathf.Deg2Rad;
            float turntable = _driver.CurrentDirUnits * (Mathf.PI / 4f);
            Transform drawn = _driver.Visual != null ? _driver.Visual : transform;
            Vector2 origin = drawn.position;
            float waterline = _driver.DesignWaterlineMeters;

            Vector2 aft = Direction(new Vector3(0f, -1f, 0f), turntable, elevation);
            _groundSquash = Mathf.Sin(elevation);
            float heightToScreen = Mathf.Cos(elevation);
            var port = Frame(DoryOarMeshLayer.PortSide, _portLock, _oars.PortPose, portStroking,
                             _oars.PortOarState, origin, turntable, elevation, waterline, aft, heightToScreen);
            var star = Frame(DoryOarMeshLayer.StarboardSide, _starLock, _oars.StarboardPose, starStroking,
                             _oars.StarboardOarState, origin, turntable, elevation, waterline, aft, heightToScreen);

            Vector2 current = Vector2.zero;
            var env = GameServices.Environment;
            if (env != null) current = env.Sample().CurrentVector;
            float speed = _boat != null ? _boat.Velocity.magnitude : 0f;

            _system.Step(in port, in star, speed, current, dt, in _config);
            Render();
        }

        private DoryOarSplashSystem.OarFrame Frame(int side, Vector3 lockLocal, in DoryOarMeshPose.Pose pose,
                                                   bool stroking, float oarState, Vector2 origin,
                                                   float turntable, float elevation, float waterline, Vector2 aft,
                                                   float heightToScreen)
        {
            Vector3 blade = DoryOarSplashMath.BladeOnWaterLocal(lockLocal, side, in pose,
                                                                _config.SplashReachMeters, waterline);
            Vector3 tip = DoryOarSplashMath.BladeOnWaterLocal(lockLocal, side, in pose,
                                                              _config.BladeReachMeters, waterline);
            return new DoryOarSplashSystem.OarFrame
            {
                Stroking = stroking,
                BladeHeightMeters = DoryOarSplashMath.BladeHeightMeters(lockLocal.z - waterline,
                                                                        _config.BladeReachMeters, pose.DipDegrees),
                BladeWorld = origin + MountedRockPoseMath.Project(blade, turntable, 0f, 0f, elevation),
                Outboard = Direction(new Vector3(side, 0f, 0f), turntable, elevation),
                Aft = aft,
                Effort01 = Mathf.Clamp01(Mathf.Abs(oarState)),
                StrokeDirection = oarState < 0f ? -1f : 1f,
                TipOnWaterWorld = origin + MountedRockPoseMath.Project(tip, turntable, 0f, 0f, elevation),
                GroundSquash = _groundSquash,
                HeightToScreen = heightToScreen,
            };
        }

        /// <summary>A hull-rig direction as a unit direction on screen (the projection is linear).</summary>
        private static Vector2 Direction(Vector3 rigDirection, float turntable, float elevation)
        {
            Vector2 d = MountedRockPoseMath.Project(rigDirection, turntable, 0f, 0f, elevation);
            return d.sqrMagnitude > 1e-8f ? d.normalized : Vector2.zero;
        }

        // ==== drawing ======================================================================================

        private bool _anyAlive;
        private float _groundSquash = 1f;

        /// <summary>The ring picture's resolution (px across). An implementation size, not a look dial:
        /// the ring is scaled to its radius in metres, and the band's shape is <see cref="OarSplashConfig"/>'s.</summary>
        public const int RingTextureSize = 64;

        private void Render()
        {
            EnsureMotes();
            WakeConfig foamCfg = _config.FoamWakeConfig();
            WakeConfig dropCfg = _config.DropletWakeConfig();
            bool foamAlive = RenderPool(_system.Foam, _foamRenderers, in foamCfg, _config.FoamColor);
            bool dropsAlive = RenderPool(_system.Droplets, _dropletRenderers, in dropCfg, _config.DropletColor);
            bool ringsAlive = RenderMarks(_system.Ripples, _ringRenderers, _config.RippleColor,
                                          _config.RippleSpreadEase, _config.RippleFadePower, _groundSquash);
            bool dripsAlive = RenderMarks(_system.Drips, _dripRenderers, _config.DropletColor, 1f, 1f, 1f);
            _anyAlive = foamAlive || dropsAlive || ringsAlive || dripsAlive;
        }

        /// <summary>
        /// Draw a pool of water marks. A ring lies on the water, so it is squashed by
        /// <paramref name="ySquash"/> (<c>sin(elevation)</c>) into the ellipse the camera sees; a drip
        /// is a round mote (squash 1). A mark still waiting out its delay stays hidden.
        /// </summary>
        private static bool RenderMarks(OarWaterMarkPool pool, SpriteRenderer[] renderers, Color tint,
                                        float spreadEase, float fadePower, float ySquash)
        {
            var marks = pool.Pool;
            bool any = false;
            for (int i = 0; i < marks.Length; i++)
            {
                SpriteRenderer sr = renderers[i];
                ref readonly var m = ref marks[i];
                if (m.Alive) any = true;
                if (!OarWaterMarkPool.IsShown(in m) || sr.sprite == null)
                {
                    if (sr.gameObject.activeSelf) sr.gameObject.SetActive(false);
                    continue;
                }

                float diameter = 2f * OarWaterMarkPool.Radius(in m, spreadEase) * SpriteScale(sr);
                tint.a = OarWaterMarkPool.Opacity(in m, fadePower);
                Transform t = sr.transform;
                t.position = new Vector3(m.Pos.x, m.Pos.y, 0f);
                t.localScale = new Vector3(diameter, diameter * ySquash, 1f);
                sr.color = tint;
                if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
            }
            return any;
        }

        private static bool RenderPool(WakeParticleSystem system, SpriteRenderer[] renderers,
                                       in WakeConfig cfg, Color tint)
        {
            var pool = system.Pool;
            bool any = false;
            for (int i = 0; i < pool.Length; i++)
            {
                SpriteRenderer sr = renderers[i];
                ref readonly var p = ref pool[i];
                if (!p.Alive)
                {
                    if (sr.gameObject.activeSelf) sr.gameObject.SetActive(false);
                    continue;
                }

                any = true;
                float life = WakeParticleSystem.Life01(p.Age, p.Lifetime);
                float size = WakeParticleSystem.LifeSpread(p.BaseSize, life, in cfg);
                tint.a = WakeParticleSystem.LifeFade(life, in cfg) * p.BirthStrength;

                Transform t = sr.transform;
                t.position = new Vector3(p.Pos.x, p.Pos.y, 0f);
                float scale = size * SpriteScale(sr);
                t.localScale = new Vector3(scale, scale, 1f);
                sr.color = tint;
                if (!sr.gameObject.activeSelf) sr.gameObject.SetActive(true);
            }
            return any;
        }

        /// <summary>Scale that makes this renderer's sprite one metre across, so a mote's size is in metres.</summary>
        private static float SpriteScale(SpriteRenderer sr)
        {
            Sprite s = sr.sprite;
            if (s == null) return 1f;
            float width = s.bounds.size.x;
            return width > 1e-4f ? 1f / width : 1f;
        }

        /// <summary>
        /// Build the two renderer pools once, and bind the wake's foam rafts as soon as the wake
        /// host has them. The motes live under their own unparented root: foam is laid on the
        /// WATER and must stay where it was laid while the boat rows on.
        /// </summary>
        private void EnsureMotes()
        {
            if (_moteRoot == null)
            {
                var root = new GameObject("[OarSplash]") { hideFlags = HideFlags.HideAndDontSave };
                if (Application.isPlaying) DontDestroyOnLoad(root);
                _moteRoot = root.transform;
                _foamRenderers = BuildRenderers(_system.Foam.Capacity, "oarFoam");
                _dropletRenderers = BuildRenderers(_system.Droplets.Capacity, "oarDroplet");
                _ringRenderers = BuildRenderers(_system.Ripples.Capacity, "oarRipple");
                _dripRenderers = BuildRenderers(_system.Drips.Capacity, "oarDrip");
                _spritesBound = false;

                // The ring is this effect's own picture (the wake's rafts are blobs), built once per
                // wiring from the band the config asks for.
                _ringSprite = BuildRingSprite(_config.RippleBandCentre, _config.RippleBandHalfWidth);
                _ringBandCentre = _config.RippleBandCentre;
                _ringBandHalfWidth = _config.RippleBandHalfWidth;
                for (int i = 0; i < _ringRenderers.Length; i++) _ringRenderers[i].sprite = _ringSprite;
            }

            if (_spritesBound) return;
            Sprite[][] rafts = BoatWakeEmitter.SharedFoamRafts;
            if (rafts == null || rafts.Length == 0) return;   // no wake host yet: try again next frame
            BindSprites(_foamRenderers, rafts);
            BindSprites(_dropletRenderers, rafts);
            BindSprites(_dripRenderers, rafts);
            _spritesBound = true;
        }

        private Sprite _ringSprite;
        private float _ringBandCentre, _ringBandHalfWidth;
        private SpriteRenderer[] _ringRenderers, _dripRenderers;

        private bool RingPictureMatches(in OarSplashConfig config)
            => Mathf.Approximately(_ringBandCentre, config.RippleBandCentre) &&
               Mathf.Approximately(_ringBandHalfWidth, config.RippleBandHalfWidth);

        private static Sprite BuildRingSprite(float bandCentre01, float bandHalfWidth01)
        {
            int n = RingTextureSize;
            float[] a = OarRippleTexture.RingCoverage(n, bandCentre01, bandHalfWidth01);
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false, true)
            {
                name = "OarRipple.Ring",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var px = new Color32[n * n];
            for (int i = 0; i < px.Length; i++)
                px[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a[i]) * 255f));
            tex.SetPixels32(px);
            tex.Apply(false, false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private SpriteRenderer[] BuildRenderers(int count, string label)
        {
            var arr = new SpriteRenderer[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject(label);
                go.transform.SetParent(_moteRoot, worldPositionStays: false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = _config.SortingOrder;
                go.SetActive(false);
                arr[i] = sr;
            }
            return arr;
        }

        private static void BindSprites(SpriteRenderer[] renderers, Sprite[][] rafts)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                Sprite[] rungs = rafts[WakeFoamTexture.VariantForSlot(i) % rafts.Length];
                if (rungs == null || rungs.Length == 0) continue;
                renderers[i].sprite = rungs[0];
                WakeFoamTexture.MirrorForSlot(i, out bool flipX, out bool flipY);
                renderers[i].flipX = flipX;
                renderers[i].flipY = flipY;
            }
        }

        private void HideAll()
        {
            Hide(_foamRenderers);
            Hide(_dropletRenderers);
            Hide(_ringRenderers);
            Hide(_dripRenderers);
        }

        private static void Hide(SpriteRenderer[] renderers)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null && renderers[i].gameObject.activeSelf)
                    renderers[i].gameObject.SetActive(false);
        }

        private void DestroyMotes()
        {
            if (_moteRoot != null)
            {
                if (Application.isPlaying) Destroy(_moteRoot.gameObject);
                else DestroyImmediate(_moteRoot.gameObject);
            }
            if (_ringSprite != null)
            {
                Texture2D tex = _ringSprite.texture;
                if (Application.isPlaying) { Destroy(_ringSprite); if (tex != null) Destroy(tex); }
                else { DestroyImmediate(_ringSprite); if (tex != null) DestroyImmediate(tex); }
            }
            _ringSprite = null;
            _moteRoot = null;
            _foamRenderers = null;
            _dropletRenderers = null;
            _ringRenderers = null;
            _dripRenderers = null;
            _spritesBound = false;
            _anyAlive = false;
        }
    }
}
