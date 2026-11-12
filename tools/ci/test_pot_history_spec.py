"""Executable design proof; runs in the existing Python CI gate without Unity."""

import copy
from fractions import Fraction as F
import itertools
import random
import unittest

from pot_history_spec import (Entrance, HistoricalPots, Pot, Route, UnknownHistory,
                              Window, WorkLimit)


def route(name="a", **kwargs):
    values = dict(occupant_id=("world", "epoch", "fish.lobster", name), region="bay",
                  length=F(4), speed=F(1), phase_origin=F(0), support_certified=True)
    values.update(kwargs)
    return Route(**values)


def pot(name="pot.a", **kwargs):
    values = dict(pot_id=name, region="bay", entrance=F(1), placed=F(0),
                  removed=F(100), capacity=3, windows=(Window(0, 100, "open"),))
    values.update(kwargs)
    return Pot(**values)


def solver(routes, pots, start=0, **kwargs):
    return HistoricalPots(routes, pots, start, manifest_complete=True,
                          pair_limit=len(routes) * len(pots),
                          item_limit=(len(routes) + len(pots) + sum(len(p.windows) for p in pots)
                                      + len(kwargs.get("initially_unavailable", ()))), **kwargs)


def drain(sim, until, budget=3):
    calls = examined = 0
    while True:
        result = sim.advance(until, budget)
        calls += 1
        examined += result.examined
        if result.status == "complete":
            return calls, examined
        if result.status != "pending":
            raise AssertionError(result)


def oracle_position(r, time):
    """Independent point sampler, not an entrance-stream generator."""
    t = (time - r.phase_origin) % r.period
    if t < r.home_rest:
        return F(0)
    t -= r.home_rest
    travel = r.length / r.speed
    if t < travel:
        return t * r.speed
    t -= travel
    if t < r.away_rest:
        return r.length
    return r.length - (t - r.away_rest) * r.speed


def exhaustive_oracle(routes, pots, start, end, unavailable=()):
    """Fixture crossings lie on the half-second lattice, strictly inside routes.

    Enumerate point equality at every lattice instant, then globally arbitrate.
    No production Stream/base/ceil method is used by this oracle.
    """
    events = []
    for half_tick in range(int(2 * start), int(2 * end)):
        time = F(half_tick, 2)
        for p in pots:
            for r in routes:
                if (not r.sessile and r.region == p.region and p.placed <= time < p.removed
                        and 0 < p.entrance < r.length and oracle_position(r, time) == p.entrance):
                    window = next((w for w in p.windows if w.start <= time < w.end), None)
                    if window is None or window.state == "unknown":
                        raise AssertionError("oracle fixture has unknown history")
                    if window.state == "open":
                        events.append(Entrance(time, p.pot_id, r.occupant_id))
    owned = set(unavailable)
    counts = {p.pot_id: 0 for p in pots}
    capacities = {p.pot_id: p.capacity for p in pots}
    caught = []
    for event in sorted(events):
        if event.occupant_id not in owned and counts[event.pot_id] < capacities[event.pot_id]:
            caught.append(event)
            owned.add(event.occupant_id)
            counts[event.pot_id] += 1
    return tuple(caught)


