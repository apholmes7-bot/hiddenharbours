"""Tests for the scene exporter. Run: ``python3 -m unittest discover tools/scene-export/tests``

These pin the rules the review (#571, corrected by #576) rules on — RLE coverage, the region
table coming from the RegionDef, the LF-sha256 convention, ``unityPivot`` never being the
cell-box fallback — plus the determinism claim the PR makes.
"""

import json
import os
import subprocess
import sys
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
TOOL = os.path.dirname(HERE)
REPO = os.path.dirname(os.path.dirname(TOOL))
sys.path.insert(0, TOOL)

from hhexport import package, unityyaml as U  # noqa: E402
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
        self.assertEqual(creek["frame"]["originNW"], [-380, 280])
        peters = self.documents["StPeters"]
        self.assertEqual(peters["region"]["worldSizeMeters"], [760, 520])
        self.assertEqual(peters["frame"]["originNW"], [-380, 260])

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
                if entity["unityPivot"] is None:
                    continue
                x, y = entity["unityPivot"]
                self.assertTrue(0.0 <= x <= 1.0 and 0.0 <= y <= 1.0, f"{name} {entity['id']}")
                self.assertTrue(entity["x-pivotSource"].startswith("sprite-import."))

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
            self.assertIsNotNone(drift["builderCommitsSinceScene"])
            # A shallow clone can only give a floor; the package must say which it gave.
            self.assertEqual(drift["exact"], document["x-provenance"]["historyIsComplete"])
            self.assertEqual("x-note" in drift, not drift["exact"])

    def test_the_height_map_is_pinned_even_though_its_bytes_are_absent(self):
        for document in self.documents.values():
            height = document["terrain"]["x-heightMap"]
            self.assertIsNotNone(height)
            self.assertTrue(height["textureLfsOidSha256"] or height.get("textureSha256"))


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

    def test_the_output_is_valid_json(self):
        repo = Repo(REPO)
        for name, scene, height in hh_scene_export.REGIONS:
            text = package.dumps(hh_scene_export.export_region(repo, name, scene, height))
            json.loads(text)
            self.assertTrue(text.endswith("\n"))
            self.assertNotIn("\r", text)


if __name__ == "__main__":
    unittest.main()
