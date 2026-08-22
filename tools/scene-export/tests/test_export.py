"""Tests for the scene exporter. Run: ``python3 -m unittest discover tools/scene-export/tests``

These pin the contract lead-architect settled from the editor's reference package (PR #588,
recorded as citations in ``docs/tools/scene-export-contract.md``) — the envelope's shape, RLE
coverage and row order, the region table coming from the RegionDef, the LF-sha256 convention,
``sortBias`` as a tie-break delta rather than an absolute order, and pivots that can never be
the cell-box fallback — plus the determinism claim the PR makes.
"""

import datetime
import copy
import json
import math
import os
import re
import tempfile
import subprocess
import sys
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
TOOL = os.path.dirname(HERE)
REPO = os.path.dirname(os.path.dirname(TOOL))
sys.path.insert(0, TOOL)

REFERENCE = "docs/tools/reference/sample-scene.json"

from hhexport import contracts, families, heightmap, package, recipes, roads  # noqa: E402
from hhexport import facing as facing_mod, unityyaml as U  # noqa: E402
from hhexport.repo import Repo, sha256_lf  # noqa: E402
from hhexport.scene import Scene  # noqa: E402

import hh_scene_export  # noqa: E402


class UnityYamlTests(unittest.TestCase):
    def test_block_sequence_sits_at_the_key_indent(self):
        docs = U.parse(
            "--- !u!1 &5\n"
            "GameObject:\n"
            "  m_Component:\n"
            "  - component: {fileID: 6}\n"
            "  - component: {fileID: 7}\n"
            "  m_Name: Wharf\n"
        )
        self.assertEqual(len(docs), 1)
        self.assertEqual(docs[0].class_id, 1)
        self.assertEqual(docs[0].file_id, 5)
        self.assertEqual(docs[0].data["m_Name"], "Wharf")
        self.assertEqual([U.ref(c["component"]) for c in docs[0].data["m_Component"]], [6, 7])

    def test_packed_hex_arrays_survive_as_text(self):
        """The bug this reader exists to avoid: YAML 1.1 reads an all-digit hex blob as octal."""
        docs = U.parse("--- !u!114 &1\nMonoBehaviour:\n  _viaStart: 0000000002000000\n")
        self.assertEqual(docs[0].data["_viaStart"], "0000000002000000")
        self.assertEqual(package._packed_ints("0000000002000000"), [0, 2])

    def test_packed_ints_decode_signed_little_endian(self):
        self.assertEqual(package._packed_ints("ffffffff00000000"), [-1, 0])
        self.assertEqual(package._packed_ints("not-hex"), [])

    def test_nested_sequences(self):
        docs = U.parse(
            "--- !u!60 &1\nPolygonCollider2D:\n"
            "  m_Points:\n"
            "    m_Paths:\n"
            "    - - {x: 1, y: 2}\n"
            "      - {x: 3, y: 4}\n"
            "  m_UseDelaunayMesh: 1\n"
        )
        paths = docs[0].data["m_Points"]["m_Paths"]
        self.assertEqual(len(paths), 1)
        self.assertEqual([U.as_float(p["x"]) for p in paths[0]], [1.0, 3.0])
        self.assertEqual(docs[0].data["m_UseDelaunayMesh"], "1")

    def test_quoted_scalar_folds_across_lines(self):
        docs = U.parse(
            "--- !u!114 &1\nMonoBehaviour:\n"
            "  Description: 'A working wharf: a squared-U quay\n"
            "    at the creek''s mouth.'\n"
            "  Id: region.x\n"
        )
        self.assertIn("creek's mouth", docs[0].data["Description"])
        self.assertEqual(docs[0].data["Id"], "region.x")


class RigPinningTests(unittest.TestCase):
    def test_lf_sha256_matches_the_value_the_road_kit_published(self):
        """The kit committed its own pin months ago; reproducing it proves the convention."""
        published = "d45e9ac657eb42daf5dc22f61068bf0ff1f0ee65152d6a4ca827a77ef0a5ee3c"
        rig = os.path.join(REPO, "docs/art/rigs/road-path-kit-v3/roadPathRig3.js")
        self.assertEqual(sha256_lf(rig), published)

    def test_lf_sha256_matches_the_posix_shell_pipeline(self):
        rig = os.path.join(REPO, "docs/art/rigs/road-path-kit-v3/roadPathRig3.js")
        shell = subprocess.run(
            f"tr -d '\\r' < {rig} | sha256sum", shell=True, capture_output=True, text=True,
        ).stdout.split()[0]
        self.assertEqual(sha256_lf(rig), shell)

    def test_a_sheet_with_no_trustworthy_sidecar_resolves_to_nothing(self):
        """Art/Boats holds four per-hull anchor files and no index — a dory must not take one."""
        repo = Repo(REPO)
        name, source, _, _ = repo.rig_for_sheet("Assets/_Project/Art/Boats/DoryIso.png")
        self.assertIsNone(name)
        self.assertIsNone(source)

    def test_a_declared_rig_beats_a_stale_prose_mention(self):
        """Trees.json declares treeIsoRig2 while its own note still credits the v1 rig."""
        repo = Repo(REPO)
        name, source, _, _ = repo.rig_for_sheet(
            "Assets/_Project/Art/Foliage/Trees/RedMaple_mature_summer.png")
        self.assertEqual(name, "treeIsoRig2")
        self.assertEqual(source, "docs/art/rigs/treeIsoRig2.js")


class PackageTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.repo = Repo(REPO)
        cls.documents = {
            name: hh_scene_export.export_region(cls.repo, name, scene, height)
            for name, scene, height in hh_scene_export.REGIONS
        }

    def test_region_frame_comes_from_the_region_def(self):
        """Review §6.2: the editor's own table had Nine Mile Creek at the C# field default."""
        creek = self.documents["NineMileCreek"]
        self.assertEqual(creek["region"]["worldSizeMeters"], [760, 560])
        self.assertEqual(creek["terrain"]["originNW"], [-380, 280])
        peters = self.documents["StPeters"]
        self.assertEqual(peters["region"]["worldSizeMeters"], [760, 520])
        self.assertEqual(peters["terrain"]["originNW"], [-380, 260])

    def test_every_rle_covers_the_grid_exactly(self):
        """Review §5 Q1 / §8.2: a short stream is indistinguishable from a truncated one."""
        for name, document in self.documents.items():
            terrain = document["terrain"]
            expected = terrain["cols"] * terrain["rows"]
            for layer, block in terrain["layers"].items():
                total = sum(count for _value, count in block["rle"])
                self.assertEqual(total, expected, f"{name}.{layer} covers {total} of {expected}")

    def test_derived_layers_are_marked_derived(self):
        for document in self.documents.values():
            for block in document["terrain"]["layers"].values():
                self.assertTrue(block["x-derived"])
                self.assertTrue(block["x-readOnly"])
                self.assertFalse(block["x-authorable"])

    def test_pivots_are_normalised_and_never_the_cell_box_fallback(self):
        """Review §8.1: the editor falls back to the cell edge for render-anchored rigs."""
        for name, document in self.documents.items():
            for entity in document["entities"]:
                if entity["cell"] is None:
                    continue
                x, y = entity["cell"]["unityPivot"]
                self.assertTrue(0.0 <= x <= 1.0 and 0.0 <= y <= 1.0, f"{name} {entity['id']}")
                self.assertTrue(entity["x-pivotSource"].startswith("sprite-import."))

    def test_the_two_pivot_forms_are_exact_inverses(self):
        """Both ship together (contract §2 #11); ADR 0026 makes disagreement impossible."""
        for name, document in self.documents.items():
            for entity in document["entities"]:
                cell = entity["cell"]
                if cell is None:
                    continue
                px, py = cell["pivot"]
                ux, uy = cell["unityPivot"]
                # Both forms are rounded for stable bytes, so they agree to within rounding —
                # a hundredth of a pixel, which is the invariant that matters: they can never
                # disagree by anything a reader could see.
                self.assertLess(abs(px - cell["w"] * ux), 0.01, f"{name} {entity['id']}")
                self.assertLess(abs(py - cell["h"] * (1.0 - uy)), 0.01, f"{name} {entity['id']}")

    def test_the_shape_matches_the_reference_package_block_for_block(self):
        """Against the editor's own `sample-scene.json`, committed under docs/tools/reference/.

        Stronger than a hand-written key list, and the reason that file was worth committing:
        the reference is the contract, so a drift in either direction shows up here. Entities
        are a SUBSET check — `call`, `facing`, `facingIndex` and `gameplaySidecar` are
        unknowable from a baked sheet — but the keys that are present must be the format's, in
        the format's order.
        """
        with open(os.path.join(REPO, REFERENCE), encoding="utf-8") as handle:
            reference = json.load(handle)

        # Keys the editor RULED IN after the reference sample was written. Named one by one and
        # dated, so this stays a drift detector: an unruled key still fails here.
        ruled_in = {
            "terrain": ("waterLevelMeters",),      # coordinator relay, 2026-08-20 evening, ask 4
        }

        def named(mapping, block=None):
            allowed = ruled_in.get(block, ())
            return [k for k in mapping if not k.startswith("x-") and k not in allowed]

        for name, document in self.documents.items():
            self.assertEqual(named(document), named(reference), name)
            for block in ("region", "frame", "terrain"):
                self.assertEqual(named(document[block], block), named(reference[block], block),
                                 f"{name}.{block}")
            # The ruled-in key must actually be present, or "allowed" would quietly mean "absent".
            self.assertIn("waterLevelMeters", document["terrain"], f"{name}.terrain")
            for layer in reference["terrain"]["layers"]:
                self.assertEqual(named(document["terrain"]["layers"][layer]),
                                 named(reference["terrain"]["layers"][layer]),
                                 f"{name}.terrain.layers.{layer}")
            expected = named(reference["entities"][0])
            for entity in document["entities"]:
                keys = named(entity)
                self.assertEqual(keys, [k for k in expected if k in keys],
                                 f"{name} {entity['id']} is not the format's order")
                self.assertTrue(set(keys) <= set(expected), f"{name} {entity['id']}")
            for lane in document["paths"]:
                self.assertEqual(named(lane), named(reference["paths"][0]),
                                 f"{name} {lane['id']}")
            self.assertEqual(document["schema"], reference["schema"], name)
            self.assertNotIn("format", document, f"{name} still uses the pre-ruling key")

    def test_pos_is_metres_from_the_region_centre(self):
        """Contract §2 #3: `pos: [x, y]`, metres, origin = region centre — and the grid agrees."""
        for name, document in self.documents.items():
            centre = document["region"]["worldCenter"]
            width, height = document["region"]["worldSizeMeters"]
            for entity in document["entities"]:
                if not entity["x-inBounds"]:
                    continue
                x, y = entity["pos"]
                self.assertLessEqual(abs(x), width, f"{name} {entity['id']}")
                self.assertLessEqual(abs(y), height, f"{name} {entity['id']}")
                # row 0 is the north edge (§2 #6), so the row grows as y falls.
                column = math.floor(x + centre[0] - (centre[0] - width / 2.0))
                row = math.floor((centre[1] + height / 2.0) - (y + centre[1]))
                self.assertEqual([column, row], entity["x-cellAt"], f"{name} {entity['id']}")

    def test_sort_bias_is_a_tie_break_delta_not_an_absolute_order(self):
        """Contract §2 #12. `YSortSprite._baseOrder` sits around 1202; a bias does not."""
        decor_base = self.repo.decor_base()
        self.assertEqual(decor_base, 1202)
        for name, document in self.documents.items():
            for entity in document["entities"]:
                self.assertLess(abs(entity["sortBias"]), decor_base / 2,
                                f"{name} {entity['id']} looks like an absolute order")

    def test_the_legend_is_top_level_and_layers_carry_none(self):
        """Contract §2 #5: one `terrain.legend`; layer objects have no legend of their own."""
        for name, document in self.documents.items():
            terrain = document["terrain"]
            self.assertIn("legend", terrain, name)
            self.assertIn("row 0 = north edge", terrain["note"], name)
            for layer_name, layer in terrain["layers"].items():
                self.assertNotIn("legend", layer, f"{name}.{layer_name}")

    def test_a_polyline_lane_needs_no_smoothing(self):
        """Our lanes are straight runs through explicit bends, so `polyline` == `nodes`."""
        for name, document in self.documents.items():
            for lane in document["paths"]:
                self.assertEqual(lane["curve"]["kind"], "polyline", f"{name} {lane['id']}")
                self.assertEqual(lane["nodes"], lane["polyline"], f"{name} {lane['id']}")

    def test_the_ppu_is_the_sprite_grid_and_never_the_water_shader_grid(self):
        """Two grids live in this repo and conflating them is a whole class of bug.

        32 is ``CameraFollow.AssetsPPU`` (``const int``, "one PPU never changes") and is what
        every sheet's import settings carry. 24 is a *material property* — every water material
        and preset sets ``_PixelsPerUnit: 24`` for the shader's own sampling grid, over a shader
        whose declared default is 32. The export takes its number from the import settings, so
        the water grid has no path into a placement.
        """
        for name, document in self.documents.items():
            self.assertEqual(document["frame"]["scale_px_per_m"], 32, name)
            for entity in document["entities"]:
                if "x-ppu" in entity:
                    self.assertEqual(entity["x-ppu"], 32, f"{name} {entity['id']}")

    def test_every_named_rig_is_pinned_and_on_disk(self):
        for name, document in self.documents.items():
            for rig in document["x-rigs"]:
                self.assertTrue(self.repo.exists(rig["rigSource"]), rig["rigSource"])
                self.assertEqual(rig["sha256"], sha256_lf(self.repo.abs(rig["rigSource"])))
            pinned = {rig["rigSource"] for rig in document["x-rigs"]}
            for entity in document["entities"]:
                if entity["rigSource"]:
                    self.assertIn(entity["rigSource"], pinned, f"{name} {entity['id']}")
                    self.assertIsNotNone(entity["x-rigSha256"])

    def test_entity_ids_are_unique_within_a_document(self):
        for name, document in self.documents.items():
            ids = [entity["id"] for entity in document["entities"]]
            self.assertEqual(len(ids), len(set(ids)), name)

    def test_lanes_export_as_polylines_between_their_own_nodes(self):
        peters = self.documents["StPeters"]
        self.assertTrue(peters["paths"], "St Peters has a RoutineLanes table")
        scene = Scene(U.parse_file(os.path.join(REPO, "Assets/_Project/Scenes/StPeters.unity")))
        nodes = []
        for component in scene.behaviours.values():
            guid = U.ref_guid(component.data.get("m_Script"))
            path = self.repo.path_for_guid(guid) if guid else None
            if path and os.path.basename(path) == "RoutineLanes.cs":
                nodes = [[round(v, 6) for v in U.vec(p, "x", "y")]
                         for p in component.data.get("_nodePositions") or []]
        for lane in peters["paths"]:
            self.assertGreaterEqual(len(lane["nodes"]), 2)
            self.assertTrue(lane["x-readOnly"])
            for end in (lane["nodes"][0], lane["nodes"][-1]):
                self.assertIn([round(float(end[0]), 6), round(float(end[1]), 6)], nodes)

    def test_the_document_records_how_stale_the_scene_is(self):
        for document in self.documents.values():
            drift = document["x-provenance"]["builderDrift"]
            self.assertIsNotNone(document["x-provenance"]["sceneLastBuiltCommit"])
            self.assertIsNotNone(drift["measuredTo"])
            self.assertIsNotNone(drift["builderCommitsSinceScene"])
            # A shallow clone can only give a floor; the package must say which it gave.
            self.assertEqual(drift["exact"], document["x-provenance"]["historyIsComplete"])
            self.assertEqual("x-note" in drift, not drift["exact"])

    def test_generated_at_is_an_input_date_not_a_wall_clock(self):
        """It must be reproducible, so it is the newest input commit's own committer date."""
        for name, document in self.documents.items():
            stamp = document["generatedAt"]
            self.assertRegex(stamp, r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$", name)
            commit = document["x-provenance"]["builderDrift"]["measuredTo"]
            actual = subprocess.run(
                ["git", "log", "-1", "--format=%cI", commit], cwd=REPO,
                capture_output=True, text=True).stdout.strip()
            expected = (
                datetime.datetime.fromisoformat(actual)
                .astimezone(datetime.timezone.utc)
                .strftime("%Y-%m-%dT%H:%M:%SZ"))
            self.assertEqual(stamp, expected, name)

    def test_the_height_map_is_pinned_even_though_its_bytes_are_absent(self):
        for document in self.documents.values():
            height = document["terrain"]["x-heightMap"]
            self.assertIsNotNone(height)
            self.assertIsNotNone(height["textureSha256"])


class EnrichmentTests(unittest.TestCase):
    """Rasterised layers and renderable entities — the coordinator's enrichment spec."""

    @classmethod
    def setUpClass(cls):
        cls.repo = Repo(REPO)
        cls.documents = {
            name: hh_scene_export.export_region(cls.repo, name, scene, height)
            for name, scene, height in hh_scene_export.REGIONS
        }

    def test_every_legend_key_is_used_and_every_rle_value_is_a_legend_key(self):
        """Contract §2 #5: the legend's keys ARE the RLE values. `0` is not one of them."""
        for name, document in self.documents.items():
            terrain = document["terrain"]
            legend = set(terrain["legend"])
            used = set()
            for layer_name, layer in terrain["layers"].items():
                for value, _count in layer["rle"]:
                    if value == 0:
                        continue
                    self.assertIn(value, legend, f"{name}.{layer_name} uses an unlisted {value}")
                    self.assertEqual(terrain["legend"][value]["layer"], layer_name, f"{name} {value}")
                    used.add(value)
            self.assertEqual(used, legend, f"{name} declares a legend key nothing uses")
            self.assertNotIn(0, legend, f"{name}: 0 is the reserved no-tile value, not a key")

    def test_the_road_layer_is_stroked_from_the_declared_ways(self):
        """Nine Mile Creek declares four ways; the painted count must match a fresh stroke."""
        ways, omitted = roads.read_ways(
            self.repo,
            "Assets/_Project/Code/App/Editor/NineMileCreekRoads.cs",
            "Assets/_Project/Code/App/Editor/NineMileCreekMainland.cs")
        self.assertTrue(ways, "no declared way was found — the reader has drifted off the table")
        self.assertTrue(omitted, "the computed ways must be reported, not silently dropped")
        document = self.documents["NineMileCreek"]
        terrain = document["terrain"]
        grid = roads.rasterise(ways, terrain["cols"], terrain["rows"], terrain["originNW"])
        self.assertEqual(document["stats"]["tiles"]["road"], sum(1 for c in grid if c))
        surfaces = {w.surface for w in ways}
        listed = {v["material"] for v in terrain["legend"].values() if v["layer"] == "road"}
        self.assertEqual(listed, surfaces, "every stroked surface needs a legend entry")

    def test_a_region_with_no_road_table_says_so_rather_than_painting_nothing_quietly(self):
        road = self.documents["StPeters"]["terrain"]["layers"]["road"]
        self.assertEqual(road["rle"], [[0, 395200]])
        self.assertIn("declares no road table", road["x-unavailable"])

    def test_the_ground_contour_reads_a_real_height_png(self):
        """The LFS-present branch, exercised against a PNG built here — the only way to have any
        confidence in a path this container's pointer-only checkout never takes."""
        rows = [bytes([0, 128, 255, 255]), bytes([0, 0, 200, 255]), bytes([0, 0, 0, 90])]
        png = _greyscale_png(4, 3, rows)
        decoded = heightmap.decode_r8(png)
        self.assertIsNotNone(decoded)
        self.assertEqual([list(r) for r in decoded.rows], [list(r) for r in rows])
        self.assertIsNone(heightmap.decode_r8(b"version https://git-lfs.github.com/spec/v1"),
                          "an LFS pointer must be refused, not coerced into an elevation field")
        floor, bands = heightmap.read_bands(self.repo)
        self.assertIsNotNone(floor)
        self.assertEqual([n for n, _f in bands], ["grass", "marram", "sand", "ripple"])
        self.assertEqual(sorted((f for _n, f in bands), reverse=True), [f for _n, f in bands],
                         "the band floors must descend, or the ladder picks the wrong band")

    def test_the_ground_layer_declares_which_honesty_level_it_produced(self):
        for name, document in self.documents.items():
            ground = document["terrain"]["layers"]["ground"]
            painted = document["stats"]["tiles"]["ground"] > 0
            if painted:
                self.assertIn("iso-contour", ground["x-derived"], name)
                self.assertNotIn("x-unavailable", ground, name)
            else:
                self.assertIn("x-unavailable", ground, name)

    def test_families_come_from_the_editors_wire_list_or_say_they_do_not(self):
        """The rule is about PROVENANCE, not about which names happen to be on the list.

        `x-familyIsSpriteStem` means "no rig resolved onto a listed name here, so this is the
        sprite stem standing in" — a statement about where the value came from. This used to
        also assert that such a stem is never itself a listed name, which held only by accident:
        the editor published `camper` as its 45th entry, a St Peters sprite stem is `camper`, and
        a true claim about provenance started failing on a coincidence of spelling. The count is
        gone with it — a growing wire list is the lane working, and a test that has to be edited
        every time the editor publishes an entry is a test nobody trusts.
        """
        wire, layers = families.load(self.repo)
        self.assertGreaterEqual(len(wire), 45, "the transcribed wire list has shrunk")
        self.assertEqual(sorted(layers), ["cliff", "ground", "road", "texture", "wharfdeck"])
        stems = resolved = 0
        for name, document in self.documents.items():
            for entity in document["entities"]:
                if entity["x-familyIsSpriteStem"]:
                    # It must SAY it is a stem and carry what it would have matched on.
                    self.assertIn("x-familyCandidate", entity, f"{name} {entity['id']}")
                    self.assertIn("x-spriteStem", entity, f"{name} {entity['id']}")
                    self.assertEqual(entity["family"], entity["x-spriteStem"],
                                     f"{name} {entity['id']} flags a stem it did not use")
                    stems += 1
                else:
                    self.assertIn(entity["family"], wire, f"{name} {entity['id']}")
                    self.assertNotIn("x-familyCandidate", entity,
                                     f"{name} {entity['id']} resolved but still asks to be listed")
                    resolved += 1
        self.assertGreater(stems, 0, "no entity exercises the fallback")
        self.assertGreater(resolved, 0, "no entity exercises resolution")

    def test_a_near_miss_is_never_aliased_onto_a_neighbour(self):
        """`wharfIsoRig` normalises to `wharf`, which the wire list does not carry.

        It held `wharfbuilding` AND `wharfmodule`, so for two rounds this export declared the
        remainder rather than pick one. The editor published `wharfmodule` on 2026-08-21 and the
        coordinator ruled the mapping, so it resolves now — **by a one-line declared table, never
        by a loosened match rule.** The guard therefore changed shape rather than being deleted:
        a candidate with no ruling behind it must still refuse.
        """
        creek = self.documents["NineMileCreek"]
        wharf = [e for e in creek["entities"]
                 if (e["rigSource"] or "").endswith("wharfIsoRig.js")]
        self.assertTrue(wharf, "the wharf kit no longer resolves — this guard has gone blind")
        for entity in wharf:
            self.assertEqual(entity["family"], "wharfmodule", entity["id"])
            self.assertFalse(entity["x-familyIsSpriteStem"], entity["id"])
            self.assertNotEqual(entity["family"], "wharfbuilding",
                                "resolved onto the OTHER neighbour — the ruling picked module")
        # The request list is empty of it now: a ruling closes an entry, it does not linger.
        self.assertNotIn("wharf", creek["x-provenance"]["entityNotes"]["unlistedFamilies"])

    def test_the_interior_gap_closed_when_the_editor_published_the_entries(self):
        """This used to assert the opposite: `interior`/`interiorprop` had no family and stayed
        declared. The editor published both on 2026-08-20, so the request list is empty for
        St Peters and every interior placement resolves. Kept as the record of a gap that closed
        the way the no-aliasing rule intended — by a ruling, not by us bending a name.

        The count is gone. It pinned 30, the scene now holds 43, and the difference is #599
        furnishing more rooms — content doing exactly what content is supposed to do. A count
        here could only ever fail for the right reason by accident; what the gap closing MEANS is
        that none of them is a stem any more, and that is what is asserted.
        """
        peters = self.documents["StPeters"]
        unlisted = peters["x-provenance"]["entityNotes"]["unlistedFamilies"]
        # Scoped to this gap. It used to assert the whole request list was empty, which coupled
        # a test about INTERIORS to every other kit in the repo: reading the shop contract
        # surfaced `shopbuilding` — a real, correctly-reported ask — and an unrelated test went
        # red for it. A closed gap is a claim about the names that closed, not about the list.
        self.assertFalse([f for f in unlisted if f.startswith("interior")],
                         f"St Peters has an unlisted interior family again: {unlisted}")
        resolved = [e for e in peters["entities"] if e["family"] in ("interior", "interiorprop")]
        self.assertTrue(resolved, "no interior placement resolves — the gap reopened")
        for entity in resolved:
            self.assertFalse(entity["x-familyIsSpriteStem"], entity["id"])
        # And nothing is left carrying an interior sprite stem instead of the published family.
        for entity in peters["entities"]:
            if entity["x-familyIsSpriteStem"]:
                self.assertFalse((entity["x-familyCandidate"] or "").startswith("interior"),
                                 f"{entity['id']} still stands on an interior stem")

    def test_a_resolved_rig_gives_the_editor_something_to_call(self):
        """A call needs a source behind it — and a resolved rig is no longer the only one.

        This used to read `rigSource is None` as `call is None`. That was true when the catalog
        was the only thing that could name a draw, and #629's ledger made it false: the yard
        recipes name the rig, the piece and the exact opts for a sheet whose folder holds several
        rigs and therefore resolves to none, so `Yards/ParishHall/postRail` legitimately carries
        a full call with `rigSource: null`. The kit contracts are now a third source in the same
        position. So the rule asserted here is the one that was always meant: **every call names
        what backs it, and an entity with nothing behind it has no call.**
        """
        for name, document in self.documents.items():
            renderable = 0
            for entity in document["entities"]:
                call = entity["call"]
                backing = [key for key in ("x-fromRecipe", "x-fromContract") if call and key in call]
                if entity["rigSource"] is None and not backing:
                    self.assertIsNone(call, f"{name} {entity['id']} calls with nothing behind it")
                    continue
                self.assertIsNotNone(call, f"{name} {entity['id']} has a source but nothing to call")
                self.assertEqual(call["fn"], "render")
                self.assertLessEqual(len(backing), 1,
                                     f"{name} {entity['id']} claims two opts provenances")
                if entity["rigSource"] is not None:
                    self.assertIsNotNone(entity["rig"], f"{name} {entity['id']}")
                if backing:
                    # A recorded provenance is a fact about the opts, so the synthesised marker
                    # and its "nothing records this" note are gone rather than contradicted.
                    self.assertNotIn("x-synthesised", call, f"{name} {entity['id']}")
                    self.assertNotIn("x-optsNote", call, f"{name} {entity['id']}")
                else:
                    self.assertEqual(call["opts"], {}, "a guessed opt draws the wrong object")
                    self.assertTrue(call["x-synthesised"])
                renderable += 1
            self.assertGreater(renderable, 0, f"{name} renders nothing at all")


def _as_pointer_only(doc):
    """Make a freshly-exported document look like what a checkout with no LFS bytes produces.

    ⚠ WITHOUT THIS, THE THREE CARRY-FORWARD TESTS BELOW TEST THE MACHINE, NOT THE CONTRACT.
    They each bank a committed package and then ask `_carry_forward_height` to carry it, which
    the contract (§6.1) only ever does when THIS run could not read the height bytes — its third
    table row is "can read the bytes -> recomputed normally", and the code returns early on
    exactly that. So on a pointer-only checkout the tests passed, on a full-LFS checkout the same
    correct code failed them, and neither run was evidence about the rule. The precondition is
    part of the fixture and is built here: bytes unread, ground emptied, and the SAME
    `textureSha256`, because a pointer's `oid sha256` IS the sha256 of the object it stands for
    (§6.1) — which is the entire proof the carry rests on.
    """
    doc = copy.deepcopy(doc)
    for block in (doc["x-provenance"]["heightMap"], doc["terrain"].get("x-heightMap") or {}):
        block["textureBytesRead"] = False
    ground = doc["terrain"]["layers"]["ground"]
    ground["rle"] = [[0, doc["terrain"]["cols"] * doc["terrain"]["rows"]]]
    ground["x-unavailable"] = "the height texture is an LFS pointer in this checkout"
    doc["terrain"].pop("x-heightField", None)
    return doc


def _greyscale_png(width, height, rows):
    import struct as _struct
    import zlib as _zlib

    def chunk(kind, body):
        return (_struct.pack(">I", len(body)) + kind + body
                + _struct.pack(">I", _zlib.crc32(kind + body) & 0xFFFFFFFF))

    raw = b"".join(b"\x00" + row for row in rows)
    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", _struct.pack(">IIBBBBB", width, height, 8, 0, 0, 0, 0))
            + chunk(b"IDAT", _zlib.compress(raw))
            + chunk(b"IEND", b""))


