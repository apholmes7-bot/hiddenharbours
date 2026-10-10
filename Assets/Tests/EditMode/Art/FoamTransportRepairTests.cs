using System;
using System.IO;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public sealed class FoamTransportRepairTests
    {
        static readonly FoamTransportContacts.Shape[] Empty=Array.Empty<FoamTransportContacts.Shape>();
        static FoamTransportContacts.Shape Pile(float x,float y,float radius=.12f) =>
            new FoamTransportContacts.Shape(new Vector2(x,y),Vector2.right,Vector2.one*radius,0,2,true,1);
        static void Prepare(FoamTransportField field, Vector2 origin=default, float phase=0,
            float sea=1, Vector2? drift=null, Vector3? strengths=null, float scale=1,
            FoamTransportMap bed=default, FoamTransportMap still=default,
            FoamTransportContacts.Shape[] contacts=null)
        {
            contacts=contacts??Empty;
            field.Prepare(origin,sea,drift??Vector2.right,FoamTransportTests.Wave(phase),scale,
                strengths??Vector3.one,bed,still,contacts,contacts.Length);
        }
        static Texture2D Map()
        {
            var texture=new Texture2D(8,8,TextureFormat.R8,false,true)
                {filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
            var raw=texture.GetRawTextureData<byte>();
            for(int i=0;i<raw.Length;i++) raw[i]=(byte)(i*4);
            texture.Apply(false,false);
            return texture;
        }
        static void Same(FoamTransportField a,FoamTransportField b)
        {
            Assert.AreEqual(a.Occupied,b.Occupied);
            Assert.AreEqual(a.Route,b.Route);
            CollectionAssert.AreEqual(a.Mask.GetRawTextureData<byte>().ToArray(),b.Mask.GetRawTextureData<byte>().ToArray());
            CollectionAssert.AreEqual(a.Velocity.GetRawTextureData<Vector2>().ToArray(),b.Velocity.GetRawTextureData<Vector2>().ToArray());
        }

        [Test] public void Repair_SteadyPrepareAndUploadDoNoWork()
        {
            using var field=new FoamTransportField(32,4);
            var contacts=new[]{Pile(1,1)};
            Prepare(field,contacts:contacts); field.Upload();
            Assert.AreEqual(2,field.UploadedTextures);
            Prepare(field,contacts:contacts); field.Upload();
            Assert.AreEqual(0,field.RebuiltCells);
            Assert.AreEqual(0,field.RebuiltMaskCells);
            Assert.AreEqual(0,field.RebuiltVelocityNodes);
            Assert.AreEqual(0,field.UploadedTextures);
            Assert.AreEqual(0,field.UploadedBytes);
        }

        [TestCase("phase")] [TestCase("drift")] [TestCase("strain")]
        [TestCase("curl")] [TestCase("collection")] [TestCase("scale")]
        public void Repair_VelocityInputsNeverRebuildOrUploadMask(string input)
        {
            using var field=new FoamTransportField(32,4);
            Prepare(field); field.Upload();
            Vector3 strengths=Vector3.one;
            if(input=="strain") strengths.x=2;
            if(input=="curl") strengths.y=2;
            if(input=="collection") strengths.z=2;
            Prepare(field,phase:input=="phase"?1:0,drift:input=="drift"?Vector2.up:Vector2.right,
                strengths:strengths,scale:input=="scale"?2:1);
            Assert.AreEqual(0,field.RebuiltCells); Assert.AreEqual(0,field.RebuiltMaskCells);
            Assert.AreEqual(field.FlowResolution*field.FlowResolution,field.RebuiltVelocityNodes);
            field.Upload(); Assert.AreEqual(1,field.UploadedTextures);
            Assert.AreEqual(8*field.FlowResolution*field.FlowResolution,field.UploadedBytes);
        }

        [Test] public void Repair_SubCellCameraPanDoesNothing()
        {
            using var field=new FoamTransportField(32,4);
            Prepare(field,FoamBuffer.WorldCellOrigin(new Vector2(.01f,.01f),4)); field.Upload();
            Prepare(field,FoamBuffer.WorldCellOrigin(new Vector2(.09f,.09f),4)); field.Upload();
            Assert.AreEqual(0,field.RebuiltCells); Assert.AreEqual(0,field.RebuiltMaskCells);
            Assert.AreEqual(0,field.RebuiltVelocityNodes); Assert.AreEqual(0,field.UploadedTextures);
        }

        [Test] public void Repair_TrainCountDirectionWavelengthAndAmplitudeInvalidateVelocity()
        {
            using var field=new FoamTransportField(32,4);
            Prepare(field); field.Upload();
            var train=new Vector4(1,0,1,.25f);
            for(int change=0;change<4;change++)
            {
                if(change==1) { train.x=.6f; train.y=.8f; }
                if(change==2) train.z=2;
                if(change==3) train.w=.5f;
                var waves=new PackedWaveField(train,new Vector4(0,1,.5f,.2f),default,default,default,default,default,default,
                    default,default,new Vector4(2,1,1,0));
                field.Prepare(Vector2.zero,1,Vector2.right,waves,1,Vector3.one,default,default,Empty);
                Assert.AreEqual(0,field.RebuiltCells); Assert.AreEqual(0,field.RebuiltMaskCells);
                Assert.AreEqual(49,field.RebuiltVelocityNodes);
                field.Upload(); Assert.AreEqual(1,field.UploadedTextures);
            }
        }

        [TestCase(1,0)] [TestCase(-1,0)] [TestCase(0,1)] [TestCase(0,-1)]
        [TestCase(1,-1)] [TestCase(-9,9)] [TestCase(31,-31)]
        public void Repair_ScrollRebuildsOnlyEnteringTerrainAndMaskTiles(int dx,int dy)
        {
            var texture=Map();
            try
            {
                var bed=new FoamTransportMap(texture,Vector2.zero,new Vector2(4,4),0,2);
                var contacts=new[]{Pile(1,1),Pile(3,2,.3f)};
                using var field=new FoamTransportField(32,4);
                using var fresh=new FoamTransportField(32,4);
                Prepare(field,bed:bed,contacts:contacts); field.Upload();
                Vector2 origin=new Vector2(dx,dy)*FoamBuffer.CellSize;
                Prepare(field,origin,bed:bed,contacts:contacts);
                Prepare(fresh,origin,bed:bed,contacts:contacts);
                Same(field,fresh);
                Assert.AreEqual(32*32-(32-Math.Abs(dx))*(32-Math.Abs(dy)),field.RebuiltCells);
                if(Math.Abs(dx)<=1 && Math.Abs(dy)<=1)
                    Assert.LessOrEqual(field.RebuiltMaskCells,32*32-24*24);
                field.Upload(); Assert.Greater(field.UploadedTextures,0);
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test] public void Repair_MovingContactRebuildsOnlyOldAndNewTiles()
        {
            using var field=new FoamTransportField(32,4);
            using var fresh=new FoamTransportField(32,4);
            var contacts=new[]{Pile(.5f,.5f),Pile(3.5f,3.5f)};
            Prepare(field,contacts:contacts); field.Upload();
            contacts[0]=Pile(1.5f,.5f);
            Prepare(field,contacts:contacts); Prepare(fresh,contacts:contacts);
            Same(field,fresh);
            Assert.AreEqual(0,field.RebuiltCells);
            Assert.AreEqual(128,field.RebuiltMaskCells,"Two one-metre tiles; the distant unchanged pile is untouched.");
            Assert.AreEqual(49,field.RebuiltVelocityNodes);
            field.Upload(); Assert.AreEqual(2,field.UploadedTextures);
        }

        [Test] public void Repair_TideWithinThresholdIntervalDoesNotTouchMask()
        {
            var texture=Map();
            try
            {
                var bed=new FoamTransportMap(texture,Vector2.zero,Vector2.one*4,0,2);
                using var field=new FoamTransportField(32,4);
                Prepare(field,bed:bed,sea:.001f); field.Upload();
                Prepare(field,bed:bed,sea:.002f); field.Upload();
                Assert.AreEqual(0,field.RebuiltCells); Assert.AreEqual(0,field.RebuiltMaskCells);
                Assert.AreEqual(0,field.RebuiltVelocityNodes); Assert.AreEqual(0,field.UploadedTextures);
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test] public void Repair_TideCrossingAndContactWetIntervalMatchFreshField()
        {
            var texture=Map();
            try
            {
                var bed=new FoamTransportMap(texture,Vector2.zero,Vector2.one*4,0,2);
                var contacts=new[]{Pile(.5f,.5f)};
                using var field=new FoamTransportField(32,4);
                Prepare(field,bed:bed,contacts:contacts,sea:.9f); field.Upload();
                foreach(float sea in new[]{1.01f,2.1f,.8f})
                {
                    using var fresh=new FoamTransportField(32,4);
                    Prepare(field,bed:bed,contacts:contacts,sea:sea);
                    Prepare(fresh,bed:bed,contacts:contacts,sea:sea);
                    Same(field,fresh); Assert.AreEqual(0,field.RebuiltCells);
                }
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [TestCase("revision")] [TestCase("filter")] [TestCase("mapping")] [TestCase("decode")]
        [TestCase("still")] [TestCase("residual")]
        public void Repair_MapAndResidualInvalidationsMatchFreshField(string input)
        {
            var texture=Map();
            try
            {
                var bed=new FoamTransportMap(texture,Vector2.zero,Vector2.one*4,0,2);
                using var field=new FoamTransportField(32,4); using var fresh=new FoamTransportField(32,4);
                Prepare(field,bed:bed); field.Upload();
                if(input=="revision") { var raw=texture.GetRawTextureData<byte>(); raw[0]=255; texture.Apply(false,false); texture.IncrementUpdateCount(); }
                if(input=="filter") texture.filterMode=FilterMode.Bilinear;
                if(input=="mapping") bed=new FoamTransportMap(texture,Vector2.one,Vector2.one*4,0,2);
                if(input=="decode") bed=new FoamTransportMap(texture,Vector2.zero,Vector2.one*4,-2,2);
                var still=input=="still"?new FoamTransportMap(texture,Vector2.zero,Vector2.one*4,1,3):default;
                Vector2 origin=input=="residual"?new Vector2(.01f,0):Vector2.zero;
                Prepare(field,origin,bed:bed,still:still); Prepare(fresh,origin,bed:bed,still:still);
                Same(field,fresh); Assert.AreEqual(1024,field.RebuiltCells);
                Assert.AreEqual(0,field.RebuiltVelocityNodes,"These inputs do not change world velocity nodes.");
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test] public void Repair_DirtyStateSurvivesSkippedUploadAndInvalidFrame()
        {
            using var field=new FoamTransportField(32,4); using var fresh=new FoamTransportField(32,4);
            Prepare(field); field.Upload(); Prepare(field,phase:1); Prepare(field,phase:1);
            field.Upload(); Assert.AreEqual(1,field.UploadedTextures);
            Prepare(field,sea:float.NaN); Assert.AreEqual(FoamTransport.Route.Hold,field.Route);
            Prepare(field,phase:1); Prepare(fresh,phase:1); Same(field,fresh);
        }

        [Test] public void Repair_ChangingPrepareHasZeroManagedAllocation()
        {
            using var field=new FoamTransportField(32,4);
            var contacts=new[]{Pile(.5f,.5f)};
            for(int i=0;i<16;i++) Prepare(field,phase:i,contacts:contacts);
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<64;i++)
            {
                contacts[0]=Pile(.5f+i*.001f,.5f);
                Prepare(field,new Vector2((i%3)*.125f,0),phase:i,sea:1+i*.001f,contacts:contacts);
            }
            long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.AreEqual(0,allocated);
        }

        [Test] public void Repair_FactoredVelocityMatchesScalarLaw()
        {
            using var field=new FoamTransportField(32,4);
            var contacts=new[]{Pile(-1,0,.3f),Pile(-.8f,.1f,.5f)};
            var waves=new PackedWaveField(new Vector4(.6f,.8f,1.3f,.7f),new Vector4(-.8f,.6f,.3f,.2f),
                new Vector4(1,0,2,.1f),Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero,
                new Vector4(.3f,1.4f,2.1f,0),Vector4.zero,new Vector4(3,1,1,0));
            foreach(var origin in new[]{new Vector2(-2,-2),new Vector2(300,-900)})
            {
                field.Prepare(origin,1,new Vector2(.4f,.2f),waves,.85f,new Vector3(.2f,.3f,.4f),default,default,contacts,contacts.Length);
                var actual=field.Velocity.GetRawTextureData<Vector2>();
                for(int y=0;y<field.FlowResolution;y++) for(int x=0;x<field.FlowResolution;x++)
                {
                    Vector2 p=field.FlowOrigin+new Vector2(x,y)*FoamTransport.FlowCellMeters;
                    Vector2 expected=new Vector2(.4f,.2f)+FoamTransport.WaveVelocity(waves,p,.85f,new Vector3(.2f,.3f,.4f))+
                        FoamTransport.CollectionVelocity(p,new Vector2(.4f,.2f),.4f,contacts,contacts.Length);
                    Assert.Less((actual[y*field.FlowResolution+x]-expected).magnitude,2e-6f);
                }
            }
        }

        [Test] public void Repair_MixedChangesMatchUncachedCellOracle()
        {
            var texture=Map();
            try
            {
                // Non-tile-aligned size and many ring wraps catch addressing that a single pan misses.
                using var field=new FoamTransportField(31,4);
                var contacts=new[]{Pile(.5f,.5f),Pile(2.5f,2.5f,.3f)};
                for(int frame=0;frame<80;frame++)
                {
                    Vector2 origin=new Vector2(frame%17-8,frame%11-5)*.125f;
                    float sea=.7f+(frame%19)*.08f;
                    contacts[0]=Pile(.5f+(frame%5)*.15f,.5f);
                    contacts[1]=new FoamTransportContacts.Shape(new Vector2(2.5f,2.5f),new Vector2(.6f,.8f),
                        new Vector2(.04f,.7f),.5f,1.5f,false,2);
                    if(frame%9==0)
                    {
                        var bytes=texture.GetRawTextureData<byte>(); bytes[frame%64]=(byte)(frame*3);
                        texture.Apply(false,false); texture.IncrementUpdateCount();
                    }
                    texture.filterMode=frame%13<6?FilterMode.Point:FilterMode.Bilinear;
                    var bed=new FoamTransportMap(texture,Vector2.zero,Vector2.one*4,-1,2);
                    var still=new FoamTransportMap(texture,Vector2.one,Vector2.one*2,0,1.5f);
                    Prepare(field,origin,phase:frame*.03f,sea:sea,bed:bed,still:still,contacts:contacts);
                    bed.TryReader(out var bedReader); still.TryReader(out var stillReader);
                    var mask=field.Mask.GetRawTextureData<byte>(); int occupied=0;
                    for(int y=0;y<31;y++) for(int x=0;x<31;x++)
                    {
                        Vector2 corner=origin+new Vector2(x,y)*FoamBuffer.CellSize;
                        bool blocked=bedReader.BoundOverCell(corner,FoamBuffer.CellSize,false)>=
                            Math.Max(sea,stillReader.BoundOverCell(corner,FoamBuffer.CellSize,true));
                        foreach(var contact in contacts)
                            blocked|=contact.Wet(sea) && contact.TouchesCell(corner+Vector2.one*.0625f,.0625f);
                        Assert.AreEqual(blocked,mask[y*31+x]!=0,"frame "+frame+" cell "+x+","+y);
                        if(blocked) occupied++;
                    }
                    Assert.AreEqual(occupied,field.Occupied);
                }
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [TestCase("TransportPassData pass", "context")]
        [TestCase("FoamPassData data", "ctx")]
        public void Repair_RecordedFoamDrawUsesCopiedPropertyBlock(string signature,string context)
        {
            // A Null device cannot execute GPU commands. This structural guard catches the exact
            // shared-material regression on CI; code preservation still needs the GPU gate.
            string source=File.ReadAllText(Path.Combine(Application.dataPath,"_Project/Code/Art/IsoFacetHullFeature.cs"));
            int start=source.IndexOf("builder.SetRenderFunc(("+signature,StringComparison.Ordinal);
            Assert.Greater(start,0);
            int end=source.IndexOf("\n                        });",start,StringComparison.Ordinal);
            if(signature.StartsWith("Transport")) end=source.IndexOf("\n                });",start,StringComparison.Ordinal);
            string body=source.Substring(start,end-start);
            StringAssert.DoesNotContain("Material.Set",body);
            StringAssert.Contains(context+".renderGraphPool.GetTempMaterialPropertyBlock()",body);
            StringAssert.Contains(context+".cmd.DrawProcedural(",body);
            StringAssert.Contains("3, 1, properties)",body);
            StringAssert.Contains("properties.SetTexture(",body);
            StringAssert.Contains("properties.SetVector(BlitScaleBias",body);
        }
    }
}
