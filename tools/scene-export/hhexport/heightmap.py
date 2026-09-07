"""The painted height map, read without Unity — and the ground contour that falls out of it.

``PaintedHeightMap`` is an R8 texture plus a world rect and an elevation range (ADR 0014: one map
serves render and sim together, so paint = sail). The texture is a Git LFS object, absent from a
pointer-only checkout — so everything here is **gated on the bytes actually being present**, and
the exporter says which of the two it produced.

⚠ **What this yields is an ISO-CONTOUR OF THE DECLARED BAND FLOORS, not the ground Unity paints.**
The engine's `ShoreMaterialAt` is more than a threshold ladder: it wiggles the elevation so the
rings meander, tests a sandbar segment with its own spine rule, and picks between two band tables
by weather sector. Reimplementing that here would be a second definition of the coastline — the
review's §7(c) trap, and the failure mode is a package whose shoreline looks approximately right
and disagrees with the sim. So this reads only what the repo *declares*: the floors, in order.
The contour is true to the height map and coarser than the paint, and it says so in the package.
"""

import struct
import zlib

# The declared band floors, deepest first. Read from the C# rather than transcribed; the ORDER is
# the ladder `ShoreMaterialAt` walks, and the names are its own enum's.
BAND_FLOORS = (
    ("grass", "GrassFloorElevation"),
    ("marram", "MarramFloorElevation"),
    ("sand", "SandFloorElevation"),
    ("ripple", "RippleFloorElevation"),
)
PAINT_FLOOR = "PaintFloorElevation"


class Png:
    """A decoded 8-bit greyscale PNG: ``width``, ``height``, ``rows`` of bytes."""

    __slots__ = ("width", "height", "rows")

    def __init__(self, width, height, rows):
        self.width = width
        self.height = height
        self.rows = rows

    def value(self, x, y):
        return self.rows[y][x]


def decode_r8(data):
    """Decode a greyscale-8 PNG. Returns ``None`` for anything else — including an LFS pointer.

    Deliberately narrow: the height maps are R8 by construction (``PaintedHeightMap`` writes
    them), so a file that is not greyscale-8 is not one of them and should be refused rather
    than coerced into a plausible-looking elevation field.
    """
    if not data.startswith(b"\x89PNG\r\n\x1a\n"):
        return None
    width = height = None
    bit_depth = colour_type = interlace = None
    idat = bytearray()
    offset = 8
    while offset + 8 <= len(data):
        length, kind = struct.unpack(">I4s", data[offset:offset + 8])
        body = data[offset + 8:offset + 8 + length]
        offset += 12 + length
        if kind == b"IHDR":
            width, height, bit_depth, colour_type, _c, _f, interlace = struct.unpack(
                ">IIBBBBB", body)
        elif kind == b"IDAT":
            idat += body
        elif kind == b"IEND":
            break
    if width is None or bit_depth != 8 or colour_type != 0 or interlace != 0:
        return None
    raw = zlib.decompress(bytes(idat))
    rows, previous, cursor = [], bytearray(width), 0
    for _ in range(height):
        filter_type = raw[cursor]
        cursor += 1
        line = bytearray(raw[cursor:cursor + width])
        cursor += width
        _unfilter(filter_type, line, previous)
        rows.append(bytes(line))
        previous = line
    return Png(width, height, rows)


def _unfilter(filter_type, line, previous):
    if filter_type == 0:
        return
    for i in range(len(line)):
        left = line[i - 1] if i else 0
        up = previous[i]
        if filter_type == 1:
            line[i] = (line[i] + left) & 0xFF
        elif filter_type == 2:
            line[i] = (line[i] + up) & 0xFF
        elif filter_type == 3:
            line[i] = (line[i] + ((left + up) >> 1)) & 0xFF
        elif filter_type == 4:
            upper_left = previous[i - 1] if i else 0
            line[i] = (line[i] + _paeth(left, up, upper_left)) & 0xFF
        else:
            raise ValueError(f"unknown PNG filter {filter_type}")


def _paeth(a, b, c):
    p = a + b - c
    pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
    if pa <= pb and pa <= pc:
        return a
    return b if pb <= pc else c


