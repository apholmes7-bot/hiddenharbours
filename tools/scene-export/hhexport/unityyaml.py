"""A reader for Unity's Force Text YAML — stdlib only, no Unity, no PyYAML.

Unity writes a small, very regular subset of YAML. Parsing that subset by hand rather than
reaching for a general YAML library is deliberate:

  * **Scalars stay strings.** A general loader applies YAML 1.1 type resolution, and Unity's
    packed primitive arrays are bare hex digit strings — ``_viaStart: 0000...0200000005000000``
    resolves as *octal* and comes back as an integer with the bytes destroyed. Keeping every
    scalar as text and converting explicitly at the point of use makes that class of bug
    impossible.
  * **No third-party dependency.** The exporter has to run in a bare container.

What is supported is exactly what Unity emits: block maps, block sequences (whose items sit at
the *same* indent as their key), inline flow maps, empty flow sequences, and single/double
quoted scalars that fold across lines.
"""

import re

_DOC = re.compile(r"^--- !u!(\d+) &(-?\d+)(?: stripped)?\s*$")
_KEY = re.compile(r"^(\s*)([^\s:][^:]*):(?: (.*))?\s*$")


class Document:
    """One ``--- !u!<classID> &<fileID>`` document."""

    __slots__ = ("class_id", "file_id", "type_name", "data", "stripped")

    def __init__(self, class_id, file_id, type_name, data, stripped=False):
        self.class_id = class_id
        self.file_id = file_id
        self.type_name = type_name
        self.data = data if isinstance(data, dict) else {}
        self.stripped = stripped

    def __repr__(self):
        return f"<{self.type_name} &{self.file_id} !u!{self.class_id}>"


def parse_file(path):
    with open(path, "r", encoding="utf-8", errors="replace", newline="") as fh:
        return parse(fh.read())


def parse(text):
    """Parse a Unity YAML stream into a list of :class:`Document`, in file order."""
    lines = text.replace("\r\n", "\n").replace("\r", "\n").split("\n")
    docs = []
    i = 0
    n = len(lines)
    while i < n:
        m = _DOC.match(lines[i])
        if not m:
            i += 1
            continue
        class_id, file_id = int(m.group(1)), int(m.group(2))
        stripped = lines[i].rstrip().endswith("stripped")
        i += 1
        start = i
        while i < n and not _DOC.match(lines[i]):
            i += 1
        body = lines[start:i]
        type_name, data = _parse_document_body(body)
        docs.append(Document(class_id, file_id, type_name, data, stripped))
    return docs


def parse_mapping(text):
    """Parse a whole document that is one top-level mapping — a ``.meta``, which carries no
    ``--- !u!`` header and whose first key (``fileFormatVersion``) is a plain scalar."""
    lines = text.replace("\r\n", "\n").replace("\r", "\n").split("\n")
    value, _ = _parse_block(lines, 0, 0)
    return value if isinstance(value, dict) else {}


def _parse_document_body(lines):
    j = _skip_blank(lines, 0)
    if j >= len(lines):
        return "?", {}
    m = _KEY.match(lines[j])
    if not m or m.group(1):
        # No single root key (rare); parse the whole thing as a block.
        value, _ = _parse_block(lines, j, 0)
        return "?", value
    type_name = m.group(2).strip()
    rest = (m.group(3) or "").strip()
    if rest:
        return type_name, _scalar_or_flow(rest)
    value, _ = _parse_block(lines, j + 1, 1)
    return type_name, value


def _skip_blank(lines, i):
    while i < len(lines) and not lines[i].strip():
        i += 1
    return i


def _indent(line):
    return len(line) - len(line.lstrip(" "))


def _parse_block(lines, i, min_indent):
    """Parse a mapping or sequence whose content is indented at least ``min_indent``."""
    i = _skip_blank(lines, i)
    if i >= len(lines):
        return {}, i
    indent = _indent(lines[i])
    if indent < min_indent:
        return {}, i
    if lines[i].lstrip(" ").startswith("- "):
        return _parse_sequence(lines, i, indent)
    return _parse_mapping(lines, i, indent)


