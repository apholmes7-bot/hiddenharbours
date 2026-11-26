using System;
using System.IO;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>The Whelps: the rocks are the key scene's rows, the sea covers each from its seat up along its own
    /// lower edge, the scene holds exactly what the step would write, and the cottage above them keeps its ground
    /// clear and its door lit.</summary>
    public class StPetersWhelpsTests
    {
        const string DefPath = WhelpsFolder + "/TidalRockField_stp_whelps.asset";

        static readonly string[] Rows =
        {
            "rock.stp_whelp_1", "rock.stp_whelp_1_b", "rock.stp_whelp_1_c", "rock.stp_whelp_2", "rock.stp_whelp_2_b",
            "rock.stp_whelp_3", "rock.stp_whelp_3_b",
        };

        static readonly string[] Forms = { "form.stp_harbour_stack", "form.stp_whelp_1", "form.stp_whelp_2", "form.stp_whelp_3" };

        /// <summary>The forms are data (one asset each, the ids the key scene's ground gives them), the stack is the
        /// form the scene's gulls are mounted on, and the pictures the recipe drew are the ones the Defs name: three
        /// of one size each, with true heights the sea can cut by.</summary>
        [Test]
        public void TheForms_AreData_AndTheirPicturesAreTheRecipes()
        {
            TidalFormsContract contract = LoadTidalFormsContract();
            var forms = LoadTidalForms(contract);
            Assert.That(forms.Select(f => f.Id), Is.EqualTo(Forms));
            var rules = Resources.Load<HiddenHarbours.Art.SunCasterRules>(nameof(HiddenHarbours.Art.SunCasterRules));
            Assert.That(rules, Is.Not.Null, "the sun caster's rules");
            Assert.That(rules.ExcludedNames, Does.Contain("*Seabed*").And.Contain("*Shade*"), "the rules no longer leave the seabed and the shade alone");
            KeySceneDef scene = LoadStPetersKeyScenes().Single(s => s.Id == "keyscene.stp_whelps_lookout");
            Assert.That(scene.Pieces.Count(p => p.MountHost == "form.stp_harbour_stack"), Is.GreaterThan(0),
                "the key scene mounts nothing on the stack: the id is not the scene's");
            foreach (TidalForm form in forms)
            {
                TidalFormsContract.Form drawn = contract.forms.Single(f => f.id == form.Id);
                Assert.That(form.Pieces.Select(q => q.Name), Is.EqualTo(drawn.pieces.Select(q => q.name)), form.Id);
                foreach (TidalFormsContract.Piece piece in drawn.pieces)
                {
                    string what = form.Id + " " + piece.name;
                    var dry = AssetDatabase.LoadAssetAtPath<Sprite>(WhelpsHeights + "/" + piece.stem + ".png");
                    var wet = AssetDatabase.LoadAssetAtPath<Sprite>(WhelpsHeights + "/" + piece.stem + "_wet.png");
                    var height = AssetDatabase.LoadAssetAtPath<Texture2D>(WhelpsHeights + "/" + piece.stem + "_height.png");
                    Assert.That(dry, Is.Not.Null, what); Assert.That(wet, Is.Not.Null, what); Assert.That(height, Is.Not.Null, what);
                    Assert.That(new[] { dry.texture.width, dry.texture.height }, Is.EqualTo(drawn.pixels), what + ": the picture is not the recipe's size");
                    Assert.That(new[] { wet.texture.width, wet.texture.height }, Is.EqualTo(drawn.pixels), what);
                    Assert.That(new[] { height.width, height.height }, Is.EqualTo(drawn.pixels), what + ": the heights do not lie over the picture");
                    Assert.That(dry.pixelsPerUnit, Is.EqualTo(contract.pixelsPerMetre), what);
                    Assert.That(dry.pivot.x / dry.rect.width, Is.EqualTo(piece.pivot[0]).Within(1e-3f), what + ": the pivot is not the recipe's");
                    Assert.That(dry.pivot.y / dry.rect.height, Is.EqualTo(piece.pivot[1]).Within(1e-3f), what);
                    Assert.That(wet.pivot, Is.EqualTo(dry.pivot), what + ": the wet picture does not lie under the dry one");
                    // One stone, one shadow, and none from under the sea: the sun caster's rules leave these names alone.
                    bool seabed = piece.name.StartsWith("Seabed", StringComparison.Ordinal);
                    Assert.That(rules.IsExcludedSprite(dry) || HiddenHarbours.Art.SunCasterRules.Matches(piece.name, "*Seabed*"), Is.EqualTo(seabed), what);
                    Assert.That(HiddenHarbours.Art.SunCasterRules.Matches(piece.name + TidalFormWetSuffix, "*Shade*"), Is.True, what);
                }
                Assert.That(drawn.tallestMetres, Is.LessThanOrEqualTo(drawn.floorMetres + drawn.rangeMetres), form.Id + ": taller than its height sheet can say");
                Vector3 atTheWater = TidalFormDecode(form.FloorMetres, form.RangeMetres, 0f);
                Vector3 above = TidalFormDecode(form.FloorMetres, form.RangeMetres, form.WetBandMetres);
                Assert.That(TidalFaceWaterline.DecodeHeight(0f, atTheWater), Is.EqualTo(drawn.floorMetres).Within(1e-4f));
                Assert.That(TidalFaceWaterline.DecodeHeight(1f, atTheWater) - TidalFaceWaterline.DecodeHeight(1f, above),
                    Is.EqualTo(form.WetBandMetres).Within(1e-4f), form.Id + ": the dry picture is not cut a wet band above the water");
                Assert.That(form.Foot != null, Is.EqualTo(form.Id == "form.stp_harbour_stack"), form.Id + ": only the stack stops a hull");
            }
            TidalForm stack = forms.Single(f => f.Id == "form.stp_harbour_stack");
            Assert.That(contract.forms.Single(f => f.id == stack.Id).tallestMetres, Is.InRange(6f, 6.8f), "the stack's top is the scene's 6.2 m and its turf");
            Assert.That(stack.At, Is.EqualTo(new Vector2(192f, -38f)), "the stack stands where the scene's ground has it");
            Assert.That(stack.Pieces.Select(q => q.Name), Is.EqualTo(new[] { "Body", "SeabedPlinth" }), "the stack is its body and the plinth under the sea");
            Assert.That(stack.Pieces[0].OffsetY, Is.LessThan(0f), "the body's pivot is its own foot, in front of the centre");
        }

        [Test]
        public void TheWhelps_AreTheKeyScenesRows_AndTheKitsRocks()
        {
            var def = AssetDatabase.LoadAssetAtPath<TidalRockFieldDef>(DefPath);
            Assert.That(def, Is.Not.Null, "no rock field at " + DefPath);
            Assert.That(def.Id, Is.EqualTo("rockfield.stp_whelps"));
            Assert.That(def.WetBandMetres, Is.GreaterThan(0f));

            RockHeightsContract contract = LoadRockHeightsContract();
            var fields = LoadTidalRockFields(contract);
            Assert.That(fields.Count, Is.EqualTo(1));
            Assert.That(fields[0].Def, Is.SameAs(def));
            Assert.That(fields[0].Rocks.Select(r => r.Id).OrderBy(i => i, StringComparer.Ordinal), Is.EqualTo(Rows));

            KeySceneDef scene = LoadStPetersKeyScenes().Single(s => s.Id == def.KeySceneId);
            foreach (TidalRock rock in fields[0].Rocks)
            {
                KeyScenePiece row = scene.Pieces.Single(p => p.Id == rock.Id);
                Assert.That(rock.At, Is.EqualTo(row.At), rock.Id + " stands where its row says");
                Assert.That(rock.SeatMetres, Is.EqualTo(row.Z), rock.Id + " sits at its row's height");
                Assert.That(rock.TopMetres, Is.InRange(0.2f, 3f), rock.Id + " has the kit's height");
                Assert.That(rock.TallestDrawnMetres, Is.GreaterThan(0f).And.LessThanOrEqualTo(contract.rangeMetres));
                Assert.That(rock.Band.Sprite.Guid, Is.EqualTo(rock.Upper.Sprite.Guid), "both pictures are cells of one sheet");
                Assert.That(rock.Band.Sprite.FileId, Is.Not.EqualTo(rock.Upper.Sprite.FileId), "the wet band is a wetter state than the rock above it");
            }
        }

        [Test]
        public void TheHeightSheets_LieOverTheirColourSheets_AndAreReadAsNumbers()
        {
            RockHeightsContract contract = LoadRockHeightsContract();
            Assert.That(contract.sheets.Length, Is.EqualTo(4), "skerry, knuckle, shelf and slab");
            foreach (var sheet in contract.sheets)
            {
                string path = WhelpsHeights + "/" + sheet.height;
                var height = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var colour = AssetDatabase.LoadAssetAtPath<Texture2D>(HiddenHarbours.Art.Editor.RockPxCatalog.TexDir + sheet.stem + ".png");
                Assert.That(height, Is.Not.Null, path);
                Assert.That(colour, Is.Not.Null, sheet.stem);
                Assert.That(new Vector2Int(height.width, height.height), Is.EqualTo(new Vector2Int(colour.width, colour.height)),
                    sheet.stem + ": a height is read at the colour's own place on the sheet");
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.sRGBTexture, Is.False, sheet.stem + ": a height is a number, not a colour");
                Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
                Assert.That(sheet.tallestDrawnMetres.Length, Is.EqualTo(HiddenHarbours.Art.Editor.RockPxCatalog.SheetCols));
            }
        }

        [Test]
        public void TheSea_CoversARockFromItsSeatUp_AndTheWetBandStandsAboveTheWater()
        {
            const float seat = -1.19f, range = 3f, top = 0.8f, tallest = 0.65f, band = 0.25f;
            Vector3 atTheWater = TidalRockDecode(seat, range, top, tallest, 0f);
            Vector3 aboveTheBand = TidalRockDecode(seat, range, top, tallest, band);
            float crown = tallest / range;   // the sheet's value at the rock's tallest pixel

            Assert.That(TidalFaceWaterline.DecodeHeight(0f, atTheWater), Is.EqualTo(seat).Within(1e-5f), "the lower edge is the seat");
            Assert.That(TidalFaceWaterline.DecodeHeight(crown, atTheWater), Is.EqualTo(seat + top).Within(1e-4f),
                "the tallest pixel stands at the kit's height, however tall it is drawn");
            for (float e = 0f; e <= crown; e += crown / 16f)
            {
                float under = TidalFaceWaterline.DecodeHeight(e, atTheWater), upper = TidalFaceWaterline.DecodeHeight(e, aboveTheBand);
                Assert.That(under - upper, Is.EqualTo(band).Within(1e-5f), "the upper picture is cut one band above the water");
                Assert.That(TidalFaceWaterline.DecodeHeight(e + crown / 16f, atTheWater), Is.GreaterThan(under), "higher on the rock is higher");
            }
            // The same numbers give the same cut, to the bit: nothing here remembers a tide.
            Assert.That(TidalRockDecode(seat, range, top, tallest, band), Is.EqualTo(aboveTheBand));
            Assert.Throws<Refusal>(() => TidalRockDecode(seat, range, 0f, tallest, 0f));
            Assert.Throws<Refusal>(() => TidalRockDecode(seat, range, top, 0f, 0f));
        }

        [Test]
        public void TheScene_HoldsExactlyTheRocksItsStepWouldWrite()
        {
            var scene = SceneYaml.Parse(File.ReadAllText(ScenePath));
            RockHeightsContract contract = LoadRockHeightsContract();
            var fields = LoadTidalRockFields(contract);
            var forms = LoadTidalForms(LoadTidalFormsContract());
            LayerPatch patch = Whelps(scene, fields, contract, forms, new EditorTidalRockAssets());
            Assert.That(patch.IsEmpty, Is.True, "StPeters.unity is not what the Whelps step writes:\n" + patch.Summary());

            var owned = scene.SubtreeOf(scene.RootNamed(WhelpsRootName).FileId);
            int pieces = forms.Sum(f => f.Pieces.Count);
            Assert.That(pieces, Is.EqualTo(Forms.Length + 1), "a piece a ledge, and the stack's two");
            Assert.That(scene.Docs.Count(d => owned.Contains(d.FileId) && d.ClassId == 212), Is.EqualTo((Rows.Length + pieces) * 2), "two pictures a rock, and two a form's piece");
            Assert.That(scene.Docs.Count(d => owned.Contains(d.FileId) && d.IsScript(typeof(TidalFaceWaterline).FullName)),
                Is.EqualTo((Rows.Length + pieces) * 2), "and the sea cuts both");
            Assert.That(scene.Docs.Count(d => owned.Contains(d.FileId) && (d.ClassId == 60 || d.ClassId == 61 || d.ClassId == 58)),
                Is.EqualTo(1), "only the stack carries a collider: what a hull meets on the ledges is the ground's own depth");
            foreach (string form in Forms)
                Assert.That(scene.Docs.Count(d => owned.Contains(d.FileId) && d.ClassId == 1 && d.Field("m_Name") == form), Is.EqualTo(1), form);
            foreach (string row in Rows)
                Assert.That(scene.Docs.Count(d => owned.Contains(d.FileId) && d.ClassId == 1 && d.Field("m_Name") == row), Is.EqualTo(1), row);
        }

        [Test]
        public void TheCottage_KeepsItsGroundClearOfPlanting_AndHasALampOverItsDoor()
        {
            var scene = SceneYaml.Parse(File.ReadAllText(ScenePath));
            var cottages = KeeperCottageZones(scene);
            Assert.That(cottages.Count, Is.EqualTo(1), "the cottage above the Whelps");
            StPetersBuildingClearance.Zone cottage = cottages[0];
            Assert.That(cottage.Path, Is.EqualTo(KeepersRootName + "/OdileMarchandCottage"));
            Assert.That(StPetersBuildingClearance.ReadZones(scene).Any(z => z.Path == cottage.Path), Is.True,
                "the rule that keeps planting off buildings reads the cottage with the rest");
            Assert.That(StPetersBuildingClearance.IsException(cottage.Path), Is.False);

            var sites = CommittedPlantSites(scene);
            Assert.That(sites.Count, Is.GreaterThan(1000), "an empty field would pass for nothing");
            Assert.That(sites.Count(s => cottage.Contains(s)), Is.EqualTo(0), "no plant roots on the cottage's ground or within the clearance of its walls");

            var owned = scene.SubtreeOf(scene.RootNamed(KeepersRootName).FileId);
            var lamp = scene.Docs.Single(d => owned.Contains(d.FileId) && d.ClassId == 1 && d.Field("m_Name") == DoorLampName);
            var parts = scene.ComponentsOf(lamp).ToList();
            Assert.That(parts.Count(d => d.IsScript(typeof(SceneLight).FullName)), Is.EqualTo(1));
            Assert.That(parts.Count(d => d.IsScript(typeof(PreconfiguredLight).FullName)), Is.EqualTo(1), "a lantern like any doorstep's, lit by the night");
        }
    }
}