def read_bands(repo):
    """``(paintFloor, [(material, floor), …])`` from the shore map's declared constants."""
    from .csharp import CSharpSource
    for candidate in repo._find_by_name("StPetersShoreMap.cs"):
        source = CSharpSource(repo, candidate)
        floor = source.number(PAINT_FLOOR)
        bands = [(name, source.number(const)) for name, const in BAND_FLOORS]
        if floor is not None and all(value is not None for _n, value in bands):
            return floor, bands
    return None, []


def contour(repo, height_map, cols, rows, origin_nw):
    """A ground grid of band names, or ``None`` when the texture's bytes are not on disk.

    Sampled per cell centre by nearest texel — the map is 2 texels per metre against a 1 m cell,
    so nearest is the honest reading; interpolating would invent a smoothness the R8 source does
    not have.
    """
    texture = height_map.get("texture") if height_map else None
    if not texture or not repo.exists(texture):
        return None, "the height texture is not on disk"
    with open(repo.abs(texture), "rb") as handle:
        data = handle.read()
    if data.startswith(b"version https://git-lfs"):
        return None, "the height texture is a Git LFS pointer — its bytes are not in this checkout"
    png = decode_r8(data)
    if png is None:
        return None, "the height texture did not decode as an 8-bit greyscale PNG"

    paint_floor, bands = read_bands(repo)
    if paint_floor is None:
        return None, "the shore map's band floors could not be read"

    low = height_map["minElevation"]
    span = height_map["maxElevation"] - low
    width_m, height_m = height_map["worldSizeMeters"]
    centre_x, centre_y = height_map["worldCenter"]
    left, top = centre_x - width_m / 2.0, centre_y + height_m / 2.0

    grid = [None] * (cols * rows)
    for row in range(rows):
        world_y = origin_nw[1] - (row + 0.5)
        # The texture's v axis runs south-to-north with the world, so the top row of the image is
        # the NORTH edge only if it was written that way; PaintedHeightMap writes v = 0 at the
        # world's minimum y, so the image row is measured up from the south.
        ty = int((world_y - (top - height_m)) / height_m * png.height)
        if not 0 <= ty < png.height:
            continue
        line = png.rows[png.height - 1 - ty]
        base = row * cols
        for col in range(cols):
            world_x = origin_nw[0] + (col + 0.5)
            tx = int((world_x - left) / width_m * png.width)
            if not 0 <= tx < png.width:
                continue
            elevation = low + (line[tx] / 255.0) * span
            if elevation < paint_floor:
                continue
            for name, floor in bands:
                if elevation >= floor:
                    grid[base + col] = name
                    break
    return grid, None


def sample_field(repo, height_map, cols, rows, origin_nw, stride_m):
    """A DOWNSAMPLED elevation grid in metres, or ``None`` when the texture's bytes are absent.

    The editor cannot read the referenced height file from its sandbox but needs elevation to
    shade tide and shore, so the package carries a coarse copy inline. This is deliberately a
    wash and not a survey: ``stride_m`` metres between samples, nearest texel, no interpolation
    and no smoothing. The full-resolution map stays in the referenced file, pinned by its hash.

    Values are metres on the SAME datum as ``terrain.waterLevelMeters``, so a shader can compare
    the two directly. Row 0 is the north edge, matching the terrain grid.
    """
    texture = height_map.get("texture") if height_map else None
    if not texture or not repo.exists(texture):
        return None, "the height texture is not on disk"
    with open(repo.abs(texture), "rb") as handle:
        data = handle.read()
    if data.startswith(b"version https://git-lfs"):
        return None, "the height texture is a Git LFS pointer — its bytes are not in this checkout"
    png = decode_r8(data)
    if png is None:
        return None, "the height texture did not decode as an 8-bit greyscale PNG"

    low = height_map["minElevation"]
    span = height_map["maxElevation"] - low
    width_m, height_m = height_map["worldSizeMeters"]
    centre_x, centre_y = height_map["worldCenter"]
    left, top = centre_x - width_m / 2.0, centre_y + height_m / 2.0

    # Ceil, so the last partial stride still gets a sample and the field spans the whole region.
    field_cols = (cols + stride_m - 1) // stride_m
    field_rows = (rows + stride_m - 1) // stride_m
    values = []
    for row in range(field_rows):
        world_y = origin_nw[1] - min(row * stride_m + stride_m / 2.0, rows - 0.5)
        ty = int((world_y - (top - height_m)) / height_m * png.height)
        line = png.rows[png.height - 1 - ty] if 0 <= ty < png.height else None
        for col in range(field_cols):
            world_x = origin_nw[0] + min(col * stride_m + stride_m / 2.0, cols - 0.5)
            tx = int((world_x - left) / width_m * png.width)
            if line is None or not 0 <= tx < png.width:
                values.append(None)     # outside the painted map: absent, not zero
                continue
            values.append(round(low + (line[tx] / 255.0) * span, 3))
    field = {
        "strideMeters": stride_m,
        "cols": field_cols,
        "rows": field_rows,
        "originNW": [round(origin_nw[0], 3), round(origin_nw[1], 3)],
        "elevationRange": [low, height_map["maxElevation"]],
        "units": "metres relative to chart datum — the same datum as terrain.waterLevelMeters",
        "order": "row-major, row 0 = north edge, one sample per stride cell centre",
        "x-note": "a shading wash, not a survey: nearest texel at a coarse stride, no "
                  "interpolation. null means the sample fell outside the painted map. The "
                  "full-resolution source is terrain.x-heightMap, pinned by textureSha256.",
        "values": values,
    }
    return field, None


