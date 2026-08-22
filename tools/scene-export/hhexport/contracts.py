"""Real ``call.opts`` from a kit's own committed contract — a second lookup, never a derivation.

``recipes.py`` reads the per-sheet ledger (#629), which records the exact opts **per cell**. It
covers six kits. This module reads the other declaration the bakers already commit: the kit
contract beside a sheet — ``Buildings.json``, ``Interiors.json``, ``yardIso.contract.json``,
``shops.contract.json``, ``shopFixtures.contract.json`` — each of which carries an ``optionsJs``
string per entry, the literal options that kit was baked with.

**Sheet-level, and it says so.** A recipe knows which cell; a contract knows the bake. Where both
exist the recipe wins, because per-cell beats per-sheet and only the recipe can resolve a variant
axis. What a contract adds is the *base* build — the school's siding, era and window pattern —
for the sheets no recipe covers, which is where every empty ``opts`` in these two packages was.

Three refusals, the same house style §6.4 set for the ledger:

* **Matched on the sheet, never on the key.** An entry speaks for the file it names. Matching a
  contract key against a sprite stem would hand the school the general store's siding on a near
  miss, which is the aliasing ruled out everywhere else in this exporter.
* **The literal is parsed, or refused whole.** ``optionsJs`` is JavaScript source. Four of the
  five contracts hold a plain object literal, which is a closed grammar this file parses
  strictly; ``shops.contract.json`` holds ``Object.assign({},Shopfront.PRESETS['harbourStore'])``,
  which is a **call into the rig's own preset table** and cannot be resolved without running the
  rig. That one is reported verbatim as ``x-optsExpression`` for a reader that can run it, and
  its ``opts`` stays empty. Half-evaluating it — taking the preset NAME as an opt, say — would
  invent an option no rig reads.
* **An unparseable literal is a refusal, not a partial dict.** The rigs resolve an unknown key as
  a silent fallback rather than an error (§6.4), so a half-read opts dict draws a confidently
  wrong object with nothing to flag it. Anything this grammar does not fully consume yields no
  opts and a named reason in ``entityNotes.contractRefusals``.
"""

import json
import os
import re

_NUMBER = re.compile(r"-?(?:0|[1-9]\d*|\d+)(?:\.\d+)?(?:[eE][+-]?\d+)?")
_IDENT = re.compile(r"[A-Za-z_$][A-Za-z0-9_$]*")
_WORDS = {"true": True, "false": False, "null": None, "undefined": None}
_QUOTES = ("'", '"')
_ESCAPES = {"n": "\n", "t": "\t", "r": "\r", "b": "\b", "f": "\f"}


class Refused(Exception):
    """The literal is not one this grammar fully understands."""


def parse_object_literal(text):
    """A JS object literal -> ``dict``. Raises ``Refused`` on anything else.

    Deliberately small: object, array, string, number, boolean, null. No identifiers as values,
    no calls, no arithmetic, no template literals, no trailing garbage. Every contract in the
    repo either fits this entirely or is a call expression that must not be guessed at.
    """
    parser = _Parser(text)
    parser.skip_space()
    if parser.peek() != "{":
        raise Refused("not an object literal")
    value = parser.value()
    parser.skip_space()
    if not parser.done():
        raise Refused("trailing input at offset %d" % parser.i)
    return value


