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
        private readonly float[] _maxBed, _minStill;
        private readonly FoamTransportContacts.Shape[] _contacts = new FoamTransportContacts.Shape[FoamTransport.MaxContacts];
        private FoamTransportMap _cachedBed, _cachedStill;
        private Vector2 _cachedOrigin;
        private int _ringX, _ringY;
        private bool _cacheValid;
        private FilterMode _bedFilter, _stillFilter;

        internal FoamTransportField(int resolution, float extent)
        {
            Resolution=resolution; FlowResolution=FoamTransport.FlowResolution(extent);
            Velocity=new Texture2D(FlowResolution,FlowResolution,TextureFormat.RGFloat,false,true)
                { name="HH Foam transport velocity", hideFlags=HideFlags.HideAndDontSave, filterMode=FilterMode.Point, wrapMode=TextureWrapMode.Clamp };
            Mask=new Texture2D(resolution,resolution,TextureFormat.R8,false,true)
                { name="HH Foam transport solids", hideFlags=HideFlags.HideAndDontSave, filterMode=FilterMode.Point, wrapMode=TextureWrapMode.Clamp };
            _maxBed=new float[resolution*resolution]; _minStill=new float[resolution*resolution];
        }

        internal void Prepare(Vector2 drawOrigin, float seaLevel, Vector2 drift, in PackedWaveField waves,
                              float scale, Vector3 strengths, in FoamTransportMap bedInput, in FoamTransportMap stillInput,
                              FoamTransportContacts.Shape[] suppliedContacts=null, int suppliedCount=0)
        {
            Fault=FoamTransport.Fault.None; Route=FoamTransport.Route.Hold; RebuiltCells=0; ContactCount=0;
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

            // Ring cache: retain overlapping world cells on a whole-cell camera pan. A changed residual
            // or source revision invalidates it; silently rounding a fractional offset would move shores.
            Vector2 delta=(drawOrigin-_cachedOrigin)*FoamBuffer.CellsPerUnit;
            bool reuse=_cacheValid && bed.Same(_cachedBed) && still.Same(_cachedStill) &&
                (!bed.Bound || bed.Texture.filterMode==_bedFilter) &&
                (!still.Bound || still.Texture.filterMode==_stillFilter) &&
                delta.x==Mathf.Round(delta.x) && delta.y==Mathf.Round(delta.y) &&
                Mathf.Abs(delta.x)<Resolution && Mathf.Abs(delta.y)<Resolution;
            int dx=reuse?(int)delta.x:0, dy=reuse?(int)delta.y:0;
            _ringX=reuse?Mod(_ringX+dx,Resolution):0; _ringY=reuse?Mod(_ringY+dy,Resolution):0;
            var mask=Mask.GetRawTextureData<byte>();
            Occupied=0;
            for(int y=0;y<Resolution;y++) for(int x=0;x<Resolution;x++)
            {
                int cache=Mod(y+_ringY,Resolution)*Resolution+Mod(x+_ringX,Resolution);
                if(!reuse || x+dx<0 || x+dx>=Resolution || y+dy<0 || y+dy>=Resolution)
                {
                    Vector2 corner=drawOrigin+new Vector2(x,y)*FoamBuffer.CellSize;
                    _maxBed[cache]=bedReader.BoundOverCell(corner,FoamBuffer.CellSize,false);
                    _minStill[cache]=stillReader.BoundOverCell(corner,FoamBuffer.CellSize,true);
                    RebuiltCells++;
                }
                bool blocked=_maxBed[cache]>=Math.Max(seaLevel,_minStill[cache]);
                mask[y*Resolution+x]=blocked?(byte)255:(byte)0;
                if(blocked) Occupied++;
            }
            _cachedBed=bed; _cachedStill=still; _cachedOrigin=drawOrigin; _cacheValid=true;
            _bedFilter=bed.Bound?bed.Texture.filterMode:FilterMode.Point;
            _stillFilter=still.Bound?still.Texture.filterMode:FilterMode.Point;
            for(int i=0;i<contactCount;i++)
            {
                var shape=_contacts[i]; Vector2 half=shape.AabbHalf;
                Vector2 lo=(shape.Center-half-drawOrigin)*FoamBuffer.CellsPerUnit;
                Vector2 hi=(shape.Center+half-drawOrigin)*FoamBuffer.CellsPerUnit;
                int x0=Math.Max(0,Mathf.CeilToInt(lo.x)-1), y0=Math.Max(0,Mathf.CeilToInt(lo.y)-1);
                int x1=Math.Min(Resolution-1,Mathf.FloorToInt(hi.x)), y1=Math.Min(Resolution-1,Mathf.FloorToInt(hi.y));
                for(int y=y0;y<=y1;y++) for(int x=x0;x<=x1;x++)
                {
                    int cell=y*Resolution+x;
                    if(mask[cell]!=0) continue;
                    Vector2 center=drawOrigin+new Vector2(x+.5f,y+.5f)*FoamBuffer.CellSize;
                    if(!shape.TouchesCell(center,FoamBuffer.CellSize*.5f)) continue;
                    mask[cell]=255; Occupied++;
                }
            }

            FlowOrigin=new Vector2(Mathf.Floor(drawOrigin.x/FoamTransport.FlowCellMeters)-1,
                                   Mathf.Floor(drawOrigin.y/FoamTransport.FlowCellMeters)-1)*FoamTransport.FlowCellMeters;
            var velocity=Velocity.GetRawTextureData<Vector2>();
            bool uniform=true; MaxComponent=0;
            for(int y=0;y<FlowResolution;y++) for(int x=0;x<FlowResolution;x++)
            {
                Vector2 point=FlowOrigin+new Vector2(x,y)*FoamTransport.FlowCellMeters;
                Vector2 v=drift+FoamTransport.WaveVelocity(waves,point,scale,strengths)+
                    FoamTransport.CollectionVelocity(point,drift,Math.Max(0,strengths.z),_contacts,contactCount);
                if(!FoamTransport.Finite(v)) { Fail(FoamTransport.Fault.Inputs); return; }
                int index=y*FlowResolution+x; velocity[index]=v;
                if(index==0) UniformVelocity=v;
                else if(v.x!=UniformVelocity.x || v.y!=UniformVelocity.y) uniform=false;
                MaxComponent=Math.Max(MaxComponent,Math.Max(Math.Abs(v.x),Math.Abs(v.y)));
            }
            Route=FoamTransport.Classify(strengths,true,true,uniform,Occupied);
        }

        private void Fail(FoamTransport.Fault fault)
        { Fault=fault; Route=FoamTransport.Route.Hold; MaxComponent=0; }
        private static int Mod(int value,int modulus) { int r=value%modulus; return r<0?r+modulus:r; }

        internal void Upload()
        {
            // Called once only when exchanges consume these textures, before the camera graph executes.
            Velocity.Apply(false,false); Mask.Apply(false,false);
            VelocityHandle ??= RTHandles.Alloc(Velocity);
            MaskHandle ??= RTHandles.Alloc(Mask);
        }

        public void Dispose()
        {
            VelocityHandle?.Release(); MaskHandle?.Release(); VelocityHandle=null; MaskHandle=null;
            CoreUtils.Destroy(Velocity); CoreUtils.Destroy(Mask);
        }
    }
}
