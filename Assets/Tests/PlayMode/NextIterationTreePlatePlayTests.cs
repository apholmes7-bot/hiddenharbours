using System;
using System.Collections;
using System.Collections.Generic;
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
    // The next-iteration art pass (2026-11-11): St Peters' own red maples, red spruces and white cedars as
    // the game draws them (tree rig 4), then the same trees at the same roots drawn from the proposal bake
    // (tree rig 5, Art/Foliage/TreesNext), at dawn, noon and the golden hour. A plate, not a gate: PNGs
    // under Evidence~ (never committed). The scene's trees are switched off and back on, never edited; the
    // proposal trees are temporary objects on the same material, with the same components.
    public partial class WharfBuildingPass2PlatePlayTests
    {
#if UNITY_EDITOR
        [Serializable] sealed class NextTreeWind { public float bendPx, limbPx, bob, flutter; public float[] shimmer; public bool conifer; }
        [Serializable] sealed class NextTreeSeason { public string season; public int paletteRow; }
        [Serializable] sealed class NextTreeEntry
        {
            public string species, stage; public int cellW, cellH, sheetW, sheetH; public float trunkAnchor;
            public NextTreeWind wind; public NextTreeSeason[] seasonRows;
        }
        [Serializable] sealed class NextTreeContract { public string rig; public NextTreeEntry[] trees; }

        [UnityTest] public IEnumerator StPeters_NextIterationTrees_ShippedThenProposed()
        {
            RequireAGraphicsDevice(); yield return LoadRegion(StPeters); EnsureTheFollow(); HoldTheMood();
            const string next = "Assets/_Project/Art/Foliage/TreesNext/";
            string folder = Path.GetFullPath("Evidence~/art-next/plates/engine"); Directory.CreateDirectory(folder);
            string numbers = Path.Combine(folder, "trees-numbers.txt"); File.WriteAllText(numbers, "");
            var contract = JsonUtility.FromJson<NextTreeContract>(File.ReadAllText(next + "Trees.json"));
            Assert.AreEqual("docs/art/rigs/treeIsoRig5.js", contract.rig);
            var palette = AssetDatabase.LoadAssetAtPath<Texture2D>(next + "TreePalette.png");
            Assert.IsNotNull(palette, "the proposal's palette");

            var originals = new List<SpriteRenderer>(); var proposed = new List<GameObject>();
            var subjects = new Dictionary<string, Vector3>();
            void Save(string name)
            {
                var image = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
                try { image.LoadRawTextureData(Capture()); image.Apply(); File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG()); }
                finally { Object.DestroyImmediate(image); }
            }
            try
            {
                foreach (NextTreeEntry e in contract.trees)
                {
                    string stem = e.species + "_" + e.stage + "_summer";
                    Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(next + stem + ".png").OfType<Sprite>().ToArray();
                    Assert.IsNotEmpty(sprites, stem + " is sliced");
                    var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(next + stem + "_mask.png");
                    var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(next + stem + "_normal.png");
                    NextTreeSeason row = e.seasonRows.Single(r => r.season == "summer");
                    var maps = new TreeWindMaps
                    {
                        Wind = AssetDatabase.LoadAssetAtPath<Texture2D>(next + stem + "_wind.png"),
                        Phase = AssetDatabase.LoadAssetAtPath<Texture2D>(next + stem + "_phase.png"),
                        Snow = AssetDatabase.LoadAssetAtPath<Texture2D>(next + stem + "_snow.png"),
                        Palette = palette,
                        BendPx = e.wind.bendPx, LimbPx = e.wind.limbPx, Bob = e.wind.bob, Flutter = e.wind.flutter,
                        ShimmerCalm = e.wind.shimmer != null && e.wind.shimmer.Length == 2 ? e.wind.shimmer[0] * e.wind.shimmer[1] : 0f,
                        Conifer = e.wind.conifer, PaletteRow = row.paletteRow,
                        CellW = e.cellW, CellH = e.cellH, SheetW = e.sheetW, SheetH = e.sheetH,
                    };
                    Assert.IsNotNull(mask, stem + " mask"); Assert.IsNotNull(normal, stem + " normal");
                    Assert.IsTrue(maps.IsComplete, stem + " wind maps");

                    SpriteRenderer[] scene = Object.FindObjectsByType<TreeTrunkAnchor>(FindObjectsSortMode.None)
                        .Where(a => a.gameObject.scene.name == StPeters && a.gameObject.activeInHierarchy)
                        .Select(a => a.GetComponent<SpriteRenderer>())
                        .Where(r => r != null && r.sprite != null && r.sprite.name.StartsWith(e.species + "_", StringComparison.Ordinal))
                        .OrderBy(r => r.transform.position.y).ThenBy(r => r.transform.position.x).ToArray();
                    Assert.IsNotEmpty(scene, "St Peters plants " + e.species);
                    subjects[e.species] = scene[0].transform.position;
                    foreach (SpriteRenderer o in scene)
                    {
                        string name = o.sprite.name; int at = name.LastIndexOf("_v", StringComparison.Ordinal);
                        string variant = at >= 0 ? name.Substring(at) : "_v0";
                        Sprite sprite = sprites.FirstOrDefault(s => s.name.EndsWith(variant, StringComparison.Ordinal)) ?? sprites[0];
                        var go = new GameObject("NextIterationTree_" + sprite.name); _spawned.Add(go); proposed.Add(go);
                        go.transform.SetPositionAndRotation(o.transform.position, o.transform.rotation);
                        go.transform.localScale = o.transform.lossyScale;
                        var sr = go.AddComponent<SpriteRenderer>();
                        sr.sprite = sprite; sr.sharedMaterial = o.sharedMaterial; sr.color = o.color; sr.flipX = o.flipX;
                        sr.sortingLayerID = o.sortingLayerID; sr.sortingOrder = o.sortingOrder;
                        foreach (MonoBehaviour c in o.GetComponents<MonoBehaviour>())
                            if (c != null && !(c is TreeTrunkAnchor) && go.GetComponent(c.GetType()) == null) go.AddComponent(c.GetType());
                        var anchor = go.AddComponent<TreeTrunkAnchor>();
                        anchor.Anchor = e.trunkAnchor; anchor.SetLightSheets(mask, normal); anchor.SetWindMaps(maps);
                        go.SetActive(false); originals.Add(o);
                    }
                    File.AppendAllText(numbers, $"{e.species} trees={scene.Length} subject={subjects[e.species]:R} cell={e.cellW}x{e.cellH} sheet={e.sheetW}x{e.sheetH} trunkAnchor={e.trunkAnchor:R} shippedSprite={scene[0].sprite.name} shippedCell={scene[0].sprite.rect.width}x{scene[0].sprite.rect.height}\n");
                }

                foreach (var board in new[] { new { Name = "dawn-0630", Hour = 6.5f }, new { Name = "noon-1230", Hour = 12.5f }, new { Name = "golden-1745", Hour = 17.75f } })
                {
                    yield return PinTheHour(board.Hour, ClearRunSeconds);
                    foreach (bool after in new[] { false, true })
                    {
                        foreach (SpriteRenderer o in originals) o.gameObject.SetActive(!after);
                        foreach (GameObject go in proposed) go.SetActive(after);
                        yield return ShadersReady();
                        foreach (var subject in subjects)
                        {
                            LandingPropsCamera(new Vector2(subject.Value.x, subject.Value.y + 5f)); yield return null; yield return null;
                            Save(board.Name + "-" + subject.Key + "-" + (after ? "next" : "ships"));
                        }
                        File.AppendAllText(numbers, $"{board.Name}-{(after ? "next" : "ships")} hour={GameServices.Clock.HourOfDay:R} worldHeight=14 size={_w}x{_h}\n");
                    }
                }
                Assert.IsTrue(HiddenHarbours.Core.ShellFlow.AtTitle);
            }
            finally
            {
                foreach (SpriteRenderer o in originals) if (o != null) o.gameObject.SetActive(true);
                foreach (GameObject go in proposed) if (go != null) go.SetActive(false);
                LandingPropsReleaseCamera();
            }
        }
#endif
    }
}
