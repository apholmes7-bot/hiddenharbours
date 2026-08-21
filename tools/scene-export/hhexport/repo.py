"""Reading the repo's own facts: GUIDs, sprite import settings, region defs, rigs.

Every fact here is read from its **source of truth**:

  * region frame        -> ``Assets/_Project/Data/Regions/*.asset`` (the RegionDef)
  * sprite cell + pivot -> the ``.png.meta`` import settings the baker wrote
  * rig identity        -> ``docs/art/rigs/**`` bytes, LF-normalised, sha256
  * height map          -> the PaintedHeightMap asset, and the Git LFS pointer's own oid

Nothing here re-derives a value the repo already states once.
"""

import hashlib
import json
import os
import re

from . import unityyaml as U

RIG_ROOT = "docs/art/rigs"
REGION_DIR = "Assets/_Project/Data/Regions"
TERRAIN_DIR = "Assets/_Project/Data/Terrain"

# CameraFollow.AssetsPPU — "one PPU never changes" (const int 32). Not the water shader's
# _PixelsPerUnit = 24 sampling grid, which is a different quantity entirely.
ASSETS_PPU = 32

_GUID = re.compile(r"^guid:\s*([0-9a-f]{32})\s*$", re.M)
_RIG_PATH = re.compile(r"docs/art/rigs/[A-Za-z0-9_\-./]+\.js")
_LFS_OID = re.compile(r"^oid sha256:([0-9a-f]{64})\s*$", re.M)

# Unity's SpriteAlignment enum -> normalised (bottom-left origin) pivot.
_ALIGNMENT_PIVOTS = {
    0: (0.5, 0.5),    # Center
    1: (0.0, 1.0),    # TopLeft
    2: (0.5, 1.0),    # TopCenter
    3: (1.0, 1.0),    # TopRight
    4: (0.0, 0.5),    # LeftCenter
    5: (1.0, 0.5),    # RightCenter
    6: (0.0, 0.0),    # BottomLeft
    7: (0.5, 0.0),    # BottomCenter
    8: (1.0, 0.0),    # BottomRight
}


def sha256_lf(path):
    """sha256 of a text file with CR stripped — the repo's rig-pinning convention.

    Matches ``tr -d '\\r' | sha256sum`` exactly, which is how every existing sidecar's
    ``derivedFromRigSha256`` was produced.
    """
    with open(path, "rb") as fh:
        return hashlib.sha256(fh.read().replace(b"\r", b"")).hexdigest()


def sha256_bytes(data):
    return hashlib.sha256(data).hexdigest()


