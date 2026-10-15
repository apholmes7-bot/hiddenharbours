using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public class PlantMapImportTests
    {
        [Test] public void EveryPlantMapIsDefaultWithNoSpriteModeOrSliceRows()
        {
            var maps = Directory.GetFiles("Assets/_Project/Art/Foliage/Plants5", "*.png");
            Assert.That(maps.Length, Is.GreaterThan(0));
            var faults = new List<string>();
            foreach (string path in maps)
            {
                var importer = AssetImporter.GetAtPath(path.Replace('\\', '/')) as TextureImporter;
                if (importer == null) { faults.Add(path + ": missing importer"); continue; }
                if (importer.textureType != TextureImporterType.Default) faults.Add(path + ": type is not Default");
                if (importer.spriteImportMode != SpriteImportMode.None) faults.Add(path + ": sprite mode is not None");
#pragma warning disable 0618
                if (importer.spritesheet.Length != 0) faults.Add(path + ": slice rows remain");
#pragma warning restore 0618
            }
            Assert.That(faults, Is.Empty, string.Join("\n", faults));
        }
    }
}
