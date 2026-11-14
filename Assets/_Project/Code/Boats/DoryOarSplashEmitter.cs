using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Boats
{
    /// <summary>
    /// <b>Draws the oar splash</b> (owner ruling 2026-11-13: the dory's oars "MUST splash and
    /// foam"): droplets on the frame a rowing blade enters the water, and a short patch of foam
    /// left where the blade works, fading behind the boat.
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
            if (_system == null || _system.Foam.Capacity != foam || _system.Droplets.Capacity != drops)
            {
                DestroyMotes();
                _system = new DoryOarSplashSystem(foam, drops);
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
            var port = Frame(DoryOarMeshLayer.PortSide, _portLock, _oars.PortPose, portStroking,
                             _oars.PortOarState, origin, turntable, elevation, waterline, aft);
            var star = Frame(DoryOarMeshLayer.StarboardSide, _starLock, _oars.StarboardPose, starStroking,
                             _oars.StarboardOarState, origin, turntable, elevation, waterline, aft);

            Vector2 current = Vector2.zero;
            var env = GameServices.Environment;
            if (env != null) current = env.Sample().CurrentVector;
            float speed = _boat != null ? _boat.Velocity.magnitude : 0f;

            _system.Step(in port, in star, speed, current, dt, in _config);
            Render();
        }

        private DoryOarSplashSystem.OarFrame Frame(int side, Vector3 lockLocal, in DoryOarMeshPose.Pose pose,
                                                   bool stroking, float oarState, Vector2 origin,
                                                   float turntable, float elevation, float waterline, Vector2 aft)
        {
            Vector3 blade = DoryOarSplashMath.BladeOnWaterLocal(lockLocal, side, in pose,
                                                                _config.SplashReachMeters, waterline);
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

        private void Render()
        {
            EnsureMotes();
            WakeConfig foamCfg = _config.FoamWakeConfig();
            WakeConfig dropCfg = _config.DropletWakeConfig();
            bool foamAlive = RenderPool(_system.Foam, _foamRenderers, in foamCfg, _config.FoamColor);
            bool dropsAlive = RenderPool(_system.Droplets, _dropletRenderers, in dropCfg, _config.DropletColor);
            _anyAlive = foamAlive || dropsAlive;
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
                _spritesBound = false;
            }

            if (_spritesBound) return;
            Sprite[][] rafts = BoatWakeEmitter.SharedFoamRafts;
            if (rafts == null || rafts.Length == 0) return;   // no wake host yet: try again next frame
            BindSprites(_foamRenderers, rafts);
            BindSprites(_dropletRenderers, rafts);
            _spritesBound = true;
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
            _moteRoot = null;
            _foamRenderers = null;
            _dropletRenderers = null;
            _spritesBound = false;
            _anyAlive = false;
        }
    }
}