class _Unset:
    """A first-run sentinel that is not ``None`` — because ``None`` is a real value here (a sample
    off the painted map) and starting the run state at it would merge the first absent cell into a
    run that had not begun."""
    __slots__ = ()


_UNSET = _Unset()


def sample_field_full(repo, height_map, cols, rows, origin_nw):
    """The elevation grid at **one sample per terrain cell**, run-length encoded.

    The sibling of :func:`sample_field`, and it exists because that one is an 8 m wash: a reader that
    thresholds it at spring high draws a shoreline in 8 m steps, which is not the shoreline the game
    shows. This is the same read at the grid the rest of the package already speaks — ``cellMeters``
    1, ``cols`` x ``rows``, row 0 = north — so ``x-heightFieldFull`` and every layer's ``rle`` index
    the same cells, and the owner's tide slider can cut water against the ground cell for cell.

    ⚠ **"Full" is the TERRAIN's resolution, not the texture's.** Both shipped height maps are
    **2 texels per metre** (1520x1120 over 760x560 m; 1520x1040 over 760x520), so a 1 m stride takes
    one texel in four. That is the honest stride for a field whose neighbours are 1 m cells, and the
    full-resolution source stays in the referenced file, pinned by ``textureSha256``.

    Values are metres above chart datum — directly comparable to ``terrain.waterLevelMeters``, with
    no decode step, which is the whole point of a field a reader thresholds at a sea level. They
    carry the map's own 8-bit quantisation and nothing finer: 255 steps across ``elevationRange``,
    so the quantum is stated and equal values run together. ``null`` is a sample outside the painted
    map — absent, not zero.
    """
    texture = height_map.get("texture") if height_map else None
    if not texture or not repo.exists(texture):
        return None, "the height texture is not on disk"
    with open(repo.abs(texture), "rb") as handle:
        data = handle.read()
    if data.startswith(b"version https://git-lfs"):
        return None, "the height texture is a Git LFS pointer — its bytes are not in this checkout"
    png = decode_r8(data)
    if png is None:
        return None, "the height texture did not decode as an 8-bit greyscale PNG"

    low = height_map["minElevation"]
    span = height_map["maxElevation"] - low
    width_m, height_m = height_map["worldSizeMeters"]
    centre_x, centre_y = height_map["worldCenter"]
    left, top = centre_x - width_m / 2.0, centre_y + height_m / 2.0

    # One decoded metre-value per 8-bit code, computed once. The elevation is a function of the code
    # alone, so a per-cell round() over 425,600 cells would be 425,600 identical divisions and 256
    # distinct answers — and rounding ONCE is also what keeps two equal codes encoding to one run.
    table = [round(low + code / 255.0 * span, 3) for code in range(256)]

    runs, current, length = [], _UNSET, 0
    for row in range(rows):
        world_y = origin_nw[1] - (row + 0.5)
        ty = int((world_y - (top - height_m)) / height_m * png.height)
        line = png.rows[png.height - 1 - ty] if 0 <= ty < png.height else None
        for col in range(cols):
            if line is None:
                value = None
            else:
                tx = int((origin_nw[0] + col + 0.5 - left) / width_m * png.width)
                value = table[line[tx]] if 0 <= tx < png.width else None
            # `None == None` is True (two absent samples ARE one run) and `x == _UNSET` is False
            # for every x including None, so the first cell always opens a run.
            if value == current:
                length += 1
            else:
                if length:
                    runs.append([current, length])
                current, length = value, 1
    if length:
        runs.append([current, length])

    quantum = span / 255.0
    field = {
        "strideMeters": 1,
        "cols": cols,
        "rows": rows,
        "originNW": [round(origin_nw[0], 3), round(origin_nw[1], 3)],
        "elevationRange": [low, height_map["maxElevation"]],
        "units": "metres relative to chart datum — the same datum as terrain.waterLevelMeters",
        "order": "row-major, row 0 = north edge, one sample per 1 m terrain cell centre — the SAME "
                 "cols x rows grid every terrain layer's rle covers",
        "encoding": "run-length: [[metres, count], …]; sum(count) == cols * rows exactly. null is a "
                    "sample outside the painted map.",
        "quantumMeters": round(quantum, 6),
        "x-note": "the tide field. Threshold it at seaLevel (see x-tideRules) and what is below is "
                  "water. Nearest texel, no interpolation and no smoothing, carrying the map's own "
                  "8-bit quantisation — quantumMeters is that step, and values are rounded to 3 dp "
                  "which is finer than it. The map is 2 texels/m, so this 1 m stride takes one texel "
                  "in four; the full-resolution source is terrain.x-heightMap, pinned by "
                  "textureSha256.",
        "values": runs,
    }
    return field, None



