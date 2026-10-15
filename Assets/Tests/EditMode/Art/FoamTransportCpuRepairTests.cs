using System;
using HiddenHarbours.Art;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public sealed class FoamTransportCpuRepairTests
    {
        [Test] public void CpuRepair_AllocationCounterDetectsKnownAllocation()
        {
            using var probe=new FoamTransportAllocationProbe();
            probe.RequirePositiveControl();
        }

        static readonly FoamTransportContacts.Shape[] Empty=Array.Empty<FoamTransportContacts.Shape>();
        static void Exact(float expected,float actual,int x,int y,string component)
        {
            int eb=BitConverter.SingleToInt32Bits(expected),ab=BitConverter.SingleToInt32Bits(actual);
            if(eb!=ab)Assert.AreEqual(eb,ab,$"node ({x},{y}) {component}: expected {expected:R} [0x{eb:X8}], actual {actual:R} [0x{ab:X8}]");
        }
        static PackedWaveField Waves(float phase,int count=8,float waveNumber=1.3f) => new PackedWaveField(
            new Vector4(.6f,.8f,waveNumber,.7f),new Vector4(-.8f,.6f,.3f,.2f),
            new Vector4(1,0,2,.1f),new Vector4(0,1,.5f,.2f),new Vector4(.8f,-.6f,.8f,.1f),
            new Vector4(-1,0,.2f,.7f),new Vector4(0,-1,.9f,.3f),new Vector4(-.6f,-.8f,1.1f,.2f),
            new Vector4(phase,phase*.7f,phase*.3f,phase*.4f),new Vector4(phase*.9f,phase*.2f,phase*.8f,phase*.6f),
            new Vector4(count,1,1,0));

        // Arithmetic compatibility oracle from 3a7e30ce: recompute each node's factored angles
        // directly, without the new axis caches, split arrays, normalization fusion or bulk copy.
        static Vector2 PhaseANode(Vector2 origin,int x,int y,in PackedWaveField waves,float scale,
            Vector2 drift,Vector3 strengths)
        {
            Vector2 slope=default;float norm=0;
            for(int t=0;t<waves.Count;t++)
            {
                var train=waves.Train(t);float weight=train.w*train.z*scale;norm+=Math.Abs(weight);
                double frequency=(double)train.z*scale;
                double px=frequency*train.x*(origin.x+x*FoamTransport.FlowCellMeters)+waves.Phase(t);
                double py=frequency*train.y*(origin.y+y*FoamTransport.FlowCellMeters);
                px-=Math.Floor(px/(2*Math.PI))*(2*Math.PI);py-=Math.Floor(py/(2*Math.PI))*(2*Math.PI);
                float value=weight*(float)(Math.Cos(px)*Math.Cos(py)-Math.Sin(px)*Math.Sin(py));
                slope.x+=train.x*value;slope.y+=train.y*value;
            }
            norm=Math.Max(1,norm);slope.x/=norm;slope.y/=norm;
            return drift+(Math.Max(0,strengths.x)*slope+Math.Max(0,strengths.y)*new Vector2(-slope.y,slope.x))+Vector2.zero;
        }

        [TestCase(0)] [TestCase(1)] [TestCase(4)] [TestCase(8)]
        public void CpuRepair_AdvancingPhasesPreservePhaseAFieldExactly(int trains)
        {
            const int n=99;
            var velocity=new FoamTransportVelocity(n);
            using var output=new NativeArray<Vector2>(n*n,Allocator.Persistent);
            var origin=new Vector2(79,-120);var drift=new Vector2(.4f,-.2f);var strengths=new Vector3(.25f,.5f,0);
            foreach(float phase in new[]{0f,.05f,1.7f,6.2f,-.4f})
            {
                var waves=Waves(phase,trains);
                Assert.AreEqual(true,velocity.Build(output,origin,drift,waves,.85f,strengths,Empty,0,false,out var first,out float maximum,out bool uniform));
                float expectedMax=0;bool expectedUniform=true;
                for(int y=0;y<n;y++)for(int x=0;x<n;x++)
                {
                    var expected=PhaseANode(origin,x,y,waves,.85f,drift,strengths);
                    var actual=output[y*n+x];
                    Exact(expected.x,actual.x,x,y,"x");
                    Exact(expected.y,actual.y,x,y,"y");
                    if(!expected.Equals(first))expectedUniform=false;
                    expectedMax=Math.Max(expectedMax,Math.Max(Math.Abs(expected.x),Math.Abs(expected.y)));
                }
                Assert.AreEqual(expectedMax,maximum);Assert.AreEqual(expectedUniform,uniform);
            }
        }

        [Test] public void CpuRepair_SpatialAndSpectrumChangesInvalidateAxisCache()
        {
            const int n=7;
            var velocity=new FoamTransportVelocity(n);
            using var output=new NativeArray<Vector2>(n*n,Allocator.Persistent);
            for(int step=0;step<24;step++)
            {
                var origin=new Vector2(step%3-2,step%5-4);float scale=.7f+step%4*.1f;
                var waves=Waves(step*.3f,step%9,.5f+step%3);
                var strengths=new Vector3(step%3*.2f,step%5*.3f,0);
                Assert.AreEqual(true,velocity.Build(output,origin,Vector2.right,waves,scale,strengths,Empty,0,false,out _,out _,out _));
                for(int y=0;y<n;y++)for(int x=0;x<n;x++)
                {
                    var expected=PhaseANode(origin,x,y,waves,scale,Vector2.right,strengths);var actual=output[y*n+x];
                    Exact(expected.x,actual.x,x,y,"x");Exact(expected.y,actual.y,x,y,"y");
                }
            }
        }

        [Test] public void CpuRepair_FieldIsIndependentOfPhaseVisitOrder()
        {
            using var visited=new FoamTransportField(32,4);using var direct=new FoamTransportField(32,4);
            foreach(float phase in new[]{10f,-1f,2f,0f,6f})
                visited.Prepare(new Vector2(-1,2),1,Vector2.right,Waves(phase),.9f,Vector3.one,default,default,Empty);
            direct.Prepare(new Vector2(-1,2),1,Vector2.right,Waves(6),.9f,Vector3.one,default,default,Empty);
            CollectionAssert.AreEqual(direct.Velocity.GetRawTextureData<Vector2>().ToArray(),visited.Velocity.GetRawTextureData<Vector2>().ToArray());
            Assert.AreEqual(direct.MaxComponent,visited.MaxComponent);
        }

        [Test] public void CpuRepair_SailingAtThreeMetresPerSecondMatchesFreshField()
        {
            using var cached=new FoamTransportField(32,4);
            var contacts=new[]{new FoamTransportContacts.Shape(Vector2.zero,Vector2.right,new Vector2(.2f,.7f),-2,2,false,1)};
            Vector2 previous=default;
            for(int frame=0;frame<80;frame++)
            {
                // 3 m/s (5.83 knots), 60 Hz; positive and negative diagonal ring traversal.
                var center=new Vector2(frame*3f/60,-frame*3f/60);
                Vector2 origin=FoamBuffer.WorldCellOrigin(center,4);var waves=Waves(frame*.03f);
                using var fresh=new FoamTransportField(32,4);
                cached.Prepare(origin,1,Vector2.right,waves,1,Vector3.one,default,default,contacts,1);
                fresh.Prepare(origin,1,Vector2.right,waves,1,Vector3.one,default,default,contacts,1);
                CollectionAssert.AreEqual(fresh.Mask.GetRawTextureData<byte>().ToArray(),cached.Mask.GetRawTextureData<byte>().ToArray());
                CollectionAssert.AreEqual(fresh.Velocity.GetRawTextureData<Vector2>().ToArray(),cached.Velocity.GetRawTextureData<Vector2>().ToArray());
                Assert.AreEqual(fresh.Occupied,cached.Occupied);
                if(frame>0)
                {
                    int dx=(int)Math.Abs((origin.x-previous.x)*8),dy=(int)Math.Abs((origin.y-previous.y)*8);
                    Assert.AreEqual(1024-(32-dx)*(32-dy),cached.RebuiltCells);
                }
                previous=origin;
            }
        }

        [Test] public void CpuRepair_ObserverAndBulkVelocityCopyAllocateNothing()
        {
            using var field=new FoamTransportField(768,96);
            var stages=new int[(int)FoamTransportPreparationStage.Finish+1];
            field.StageCompleted=stage=>stages[(int)stage]++;
            for(int i=0;i<16;i++)field.Prepare(Vector2.zero,1,Vector2.right,Waves(i),1,Vector3.one,default,default,Empty);
            Array.Clear(stages,0,stages.Length);
            using var probe=new FoamTransportAllocationProbe();probe.RequirePositiveControl();
            probe.Begin();
            for(int i=0;i<32;i++)field.Prepare(new Vector2(i*.125f,0),1,Vector2.right,Waves(i*.1f),1,Vector3.one,default,default,Empty);
            long allocated=probe.End();
            Assert.AreEqual(0,allocated);
            foreach(int count in stages)Assert.AreEqual(32,count);
        }

        [TestCase(1,1,0)] [TestCase(4,1,0)] [TestCase(8,1,0)]
        [TestCase(1,0,1)] [TestCase(4,0,1)] [TestCase(8,0,1)]
        [TestCase(1,1,-1)] [TestCase(4,1,-1)] [TestCase(8,1,-1)]
        [TestCase(1,-1,-1)] [TestCase(4,-1,-1)] [TestCase(8,-1,-1)]
        public void CpuRepair_RingPansMatchFreshTerrainMaskAndVelocityExactly(int pans,int dx,int dy)
        {
            var bedTexture=new Texture2D(17,23,TextureFormat.R16,false,true) {wrapMode=TextureWrapMode.Clamp};
            var stillTexture=new Texture2D(13,19,TextureFormat.R8,false,true) {wrapMode=TextureWrapMode.Clamp};
            try
            {
                var raw=bedTexture.GetRawTextureData<ushort>();for(int i=0;i<raw.Length;i++)raw[i]=(ushort)((i*1049)%65536);
                var water=stillTexture.GetRawTextureData<byte>();for(int i=0;i<water.Length;i++)water[i]=(byte)(i%7==0?0:112);
                bedTexture.Apply(false,false);stillTexture.Apply(false,false);
                foreach(var filter in new[]{FilterMode.Point,FilterMode.Bilinear})
                {
                    bedTexture.filterMode=filter;stillTexture.filterMode=filter;
                    var bed=new FoamTransportMap(bedTexture,new Vector2(-8,-8),new Vector2(16,16),-2,2);
                    var still=new FoamTransportMap(stillTexture,new Vector2(-7.25f,-8.5f),new Vector2(16.5f,18.25f),-2,2);
                    var contacts=new[]{new FoamTransportContacts.Shape(new Vector2(2,1),Vector2.right,new Vector2(.2f,.7f),-2,2,false,1)};
                    using var ring=new FoamTransportField(32,4);
                    var origin=new Vector2(dx>0?31:0,dy>0?31:0)*FoamBuffer.CellSize;
                    ring.Prepare(Vector2.zero,0,Vector2.right,Waves(0),1,Vector3.one,bed,still,contacts,1);
                    ring.Prepare(origin,0,Vector2.right,Waves(0),1,Vector3.one,bed,still,contacts,1);
                    for(int step=1;step<=pans;step++)
                    {
                        origin+=new Vector2(dx,dy)*FoamBuffer.CellSize;
                        ring.Prepare(origin,0,Vector2.right,Waves(step*.03f),1,Vector3.one,bed,still,contacts,1);
                        int entering=1024-(32-Math.Abs(dx))*(32-Math.Abs(dy));
                        Assert.AreEqual(entering,ring.RebuiltCells);
                        Assert.AreEqual(entering,ring.RebuiltMaskCells,"The mask must compose only entering cells, including a physical ring seam.");
                    }
                    using var fresh=new FoamTransportField(32,4);
                    fresh.Prepare(origin,0,Vector2.right,Waves(pans*.03f),1,Vector3.one,bed,still,contacts,1);
                    CollectionAssert.AreEqual(fresh.Mask.GetRawTextureData<byte>().ToArray(),ring.Mask.GetRawTextureData<byte>().ToArray());
                    CollectionAssert.AreEqual(fresh.Velocity.GetRawTextureData<Vector2>().ToArray(),ring.Velocity.GetRawTextureData<Vector2>().ToArray());
                    Assert.AreEqual(fresh.Occupied,ring.Occupied);Assert.AreEqual(fresh.Route,ring.Route);
                    const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                    var type=typeof(FoamTransportField);
                    var expected=(float[])type.GetField("_dryAt",flags).GetValue(fresh);
                    var actual=(float[])type.GetField("_dryAt",flags).GetValue(ring);
                    int rx=(int)type.GetField("_ringX",flags).GetValue(ring),ry=(int)type.GetField("_ringY",flags).GetValue(ring);
                    for(int y=0;y<32;y++)for(int x=0;x<32;x++)Exact(expected[y*32+x],actual[((y+ry)%32)*32+(x+rx)%32],x,y,"terrain");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(bedTexture);UnityEngine.Object.DestroyImmediate(stillTexture); }
        }

        [Test] public void CpuRepair_ObserverDoesNotAffectFieldOrSteadyCache()
        {
            using var field=new FoamTransportField(32,4);using var plain=new FoamTransportField(32,4);
            int calls=0;field.StageCompleted=stage=>calls++;
            var waves=Waves(.5f);
            field.Prepare(Vector2.zero,1,Vector2.right,waves,1,Vector3.one,default,default,Empty);field.Upload();
            plain.Prepare(Vector2.zero,1,Vector2.right,waves,1,Vector3.one,default,default,Empty);
            CollectionAssert.AreEqual(plain.Velocity.GetRawTextureData<Vector2>().ToArray(),field.Velocity.GetRawTextureData<Vector2>().ToArray());
            field.Prepare(Vector2.zero,1,Vector2.right,waves,1,Vector3.one,default,default,Empty);field.Upload();
            Assert.AreEqual(0,field.RebuiltCells);Assert.AreEqual(0,field.RebuiltMaskCells);
            Assert.AreEqual(0,field.RebuiltVelocityNodes);Assert.AreEqual(0,field.UploadedTextures);
            Assert.Greater(calls,0);
        }

        [Test] public void CpuRepair_RetainedPayloadStaysWithinExistingBudget()
        {
            using var field=new FoamTransportField(768,96);
            // Managed array payload plus the readable texture backing; excludes object headers,
            // driver storage and borrowed map textures, as does the existing declared budget.
            long bytes=768L*768+8L*99*99;
            const System.Reflection.BindingFlags flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            foreach(var f in typeof(FoamTransportField).GetFields(flags))
            {
                object value=f.GetValue(field);
                if(value is Array array)bytes+=ArrayBytes(array);
                if(value is FoamTransportVelocity velocity)
                    foreach(var v in typeof(FoamTransportVelocity).GetFields(flags))
                        if(v.GetValue(velocity) is Array data)bytes+=ArrayBytes(data);
            }
            Assert.LessOrEqual(bytes,FoamTransport.CpuBytes(768,99));
        }

        static long ArrayBytes(Array array)
        {
            Type element=array.GetType().GetElementType();
            return (long)array.Length*(element==typeof(bool)?1:System.Runtime.InteropServices.Marshal.SizeOf(element));
        }

        [Test] public void CpuRepair_ComposeKeepsFiniteEndpointsAndRejectsNonFinite()
        {
            var velocity=new FoamTransportVelocity(7);
            using var output=new NativeArray<Vector2>(49,Allocator.Persistent);
            Assert.AreEqual(true,velocity.Build(output,Vector2.zero,new Vector2(float.MaxValue,-float.MaxValue),
                PackedWaveField.Empty,1,Vector3.zero,Empty,0,false,out var first,out var maximum,out var uniform));
            Assert.AreEqual(float.MaxValue,maximum);Assert.AreEqual(true,uniform);
            Exact(float.MaxValue,first.x,0,0,"max");Exact(-float.MaxValue,first.y,0,0,"min");
            foreach(float bad in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity})
                Assert.AreEqual(false,velocity.Build(output,Vector2.zero,new Vector2(bad,0),
                    PackedWaveField.Empty,1,Vector3.zero,Empty,0,false,out _,out _,out _));
        }

        [TestCase(false)] [TestCase(true)]
        public void CpuRepair_FullInvalidationResetsWrappedLogicalView(bool invalidFrame)
        {
            var texture=new Texture2D(32,32,TextureFormat.R8,false,true) {filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            try
            {
                var bytes=texture.GetRawTextureData<byte>();for(int i=0;i<bytes.Length;i++)bytes[i]=0;
                for(int y=0;y<32;y++)bytes[y*32+2]=255;
                texture.Apply(false,false);
                var bed=new FoamTransportMap(texture,Vector2.zero,Vector2.one*4,0,2);
                using var ring=new FoamTransportField(32,4);using var fresh=new FoamTransportField(32,4);
                ring.Prepare(Vector2.zero,1,Vector2.right,Waves(0),1,Vector3.one,bed,default,Empty);ring.Upload();
                ring.Prepare(new Vector2(.125f,0),1,Vector2.right,Waves(0),1,Vector3.one,bed,default,Empty);ring.Upload();
                // Departing/entering columns are both wet: physical ring bytes did not change,
                // but resetting the offset changes their logical positions and requires upload.
                if(invalidFrame)ring.Prepare(Vector2.zero,float.NaN,Vector2.right,Waves(0),1,Vector3.one,bed,default,Empty);
                else texture.IncrementUpdateCount();
                ring.Prepare(Vector2.zero,1,Vector2.right,Waves(0),1,Vector3.one,bed,default,Empty);ring.Upload();
                fresh.Prepare(Vector2.zero,1,Vector2.right,Waves(0),1,Vector3.one,bed,default,Empty);
                CollectionAssert.AreEqual(fresh.Mask.GetRawTextureData<byte>().ToArray(),ring.Mask.GetRawTextureData<byte>().ToArray());
                Assert.Greater(ring.UploadedTextures,0);Assert.AreEqual(fresh.Occupied,ring.Occupied);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}
