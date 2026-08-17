using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;                 // SpriteLightMath
using HiddenHarbours.Art.Editor;          // ShopCatalog, ShopKit, BuildingFacing
using HiddenHarbours.World;               // InteriorFootprint

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE VILLAGE OPENS FOR BUSINESS</b> — the general store and the post office, sited, turned and
    /// opened. Sibling of <c>StPetersVillageTests</c>, and shaped by the same fact: a placement bug here
    /// is silent. A shop on the wrong ground still draws; a shop turned the wrong way still draws; a
    /// doorway cut at the middle of a wall whose door is 2.5 m to the left still draws.
    /// </summary>
    public class StPetersShopsTests
    {
        static System.Collections.Generic.IReadOnlyList<StPetersShops.Site> Sites => StPetersShops.Sites;

        static ShopCatalog.Placement Shell(string key)
        {
            var p = ShopCatalog.FindShell(key);
            if (!p.IsValid) Assert.Ignore($"'{key}' is not in the shop contract — run Bake Shops.");
            return p;
        }

        // ---- the siting ------------------------------------------------------------------------

        [Test]
        public void TheTwoShopsAreTheOnesTheOwnerRuled_AndBothAreBaked()
        {
            CollectionAssert.AreEquivalent(
                new[] { "generalStore", "postOffice" }, Sites.Select(s => s.Key).ToArray(),
                "the shops drifted from the owner's 2026-08-11 ruling (general store · post office; the " +
                "restaurant is Nine Mile Creek's)");

            foreach (var site in Sites)
            {
                Assert.IsTrue(ShopCatalog.FindShell(site.Key).IsValid, $"{site.Key} has no shell baked");
                Assert.IsTrue(ShopCatalog.FindLevel(site.Key, ShopKit.GroundLevel).IsValid,
                    $"{site.Key} has no ground plan baked — and EVERY building gets an interior is canon " +
                    "since 2026-08-11, so a shop with no plan is a shop that must not be placed.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(site.Reason), $"{site.Key} has no authored reason");
            }
        }

        /// <summary>
        /// The general store is at the SAME coordinate it has had since #353. The owner ruled the shop
        /// replaces the house stand-in <i>at its authored site</i>, and three other things derive from
        /// that one number — the storekeeper's dooryard, the counter, and the store's whole economy.
        /// A "tidy-up" that nudged it would move all three without touching them.
        /// </summary>
        [Test]
        public void TheGeneralStoreDidNotMove_BecauseThreeOtherThingsAreDerivedFromWhereItIs()
        {
            var store = Sites.First(s => s.Key == "generalStore");
            Assert.AreEqual((Vector2)StPetersBuilder.GeneralStorePos, store.Position,
                "the store's site is StPetersBuilder.GeneralStorePos and must stay derived from it");

            Vector2 counter = StPetersBuilder.GeneralStoreCounterPos;
            Assert.Less(Vector2.Distance(counter, store.Position),
                        ShopCatalog.FootprintRadiusMetres(Shell("generalStore")) + 6f,
                        "the counter is no longer beside its own shop");
        }

        /// <summary>
        /// Every shop clears every house, every other shop, and the cottage — measured against the
        /// footprints the bake published, not against the numbers in a comment.
        ///
        /// <para>⚠️ The shop shell is BIGGER than the house that stood in for it: 8.00 × 10.00 m against
        /// 7.08 × 8.89, i.e. a footprint radius of 6.40 m where it was 5.68. Every clearance the general
        /// store is part of got tighter on 2026-08-11 and none of them was re-checked by anything that
        /// already existed.</para>
        /// </summary>
        [Test]
        public void EveryShopClearsEveryHouse_EveryOtherShop_AndTheCottage()
        {
            foreach (var site in Sites)
            {
                var shell = Shell(site.Key);
                float r = ShopCatalog.FootprintRadiusMetres(shell);

                foreach (var house in StPetersVillage.Sites)
                {
                    var hp = VillageBuildingCatalog.Find(house.Key);
                    if (!hp.IsValid) continue;

                    float need = r + StPetersVillage.FootprintRadiusMetres(hp) + StPetersVillage.LaneGap;
                    float d = Vector2.Distance(site.Position, house.Position);
                    Assert.Greater(d, need,
                        $"{site.Key} stands {d:0.0} m from the {house.Key} and needs {need:0.0} m " +
                        $"({r:0.0} + {StPetersVillage.FootprintRadiusMetres(hp):0.0} footprints + " +
                        $"{StPetersVillage.LaneGap:0.0} m of lane)");
                }

                foreach (var other in Sites)
                {
                    if (other.Key == site.Key) continue;
                    var op = ShopCatalog.FindShell(other.Key);
                    if (!op.IsValid) continue;

                    float need = r + ShopCatalog.FootprintRadiusMetres(op) + StPetersVillage.LaneGap;
                    float d = Vector2.Distance(site.Position, other.Position);
                    Assert.Greater(d, need,
                        $"{site.Key} stands {d:0.0} m from the {other.Key} and needs {need:0.0} m");
                }

                float toHearth = Vector2.Distance(site.Position, StPetersBuilder.VillageHearthPos);
                Assert.Greater(toHearth, r + StPetersVillage.HearthClearanceRadius,
                    $"{site.Key} is inside the hearth's authored clearance — nothing may crowd the " +
                    "ground the green is measured from");
            }
        }

        /// <summary>Both shops must sit inside the woods' clearing, or trees and shrubs plant through a
        /// wall. Re-derived from the contract's own footprints, so it fails with the number to use.</summary>
        [Test]
        public void BothShopsStandInsideTheWoodsClearing()
        {
            float need = StPetersShops.RequiredClearingRadius();
            Assert.Greater(need, 0f, "nothing is baked, so this would have passed vacuously");
            Assert.LessOrEqual(need, StPetersWoods.VillageClearingRadius,
                $"the furthest shop footprint reaches {need:0.0} m from the cottage against a " +
                $"{StPetersWoods.VillageClearingRadius:0.0} m clearing — widen the clearing or move the site");
        }

        // ---- the facings -----------------------------------------------------------------------

        /// <summary>
        /// Every shop's door is turned toward the green, and no other facing points it closer.
        ///
        /// <para>The error is measured from the bake's own door anchors by <see cref="BuildingFacing"/>,
        /// NOT re-derived from the same arithmetic the placer used. That distinction is the whole reason
        /// this test is worth anything: the village's equivalent used to restate its implementation's
        /// formula, reported 0° for every building, and sat green over a village whose saltbox door
        /// pointed a full half-turn away from the green it was supposed to face.</para>
        /// </summary>
        [Test]
        public void EveryShopDoorTurnsTowardTheGreen()
        {
            Vector2 green = StPetersBuilder.VillageGreen;

            foreach (var site in Sites)
            {
                var shell = Shell(site.Key);
                int facing = StPetersShops.FacingFor(shell, site);
                int facings = shell.Entry.facings;

                Assert.That(facing, Is.InRange(0, facings - 1),
                    $"{site.Key} was given facing {facing} of {facings}");

                float chosen = BuildingFacing.DoorErrorDegrees(
                    shell.Entry.doorY, shell.Entry.pivotY, facings,
                    SpriteLightMath.GroundDepthScale, site.Position, green, facing);

                for (int f = 0; f < facings; f++)
                    Assert.LessOrEqual(chosen, BuildingFacing.DoorErrorDegrees(
                        shell.Entry.doorY, shell.Entry.pivotY, facings,
                        SpriteLightMath.GroundDepthScale, site.Position, green, f) + 1e-3f,
                        $"{site.Key} faces {facing} but {f} points its door closer to the green");

                Assert.LessOrEqual(chosen, 180f / facings + 1e-3f,
                    $"{site.Key}'s door is {chosen:0.0}° off the green — worse than the " +
                    $"{180f / facings:0.0}° a nearest-of-{facings} rounding allows");
            }
        }

        /// <summary>The sprite the placer will ask for has to exist. A miss places NOTHING and logs a
        /// warning nobody reads — and the two ends of the name come from different places.</summary>
        [Test]
        public void EverySpriteThePlacerAsksFor_ExistsOnTheCommittedSheets()
        {
            foreach (var site in Sites)
            {
                var shell = Shell(site.Key);
                int facing = StPetersShops.FacingFor(shell, site);

                Sprite shellSprite = ShopCatalog.LoadFacing(shell, facing);
                Assert.IsNotNull(shellSprite,
                    $"the placer would ask for facing {facing} of {site.Key} and {shell.SheetPath} does " +
                    "not have it");
                Assert.AreEqual(ShopKit.SpriteNameFor(shell.Entry, facing), shellSprite.name);

                var level = ShopCatalog.FindLevel(site.Key, ShopKit.GroundLevel);
                int interior = ShopCatalog.InteriorFacingFor(facing);
                Assert.IsNotNull(ShopCatalog.LoadFacing(level, interior),
                    $"{site.Key}'s ground plan has no facing-{interior} sprite, so the door would open " +
                    "on nothing");
            }
        }

        // ---- the interiors ---------------------------------------------------------------------

        /// <summary>
        /// A shop's plan stands under its shell at the SAME facing — the contract's measured offset of 0.
        ///
        /// <para>⚠️ The house family's answer is 4, because <c>interiorIsoRig</c>'s door is on −Y. Carrying
        /// it here would put every shop's doorway against its back wall, and it would read as an art bug
        /// rather than a placement one.</para>
        /// </summary>
        [Test]
        public void TheInteriorFacingIsTheShellsOwn_NotTheHouseFamilysFour()
        {
            var contract = ShopKit.Load();
            if (contract == null) Assert.Ignore("no shop contract on this machine");

            Assert.AreEqual(0, contract.shellFacingOffset, "the measured shell↔level offset moved");

            for (int f = 0; f < ShopKit.Facings; f++)
                Assert.AreEqual(f, ShopCatalog.InteriorFacingFor(f),
                    $"exterior {f} should show interior {f}; a 4 here is the HOUSE kit's answer");
        }

        /// <summary>
        /// 🔴 <b>THE DOORWAY IS CUT WHERE THE DOOR IS DRAWN.</b> The post office's street door sits
        /// 1.68 m left of its wall's centre; a gap cut at the centre would block the door the player can
        /// see and open a hole in the wall beside it. Both would draw perfectly.
        ///
        /// <para>Measured against the bake's own anchors, and asserted as a WORLD-space agreement between
        /// two independent things: the anchor the bake wrote, and the threshold the collider leaves open.</para>
        /// </summary>
        [Test]
        public void TheWalkableGapLandsOnTheDrawnDoor_NotOnTheMiddleOfTheWall()
        {
            foreach (var site in Sites)
            {
                var level = ShopCatalog.FindLevel(site.Key, ShopKit.GroundLevel);
                if (!level.IsValid) Assert.Ignore($"{site.Key} has no baked plan");

                Vector2 door = ShopCatalog.DoorModelMetres(level);
                float sign = door.y >= 0f ? 1f : -1f;

                // The door must be IN a gable wall — |y| is half the footprint length, by construction.
                Assert.AreEqual(level.FootprintMetres.y * 0.5f, Mathf.Abs(door.y), 0.05f,
                    $"{site.Key}: the street door is {Mathf.Abs(door.y):0.00} m from the floor centre but " +
                    $"the room is {level.FootprintMetres.y:0.00} m deep — it is not in a gable wall, so " +
                    "everything below is measuring the wrong thing.");

                Assert.AreEqual(1f, sign,
                    $"{site.Key}: the shop kit's street door is on the +Y wall (its room is the shopfront " +
                    "seen from inside). A −1 here is the HOUSE kit's convention.");

                var footprint = new InteriorFootprint(
                    Vector2.zero, level.FootprintMetres.x, level.FootprintMetres.y,
                    facing: 0, facings: level.Entry.facings, SpriteLightMath.GroundDepthScale,
                    sign, door.x);

                // The threshold the geometry offers must be the door the bake drew.
                Vector2 threshold = footprint.DoorWorld;
                Vector2 drawn = new Vector2(door.x, door.y * SpriteLightMath.GroundDepthScale);
                Assert.AreEqual(drawn.x, threshold.x, 0.02f, $"{site.Key} threshold x");
                Assert.AreEqual(drawn.y, threshold.y, 0.02f, $"{site.Key} threshold y");

                // And the gap in the walls has to be AT it — not merely somewhere on that wall.
                Vector2[][] quads = footprint.WallQuads(StPetersShops.WallThicknessMetres,
                                                        StPetersShops.DoorwayWidthMetres);
                Assert.AreEqual(5, quads.Length, "back, left, right, and the front wall split in two");

                bool blocked = quads.Any(q => PolygonContains(q, threshold));
                Assert.IsFalse(blocked,
                    $"{site.Key}: the doorway the bake drew at model x={door.x:0.00} m is INSIDE a wall " +
                    "collider. A centred gap under an off-centre door is exactly this failure, and the " +
                    "room draws perfectly either way.");
            }
        }

        /// <summary>
        /// The off-centre door is not a hypothetical: at least one shop's must actually be off centre, or
        /// the test above is passing on the easy case and would keep passing if the offset were dropped.
        /// </summary>
        [Test]
        public void AtLeastOneShopsDoorIsGenuinelyOffCentre()
        {
            float worst = 0f;
            string which = null;

            foreach (var site in Sites)
            {
                var level = ShopCatalog.FindLevel(site.Key, ShopKit.GroundLevel);
                if (!level.IsValid) continue;

                float off = Mathf.Abs(ShopCatalog.DoorModelMetres(level).x);
                if (off > worst) { worst = off; which = site.Key; }
            }

            Assert.Greater(worst, 0.5f,
                "every shop's street door is centred on its gable, so the doorway-offset machinery is " +
                "untested by the placement. Measured on 2026-08-11 the post office's is 1.68 m off and " +
                $"the restaurant's 2.52 m — if that is genuinely no longer true, this test and " +
                "InteriorFootprint.DoorAcrossMetres should both be re-derived, not deleted.");

            Debug.Log($"[stpeters-shops] furthest off-centre street door: {which} at {worst:0.00} m.");
        }

        /// <summary>Even-odd point-in-polygon. The wall quads are sheared parallelograms at four of the
        /// eight facings, so a bounding-box test would be wrong exactly where it matters.</summary>
        static bool PolygonContains(Vector2[] poly, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                if (poly[i].y > p.y != poly[j].y > p.y &&
                    p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                    inside = !inside;
            return inside;
        }
    }
}
