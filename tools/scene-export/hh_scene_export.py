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
import json
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

    manifest = {"schema": package.SCHEMA, "packages": []}
    written = []
    for region_name, scene_rel, height_name in REGIONS:
        if wanted and region_name not in wanted:
            continue
        document = export_region(repo, region_name, scene_rel, height_name)
        text = package.dumps(document)
        # `<sceneName>.scene.json` — the name the editor's own doExport() writes.
        filename = f"{document['region']['sceneName']}.scene.json"
        manifest["packages"].append({
            "region": document["region"]["id"],
            "file": filename,
            "sha256": hashlib.sha256(text.encode("utf-8")).hexdigest(),
            "entities": document["stats"]["entities"],
            "paths": document["stats"]["x-paths"],
            "rigsPinned": document["stats"]["x-rigsPinned"],
            "sceneLastBuiltCommit": document["x-provenance"]["sceneLastBuiltCommit"],
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
                # Universal newlines on the READ side: a checkout with autocrlf on rewrites the
                # committed packages to CRLF, and comparing raw bytes would then fail on line
                # endings before it ever considered the content. What --check means is "does
                # this commit still produce this document", not "is your working tree LF".
                with open(target, "r", encoding="utf-8") as fh:
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
            cause = _lfs_state_differs(written, out_dir)
            if cause:
                print(cause, file=sys.stderr)
            return 1
        print(f"up to date ({len(written)} files)")
    return 0


def _lfs_state_differs(written, out_dir):
    """The one staleness cause that is about the checkout, not the commit — named, not guessed.

    The seabed textures are Git LFS objects. Export them where the bytes are present and the
    ground layer is a contour; export the same commit where they are pointers and it is empty.
    Both are correct for what they could read, so a reader who sees a bare STALE would go looking
    for a scene re-bank that never happened. Returns a message only when the flag actually flips.
    """
    for filename, text in written:
        if not filename.endswith(".scene.json"):
            continue
        target = os.path.join(out_dir, filename)
        if not os.path.exists(target):
            continue
        try:
            with open(target, "r", encoding="utf-8") as fh:
                was = json.load(fh)
            now = json.loads(text)
        except (ValueError, OSError):
            continue
        was_read = (was.get("x-provenance", {}).get("heightMap") or {}).get("textureBytesRead")
        now_read = (now.get("x-provenance", {}).get("heightMap") or {}).get("textureBytesRead")
        if was_read is not None and now_read is not None and was_read != now_read:
            here, there = ("present", "absent") if now_read else ("absent", "present")
            return (
                f"  cause: height-map bytes are {here} in this checkout but were {there} when "
                f"the committed packages were generated (Git LFS). The ground layer is contoured "
                f"in one and empty in the other. This is a checkout difference, not a stale "
                f"scene \u2014 regenerate deliberately, or compare from a matching checkout."
            )
    return None


if __name__ == "__main__":
    raise SystemExit(main())
