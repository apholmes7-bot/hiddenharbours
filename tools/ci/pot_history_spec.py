"""Nonshipping executable design for historical pot entrances.

Synthetic certified one-dimensional routes and exact rational Core game times.
This is not a terrain adapter, production solver, save serializer or catch path.
See docs/design/living-fish/10-historical-pot-encounters.md.
"""

from bisect import bisect_right
from dataclasses import dataclass, field
from fractions import Fraction
import heapq


class UnknownHistory(ValueError):
    """The complete candidate/support manifest is unavailable."""


class WorkLimit(ValueError):
    """The supplied manifest exceeds the explicit admission budget."""


def exact(value):
    # Floats, including NaN/infinity, have no implicit quantization in this spec.
    if type(value) not in (int, Fraction):
        raise ValueError("use integer or rational time/geometry")
    return Fraction(value)


@dataclass(frozen=True)
class Route:
    occupant_id: tuple
    region: str
    length: Fraction
    speed: Fraction
    phase_origin: Fraction
    home_rest: Fraction = Fraction(0)
    away_rest: Fraction = Fraction(0)
    sessile: bool = False
    support_certified: bool = False

    def __post_init__(self):
        for name in ("length", "speed", "phase_origin", "home_rest", "away_rest"):
            object.__setattr__(self, name, exact(getattr(self, name)))
        if (type(self.occupant_id) is not tuple or not self.occupant_id
                or not all(type(x) is str and x for x in self.occupant_id)
                or type(self.region) is not str or not self.region
                or self.length <= 0 or self.speed <= 0
                or self.home_rest < 0 or self.away_rest < 0):
            raise ValueError("invalid route")

    @property
    def period(self):
        return self.home_rest + 2 * self.length / self.speed + self.away_rest


@dataclass(frozen=True)
class Window:
    start: Fraction
    end: Fraction
    state: str  # open = all admission evidence known; closed = known rejection

    def __post_init__(self):
        object.__setattr__(self, "start", exact(self.start))
        object.__setattr__(self, "end", exact(self.end))
        if self.end <= self.start or self.state not in ("open", "closed", "unknown"):
            raise ValueError("invalid admission window")


@dataclass(frozen=True)
class Pot:
    pot_id: str
    region: str
    entrance: Fraction
    placed: Fraction
    removed: Fraction
    capacity: int
    windows: tuple
    _window_starts: tuple = field(init=False, repr=False, compare=False)

    def __post_init__(self):
        for name in ("entrance", "placed", "removed"):
            object.__setattr__(self, name, exact(getattr(self, name)))
        if (type(self.pot_id) is not str or not self.pot_id
                or type(self.region) is not str or not self.region or self.removed <= self.placed
                or type(self.capacity) is not int or self.capacity < 0
                or type(self.windows) is not tuple):
            raise ValueError("invalid pot")
        for index, window in enumerate(self.windows):
            if index and self.windows[index - 1].end > window.start:
                raise ValueError("unordered or overlapping history")
        object.__setattr__(self, "_window_starts", tuple(w.start for w in self.windows))

    def gate_at(self, time):
        index = bisect_right(self._window_starts, time) - 1
        if index >= 0 and time < self.windows[index].end:
            return self.windows[index]
        return None  # a history gap is UNKNOWN, never an open entrance


@dataclass(frozen=True, order=True)
class Entrance:
    time: Fraction
    pot_id: str
    occupant_id: tuple