def _parse_mapping(lines, i, indent):
    out = {}
    while True:
        i = _skip_blank(lines, i)
        if i >= len(lines):
            break
        cur = _indent(lines[i])
        if cur < indent:
            break
        stripped = lines[i].lstrip(" ")
        if cur > indent or stripped.startswith("- "):
            # Dangling content we did not expect; stop rather than mis-nest.
            break
        m = _KEY.match(lines[i])
        if not m:
            break
        key = m.group(2).strip()
        rest = m.group(3)
        if rest is None or not rest.strip():
            # An empty value is either a nested block or a genuinely empty scalar. Unity puts a
            # sequence's items at the SAME indent as the key that owns them, so "deeper" is not
            # the test — "next line is a dash at this indent" is.
            j = _skip_blank(lines, i + 1)
            if j < len(lines):
                nxt_indent = _indent(lines[j])
                nxt = lines[j].lstrip(" ")
                if nxt_indent > indent or (nxt_indent == indent and nxt.startswith("- ")):
                    out[key], i = _parse_block(lines, j, min(indent, nxt_indent))
                    continue
            out[key] = ""
            i += 1
            continue
        value, i = _scalar_value(lines, i, rest.strip())
        out[key] = value
    return out, i


def _parse_sequence(lines, i, indent):
    out = []
    while True:
        i = _skip_blank(lines, i)
        if i >= len(lines):
            break
        if _indent(lines[i]) != indent:
            break
        stripped = lines[i].lstrip(" ")
        if not stripped.startswith("- "):
            break
        rest = stripped[2:].strip()
        item_indent = indent + 2
        if rest.startswith("- "):
            # A sequence of sequences (``m_Paths`` on a PolygonCollider2D): the inner sequence
            # starts on the same line, indented two past the outer dash.
            value, i = _parse_nested_sequence(lines, i, indent)
            out.append(value)
            continue
        if rest.startswith("{") or rest.startswith("["):
            value, i = _scalar_value(lines, i, rest)
            out.append(value)
            continue
        m = _KEY.match("  " + rest)
        if m and m.group(2).strip():
            # A mapping whose first key rides on the dash line; the rest is indented under it.
            head_key = m.group(2).strip()
            head_rest = (m.group(3) or "").strip()
            item = {}
            if head_rest:
                item[head_key], i = _scalar_value(lines, i, head_rest)
                i = i
            else:
                j = _skip_blank(lines, i + 1)
                if j < len(lines) and (
                    _indent(lines[j]) > item_indent
                    or (_indent(lines[j]) == item_indent and lines[j].lstrip(" ").startswith("- "))
                ):
                    item[head_key], i = _parse_block(lines, j, item_indent)
                else:
                    item[head_key] = ""
                    i += 1
            tail, i = _parse_mapping_at(lines, i, item_indent)
            item.update(tail)
            out.append(item)
            continue
        value, i = _scalar_value(lines, i, rest)
        out.append(value)
    return out, i


def _parse_nested_sequence(lines, i, outer_indent):
    """Parse ``- - item`` — a sequence item that is itself a sequence.

    The inner sequence's first item rides on the outer dash line, so its items live at
    ``outer_indent + 2``; rewriting the first line to that indent lets the ordinary sequence
    parser take it from there.
    """
    inner_indent = outer_indent + 2
    patched = list(lines)
    patched[i] = " " * inner_indent + lines[i].lstrip(" ")[2:]
    return _parse_sequence(patched, i, inner_indent)


def _parse_mapping_at(lines, i, indent):
    j = _skip_blank(lines, i)
    if j >= len(lines) or _indent(lines[j]) != indent:
        return {}, i
    if lines[j].lstrip(" ").startswith("- "):
        return {}, i
    return _parse_mapping(lines, j, indent)


