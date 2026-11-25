using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;
using static HiddenHarbours.Tests.EditMode.NineMileCreekLiveGround;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>NINE MILE CREEK'S THREE VENDORS ARE TALKABLE</b>, read from the committed scene. The harbour office, the
    /// yard's catalogue and Hector at his barrel each carry a live, enabled Interactable that the region's
    /// WorldInteractor lists; each names a person or role whose conversation has exactly one browse row, for the
    /// seller id a live vendor in the scene already sells as, and that seller is stocked. Each anchor stands
    /// within the interactor's reach of the walk's step to its counter.
    ///
    /// <para>The anchors are the builders' output, not a hand edit: the vendor-anchor builders planned on the
    /// committed scene have nothing to do, and planned on the committed scene with the two anchors taken back
    /// out by the test's own hand, they give the committed text back to the byte. The scene is never written.</para>
    /// </summary>
    public class NmcVendorAnchorTests
    {
        const string CatalogRoot = "Assets/_Project/Data/Resources/Catalog";

        /// <summary>The counters, the role each speaks as, and the walk's step to each (the arrival test's
        /// LicenceStep, DoryStep and OutboardStep).</summary>
        static readonly (string name, long go, string sellerId, string speaker, Vector2 step)[] Vendors =
        {
            (NineMileCreekWharfLandSide.OfficeVendorName, 1023890145, "seller.nmc_harbourmaster", "Harbourmaster", new Vector2(83.6f, 110.566f)),
            (NineMileCreekWharfYard.CatalogueName, 813580451, "seller.nmc_yard", "Yard hand", new Vector2(197.8f, 123.9f)),
            ("HectorBernard", 1871746041, "seller.hectors_barrel", "Hector Bernard", new Vector2(198f, 89.5f)),
        };

        static readonly Func<SceneYaml, LayerPatch>[] Builders =
        {
            NineMileCreekWharfLandSide.PlaceOfficeVendor, NineMileCreekWharfLandSide.PlaceYardCatalogue,
            NineMileCreekWharfLandSide.RegisterVendorAnchors,
        };

        static Doc Interactor => Script(Scene.RootNamed(NineMileCreekWharfLandSide.InteractorName), "HiddenHarbours.World.WorldInteractor");

        [Test]
        public void EachVendor_HasARegisteredLiveAnchor_ThatResolvesItsSeller()
        {
            List<long> talks = Interactor.FieldRefs("_interactables");
            float reach = Number(Interactor, "_radius");
            foreach (var v in Vendors)
            {
                Doc go = Scene.Require(v.go, v.name);
                Assert.That(Name(go), Is.EqualTo(v.name), $"&{v.go} keeps its name");
                Assert.That(Live(go), Is.True, $"{v.name} is live");
                Doc talk = Script(go, "HiddenHarbours.World.Interactable");
                Assert.That(Enabled(talk), Is.True, $"{v.name}'s Interactable is enabled");
                Assert.That(talks, Does.Contain(talk.FileId), $"the interactor talks to {v.name}");

                string npcPath = AssetDatabase.GUIDToAssetPath(RefGuid(talk.Field("_npc")));
                var npc = AssetDatabase.LoadAssetAtPath<NpcDef>(npcPath);
                Assert.That(npc, Is.Not.Null, $"{v.name}'s Interactable names an NpcDef ({npcPath})");
                Assert.That(npc.DisplayName, Is.EqualTo(v.speaker), $"{v.name} speaks as {v.speaker}");
                Assert.That(npc.HasDialogue, Is.True, $"{npc.Id} has a conversation");
                var browse = npc.Dialogue.Options.Where(o => o.CatalogSellerId == v.sellerId).ToList();
                Assert.That(browse.Count, Is.EqualTo(1), $"{npc.Dialogue.Id} has exactly one row browsing {v.sellerId}");
                Assert.That(string.IsNullOrEmpty(browse[0].SellAtSellerId), Is.True, "the browse row does not also sell");

                var sellers = Scene.Docs.Where(d => d.ClassId == 114 && d.Field("_sellerId") == v.sellerId && Enabled(d)
                    && Live(Scene.Require(d.FieldRef("m_GameObject"), "a vendor"))).ToList();
                Assert.That(sellers, Is.Not.Empty, $"a live vendor in the scene sells as {v.sellerId}");
                Assert.That(Stocked(v.sellerId), Is.True, $"the catalogue lists wares under {v.sellerId}");

                float d = Vector2.Distance(v.step, WorldPosition(go));
                TestContext.WriteLine($"{v.name}: Interactable &{talk.FileId} as {npc.Id}, browses {v.sellerId} " +
                                      $"({sellers.Count} vendor(s)); {d:0.###} m from its step, reach {reach:0.###}");
                Assert.That(d, Is.LessThanOrEqualTo(reach), $"{v.name} is within the interactor's reach of its step");
            }
        }

        [Test]
        public void TheVendorAnchorBuilders_HaveNothingToDoOnTheCommittedScene()
        {
            SceneYaml scene = SceneYaml.Parse(File.ReadAllText(NineMileCreekLayerRefresh.ScenePath));
            foreach (var build in Builders)
            {
                LayerPatch patch = build(scene);
                Assert.That(patch.IsEmpty, Is.True, $"{patch.Step} {patch.Root} would still change the committed scene: {patch.Summary()}");
            }
        }

        [Test]
        public void TheVendorAnchorBuilders_RebuildTheCommittedAnchors_ToTheByte()
        {
            string committed = File.ReadAllText(NineMileCreekLayerRefresh.ScenePath);
            SceneYaml scene = SceneYaml.Parse(committed);
            string stripped = committed;
            foreach (string root in NineMileCreekWharfLandSide.VendorAnchorRoots)
            {
                long id = Script(scene.RootNamed(root), "HiddenHarbours.World.Interactable").FileId;
                string before = stripped;
                stripped = Regex.Replace(stripped, @"(?ms)^--- !u!114 &" + id + @"\n.*?(?=^--- )", "");
                stripped = stripped.Replace("  - component: {fileID: " + id + "}\n", "").Replace("  - {fileID: " + id + "}\n", "");
                Assert.That(before.Length - stripped.Length, Is.GreaterThan(0), $"the test took {root}'s anchor out");
            }
            Assert.That(stripped.Contains("NmcHarbourmaster") || stripped.Contains(GuidOf(NineMileCreekWharfLandSide.HarbourmasterNpcPath)),
                Is.False, "no trace of the anchors is left for the builders to find");

            SceneYaml rebuilt = SceneYaml.Parse(stripped);
            foreach (var build in Builders) rebuilt = build(rebuilt).Apply(rebuilt);
            string text = rebuilt.Render();
            Assert.That(text.Length, Is.EqualTo(committed.Length), "the rebuilt scene is the committed scene's length");
            Assert.That(text == committed, Is.True, "the builders give the committed scene back to the byte");
        }

        /// <summary>True when some catalogue listing names this seller in its Sellers list.</summary>
        static bool Stocked(string sellerId) =>
            Directory.EnumerateFiles(CatalogRoot, "*.asset", SearchOption.AllDirectories)
                .Any(path => File.ReadAllText(path).Contains("    - " + sellerId + "\n"));
    }
}
