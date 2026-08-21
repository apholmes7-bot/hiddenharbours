"""Real ``call.opts`` for a baked cell, read from the recipe ledger (#629).

Until the ledger landed, every ``call`` this exporter emitted carried an empty ``opts`` and said
why: a baked sheet did not record which option axes produced a given cell, and the rigs resolve an
unknown key as a *silent fallback* rather than an error, so a guessed option renders a confidently
wrong object with nothing to flag it.

``<stem>.recipe.json`` beside a sheet now records exactly that. This module is a **lookup, not a
derivation** — the ledger's own §6 word for it. Nothing here re-implements a baker.
"""

import json
import os

SCHEMA = "hiddenharbours.rig-recipe/1"

# The whole schema, so an unfamiliar key can be refused rather than ignored. The ledger's C# reader
# is strict for the reason its §6 gives: silently dropping a key is how a consumer draws the wrong
# variant while believing it read the whole file. A reader that is strict on one side of the fence
# and lax on the other gives that guarantee away.
_TOP = {"schema", "sheet", "kit", "baker", "rig", "call", "grid", "pack", "sheetSha256"}
_RIG = {"key", "global", "source", "sha256", "convention", "prerequisites"}
_CALL = {"fn", "args", "opts"}
_GRID = {"columns", "rows", "order", "axes"}
_AXIS = {"name", "bind", "values"}


def read(repo, sheet_rel):
    """``(recipe, why)`` — the parsed recipe beside a sheet, or ``(None, why)``.

    ``why`` is ``None`` when the sheet simply has no recipe (the honest empty-opts form still
    stands for it); it carries a reason when a recipe exists but must not be trusted.
    """
    if not sheet_rel:
        return None, None
    rel = os.path.splitext(sheet_rel)[0] + ".recipe.json"
    if not repo.exists(rel):
        return None, None
    try:
        with open(repo.abs(rel), "r", encoding="utf-8") as handle:
            recipe = json.load(handle)
    except (OSError, ValueError) as problem:
        return None, f"{rel} could not be read ({problem})"

    if recipe.get("schema") != SCHEMA:
        return None, f"{rel} declares schema {recipe.get('schema')!r}, not {SCHEMA!r}"
    unknown = _unknown_keys(recipe)
    if unknown:
        return None, (f"{rel} carries {unknown}, which this reader does not know. Refusing rather "
                      f"than ignoring: a key dropped in silence is how a consumer draws the wrong "
                      f"variant while believing it read the whole file.")

    # The sheet's own hash, cross-checked. A recipe describes ONE bake; if the sheet on disk is a
    # different one, its axes and opts describe cells that are not there. Refused, not warned.
    # The PNG is a Git LFS object, but a pointer's `oid sha256` IS the content sha256, so this
    # holds in a pointer-only checkout exactly as it does in a full one.
    declared = recipe.get("sheetSha256")
    actual = repo.content_sha256(sheet_rel)
    if declared and actual and declared != actual:
        return None, (f"{rel} was written for a sheet with sha256 {declared[:12]}…, but the sheet "
                      f"on disk is {actual[:12]}… — the recipe describes a different bake")

    recipe["x-recipePath"] = rel
    return recipe, None


def _unknown_keys(recipe):
    """Every key in the document this reader has no meaning for, deepest names qualified."""
    found = sorted(set(recipe) - _TOP)
    for block, allowed in (("rig", _RIG), ("call", _CALL), ("grid", _GRID)):
        node = recipe.get(block)
        if isinstance(node, dict):
            found += [f"{block}.{key}" for key in sorted(set(node) - allowed)]
    for axis in (recipe.get("grid") or {}).get("axes") or []:
        if isinstance(axis, dict):
            found += [f"grid.axes.{key}" for key in sorted(set(axis) - _AXIS)]
    return found


def cell_index(recipe, rect):
    """The odometer index of the cell a sliced sprite occupies, or ``None``.

    Unity's sprite rect has its origin at the texture's BOTTOM-left while the bakers pack
    ``row = i // columns`` from the TOP, so the row is flipped here. Measured rather than assumed:
    `camper_clipper_rest_d0` … `_d7` occupy top-rows 0…7 in that order.
    """
    pack, grid = recipe.get("pack") or {}, recipe.get("grid") or {}
    cell_w, cell_h = pack.get("cellW"), pack.get("cellH")
    sheet_h, columns = pack.get("sheetH"), grid.get("columns")
    if not (cell_w and cell_h and sheet_h and columns) or not rect or len(rect) < 4:
        return None
    x, y, _, height = rect
    if None in (x, y, height):
        return None
    column = int(round(x / cell_w))
    row = int(round((sheet_h - y - height) / cell_h))
    if column < 0 or row < 0 or column >= columns:
        return None
    return row * columns + column


def call_for(recipe, index):
    """``(call, dir_value)`` — the rig call that drew ONE cell, or ``(None, None)``.

    The axes are an odometer with the **first axis fastest** (ledger §2.3), so an axis's own index
    is ``(i // stride) % len(values)`` with ``stride`` the product of every faster axis's length.
    Each `opt:` axis writes its value over the base opts; the `dir` axis is the direction argument
    rather than an option, and is reported separately.
    """
    grid = recipe.get("grid") or {}
    if grid.get("order") != "rowMajor" or index is None:
        return None, None
    base = recipe.get("call") or {}
    opts = dict(base.get("opts") or {})
    direction, stride = None, 1
    for axis in grid.get("axes") or []:
        values = axis.get("values") or []
        if not values:
            return None, None
        value = values[(index // stride) % len(values)]
        bind = axis.get("bind") or ""
        if bind == "dir":
            direction = value
        elif bind.startswith("opt:"):
            opts[bind[4:]] = value
        else:
            # `arg:<n>` is in the ledger's grammar but appears in no committed recipe. Refuse
            # rather than drop it: an unbound axis means these opts are not the cell's opts.
            return None, None
        stride *= len(values)
    return {"fn": base.get("fn"), "args": list(base.get("args") or []), "opts": opts}, direction