@dataclass(frozen=True)
class Stream:
    route: Route
    pot: Pot
    base: Fraction

    def at_or_after(self, lower):
        # ceil((lower - base) / period), including negative diagnostic times.
        ratio = (lower - self.base) / self.route.period
        cycle = -((-ratio.numerator) // ratio.denominator)
        return self.base + cycle * self.route.period


@dataclass(frozen=True)
class Advance:
    status: str  # complete, pending, unknown
    examined: int
    committed_until: Fraction


class HistoricalPots:
    """Merge all competing streams for one immutable soak cohort.

    Clients may read catches only after COMPLETE. A PENDING/UNKNOWN result never
    publishes partially settled ownership. Release/haul commands need a frontier
    barrier and a new reviewed cohort; they cannot mutate this manifest in place.
    """

    def __init__(self, routes, pots, start, *, manifest_complete=False,
                 pair_limit, item_limit, initially_unavailable=()):
        self.start = self.committed_until = self.requested_until = exact(start)
        if (type(routes) not in (tuple, list) or type(pots) not in (tuple, list)
                or type(initially_unavailable) not in (tuple, list)):
            raise ValueError("bounded value manifests required, not lazy iterators")
        if (type(pair_limit) is not int or pair_limit < 0
                or type(item_limit) is not int or item_limit < 0):
            raise ValueError("invalid manifest limits")
        if len(routes) + len(pots) + len(initially_unavailable) > item_limit:
            raise WorkLimit("manifest item admission limit exceeded")
        if (len(routes) * len(pots) > pair_limit
                or sum(len(p.windows) for p in pots) + len(routes) + len(pots)
                + len(initially_unavailable) > item_limit):
            raise WorkLimit("manifest pair or history admission limit exceeded")
        self.routes = tuple(sorted(routes, key=lambda r: r.occupant_id))
        self.pots = tuple(sorted(pots, key=lambda p: p.pot_id))
        if not manifest_complete or any(not r.support_certified for r in self.routes):
            raise UnknownHistory("complete certified candidate manifest required")
        if len({r.occupant_id for r in self.routes}) != len(self.routes):
            raise ValueError("duplicate occupant identity")
        if len({p.pot_id for p in self.pots}) != len(self.pots):
            raise ValueError("duplicate pot identity")
        # A cohort starts before its participating placements. Restore later work
        # from its cursor; never construct a fresh solver at a later haul instant.
        if any(p.placed < self.start for p in self.pots):
            raise ValueError("missing earlier soak history")
        self._unavailable = set(initially_unavailable)
        if not self._unavailable.issubset({r.occupant_id for r in self.routes}):
            raise ValueError("unavailable id missing from manifest")
        self._counts = {p.pot_id: 0 for p in self.pots}
        self._accepted = []
        self._streams = []
        self._heap = []
        self._readable = True
        for pot in self.pots:
            for route in self.routes:
                if (route.sessile or route.region != pot.region
                        or not 0 < pot.entrance < route.length):
                    continue
                outward = route.phase_origin + route.home_rest + pot.entrance / route.speed
                inward = (route.phase_origin + route.home_rest + route.length / route.speed
                          + route.away_rest + (route.length - pot.entrance) / route.speed)
                for base in (outward, inward):
                    stream = Stream(route, pot, base)
                    self._streams.append(stream)
                    self._schedule(len(self._streams) - 1, max(self.start, pot.placed))

    def _schedule(self, index, lower):
        stream = self._streams[index]
        time = stream.at_or_after(lower)
        if time < stream.pot.removed:
            heapq.heappush(self._heap, (Entrance(time, stream.pot.pot_id,
                                               stream.route.occupant_id), index))

    def advance(self, until, budget):
        until = exact(until)
        if until < self.requested_until or type(budget) is not int or budget < 1:
            raise ValueError("monotone target and positive work budget required")
        self.requested_until = until
        self._readable = False
        examined = 0
        while self._heap and self._heap[0][0].time < until and examined < budget:
            event, index = self._heap[0]
            stream = self._streams[index]
            pot = stream.pot
            examined += 1
            if (event.occupant_id in self._unavailable
                    or self._counts[pot.pot_id] == pot.capacity):
                heapq.heappop(self._heap)  # permanently irrelevant in this cohort
                continue
            gate = pot.gate_at(event.time)
            if gate is None or gate.state == "unknown":
                return Advance("unknown", examined, self.committed_until)
            heapq.heappop(self._heap)
            if gate.state == "closed":
                # Every crossing before end is rejected by the SAME known rule.
                # Jump analytically; do not loop through every sleeping cycle.
                self._schedule(index, gate.end)
            else:
                self._accepted.append(event)
                self._unavailable.add(event.occupant_id)
                self._counts[pot.pot_id] += 1
        if self._heap and self._heap[0][0].time < until:
            return Advance("pending", examined, self.committed_until)
        self.committed_until = until
        self._readable = True
        return Advance("complete", examined, self.committed_until)

    def catches(self):
        if not self._readable:
            raise RuntimeError("unsettled historical frontier")
        return tuple(self._accepted)

    @property
    def pending_keys(self):
        """Exact next-event cursor, useful for checkpoint equivalence proof."""
        return tuple(sorted(self._heap))