class Repo:
    """An index over the committed repo, built once per run."""

    def __init__(self, root):
        self.root = os.path.abspath(root)
        self._guid_to_path = None
        self._meta_cache = {}
        self._contract_cache = {}
        self._sidecar_cache = {}
        self._rig_link_cache = {}
        self._facings_cache = {}
        self._zone_table = None
        self._rig_sha_cache = {}
        self._decor_base = None
        self._rig_globals = None
        self._rig_global_cache = {}

    # --- paths ---------------------------------------------------------------------------

    def abs(self, rel):
        return os.path.join(self.root, rel)

    def exists(self, rel):
        return os.path.exists(self.abs(rel))

    # --- GUIDs ---------------------------------------------------------------------------

    @property
    def guid_to_path(self):
        if self._guid_to_path is None:
            self._guid_to_path = self._index_guids()
        return self._guid_to_path

    def _index_guids(self):
        index = {}
        for base in ("Assets", "Packages"):
            top = self.abs(base)
            if not os.path.isdir(top):
                continue
            for dirpath, dirnames, filenames in os.walk(top):
                dirnames[:] = sorted(d for d in dirnames if d not in ("Library", "Temp", "obj"))
                for name in sorted(filenames):
                    if not name.endswith(".meta"):
                        continue
                    meta = os.path.join(dirpath, name)
                    try:
                        with open(meta, "r", encoding="utf-8", errors="replace") as fh:
                            head = fh.read(512)
                    except OSError:
                        continue
                    found = _GUID.search(head)
                    if found:
                        rel = os.path.relpath(meta[:-5], self.root).replace(os.sep, "/")
                        index[found.group(1)] = rel
        return index

    def path_for_guid(self, guid):
        return self.guid_to_path.get(guid)

    # --- sprite import settings ------------------------------------------------------------

    def sprite_table(self, asset_rel):
        """``{internalID: {...}}`` for one texture, from its ``.meta`` import settings.

        The pivot returned is the **normalised, bottom-left** form Unity itself will use — which
        is `unityPivot` in the editor's own terms. Reading it here rather than re-deriving it
        from a rig sidesteps the review's §8.1 defect class entirely: the baker already resolved
        each rig's anchor into the import settings, so there is no fallback-to-cell-box path.
        """
        if asset_rel in self._meta_cache:
            return self._meta_cache[asset_rel]
        table = {}
        meta_path = self.abs(asset_rel + ".meta")
        if os.path.exists(meta_path):
            table = _sprite_entries(_read_meta(meta_path).get("TextureImporter", {}))
        self._meta_cache[asset_rel] = table
        return table

    # --- region defs ------------------------------------------------------------------------

    def region_def(self, name):
        rel = f"{REGION_DIR}/{name}.asset"
        docs = U.parse_file(self.abs(rel))
        data = docs[0].data if docs else {}
        size = U.vec(data.get("WorldSizeMeters"), "x", "y")
        centre = U.vec(data.get("WorldCenter"), "x", "y")
        return {
            "asset": rel,
            "id": data.get("Id", ""),
            "displayName": data.get("DisplayName", ""),
            "sceneName": data.get("SceneName", ""),
            "worldSizeMeters": [size[0], size[1]],
            "worldCenter": [centre[0], centre[1]],
            "seabedPixelsPerMetre": U.as_float(data.get("SeabedPixelsPerMetre"), 0.0),
            "tideAmplitude": U.as_float(data.get("TideAmplitude"), 0.0),
            # TideModel.Height is `profile.MeanLevel + amp * carrier`, so MeanLevel IS mean water,
            # in the same "metres relative to chart datum" the height map's elevations use. Water
            # level is a FUNCTION OF TIME here (rule 5: recomputed, never saved); this is its mean,
            # which is the only time-independent number the repo actually declares.
            "tideMeanLevel": U.as_float(data.get("TideMeanLevel"), 0.0),
            "tidePhaseHours": U.as_float(data.get("TidePhaseHours"), 0.0),
        }

    # --- painted height ----------------------------------------------------------------------

    def painted_height(self, asset_name):
        """The PaintedHeightMap asset plus the LFS identity of its texture, if committed.

        The texture bytes are a Git LFS object and are not present in a fresh checkout, so the
        map is pinned by the pointer's own ``oid sha256`` — an exact content identity that needs
        no bytes.
        """
        rel = f"{TERRAIN_DIR}/{asset_name}.asset"
        if not self.exists(rel):
            return None
        docs = U.parse_file(self.abs(rel))
        data = docs[0].data if docs else {}
        size = U.vec(data.get("_worldSize"), "x", "y")
        centre = U.vec(data.get("_worldCenter"), "x", "y")
        tex_guid = U.ref_guid(data.get("_heightTexture"))
        tex_rel = self.path_for_guid(tex_guid) if tex_guid else None
        out = {
            "asset": rel,
            "worldSizeMeters": [size[0], size[1]],
            "worldCenter": [centre[0], centre[1]],
            "minElevation": U.as_float(data.get("_minElevation")),
            "maxElevation": U.as_float(data.get("_maxElevation")),
            "texture": tex_rel,
            "textureSha256": None,
        }
        if tex_rel and self.exists(tex_rel):
            out["textureSha256"] = self._content_sha(tex_rel)
        return out

    def content_sha256(self, rel):
        """The sha256 of a committed file's CONTENT, pointer-only checkout or not."""
        return self._content_sha(rel) if self.exists(rel) else None

    def _content_sha(self, rel):
        """sha256 of a file's content — **from the LFS pointer's oid when that is what is on disk**.

        A Git LFS pointer's ``oid sha256`` IS the sha256 of the object it stands for, so a
        pointer checkout and a full checkout yield the same hash under the same key. Reporting
        which of the two was on disk would make the export differ between environments for no
        gain: it is a fact about the machine, not about the harbour, and it broke ``--check``
        on a full-LFS clone before this.
        """
        with open(self.abs(rel), "rb") as handle:
            head = handle.read(256)
            if head.startswith(b"version https://git-lfs"):
                found = _LFS_OID.search(head.decode("utf-8", "replace"))
                return found.group(1) if found else None
            return sha256_bytes(head + handle.read())

    # --- rigs --------------------------------------------------------------------------------

    def sidecars_for_sheet(self, sheet_rel):
        """Sidecar JSONs that may speak for a baked sheet, most specific first.

        The bakers write a JSON next to what they bake — ``*.contract.json`` for the iso-pack
        kits, ``<Sheet>.json`` / ``<Family>.json`` for the trees, buildings, interiors and
        character builds — and every one names the rig it was measured from. That committed data
        is the repo's answer to the review's Q2 finding that the editor's flattened ``rigSource``
        does not resolve, so the exporter needs no ``family -> filename`` table of its own.

        A sidecar only speaks for a sheet if it plainly covers it: the same stem, a kit
        ``*.contract.json``, an index named for its own folder (``Trees/Trees.json``), or a
        folder's lone sidecar that is not itself a per-sheet file. ``Art/Boats/`` holds four
        per-hull anchor files and no index, so a dory resolves to nothing there rather than to
        whichever hull sorts first — which is the failure this rule exists to prevent.
        """
        stem = _stem(sheet_rel)
        folder = os.path.dirname(sheet_rel)
        ordered = []
        while folder and folder.startswith("Assets"):
            if folder not in self._contract_cache:
                names = []
                abs_folder = self.abs(folder)
                if os.path.isdir(abs_folder):
                    names = sorted(n for n in os.listdir(abs_folder) if n.endswith(".json"))
                self._contract_cache[folder] = names
            names = self._contract_cache[folder]
            folder_name = os.path.basename(folder)
            exact = [n for n in names if n[:-5] == stem]
            contracts = [n for n in names if n.endswith(".contract.json") and n not in exact]
            index = [
                n for n in names
                if n not in exact and n not in contracts
                and (n[:-5] == folder_name
                     or (len(names) == 1 and not self.exists(f"{folder}/{n[:-5]}.png")))
            ]
            ordered += [f"{folder}/{n}" for n in exact + contracts + index]
            folder = os.path.dirname(folder)
        return ordered

    def rig_for_sheet(self, sheet_rel):
        """``(rigName, rigSourcePath, evidencePath, candidates)`` for a baked sheet.

        Resolution is by **verified existence**, never by guess: a sidecar must name a
        ``docs/art/rigs/**.js`` that is on disk. The first sidecar that names any resolvable rig
        decides — and if it names more than one, the sheet is reported ambiguous with the list
        rather than assigned one of them. The review's own lesson from the sport-skiff rename is
        that a confident wrong link costs more than an admitted gap.
        """
        if sheet_rel in self._rig_link_cache:
            return self._rig_link_cache[sheet_rel]
        stem = _stem(sheet_rel).lower()
        result = (None, None, None, [])
        for sidecar in self.sidecars_for_sheet(sheet_rel):
            found = self._rigs_named_by(sidecar)
            if not found:
                continue
            rigs = [rig for _, rig in found]
            if len(found) == 1:
                result = (_stem(found[0][1]), found[0][1], sidecar, rigs)
                break
            # Several rigs in one kit: let the sheet's own name pick, by the longest owner key
            # it starts with. A tie or a miss stays ambiguous rather than picking the first.
            matches = []
            for labels, rig in found:
                hit = max((label for label in labels if stem.startswith(label.lower())),
                          key=len, default=None)
                if hit:
                    matches.append((len(hit), hit, rig))
            matches.sort(key=lambda m: m[0], reverse=True)
            if len(matches) == 1 or (len(matches) > 1 and matches[0][0] > matches[1][0]):
                _, hit, rig = matches[0]
                result = (_stem(rig), rig, f"{sidecar}#{hit}", rigs)
            else:
                result = (None, None, sidecar, rigs)
            break
        self._rig_link_cache[sheet_rel] = result
        return result

    def root_zones(self):
        """``[(prefix, zone, evidence)]`` longest-prefix-first, or ``[]`` when the table is absent.

        The write-back contract's landing zones (its §1.2) decide whether an edit is a one-value
        change to a row or a thing the builder has no row for at all — the difference between
        "the owner may move this" and a gesture the next regenerate throws away. Ruled 2026-08-21
        to travel IN the package rather than as a side-channel list the editor keeps in step.

        Keyed on a path PREFIX, not a root, because roots mix: ``StPetersWharf`` holds both a deck
        and its fittings. A prefix this table does not carry exports no zone rather than a guess.
        """
        if self._zone_table is None:
            rel = "docs/tools/reference/root-zones.json"
            rows = []
            if self.exists(rel):
                try:
                    with open(self.abs(rel), "r", encoding="utf-8") as handle:
                        data = json.load(handle)
                    rows = [(row["prefix"], row["zone"], row.get("evidence"),
                             row.get("resolve", True), row.get("resolveNote"))
                            for row in data.get("prefixes") or []]
                except (OSError, ValueError, KeyError):
                    rows = []
            rows.sort(key=lambda row: len(row[0]), reverse=True)
            self._zone_table = rows
        return self._zone_table

    def zone_for_path(self, hierarchy):
        """``(zone, evidence)`` for a scene path — longest matching prefix, or ``(None, None)``."""
        if not hierarchy:
            return None, None
        for prefix, zone, evidence, _resolve, _note in self.root_zones():
            if hierarchy == prefix or hierarchy.startswith(prefix + "/"):
                return zone, evidence
        return None, None

    def resolution_excluded(self, hierarchy):
        """Why this path is deliberately left out of the rig-resolution map, or ``None``.

        Distinct from "no sidecar names a rig for this sheet": that is a gap somebody could close,
        and this is a ruling that closing it would be wrong.
        """
        if not hierarchy:
            return None
        for prefix, _zone, _evidence, resolve, note in self.root_zones():
            if hierarchy == prefix or hierarchy.startswith(prefix + "/"):
                return None if resolve else (note or "excluded by ruling")
        return None

    def facings_for_sheet(self, sheet_rel):
        """``(count, evidencePath)`` — the sheet's DECLARED facing count, or ``(None, None)``.

        The write-back contract §2 is emphatic that the steps are the sheet's and the sheet says
        how many — *"it reads the count, it does not assume one"* — because `IsoPropSheetBaker`
        already fails a bake when a rig's measured `NativeDirs` disagrees with its contract's
        `Facings`. So this reads a declaration or reports nothing; it never falls back to 8.

        Classes fall out of that rule rather than being listed here, which keeps a content
        question out of code (rule 2): a baked building's sidecar declares `facings: 8`; foliage
        and shore plants declare none, because those rigs publish variants and seasons rather
        than directions; and the legacy single sprites declare none either, which is exactly the
        contract's own per-entity test rather than a family-name list.
        """
        if sheet_rel in self._facings_cache:
            return self._facings_cache[sheet_rel]
        result = (None, None)
        for sidecar in self.sidecars_for_sheet(sheet_rel):
            try:
                with open(self.abs(sidecar), "r", encoding="utf-8", errors="replace") as fh:
                    data = json.load(fh)
            except (OSError, ValueError):
                continue
            found = _declared_facings(data)
            if found is not None:
                result = (found, sidecar)
                break
        self._facings_cache[sheet_rel] = result
        return result

    def _rigs_named_by(self, sidecar_rel):
        """Every rig a sidecar names that exists on disk — **declared keys first, prose second**.

        ``Foliage/Trees/Trees.json`` is why the order matters: its ``rig`` key says
        ``treeIsoRig2.js`` while its prose ``note`` still credits the v1 rig it was ported from.
        A declared field is the sidecar speaking; prose is the sidecar reminiscing.
        """
        if sidecar_rel in self._sidecar_cache:
            return self._sidecar_cache[sidecar_rel]
        try:
            with open(self.abs(sidecar_rel), "r", encoding="utf-8", errors="replace") as fh:
                raw = fh.read()
        except OSError:
            raw = ""
        try:
            data = json.loads(raw) if raw else None
        except ValueError:
            data = None
        found = self._resolve_all(_declared_rig_blocks(data))
        if not found:
            # Nothing declared resolves — fall back to any rig path the sidecar's prose names.
            found = self._resolve_all([{"labels": [], "names": _RIG_PATH.findall(raw)}])
        self._sidecar_cache[sidecar_rel] = found
        return found

    def _resolve_all(self, blocks):
        """``(labels, rigPath)`` for each declaring block whose rig exists on disk."""
        out, seen = [], set()
        for block in blocks:
            for name in block["names"]:
                for candidate in (name, f"{RIG_ROOT}/{name}.js"):
                    if (candidate.startswith(RIG_ROOT) and candidate.endswith(".js")
                            and candidate not in seen and self.exists(candidate)):
                        seen.add(candidate)
                        out.append((tuple(block["labels"]), candidate))
                        break
        return out

    def rig_global(self, rig_rel):
        """The global a rig installs (``doryIsoRig.js`` -> ``DoryIso``), per ``RigCatalog``.

        The scene editor renders by calling exactly this global, so without it an entity gives it
        nothing to draw. Read from the catalog's registrations rather than guessed from the
        filename — several rigs install a global whose name is not their stem.
        """
        if self._rig_globals is None:
            from .csharp import rig_globals
            self._rig_globals = rig_globals(self)
        if rig_rel in self._rig_globals:
            return self._rig_globals[rig_rel]
        if rig_rel not in self._rig_global_cache:
            from .csharp import global_declared_by_rig
            self._rig_global_cache[rig_rel] = global_declared_by_rig(self, rig_rel)
        return self._rig_global_cache[rig_rel]

    def decor_base(self):
        """``SortingBands.DecorBase``, computed from the constants the C# declares.

        Read rather than hardcoded because it is derived there too —
        ``DecorFloor + (int)(DecorHalfExtentMetres * OrdersPerMetre)`` — so a change to the band
        moves this with it instead of silently biasing every exported ``sortBias``.
        """
        if self._decor_base is None:
            self._decor_base = self._read_decor_base()
        return self._decor_base

    def _read_decor_base(self):
        # Found by name rather than by a path table: a table of where a file lives is exactly
        # the kind of thing that drifts when somebody moves it.
        for candidate in self._find_by_name("SortingBands.cs"):
            with open(self.abs(candidate), encoding="utf-8", errors="replace") as handle:
                text = handle.read()
            values = {}
            for name in ("DecorFloor", "DecorHalfExtentMetres", "OrdersPerMetre"):
                found = re.search(
                    rf"\bconst\s+\w+\s+{name}\s*=\s*([0-9.]+)f?\s*;", text)
                if not found:
                    break
                values[name] = float(found.group(1))
            if len(values) == 3:
                return int(values["DecorFloor"]
                           + int(values["DecorHalfExtentMetres"] * values["OrdersPerMetre"]))
        raise RuntimeError(
            "SortingBands.DecorBase could not be read. sortBias is a delta from it, so guessing "
            "a value would bias every entity in the export.")

    def _find_by_name(self, filename):
        """Every path under ``Assets/`` with this basename, in a stable order."""
        hits = []
        for dirpath, dirnames, filenames in os.walk(self.abs("Assets")):
            dirnames[:] = sorted(d for d in dirnames if d not in ("Library", "Temp", "obj"))
            if filename in filenames:
                hits.append(os.path.relpath(
                    os.path.join(dirpath, filename), self.root).replace(os.sep, "/"))
        return sorted(hits)

    def rig_sha(self, rig_rel):
        if rig_rel not in self._rig_sha_cache:
            self._rig_sha_cache[rig_rel] = sha256_lf(self.abs(rig_rel))
        return self._rig_sha_cache[rig_rel]


