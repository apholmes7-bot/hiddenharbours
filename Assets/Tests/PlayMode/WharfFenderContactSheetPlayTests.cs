using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Art;
using HiddenHarbours.World;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// A contact sheet of every wharf face that boats berth against, at spring low and spring high water:
    /// the fender strings down each face and the ladders, with the fisher standing beside each ladder for
    /// scale. Evidence only (it needs a GPU and skips on CI); the folder comes from
    /// <c>HH_FENDER_PLATES</c> so a before and an after run land side by side.
    /// </summary>
    public partial class WharfBuildingPass2PlatePlayTests
    {
#if UNITY_EDITOR
        const string NineMileCreekScene = "NineMileCreek";
        const float FenderShotWorldHeight = 14f;

        static string FenderPlates
        {
            get
            {
                string dir = System.Environment.GetEnvironmentVariable("HH_FENDER_PLATES");
                if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.temporaryCachePath, "fender-plates");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }

        void FenderSave(string name)
        {
            var texture = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
            try
            {
                texture.LoadRawTextureData(Capture()); texture.Apply();
                File.WriteAllBytes(Path.Combine(FenderPlates, name + ".png"), texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator StPeters_EveryFenderFaceAndLadder_AtSpringLowAndHigh()
        {
            yield return FenderSheet(StPeters);
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator NineMileCreek_EveryFenderFaceAndLadder_AtSpringLowAndHigh()
        {
            yield return FenderSheet(NineMileCreekScene);
        }

        IEnumerator FenderSheet(string region)
        {
            RequireAGraphicsDevice();
            // Nine Mile Creek is reached the way play reaches it, St Peters first (see CliffPxLookPlatePlayTests).
            if (region != StPeters) yield return LoadRegion(StPeters);
            yield return LoadRegion(region); EnsureTheFollow(); HoldTheMood();
            var held = GameServices.Environment; yield return PinTheHour(12, ClearRunSeconds);
            TideProfile tide = held.ActiveTideProfile;
            float low = WharfLadderReach.LowestWater(tide), high = WharfLadderReach.HighestWater(tide);
            string numbers = Path.Combine(FenderPlates, region + "-numbers.txt");
            File.WriteAllText(numbers, $"tide mean={tide.MeanLevel:R} amplitude={tide.Amplitude:R}\n");

            var ladders = Object.FindObjectsByType<WharfLadder>(FindObjectsSortMode.None)
                                .Where(l => l.gameObject.scene.name == region).OrderBy(l => l.transform.position.x).ToArray();
            var fenders = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                                .Where(r => r.gameObject.scene.name == region &&
                                            (r.name.StartsWith("Fitting_tyre", StringComparison.Ordinal) ||
                                             r.name.StartsWith("Fender_", StringComparison.Ordinal)))
                                .OrderBy(r => r.transform.position.x).ToArray();
            foreach (var f in fenders)
                File.AppendAllText(numbers, $"fender {f.name} pos={f.transform.position:R} sprite={(f.sprite != null ? f.sprite.name : "none")} " +
                                            $"size={(f.sprite != null ? f.sprite.bounds.size : Vector3.zero):R} order={f.sortingOrder}\n");
            foreach (var l in ladders)
                File.AppendAllText(numbers, $"ladder {l.Id} pos={l.transform.position:R} top={l.TopElevationMeters:R} length={l.LengthMetres:R} " +
                                            $"kind={(l.Kind != null ? l.Kind.Id : "none")} climbTop={l.ClimbTopScreenOffset:R}\n");

            // A boat alongside: St Peters' dory is laid on the south face abreast of the fenders.
            if (region == StPeters)
            {
                GameObject dory = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .Where(t => t.parent == null && t.name == "Dory" && t.gameObject.scene.name == region)
                    .Select(t => t.gameObject).FirstOrDefault();
                if (dory != null)
                {
                    float faceY = ladders.Length > 0 ? ladders[0].transform.position.y : dory.transform.position.y;
                    dory.transform.position = new Vector3(207.4f, faceY - 1.6f, dory.transform.position.z);
                    File.AppendAllText(numbers, $"dory laid alongside at {dory.transform.position:R}\n");
                }
            }

            // The faces: every fender x, in shots one camera-width apart along each face.
            float width = FenderShotWorldHeight * (24.89f / 14f);
            var faceYs = fenders.Select(f => Mathf.Round(f.transform.position.y * 4f) / 4f)
                                .Concat(ladders.Select(l => Mathf.Round(l.transform.position.y * 4f) / 4f)).Distinct().ToArray();
            var shots = new System.Collections.Generic.List<(string id, Vector2 centre)>();
            foreach (float y in faceYs)
            {
                var xs = fenders.Where(f => Mathf.Abs(f.transform.position.y - y) < 0.5f).Select(f => f.transform.position.x)
                                .Concat(ladders.Where(l => Mathf.Abs(l.transform.position.y - y) < 0.5f).Select(l => l.transform.position.x))
                                .OrderBy(x => x).ToArray();
                if (xs.Length == 0) continue;
                float from = xs.First(), to = xs.Last();
                int count = Mathf.Max(1, Mathf.CeilToInt((to - from + 2f) / (width * 0.9f)));
                for (int i = 0; i < count; i++)
                {
                    float cx = count == 1 ? (from + to) * 0.5f : Mathf.Lerp(from + width * 0.4f, to - width * 0.4f, i / (float)(count - 1));
                    shots.Add(($"face-y{y:0.##}-{i}", new Vector2(cx, y - 3.2f)));
                }
            }

            var skin = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>("Assets/_Project/Data/Characters/Skin/fisher.asset");
            var figureObject = new GameObject("FenderSheetScaleFigure"); _spawned.Add(figureObject);
            var sort = figureObject.AddComponent<SpriteRenderer>();
            var figure = figureObject.AddComponent<IsoCharacterFigureRenderer>(); figure.Configure(skin);
            Assert.IsTrue(figure.EnterAshore(sort)); Assert.IsTrue(figure.SetPose("idle", 0)); figure.SetAshoreYaw(180);
            figureObject.SetActive(false);
            try
            {
                foreach (float level in new[] { low, high })
                {
                    yield return LandingPropsTide(held, level);
                    string tideName = level < 0 ? "low" : "high";
                    figureObject.SetActive(false);
                    foreach (var shot in shots)
                    {
                        LandingPropsCamera(shot.centre, FenderShotWorldHeight);
                        yield return ShadersReady(); yield return null;
                        FenderSave($"{region}-{shot.id}-{tideName}");
                    }
                    figureObject.SetActive(true);
                    foreach (var ladder in ladders)
                    {
                        // The figure stands on the deck beside the ladder's cap: the deck lies away from the
                        // water the ladder faces (heading 180 = water to the south, deck to the north).
                        Vector3 at = ladder.transform.position;
                        float deckSide = Mathf.Cos(ladder.FaceHeadingDegrees * Mathf.Deg2Rad) > 0f ? -1f : 1f;
                        figureObject.transform.position = at + new Vector3(-1.0f, ladder.ClimbTopScreenOffset + deckSide * 0.5f, 0f);
                        sort.sortingOrder = 30000;
                        LandingPropsCamera(new Vector2(at.x, at.y - 0.6f), 11f);
                        yield return ShadersReady(); yield return null;
                        FenderSave($"{region}-ladder-{ladder.Id}-{tideName}");
                    }
                }
            }
            finally { figure.LeaveAshore(); GameServices.Environment = held; LandingPropsReleaseCamera(); }
        }
#endif
    }
}
