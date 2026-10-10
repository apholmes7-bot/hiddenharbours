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
            long before=GC.GetAllocatedBytesForCurrentThread();
            var probe=new byte[256];
            long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            GC.KeepAlive(probe);
            Assert.Greater(allocated,0,"Zero-byte measurements are not evidence if this runtime's counter cannot see an allocation.");
        }

        static readonly FoamTransportContacts.Shape[] Empty=Array.Empty<FoamTransportContacts.Shape>();
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
                    Assert.AreEqual(BitConverter.SingleToInt32Bits(expected.x),BitConverter.SingleToInt32Bits(actual.x));
                    Assert.AreEqual(BitConverter.SingleToInt32Bits(expected.y),BitConverter.SingleToInt32Bits(actual.y));
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
                    Assert.AreEqual(PhaseANode(origin,x,y,waves,scale,Vector2.right,strengths),output[y*n+x]);
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
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<32;i++)field.Prepare(new Vector2(i*.125f,0),1,Vector2.right,Waves(i*.1f),1,Vector3.one,default,default,Empty);
            long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.AreEqual(0,allocated);
            foreach(int count in stages)Assert.AreEqual(32,count);
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
    }
}
