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


def exported_region_ids(repo):
    """The region ids this exporter ships, from REGIONS and each region's own Def.

    A passage that leads to Coddle Cove or East Water leads somewhere REAL — those have RegionDefs
    — but not somewhere this export pictures, and the package says which so a reader is not left
    wondering whether it lost a package or the region was never in one.
    """
    ids = set()
    for region_name, _scene, _height in REGIONS:
        region = repo.region_def(region_name)
        if region.get("id"):
            ids.add(region["id"])
    return frozenset(ids)


def export_region(repo, region_name, scene_rel, height_name, shipped_ids=None):
    region = repo.region_def(region_name)
    height_map = repo.painted_height(height_name)
    prov = provenance.collect(repo, region_name, scene_rel, height_map)
    scene = Scene(unityyaml.parse_file(repo.abs(scene_rel)))
    if shipped_ids is None:
        shipped_ids = exported_region_ids(repo)
    return package.build_document(repo, region, scene, prov, shipped_ids)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    parser.add_argument("--repo", default=os.path.join(os.path.dirname(os.path.abspath(__file__)),
                                                       "..", ".."),
                        help="repo root (default: two levels above this script)")
    parser.add_argument("--out", default="tools/scene-export/packages",
                        help="output directory, relative to the repo root")
    parser.add_argument("--region", action="append", default=None,
                        help="export only this region (repeatable)")
    parser.add_argument("--allow-downgrade", action="store_true",
                        help="permit overwriting a package that was generated WITH height-map "
                             "bytes from a checkout that has none. Refused by default: the "
                             "contoured ground and the height field cannot be rebuilt here, so "
                             "the write would destroy data this checkout cannot reproduce.")
    parser.add_argument("--check", action="store_true",
                        help="write nothing; fail if the output on disk is not what would be "
                             "written (a determinism / staleness gate)")
    args = parser.parse_args(argv)

    repo = Repo(args.repo)
    out_dir = os.path.join(repo.root, args.out)
    wanted = set(args.region) if args.region else None

    manifest = {"schema": package.SCHEMA, "packages": []}
    written, refusals, carried_forward = [], [], []
    for region_name, scene_rel, height_name in REGIONS:
        if wanted and region_name not in wanted:
            continue
        document = export_region(repo, region_name, scene_rel, height_name)
        # `<sceneName>.scene.json` — the name the editor's own doExport() writes.
        filename = f"{document['region']['sceneName']}.scene.json"
        # Before serialising: if this checkout cannot read the height bytes but the committed
        # package already holds a contour for the very same texture, keep it. Verified by the
        # LFS pointer's own oid, so it is a fact rather than an assumption.
        if not args.allow_downgrade:
            carried, refusal = _carry_forward_height(os.path.join(out_dir, filename), document)
            if refusal:
                print(f"REFUSED: {filename} — {refusal}", file=sys.stderr)
                refusals.append(filename)
            elif carried:
                carried_forward.append(filename)
        text = package.dumps(document)
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

    if refusals:
        # All or nothing: a MANIFEST naming sha256s of bytes we declined to write would be a
        # third state, worse than either of the two honest ones.
        print(f"refused {len(refusals)} package(s); nothing was written. Re-run where the Git "
              f"LFS objects are present, or pass --allow-downgrade to empty the layer on "
              f"purpose.", file=sys.stderr)
        return 2
    for filename in carried_forward:
        print(f"carried the committed height contour forward into {filename} "
              f"(same texture, verified by its LFS oid)")

    failures, refused = [], []
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
            # Two causes are about the CHECKOUT rather than the commit, and each reads as an
            # ordinary STALE without a name on it. Both are printed: a pointer-only checkout of a
            # shallow clone has both, and a reader who fixed one and still saw red would go
            # hunting for a scene re-bank that never happened.
            for cause in (_shallow_history_differs(written, out_dir),
                          _lfs_state_differs(written, out_dir)):
                if cause:
                    print(cause, file=sys.stderr)
            return 1
        print(f"up to date ({len(written)} files)")
    return 0


