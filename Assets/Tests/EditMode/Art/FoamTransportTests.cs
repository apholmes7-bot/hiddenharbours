using System;
using System.Runtime.InteropServices;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public sealed class FoamTransportTests
    {
        const uint K=65535;

        [Test] public void FoamTransport_UniformFlowPreservesShape()
        {
            var route=FoamTransport.Classify(Vector3.right,true,true,true,0);
            Assert.AreEqual(FoamTransport.Route.Uniform,route);
            Vector2 remainder=Vector2.zero, old=new Vector2(-2,-1), accumulated=Vector2.zero;
            Vector2Int moved=Vector2Int.zero;
            for(int frame=0;frame<600;frame++)
            {
                float dt=1f/(32<<(frame%3));
                Vector2 velocity=frame<300?new Vector2(.5f,-.25f):new Vector2(-.25f,.5f);
                Vector2 next=old+(frame%17==0?new Vector2(.125f,-.125f):Vector2.zero);
                var cells=FoamTransport.DriftCells(route,ref remainder,Vector2.zero,velocity,dt);
                moved+=cells; accumulated+=velocity*dt;
                Assert.AreEqual(accumulated,new Vector2(moved.x,moved.y)*.125f+remainder);
                var offset=FoamBuffer.SourceOffsetCells(old,next,cells);
                // Integer source indexing preserves arbitrary values and a sharp edge in BOTH channels.
                for(int y=0;y<16;y++) for(int x=0;x<16;x++)
                {
                    int sx=x+offset.x, sy=y+offset.y;
                    Vector2 worldSource=next+new Vector2(x,y)*.125f-new Vector2(cells.x,cells.y)*.125f;
                    Assert.AreEqual(new Vector2(sx,sy),(worldSource-old)*8);
                    if(sx>=0 && sx<16 && sy>=0 && sy<16)
                    {
                        uint value=(uint)(sy*16+sx)*193;
                        Assert.AreEqual(value,(uint)Math.Round((value/(double)K)*K));
                    }
                }
                old=next;
            }
            foreach(int fps in new[]{30,60,144})
            {
                Vector2 legacy=Vector2.zero, enabled=Vector2.zero;
                for(int frame=0;frame<fps*10;frame++)
                {
                    Vector2 velocity=frame<fps*5?new Vector2(.43f,-.37f):new Vector2(-.17f,.31f);
                    Assert.AreEqual(FoamBuffer.AdvectCells(ref legacy,velocity*(1f/fps)),
                        FoamTransport.DriftCells(route,ref enabled,Vector2.zero,velocity,1f/fps));
                    Assert.AreEqual(legacy,enabled,"Uniform route must preserve the exact old float operations.");
                }
            }
        }

        [Test] public void FoamTransport_BoundsMassAndFreshness()
        {
            uint[] masses={0,1,71,32768,K-1,K};
            uint[,] ages={{0,K},{K,0},{991,35000},{K,K}};
            float[] speeds={-.25f,-.07f,0,.07f,.25f};
            foreach(uint ma in masses) foreach(uint mb in masses)
            for(int age=0;age<4;age++) foreach(float c in speeds)
            {
                uint fa=ages[age,0], fb=ages[age,1];
                FoamTransport.Exchange(ma,fa,mb,fb,c,out uint a,out uint af,out uint b,out uint bf);
                Assert.AreEqual(ma+mb,a+b);
                Assert.LessOrEqual(a,K); Assert.LessOrEqual(b,K);
                Assert.LessOrEqual(af,K); Assert.LessOrEqual(bf,K);
                if(a!=ma)
                {
                    uint mixed=c>0?bf:af;
                    Assert.That(mixed,Is.InRange(Math.Min(fa,fb),Math.Max(fa,fb)));
                }
            }
            FoamTransport.Exchange(40000,60000,20000,10000,.25f,out var m0,out var f0,out var m1,out var f1);
            Assert.AreEqual(30000,m0); Assert.AreEqual(30000,m1); Assert.AreEqual(60000,f0); Assert.AreEqual(26667,f1);
            FoamTransport.Exchange(K,K,K,0,.25f,out m0,out f0,out m1,out f1);
            Assert.AreEqual(K,m0); Assert.AreEqual(K,m1); Assert.AreEqual(0,f1,"Full receiver refuses material and its clock.");
            FoamTransport.Exchange(100,17000,0,K,.25f,out m0,out f0,out m1,out f1);
            Assert.AreEqual(17000,f1,"Empty-cell stale freshness has zero weight.");
            var mass=new uint[144]; var fresh=new uint[144];
            for(int i=0;i<mass.Length;i++) { mass[i]=(uint)((i*3571+71)%K); fresh[i]=(uint)((i*919+311)%K); }
            ulong before=Sum(mass);
            for(int i=0;i<200;i++) Sweep(mass,fresh,12,new Vector2(.07f,-.09f),null);
            Assert.AreEqual(before,Sum(mass));
        }

        [Test] public void FoamTransport_ObstaclesBlockFlux()
        {
            const int n=16;
            var mass=new uint[n*n]; var fresh=new uint[n*n]; var solid=new bool[n*n];
            for(int y=0;y<n;y++) { solid[y*n+8]=true; mass[y*n+6]=40000; fresh[y*n+6]=K; }
            ulong before=Sum(mass);
            Assert.AreEqual(FoamTransport.Route.Local,FoamTransport.Classify(Vector3.right,true,true,true,n),
                "Uniform INCIDENT wind must not select an unrestricted copy through a wall.");
            for(int step=0;step<160;step++) Sweep(mass,fresh,n,new Vector2(.2f,.01f),solid);
            for(int y=0;y<n;y++) for(int x=8;x<n;x++) Assert.AreEqual(0,mass[y*n+x]);
            Assert.AreEqual(before,Sum(mass));
        }

        [Test] public void FoamTransport_SubcellCameraPanIsInvariant()
        {
            Vector2 a=FoamBuffer.WorldCellOrigin(new Vector2(-.11f,.021f),4);
            Vector2 b=FoamBuffer.WorldCellOrigin(new Vector2(-.08f,.051f),4);
            Assert.AreEqual(a,b);
            Vector2 residual=new Vector2(.03125f,.0625f), copy=residual;
            Assert.AreEqual(Vector2Int.zero,FoamTransport.DriftCells(FoamTransport.Route.Local,
                ref residual,Vector2.one,Vector2.one,1));
            Assert.AreEqual(FoamBuffer.DrawOrigin(a,copy),FoamBuffer.DrawOrigin(b,residual));
            Assert.AreEqual(1,FoamTransport.Parity(-3)); Assert.AreEqual(0,FoamTransport.Parity(-2));
        }

        [Test] public void FoamTransport_ModeSwitchPreservesDrawOrigin()
        {
            Vector2 remainder=new Vector2(.09375f,.03125f), retained=remainder;
            foreach(var route in new[]{FoamTransport.Route.Local,FoamTransport.Route.Hold,FoamTransport.Route.Legacy,FoamTransport.Route.Uniform})
            {
                Assert.AreEqual(Vector2Int.zero,FoamTransport.DriftCells(route,ref remainder,Vector2.one,Vector2.one,0));
                Assert.AreEqual(retained,remainder);
            }
            var cells=FoamTransport.DriftCells(FoamTransport.Route.Uniform,ref remainder,Vector2.zero,Vector2.right,.0625f);
            Assert.AreEqual(new Vector2Int(1,0),cells);
            Assert.AreEqual(retained+Vector2.right*.0625f,new Vector2(cells.x,cells.y)*.125f+remainder);
        }

        [Test] public void FoamTransport_CflCapReportsLostDuration()
        {
            var hitch=FoamTransport.Substeps(1,.2f);
            Assert.AreEqual(4,hitch.Count); Assert.AreEqual(.03125f,hitch.Seconds);
            Assert.AreEqual(.075f,hitch.DroppedSeconds,1e-7);
            var normal=FoamTransport.Substeps(1,1f/60);
            Assert.AreEqual(1,normal.Count); Assert.AreEqual(0,normal.DroppedSeconds);
            Assert.AreEqual(0,FoamTransport.Substeps(1,0).Count);
            Assert.AreEqual(0,FoamTransport.Substeps(float.NaN,1).Count);
            Assert.LessOrEqual(FoamTransport.Substeps(float.MaxValue,float.MaxValue).Count,4);
        }

        [Test] public void FoamTransport_PassthroughIsLegacy()
        {
            Assert.AreEqual(FoamTransport.Route.Legacy,FoamTransport.Classify(Vector3.zero,false,false,false,128));
            Assert.IsFalse(FoamTransport.Requested(-Vector3.one));
            Assert.AreEqual(FoamTransport.Route.Hold,FoamTransport.Classify(new Vector3(float.NaN,0,0),true,true,true,0));
            Vector2 a=new Vector2(.01f,.11f), b=a;
            for(int i=0;i<100;i++)
            {
                Vector2 drift=new Vector2(.43f,-.37f);
                Assert.AreEqual(FoamBuffer.AdvectCells(ref a,drift*(1f/60)),
                    FoamTransport.DriftCells(FoamTransport.Route.Legacy,ref b,drift,Vector2.zero,1f/60));
                Assert.AreEqual(a,b);
            }
        }

        [Test] public void FoamTransport_FallbackIsExplicit()
        {
            Assert.AreEqual(FoamTransport.Fault.Format,FoamTransport.Availability(RenderTextureFormat.RGHalf,96,true));
            Assert.AreEqual(FoamTransport.Fault.Format,FoamTransport.Availability(RenderTextureFormat.RG16,96,true));
            Assert.AreEqual(FoamTransport.Fault.Extent,FoamTransport.Availability(RenderTextureFormat.RG32,512,true));
            Assert.AreEqual(FoamTransport.Fault.None,FoamTransport.Availability(RenderTextureFormat.RG32,96,true));
            Assert.AreEqual(FoamTransport.Route.Unavailable,FoamTransport.Classify(Vector3.one,false,true,true,0));
            Assert.AreEqual(FoamTransport.Route.Hold,FoamTransport.Classify(Vector3.one,true,false,true,0));
        }

        [Test] public void FoamTransport_BudgetMatchesResources()
        {
            int res=FoamBuffer.ResolutionForExtent(96), flow=FoamTransport.FlowResolution(96);
            Assert.AreEqual(768,res); Assert.AreEqual(99,flow);
            Assert.AreEqual(5386824,FoamTransport.GpuBytes(res,flow));
            Assert.AreEqual(5395016,FoamTransport.CpuBytes(res,flow));
            Assert.AreEqual(64,Marshal.SizeOf<FoamTransportContacts.Shape>());
            Assert.AreEqual(17,1+FoamTransport.Matchings*FoamTransport.MaxSubsteps);
            Assert.AreEqual(8,FoamBuffer.MaxInjectors);
        }

        [Test] public void FoamTransport_WaveDriveUsesPublishedPhase()
        {
            var wave=Wave(0);
            Vector2 velocity=FoamTransport.WaveVelocity(wave,Vector2.zero,1,new Vector3(2,3,0));
            Assert.AreEqual(.5f,velocity.x,1e-6); Assert.AreEqual(.75f,velocity.y,1e-6);
            Assert.Less(FoamTransport.WaveVelocity(Wave(Mathf.PI),Vector2.zero,1,Vector3.right).x,0);
            Assert.AreEqual(Vector2.zero,FoamTransport.WaveVelocity(PackedWaveField.Empty,Vector2.one,1,Vector3.one));
        }

        [Test] public void FoamTransport_CollectionIgnoresRegistrationOrder()
        {
            var a=new FoamTransportContacts.Shape(Vector2.zero,Vector2.right,Vector2.one,-1,1,false,1);
            var b=new FoamTransportContacts.Shape(Vector2.up,Vector2.right,Vector2.one,-1,1,false,2);
            var point=new Vector2(2,.5f);
            var first=FoamTransport.CollectionVelocity(point,Vector2.right,1,new[]{a,b},2);
            var second=FoamTransport.CollectionVelocity(point,Vector2.right,1,new[]{b,a},2);
            Assert.AreEqual(first,second); Assert.Less(first.x,0);
            Assert.AreEqual(Vector2.zero,FoamTransport.CollectionVelocity(point,Vector2.zero,1,new[]{a},1));
        }

        internal static PackedWaveField Wave(float phase) => new PackedWaveField(
            new Vector4(1,0,1,.25f),Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero,
            new Vector4(phase,0,0,0),Vector4.zero,new Vector4(1,1,.25f,0));
        private static ulong Sum(uint[] values) { ulong result=0; foreach(uint v in values) result+=v; return result; }
        private static void Sweep(uint[] m,uint[] f,int n,Vector2 courant,bool[] blocked)
        {
            for(int matching=0;matching<4;matching++)
            {
                int axis=matching/2, parity=matching%2;
                for(int y=0;y<n;y++) for(int x=0;x<n;x++)
                {
                    if(((axis==0?x:y)&1)!=parity) continue;
                    int bx=x+(axis==0?1:0), by=y+(axis==0?0:1);
                    if(bx>=n || by>=n) continue;
                    int a=y*n+x,b=by*n+bx;
                    if(blocked!=null && (blocked[a] || blocked[b])) continue;
                    FoamTransport.Exchange(m[a],f[a],m[b],f[b],axis==0?courant.x:courant.y,
                        out uint ma,out uint fa,out uint mb,out uint fb);
                    m[a]=ma; f[a]=fa; m[b]=mb; f[b]=fb;
                }
            }
        }
    }
}
