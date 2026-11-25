using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking.EditMode
{
    public class StPetersLandingFittingsBakeTests
    {
        const string FixturePath="Assets/Tests/EditMode/RigBaking/Fixtures/stpLandingFittings.node.json";
        [Serializable] public sealed class ExpectedFrame
        {public string key,rgbaSha256,heightSha256;public int cell,dir,width,height,pivotX,pivotY;}
        [Serializable] public sealed class ProtectedFile {public string path,sha256;public bool lf;}
        [Serializable] public sealed class Fixture {public string schema,node;public ExpectedFrame[] frames;public ProtectedFile[] protectedFiles;}
        static Fixture Read()=>JsonUtility.FromJson<Fixture>(File.ReadAllText(FixturePath));
        [Test] public void DedicatedSheetsMatchRealNode()
        {
            var fixture=Read();Assert.AreEqual(24,fixture.frames.Length);StringAssert.StartsWith("v",fixture.node);
            var rendered=StPetersLandingFittingsBaker.RenderAll();
            foreach(var expected in fixture.frames)
            {
                var p=rendered.Single(x=>x.Key==expected.key);
                Assert.AreEqual(expected.rgbaSha256,StPetersLandingFittingsBaker.Sha(p.Rgba[expected.cell]),expected.key+" cell "+expected.cell);
                if(!string.IsNullOrEmpty(expected.heightSha256))
                    Assert.AreEqual(expected.heightSha256,StPetersLandingFittingsBaker.Sha(p.HeightRgba[expected.cell]),expected.key+" height "+expected.cell);
            }
        }
        [Test] public void DedicatedPivotsAndPixelsStayAtPpu32()
        {
            var fixture=Read();var c=SetPieceSheetSlicer.LoadContract(StPetersLandingFittingsBaker.Sheets,out string error);
            Assert.IsNotNull(c,error);Assert.AreEqual(3,c.pieces.Count);
            foreach(var p in c.pieces)
            {
                var d=AssetDatabase.LoadAssetAtPath<SetPieceDef>(SetPieceDefBuilder.DefPath(p.key));
                Assert.IsNotNull(d);Assert.IsTrue(d.HasArt);Assert.AreEqual(32,d.PixelsPerMetre);Assert.AreEqual(8,d.Frames.Length);
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(SetPieceSheetSlicer.SheetPath(StPetersLandingFittingsBaker.Sheets,p.key));
                var pixels=texture.GetPixels32();
                foreach(var f in p.frames)
                {
                    var expected=fixture.frames.Single(x=>x.key==p.key&&x.cell==f.cell);
                    var bytes=new byte[f.nativeW*f.nativeH*4];
                    for(int y=0;y<p.cellH;y++)for(int x=0;x<p.cellW;x++)
                    {
                        var color=pixels[(p.sheetH-1-y)*p.sheetW+f.cell*p.cellW+x];
                        // The shared sprite importer dilates invisible RGB; alpha-zero has no painted colour.
                        if(color.a==0)color=new Color32(0,0,0,0);
                        int offset=((f.cropY+y)*f.nativeW+f.cropX+x)*4;
                        bytes[offset]=color.r;bytes[offset+1]=color.g;bytes[offset+2]=color.b;bytes[offset+3]=color.a;
                    }
                    Assert.AreEqual(expected.rgbaSha256,StPetersLandingFittingsBaker.Sha(bytes),p.key+" imported pixels "+f.cell);
                    Assert.AreEqual(new Vector2(f.pivotX,p.cellH-f.pivotY),d.Frames[f.cell].Sprite.pivot);
                    Assert.AreEqual(expected.dir,d.Frames[f.cell].RigDir);
                }
            }
        }
        [Test] public void SharedSheetsAndOtherRegionRemainByteIdentical()
        {
            foreach(var p in Read().protectedFiles)
            {
                byte[] bytes=p.lf?Encoding.UTF8.GetBytes(File.ReadAllText(p.path).Replace("\r\n","\n")):File.ReadAllBytes(p.path);
                Assert.AreEqual(p.sha256,StPetersLandingFittingsBaker.Sha(bytes),p.path);
            }
        }
        [Test] public void ExactlyThreeMetricFittingsKeepTheirIdsAndContacts()
        {
            foreach(var pair in new[]{new[]{"landingTyreWest","prop.stp_landing_tyre_west"},new[]{"landingTyreEast","prop.stp_landing_tyre_east"},new[]{"landingRope","prop.stp_landing_rope"}})
            {
                var d=AssetDatabase.LoadAssetAtPath<SetPieceDef>(SetPieceDefBuilder.DefPath(pair[0]));Assert.IsNotNull(d);Assert.AreEqual(pair[1],d.Id);
                var c=DeckSidecarJson.Parse(d.GameplayJson);
                if(pair[0].StartsWith("landingTyre",StringComparison.Ordinal))
                {
                    Assert.AreEqual(.6,Convert.ToDouble(DeckSidecarJson.Member(c,"outsideDiameter")));
                    Assert.AreEqual(.2,Convert.ToDouble(DeckSidecarJson.Member(c,"sectionDepth")));
                    Assert.That(d.BoundsMax.x-d.BoundsMin.x,Is.EqualTo(.6).Within(.000001));
                    Assert.That(d.BoundsMax.y-d.BoundsMin.y,Is.InRange(.2,.2*1.15));
                    Assert.IsTrue(d.TryGetAnchor("hanger",out var hanger));Assert.AreEqual(Vector3.zero,hanger);
                }
                else
                {
                    Assert.AreEqual(.025,Convert.ToDouble(DeckSidecarJson.Member(c,"lineDiameter")));
                    Assert.AreEqual(.025,Convert.ToDouble(DeckSidecarJson.Member(c,"tailDiameter")));
                    Assert.AreEqual(3,Convert.ToInt32(DeckSidecarJson.Member(c,"layers")));
                    Assert.That(d.BoundsMax.z-d.BoundsMin.z,Is.InRange(.075*.85,.075*1.15));
                    var tail=DeckSidecarJson.AsArray(DeckSidecarJson.Member(c,"looseTail"));Assert.Greater(tail.Count,1);
                }
            }
        }
    }
}
