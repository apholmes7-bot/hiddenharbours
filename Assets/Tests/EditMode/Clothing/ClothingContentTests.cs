using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Store clothing, pass 1: the shipped garments and rack are valid content. Ids are stable and unique,
    /// every effect names a real need or kind of work, every stand-in colour is one the character builder
    /// accepts for the player, every structural look key names a real rig option, and the rack sells only
    /// catalogued garments with stock.
    /// </summary>
    public class ClothingContentTests
    {
        private static readonly Regex GarmentId = new Regex(@"^clothing\.[a-z0-9]+(_[a-z0-9]+)*$");
        private static readonly Regex RackId = new Regex(@"^clothing_rack\.[a-z0-9]+(_[a-z0-9]+)*$");
        private const string StPetersGeneral = "shop.st_peters_general";

        private static List<T> AllUnderData<T>() where T : UnityEngine.Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/_Project/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null).ToList();

        [Test]
        public void Catalogue_IsShipped_AndEveryGarmentLivesInIt()
        {
            ClothingDef[] shipped = Resources.LoadAll<ClothingDef>(ClothingService.ResourcesFolder);
            Assert.That(shipped.Length, Is.GreaterThanOrEqualTo(6), "the rack offers six to eight garments");
            foreach (ClothingDef def in AllUnderData<ClothingDef>())
                CollectionAssert.Contains(shipped, def, def.name + " is outside Resources/Clothing, so play never finds it");
            var service = new ClothingService();
            Assert.AreEqual(shipped.Length, service.All.Count);
        }

        [Test]
        public void Garments_HaveStableIds_AndPlainText()
        {
            var seen = new HashSet<string>();
            foreach (ClothingDef def in Resources.LoadAll<ClothingDef>(ClothingService.ResourcesFolder))
            {
                Assert.IsTrue(GarmentId.IsMatch(def.Id ?? ""), def.name + ": id '" + def.Id + "'");
                Assert.IsTrue(seen.Add(def.Id), "duplicate id " + def.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(def.DisplayName), def.Id + ": name");
                Assert.IsFalse(string.IsNullOrWhiteSpace(def.Does), def.Id + ": what it does");
                Assert.IsTrue(ClothingSlots.IsValid(def.Slot), def.Id + ": slot");
                Assert.That(def.Price, Is.GreaterThanOrEqualTo(0), def.Id + ": price");
            }
        }

        [Test]
        public void Effects_NameRealSystems()
        {
            foreach (ClothingDef def in Resources.LoadAll<ClothingDef>(ClothingService.ResourcesFolder))
            {
                ClothingEffect[] effects = def.Effects ?? Array.Empty<ClothingEffect>();
                if (def.LookOnly) StringAssert.Contains("look", def.Does.ToLowerInvariant(),
                                                         def.Id + " does nothing, so its line must say it is for looks");
                foreach (ClothingEffect e in effects)
                {
                    Assert.IsTrue(Enum.IsDefined(typeof(ClothingEffectKind), e.Kind) && e.Kind != ClothingEffectKind.None,
                                  def.Id + ": effect kind");
                    Assert.That(e.Scale, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f), def.Id + ": an effect eases, never adds");
                    if (e.Kind == ClothingEffectKind.NeedRelief)
                        Assert.IsTrue(Enum.IsDefined(typeof(NeedKind), e.Need), def.Id + ": need");
                    else
                        Assert.IsTrue(Enum.IsDefined(typeof(DirtActivity), e.Activity) && e.Activity != DirtActivity.None,
                                      def.Id + ": kind of work");
                }
            }
        }

        [Test]
        public void StandIns_AreColoursTheBuilderTakesForThePlayer()
        {
            CharacterBuilderDef builder = CharacterBuilderDef.LoadShipped();
            Assert.IsNotNull(builder, "the shipped character builder");
            foreach (ClothingDef def in Resources.LoadAll<ClothingDef>(ClothingService.ResourcesFolder))
            {
                ClothingLookKey[] keys = def.StandIn ?? Array.Empty<ClothingLookKey>();
                if (keys.Length == 0) continue;
                var recipe = new CharacterRecipe();
                foreach (ClothingLookKey k in keys)
                {
                    CharacterField field = builder.Contract.Field(k.Field);
                    Assert.IsNotNull(field, def.Id + ": no rig field '" + k.Field + "'");
                    Assert.AreEqual(CharacterFieldKind.Colour, field.Kind,
                                    def.Id + ": '" + k.Field + "' is structure; it waits for the art pass (RigLook)");
                    Assert.IsTrue(field.Allows(k.Value), def.Id + ": '" + k.Value + "' is not a " + k.Field + " option");
                    Assert.IsTrue(recipe.Set(k.Field, k.Value));
                }
                Assert.IsTrue(CharacterBuilder.TryBuild(builder, recipe, CharacterAudience.Player, out _, out string why),
                              def.Id + ": " + why);
            }
        }

        [Test]
        public void RigLooks_NameRealRigOptions()
        {
            CharacterBuilderDef builder = CharacterBuilderDef.LoadShipped();
            Assert.IsNotNull(builder);
            foreach (ClothingDef def in Resources.LoadAll<ClothingDef>(ClothingService.ResourcesFolder))
                foreach (ClothingLookKey k in def.RigLook ?? Array.Empty<ClothingLookKey>())
                {
                    CollectionAssert.Contains(CharacterRecipe.Fields, k.Field, def.Id + ": recipe field");
                    CharacterField field = builder.Contract.Field(k.Field);
                    Assert.IsNotNull(field, def.Id + ": rig field " + k.Field);
                    Assert.IsTrue(field.Allows(k.Value), def.Id + ": '" + k.Value + "' is not a " + k.Field + " option");
                }
        }

        [Test]
        public void Rack_AtLeBlancs_SellsCataloguedGarmentsWithStock()
        {
            ClothingRackDef[] racks = Resources.LoadAll<ClothingRackDef>(ClothingService.ResourcesFolder);
            Assert.AreEqual(1, racks.Length, "one rack this pass");
            ClothingDef[] shipped = Resources.LoadAll<ClothingDef>(ClothingService.ResourcesFolder);
            foreach (ClothingRackDef rack in racks)
            {
                Assert.IsTrue(RackId.IsMatch(rack.Id ?? ""), rack.name + ": id '" + rack.Id + "'");
                Assert.AreEqual(StPetersGeneral, rack.ShopId);
                Assert.IsFalse(string.IsNullOrWhiteSpace(rack.DisplayName));
                Assert.That(rack.Lines.Length, Is.InRange(6, 8));
                var seen = new HashSet<ClothingDef>();
                foreach (ClothingRackLine line in rack.Lines)
                {
                    Assert.IsNotNull(line.Clothing, rack.Id + ": empty line");
                    Assert.IsTrue(seen.Add(line.Clothing), rack.Id + ": " + line.Clothing.Id + " listed twice");
                    Assert.That(line.Stock, Is.GreaterThanOrEqualTo(1), line.Clothing.Id + ": stock");
                    CollectionAssert.Contains(shipped, line.Clothing, line.Clothing.Id + " is not in the catalogue");
                }
            }
        }
    }
}
