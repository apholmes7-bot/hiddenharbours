using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>One deck-only draw with cached camera-visibility and house-intersection geometry.</summary>
    [DisallowMultipleComponent,DefaultExecutionOrder(201)]
    public sealed class HullDeckShadowCaster:MonoBehaviour
    {
        static Material _shared,_legacy;
        static int _users;
        static readonly int RayId=Shader.PropertyToID("_HHDeckSunRay"),LevelId=Shader.PropertyToID("_HHDeckLevel");
        static readonly int TrianglesId=Shader.PropertyToID("_HHDeckTriangles"),NodesId=Shader.PropertyToID("_HHDeckNodes"),CountId=Shader.PropertyToID("_HHDeckNodeCount");
        static readonly int ColorId=Shader.PropertyToID("_ShadowColor"),IdsId=Shader.PropertyToID("_HullIds");
        static readonly int GateId=Shader.PropertyToID("_HHSunLevelGate"),DoorId=Shader.PropertyToID("_HHSunDoorOpen");
        IsoFacetHullRenderer _hull;
        HullDeckShadowMesh _geometry;
        Mesh _source,_closed,_open;
        float _height;
        GameObject _draw;
        MeshRenderer _renderer;
        MaterialPropertyBlock _block;
        readonly Vector4[] _roles=new Vector4[31];
        public HullDeckShadowMesh Geometry=>_geometry;
        public MeshRenderer ShadowRenderer=>_renderer;
        public SpriteShadowProfile Profile=>SpriteShadow.SharedProfile;
        public static HullDeckShadowCaster Fit(GameObject host)
        {
            var caster=host.GetComponent<HullDeckShadowCaster>();
            if(caster==null)caster=host.AddComponent<HullDeckShadowCaster>();
            caster.Tick();return caster;
        }
        void LateUpdate()=>Tick();
        void OnDisable()=>Release();
        void OnDestroy()=>Release();
        public void Tick()
        {
            if(!isActiveAndEnabled){Release();return;}
            if(_hull==null)_hull=GetComponent<IsoFacetHullRenderer>();
            if(_hull==null||!_hull.IsConfigured||!_hull.isActiveAndEnabled){if(_renderer!=null)_renderer.enabled=false;return;}
            HullSunShadowState state=_hull.SunShadowState;
            if(_geometry==null||_source!=state.Hull||_closed!=state.ClosedLeaf||_open!=state.OpenLeaf||_height!=state.DeckHeight)Rebuild(state);
            if(_renderer==null)return;
            var profile=Profile;
            Material material=profile.ScreenSpaceShade?_shared:_legacy;
            _renderer.sharedMaterial=material;
            float elevation=Shader.GetGlobalFloat("_SunElevation"),strength=Shader.GetGlobalFloat("_ShadowStrength");
            _renderer.enabled=material!=null&&_hull.HullId!=0&&HullSunShadowMath.IsActive(elevation,strength,material.GetFloat(HullSunShadowCaster.EnabledId));
            if(!_renderer.enabled)return;
            Transform pose=_hull.PosedMesh;
            _draw.transform.SetPositionAndRotation(pose.position,pose.rotation);_draw.transform.localScale=pose.lossyScale;
            Vector4 sun=Shader.GetGlobalVector("_SunDir");Vector2 dir=new Vector2(-sun.x,-sun.y).normalized;
            Vector3 localDirection=transform.worldToLocalMatrix.MultiplyVector(new Vector3(dir.x,dir.y,0));
            dir=new Vector2(localDirection.x,localDirection.y).normalized;
            float length=DayNightMath.ShadowLength(elevation,profile.LengthAtNoon,profile.LengthAtHorizon,profile.MaxLength);
            Vector4 projection=HullSunShadowMath.Projection(dir,length,state.Elevation,0,0,state.PixelsPerMetre);
            Vector3 towardSun=HullSunShadowMath.Pose(state.Heading,state.Elevation,state.Roll,state.Pitch).inverse.MultiplyVector(
                new Vector3(-projection.x,-projection.y/Mathf.Max(projection.z,1e-5f),1)).normalized;
            _block.SetVector(RayId,towardSun);_block.SetFloat(LevelId,_geometry.DeckLevel);
            Color color=profile.ShadowColor;color.a=DayNightMath.ShadowAlpha(profile.MaxAlpha,strength);_block.SetColor(ColorId,color);
            _block.SetVector(IdsId,new Vector4(_hull.HullId/255f,_hull.ForeHullId/255f,IsoFacetHullRenderer.DeckOccupantSlots,0));
            _block.SetFloat(GateId,state.LevelGate?1:0);_block.SetFloat(DoorId,state.DoorOpen?1:0);
            _block.SetFloat(IsoFacetShaderIds.LevelShown,state.Level);_block.SetInteger(IsoFacetShaderIds.LiftLevels,state.LiftLevels);
            _block.SetInteger(IsoFacetShaderIds.ShownRoles,state.ShownRoles);
            for(int i=0;i<_roles.Length;i++)_roles[i]=state.RoleMask(i);
            _block.SetVectorArray(IsoFacetShaderIds.RoleLiftMasks,_roles);_renderer.SetPropertyBlock(_block);
        }
        void Rebuild(HullSunShadowState state)
        {
            Release();_source=state.Hull;_closed=state.ClosedLeaf;_open=state.OpenLeaf;_height=state.DeckHeight;
            _geometry=new HullDeckShadowMesh(state);
            if(_geometry.Receiver==null)return;
            if(_users++==0)
            {
                _shared=Shared(HullSunShadowCaster.MaterialPath);_legacy=Shared(HullSunShadowCaster.LegacyMaterialPath);
            }
            _draw=new GameObject("HullDeckShadow"){hideFlags=HideFlags.HideAndDontSave,layer=gameObject.layer};
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_draw,gameObject.scene);
            _draw.AddComponent<MeshFilter>().sharedMesh=_geometry.Receiver;
            _renderer=_draw.AddComponent<MeshRenderer>();_renderer.shadowCastingMode=ShadowCastingMode.Off;_renderer.receiveShadows=false;
            _renderer.lightProbeUsage=LightProbeUsage.Off;_renderer.sortingOrder=HiddenHarbours.Core.SortingBands.SunShade;
            _draw.AddComponent<SortingGroup>().sortingOrder=HiddenHarbours.Core.SortingBands.SunShade;
            _block=new MaterialPropertyBlock();_block.SetBuffer(TrianglesId,_geometry.TriangleBuffer);_block.SetBuffer(NodesId,_geometry.NodeBuffer);
            _block.SetInteger(CountId,_geometry.Nodes.Length);
        }
        static Material Shared(string path)
        {
            var material=new Material(Resources.Load<Material>(path)){hideFlags=HideFlags.HideAndDontSave,name="SharedHullDeckShadow"};
            material.EnableKeyword("HH_DECK_SHADOW");return material;
        }
        void Release()
        {
            if(_draw!=null)
            {
                if(_renderer!=null)_renderer.enabled=false;
                DestroyOwned(_draw);
                if(--_users==0){DestroyOwned(_shared);DestroyOwned(_legacy);_shared=null;_legacy=null;}
            }
            _geometry?.Dispose();_geometry=null;_draw=null;_renderer=null;
        }
        static void DestroyOwned(Object value){if(value==null)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
    }
}
