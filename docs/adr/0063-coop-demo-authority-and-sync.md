# ADR 0063 — Co-op in the demo: the host decides what is counted, the sea is recomputed, players may be apart

- **Status:** Proposed (owner rulings 2026-11-13 and 2026-11-25, including his answers of 2026-11-25 to
  the questions in [the plan](../design/coop-in-the-demo.md) section 8; awaiting the lead-architect on
  the Core contracts, and the owner's separate yes for the save step and for the two packages)
- **Owner:** lead-architect
- **Touches:** Core (a session roster, a per-player record that includes the player's region, an actor
  on counted events, a request contract); App (the one place that may name a networking package);
  World and Environment (answering for a named region instead of the one current region); the save
  (one later, versioned step); `Packages/manifest.json` (two packages, at the first code slice).
  **No code, package or save change in this ADR's PR.**
- **Relates to:** the unmerged co-op ADR draft carried by #1182 (numbered 0057 there, 0058 in #1243).
  If this ADR is accepted, that draft is superseded: this one keeps its host-owned world and replaces
  its "one region at a time" with players who may be apart.

## Context

The owner ruled that the demo includes co-op, compared against Stardew Valley. On 2026-11-25 he
answered the open questions: one shared purse; shared licences; one shared boat where one rows or
steers and anyone else aboard may fish; join at any time wherever the host is, with two scenes live at
once and the joiner appearing at his last saved bed (a lost boat bed falls back to his last land bed);
a leaving guest's character and held things vanish and return with him; progress kept on the host's
computer only; two players at first; the bunkhouse stays once it exists; only the host can pause; the
host may play alone; any purchase may be made by one player alone; no Steam for now, a typed address.

Earlier rulings stand: every player sleeps in a separate bunk; sleep is the save; the demo is the first
week on St Peters and keeps its own save file.

Main is built for one player. One static service locator holds the single body, hands, pail, helm,
needs and current region. No event names who acted. The save holds one of each personal fact. The
plan's appendix lists the files and lines.

Main also has one large advantage. Tide, wind, weather, waves, fish schools and village routines are
pure functions of the world seed and the game time (rule 5). Two machines that agree on those agree on
the sea without sending it, and a second live region costs no extra traffic for its sea.

## Decision (proposed)

1. **One world, on the host.** The host's machine is the only place where counted state changes: time,
   catch, goods and who holds them, shelf stock, money, licences, story flags, sleep. It is the only
   machine that writes a save. A guest's machine writes no save file.

2. **The sea is never sent.** Each machine recomputes tide, weather, waves, schools and routines from
   the host's seed and clock. The host sends one atomic clock sample: seed, time, flow, rate, hold and
   pause. Only the host may pause. A guest whose seed, build or content fingerprint differs is refused.

3. **Each player moves himself.** A player's own machine drives his body and the boat he rows or
   steers, and reports the pose. The host checks it for sense and relays it. Boat physics is not
   expected to match bit for bit across machines, so there is no lockstep and no rollback.

4. **Everything counted is a request.** Take, put down, dig, land a fish, buy, sell, claim the oars or
   helm, lie down, talk. A request carries the acting player, an order number and a retry key. The
   host applies it once and publishes the result. A repeated request returns the first result. A stale
   or losing request changes nothing and says so. There is no approval step between players: either
   may make any purchase alone.

5. **"The player" becomes "this player".** Core gains a session roster (who is present, which one is
   local, the cap) and a per-player record: body, hands, pail, oars or helm claim, needs, look, and the
   region he is in. The existing single-player reads keep working as "the local player" while features
   move over. Counted events gain the acting player, additively. All session state is keyed by player
   id or connection id; no code path may assume two.

6. **A list, capped by a tunable.** Two at first. Raised to four when the bunkhouse exists.

7. **Players may be apart: up to two regions live at once.** The host loads and simulates every region
   a player is in. A region nobody on that machine is looking at is not drawn and is silent, and only
   its counted parts tick, on the slow tick. A guest's machine loads only his own region. "Current
   region", the arrival key and whereabouts become per-player. Environment and routine queries take a
   region, not the global one. The first target is two live regions; more is not promised here.

8. **One boat, shared.** One player holds the oars or helm. Anyone else aboard may fish. When nobody
   holds them, everyone aboard may fish. The same rule on every hull. Whether fishing is offered at
   speed is a tunable decided by test.

9. **Shared and personal.** Shared: purse, licences, the boat, shelf stock, containers and their
   contents, island story facts. Personal: body, needs, look, hands and what they hold, Notebook notes
   and caught list, friendships, his last bed on land and his last bed on a boat.

10. **Joining and leaving.** A player may join at any time, wherever the host is. He appears at his
    last saved bed. If that was a boat bed and the boat no longer exists, he appears at his last land
    bed. If the region of his bed is not loaded, the host loads it first. When he leaves, his character
    and everything in his hands are removed from the world and kept in his record, and return with him
    unchanged. The host may play alone.

11. **Beds follow the player count, and are not saved as buildings.** Every home has at least two
    player beds, checked by content validation. The bunkhouse is a prefab placed from data once the
    save has known a third player, and it then stays for that save. Its storage is saved by key.

12. **The save changes once, later, by its own decision.** Per-player records (item 9's personal list,
    including held things and the two beds) are added in one versioned step with a migration that
    loads every older save and maps today's personal fields to the first player. Until then a guest is
    not remembered between sessions. The step needs the owner's separate approval and a version number
    handed out at that time; this ADR reserves none.

13. **The transport sits behind one seam.** A typed address over UDP first, with no platform service.
    A platform transport can be added later without touching the rules above. Networking packages are
    named only in the App composition code. Co-op is off unless a session is started from a
    development or demo build; solo play keeps its path and tests.

14. **No host migration.** The host leaving ends the session. A crash returns everyone to the last
    completed sleep.

## Alternatives considered

- **Each machine decides its own catch and money (the trial's shape, extended).** Rejected: two
  machines would each pay for the same pail and each sell the last loaf.
- **Lockstep or rollback from identical simulation.** Rejected: boat physics is not guaranteed
  identical across machines, and the rod fight is deliberately outside the seed-and-time contract.
- **A dedicated server.** Rejected for the demo: running costs, and a different product.
- **One region at a time, crossed together.** Cheaper by the whole of item 7. Set aside by the owner's
  ruling that players may be in different scenes.
- **The guest's machine simulates the region the host is not in.** Would spare the host the second
  region. Rejected: it gives a guest authority over counted state and breaks item 1.
- **A leaving guest's goods are set down in the world.** Set aside by the owner's ruling that they
  vanish and return with him.
- **A separate save on each player's machine, merged.** Rejected: merging goods and money has no
  correct answer and invites duplication.

## Consequences

- The per-player record is the largest Core change the project has taken. It lands in steps, with the
  ownership guard from #1521 measuring progress.
- Two live regions put the host's machine at the worst case for the frame budget (rule 7): a second
  region's objects and textures in memory, and its counted simulation. This must be measured on the
  target laptop before that slice is accepted. If it does not fit, the unseen region is thinned; the
  host's own picture is not.
- Shared goods can leave the world in a departing guest's hands until he returns. This follows the
  ruling and should be said on screen when it happens.
- Solo play must be unchanged at every step. Each slice keeps the solo suite green.
- Determinism (rule 5) becomes load-bearing for play. Hidden randomness in a simulation system would
  show as two players seeing different seas.
- The demo's save gains per-player records in one later step. Old saves keep loading.
- Two packages are added at the first code slice, on a draft PR, merged only with the owner's yes.
- Not decided here: platform accounts and services, more than two live regions, more than four
  players, host migration, and what a joiner sees if his boat bed exists but is under way.

## Proof expected before acceptance of each step

Named in the plan's build order. In short: two requests for one object give one winner; two purchases
with money for one give one debit; a repeated request does nothing twice; a guest's quit writes no
file; a wrong seed or build is refused; the night passes once, only with every player in a separate
bunk; a player in a second region is simulated on the host with the host's frame time recorded; a
leaving player's held things return with him; an old save loads and round-trips; and one run on two
real machines from start to the demo's end.
