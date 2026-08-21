"""Tests for the scene exporter. Run: ``python3 -m unittest discover tools/scene-export/tests``

These pin the contract lead-architect settled from the editor's reference package (PR #588,
recorded as citations in ``docs/tools/scene-export-contract.md``) — the envelope's shape, RLE
coverage and row order, the region table coming from the RegionDef, the LF-sha256 convention,
``sortBias`` as a tie-break delta rather than an absolute order, and pivots that can never be
the cell-box fallback — plus the determinism claim the PR makes.
"""

import datetime
import json
import math
import os
import tempfile
import subprocess
import sys
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
TOOL = os.path.dirname(HERE)
REPO = os.path.dirname(os.path.dirname(TOOL))
sys.path.insert(0, TOOL)

REFERENCE = "docs/tools/reference/sample-scene.json"

from hhexport import families, heightmap, package, recipes, roads, unityyaml as U  # noqa: E402
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
        wire, layers = families.load(self.repo)
        # 43 at first relay + interior/interiorprop, published by the editor 2026-08-20 evening.
        self.assertEqual(len(wire), 45, "the transcribed wire list has drifted")
        self.assertEqual(sorted(layers), ["cliff", "ground", "road", "texture", "wharfdeck"])
        for name, document in self.documents.items():
            for entity in document["entities"]:
                if entity["x-familyIsSpriteStem"]:
                    self.assertNotIn(entity["family"], wire,
                                     f"{name} {entity['id']} is on the list but flagged as not")
                    self.assertIn("x-familyCandidate", entity, f"{name} {entity['id']}")
                else:
                    self.assertIn(entity["family"], wire, f"{name} {entity['id']}")

    def test_a_near_miss_is_never_aliased_onto_a_neighbour(self):
        """`wharfIsoRig` normalises to `wharf`; the list holds `wharfbuilding` AND `wharfmodule`.

        Picking either would be the aliasing the ruling forbids, so it must stay unresolved —
        and appear on the request list instead.
        """
        creek = self.documents["NineMileCreek"]
        wharf = [e for e in creek["entities"]
                 if (e["rigSource"] or "").endswith("wharfIsoRig.js")]
        self.assertTrue(wharf, "the wharf kit no longer resolves — this guard has gone blind")
        for entity in wharf:
            self.assertTrue(entity["x-familyIsSpriteStem"], entity["id"])
            self.assertNotIn(entity["family"], ("wharfbuilding", "wharfmodule"), entity["id"])
        unlisted = creek["x-provenance"]["entityNotes"]["unlistedFamilies"]
        self.assertIn("wharf", unlisted)
        self.assertEqual(unlisted["wharf"]["placements"], len(wharf))

    def test_the_interior_gap_closed_when_the_editor_published_the_entries(self):
        """This used to assert the opposite: `interior`/`interiorprop` had no family and stayed
        declared. The editor published both on 2026-08-20, so the request list is now empty for
        St Peters and those 30 placements resolve. Kept as the record of a gap that closed the
        way the no-aliasing rule intended — by a ruling, not by us bending a name."""
        peters = self.documents["StPeters"]
        unlisted = peters["x-provenance"]["entityNotes"]["unlistedFamilies"]
        self.assertEqual(unlisted, {}, "St Peters has an unlisted family again")
        resolved = [e for e in peters["entities"] if e["family"] in ("interior", "interiorprop")]
        self.assertEqual(len(resolved), 30)
        for entity in resolved:
            self.assertFalse(entity["x-familyIsSpriteStem"], entity["id"])

    def test_a_resolved_rig_gives_the_editor_something_to_call(self):
        for name, document in self.documents.items():
            renderable = 0
            for entity in document["entities"]:
                if entity["rigSource"] is None:
                    self.assertIsNone(entity["call"], f"{name} {entity['id']}")
                    continue
                self.assertIsNotNone(entity["rig"], f"{name} {entity['id']}")
                call = entity["call"]
                self.assertIsNotNone(call, f"{name} {entity['id']} has a rig but nothing to call")
                self.assertEqual(call["fn"], "render")
                if call.get("x-fromRecipe"):
                    # The ledger recorded this cell's own call (#629) — opts are a fact here,
                    # so the synthesised marker and its note are gone rather than contradicted.
                    self.assertNotIn("x-synthesised", call, f"{name} {entity['id']}")
                else:
                    self.assertEqual(call["opts"], {}, "a guessed opt draws the wrong object")
                    self.assertTrue(call["x-synthesised"])
                renderable += 1
            self.assertGreater(renderable, 0, f"{name} renders nothing at all")


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
        for name, doc in self.docs.items():
            table = doc["x-rigVersions"]["families"]
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

            fresh = hh_scene_export.export_region(repo, name, scene, height)
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

            fresh = hh_scene_export.export_region(repo, name, scene, height)
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

            fresh = hh_scene_export.export_region(repo, name, scene, height)
            carried, refusal = hh_scene_export._carry_forward_height(
                os.path.join(tmp, filename), fresh)
            self.assertFalse(carried)
            self.assertIsNotNone(refusal, "a changed texture was silently carried forward")
            self.assertIn("CHANGED", refusal)

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
        NAME and `facingIndex` is the baked step, so the integer never lands in `facing`; and
        `facing` stays null because turning a step into a bearing is the sign error that put the
        schoolhouse door 92 degrees off the green.
        """
        repo = Repo(REPO)
        for name, scene, height in hh_scene_export.REGIONS:
            doc = hh_scene_export.export_region(repo, name, scene, height)
            indexed = 0
            for entity in doc["entities"]:
                sprite = (entity.get("x-sprite") or {}).get("name") or ""
                self.assertIsNone(entity["facing"], "a bearing was derived from an index")
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

    def test_opts_come_from_the_ledger_or_say_they_do_not(self):
        """#629: a lookup, never a derivation. Either the recipe drew this call or the note stands."""
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
                else:
                    self.assertEqual(call["opts"], {}, "opts appeared without a recipe behind them")
                    self.assertIn("x-optsNote", call)

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

    def test_the_output_is_valid_json(self):
        repo = Repo(REPO)
        for name, scene, height in hh_scene_export.REGIONS:
            text = package.dumps(hh_scene_export.export_region(repo, name, scene, height))
            json.loads(text)
            self.assertTrue(text.endswith("\n"))
            self.assertNotIn("\r", text)


if __name__ == "__main__":
    unittest.main()
