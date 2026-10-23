using System;
using System.Linq;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    public class HeronViewCatalogTests
    {
        [Test]
        public void HeronViewCatalog_FillKeepsCoreBuilds_StrictWithGolfPresent()
        {
            var builds = AssetDatabase.FindAssets("t:CharacterBuildDef", new[] { CharacterBuilderContractBaker.BuildsFolder })
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<CharacterBuildDef>).ToArray();
            var shipped = AssetDatabase.LoadAssetAtPath<CharacterBuilderDef>(CharacterBuilderContractBaker.CatalogPath);
            var fresh = ScriptableObject.CreateInstance<CharacterBuilderDef>();
            var duplicate = ScriptableObject.CreateInstance<CharacterBuildDef>();
            var withoutPlayer = UnityEngine.Object.Instantiate(shipped.Contract);
            try
            {
                Assert.That(builds.Length, Is.EqualTo(18));
                string[] coreIds = {
                    "char_build.fisher", "char_build.ginny", "char_build.skipper", "char_build.nan", "char_build.deckboss",
                    "char_build.packer", "char_build.cutter", "char_build.hand", "char_build.boy", "char_build.girl"
                };
                Assert.That(shipped.Builds.Select(b => b.Id), Is.EqualTo(coreIds));
                var visitors = builds.Where(b => !coreIds.Contains(b.Id)).ToArray();
                Assert.That(visitors.Length, Is.EqualTo(8));
                CharacterBuilderContractBaker.Fill(fresh, shipped.Contract, shipped.Ramps, shipped.Fit, builds);
                Assert.That(fresh.Builds.Select(b => b.Id), Is.EqualTo(coreIds));
                Assert.That(fresh.Builds, Is.EqualTo(shipped.Builds));
                Assert.That(fresh.Builds.Select(b => b.Preset), Is.EqualTo(shipped.Contract.Presets.Select(p => p.Key)));
                Assert.That(fresh.PlayerDefaultBuildId, Is.EqualTo("char_build.fisher"));

                var missingHand = builds.Where(b => b.Id != "char_build.hand").ToArray();
                Assert.That(visitors.All(b => missingHand.Contains(b)), Is.True);
                Assert.That(missingHand.Count(b => b.Preset == "hand"), Is.EqualTo(4));
                var missing = Assert.Throws<InvalidOperationException>(() =>
                    CharacterBuilderContractBaker.Fill(fresh, shipped.Contract, shipped.Ramps, shipped.Fit, missingHand));
                Assert.That(missing.Message, Does.Contain("hand"));

                duplicate.Id = "char_build.hand"; duplicate.Preset = "hand";
                var repeated = Assert.Throws<InvalidOperationException>(() =>
                    CharacterBuilderContractBaker.Fill(fresh, shipped.Contract, shipped.Ramps, shipped.Fit, builds.Concat(new[] { duplicate }).ToArray()));
                Assert.That(repeated.Message, Does.Contain("hand"));

                withoutPlayer.Presets = withoutPlayer.Presets.Where(p => p.Key != "fisher").ToArray();
                var missingDefault = Assert.Throws<InvalidOperationException>(() =>
                    CharacterBuilderContractBaker.Fill(fresh, withoutPlayer, shipped.Ramps, shipped.Fit, builds));
                Assert.That(missingDefault.Message, Does.Contain("char_build.fisher"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(fresh);
                UnityEngine.Object.DestroyImmediate(duplicate);
                UnityEngine.Object.DestroyImmediate(withoutPlayer);
            }
        }
    }
}
