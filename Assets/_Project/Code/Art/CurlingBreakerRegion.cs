using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>ADR 0040 B2 — one region's curl host.</b> Holds the region's picked
    /// <see cref="CurlingBreakerSiteDef"/>s, reads the sea the water draws once per frame, and keeps
    /// the fixed <see cref="CurlingBreakerPool"/> filled for the renderer feature to draw. Render and
    /// pool state only: it never writes the wave field, the surf, the tide or anything a hull reads.
    ///
    /// <para>Installed by the editor's scene wiring in a region scene whose data folder has picked
    /// sites; with the master at 0 it holds no slot and the feature records no pass.</para>
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class CurlingBreakerRegion : MonoBehaviour
    {
        [SerializeField] private CurlingBreakerSiteDef[] _sites = Array.Empty<CurlingBreakerSiteDef>();
        [Tooltip("The sea renderer whose water material the curl takes its surf dials and colours from.")]
        [SerializeField] private Renderer _sea;
        [SerializeField] private Shader _shader;

        private static readonly int SurfStrengthId = Shader.PropertyToID("_SurfStrength");
        private static readonly int PlungeStrengthId = Shader.PropertyToID("_SurfPlungeStrength");
        private static readonly int BeatStrengthId = Shader.PropertyToID("_SurfBeatStrength");
        private static readonly int LipThrowId = Shader.PropertyToID("_SurfLipThrow");
        private static readonly int LipWidthId = Shader.PropertyToID("_SurfLipWidth");
        private static readonly int LipColorId = Shader.PropertyToID("_SurfLipColor");
        private static readonly int FoamColorId = Shader.PropertyToID("_PaletteFoam");
        private static readonly int AgeStrengthId = Shader.PropertyToID("_SurfAgeStrength");
        private static readonly int BarrelColorId = Shader.PropertyToID("_SurfBarrelColor");
        private static readonly int BarrelShadeId = Shader.PropertyToID("_SurfBarrelShade");

        private CurlingBreakerPool _pool;
        private Material _material;
        private MaterialPropertyBlock _seaBlock;

        // The dominant train's contour, solved again only when its inputs change.
        private BreakerContour _contour = BreakerContour.None;
        private bool _contourSolved;
        private float _contourWavelength, _contourAmplitude, _contourSpeed, _contourLee, _contourIndex, _contourBand;

        public IReadOnlyList<CurlingBreakerSiteDef> Sites => _sites;
        public Material Material => _material;
        public CurlingBreakerPool Pool => _pool;
        public int LiveCount => _pool != null ? _pool.LiveCount : 0;

        /// <summary>Sets the sites, the sea and the shader (the scene wiring, and fixtures).</summary>
        public void Configure(CurlingBreakerSiteDef[] sites, Renderer sea, Shader shader)
        {
            _pool?.ReleaseAll();
            _sites = sites ?? Array.Empty<CurlingBreakerSiteDef>();
            _sea = sea;
            if (_shader != shader)
            {
                DestroyMaterial();
                _shader = shader;
            }
            if (isActiveAndEnabled) EnsureMaterial();
        }

        private void OnEnable()
        {
            _pool ??= new CurlingBreakerPool();
            _seaBlock ??= new MaterialPropertyBlock();
            EnsureMaterial();
            CurlingBreakerRegistry.Register(this);
        }

        private void OnDisable()
        {
            CurlingBreakerRegistry.Unregister(this);
            _pool?.Dispose();
            _pool = null;
            DestroyMaterial();
        }

        private void LateUpdate() => Tick();

        /// <summary>One frame: the live sea, the look, then <see cref="Step"/>. Anything missing (the
        /// master at 0, no published field, no displaced sea) releases every slot.</summary>
        public void Tick()
        {
            if (_pool == null) return;
            CurlingBreakerSettings settings = CurlingBreakerPasses.Settings;
            ITidalTerrain terrain = GameServices.TidalTerrain;
            IEnvironmentService environment = GameServices.Environment;
            IGameClock clock = GameServices.Clock;
            if (!settings.Enabled || terrain == null || environment == null || clock == null
                || !DisplacedWaterRegistry.TryGetIsoDepthFrame(out WaterIsoDepthFrame frame)
                || !TryReadLive(terrain, environment.WaterLevelAt(clock.TotalSeconds), out CurlingBreakerInputs inputs))
            {
                _pool.ReleaseAll();
                return;
            }
            CurlingBreakerLook look = ReadLook();
            CopyColours();
            Step(in inputs, in frame, in settings, in look);
        }

        /// <summary>
        /// The sea as the water draws it this frame: the PUBLISHED trains (<see cref="SharedWaveField"/>,
        /// the same the bridge packs into the shader) read at time 0, on the drawn wave scale — the
        /// spray emitter's read. False when nothing has published or the displaced sea is off.
        /// </summary>
        public bool TryReadLive(ITidalTerrain terrain, float waterLevelMeters, out CurlingBreakerInputs inputs)
        {
            inputs = default;
            if (terrain == null || !SharedWaveField.TryGet(out WaveTrains trains) || trains.Count <= 0) return false;
            if (!DisplacedSea.TryGet(out DisplacedSeaState sea)) return false;
            BreakerSettings breakers = GameServices.Breakers;
            BreakerContour contour = ContourFor(in trains, in breakers);
            if (!contour.Breaks) return false;
            inputs = new CurlingBreakerInputs(terrain, waterLevelMeters, in contour, in trains,
                WaveFieldBridge.Pack(in trains), GameServices.WaveField.Gravity, in breakers,
                FoamInjectionRegistry.DrawnWaveScale, 0.0, in sea);
            return true;
        }

        /// <summary>
        /// Samples every enabled site into its slot and writes its meshes. A site without eligible
        /// surf, or with no free slot, yields. Public so fixtures can drive a frame from known inputs.
        /// </summary>
        public void Step(in CurlingBreakerInputs inputs, in WaterIsoDepthFrame frame,
                         in CurlingBreakerSettings settings, in CurlingBreakerLook look)
        {
            if (_pool == null) return;
            if (!settings.Enabled || !look.Active)
            {
                _pool.ReleaseAll();
                return;
            }
            int rows = CurlingBreakerSampler.RowsFor(in settings);
            for (int i = 0; i < _sites.Length; i++)
            {
                CurlingBreakerSiteDef site = _sites[i];
                if (site == null) continue;
                if (!site.Enabled || !site.IsValid(in settings))
                {
                    _pool.Release(site);
                    continue;
                }
                CurlingBreakerPool.Slot slot = _pool.Claim(site);
                if (slot == null) continue;
                CurlingBreakerSampler.Sample(in inputs, GameServices.FetchAt(site.Centre), site.Centre,
                    site.LengthMeters, in settings, in look, slot.Pose);
                if (!slot.Pose.Live)
                {
                    _pool.Release(site);
                    continue;
                }
                Bounds sheet = CurlingBreakerSampler.FillSheet(slot.Pose, in frame, rows,
                    slot.SheetVertices, slot.SheetUvs);
                Bounds mask = CurlingBreakerSampler.FillMask(slot.Pose, in frame, look.LipWidth,
                    slot.MaskVertices, slot.MaskUvs);
                slot.Apply(slot.Pose.Columns, rows, sheet, mask);
            }
        }

        /// <summary>The surf dials as the sea renderer draws them (its property block first).</summary>
        public CurlingBreakerLook ReadLook()
        {
            Material sea = _sea != null ? _sea.sharedMaterial : null;
            if (sea == null || _seaBlock == null) return default;
            _sea.GetPropertyBlock(_seaBlock);
            return new CurlingBreakerLook(SeaFloat(sea, SurfStrengthId), SeaFloat(sea, PlungeStrengthId),
                SeaFloat(sea, BeatStrengthId), SeaFloat(sea, LipThrowId), SeaFloat(sea, LipWidthId));
        }

        private void CopyColours()
        {
            Material sea = _sea != null ? _sea.sharedMaterial : null;
            if (sea == null || _material == null) return;
            _material.SetColor(LipColorId, SeaColor(sea, LipColorId));
            _material.SetColor(FoamColorId, SeaColor(sea, FoamColorId));
            _material.SetColor(BarrelColorId, SeaColor(sea, BarrelColorId));
            _material.SetFloat(AgeStrengthId, SeaFloat(sea, AgeStrengthId));
            _material.SetFloat(BarrelShadeId, SeaFloat(sea, BarrelShadeId));
        }

        private float SeaFloat(Material sea, int id)
        {
            if (_seaBlock.HasFloat(id)) return _seaBlock.GetFloat(id);
            return sea.HasProperty(id) ? sea.GetFloat(id) : 0f;
        }

        private Color SeaColor(Material sea, int id)
        {
            if (_seaBlock.HasColor(id)) return _seaBlock.GetColor(id);
            return sea.HasProperty(id) ? sea.GetColor(id) : Color.clear;
        }

        private BreakerContour ContourFor(in WaveTrains trains, in BreakerSettings breakers)
        {
            WaveTrain dominant = trains.Dominant;
            WaveFetchSettings fetch = GameServices.WaveFetch;
            float lee = GameServices.SwellClimate != null && trains.DominantIndex >= SwellMath.FirstSlot
                ? 0f : WaveFetch.TrainEnvelope01(0f, dominant.Wavelength, in fetch);
            if (_contourSolved && dominant.Wavelength == _contourWavelength && dominant.Amplitude == _contourAmplitude
                && dominant.PhaseSpeed == _contourSpeed && lee == _contourLee
                && breakers.BreakerIndex == _contourIndex && breakers.BreakBandRatio == _contourBand)
                return _contour;
            _contour = BreakerMath.ContourFor(in dominant, lee, in breakers);
            _contourSolved = true;
            _contourWavelength = dominant.Wavelength;
            _contourAmplitude = dominant.Amplitude;
            _contourSpeed = dominant.PhaseSpeed;
            _contourLee = lee;
            _contourIndex = breakers.BreakerIndex;
            _contourBand = breakers.BreakBandRatio;
            return _contour;
        }

        private void EnsureMaterial()
        {
            if (_material != null || _shader == null) return;
            _material = new Material(_shader) { name = "CurlingBreaker", hideFlags = HideFlags.HideAndDontSave };
        }

        private void DestroyMaterial()
        {
            if (_material == null) return;
            if (Application.isPlaying) Object.Destroy(_material);
            else Object.DestroyImmediate(_material);
            _material = null;
        }
    }
}
