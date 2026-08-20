"""The road layer, rasterised from the region's own declared routes.

The committed scenes carry no road tilemap — the builder paints roads at build time — but the
routes themselves are declared data: ``NineMileCreekMainland`` holds each way's vertices, and
``NineMileCreekRoads`` declares each way's surface, half-width and rank. Stroking those into
one-metre cells is a faithful *surface-material* raster, which is exactly what the format's road
RLE carries (the review: "the export does not ship a mask index for roads — the RLE carries only
the surface material").

**What this is not.** It is not what Unity paints. The engine sends the same centrelines through
``RoadKitContract``'s blob-47 lookup into two tilemaps, and the review is explicit that the
mask→index law is never re-derived. Nothing here derives one; the cells carry material only.

**What it leaves out, by name.** Ways whose route is *computed* rather than declared — the park
spur solves for the nearest point on any road; the town walks solve from each lot to the nearest
carriageway. Reproducing that solver here would be a second definition of where the roads go.
They are reported as omitted instead.
"""

import math
import re

from .csharp import CSharpSource

_WAY_CALL = re.compile(
    r"new\s+Way\(\s*([A-Za-z0-9_]+)\s*,\s*([A-Za-z0-9_]+)\s*,\s*"
    r"([A-Za-z0-9_.]+(?:\(\))?)\s*,\s*([A-Za-z0-9_]+)\s*,\s*([A-Za-z0-9_]+)", re.S)


class Way:
    __slots__ = ("name", "surface", "route", "half_width", "rank", "source")

    def __init__(self, name, surface, route, half_width, rank, source):
        self.name = name
        self.surface = surface
        self.route = route
        self.half_width = half_width
        self.rank = rank
        self.source = source


def read_ways(repo, roads_rel, geometry_rel):
    """``(ways, omitted)`` — the declared ways of a region, and the computed ones it skips."""
    roads = CSharpSource(repo, roads_rel)
    geometry = CSharpSource(repo, geometry_rel)
    if not roads.present:
        return [], []

    ways, omitted = [], []
    parsed = 0
    for name_const, surface_const, route_expr, width_const, rank_const in _WAY_CALL.findall(roads.text):
        parsed += 1
        name = roads.string(name_const) or name_const
        surface = roads.string(surface_const) or surface_const
        half_width = roads.number(width_const)
        rank = roads.number(rank_const)
        route = None
        if "." in route_expr and not route_expr.endswith("()"):
            route = geometry.vector2_array(route_expr.split(".")[-1])
        elif not route_expr.endswith("()"):
            route = roads.vector2_array(route_expr) or geometry.vector2_array(route_expr)
        if route and half_width is not None and rank is not None:
            ways.append(Way(name, surface, route, half_width, int(rank),
                            geometry.rel if "." in route_expr else roads.rel))
        else:
            omitted.append({
                "name": name,
                "route": route_expr,
                "why": "computed, not declared — its vertices are solved for at build time, and "
                       "solving for them here would be a second definition of where it runs",
            })
    # Ways built inside a loop never reach the regex at all (the town walks solve a doorstep and
    # a join point per lot). Counting the `new Way(` sites is how they get reported rather than
    # silently missed — a silent omission reads as "the region has no walks".
    total = roads.text.count("new Way(")
    if total > parsed:
        omitted.append({
            "name": f"{total - parsed} way(s) constructed in a loop",
            "route": "(inline)",
            "why": "built per-lot at run time from a nearest-point solve; not declared anywhere "
                   "this reader can follow",
        })
    return ways, omitted


def rasterise(ways, cols, rows, origin_nw):
    """Paint the ways into a ``cols x rows`` grid of surface names, lowest rank first.

    Row 0 is the north edge, as the format requires. A cell belongs to a way when its **centre**
    lies within the way's half-width of the centreline — the same "is this metre road" question
    the builder asks, without any of the tiling that follows it.
    """
    grid = [None] * (cols * rows)
    for way in sorted(ways, key=lambda w: w.rank):
        _stroke(grid, way, cols, rows, origin_nw)
    return grid


def _stroke(grid, way, cols, rows, origin_nw):
    reach = way.half_width
    for index in range(len(way.route) - 1):
        (ax, ay), (bx, by) = way.route[index], way.route[index + 1]
        # Only the cells inside this segment's own bounding box can be within reach of it —
        # testing all 425,600 against every segment would be minutes of arithmetic for nothing.
        min_col = max(0, int(math.floor(min(ax, bx) - origin_nw[0] - reach)))
        max_col = min(cols - 1, int(math.ceil(max(ax, bx) - origin_nw[0] + reach)))
        min_row = max(0, int(math.floor(origin_nw[1] - max(ay, by) - reach)))
        max_row = min(rows - 1, int(math.ceil(origin_nw[1] - min(ay, by) + reach)))
        for row in range(min_row, max_row + 1):
            world_y = origin_nw[1] - (row + 0.5)
            base = row * cols
            for col in range(min_col, max_col + 1):
                world_x = origin_nw[0] + (col + 0.5)
                if _distance_to_segment(world_x, world_y, ax, ay, bx, by) <= reach:
                    grid[base + col] = way.surface


def _distance_to_segment(px, py, ax, ay, bx, by):
    dx, dy = bx - ax, by - ay
    length = dx * dx + dy * dy
    if length <= 0.0:
        return math.hypot(px - ax, py - ay)
    t = ((px - ax) * dx + (py - ay) * dy) / length
    t = 0.0 if t < 0.0 else (1.0 if t > 1.0 else t)
    return math.hypot(px - (ax + t * dx), py - (ay + t * dy))