def _carry_forward_height(target, doc):
    """Keep an LFS-derived ground layer that is **provably still current**, or say why not.

    Returns ``(carried, refusal)``. ``carried`` is True when this run could not read the height
    bytes but the committed package already holds a contour for *exactly* the texture this
    checkout points at — in which case ``doc`` is updated in place with that contour and stamped.

    The proof is the pointer itself. A Git LFS pointer's ``oid sha256`` IS the sha256 of the
    object it stands for, so a pointer-only checkout can verify which bytes the committed
    contour was made from without ever holding them. Equal hash, equal bytes, equal contour —
    carrying it forward states a fact, it does not guess one.

    When the hashes differ the carried data really is stale, and no amount of local work can
    rebuild it: that returns a refusal instead, because the alternative is a routine re-export
    quietly deleting a coastline and reporting success.
    """
    if not os.path.exists(target):
        return False, None
    now_height = doc.get("x-provenance", {}).get("heightMap") or {}
    if now_height.get("textureBytesRead"):
        return False, None                      # this run read the bytes; nothing to carry
    try:
        with open(target, "r", encoding="utf-8") as fh:
            was = json.load(fh)
    except (ValueError, OSError):
        return False, None
    was_height = was.get("x-provenance", {}).get("heightMap") or {}
    # A package that was ITSELF carried forward still holds the contour and is still pinned to
    # the same texture hash, so it is just as valid a source as a freshly-read one. Requiring
    # `textureBytesRead` here made the guard fire exactly once: the second pointer-only export
    # in a row saw its own carried-forward output, judged it no richer, and emptied the coast.
    if not (was_height.get("textureBytesRead") or was_height.get("heightCarriedForward")):
        return False, None                      # the committed one is no richer than this one

    was_terrain = was.get("terrain", {})
    ground = (was_terrain.get("layers", {}) or {}).get("ground")
    if not ground:
        return False, None

    committed_sha = was_height.get("textureSha256")
    current_sha = now_height.get("textureSha256")
    if not committed_sha or committed_sha != current_sha:
        painted = sum(run[1] for run in (ground.get("rle") or []) if run and run[0])
        return False, (
            f"the committed package holds a ground contour of {painted:,} painted cells built "
            f"from height bytes this checkout does not have, and the texture has CHANGED since "
            f"(committed {str(committed_sha)[:12]}…, now {str(current_sha)[:12]}…). It cannot be "
            f"rebuilt here and must not be carried forward stale — re-run where the bytes are.")

    # Same texture, so everything the committed package derived from those bytes is exactly what
    # these bytes produce. Carry ALL of it across and stamp where it came from.
    #
    # ⚠ EVERY field, not the ones somebody remembered. §6.1's promise is that a pointer-only
    # re-export must not delete a coastline, and that promise is only as wide as this list: a
    # field added to the exporter and not added here is silently blanked by the next re-export
    # from a checkout without the bytes. That has happened twice — `stats.tiles.ground` and the
    # ground legend were both being dropped, and `x-heightFieldFull`, the face samples and the
    # moored hulls' beds joined them when the tide landed. `_height_derived` is now the one list,
    # and `test_a_pointer_only_re_export_keeps_every_height_derived_field` walks the document
    # rather than a remembered set, so the next such field fails a test instead of a harbour.
    _carry_height_derived(was, doc)
    for block in (doc["x-provenance"]["heightMap"], doc["terrain"].get("x-heightMap") or {}):
        block["textureBytesRead"] = False
        block["heightCarriedForward"] = True
        block["carryForwardNote"] = (
            "this checkout has only the LFS pointer, but its oid sha256 matches the texture the "
            "committed contour was built from, so that contour is exactly what these bytes "
            "produce and is kept rather than emptied")
    return True, None


