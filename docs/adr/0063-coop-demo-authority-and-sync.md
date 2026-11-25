# ADR 0063 — Co-op in the demo: the host decides what is counted, the sea is recomputed, up to four players

- **Status:** Proposed (owner rulings 2026-11-13 and 2026-11-25; awaiting the owner on the questions in
  [the plan](../design/coop-in-the-demo.md) section 8, and the lead-architect on the Core contracts)
- **Owner:** lead-architect
- **Touches:** Core (a session roster, a per-player record, an actor on counted events, a request
  contract); App (the one assembly that may name a networking package); the save (one later, versioned
  step); `Packages/manifest.json` (two packages, at the first code slice). **No code, package or save
  change in this ADR's PR.**
- **Relates to:** the unmerged co-op ADR draft carried by #1182 (numbered 0057 there, 0058 in #1243).
  If this ADR is accepted, that draft is superseded for the demo: this one keeps its host-owned world
  and adds the player count, the bed rule, the one-island scope and the request contract.

## Context

The owner ruled that the demo includes co-op, compared against Stardew Valley, with two bunks in every
home and a bunkhouse for players three and four. Earlier rulings stand: every player sleeps in a
separate bunk; sleep is the save; the demo is the first week on St Peters alone and keeps its own save
file; one shared purse (2026-08-04).

Main is built for one player. One static service locator holds the single body, hands, pail, helm and
needs. No event names who acted. The save holds one of each personal fact. The plan's appendix lists
the files and lines.

Main also has one large advantage. Tide, wind, weather, waves, fish schools and village routines are
pure functions of the world seed and the game time (rule 5). Two machines that agree on those agree on
the sea without sending it.

The trial in #1182 proves the transport and the shared clock, with each machine writing its own boat
and nothing counted being shared.

## Decision (proposed)

1. **One world, on the host.** The host's machine is the only place where counted state changes: time,
   catch, goods and who holds them, shelf stock, money, licences, story flags, sleep. It is the only
   machine that writes a save. A guest's machine writes no save file.

2. **The sea is never sent.** Each machine recomputes tide, weather, waves, schools and routines from
   the host's seed and clock. The host sends one atomic clock sample: seed, time, flow, rate, hold and
   pause. A guest whose seed, build or content fingerprint differs is refused before any action.

3. **Each player moves himself.** A player's own machine drives his body and the boat he is steering,
   and reports the pose. The host checks it for sense (speed, reach, the helm claim) and relays it.
   Boat physics is not expected to match bit for bit across machines, so there is no lockstep and no
   rollback. Other players' motion is smoothed for display.

4. **Everything counted is a request.** Take, put down, dig, land a fish, buy, sell, claim the helm,
   lie down, talk. A request carries the acting player, an order number and a retry key. The host
   checks it against the current state, applies it once, and publishes the result. A repeated request
   returns the first result. A stale or losing request changes nothing and says so.

5. **"The player" becomes "this player".** Core gains a session roster (who is present, which one is
   local, the cap) and a per-player record for body, hands, pail, helm claim, needs and look. The
   existing single-player reads keep working as "the local player" while features move over. Counted
   events gain the acting player, additively. No feature module names another's classes; the roster,
   the record and the request contract live in Core, and only App composes them with a network.

6. **Built for a list, not a pair.** The cap is a tunable in `GameConfig`, proposed at four. No code
   path may assume two. The demo opens with two and raises the cap when the bunkhouse exists.

7. **One island for the demo.** All players are in the St Peters scene. Storey and cutaway are
   per-player presentation. Region travel together, and the opening at sea for two, are outside this
   decision and need their own.

8. **Shared and personal.** Shared: purse, ordinary licences, the dory, shelf stock, containers and
   their contents, island story facts. Personal: body, needs, look, hands, Notebook notes and caught
   list, friendships, the place he sleeps and wakes.

9. **Beds follow the player count, and are not saved as buildings.** Every home has at least two
   player beds, checked by content validation. The bunkhouse is a prefab placed from data when the
   save knows three or four players. Its storage is saved by key like any other container.

10. **The save changes once, later, by its own decision.** Per-player records are added in one
    versioned step with a migration that loads every older save and maps today's personal fields to
    the first player. Until then a guest is not remembered between sessions. The step needs the
    owner's approval and a version number handed out at that time; this ADR reserves none.

11. **The transport sits behind one seam.** Typed address over UDP first. A Steam transport can be
    added later without touching the rules above. Networking packages are named only in the App
    composition assembly. Co-op is off unless a session is started; solo play keeps its path and tests.

12. **No host migration.** The host leaving ends the session. A crash returns everyone to the last
    completed sleep.

## Alternatives considered

- **Each machine decides its own catch and money (the trial's shape, extended).** Cheapest to start.
  Rejected: two machines would each pay for the same pail and each sell the last loaf.
- **Lockstep or rollback from identical simulation.** Rejected: boat physics is not guaranteed
  identical across machines, and the rod fight is deliberately not part of the seed-and-time contract.
- **A dedicated server.** Rejected for the demo: running costs, and a different product.
- **Two players only.** Cheaper by the bunkhouse and some testing. Rejected as a design limit because
  the owner asked about four and the difference is small if no code assumes two. Kept as the first
  shipping count.
- **A separate save on each player's machine, merged.** Rejected: merging goods and money has no
  correct answer and invites duplication.

## Consequences

- The per-player record is the largest Core change the project has taken. It is additive in shape but
  touches many callers, so it lands in steps, with the ownership guard from #1521 measuring progress.
- Solo play must be unchanged at every step. Each slice keeps the solo suite green.
- Determinism (rule 5) becomes load-bearing for play, not only for saves. Hidden randomness in a
  simulation system would show as two players seeing different seas.
- Performance (rule 7): up to four mesh characters and their gear, measured on the target laptop at
  the proving run. Network objects are pooled; nothing is allocated per frame; counted work stays on
  the slow tick.
- The demo's save gains per-player records in one later step. Old saves keep loading.
- Two packages are added at the first code slice, with owner and lead-architect approval.
- Not decided here: Steam accounts and App IDs, relay costs, region travel for a group, the opening at
  sea for two, more than four players, host migration.

## Proof expected before acceptance of each step

Named in the plan's build order. In short: two requests for one object give one winner; two purchases
with money for one give one debit; a repeated request does nothing twice; a guest's quit writes no
file; a wrong seed or build is refused; the night passes once, only with every player in a separate
bunk; an old save loads and round-trips; and one run on two real machines from start to the demo's end.
