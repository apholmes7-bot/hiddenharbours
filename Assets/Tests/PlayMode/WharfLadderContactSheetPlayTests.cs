using System.Collections;
using System.Collections.Generic;
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
    /// A contact sheet of every wharf ladder in a harbour at spring low and spring high water, with the
    /// fisher standing on the deck beside each one for scale. St Peters is shot twice: as shipped, and
    /// with the kit's old 0.63 m ladder slice put back in place, so the two read side by side.
    /// </summary>
    public partial class WharfBuildingPass2PlatePlayTests
    {
#if UNITY_EDITOR
        static string LadderSheetPlates => Path.GetFullPath("Evidence~/ladders/plates");
        const string KitOverlaysPath = "Assets/_Project/Art/Tilesets/Wharf/WharfOverlays.png";
        const long KitLadderSliceId = 159253323;
        const string KitSpriteMaterialGuid = "a97c105638bdf8b4a8650670310a4cd3";
        const float LadderShotWorldHeight = 11f;

        static float FigureTop(GameObject figure) =>
            figure.GetComponentsInChildren<Renderer>().Where(r => r.enabled).Select(r => r.bounds.max.y).DefaultIfEmpty(float.NaN).Max();

        void LadderSheetSave(string name)
        {
            Directory.CreateDirectory(LadderSheetPlates);
            var texture = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
            try
            {
                texture.LoadRawTextureData(Capture()); texture.Apply();
                File.WriteAllBytes(Path.Combine(LadderSheetPlates, name + ".png"), texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator StPeters_EveryLadder_AtSpringLowAndHigh_BeforeAndAfter()
        {
            yield return LadderSheet(StPeters, true);
        }

        IEnumerator LadderSheet(string region, bool beforeAndAfter)
        {
            RequireAGraphicsDevice(); yield return LoadRegion(region); EnsureTheFollow(); HoldTheMood();
            var held = GameServices.Environment; yield return PinTheHour(12, ClearRunSeconds);
            TideProfile tide = held.ActiveTideProfile;
            float low = WharfLadderReach.LowestWater(tide), high = WharfLadderReach.HighestWater(tide);
            var ladders = Object.FindObjectsByType<WharfLadder>(FindObjectsSortMode.None)
                                .Where(l => l.gameObject.scene.name == region).OrderBy(l => l.transform.position.x).ToArray();
            Assert.IsNotEmpty(ladders, region + " carries ladders");

            string numbers = Path.Combine(LadderSheetPlates, region + "-numbers.txt");
            Directory.CreateDirectory(LadderSheetPlates); File.WriteAllText(numbers, "");

            var skin = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>("Assets/_Project/Data/Characters/Skin/fisher.asset");
            var figureObject = new GameObject("LadderSheetScaleFigure"); _spawned.Add(figureObject);
            var sort = figureObject.AddComponent<SpriteRenderer>();
            var figure = figureObject.AddComponent<IsoCharacterFigureRenderer>(); figure.Configure(skin);
            Assert.IsTrue(figure.EnterAshore(sort)); Assert.IsTrue(figure.SetPose("idle", 0)); figure.SetAshoreYaw(180);

            // The visible pictures standing on each ladder's plan point (the kit slice, the full art, a span).
            var pictures = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                                 .Where(r => r.gameObject.scene.name == region && r.name.StartsWith("Fitting_ladder")).ToArray();
            foreach (var p in pictures)
                File.AppendAllText(numbers, $"picture {p.name} pos={p.transform.position:R} sprite={(p.sprite != null ? p.sprite.name : "none")} " +
                                            $"size={(p.sprite != null ? p.sprite.bounds.size : Vector3.zero):R} order={p.sortingOrder} mat={p.sharedMaterial?.name}\n");

            Sprite kitSlice = AssetDatabase.LoadAllAssetsAtPath(KitOverlaysPath).OfType<Sprite>().FirstOrDefault(s =>
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(s, out string _, out long id) && id == KitLadderSliceId);
            var kitMaterial = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(KitSpriteMaterialGuid));

            var stages = beforeAndAfter ? new[] { "before", "after" } : new[] { "now" };
            try
            {
                foreach (string stage in stages)
                {
                    var restore = new List<System.Action>();
                    if (stage == "before")
                    {
                        Assert.IsNotNull(kitSlice, "the kit's ladder slice");
                        foreach (var p in pictures)
                        {
                            var face = p.GetComponent<TidalFaceWaterline>();
                            Sprite s = p.sprite; Material m = p.sharedMaterial; Vector2 size = p.size;
                            bool faceOn = face != null && face.enabled;
                            if (face != null) face.enabled = false;
                            var block = new MaterialPropertyBlock(); p.GetPropertyBlock(block);
                            p.SetPropertyBlock(null);
                            p.sprite = kitSlice; if (kitMaterial != null) p.sharedMaterial = kitMaterial;
                            restore.Add(() => { p.sprite = s; p.sharedMaterial = m; p.size = size; p.SetPropertyBlock(block); if (face != null) face.enabled = faceOn; });
                        }
                    }
                    foreach (float level in new[] { low, high })
                    {
                        yield return LandingPropsTide(held, level);
                        string tideName = level < 0 ? "low" : "high";
                        foreach (var ladder in ladders)
                        {
                            Vector3 at = ladder.transform.position;
                            figureObject.transform.position = at + new Vector3(-0.9f, 0.45f, 0f);
                            sort.sortingOrder = 30000;
                            LandingPropsCamera(new Vector2(at.x, at.y - 2.6f), LadderShotWorldHeight);
                            yield return ShadersReady(); yield return null;
                            string id = $"{region}-{ladder.Id}-{tideName}-{stage}";
                            LadderSheetSave(id);
                            var picture = pictures.Where(p => Vector2.Distance(p.transform.position, at) < 0.05f).Select(p => p.bounds).ToArray();
                            var span = ladder.transform.Find("Ladder span")?.GetComponent<SpriteRenderer>();
                            float drawnFoot = picture.Length > 0 ? picture.Min(b => b.min.y) : float.NaN;
                            if (span != null && span.enabled) drawnFoot = Mathf.Min(float.IsNaN(drawnFoot) ? float.MaxValue : drawnFoot, span.bounds.min.y);
                            float waterY = at.y + TidalRide.ScreenRise(level, ladder.TopElevationMeters);
                            File.AppendAllText(numbers, $"{id} pos={at:R} top={ladder.TopElevationMeters:R} length={ladder.LengthMetres:R} " +
                                $"foot={ladder.FootElevationMeters:R} kind={(ladder.Kind != null ? ladder.Kind.Id : "none")} rung={ladder.RungMetres:R} " +
                                $"climbTop={ladder.ClimbTopScreenOffset:R} span={(span != null && span.enabled ? span.bounds.size.y.ToString("R") : "none")} " +
                                $"tide={level:R} waterlineY={waterY:R} drawnFootY={drawnFoot:R} reachesWater={(drawnFoot <= waterY)} " +
                                $"figureTopY={FigureTop(figureObject):R} figureFootY={figureObject.transform.position.y:R}\n");
                        }
                    }
                    foreach (var r in restore) r();
                }
            }
            finally { figure.LeaveAshore(); GameServices.Environment = held; LandingPropsReleaseCamera(); }
        }
#endif
    }
}
