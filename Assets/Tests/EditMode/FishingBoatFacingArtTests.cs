using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Content guard for the owner's 8-direction fishing-boat facing set (the playable boat's snap-
    /// directional skin). The art is data; these tests pin the CONTRACT the art must keep so a re-export
    /// or a new facing can't silently break the boat on the water:
    ///
    ///   1. The builder's facing array is the full compass in CLOCKWISE order from North — and the
    ///      pure snap math routes each of the 8 real headings to the facing whose filename says so
    ///      (the end-to-end wiring guard: art order ↔ heading math).
    ///   2. Every facing imports as a Single sprite on the same canvas with the same PPU/filter as the
    ///      shipped North reference (one boat per file; drift here = a boat that changes size per heading).
    ///   3. Every facing's pivot sits at its alpha-bbox centre — the convention the 4-cardinal pass
    ///      established (custom pivot per facing so the drawn hull stays put when the snap swaps
    ///      pictures; the old 1px-off side facing read as a 2px pop). Measured from the PNG bytes, so
    ///      it holds no matter what the importer flags say.
    /// </summary>
    public class FishingBoatFacingArtTests
    {
        // The CW-from-North compass the facing array must spell, by filename suffix.
        private static readonly string[] ExpectedSuffixes =
            { "_N", "_NE", "_E", "_SE", "_S", "_SW", "_W", "_NW" };

        // Where the compass LIVES now. It used to be a private string[] of art paths inside
        // PersistentCoreBuilder, read here by reflection — but the player's boat wears the ISO DORY, and the
        // skin became data (BoatHullDef.Visual → BoatVisualDef), so that array is gone. The live consumer of
        // the FishingBoat_* art is the AMBIENT FLEET, whose Def authors the same compass as sprite refs. That
        // asset is the contract now: element i must be the facing for heading i*45° CW from North, so the
        // fleet snaps to the picture the filename promises. Paths are recovered from the authored refs, which
        // keeps the end-to-end guard (art order ↔ heading math) pointed at a real consumer rather than a
        // builder constant.
        private const string FleetDefPath = "Assets/_Project/Data/Boats/StPetersAmbientFleet.asset";

        private static string[] FacingPaths()
        {
            var def = AssetDatabase.LoadAssetAtPath<AmbientFleetDef>(FleetDefPath);
            Assert.IsNotNull(def, $"the ambient fleet def exists at {FleetDefPath} (moved? update this test)");
            Assert.IsTrue(def.HasFullHullCompass(),
                "the fleet authors the FULL 8-way compass — a partial set would snap into a stale picture");

            var paths = new string[def.HullFacings.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                paths[i] = AssetDatabase.GetAssetPath(def.HullFacings[i]);
                Assert.IsNotEmpty(paths[i], $"facing[{i}] resolves to an asset on disk");
            }
            return paths;
        }

        [Test]
        public void FacingArray_IsTheFullCompass_ClockwiseFromNorth()
        {
            var paths = FacingPaths();
            Assert.AreEqual(8, paths.Length, "8 facings — the owner's full compass");
            for (int i = 0; i < paths.Length; i++)
            {
                string name = Path.GetFileNameWithoutExtension(paths[i]);
                Assert.IsTrue(name.EndsWith(ExpectedSuffixes[i]),
                    $"facing[{i}] must be the {ExpectedSuffixes[i].TrimStart('_')} sprite (CW from North); got '{name}'");
            }
        }

        [Test]
        public void SnapMath_RoutesEachRealHeading_ToTheFacingTheFilenameSays()
        {
            var paths = FacingPaths();
            for (int i = 0; i < paths.Length; i++)
            {
                float heading = i * (360f / paths.Length);
                int idx = DirectionalBoatSprite.HeadingToFacingIndex(heading, paths.Length, 0f);
                Assert.AreEqual(i, idx, $"heading {heading}° must pick facing[{i}] ({ExpectedSuffixes[i]})");
            }
        }

        [Test]
        public void EveryFacing_ImportsAsASingleSprite_OnTheNorthReferenceSettings()
        {
            var paths = FacingPaths();
            var reference = (TextureImporter)AssetImporter.GetAtPath(paths[0]);
            Assert.IsNotNull(reference, $"North reference importer at {paths[0]}");

            foreach (string path in paths)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.IsNotNull(sprite, $"{path} imports as a Sprite (missing PNG or meta?)");

                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.IsNotNull(importer, $"{path} has a TextureImporter");
                Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode,
                    $"{path}: one boat per file — spriteMode Single");
                Assert.AreEqual(reference.spritePixelsPerUnit, importer.spritePixelsPerUnit,
                    $"{path}: PPU must match the North reference (hull size changes per heading otherwise)");
                Assert.AreEqual(reference.filterMode, importer.filterMode,
                    $"{path}: filter mode must match the North reference (pixel-art point filter)");
                Assert.AreEqual(reference.textureCompression, importer.textureCompression,
                    $"{path}: compression must match the North reference");
                Assert.AreEqual(new Vector2(sprite.texture.width, sprite.texture.height),
                    new Vector2(128f, 128f),
                    $"{path}: the facing set shares one 128×128 canvas (the 4-cardinal pass rule)");
            }
        }

        [Test]
        public void EveryFacing_PivotSitsAtItsAlphaBBoxCentre()
        {
            // The centring rule from the 4-cardinal pass, measured from the PNG BYTES (the importer marks
            // the texture non-readable, so decode a scratch copy): pivot.x = (minX+maxX)/2 and
            // pivot.y = height - (minYtop+maxYtop)/2 — exactly the custom-pivot formula the shipped
            // N/E/S/W metas encode. Meta floats are 5-decimal, so allow a hair over their rounding error.
            const float Tol = 0.01f;

            foreach (string path in FacingPaths())
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.IsNotNull(sprite, $"{path} imports as a Sprite");

                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.IsTrue(ImageConversion.LoadImage(tex, File.ReadAllBytes(path)),
                        $"{path}: PNG decodes");
                    int w = tex.width, h = tex.height;
                    var px = tex.GetPixels32();   // bottom-up rows
                    int minX = w, maxX = -1, minY = h, maxY = -1;   // bottom-up bbox
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                            if (px[y * w + x].a > 0)
                            {
                                if (x < minX) minX = x;
                                if (x > maxX) maxX = x;
                                if (y < minY) minY = y;
                                if (y > maxY) maxY = y;
                            }
                    Assert.IsTrue(maxX >= 0, $"{path}: has opaque pixels");

                    // Bottom-up bbox → the meta convention: x = top-down column centre;
                    // y = h - (top-down row centre) = bottom-up row centre + 1.
                    var expected = new Vector2((minX + maxX) / 2f, (minY + maxY) / 2f + 1f);
                    Assert.AreEqual(expected.x, sprite.pivot.x, Tol,
                        $"{path}: pivot.x off the alpha-bbox centre — the snap will pop sideways");
                    Assert.AreEqual(expected.y, sprite.pivot.y, Tol,
                        $"{path}: pivot.y off the alpha-bbox centre — the snap will pop along the keel");
                }
                finally
                {
                    Object.DestroyImmediate(tex);
                }
            }
        }
    }
}