class _Parser:
    def __init__(self, text):
        self.s, self.i = text, 0

    def done(self):
        return self.i >= len(self.s)

    def peek(self):
        return self.s[self.i] if self.i < len(self.s) else ""

    def skip_space(self):
        while self.i < len(self.s) and self.s[self.i] in " \t\r\n":
            self.i += 1

    def expect(self, char):
        if self.peek() != char:
            raise Refused("expected %r at offset %d" % (char, self.i))
        self.i += 1

    def value(self):
        self.skip_space()
        char = self.peek()
        if char == "{":
            return self.obj()
        if char == "[":
            return self.arr()
        if char in _QUOTES:
            return self.string()
        if char == "-" or char.isdigit():
            return self.number()
        word = _IDENT.match(self.s, self.i)
        if word and word.group() in _WORDS:
            self.i = word.end()
            return _WORDS[word.group()]
        # An identifier here is a reference into the rig's scope, not a value this file can read.
        raise Refused("unreadable value at offset %d" % self.i)

    def obj(self):
        self.expect("{")
        out = {}
        self.skip_space()
        if self.peek() == "}":
            self.i += 1
            return out
        while True:
            self.skip_space()
            if self.peek() in _QUOTES:
                key = self.string()
            else:
                word = _IDENT.match(self.s, self.i)
                if not word:
                    raise Refused("unreadable key at offset %d" % self.i)
                self.i = word.end()
                key = word.group()
            self.skip_space()
            self.expect(":")
            out[key] = self.value()
            self.skip_space()
            if self.peek() == ",":
                self.i += 1
                self.skip_space()
                if self.peek() == "}":          # a trailing comma is legal JS
                    break
                continue
            break
        self.skip_space()
        self.expect("}")
        return out

    def arr(self):
        self.expect("[")
        out = []
        self.skip_space()
        if self.peek() == "]":
            self.i += 1
            return out
        while True:
            out.append(self.value())
            self.skip_space()
            if self.peek() == ",":
                self.i += 1
                self.skip_space()
                if self.peek() == "]":
                    break
                continue
            break
        self.skip_space()
        self.expect("]")
        return out

    def string(self):
        quote = self.peek()
        self.i += 1
        out = []
        while True:
            if self.done():
                raise Refused("unterminated string")
            char = self.s[self.i]
            if char == "\\":
                self.i += 1
                if self.done():
                    raise Refused("unterminated escape")
                out.append(_ESCAPES.get(self.s[self.i], self.s[self.i]))
                self.i += 1
                continue
            if char == quote:
                self.i += 1
                return "".join(out)
            out.append(char)
            self.i += 1

    def number(self):
        match = _NUMBER.match(self.s, self.i)
        if not match:
            raise Refused("unreadable number at offset %d" % self.i)
        self.i = match.end()
        raw = match.group()
        return float(raw) if ("." in raw or "e" in raw or "E" in raw) else int(raw)


def entry_for_sheet(repo, sheet_rel):
    """The kit-contract entry that names this sheet, or ``None``.

    Discovery is ``repo.sidecars_for_sheet`` — the exporter's ONE rule for which committed file
    speaks for a baked sheet — so a contract that can supply opts is by construction the same
    contract that supplies the rig and the facing count. A second discovery rule here would be a
    second chance to disagree with that one.
    """
    if not sheet_rel:
        return None
    wanted = os.path.basename(sheet_rel)
    for sidecar in repo.sidecars_for_sheet(sheet_rel):
        try:
            with open(repo.abs(sidecar), "r", encoding="utf-8") as handle:
                data = json.load(handle)
        except (OSError, ValueError):
            continue
        if not isinstance(data, dict):
            continue
        for block in sorted(data):
            rows = data[block]
            if not isinstance(rows, list):
                continue
            for row in rows:
                if isinstance(row, dict) and row.get("sheet") == wanted:
                    return {"row": row, "source": sidecar, "block": block}
    return None


def opts_for_sheet(repo, sheet_rel):
    """``(found, problem)`` — the declared base opts for a sheet, or a stated reason there are none.

    ``found`` is ``{opts, source, key, expression, rigScript, rigGlobal}``. ``opts`` is ``None``
    when the entry's ``optionsJs`` is an expression rather than a literal; ``expression`` then
    carries it verbatim, because a reader that can run the rig can resolve what this cannot.
    """
    entry = entry_for_sheet(repo, sheet_rel)
    if entry is None:
        return None, None
    row, source = entry["row"], entry["source"]
    key = row.get("key")
    raw = row.get("optionsJs")
    found = {
        "opts": None,
        "source": source,
        "key": key,
        "expression": None,
        "rigScript": row.get("rigScript"),
        "rigGlobal": row.get("rigGlobal"),
    }
    if not isinstance(raw, str) or not raw.strip():
        return found, None                  # the entry speaks for the sheet, but not about opts
    try:
        found["opts"] = parse_object_literal(raw)
    except Refused as why:
        found["expression"] = raw
        return found, (
            "%s entry %r declares optionsJs that is not a literal this reader can resolve (%s). "
            "It is carried verbatim as x-optsExpression rather than guessed at: the rigs resolve "
            "an unknown key as a silent fallback, so a half-read opts dict draws a confidently "
            "wrong object with nothing to flag it." % (source, key, why))
    return found, None
