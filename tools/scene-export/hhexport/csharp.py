"""Reading DECLARED values out of the repo's C# — literals only, never logic.

The builders hold two kinds of fact. Some are *declared*: a route's vertices, a band's floor
elevation, a road's half-width, a rig's registered global. Those are literals in the source and
this module reads them, so the export follows the repo when somebody moves a road.

The rest are *computed* — the park spur solves for the nearest point on any road, St Peters'
paths bend a straight line by a hash-salted offset, and the ground's real band comes off a
wiggled elevation with a sandbar spine test and a weather-sector split. **This module will not
reimplement any of that**, and the exporter reports what it therefore leaves out by name. A
second implementation of a law the repo defines once is how two truths start.
"""

import os
import re


def _stem(path):
    return os.path.splitext(os.path.basename(path))[0]

_VECTOR2 = re.compile(r"new\s+Vector2\s*\(\s*(-?[\d.]+)f?\s*,\s*(-?[\d.]+)f?\s*\)")
_CONST = r"(?:const|static\s+readonly)\s+(?:float|int|double)\s+{name}\s*=\s*(-?[\d.]+)f?\s*;"
_STRING_CONST = r"(?:const|static\s+readonly)\s+string\s+{name}\s*=\s*\"([^\"]*)\"\s*;"


class CSharpSource:
    """One C# file, read as text and asked only for things it states outright."""

    def __init__(self, repo, rel):
        self.repo = repo
        self.rel = rel
        self.text = ""
        if repo.exists(rel):
            with open(repo.abs(rel), encoding="utf-8", errors="replace") as handle:
                self.text = handle.read()

    @property
    def present(self):
        return bool(self.text)

    def number(self, name, default=None, _depth=0):
        found = re.search(_CONST.format(name=re.escape(name)), self.text)
        if found:
            return float(found.group(1))
        # An expression-bodied property that scales ONE other declared constant —
        # `FootpathHalfWidthMetres => StPetersStarterSplat.PathWidthMetres * 0.5f`. Still reading
        # a declaration, one hop further; anything more involved than that is logic and is left
        # alone.
        expr = re.search(
            rf"(?:float|int|double)\s+{re.escape(name)}\s*=>\s*"
            rf"(?:([A-Za-z0-9_]+)\.)?([A-Za-z0-9_]+)\s*(?:([*/])\s*(-?[\d.]+)f?)?\s*;",
            self.text)
        if expr and _depth < 3:
            owner, member = expr.group(1), expr.group(2)
            source = self if not owner or owner == _stem(self.rel) else self._sibling(owner)
            base = source.number(member, None, _depth + 1) if source else None
            if base is not None:
                if expr.group(3) == "*":
                    return base * float(expr.group(4))
                if expr.group(3) == "/":
                    return base / float(expr.group(4))
                return base
        return default

    def _sibling(self, type_name):
        """The C# file that declares ``type_name``, found by filename anywhere under Assets/."""
        for candidate in self.repo._find_by_name(f"{type_name}.cs"):
            return CSharpSource(self.repo, candidate)
        return None

    def string(self, name, default=None):
        found = re.search(_STRING_CONST.format(name=re.escape(name)), self.text)
        return found.group(1) if found else default

    def vector2_array(self, name):
        """The literal vertices of a ``static readonly Vector2[] <name> = { … };`` declaration.

        Returns ``None`` when the member exists but is not a literal array — a method, an
        expression body, a call — because that is a computed route and this module does not
        compute. The caller reports it as omitted rather than approximating it.
        """
        anchor = re.search(
            rf"Vector2\[\]\s+{re.escape(name)}\s*=\s*(?:new\s+Vector2\[\]\s*)?{{", self.text)
        if not anchor:
            return None
        body, depth, index = [], 1, anchor.end()
        while index < len(self.text) and depth:
            char = self.text[index]
            if char == "{":
                depth += 1
            elif char == "}":
                depth -= 1
                if not depth:
                    break
            body.append(char)
            index += 1
        points = [(float(x), float(y)) for x, y in _VECTOR2.findall("".join(body))]
        return points or None

    def declares_member(self, name):
        return re.search(rf"\b{re.escape(name)}\b", self.text) is not None


def global_declared_by_rig(repo, rig_rel):
    """The global a rig installs, read from the rig's own source.

    Every rig's IIFE ends by publishing itself — ``root.ShorePlants = …``,
    ``globalThis.WharfDecor = …``. That covers the rigs ``RigCatalog`` does not register, which
    is most of them: the catalog holds only what the Unity bakers bake, and the palette is
    wider than the bake list.
    """
    if not repo.exists(rig_rel):
        return None
    with open(repo.abs(rig_rel), encoding="utf-8", errors="replace") as handle:
        text = handle.read()
    names = re.findall(r"(?:globalThis|root|window)\.([A-Za-z_][A-Za-z0-9_]*)\s*=(?!=)", text)
    if not names:
        return None
    # The same global is usually published twice (globalThis and root); take the one published
    # most, and break a tie by first appearance so the answer is stable.
    ranked = sorted(set(names), key=lambda n: (-names.count(n), names.index(n)))
    return ranked[0]


def rig_globals(repo):
    """``{rigScriptPath: globalName}`` from every ``RigCatalog.<Kit>.cs`` registration.

    The catalog is where the repo says which global a rig installs — ``doryIsoRig.js`` puts up
    ``DoryIso`` — and the scene editor renders by calling exactly that global. Read rather than
    tabled, so a kit that renames its global moves the export with it.
    """
    out = {}
    folder = "Assets/_Project/Code/Tools/Editor/RigBaking"
    if not os.path.isdir(repo.abs(folder)):
        return out
    for name in sorted(os.listdir(repo.abs(folder))):
        if not (name.startswith("RigCatalog.") and name.endswith(".cs")):
            continue
        with open(repo.abs(f"{folder}/{name}"), encoding="utf-8", errors="replace") as handle:
            text = handle.read()
        for script, global_name in re.findall(
                r'RigEntry\(\s*\$?"\{?RigFolder\}?/([^"]+)"\s*,\s*"([^"]+)"', text):
            out[f"docs/art/rigs/{script}"] = global_name
    return out