def _declared_facings(node):
    """The first ``facings``/``Facings`` integer a sidecar declares, at any depth.

    Depth matters: the building kits put it at the top level, while the shop contracts nest it
    under ``projection``. Both are the sheet declaring its own count, so both count.
    """
    if isinstance(node, dict):
        for key, value in node.items():
            if key.lower() == "facings" and isinstance(value, int) and value > 0:
                return value
        for value in node.values():
            found = _declared_facings(value)
            if found is not None:
                return found
    elif isinstance(node, list):
        for value in node:
            found = _declared_facings(value)
            if found is not None:
                return found
    return None


def _stem(path):
    return os.path.splitext(os.path.basename(path))[0]


# Sidecar keys whose value names the rig a sheet was baked from.
_RIG_KEYS = ("rig", "rigs", "rigsource", "rigpath", "rigscript", "derivedfrom", "source")


def _declared_rig_blocks(data, owner="", out=None):
    """One record per block of the sidecar that declares a rig: ``{"labels", "names"}``.

    A multi-family kit declares its rigs side by side and labels each one — ``Interiors.json``
    has ``rooms[i].rig = "interior"`` next to the full ``interiorIsoRig.js`` path, and ``props[i]``
    the same for ``interiorPropRig.js``. Keeping the labels with the path is what lets a sheet
    called ``InteriorProp_chair`` claim its own rig instead of the folder reading as ambiguous.
    """
    if out is None:
        out = []
    if isinstance(data, dict):
        names = []
        for key, value in data.items():
            if str(key).lower() in _RIG_KEYS:
                for item in (value if isinstance(value, list) else [value]):
                    if isinstance(item, str) and item:
                        names.append(item)
            else:
                _declared_rig_blocks(value, str(key), out)
        if names:
            labels = [owner] if owner else []
            labels += [n for n in names if "/" not in n]
            out.append({"labels": labels, "names": names})
    elif isinstance(data, list):
        for item in data:
            _declared_rig_blocks(item, owner, out)
    return out


