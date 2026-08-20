"""Mapping a resolved rig onto the scene editor's own ``family`` vocabulary.

The editor renders by family, and its wire list is closed: 43 names, transcribed at
``docs/tools/reference/family-names.json``. The ruling that shapes this module is that a name
outside the list is **not renderable there and must never be aliased onto a near neighbour** —
so resolution is normalise-then-exact-match, and everything else is declared unresolved and
reported by name so a new entry can be requested on the editor side.

The normalisation is mechanical: lowercase the rig's file stem, drop a trailing pass digit, then
the ``rig`` and ``iso`` suffixes the repo's naming convention adds. ``characterIsoRig6`` becomes
``character``; ``treeIsoRig2`` becomes ``tree``. Anything that does not land exactly on a listed
name fails closed, which is the point: ``wharfIsoRig`` normalises to ``wharf``, and the list
holds both ``wharfbuilding`` and ``wharfmodule``. Guessing between them is precisely the aliasing
the ruling forbids, so it stays unresolved.
"""

import json
import os
import re

WIRE_LIST = "docs/tools/reference/family-names.json"

_TRAILING_DIGITS = re.compile(r"\d+$")


def load(repo):
    """``(families, layers)`` from the transcribed wire list, or ``(frozenset(), {})``."""
    if not repo.exists(WIRE_LIST):
        return frozenset(), {}
    with open(repo.abs(WIRE_LIST), encoding="utf-8") as handle:
        data = json.load(handle)
    return frozenset(data.get("families") or ()), dict(data.get("layers") or {})


def normalise(rig_rel):
    """The candidate family name a rig's filename implies — before any list check."""
    if not rig_rel:
        return None
    stem = os.path.splitext(os.path.basename(rig_rel))[0].lower()
    stem = _TRAILING_DIGITS.sub("", stem)
    for suffix in ("rig", "iso"):
        if stem.endswith(suffix) and len(stem) > len(suffix):
            stem = stem[: -len(suffix)]
    return _TRAILING_DIGITS.sub("", stem) or None


def resolve(rig_rel, wire_families):
    """``(family, candidate)`` — ``family`` only when the candidate is a listed wire name."""
    candidate = normalise(rig_rel)
    if candidate and candidate in wire_families:
        return candidate, candidate
    return None, candidate
