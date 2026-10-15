using System;
using Unity.Collections;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>Same F2 law, factored over a regular grid. No cadence, phase integration or clock.</summary>
    internal sealed class FoamTransportVelocity
    {
        private readonly int _size;
        private readonly Vector2[] _collection, _output;
        private readonly float[] _slopeX, _slopeY;
        private readonly double[] _cosX, _sinX, _cosY, _sinY;
        private readonly float[] _weights = new float[PackedWaveField.MaxTrains];
        private float _normalizer;
        private readonly float[] _best;
        private readonly int[] _winner;
        private PackedWaveField _waves;
        private Vector2 _origin, _drift;
        private float _scale, _strength;
        private bool _valid;

        internal FoamTransportVelocity(int size)
        {
            _size=size;
            _slopeX=new float[size*size]; _slopeY=new float[size*size];
            _collection=new Vector2[size*size];
            _output=new Vector2[size*size];
            _cosX=new double[size*PackedWaveField.MaxTrains]; _sinX=new double[_cosX.Length];
            _cosY=new double[_cosX.Length]; _sinY=new double[_cosX.Length];
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
            out Vector2 uniformVelocity, out float maximum, out bool uniform,
            Action<FoamTransportPreparationStage> stageCompleted=null)
        {
            bool moved=!_valid || !origin.Equals(_origin);
            if(moved || scale!=_scale || !SameWaves(waves,_waves)) BuildSlope(origin,waves,scale,stageCompleted);
            else
            {
                stageCompleted?.Invoke(FoamTransportPreparationStage.WaveFactors);
                stageCompleted?.Invoke(FoamTransportPreparationStage.WaveAccumulation);
            }
            if(moved || contactsChanged || !drift.Equals(_drift) || strengths.z!=_strength)
                BuildCollection(origin,drift,Math.Max(0,strengths.z),contacts,count);
            stageCompleted?.Invoke(FoamTransportPreparationStage.Collection);
            float strain=Math.Max(0,strengths.x), curl=Math.Max(0,strengths.y);
            uniformVelocity=default; maximum=0; uniform=true;
            for(int i=0;i<_output.Length;i++)
            {
                // Mono can retain extra precision in float locals/expressions. Phase A rounded
                // normalization into a Vector2 field, and every operator returned another Vector2.
                // Explicit single conversions preserve each of those storage boundaries without
                // restoring a separate normalization pass or per-node NativeArray writes.
                float sx=(float)(_slopeX[i]/_normalizer), sy=(float)(_slopeY[i]/_normalizer);
                Vector2 collection=_collection[i];
                float vx=(float)((float)(drift.x+(float)((float)(strain*sx)+(float)(curl*(-sy))))+collection.x);
                float vy=(float)((float)(drift.y+(float)((float)(strain*sy)+(float)(curl*sx)))+collection.y);
                float ax=vx<0?-vx:vx,ay=vy<0?-vy:vy;
                // Ordered comparisons of magnitudes reject NaN and either infinity without
                // four runtime IsNaN/IsInfinity calls per node on Mono. Endpoints are inclusive.
                if(!(ax<=float.MaxValue && ay<=float.MaxValue))
                { _valid=false; return false; }
                _output[i]=new Vector2(vx,vy);
                if(i==0) uniformVelocity=new Vector2(vx,vy);
                else if(uniform && (vx!=uniformVelocity.x || vy!=uniformVelocity.y)) uniform=false;
                if(ax>maximum)maximum=ax;
                if(ay>maximum)maximum=ay;
            }
            stageCompleted?.Invoke(FoamTransportPreparationStage.VelocityCompose);
            // Validate/copy once, instead of 9,801 NativeArray indexer safety checks in the editor.
            // The managed staging array is owned for this field's lifetime, not allocated per frame.
            NativeArray<Vector2>.Copy(_output,output);
            stageCompleted?.Invoke(FoamTransportPreparationStage.VelocityCopy);
            _origin=origin; _drift=drift; _waves=waves; _scale=scale; _strength=strengths.z; _valid=true;
            return true;
        }

        private void BuildSlope(Vector2 origin, in PackedWaveField waves, float scale,
            Action<FoamTransportPreparationStage> stageCompleted)
        {
            float norm=0;
            for(int trainIndex=0;trainIndex<waves.Count;trainIndex++)
            {
                Vector4 train=waves.Train(trainIndex);
                float weight=train.w*train.z*scale;
                _weights[trainIndex]=weight;
                norm+=Math.Abs(weight);
                // Evaluate world coordinates directly, never advance last frame's phase. Double
                // angle addition replaces N*N cosines by 2*N sin/cos pairs per train. Rounding
                // differs from the scalar oracle by a few float ulps; the field tests bound it.
                double frequency=(double)train.z*scale;
                int offset=trainIndex*_size;
                float publishedPhase=waves.Phase(trainIndex);
                for(int x=0;x<_size;x++)
                {
                    double phase=frequency*train.x*(origin.x+x*FoamTransport.FlowCellMeters)+publishedPhase;
                    phase-=Math.Floor(phase/(2*Math.PI))*(2*Math.PI);
                    _cosX[offset+x]=Math.Cos(phase); _sinX[offset+x]=Math.Sin(phase);
                }
                // Published phase occurs ONLY in X above. Y is unchanged when time advances.
                // Cache by the exact spatial inputs, never by accumulated phase or elapsed time.
                Vector4 oldTrain=_waves.Train(trainIndex);
                if(!_valid || trainIndex>=_waves.Count || origin.y!=_origin.y || scale!=_scale ||
                    train.y!=oldTrain.y || train.z!=oldTrain.z)
                {
                    for(int y=0;y<_size;y++)
                    {
                        double phase=frequency*train.y*(origin.y+y*FoamTransport.FlowCellMeters);
                        phase-=Math.Floor(phase/(2*Math.PI))*(2*Math.PI);
                        _cosY[offset+y]=Math.Cos(phase); _sinY[offset+y]=Math.Sin(phase);
                    }
                }
            }
            _normalizer=Math.Max(1,norm);
            stageCompleted?.Invoke(FoamTransportPreparationStage.WaveFactors);
            Array.Clear(_slopeX,0,_slopeX.Length); Array.Clear(_slopeY,0,_slopeY.Length);
            // Train order and every multiply/add/cast remain identical to the Phase A evaluator.
            float[] slopeX=_slopeX, slopeY=_slopeY;
            double[] cosX=_cosX,sinX=_sinX;
            for(int trainIndex=0;trainIndex<waves.Count;trainIndex++)
            {
                Vector4 train=waves.Train(trainIndex); float weight=_weights[trainIndex];
                float tx=train.x,ty=train.y;
                int offset=trainIndex*_size;
                for(int y=0;y<_size;y++)
                {
                    double cy=_cosY[offset+y], sy=_sinY[offset+y];
                    int row=y*_size;
                    for(int x=0;x<_size;x++)
                    {
                        int axis=offset+x;
                        float value=weight*(float)(cosX[axis]*cy-sinX[axis]*sy);
                        int i=row+x;
                        slopeX[i]+=tx*value; slopeY[i]+=ty*value;
                    }
                }
            }
            stageCompleted?.Invoke(FoamTransportPreparationStage.WaveAccumulation);
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
