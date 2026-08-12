using System;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The fuel containers as CONTENT: one Def per container, one prefab each, and a presenter that
    /// picks a frame from a fraction. Visual only — nothing here consumes fuel or touches the market.
    /// </summary>
    public class FuelContainerDefTests
    {
        static FuelStorageKit.Contract Contract => FuelSheetSlicer.LoadContract();

        static FuelContainerDef[] AllDefs() =>
            AssetDatabase.FindAssets($"t:{nameof(FuelContainerDef)}",
                                     new[] { FuelContainerDefBuilder.DefFolder })
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Select(AssetDatabase.LoadAssetAtPath<FuelContainerDef>)
                         .Where(d => d != null)
                         .ToArray();

        // ---- the frame picker, as pure logic ---------------------------------------------------

        [TestCase(0f, 0)]
        [TestCase(0.01f, 0)]
        [TestCase(0.13f, 1)]      // nearer 0.25 than 0
        [TestCase(0.25f, 1)]
        [TestCase(0.5f, 2)]
        [TestCase(0.62f, 2)]      // nearer 0.5 than 0.75
        [TestCase(0.7f, 3)]
        [TestCase(1f, 4)]
        [TestCase(2f, 4)]         // clamped
        [TestCase(-1f, 0)]        // clamped
        public void NearestFillIndex_picks_the_nearest_baked_rung(float fill, int expected)
        {
            var ladder = new[] { 0f, 0.25f, 0.5f, 0.75f, 1f };
            Assert.That(FuelLevelPresenter.NearestFillIndex(ladder, fill), Is.EqualTo(expected));
        }

        [Test]
        public void NearestFillIndex_survives_a_vessel_that_holds_nothing()
        {
            Assert.That(FuelLevelPresenter.NearestFillIndex(null, 0.5f), Is.Zero);
            Assert.That(FuelLevelPresenter.NearestFillIndex(Array.Empty<float>(), 0.5f), Is.Zero);
            Assert.That(FuelLevelPresenter.NearestFillIndex(new[] { 0f }, 0.9f), Is.Zero);
        }

        // ---- the Def's frame table ---------------------------------------------------------------

        [Test]
        public void Frame_indexes_row_major_and_refuses_out_of_range()
        {
            var def = ScriptableObject.CreateInstance<FuelContainerDef>();
            try
            {
                def.Facings = 4;
                def.FillFractions = new[] { 0f, 0.5f, 1f };
                def.Frames = new Sprite[12];

                // Distinct instances so the row-major mapping is checked by identity, not by count.
                for (int i = 0; i < 12; i++)
                    def.Frames[i] = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1),
                                                  Vector2.zero);

                Assert.That(def.Frame(0, 0), Is.SameAs(def.Frames[0]));
                Assert.That(def.Frame(0, 3), Is.SameAs(def.Frames[3]));
                Assert.That(def.Frame(1, 0), Is.SameAs(def.Frames[4]));
                Assert.That(def.Frame(2, 3), Is.SameAs(def.Frames[11]));

                Assert.That(def.Frame(-1, 0), Is.Null);
                Assert.That(def.Frame(3, 0), Is.Null, "fill index past the ladder");
                Assert.That(def.Frame(0, 4), Is.Null, "facing past the baked count");
            }
            finally { UnityEngine.Object.DestroyImmediate(def); }
        }

        // ---- the built content --------------------------------------------------------------------

        [Test]
        public void There_is_one_Def_per_baked_sheet()
        {
            var contract = Contract;
            Assert.That(contract, Is.Not.Null, "no bake contract — bake and slice the kit first");

            var defs = AllDefs();
            Assert.That(defs.Length, Is.EqualTo(contract.sheets.Length),
                "one Def per container, so a sheet without one is a container that cannot be placed");
        }

        [Test]
        public void Ids_are_unique_stable_and_snake_case()
        {
            var defs = AllDefs();
            Assert.That(defs, Is.Not.Empty);

            Assert.That(defs.Select(d => d.Id).Distinct().Count(), Is.EqualTo(defs.Length),
                "ids are the save-facing key; a collision silently merges two containers");

            foreach (var d in defs)
            {
                Assert.That(d.Id, Does.StartWith(FuelContainerDefBuilder.IdPrefix + "."));
                Assert.That(d.Id, Does.Match(@"^[a-z0-9]+\.[a-z0-9_]+$"),
                    $"'{d.Id}' is not type.snake_case — ids are append-only and never renamed");
            }
        }

        [Test]
        public void Every_Def_carries_a_full_frame_table_matching_its_sheet()
        {
            var contract = Contract;
            foreach (var def in AllDefs())
            {
                var sheet = contract.Find($"{def.VesselType}_{def.SizeKey}_{def.Grade}");
                Assert.That(sheet, Is.Not.Null, $"{def.Id} has no sheet in the contract");

                Assert.That(def.Facings, Is.EqualTo(sheet.facings));
                Assert.That(def.FillFractions, Is.EqualTo(sheet.fills));
                Assert.That(def.Frames.Length, Is.EqualTo(sheet.facings * sheet.fills.Length));

                // ⚠️ A null frame is the failure a "Multiple but not sliced" sheet produces, and it
                // draws nothing rather than erroring.
                for (int f = 0; f < def.FillCount; f++)
                    for (int d = 0; d < def.Facings; d++)
                        Assert.That(def.Frame(f, d), Is.Not.Null,
                            $"{def.Id} fill {f} facing {d} has no sprite");
            }
        }

        [Test]
        public void Volumes_come_from_the_rig_and_a_full_container_outweighs_its_tare()
        {
            foreach (var def in AllDefs())
            {
                Assert.That(def.TareKg, Is.GreaterThanOrEqualTo(0f));
                if (def.CapacityLitres <= 0) continue;      // nozzle, dispenser

                Assert.That(def.FullKg, Is.GreaterThan(def.TareKg),
                    $"{def.Id} full mass must exceed its empty mass");
            }
        }

        /// <summary>
        /// ⚠️ Pins the readability fact behind the owner's mixed-fuel question: grade is COLOUR
        /// ONLY. Every grade of a vessel shares one silhouette, so if this ever stops being true the
        /// answer to "is mixed its own art?" has changed and wants re-ruling.
        /// </summary>
        [Test]
        public void Every_grade_of_a_vessel_shares_one_cell_and_one_pivot()
        {
            var contract = Contract;
            foreach (var group in contract.sheets.GroupBy(s => $"{s.type}_{s.size}"))
            {
                var first = group.First();
                foreach (var s in group.Skip(1))
                {
                    Assert.That(new[] { s.cellW, s.cellH, s.pivotX, s.pivotY },
                                Is.EqualTo(new[] { first.cellW, first.cellH, first.pivotX, first.pivotY }),
                        $"{s.key} differs in cell or pivot from {first.key}. Grade is supposed to " +
                        "select colour ramps only — geo() never receives `fuel`.");
                }
            }
        }

        // ---- the prefabs ---------------------------------------------------------------------------

        /// <summary>
        /// ⚠️ The assertion here is NOT "sortingOrder is 0". <c>YSortSprite</c> is
        /// <c>[ExecuteAlways]</c>, so it sorts the moment it is added and its own output is
        /// serialised into the prefab — zeroing that on the way past does not stick, and it is not
        /// what "no parked sorting constants" means anyway. A cached band output is recomputed on
        /// every <c>OnEnable</c>; an AUTHORED constant is the thing that fights the band.
        ///
        /// <para>So: every prefab must carry the SAME order (they all sit at world Y = 0, so the
        /// band gives them all one number) and a zero sort offset. A hand-set order on any one
        /// prefab breaks the first; a pivot fudge breaks the second.</para>
        /// </summary>
        [Test]
        public void Every_Def_has_a_prefab_sorting_through_the_band_not_a_parked_constant()
        {
            int? bandOrder = null;

            foreach (var def in AllDefs())
            {
                string path = $"{FuelContainerDefBuilder.PrefabFolder}/{def.name}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, $"no prefab at {path}");

                var presenter = prefab.GetComponent<FuelLevelPresenter>();
                Assert.That(presenter, Is.Not.Null, $"{def.name} prefab has no presenter");
                Assert.That(presenter.Container, Is.SameAs(def), "presenter points at another Def");

                var ysort = prefab.GetComponent<YSortSprite>();
                Assert.That(ysort, Is.Not.Null,
                    "decor sorts through the ADR 0032 band, not a literal order");
                Assert.That(ysort.SortPivotYOffset, Is.Zero,
                    $"{def.name} offsets its sort point. These vessels pivot at their base centre on " +
                    "the ground plane — measured at the bake and asserted by the slicer — so the " +
                    "offset that the wharf pack needs is exactly wrong here.");

                var sr = prefab.GetComponent<SpriteRenderer>();
                Assert.That(sr, Is.Not.Null);

                bandOrder ??= sr.sortingOrder;
                Assert.That(sr.sortingOrder, Is.EqualTo(bandOrder.Value),
                    $"{def.name} sorts differently from its siblings. Every prefab sits at world " +
                    "Y = 0, so the band gives them all one number; a different one is an AUTHORED " +
                    "constant, and that is what stops sorting the day the band moves.");
            }

            Assert.That(bandOrder, Is.Not.Null.And.Not.Zero,
                "a zero here would mean YSortSprite never ran — the band's value at Y = 0 is the " +
                "middle of the decor range, not the bottom of it.");
        }

        /// <summary>
        /// Nothing picks a can up yet, and this is the test that says so out loud. Carrying needs an
        /// interact verb that does not exist; the art is carry-ready and deliberately unwired.
        /// </summary>
        [Test]
        public void Carriables_are_marked_but_nothing_is_wired_to_pick_them_up()
        {
            var carry = AllDefs().Where(d => d.Carriable).ToArray();
            Assert.That(carry, Is.Not.Empty, "jugs, cans and nozzles are the carriable half");

            foreach (var def in carry)
            {
                string path = $"{FuelContainerDefBuilder.PrefabFolder}/{def.name}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab.GetComponent<Collider2D>(), Is.Null,
                    $"{def.name} has a collider — the first half of pick-up wiring. Carrying is out " +
                    "of scope until the interact verb lands; wire it there, not here.");
            }
        }
    }
}