def _read_meta(path):
    """A ``.meta`` is one top-level mapping with no document header."""
    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        return U.parse_mapping(fh.read())


def _sprite_entries(importer):
    """``{internalID: entry}`` from a TextureImporter block."""
    out = {}
    ppu = U.as_float(importer.get("spritePixelsToUnits"), float(ASSETS_PPU))
    sheet = importer.get("spriteSheet") or {}
    sprites = sheet.get("sprites") or []
    for entry in sprites:
        if not isinstance(entry, dict):
            continue
        internal = U.as_int(entry.get("internalID"), 0)
        rect = entry.get("rect") or {}
        width = U.as_float(rect.get("width"))
        height = U.as_float(rect.get("height"))
        alignment = U.as_int(entry.get("alignment"), 0)
        pivot = entry.get("pivot") or {}
        if alignment in _ALIGNMENT_PIVOTS:
            px, py = _ALIGNMENT_PIVOTS[alignment]
            pivot_source = "sprite-import.alignment"
        else:
            px, py = U.as_float(pivot.get("x"), 0.5), U.as_float(pivot.get("y"), 0.5)
            pivot_source = "sprite-import.customPivot"
        out[internal] = {
            "name": entry.get("name", ""),
            "rect": [U.as_float(rect.get("x")), U.as_float(rect.get("y")), width, height],
            "cell": [width, height],
            "unityPivot": [px, py],
            "pivotSource": pivot_source,
            "alignment": alignment,
            "ppu": ppu,
        }
    return out
