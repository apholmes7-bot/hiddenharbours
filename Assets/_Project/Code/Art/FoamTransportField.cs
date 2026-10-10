using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>Read-only view of the SAME published map; captures the byte revision at preparation.</summary>
    internal readonly struct FoamTransportMap
    {
        internal readonly Texture Texture;
        internal readonly Vector2 Min, Size;
        internal readonly float Low, High;
        internal readonly uint Revision;
        internal bool Bound => Texture != null;
        internal FoamTransportMap(Texture texture, Vector2 min, Vector2 size, float low, float high)
        { Texture=texture; Min=min; Size=size; Low=low; High=high; Revision=texture!=null?texture.updateCount:0; }
        internal FoamTransportMap Capture() => new FoamTransportMap(Texture,Min,Size,Low,High);
        internal bool Same(in FoamTransportMap other) => Texture==other.Texture && Min.Equals(other.Min) &&
            Size.Equals(other.Size) && Low==other.Low && High==other.High && Revision==other.Revision;

        internal bool TryReader(out Reader reader)
        {
            reader=default;
            if(!Bound) return true;
            if(!(Texture is Texture2D t) || !t.isReadable || !FoamTransport.Finite(Min) ||
                !FoamTransport.Finite(Size) || Size.x<=0 || Size.y<=0 ||
                !FoamTransport.Finite(Low) || !FoamTransport.Finite(High) || Low>High ||
                t.wrapModeU!=TextureWrapMode.Clamp || t.wrapModeV!=TextureWrapMode.Clamp ||
                GraphicsFormatUtility.IsSRGBFormat(t.graphicsFormat)) return false;
            if(t.format!=TextureFormat.R8 && t.format!=TextureFormat.R16 && t.format!=TextureFormat.RGBA32) return false;
            reader=new Reader(this,t);
            return true;
        }

        internal readonly struct Reader
        {
            private readonly NativeArray<byte> _bytes;
            private readonly NativeArray<ushort> _words;
            private readonly Vector2 _min, _size;
            private readonly float _low, _high;
            private readonly int _width, _height, _stride;
            private readonly bool _point, _bound;
            internal Reader(in FoamTransportMap map, Texture2D texture)
            {
                _min=map.Min; _size=map.Size; _low=map.Low; _high=map.High;
                _width=texture.width; _height=texture.height;
                _stride=texture.format==TextureFormat.RGBA32?4:1;
                _point=texture.filterMode==FilterMode.Point; _bound=true;
                _words=texture.format==TextureFormat.R16?texture.GetRawTextureData<ushort>():default;
                _bytes=texture.format!=TextureFormat.R16?texture.GetRawTextureData<byte>():default;
            }

            /// <summary>All filter support, not just corners. Still-water zero/outside = none.</summary>
            internal float BoundOverCell(Vector2 corner, float width, bool still)
            {
                if(!_bound) return float.NegativeInfinity;
                Vector2 lo=new Vector2((corner.x-_min.x)/_size.x,(corner.y-_min.y)/_size.y);
                Vector2 hi=new Vector2((corner.x+width-_min.x)/_size.x,(corner.y+width-_min.y)/_size.y);
                if(still && (lo.x<0 || lo.y<0 || hi.x>1 || hi.y>1)) return float.NegativeInfinity;
                float offset=_point?0:.5f;
                int x0=Mathf.Clamp(Mathf.FloorToInt(lo.x*_width-offset),0,_width-1);
                int y0=Mathf.Clamp(Mathf.FloorToInt(lo.y*_height-offset),0,_height-1);
                int x1=Mathf.Clamp(Mathf.FloorToInt(hi.x*_width-offset)+(_point?0:1),0,_width-1);
                int y1=Mathf.Clamp(Mathf.FloorToInt(hi.y*_height-offset)+(_point?0:1),0,_height-1);
                float result=still?float.PositiveInfinity:float.NegativeInfinity;
                for(int y=y0;y<=y1;y++) for(int x=x0;x<=x1;x++)
                {
                    int index=y*_width+x;
                    float code=_words.IsCreated?_words[index]/65535f:_bytes[index*_stride]/255f;
                    if(still && code==0) return float.NegativeInfinity;
                    float level=Mathf.Lerp(_low,_high,code);
                    result=still?Math.Min(result,level):Math.Max(result,level);
                }
                return result;
            }
        }
    }

    /// <summary>Per-camera, enabled-only storage. Raw texture backing is the upload buffer: no second copy.</summary>
    internal sealed class FoamTransportField : IDisposable
    {
        internal readonly int Resolution, FlowResolution;
        internal readonly Texture2D Velocity, Mask;
        internal RTHandle VelocityHandle { get; private set; }
        internal RTHandle MaskHandle { get; private set; }
        internal Vector2 FlowOrigin { get; private set; }
        internal Vector2 UniformVelocity { get; private set; }
        internal float MaxComponent { get; private set; }
        internal int Occupied { get; private set; }
        internal FoamTransport.Route Route { get; private set; }
        internal FoamTransport.Fault Fault { get; private set; }
        internal int RebuiltCells { get; private set; }
        internal int ContactCount { get; private set; }
        // A cell is dry iff sea <= bed AND bed >= still. One threshold is equivalent to
        // the previous two bounds, leaving room within the original budget for dirty caches.
        private readonly float[] _dryAt;
        private NativeArray<byte> _scroll;
        private const int TileSize = (int)(FoamTransport.FlowCellMeters * FoamBuffer.CellsPerUnit);
        private readonly int _tiles;
        private readonly float[] _tileMin, _tileMax;
        private readonly bool[] _terrainDirty, _maskDirty;
        private readonly FoamTransportVelocity _velocity;
        private readonly FoamTransportContacts.Shape[] _contacts = new FoamTransportContacts.Shape[FoamTransport.MaxContacts];
        private readonly FoamTransportContacts.Shape[] _previousContacts = new FoamTransportContacts.Shape[FoamTransport.MaxContacts];
        private int _previousCount;
        private FoamTransportMap _cachedBed, _cachedStill;
        private Vector2 _cachedOrigin, _cachedDrift;
        private Vector3 _cachedStrengths;
        private PackedWaveField _cachedWaves;
        private float _cachedSea, _cachedScale;
        private int _ringX, _ringY;
        private bool _cacheValid, _preparedValid, _uniform;
        private bool _maskPending = true, _velocityPending = true;
        private FilterMode _bedFilter, _stillFilter;
        internal int RebuiltMaskCells { get; private set; }
        internal int RebuiltVelocityNodes { get; private set; }
        internal int UploadedTextures { get; private set; }
        internal int UploadedBytes { get; private set; }

        internal FoamTransportField(int resolution, float extent)
        {
            Resolution=resolution; FlowResolution=FoamTransport.FlowResolution(extent);
            Velocity=new Texture2D(FlowResolution,FlowResolution,TextureFormat.RGFloat,false,true)
                { name="HH Foam transport velocity", hideFlags=HideFlags.HideAndDontSave, filterMode=FilterMode.Point, wrapMode=TextureWrapMode.Clamp };
            Mask=new Texture2D(resolution,resolution,TextureFormat.R8,false,true)
                { name="HH Foam transport solids", hideFlags=HideFlags.HideAndDontSave, filterMode=FilterMode.Point, wrapMode=TextureWrapMode.Clamp };
            var initialMask=Mask.GetRawTextureData<byte>();
            for(int i=0;i<initialMask.Length;i++) initialMask[i]=0;
            _dryAt=new float[resolution*resolution];
            _scroll=new NativeArray<byte>(resolution*resolution,Allocator.Persistent);
            _tiles=(resolution+TileSize-1)/TileSize;
            _tileMin=new float[_tiles*_tiles]; _tileMax=new float[_tiles*_tiles];
            _terrainDirty=new bool[_tiles*_tiles]; _maskDirty=new bool[_tiles*_tiles];
            _velocity=new FoamTransportVelocity(FlowResolution);
        }

        internal void Prepare(Vector2 drawOrigin, float seaLevel, Vector2 drift, in PackedWaveField waves,
                              float scale, Vector3 strengths, in FoamTransportMap bedInput, in FoamTransportMap stillInput,
                              FoamTransportContacts.Shape[] suppliedContacts=null, int suppliedCount=0)
        {
            Fault=FoamTransport.Fault.None; Route=FoamTransport.Route.Hold; RebuiltCells=0; ContactCount=0;
            RebuiltMaskCells=0; RebuiltVelocityNodes=0; UploadedTextures=0; UploadedBytes=0;
            if(!FoamTransport.Finite(drawOrigin) || !FoamTransport.Finite(seaLevel) || !FoamTransport.Finite(drift) ||
                !FoamTransport.Finite(strengths) || !FoamTransport.Finite(scale) || scale<=0 ||
                waves.Count<0 || waves.Count>PackedWaveField.MaxTrains)
            { Fail(FoamTransport.Fault.Inputs); return; }
            int contactCount;
            var window=new Rect(drawOrigin,Vector2.one*(Resolution*FoamBuffer.CellSize));
            if(suppliedContacts==null)
            {
                if(!FoamTransportContacts.Capture(_contacts,window,seaLevel,out contactCount,out var fault))
                { ContactCount=contactCount; Fail(fault); return; }
            }
            else
            {
                if(suppliedCount<0 || suppliedCount>suppliedContacts.Length || suppliedCount>_contacts.Length)
                { ContactCount=suppliedCount; Fail(FoamTransport.Fault.Capacity); return; }
                contactCount=0;
                for(int i=0;i<suppliedCount;i++)
                {
                    if(!suppliedContacts[i].Valid) { Fail(FoamTransport.Fault.Contacts); return; }
                    if(suppliedContacts[i].Wet(seaLevel)) _contacts[contactCount++]=suppliedContacts[i];
                }
            }
            ContactCount=contactCount;
            FoamTransportMap bed=bedInput.Capture(), still=stillInput.Capture();
            if(!bed.TryReader(out var bedReader) || !still.TryReader(out var stillReader))
            { Fail(FoamTransport.Fault.Map); return; }

            bool contactsChanged=!_preparedValid || contactCount!=_previousCount;
            for(int i=0;i<contactCount && !contactsChanged;i++)
                contactsChanged=!SameContact(_contacts[i],_previousContacts[i]);

            Vector2 delta=(drawOrigin-_cachedOrigin)*FoamBuffer.CellsPerUnit;
            bool reuse=_cacheValid && bed.Same(_cachedBed) && still.Same(_cachedStill) &&
                (!bed.Bound || bed.Texture.filterMode==_bedFilter) &&
                (!still.Bound || still.Texture.filterMode==_stillFilter) &&
                delta.x==Mathf.Round(delta.x) && delta.y==Mathf.Round(delta.y) &&
                Mathf.Abs(delta.x)<Resolution && Mathf.Abs(delta.y)<Resolution;
            int dx=reuse?(int)delta.x:0, dy=reuse?(int)delta.y:0;
            _ringX=reuse?Mod(_ringX+dx,Resolution):0; _ringY=reuse?Mod(_ringY+dy,Resolution):0;
            var mask=Mask.GetRawTextureData<byte>();
            if(!reuse)
            {
                RebuildTerrain(0,0,Resolution,Resolution,drawOrigin,bedReader,stillReader);
                MarkMask(0,0,Resolution,Resolution);
            }
            else if(dx!=0 || dy!=0)
            {
                ScrollMask(mask,dx,dy);
                int x0=Math.Max(0,-dx), x1=Math.Min(Resolution,Resolution-dx);
                int y0=Math.Max(0,-dy), y1=Math.Min(Resolution,Resolution-dy);
                RebuildTerrain(0,0,Resolution,y0,drawOrigin,bedReader,stillReader);
                RebuildTerrain(0,y1,Resolution,Resolution,drawOrigin,bedReader,stillReader);
                RebuildTerrain(0,y0,x0,y1,drawOrigin,bedReader,stillReader);
                RebuildTerrain(x1,y0,Resolution,y1,drawOrigin,bedReader,stillReader);
                MarkMask(0,0,Resolution,y0); MarkMask(0,y1,Resolution,Resolution);
                MarkMask(0,y0,x0,y1); MarkMask(x1,y0,Resolution,y1);
            }
            RefreshTerrainTiles();
            if(reuse && seaLevel!=_cachedSea) MarkTideChanges(seaLevel);
            if(contactsChanged || !reuse)
            {
                for(int i=0;i<Math.Max(_previousCount,contactCount);i++)
                {
                    if(reuse && i<_previousCount && i<contactCount && SameContact(_previousContacts[i],_contacts[i])) continue;
                    if(i<_previousCount) MarkContact(_previousContacts[i],drawOrigin);
                    if(i<contactCount) MarkContact(_contacts[i],drawOrigin);
                }
            }
            RebuildMask(mask,drawOrigin,seaLevel,contactCount);
            _cachedBed=bed; _cachedStill=still; _cachedOrigin=drawOrigin; _cacheValid=true;
            _bedFilter=bed.Bound?bed.Texture.filterMode:FilterMode.Point;
            _stillFilter=still.Bound?still.Texture.filterMode:FilterMode.Point;

            Vector2 flowOrigin=new Vector2(Mathf.Floor(drawOrigin.x/FoamTransport.FlowCellMeters)-1,
                                          Mathf.Floor(drawOrigin.y/FoamTransport.FlowCellMeters)-1)*FoamTransport.FlowCellMeters;
            bool velocityChanged=!_preparedValid || !flowOrigin.Equals(FlowOrigin) ||
                !_cachedDrift.Equals(drift) || !_cachedStrengths.Equals(strengths) || _cachedScale!=scale ||
                !FoamTransportVelocity.SameWaves(_cachedWaves,waves) || (contactsChanged && strengths.z>0);
            FlowOrigin=flowOrigin;
            if(velocityChanged)
            {
                if(!_velocity.Build(Velocity.GetRawTextureData<Vector2>(),FlowOrigin,drift,waves,scale,strengths,
                    _contacts,contactCount,contactsChanged,out var uniformVelocity,out var maximum,out _uniform))
                { Fail(FoamTransport.Fault.Inputs); return; }
                UniformVelocity=uniformVelocity; MaxComponent=maximum;
                RebuiltVelocityNodes=FlowResolution*FlowResolution; _velocityPending=true;
            }
            Array.Copy(_contacts,_previousContacts,contactCount); _previousCount=contactCount;
            _cachedSea=seaLevel; _cachedDrift=drift; _cachedStrengths=strengths;
            _cachedWaves=waves; _cachedScale=scale; _preparedValid=true;
            Route=FoamTransport.Classify(strengths,true,true,_uniform,Occupied);
        }

        private static bool SameContact(in FoamTransportContacts.Shape a, in FoamTransportContacts.Shape b) =>
            a.Center.Equals(b.Center) && a.Axis.Equals(b.Axis) && a.HalfSize.Equals(b.HalfSize) &&
            a.MinLevel==b.MinLevel && a.MaxLevel==b.MaxLevel && a.Circle==b.Circle && a.Key==b.Key;

        private void RebuildTerrain(int x0,int y0,int x1,int y1,Vector2 origin,
            FoamTransportMap.Reader bed,FoamTransportMap.Reader still)
        {
            for(int y=y0;y<y1;y++) for(int x=x0;x<x1;x++)
            {
                int rx=Mod(x+_ringX,Resolution), ry=Mod(y+_ringY,Resolution);
                Vector2 corner=origin+new Vector2(x,y)*FoamBuffer.CellSize;
                float height=bed.BoundOverCell(corner,FoamBuffer.CellSize,false);
                float water=still.BoundOverCell(corner,FoamBuffer.CellSize,true);
                _dryAt[ry*Resolution+rx]=height>=water?height:float.NegativeInfinity;
                _terrainDirty[(ry/TileSize)*_tiles+rx/TileSize]=true;
                RebuiltCells++;
            }
        }

        private void RefreshTerrainTiles()
        {
            for(int t=0;t<_terrainDirty.Length;t++)
            {
                if(!_terrainDirty[t]) continue;
                _terrainDirty[t]=false;
                int x0=(t%_tiles)*TileSize,y0=(t/_tiles)*TileSize;
                int x1=Math.Min(Resolution,x0+TileSize),y1=Math.Min(Resolution,y0+TileSize);
                float low=float.PositiveInfinity,high=float.NegativeInfinity;
                for(int y=y0;y<y1;y++) for(int x=x0;x<x1;x++)
                {
                    float value=_dryAt[y*Resolution+x];
                    if(float.IsNegativeInfinity(value)) continue;
                    low=Math.Min(low,value); high=Math.Max(high,value);
                }
                _tileMin[t]=low; _tileMax[t]=high;
            }
        }

        private void MarkTideChanges(float sea)
        {
            float low=Math.Min(sea,_cachedSea),high=Math.Max(sea,_cachedSea);
            for(int t=0;t<_tileMin.Length;t++)
            {
                if(_tileMax[t]<low || _tileMin[t]>=high) continue;
                int x0=t%_tiles*TileSize,y0=t/_tiles*TileSize;
                int x1=Math.Min(Resolution,x0+TileSize),y1=Math.Min(Resolution,y0+TileSize);
                for(int y=y0;y<y1;y++) for(int x=x0;x<x1;x++)
                {
                    float threshold=_dryAt[y*Resolution+x];
                    if((threshold>=sea)==(threshold>=_cachedSea)) continue;
                    int sx=Mod(x-_ringX,Resolution),sy=Mod(y-_ringY,Resolution);
                    _maskDirty[sy/TileSize*_tiles+sx/TileSize]=true;
                }
            }
        }

        private void MarkMask(int x0,int y0,int x1,int y1)
        {
            x0=Math.Max(0,x0); y0=Math.Max(0,y0); x1=Math.Min(Resolution,x1); y1=Math.Min(Resolution,y1);
            if(x1<=x0 || y1<=y0) return;
            for(int y=y0/TileSize;y<=(y1-1)/TileSize;y++)
                for(int x=x0/TileSize;x<=(x1-1)/TileSize;x++) _maskDirty[y*_tiles+x]=true;
        }

        private void MarkContact(in FoamTransportContacts.Shape shape,Vector2 origin)
        {
            ContactBounds(shape,origin,out int x0,out int y0,out int x1,out int y1);
            MarkMask(x0,y0,x1,y1);
        }

        private static void ContactBounds(in FoamTransportContacts.Shape shape,Vector2 origin,
            out int x0,out int y0,out int x1,out int y1)
        {
            Vector2 half=shape.AabbHalf;
            Vector2 lo=(shape.Center-half-origin)*FoamBuffer.CellsPerUnit;
            Vector2 hi=(shape.Center+half-origin)*FoamBuffer.CellsPerUnit;
            x0=Mathf.CeilToInt(lo.x)-1; y0=Mathf.CeilToInt(lo.y)-1;
            x1=Mathf.FloorToInt(hi.x)+1; y1=Mathf.FloorToInt(hi.y)+1;
        }

        private void ScrollMask(NativeArray<byte> mask,int dx,int dy)
        {
            NativeArray<byte>.Copy(mask,_scroll);
            // Contiguous native row copies retain all overlap. Only exiting/entering strips are
            // visited individually; the scratch is allocated once, never during preparation.
            int x0=Math.Max(0,-dx),x1=Math.Min(Resolution,Resolution-dx);
            int y0=Math.Max(0,-dy),y1=Math.Min(Resolution,Resolution-dy);
            for(int y=0;y<Resolution;y++)
            {
                int start=(y>=y0 && y<y1)?x0:Resolution;
                int end=(y>=y0 && y<y1)?x1:Resolution;
                for(int x=0;x<start;x++) mask[y*Resolution+x]=0;
                for(int x=end;x<Resolution;x++) mask[y*Resolution+x]=0;
                if(end>start) NativeArray<byte>.Copy(_scroll,(y+dy)*Resolution+x0+dx,mask,y*Resolution+x0,x1-x0);
                // Count the old cells no longer represented in the shifted window.
                int oldStart=(y>=y0+dy && y<y1+dy)?x0+dx:Resolution;
                int oldEnd=(y>=y0+dy && y<y1+dy)?x1+dx:Resolution;
                for(int x=0;x<oldStart;x++) if(_scroll[y*Resolution+x]!=0) Occupied--;
                for(int x=oldEnd;x<Resolution;x++) if(_scroll[y*Resolution+x]!=0) Occupied--;
            }
            _maskPending=true;
        }

        private void RebuildMask(NativeArray<byte> mask,Vector2 origin,float sea,int count)
        {
            for(int t=0;t<_maskDirty.Length;t++)
            {
                if(!_maskDirty[t]) continue;
                _maskDirty[t]=false;
                int x0=t%_tiles*TileSize,y0=t/_tiles*TileSize;
                int x1=Math.Min(Resolution,x0+TileSize),y1=Math.Min(Resolution,y0+TileSize);
                // Compose this small tile in scratch so unchanged final bytes need no upload.
                for(int y=y0;y<y1;y++) for(int x=x0;x<x1;x++)
                {
                    int cache=Mod(y+_ringY,Resolution)*Resolution+Mod(x+_ringX,Resolution);
                    _scroll[y*Resolution+x]=_dryAt[cache]>=sea?(byte)255:(byte)0;
                    RebuiltMaskCells++;
                }
                for(int c=0;c<count;c++)
                {
                    var shape=_contacts[c];
                    ContactBounds(shape,origin,out int left,out int bottom,out int right,out int top);
                    left=Math.Max(x0,left); bottom=Math.Max(y0,bottom); right=Math.Min(x1,right); top=Math.Min(y1,top);
                    for(int y=bottom;y<top;y++) for(int x=left;x<right;x++)
                    {
                        int i=y*Resolution+x;
                        if(_scroll[i]!=0) continue;
                        Vector2 center=origin+new Vector2(x+.5f,y+.5f)*FoamBuffer.CellSize;
                        if(shape.TouchesCell(center,FoamBuffer.CellSize*.5f)) _scroll[i]=255;
                    }
                }
                for(int y=y0;y<y1;y++) for(int x=x0;x<x1;x++)
                {
                    int i=y*Resolution+x; byte value=_scroll[i];
                    if(mask[i]==value) continue;
                    Occupied+=value!=0?1:-1; mask[i]=value; _maskPending=true;
                }
            }
        }

        private void Fail(FoamTransport.Fault fault)
        { Fault=fault; Route=FoamTransport.Route.Hold; MaxComponent=0; _preparedValid=false; _cacheValid=false; }
        private static int Mod(int value,int modulus) { int r=value%modulus; return r<0?r+modulus:r; }

        internal void Upload()
        {
            // Called once only when exchanges consume these textures, before the camera graph executes.
            UploadedTextures=0; UploadedBytes=0;
            if(_velocityPending)
            { Velocity.Apply(false,false); _velocityPending=false; UploadedTextures++; UploadedBytes+=8*FlowResolution*FlowResolution; }
            if(_maskPending)
            { Mask.Apply(false,false); _maskPending=false; UploadedTextures++; UploadedBytes+=Resolution*Resolution; }
            VelocityHandle ??= RTHandles.Alloc(Velocity);
            MaskHandle ??= RTHandles.Alloc(Mask);
        }

        public void Dispose()
        {
            VelocityHandle?.Release(); MaskHandle?.Release(); VelocityHandle=null; MaskHandle=null;
            _scroll.Dispose();
            CoreUtils.Destroy(Velocity); CoreUtils.Destroy(Mask);
        }
    }
}
