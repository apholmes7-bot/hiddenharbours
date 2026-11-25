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
    public class StPetersLandingPropsBakeTests
    {
        const string FixturePath="Assets/Tests/EditMode/RigBaking/Fixtures/stpLandingProps.node.json";
        [Serializable] public sealed class ExpectedFrame
        {public string key,rgbaSha256,heightSha256;public int cell,dir,width,height,pivotX,pivotY;}
        [Serializable] public sealed class ProtectedFile {public string path,sha256;public bool lf;}
        [Serializable] public sealed class Fixture {public string schema,node;public ExpectedFrame[] frames;public ProtectedFile[] protectedFiles;}
        static Fixture Read()=>JsonUtility.FromJson<Fixture>(File.ReadAllText(FixturePath));
        [Test] public void DedicatedSheetsMatchRealNode()
        {
            var fixture=Read();Assert.AreEqual(32,fixture.frames.Length);StringAssert.StartsWith("v",fixture.node);
            var rendered=StPetersLandingPropsBaker.RenderAll();
            foreach(var expected in fixture.frames)
            {
                var p=rendered.Single(x=>x.Key==expected.key);
                Assert.AreEqual(expected.rgbaSha256,StPetersLandingPropsBaker.Sha(p.Rgba[expected.cell]),expected.key+" cell "+expected.cell);
                if(!string.IsNullOrEmpty(expected.heightSha256))
                    Assert.AreEqual(expected.heightSha256,StPetersLandingPropsBaker.Sha(p.HeightRgba[expected.cell]),expected.key+" height "+expected.cell);
            }
        }
        [Test] public void DedicatedPivotsAndPixelsStayAtPpu32()
        {
            var fixture=Read();var c=SetPieceSheetSlicer.LoadContract(StPetersLandingPropsBaker.Sheets,out string error);
            Assert.IsNotNull(c,error);Assert.AreEqual(4,c.pieces.Count);
            foreach(var p in c.pieces)
            {
                var d=AssetDatabase.LoadAssetAtPath<SetPieceDef>(SetPieceDefBuilder.DefPath(p.key));
                Assert.IsNotNull(d);Assert.IsTrue(d.HasArt);Assert.AreEqual(32,d.PixelsPerMetre);Assert.AreEqual(8,d.Frames.Length);
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(SetPieceSheetSlicer.SheetPath(StPetersLandingPropsBaker.Sheets,p.key));
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
                    Assert.AreEqual(expected.rgbaSha256,StPetersLandingPropsBaker.Sha(bytes),p.key+" imported pixels "+f.cell);
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
                Assert.AreEqual(p.sha256,StPetersLandingPropsBaker.Sha(bytes),p.path);
            }
        }
        [Test] public void TheBoardTrapAndToteOptionsArePreserved()
        {
            var traps=AssetDatabase.LoadAssetAtPath<SetPieceDef>(SetPieceDefBuilder.DefPath("landingTraps"));
            var totes=AssetDatabase.LoadAssetAtPath<SetPieceDef>(SetPieceDefBuilder.DefPath("landingTotes"));
            Assert.AreEqual("prop.stp_landing_traps",traps.Id);Assert.AreEqual("prop.stp_landing_totes",totes.Id);
            StringAssert.Contains("\"variant\":0",traps.BakedOptionsJson);StringAssert.Contains("\"len\":0.5",traps.BakedOptionsJson);StringAssert.Contains("\"weather\":0.35",traps.BakedOptionsJson);
            Assert.AreEqual("{\"variant\":1}",totes.BakedOptionsJson);
        }
        [Test] public void LadderContractAndDrawnRungsAgree()
        {
            var d=AssetDatabase.LoadAssetAtPath<SetPieceDef>(SetPieceDefBuilder.DefPath("landingLadder"));
            Assert.AreEqual("prop.stp_landing_ladder",d.Id);Assert.IsTrue(d.TryGetAnchor("hanger",out var top));Assert.AreEqual(Vector3.zero,top);
            Assert.IsTrue(d.TryGetAnchor("foot",out var foot));Assert.AreEqual(new Vector3(0,.275f,-8.1f),foot);
            object data=DeckSidecarJson.Parse(d.GameplayJson);
            var rungs=DeckSidecarJson.AsArray(DeckSidecarJson.Member(data,"rungZ"));Assert.AreEqual(27,rungs.Count);
            for(int i=0;i<27;i++)Assert.That(Convert.ToDouble(rungs[i]),Is.EqualTo(-.3*(i+1)).Within(.000001));
            Assert.That(d.BoundsMin.z,Is.LessThan(-8.1f));Assert.That(d.BoundsMax.z,Is.EqualTo(0).Within(.000001));
        }
    }
}