def _carry_height_derived(was, doc):
    """Copy every value the height texture produced from ``was`` into ``doc``, in place.

    Grouped by where it lives rather than listed flat, because the three shapes need three
    different joins: terrain is one object, entities and hulls are lists that have to be matched
    up by identity before a field can move between them.
    """
    was_terrain, terrain = was.get("terrain") or {}, doc.get("terrain") or {}

    # 1. The ground contour and both height fields — whole objects, straight across.
    for key in ("x-heightField", "x-heightFieldFull"):
        if key in was_terrain:
            terrain[key] = was_terrain[key]
    layers = (was_terrain.get("layers") or {}).get("ground")
    if layers and "ground" in (terrain.get("layers") or {}):
        terrain["layers"]["ground"] = layers

    # 2. The ground LEGEND. Its keys are the values the ground rle points at, so carrying the
    # layer without them leaves an rle naming legend entries that are not there — which is the
    # one shape `test_every_legend_key_is_used_and_every_rle_value_is_a_legend_key` forbids.
    # Only the ground's own keys move; another layer's legend is this run's to state.
    was_legend = was_terrain.get("legend") or {}
    legend = terrain.setdefault("legend", {})
    for key, value in was_legend.items():
        if (value or {}).get("layer") == "ground":
            legend[key] = value

    # 3. The painted-cell count. ⚠ It lives at the DOCUMENT's `stats`, not the terrain's — the
    # code here looked for `terrain.stats` for months and therefore never carried it at all,
    # which is why a pointer-only re-export reported 0 painted ground cells beside a ground layer
    # full of them.
    was_tiles = (was.get("stats") or {}).get("tiles") or {}
    tiles = (doc.get("stats") or {}).get("tiles")
    if isinstance(tiles, dict) and "ground" in was_tiles:
        tiles["ground"] = was_tiles["ground"]

    # 4. Per-entity: the face's height-map cross-check. Joined on the entity id, which is minted
    # from path and position and is stable across runs (see package._stable_id) — never on list
    # position, because a scene edit that adds one entity would shift every sample by one and put
    # the north wall's reading on the breakwater.
    was_faces = {e.get("id"): (e.get("x-tidalFace") or {}).get("x-heightMapSample")
                 for e in (was.get("entities") or [])}
    for entity in (doc.get("entities") or []):
        face = entity.get("x-tidalFace")
        if not face:
            continue
        sample = was_faces.get(entity.get("id"))
        if sample is not None:
            face["x-heightMapSample"] = sample

    # 5. Per-hull: the bed under her, and the sentence saying where it came from. Joined on
    # x-path, which is what identifies a hull here — they carry no minted id, and two boats at one
    # berth would otherwise be indistinguishable.
    was_hulls = {h.get("x-path"): (h.get("x-tidalRide") or {})
                 for h in (was.get("x-tidalHulls") or [])}
    #
    # ⚠ BOTH fields, and for EVERY matched hull — including the ones whose bed is legitimately
    # null. "Her plan point falls outside the painted map" is itself something only a run holding
    # the bytes can say; a pointer-only run can only manage "not sampled: the texture is an LFS
    # pointer", which is true of the checkout and says nothing about the hull. The committed
    # sentence is the better-informed one, and dropping it because the number beside it is null
    # was the first thing the walk-the-document test caught.
    carried_beds = 0
    for hull in (doc.get("x-tidalHulls") or []):
        ride = hull.get("x-tidalRide") or {}
        previous = was_hulls.get(hull.get("x-path"))
        if previous is None or ride.get("bedElevation") is not None:
            continue
        if "bedElevation" in previous:
            ride["bedElevation"] = previous["bedElevation"]
            if previous["bedElevation"] is not None:
                carried_beds += 1
        if previous.get("x-bedFrom"):
            ride["x-bedFrom"] = previous["x-bedFrom"]

    # 6. And the tally that COUNTS what step 5 just restored. A note left saying 1 of 8 hulls has
    # a bed, beside eight hulls that have one, is worse than either answer on its own.
    if carried_beds:
        notes = ((doc.get("x-provenance") or {}).get("entityNotes") or {}).get("tidalHulls")
        if isinstance(notes, dict):
            working = [h for h in (doc.get("x-tidalHulls") or []) if h.get("x-kind") != "review"]
            notes["workingWithBed"] = sum(
                1 for h in working if (h.get("x-tidalRide") or {}).get("bedElevation") is not None)


def _shallow_history_differs(written, out_dir):
    """The other checkout-shaped staleness — named, so it is not mistaken for a stale scene.

    A shallow clone cannot say when a scene was banked (see ``provenance``'s note), so it exports
    nulls where a full clone exports a commit, a date, a subject and a drift. Both are correct for
    what they could see, and the difference is the CHECKOUT — but a bare "STALE" sends a reader
    looking for a re-bank that never happened, which is the same trap ``_lfs_state_differs``
    exists to close.
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
        was_complete = (was.get("x-provenance") or {}).get("historyIsComplete")
        now_complete = (now.get("x-provenance") or {}).get("historyIsComplete")
        if was_complete is None or now_complete is None or was_complete == now_complete:
            continue
        here, there = ("SHALLOW", "complete") if not now_complete else ("complete", "SHALLOW")
        return (
            f"  cause: this checkout's git history is {here} where the committed packages were "
            f"generated from a {there} one. A shallow clone cannot see which commit banked a "
            f"scene — at depth 1 `git log -1 -- <scene>` answers HEAD for every path — so it "
            f"states null rather than a falsehood, and the two documents differ by exactly those "
            f"nulls. This is a checkout difference, not a stale scene — run "
            f"`git fetch --unshallow` (or checkout with fetch-depth: 0) and compare again.")
    return None


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