class PotHistorySpecTests(unittest.TestCase):
    def test_crossing_between_ticks_is_found_at_exact_time(self):
        r, p = route(), pot(entrance=F(3, 2))
        self.assertNotEqual(oracle_position(r, 0), p.entrance)
        self.assertNotEqual(oracle_position(r, 4), p.entrance)
        sim = solver([r], [p])
        drain(sim, 4)
        self.assertEqual(sim.catches(), (Entrance(F(3, 2), p.pot_id, r.occupant_id),))

    def test_inbound_rest_and_history_boundaries(self):
        r = route(home_rest=F(2), away_rest=F(3))
        p = pot(windows=(Window(0, 12, "closed"), Window(12, 100, "open")))
        sim = solver([r], [p])
        drain(sim, 20)
        self.assertEqual(sim.catches(), (Entrance(F(12), p.pot_id, r.occupant_id),))

    def test_half_open_queries_and_pot_lifetime_do_not_duplicate(self):
        r = route()
        sim = solver([r], [pot()])
        drain(sim, 1)
        self.assertEqual(sim.catches(), ())
        drain(sim, 2)
        expected = (Entrance(F(1), "pot.a", r.occupant_id),)
        self.assertEqual(sim.catches(), expected)
        drain(sim, 2)
        self.assertEqual(sim.catches(), expected)
        stopped = solver([r], [pot(removed=F(1))])
        drain(stopped, 2)
        self.assertEqual(stopped.catches(), ())
        placed = solver([r], [pot(placed=F(1))])
        drain(placed, 2)
        self.assertEqual(placed.catches(), expected)

    def test_competing_pots_and_animals_use_full_global_order(self):
        a, b = route("a"), route("b")
        pots = [pot("pot.b", capacity=1), pot("pot.a", capacity=1)]
        expected = (Entrance(F(1), "pot.a", a.occupant_id),
                    Entrance(F(1), "pot.b", b.occupant_id))
        for rs in itertools.permutations([a, b]):
            for ps in itertools.permutations(pots):
                sim = solver(rs, ps)
                drain(sim, 20, budget=1)
                self.assertEqual(sim.catches(), expected)

    def test_unknown_first_candidate_blocks_later_competing_pot(self):
        sim = solver([route()], [pot("pot.a", windows=()), pot("pot.b")])
        before = sim.pending_keys
        result = sim.advance(20, 4)
        self.assertEqual((result.status, result.examined, result.committed_until),
                         ("unknown", 1, F(0)))
        self.assertEqual(sim.pending_keys, before)
        with self.assertRaisesRegex(RuntimeError, "unsettled"):
            sim.catches()

    def test_unknown_window_and_history_gap_are_not_empty_or_open(self):
        for windows in [(Window(0, 100, "unknown"),), (Window(2, 100, "open"),)]:
            sim = solver([route()], [pot(windows=windows)])
            self.assertEqual(sim.advance(2, 4).status, "unknown")

    def test_budget_does_not_publish_part_of_a_tie_group(self):
        sim = solver([route("a"), route("b")], [pot()])
        result = sim.advance(2, 1)
        self.assertEqual((result.status, result.examined, result.committed_until),
                         ("pending", 1, F(0)))
        with self.assertRaises(RuntimeError):
            sim.catches()
        result = sim.advance(2, 1)
        self.assertEqual(result.status, "complete")
        self.assertEqual(len(sim.catches()), 2)

    def test_checkpoint_resume_preserves_exact_cursor_and_receipts(self):
        sim = solver([route("a"), route("b")], [pot()])
        self.assertEqual(sim.advance(20, 1).status, "pending")
        resumed = copy.deepcopy(sim)  # structural cursor proof, not a save-format claim
        self.assertEqual(resumed.pending_keys, sim.pending_keys)
        drain(sim, 20, 1)
        drain(resumed, 20, 7)
        self.assertEqual(resumed.catches(), sim.catches())
        self.assertEqual(resumed.pending_keys, sim.pending_keys)

    def test_huge_closed_soak_skips_cycles_with_fixed_work(self):
        end = 10 ** 30
        sim = solver([route()], [pot(removed=F(end + 100),
                                     windows=(Window(0, end, "closed"),
                                              Window(end, end + 100, "open")))])
        calls, examined = drain(sim, end + 10, 1)
        self.assertEqual((calls, examined), (4, 4))
        self.assertEqual(sim.catches(), (Entrance(F(end + 1), "pot.a", route().occupant_id),))

    def test_negative_and_long_game_times_are_exact(self):
        for start in [-100, 10 ** 30]:
            p = pot(placed=F(start), removed=F(start + 100),
                    windows=(Window(start, start + 100, "open"),))
            sim = solver([route()], [p], start)
            drain(sim, start + 8)
            expected_time = start + 3 if start < 0 else start + 1
            self.assertEqual(sim.catches(), (Entrance(F(expected_time), p.pot_id,
                                                       route().occupant_id),))

    def test_known_empty_sessile_and_other_region_never_fill_pot(self):
        for routes in [[], [route(sessile=True)], [route(region="elsewhere")]]:
            sim = solver(routes, [pot()])
            self.assertEqual(drain(sim, 100), (1, 0))
            self.assertEqual(sim.catches(), ())

    def test_external_reservation_and_zero_capacity_conserve_tokens(self):
        a, b = route("a"), route("b")
        sim = solver([a, b], [pot()], initially_unavailable=[a.occupant_id])
        drain(sim, 100)
        self.assertEqual(sim.catches(), (Entrance(F(1), "pot.a", b.occupant_id),))
        zero = solver([a], [pot(capacity=0)])
        drain(zero, 100)
        self.assertEqual(zero.catches(), ())

    def test_manifest_limits_and_missing_support_are_explicit(self):
        with self.assertRaises(WorkLimit):
            HistoricalPots([route()], [pot()], 0, manifest_complete=True,
                           pair_limit=0, item_limit=3)
        with self.assertRaises(WorkLimit):
            HistoricalPots([route()], [pot()], 0, manifest_complete=True,
                           pair_limit=1, item_limit=2)
        with self.assertRaises(WorkLimit):
            HistoricalPots([route()], [], 0, manifest_complete=True,
                           pair_limit=0, item_limit=0)
        for complete, r in [(False, route()), (True, route(support_certified=False))]:
            with self.assertRaises(UnknownHistory):
                HistoricalPots([r], [pot()], 0, manifest_complete=complete,
                               pair_limit=1, item_limit=3)

    def test_malformed_and_missing_earlier_history_are_rejected(self):
        for kwargs in [dict(length=F(0)), dict(speed=F(0)), dict(home_rest=F(-1)),
                       dict(phase_origin=float("nan")), dict(length=1.5)]:
            with self.assertRaises(ValueError):
                route(**kwargs)
        with self.assertRaises(ValueError):
            pot(windows=(Window(0, 3, "open"), Window(2, 5, "closed")))
        with self.assertRaises(ValueError):
            solver([route()], [pot()], start=5)
        with self.assertRaises(ValueError):
            solver([route(), route()], [pot()])
        with self.assertRaises(ValueError):
            solver([route()], [pot(), pot()])
        sim = solver([route()], [pot()])
        sim.advance(2, 1)
        with self.assertRaises(ValueError):
            sim.advance(1, 1)
        with self.assertRaises(ValueError):
            sim.advance(3, 0)

    def test_1000_seed_oracle_tick_sleep_unload_and_budget_equivalence(self):
        for seed in range(1000):
            # Local fixture RNG only: the solver itself has no random source.
            rng = random.Random(seed)
            routes = [route(str(i), length=F(rng.randrange(3, 8)),
                            phase_origin=F(rng.randrange(-9, 10)),
                            home_rest=F(rng.randrange(3)), away_rest=F(rng.randrange(3)))
                      for i in range(4)]
            pots = [pot("pot." + str(i), entrance=F(2 * i + 1, 2),
                        placed=F(rng.randrange(3)), removed=F(40),
                        capacity=rng.randrange(1, 4),
                        windows=(Window(0, 3 + i, "closed"),
                                 Window(3 + i, 40, "open"))) for i in range(3)]
            expected = exhaustive_oracle(routes, pots, 0, 40)
            slept = solver(routes, pots)
            drain(slept, 40, 100)
            ticked = solver(list(reversed(routes)), list(reversed(pots)))
            for time in range(1, 41):
                drain(ticked, time, 1 + seed % 4)
            # An unloaded region retains value data; no scene or loaded flag is consulted.
            unloaded = solver(copy.deepcopy(routes), copy.deepcopy(pots))
            drain(unloaded, 40, 1)
            with self.subTest(seed=seed):
                self.assertEqual(slept.catches(), expected)
                self.assertEqual(ticked.catches(), expected)
                self.assertEqual(unloaded.catches(), expected)
                self.assertEqual(len({e.occupant_id for e in expected}), len(expected))
                self.assertEqual(ticked.pending_keys, slept.pending_keys)


if __name__ == "__main__":
    unittest.main()
