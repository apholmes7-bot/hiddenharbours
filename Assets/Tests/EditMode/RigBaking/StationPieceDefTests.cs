using System.IO;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The gas station's content half: 23 <see cref="StationPieceDef"/> assets and their prefabs.
    ///
    /// <para>These check the two things a Def builder gets wrong silently — an id that moved, and a
    /// piece that carries art but no gameplay — plus the three rulings this kit is built on: doors
    /// default SHUT, the bipart slider's collider is the LEAF, and a null reach point is a REFUSAL
    /// rather than missing data.</para>
    /// </summary>
    public class StationPieceDefTests
    {
        static StationPieceDef[] All =>
            AssetDatabase.FindAssets($"t:{nameof(StationPieceDef)}",
                                     new[] { StationPieceDefBuilder.DefFolder })
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Select(AssetDatabase.LoadAssetAtPath<StationPieceDef>)
                         .Where(d => d != null)
                         .OrderBy(d => d.Id, System.StringComparer.Ordinal)
                         .ToArray();

        static StationPieceDef[] Built()
        {
            var all = All;
            if (all.Length == 0) Assert.Ignore("The Defs have not been built in this checkout.");
            return all;
        }

        // ---- identity ------------------------------------------------------------------------------

        /// <summary>
        /// One Def per baked sheet, with the ids the economy lane builds against. Ids are
        /// <b>append-only</b>: this list may grow, but an entry that CHANGES silently breaks every
        /// saved reference to it, which is why it is spelled out rather than derived.
        /// </summary>
        [Test]
        public void The_twenty_three_piece_ids_are_the_published_contract()
        {
            var expected = new[]
            {
                "station.canopy_sb2", "station.canopy_sb3", "station.canopy_sb4",
                "station.dispenser_sdock", "station.dispenser_skerb", "station.dispenser_slock",
                "station.dispenser_smpd", "station.dispenser_struck", "station.dispenser_stwin",
                "station.dispenser_svintage",
                "station.fillport_scluster", "station.fillport_svent",
                "station.interior_skiosk", "station.interior_sstore",
                "station.island_s1", "station.island_s2", "station.island_s3", "station.island_s4",
                "station.sign_saframe", "station.sign_spylon", "station.sign_stotem",
                "station.store_skiosk", "station.store_sstore",
            };

            Assert.That(Built().Select(d => d.Id).ToArray(), Is.EqualTo(expected),
                "Ids are append-only and never renamed. A changed id is not a rename — it is a new " +
                "piece plus a dangling reference from every scene and save that named the old one.");
        }

        /// <summary>
        /// The id lowercases the rig's camelCase size key because CLAUDE.md wants
        /// <c>type.snake_case</c>; the rig's own spelling has to survive on <c>SizeKey</c>, because
        /// that is what a re-bake matches against.
        /// </summary>
        [Test]
        public void The_rigs_own_size_spelling_survives_even_though_the_id_lowercases_it()
        {
            var twin = Built().Single(d => d.Id == "station.dispenser_stwin");
            Assert.That(twin.SizeKey, Is.EqualTo("sTwin"));
            Assert.That(twin.PieceType, Is.EqualTo("dispenser"));

            // And it is sLock, not the sCardlock the rig's own header comment names — that name
            // resolves to a KERB POST.
            Assert.That(Built().Any(d => d.SizeKey == "sLock"), Is.True);
            Assert.That(Built().Any(d => d.SizeKey == "sCardlock"), Is.False);
        }

        // ---- art -----------------------------------------------------------------------------------

        [Test]
        public void Every_piece_carries_its_baked_frames()
        {
            foreach (var d in Built())
            {
                Assert.That(d.HasArt, Is.True, $"{d.Id} has no sprite in frame 0.");
                Assert.That(d.Frames.Length, Is.EqualTo(d.Facings), d.Id);
                Assert.That(d.Frames.Any(f => f == null), Is.False,
                    $"{d.Id} has a null frame — the sheet was Multiple-mode with missing rects, which " +
                    "reads as 'imported' everywhere else.");
            }
        }

        /// <summary>
        /// A folded piece has FOUR frames covering EIGHT facings. <c>Frame(dir)</c> is the only safe
        /// accessor: <c>Frames[dir]</c> throws for half the compass, and a caller who "fixes" that by
        /// clamping shows the wrong side instead.
        /// </summary>
        [Test]
        public void A_folded_piece_answers_all_eight_facings_from_four_frames()
        {
            var canopy = Built().Single(d => d.Id == "station.canopy_sb4");

            Assert.That(canopy.Fold180, Is.True);
            Assert.That(canopy.Facings, Is.EqualTo(4));

            for (int dir = 0; dir < 8; dir++)
                Assert.That(canopy.Frame(dir), Is.Not.Null, $"dir {dir}");

            Assert.That(canopy.Frame(4), Is.SameAs(canopy.Frame(0)));
            Assert.That(canopy.Frame(7), Is.SameAs(canopy.Frame(3)));

            var pump = Built().Single(d => d.Id == "station.dispenser_stwin");
            Assert.That(pump.Fold180, Is.False);
            Assert.That(pump.Frame(4), Is.Not.SameAs(pump.Frame(0)));
        }

        // ---- gameplay --------------------------------------------------------------------------------

        /// <summary>
        /// Art without gameplay is the failure a Def builder ships silently. Every piece must carry
        /// SOMETHING the world can read — a blocker, a walkable, or an interactable.
        /// </summary>
        [Test]
        public void No_piece_carries_art_and_nothing_else()
        {
            foreach (var d in Built())
                Assert.That(d.Blockers.Length + d.Walkables.Length + d.Interactables.Length,
                            Is.GreaterThan(0),
                            $"{d.Id} has frames but no blockers, no walkables and nothing to use — " +
                            "the sidecar block was not found or not read.");
        }

        /// <summary>
        /// Treatment decides the collider, and only two of the four earn one. Giving the island's
        /// 0.165 m kerb a collider would fence the pumps off from the player who came to use them.
        /// </summary>
        [Test]
        public void Only_wall_and_waist_block_stop_a_body()
        {
            var known = new[] { "wall", "waist_block", "step_over", "flat" };

            foreach (var d in Built())
                foreach (var b in d.Blockers)
                {
                    Assert.That(known, Does.Contain(b.Treatment),
                        $"{d.Id}/{b.Kind}: unknown treatment '{b.Treatment}'. The runtime rules on " +
                        "this word; an unrecognised one is a blocker nobody knows what to do with.");
                    Assert.That(b.Blocks,
                                Is.EqualTo(b.Treatment == "wall" || b.Treatment == "waist_block"));
                }

            var island = Built().Single(d => d.Id == "station.island_s2");
            var kerb = island.Blockers.Single(b => b.Kind == "kerb");
            Assert.That(kerb.Treatment, Is.EqualTo("step_over"));
            Assert.That(kerb.Blocks, Is.False,
                "A kerb is crossed, not climbed — the sidecar calls the island top a kerb rather than " +
                "a floor for exactly this reason.");
        }

        /// <summary>
        /// ⚠️ <c>keep_clear: null</c> on the storefront entry is BY DESIGN, not missing data: a bipart
        /// slider sweeps no arc, so the collider is the LEAF, which travels inside the wall line.
        /// Reading that null as absent data leaves the shop's front wall with a hole in it.
        /// </summary>
        [Test]
        public void The_storefront_entry_is_a_bipart_slider_whose_collider_is_the_leaf()
        {
            var store = Built().Single(d => d.Id == "station.store_sstore");

            Assert.That(store.Entry.Kind, Is.EqualTo("slide_bipart"));
            Assert.That(store.Entry.HasKeepClear, Is.False,
                "No swept arc, so there is no volume to keep clear. This false is the design.");
            Assert.That(store.Entry.LeafWidthMeters, Is.GreaterThan(0f),
                "The leaf IS the collider, so its width has to survive the import.");
            Assert.That(store.Entry.ClearAtOpenFraction, Is.GreaterThan(0f).And.LessThan(1f),
                "How far open before 0.80 m of body fits.");

            // The service door is the opposite case, and the contrast is the point.
            Assert.That(store.ServiceDoor.Kind, Is.EqualTo("swing_single_out"));
            Assert.That(store.ServiceDoor.HasKeepClear, Is.True,
                "A single leaf swinging OUT does sweep — unlike the entry, this one owns a keep-clear.");
            Assert.That(store.ServiceDoor.KeepClear.Length, Is.GreaterThanOrEqualTo(3));
        }

        /// <summary>
        /// A reach point that could not be found is published as null WITH A REASON rather than as a
        /// plausible lie. The current bake nulls none of them — 45 checked, 42 as written, 3 repaired
        /// — so a false here is a real regression in the audit and not a data gap.
        /// </summary>
        [Test]
        public void Every_reach_point_was_tested_and_none_was_refused()
        {
            var all = Built().SelectMany(d => d.Interactables.Select(i => (d, i))).ToArray();

            Assert.That(all.Length, Is.EqualTo(45),
                "The kit's reach audit checked 45 interactables. A different count means the sidecar " +
                "moved and the Defs were built against a different one.");

            foreach (var (d, i) in all)
            {
                Assert.That(i.HasReachPoint, Is.True,
                    $"{d.Id}/{i.Id} has no reach point — the audit found nowhere inside arm's reach " +
                    $"that clears this piece's blockers ('{i.ReachVerdict}'). That is a refusal, not " +
                    "missing data, and it means the piece cannot be used where it stands.");
                Assert.That(i.ReachVerdict, Is.Not.Empty, $"{d.Id}/{i.Id}");
            }

            Assert.That(all.Count(x => x.i.ReachVerdict != "ok"), Is.EqualTo(3),
                "3 repaired, exactly as the bake reports: a display window nudged clear, and the " +
                "cooler and snack-aisle spots, which faced INTO the fixture they served.");
        }

        /// <summary>
        /// The two sprite layers of one building split their gameplay rather than duplicating it: the
        /// shell owns the apron and the doors, the room owns the sales floor and the six things on it.
        /// </summary>
        [Test]
        public void The_shell_and_its_sales_floor_split_the_building_rather_than_repeating_it()
        {
            var shell = Built().Single(d => d.Id == "station.store_sstore");
            var room = Built().Single(d => d.Id == "station.interior_sstore");

            Assert.That(room.IsInterior, Is.True);
            Assert.That(shell.IsInterior, Is.False);

            Assert.That(room.Interactables.Select(i => i.Id).OrderBy(x => x).ToArray(),
                        Is.EqualTo(new[] { "atm", "coffee", "cooler", "hotcase", "snacks", "till" }));
            Assert.That(room.Interactables.All(i => i.Level == "sales_floor"), Is.True);

            Assert.That(shell.Interactables.Any(i => i.Level == "sales_floor"), Is.False,
                "The room's six are NOT repeated on the shell — one building, two layers, each Def " +
                "owning its own layer.");
            Assert.That(shell.Interactables.Select(i => i.Id), Does.Contain("ice_box"));

            Assert.That(room.Walkables.Single().Id, Is.EqualTo("sales_floor"));
            Assert.That(shell.Walkables.Select(w => w.Id),
                        Is.EquivalentTo(new[] { "walkway", "service_stoop", "roof_deck" }));

            // The room's fit-out is measured above the SOLE, and the 2.10 m gantry is the only
            // wall-height fixture in it.
            Assert.That(room.Blockers.Length, Is.GreaterThan(0));
            Assert.That(room.Blockers.All(b => b.Level == "sales_floor"), Is.True);
        }

        [Test]
        public void The_dispensers_carry_their_hose_count_and_flow()
        {
            var truck = Built().Single(d => d.Id == "station.dispenser_struck");
            Assert.That(truck.MaxHoses, Is.GreaterThan(0));
            Assert.That(truck.LitresPerMinute, Is.EqualTo(130f).Within(0.01f),
                "The high-flow pump is what fuels the boats and the rigs.");
            Assert.That(truck.HoseReachMeters, Is.EqualTo(6.2f).Within(0.01f));

            var kerb = Built().Single(d => d.Id == "station.dispenser_skerb");
            Assert.That(kerb.LitresPerMinute, Is.EqualTo(38f).Within(0.01f));

            // Hoses are NEVER baked — the reach is a number, not a sprite.
            Assert.That(Built().All(d => d.PieceType != "dispenser" || d.HoseReachMeters > 0f), Is.True);
        }

        [Test]
        public void The_islands_carry_their_slots_on_the_kits_one_pitch()
        {
            foreach (var d in Built().Where(x => x.PieceType == "island"))
            {
                Assert.That(d.Slots, Is.GreaterThan(0), d.Id);
                Assert.That(d.SlotPitchMeters, Is.EqualTo(3.3f).Within(0.01f),
                    $"{d.Id}: one pitch across the whole kit, so any variety mounts in any slot.");
            }

            Assert.That(Built().Single(d => d.Id == "station.island_s4").Slots, Is.EqualTo(4));
        }

        [Test]
        public void The_canopies_carry_the_clearance_a_vehicle_has_to_fit_under()
        {
            foreach (var d in Built().Where(x => x.PieceType == "canopy"))
                Assert.That(d.ClearHeightMeters, Is.EqualTo(4.55f).Within(0.01f),
                    $"{d.Id}: clear height is to the underside, and the fascia is the lowest edge.");
        }

        // ---- prefabs -------------------------------------------------------------------------------------

        [Test]
        public void Every_piece_has_a_prefab_that_draws_and_collides_where_it_should()
        {
            foreach (var d in Built())
            {
                string path = $"{StationPieceDefBuilder.PrefabFolder}/{d.name}.prefab";
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(go, Is.Not.Null, $"{d.Id}: no prefab at {path}.");

                var sr = go.GetComponent<SpriteRenderer>();
                Assert.That(sr, Is.Not.Null, d.Id);
                Assert.That(sr.sprite, Is.Not.Null, $"{d.Id}: prefab draws nothing.");
                Assert.That(sr.spriteSortPoint, Is.EqualTo(SpriteSortPoint.Pivot),
                    $"{d.Id}: the pivot is the ground centre of the footprint, so it is what sorts.");

                int wanted = d.Blockers.Count(b => b.Blocks);
                int got = go.GetComponentsInChildren<Collider2D>(true)
                            .Count(c => !(c is BoxCollider2D && c.name.StartsWith("door_")));
                Assert.That(got, Is.EqualTo(wanted),
                    $"{d.Id}: {got} blocker colliders for {wanted} blocking blockers. step_over and " +
                    "flat must NOT earn one.");
            }
        }

        /// <summary>
        /// Every piece Y-sorts in the ADR 0032 band — a forecourt stands among boats, buildings and
        /// people and has to interleave with all of them.
        /// </summary>
        [Test]
        public void Every_prefab_y_sorts_in_the_band()
        {
            foreach (var d in Built())
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"{StationPieceDefBuilder.PrefabFolder}/{d.name}.prefab");
                var sort = go.GetComponent<YSortSprite>();
                Assert.That(sort, Is.Not.Null,
                    $"{d.Id}: no YSortSprite, so it would sit on a fixed order and either swallow or " +
                    "be swallowed by everything it stands next to.");

                var so = new SerializedObject(sort);
                Assert.That(so.FindProperty("_minOrder").intValue,
                            Is.EqualTo(SortingBands.DecorFloor), d.Id);
                Assert.That(so.FindProperty("_maxOrder").intValue,
                            Is.EqualTo(SortingBands.DecorCeiling), d.Id);
                Assert.That(so.FindProperty("_orderPerUnit").floatValue,
                            Is.EqualTo(SortingBands.OrdersPerMetre), d.Id);
            }
        }

        /// <summary>
        /// The room sorts UNDER its own shell at the identical world position.
        ///
        /// <para>⚠️ The offset has to live in <c>_baseOrder</c>, not in
        /// <c>SpriteRenderer.sortingOrder</c>: they share a cell AND a pivot, so nothing about their
        /// positions separates them, and a hand-set <c>sortingOrder</c> is overwritten the moment
        /// <c>YSortSprite.OnEnable</c> runs. A shipped <c>sortingOrder</c> is a cached band output,
        /// so this asserts the base rather than that cache.</para>
        /// </summary>
        [Test]
        public void A_sales_floor_sorts_one_step_under_its_shell_at_every_y()
        {
            foreach (var room in Built().Where(x => x.IsInterior))
            {
                var shell = Built().Single(x => !x.IsInterior && x.PieceType == "store"
                                                && x.SizeKey == room.SizeKey);

                float roomBase = BaseOrderOf(room);
                float shellBase = BaseOrderOf(shell);

                Assert.That(shellBase, Is.EqualTo((float)SortingBands.DecorBase), shell.Id);
                Assert.That(roomBase, Is.EqualTo(shellBase - 1f),
                    $"{room.Id} must sit exactly one order under {shell.Id} at EVERY y — otherwise " +
                    "the storefront's walls vanish behind their own floor.");
            }
        }

        static float BaseOrderOf(StationPieceDef d)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{StationPieceDefBuilder.PrefabFolder}/{d.name}.prefab");
            return new SerializedObject(go.GetComponent<YSortSprite>())
                   .FindProperty("_baseOrder").floatValue;
        }

        /// <summary>Doors default SHUT (owner ruling, 2026-08-19), and a shut door is a collider.</summary>
        [Test]
        public void A_storefronts_doors_are_shut_and_that_means_a_collider()
        {
            foreach (string id in new[] { "station.store_sstore", "station.store_skiosk" })
            {
                var d = Built().Single(x => x.Id == id);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"{StationPieceDefBuilder.PrefabFolder}/{d.name}.prefab");

                var doors = go.GetComponentsInChildren<Collider2D>(true)
                              .Where(c => c.name.StartsWith("door_")).ToArray();
                Assert.That(doors.Length, Is.GreaterThan(0),
                    $"{id}: a shut door with no collider is a doorway you can walk through while it " +
                    "is closed.");
                Assert.That(doors.Any(c => c.name.Contains("Entry")), Is.True, id);
            }
        }

        [Test]
        public void The_defs_and_the_bake_contract_describe_the_same_kit()
        {
            var contract = GasStationSheetSlicer.LoadContract();
            if (contract == null) Assert.Ignore("The kit has not been baked in this checkout.");

            Assert.That(Built().Length, Is.EqualTo(contract.sheets.Length));

            foreach (var s in contract.sheets)
            {
                var d = Built().SingleOrDefault(x => x.Id == StationPieceDefBuilder.IdFor(s.type, s.size));
                Assert.That(d, Is.Not.Null, $"No Def for baked sheet {s.key}.");
                Assert.That(d.Facings, Is.EqualTo(s.facings), s.key);
                Assert.That(d.Fold180, Is.EqualTo(s.fold180), s.key);
                Assert.That(d.BakedGrades, Is.EqualTo(s.grades), s.key);
            }
        }

        [Test]
        public void Every_def_asset_sits_beside_its_own_file_one_entity_per_file()
        {
            foreach (var d in Built())
            {
                string path = AssetDatabase.GetAssetPath(d);
                Assert.That(Path.GetFileNameWithoutExtension(path), Is.EqualTo(d.name));
                Assert.That(AssetDatabase.LoadAllAssetsAtPath(path).Length, Is.EqualTo(1),
                    $"{d.Id}: one entity per file (CLAUDE.md rule 2).");
            }
        }
    }
}
