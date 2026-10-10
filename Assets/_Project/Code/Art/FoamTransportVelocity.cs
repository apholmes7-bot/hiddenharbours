using System;
using Unity.Collections;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>Same F2 law, factored over a regular grid. No cadence, phase integration or clock.</summary>
    internal sealed class FoamTransportVelocity
    {
        private readonly int _size;
        private readonly Vector2[] _slope, _collection;
        private readonly double[] _cosX, _sinX;
        private readonly float[] _best;
        private readonly int[] _winner;
        private PackedWaveField _waves;
        private Vector2 _origin, _drift;
        private float _scale, _strength;
        private bool _valid;

        internal FoamTransportVelocity(int size)
        {
            _size=size;
            _slope=new Vector2[size*size]; _collection=new Vector2[size*size];
            _cosX=new double[size]; _sinX=new double[size];
            _best=new float[size*size]; _winner=new int[size*size];
        }

        internal static bool SameWaves(in PackedWaveField a, in PackedWaveField b)
        {
            if(a.Count!=b.Count) return false;
            for(int i=0;i<a.Count;i++)
                if(!a.Train(i).Equals(b.Train(i)) || a.Phase(i)!=b.Phase(i)) return false;
            return true;
        }

        internal bool Build(NativeArray<Vector2> output, Vector2 origin, Vector2 drift,
            in PackedWaveField waves, float scale, Vector3 strengths,
            FoamTransportContacts.Shape[] contacts, int count, bool contactsChanged,
            out Vector2 uniformVelocity, out float maximum, out bool uniform)
        {
            bool moved=!_valid || !origin.Equals(_origin);
            if(moved || scale!=_scale || !SameWaves(waves,_waves)) BuildSlope(origin,waves,scale);
            if(moved || contactsChanged || !drift.Equals(_drift) || strengths.z!=_strength)
                BuildCollection(origin,drift,Math.Max(0,strengths.z),contacts,count);
            float strain=Math.Max(0,strengths.x), curl=Math.Max(0,strengths.y);
            uniformVelocity=default; maximum=0; uniform=true;
            for(int i=0;i<output.Length;i++)
            {
                Vector2 slope=_slope[i];
                Vector2 v=drift+(strain*slope+curl*new Vector2(-slope.y,slope.x))+_collection[i];
                if(!FoamTransport.Finite(v)) { _valid=false; return false; }
                output[i]=v;
                if(i==0) uniformVelocity=v;
                else if(!v.Equals(uniformVelocity)) uniform=false;
                maximum=Math.Max(maximum,Math.Max(Math.Abs(v.x),Math.Abs(v.y)));
            }
            _origin=origin; _drift=drift; _waves=waves; _scale=scale; _strength=strengths.z; _valid=true;
            return true;
        }

        private void BuildSlope(Vector2 origin, in PackedWaveField waves, float scale)
        {
            Array.Clear(_slope,0,_slope.Length);
            float norm=0;
            for(int trainIndex=0;trainIndex<waves.Count;trainIndex++)
            {
                Vector4 train=waves.Train(trainIndex);
                float weight=train.w*train.z*scale;
                norm+=Math.Abs(weight);
                // Evaluate world coordinates directly, never advance last frame's phase. Double
                // angle addition replaces N*N cosines by 2*N sin/cos pairs per train. Rounding
                // differs from the scalar oracle by a few float ulps; the field tests bound it.
                double frequency=(double)train.z*scale;
                for(int x=0;x<_size;x++)
                {
                    double phase=frequency*train.x*(origin.x+x*FoamTransport.FlowCellMeters)+waves.Phase(trainIndex);
                    phase-=Math.Floor(phase/(2*Math.PI))*(2*Math.PI);
                    _cosX[x]=Math.Cos(phase); _sinX[x]=Math.Sin(phase);
                }
                for(int y=0;y<_size;y++)
                {
                    double phase=frequency*train.y*(origin.y+y*FoamTransport.FlowCellMeters);
                    phase-=Math.Floor(phase/(2*Math.PI))*(2*Math.PI);
                    double cy=Math.Cos(phase), sy=Math.Sin(phase);
                    for(int x=0;x<_size;x++)
                    {
                        float value=weight*(float)(_cosX[x]*cy-_sinX[x]*sy);
                        int i=y*_size+x;
                        _slope[i].x+=train.x*value; _slope[i].y+=train.y*value;
                    }
                }
            }
            norm=Math.Max(1,norm);
            for(int i=0;i<_slope.Length;i++) { _slope[i].x/=norm; _slope[i].y/=norm; }
        }

        private void BuildCollection(Vector2 origin, Vector2 drift, float strength,
            FoamTransportContacts.Shape[] contacts, int count)
        {
            Array.Clear(_collection,0,_collection.Length);
            if(strength<=0 || drift.sqrMagnitude==0 || count==0) return;
            Array.Clear(_best,0,_best.Length);
            Vector2 e=drift.normalized, side=new Vector2(-e.y,e.x);
            for(int c=0;c<count;c++)
            {
                var contact=contacts[c]; float radius=Math.Max(FoamTransport.FlowCellMeters,contact.Radius);
                Vector2 center=contact.Center+e*(2*radius);
                Vector2 half=new Vector2(Math.Abs(e.x)+Math.Abs(side.x),Math.Abs(e.y)+Math.Abs(side.y))*(2*radius);
                // One extra node is conservative against float rounding at support boundaries.
                int x0=Math.Max(0,Mathf.FloorToInt((center.x-half.x-origin.x)/FoamTransport.FlowCellMeters)-1);
                int y0=Math.Max(0,Mathf.FloorToInt((center.y-half.y-origin.y)/FoamTransport.FlowCellMeters)-1);
                int x1=Math.Min(_size-1,Mathf.CeilToInt((center.x+half.x-origin.x)/FoamTransport.FlowCellMeters)+1);
                int y1=Math.Min(_size-1,Mathf.CeilToInt((center.y+half.y-origin.y)/FoamTransport.FlowCellMeters)+1);
                for(int y=y0;y<=y1;y++) for(int x=x0;x<=x1;x++)
                {
                    Vector2 point=origin+new Vector2(x,y)*FoamTransport.FlowCellMeters;
                    Vector2 d=point-contact.Center;
                    float down=Vector2.Dot(d,e), across=Vector2.Dot(d,side);
                    float lee=Math.Max(0,1-Math.Abs(down-2*radius)/(2*radius))*Math.Max(0,1-Math.Abs(across)/(2*radius));
                    int i=y*_size+x;
                    if(lee<=0 || lee<_best[i] || (lee==_best[i] &&
                        FoamTransportContacts.Compare(contact,contacts[_winner[i]])>=0)) continue;
                    _best[i]=lee; _winner[i]=c;
                    _collection[i]=-strength*lee*(e+across/(2*radius)*side)*.5f;
                }
            }
        }
    }
}
