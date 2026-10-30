using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>A value snapshot; meshes are borrowed and visibility masks can only be read by index.</summary>
    public readonly struct HullSunShadowState
    {
        public readonly Mesh Hull, ClosedLeaf, OpenLeaf;
        public readonly float Heading, Elevation, Roll, Pitch, HeavePixels, RidePixels;
        public readonly int PixelsPerMetre;
        public readonly bool LevelGate, DoorOpen;
        public readonly int Level, LiftLevels, ShownRoles;
        readonly Vector4[] _roles;

        internal HullSunShadowState(IsoFacetHullSetup setup, float heading, float roll, float pitch,
            float heave, float ride, bool levelGate, bool doorOpen, HullMeshDef.Cut cut, Vector4[] roles)
        {
            Hull = setup.Mesh; ClosedLeaf = setup.DoorLeafClosed; OpenLeaf = setup.DoorLeafOpen;
            Heading = heading; Elevation = setup.ElevationDeg; Roll = roll; Pitch = pitch;
            HeavePixels = heave; RidePixels = ride; PixelsPerMetre = setup.PxPerMetre;
            LevelGate = levelGate; DoorOpen = doorOpen; Level = cut.Level;
            LiftLevels = cut.LiftLevels; ShownRoles = cut.ShownRoles; _roles = roles;
        }

        public Vector4 RoleMask(int index) => _roles != null && index >= 0 && index < _roles.Length ? _roles[index] : Vector4.zero;
    }

    /// <summary>Projects one cached mesh into the existing sun-shade band. The shared material owns its enabled switch.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(200)]
    public sealed class HullSunShadowCaster : MonoBehaviour
    {
        public const string MaterialPath = "HullSunShadow", LegacyMaterialPath = "HullSunShadowLegacy";
        public static readonly int EnabledId = Shader.PropertyToID("_CastSunShadow");
        static readonly int SunDir = Shader.PropertyToID("_SunDir"), SunElevation = Shader.PropertyToID("_SunElevation");
        static readonly int Strength = Shader.PropertyToID("_ShadowStrength"), PoseId = Shader.PropertyToID("_HHSunPose");
        static readonly int ProjectionId = Shader.PropertyToID("_HHSunProjection"), WaterlineId = Shader.PropertyToID("_HHSunWaterline");
        static readonly int ColorId = Shader.PropertyToID("_ShadowColor"), IdsId = Shader.PropertyToID("_HullIds");
        static readonly int GateId = Shader.PropertyToID("_HHSunLevelGate"), DoorId = Shader.PropertyToID("_HHSunDoorOpen");
        static Material _multiply, _legacy;
        IsoFacetHullRenderer _hull;
        GameObject _draw;
        MeshRenderer _renderer;
        SortingGroup _group;
        Mesh _proxy, _source, _closed, _open;
        MaterialPropertyBlock _block;
        readonly Vector4[] _roles = new Vector4[31];
        public Mesh Proxy => _proxy;
        public MeshRenderer ShadowRenderer => _renderer;
        public SpriteShadowProfile Profile => SpriteShadow.SharedProfile;

        public static HullSunShadowCaster Fit(GameObject host)
        {
            if (host == null) return null;
            var caster = host.GetComponent<HullSunShadowCaster>();
            if (caster == null) caster = host.AddComponent<HullSunShadowCaster>();
            caster.Tick();
            return caster;
        }

        void LateUpdate() => Tick();
        void OnDisable() => Release();
        void OnDestroy() => Release();

        public void Tick()
        {
            if (!isActiveAndEnabled) { Release(); return; }
            if (_hull == null) _hull = GetComponent<IsoFacetHullRenderer>();
            if (_hull == null || !_hull.IsConfigured || !_hull.isActiveAndEnabled)
            { if (_renderer != null) _renderer.enabled = false; return; }
            HullSunShadowState state = _hull.SunShadowState;
            if (_proxy == null || _source != state.Hull || _closed != state.ClosedLeaf || _open != state.OpenLeaf)
                Rebuild(state);
            SpriteShadowProfile look = Profile;
            Material material = look.ScreenSpaceShade ? (_multiply != null ? _multiply : _multiply = Resources.Load<Material>(MaterialPath))
                : (_legacy != null ? _legacy : _legacy = Resources.Load<Material>(LegacyMaterialPath));
            if (_renderer.sharedMaterial != material) _renderer.sharedMaterial = material;
            float e = Shader.GetGlobalFloat(SunElevation), strength = Shader.GetGlobalFloat(Strength);
            bool active = material != null && _hull.HullId != 0 && HullSunShadowMath.IsActive(e, strength, material.GetFloat(EnabledId));
            _renderer.enabled = active;
            if (!active) return;
            Vector4 light = Shader.GetGlobalVector(SunDir);
            Vector2 direction = new Vector2(-light.x, -light.y);
            if (direction.sqrMagnitude > 1e-6f) direction.Normalize(); else direction = Vector2.up;
            Vector3 localDirection = transform.worldToLocalMatrix.MultiplyVector(new Vector3(direction.x, direction.y, 0));
            float length = DayNightMath.ShadowLength(e, look.LengthAtNoon, look.LengthAtHorizon, look.MaxLength);
            Matrix4x4 pose = HullSunShadowMath.Pose(state.Heading, state.Elevation, state.Roll, state.Pitch);
            Vector4 projection = HullSunShadowMath.Projection(new Vector2(localDirection.x, localDirection.y), length,
                state.Elevation, state.HeavePixels, state.RidePixels, state.PixelsPerMetre);
            float waterline = state.RidePixels / Mathf.Max(1, state.PixelsPerMetre);
            _draw.transform.SetPositionAndRotation(transform.position, transform.rotation);
            _draw.transform.localScale = transform.lossyScale;
            _renderer.localBounds = HullSunShadowMath.ProjectBounds(_proxy.bounds, pose, projection, waterline);
            _block.SetMatrix(PoseId, pose);
            _block.SetVector(ProjectionId, projection);
            _block.SetFloat(WaterlineId, waterline);
            Color tint = look.ShadowColor;
            _block.SetColor(ColorId, new Color(tint.r, tint.g, tint.b, DayNightMath.ShadowAlpha(look.MaxAlpha, strength)));
            _block.SetVector(IdsId, new Vector4(_hull.HullId / 255f, _hull.ForeHullId / 255f, IsoFacetHullRenderer.DeckOccupantSlots, 0));
            _block.SetFloat(GateId, state.LevelGate ? 1 : 0);
            _block.SetFloat(DoorId, state.DoorOpen ? 1 : 0);
            _block.SetFloat(IsoFacetShaderIds.LevelShown, state.Level);
            _block.SetInteger(IsoFacetShaderIds.LiftLevels, state.LiftLevels);
            _block.SetInteger(IsoFacetShaderIds.ShownRoles, state.ShownRoles);
            for (int i = 0; i < _roles.Length; i++) _roles[i] = state.RoleMask(i);
            _block.SetVectorArray(IsoFacetShaderIds.RoleLiftMasks, _roles);
            _renderer.SetPropertyBlock(_block);
            int order = look.ScreenSpaceShade ? SortingBands.SunShade : _hull.OverlayRenderer.sortingOrder - 1;
            _group.sortingOrder = order;
            _renderer.sortingOrder = order;
        }

        void Rebuild(HullSunShadowState state)
        {
            Release();
            _source = state.Hull; _closed = state.ClosedLeaf; _open = state.OpenLeaf;
            _proxy = HullSunShadowMesh.Build(_source, _closed, _open);
            _draw = new GameObject("HullSunShadow") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_draw, gameObject.scene);
            _draw.AddComponent<MeshFilter>().sharedMesh = _proxy;
            _renderer = _draw.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _group = _draw.AddComponent<SortingGroup>();
            _group.sortingLayerID = 0;
            _block = new MaterialPropertyBlock();
        }

        void Release()
        {
            if (_renderer != null) _renderer.enabled = false;
            if (_draw != null) { if (Application.isPlaying) Destroy(_draw); else DestroyImmediate(_draw); }
            if (_proxy != null) { if (Application.isPlaying) Destroy(_proxy); else DestroyImmediate(_proxy); }
            _draw = null; _renderer = null; _group = null; _proxy = null;
        }
    }
}
