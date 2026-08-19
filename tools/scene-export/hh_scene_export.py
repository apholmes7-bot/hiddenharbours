#!/usr/bin/env python3
"""Export a Hidden Harbours region into the scene editor's package format.

Runs **outside Unity**, on the committed text alone: the region's ``.unity`` (Force Text YAML),
``Data/**.asset``, the ``.meta`` import settings, and ``docs/art/rigs/**``. No Unity APIs, no
engine, no LFS objects required.

    python3 tools/scene-export/hh_scene_export.py

Determinism: the output is a pure function of the repo at a given commit — no timestamps, no
run ids, no dictionary-order dependence. Re-running on the same commit rewrites the same bytes.
"""

import argparse
import hashlib
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from hhexport import package, provenance, unityyaml
from hhexport.repo import Repo
from hhexport.scene import Scene

# WestWater is excluded deliberately: it is unbanked and awaiting rebuild, so there is no
# committed scene to picture.
REGIONS = [
    ("NineMileCreek", "Assets/_Project/Scenes/NineMileCreek.unity", "NineMileCreekSeabed"),
    ("StPeters", "Assets/_Project/Scenes/StPeters.unity", "StPetersSeabed"),
]


def slug(name):
    out = []
    for index, ch in enumerate(name):
        if ch.isupper() and index:
            out.append("-")
        out.append(ch.lower())
    return "".join(out)


def export_region(repo, region_name, scene_rel, height_name):
    region = repo.region_def(region_name)
    height_map = repo.painted_height(height_name)
    prov = provenance.collect(repo, region_name, scene_rel, height_map)
    scene = Scene(unityyaml.parse_file(repo.abs(scene_rel)))
    return package.build_document(repo, region, scene, prov)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--repo", default=os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                                       "..", ".."),
                        help="repo root (default: two levels above this script)")
    parser.add_argument("--out", default="tools/scene-export/packages",
                        help="output directory, relative to the repo root")
    parser.add_argument("--region", action="append", default=None,
                        help="export only this region (repeatable)")
    parser.add_argument("--check", action="store_true",
                        help="write nothing; fail if the output on disk is not what would be "
                             "written (a determinism / staleness gate)")
    args = parser.parse_args(argv)

    repo = Repo(args.repo)
    out_dir = os.path.join(repo.root, args.out)
    wanted = set(args.region) if args.region else None

    manifest = {"format": package.FORMAT, "packages": []}
    written = []
    for region_name, scene_rel, height_name in REGIONS:
        if wanted and region_name not in wanted:
            continue
        document = export_region(repo, region_name, scene_rel, height_name)
        text = package.dumps(document)
        filename = f"{slug(region_name)}.scene.json"
        manifest["packages"].append({
            "region": document["region"]["id"],
            "file": filename,
            "sha256": hashlib.sha256(text.encode("utf-8")).hexdigest(),
            "entities": document["stats"]["entities"],
            "paths": document["stats"]["paths"],
            "rigsPinned": document["stats"]["x-rigsPinned"],
            "sourceCommit": document["x-provenance"]["sourceCommit"],
        })
        written.append((filename, text))

    manifest["packages"].sort(key=lambda p: p["file"])
    written.append(("MANIFEST.json", package.dumps(manifest)))

    failures = []
    for filename, text in written:
        target = os.path.join(out_dir, filename)
        if args.check:
            existing = None
            if os.path.exists(target):
                with open(target, "r", encoding="utf-8", newline="") as fh:
                    existing = fh.read()
            if existing != text:
                failures.append(filename)
            continue
        os.makedirs(out_dir, exist_ok=True)
        with open(target, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(text)
        print(f"wrote {os.path.relpath(target, repo.root)} ({len(text):,} bytes)")

    if args.check:
        if failures:
            print("STALE: " + ", ".join(failures), file=sys.stderr)
            return 1
        print(f"up to date ({len(written)} files)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
