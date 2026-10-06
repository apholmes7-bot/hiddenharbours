using System;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public sealed class FoamTransportFieldTests
    {
        static readonly FoamTransportContacts.Shape[] Empty=Array.Empty<FoamTransportContacts.Shape>();
        static void Prepare(FoamTransportField f,Vector2 origin,FoamTransportMap bed=default,
                            FoamTransportMap still=default,float level=1,float phase=0)
            => f.Prepare(origin,level,Vector2.right,FoamTransportTests.Wave(phase),1,Vector3.right,bed,still,Empty);
        static Texture2D Map(int size=8) => new Texture2D(size,size,TextureFormat.R8,false,true)
            {filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        static void Zero(Texture2D texture)
        { var data=texture.GetRawTextureData<byte>(); for(int i=0;i<data.Length;i++) data[i]=0; texture.Apply(false,false); }

        [Test] public void FoamTransport_FieldIsWorldAnchoredAndUniformIsExact()
        {
            using var a=new FoamTransportField(16,2);
            using var b=new FoamTransportField(16,2);
            Vector2 first=FoamBuffer.WorldCellOrigin(new Vector2(.01f,.02f),2);
            Vector2 second=FoamBuffer.WorldCellOrigin(new Vector2(.07f,.08f),2);
            Prepare(a,first); Prepare(b,second);
            Assert.AreEqual(a.FlowOrigin,b.FlowOrigin);
            CollectionAssert.AreEqual(a.Velocity.GetRawTextureData<Vector2>().ToArray(),b.Velocity.GetRawTextureData<Vector2>().ToArray());
            Assert.AreEqual(FoamTransport.Route.Local,a.Route);
            a.Prepare(first,1,new Vector2(.3f,-.2f),PackedWaveField.Empty,1,Vector3.one,default,default,Empty);
            Assert.AreEqual(FoamTransport.Route.Uniform,a.Route); Assert.AreEqual(new Vector2(.3f,-.2f),a.UniformVelocity);
        }

        [Test] public void FoamTransport_MapBoundsIncludeInteriorFilterSupport()
        {
            var texture=Map();
            try
            {
                Zero(texture); var raw=texture.GetRawTextureData<byte>(); raw[4*8+4]=255; texture.Apply(false,false);
                var map=new FoamTransportMap(texture,Vector2.zero,Vector2.one,0,2);
                Assert.IsTrue(map.TryReader(out var reader));
                Assert.AreEqual(2,reader.BoundOverCell(Vector2.zero,1,false),"Centre/corner-only sampling misses this texel.");
                Assert.AreEqual(float.NegativeInfinity,reader.BoundOverCell(Vector2.zero,1,true),"Zero still-water codes mean none.");
                texture.filterMode=FilterMode.Bilinear;
                Assert.IsTrue(map.TryReader(out reader));
                Assert.AreEqual(2,reader.BoundOverCell(new Vector2(.49f,.49f),.02f,false));
                Assert.AreEqual(float.NegativeInfinity,reader.BoundOverCell(new Vector2(-.01f,0),.1f,true));
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test] public void FoamTransport_CameraScrollCacheMatchesFreshPreparation()
        {
            var texture=Map();
            try
            {
                Zero(texture); var raw=texture.GetRawTextureData<byte>();
                for(int i=0;i<raw.Length;i++) raw[i]=(byte)((i*47)%256);
                texture.Apply(false,false);
                var map=new FoamTransportMap(texture,Vector2.zero,new Vector2(4,4),0,2);
                using var reused=new FoamTransportField(16,2); using var fresh=new FoamTransportField(16,2);
                Prepare(reused,Vector2.zero,map);
                Prepare(reused,new Vector2(.125f,-.125f),map);
                Assert.AreEqual(31,reused.RebuiltCells);
                Prepare(fresh,new Vector2(.125f,-.125f),map);
                CollectionAssert.AreEqual(fresh.Mask.GetRawTextureData<byte>().ToArray(),reused.Mask.GetRawTextureData<byte>().ToArray());
                Prepare(reused,new Vector2(.13f,-.125f),map);
                Assert.AreEqual(256,reused.RebuiltCells,"A changed drawn residual must not be rounded into a cached cell.");
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test] public void FoamTransport_MapRevisionAndTideInvalidateCorrectly()
        {
            var texture=Map();
            try
            {
                Zero(texture); var map=new FoamTransportMap(texture,Vector2.zero,new Vector2(2,2),0,2);
                using var field=new FoamTransportField(16,2);
                Prepare(field,Vector2.zero,map); Assert.AreEqual(0,field.Occupied);
                var raw=texture.GetRawTextureData<byte>(); raw[4*8+4]=255; texture.Apply(false,false);
                // Null-device Apply need not advance the revision; explicitly exercise cache invalidation.
                texture.IncrementUpdateCount();
                Prepare(field,Vector2.zero,map); Assert.AreEqual(256,field.RebuiltCells); Assert.Greater(field.Occupied,0);
                Prepare(field,Vector2.zero,map,level:3);
                Assert.AreEqual(0,field.RebuiltCells); Assert.AreEqual(0,field.Occupied,"Tide re-compares cached bounds.");
                Prepare(field,Vector2.zero,map,phase:1);
                Assert.AreEqual(0,field.RebuiltCells,"Wave phase must not invalidate terrain bytes.");
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test] public void FoamTransport_InvalidMapHoldsMovement()
        {
            var texture=Map();
            try
            {
                Zero(texture); texture.Apply(false,true);
                using var field=new FoamTransportField(16,2);
                Prepare(field,Vector2.zero,new FoamTransportMap(texture,Vector2.zero,Vector2.one,0,2));
                Assert.AreEqual(FoamTransport.Route.Hold,field.Route); Assert.AreEqual(FoamTransport.Fault.Map,field.Fault);
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [Test] public void FoamTransport_ContactMaskMatchesDeclaredGeometry()
        {
            using var field=new FoamTransportField(16,2);
            var thin=new FoamTransportContacts.Shape(new Vector2(1,1),new Vector2(1,1).normalized,
                new Vector2(.02f,.6f),0,2,false,1);
            var deck=new FoamTransportContacts.Shape(Vector2.one,Vector2.right,Vector2.one,3,4,false,2);
            field.Prepare(Vector2.zero,1,Vector2.right,PackedWaveField.Empty,1,Vector3.one,default,default,new[]{thin,deck},2);
            Assert.Greater(field.Occupied,0); Assert.Less(field.Occupied,100,"Elevated deck must not become a dam.");
            var mask=field.Mask.GetRawTextureData<byte>();
            for(int y=0;y<16;y++) for(int x=0;x<16;x++)
                Assert.AreEqual(thin.TouchesCell(new Vector2(x+.5f,y+.5f)*.125f,.0625f),mask[y*16+x]!=0);
            field.Prepare(Vector2.zero,1,Vector2.right,PackedWaveField.Empty,1,Vector3.one,default,default,new[]{deck},1);
            Assert.AreEqual(0,field.Occupied);
        }

        [Test] public void FoamTransport_ContactCapacityAndInvalidInputsHold()
        {
            using var field=new FoamTransportField(16,2);
            field.Prepare(Vector2.zero,1,Vector2.right,PackedWaveField.Empty,1,Vector3.one,default,default,
                new FoamTransportContacts.Shape[129],129);
            Assert.AreEqual(FoamTransport.Fault.Capacity,field.Fault); Assert.AreEqual(FoamTransport.Route.Hold,field.Route);
            Assert.AreEqual(129,field.ContactCount);
            field.Prepare(Vector2.zero,1,Vector2.right,PackedWaveField.Empty,1,Vector3.one,default,default,
                new FoamTransportContacts.Shape[1],1);
            Assert.AreEqual(FoamTransport.Fault.Contacts,field.Fault);
            field.Prepare(Vector2.zero,1,new Vector2(float.NaN,0),PackedWaveField.Empty,1,Vector3.one,default,default,Empty);
            Assert.AreEqual(FoamTransport.Fault.Inputs,field.Fault);
        }

        [Test] public void FoamTransport_ContactComponentUsesAuthoredWaterInterval()
        {
            var definition=ScriptableObject.CreateInstance<FoamTransportContactDef>();
            var go=new GameObject("F2 test contact");
            try
            {
                definition.Id="foam_contact.test_pile"; definition.Shape=FoamTransportContactDef.Outline.Circle;
                definition.HalfSize=new Vector2(.1f,.1f); definition.MinLevel=-2; definition.MaxLevel=2;
                go.transform.position=new Vector3(3,4,100);
                var contact=go.AddComponent<FoamTransportContact>(); contact.Configure(definition);
                Assert.IsTrue(contact.TrySnapshot(out var shape));
                Assert.AreEqual(new Vector2(3,4),shape.Center); Assert.IsTrue(shape.Wet(0));
                Assert.IsFalse(shape.Wet(100),"Projected sprite Z must not replace chart datum.");
                definition.Id=""; Assert.IsFalse(contact.TrySnapshot(out shape));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(definition); }
        }

        [Test] public void FoamTransport_PreparationHasNoSteadyManagedAllocation()
        {
            using var field=new FoamTransportField(16,2);
            for(int i=0;i<8;i++) Prepare(field,Vector2.zero);
            long before=GC.GetAllocatedBytesForCurrentThread();
            for(int i=0;i<16;i++) Prepare(field,Vector2.zero);
            long allocated=GC.GetAllocatedBytesForCurrentThread()-before;
            Assert.AreEqual(0,allocated,"Cold allocation/upload/driver costs are separately profiled in Phase C.");
        }

        [Test] public void FoamTransport_CameraFieldsAreIndependent()
        {
            using var a=new FoamTransportField(16,2); using var b=new FoamTransportField(16,2);
            Prepare(a,Vector2.zero); Prepare(b,Vector2.one);
            var retained=b.Velocity.GetRawTextureData<Vector2>().ToArray();
            Prepare(a,new Vector2(10,20),phase:2);
            CollectionAssert.AreEqual(retained,b.Velocity.GetRawTextureData<Vector2>().ToArray());
            Assert.AreNotSame(a.Mask,b.Mask); Assert.AreNotSame(a.Velocity,b.Velocity);
        }
    }
}
