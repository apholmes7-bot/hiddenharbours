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
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    // The next-iteration art pass (2026-11-11): the shipped fisher and nan (rig 10.3) beside their
    // proposal skins (rig 11.0, Data/Characters/SkinNext), in the game's own view on the St Peters
    // landing, at dawn, noon and dusk, standing and mid-stride. A plate, not a gate: it writes PNGs
    // under Evidence~ (never committed) and asserts only that all four figures drew from the defs it
    // names. Temporary figures only; no scene, placement or production consumer is changed.
    public partial class WharfBuildingPass2PlatePlayTests
    {
#if UNITY_EDITOR
        [UnityTest] public IEnumerator StPeters_NextIterationFigures_ShippedBesideProposed()
        {
            RequireAGraphicsDevice(); yield return LoadRegion(StPeters); EnsureTheFollow(); HoldTheMood();
            string folder = Path.GetFullPath("Evidence~/art-next/plates/engine"); Directory.CreateDirectory(folder);
            string numbers = Path.Combine(folder, "figures-numbers.txt"); File.WriteAllText(numbers, "");
            var held = GameServices.Environment;

            string[] paths =
            {
                "Assets/_Project/Data/Characters/Skin/fisher.asset",
                "Assets/_Project/Data/Characters/SkinNext/fisher_rig11.asset",
                "Assets/_Project/Data/Characters/Skin/nan.asset",
                "Assets/_Project/Data/Characters/SkinNext/nan_rig11.asset",
            };
            string[] revisions = { "10.3", "11.0", "10.3", "11.0" };
            var skins = paths.Select(p => AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(p)).ToArray();
            for (int i = 0; i < skins.Length; i++)
            {
                Assert.IsNotNull(skins[i], paths[i]);
                Assert.AreEqual(revisions[i], skins[i].SourceRigRevision, paths[i]);
            }

            var tyres = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                .Where(x => x.name == "Fitting_tyre" && x.gameObject.scene.name == StPeters)
                .OrderBy(x => x.transform.position.x).ToArray();
            Assert.AreEqual(2, tyres.Length);
            float inset = HiddenHarbours.App.Editor.StPetersLandingLayout.StandingInset;
            Vector3 mid = (tyres[0].transform.position + tyres[1].transform.position) / 2;
            const float spacing = 1.2f, figureHeight = 1.8f;

            var figures = new IsoCharacterFigureRenderer[skins.Length];
            void Save(string name)
            {
                var image = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
                try { image.LoadRawTextureData(Capture()); image.Apply(); File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG()); }
                finally { Object.DestroyImmediate(image); }
            }
            try
            {
                for (int i = 0; i < skins.Length; i++)
                {
                    var go = new GameObject("NextIterationFigure_" + skins[i].Id); _spawned.Add(go);
                    go.transform.position = new Vector3(mid.x + (i - 1.5f) * spacing, mid.y + inset, 0);
                    var renderer = go.AddComponent<SpriteRenderer>(); renderer.sortingOrder = tyres[0].sortingOrder + 1;
                    figures[i] = go.AddComponent<IsoCharacterFigureRenderer>(); figures[i].Configure(skins[i]);
                    Assert.IsTrue(figures[i].EnterAshore(renderer), skins[i].Id + " enters ashore");
                    File.AppendAllText(numbers, $"{skins[i].Id} rig={skins[i].SourceRigRevision} at={go.transform.position:R} key={skins[i].KeyScreen:R} reach={skins[i].ReachPx:R}\n");
                }
                Vector2 centre = new Vector2(mid.x, mid.y + inset + figureHeight / 2);
                yield return LandingPropsTide(held, 1.0f);
                foreach (var board in new[] { new { Name = "dawn-0630", Hour = 6.5f }, new { Name = "noon-1230", Hour = 12.5f }, new { Name = "golden-1745", Hour = 17.75f } })
                {
                    yield return PinTheHour(board.Hour, ClearRunSeconds);
                    foreach (var pose in new[] { new { Name = "standing", State = "idle", Frame = 0, Yaw = 180f }, new { Name = "walking", State = "walk", Frame = 2, Yaw = 135f } })
                    {
                        for (int i = 0; i < figures.Length; i++)
                        {
                            Assert.IsTrue(figures[i].SetPose(pose.State, pose.Frame), skins[i].Id + " " + pose.State);
                            figures[i].SetAshoreYaw(pose.Yaw);
                        }
                        yield return ShadersReady(); LandingPropsCamera(centre); yield return null; yield return null;
                        Save(board.Name + "-" + pose.Name + "-game");
                        LandingPropsCamera(centre, figureHeight * 2.4f); yield return null; yield return null;
                        Save(board.Name + "-" + pose.Name + "-detail");
                        File.AppendAllText(numbers, $"{board.Name}-{pose.Name} hour={GameServices.Clock.HourOfDay:R} state={pose.State} frame={pose.Frame} yaw={pose.Yaw:R} size={_w}x{_h} order=fisher10.3,fisher11.0,nan10.3,nan11.0\n");
                    }
                }
                Assert.IsTrue(HiddenHarbours.Core.ShellFlow.AtTitle);
            }
            finally
            {
                foreach (var figure in figures) if (figure != null) figure.LeaveAshore();
                GameServices.Environment = held; LandingPropsReleaseCamera();
            }
        }
#endif
    }
}