def _scalar_value(lines, i, rest):
    """Return (value, next_line_index) for a scalar that may fold across lines."""
    if rest.startswith("{") or rest.startswith("["):
        return _scalar_or_flow(rest), i + 1
    if rest[:1] in ("'", '"'):
        quote = rest[0]
        text = rest
        while not _closed(text, quote):
            i += 1
            if i >= len(lines):
                break
            text += "\n" + lines[i].strip()
        return _unquote(text, quote), i + 1
    return rest, i + 1


def _closed(text, quote):
    if len(text) < 2:
        return False
    body = text[1:]
    k = 0
    while k < len(body):
        if body[k] == quote:
            if quote == "'" and k + 1 < len(body) and body[k + 1] == "'":
                k += 2
                continue
            if quote == '"' and k > 0 and body[k - 1] == "\\":
                k += 1
                continue
            return True
        k += 1
    return False


def _unquote(text, quote):
    body = text[1:]
    end = len(body)
    k = 0
    while k < len(body):
        if body[k] == quote:
            if quote == "'" and k + 1 < len(body) and body[k + 1] == "'":
                k += 2
                continue
            if quote == '"' and k > 0 and body[k - 1] == "\\":
                k += 1
                continue
            end = k
            break
        k += 1
    body = body[:end]
    if quote == "'":
        return body.replace("''", "'")
    return body.replace('\\"', '"').replace("\\\\", "\\")


def _scalar_or_flow(rest):
    rest = rest.strip()
    if rest.startswith("{"):
        return _flow_map(rest)
    if rest.startswith("["):
        inner = rest[1:-1].strip() if rest.endswith("]") else rest[1:].strip()
        if not inner:
            return []
        return [p.strip() for p in _split_top(inner)]
    if rest[:1] in ("'", '"') and _closed(rest, rest[0]):
        return _unquote(rest, rest[0])
    return rest


def _flow_map(rest):
    if rest.endswith("}"):
        inner = rest[1:-1]
    else:
        inner = rest[1:]
    out = {}
    for part in _split_top(inner):
        if not part.strip():
            continue
        key, _, value = part.partition(":")
        out[key.strip()] = _scalar_or_flow(value.strip())
    return out


def _split_top(text):
    """Split on commas that are not inside nested braces/brackets or quotes."""
    parts, depth, buf, quote = [], 0, [], None
    for ch in text:
        if quote:
            buf.append(ch)
            if ch == quote:
                quote = None
            continue
        if ch in "'\"":
            quote = ch
            buf.append(ch)
        elif ch in "{[":
            depth += 1
            buf.append(ch)
        elif ch in "}]":
            depth -= 1
            buf.append(ch)
        elif ch == "," and depth == 0:
            parts.append("".join(buf))
            buf = []
        else:
            buf.append(ch)
    parts.append("".join(buf))
    return parts


# --- typed accessors -------------------------------------------------------------------------

def as_float(value, default=0.0):
    try:
        return float(str(value).strip())
    except (TypeError, ValueError):
        return default


def as_int(value, default=0):
    try:
        return int(str(value).strip())
    except (TypeError, ValueError):
        try:
            return int(float(str(value).strip()))
        except (TypeError, ValueError):
            return default


def vec(value, *keys):
    if not isinstance(value, dict):
        return tuple(0.0 for _ in keys)
    return tuple(as_float(value.get(k, 0)) for k in keys)


def ref(value):
    """The fileID out of a ``{fileID: n}`` / ``{fileID: n, guid: g, type: t}`` reference."""
    if not isinstance(value, dict):
        return 0
    return as_int(value.get("fileID", 0))


def ref_guid(value):
    if not isinstance(value, dict):
        return None
    guid = value.get("guid")
    return guid if isinstance(guid, str) and guid else None