class EditorAskTests(unittest.TestCase):
    """The 2026-08-20 editor-side asks: rig versions, stable ids, water datum, height wash, interiors."""

    @classmethod
    def setUpClass(cls):
        repo = Repo(REPO)
        cls.docs = {name: hh_scene_export.export_region(repo, name, scene, height)
                    for name, scene, height in hh_scene_export.REGIONS}

    def test_rig_versions_are_one_hash_per_family_and_agree_with_x_rigs(self):
        """The table is FLAT: iterating it yields family names and nothing else.

        It used to nest the families under `{x-shaRule, families}`, and the editor — told "one
        entry per family" — read the two wrapper keys as families and went looking for rigs
        called `x-shaRule`. The rule now sits beside the table as `x-rigVersionsShaRule`, which
        is also where every `x-rigs` row already carried its own copy of it.
        """
        for name, doc in self.docs.items():
            table = doc["x-rigVersions"]
            self.assertTrue(doc["x-rigVersionsShaRule"], f"{name} dropped the hash rule")
            for key, entry in table.items():
                self.assertFalse(key.startswith("x-"),
                                 f"{name} put the note {key!r} where a family name goes")
                self.assertIsInstance(entry, dict, f"{name}/{key}")
                self.assertEqual(sorted(k for k in entry if not k.startswith("x-")),
                                 ["rigSource", "sha256"], f"{name}/{key}")
            self.assertTrue(table, f"{name} names no rig versions")
            by_source = {r["rigSource"]: r["sha256"] for r in doc["x-rigs"]}
            for family, entry in table.items():
                if entry.get("x-ambiguous"):
                    continue        # reported, deliberately unhashed — see _rig_versions
                self.assertEqual(entry["sha256"], by_source[entry["rigSource"]],
                                 f"{name}/{family} disagrees with x-rigs")
            # Every family in the table is one an entity actually uses.
            used = {e["family"] for e in doc["entities"] if e.get("rigSource")}
            self.assertEqual(set(table) - used, set(), f"{name} lists an unused family")

    def test_entity_ids_are_unique_and_not_positional(self):
        for name, doc in self.docs.items():
            ids = [e["id"] for e in doc["entities"]]
            self.assertEqual(len(ids), len(set(ids)), f"{name} has duplicate ids")
            # A positional id would count 001, 002, ... — the defect the editor asked us to avoid.
            self.assertFalse(any(i.endswith("_001") for i in ids),
                             f"{name} still mints ordinal ids")
            # 48 bits: comfortably clear of the birthday bound at this scale, and widening it
            # later would itself cost a re-key.
            self.assertTrue(all(len(i) == 12 for i in ids), f"{name} id width drifted")

    def test_an_unrelated_insert_does_not_rekey_other_rows(self):
        """The whole point: their write-back matches our rows by id."""
        doc = self.docs["StPeters"]
        before = {e["x-path"]: e["id"] for e in doc["entities"]}
        # Re-mint every id as if one row had been inserted ahead of the others. A positional
        # scheme shifts every subsequent id; an identity-derived one cannot.
        after = {}
        for offset, entity in enumerate(doc["entities"]):
            del offset
            after[entity["x-path"]] = package._stable_id(
                entity["x-path"], entity["pos"][0], entity["pos"][1])
        self.assertEqual(before, after)

    def test_an_id_carries_no_vocabulary(self):
        """Ruled 2026-08-20: a family rename must never re-key a row.

        The earlier `{family}_{hash}` form re-keyed 30 rows when the editor published
        `interior`/`interiorprop`. The id is content identity alone; `family` is its own field.
        """
        for name, doc in self.docs.items():
            families_used = {e["family"] for e in doc["entities"] if e.get("family")}
            for entity in doc["entities"]:
                self.assertRegex(entity["id"], r"^[0-9a-f]{12}$", f"{name} {entity['id']}")
                for family in families_used:
                    if len(family) > 3:
                        self.assertNotIn(family.lower(), entity["id"].lower(), name)
            # Re-minting with a different family must not move the id.
            sample = doc["entities"][0]
            self.assertEqual(
                sample["id"],
                package._stable_id(sample["x-path"], sample["pos"][0], sample["pos"][1]))

    def test_the_water_level_comes_from_the_region_def(self):
        repo = Repo(REPO)
        for name, doc in self.docs.items():
            declared = repo.region_def(name)["tideMeanLevel"]
            self.assertEqual(doc["terrain"]["waterLevelMeters"], declared, name)
            # The tide swings about it, so the amplitude must travel with the number.
            self.assertEqual(doc["terrain"]["x-tide"]["amplitudeMeters"],
                             repo.region_def(name)["tideAmplitude"], name)
            self.assertIn("chart datum", doc["terrain"]["x-waterDatum"])

    def test_the_height_field_states_its_stride_and_is_two_state(self):
        for name, doc in self.docs.items():
            field = doc["terrain"]["x-heightField"]
            self.assertEqual(field["strideMeters"], package.HEIGHT_FIELD_STRIDE_M, name)
            if field.get("values") is None:
                self.assertIn("x-unavailable", field,
                              f"{name} has no height field and does not say why")
                continue
            self.assertEqual(len(field["values"]), field["cols"] * field["rows"], name)

    def test_the_height_field_reads_a_real_texture_at_the_declared_stride(self):
        """Exercised against a synthetic R8 map, since the real one is an LFS pointer here."""
        stride = 8
        png = _greyscale_png(16, 12, [bytes([(x * 16) % 256 for x in range(16)]) for _ in range(12)])
        with tempfile.TemporaryDirectory() as tmp:
            rel = "height.png"
            with open(os.path.join(tmp, rel), "wb") as fh:
                fh.write(png)

            class _Stub:
                root = tmp

                def exists(self, path):
                    return os.path.exists(os.path.join(tmp, path))

                def abs(self, path):
                    return os.path.join(tmp, path)

            height_map = {"texture": rel, "worldSizeMeters": [32.0, 24.0],
                          "worldCenter": [0.0, 0.0], "minElevation": -2.0, "maxElevation": 6.0}
            field, note = heightmap.sample_field(_Stub(), height_map, 32, 24, [-16.0, 12.0], stride)
            self.assertIsNone(note)
            self.assertEqual(field["strideMeters"], stride)
            self.assertEqual(len(field["values"]), field["cols"] * field["rows"])
            values = [v for v in field["values"] if v is not None]
            self.assertTrue(values)
            for value in values:
                self.assertGreaterEqual(value, -2.0)
                self.assertLessEqual(value, 6.0)

    def test_every_interior_names_the_building_that_contains_it(self):
        doc = self.docs["StPeters"]
        by_id = {e["id"]: e for e in doc["entities"]}
        interiors = [e for e in doc["entities"] if "x-interiorOf" in e]
        self.assertTrue(interiors, "no interiors found to link")
        for entity in interiors:
            container = entity["x-interiorOf"]
            self.assertIsNotNone(container, f"{entity['id']} names no container")
            # The link must be an ANCESTOR in the hierarchy, never a geometric neighbour.
            self.assertTrue(entity["x-path"].startswith(by_id[container]["x-path"] + "/"),
                            f"{entity['id']} is not inside {container}")

    def test_the_continuous_per_instance_scale_is_exported_not_quantised(self):
        """Measured, not assumed: the scatter tables jitter scale on a continuous hash.

        The editor's bake cache keys on family|facing|opts, so this axis must reach them as a
        DRAW-TIME transform and never as a bake key. Exporting it verbatim is what lets them
        choose that; quantising it here would be us enumerating an axis that is not enumerated.
        """
        doc = self.docs["StPeters"]
        scaled = [e for e in doc["entities"] if e.get("x-scale")]
        self.assertTrue(scaled, "the shore-plant scatter should carry per-instance scale")
        distinct = {tuple(e["x-scale"]) for e in scaled}
        # Continuous by nature: near-1:1 distinct values. If this ever collapses to a handful,
        # the pipeline changed and the answer to the editor's question changed with it.
        self.assertGreater(len(distinct), len(scaled) // 2)
        for entity in scaled:
            self.assertIsNone(entity.get("call", {}) and entity["call"].get("opts", {}).get("scale"),
                              "scale must not be folded into call.opts — it would kill their cache")


class PortabilityTests(unittest.TestCase):
    """Three ways the output stopped being the same on a Windows full-LFS checkout."""

    def test_the_height_map_hash_does_not_depend_on_whether_lfs_is_pulled(self):
        """An LFS pointer's oid IS the content sha256, so one key serves both checkouts."""
        repo = Repo(REPO)
        for _name, _scene, height_name in hh_scene_export.REGIONS:
            height = repo.painted_height(height_name)
            self.assertIsNotNone(height["textureSha256"], height_name)
            self.assertRegex(height["textureSha256"], r"^[0-9a-f]{64}$", height_name)
            self.assertNotIn("textureBytesPresent", height,
                             "an environment fact must not reach the compared content")
            self.assertNotIn("textureLfsOidSha256", height,
                             "one hash, one key — see PortabilityTests' docstring")

    def test_git_output_is_decoded_as_utf8_not_the_platform_locale(self):
        """cp1252 turns every em-dash in a commit subject into mojibake in the package."""
        repo = Repo(REPO)
        for name, scene, height in hh_scene_export.REGIONS:
            text = package.dumps(hh_scene_export.export_region(repo, name, scene, height))
            self.assertNotIn("\u00e2\u20ac\u201d", text, f"{name} carries cp1252 mojibake")
            self.assertIn("\u2014", text, f"{name} should carry real em-dashes")

    def test_check_compares_content_not_line_endings(self):
        """A checkout with autocrlf rewrites the committed packages; --check must not care."""
        with tempfile.TemporaryDirectory() as out:
            self.assertEqual(hh_scene_export.main(["--out", out]), 0)
            for filename in os.listdir(out):
                target = os.path.join(out, filename)
                with open(target, "rb") as handle:
                    body = handle.read()
                with open(target, "wb") as handle:
                    handle.write(body.replace(b"\n", b"\r\n"))
            self.assertEqual(hh_scene_export.main(["--check", "--out", out]), 0,
                             "--check failed on a CRLF working tree")


class DeterminismTests(unittest.TestCase):
    def test_the_same_commit_emits_the_same_bytes(self):
        repo = Repo(REPO)
        first = {}
        for name, scene, height in hh_scene_export.REGIONS:
            first[name] = package.dumps(hh_scene_export.export_region(repo, name, scene, height))
        fresh = Repo(REPO)  # a cold index, so no cache order can leak into the output
        for name, scene, height in hh_scene_export.REGIONS:
            again = package.dumps(hh_scene_export.export_region(fresh, name, scene, height))
            self.assertEqual(first[name], again, name)

    def test_the_committed_artifacts_are_what_this_commit_produces(self):
        """``--check`` is the gate: a stale package in the repo fails here, not in the editor."""
        self.assertEqual(hh_scene_export.main(["--check"]), 0,
                         "tools/scene-export/packages is stale — re-run the exporter and commit")

    def test_nothing_in_a_package_is_keyed_to_the_checked_out_commit(self):
        """Otherwise committing a package invalidates it, and --check can never pass."""
        repo = Repo(REPO)
        head = subprocess.run(["git", "rev-parse", "HEAD"], cwd=REPO,
                              capture_output=True, text=True).stdout.strip()
        self.assertTrue(head)
        for name, scene, height in hh_scene_export.REGIONS:
            text = package.dumps(hh_scene_export.export_region(repo, name, scene, height))
            self.assertNotIn(head, text, f"{name} pins the checked-out commit")

    def test_every_package_stamps_whether_the_height_bytes_were_read(self):
        """Determinism here is per-commit AND per-LFS-state; the file has to say which it is.

        The seabed textures are Git LFS objects. The same commit exports a contoured ground
        where their bytes are present and an empty one where they are pointers — both correct
        for what they could read. Without this flag the two are indistinguishable in the file.
        """
        repo = Repo(REPO)
        for name, scene, height in hh_scene_export.REGIONS:
            doc = hh_scene_export.export_region(repo, name, scene, height)
            stamped = doc["x-provenance"]["heightMap"]["textureBytesRead"]
            self.assertIsInstance(stamped, bool, f"{name} does not stamp textureBytesRead")
            # The same flag reaches the terrain block, so a reader of the layer sees it too.
            self.assertEqual(doc["terrain"]["x-heightMap"]["textureBytesRead"], stamped, name)
            ground = doc["terrain"]["layers"]["ground"]
            painted = any(value for value, _ in ground["rle"])
            self.assertEqual(painted, stamped,
                             f"{name}: ground content disagrees with the stamp")
            self.assertEqual("x-unavailable" not in ground, stamped, name)

    def test_check_names_an_lfs_state_difference_instead_of_a_bare_stale(self):
        """A checkout difference must not read as a scene re-bank — CI pulls LFS, this repo does."""
        with tempfile.TemporaryDirectory() as tmp:
            repo = Repo(REPO)
            name, scene, height = hh_scene_export.REGIONS[0]
            doc = hh_scene_export.export_region(repo, name, scene, height)
            filename = f"{doc['region']['sceneName']}.scene.json"
            # Bank a package claiming the opposite LFS state, and nothing else changed.
            flipped = json.loads(package.dumps(doc))
            was = flipped["x-provenance"]["heightMap"]["textureBytesRead"]
            flipped["x-provenance"]["heightMap"]["textureBytesRead"] = not was
            with open(os.path.join(tmp, filename), "w", encoding="utf-8", newline="\n") as fh:
                json.dump(flipped, fh)
            cause = hh_scene_export._lfs_state_differs(
                [(filename, package.dumps(doc))], tmp)
            self.assertIsNotNone(cause, "an LFS-state flip was not diagnosed")
            self.assertIn("Git LFS", cause)
            self.assertIn("not a stale scene", cause)

    def test_check_stays_quiet_about_lfs_when_the_state_matches(self):
        """The diagnosis must not fire on ordinary staleness, or it becomes noise."""
        with tempfile.TemporaryDirectory() as tmp:
            repo = Repo(REPO)
            name, scene, height = hh_scene_export.REGIONS[0]
            doc = hh_scene_export.export_region(repo, name, scene, height)
            filename = f"{doc['region']['sceneName']}.scene.json"
            stale = json.loads(package.dumps(doc))
            stale["entities"] = stale["entities"][:5]  # stale in content, same LFS state
            with open(os.path.join(tmp, filename), "w", encoding="utf-8", newline="\n") as fh:
                json.dump(stale, fh)
            self.assertIsNone(
                hh_scene_export._lfs_state_differs([(filename, package.dumps(doc))], tmp))

    def test_a_pointer_only_export_keeps_a_verified_ground_contour(self):
        """The routine on this lane re-exports on every builder commit. In a pointer-only
        container that must not delete a coastline that only another machine can build."""
        with tempfile.TemporaryDirectory() as tmp:
            repo = Repo(REPO)
            name, scene, height = hh_scene_export.REGIONS[0]
            doc = hh_scene_export.export_region(repo, name, scene, height)
            filename = f"{doc['region']['sceneName']}.scene.json"
            rich = json.loads(package.dumps(doc))
            rich["x-provenance"]["heightMap"]["textureBytesRead"] = True
            rich["terrain"]["layers"]["ground"]["rle"] = [[1, 400], [0, rich["terrain"]["cols"]
                                                          * rich["terrain"]["rows"] - 400]]
            rich["terrain"]["x-heightField"] = {"values": [[0, 1], [2, 3]], "strideMeters": 8}
            with open(os.path.join(tmp, filename), "w", encoding="utf-8", newline="\n") as fh:
                json.dump(rich, fh)

            fresh = _as_pointer_only(hh_scene_export.export_region(repo, name, scene, height))
            carried, refusal = hh_scene_export._carry_forward_height(
                os.path.join(tmp, filename), fresh)
            self.assertTrue(carried, "a verified contour was not carried forward")
            self.assertIsNone(refusal)
            painted = sum(r[1] for r in fresh["terrain"]["layers"]["ground"]["rle"] if r[0])
            self.assertEqual(painted, 400)
            self.assertEqual(fresh["terrain"]["x-heightField"]["strideMeters"], 8)
            block = fresh["x-provenance"]["heightMap"]
            self.assertTrue(block["heightCarriedForward"])
            self.assertFalse(block["textureBytesRead"], "carrying is not the same as reading")

    def test_carrying_forward_is_idempotent(self):
        """The bug this pins: requiring `textureBytesRead` on the SOURCE made the guard fire
        once, and the next pointer-only run read its own output, judged it no richer, and
        emptied the coast."""
        with tempfile.TemporaryDirectory() as tmp:
            repo = Repo(REPO)
            name, scene, height = hh_scene_export.REGIONS[0]
            doc = hh_scene_export.export_region(repo, name, scene, height)
            filename = f"{doc['region']['sceneName']}.scene.json"
            carried_pkg = json.loads(package.dumps(doc))
            hm = carried_pkg["x-provenance"]["heightMap"]
            hm["textureBytesRead"], hm["heightCarriedForward"] = False, True
            carried_pkg["terrain"]["layers"]["ground"]["rle"] = [
                [1, 400], [0, carried_pkg["terrain"]["cols"] * carried_pkg["terrain"]["rows"] - 400]]
            with open(os.path.join(tmp, filename), "w", encoding="utf-8", newline="\n") as fh:
                json.dump(carried_pkg, fh)

            fresh = _as_pointer_only(hh_scene_export.export_region(repo, name, scene, height))
            carried, _ = hh_scene_export._carry_forward_height(
                os.path.join(tmp, filename), fresh)
            self.assertTrue(carried, "an already-carried package was not accepted as a source")
            self.assertEqual(
                sum(r[1] for r in fresh["terrain"]["layers"]["ground"]["rle"] if r[0]), 400)

    def test_a_changed_texture_is_refused_not_carried(self):
        """Same-hash is the whole proof. A different texture means the contour really is stale,
        and no local work can rebuild it — so refuse rather than ship it as current."""
        with tempfile.TemporaryDirectory() as tmp:
            repo = Repo(REPO)
            name, scene, height = hh_scene_export.REGIONS[0]
            doc = hh_scene_export.export_region(repo, name, scene, height)
            filename = f"{doc['region']['sceneName']}.scene.json"
            stale = json.loads(package.dumps(doc))
            stale["x-provenance"]["heightMap"]["textureBytesRead"] = True
            stale["x-provenance"]["heightMap"]["textureSha256"] = "0" * 64
            stale["terrain"]["layers"]["ground"]["rle"] = [
                [1, 400], [0, stale["terrain"]["cols"] * stale["terrain"]["rows"] - 400]]
            with open(os.path.join(tmp, filename), "w", encoding="utf-8", newline="\n") as fh:
                json.dump(stale, fh)

            fresh = _as_pointer_only(hh_scene_export.export_region(repo, name, scene, height))
            carried, refusal = hh_scene_export._carry_forward_height(
                os.path.join(tmp, filename), fresh)
            self.assertFalse(carried)
            self.assertIsNotNone(refusal, "a changed texture was silently carried forward")
            self.assertIn("CHANGED", refusal)

    def test_a_run_that_can_read_the_bytes_recomputes_and_never_carries(self):
        """§6.1's THIRD row — "anything | can read the bytes | recomputed normally" — pinned.

        It had no test, and its absence was not free: the other three carry-forward tests were
        written without building the pointer-only precondition, so on a checkout holding the LFS
        objects they asked this exporter to carry a contour it had just read for itself. The code
        was right and refused; the tests failed for it. Stating the row as its own assertion is
        what stops the next reader "fixing" the early return to make those three go green.

        Skipped, loudly, where the bytes are absent: a checkout that cannot read them has nothing
        to say about the row that begins "can read the bytes".
        """
        with tempfile.TemporaryDirectory() as tmp:
            repo = Repo(REPO)
            name, scene, height = hh_scene_export.REGIONS[0]
            fresh = hh_scene_export.export_region(repo, name, scene, height)
            if not fresh["x-provenance"]["heightMap"]["textureBytesRead"]:
                self.skipTest("pointer-only checkout: the LFS bytes this row is about are absent")
            filename = f"{fresh['region']['sceneName']}.scene.json"

            # A committed package that would be carried if this run were blind: same texture,
            # richer ground. The ONLY thing standing between it and a carry is that we can read.
            banked = json.loads(package.dumps(fresh))
            banked["x-provenance"]["heightMap"]["heightCarriedForward"] = True
            banked["terrain"]["layers"]["ground"]["rle"] = [
                [1, 7], [0, banked["terrain"]["cols"] * banked["terrain"]["rows"] - 7]]
            with open(os.path.join(tmp, filename), "w", encoding="utf-8",
                      newline="\n") as fh:
                json.dump(banked, fh)

            carried, refusal = hh_scene_export._carry_forward_height(
                os.path.join(tmp, filename), fresh)
            self.assertFalse(carried, "a run that read the bytes carried a package's contour")
            self.assertIsNone(refusal, "reading the bytes is not a refusal, it is the good case")
            painted = sum(r[1] for r in fresh["terrain"]["layers"]["ground"]["rle"] if r[0])
            self.assertNotEqual(painted, 7, "the banked contour overwrote a freshly-read one")
            self.assertTrue(fresh["x-provenance"]["heightMap"]["textureBytesRead"])
            self.assertNotIn("heightCarriedForward", fresh["x-provenance"]["heightMap"])

    def test_every_entity_states_its_origin(self):
        """Their top request: the tool was inferring zones from `x-path` and misclassifying any
        row whose key ends in a digit. An explicit origin removes the inference."""
        repo = Repo(REPO)
        for name, scene, height in hh_scene_export.REGIONS:
            doc = hh_scene_export.export_region(repo, name, scene, height)
            digit_keyed = 0
            for entity in doc["entities"]:
                self.assertTrue(entity.get("x-origin"), f"{name} {entity['id']} has no origin")
                self.assertEqual(entity["x-origin"], entity["x-path"].split("/")[0],
                                 "origin must be the builder's own root, unmodified")
                if entity["x-path"].split("/")[-1][-1:].isdigit():
                    digit_keyed += 1
            self.assertGreater(digit_keyed, 0, f"{name} has no digit-keyed row to protect")

    def test_the_road_layer_names_every_surface_it_will_not_solve(self):
        """A road layer that quietly drops the pads and the spur reads as "there are none".

        This pins a claim I had made and had NOT shipped: `read_ways` built the omitted list and
        `_road_grid` discarded it, so no package ever carried one.
        """
        repo = Repo(REPO)
        name, scene, height = hh_scene_export.REGIONS[0]          # Nine Mile Creek declares roads
        doc = hh_scene_export.export_region(repo, name, scene, height)
        omitted = doc["terrain"]["layers"]["road"].get("x-omitted")
        self.assertTrue(omitted, "the road layer names nothing it skipped")
        names = {entry["name"] for entry in omitted}
        # The computed spur and the paved rectangles are the two classes that must never vanish.
        self.assertIn("TruckParkSpur", names)
        self.assertTrue(any("Forecourt" in n or "Apron" in n or "Park" in n for n in names),
                        f"no paved area is named among {sorted(names)}")
        for entry in omitted:
            self.assertTrue(entry.get("why"), f"{entry['name']} is skipped without a reason")
            self.assertNotIn('"', entry["name"], "a C# string literal leaked its quotes")

    def test_a_facing_is_read_never_assumed(self):
        """Write-back contract §2: the count is the sheet's, and the sheet says how many.

        Also pins the two type rulings the reference package settled: `facing` is a compass
        NAME and `facingIndex` is the baked step, so the integer never lands in `facing`.

        `facing` is derived as of 2026-08-22 and no longer ships null; whether the DERIVATION is
        right is not asked here, because a test in the same file as the arithmetic is how the
        92-degree schoolhouse bug stayed green. `FacingTests` asks that question against the
        village green instead.
        """
        repo = Repo(REPO)
        for name, scene, height in hh_scene_export.REGIONS:
            doc = hh_scene_export.export_region(repo, name, scene, height)
            indexed = 0
            for entity in doc["entities"]:
                sprite = (entity.get("x-sprite") or {}).get("name") or ""
                self.assertIsInstance(entity["facing"], (str, type(None)),
                                      "an index landed in a field that holds a compass name")
                index = entity["facingIndex"]
                if index is None:
                    # Absence is meaningful: legacy single sprites end `_0`, with no `_d` at all.
                    self.assertNotRegex(sprite or "x", r"_d\d+(_|$)", f"{name} {sprite}")
                    continue
                indexed += 1
                self.assertIsInstance(index, int)
                self.assertRegex(sprite, r"_d\d+(_|$)", f"{name} {sprite}")
                if "x-facings" in entity:
                    self.assertLess(index, entity["x-facings"], f"{name} {sprite}")
            self.assertGreater(indexed, 0, f"{name} resolved no facing at all")
            for entity in doc["entities"]:
                if "x-facings" in entity:
                    self.assertGreater(entity["x-facings"], 0, name)
                    self.assertTrue(entity.get("x-facingsSource"),
                                    "a count with no declaration behind it")
                else:
                    # §6.2's standing ruling, unchanged by the derivation: the nine character
                    # entities carry an index with no machine-readable count, and a name derived
                    # from a count nobody declared would be the assumption §2 forbids.
                    self.assertIsNone(entity["facing"],
                                      f"{name} named a facing off an undeclared count")

    def test_opts_come_from_a_committed_declaration_or_say_they_do_not(self):
        """A lookup, never a derivation — now from either of the two things that record one.

        #629's per-cell recipe ledger was the first. The kit contracts are the second: they are
        what the bakers write beside a sheet, they carry `optionsJs` verbatim, and they are the
        reason the school can be redrawn with its own siding instead of a default house. The
        invariant is unchanged and is the whole point of both — **an opt in this file is one
        somebody committed, and an entity with no declaration behind it still says so.**
        """
        repo = Repo(REPO)
        for name, scene, height in hh_scene_export.REGIONS:
            doc = hh_scene_export.export_region(repo, name, scene, height)
            for entity in doc["entities"]:
                call = entity.get("call")
                if not call:
                    continue
                if call.get("x-fromRecipe"):
                    # A recipe-backed call must carry the ledger's own args and no empty-opts note.
                    self.assertNotIn("x-optsNote", call, f"{name} {entity['id']}")
                    self.assertTrue(call["args"], "the recipe's args were dropped")
                    self.assertEqual(call["fn"], "render")
                    self.assertIsNotNone(call.get("x-cellIndex"))
                elif call.get("x-fromContract"):
                    # Sheet-level, and it has to say so or a reader takes it for the cell's own.
                    self.assertNotIn("x-optsNote", call, f"{name} {entity['id']}")
                    self.assertTrue(call["opts"], "a contract-backed call with nothing in it")
                    self.assertIn("SHEET", call["x-optsScope"], f"{name} {entity['id']}")
                    self.assertIn("#", call["x-fromContract"], "the entry is not named")
                else:
                    self.assertEqual(call["opts"], {},
                                     "opts appeared with no declaration behind them")
                    self.assertIn("x-optsNote", call)
                    # The one thing this exporter may say about opts it cannot resolve: the
                    # verbatim expression, never a half-evaluation of it.
                    if "x-optsExpression" in call:
                        self.assertIn("x-optsExpressionFrom", call, f"{name} {entity['id']}")

    def test_a_recipe_for_a_different_bake_is_refused(self):
        """The sheet hash is the proof the recipe describes THIS bake. Refused, not warned."""
        repo = Repo(REPO)
        sheet = "Assets/_Project/Art/Sprites/Wharf/Decor/trapStack.png"
        recipe, why = recipes.read(repo, sheet)
        self.assertIsNotNone(recipe, why)
        self.assertIsNone(why)

        with tempfile.TemporaryDirectory() as tmp:
            class Wrong:
                root = repo.root
                exists = staticmethod(repo.exists)
                abs = staticmethod(repo.abs)
                content_sha256 = staticmethod(lambda rel: "0" * 64)
            broken, reason = recipes.read(Wrong(), sheet)
            self.assertIsNone(broken)
            self.assertIn("different bake", reason)

    def test_an_unknown_recipe_key_is_refused_not_ignored(self):
        """The ledger's C# reader is strict for a reason; a lax reader gives that away."""
        with tempfile.TemporaryDirectory() as tmp:
            sheet_dir = os.path.join(tmp, "Art")
            os.makedirs(sheet_dir)
            good = json.load(open(os.path.join(
                REPO, "Assets/_Project/Art/Sprites/Wharf/Decor/trapStack.recipe.json"),
                encoding="utf-8"))
            good["somethingNew"] = 1
            with open(os.path.join(sheet_dir, "s.recipe.json"), "w", encoding="utf-8") as fh:
                json.dump(good, fh)

            class Local:
                root = tmp
                exists = staticmethod(lambda rel: os.path.exists(os.path.join(tmp, rel)))
                abs = staticmethod(lambda rel: os.path.join(tmp, rel))
                content_sha256 = staticmethod(lambda rel: good["sheetSha256"])
            recipe, reason = recipes.read(Local(), "Art/s.png")
            self.assertIsNone(recipe)
            self.assertIn("somethingNew", reason)

    def test_the_cell_index_is_measured_from_the_top_row(self):
        """Unity's rect origin is bottom-left; the bakers pack row 0 at the top."""
        recipe = {"grid": {"columns": 1, "rows": 8, "order": "rowMajor",
                           "axes": [{"name": "facing", "bind": "dir",
                                     "values": [0, 7, 6, 5, 4, 3, 2, 1]}]},
                  "pack": {"cellW": 10, "cellH": 10, "sheetH": 80},
                  "call": {"fn": "render", "args": ["$dir", "$opts"], "opts": {}}}
        # The bottom-most cell in Unity's coordinates is the LAST row from the top.
        self.assertEqual(recipes.cell_index(recipe, [0, 0, 10, 10]), 7)
        self.assertEqual(recipes.cell_index(recipe, [0, 70, 10, 10]), 0)
        call, direction = recipes.call_for(recipe, 0)
        self.assertEqual(direction, 0)
        self.assertEqual(recipes.call_for(recipe, 3)[1], 5)

    def test_the_odometer_runs_first_axis_fastest(self):
        """Ledger §2.3. A slower-first reading would pick the wrong variant for every cell but 0."""
        recipe = {"grid": {"columns": 2, "rows": 3, "order": "rowMajor",
                           "axes": [{"name": "fill", "bind": "opt:fill", "values": ["a", "b"]},
                                    {"name": "facing", "bind": "dir", "values": [0, 1, 2]}]},
                  "pack": {"cellW": 10, "cellH": 10, "sheetH": 30},
                  "call": {"fn": "render", "args": ["$dir", "$opts"], "opts": {"base": 1}}}
        self.assertEqual(recipes.call_for(recipe, 0), ({"fn": "render", "args": ["$dir", "$opts"],
                                                        "opts": {"base": 1, "fill": "a"}}, 0))
        self.assertEqual(recipes.call_for(recipe, 1)[0]["opts"]["fill"], "b")
        self.assertEqual(recipes.call_for(recipe, 2)[1], 1)   # second row -> next dir
        self.assertEqual(recipes.call_for(recipe, 3)[0]["opts"]["fill"], "b")

    def test_the_zone_travels_in_band_and_never_guesses(self):
        """Ruled 2026-08-21: the landing zone ships in the package, not as a side-channel list."""
        repo = Repo(REPO)
        for name, scene, height in hh_scene_export.REGIONS:
            doc = hh_scene_export.export_region(repo, name, scene, height)
            zoned = 0
            for entity in doc["entities"]:
                zone = entity["x-zone"]
                self.assertIn(zone, (None, "a", "b"),
                              "(c) classifies an EDIT, not an entity — it cannot be a zone here")
                if zone is None:
                    self.assertIsNone(entity["x-zoneEvidence"])
                    continue
                zoned += 1
                self.assertTrue(entity["x-zoneEvidence"], f"{name} zone with no evidence behind it")
                # A sibling of x-origin, never folded into it (§8.2, one key one meaning).
                self.assertNotIn(zone, str(entity["x-origin"]).split("/")[-1:] or [""])
            self.assertGreater(zoned, 0, f"{name} classified nothing")

    def test_the_longest_prefix_wins(self):
        """Roots mix: StPetersWharf holds a deck and its fittings under one name."""
        repo = Repo(REPO)
        lengths = [len(prefix) for prefix, *_ in repo.root_zones()]
        self.assertEqual(lengths, sorted(lengths, reverse=True), "the table is not longest-first")
        self.assertIsNotNone(repo.resolution_excluded("StPetersWharf/Fittings/x"))
        self.assertIsNone(repo.resolution_excluded("StPetersWharf/Deck/x"),
                          "the deck was swept up by its own root's fittings rule")

    def test_a_ruled_out_path_is_not_reported_as_a_missing_sidecar(self):
        """A decision and a gap must not read the same. The fittings are the decision."""
        repo = Repo(REPO)
        doc = hh_scene_export.export_region(repo, *hh_scene_export.REGIONS[0][:1],
                                            *hh_scene_export.REGIONS[0][1:])
        notes = doc["x-provenance"]["entityNotes"]
        self.assertGreater(notes["resolutionExcluded"], 0)
        for entity in doc["entities"]:
            if entity.get("x-resolutionExcluded"):
                self.assertIsNone(entity["rig"], "a ruled-out entity resolved a rig anyway")
                sheet = (entity.get("x-sprite") or {}).get("sheet")
                self.assertNotIn(sheet, notes["unresolvedSheets"],
                                 "a ruling is being reported as a missing sidecar")

    def test_wharf_resolves_only_because_a_ruling_says_so(self):
        """`wharf` is not on the wire list; `wharfmodule` is, and the mapping is declared."""
        wire, _layers = families.load(Repo(REPO))
        self.assertNotIn("wharf", wire, "the wire list grew a `wharf` entry — drop the alias")
        self.assertIn("wharfmodule", wire)
        family, candidate = families.resolve("docs/art/rigs/wharfIsoRig.js", wire)
        self.assertEqual((family, candidate), ("wharfmodule", "wharf"))
        # A candidate with no ruling still refuses rather than reaching for a near neighbour.
        self.assertEqual(families.resolve("docs/art/rigs/madeUpRig.js", wire)[0], None)

    def test_the_output_is_valid_json(self):
        repo = Repo(REPO)
        for name, scene, height in hh_scene_export.REGIONS:
            text = package.dumps(hh_scene_export.export_region(repo, name, scene, height))
            json.loads(text)
            self.assertTrue(text.endswith("\n"))
            self.assertNotIn("\r", text)


if __name__ == "__main__":
    unittest.main()


class FacingTests(unittest.TestCase):
    """Whether the DERIVED compass name is the right one — asked from outside the arithmetic.

    ⚠️⚠️ THE POINT OF THIS CLASS IS THAT IT DOES NOT RE-DERIVE THE ANSWER. The defect this
    field spent two rounds shipping `null` to avoid was not a hard sum; it was a sign, and its
    test was green throughout because the test was the algebraic inverse of the implementation
    (`StPetersVillage.FacingToward` vs `StPetersVillageTests.DoorErrorDegrees`, recorded in
    BuildingFacing's remarks and in contract §6.2). So the oracle here is a FACT ABOUT THE
    WORLD that `hhexport.facing` has never heard of: **the village builder turns every door
    toward the village green**, and the green is the midpoint of the hearth and the start spawn,
    both of which are read out of the builder's own source below rather than copied here.

    A facing is quantised to 45 degrees, so "correct" means within a half-cell of the true
    bearing. Under the rule this exporter uses, the four village buildings land at 4-20 degrees.
    Under the inverted reading they land at up to 176 — the red saltbox facing away from the
    village it stands in — which is what `test_the_inverted_reading_is_caught_by_that_check`
    exists to demonstrate, because a test that cannot fail is not evidence.
    """

    BUILDER = "Assets/_Project/Code/App/Editor/StPetersBuilder.cs"
    # sin 40 degrees: the shared bake camera's elevation, so one metre of NORTHWARD ground
    # travel draws 0.643 world units up the screen (ADR 0034 / SpriteLightMath.GroundDepthScale).
    # Un-squashing before taking any angle is the second of BuildingFacing's two named traps.
    GROUND_DEPTH_SCALE = math.sin(math.radians(40.0))

    @classmethod
    def setUpClass(cls):
        repo = Repo(REPO)
        name, scene, height = hh_scene_export.REGIONS[1]
        assert name == "StPeters", name
        cls.doc = hh_scene_export.export_region(repo, name, scene, height)
        cls.green = cls._village_green()

    @classmethod
    def _village_green(cls):
        """The builder's own green, parsed from its own source — never restated here.

        ``VillageGreen => (VillageHearthPos + StartSpawnPos) * 0.5``. Reading the two constants
        keeps this oracle tracking the builder: move a site and this test moves with it, which is
        the opposite of a hard-coded expectation that has to be re-blessed after every edit.
        """
        with open(os.path.join(REPO, cls.BUILDER), encoding="utf-8", errors="replace") as handle:
            source = handle.read()
        points = {}
        for field in ("VillageHearthPos", "StartSpawnPos"):
            match = re.search(
                field + r"\s*=\s*new Vector3\(\s*(-?[\d.]+)f?\s*,\s*(-?[\d.]+)f?\s*,", source)
            assert match, f"{field} is no longer declared as a Vector3 literal in {cls.BUILDER}"
            points[field] = (float(match.group(1)), float(match.group(2)))
        hearth, spawn = points["VillageHearthPos"], points["StartSpawnPos"]
        return ((hearth[0] + spawn[0]) / 2.0, (hearth[1] + spawn[1]) / 2.0)

    def _bearing_to_green(self, position):
        """Compass degrees from a world position to the green, on the GROUND plane."""
        dx = self.green[0] - position[0]
        dy = (self.green[1] - position[1]) / self.GROUND_DEPTH_SCALE
        return (90.0 - math.degrees(math.atan2(dy, dx))) % 360.0

    @staticmethod
    def _error(a, b):
        return abs((a - b + 180.0) % 360.0 - 180.0)

    def _village_buildings(self):
        out = []
        for entity in self.doc["entities"]:
            path = entity.get("x-path") or ""
            if (path.startswith("IslandVillage/") and path.count("/") == 1
                    and entity.get("facingIndex") is not None and entity.get("x-facings")):
                out.append(entity)
        return out

    def test_the_villages_doors_point_at_the_village_green(self):
        """The oracle. Every village door is turned toward the green by the builder, so the
        exported bearing has to agree with the bearing to the green within a half-cell."""
        buildings = self._village_buildings()
        self.assertGreaterEqual(len(buildings), 4, "the village lost its buildings")
        for entity in buildings:
            half_cell = 180.0 / entity["x-facings"]
            wanted = self._bearing_to_green(entity["pos"])
            got = entity["x-facingBearingDeg"]
            self.assertIsNotNone(got, entity["x-path"])
            self.assertLessEqual(
                self._error(wanted, got), half_cell,
                f"{entity['x-path']} faces {entity['facing']} ({got} deg) but the green is at "
                f"{wanted:.1f} deg — more than the {half_cell} deg a facing can be quantised by")

    def test_the_inverted_reading_is_caught_by_that_check(self):
        """The check above must be able to FAIL, or it is decoration.

        `dir = (facings - i) mod facings` is real — it is what `RigBaker.DirForCell` hands a
        counter-clockwise rig — but it describes the argument, not the sheet, and applying it to
        the exported index is the mistake this whole field was held back for. Note where it
        agrees: index 4 is a fixed point of that map, so the SCHOOL alone cannot tell the two
        readings apart. The saltbox can, and does, by 176 degrees.
        """
        worst = 0.0
        for entity in self._village_buildings():
            facings = entity["x-facings"]
            inverted = ((facings - entity["facingIndex"]) % facings) * (360.0 / facings)
            worst = max(worst, self._error(self._bearing_to_green(entity["pos"]), inverted))
        self.assertGreater(worst, 90.0,
                           "the inverted reading passes the green check, so that check is not "
                           "evidence for either reading")

    def test_the_derivation_agrees_with_the_reference_packages_worked_pairs(self):
        """The editor's own sample carries two: `facingIndex 3 -> "SE"` and `4 -> "S"`.

        SE is the one that counts. S survives the inverted reading too.
        """
        with open(os.path.join(REPO, REFERENCE), encoding="utf-8") as handle:
            reference = json.load(handle)
        pairs = [(e["facingIndex"], e["facing"]) for e in reference["entities"]
                 if e.get("facing") and e.get("facingIndex") is not None]
        self.assertTrue(pairs, "the reference sample no longer carries a worked facing pair")
        for index, name in pairs:
            self.assertEqual(facing_mod.name_for(index, 8), name, f"index {index}")

    def test_a_bearing_between_two_names_is_not_rounded_onto_either(self):
        """Naming the nearest point is the aliasing this exporter refuses everywhere else."""
        self.assertEqual(facing_mod.name_for(1, 4), "E")            # 4 facings: cardinals
        self.assertEqual(facing_mod.name_for(2, 16), "NE")          # 16: the even cells land
        self.assertEqual(facing_mod.name_for(4, 16), "E")
        self.assertIsNone(facing_mod.name_for(1, 16), "22.5 deg was named as though it were NE")
        self.assertEqual(facing_mod.bearing_degrees(1, 16), 22.5, "the bearing itself is still a fact")
        self.assertIsNone(facing_mod.name_for(1, 5))
        self.assertIsNone(facing_mod.name_for(None, 8))
        self.assertIsNone(facing_mod.name_for(3, None))

    def test_the_index_still_wraps_the_way_the_baker_packs_it(self):
        for index in range(8):
            self.assertEqual(facing_mod.name_for(index, 8), facing_mod.name_for(index + 8, 8))

    def test_every_named_facing_carries_the_bearing_it_was_named_from(self):
        """The number the name rounds off, kept beside it so the claim stays checkable."""
        named = 0
        for entity in self.doc["entities"]:
            if not entity.get("facing"):
                continue
            named += 1
            bearing = entity["x-facingBearingDeg"]
            self.assertEqual(facing_mod.name_for(entity["facingIndex"], entity["x-facings"]),
                             entity["facing"], entity["x-path"])
            self.assertAlmostEqual(
                bearing, (360.0 / entity["x-facings"]) * entity["facingIndex"] % 360.0, places=4)
        self.assertGreater(named, 0, "no facing was named at all")

    def test_the_interior_sits_a_half_turn_from_its_building(self):
        """A cross-check the kit states and this export never uses: `Interiors.json` declares
        `exteriorFacingOffset: 4`, because `interiorIsoRig` puts its door on the OTHER gable.

        If the bearing table were mirrored, the two would not stay a half-turn apart — so this
        is a second, independent witness that the step direction is right, and it comes from the
        art contract rather than from anything in `hhexport`.
        """
        by_path = {e.get("x-path"): e for e in self.doc["entities"]}
        with open(os.path.join(REPO, "Assets/_Project/Art/Sprites/Interiors/Interiors.json"),
                  encoding="utf-8") as handle:
            offset = json.load(handle)["exteriorFacingOffset"]
        pairs = 0
        for path, interior in by_path.items():
            if not path.endswith("/Interior") or interior.get("facingIndex") is None:
                continue
            building = by_path.get(path[: -len("/Interior")])
            if not building or building.get("facingIndex") is None:
                continue
            # The offset is the INTERIOR rig measured against houseIsoRig — Interiors.json says
            # so, and the reason it is 4 is that houseIsoRig puts its door on +y and
            # interiorIsoRig on -y. The shop shells are a different exterior with a different
            # answer: shops.contract.json declares its own `shellFacingOffset`, and it is 0.
            # Applying the house number to a shopfront would be asserting the wrong contract.
            if building.get("rigSource") != "docs/art/rigs/houseIsoRig.js":
                continue
            facings = interior["x-facings"]
            self.assertEqual(interior["facingIndex"],
                             (building["facingIndex"] + offset) % facings, path)
            self.assertEqual(
                self._error(interior["x-facingBearingDeg"], building["x-facingBearingDeg"]),
                180.0, f"{path} is not a half-turn from its building")
            pairs += 1
        self.assertGreater(pairs, 0, "no building/interior pair to cross-check")


class ContractOptsTests(unittest.TestCase):
    """`call.opts` read from a kit's committed contract, where no per-cell recipe covers a sheet.

    The rule is §6.4's, applied to a second file: a LOOKUP, matched on the sheet an entry names,
    refused whole when it cannot be read rather than half-evaluated.
    """

    @classmethod
    def setUpClass(cls):
        cls.repo = Repo(REPO)
        cls.docs = {name: hh_scene_export.export_region(cls.repo, name, scene, height)
                    for name, scene, height in hh_scene_export.REGIONS}

    def _by_path(self, region, path):
        for entity in self.docs[region]["entities"]:
            if entity.get("x-path") == path:
                return entity
        self.fail(f"{region} has no entity at {path}")

    def test_one_entity_per_kit_carries_its_own_committed_opts(self):
        """Every kit contract in the repo that a placed entity reaches, proven one at a time.

        The yard is the ledger's (per-cell); the rest are the contracts' (per-sheet). Naming
        them individually is deliberate: a single "some entity somewhere has opts" would go on
        passing while four of the five kits silently stopped resolving.
        """
        cases = [
            ("StPeters", "IslandVillage/school", "Buildings.json", "era", "colonial"),
            ("StPeters", "IslandVillage/school/Interior", "Interiors.json", "floor", "wideBoard"),
            ("StPeters", "IslandVillage/school/Furniture/Table (0,0.6)", "Interiors.json",
             "wood", "oak"),
        ]
        for region, path, contract, key, value in cases:
            entity = self._by_path(region, path)
            call = entity["call"]
            self.assertIsNotNone(call, path)
            self.assertIn(contract, call["x-fromContract"], path)
            self.assertEqual(call["opts"].get(key), value, f"{path} opts: {call['opts']}")
            self.assertNotIn("x-synthesised", call, path)

    def test_the_yard_keeps_its_per_cell_recipe_when_a_contract_also_covers_the_sheet(self):
        """Both declarations exist for the yard. The recipe wins: it knows which CELL.

        `yardIso.contract.json` says `kept: 0.88` for the whole bake and the postRail recipe says
        `0.72` for this one, so the value is the tell — a contract that had overwritten a recipe
        would show up here as the sheet-level number.
        """
        entity = self._by_path("NineMileCreek", "Yards/ParishHall/postRail_000")
        call = entity["call"]
        self.assertIn("x-fromRecipe", call)
        self.assertNotIn("x-fromContract", call)
        self.assertEqual(call["opts"]["kept"], 0.72,
                         "the sheet-level contract overwrote the cell's own recipe")

    def test_a_preset_expression_is_carried_verbatim_and_never_evaluated(self):
        """`shops.contract.json` holds `Object.assign({},Shopfront.PRESETS['harbourStore'])`.

        That is a call into the rig's own preset table. Half-reading it — taking `harbourStore`
        as an opt, say — would invent an option no rig reads, and the rigs fail SOFT, so nothing
        would flag it. It is reported for a reader that can run the rig, and opts stay empty.
        """
        shops = [e for e in self.docs["NineMileCreek"]["entities"]
                 if (e.get("call") or {}).get("x-optsExpression")]
        self.assertTrue(shops, "no shop carries the unresolved preset expression")
        for entity in shops:
            call = entity["call"]
            self.assertEqual(call["opts"], {}, "a preset expression was half-evaluated")
            self.assertIn("PRESETS", call["x-optsExpression"])
            self.assertIn("shops.contract.json", call["x-optsExpressionFrom"])
        notes = self.docs["NineMileCreek"]["x-provenance"]["entityNotes"]
        self.assertTrue(notes["contractRefusals"], "the refusal was not reported")
        self.assertTrue(any("not a literal" in why for why in notes["contractRefusals"]))

    def test_the_literal_grammar_reads_what_the_repo_commits(self):
        """Parsed against every `optionsJs` on disk, not against invented samples."""
        seen = 0
        for folder, _dirs, files in os.walk(os.path.join(REPO, "Assets")):
            for filename in files:
                if not filename.endswith(".json"):
                    continue
                try:
                    with open(os.path.join(folder, filename), encoding="utf-8") as handle:
                        data = json.load(handle)
                except (ValueError, OSError, UnicodeDecodeError):
                    continue
                if not isinstance(data, dict):
                    continue
                for rows in data.values():
                    for row in rows if isinstance(rows, list) else ():
                        raw = isinstance(row, dict) and row.get("optionsJs")
                        if not isinstance(raw, str) or not raw.startswith("{"):
                            continue
                        parsed = contracts.parse_object_literal(raw)
                        self.assertIsInstance(parsed, dict, raw[:60])
                        self.assertTrue(parsed, raw[:60])
                        seen += 1
        self.assertGreater(seen, 10, "the sweep found almost no literals — it stopped walking")

    def test_the_grammar_refuses_what_it_cannot_fully_read(self):
        """Every refusal is whole. A partial dict is the dangerous outcome, not the safe one."""
        for bad in ("Object.assign({},Shopfront.PRESETS['x'])",
                    "{a:1",
                    "{a:someIdentifier}",
                    "{a:1}{b:2}",
                    "{a:1,}}",
                    "{'unterminated:1}",
                    "{a:1} // trailing",
                    "[1,2]",
                    ""):
            with self.assertRaises(contracts.Refused, msg=f"accepted {bad!r}"):
                contracts.parse_object_literal(bad)
        # ...and reads the shapes the contracts actually use, including a trailing comma.
        self.assertEqual(contracts.parse_object_literal(
            "{a:'x',b:0.5,c:true,d:false,e:null,f:-2,g:[1,'y'],h:{i:1},'j':2,}"),
            {"a": "x", "b": 0.5, "c": True, "d": False, "e": None, "f": -2,
             "g": [1, "y"], "h": {"i": 1}, "j": 2})

    def test_an_entry_speaks_only_for_the_sheet_it_names(self):
        """Matched on the sheet, never on a key resembling a stem — §6.4's no-aliasing rule.

        The village kit is the case with teeth: `Buildings.json` holds nine entries whose keys
        are `school`, `generalStore`, `redSaltbox` and so on, and a key-based match would hand
        one building another's siding on a near miss.
        """
        village = "Assets/_Project/Art/Sprites/Buildings/Village"
        school, _ = contracts.opts_for_sheet(self.repo, f"{village}/Village_school.png")
        saltbox, _ = contracts.opts_for_sheet(self.repo, f"{village}/Village_redSaltbox.png")
        self.assertEqual(school["key"], "school")
        self.assertEqual(saltbox["key"], "redSaltbox")
        self.assertNotEqual(school["opts"]["body"], saltbox["opts"]["body"],
                            "two buildings resolved to the same entry")
        # A sheet no entry names gets nothing rather than the folder's first row.
        missing, why = contracts.opts_for_sheet(self.repo, f"{village}/Village_notABuilding.png")
        self.assertIsNone(missing)
        self.assertIsNone(why)

    def test_the_village_resolves_its_rig_from_the_entry_not_the_folder(self):
        """The entry names ONE rig for ONE sheet; the folder cannot choose and does not have to.

        `Buildings.json` declares houseIsoRig AND wharfBuildingRig side by side, so
        `rig_for_sheet` stays honestly ambiguous over the folder — that guard is not loosened
        here. The per-entry `rigScript` is a narrower declaration than the folder-wide scan, and
        it is what moves these eight off a sprite stem onto the editor's own vocabulary.
        """
        _name, source, _evidence, candidates = self.repo.rig_for_sheet(
            "Assets/_Project/Art/Sprites/Buildings/Village/Village_school.png")
        self.assertIsNone(source, "the folder-wide scan picked a rig it cannot choose between")
        self.assertGreater(len(candidates), 1, "the folder is no longer ambiguous — re-check this")

        school = self._by_path("StPeters", "IslandVillage/school")
        self.assertEqual(school["rigSource"], "docs/art/rigs/houseIsoRig.js")
        self.assertEqual(school["family"], "house")
        self.assertFalse(school["x-familyIsSpriteStem"])
        self.assertIn("Buildings.json#school", school["x-rigFrom"])
        wire, _layers = families.load(self.repo)
        self.assertIn(school["family"], wire, "resolved onto a name the editor cannot draw")

    def test_a_kit_index_is_found_beside_its_per_sheet_sidecars(self):
        """The discovery gap that hid `Buildings.json`, and the guard it must not cost.

        The index rule used to require the folder hold exactly ONE json, which `Village/` never
        will — it publishes a sidecar per sheet. Counting PNG-less candidates instead finds the
        kit index there and still finds nothing in `Art/Boats/`, which holds four anchor files
        and no index; resolving a dory to whichever of them sorts first is the failure that
        clause exists to prevent.
        """
        found = self.repo.sidecars_for_sheet(
            "Assets/_Project/Art/Sprites/Buildings/Village/Village_school.png")
        self.assertEqual([os.path.basename(p) for p in found],
                         ["Village_school.json", "Buildings.json"])
        self.assertEqual(self.repo.sidecars_for_sheet("Assets/_Project/Art/Boats/DoryIso.png"), [])
        self.assertEqual(self.repo.rig_for_sheet("Assets/_Project/Art/Boats/DoryIso.png"),
                         (None, None, None, []))

    def test_contract_opts_say_they_are_the_sheets_and_not_the_cells(self):
        """A per-sheet value read as a per-cell one is a wrong variant drawn with confidence."""
        carried = 0
        for name, doc in self.docs.items():
            for entity in doc["entities"]:
                call = entity.get("call") or {}
                if not call.get("x-fromContract"):
                    continue
                carried += 1
                self.assertIn("SHEET", call["x-optsScope"], f"{name} {entity['id']}")
                self.assertNotIn("x-cellIndex", call, "a sheet-level opt claimed a cell")
            self.assertTrue(doc["x-provenance"]["entityNotes"]["x-contractMeaning"])
        self.assertGreater(carried, 0, "no entity reads a kit contract at all")