class Sampler:
    """Point reads of the painted height map, for the few facts that want ONE elevation.

    The grid fields answer "what is the ground everywhere"; this answers "what is the ground under
    this hull" and "does the map agree with the deck constant this course stands on". Same read as
    theirs — nearest texel, no interpolation — so a sampled bed and the field a reader thresholds
    can never disagree about the same square metre.

    ``ready`` is False when the texture's bytes are absent (a pointer-only checkout), and every
    caller then ships ``null`` with a reason rather than a zero: an elevation of 0 is a real
    elevation, and a hull handed one at chart datum would be reported aground at every tide.
    """

    __slots__ = ("_png", "_low", "_span", "_left", "_bottom", "_width_m", "_height_m", "reason")

    def __init__(self, repo, height_map):
        self._png = None
        self.reason = None
        texture = height_map.get("texture") if height_map else None
        if not texture or not repo.exists(texture):
            self.reason = "the height texture is not on disk"
            return
        with open(repo.abs(texture), "rb") as handle:
            data = handle.read()
        if data.startswith(b"version https://git-lfs"):
            self.reason = ("the height texture is a Git LFS pointer — its bytes are not in this "
                           "checkout")
            return
        png = decode_r8(data)
        if png is None:
            self.reason = "the height texture did not decode as an 8-bit greyscale PNG"
            return
        self._png = png
        self._low = height_map["minElevation"]
        self._span = height_map["maxElevation"] - self._low
        self._width_m, self._height_m = height_map["worldSizeMeters"]
        centre_x, centre_y = height_map["worldCenter"]
        self._left = centre_x - self._width_m / 2.0
        self._bottom = centre_y - self._height_m / 2.0

    @property
    def ready(self):
        return self._png is not None

    @property
    def quantum(self):
        """The map's own elevation step — ``elevationRange`` over 255. Every sampled elevation is a
        multiple of it, so a comparison against a declared constant is only ever meaningful to
        within one of these."""
        return None if self._png is None else self._span / 255.0

    def at(self, x, y):
        """Metres above chart datum at a world point, or ``None`` outside the painted map."""
        if self._png is None:
            return None
        tx = int((x - self._left) / self._width_m * self._png.width)
        ty = int((y - self._bottom) / self._height_m * self._png.height)
        if not (0 <= tx < self._png.width and 0 <= ty < self._png.height):
            return None
        code = self._png.rows[self._png.height - 1 - ty][tx]
        return round(self._low + code / 255.0 * self._span, 3)
