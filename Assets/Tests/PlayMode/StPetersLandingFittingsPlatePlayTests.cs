using System;
using System.Collections;
using System.IO;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object=UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    public partial class WharfBuildingPass2PlatePlayTests
    {
#if UNITY_EDITOR
        [Serializable] sealed class LandingFittingMap { public string key,path; public float minimumLocalZ,maximumLocalZ; }
        [Serializable] sealed class LandingFittingHeight { public LandingFittingMap[] maps; }
        [Serializable] sealed class LandingFittingOptions { public float top; }

        [UnityTest] public IEnumerator StPeters_LandingFittings_BeforeAndAfterAtBothBoards()
        {
            RequireAGraphicsDevice();yield return LoadRegion(StPeters);EnsureTheFollow();HoldTheMood();
            const float figureHeight=1.8f;
            string folder=Path.GetFullPath("Evidence~/stp-props/phase-d/plates");Directory.CreateDirectory(folder);
            string numbers=Path.Combine(folder,"fittings-numbers.txt");File.WriteAllText(numbers,"");
            var held=GameServices.Environment;
            var tyres=Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                .Where(x=>x.name=="Fitting_tyre"&&x.gameObject.scene.name==StPeters).OrderBy(x=>x.transform.position.x).ToArray();
            Assert.AreEqual(2,tyres.Length);
            var originals=new[]{tyres[0],tyres[1],GameObject.Find("prop.stp_landing_rope").GetComponent<SpriteRenderer>()};
            var keys=new[]{"landingTyreWest","landingTyreEast","landingRope"};
            var ids=new[]{"prop.stp_landing_tyre_west","prop.stp_landing_tyre_east","prop.stp_landing_rope"};
            var positions=originals.Select(x=>x.transform.position).ToArray();
            var sprites=originals.Select(x=>x.sprite).ToArray();var materials=originals.Select(x=>x.sharedMaterial).ToArray();
            var defs=keys.Select(k=>AssetDatabase.LoadAssetAtPath<SetPieceDef>("Assets/_Project/Data/SetPieces/SetPiece_"+k+".asset")).ToArray();
            var maps=JsonUtility.FromJson<LandingFittingHeight>(File.ReadAllText("Assets/_Project/Art/Sprites/StPeters/LandingFittings/stpLandingFittings.height-contract.json"));
            var skin=AssetDatabase.LoadAssetAtPath<CharacterSkinDef>("Assets/_Project/Data/Characters/Skin/fisher.asset");Assert.AreEqual("10.3",skin.SourceRigRevision);
            var figures=new IsoCharacterFigureRenderer[3];var waters=new TidalFaceWaterline[2];
            void RestorePictures()
            {
                for(int i=0;i<waters.Length;i++)if(waters[i]!=null){Object.DestroyImmediate(waters[i]);waters[i]=null;}
                for(int i=0;i<originals.Length;i++){originals[i].sprite=sprites[i];originals[i].sharedMaterial=materials[i];}
            }
            void Save(string name)
            {
                var image=new Texture2D(_w,_h,TextureFormat.RGBA32,false);
                try{image.LoadRawTextureData(Capture());image.Apply();File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());}
                finally{Object.DestroyImmediate(image);}
            }
            try
            {
                for(int i=0;i<3;i++)
                {
                    Assert.IsNotNull(defs[i],keys[i]);Assert.AreEqual(ids[i],defs[i].Id);
                    var go=new GameObject("LandingFittingsRig10Figure_"+keys[i]);_spawned.Add(go);
                    float inset=HiddenHarbours.App.Editor.StPetersLandingLayout.StandingInset;
                    go.transform.position=positions[i]+new Vector3(defs[i].BoundsMax.x-defs[i].BoundsMin.x+inset,inset,0);
                    var renderer=go.AddComponent<SpriteRenderer>();renderer.sortingOrder=originals[i].sortingOrder+1;
                    figures[i]=go.AddComponent<IsoCharacterFigureRenderer>();figures[i].Configure(skin);
                    Assert.IsTrue(figures[i].EnterAshore(renderer));Assert.IsTrue(figures[i].SetPose("idle",0));figures[i].SetAshoreYaw(180);
                    File.AppendAllText(numbers,$"{keys[i]} id={ids[i]} existingAnchor={positions[i]:R} figureRig={skin.SourceRigRevision} figureAt={go.transform.position:R} boundsMin={defs[i].BoundsMin:R} boundsMax={defs[i].BoundsMax:R}\n");
                }
                foreach(var board in new[]{new{Id="landing.wide_approach.1431",Hour=14+31f/60,Tide=2.18f,At=new Vector2(203,2.649f),West=false},
                    new{Id="cannery.wide_landing.0600",Hour=6f,Tide=-.85f,At=new Vector2(181,12.596f),West=true}})
                {
                    yield return PinTheHour(board.Hour,ClearRunSeconds);yield return LandingPropsTide(held,board.Tide);
                    foreach(bool after in new[]{false,true})
                    {
                        RestorePictures();
                        if(after)for(int i=0;i<3;i++)
                        {
                            // Temporary art proof only. No scene, placement or production consumer is changed.
                            originals[i].sprite=defs[i].SpriteFor(SetPieceDef.FrameForRigDir(4));
                            Assert.AreNotSame(sprites[i],originals[i].sprite,keys[i]);
                            if(i<2)
                            {
                                var m=maps.maps.Single(x=>x.key==keys[i]);var options=JsonUtility.FromJson<LandingFittingOptions>(defs[i].BakedOptionsJson);
                                waters[i]=originals[i].gameObject.AddComponent<TidalFaceWaterline>();_spawned.Add(waters[i]);
                                waters[i].Configure(positions[i].y,options.top);
                                waters[i].ConfigureHeight(AssetDatabase.LoadAssetAtPath<Texture2D>(m.path),m.minimumLocalZ,m.maximumLocalZ-m.minimumLocalZ,-options.top);
                            }
                        }
                        yield return ShadersReady();LandingPropsCamera(board.At);yield return null;
                        string state=after?"after":"before";Save(board.Id+"-"+state);
                        Assert.AreEqual(1,GameServices.Clock.DayIndex);
                        File.AppendAllText(numbers,$"{board.Id}-{state} day=1 hour={GameServices.Clock.HourOfDay:R} tide={board.Tide:R} camera={_cam.transform.position:R} worldHeight=14 size=796x448\n");
                        for(int i=0;i<3;i++)
                        {
                            Assert.AreEqual(positions[i],originals[i].transform.position,keys[i]+" anchor unchanged");
                            LandingPropsCamera(board.At);
                            var projected=_cam.WorldToViewportPoint(positions[i]);
                            File.AppendAllText(numbers,$"{board.Id}-{state} {keys[i]} anchorViewport={projected:R} sprite={originals[i].sprite.name}\n");
                            if(board.West!=(i==0))continue;
                            Vector3 centre=(positions[i]+figures[i].transform.position)/2;
                            LandingPropsCamera(new Vector2(centre.x,positions[i].y+figureHeight/2),figureHeight*2);
                            yield return null;Save(board.Id+"-"+keys[i]+"-detail-"+state);
                        }
                    }
                }
                Assert.IsTrue(HiddenHarbours.Core.ShellFlow.AtTitle);
            }
            finally
            {
                RestorePictures();
                for(int i=0;i<originals.Length;i++)
                {
                    Assert.AreSame(sprites[i],originals[i].sprite,keys[i]+" sprite restored");
                    Assert.AreSame(materials[i],originals[i].sharedMaterial,keys[i]+" material restored");
                    Assert.AreEqual(positions[i],originals[i].transform.position,keys[i]+" anchor restored");
                    File.AppendAllText(numbers,$"RESTORED {keys[i]} sprite={originals[i].sprite.name} material={originals[i].sharedMaterial.name} anchor={originals[i].transform.position:R}\n");
                }
                foreach(var figure in figures)if(figure!=null)figure.LeaveAshore();
                GameServices.Environment=held;LandingPropsReleaseCamera();
            }
        }
#endif
    }
}
